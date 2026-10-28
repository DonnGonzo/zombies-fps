"""
Packs the D&D Heroes (2003) Yuan-ti (Ziggertron, Sketchfab, CC-BY-4.0) into the game's own formats:

    python3 -I tools/yuanti/pack_yuanti.py <yuan-ti.glb>

Writes Assets/_Project/Resources/Characters/YuanTi/:
  YuanTi.bytes          skeleton + skinned mesh, "SKN1" (see tools/characters/pack_characters.py), feet at the
                        origin, facing +Z, scaled to HEIGHT metres. Its rest pose is the model's idle pose, not a T-pose.
  YuanTi_clips.bytes    baked animation, "ANM1" (little endian):
                          int clipCount; per clip: string name, float fps, int frameCount, int boneCount,
                          then per frame, per bone (SKN1 order): float3 localPos, float4 localRot  (Unity axes)
  YuanTi_albedo.jpg     colour
The clips keep their places (no root motion): the game moves the body.
"""
import os, struct, sys
import numpy as np
from PIL import Image
import io

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'characters'))
from gltfio import Gltf
import pack_characters as pc

HEIGHT = 2.9
FPS = 30.0
# Game name: glTF animation name.
CLIPS = {
    'idle': 'yuanti_idle', 'slither': 'yuanti_slither', 'run': 'yuanti_run', 'strike': 'yuanti_strike',
    'lunge': 'yuanti_thrustlunge', 'thrust': 'yuanti_thruststandard', 'hit': 'yuanti_reacthit', 'die': 'yuanti_die',
    'roar': 'yuanti_noticedheroes', 'cast': 'yuanti_healingspell', 'toss': 'yuanti_toss', 'uncoil': 'yuanti_uncoil',
    'sleep': 'yuan-ti_sleep',
}


def quat_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def trs(t, q, s):
    M = np.eye(4)
    M[:3, :3] = quat_mat(q) * np.asarray(s)
    M[:3, 3] = t
    return M


def decompose(M):
    t = M[:3, 3].copy()
    sc = np.linalg.norm(M[:3, :3], axis=0)
    q = np.array(pc.mat_to_quat(M[:3, :3] / sc))
    return t, q / np.linalg.norm(q), sc


def slerp(a, b, f):
    d = np.dot(a, b)
    if d < 0:
        b, d = -b, -d
    if d > 0.9995:
        r = a + f * (b - a)
        return r / np.linalg.norm(r)
    th = np.arccos(d)
    return (np.sin((1 - f) * th) * a + np.sin(f * th) * b) / np.sin(th)


