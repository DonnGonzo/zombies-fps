# Builds the game's guns, their attachments and the Aegis Shield from Ivan's TenWeapons pack designs, straight
# into Assets/_Project/Resources/Guns (PRP1 meshes + guns.txt, loaded by Weapons/GunArt.cs). Run from this folder:
#   python3 build_guns.py          (needs numpy and trimesh)
#
# The shapes, sizes and materials follow pack/generate_models.py, but the pack's parts don't meet: stocks, grips,
# rails and sights float a few centimetres off the gun. Here every part is placed to touch the one it hangs on,
# and each gun gets things the game needs: notched iron sights with a glowing front post, and a set of
# attachments built to fit it (red dot, scope, extended and fast mags, foregrip, suppressor, heavy barrel,
# hollow-core rounds, full-auto selector), each in its own mesh so the game can show what's been bought.
#
# Pack units are the pack's (oversized) metres. Each gun is scaled to a view-model length, made 25% narrower and
# moved so its sight line sits on GunModels.SightY (0.071) with the back of its sight at REAR_Z, all baked in.
# OBJ/trimesh are right-handed and Unity left-handed, so x is negated and the winding flipped.
import math, os, struct, uuid
import numpy as np
import trimesh
from trimesh.transformations import rotation_matrix

HERE = os.path.dirname(os.path.abspath(__file__))
DST = os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Resources', 'Guns')
SIGHT_Y = 0.071
REAR_Z = -0.08
NARROW = 0.75
COS = np.cos(np.radians(40))


# ------------------------------------------------------------------ building blocks (after the pack's generator)

def rounded_box(ext, bevel=.03):
    """Box with its long edges chamfered (octagonal cross-section in xy, extruded along z)."""
    x, y, z = np.array(ext) / 2
    b = min(bevel, .49 * min(ext))
    pts = [(-x + b, -y), (x - b, -y), (x, -y + b), (x, y - b), (x - b, y), (-x + b, y), (-x, y - b), (-x, -y + b)]
    vv = np.array([[a, c, zz] for zz in [-z, z] for a, c in pts], dtype=float)
    faces = []
    for i in range(8):
        j = (i + 1) % 8
        faces.extend([[i, j, 8 + j], [i, 8 + j, 8 + i]])
    for i in range(1, 7):
        faces.append([0, i, i + 1])
        faces.append([8, 8 + i + 1, 8 + i])
    return trimesh.Trimesh(vertices=vv, faces=faces, process=False)


class Model:
    """Parts tagged with a group (body, irons, mag, or an attachment); `g` is the group new parts go in."""

    def __init__(self, name):
        self.name, self.parts, self.g = name, [], 'body'

    def add(self, name, mesh, mat):
        self.parts.append((self.g, name, mesh.copy(), mat))

    def box(self, name, ext, pos, mat, rot=None, bevel=.008):
        mesh = rounded_box(ext, bevel) if bevel else trimesh.creation.box(extents=ext)
        if rot:
            mesh.apply_transform(rotation_matrix(rot[0], rot[1]))
        mesh.apply_translation(pos)
        self.add(name, mesh, mat)

    def rod(self, name, a, b, r, mat, sections=12):
        a, b = np.array(a, float), np.array(b, float)
        v = b - a
        l = float(np.linalg.norm(v))
        m = trimesh.creation.cylinder(radius=r, height=l, sections=sections)
        m.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], v / l))
        m.apply_translation((a + b) / 2)
        self.add(name, m, mat)

    def cyl(self, name, r, length, pos, mat, axis='z', sections=18):
        m = trimesh.creation.cylinder(radius=r, height=length, sections=sections)
        if axis == 'y':
            m.apply_transform(rotation_matrix(-math.pi / 2, [1, 0, 0]))
        if axis == 'x':
            m.apply_transform(rotation_matrix(math.pi / 2, [0, 1, 0]))
        m.apply_translation(pos)
        self.add(name, m, mat)

    def ring(self, name, R, r, pos, mat, segments=24, sides=6):
        verts, faces = [], []
        for i in range(segments):
            t = i * 2 * math.pi / segments
            for j in range(sides):
                q = j * 2 * math.pi / sides
                mr = R + r * math.cos(q)
                verts.append([mr * math.cos(t), mr * math.sin(t), r * math.sin(q)])
        for i in range(segments):
            for j in range(sides):
                a = i * sides + j
                b = ((i + 1) % segments) * sides + j
                c = ((i + 1) % segments) * sides + (j + 1) % sides
                d = i * sides + (j + 1) % sides
                faces.extend([[a, b, c], [a, c, d]])
        m = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
        m.apply_translation(pos)
        self.add(name, m, mat)

    def tube(self, name, r_out, r_in, length, pos, mat, sections=20):
        """An open-ended pipe along z (a suppressor's or scope's shell you can see into)."""
        ring = trimesh.creation.annulus(r_min=r_in, r_max=r_out, height=length, sections=sections)
        ring.apply_translation(pos)
        self.add(name, ring, mat)

    def rail(self, z1, z2, y, width=.17, teeth=9):
        """Picatinny rail whose base sits at y; returns the height of its top (where optics mount)."""
        self.box('Top_Picatinny', [width, .026, z2 - z1], [0, y + .013, (z1 + z2) / 2], 'BlackSteel')
        for j in range(teeth):
            zz = z1 + (j + .5) * (z2 - z1) / teeth
            self.box(f'Rail_Slot_{j}', [width + .025, .018, .017], [0, y + .031, zz], 'BrushedMetal')
        return y + .04

    def screws(self, locs, mat='Brass', r=.012):
        for i, p in enumerate(locs):
            self.cyl(f'HexScrew_{i}', r, .01, p, mat, axis='x', sections=8)

    def ribbed_grip(self, top, sz, mat='Polymer', ribs=6, tilt=.18):
        """A pistol grip raked back by `tilt` whose top middle is at `top` (so it meets the receiver above)."""
        ty, tz = top[1], top[2]
        # Rotating about x by +tilt swings the bottom forward; the pack's grips rake backwards the same way.
        cy = ty - sz[1] / 2 * math.cos(tilt)
        cz = tz - sz[1] / 2 * math.sin(tilt)
        p = (top[0], cy, cz)
        self.box('Angled_Grip', sz, p, mat, rot=(tilt, [1, 0, 0]), bevel=.035)
        for i in range(ribs):
            d = -sz[1] * .39 + i * (sz[1] * .78 / max(1, ribs - 1))
            self.box(f'Grip_Stipple_{i}', [sz[0] + .005, .008, sz[2] * .86], [p[0], cy + d * math.cos(tilt), cz + d * math.sin(tilt)], 'Rubber',
                     rot=(tilt, [1, 0, 0]))

    def magazine(self, top, ext, mat='Polymer', ridges=6, tilt=.08, x=0.0):
        """A box magazine hanging from `top` (its top middle), raked forward by `tilt`."""
        cy = top[1] - ext[1] / 2
        cz = top[2] + ext[1] / 2 * math.sin(tilt)
        self.box('Magazine', ext, [x, cy, cz], mat, rot=(tilt, [1, 0, 0]), bevel=.025)
        for i in range(ridges):
            y = cy - (i - ridges / 2) * .017
            self.box(f'Magazine_Groove_{i}', [ext[0] + .008, .005, ext[2] * .85], [x, y, cz], 'Rubber')
        bottom = top[1] - ext[1]
        self.box('Magazine_BasePlate', [ext[0] + .035, .017, ext[2] + .02], [x, bottom - .006, cz + ext[1] / 2 * math.sin(tilt)], 'BrushedMetal')
        return bottom


