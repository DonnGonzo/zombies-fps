# Converts models from Kenney's Retro Fantasy Kit (CC0, kenney.nl; the zip's Models/GLB format folder) into the
# PRP1 meshes the game reads, through tools/quaternius/pack_gltf.py, so their submesh keys are the same
# "Q|albedo|normal|..." the Quaternius kits use (World/ZoneMap.Kits.cs, KitMaterial). The kit's textures are small
# pixel-art tiles: they're copied as they are (png, point filtered by their .meta) into
# Resources/CC0/Quaternius/Textures as kr_<name>_albedo.png.
#
#   python3 pack_kenney.py "<kit>/Models/GLB format" name ...
#
# Writes Resources/CC0/Kenney/<name>.bytes (+ .meta for anything new) and prints each model's bounds, for the
# PackItems in World/ZoneMap.Greenhouse.cs.
import sys, os, json, struct, shutil, subprocess, tempfile, uuid

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
RES = os.path.join(REPO, 'Assets', '_Project', 'Resources', 'CC0')
OUT = os.path.join(RES, 'Kenney')
TEX = os.path.join(RES, 'Quaternius', 'Textures')

BYTES_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def meta(path, text):
    if os.path.exists(path + '.meta'): return
    open(path + '.meta', 'w').write(text.format(guid=uuid.uuid4().hex))


def texture_meta():
    # An existing albedo's importer settings, point filtered so the pixel art stays crisp, keeping the alpha (the
    # trees and the iron fence are cut out of it; KitMaterial draws those alpha tested).
    src = open(os.path.join(TEX, 'fp_T_Page_Noise_albedo.jpg.meta')).read()
    lines = []
    for line in src.splitlines():
        if line.startswith('guid:'): line = 'guid: {guid}'
        elif line.strip().startswith('filterMode:'): line = line.split(':')[0] + ': 0'
        elif line.strip().startswith(('alphaUsage:', 'alphaIsTransparency:')): line = line.split(':')[0] + ': 1'
        lines.append(line.replace('{', '{{').replace('}', '}}') if not line.startswith('guid:') else line)
    return '\n'.join(lines) + '\n'


def main():
    src, names = sys.argv[1], sys.argv[2:]
    os.makedirs(OUT, exist_ok=True)
    tmp = tempfile.mkdtemp()
    gltfs, textures = [], set()
    for name in names:
        b = open(os.path.join(src, name + '.glb'), 'rb').read()
        n = struct.unpack_from('<I', b, 12)[0]
        g = json.loads(b[20:20 + n])
        o = 20 + n
        bn = struct.unpack_from('<I', b, o)[0]
        open(os.path.join(tmp, name + '.bin'), 'wb').write(b[o + 8:o + 8 + bn])
        g['buffers'] = [{'uri': name + '.bin', 'byteLength': bn}]
        for im in g.get('images', []): textures.add(os.path.basename(im['uri']))
        json.dump(g, open(os.path.join(tmp, name + '.gltf'), 'w'))
        gltfs.append(os.path.join(tmp, name + '.gltf'))
    subprocess.check_call([sys.executable, '-I', os.path.join(REPO, 'tools', 'quaternius', 'pack_gltf.py'), 'kr', OUT] + gltfs)
    for name in names: meta(os.path.join(OUT, name + '.bytes'), BYTES_META)
    tmeta = texture_meta()
    for t in sorted(textures):
        dst = os.path.join(TEX, 'kr_' + os.path.splitext(t)[0] + '_albedo.png')
        shutil.copyfile(os.path.join(src, 'Textures', t), dst)
        meta(dst, tmeta)
    shutil.rmtree(tmp)


main()
