# Converts a Poly Haven glTF prop into what Core/PropModels.cs reads:
#   <slug>_mesh.bytes : "PRP1", int submeshes, per submesh a material key (int length + utf8),
#                       int vertices, float3 positions, float3 normals, float2 uvs, then per submesh int count + int indices.
#   <key>_albedo.jpg (diffuse with AO baked in) and <key>_normal.jpg per material.
# Converts glTF (right-handed, Y up, metres) to Unity (left-handed): x negated, winding flipped, v flipped.
import sys, os, json, struct, math
from PIL import Image, ImageChops
import numpy as np

src_dir, dst_root, slug, tex_size = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
gl_path = [f for f in os.listdir(src_dir) if f.endswith('.gltf')][0]
g = json.load(open(os.path.join(src_dir, gl_path)))
bins = [open(os.path.join(src_dir, b['uri']), 'rb').read() for b in g['buffers']]

def accessor(i):
    a = g['accessors'][i]; bv = g['bufferViews'][a['bufferView']]
    comps = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
    dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32, 5121: np.uint8}[a['componentType']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride', 0)
    n = a['count']; isz = np.dtype(dt).itemsize
    if stride and stride != comps * isz:
        raw = np.frombuffer(bins[bv['buffer']], dtype=np.uint8, count=stride * (n - 1) + comps * isz, offset=off)
        out = np.stack([np.frombuffer(raw[k * stride:k * stride + comps * isz].tobytes(), dtype=dt) for k in range(n)])
    else:
        out = np.frombuffer(bins[bv['buffer']], dtype=dt, count=n * comps, offset=off).reshape(n, comps)
    return out

def quat_mat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                     [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def node_mat(n):
    m = np.eye(4)
    if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
    s = np.diag(n.get('scale', [1, 1, 1]))
    r = quat_mat(n.get('rotation', [0, 0, 0, 1]))
    m[:3, :3] = r @ s
    m[:3, 3] = n.get('translation', [0, 0, 0])
    return m

subs = {}  # material index -> list of (pos, nrm, uv, idx)
def walk(ni, parent):
    n = g['nodes'][ni]
    m = parent @ node_mat(n)
    if 'mesh' in n:
        nm = np.linalg.inv(m[:3, :3]).T
        for p in g['meshes'][n['mesh']]['primitives']:
            pos = accessor(p['attributes']['POSITION']).astype(np.float64)
            nrm = accessor(p['attributes']['NORMAL']).astype(np.float64)
            uv = accessor(p['attributes']['TEXCOORD_0']).astype(np.float64)
            idx = accessor(p['indices']).reshape(-1).astype(np.int64)
            pos = (m[:3, :3] @ pos.T).T + m[:3, 3]
            nrm = (nm @ nrm.T).T
            nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-12
            subs.setdefault(p.get('material', 0), []).append((pos, nrm, uv, idx))
    for c in n.get('children', []): walk(c, m)
for root in g['scenes'][g.get('scene', 0)]['nodes']: walk(root, np.eye(4))

P, N, U, groups = [], [], [], []
base = 0
for mi in sorted(subs):
    idxs = []
    for pos, nrm, uv, idx in subs[mi]:
        P.append(pos); N.append(nrm); U.append(uv); idxs.append(idx + base); base += len(pos)
    tri = np.concatenate(idxs).reshape(-1, 3)[:, [0, 2, 1]].reshape(-1)  # flip winding
    groups.append((mi, tri))
P = np.concatenate(P); N = np.concatenate(N); U = np.concatenate(U)
P[:, 0] *= -1; N[:, 0] *= -1; U[:, 1] = 1 - U[:, 1]

os.makedirs(dst_root, exist_ok=True)
keys = []
for mi, _ in groups:
    mat = g['materials'][mi]
    def img(tex):
        return os.path.join(src_dir, g['images'][g['textures'][tex['index']]['source']]['uri'])
    pbr = mat.get('pbrMetallicRoughness', {})
    diff_path = img(pbr['baseColorTexture'])
    key = os.path.basename(diff_path).replace('_diff_1k.jpg', '').replace('_diff_1k.png', '')
    keys.append(key)
    diff = Image.open(diff_path).convert('RGB')
    if 'metallicRoughnessTexture' in pbr:
        ao = Image.open(img(pbr['metallicRoughnessTexture'])).convert('RGB').split()[0].resize(diff.size)
        ao = ao.point(lambda v: int(76.5 + v * 0.7))
        diff = ImageChops.multiply(diff, Image.merge('RGB', (ao, ao, ao)))
    diff.resize((tex_size, tex_size), Image.LANCZOS).save(os.path.join(dst_root, key + '_albedo.jpg'), quality=86, optimize=True)
    if 'normalTexture' in mat:
        Image.open(img(mat['normalTexture'])).convert('RGB').resize((tex_size, tex_size), Image.LANCZOS).save(os.path.join(dst_root, key + '_normal.jpg'), quality=90, optimize=True)

with open(os.path.join(dst_root, slug + '_mesh.bytes'), 'wb') as f:
    f.write(b'PRP1'); f.write(struct.pack('<i', len(groups)))
    for k in keys:
        b = k.encode(); f.write(struct.pack('<i', len(b))); f.write(b)
    f.write(struct.pack('<i', len(P)))
    f.write(P.astype('<f4').tobytes()); f.write(N.astype('<f4').tobytes()); f.write(U.astype('<f4').tobytes())
    for _, tri in groups:
        f.write(struct.pack('<i', len(tri))); f.write(tri.astype('<i4').tobytes())
print(slug, 'verts', len(P), 'tris', sum(len(t) for _, t in groups) // 3, 'keys', keys, 'bounds', P.min(0).round(3), P.max(0).round(3))