# ------------------------------------------------------------------ sights and attachments, fitted per gun

def irons(m, rear_z, front_z, base_y, sy, front_base_y=None):
    """Notch rear sight and a hooded front post with a glowing dot, both topping out on the sight line `sy`."""
    m.g = 'irons'
    fb = base_y if front_base_y is None else front_base_y
    m.box('RearSight_Base', [.11, sy - .035 - base_y, .045], [0, (base_y + sy - .035) / 2, rear_z], 'BlackSteel', bevel=.006)
    for x in (-.034, .034):
        m.box('RearSight_Ear', [.034, .035, .03], [x, sy - .0175, rear_z], 'BlackSteel', bevel=.004)
    m.box('FrontSight_Base', [.09, .03, .045], [0, fb + .015, front_z], 'BlackSteel', bevel=.006)
    post = sy - fb - .03
    m.box('FrontSight_Post', [.016, post, .02], [0, fb + .03 + post / 2, front_z], 'BlackSteel', bevel=0)
    for x in (-.04, .04):
        m.box('FrontSight_Hood', [.012, post, .03], [x, fb + .03 + post / 2, front_z], 'BlackSteel', bevel=0)
    m.box('FrontSight_Dot', [.014, .014, .006], [0, sy - .009, front_z - .013], 'SightGlow', bevel=0)
    m.g = 'body'


def red_dot(m, z, base_y, sy):
    """An open reflex sight: a frame round a clear pane, with the dot on the sight line in the middle of it."""
    m.g = 'reddot'
    m.box('RedDot_Mount', [.12, .02, .16], [0, base_y + .01, z], 'Gunmetal', bevel=.005)
    bottom = base_y + .02
    top = 2 * sy - bottom
    h = top - bottom
    m.box('RedDot_Base', [.15, .025, .12], [0, bottom + .0125, z], 'BlackSteel', bevel=.006)
    for x in (-.068, .068):
        m.box('RedDot_Side', [.016, h + .02, .1], [x, bottom + h / 2 + .01, z], 'BlackSteel', bevel=.004)
    m.box('RedDot_Top', [.152, .02, .1], [0, top + .02, z], 'BlackSteel', bevel=.004)
    m.box('RedDot_Pane', [.12, h, .004], [0, bottom + .025 + (h - .025) / 2, z + .03], 'OpticGlass', bevel=0)
    m.box('RedDot_Dot', [.008, .008, .004], [0, sy, z + .028], 'EmissiveRed', bevel=0)
    m.box('RedDot_Switch', [.02, .02, .03], [.08, bottom + .03, z - .02], 'Rubber', bevel=0)
    m.g = 'body'


def scope(m, z, base_y, sy, length=.5, group='scope'):
    """A rifle scope on two rings, its axis on the sight line. The game hides it and draws the scope view."""
    m.g = group
    r = min(.06, sy - base_y - .012)
    for oz in (z - .14, z + .14):
        m.box('Scope_Ring_Base', [.08, sy - base_y - r + .01, .045], [0, base_y + (sy - base_y - r + .01) / 2, oz], 'BrushedMetal', bevel=.004)
        m.ring('Scope_Ring', r + .008, .009, [0, sy, oz], 'BrushedMetal', segments=20)
    m.cyl('Scope_Tube', r, length, [0, sy, z], 'BlackSteel', sections=20)
    m.cyl('Scope_Bell', r * 1.45, .1, [0, sy, z + length / 2], 'Gunmetal', sections=20)
    m.cyl('Scope_Bell_Taper', r * 1.2, .06, [0, sy, z + length / 2 - .07], 'Gunmetal', sections=20)
    m.cyl('Scope_Eyepiece', r * 1.25, .11, [0, sy, z - length / 2 - .03], 'Rubber', sections=20)
    m.cyl('Scope_Lens', r * 1.3, .004, [0, sy, z + length / 2 + .052], 'Optics', sections=20)
    m.cyl('Scope_Turret', r * .55, .05, [0, sy + r + .02, z], 'BrushedMetal', axis='y', sections=12)
    m.cyl('Scope_Windage', r * .55, .05, [r + .02, sy, z], 'BrushedMetal', axis='x', sections=12)
    m.g = 'body'
    return z - length / 2 - .085, z + length / 2 + .05


def suppressor(m, z, y, r, length=.3):
    m.g = 'supp'
    m.cyl('Suppressor_Mount', r * .8, .04, [0, y, z + .02], 'BlackSteel')
    m.cyl('Suppressor', r, length, [0, y, z + .04 + length / 2], 'BlackSteel', sections=20)
    for i in range(5):
        m.ring(f'Suppressor_Ring_{i}', r, .004, [0, y, z + .07 + i * (length - .06) / 4], 'Gunmetal', segments=20)
    m.cyl('Suppressor_Bore', r * .3, .004, [0, y, z + .04 + length + .001], 'Rubber')
    m.g = 'body'
    return .04 + length


