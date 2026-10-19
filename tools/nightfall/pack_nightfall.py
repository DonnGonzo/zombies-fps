# Packs Ivan's Nightfall AR-01 (Nightfall_AR01_Detailed_Unity_Pack.zip, unzipped) into the game as the Kestrel AR's
# model: Assets/_Project/Resources/Guns/Nightfall (PRP1 meshes with UVs, guns.txt, textures), loaded by
# Weapons/GunArt.cs like the TenWeapons guns. Run from this folder:
#   python3 pack_nightfall.py <unzipped>/Nightfall_AR_Unity     (needs numpy, trimesh and pillow)
#
# The pack is X forward, Y up, Z to its detailed right side. It is mirrored so that detailed side faces the player
# (a view model shows its left side), and the engraved markings' u is flipped so they still read the right way.
# Groups: the carry handle with its peep sight and a raised front post goes in 'irons' (hidden by an optic), the
# magazine in 'mag', the pack's own vertical grip in 'grip' (shown when the foregrip is bought). Longer and taped
# magazines are made from the pack's magazine; optics, suppressor, heavy barrel, hollow-core band and auto sear come
# from tools/tenweapons/build_guns.py, fitted to this rifle.
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tenweapons'))
sys.path.insert(0, os.path.join(HERE, '..', 'characters'))
import build_guns as bg  # noqa: E402
from gltfio import Gltf  # noqa: E402
from pack_characters import meta, folder_meta, TEXTURE_META  # noqa: E402

DST = os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Resources', 'Guns', 'Nightfall')
SLUG = 'Nightfall/nightfall'
LENGTH = .95           # view-model length in metres, as the Kestrel AR's old model
SY = .282              # sight line: the middle of the rear peep (pack metres)
REAR = -.191           # the rear peep's position along the gun
BORE = (.055, .872)    # bore height and muzzle end
TEX = 1024
# Normal maps that actually carry detail (the metals' are flat).
NORMALS = {'Canvas_Wrap', 'Tactical_Polymer', 'Walnut_Distressed'}
DECALS = {'Nightfall_Markings_Engraving', 'Quarantine_Markings_Engraving', 'Receiver_Serial_Engraving'}
GROUP_OF = {'RearSight': 'irons', 'Magazine': 'mag', 'Foregrip': 'grip'}


def outward(V, F):
    """Some of the pack's solids are wound inside out: flip each connected piece whose signed volume is negative."""
    _, ids = np.unique(np.round(V, 5), axis=0, return_inverse=True)
    ids = ids.reshape(-1)
    parent = list(range(ids.max() + 1))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for f in ids[F]:
        for b in f[1:]:
            ra, rb = find(f[0]), find(b)
            if ra != rb:
                parent[rb] = ra
    piece = np.array([find(ids[f[0]]) for f in F])
    F = F.copy()
    for p in np.unique(piece):
        sel = piece == p
        c = V[F[sel]].reshape(-1, 3).mean(0)
        A, B, C = (V[F[sel, k]] - c for k in range(3))
        if np.einsum('ij,ij->i', A, np.cross(B, C)).sum() < 0:
            F[sel] = F[sel][:, [0, 2, 1]]
    return F


def load(src):
    """The pack's parts in build_guns' frame (x = pack Z, y up, z = pack X; counter-clockwise outward): group -> list
    of (key, vertices, faces, uv)."""
    g = Gltf(os.path.join(src, 'Models', 'Nightfall_AR01_HERO.glb'))
    out = {}
    for i, node in enumerate(g.nodes):
        # The pack's solid rear drum sits right on the sight line; a peep ring takes its place.
        if 'mesh' not in node or g.name(i) == 'RearSight__Parkerized_Steel':
            continue
        M = g.world(i)
        for p in g.j['meshes'][node['mesh']]['primitives']:
            mat = g.j['materials'][p['material']]['name']
            V = g.acc(p['attributes']['POSITION'])
            V = V @ M[:3, :3].T + M[:3, 3]
            F = g.acc(p['indices']).astype(np.int64).reshape(-1, 3)
            uv = g.acc(p['attributes']['TEXCOORD_0']).copy()
            uv[:, 1] = 1 - uv[:, 1]
            if mat in DECALS:
                uv[:, 0] = 1 - uv[:, 0]
            # Swapping two axes turns the faces inside out, so flip them back.
            V = V[:, [2, 1, 0]]
            F = F[:, [0, 2, 1]] if mat in DECALS else outward(V, F[:, [0, 2, 1]])
            key = ('NFD_' if mat in DECALS else 'NF_') + mat
            part = g.name(i).split('__')[0]
            out.setdefault(GROUP_OF.get(part, 'body'), []).append((key, V, F, uv))
    return out


