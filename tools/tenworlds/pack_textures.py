# Copies the Ten Worlds texture sets that the exported props use (their submesh keys) into Resources as jpg:
# key_albedo.jpg (colour) and key_normal.jpg (normal map, glTF/OpenGL convention, which Unity also uses).
#   python3 pack_textures.py <TenWorlds/Textures> <Resources/CC0/TenWorlds>
import sys, os, struct
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
keys = set()
for f in os.listdir(out):
    if not f.endswith('.bytes'): continue
    b = open(os.path.join(out, f), 'rb').read(); o = 8
    for _ in range(struct.unpack_from('<i', b, 4)[0]):
        n = struct.unpack_from('<i', b, o)[0]; keys.add(b[o + 4:o + 4 + n].decode()); o += 4 + n
os.makedirs(os.path.join(out, 'Textures'), exist_ok=True)
for k in sorted(keys):
    for kind, suffix in (('BaseColor', 'albedo'), ('Normal', 'normal')):
        p = os.path.join(src, '%s_%s.png' % (k, kind))
        if os.path.exists(p):
            Image.open(p).convert('RGB').save(os.path.join(out, 'Textures', '%s_%s.jpg' % (k, suffix)), quality=92)
    print(k)
