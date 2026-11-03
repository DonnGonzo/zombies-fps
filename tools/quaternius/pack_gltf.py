# Converts Quaternius .gltf models (the Fantasy Props MegaKit, Sci-Fi Essentials Kit and Modular SciFi MegaKit) into
# the PRP1 binary meshes that Core/PropModels.LoadRaw reads (see tools/cc0/pack_props.py), in Unity axes (x is
# negated, the winding flipped, v flipped), keeping the models' own normals and UVs.
#
# Each submesh's key says how to draw it, so the game needs no table per model:
#   Q|albedo|normal|rrggbb|metallic|gloss|flags
# albedo and normal are texture names in Resources/CC0/Quaternius/Textures (made by pack_textures.py), or '-';
# rrggbb tints the colour (the kits' "_Vertex" materials colour each part by vertex colour, so those parts are
# split by colour); flags: L glows (light strips), G glass, S screen. Decal submeshes (alpha-cut stickers) are
# left out. Cloth and banners get back faces too, since the game's shader draws one side only.
#
#   python3 pack_gltf.py <pack prefix> <outdir> [TURN=deg] [FLAT=depth] file.gltf...
# TURN turns the following models about y (Unity degrees) so their front faces +z; FLAT squashes their depth (z,
# after turning) to at most that many metres (wall panels laid on the map's walls). Prints each model's bounds.
import sys, json, struct, os, numpy as np
prefix, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
turn, flat = 0.0, 0.0

def qmat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def nmat(n):
    if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
    m = np.eye(4); m[:3, :3] = qmat(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); m[:3, 3] = n.get('translation', [0, 0, 0]); return m

def tex_name(g, t):
    if not t: return '-'
    uri = g['images'][g['textures'][t['index']]['source']]['uri']
    return prefix + '_' + os.path.splitext(os.path.basename(uri))[0]

