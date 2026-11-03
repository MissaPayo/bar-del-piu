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

    # ampolleta apagada: la misma lámpara con el vidrio gris (se alterna con lamp.png al tocarla)
    o = Canvas(14, 30)
    o.vline(7, 0, 15, P["ink"])
    o.rrect(5, 14, 4, 3, 1, P["amber_dk"])
    for i, y in enumerate(range(17, 25)):
        hw = 2 + i // 2
        o.hline(7 - hw, 6 + hw, y, mix(P["amber"], P["ink"], 0.35) if i < 6 else mix(P["amber_dk"], P["ink"], 0.3))
        o.set(7 - hw, y, mix(P["amber_lt"], P["ink"], 0.4))
    o.hline(1, 12, 25, mix(P["amber_dk"], P["ink"], 0.3))
    o.rrect(4, 25, 6, 4, 2, hexc("6E6A72"))
    o.set(6, 26, hexc("9C98A0"))
    o.outline()
    o.save("Bar/lamp_off.png", pivot=(7, 30))

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
        for state in ("idle", "sel", "pull", "hint"):
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
            if state == "hint":  # contorno doble: esta llave la pide un cliente que espera
                h.outline(P["amber_lt"])
                h.outline(P["neon"])
            h.save("Taps/handle_%s_%s.png" % (key, state), pivot=(HANDLE_W // 2, 0))


# ---------------------------------------------------------------- vaso
GLASS_W, GLASS_H = 18, 28  # antes 16x24: un poco más grande para celular


def glass_row_span(y):
    """Interior del vaso para la fila y (arriba->abajo). Pinta cónica."""
    t = y / (GLASS_H - 1)
    half = (GLASS_W / 2 - 0.5) - 2.2 * t
    c = GLASS_W / 2
    return int(round(c - half)), int(round(c - 1 + half))


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
# Clientes del modo Hardcore: punks y metaleros (pelo, ropa y accesorios propios)
HC_HAIRS = [hexc("3FD17A"), hexc("FF4FA3"), hexc("15131A"), hexc("E8E2D0"), hexc("9B59FF")]
HC_SHIRTS = [(hexc("2A2A30"), hexc("18181D")), (hexc("15131A"), hexc("0B0A0E")), (hexc("6B1F24"), hexc("4A1418"))]
CUSTOMERS_HC = [
    # skin, hair (HC), style, shirt (HC), extras
    (0, 0, "mohawk", 0, ("studs", "piercing")),
    (2, 2, "metal", 1, ("skull", "beard")),
    (1, 1, "spikes", 0, ("studs", "shades")),
    (3, 2, "metal", 2, ("skull", "piercing")),
    (0, 3, "spikes", 1, ("skull", "shades")),
    (2, 4, "mohawk", 2, ("studs", "beard")),
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


def gen_customer_hc(idx, spec):
    """Cliente punk/metalero: mismas proporciones y expresiones que los normales."""
    skin_i, hair_i, style, shirt_i, extras = spec
    skin, skin_dk = SKINS[skin_i]
    hair = HC_HAIRS[hair_i]
    hair_dk = mix(hair, P["ink"], 0.4)
    shirt, shirt_dk = HC_SHIRTS[shirt_i]
    steel_lt = P["steel_lt"]
    for expr in EXPRESSIONS:
        c = Canvas(32, CUST_H)
        face = skin if expr != "enojado" else mix(skin, P["red"], 0.25)
        face_dk = skin_dk if expr != "enojado" else mix(skin_dk, P["red_dk"], 0.25)
        if style == "metal":  # melena larga hasta los hombros
            c.rrect(7, 6, 18, 28, 4, hair)
            c.vline(24, 10, 32, hair_dk)
        # chaqueta / polera
        c.rrect(4, 27, 24, CUST_H - 27 + 4, 4, shirt)
        c.rect(22, 29, 5, CUST_H - 29, shirt_dk)
        if "studs" in extras:  # chaqueta de cuero con tachas y polera blanca
            c.vline(13, 29, CUST_H - 1, shirt_dk)
            c.vline(19, 29, CUST_H - 1, shirt_dk)
            c.rect(14, 28, 5, 8, hexc("D8D4DA"))
            for sx, sy in ((7, 31), (10, 30), (22, 30), (25, 31), (6, 35), (26, 35)):
                c.set(sx, sy, steel_lt)
        if "skull" in extras:  # polera de banda con calavera
            c.ellipse(16, 37, 4, 4, P["cream"])
            c.rect(14, 40, 5, 2, P["cream"])
            for sx in (14, 15, 17, 18):
                c.set(sx, 37, P["ink"])
            c.set(16, 39, P["ink"])
        c.rect(13, 22, 6, 6, face_dk)
        c.rrect(9, 7, 14, 16, 4, face)
        c.vline(21, 10, 19, face_dk)
        c.hline(12, 19, 22, face_dk)
        c.rect(8, 14, 1, 3, face_dk)
        c.rect(23, 14, 1, 3, face_dk)
        if style == "mohawk":  # costados rapados y cresta alta
            c.hline(10, 21, 8, face_dk)
            for x in range(13, 19):
                top = int(abs(x - 15.5) // 1.5)
                c.vline(x, top, 8, hair)
            c.vline(18, 1, 8, hair_dk)
        elif style == "spikes":  # pelo en punta
            c.rrect(9, 5, 14, 6, 3, hair)
            for sx in (10, 13, 16, 19, 22):
                for k in range(4):
                    c.hline(sx - (3 - k) // 2, sx + (3 - k) // 2, 4 - k, hair)
            c.hline(12, 20, 10, hair_dk)
        elif style == "metal":
            c.rrect(9, 5, 14, 6, 3, hair)
            c.rect(9, 8, 3, 12, hair)
            c.rect(20, 8, 3, 12, hair)
            c.vline(15, 5, 8, hair_dk)  # partidura al medio
        if "beard" in extras:
            c.rrect(10, 17, 12, 7, 2, hair if style != "mohawk" else hexc("3A2A20"))
            c.rect(14, 19, 4, 1, face)
        draw_face(c, 15, 14, 19, expr, face, P["ink"])
        if "shades" in extras:  # lentes oscuros
            c.rect(10, 13, 12, 3, P["ink"])
            c.set(11, 13, hexc("5A5660"))
            c.set(18, 13, hexc("5A5660"))
        if "piercing" in extras:
            c.set(8, 16, steel_lt)
            c.set(23, 16, steel_lt)
            c.set(17, 18, steel_lt)
        if expr == "impaciente":
            c.set(24, 9, P["sweat"]); c.rect(24, 10, 2, 2, P["sweat"]); c.set(24, 10, P["white"])
        if expr == "enojado":
            for (vx, vy) in ((24, 5), (25, 4), (26, 5), (25, 6)):
                c.set(vx, vy, P["red"])
        c.outline()
        c.save("Characters/Customers/customer_hc_%d_%s.png" % (idx, expr), pivot=(16, 0))


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


def staff_face(c, expr, skin, hair_dk, ink, eyeliner=None, lips=None):
    """Expresiones de los retratos 48x48 (las mismas que gen_staff)."""
    ey, my = 20, 26
    lx, rx = 19, 28
    if expr == "feliz":
        for ex in (lx, rx):
            c.set(ex - 1, ey + 1, ink); c.set(ex, ey, ink); c.set(ex + 1, ey, ink); c.set(ex + 2, ey + 1, ink)
        c.hline(21, 26, my, ink)
        c.hline(22, 25, my + 1, lips or P["red_dk"])
        c.set(20, my - 1, ink); c.set(27, my - 1, ink)
        c.rect(16, 24, 2, 1, P["red"][:3] + (150,)); c.rect(30, 24, 2, 1, P["red"][:3] + (150,))
    elif expr == "ebrio":
        for ex in (lx, rx):
            c.hline(ex - 1, ex + 2, ey + 1, ink)
            c.hline(ex, ex + 1, ey, mix(skin, ink, 0.45))
        c.rect(15, 23, 4, 2, P["red"][:3] + (190,)); c.rect(29, 23, 4, 2, P["red"][:3] + (190,))
        c.set(21, my, ink); c.set(22, my + 1, ink); c.set(23, my + 1, ink); c.set(24, my, ink)
        c.set(25, my - 1, ink); c.set(26, my - 1, ink)
        c.rect(33, 12, 2, 3, P["sweat"]); c.set(33, 12, P["white"])
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
        c.hline(22, 25, my, lips or mix(skin, ink, 0.7))
        c.set(21, my - 1, lips or mix(skin, ink, 0.7)); c.set(26, my - 1, lips or mix(skin, ink, 0.7))
    if eyeliner and expr not in ("feliz", "ebrio"):
        for ex in (lx, rx):  # delineado con «colita»
            c.hline(ex - 1, ex + 2, ey - 1, eyeliner)
        c.set(rx + 3, ey - 2, eyeliner); c.set(lx - 2, ey - 2, eyeliner)


def gen_staff_hc():
    """Pipe y Nata en versión Hardcore: mismo delantal con su inicial, look punk/metalero completo."""
    ink = P["ink"]
    steel_lt = P["steel_lt"]
    for name in ("pipe", "nata"):
        skin, skin_dk = SKINS[1] if name == "pipe" else SKINS[0]
        for expr in ("neutral", "feliz", "sorpresa", "ebrio"):
            c = Canvas(48, 48)
            if name == "nata":  # cola teñida (detrás)
                c.rrect(32, 12, 8, 22, 4, hexc("9B59FF"))
                c.vline(38, 14, 32, hexc("6A3AC0"))
                c.vline(34, 16, 30, hexc("FF4FA3"))
            # torso: chaqueta/chaleco negro + delantal negro con tachas e inicial
            c.rrect(6, 34, 36, 14, 6, hexc("1A1A20"))
            c.rect(8, 36, 4, 12, hexc("26242C")); c.rect(36, 36, 4, 12, hexc("26242C"))
            for (sx, sy) in ((8, 35), (11, 34), (36, 34), (39, 35)):  # tachas en hombros
                c.set(sx, sy, steel_lt)
            c.rrect(12, 36, 24, 12, 2, hexc("15131A"))
            c.vline(13, 36, 47, hexc("2E2C36"))
            c.rect(14, 34, 2, 3, hexc("15131A")); c.rect(32, 34, 2, 3, hexc("15131A"))
            c.set(14, 34, steel_lt); c.set(33, 34, steel_lt)
            c.rrect(21, 40, 7, 6, 2, hexc("C62E3A"))  # parche rojo con la inicial
            if name == "pipe":
                for (x, y) in ((23, 41), (23, 42), (23, 43), (23, 44), (24, 41), (25, 41), (25, 42), (24, 43)):
                    c.set(x, y, P["cream"])
                c.rect(30, 33, 8, 6, hexc("C62E3A"))  # pañuelo rojo al hombro
                for (x, y) in ((31, 34), (34, 35), (36, 37), (32, 37)):
                    c.set(x, y, P["cream"])
            else:
                for (x, y) in ((22, 41), (22, 42), (22, 43), (22, 44), (25, 41), (25, 42), (25, 43), (25, 44), (23, 42), (24, 43)):
                    c.set(x, y, P["cream"])
            # cuello (tatuaje en Pipe, gargantilla en Nata) y cabeza
            c.rect(19, 29, 10, 6, skin_dk)
            if name == "pipe":
                for (x, y) in ((20, 31), (21, 30), (22, 31), (21, 32)):
                    c.set(x, y, hexc("3E5C8A"))
            else:
                c.rect(18, 30, 12, 2, ink)
                for x in range(19, 30, 2):
                    c.set(x, 30, steel_lt)
            c.rrect(13, 9, 22, 23, 7, skin)
            c.vline(33, 14, 27, skin_dk)
            c.hline(18, 30, 31, skin_dk)
            c.rect(11, 18, 2, 5, skin_dk); c.rect(35, 18, 2, 5, skin_dk)
            if name == "pipe":
                # cresta negra con puntas rojas, costados rapados
                c.hline(14, 33, 10, mix(skin_dk, P["ink"], 0.25))
                c.hline(14, 33, 11, mix(skin_dk, P["ink"], 0.15))
                # cresta angosta y alta con puntas en serrucho (vista de frente)
                tops = {21: 3, 22: 0, 23: 2, 24: 0, 25: 2, 26: 4}
                for x, top in tops.items():
                    c.vline(x, top, 11, hexc("15131A"))
                    c.vline(x, top, top + 2, hexc("C62E3A"))
                c.vline(22, 3, 10, hexc("2E2C36"))  # brillo
                # barba de días (tramada)
                for y in range(24, 31):
                    for x in range(15, 33):
                        if (x + y) % 2 == 0 and c.get(x, y) == skin:
                            c.set(x, y, mix(skin, hexc("15131A"), 0.35))
                hair_dk = hexc("15131A")
                staff_face(c, expr, skin, hair_dk, ink)
                c.set(31, 16, steel_lt); c.set(31, 17, steel_lt)  # piercing en la ceja
                c.set(11, 21, steel_lt); c.set(36, 21, steel_lt)  # expansores
            else:
                # rapada de un lado, pelo rosado/morado barrido al otro
                c.hline(14, 22, 10, mix(skin_dk, P["ink"], 0.2))
                for x in range(14, 22):
                    c.vline(x, 11, 12, mix(skin_dk, P["ink"], 0.12))
                c.rrect(20, 5, 16, 10, 4, hexc("FF4FA3"))
                c.rect(30, 12, 6, 12, hexc("FF4FA3"))
                c.rect(22, 12, 9, 3, hexc("FF4FA3"))
                c.hline(22, 34, 6, hexc("FFB0D6"))
                c.vline(33, 12, 23, hexc("9B59FF"))
                c.vline(28, 7, 13, hexc("C2306F"))
                hair_dk = hexc("6A3AC0")
                staff_face(c, expr, skin, hair_dk, ink, eyeliner=ink, lips=hexc("5A1E3A"))
                c.set(24, 28, steel_lt)                           # aro en el labio
                for y in (19, 21, 23):                             # aros en la oreja
                    c.set(11, y, steel_lt)
                c.set(36, 21, steel_lt)
                c.rect(36, 10, 2, 9, hexc("C9A15A")); c.set(36, 10, hexc("E8D7A8"))  # baqueta detrás de la oreja
            c.outline()
            c.save("Characters/Staff/%s_hc_%s.png" % (name, expr), pivot=(24, 0))


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
        for variant in ("", "_hint"):
            o = Canvas(30, 26)
            o.paste(g, 2, 2)
            o.outline(P["cafe_dk"])
            if variant:
                o.outline(P["amber_lt"])
                o.outline(P["neon"])
            o.save("UI/order_%s%s.png" % (key, variant), pivot=(9, 2))

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


# ---------------------------------------------------------------- modo Hardcore: bar punk
def gen_hardcore_look():
    # Velo tramado sepia para la pantalla de concentración (se repite en mosaico)
    v = Canvas(2, 2)
    v.set(0, 0, hexc("2A1E18", 235))
    v.set(1, 1, hexc("2A1E18", 235))
    v.set(1, 0, hexc("8A6440", 150))
    v.set(0, 1, hexc("8A6440", 150))
    v.save("UI/veil_dither.png")

    # Decoración punk que se superpone a la pared (384x216, transparente)
    W, H = 384, 216
    d = Canvas(W, H)
    rnd = random.Random(66)

    def poster(x0, y0, w, h, bg, ink, lines, icon):
        d.rect(x0, y0, w, h, bg)
        d.hline(x0, x0 + w - 1, y0, mix(bg, P["white"], 0.25))
        # esquina rota y cinta adhesiva
        for k in range(4):
            d.hline(x0 + w - 4 + k, x0 + w - 1, y0 + k, T)
        d.rect(x0 + w // 2 - 3, y0 - 2, 6, 3, hexc("D8D4DA", 170))
        if icon == "skull":
            d.ellipse(x0 + w / 2, y0 + 10, 5, 5, ink)
            d.rect(x0 + w // 2 - 3, y0 + 13, 7, 3, ink)
            for ex in (-3, 2):
                d.rect(x0 + w // 2 + ex, y0 + 8, 2, 3, bg)
            d.set(x0 + w // 2, y0 + 12, bg)
        elif icon == "bolt":
            pts = [(0, 0), (1, 1), (1, 2), (2, 3), (1, 4), (2, 5), (3, 6), (2, 7), (3, 8), (4, 9)]
            for i in range(10):
                d.rect(x0 + w // 2 - 4 + pts[i][0] * 2, y0 + 6 + i * 2, 3, 2, ink)
        for i, line in enumerate(lines):
            tw = text_width(line)
            draw_text(d, line, x0 + (w - tw) // 2, y0 + h - 12 - (len(lines) - 1 - i) * 9, ink)
        d.outline(P["ink"])

    poster(124, 66, 32, 40, hexc("6B1F24"), P["cream"], ["PIU", "FEST"], "skull")
    poster(230, 66, 32, 40, hexc("2C1F3D"), P["amber_lt"], ["RUIDO"], "bolt")
    poster(10, 112, 26, 34, hexc("1F3A2C"), hexc("FF4FA3"), ["OI!"], "skull")
    poster(350, 108, 26, 34, hexc("3D2B1A"), P["amber_lt"], ["2026"], "bolt")

    # Grafitis con chorreado sobre el zócalo verde
    def spray(text, x, y, col, scale=2):
        tmp = Canvas(W, H)
        draw_text(tmp, text, x, y, col, scale)
        for yy in range(H):
            for xx in range(W):
                if tmp.get(xx, yy)[3]:
                    d.set(xx, yy, col)
                    if rnd.random() < 0.05:  # chorreado de pintura
                        for k in range(rnd.randrange(2, 7)):
                            d.set(xx, yy + k, col)
        return tmp

    spray("HARDCORE", 96, 124, hexc("FF4FA3"), 2)
    spray("OI!", 178, 88, hexc("3FD17A"), 2)   # sobre la pizarra del menú
    spray("PIU", 322, 126, hexc("9B59FF"), 2)
    # chispas de pintura sueltas
    for _ in range(60):
        d.set(rnd.randrange(80, 300), rnd.randrange(118, 175), hexc("FF4FA3", 160))
    d.save("Background/bg_hardcore_decor.png")

    # Letrero de neón HARDCORE (rojo) para reemplazar a BAR DEL PIU en modo Hardcore
    for state in ("on", "off"):
        tw = text_width("HARDCORE", 2)
        Wn, Hn = tw + 16, 7 * 2 + 12
        c = Canvas(Wn, Hn)
        c.rrect(0, 0, Wn, Hn, 3, P["ink"])
        c.rrect(1, 1, Wn - 2, Hn - 2, 2, hexc("1A0E14"))
        tube = mix(hexc("FF2E3E"), hexc("FFD0D0"), 0.4) if state == "on" else hexc("4A1A20")
        tmp = Canvas(Wn, Hn)
        draw_text(tmp, "HARDCORE", 8, 6, tube, 2)
        if state == "on":
            tmp.outline(hexc("FF2E3E", 120), diagonal=True)
        c.paste(tmp, 0, 0)
        for sx, sy in ((3, 3), (Wn - 4, 3), (3, Hn - 4), (Wn - 4, Hn - 4)):
            c.set(sx, sy, P["steel"])
        c.save("Bar/neon_hc_%s.png" % state)

    # Sello «MODO HARDCORE» para el menú
    tw = text_width("MODO HARDCORE")
    st = Canvas(tw + 12, 17)
    st.rect(1, 1, tw + 10, 15, hexc("C62E3A"))
    st.rect(3, 3, tw + 6, 11, hexc("1A0E14"))
    draw_text(st, "MODO HARDCORE", 6, 5, hexc("FF6B78"))
    for _ in range(18):  # tinta gastada
        st.set(rnd.randrange(st.w), rnd.randrange(st.h), T)
    st.save("UI/stamp_hardcore.png")

    # Accesorios punk para Pipe y Nata (se superponen a sus retratos 48x48)
    a = Canvas(48, 48)
    a.rect(16, 18, 16, 4, P["ink"])           # lentes oscuros
    a.hline(17, 19, 18, hexc("5A5660"))
    a.hline(26, 28, 18, hexc("5A5660"))
    a.rect(18, 31, 12, 2, P["ink"])           # collar con tachas
    for x in range(19, 30, 2):
        a.set(x, 30, P["steel_lt"])
    a.save("Characters/Staff/pipe_punk.png", pivot=(24, 0))
    b = Canvas(48, 48)
    for y in range(7, 16):                    # mecha rosada
        b.hline(14, 19, y, hexc("FF4FA3"))
    b.hline(14, 19, 15, hexc("C2306F"))
    b.rect(18, 31, 12, 2, P["ink"])           # gargantilla
    for x in range(19, 30, 2):
        b.set(x, 30, P["steel_lt"])
    b.set(12, 23, P["steel_lt"]); b.set(12, 24, P["steel_lt"])   # aros
    b.set(36, 23, P["steel_lt"]); b.set(36, 24, P["steel_lt"])
    b.save("Characters/Staff/nata_punk.png", pivot=(24, 0))


# ---------------------------------------------------------------- espera Hardcore (exterior punk)
def gen_exterior_hardcore():
    W, H = 384, 216
    d = Canvas(W, H)
    rnd = random.Random(99)
    # toldos rojo y negro
    for ax in (82, 230):
        for y in range(98, 108):
            for x in range(ax, ax + 72):
                d.set(x, y, hexc("8E1E28") if ((x - ax) // 6) % 2 == 0 else hexc("15131A"))
        for x in range(ax, ax + 72, 6):
            d.ellipse(x + 3, 108, 3, 2, hexc("8E1E28") if ((x - ax) // 6) % 2 == 0 else hexc("15131A"))
    # siluetas con crestas en las vitrinas (fiesta punk adentro)
    for wx0 in (86, 234):
        for i, sx in enumerate(range(wx0 + 4, wx0 + 60, 12)):
            h = rnd.randrange(16, 22)
            base = 152
            col = hexc("4A1E2A")
            d.ellipse(sx + 3, base - h - 2, 3.5, 3.5, col)
            d.rrect(sx - 1, base - h + 1, 9, h - 1, 3, col)
            if i % 2 == 0:  # cresta
                for k in range(5):
                    d.vline(sx + 2 + (k % 2), base - h - 6 - k, base - h - 4, col)
            else:  # brazo arriba (pogo)
                d.rect(sx + 7, base - h - 6, 2, 8, col)
    # grafitis sobre el ladrillo
    def spray(text, x, y, col, scale=2):
        tmp = Canvas(W, H)
        draw_text(tmp, text, x, y, col, scale)
        for yy in range(H):
            for xx in range(W):
                if tmp.get(xx, yy)[3]:
                    d.set(xx, yy, col)
                    if rnd.random() < 0.05:
                        for k in range(rnd.randrange(2, 6)):
                            d.set(xx, yy + k, col)
    spray("PUNK", 236, 66, hexc("3FD17A"))
    spray("OI!", 84, 72, hexc("FF4FA3"))
    spray("HC", 150, 132, hexc("9B59FF"), 1)
    # carteles pegados en la pared
    for (px, py, bg, txt) in ((222, 128, hexc("6B1F24"), "PIU"), (156, 140, hexc("2C1F3D"), "RUIDO")):
        tw = text_width(txt)
        d.rect(px, py, tw + 6, 14, bg)
        draw_text(d, txt, px + 3, py + 4, P["cream"])
        d.rect(px + (tw + 6) // 2 - 2, py - 1, 4, 2, hexc("D8D4DA", 170))
    d.save("Exterior/exterior_hardcore_decor.png")

    # moto con piloto punk (2 cuadros de ruedas)
    for frame in range(2):
        c = Canvas(40, 26)
        base = 21
        c.rrect(8, base - 7, 22, 5, 2, hexc("2A2A30"))       # chasis
        c.rect(12, base - 10, 10, 4, hexc("8E1E28"))          # estanque
        c.hline(13, 20, base - 10, hexc("C62E3A"))
        c.rect(29, base - 12, 2, 7, P["steel"])               # horquilla
        c.rect(27, base - 13, 6, 2, P["steel_lt"])            # manubrio
        c.rect(33, base - 9, 2, 2, P["cream"])                # foco
        # piloto: chaqueta negra, cresta verde
        c.rrect(15, base - 20, 8, 10, 2, hexc("15131A"))
        c.rect(22, base - 17, 6, 2, hexc("15131A"))           # brazo al manubrio
        c.rrect(16, base - 26 + 2, 6, 6, 2, SKINS[0][0])      # cabeza
        for k in range(4):
            c.vline(18 + (k % 2), base - 26, base - 24 + k // 2, hexc("3FD17A"))
        for wx in (10, 30):
            c.ellipse(wx + 0.5, base + 0.5, 4.5, 4.5, P["ink"])
            c.ellipse(wx + 0.5, base + 0.5, 2, 2, P["steel"])
            sx, sy = ((1, 0), (0, 1))[frame]
            c.set(wx + sx, base + sy, P["ink"])
        c.outline()
        c.save("Exterior/moto_%d.png" % frame, pivot=(20, 0))


# ---------------------------------------------------------------- clientes Hardcore v2: arquetipos
HC_ARCHETYPES = ["crestas", "melenudo", "gotica", "coletas", "motoquero", "blackmetal"]


def gen_punk(idx, kind):
    """Clientes del modo Hardcore: cada uno es un arquetipo con silueta y vestuario propios."""
    ink = P["ink"]
    steel, steel_lt = P["steel"], P["steel_lt"]
    rnd = random.Random(idx * 31 + 7)
    for expr in EXPRESSIONS:
        c = Canvas(32, CUST_H)
        angry = expr == "enojado"

        # ---- paleta por arquetipo
        if kind == "crestas":
            skin, skin_dk = SKINS[0]
            hair, hair_dk, hair_lt = hexc("3FD17A"), hexc("1F8A4C"), hexc("A8F5C8")
            jacket, jacket_dk = hexc("26242C"), hexc("141218")
        elif kind == "melenudo":
            skin, skin_dk = SKINS[1]
            hair, hair_dk, hair_lt = hexc("2E1E16"), hexc("180F0B"), hexc("5A3B2A")
            jacket, jacket_dk = hexc("3E5C8A"), hexc("2A4166")  # chaleco de mezclilla
        elif kind == "gotica":
            skin, skin_dk = hexc("EDE2E4"), hexc("CDBFC4")
            hair, hair_dk, hair_lt = hexc("141218"), hexc("0A090C"), hexc("3A3044")
            jacket, jacket_dk = hexc("2A1A33"), hexc("170E1D")
        elif kind == "coletas":
            skin, skin_dk = SKINS[2]
            hair, hair_dk, hair_lt = hexc("FF4FA3"), hexc("C2306F"), hexc("FFB0D6")
            jacket, jacket_dk = hexc("9E2A33"), hexc("6B1A21")  # escocés rojo
        elif kind == "motoquero":
            skin, skin_dk = SKINS[1]
            hair, hair_dk, hair_lt = hexc("9C98A0"), hexc("6E6A72"), hexc("D8D4DA")  # barba gris
            jacket, jacket_dk = hexc("2A2A30"), hexc("18181D")
        else:  # blackmetal
            skin, skin_dk = hexc("F2EEEA"), hexc("D2CCC6")  # pintura de cadáver
            hair, hair_dk, hair_lt = hexc("0E0D12"), hexc("060508"), hexc("2A2833")
            jacket, jacket_dk = hexc("15131A"), hexc("0B0A0E")
        face = mix(skin, P["red"], 0.25) if angry and kind not in ("blackmetal", "gotica") else skin
        face_dk = mix(skin_dk, P["red_dk"], 0.25) if angry and kind not in ("blackmetal", "gotica") else skin_dk

        # ---- pelo de atrás (va detrás de la cabeza)
        if kind in ("melenudo", "blackmetal"):
            c.rrect(6, 6, 20, 30, 5, hair)
            for x in range(7, 25, 3):  # mechones con brillo
                c.vline(x, 12, 33 - (x % 4), hair_lt if kind == "melenudo" else hair_dk)
        if kind == "coletas":
            for cx in (6, 26):
                c.ellipse(cx, 14, 4.5, 6, hair)
                c.vline(cx - 1, 10, 18, hair_lt)
                c.rect(cx - 1, 8, 3, 2, hexc("3FD17A"))  # colet
        if kind == "gotica":
            c.rrect(8, 6, 16, 18, 4, hair)

        # ---- torso
        c.rrect(4, 27, 24, CUST_H - 23, 4, jacket)
        c.rect(22, 29, 5, CUST_H - 29, jacket_dk)
        if kind == "crestas":
            c.rect(13, 28, 6, 10, hexc("E8E2D0"))           # polera blanca
            c.vline(12, 28, CUST_H - 1, jacket_dk)           # solapas
            c.vline(19, 28, CUST_H - 1, jacket_dk)
            for sx in (6, 9, 23, 26):                          # tachas en punta en los hombros
                c.set(sx, 27, steel_lt); c.set(sx, 28, steel)
            c.vline(9, 33, 37, steel_lt); c.set(10, 33, steel_lt)  # imperdible
        elif kind == "melenudo":
            c.rect(12, 28, 8, CUST_H - 28, hexc("15131A"))  # polera negra
            for y in range(31, 38, 2):                       # logo de banda garabateado
                c.hline(13, 18, y, hexc("D8D4DA") if y % 4 else hexc("9C98A0"))
            for (px, py, col) in ((6, 31, hexc("C62E3A")), (24, 33, hexc("F2C25C")), (7, 37, hexc("3FD17A"))):
                c.rect(px, py, 3, 3, col)                     # parches
            c.vline(11, 28, CUST_H - 1, hexc("2A4166")); c.vline(20, 28, CUST_H - 1, hexc("2A4166"))
        elif kind == "gotica":
            for y in range(29, CUST_H):                        # encaje tramado
                for x in range(6, 26):
                    if (x + y) % 3 == 0 and c.get(x, y)[3]:
                        c.set(x, y, hexc("4A2E5C"))
            c.ellipse(16, 30, 2, 1.5, hexc("C8C0D0"))         # colgante de luna
            c.set(16, 30, jacket)
        elif kind == "coletas":
            for y in range(28, CUST_H):                        # escocés
                for x in range(4, 28):
                    if not c.get(x, y)[3]:
                        continue
                    if x % 6 in (0, 1) or y % 6 in (0, 1):
                        c.set(x, y, hexc("15131A") if (x % 6 in (0, 1) and y % 6 in (0, 1)) else jacket_dk)
                    if x % 6 == 3 and y % 6 == 3:
                        c.set(x, y, hexc("F2C25C"))
        elif kind == "motoquero":
            c.rrect(4, 27, 6, 14, 2, skin)                     # brazos al aire tatuados
            c.rrect(22, 27, 6, 14, 2, skin_dk)
            for (tx, ty) in ((5, 30), (6, 32), (7, 34), (5, 35), (23, 30), (24, 33), (25, 31)):
                c.set(tx, ty, hexc("3E5C8A"))
            c.rect(10, 27, 12, CUST_H - 27, jacket)            # chaleco de cuero
            c.rect(13, 28, 6, CUST_H - 28, hexc("6E6A72"))    # polera gris
            for x in range(10, 22, 2):                         # cadena
                c.set(x, 38 + (x % 4 == 0), steel_lt)
        else:  # blackmetal
            c.rect(12, 28, 8, CUST_H - 28, jacket)
            for (x, y) in ((13, 31), (14, 30), (15, 32), (16, 30), (17, 31), (18, 30), (15, 33), (17, 33)):
                c.set(x, y, hexc("D8D4DA"))                    # logo puntiagudo
            for sx in (5, 8, 24, 27):
                c.set(sx, 27, steel_lt)

        # ---- cuello y cabeza
        c.rect(13, 22, 6, 6, face_dk)
        if kind in ("gotica", "coletas"):
            c.rect(12, 25, 8, 2, ink)                           # gargantilla
            for x in range(13, 20, 2):
                c.set(x, 25, steel_lt)
        c.rrect(9, 7, 14, 16, 4, face)
        c.vline(21, 10, 19, face_dk)
        c.hline(12, 19, 22, face_dk)
        c.rect(8, 14, 1, 3, face_dk)
        c.rect(23, 14, 1, 3, face_dk)

        # ---- pelo de adelante y tocados
        if kind == "crestas":
            c.hline(10, 21, 8, face_dk)                          # costados rapados
            c.hline(10, 21, 9, mix(face_dk, hair, 0.3))
            for i, sx in enumerate((11, 14, 17, 20)):            # púas de colores
                col = (hair, hexc("FF4FA3"), hexc("F2C25C"), hair)[i]
                h = 7 + (i % 2) * 2
                for k in range(h):
                    w = max(0, (h - k) // 3)
                    c.hline(sx - w, sx + w, 7 - k, col)
                c.set(sx, 7 - h + 1, mix(col, P["white"], 0.5))
        elif kind == "melenudo":
            c.rrect(9, 5, 14, 6, 3, hair)
            c.rect(9, 8, 3, 12, hair); c.rect(20, 8, 3, 12, hair)
            c.vline(15, 5, 8, hair_dk)
            c.rrect(13, 19, 6, 5, 2, hair)                      # chivita
            c.rect(14, 19, 4, 1, face)
        elif kind == "gotica":
            c.rrect(9, 5, 14, 5, 2, hair)
            c.rect(9, 9, 14, 3, hair)                           # flequillo recto
            c.hline(10, 21, 9, hair_lt)
            c.rect(9, 9, 2, 12, hair); c.rect(21, 9, 2, 12, hair)
        elif kind == "coletas":
            c.rrect(9, 5, 14, 6, 3, hair)
            c.rect(9, 8, 2, 5, hair); c.rect(21, 8, 2, 5, hair)
            c.hline(12, 19, 6, hair_lt)
            c.set(11, 18, hexc("3E5C8A")); c.set(10, 17, hexc("3E5C8A")); c.set(12, 17, hexc("3E5C8A"))  # estrellita
        elif kind == "motoquero":
            c.rrect(8, 5, 16, 6, 3, hexc("C62E3A"))             # bandana
            for (x, y) in ((11, 7), (15, 6), (19, 8), (13, 9)):
                c.set(x, y, P["cream"])
            c.rect(22, 8, 4, 2, hexc("C62E3A"))                  # nudo
            c.rrect(9, 16, 14, 10, 4, hair)                      # barba grande
            c.vline(12, 19, 24, hair_lt); c.vline(17, 20, 25, hair_dk)
        else:  # blackmetal
            c.rrect(9, 5, 14, 5, 2, hair)
            c.rect(9, 8, 3, 14, hair); c.rect(20, 8, 3, 14, hair)
            c.vline(15, 5, 8, hair_dk)

        # ---- cara (ojos, cejas, boca) por arquetipo
        if kind == "blackmetal":
            # pintura de cadáver: manchas negras en ojos y boca
            for ex in (11, 18):
                c.rect(ex, 12, 4, 5, ink)
                c.set(ex + (1 if ex == 11 else 2), 14, P["white"] if not angry else P["red"])
            c.vline(15, 18, 21, ink); c.vline(16, 18, 21, ink)
            c.hline(13, 18, 20, ink if expr != "feliz" else mix(face, ink, 0.6))
            if expr == "feliz":
                c.hline(14, 17, 21, ink)
        elif kind in ("motoquero",):
            c.rect(10, 13, 12, 3, ink)                           # lentes de aviador
            c.hline(11, 13, 13, hexc("5A5660")); c.hline(18, 20, 13, hexc("5A5660"))
            brow = {"enojado": [(10, 11), (11, 12), (20, 11), (19, 12)], "impaciente": [(10, 11), (11, 11), (20, 11), (21, 11)]}
            for (x, y) in brow.get(expr, []):
                c.set(x, y, ink)
            if expr == "feliz":
                c.hline(13, 18, 21, P["red_dk"])
        else:
            draw_face(c, 15, 14, 19, expr, face, ink)
            if kind == "gotica":
                for ex in (12, 19):                             # sombra de ojos morada
                    c.set(ex - 1, 13, hexc("7A3E9A")); c.set(ex, 13, hexc("7A3E9A")); c.set(ex + 1, 13, hexc("7A3E9A"))
                c.hline(14, 17, 19, hexc("5A1E3A") if expr != "feliz" else hexc("5A1E3A"))  # labios oscuros
            if kind in ("crestas", "coletas"):
                c.set(19, 12, steel_lt)                           # piercing en la ceja
                c.set(13, 17, steel_lt)                           # aro en la nariz
            if kind == "melenudo" and expr == "feliz":
                # cuernos con la mano levantada
                c.rrect(25, 17, 5, 6, 1, skin)
                c.vline(25, 13, 17, skin); c.vline(29, 13, 17, skin)
                c.set(25, 12, ink); c.set(29, 12, ink)
        if kind == "coletas" or kind == "crestas":
            c.set(8, 15, steel_lt); c.set(23, 15, steel_lt)    # aros

        if expr == "impaciente":
            c.set(24, 9, P["sweat"]); c.rect(24, 10, 2, 2, P["sweat"]); c.set(24, 10, P["white"])
        if angry:
            for (vx, vy) in ((25, 5), (26, 4), (27, 5), (26, 6)):
                c.set(vx, vy, P["red"])
        c.outline()
        c.save("Characters/Customers/customer_hc_%d_%s.png" % (idx, expr), pivot=(16, 0))


def main():
    random.seed(1)
    gen_font()
    gen_background()
    gen_counter()
    gen_lamps()
    gen_neon()
    gen_taps()
    gen_glass()
    for i, kind in enumerate(HC_ARCHETYPES):
        gen_punk(i, kind)
    for i, spec in enumerate(CUSTOMERS):
        gen_customer(i, spec)
    gen_staff()
    gen_staff_hc()
    gen_ui()
    gen_fx()
    gen_party()
    gen_gear()
    gen_exterior()
    gen_logo_kid()
    gen_cars()
    gen_hardcore_look()
    gen_exterior_hardcore()
    with open(os.path.join(OUT, "sprite_meta.json"), "w") as f:
        json.dump(META, f, indent=1, sort_keys=True)
    print("Sprites generados:", len(META))


if __name__ == "__main__":
    main()
