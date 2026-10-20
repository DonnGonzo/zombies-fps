"""Copies the effect textures the game uses out of Ivan's Brackeys VFX bundle (CC0) into Resources/Fx.

    python3 tools/fx/pack_fx.py <unzipped brackeys_vfx_bundle folder>

Flipbooks keep their grid in the name (explosion_8x8), which FxSystem reads. Mipmaps are off so cells don't bleed.
"""
import os, sys, uuid
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'Fx')
PICK = {
    'flipbooks/explosion_01_8x8.tga': 'explosion_8x8',
    'flipbooks/explosion_smoke_01_8x8.tga': 'explosion_smoke_8x8',
    'flipbooks/wispy_smoke_01_8x8.tga': 'wispy_smoke_8x8',
    'particles/alpha/smoke_04_a.png': 'smoke',
    'particles/alpha/muzzle_01_a.png': 'muzzle_flame',
    'particles/alpha/muzzle_02_a.png': 'muzzle_star',
}
META = """TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  maxTextureSize: 1024
  textureSettings:
    wrapU: 1
    wrapV: 1
    wrapW: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def meta(path, body):
    if not os.path.exists(path + '.meta'):
        with open(path + '.meta', 'w', newline='\n') as f:
            f.write(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n' + body)


def main(src):
    os.makedirs(OUT, exist_ok=True)
    meta(OUT, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    with open(os.path.join(OUT, '.gitattributes'), 'w', newline='\n') as f:
        f.write("# Stored in plain git, not LFS: this environment can't reach the LFS server.\n*.png !filter !diff !merge binary\n")
    with open(os.path.join(OUT, 'CREDITS.txt'), 'w', newline='\n') as f:
        f.write('From the Brackeys VFX bundle, CC0.\nFlipbooks by Thomas Iche (Unity free VFX image sequences); particle textures by Kenney (kenney.nl).\n')
    for rel, name in PICK.items():
        im = Image.open(os.path.join(src, rel)).convert('RGBA')
        if im.width > 1024:
            im = im.resize((1024, 1024 * im.height // im.width), Image.LANCZOS)
        path = os.path.join(OUT, name + '.png')
        im.save(path, optimize=True)
        meta(path, META)
        print(path, im.size, os.path.getsize(path) // 1024, 'KB')


if __name__ == '__main__':
    main(sys.argv[1])
