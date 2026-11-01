"""Bar del Piu - generador de sprites pixel art.

Genera todos los PNG en Assets/Art/Sprites y un sprite_meta.json con pivotes
y bordes 9-slice que lee PixelArtImporter.cs al importar.

Uso: python Tools/ArtGen/gen_sprites.py
"""
import json
import math
import os
import random

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Sprites")
META = {}


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mix(c1, c2, t):
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


P = {
    "ink": hexc("1A1410"),
    "night1": hexc("2A1F2D"), "night2": hexc("3D2B3A"),
    "sky1": hexc("1F1B33"), "sky2": hexc("2E2546"), "city": hexc("16131F"),
    "wood_dk": hexc("42291F"), "wood": hexc("5A3A2E"), "wood_lt": hexc("7A5038"),
    "green_dk": hexc("1B2B24"), "green": hexc("263B32"), "green_lt": hexc("3A5A49"), "green_hl": hexc("5C8068"),
    "cream_dk": hexc("D9C9A8"), "cream": hexc("F4EBD8"), "white": hexc("FFFFFF"),
    "amber_dk": hexc("A8701F"), "amber": hexc("D99B32"), "amber_lt": hexc("F2C25C"),
    "cafe_dk": hexc("553B26"), "cafe": hexc("795638"),
    "red_dk": hexc("8E3A30"), "red": hexc("C65C4B"),
    "steel_dk": hexc("5E5A60"), "steel": hexc("9C98A0"), "steel_lt": hexc("D8D4DA"),
    "neon": hexc("FF7A6B"), "neon_core": hexc("FFE1D6"), "neon_off": hexc("5A2E30"),
    "mint": hexc("7FE0C8"), "mint_core": hexc("E2FFF6"), "mint_off": hexc("2C4A44"),
    "sweat": hexc("9FD8F0"),
}

BEERS = {
    "rubia": {"letter": "R", "lt": hexc("F5C84C"), "dk": hexc("D9A030"), "foam": hexc("FFF8EA")},
    "ambar": {"letter": "A", "lt": hexc("D2802E"), "dk": hexc("9A5420"), "foam": hexc("FFF1DA")},
    "negra": {"letter": "N", "lt": hexc("4A2C1C"), "dk": hexc("24150E"), "foam": hexc("E9D3B0")},
}

T = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), T)
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            if c[3] == 255 or self.px[x, y][3] == 0:
                self.px[x, y] = c
            else:
                self.px[x, y] = mix(self.px[x, y], (c[0], c[1], c[2], 255), c[3] / 255)

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return T

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.set(xx, yy, c)

    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1):
            self.set(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(y0, y1 + 1):
            self.set(x, y, c)

    def rrect(self, x, y, w, h, r, c):
        for yy in range(h):
            for xx in range(w):
                dx = min(xx, w - 1 - xx)
                dy = min(yy, h - 1 - yy)
                if dx < r and dy < r:
                    if (r - dx - 0.5) ** 2 + (r - dy - 0.5) ** 2 > r * r:
                        continue
                self.set(x + xx, y + yy, c)

    def ellipse(self, cx, cy, rx, ry, c):
        for yy in range(int(cy - ry - 1), int(cy + ry + 2)):
            for xx in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2 <= 1.0:
                    self.set(xx, yy, c)

    def outline(self, c=None, diagonal=False):
        c = c or P["ink"]
        src = [[self.px[x, y][3] > 0 for y in range(self.h)] for x in range(self.w)]
        nb = [(-1, 0), (1, 0), (0, -1), (0, 1)]
        if diagonal:
            nb += [(-1, -1), (1, -1), (-1, 1), (1, 1)]
        for y in range(self.h):
            for x in range(self.w):
                if src[x][y]:
                    continue
                for dx, dy in nb:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx][ny]:
                        self.px[x, y] = c
                        break

    def recolor(self, frm, to):
        for y in range(self.h):
            for x in range(self.w):
                if self.px[x, y] == frm:
                    self.px[x, y] = to

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                c = other.px[x, y]
                if c[3] > 0:
                    self.set(ox + x, oy + y, c)

    def save(self, rel, pivot=None, border=None):
        path = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.save(path)
        m = {}
        if pivot is not None:
            m["pivot"] = [pivot[0], pivot[1]]  # en píxeles desde abajo-izquierda
        if border is not None:
            m["border"] = list(border)  # l, b, r, t
        META[rel.replace("\\", "/")] = m