def heavy_barrel(m, z0, z1, y, r, ext=.08):
    """A thicker barrel sleeve with cooling rings, and a compensator stretching the muzzle by `ext`."""
    m.g = 'heavy'
    m.cyl('Heavy_Sleeve', r, z1 - z0, [0, y, (z0 + z1) / 2], 'Gunmetal', sections=20)
    n = max(2, int((z1 - z0) / .06))
    for i in range(n):
        m.ring(f'Heavy_Fin_{i}', r, .006, [0, y, z0 + .03 + i * (z1 - z0 - .06) / max(1, n - 1)], 'BlackSteel', segments=20)
    m.cyl('Compensator', r * .9, ext, [0, y, z1 + ext / 2], 'BlackSteel', sections=16)
    for x in (-1, 1):
        m.box('Compensator_Port', [.012, r * .9, ext * .5], [x * r * .82, y, z1 + ext / 2], 'Rubber', bevel=0)
    m.g = 'body'
    return z1 + ext


def foregrip(m, y, z, mat='Rubber'):
    m.g = 'grip'
    m.box('Foregrip_Clamp', [.09, .03, .1], [0, y - .015, z], 'BlackSteel', bevel=.006)
    m.box('Foregrip', [.07, .17, .075], [0, y - .03 - .085, z], mat, bevel=.025)
    for i in range(4):
        m.box(f'Foregrip_Rib_{i}', [.075, .008, .06], [0, y - .06 - i * .035, z], 'BlackSteel', bevel=0)
    m.g = 'body'


def box_mags(m, top, ext, mat='Polymer', ridges=6, tilt=.08):
    """The standard magazine, a longer one, and two taped side by side for fast reloads."""
    m.g = 'mag'
    m.magazine(top, ext, mat, ridges, tilt)
    m.g = 'extmag'
    m.magazine(top, (ext[0], ext[1] * 1.5, ext[2]), mat, int(ridges * 1.5), tilt)
    m.g = 'fastmag'
    off = ext[0] * .55
    m.magazine(top, ext, mat, ridges, tilt, x=-off)
    m.magazine((top[0], top[1] - .03, top[2] - ext[2] * .1), ext, mat, ridges, tilt, x=off)
    cz = top[2] + ext[1] * .3 * math.sin(tilt)
    for yy in (top[1] - ext[1] * .3, top[1] - ext[1] * .7):
        m.box('Mag_Coupler', [ext[0] * 2.2, .03, ext[2] * .9], [0, yy, cz], 'Warning', bevel=.004)
    m.g = 'body'


def ammo_band(m, y, z, w, d):
    """Hollow-core rounds: red-tipped rounds peeking out of a window, and a red band."""
    m.g = 'ammo'
    m.box('HollowCore_Band', [w + .01, .025, d + .01], [0, y, z], 'RedDetail', bevel=.004)
    m.g = 'body'


def auto_selector(m, x, y, z):
    """Full-auto conversion: a bright selector lever and sear block on the side of the receiver."""
    m.g = 'auto'
    m.box('Auto_Sear', [.012, .05, .07], [x + .006, y, z], 'Warning', bevel=.003)
    m.box('Auto_Lever', [.01, .015, .06], [x + .014, y + .02, z + .02], 'RedDetail', rot=(.5, [1, 0, 0]), bevel=0)
    m.g = 'body'


# ------------------------------------------------------------------ the guns

def gun_base(name, main='Gunmetal', stock='Polymer', g=1.0):
    """The shared receiver for the SMG, carbine and battle rifle. Returns the model and its anchors."""
    m = Model(name)
    m.box('Machined_Upper_Receiver', [.19, .16, .36 * g], [0, .034, 0], main, bevel=.035)
    m.box('Lower_Receiver', [.157, .125, .32 * g], [0, -.087, -.004 * g], 'BlackSteel', bevel=.025)
    m.box('Bolt_Cover', [.016, .058, .155 * g], [.103, .065, .05 * g], 'BrushedMetal')
    m.box('Ejection_Port', [.012, .037, .088 * g], [.114, .06, .075 * g], 'Rubber')
    m.cyl('Selector_Switch', .032, .018, [.104, -.016, -.078 * g], 'Brass', 'x', sections=12)
    # Trigger guard: a bar under the trigger, joined to the receiver at the front and the grip at the back.
    m.box('Trigger_Guard', [.07, .025, .17 * g], [0, -.2, -.01 * g], 'BlackSteel', bevel=.006)
    m.box('Trigger_Guard_Front', [.04, .06, .025], [0, -.17, .075 * g], 'BlackSteel', bevel=.006)
    m.rod('Trigger', [0, -.149, .0], [0, -.19, .02 * g], .01, 'BrushedMetal')
    m.ribbed_grip((0, -.145, -.12 * g), (.13, .25, .12 * g), stock, tilt=.24)
    m.cyl('Main_Barrel', .038, .43 * g, [0, 0, .26 * g], 'BlackSteel')
    m.cyl('Outer_Barrel_Shroud', .065, .33 * g, [0, 0, .25 * g], main, sections=14)
    for i in range(7):
        m.box(f'Cooling_Vent_{i}', [.012, .025, .018 * g], [.071, 0, .115 * g + i * .038 * g], 'BlackSteel')
    m.cyl('Muzzle_Shroud', .064, .11, [0, 0, .5 * g], 'BlackSteel')
    m.cyl('Dark_Muzzle_Bore', .04, .006, [0, 0, .5 * g + .056], 'Rubber')
    for x in (-1, 1):
        m.box('Compensator_Port', [.015, .016, .042], [x * .075, 0, .5 * g], 'BrushedMetal')
    # The rail runs the full length: a riser joins its front half to the barrel shroud.
    rail_top = m.rail(-.1 * g, .38 * g, .114, width=.18)
    m.box('Rail_Riser', [.1, .062, .2 * g], [0, .083, .28 * g], main, bevel=.01)
    m.screws([(.096, .049, -.12 * g), (.096, .049, .13 * g), (-.096, .049, -.12 * g), (-.096, .049, .13 * g)], 'Brass')
    m.box('Serial_Plate', [.006, .045, .16], [.0965, -.016, .025], 'BrushedMetal', bevel=0)
    sy = rail_top + .085
    a = dict(sy=sy, rear=-.08 * g, base=rail_top, optic_z=.04 * g, muzzle=(0, .5 * g + .059), bore_r=.064,
             under=(-.065, .3 * g), mag_top=(0, -.12, .092 * g), side=(.0785, -.09, -.02 * g))
    irons(m, a['rear'], .34 * g, rail_top, sy)
    box_mags(m, a['mag_top'], (.136, .30, .098 * g))
    ammo_band(m, -.2, .092 * g + .012, .136, .098 * g)
    auto_selector(m, .0785, -.09, -.06 * g)
    return m, a


