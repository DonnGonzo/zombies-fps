"""Copies Ivan's Mixamo characters and clips into Resources, with their textures pulled out of the FBX files.

    python3 tools/creatures/pack_creatures.py <Mutant.fbx> <creature clips dir> <Warrok.fbx> <Ely.fbx> <shooter clips dir>

Creatures/Mutant    the Mutant (the Warden, and Azhrakel in red via redden.py)
Creatures/Warrok    Warrok (General Ashgrave)
Creatures/Animations  the creature pack clips both play
Survivors/Character Ely (teammates), Survivors/Animations the shooter pack

All are imported Generic (Humanoid can't map the Mutant: its left hand has no fingers, and both packs' clips are on
its skeleton). The game plays the clips on a hidden Mutant and copies the pose onto Warrok and Ely. Textures
are named <Material>_Diffuse/_Normal/_Glow, which is how ZombieModel and SurvivorModel find them.
"""
import io, os, shutil, subprocess, sys, uuid, collections
sys.path.insert(0, os.path.dirname(__file__))
import fbx
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources')
CREATURE_CLIPS = ['mutant breathing idle', 'mutant walking', 'mutant run', 'mutant dying', 'mutant swiping', 'mutant punch',
                  'mutant jump attack', 'mutant roaring']
SHOOTER_CLIPS = ['rifle aiming idle', 'walking', 'walking backwards', 'strafe left', 'strafe right', 'rifle run', 'run backwards',
                 'rifle jump', 'firing rifle', 'toss grenade', 'hit reaction']
GITATTRIBUTES = ('# Stored in plain git, not LFS: this environment can\'t reach the LFS server, and each file is under GitHub\'s 100 MB limit.\n'
                 '*.fbx !filter !diff !merge binary\n*.jpg !filter !diff !merge binary\n')
MODEL_META = """ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects: {}
  animationType: 2
  importAnimation: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXTURE_META = """TextureImporter:
  serializedVersion: 13
  mipmaps:
    sRGBTexture: {srgb}
  textureType: {type}
  maxTextureSize: 2048
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
MAPS = {'DiffuseColor': 'Diffuse', 'NormalMap': 'Normal', 'EmissiveColor': 'Glow'}


def meta(path, body):
    m = path + '.meta'
    if os.path.exists(m):
        return
    with open(m, 'w', newline='\n') as f:
        f.write(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n' + body)


def folder(path):
    os.makedirs(path, exist_ok=True)
    meta(path, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def texture(path, normal):
    meta(path, TEXTURE_META.format(srgb=0 if normal else 1, type=1 if normal else 0))


def model(src, dst):
    shutil.copyfile(src, dst)
    meta(dst, MODEL_META)


def textures(src, out):
    """Each material's embedded textures, as JPGs named for the material."""
    folder(out)
    top = fbx.parse(src)
    d = {n[0]: n for n in top}
    objs = {k[1][0]: k for k in d['Objects'][2]}
    children = collections.defaultdict(list)
    props = []
    for c in d['Connections'][2]:
        a = c[1]
        if a[0] == 'OO':
            children[a[2]].append(a[1])
        elif a[0] == 'OP':
            props.append(a[1:4])
    written = []
    for tex, mat, prop in props:
        if objs.get(tex, [None])[0] != 'Texture' or objs.get(mat, [None])[0] != 'Material' or prop not in MAPS:
            continue
        for v in children[tex]:
            if objs.get(v, [None])[0] != 'Video':
                continue
            data = [k for k in objs[v][2] if k[0] == 'Content'][0][1][0]
            name = objs[mat][1][1].split('\x00')[0]
            path = os.path.join(out, f'{name}_{MAPS[prop]}.jpg')
            Image.open(io.BytesIO(data)).convert('RGB').save(path, quality=90)
            texture(path, prop == 'NormalMap')
            written.append(path)
    return written


def clips(src_dir, names, out):
    folder(out)
    for n in names:
        model(os.path.join(src_dir, n + '.fbx'), os.path.join(out, n + '.fbx'))


def main(mutant, creature_dir, warrok, ely, shooter_dir):
    creatures = os.path.join(ROOT, 'Creatures')
    folder(creatures)
    with open(os.path.join(creatures, '.gitattributes'), 'w', newline='\n') as f:
        f.write(GITATTRIBUTES)
    folder(os.path.join(creatures, 'Mutant'))
    model(mutant, os.path.join(creatures, 'Mutant', 'Mutant.fbx'))
    tex = textures(mutant, os.path.join(creatures, 'Mutant', 'Textures'))
    diffuse = [t for t in tex if t.endswith('_Diffuse.jpg')][0]
    red, glow = diffuse.replace('_Diffuse', '_Red_Diffuse'), diffuse.replace('_Diffuse', '_Red_Glow')
    subprocess.check_call([sys.executable, '-I', os.path.join(os.path.dirname(__file__), 'redden.py'), diffuse, red, glow])
    texture(red, False)
    texture(glow, False)
    folder(os.path.join(creatures, 'Warrok'))
    model(warrok, os.path.join(creatures, 'Warrok', 'Warrok.fbx'))
    textures(warrok, os.path.join(creatures, 'Warrok', 'Textures'))
    clips(creature_dir, CREATURE_CLIPS, os.path.join(creatures, 'Animations'))

    survivors = os.path.join(ROOT, 'Survivors')
    folder(survivors)
    with open(os.path.join(survivors, '.gitattributes'), 'w', newline='\n') as f:
        f.write(GITATTRIBUTES)
    folder(os.path.join(survivors, 'Character'))
    model(ely, os.path.join(survivors, 'Character', 'Ely.fbx'))
    textures(ely, os.path.join(survivors, 'Character', 'Textures'))
    clips(shooter_dir, SHOOTER_CLIPS, os.path.join(survivors, 'Animations'))


if __name__ == '__main__':
    main(*sys.argv[1:6])