def stretched(parts, top, k):
    """The magazine `k` times as long, stretched down from its top."""
    return [(key, np.column_stack([V[:, 0], top - (top - V[:, 1]) * k, V[:, 2]]), F, uv) for key, V, F, uv in parts]


def shifted(parts, d):
    return [(key, V + np.array(d), F, uv) for key, V, F, uv in parts]


def attachments(groups):
    """Every shop attachment, fitted to this rifle."""
    m = bg.Model('nightfall')
    # Irons: a peep ring on the carry handle where the pack's drum was, and a front post raised to the sight line.
    m.g = 'irons'
    m.ring('RearSight_Peep', .013, .004, [0, SY, REAR], 'BlackSteel', segments=20)
    m.box('FrontSight_Post', [.012, SY - .236, .012], [0, (SY + .236) / 2, .677], 'BlackSteel', bevel=0)
    m.box('FrontSight_Dot', [.01, .01, .004], [0, SY - .006, .669], 'SightGlow', bevel=0)
    # Optics stand on a riser bolted to the flat top that the carry handle covered.
    deck = .127
    base = SY - .085
    for group, z, length in (('reddot', -.06, .16), ('scope', -.04, .34)):
        m.g = group
        m.box('Optic_Riser', [.07, base - deck, length], [0, (deck + base) / 2, z], 'BlackSteel', bevel=.004)
    bg.red_dot(m, -.06, base, SY)
    bg.scope(m, -.02, base, SY, .42)
    a = {'supp_len': bg.suppressor(m, BORE[1], BORE[0], .036, .26)}
    a['heavy_ext'] = bg.heavy_barrel(m, .71, BORE[1], BORE[0], .031, .07) - BORE[1]
    bg.ammo_band(m, -.15, .1, .066, .1)
    bg.auto_selector(m, .084, -.012, -.12)
    out = {}
    for g, name, mesh, mat in m.parts:
        out.setdefault(g, []).append((mat, np.asarray(mesh.vertices, float), np.asarray(mesh.faces), None))
    # Bigger magazines from the pack's own: half as long again, or two taped side by side.
    mag = groups['mag']
    top = -.117
    out['extmag'] = stretched(mag, top, 1.45)
    w = .036
    out['fastmag'] = shifted(mag, (-w, 0, 0)) + shifted(mag, (w, -.02, -.01))
    tape = bg.Model('tape')
    tape.g = 'fastmag'
    for y in (-.2, -.3):
        tape.box('Mag_Coupler', [.15, .028, .1], [0, y, .095], 'Warning', bevel=.004)
    out['fastmag'] += [(mat, np.asarray(mesh.vertices, float), np.asarray(mesh.faces), None) for _, _, mesh, mat in tape.parts]
    return out, a