def main(path):
    g = Gltf(path)
    j = g.j
    skin = j['skins'][0]
    joints = skin['joints']
    jindex = {n: i for i, n in enumerate(joints)}
    ibm = g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    W = np.array([g.world(n) for n in joints])
    root_parent = g.world(g.parent[joints[0]])

    # The mesh at the nodes' rest pose, in the scene's frame.
    prim = j['meshes'][0]['primitives'][0]
    a = prim['attributes']
    P = g.acc(a['POSITION']); N = g.acc(a['NORMAL']); T = g.acc(a['TEXCOORD_0'])
    J = g.acc(a['JOINTS_0']).astype(int); Wt = g.acc(a['WEIGHTS_0'])
    Wt = Wt / np.maximum(Wt.sum(1, keepdims=True), 1e-9)
    skinM = np.einsum('jab,jbc->jac', W, ibm)
    Ph = np.c_[P, np.ones(len(P))]
    pos = np.zeros((len(P), 3)); nrm = np.zeros((len(P), 3))
    for k in range(4):
        M = skinM[J[:, k]]
        pos += Wt[:, k:k + 1] * np.einsum('nab,nb->na', M, Ph)[:, :3]
        nrm += Wt[:, k:k + 1] * np.einsum('nab,nb->na', M[:, :3, :3], N)
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)

    # Up is the scene's +Y; turn it to face +Z (head forward of the tail) and scale it to HEIGHT, feet on the floor.
    lo, hi = pos.min(0), pos.max(0)
    head = W[[i for i, n in enumerate(joints) if 'Head' in g.name(n)][0]][:3, 3]
    tail = W[[i for i, n in enumerate(joints) if 'tail09' in g.name(n)][0]][:3, 3]
    fwd = head - tail
    fwd[1] = 0
    yaw = np.arctan2(fwd[0], fwd[2])
    c, s_ = np.cos(-yaw), np.sin(-yaw)
    R = np.array([[c, 0, s_], [0, 1, 0], [-s_, 0, c]])
    scale = HEIGHT / (hi[1] - lo[1])
    C = np.eye(4)
    C[:3, :3] = R * scale
    pos = (pos @ R.T) * scale
    nrm = nrm @ R.T
    centre = pos.min(0) + (pos.max(0) - pos.min(0)) / 2
    # Centre on the raised torso in z (the tail trails behind), on the whole body in x.
    upper = pos[:, 1] - pos[:, 1].min() > HEIGHT * 0.5
    shift = np.array([-centre[0], -pos[:, 1].min(), -pos[upper][:, 2].mean()])
    pos += shift
    C[:3, 3] = shift
    P0 = C @ root_parent
    print('facing yaw %.1f deg, scale %.4f, size' % (np.degrees(yaw), scale), np.round(pos.max(0) - pos.min(0), 2))

    def unity(M):
        t, q, sc = decompose(M)
        return (-t[0], t[1], t[2]), (q[0], -q[1], -q[2], q[3]), tuple(sc)

    bones = []
    for i, n in enumerate(joints):
        p = jindex.get(g.parent.get(n), -1)
        M = P0 @ g.local(n) if p < 0 else g.local(n)
        t, q, sc = unity(M)
        bones.append((g.name(n), p, t, q, sc))

    out = [b'SKN1', struct.pack('<i', len(bones))]
    for name, parent, t, q, sc in bones:
        out.append(pc.s(name) + struct.pack('<i3f4f3f', parent, *t, *q, *sc))
    out.append(struct.pack('<i', 1) + pc.s('YuanTi'))
    P2 = pos.copy(); P2[:, 0] *= -1
    N2 = nrm.copy(); N2[:, 0] *= -1
    T2 = T.copy(); T2[:, 1] = 1.0 - T2[:, 1]
    out.append(struct.pack('<i', len(P2)))
    for arr in (P2, N2, T2):
        out.append(arr.astype('<f4').tobytes())
    out.append(J.astype('u1').tobytes())
    out.append(Wt.astype('<f4').tobytes())
    tri = g.acc(prim['indices']).astype(int).reshape(-1, 3)[:, [0, 2, 1]].reshape(-1)
    out.append(struct.pack('<i', len(tri)) + tri.astype('<i4').tobytes())

    folder = os.path.join(pc.OUT, 'YuanTi')
    os.makedirs(folder, exist_ok=True)
    pc.write(os.path.join(folder, 'YuanTi.bytes'), b''.join(out))

    # Clips, sampled at FPS.
    anims = {an['name']: an for an in j['animations']}
    clips = [struct.pack('<i', len(CLIPS))]
    hips = [i for i, n in enumerate(joints) if g.name(n).startswith('Bip01_')][0]

    def chain(locs):
        world = [None] * len(joints)
        for i, n in enumerate(joints):
            p = jindex.get(g.parent.get(n), -1)
            world[i] = locs[i] if p < 0 else world[p] @ locs[i]
        return world

    rest_world = chain([P0 @ g.local(n) if jindex.get(g.parent.get(n), -1) < 0 else g.local(n) for n in joints])
    rest_inv = [np.linalg.inv(M) for M in rest_world]
    Ph = np.c_[pos, np.ones(len(pos))]

    def lowest(world):
        S = np.array([world[i] @ rest_inv[i] for i in range(len(joints))])
        y = np.zeros(len(pos))
        for k in range(4):
            y += Wt[:, k] * np.einsum('nb,nb->n', S[J[:, k], 1, :], Ph)
        return y.min()

    # The clips carry the body ~70 source units higher than the bind pose does; drop every clip by the
    # same amount so the idle's lowest coil rests on the floor, and keep the hips over the bind pose spot.
    lift = None
    base = rest_world[hips][:3, 3].copy()
    for game, src in CLIPS.items():
        an = anims[src]
        chans = {}
        dur = 0.0
        for ch in an['channels']:
            sm = an['samplers'][ch['sampler']]
            ts = g.acc(sm['input'])[:, 0]
            vs = g.acc(sm['output'])
            chans[(ch['target']['node'], ch['target']['path'])] = (ts, vs)
            dur = max(dur, ts[-1])
        frames = max(2, int(round(dur * FPS)) + 1)

        def sample(node, path, t, rest):
            if (node, path) not in chans:
                return rest
            ts, vs = chans[(node, path)]
            if t <= ts[0]:
                return vs[0]
            if t >= ts[-1]:
                return vs[-1]
            k = np.searchsorted(ts, t) - 1
            f = (t - ts[k]) / (ts[k + 1] - ts[k])
            return slerp(vs[k], vs[k + 1], f) if path == 'rotation' else vs[k] + f * (vs[k + 1] - vs[k])

        data = []
        locs_all = []
        for fi in range(frames):
            t = min(dur, fi / FPS)
            locs = []
            for i, n in enumerate(joints):
                node = g.nodes[n]
                tt = np.asarray(sample(n, 'translation', t, node.get('translation', [0, 0, 0])), float)
                qq = np.asarray(sample(n, 'rotation', t, node.get('rotation', [0, 0, 0, 1])), float)
                ss = np.asarray(sample(n, 'scale', t, node.get('scale', [1, 1, 1])), float)
                L = trs(tt, qq, ss)
                p = jindex.get(g.parent.get(n), -1)
                locs.append(P0 @ L if p < 0 else L)
            # No root motion: keep the hips over the bind pose spot (die and lunge keep their travel from frame 0).
            world = chain(locs)
            hp = world[hips][:3, 3]
            if lift is None:
                lift = lowest(world)
                print('clips lifted %.3f m above the bind pose' % lift)
            if fi == 0:
                start = hp.copy()
            ref = start if game in ('die', 'lunge') else hp
            d = np.array([ref[0] - base[0], lift, ref[2] - base[2]])
            parent = jindex.get(g.parent.get(joints[hips]), -1)
            Wp = world[parent] if parent >= 0 else np.eye(4)
            locs[hips] = locs[hips].copy()
            locs[hips][:3, 3] -= np.linalg.inv(Wp[:3, :3]) @ d
            for M in locs:
                t3, q4, _ = unity(M)
                data.append(struct.pack('<3f4f', *t3, *q4))
        clips.append(pc.s(game) + struct.pack('<fii', FPS, frames, len(joints)) + b''.join(data))
        print(f'{game}: {src} {dur:.2f}s')
    pc.write(os.path.join(folder, 'YuanTi_clips.bytes'), b''.join(clips))

    img = j['images'][0]
    bv = j['bufferViews'][img['bufferView']]
    raw = g.blobs[0][bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]
    im = Image.open(io.BytesIO(raw)).convert('RGB')
    print('texture', im.size)
    if max(im.size) > 1024:
        im = im.resize((1024, 1024), Image.LANCZOS)
    pc.save_jpg(im, os.path.join(folder, 'YuanTi_albedo.jpg'), 92, srgb=True)
    pc.folder_meta(folder)


if __name__ == '__main__':
    main(sys.argv[1])
