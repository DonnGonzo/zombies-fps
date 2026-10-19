# Splits a Ten Worlds *_Props.glb (a flat list of part meshes laid out in a 14 m room) into separate props:
# parts whose boxes touch belong to one prop. Lists each kind of prop (named by its first part, e.g. ChurchPew)
# with how many there are and its size, and with --export writes the first of each named kind as a PRP1 mesh
# (see tools/cc0/pack_props.py), in Unity axes, its footprint centred on the origin and its base at y = 0, turned
# so its back (the side that faced the nearest room wall) is -z.
#   python3 split_props.py <room>_Props.glb [--export outdir Kind1,Kind2,...]
import sys, json, struct, re, os, numpy as np
path = sys.argv[1]
SLACK = float(os.environ.get('SLACK', '0.02'))
b = open(path, 'rb').read()
jl = struct.unpack_from('<I', b, 12)[0]
g = json.loads(b[20:20 + jl])
binoff = 20 + jl + 8

def acc(i):
    a = g['accessors'][i]; bv = g['bufferViews'][a['bufferView']]
    comps = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
    dt = {5126: '<f4', 5123: '<u2', 5125: '<u4', 5121: 'u1'}[a['componentType']]
    off = binoff + bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    return np.frombuffer(b, dt, a['count'] * comps, off).reshape(a['count'], comps)

def qmat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def nmat(n):
    if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
    m = np.eye(4); m[:3, :3] = qmat(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); m[:3, 3] = n.get('translation', [0, 0, 0]); return m

parts = []
def walk(i, parent):
    n = g['nodes'][i]; m = parent @ nmat(n)
    if 'mesh' in n:
        for p in g['meshes'][n['mesh']]['primitives']:
            pos = acc(p['attributes']['POSITION']).astype(np.float64)
            pos = (m[:3, :3] @ pos.T).T + m[:3, 3]
            nrm = acc(p['attributes']['NORMAL']).astype(np.float64) if 'NORMAL' in p['attributes'] else None
            if nrm is not None:
                nrm = (np.linalg.inv(m[:3, :3]).T @ nrm.T).T; nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-12
            uv = acc(p['attributes']['TEXCOORD_0']).astype(np.float64) if 'TEXCOORD_0' in p['attributes'] else np.zeros((len(pos), 2))
            idx = acc(p['indices']).reshape(-1).astype(np.int64) if 'indices' in p else np.arange(len(pos))
            mat = g['materials'][p['material']]['name'] if 'material' in p else 'default'
            parts.append({'name': n.get('name', ''), 'pos': pos, 'nrm': nrm, 'uv': uv, 'idx': idx, 'mat': mat, 'lo': pos.min(0), 'hi': pos.max(0)})
    for c in n.get('children', []): walk(c, m)
for r in g['scenes'][g.get('scene', 0)]['nodes']: walk(r, np.eye(4))

# Union parts whose boxes touch (2 cm slack, or SLACK metres from the environment).
par = list(range(len(parts)))
def find(i):
    while par[i] != i: par[i] = par[par[i]]; i = par[i]
    return i
for i in range(len(parts)):
    for j in range(i + 1, len(parts)):
        a, c = parts[i], parts[j]
        if np.all(a['lo'] - SLACK <= c['hi']) and np.all(c['lo'] - SLACK <= a['hi']): par[find(i)] = find(j)
groups = {}
for i in range(len(parts)): groups.setdefault(find(i), []).append(i)

def kind(ps):
    first = parts[min(ps)]['name']
    return re.sub(r'^\d+_', '', re.sub(r'[_\.-]*\d*$', '', first)).split('_')[0] or 'Unnamed'

kinds = {}
for ps in groups.values(): kinds.setdefault(kind(ps), []).append(ps)
for k, inst in sorted(kinds.items()):
    lo = np.min([parts[i]['lo'] for i in inst[0]], 0); hi = np.max([parts[i]['hi'] for i in inst[0]], 0)
    print('%-26s x%-3d parts %-4d size %s centre %s' % (k, len(inst), len(inst[0]), np.round(hi - lo, 2), np.round((hi + lo) / 2, 2)))

if len(sys.argv) > 3 and sys.argv[2] == '--export':
    out = sys.argv[3]; os.makedirs(out, exist_ok=True)
    wanted = sys.argv[4].split(',')
    # CLIP=axis,lo,hi keeps only the parts whose middle lies in that slice of the room (one bay of a long shelf run).
    clip = os.environ.get('CLIP')
    for k in wanted:
        inst = kinds[k][0]
        if clip:
            ax, c0, c1 = clip.split(','); ax = 'xyz'.index(ax)
            inst = [i for i in inst if float(c0) <= (parts[i]['lo'][ax] + parts[i]['hi'][ax]) / 2 <= float(c1)]
        P, N, U, subs, base = [], [], [], {}, 0
        lo = np.min([parts[i]['lo'] for i in inst], 0); hi = np.max([parts[i]['hi'] for i in inst], 0)
        c = (lo + hi) / 2
        # Turn so the side that faced the nearest wall is the back (-z). The room spans about -7..7.
        d = {'+x': 7 - hi[0], '-x': lo[0] + 7, '+z': 7 - hi[2], '-z': lo[2] + 7}
        side = os.environ.get('BACK') or min(d, key=d.get)   # BACK=+z and so on overrides the guess
        yaw = {'-z': 0, '+z': np.pi, '-x': -np.pi / 2, '+x': np.pi / 2}[side]
        R = np.array([[np.cos(yaw), 0, np.sin(yaw)], [0, 1, 0], [-np.sin(yaw), 0, np.cos(yaw)]])
        for i in inst:
            p = parts[i]
            pos = (R @ (p['pos'] - np.array([c[0], lo[1], c[2]])).T).T
            nrm = (R @ p['nrm'].T).T if p['nrm'] is not None else np.zeros_like(pos)
            P.append(pos); N.append(nrm); U.append(p['uv'])
            subs.setdefault(p['mat'], []).append(p['idx'] + base); base += len(pos)
        P = np.concatenate(P); N = np.concatenate(N); U = np.concatenate(U).copy()
        P[:, 0] *= -1; N[:, 0] *= -1; U[:, 1] = 1 - U[:, 1]
        with open(os.path.join(out, k + '.bytes'), 'wb') as f:
            f.write(b'PRP1'); f.write(struct.pack('<i', len(subs)))
            for m in subs:
                e = m.encode(); f.write(struct.pack('<i', len(e))); f.write(e)
            f.write(struct.pack('<i', len(P)))
            for a in (P, N, U): f.write(a.astype('<f4').tobytes())
            for m, ts in subs.items():
                t = np.concatenate(ts).reshape(-1, 3)[:, [0, 2, 1]].reshape(-1)
                f.write(struct.pack('<i', len(t))); f.write(t.astype('<i4').tobytes())
        lo2, hi2 = P.min(0), P.max(0)
        print('EXPORT %s back=%s verts %d min %s max %s mats %s' % (k, side, len(P), np.round(lo2, 2), np.round(hi2, 2), list(subs)))
