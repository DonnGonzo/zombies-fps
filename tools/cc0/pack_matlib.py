# Packs Material-LIB maps (base colour -B and normal -N, no height map) into the two textures the Surface
# shader reads: <slug>_albedo.jpg and <slug>_nrh.jpg (normal X/Y in RG, height in B). Height is made from the
# base colour's brightness, blurred, since the pack ships none.
# Usage: python3 pack_matlib.py <Material-LIB dir> <out dir>
import sys, os
from PIL import Image, ImageFilter, ImageOps
src, dst = sys.argv[1], sys.argv[2]
SIZE = 1024
# slug: (base colour file, normal file)
MAPS = {
    "matlib_ashlar": ("StoneTiles/StoneTiles-B.png", "StoneTiles/StoneTiles-N.png"),
    "matlib_mosaic": ("MedievalTiles/MedievalTiles[Red]-B.png", "MedievalTiles/MedievalTiles-N.png"),
    "matlib_mosaic_grey": ("MedievalTiles/MedievalTiles[Grey]-B.png", "MedievalTiles/MedievalTiles-N.png"),
    "matlib_fluted": ("StonePillars/StonePillars-B.png", "StonePillars/StonePillars-N.png"),
    "matlib_masonry": ("BrickWall/BrickWall[Grey]-B.png", "BrickWall/BrickWall-N.png"),
}
for slug, (b, n) in MAPS.items():
    base = Image.open(os.path.join(src, b)).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    base.save(f"{dst}/{slug}_albedo.jpg", quality=88, optimize=True)
    nor = Image.open(os.path.join(src, n)).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    height = ImageOps.autocontrast(base.convert("L").filter(ImageFilter.GaussianBlur(2)), cutoff=1)
    r, g, _ = nor.split()
    Image.merge("RGB", (r, g, height)).save(f"{dst}/{slug}_nrh.jpg", quality=90, optimize=True)
    print(slug, os.path.getsize(f"{dst}/{slug}_albedo.jpg"), os.path.getsize(f"{dst}/{slug}_nrh.jpg"))