def finish_rifle_attachments(m, a, g, heavy_from):
    red_dot(m, a['optic_z'], a['base'], a['sy'])
    scope(m, a['optic_z'] + .04, a['base'], a['sy'], .46 * min(1.0, g))
    foregrip(m, *a['under'])
    a['heavy_ext'] = heavy_barrel(m, heavy_from, a['muzzle'][1] - .115, a['muzzle'][0], .078) - (a['muzzle'][1] - .115)
    a['supp_len'] = suppressor(m, a['muzzle'][1], a['muzzle'][0], .066)


def pistol():
    m = Model('pistol')
    m.box('Polymer_Frame', [.157, .106, .28], [0, -.036, 0], 'Polymer', bevel=.025)
    m.box('Metal_Slide', [.158, .125, .38], [0, .075, .035], 'Gunmetal', bevel=.03)
    m.cyl('Barrel', .019, .355, [0, .042, .08], 'BlackSteel')
    m.cyl('Muzzle_Bore', .016, .007, [0, .042, .2275], 'Rubber')
    for i in range(5):
        m.box(f'Rear_Serration_{i}', [.005, .096, .009], [.079, .07, -.12 + i * .02], 'BrushedMetal')
    m.box('Ejection_Port', [.007, .043, .086], [.082, .088, .055], 'BlackSteel')
    GL = .24
    m.ribbed_grip((0, -.075, -.06), (.133, GL, .15), 'Polymer', 5, tilt=.16)
    m.box('Trigger_Guard', [.07, .022, .13], [0, -.15, .075], 'BlackSteel', bevel=.006)
    m.box('Trigger_Guard_Front', [.04, .07, .022], [0, -.12, .135], 'BlackSteel', bevel=.006)
    m.rod('Trigger', [0, -.089, .046], [0, -.14, .075], .008, 'BrushedMetal')
    m.cyl('Release', .018, .012, [.071, -.11, -.03], 'Brass', 'x')
    m.box('Marking', [.09, .006, .023], [0, .14, .05], 'Warning', bevel=0)
    top = .1375
    sy = top + .055
    a = dict(sy=sy, rear=-.12, base=top, optic_z=-.03, muzzle=(.042, .2575), bore_r=.019, side=(.0785, -.05, .0))
    irons(m, -.12, .19, top, sy)
    # Magazine: the base plate under the grip, longer for an extended mag, a flared magwell for fast reloads.
    grip_bottom_y = -.075 - GL * math.cos(.16)
    grip_bottom_z = -.06 - GL * math.sin(.16)
    m.g = 'mag'
    m.box('Magazine_Base', [.14, .023, .16], [0, grip_bottom_y - .006, grip_bottom_z], 'Rubber')
    m.g = 'extmag'
    m.box('Magazine_Extension', [.124, .09, .15], [0, grip_bottom_y - .04, grip_bottom_z - .006], 'Polymer', bevel=.02)
    m.box('Magazine_Base', [.14, .023, .16], [0, grip_bottom_y - .09, grip_bottom_z - .012], 'Rubber')
    m.g = 'fastmag'
    m.box('Magwell_Flare', [.17, .05, .19], [0, grip_bottom_y + .01, grip_bottom_z], 'Brass', bevel=.012)
    m.box('Magazine_Base', [.14, .023, .16], [0, grip_bottom_y - .02, grip_bottom_z], 'Rubber')
    m.g = 'body'
    ammo_band(m, grip_bottom_y + .05, grip_bottom_z + .01, .135, .14)
    auto_selector(m, .0785, -.05, -.06)
    red_dot(m, -.02, top, sy)
    scope(m, .02, top, sy, .3)
    # Underbarrel on a pistol: a weapon light hung from the slide's front.
    m.g = 'grip'
    m.box('Weapon_Light', [.075, .065, .11], [0, -.02, .175], 'BlackSteel', bevel=.012)
    m.cyl('Light_Lens', .025, .005, [0, -.025, .232], 'EmissiveAmber')
    m.g = 'body'
    m.g = 'heavy'
    m.cyl('Compensator', .034, .07, [0, .042, .2575 + .035], 'Gunmetal', sections=16)
    for x in (-1, 1):
        m.box('Compensator_Port', [.012, .03, .035], [x * .03, .052, .2575 + .035], 'Rubber', bevel=0)
    m.g = 'body'
    a['heavy_ext'] = .07
    a['supp_len'] = suppressor(m, .2575, .042, .036, .2)
    return m, a


def smg():
    g = .78
    m, a = gun_base('smg', main='BlackSteel', g=g)
    for x in (-.06, .06):
        m.rod(f'Folding_Stock_Rail_{x}', [x, .03, -.12], [x, -.02, -.46], .012, 'BrushedMetal')
    m.box('Folding_Stock_Butt', [.15, .21, .11], [0, -.06, -.48], 'Rubber', bevel=.02)
    finish_rifle_attachments(m, a, g, .1)
    return m, a


def carbine():
    g = 1.1
    m, a = gun_base('carbine', main='OliveCerakote', g=g)
    m.cyl('Buffer_Tube', .033, .33, [0, 0, -.355], 'BlackSteel')
    m.box('Adjustable_Stock', [.19, .19, .23], [0, -.015, -.58], 'OliveCerakote', bevel=.04)
    m.box('Recoil_Pad', [.2, .22, .026], [0, -.015, -.705], 'Rubber')
    m.box('Cheek_Riser', [.14, .048, .14], [0, .1, -.57], 'Rubber')
    m.box('Laser_Pod', [.065, .055, .15], [.085, .035, .24], 'BlackSteel')
    m.cyl('Laser_Diode', .018, .008, [.085, .035, .318], 'EmissiveAmber')
    finish_rifle_attachments(m, a, g, .12)
    return m, a


def battle():
    g = 1.22
    m, a = gun_base('battle', main='SandCerakote', stock='SandCerakote', g=g)
    m.box('Solid_Stock', [.2, .215, .45], [0, -.025, -.445], 'SandCerakote', bevel=.045)
    m.box('EndPad', [.21, .24, .025], [0, -.027, -.67], 'Rubber')
    m.cyl('Flash_Hider', .048, .12, [0, 0, .5 * g + .115], 'BluedSteel')
    m.box('Forward_Bipod_Housing', [.16, .04, .11], [0, -.085, .37], 'BrushedMetal')
    a['muzzle'] = (0, .5 * g + .175)
    a['under'] = (-.065, .22 * g)
    finish_rifle_attachments(m, a, g, .14)
    return m, a


