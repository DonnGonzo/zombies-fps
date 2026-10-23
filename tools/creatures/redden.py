"""Azhrakel's skin: the Mutant's diffuse pushed to blood red, its cyan glow turned to fire.

    python3 tools/creatures/redden.py <Mutant diffuse> <out_Diffuse.jpg> <out_Glow.jpg>

The glow map is the fire alone (black elsewhere), for the material's emission.
"""
import sys
import numpy as np
from PIL import Image

src, out, glow_out = sys.argv[1], sys.argv[2], sys.argv[3]
hsv = np.asarray(Image.open(src).convert('RGB').convert('HSV')).astype(np.float32)
h, s, v = hsv[..., 0] * 360 / 255, hsv[..., 1] / 255, hsv[..., 2] / 255

glow = (h > 100) & (h < 205) & (s > 0.35) & (v > 0.35)
# Skin, rock and cloth: deep red, darker in the folds.
nh = np.full_like(h, 356.0)
ns = np.clip(s * 0.5 + 0.6 - v * 0.1, 0, 0.92)
nv = np.clip(v * 1.05, 0, 1)
# The glowing veins and eyes: fire, from orange to near-white at the hottest.
nh[glow] = 18 + 22 * v[glow]
ns[glow] = np.clip(1.1 - v[glow] * 0.4, 0.55, 1)
nv[glow] = np.clip(v[glow] * 1.15, 0, 1)

hsv2 = np.stack([nh % 360 * 255 / 360, ns * 255, nv * 255], -1).astype(np.uint8)
Image.fromarray(hsv2, 'HSV').convert('RGB').save(out, quality=90)

rgb = np.asarray(Image.open(out).convert('RGB')).astype(np.float32)
mask = np.clip((v - 0.35) / 0.3, 0, 1) * glow
Image.fromarray((rgb * mask[..., None]).astype(np.uint8)).save(glow_out, quality=90)
