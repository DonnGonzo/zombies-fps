# Converts Ivan's TenWeapons pack (pack/OBJ, the pack's Unity-ready OBJ + MTL files) into the PRP1 meshes the game
# loads from Assets/_Project/Resources/Guns (see Weapons/GunArt.cs), plus guns.txt with each gun's muzzle, bounds and
# sight. Run from this folder:  python3 pack_guns.py
#
# The pack's guns are chunky and far bigger than life (the pistol is 46 cm long), so each one is scaled to a length
# that suits the view model, made a little narrower, and moved so its sight line sits on GunModels.SightY with the
# back of its sight at RearZ. That's baked into the mesh, so the game only places it. Optic housings and scope tubes
# go in a second mesh (slug_optic) that the game hides while aiming down the sights, as the old scope did.
# Shield pieces keep their own size. OBJ is right-handed; Unity is left-handed, so x is negated and the winding
# flipped, the same as Unity's own OBJ import.
import os, struct, numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'pack', 'OBJ')
DST = os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Resources', 'Guns')

SIGHT_Y = 0.071   # GunModels.SightY
REAR_Z = -0.08    # where the back of the sight ends up
NARROW = 0.75     # width squash: the pack's guns are about twice as wide as real ones

# slug: (OBJ name, length in metres in the game, or None to keep the model's size)
MODELS = {
    'harker9': ('01_Harker9_Service_Pistol', 0.30),
    'sable9': ('02_Sable9_Compact_SMG', 0.62),
    'atlas5': ('03_Atlas5_Tactical_Carbine', 0.86),
    'kodiak7': ('04_Kodiak7_Battle_Rifle', 0.95),
    'riot12': ('05_Riot12_Pump_Shotgun', 0.95),
    'specter308': ('06_Specter308_Sniper_Rifle', 1.1),
    'grinder': ('07_Grinder_Light_Machine_Gun', 1.0),
    'helix': ('09_Helix_Plasma_Carbine', 0.9),
    'pulse': ('10_Resonance_Pulse_Projector', 0.95),
    'shield': ('08_Aegis_Shield_Assembled', None),
    'shield_plate': ('08A_Aegis_Shield_Armor_Plate', None),
    'shield_view': ('08B_Aegis_Shield_View_Module', None),
    'shield_core': ('08C_Aegis_Shield_Power_Core', None),
    'shield_brace': ('08D_Aegis_Shield_Brace_Kits', None),
}
OPTIC = ('Reflex_Housing', 'FrontLens', 'ScopeBody', 'ScopeBell', 'ScopeEyepiece', 'ScopeLens', 'ScopeDial')
MUZZLE = ('Dark_Muzzle_Bore', 'Muzzle_Bore', 'Muzzle_Brake', 'Emitter_Lens', 'Pulse_Mouth', 'Rifled_Game_Barrel')
COS = np.cos(np.radians(40))


def read_obj(path):
    """Parts in file order: (name, material, vertex array, triangles indexing that array)."""
    V, parts = [], []
    for line in open(path):
        if line.startswith('v '):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('o '):
            parts.append([line.split(None, 1)[1].strip(), None, []])
        elif line.startswith('usemtl'):
            parts[-1][1] = line.split()[1]
        elif line.startswith('f '):
            idx = [int(x.split('/')[0]) - 1 for x in line.split()[1:]]
            for k in range(1, len(idx) - 1):
                parts[-1][2].append([idx[0], idx[k], idx[k + 1]])
    V = np.array(V)
    out = []
    for name, mat, faces in parts:
        F = np.array(faces)
        used = np.unique(F)
        remap = {v: i for i, v in enumerate(used)}
        out.append((name.split('_', 1)[1], mat, V[used].copy(), np.vectorize(remap.get)(F)))
    return out


def find(parts, names):
    for want in names:
        for name, _, v, _ in parts:
            if name.startswith(want):
                return v
    return None


def box(v):
    return v.min(0), v.max(0)


