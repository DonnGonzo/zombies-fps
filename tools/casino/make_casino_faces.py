# Draws the casino's card faces and slot reel symbols (original art, made by this script with Pillow and the DejaVu
# fonts) into Assets/_Project/Resources/Casino:
#   cards.png      2048 x 1024 atlas, 13 columns x 5 rows. Rows 0-3 are spades, hearts, diamonds, clubs (the order of
#                  CasinoRules.CardName), columns A, 2 .. 10, J, Q, K. Row 4 column 0 is the card back.
#   slot_reel.png  256 x 1024 strip, 6 cells top to bottom in CasinoRules.Symbols order (cherry, lemon, bell, bar,
#                  diamond, seven).
# World/CasinoTable.cs reads both by those layouts.
#   python tools/casino/make_casino_faces.py
import os, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'Casino')
FONT = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
SERIF = '/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf'
SS = 4  # supersampling

def font(size, path=FONT): return ImageFont.truetype(path, size * SS)

# ------------------------------------------------------------------ suits as shapes (no glyph fallbacks needed)
def suit(d, s, cx, cy, size, color):
    """Draws suit s (0 spade, 1 heart, 2 diamond, 3 club) centred on cx, cy, size across, in supersampled pixels."""
    r = size / 2
    if s == 2:
        d.polygon([(cx, cy - r * 1.1), (cx + r * 0.78, cy), (cx, cy + r * 1.1), (cx - r * 0.78, cy)], fill=color)
        return
    if s == 1 or s == 0:
        # The classic parametric heart; a spade is the same heart upside down on a flared stem.
        sign = 1 if s == 1 else -1
        pts = []
        for i in range(64):
            t = 2 * math.pi * i / 64
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((cx + x / 17 * r, cy - sign * (y + 2.5) / 15.5 * r))
        d.polygon(pts, fill=color)
        if s == 0:
            d.polygon([(cx, cy + r * 0.1), (cx - r * 0.42, cy + r * 1.05), (cx + r * 0.42, cy + r * 1.05)], fill=color)
        return
    # Club: three balls and a stem.
    b = r * 0.44
    for ox, oy in ((0, -r * 0.48), (-r * 0.5, r * 0.12), (r * 0.5, r * 0.12)):
        d.ellipse([cx + ox - b, cy + oy - b, cx + ox + b, cy + oy + b], fill=color)
    d.rectangle([cx - r * 0.12, cy - r * 0.2, cx + r * 0.12, cy + r * 0.5], fill=color)
    d.polygon([(cx, cy + r * 0.2), (cx - r * 0.4, cy + r * 1.05), (cx + r * 0.4, cy + r * 1.05)], fill=color)

RED, BLACK = (178, 22, 32), (22, 22, 28)
RANKS = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

def card_face(rank, s, w, h):
    W, H = w * SS, h * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = 3 * SS
    d.rounded_rectangle([m, m, W - m, H - m], radius=14 * SS, fill=(250, 247, 238), outline=(150, 140, 120), width=2 * SS)
    color = RED if s in (1, 2) else BLACK
    label = RANKS[rank]
    # Big corner index: readable from across the table, which is what matters in the game.
    f = font(38 if label != '10' else 32)
    def index(dr):
        # Rank over a small suit, centred on one column in the top-left corner.
        col = m + 26 * SS
        tb = dr.textbbox((0, 0), label, font=f)
        dr.text((col - (tb[0] + tb[2]) / 2, m + 8 * SS - tb[1]), label, font=f, fill=color)
        suit(dr, s, col, m + 66 * SS, 24 * SS, color)
    index(d)
    # The same index upside down in the far corner.
    corner = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    index(ImageDraw.Draw(corner))
    img.alpha_composite(corner.rotate(180))
    cx, cy = W / 2, H / 2
    if rank >= 10:
        # Court cards: a gilt frame with the letter and its suit.
        fw, fh = 58 * SS, 88 * SS
        d.rounded_rectangle([cx - fw / 2, cy - fh / 2, cx + fw / 2, cy + fh / 2], radius=8 * SS, fill=(245, 222, 160),
                            outline=(170, 120, 40), width=3 * SS)
        fl = font(40, SERIF)
        tb = d.textbbox((0, 0), label, font=fl)
        d.text((cx - (tb[2] - tb[0]) / 2 - tb[0], cy - 18 * SS - (tb[3] - tb[1]) / 2 - tb[1]), label, font=fl, fill=color)
        suit(d, s, cx, cy + 22 * SS, 26 * SS, color)
    else:
        suit(d, s, cx, cy, 46 * SS if rank > 0 else 58 * SS, color)
    return img.resize((w, h), Image.LANCZOS)

