# Converts Models/Reactor_01.obj into what World/Reactor.cs reads (same PRP1 format as tools/cc0/pack_props.py):
#   Reactor_01_mesh.bytes  : everything but the plasma blobs, one submesh per OBJ material
#   Reactor_01_blobs.bytes : one submesh per plasma blob (they bob about at runtime)
# The OBJ has no normals: faces are split where they meet at more than 40 degrees and smoothed elsewhere.
# OBJ is right-handed Y-up metres; Unity is left-handed, so x is negated and the winding flipped.
import sys, struct, numpy as np
src, out_dir = sys.argv[1], sys.argv[2]
V = []; objs = []; cur = None
for line in open(src):
    if line.startswith('v '): V.append([float(x) for x in line.split()[1:4]])
    elif line.startswith('o '): cur = {'name': line.split()[1], 'mat': None, 'faces': []}; objs.append(cur)
    elif line.startswith('usemtl'): cur['mat'] = line.split()[1]
    elif line.startswith('f '): cur['faces'].append([int(x.split('/')[0]) - 1 for x in line.split()[1:]])
V = np.array(V); V[:, 0] *= -1
COS = np.cos(np.radians(40))

def shade(faces):
    """Unwelded corners with smoothed normals, as (positions, normals, triangles)."""
    F = np.array([f[:3] for f in faces])[:, [0, 2, 1]]  # flip winding for the x mirror
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
    by_vert = {}
    for fi, f in enumerate(F):
        for v in f: by_vert.setdefault(v, []).append(fi)
    P, N, keys, T = [], [], {}, []
    for fi, f in enumerate(F):
        tri = []
        for v in f:
            near = [g for g in by_vert[v] if fn[g] @ fn[fi] > COS]
            n = fn[near].sum(0); n /= np.linalg.norm(n) + 1e-12
            k = (v, tuple(np.round(n, 3)))
            if k not in keys:
                keys[k] = len(P); P.append(V[v]); N.append(n)
            tri.append(keys[k])
        T.append(tri)
    return np.array(P), np.array(N), np.array(T).ravel()

def write(path, groups):
    P, N, subs, base = [], [], [], 0
    for key, (p, n, t) in groups:
        P.append(p); N.append(n); subs.append((key, t + base)); base += len(p)
    P = np.concatenate(P); N = np.concatenate(N); U = np.zeros((len(P), 2))
    with open(path, 'wb') as f:
        f.write(b'PRP1'); f.write(struct.pack('<i', len(subs)))
        for k, _ in subs:
            b = k.encode(); f.write(struct.pack('<i', len(b))); f.write(b)
        f.write(struct.pack('<i', len(P)))
        for a in (P, N, U): f.write(a.astype('<f4').tobytes())
        for _, t in subs:
            f.write(struct.pack('<i', len(t))); f.write(t.astype('<i4').tobytes())
    print(path, len(P), 'verts', sum(len(t) for _, t in subs) // 3, 'tris', [k for k, _ in subs])

by_mat = {}
blobs = []
for o in objs:
    if o['name'].startswith('Plasma_Blob'): blobs.append((o['mat'], shade(o['faces'])))
    else: by_mat.setdefault(o['mat'], []).extend(o['faces'])
write(out_dir + '/Reactor_01_mesh.bytes', [(m, shade(f)) for m, f in sorted(by_mat.items())])
write(out_dir + '/Reactor_01_blobs.bytes', blobs)