def shotgun():
    m = Model('shotgun')
    m.box('Forged_Receiver', [.175, .16, .37], [0, .01, -.03], 'BluedSteel', bevel=.035)
    m.cyl('Rifled_Game_Barrel', .035, .77, [0, .067, .42], 'BlackSteel', sections=22)
    m.cyl('Magazine_Tube', .043, .58, [0, -.054, .32], 'Gunmetal', sections=22)
    m.box('Slide_Pump', [.15, .15, .26], [0, -.035, .38], 'DarkWalnut', bevel=.035)
    for i in range(7):
        m.box(f'Pump_Texture_{i}', [.157, .016, .01], [0, .035, .27 + i * .034], 'BirchWood')
    m.box('Wood_Stock', [.17, .22, .41], [0, -.105, -.42], 'DarkWalnut', rot=(-.055, [1, 0, 0]), bevel=.048)
    m.box('Butt_Pad', [.18, .24, .03], [0, -.105, -.63], 'Rubber', rot=(-.055, [1, 0, 0]))
    m.ribbed_grip((0, -.06, -.14), (.13, .2, .13), 'DarkWalnut', 5)
    m.box('Trigger_Guard', [.07, .022, .11], [0, -.12, -.01], 'BlackSteel', bevel=.006)
    m.box('Trigger_Guard_Front', [.04, .06, .022], [0, -.095, .045], 'BlackSteel', bevel=.006)
    m.rod('Trigger', [0, -.07, -.01], [0, -.11, .01], .008, 'BrushedMetal')
    m.cyl('Receiver_BreechPin', .025, .02, [.095, .045, -.06], 'Brass', 'x')
    for x in (-1, 1):
        m.box('Sling_Mount', [.01, .046, .06], [x * .09, -.1, -.44], 'BrushedMetal')
    # Sights: a ghost-ring style notch on the receiver and the brass bead's post near the muzzle.
    top = .09
    sy = .145
    m.g = 'irons'
    for x in (-.025, .025):
        m.box('RearSight_Ear', [.02, sy - top, .03], [x, (top + sy) / 2, -.17], 'BlackSteel', bevel=.004)
    m.box('Bead_Post', [.014, sy - .102 - .012, .014], [0, (.102 + sy - .012) / 2, .75], 'BlackSteel', bevel=0)
    m.cyl('Front_Bead', .009, .012, [0, sy - .009, .75], 'Brass', axis='y', sections=10)
    m.box('FrontSight_Dot', [.012, .012, .006], [0, sy - .006, .743], 'SightGlow', bevel=0)
    m.g = 'body'
    a = dict(sy=sy, rear=-.17, base=top, optic_z=-.06, muzzle=(.067, .805), bore_r=.035)
    # Tube magazine: its cap, a longer tube, or a side saddle of spare shells.
    m.g = 'mag'
    m.cyl('Tube_Cap', .051, .04, [0, -.054, .63], 'Brass')
    m.g = 'extmag'
    m.cyl('Tube_Extension', .043, .14, [0, -.054, .68], 'Gunmetal', sections=22)
    m.box('Tube_Clamp', [.05, .14, .03], [0, .006, .72], 'BlackSteel', bevel=.005)
    m.cyl('Tube_Cap', .051, .04, [0, -.054, .77], 'Brass')
    m.g = 'fastmag'
    m.cyl('Tube_Cap', .051, .04, [0, -.054, .63], 'Brass')
    m.box('Side_Saddle', [.02, .08, .2], [.0975, .01, -.05], 'BlackSteel', bevel=.004)
    for i in range(5):
        m.cyl(f'Saddle_Shell_{i}', .014, .07, [.11, .01, -.13 + i * .04], 'RedDetail', axis='y', sections=10)
    m.g = 'body'
    ammo_band(m, -.054, .6, .1, .02)
    auto_selector(m, .0875, -.02, -.1)
    red_dot(m, a['optic_z'], top, sy)
    scope(m, a['optic_z'] + .03, top, sy, .34)
    foregrip(m, -.11, .38, 'DarkWalnut')
    a['heavy_ext'] = heavy_barrel(m, .6, .805, .067, .045, .1) - .805
    a['supp_len'] = suppressor(m, .805, .067, .05, .26)
    return m, a


def sniper():
    m = Model('sniper')
    m.box('Precision_Chassis', [.19, .18, .48], [0, 0, 0], 'SandCerakote', bevel=.04)
    m.box('Chassis_Underbody', [.15, .08, .35], [0, -.11, .02], 'Gunmetal')
    m.cyl('Long_Fluted_Barrel', .033, .95, [0, .056, .67], 'BluedSteel', sections=22)
    m.cyl('Muzzle_Brake', .062, .135, [0, .056, 1.2], 'BlackSteel')
    for i in range(4):
        m.box(f'Flute_{i}', [.008, .008, .48], [(1 if i % 2 else -1) * .035, .056 + (.015 if i > 1 else -.015), .57], 'BrushedMetal')
    m.box('Skeletonized_Stock', [.16, .16, .28], [0, -.02, -.45], 'SandCerakote')
    m.box('Cheek_Rest', [.14, .045, .3], [0, .08, -.47], 'Rubber')
    m.box('Butt', [.18, .22, .03], [0, -.01, -.6], 'Rubber')
    for x in (-.058, .058):
        m.rod(f'Stock_Strut_{x}', [x, -.07, -.15], [x, -.065, -.6], .013, 'BrushedMetal')
    m.box('Grip_Tang', [.11, .07, .08], [0, -.12, -.19], 'Gunmetal', bevel=.01)
    m.ribbed_grip((0, -.13, -.17), (.115, .25, .12), 'Rubber', 4)
    m.box('Bipod_Clamp', [.1, .085, .06], [0, -.015, .64], 'Gunmetal', bevel=.01)
    for xx in (-.08, .08):
        m.rod(f'Bipod_Leg_{xx}', [xx, -.04, .64], [xx * 1.7, -.37, .7], .018, 'BrushedMetal')
    top = m.rail(-.2, .4, .09, width=.2)
    sy = top + .08
    a = dict(sy=sy, rear=-.33, base=top, optic_z=.05, muzzle=(.056, 1.2675), bore_r=.062, builtin_scope=True)
    # The sniper's own scope; a red dot bought for it takes the scope's place.
    front = scope(m, .05, top, sy, .5, group='builtin')
    a['rear'] = front[0]
    box_mags(m, (0, -.14, -.075), (.12, .28, .12), 'Polymer', 3)
    ammo_band(m, -.22, -.06, .12, .12)
    auto_selector(m, .095, -.03, -.12)
    red_dot(m, .0, top, sy)
    scope(m, .05, top, sy, .5)
    foregrip(m, -.15, .14)
    a['heavy_ext'] = heavy_barrel(m, .3, 1.1325, .056, .045, .06) - 1.1325 + (1.1325 - 1.2675)
    a['heavy_ext'] = max(0.0, a['heavy_ext'])
    a['supp_len'] = suppressor(m, 1.2675, .056, .06, .32)
    return m, a


