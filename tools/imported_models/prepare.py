"""Extract user packs, including nested archives, and stage Unity source art.

Usage: python tools/imported_models/prepare.py [archive directory]
Original archives are never modified. Blender-only sources are converted separately.
"""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import sys
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r'D:\GitHub\Models')
STAGE = ROOT / 'Temp/model-import/source'
DEST = ROOT / 'Assets/_Project/ImportedModels'

def extract(archive, destination):
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        for entry in z.infolist():
            target = (destination / entry.filename).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise ValueError(f'Unsafe archive path: {entry.filename}')
            z.extract(entry, destination)

def main():
    manifest = []
    for archive in sorted(SOURCE.glob('*.zip')):
        slug = archive.stem
        stage = STAGE / slug
        extract(archive, stage)
        for nested in list(stage.rglob('*.zip')):
            extract(nested, nested.parent / 'unpacked')
        for nested in stage.rglob('*.rar'):
            subprocess.run([r'C:\Program Files\WinRAR\UnRAR.exe', 'x', '-o+', '-idq', str(nested), str(nested.parent) + '/'], check=True)
        dest = DEST / slug
        dest.mkdir(parents=True, exist_ok=True)
        models = [p for p in stage.rglob('*') if p.suffix.lower() in ('.fbx', '.obj')]
        for model in models:
            shutil.copy2(model, dest / ('model' + model.suffix.lower()))
        textures = dest / 'Textures'
        textures.mkdir(exist_ok=True)
        for texture in (stage / 'textures').glob('*'):
            with Image.open(texture) as im:
                im.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
                im.save(textures / (texture.stem + '.png'))
        manifest.append(dict(id=slug, archive=archive.name, sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
                             sourceModels=[str(p.relative_to(stage)) for p in stage.rglob('*') if p.suffix.lower() in ('.fbx', '.obj', '.blend')],
                             textureLimit=2048))
        print(slug, flush=True)
    # Keep hand-added entries (models that came without an archive, such as the blackjack table).
    path = ROOT / 'tools/imported_models/sources.json'
    if path.exists(): manifest += [e for e in json.loads(path.read_text()) if not e.get('archive')]
    path.write_text(json.dumps(manifest, indent=2)+'\n')

if __name__ == '__main__':
    main()
