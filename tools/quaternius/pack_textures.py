# Copies the texture sets the converted Quaternius models use (named in their submesh keys, see pack_gltf.py) into
# Resources/CC0/Quaternius/Textures as 1024 px jpg: name_albedo (colour, with the set's ambient occlusion baked in,
# since the game's material has no occlusion map) and name_normal (OpenGL convention, as Unity uses). Prints each
# set's average metallic and gloss from its ORM map, for the table in World/ZoneMap.Kits.cs.
#   python3 pack_textures.py <Resources/CC0> fp=<dir>[,<dir>] sf=<dir>... (folders holding each kit's pngs)
import sys, os, struct, numpy as np
from PIL import Image
res = sys.argv[1]
dirs = {}
for a in sys.argv[2:]:
    k, v = a.split('=', 1); dirs[k] = v.split(',')
keys = set()
for sub in ('Fantasy', 'SciFi', 'SciFiModular'):
    d = os.path.join(res, sub)
    for f in os.listdir(d):
        if not f.endswith('.bytes'): continue
        b = open(os.path.join(d, f), 'rb').read(); o = 8
        for _ in range(struct.unpack_from('<i', b, 4)[0]):
            n = struct.unpack_from('<i', b, o)[0]; keys.add(b[o + 4:o + 4 + n].decode()); o += 4 + n
out = os.path.join(res, 'Quaternius', 'Textures'); os.makedirs(out, exist_ok=True)
def find(name):
    pre, rest = name.split('_', 1)
    for d in dirs[pre]:
        p = os.path.join(d, rest + '.png')
        if os.path.exists(p): return p
    raise SystemExit('missing ' + name)
done = set()
for k in sorted(keys):
    _, albedo, normal = k.split('|')[:3]
    if albedo != '-' and albedo not in done:
        done.add(albedo)
        col = np.asarray(Image.open(find(albedo)).convert('RGB').resize((1024, 1024), Image.LANCZOS)).astype(np.float32)
        orm_name = albedo.replace('_BaseColor', '_ORM')
        for suffix in ('_Red', '_Blue'): orm_name = orm_name.replace('_ORM' + suffix, '_ORM')
        try:
            orm = np.asarray(Image.open(find(orm_name)).convert('RGB').resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255
            col *= (0.35 + 0.65 * orm[..., :1])
            print('%-36s metallic %.2f gloss %.2f' % (albedo, orm[..., 2].mean(), 1 - orm[..., 1].mean()))
        except SystemExit:
            print('%-36s (no ORM)' % albedo)
        Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(os.path.join(out, albedo + '_albedo.jpg'), quality=88)
    if normal != '-' and normal not in done:
        done.add(normal)
        Image.open(find(normal)).convert('RGB').resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, normal + '_normal.jpg'), quality=90)
