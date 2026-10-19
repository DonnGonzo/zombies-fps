# Packs Poly Haven surface maps into the two textures the Surface shader reads:
# <slug>_albedo.jpg (diffuse with some AO baked in) and <slug>_nrh.jpg (normal X/Y in RG, height in B).
import sys, os
from PIL import Image, ImageChops
src, dst = sys.argv[1], sys.argv[2]
slugs = sys.argv[3:]
for s in slugs:
    diff = Image.open(f"{src}/{s}_diff.jpg").convert("RGB")
    size = diff.size
    ao = Image.open(f"{src}/{s}_ao.jpg").convert("L").resize(size)
    # albedo * lerp(1, ao, 0.5)
    ao_soft = ao.point(lambda v: int(127.5 + v / 2))
    albedo = ImageChops.multiply(diff, Image.merge("RGB", (ao_soft, ao_soft, ao_soft)))
    albedo.save(f"{dst}/{s}_albedo.jpg", quality=88, optimize=True)
    nor = Image.open(f"{src}/{s}_nor.jpg").convert("RGB").resize(size)
    disp = Image.open(f"{src}/{s}_disp.jpg").convert("L").resize(size)
    r, g, _ = nor.split()
    Image.merge("RGB", (r, g, disp)).save(f"{dst}/{s}_nrh.jpg", quality=90, optimize=True)
    print(s, os.path.getsize(f"{dst}/{s}_albedo.jpg"), os.path.getsize(f"{dst}/{s}_nrh.jpg"))