def write_mesh(path, parts, xf):
    """PRP1 like build_guns.write_mesh, keeping each vertex's uv (none for attachments)."""
    groups = {}
    for key, V, F, uv in parts:
        groups.setdefault(key, []).append((xf(V), F[:, [0, 2, 1]], uv if uv is not None else np.zeros((len(V), 2))))
    P, N, U, subs = [], [], [], []
    for key in sorted(groups):
        T = []
        for V, F, uv in groups[key]:
            fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
            fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
            by_vert = {}
            for fi, face in enumerate(F):
                for v in face:
                    by_vert.setdefault(v, []).append(fi)
            seen = {}
            for fi, face in enumerate(F):
                for v in face:
                    near = [q for q in by_vert[v] if fn[q] @ fn[fi] > bg.COS]
                    n = fn[near].sum(0)
                    n /= np.linalg.norm(n) + 1e-12
                    k = (v, tuple(np.round(n, 3)))
                    if k not in seen:
                        seen[k] = len(P)
                        P.append(V[v])
                        N.append(n)
                        U.append(uv[v])
                    T.append(seen[k])
        subs.append((key, np.array(T)))
    P, N, U = np.array(P), np.array(N), np.array(U)
    with open(path, 'wb') as f:
        f.write(b'PRP1')
        f.write(bg.struct.pack('<i', len(subs)))
        for k, _ in subs:
            b = k.encode()
            f.write(bg.struct.pack('<i', len(b)))
            f.write(b)
        f.write(bg.struct.pack('<i', len(P)))
        for arr in (P, N, U):
            f.write(arr.astype('<f4').tobytes())
        for _, t in subs:
            f.write(bg.struct.pack('<i', len(t)))
            f.write(t.astype('<i4').tobytes())
    meta(path, bg.META.split('\n', 2)[2])
    return P


def textures(src):
    for mat in ('Gunmetal_Worn', 'Parkerized_Steel', 'Edge_Burnished', 'Walnut_Distressed', 'Tactical_Polymer', 'Canvas_Wrap', 'Grime_Bronze'):
        im = Image.open(os.path.join(src, 'Textures', mat + '_BaseColor.png')).convert('RGB').resize((TEX, TEX), Image.LANCZOS)
        path = os.path.join(DST, mat + '_albedo.jpg')
        im.save(path, quality=90)
        meta(path, TEXTURE_META.format(srgb=1, type=0))
        if mat in NORMALS:
            im = Image.open(os.path.join(src, 'Textures', mat + '_Normal.png')).convert('RGB').resize((TEX, TEX), Image.LANCZOS)
            path = os.path.join(DST, mat + '_normal.jpg')
            im.save(path, quality=92)
            meta(path, TEXTURE_META.format(srgb=0, type=1))
    for mat in DECALS:
        path = os.path.join(DST, mat + '.png')
        Image.open(os.path.join(src, 'Textures', mat + '.png')).convert('RGBA').save(path, optimize=True)
        meta(path, TEXTURE_META.format(srgb=1, type=0).replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1'))


def main(src):
    os.makedirs(DST, exist_ok=True)
    folder_meta(DST)
    groups = load(src)
    extra, a = attachments(groups)
    for g, parts in extra.items():
        groups.setdefault(g, []).extend(parts)
    allv = np.vstack([V for g in ('body', 'irons', 'mag') for _, V, _, _ in groups[g]])
    s = LENGTH / (allv[:, 2].max() - allv[:, 2].min())

    def xf(v):
        return np.column_stack([-v[:, 0] * s * bg.NARROW, (v[:, 1] - SY) * s + bg.SIGHT_Y, (v[:, 2] - REAR) * s + bg.REAR_Z])

    pts = []
    for g, parts in groups.items():
        P = write_mesh(os.path.join(DST, 'nightfall' + ('' if g == 'body' else '_' + g) + '.bytes'), parts, xf)
        if g in ('body', 'irons', 'mag'):
            pts.append(P)
    P = np.vstack(pts)
    lo, hi = P.min(0), P.max(0)
    mz = xf(np.array([[0.0, BORE[0], BORE[1]]]))[0]
    have = [g for g in bg.GROUPS if g in groups]
    line = ' '.join([SLUG, 'iron'] + ['%.4f' % x for x in list(mz) + list(lo) + list(hi)]
                    + ['%.4f' % (a['supp_len'] * s), '%.4f' % (a['heavy_ext'] * s)] + have)
    path = os.path.join(DST, 'guns.txt')
    with open(path, 'w', newline='\n') as f:
        f.write('# slug builtin muzzleX muzzleY muzzleZ minX minY minZ maxX maxY maxZ suppLen heavyLen groups...  (tools/nightfall/pack_nightfall.py)\n')
        f.write(line + '\n')
    meta(path, bg.META.split('\n', 2)[2])
    textures(src)
    print('scale %.3f  min %s max %s  %s' % (s, np.round(lo, 3), np.round(hi, 3), ' '.join(have)))


if __name__ == '__main__':
    main(sys.argv[1])