def convert(path):
    g = json.load(open(path)); d = os.path.dirname(path)
    bufs = [open(os.path.join(d, b['uri']), 'rb').read() for b in g['buffers']]
    def acc(i):
        a = g['accessors'][i]; bv = g['bufferViews'][a['bufferView']]
        comps = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        dt = {5126: '<f4', 5123: '<u2', 5125: '<u4', 5121: 'u1'}[a['componentType']]
        v = np.frombuffer(bufs[bv['buffer']], dt, a['count'] * comps, bv.get('byteOffset', 0) + a.get('byteOffset', 0)).reshape(a['count'], comps).astype(np.float64)
        if a.get('normalized'): v /= {5123: 65535.0, 5121: 255.0}.get(a['componentType'], 1.0)
        return v
    subs = {}   # key -> list of (pos, nrm, uv, tris)
    def walk(i, parent):
        n = g['nodes'][i]; m = parent @ nmat(n)
        if 'mesh' in n:
            for p in g['meshes'][n['mesh']]['primitives']:
                mat = g['materials'][p['material']] if 'material' in p else {'name': 'default'}
                name = mat.get('name', '')
                if 'Decal' in name and 'Screen' not in name: continue
                pos = acc(p['attributes']['POSITION']); pos = (m[:3, :3] @ pos.T).T + m[:3, 3]
                nrm = acc(p['attributes']['NORMAL']); nrm = (np.linalg.inv(m[:3, :3]).T @ nrm.T).T
                nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-12
                uv = acc(p['attributes']['TEXCOORD_0']) if 'TEXCOORD_0' in p['attributes'] else np.zeros((len(pos), 2))
                idx = acc(p['indices']).reshape(-1, 3).astype(np.int64)
                pbr = mat.get('pbrMetallicRoughness', {})
                base = pbr.get('baseColorFactor', [1, 1, 1, 1])
                emis = mat.get('emissiveFactor', [0, 0, 0])
                flags = ''
                if 'Light' in name or (max(emis) > 0.2 and 'baseColorTexture' not in pbr): flags = 'L'
                elif 'Glass' in name: flags = 'G'
                elif 'Screen' in name: flags = 'S'
                albedo, normal = tex_name(g, pbr.get('baseColorTexture')), tex_name(g, mat.get('normalTexture'))
                metal, gloss = pbr.get('metallicFactor', 1.0), 1 - pbr.get('roughnessFactor', 1.0)
                if albedo != '-': metal, gloss = -1, -1   # from the texture set (pack_textures.py)
                colour = np.array(emis if flags == 'L' and max(emis) > 0.2 else base[:3])
                vertex = 'COLOR_0' in p['attributes'] and name.endswith('_Vertex')
                if vertex:
                    col = acc(p['attributes']['COLOR_0'])[:, :3]
                    # Linear vertex colours, grouped by the first corner's colour, a little rounded.
                    q = np.round(col[idx[:, 0]] * 20) / 20
                    groups = {}
                    for t, c in zip(idx, map(tuple, q)): groups.setdefault(c, []).append(t)
                    parts = [(np.array(c) ** (1 / 2.2), np.array(ts)) for c, ts in groups.items()]
                else:
                    parts = [(colour ** (1 / 2.2) if albedo == '-' else np.ones(3), idx)]
                for c, tris in parts:
                    hexc = '%02x%02x%02x' % tuple(int(round(min(1, max(0, x)) * 255)) for x in c)
                    key = 'Q|%s|%s|%s|%s|%s|%s' % (albedo, normal, hexc, '%.2f' % metal if metal >= 0 else '-', '%.2f' % gloss if gloss >= 0 else '-', flags)
                    subs.setdefault(key, []).append((pos, nrm, uv, tris))
                    if 'Banner' in name or 'Cloth' in name:
                        subs[key].append((pos, -nrm, uv, tris[:, [0, 2, 1]]))
        for c in n.get('children', []): walk(c, m)
    for r in g['scenes'][g.get('scene', 0)]['nodes']: walk(r, np.eye(4))

    P, N, U, T, keys, base = [], [], [], [], [], 0
    for key, chunks in subs.items():
        tl = []
        for pos, nrm, uv, tris in chunks:
            P.append(pos); N.append(nrm); U.append(uv); tl.append(tris + base); base += len(pos)
        keys.append(key); T.append(np.concatenate(tl))
    P = np.concatenate(P); N = np.concatenate(N); U = np.concatenate(U).copy()
    P[:, 0] *= -1; N[:, 0] *= -1; U[:, 1] = 1 - U[:, 1]
    if turn:
        a = np.radians(turn); R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
        P = P @ R.T; N = N @ R.T
    if flat:
        lo, hi = P[:, 2].min(), P[:, 2].max()
        if hi - lo > flat:
            s = flat / (hi - lo)
            P[:, 2] = lo + (P[:, 2] - lo) * s
            N[:, 2] /= s; N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-12
    name = os.path.splitext(os.path.basename(path))[0]
    with open(os.path.join(out, name + '.bytes'), 'wb') as f:
        f.write(b'PRP1'); f.write(struct.pack('<i', len(keys)))
        for k in keys:
            e = k.encode(); f.write(struct.pack('<i', len(e))); f.write(e)
        f.write(struct.pack('<i', len(P)))
        for a in (P, N, U): f.write(a.astype('<f4').tobytes())
        for t in T:
            t = t[:, [0, 2, 1]].reshape(-1)
            f.write(struct.pack('<i', len(t))); f.write(t.astype('<i4').tobytes())
    lo, hi = P.min(0), P.max(0)
    print('%-28s verts %6d subs %2d  min (%.2f, %.2f, %.2f) max (%.2f, %.2f, %.2f)' % (name, len(P), len(keys), *lo, *hi))

for a in sys.argv[3:]:
    if a.startswith('TURN='): turn = float(a[5:])
    elif a.startswith('FLAT='): flat = float(a[5:])
    else: convert(a)