def lmg():
    m = Model('lmg')
    m.box('Heavy_Receiver', [.255, .24, .53], [0, .02, -.06], 'Gunmetal', bevel=.035)
    m.box('Feed_Cover', [.31, .055, .43], [0, .1625, -.03], 'BrushedMetal')
    m.box('Charging_Handle', [.09, .07, .13], [.15, .03, -.13], 'BlackSteel')
    m.cyl('Machine_Barrel', .048, .79, [0, .025, .59], 'BlackSteel', sections=22)
    m.cyl('Heat_Shroud', .105, .59, [0, .025, .48], 'ParkerizedSteel', sections=20)
    for i in range(8):
        for sx in (-1, 1):
            m.box(f'BarrelVent_{i}_{sx}', [.008, .048, .031], [sx * .108, .02, .2 + i * .065], 'Rubber')
    m.cyl('Muzzle_Shroud', .074, .11, [0, .025, 1.0], 'BlackSteel')
    m.cyl('Dark_Muzzle_Bore', .046, .006, [0, .025, 1.056], 'Rubber')
    m.box('Hollow_Stock', [.22, .22, .4], [0, -.03, -.51], 'Polymer')
    m.box('Stock_Pad', [.23, .235, .03], [0, -.03, -.72], 'Rubber')
    m.ribbed_grip((0, -.1, -.2), (.15, .26, .14), 'Rubber', 5)
    m.box('Trigger_Guard', [.07, .022, .12], [0, -.15, -.12], 'BlackSteel', bevel=.006)
    m.box('Trigger_Guard_Front', [.04, .05, .022], [0, -.125, -.065], 'BlackSteel', bevel=.006)
    for xx in (-.1, .1):
        m.rod(f'HeavyBipod_{xx}', [xx, -.05, .73], [xx * 1.6, -.42, .8], .022, 'Gunmetal')
    top = m.rail(-.3, .4, .19, width=.2)
    sy = top + .085
    a = dict(sy=sy, rear=-.25, base=top, optic_z=.0, muzzle=(.025, 1.059), bore_r=.074, under=(-.08, .55))
    irons(m, -.25, .35, top, sy)
    # Belt box: the box and its belt; a bigger box; or a box with a quick-change handle.
    def belt_box(h, handle):
        m.box('Belt_Box', [.29, h, .24], [-.04, -.095 - h / 2, .02], 'OliveCerakote', bevel=.035)
        for i in range(9):
            m.cyl(f'Visible_Link_{i}', .022, .07, [-.14 + i * .027, -.085, .16], 'Brass', axis='y', sections=12)
        if handle:
            m.box('Box_Handle', [.04, .1, .16], [-.04 - .165, -.095 - h / 2, .02], 'Brass', bevel=.01)
    m.g = 'mag'
    belt_box(.32, False)
    m.g = 'extmag'
    belt_box(.45, False)
    m.g = 'fastmag'
    belt_box(.32, True)
    m.g = 'body'
    ammo_band(m, -.2, .02, .29, .24)
    auto_selector(m, .1275, -.04, -.2)
    red_dot(m, a['optic_z'], top, sy)
    scope(m, a['optic_z'] + .05, top, sy, .46)
    foregrip(m, *a['under'])
    a['heavy_ext'] = heavy_barrel(m, .8, .945, .025, .085, .1) - .945
    a['supp_len'] = suppressor(m, 1.059, .025, .08, .34)
    return m, a


def helix():
    m = Model('helix')
    m.box('Ceramic_Core', [.21, .2, .33], [0, 0, -.115], 'WornPaint', bevel=.06)
    m.box('Upper_Housing', [.25, .11, .66], [0, .123, .07], 'BluedSteel', bevel=.04)
    m.box('Under_Rail', [.16, .05, .54], [0, -.12, .04], 'BlackSteel')
    m.cyl('Energy_Chamber', .091, .3, [0, .007, .2], 'EnergyGlass', sections=30)
    m.cyl('Contained_Plasma', .055, .28, [0, .007, .2], 'EmissiveCyan', sections=22)
    for i in range(7):
        m.ring(f'Containment_Coil_{i}', .095, .015, [0, .007, .08 + i * .043], 'Bronze', segments=24)
    m.cyl('Emitter_Barrel', .074, .37, [0, 0, .535], 'Gunmetal', sections=24)
    m.ring('Output_Emitter', .1, .027, [0, 0, .72], 'BrushedMetal', segments=32)
    m.cyl('Emitter_Lens', .058, .01, [0, 0, .725], 'EmissiveCyan', sections=30)
    m.box('Folded_Stock', [.2, .19, .34], [0, -.05, -.45], 'Gunmetal', bevel=.06)
    m.box('Shoulder_Gel', [.2, .21, .035], [0, -.06, -.635], 'Rubber')
    m.ribbed_grip((0, -.1, -.15), (.13, .29, .15), 'SlatePolymer', 6)
    for sx in (-1, 1):
        m.rod(f'Copper_Power_Line_{sx}', [sx * .112, -.07, -.12], [sx * .112, -.05, .34], .014, 'Copper')
        for z in (.02, .16, .3):
            m.box(f'Power_Indicator_{sx}_{z}', [.014, .025, .043], [sx * .13, .12, z], 'EmissiveCyan')
    top = .1785
    sy = top + .07
    a = dict(sy=sy, rear=-.2, base=top, optic_z=-.05, muzzle=(0, .747), bore_r=.074, under=(-.145, .25))
    irons(m, -.2, .35, top, sy)
    # Energy cell: the glass cell, a longer one, or two side by side.
    box_mags(m, (0, -.14, .14), (.15, .18, .15), 'EnergyGlass', 2)
    ammo_band(m, -.2, .15, .15, .15)
    auto_selector(m, .105, -.04, -.1)
    red_dot(m, a['optic_z'], top, sy)
    scope(m, a['optic_z'] + .04, top, sy, .4)
    foregrip(m, *a['under'], 'SlatePolymer')
    a['heavy_ext'] = heavy_barrel(m, .45, .69, 0, .088, .1) - .69 + (.69 - .747)
    a['heavy_ext'] = max(.04, a['heavy_ext'])
    a['supp_len'] = suppressor(m, .747, 0, .085, .24)
    return m, a


