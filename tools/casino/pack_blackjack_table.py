# Packs Ivan's blackjack table (made by him with Hunyuan3D-2: textured_mesh.obj + material_0.png, a trimesh export)
# into the PRP1 binary mesh that Core/PropModels.LoadRaw reads, keeping the UVs and normals pack_obj.py drops.
# OBJ is right-handed Y-up; Unity is left-handed, so x is negated and the winding flipped. The model is scaled so the
# chair backs stand HEIGHT metres tall and its origin moved to the middle of its base. The open side (no chair) is +z.
#   python tools/casino/pack_blackjack_table.py <folder with textured_mesh.obj and material_0.png>
import sys, struct, os, numpy as np
from PIL import Image
HEIGHT = 1.15
src = sys.argv[1]
out = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'CC0', 'Casino')
V, T, N, F = [], [], [], []
for line in open(os.path.join(src, 'textured_mesh.obj')):
    p = line.split()
    if not p: continue
    if p[0] == 'v': V.append([float(x) for x in p[1:4]])
    elif p[0] == 'vt': T.append([float(x) for x in p[1:3]])
    elif p[0] == 'vn': N.append([float(x) for x in p[1:4]])
    elif p[0] == 'f':
        idx = [[int(x) - 1 for x in q.split('/')] for q in p[1:]]
        for k in range(1, len(idx) - 1): F.append([idx[0], idx[k + 1], idx[k]])
V, T, N = np.array(V), np.array(T), np.array(N)
seen, P, U, NN, tris = {}, [], [], [], []
for f in F:
    for v in f:
        key = tuple(v)
        if key not in seen:
            seen[key] = len(P); P.append(V[v[0]]); U.append(T[v[1]]); NN.append(N[v[2]] if len(v) > 2 else [0, 0, 0])
        tris.append(seen[key])
P, U, NN = np.array(P), np.array(U), np.array(NN)
P[:, 0] *= -1; NN[:, 0] *= -1
lo, hi = P.min(0), P.max(0)
s = HEIGHT / (hi[1] - lo[1])
P = (P - [(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2]) * s
with open(os.path.join(out, 'blackjack_table.bytes'), 'wb') as f:
    key = b'blackjack_table'
    f.write(b'PRP1'); f.write(struct.pack('<i', 1)); f.write(struct.pack('<i', len(key))); f.write(key)
    f.write(struct.pack('<i', len(P)))
    for a in (P, NN, U): f.write(a.astype('<f4').tobytes())
    t = np.array(tris); f.write(struct.pack('<i', len(t))); f.write(t.astype('<i4').tobytes())
Image.open(os.path.join(src, 'material_0.png')).convert('RGB').save(os.path.join(out, 'blackjack_table_albedo.jpg'), quality=90)
print('verts %d tris %d size %s' % (len(P), len(tris) // 3, np.round(P.max(0) - P.min(0), 3)))