def card_back(w, h):
    W, H = w * SS, h * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = 3 * SS
    d.rounded_rectangle([m, m, W - m, H - m], radius=14 * SS, fill=(250, 247, 238), outline=(150, 140, 120), width=2 * SS)
    i = 11 * SS
    d.rounded_rectangle([i, i, W - i, H - i], radius=8 * SS, fill=(112, 18, 26))
    step = 16 * SS
    for k in range(-H, W + H, step):
        d.line([(k, i), (k + H, i + H)], fill=(150, 36, 40), width=2 * SS)
        d.line([(k, H - i), (k + H, H - i - H)], fill=(150, 36, 40), width=2 * SS)
    # Re-trim the lattice to the inner panel and add the gilt border and a centre medallion.
    mask = Image.new('L', (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([i, i, W - i, H - i], radius=8 * SS, fill=255)
    base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(base)
    bd.rounded_rectangle([m, m, W - m, H - m], radius=14 * SS, fill=(250, 247, 238), outline=(150, 140, 120), width=2 * SS)
    base.paste(img, (0, 0), mask)
    bd = ImageDraw.Draw(base)
    bd.rounded_rectangle([i, i, W - i, H - i], radius=8 * SS, outline=(212, 170, 80), width=3 * SS)
    cx, cy, r = W / 2, H / 2, 28 * SS
    bd.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(112, 18, 26), outline=(212, 170, 80), width=3 * SS)
    suit(bd, 0, cx, cy, 30 * SS, (212, 170, 80))
    return base.resize((w, h), Image.LANCZOS)

def cards():
    W, H, cols, rows = 2048, 1024, 13, 5
    atlas = Image.new('RGBA', (W, H), (40, 60, 40, 255))
    cw, ch = W / cols, H / rows
    w, h = int(cw) - 2, int(ch) - 2
    for s in range(4):
        for r in range(13):
            atlas.alpha_composite(card_face(r, s, w, h), (int(r * cw) + 1, int(s * ch) + 1))
    atlas.alpha_composite(card_back(w, h), (1, int(4 * ch) + 1))
    atlas.convert('RGB').save(os.path.join(OUT, 'cards.png'), optimize=True)

# ------------------------------------------------------------------ slot symbols
def symbol(k, w, h):
    W, H = w * SS, h * SS
    img = Image.new('RGB', (W, H), (250, 246, 232))
    d = ImageDraw.Draw(img)
    # A soft vertical shade, as if lit from the front of a curved reel.
    for y in range(H):
        t = abs(y / H - 0.5) * 2
        c = int(250 - 60 * t ** 2)
        d.line([(0, y), (W, y)], fill=(c, int(c * 0.98), int(c * 0.92)))
    cx, cy = W / 2, H / 2
    u = min(W, H) / 2
    if k == 0:  # cherry
        d.line([(cx - u * 0.35, cy + u * 0.2), (cx + u * 0.1, cy - u * 0.75)], fill=(40, 110, 30), width=int(u * 0.09))
        d.line([(cx + u * 0.4, cy + u * 0.1), (cx + u * 0.1, cy - u * 0.75)], fill=(40, 110, 30), width=int(u * 0.09))
        d.ellipse([cx + u * 0.05, cy - u * 0.95, cx + u * 0.6, cy - u * 0.6], fill=(60, 150, 40))
        for ox, oy in ((-0.4, 0.38), (0.38, 0.3)):
            x, y, r = cx + ox * u, cy + oy * u, u * 0.36
            d.ellipse([x - r, y - r, x + r, y + r], fill=(190, 16, 30), outline=(90, 0, 10), width=int(u * 0.04))
            d.ellipse([x - r * 0.55, y - r * 0.6, x - r * 0.15, y - r * 0.2], fill=(255, 150, 150))
    elif k == 1:  # lemon
        rx, ry = u * 0.78, u * 0.55
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(250, 214, 30), outline=(170, 120, 0), width=int(u * 0.05))
        d.polygon([(cx - rx * 1.12, cy), (cx - rx * 0.85, cy - ry * 0.3), (cx - rx * 0.85, cy + ry * 0.3)], fill=(250, 214, 30))
        d.polygon([(cx + rx * 1.12, cy), (cx + rx * 0.85, cy - ry * 0.3), (cx + rx * 0.85, cy + ry * 0.3)], fill=(250, 214, 30))
        d.ellipse([cx - rx * 0.5, cy - ry * 0.6, cx - rx * 0.1, cy - ry * 0.3], fill=(255, 245, 170))
    elif k == 2:  # bell
        top, bot = cy - u * 0.8, cy + u * 0.5
        pts = []
        for i in range(21):
            t = i / 20
            y = top + (bot - top) * t
            half = u * (0.18 + 0.5 * t ** 0.7 + (0.2 if t > 0.92 else 0))
            pts.append((cx + half, y))
        pts += [(2 * cx - x, y) for x, y in reversed(pts)]
        d.polygon(pts, fill=(236, 176, 30), outline=(140, 90, 0))
        d.ellipse([cx - u * 0.16, bot - u * 0.05, cx + u * 0.16, bot + u * 0.27], fill=(140, 90, 0))
        d.ellipse([cx - u * 0.1, top - u * 0.16, cx + u * 0.1, top + u * 0.04], fill=(140, 90, 0))
        d.line([(cx - u * 0.25, cy - u * 0.4), (cx - u * 0.32, cy + u * 0.3)], fill=(255, 236, 150), width=int(u * 0.08))
    elif k == 3:  # BAR
        bw, bh = u * 1.45, u * 0.62
        d.rounded_rectangle([cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2], radius=u * 0.1, fill=(24, 24, 30),
                            outline=(200, 200, 210), width=int(u * 0.05))
        f = font(int(u * 0.5 / SS))
        tb = d.textbbox((0, 0), 'BAR', font=f)
        d.text((cx - (tb[2] + tb[0]) / 2, cy - (tb[3] + tb[1]) / 2), 'BAR', font=f, fill=(250, 250, 250))
    elif k == 4:  # diamond
        top, mid, bot, half = cy - u * 0.62, cy - u * 0.25, cy + u * 0.8, u * 0.8
        d.polygon([(cx - half, mid), (cx - half * 0.55, top), (cx + half * 0.55, top), (cx + half, mid), (cx, bot)], fill=(70, 200, 240),
                  outline=(10, 80, 130))
        d.polygon([(cx - half * 0.55, top), (cx, mid), (cx + half * 0.55, top)], fill=(150, 235, 255))
        d.polygon([(cx - half, mid), (cx, mid), (cx, bot)], fill=(40, 150, 210))
        d.line([(cx - half, mid), (cx + half, mid)], fill=(10, 80, 130), width=int(u * 0.03))
    else:  # seven
        f = font(int(u * 1.55 / SS), SERIF)
        tb = d.textbbox((0, 0), '7', font=f)
        x, y = cx - (tb[2] + tb[0]) / 2, cy - (tb[3] + tb[1]) / 2
        for ox in range(-3, 4):
            for oy in range(-3, 4):
                d.text((x + ox * SS * 1.5, y + oy * SS * 1.5), '7', font=f, fill=(60, 0, 0))
        d.text((x, y), '7', font=f, fill=(220, 20, 30))
    # A thin rule between cells, like the gap between symbols on a real reel strip.
    d.line([(0, 0), (W, 0)], fill=(150, 140, 120), width=SS)
    return img.resize((w, h), Image.LANCZOS)

def reel():
    W, H = 256, 1024
    strip = Image.new('RGB', (W, H))
    for k in range(6):
        y0, y1 = round(k * H / 6), round((k + 1) * H / 6)
        strip.paste(symbol(k, W, y1 - y0), (0, y0))
    strip.save(os.path.join(OUT, 'slot_reel.png'), optimize=True)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    cards()
    reel()
    print('wrote', os.path.abspath(OUT))