def pulse():
    m = Model('pulse')
    m.box('Power_Receiver', [.27, .27, .62], [0, 0, -.09], 'BlackSteel', bevel=.08)
    m.box('Shoulder_Brace', [.21, .25, .35], [0, -.03, -.47], 'SlatePolymer', bevel=.05)
    m.box('ButtPad', [.22, .26, .03], [0, -.04, -.655], 'Rubber')
    m.ribbed_grip((0, -.13, -.16), (.13, .29, .16), 'SlatePolymer', 6)
    for i in range(3):
        zz = .16 + i * .15
        m.ring(f'Magnetic_Torus_{i}', .15 - i * .012, .028, [0, .01, zz], 'BrushedMetal', segments=32)
        m.ring(f'Coil_Glow_{i}', .13 - i * .012, .008, [0, .01, zz + .018], 'EmissiveViolet', segments=32)
        for sx in (-1, 1):
            m.rod(f'Coil_Rail_{i}_{sx}', [sx * .13, -.05, zz - .06], [sx * .13, -.05, zz + .08], .012, 'Bronze')
    m.cyl('Pulse_Capacitor', .108, .56, [0, .01, .36], 'EnergyGlass', sections=32)
    m.cyl('Purple_Plasma', .082, .53, [0, .01, .36], 'EmissiveViolet', sections=28)
    m.cyl('Dish_Base', .185, .1, [0, .01, .68], 'Gunmetal', sections=32)
    m.cyl('Dish_Reflector', .23, .045, [0, .01, .75], 'BrushedMetal', sections=36)
    m.ring('Pulse_Mouth', .178, .025, [0, .01, .78], 'EmissiveCyan', segments=32)
    for i in range(8):
        t = i * math.pi / 4
        m.rod(f'Emitter_Fin_{i}', [.11 * math.cos(t), .01 + .11 * math.sin(t), .73], [.21 * math.cos(t), .01 + .21 * math.sin(t), .8], .018, 'Bronze')
    m.box('Reactor_Display', [.19, .048, .13], [0, .155, -.19], 'EmissiveViolet', bevel=.012)
    for x in (-.06, .06):
        m.cyl(f'BatteryCell_{x}', .04, .19, [x, -.225, .05], 'EmissiveCyan', 'y')
    top = .135
    sy = top + .09
    a = dict(sy=sy, rear=-.33, base=top, optic_z=-.02, muzzle=(.01, .8), bore_r=.2, under=(-.098, .42))
    irons(m, -.33, .14, top, sy)
    # Battery pack: the pack, a bigger one, or one with a quick-release lever.
    m.g = 'mag'
    m.box('Battery_Pack', [.18, .22, .2], [0, -.205, .045], 'Gunmetal')
    m.g = 'extmag'
    m.box('Battery_Pack', [.18, .32, .2], [0, -.255, .045], 'Gunmetal')
    m.g = 'fastmag'
    m.box('Battery_Pack', [.18, .22, .2], [0, -.205, .045], 'Gunmetal')
    m.box('Quick_Release', [.03, .12, .04], [.105, -.205, .1], 'Brass', bevel=.006)
    m.g = 'body'
    ammo_band(m, -.15, .045, .18, .2)
    auto_selector(m, .135, -.06, -.2)
    red_dot(m, a['optic_z'], top, sy)
    scope(m, a['optic_z'] + .04, top, sy, .4)
    foregrip(m, *a['under'], 'SlatePolymer')
    # The dish is the muzzle: a heavy barrel adds a focusing ring, a suppressor a damping cone.
    m.g = 'heavy'
    m.ring('Focus_Ring', .2, .03, [0, .01, .83], 'Bronze', segments=36)
    for i in range(4):
        t = i * math.pi / 2 + math.pi / 4
        m.rod(f'Focus_Strut_{i}', [.19 * math.cos(t), .01 + .19 * math.sin(t), .78], [.2 * math.cos(t), .01 + .2 * math.sin(t), .83], .012, 'Bronze')
    m.g = 'supp'
    m.tube('Damper', .2, .17, .18, [0, .01, .89], 'BlackSteel', sections=32)
    m.g = 'body'
    a['heavy_ext'] = .05
    a['supp_len'] = .2
    return m, a


# ------------------------------------------------------------------ the Aegis Shield and its four parts

def shield():
    m = Model('shield')
    m.box('Armored_Plate', [1.43, 1.78, .135], [0, .91, 0], 'BluedSteel', bevel=.11)
    m.box('Forward_Bumper', [1.51, .14, .17], [0, .18, .025], 'BlackSteel')
    m.box('RimLeft', [.09, 1.71, .2], [-.72, .91, 0], 'BrushedMetal')
    m.box('RimRight', [.09, 1.71, .2], [.72, .91, 0], 'BrushedMetal')
    m.box('TopArmor', [1.29, .15, .2], [0, 1.8, 0], 'Gunmetal')
    m.box('Viewing_Slit_Frame', [.78, .3, .11], [0, 1.43, .108], 'BlackSteel')
    m.box('BulletResistant_Viewport', [.68, .21, .035], [0, 1.43, .177], 'ShieldGlass')
    m.box('Power_Armor_Reinforcement', [.95, .16, .12], [0, .8, .125], 'Gunmetal')
    m.box('Seal_Insignia', [.23, .23, .018], [0, .57, .075], 'Warning')
    for sx in (-1, 1):
        for yy in (.28, .57, .94, 1.71 - .1):
            m.cyl(f'Armor_Bolt_{sx}_{yy}', .035, .015, [sx * .6, yy, .074], 'Brass')
    for yy in (.48, .72):
        m.box(f'Rear_ArmStrap_{yy}', [.44, .08, .08], [0, yy, -.1], 'Rubber')
    m.box('Rear_Handle', [.08, .38, .12], [0, .9, -.2], 'BlackSteel')
    m.box('Pressure_Lock', [.35, .25, .06], [0, 1.09, -.095], 'BrushedMetal')
    m.cyl('Power_Unit', .12, .15, [0, .33, -.14], 'Bronze', axis='z')
    for sx in (-1, 1):
        m.box(f'PowerIndicator_{sx}', [.035, .33, .023], [sx * .5, 1.03, .074], 'EmissiveCyan')
    return m