def pivot_bottom_center(c):
    return (c.w // 2, 0)


# ---------------------------------------------------------------- fuente 5x7
GLYPHS = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "J": ["..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#."],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": [".#.", "##.", ".#.", ".#.", ".#.", ".#.", "###"],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": [".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."],
    "7": ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    "9": [".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."],
    "!": ["#", "#", "#", "#", "#", ".", "#"],
    "¡": ["#", ".", "#", "#", "#", "#", "#"],
    "?": [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
    "¿": ["..#..", ".....", "..#..", ".#...", "#....", "#...#", ".###."],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    ":": [".", ".", "#", ".", ".", "#", "."],
    "-": ["...", "...", "...", "###", "...", "...", "..."],
    "+": [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
    "%": ["##...", "##..#", "...#.", "..#..", ".#...", "#..##", "...##"],
    "$": ["..#..", ".####", "#.#..", ".###.", "..#.#", "####.", "..#.."],
    "'": ["#", "#", ".", ".", ".", ".", "."],
    '"': ["#.#", "#.#", "...", "...", "...", "...", "..."],
    "(": [".#", "#.", "#.", "#.", "#.", "#.", ".#"],
    ")": ["#.", ".#", ".#", ".#", ".#", ".#", "#."],
    "/": ["....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...."],
    "«": [".....", "..#.#", ".#.#.", "#.#..", ".#.#.", "..#.#", "....."],
    "»": [".....", "#.#..", ".#.#.", "..#.#", ".#.#.", "#.#..", "....."],
    "*": [".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."],
}
ACCENTS = {
    "Á": ("A", ["...#.", "..#.."]), "É": ("E", ["...#.", "..#.."]), "Í": ("I", [".#.", "#.."]),
    "Ó": ("O", ["...#.", "..#.."]), "Ú": ("U", ["...#.", "..#.."]), "Ü": ("U", [".....", ".#.#."]),
    "Ñ": ("N", [".##.#", "#.##."]),
}
# Coma: usa la fila de descendente (fila 9 de la celda de 10)
COMMA = [".", ".", ".", ".", ".", ".", "#"]
GLYPH_H = 10  # 2 filas acento + 7 cuerpo + 1 descendente


def glyph_rows(ch):
    """Devuelve 10 filas (arriba->abajo) para un carácter."""
    if ch == ",":
        rows = [".."] * 8 + [".#", "#."]
        return rows
    if ch in ACCENTS:
        base, acc = ACCENTS[ch]
        body = GLYPHS[base]
        w = len(body[0])
        acc = [a[:w].ljust(w, ".") for a in acc]
        return acc + body + ["." * w]
    body = GLYPHS[ch]
    w = len(body[0])
    return ["." * w, "." * w] + body + ["." * w]


def gen_font():
    font = {"height": GLYPH_H, "spacing": 1, "spaceWidth": 3, "glyphs": []}
    chars = list(GLYPHS.keys()) + list(ACCENTS.keys()) + [","]
    for ch in chars:
        rows = glyph_rows(ch)
        w = len(rows[0])
        c = Canvas(w, GLYPH_H)
        for y, row in enumerate(rows):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x, y, P["white"])
        name = "glyph_%04X.png" % ord(ch)
        c.save("Font/" + name, pivot=(0, 0))
        font["glyphs"].append({"character": ch, "file": name, "width": w})
    path = os.path.join(OUT, "Font", "font.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(font, f, ensure_ascii=False, indent=1)


def draw_text(c, text, x, y, color, scale=1, shadow=None, space=3):
    """Dibuja texto con la fuente 5x7. (x, y) = esquina superior izquierda del cuerpo."""
    cx = x
    for ch in text.upper():
        if ch == " ":
            cx += space * scale
            continue
        rows = glyph_rows(ch)
        for gy, row in enumerate(rows):
            for gx, v in enumerate(row):
                if v == "#":
                    for sy in range(scale):
                        for sx in range(scale):
                            px, py = cx + gx * scale + sx, y + (gy - 2) * scale + sy
                            if shadow:
                                c.set(px, py + scale, shadow)
                            c.set(px, py, color)
        cx += (len(rows[0]) + 1) * scale
    return cx - x


def text_width(text, scale=1, space=3):
    w = 0
    for ch in text.upper():
        w += space * scale if ch == " " else (len(glyph_rows(ch)[0]) + 1) * scale
    return w - scale


# ---------------------------------------------------------------- fondo
def gen_background():
    W, H = 384, 216
    c = Canvas(W, H)
    rnd = random.Random(7)
    # pared de tablas verticales, más oscura arriba (bandas con tramado)
    for x in range(W):
        plank = x // 12
        base = [P["wood"], hexc("553629")][plank % 2]
        for y in range(H):
            col = base
            if x % 12 == 0:
                col = P["wood_dk"]
            elif x % 12 == 1:
                col = mix(base, P["wood_lt"], 0.35)
            band = 0 if y > 90 else (1 if y > 45 else 2)
            if y in (44, 45, 89, 90) and (x + y) % 2 == 0:
                band += 1 if band < 2 else 0
            col = mix(col, P["night1"], [0.0, 0.3, 0.55][min(band, 2)])
            c.set(x, y, col)
        # vetas
        for _ in range(3):
            gy = rnd.randrange(0, H)
            if x % 12 > 2:
                c.set(x, gy, mix(c.get(x, gy), P["wood_dk"], 0.5))

    # guirnalda de luces
    for i in range(0, W, 1):
        y = int(22 + 6 * math.sin(math.pi * ((i % 96) / 96.0)))
        c.set(i, y, P["ink"])
        if i % 16 == 8:
            c.set(i, y + 1, P["ink"])
            c.rrect(i - 1, y + 2, 3, 3, 1, P["amber_lt"])
            c.set(i, y + 2, P["cream"])

    # ventanas con ciudad nocturna
    def window(x0, y0, w, h, moon):
        c.rect(x0 - 3, y0 - 3, w + 6, h + 6, P["cafe_dk"])
        c.rect(x0 - 2, y0 - 2, w + 4, 1, P["cafe"])
        for yy in range(h):
            col = P["sky1"] if yy < h * 0.55 else P["sky2"]
            if int(h * 0.55) - 1 <= yy <= int(h * 0.55) and (yy % 2 == 0):
                col = P["sky2"]
            c.hline(x0, x0 + w - 1, y0 + yy, col)
        for _ in range(10):
            c.set(x0 + rnd.randrange(w), y0 + rnd.randrange(int(h * 0.5)), P["cream_dk"])
        if moon:
            c.ellipse(x0 + w - 16, y0 + 10, 5, 5, P["cream"])
            c.ellipse(x0 + w - 14, y0 + 9, 4, 4, P["sky1"])
        bx = x0
        while bx < x0 + w:
            bw = rnd.randrange(8, 16)
            bh = rnd.randrange(12, int(h * 0.6))
            for yy in range(y0 + h - bh, y0 + h):
                for xx in range(bx, min(bx + bw, x0 + w)):
                    c.set(xx, yy, P["city"])
            for wy in range(y0 + h - bh + 3, y0 + h - 2, 4):
                for wx in range(bx + 2, min(bx + bw, x0 + w) - 1, 3):
                    if rnd.random() < 0.35:
                        c.set(wx, wy, P["amber_dk"] if rnd.random() < 0.7 else P["amber_lt"])
            bx += bw + rnd.randrange(0, 3)
        # marco y cruz
        c.rect(x0 + w // 2 - 1, y0, 2, h, P["cafe_dk"])
        c.rect(x0, y0 + h // 2 - 1, w, 2, P["cafe_dk"])
        c.rect(x0 - 4, y0 + h + 3, w + 8, 3, P["cafe"])
        c.hline(x0 - 4, x0 + w + 3, y0 + h + 3, P["wood_lt"])

    window(44, 46, 76, 52, moon=True)
    window(264, 46, 76, 52, moon=False)

    # pizarra de menú bajo el neón
    bx, by, bw, bh = 162, 82, 60, 26
    c.rect(bx - 2, by - 2, bw + 4, bh + 4, P["cafe"])
    c.rect(bx, by, bw, bh, hexc("2B332E"))
    for i, (yy, lw) in enumerate([(by + 4, 34), (by + 10, 30), (by + 16, 38)]):
        for xx in range(bx + 5, bx + 5 + lw):
            if (xx * 7 + i) % 5 != 0:
                c.set(xx, yy, mix(P["cream"], hexc("2B332E"), 0.25))
        list(BEERS.values())[i]
        c.rect(bx + bw - 12, yy - 1, 3, 3, list(BEERS.values())[i]["lt"])
        c.set(bx + bw - 7, yy, P["cream_dk"])
        c.set(bx + bw - 6, yy, P["cream_dk"])
    c.hline(bx + 4, bx + bw - 5, by + bh - 4, mix(P["cream"], hexc("2B332E"), 0.5))

    # cuadros pequeños
    def frame(x0, y0, w, h, inner):
        c.rect(x0 - 2, y0 - 2, w + 4, h + 4, P["amber_dk"])
        c.hline(x0 - 2, x0 + w + 1, y0 - 2, P["amber"])
        c.rect(x0, y0, w, h, inner)
    frame(136, 86, 14, 18, P["green"])
    c.rrect(139, 91, 8, 10, 1, P["amber"])
    c.rect(139, 89, 8, 3, P["cream"])
    frame(234, 86, 14, 18, P["red_dk"])
    c.ellipse(241, 95, 4, 4, P["amber_lt"])

    # zócalo verde detrás de los clientes
    top = 114
    c.rect(0, top, W, H - top, P["green"])
    c.hline(0, W - 1, top, P["amber_lt"])
    c.hline(0, W - 1, top + 1, P["amber_dk"])
    c.hline(0, W - 1, top + 2, P["green_dk"])
    for px0 in range(4, W, 48):
        c.rect(px0, top + 8, 40, 40, P["green_dk"])
        c.rect(px0 + 1, top + 9, 38, 38, mix(P["green"], P["green_dk"], 0.4))
        c.hline(px0 + 1, px0 + 38, top + 9, P["green_lt"])
    c.save("Background/bg_bar_room.png")


def gen_counter():
    W = 384
    c = Canvas(W, 14)
    rows = [P["green_hl"], P["green_lt"], P["green_lt"], P["green"], P["green"], P["green"],
            P["amber_lt"], P["amber_dk"], P["green_dk"], P["green_dk"], P["green_dk"], P["ink"]]
    for y, col in enumerate(rows):
        c.hline(0, W - 1, y, col)
    rnd = random.Random(3)
    for _ in range(140):
        x, y = rnd.randrange(W), rnd.randrange(1, 6)
        c.set(x, y, mix(c.get(x, y), P["green_hl"], 0.4))
    for x in range(0, W, 2):  # sombra tramada bajo el borde
        c.set(x, 12, mix(P["ink"], T, 0.0)[:3] + (160,))
        c.set(x + 1, 13, P["ink"][:3] + (90,))
    c.save("Bar/counter.png", pivot=(W // 2, 14))

    # bajo barra: estantes oscuros
    H = 62
    u = Canvas(W, H)
    base = mix(P["night1"], P["green_dk"], 0.5)
    u.rect(0, 0, W, H, base)
    rnd = random.Random(11)
    for sy in (22, 46):
        u.hline(0, W - 1, sy, P["cafe_dk"])
        u.hline(0, W - 1, sy + 1, mix(P["cafe_dk"], P["ink"], 0.5))
        x = 6
        while x < W - 8:
            kind = rnd.random()
            if kind < 0.6:  # vaso boca abajo
                for yy in range(10):
                    hw = 3 + yy // 5
                    u.set(x - hw, sy - 1 - yy, mix(base, P["cream_dk"], 0.18))
                    u.set(x + hw, sy - 1 - yy, mix(base, P["cream_dk"], 0.18))
                u.hline(x - 4, x + 4, sy - 1, mix(base, P["cream_dk"], 0.25))
                x += 12
            else:  # botella
                bc = mix(base, rnd.choice([P["green_lt"], P["amber_dk"], P["red_dk"]]), 0.45)
                u.rect(x - 2, sy - 12, 5, 12, bc)
                u.rect(x - 1, sy - 17, 3, 5, bc)
                u.set(x - 1, sy - 11, mix(bc, P["cream"], 0.25))
                x += 10
    u.hline(0, W - 1, 0, P["ink"])
    u.save("Bar/underbar.png", pivot=(W // 2, H))


def gen_lamps():
    c = Canvas(14, 30)
    c.vline(7, 0, 15, P["ink"])
    c.rrect(5, 14, 4, 3, 1, P["amber_dk"])
    for i, y in enumerate(range(17, 25)):
        hw = 2 + i // 2
        c.hline(7 - hw, 6 + hw, y, P["amber"] if i < 6 else P["amber_dk"])
        c.set(7 - hw, y, P["amber_lt"])
    c.hline(1, 12, 25, P["amber_dk"])
    c.rrect(4, 25, 6, 4, 2, P["cream"])
    c.set(6, 26, P["white"])
    c.outline()
    c.save("Bar/lamp.png", pivot=(7, 30))

    # ampolleta rota: sin vidrio, con el casquillo y un trozo quebrado
    b = Canvas(14, 30)
    b.vline(7, 0, 15, P["ink"])
    b.rrect(5, 14, 4, 3, 1, P["amber_dk"])
    for i, y in enumerate(range(17, 25)):
        hw = 2 + i // 2
        b.hline(7 - hw, 6 + hw, y, mix(P["amber"], P["ink"], 0.35) if i < 6 else P["amber_dk"])
    b.hline(1, 12, 25, P["amber_dk"])
    b.rect(5, 25, 4, 2, P["steel_dk"])  # casquillo
    b.set(4, 27, P["cream_dk"]); b.set(9, 26, P["cream_dk"]); b.set(8, 28, P["cream_dk"])  # vidrio roto
    b.outline()
    b.save("Bar/lamp_broken.png", pivot=(7, 30))


def gen_neon():
    def sign(text, scale, col, core, off, name, board=True):
        tw = text_width(text, scale)
        W = tw + 16
        H = 7 * scale + 12
        for state in ("on", "off"):
            c = Canvas(W, H)
            if board:
                c.rrect(0, 0, W, H, 3, P["wood_dk"])
                c.rrect(1, 1, W - 2, H - 2, 2, P["night1"])
                for sx, sy in ((3, 3), (W - 4, 3), (3, H - 4), (W - 4, H - 4)):
                    c.set(sx, sy, P["steel"])
            tube = mix(col, core, 0.45) if state == "on" else off
            tmp = Canvas(W, H)
            draw_text(tmp, text, 8, 6, tube, scale)
            if state == "on":
                tmp.outline(col[:3] + (110,), diagonal=True)
            c.paste(tmp, 0, 0)
            c.save("Bar/%s_%s.png" % (name, state))

    sign("BAR DEL PIU", 2, P["neon"], P["neon_core"], P["neon_off"], "neon_sign")
    sign("ABIERTO", 1, P["mint"], P["mint_core"], P["mint_off"], "neon_open")


# ---------------------------------------------------------------- llaves
TOWER_W, TOWER_H = 80, 52
SPOUT_X = [16, 40, 64]  # 24 px entre llaves: columnas anchas para tocar en celular
HANDLE_W, HANDLE_H = 18, 30


def gen_taps():
    c = Canvas(TOWER_W, TOWER_H)
    # bandeja de goteo
    c.rrect(0, TOWER_H - 6, TOWER_W, 6, 1, P["steel_dk"])
    c.hline(1, TOWER_W - 2, TOWER_H - 6, P["steel_lt"])
    for x in range(3, TOWER_W - 3, 3):
        c.set(x, TOWER_H - 4, P["ink"])
    # postes
    for px in (3, TOWER_W - 7):
        c.rect(px, 10, 4, TOWER_H - 16, P["amber"])
        c.vline(px, 10, TOWER_H - 7, P["amber_lt"])
        c.vline(px + 3, 10, TOWER_H - 7, P["amber_dk"])
    # barra horizontal
    c.rrect(0, 2, TOWER_W, 9, 3, P["amber"])
    c.hline(2, TOWER_W - 3, 3, P["amber_lt"])
    c.hline(2, TOWER_W - 3, 9, P["amber_dk"])
    # caños
    for sx in SPOUT_X:
        c.rect(sx - 1, 11, 3, 5, P["steel"])
        c.vline(sx - 1, 11, 15, P["steel_lt"])
        c.rect(sx - 2, 16, 5, 2, P["steel_dk"])
    c.outline()
    c.save("Taps/tap_tower.png", pivot=(TOWER_W // 2, 0))
    meta = {"spouts_px": [[x - TOWER_W // 2, TOWER_H - 18] for x in SPOUT_X],
            "handle_px": [[x - TOWER_W // 2, TOWER_H - 2] for x in SPOUT_X],
            "tray_top_px": 6, "column_w_px": 22, "handle_h_px": HANDLE_H}
    meta["spouts_flat"] = [v for pair in meta["spouts_px"] for v in pair]
    meta["handles_flat"] = [v for pair in meta["handle_px"] for v in pair]
    with open(os.path.join(OUT, "Taps", "tap_tower.json"), "w") as f:
        json.dump(meta, f, indent=1)

    # manijas grandes: insignia 12x13 con la letra completa 5x7
    for key, b in BEERS.items():
        for state in ("idle", "sel", "pull"):
            h = Canvas(HANDLE_W, HANDLE_H)
            pull = state == "pull"
            top = 2 if not pull else 5
            badge_w, badge_h = 12, 13
            ox = (HANDLE_W - badge_w) // 2 + (2 if pull else 0)
            # palo de madera (se inclina al tirar)
            for y in range(top + badge_h - 1, HANDLE_H - 1):
                off = ((HANDLE_H - 1 - y) // 5) if pull else 0
                h.rect(6 + off, y, 6, 1, P["cafe_dk"])
                h.set(6 + off, y, P["cafe"])
                h.set(7 + off, y, P["cafe"])
            # insignia
            h.rrect(ox, top, badge_w, badge_h, 3, b["lt"])
            h.hline(ox + 2, ox + badge_w - 3, top + badge_h - 1, b["dk"])
            h.hline(ox + 2, ox + badge_w - 3, top, mix(b["lt"], P["white"], 0.4))
            letter_col = P["ink"] if key != "negra" else P["cream"]
            draw_text(h, b["letter"], ox + 4, top + 3, letter_col)
            h.outline()
            if state == "sel":
                h.outline(P["cream"])
            h.save("Taps/handle_%s_%s.png" % (key, state), pivot=(HANDLE_W // 2, 0))


# ---------------------------------------------------------------- vaso
GLASS_W, GLASS_H = 16, 24


def glass_row_span(y):
    """Interior del vaso para la fila y (arriba->abajo). Pinta cónica."""
    t = y / (GLASS_H - 1)
    half = 6.5 - 2.0 * t
    return int(round(8 - half)), int(round(7 + half))


def gen_glass():
    back = Canvas(GLASS_W, GLASS_H)
    mask = Canvas(GLASS_W, GLASS_H)
    front = Canvas(GLASS_W, GLASS_H)
    edge = hexc("FFF6E6", 150)
    inner = hexc("FFF6E6", 38)
    inner_top, inner_bot = 1, GLASS_H - 4
    for y in range(GLASS_H):
        x0, x1 = glass_row_span(y)
        if y == 0:
            back.hline(x0, x1, y, hexc("FFF6E6", 200))
            continue
        if y >= GLASS_H - 3:  # base gruesa
            back.hline(x0, x1, y, hexc("FFF6E6", 120 if y < GLASS_H - 1 else 170))
            continue
        back.set(x0, y, edge)
        back.set(x1, y, edge)
        back.hline(x0 + 1, x1 - 1, y, inner)
        mask.hline(x0 + 1, x1 - 1, y, P["white"])
        # brillo izquierdo
        if 3 <= y <= GLASS_H - 7:
            front.set(x0 + 2, y, hexc("FFFFFF", 150))
        if 5 <= y <= 9:
            front.set(x1 - 2, y, hexc("FFFFFF", 90))
    front.hline(glass_row_span(0)[0] + 1, glass_row_span(0)[1] - 1, 1, hexc("FFFFFF", 90))
    # contorno exterior suave
    outl = Canvas(GLASS_W, GLASS_H)
    outl.paste(back, 0, 0)
    back.save("Glass/glass_back.png", pivot=(GLASS_W // 2, 0))
    mask.save("Glass/glass_mask.png", pivot=(GLASS_W // 2, 0))
    front.save("Glass/glass_front.png", pivot=(GLASS_W // 2, 0))
    meta = {"innerBottomPx": GLASS_H - 1 - inner_bot, "innerTopPx": GLASS_H - 1 - inner_top,
            "width": GLASS_W, "height": GLASS_H}
    with open(os.path.join(OUT, "Glass", "glass.json"), "w") as f:
        json.dump(meta, f, indent=1)

    # chorro: 3 cuadros en escala de grises (se tiñe por color)
    for i in range(3):
        s = Canvas(2, 16)
        for y in range(16):
            s.set(0, y, hexc("E8E8E8"))
            s.set(1, y, hexc("C8C8C8"))
            if (y + i * 5) % 8 in (0, 1):
                s.set(0, y, P["white"])
        s.save("Glass/pour_stream_%d.png" % i, pivot=(1, 16))


# ---------------------------------------------------------------- personajes
SKINS = [(hexc("F1C9A5"), hexc("D9A784")), (hexc("D9A47E"), hexc("B9845F")),
         (hexc("B07850"), hexc("8D5C3A")), (hexc("7A4E33"), hexc("5E3A25"))]
HAIRS = [hexc("2B1D16"), hexc("6B3E22"), hexc("C9A15A"), hexc("8A8A8A"), hexc("A4402E")]
SHIRTS = [(P["red"], P["red_dk"]), (hexc("4E6E9E"), hexc("36507A")), (P["amber"], P["amber_dk"]),
          (P["green_lt"], P["green"]), (hexc("8C5E9A"), hexc("6A4276")), (P["cream_dk"], hexc("B3A27F"))]

CUSTOMERS = [
    # skin, hair, style, shirt, extra
    (0, 0, "short", 1, None), (2, 1, "long", 0, None), (1, 4, "bun", 3, "glasses"),
    (3, 0, "afro", 2, None), (0, 2, "cap", 4, None), (2, 3, "bald", 5, "beard"),
    (1, 1, "long", 4, "glasses"), (3, 0, "short", 0, "beard"),
]
CUST_H = 54  # el torso largo queda oculto detrás de la barra
EXPRESSIONS = ["neutral", "feliz", "impaciente", "enojado"]


def draw_face(c, cx, ey, my, expr, skin, ink, scale_w=1):
    """Ojos en (cx-3, ey) y (cx+4, ey); boca centrada en my."""
    lx, rx = cx - 3 * scale_w, cx + 3 * scale_w + 1
    if expr == "feliz":
        for ex in (lx, rx):
            c.set(ex - 1, ey + 1, ink); c.set(ex, ey, ink); c.set(ex + 1, ey + 1, ink)
        c.hline(cx - 1, cx + 2, my, ink)
        c.set(cx - 2, my - 1, ink); c.set(cx + 3, my - 1, ink)
        c.hline(cx, cx + 1, my + 1, P["red_dk"])
        c.set(lx - 2, my - 2, P["red"][:3] + (140,)); c.set(rx + 2, my - 2, P["red"][:3] + (140,))
    elif expr == "impaciente":
        for ex in (lx, rx):
            c.set(ex, ey, ink); c.set(ex, ey + 1, ink)
        c.hline(lx - 1, lx + 1, ey - 2, ink)
        c.hline(rx - 1, rx + 1, ey - 2, ink)
        c.set(cx - 1, my, ink); c.set(cx, my + 1, ink); c.set(cx + 1, my, ink); c.set(cx + 2, my + 1, ink)
    elif expr == "enojado":
        for ex in (lx, rx):
            c.set(ex, ey, ink); c.set(ex, ey + 1, ink)
        c.set(lx - 1, ey - 3, ink); c.set(lx, ey - 2, ink); c.set(lx + 1, ey - 1, ink)
        c.set(rx + 1, ey - 3, ink); c.set(rx, ey - 2, ink); c.set(rx - 1, ey - 1, ink)
        c.hline(cx - 1, cx + 2, my, ink)
        c.set(cx - 2, my + 1, ink); c.set(cx + 3, my + 1, ink)
    elif expr == "sorpresa":
        for ex in (lx, rx):
            c.rect(ex, ey - 1, 1, 3, ink)
        c.hline(lx - 1, lx + 1, ey - 3, ink)
        c.hline(rx - 1, rx + 1, ey - 3, ink)
        c.rrect(cx - 1, my - 1, 4, 3, 1, ink)
        c.hline(cx, cx + 1, my, P["red_dk"])
    else:
        for ex in (lx, rx):
            c.set(ex, ey, ink); c.set(ex, ey + 1, ink)
        c.hline(cx - 1, cx + 2, my, mix(skin, ink, 0.7))


def gen_customer(idx, spec):
    skin_i, hair_i, style, shirt_i, extra = spec
    skin, skin_dk = SKINS[skin_i]
    hair = HAIRS[hair_i]
    hair_dk = mix(hair, P["ink"], 0.35)
    shirt, shirt_dk = SHIRTS[shirt_i]
    for expr in EXPRESSIONS:
        c = Canvas(32, CUST_H)
        face = skin if expr != "enojado" else mix(skin, P["red"], 0.25)
        face_dk = skin_dk if expr != "enojado" else mix(skin_dk, P["red_dk"], 0.25)
        # pelo trasero
        if style == "long":
            c.rrect(8, 7, 16, 24, 4, hair)
            c.vline(23, 10, 29, hair_dk)
        if style == "afro":
            c.ellipse(16, 12, 10, 9, hair)
            c.ellipse(18, 13, 7, 7, hair_dk)
            c.ellipse(15, 11, 8, 7, hair)
        # torso
        c.rrect(4, 27, 24, CUST_H - 27 + 4, 4, shirt)
        c.rect(22, 29, 5, CUST_H - 29, shirt_dk)
        c.vline(16, 31, CUST_H - 1, shirt_dk)
        # cuello
        c.rect(13, 22, 6, 6, face_dk)
        c.set(15, 28, face_dk); c.set(16, 28, face_dk); c.set(16, 29, face_dk)
        # cabeza
        c.rrect(9, 7, 14, 16, 4, face)
        c.vline(21, 10, 19, face_dk)
        c.hline(12, 19, 22, face_dk)
        c.rect(8, 14, 1, 3, face_dk)
        c.rect(23, 14, 1, 3, face_dk)
        # pelo delantero
        if style in ("short", "bun"):
            c.rrect(9, 5, 14, 6, 3, hair)
            c.rect(9, 8, 2, 5, hair)
            c.rect(21, 8, 2, 4, hair)
            c.hline(12, 20, 10, hair_dk)
            if style == "bun":
                c.ellipse(16, 4, 4, 3, hair)
                c.hline(14, 18, 5, hair_dk)
        elif style == "long":
            c.rrect(9, 5, 14, 6, 3, hair)
            c.rect(9, 8, 3, 8, hair)
            c.rect(20, 8, 3, 8, hair)
            c.set(13, 10, hair); c.set(14, 11, hair)
        elif style == "afro":
            c.rrect(9, 5, 14, 5, 2, hair)
        elif style == "cap":
            cap, cap_dk = SHIRTS[(shirt_i + 2) % len(SHIRTS)]
            c.rrect(9, 4, 14, 7, 3, cap)
            c.hline(10, 21, 9, cap_dk)
            c.rect(20, 9, 7, 2, cap_dk)
            c.rect(9, 10, 2, 3, hair)
        if extra == "beard":
            c.rrect(10, 17, 12, 7, 2, hair)
            c.rect(14, 19, 4, 1, face)
        draw_face(c, 15, 14, 19, expr, face, P["ink"])
        if extra == "beard":
            c.hline(14, 17, 19, mix(face, P["ink"], 0.6) if expr == "neutral" else c.get(14, 19))
        if extra == "glasses":
            for gx in (11, 18):
                c.rect(gx, 13, 4, 1, P["ink"])
                c.set(gx, 14, P["ink"]); c.set(gx + 3, 14, P["ink"])
                c.hline(gx, gx + 3, 15, P["ink"])
                c.set(gx + 1, 14, hexc("FFFFFF", 120))
            c.hline(15, 17, 13, P["ink"])
        if expr == "impaciente":
            c.set(24, 9, P["sweat"]); c.rect(24, 10, 2, 2, P["sweat"]); c.set(24, 10, P["white"])
        if expr == "enojado":
            for (vx, vy) in ((24, 5), (25, 4), (26, 5), (25, 6)):
                c.set(vx, vy, P["red"])
        c.outline()
        c.save("Characters/Customers/customer_%d_%s.png" % (idx, expr), pivot=(16, 0))


def gen_staff():
    """Retratos provisorios de Pipe y Nata (48x48) para diálogos."""
    staff = {
        "pipe": dict(skin=SKINS[1], hair=HAIRS[0], style="short", towel=True, pencil=False, letter="P"),
        "nata": dict(skin=SKINS[0], hair=HAIRS[1], style="pony", towel=False, pencil=True, letter="N"),
    }
    for name, s in staff.items():
        skin, skin_dk = s["skin"]
        hair, hair_dk = s["hair"], mix(s["hair"], P["ink"], 0.35)
        for expr in ("neutral", "feliz", "sorpresa", "ebrio"):
            c = Canvas(48, 48)
            if s["style"] == "pony":
                c.rrect(32, 12, 8, 20, 4, hair)
                c.vline(38, 14, 30, hair_dk)
                c.rrect(11, 9, 26, 14, 6, hair)
            # torso: camisa crema + delantal verde
            c.rrect(6, 34, 36, 14, 6, P["cream_dk"])
            c.rrect(12, 36, 24, 12, 2, P["green"])
            c.vline(13, 36, 47, P["green_lt"])
            c.rect(14, 34, 2, 3, P["green"]); c.rect(32, 34, 2, 3, P["green"])
            c.rrect(21, 40, 7, 6, 2, P["amber"])
            c.set(23, 42, P["ink"]); c.set(24, 42, P["ink"]); c.set(23, 43, P["ink"]); c.set(23, 44, P["ink"])
            if s["towel"]:
                c.rect(30, 33, 8, 6, P["cream"])
                c.hline(30, 37, 35, P["red"]); c.hline(30, 37, 37, P["red"])
            # cuello y cabeza
            c.rect(19, 29, 10, 6, skin_dk)
            c.rrect(13, 9, 22, 23, 7, skin)
            c.vline(33, 14, 27, skin_dk)
            c.hline(18, 30, 31, skin_dk)
            c.rect(11, 18, 2, 5, skin_dk); c.rect(35, 18, 2, 5, skin_dk)
            # pelo
            if s["style"] == "short":
                c.rrect(12, 6, 24, 9, 4, hair)
                c.rect(12, 12, 3, 6, hair); c.rect(33, 12, 3, 5, hair)
                c.hline(16, 31, 14, hair_dk)
                c.set(22, 15, hair); c.set(23, 15, hair)
            else:
                c.rrect(12, 6, 24, 9, 4, hair)
                c.rect(12, 12, 3, 9, hair); c.rect(33, 12, 2, 6, hair)
                c.rect(15, 13, 6, 2, hair)
                c.hline(16, 31, 8, mix(hair, P["cream"], 0.25))
            if s["pencil"]:
                c.rect(35, 12, 2, 8, P["amber"]); c.set(35, 20, P["ink"]); c.rect(35, 11, 2, 1, P["red"])
            # cara (más grande: ojos de 2x2)
            ey, my = 20, 26
            ink = P["ink"]
            lx, rx = 19, 28
            if expr == "feliz":
                for ex in (lx, rx):
                    c.set(ex - 1, ey + 1, ink); c.set(ex, ey, ink); c.set(ex + 1, ey, ink); c.set(ex + 2, ey + 1, ink)
                c.hline(21, 26, my, ink)
                c.hline(22, 25, my + 1, P["red_dk"])
                c.set(20, my - 1, ink); c.set(27, my - 1, ink)
                c.rect(16, 24, 2, 1, P["red"][:3] + (150,)); c.rect(30, 24, 2, 1, P["red"][:3] + (150,))
            elif expr == "ebrio":
                # ojos entrecerrados, mejillas muy rojas y sonrisa torcida
                for ex in (lx, rx):
                    c.hline(ex - 1, ex + 2, ey + 1, ink)
                    c.hline(ex, ex + 1, ey, mix(skin, ink, 0.45))
                c.rect(15, 23, 4, 2, P["red"][:3] + (190,)); c.rect(29, 23, 4, 2, P["red"][:3] + (190,))
                c.set(21, my, ink); c.set(22, my + 1, ink); c.set(23, my + 1, ink); c.set(24, my, ink)
                c.set(25, my - 1, ink); c.set(26, my - 1, ink)
                c.rect(33, 12, 2, 3, P["sweat"]); c.set(33, 12, P["white"])  # gota
            elif expr == "sorpresa":
                for ex in (lx, rx):
                    c.rect(ex, ey - 1, 2, 3, ink)
                    c.hline(ex - 1, ex + 2, ey - 4, hair_dk)
                c.rrect(22, my - 1, 4, 4, 1, ink)
                c.hline(23, 24, my + 1, P["red_dk"])
            else:
                for ex in (lx, rx):
                    c.rect(ex, ey, 2, 2, ink)
                    c.set(ex, ey, P["white"])
                    c.hline(ex - 1, ex + 2, ey - 3, hair_dk)
                c.hline(22, 25, my, mix(skin, ink, 0.7))
                c.set(21, my - 1, mix(skin, ink, 0.7)); c.set(26, my - 1, mix(skin, ink, 0.7))
            c.outline()
            c.save("Characters/Staff/%s_%s.png" % (name, expr), pivot=(24, 0))


# ---------------------------------------------------------------- UI
def gen_ui():
    # panel crema 9-slice
    p = Canvas(16, 16)
    p.rrect(0, 1, 16, 15, 3, P["cafe_dk"])
    p.rrect(0, 0, 16, 15, 3, P["cafe"])
    p.rrect(1, 1, 14, 13, 2, P["cream"])
    p.hline(3, 12, 13, P["cream_dk"])
    p.save("UI/panel_cream.png", border=(5, 5, 5, 5))
    # panel oscuro 9-slice (HUD)
    d = Canvas(16, 16)
    d.rrect(0, 0, 16, 16, 3, P["ink"][:3] + (220,))
    d.rrect(1, 1, 14, 14, 2, P["green_dk"][:3] + (235,))
    d.hline(3, 12, 1, P["green_lt"])
    d.save("UI/panel_dark.png", border=(5, 5, 5, 5))
    # botones
    for state in ("normal", "pressed"):
        b = Canvas(24, 14)
        dy = 0 if state == "normal" else 2
        b.rrect(0, 2, 24, 12, 3, P["amber_dk"])
        b.rrect(0, dy, 24, 12, 3, P["amber"])
        b.hline(3, 20, dy + 1, P["amber_lt"])
        b.outline()
        b.save("UI/button_%s.png" % state, border=(5, 5, 5, 5))

    # iconos 11x11
    def icon(name, fn):
        ic = Canvas(11, 11)
        fn(ic)
        ic.outline()
        ic.save("UI/icon_%s.png" % name)

    def clock(c):
        c.ellipse(5.5, 5.5, 5, 5, P["cream"])
        c.vline(5, 2, 5, P["ink"]); c.hline(5, 7, 5, P["ink"])
        c.set(5, 1, P["cafe"])

    def coin(c):
        c.ellipse(5.5, 5.5, 5, 5, P["amber_dk"])
        c.ellipse(5.0, 5.0, 4, 4, P["amber"])
        c.vline(5, 3, 7, P["amber_lt"]); c.set(4, 3, P["amber_lt"])

    def star(c):
        pts = [".....#.....", "....###....", "....###....", "###########", ".#########.",
               "..#######..", "...#####...", "..###.###..", "..##...##..", ".##.....##.", "..........."]
        for y, row in enumerate(pts):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x, y, P["amber_lt"] if y < 5 else P["amber"])

    def flame(c):
        pts = ["....#......", "...##......", "...###.....", "..####..#..", "..#####.##.",
               ".#########.", ".####o####.", ".###ooo###.", ".###ooo###.", "..##ooo##..", "...#####..."]
        for y, row in enumerate(pts):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x, y, P["red"] if y < 6 else P["amber"])
                elif v == "o":
                    c.set(x, y, P["amber_lt"])

    def err_empty(c):
        c.ellipse(5.5, 5.5, 5, 5, P["cafe_dk"][:3] + (200,))
        c.ellipse(5.5, 5.5, 4, 4, P["night2"][:3] + (200,))

    def err_full(c):
        c.ellipse(5.5, 5.5, 5, 5, P["red_dk"])
        c.ellipse(5.5, 5.5, 4, 4, P["red"])
        for i in range(5):
            c.set(3 + i, 3 + i, P["cream"]); c.set(7 - i, 3 + i, P["cream"])

    def heart(c):
        pts = [".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...."]
        for y, row in enumerate(pts):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x + 1, y + 1, P["red"])
        c.set(2, 2, P["cream"])

    def sound_on(c):
        for y, row in enumerate(["...#.......", "..##...#...", "####.#..#..", "####..#.#..", "####..#.#..",
                                 "####.#..#..", "..##...#...", "...#.......", "...........", "...........", "..........."]):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x, y + 1, P["cream"])

    def sound_off(c):
        for y, row in enumerate(["...#.......", "..##.......", "####.#...#.", "####..#.#..", "####...#...",
                                 "####..#.#..", "..##.#...#.", "...#.......", "...........", "...........", "..........."]):
            for x, v in enumerate(row):
                if v == "#":
                    c.set(x, y + 1, P["red"] if x > 4 else P["cream"])

    for n, f in (("clock", clock), ("coin", coin), ("star", star), ("flame", flame),
                 ("sound_on", sound_on), ("sound_off", sound_off),
                 ("error_empty", err_empty), ("error_full", err_full), ("heart", heart)):
        icon(n, f)

    # flecha guía del tutorial (apunta hacia abajo)
    ar = Canvas(11, 10)
    for y, row in enumerate(["..#######..", "..#######..", "..#######..", "###########", ".#########.",
                             "..#######..", "...#####...", "....###....", ".....#.....", "..........."]):
        for x, v in enumerate(row):
            if v == "#":
                ar.set(x, y, P["amber_lt"] if x < 5 else P["amber"])
    ar.outline()
    ar.save("UI/arrow_down.png", pivot=(5, 0))

    # barra de paciencia
    bar = Canvas(20, 5)
    bar.rrect(0, 0, 20, 5, 1, P["ink"])
    bar.rect(1, 1, 18, 3, P["night2"])
    bar.save("UI/patience_frame.png", pivot=(10, 0))
    w = Canvas(1, 1)
    w.set(0, 0, P["white"])
    w.save("UI/px_white.png", pivot=(0, 0))

    # globos de pedido con mini vaso + letra
    for key, b in BEERS.items():
        g = Canvas(26, 22)
        g.rrect(0, 0, 26, 17, 4, P["cream"])
        g.hline(3, 22, 15, P["cream_dk"])
        for i in range(4):  # cola
            g.hline(5, 9 - i, 17 + i, P["cream"])
        # mini pinta 7x11
        for y in range(11):
            x0 = 4 + (1 if y > 7 else 0)
            x1 = 10 - (1 if y > 7 else 0)
            col = b["foam"] if y < 3 else (b["lt"] if y < 7 else b["dk"])
            g.hline(x0, x1, 3 + y, col)
            g.set(x0, 3 + y, mix(col, P["ink"], 0.25))
        g.set(5, 6, P["white"])
        letter_col = P["ink"]
        draw_text(g, b["letter"], 15, 5, letter_col)
        g.outline(P["cafe_dk"])
        g.save("UI/order_%s.png" % key, pivot=(7, 0))

    # logo
    title, sub = "BAR DEL PIU", "UNA NOCHE EN LA BARRA"
    tw = text_width(title, 3, space=6)
    L = Canvas(tw + 12, 46)
    tmp = Canvas(L.w, L.h)
    draw_text(tmp, title, 6, 4, P["amber"], 3, space=6)
    for y in range(L.h):
        for x in range(L.w):
            if tmp.get(x, y)[3]:
                L.set(x, y + 3, P["cafe_dk"])
    for y in range(L.h):
        for x in range(L.w):
            if tmp.get(x, y)[3]:
                L.set(x, y, P["amber_lt"] if (y - 4) % 21 < 6 else P["amber"])
    L.outline(P["ink"], diagonal=True)
    sw = text_width(sub)
    draw_text(L, sub, (L.w - sw) // 2, 34, P["cream"], 1, shadow=P["ink"])
    L.save("UI/logo.png")


# ---------------------------------------------------------------- partículas
def gen_fx():
    f = Canvas(3, 3)
    f.rect(0, 0, 3, 3, P["white"]); f.set(0, 0, T); f.set(2, 0, T); f.set(0, 2, T); f.set(2, 2, T)
    f.save("FX/fx_foam.png")
    b = Canvas(3, 3)
    for x, y in ((1, 0), (0, 1), (2, 1), (1, 2)):
        b.set(x, y, P["white"])
    b.save("FX/fx_bubble.png")
    d = Canvas(2, 3)
    d.rect(0, 1, 2, 2, P["white"]); d.set(0, 0, P["white"])
    d.save("FX/fx_drop.png")
    sparks = [
        [".......", ".......", "...#...", "..###..", "...#...", ".......", "......."],
        [".......", "...#...", "...#...", ".##o##.", "...#...", "...#...", "......."],
        ["...#...", "...#...", "..#o#..", "##ooo##", "..#o#..", "...#...", "...#..."],
    ]
    for i, rows in enumerate(sparks):
        s = Canvas(7, 7)
        for y, row in enumerate(rows):
            for x, v in enumerate(row):
                if v == "#":
                    s.set(x, y, P["amber_lt"])
                elif v == "o":
                    s.set(x, y, P["white"])
        s.save("FX/fx_sparkle_%d.png" % i)
    widths = [6, 4, 2, 4]
    for i, w in enumerate(widths):
        c = Canvas(6, 6)
        x0 = (6 - w) // 2
        c.rrect(x0, 0, w, 6, min(2, w // 2), P["amber"])
        if w > 2:
            c.vline(x0, 1, 4, P["amber_lt"])
            c.vline(x0 + w - 1, 1, 4, P["amber_dk"])
        else:
            c.vline(x0, 0, 5, P["amber_dk"])
        c.save("FX/fx_coin_%d.png" % i)
    p = Canvas(6, 6)
    p.ellipse(3, 3.5, 3, 2.5, hexc("6E6470"))
    p.ellipse(2.5, 2.5, 2, 2, hexc("8E8490"))
    p.save("FX/fx_puff.png")
    h = Canvas(5, 5)
    for y, row in enumerate([".#.#.", "#####", "#####", ".###.", "..#.."]):
        for x, v in enumerate(row):
            if v == "#":
                h.set(x, y, P["red"])
    h.set(1, 1, P["cream"])
    h.save("FX/fx_heart.png")


# ---------------------------------------------------------------- cumpleaños (easter egg)
def gen_party():
    # gorro de fiesta
    for name, col in (("hat_red", P["red"]), ("hat_mint", P["mint"])):
        h = Canvas(12, 14)
        for y in range(2, 13):
            half = (y - 1) * 5 // 11
            for x in range(6 - half - 1, 6 + half + 1):
                stripe = ((x + y) // 2) % 2 == 0
                h.set(x, y, col if stripe else P["cream"])
        h.hline(0, 11, 13, P["amber"])
        h.ellipse(6, 1.5, 1.6, 1.6, P["amber_lt"])
        h.outline()
        h.save("Party/%s.png" % name, pivot=(6, 0))

    # torta con dos velas
    cake = Canvas(36, 26)
    choco, choco_dk = hexc("6B3E2A"), hexc("4E2B1D")
    cake.rrect(2, 12, 32, 13, 2, choco)
    cake.rect(2, 21, 32, 4, choco_dk)
    cake.hline(2, 33, 17, P["cream_dk"])  # relleno
    cake.rrect(1, 10, 34, 4, 2, P["cream"])  # glaseado
    for dx in (4, 9, 15, 22, 28, 32):  # gotas
        cake.vline(dx, 13, 14 + dx % 3, P["cream"])
    for cx in (7, 18, 29):  # cerezas
        cake.rrect(cx - 1, 8, 3, 3, 1, P["red"])
        cake.set(cx - 1, 8, P["neon_core"])
    cake.rrect(0, 24, 36, 2, 1, P["steel_lt"])  # plato
    for vx in (13, 22):
        for y in range(2, 10):
            cake.rect(vx, y, 2, 1, P["mint"] if y % 3 else P["cream"])
        cake.vline(vx + 1, 1, 1, P["ink"])  # mecha
    cake.outline()
    cake.save("Party/cake.png", pivot=(18, 0))
    meta = {"candles_px": [[13 - 18 + 1, 26 - 1], [22 - 18 + 1, 26 - 1]]}
    with open(os.path.join(OUT, "Party", "cake.json"), "w") as f:
        json.dump(meta, f, indent=1)

    for i in range(2):
        fl = Canvas(3, 5)
        rows = [".#.", "###", "#o#", "#o#", ".#."] if i == 0 else ["#..", ".#.", "#o#", "#o#", ".#."]
        for y, row in enumerate(rows):
            for x, v in enumerate(row):
                if v == "#":
                    fl.set(x, y, P["amber_lt"])
                elif v == "o":
                    fl.set(x, y, P["white"])
        fl.save("Party/flame_%d.png" % i, pivot=(1, 0))

    cf = Canvas(2, 2)
    cf.rect(0, 0, 2, 2, P["white"])
    cf.save("Party/confetti.png")


def gen_gear():
    g = Canvas(11, 11)
    g.ellipse(5.5, 5.5, 4, 4, P["cream"])
    for x, y in ((5, 0), (5, 10), (0, 5), (10, 5), (2, 2), (8, 2), (2, 8), (8, 8)):
        g.rect(x, y, 1, 1, P["cream"])
    g.rect(4, 0, 3, 2, P["cream"]); g.rect(4, 9, 3, 2, P["cream"])
    g.rect(0, 4, 2, 3, P["cream"]); g.rect(9, 4, 2, 3, P["cream"])
    g.ellipse(5.5, 5.5, 1.6, 1.6, T)
    for y in range(11):
        for x in range(11):
            if ((x - 5) ** 2 + (y - 5) ** 2) <= 2:
                g.px[x, y] = T
    g.outline()
    g.save("UI/icon_gear.png")


# ---------------------------------------------------------------- pantalla de espera (exterior)
STREET_TOP = 176  # fila (desde arriba, en 216) donde empieza la calle


def gen_exterior():
    W, H = 384, 216
    c = Canvas(W, H)
    rnd = random.Random(21)
    # cielo en bandas con tramado
    for y in range(H):
        t = y / 120.0
        col = P["sky1"] if t < 0.45 else (P["sky2"] if t < 0.85 else mix(P["sky2"], P["night2"], 0.5))
        for x in range(W):
            if abs(t - 0.45) < 0.01 or abs(t - 0.85) < 0.01:
                col2 = col if (x + y) % 2 else mix(col, P["sky2"], 0.6)
                c.set(x, y, col2)
            else:
                c.set(x, y, col)
    for _ in range(60):
        c.set(rnd.randrange(W), rnd.randrange(90), P["cream_dk"] if rnd.random() < 0.7 else P["white"])
    c.ellipse(330, 26, 9, 9, P["cream"])
    c.ellipse(334, 24, 8, 8, P["sky1"])

    # ciudad de fondo
    bx = 0
    while bx < W:
        bw = rnd.randrange(14, 30)
        bh = rnd.randrange(50, 110)
        top = STREET_TOP - 20 - bh
        c.rect(bx, top, bw, STREET_TOP - top, P["city"])
        for wy in range(top + 4, STREET_TOP - 24, 6):
            for wx in range(bx + 3, bx + bw - 2, 4):
                if rnd.random() < 0.28:
                    c.rect(wx, wy, 2, 2, P["amber_dk"] if rnd.random() < 0.75 else P["amber_lt"])
        bx += bw + rnd.randrange(0, 4)

    # edificio del bar
    x0, x1, top = 72, 312, 58
    ground = STREET_TOP - 12
    brick, brick_dk, mortar = hexc("7A3B2E"), hexc("5E2C22"), hexc("4A2219")
    for y in range(top, ground):
        for x in range(x0, x1):
            row = (y - top) // 4
            off = 4 if row % 2 else 0
            col = brick if (x + off) % 8 else mortar
            if (y - top) % 4 == 3:
                col = mortar
            elif (x * 7 + y * 3) % 23 == 0:
                col = brick_dk
            c.set(x, y, col)
    # cornisa y techo
    c.rect(x0 - 6, top - 8, x1 - x0 + 12, 8, P["green_dk"])
    c.hline(x0 - 6, x1 + 5, top - 8, P["green_lt"])
    c.rect(x0 - 4, top, x1 - x0 + 8, 3, P["green"])
    # placa para el logo (el logo es un sprite aparte)
    c.rrect(166, top + 4, 52, 50, 4, P["green_dk"])
    c.rrect(167, top + 5, 50, 48, 3, P["green"])
    # vitrinas con interior cálido
    def shop_window(wx, wy, ww, wh):
        c.rect(wx - 3, wy - 3, ww + 6, wh + 6, P["green_dk"])
        for yy in range(wh):
            for xx in range(ww):
                k = yy / wh
                col = mix(P["amber_lt"], P["amber_dk"], k * 0.8)
                c.set(wx + xx, wy + yy, col)
        # siluetas de clientes en la barra
        for sx in range(wx + 6, wx + ww - 6, 14):
            h = rnd.randrange(14, 19)
            c.ellipse(sx + 3, wy + wh - h - 2, 3.5, 3.5, hexc("6E3F22"))
            c.rrect(sx - 1, wy + wh - h + 1, 9, h, 3, hexc("6E3F22"))
        c.rect(wx, wy + wh - 8, ww, 8, hexc("4E2B1D"))  # barra interior
        c.hline(wx, wx + ww - 1, wy + wh - 8, P["amber"])
        c.rect(wx + ww // 2, wy, 2, wh, P["green_dk"])  # parante
        c.hline(wx, wx + ww - 1, wy + 1, mix(P["amber_lt"], P["white"], 0.4))

    shop_window(86, 112, 64, 40)
    shop_window(234, 112, 64, 40)
    # toldos verdes con festón
    for ax in (82, 230):
        for y in range(98, 108):
            for x in range(ax, ax + 72):
                stripe = ((x - ax) // 6) % 2 == 0
                c.set(x, y, P["green"] if stripe else P["cream"])
        for x in range(ax, ax + 72, 6):
            c.ellipse(x + 3, 108, 3, 2, P["green"] if ((x - ax) // 6) % 2 == 0 else P["cream"])
        c.hline(ax, ax + 71, 98, P["green_hl"])
    # puerta
    dx, dy, dw, dh = 174, 112, 36, ground - 112
    c.rect(dx - 4, dy - 6, dw + 8, dh + 6, P["green_dk"])
    c.rect(dx, dy, dw, dh, P["cafe_dk"])
    c.rect(dx + 4, dy + 4, dw // 2 - 6, dh - 16, mix(P["amber_lt"], P["amber"], 0.4))
    c.rect(dx + dw // 2 + 2, dy + 4, dw // 2 - 6, dh - 16, mix(P["amber_lt"], P["amber"], 0.4))
    c.vline(dx + dw // 2, dy, dy + dh - 1, P["cafe"])
    c.rect(dx + dw // 2 - 4, dy + dh // 2, 2, 3, P["amber_lt"])
    c.rect(dx + dw // 2 + 3, dy + dh // 2, 2, 3, P["amber_lt"])
    c.rect(dx - 6, ground - 3, dw + 12, 3, P["steel"])  # escalón
    # vereda y calle
    c.rect(0, ground, W, 12, hexc("6E6A72"))
    for x in range(0, W, 16):
        c.vline(x, ground, ground + 11, hexc("5A5660"))
    c.hline(0, W - 1, ground, hexc("8E8A92"))
    c.rect(0, STREET_TOP, W, 2, hexc("A8A4AC"))  # solera
    c.rect(0, STREET_TOP + 2, W, H - STREET_TOP - 2, hexc("2C2A32"))
    for x in range(0, W, 24):
        c.rect(x, 200, 12, 2, P["cream_dk"])
    # poste del farol
    px = 40
    c.rect(px, 60, 3, ground - 60 + 2, P["ink"])
    c.rect(px - 1, ground - 4, 5, 6, P["ink"])
    c.rect(px - 6, 58, 14, 3, P["ink"])
    c.rrect(px - 7, 61, 6, 5, 2, P["cream"])
    c.rrect(px - 6, 62, 4, 3, 1, P["white"])
    c.save("Exterior/bg_exterior.png")
    meta = {"lamp_px": [px - 4 - 192, 216 - 63 - 108], "street_y_px": [216 - 188 - 108, 216 - 206 - 108],
            "sign_px": [192 - 192, 216 - (top + 29) - 108]}
    with open(os.path.join(OUT, "Exterior", "exterior.json"), "w") as f:
        json.dump(meta, f, indent=1)


def gen_logo_kid():
    """Logo del bar: niño sonriente con gorra que levanta una jarra espumosa (dentro de un escudo)."""
    S = 44
    c = Canvas(S, S)
    c.ellipse(22, 22, 21, 21, P["amber_dk"])
    c.ellipse(22, 22, 19.5, 19.5, P["cream"])
    c.ellipse(22, 22, 17, 17, mix(P["cream"], P["amber_lt"], 0.35))
    skin, skin_dk = SKINS[1]
    # cuerpo (polera verde del bar)
    c.rrect(10, 30, 24, 14, 5, P["green"])
    c.vline(22, 33, 42, P["green_dk"])
    # cabeza
    c.rrect(13, 12, 16, 16, 5, skin)
    c.vline(27, 15, 25, skin_dk)
    c.rect(12, 18, 1, 3, skin_dk)
    c.rect(29, 18, 1, 3, skin_dk)
    # gorra roja con visera
    c.rrect(12, 9, 18, 7, 3, P["red"])
    c.hline(13, 28, 14, P["red_dk"])
    c.rect(26, 14, 7, 2, P["red_dk"])
    c.set(20, 9, P["cream"])
    # cara feliz
    for ex in (17, 23):
        c.set(ex - 1, 19, P["ink"]); c.set(ex, 18, P["ink"]); c.set(ex + 1, 19, P["ink"])
    c.hline(18, 23, 23, P["ink"])
    c.hline(19, 22, 24, P["red_dk"])
    c.set(17, 22, P["ink"]); c.set(24, 22, P["ink"])
    c.rect(15, 21, 2, 1, P["red"][:3] + (150,)); c.rect(25, 21, 2, 1, P["red"][:3] + (150,))
    # jarra grande con espuma, sostenida con las dos manos
    jx, jy = 24, 26
    c.rrect(jx, jy, 12, 13, 2, P["amber"])
    c.vline(jx + 2, jy + 2, jy + 10, P["amber_lt"])
    c.vline(jx + 9, jy + 2, jy + 11, P["amber_dk"])
    c.rrect(jx + 11, jy + 3, 4, 7, 2, P["steel_lt"])  # asa
    c.rect(jx + 12, jy + 5, 2, 3, mix(P["cream"], P["amber_lt"], 0.35))
    c.ellipse(jx + 6, jy, 7, 3, P["foam"] if "foam" in P else P["cream"])
    c.ellipse(jx + 3, jy - 2, 3, 2.5, P["cream"])
    c.ellipse(jx + 8, jy - 2, 3, 2.5, P["cream"])
    c.set(jx + 1, jy + 3, P["cream"])  # gota de espuma
    c.set(jx + 1, jy + 4, P["cream"])
    c.rrect(jx - 3, jy + 4, 4, 4, 1, skin)  # manos
    c.rrect(jx + 4, jy + 8, 4, 4, 1, skin)
    c.outline()
    c.save("Exterior/logo_kid.png")


def gen_cars():
    cars = {
        "red": (P["red"], P["red_dk"], 48, 18, False),
        "taxi": (P["amber_lt"], P["amber_dk"], 48, 18, True),
        "van": (hexc("4E6E9E"), hexc("36507A"), 56, 24, False),
    }
    for name, (body, body_dk, w, h, taxi) in cars.items():
        for frame in range(2):
            c = Canvas(w, h + 2)
            base = h - 5
            roof_y = 2 if name == "van" else 5
            # carrocería
            c.rrect(1, base - 7, w - 2, 9, 3, body)
            c.hline(2, w - 3, base + 1, body_dk)
            if name == "van":
                c.rrect(3, roof_y, w - 14, base - roof_y - 5, 3, body)
            else:
                c.rrect(12, roof_y, 24, base - roof_y - 5, 4, body)
            # ventanas
            if name == "van":
                for wx in range(6, w - 18, 12):
                    c.rect(wx, roof_y + 3, 9, 6, mix(P["sky2"], P["cream"], 0.25))
                c.rect(w - 15, roof_y + 3, 6, 6, mix(P["sky2"], P["cream"], 0.25))
            else:
                c.rect(15, roof_y + 2, 8, 5, mix(P["sky2"], P["cream"], 0.25))
                c.rect(25, roof_y + 2, 8, 5, mix(P["sky2"], P["cream"], 0.25))
            if taxi:
                c.rect(20, roof_y - 3, 8, 3, P["cream"])
                c.hline(21, 26, roof_y - 2, P["ink"])
                for x in range(3, w - 3, 4):
                    c.set(x, base - 4, P["ink"] if (x // 4) % 2 else P["cream"])
            c.hline(4, w - 5, base - 7, mix(body, P["white"], 0.35))  # brillo
            # luces: delantera (derecha) y trasera (izquierda)
            c.rect(w - 3, base - 5, 2, 3, P["cream"])
            c.rect(1, base - 5, 2, 3, P["red"])
            # ruedas
            for wx in (10, w - 12):
                c.ellipse(wx + 0.5, base + 0.5, 4, 4, P["ink"])
                c.ellipse(wx + 0.5, base + 0.5, 2, 2, P["steel"])
                sx, sy = ((1, 0), (0, 1))[frame]
                c.set(wx + sx, base + sy, P["ink"])
            c.outline()
            c.save("Exterior/car_%s_%d.png" % (name, frame), pivot=(w // 2, 0))


def main():
    random.seed(1)
    gen_font()
    gen_background()
    gen_counter()
    gen_lamps()
    gen_neon()
    gen_taps()
    gen_glass()
    for i, spec in enumerate(CUSTOMERS):
        gen_customer(i, spec)
    gen_staff()
    gen_ui()
    gen_fx()
    gen_party()
    gen_gear()
    gen_exterior()
    gen_logo_kid()
    gen_cars()
    with open(os.path.join(OUT, "sprite_meta.json"), "w") as f:
        json.dump(META, f, indent=1, sort_keys=True)
    print("Sprites generados:", len(META))


if __name__ == "__main__":
    main()
