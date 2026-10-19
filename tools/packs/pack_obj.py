# Converts an OBJ (trimesh export: materials via usemtl, or a colour per vertex) into the PRP1 binary mesh that
# Core/PropModels.LoadRaw reads (see tools/cc0/pack_props.py). One submesh per material, keyed by its name, or
# per vertex colour, keyed "#rrggbb". The OBJ has no normals: faces are split where they meet at more than
# 40 degrees and smoothed elsewhere. OBJ is right-handed Y-up metres; Unity is left-handed, so x is negated and
# the winding flipped. Prints the model's bounds (Unity axes) for the size table in ZoneMap.Rooms.cs.
import sys, struct, numpy as np
src, dst = sys.argv[1], sys.argv[2]
V, C, groups, mat = [], [], {}, None
for line in open(src):
    if line.startswith('v '):
        p = line.split()[1:]
        V.append([float(x) for x in p[:3]])
        C.append([float(x) for x in p[3:6]] if len(p) >= 6 else None)
    elif line.startswith('usemtl'): mat = line.split()[1]
    elif line.startswith('f '):
        idx = [int(x.split('/')[0]) - 1 for x in line.split()[1:]]
        for k in range(1, len(idx) - 1):
            tri = [idx[0], idx[k], idx[k + 1]]
            if mat: key = mat
            else:
                c = C[tri[0]] or [0.5, 0.5, 0.5]
                key = '#%02x%02x%02x' % tuple(int(round(min(1, max(0, x)) * 255)) for x in c)
            groups.setdefault(key, []).append(tri)
V = np.array(V); V[:, 0] *= -1
COS = np.cos(np.radians(40))
P, N, subs = [], [], []
for key, faces in sorted(groups.items()):
    F = np.array(faces)[:, [0, 2, 1]]
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
    by_vert = {}
    for fi, f in enumerate(F):
        for v in f: by_vert.setdefault(v, []).append(fi)
    seen, T = {}, []
    for fi, f in enumerate(F):
        for v in f:
            near = [g for g in by_vert[v] if fn[g] @ fn[fi] > COS]
            n = fn[near].sum(0); n /= np.linalg.norm(n) + 1e-12
            k = (v, tuple(np.round(n, 3)))
            if k not in seen:
                seen[k] = len(P); P.append(V[v]); N.append(n)
            T.append(seen[k])
    subs.append((key, np.array(T)))
P = np.array(P); N = np.array(N); U = np.zeros((len(P), 2))
with open(dst, 'wb') as f:
    f.write(b'PRP1'); f.write(struct.pack('<i', len(subs)))
    for k, _ in subs:
        b = k.encode(); f.write(struct.pack('<i', len(b))); f.write(b)
    f.write(struct.pack('<i', len(P)))
    for a in (P, N, U): f.write(a.astype('<f4').tobytes())
    for _, t in subs:
        f.write(struct.pack('<i', len(t))); f.write(t.astype('<i4').tobytes())
lo, hi = P.min(0), P.max(0)
print('%-28s verts %6d tris %6d  min %s max %s  keys %s' % (src.rsplit('/', 1)[-1], len(P), sum(len(t) for _, t in subs) // 3,
      np.round(lo, 2), np.round(hi, 2), [k for k, _ in subs]))