def anchors(parts):
    """Sight kind, sight height and back (source units), and the muzzle point."""
    reflex, scope = find(parts, ['Reflex_Housing']), find(parts, ['ScopeBody'])
    if reflex is not None:
        led = find(parts, ['LED_Reticle'])
        lo, hi = box(led if led is not None else reflex)
        kind, y, z = 'reflex', (lo[1] + hi[1]) / 2, box(reflex)[0][2]
    elif scope is not None:
        lo, hi = box(scope)
        eye = find(parts, ['ScopeEyepiece'])
        kind, y, z = 'scope', (lo[1] + hi[1]) / 2, box(eye if eye is not None else scope)[0][2]
    else:
        rear, bead = find(parts, ['Rear_Sight']), find(parts, ['Front_Bead'])
        if rear is not None:
            lo, hi = box(rear)
            kind, y, z = 'iron', hi[1], lo[2]
        elif bead is not None:
            receiver = find(parts, ['Forged_Receiver'])
            kind, y, z = 'iron', box(bead)[1][1], box(receiver)[0][2] if receiver is not None else 0.0
        else:
            # No sight at all (the pulse projector): look over the top of the whole thing.
            display = find(parts, ['Reactor_Display'])
            top = max(box(v)[1][1] for _, _, v, _ in parts)
            kind, y, z = 'none', top + 0.01, box(display)[0][2]
    m = find(parts, MUZZLE)
    lo, hi = box(m)
    top = max(box(v)[1][2] for _, _, v, _ in parts)
    return kind, y, z, np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, top])


def write_mesh(path, parts, xf):
    """One submesh per material; faces split where they meet at more than 40 degrees, smoothed elsewhere."""
    groups = {}
    for _, mat, v, f in parts:
        groups.setdefault(mat, []).append((xf(v), f))
    P, N, subs = [], [], []
    for key in sorted(groups):
        T = []
        for V, F in groups[key]:
            F = F[:, [0, 2, 1]]
            fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
            fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
            by_vert = {}
            for fi, face in enumerate(F):
                for v in face:
                    by_vert.setdefault(v, []).append(fi)
            seen = {}
            for fi, face in enumerate(F):
                for v in face:
                    near = [g for g in by_vert[v] if fn[g] @ fn[fi] > COS]
                    n = fn[near].sum(0)
                    n /= np.linalg.norm(n) + 1e-12
                    k = (v, tuple(np.round(n, 3)))
                    if k not in seen:
                        seen[k] = len(P)
                        P.append(V[v])
                        N.append(n)
                    T.append(seen[k])
        subs.append((key, np.array(T)))
    P, N = np.array(P), np.array(N)
    U = np.zeros((len(P), 2))
    with open(path, 'wb') as f:
        f.write(b'PRP1')
        f.write(struct.pack('<i', len(subs)))
        for k, _ in subs:
            b = k.encode()
            f.write(struct.pack('<i', len(b)))
            f.write(b)
        f.write(struct.pack('<i', len(P)))
        for a in (P, N, U):
            f.write(a.astype('<f4').tobytes())
        for _, t in subs:
            f.write(struct.pack('<i', len(t)))
            f.write(t.astype('<i4').tobytes())
    return P


def main():
    os.makedirs(DST, exist_ok=True)
    info = ['# slug sight muzzleX muzzleY muzzleZ minX minY minZ maxX maxY maxZ scopeFrontZ scopeRadius  (made by tools/tenweapons/pack_guns.py)']
    for slug, (name, length) in MODELS.items():
        parts = read_obj(os.path.join(SRC, name + '.obj'))
        if length is None:
            xf = lambda v: v * np.array([-1.0, 1.0, 1.0])
            kind = None
        else:
            kind, sy, sz, muzzle = anchors(parts)
            allv = np.vstack([v for _, _, v, _ in parts])
            s = length / (allv[:, 2].max() - allv[:, 2].min())
            xf = lambda v, s=s, sy=sy, sz=sz: np.column_stack([-v[:, 0] * s * NARROW, (v[:, 1] - sy) * s + SIGHT_Y, (v[:, 2] - sz) * s + REAR_Z])
        main_parts = [p for p in parts if not (kind and p[0].startswith(OPTIC))]
        optic_parts = [p for p in parts if kind and p[0].startswith(OPTIC)]
        P = write_mesh(os.path.join(DST, slug + '.bytes'), main_parts, xf)
        if optic_parts:
            P = np.vstack([P, write_mesh(os.path.join(DST, slug + '_optic.bytes'), optic_parts, xf)])
        lo, hi = P.min(0), P.max(0)
        print('%-13s %-7s verts %6d  min %s max %s' % (slug, kind or '-', len(P), np.round(lo, 3), np.round(hi, 3)))
        if kind is None:
            continue
        mz = xf(muzzle[None, :])[0]
        lens = find(parts, ['ScopeLens'])
        front, radius = -1.0, 0.0
        if kind == 'scope':
            llo, lhi = box(xf(lens))
            front, radius = llo[2] - 0.002, (lhi[1] - llo[1]) / 2
        info.append(' '.join([slug, kind] + ['%.4f' % x for x in list(mz) + list(lo) + list(hi) + [front, radius]]))
    with open(os.path.join(DST, 'guns.txt'), 'w') as f:
        f.write('\n'.join(info) + '\n')


main()