def shield_plate():
    m = Model('shield_plate')
    m.box('MainArmor', [1.43, 1.78, .13], [0, .91, 0], 'BluedSteel', bevel=.1)
    for x in (-.6, .6):
        for y in (.35, .9, 1.5):
            m.cyl(f'Fastener_{x}_{y}', .035, .016, [x, y, .071], 'Brass')
    return m


def shield_view():
    m = Model('shield_view')
    m.box('Bezel', [.83, .34, .1], [0, .14, 0], 'BlackSteel')
    m.box('ArmoredGlass', [.68, .21, .045], [0, .14, .07], 'ShieldGlass')
    for x in (-.37, .37):
        m.cyl(f'GlassBolt_{x}', .026, .017, [x, .14, .058], 'Brass')
    return m


def shield_core():
    m = Model('shield_core')
    m.cyl('Battery', .19, .35, [0, 0, 0], 'Gunmetal', 'z', sections=24)
    m.ring('EnergyLoop', .21, .038, [0, 0, .16], 'EmissiveCyan', segments=32)
    for i in range(6):
        t = i * math.pi / 3
        m.box(f'Cell_{i}', [.06, .09, .05], [.12 * math.cos(t), .12 * math.sin(t), .195], 'Bronze')
    return m


def shield_brace():
    m = Model('shield_brace')
    m.box('CrossBrace', [1.22, .11, .12], [0, 0, 0], 'BrushedMetal')
    for sx in (-1, 1):
        m.rod(f'Rail_{sx}', [sx * .51, -.15, .03], [sx * .51, .3, .03], .05, 'BlackSteel')
    m.box('Handle', [.14, .32, .12], [0, 0, -.09], 'Rubber')
    return m


# ------------------------------------------------------------------ output

WRITTEN = set()


def write_mesh(path, parts, xf):
    """PRP1 (see Core/PropModels.LoadRaw): one submesh per material, faces split where they meet at over 40 degrees."""
    WRITTEN.add(os.path.basename(path))
    groups = {}
    for name, mesh, mat in parts:
        groups.setdefault(mat, []).append((xf(np.asarray(mesh.vertices, float)), np.asarray(mesh.faces)))
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
                    near = [q for q in by_vert[v] if fn[q] @ fn[fi] > COS]
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
    with open(path, 'wb') as f:
        f.write(b'PRP1')
        f.write(struct.pack('<i', len(subs)))
        for k, _ in subs:
            b = k.encode()
            f.write(struct.pack('<i', len(b)))
            f.write(b)
        f.write(struct.pack('<i', len(P)))
        for arr in (P, N, np.zeros((len(P), 2))):
            f.write(arr.astype('<f4').tobytes())
        for _, t in subs:
            f.write(struct.pack('<i', len(t)))
            f.write(t.astype('<i4').tobytes())
    return P


GUNS = {  # slug: (builder, view-model length in metres)
    'harker9': (pistol, .3),
    'sable9': (smg, .62),
    'atlas5': (carbine, .86),
    'kodiak7': (battle, .95),
    'riot12': (shotgun, .95),
    'specter308': (sniper, 1.1),
    'grinder': (lmg, 1.0),
    'helix': (helix, .9),
    'pulse': (pulse, .95),
}
GROUPS = ('irons', 'mag', 'builtin', 'reddot', 'scope', 'extmag', 'fastmag', 'grip', 'supp', 'heavy', 'ammo', 'auto')


def main():
    os.makedirs(DST, exist_ok=True)
    info = ['# slug builtin muzzleX muzzleY muzzleZ minX minY minZ maxX maxY maxZ suppLen heavyLen groups...  (tools/tenweapons/build_guns.py)']
    for slug, (build, length) in GUNS.items():
        m, a = build()
        base = [p for p in m.parts if p[0] in ('body', 'irons', 'mag', 'builtin')]
        allv = np.vstack([np.asarray(p[2].vertices) for p in base])
        s = length / (allv[:, 2].max() - allv[:, 2].min())
        sy, rear = a['sy'], a['rear']

        def xf(v, s=s, sy=sy, rear=rear):
            return np.column_stack([-v[:, 0] * s * NARROW, (v[:, 1] - sy) * s + SIGHT_Y, (v[:, 2] - rear) * s + REAR_Z])

        groups = {}
        for g, name, mesh, mat in m.parts:
            groups.setdefault(g, []).append((name, mesh, mat))
        pts = []
        for g, parts in groups.items():
            P = write_mesh(os.path.join(DST, slug + ('' if g == 'body' else '_' + g) + '.bytes'), parts, xf)
            if g in ('body', 'irons', 'mag', 'builtin'):
                pts.append(P)
        P = np.vstack(pts)
        lo, hi = P.min(0), P.max(0)
        mz = xf(np.array([[0.0, a['muzzle'][0], a['muzzle'][1]]]))[0]
        have = [g for g in GROUPS if g in groups]
        info.append(' '.join([slug, 'scope' if a.get('builtin_scope') else 'iron'] + ['%.4f' % x for x in list(mz) + list(lo) + list(hi)]
                             + ['%.4f' % (a['supp_len'] * s), '%.4f' % (a['heavy_ext'] * s)] + have))
        print('%-11s scale %.3f  min %s max %s  %s' % (slug, s, np.round(lo, 3), np.round(hi, 3), ' '.join(have)))
    for build in (shield, shield_plate, shield_view, shield_core, shield_brace):
        m = build()
        write_mesh(os.path.join(DST, m.name + '.bytes'), [(n, mesh, mat) for _, n, mesh, mat in m.parts], lambda v: v * np.array([-1.0, 1.0, 1.0]))
    with open(os.path.join(DST, 'guns.txt'), 'w') as f:
        f.write('\n'.join(info) + '\n')
    tidy()


META = """fileFormatVersion: 2
guid: %s
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def tidy():
    """Drops meshes this script no longer makes (a .bytes not written this run) and gives new ones a Unity .meta."""
    for f in os.listdir(DST):
        path = os.path.join(DST, f)
        if f.endswith('.bytes') and f not in WRITTEN:
            os.remove(path)
            if os.path.exists(path + '.meta'):
                os.remove(path + '.meta')
        elif f.endswith('.bytes') and not os.path.exists(path + '.meta'):
            with open(path + '.meta', 'w') as m:
                m.write(META % uuid.uuid4().hex)


if __name__ == '__main__':
    main()
