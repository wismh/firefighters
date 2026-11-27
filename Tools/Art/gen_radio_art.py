"""Pixel art for the dispatcher's radio: her portrait (mouth closed, two talking frames, a blink), the static
overlay and the panel frame.

Run: python Tools/Art/gen_radio_art.py  -> writes Assets/_Project/Art/Radio/*.png
The portrait is drawn at 48x48 from shapes, then features are placed by hand, outlined and scaled x4.
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Art", "Radio")
N = 48
SCALE = 4
rng = np.random.default_rng(11)


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


P = {
    "bg0": hexc("#141a2c"), "bg1": hexc("#1e2740"), "bg2": hexc("#2a3556"),
    "glow": hexc("#5a3420"),
    "ink": hexc("#120d16"),
    "hair": hexc("#5c2b1c"), "hairL": hexc("#8c4628"), "hairD": hexc("#3c1a12"), "hairR": hexc("#c4683a"),
    "skin": hexc("#e2a982"), "skinD": hexc("#b97a5c"), "skinDD": hexc("#94573f"), "skinL": hexc("#f3c7a2"),
    "skinR": hexc("#ffd2a0"),
    "eyeW": hexc("#efe6df"), "eye": hexc("#2b2c45"), "brow": hexc("#4a2216"),
    "lip": hexc("#b5544c"), "lipD": hexc("#7c2a2c"), "mouth": hexc("#3e1418"), "teeth": hexc("#f2e9e2"),
    "set": hexc("#323743"), "setL": hexc("#5d6474"), "setD": hexc("#1d2029"), "led": hexc("#ff5a3a"),
    "navy": hexc("#1f2c4e"), "navyL": hexc("#33467a"), "navyD": hexc("#141c33"),
    "shirt": hexc("#d6dbe5"), "shirtD": hexc("#a2a9b8"),
    "gold": hexc("#f2b233"), "orange": hexc("#e8622a"),
}


def blank():
    return np.zeros((N, N, 4), dtype=np.uint8)


def put(img, x, y, c):
    if 0 <= x < N and 0 <= y < N:
        img[y, x] = P[c]


def ellipse(img, cx, cy, rx, ry, c, mask=None):
    for y in range(N):
        for x in range(N):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1.0 and (mask is None or mask(x, y)):
                img[y, x] = P[c]


def is_set(img, x, y):
    return 0 <= x < N and 0 <= y < N and img[y, x, 3] > 0


def face_shape(x, y):
    """Egg shaped face: wide cheeks, narrower jaw."""
    cx, cy = 23.5, 23.0
    ry = 11.5
    t = (y + 0.5 - cy) / ry
    if abs(t) > 1:
        return False
    rx = 9.6 if t < 0.25 else 9.6 - (t - 0.25) * 6.2
    return abs(x + 0.5 - cx) <= rx * np.sqrt(max(0.0, 1 - t * t * 0.55))


def draw_base():
    img = blank()
    # Uniform: shoulders, darker on the right, a lighter edge on the left where the fire light falls.
    ellipse(img, 24, 54, 23, 16, "navy")
    for y in range(N):
        for x in range(N):
            if is_set(img, x, y):
                if x > 31:
                    img[y, x] = P["navyD"]
                elif x < 9 and y > 40:
                    img[y, x] = P["navyL"]
    # Neck in shadow under the jaw.
    for y in range(31, 41):
        for x in range(19, 29):
            put(img, x, y, "skinD" if x < 26 else "skinDD")
    # Shirt collar: a V of white under the neck.
    for y in range(38, 48):
        w = (y - 38) // 2 + 2
        for x in range(24 - w - 3, 24 - w + 1):
            put(img, x, y, "shirt")
        for x in range(24 + w - 1, 24 + w + 3):
            put(img, x, y, "shirtD")
    for y in range(38, 44):
        put(img, 24 - (y - 38) // 2 - 1, y, "shirt")

    # Hair behind the head: a bob down to the jaw and a bun on top-right.
    ellipse(img, 23.5, 19, 13, 12.5, "hair")
    for y in range(18, 34):
        for x in range(10, 38):
            side = x < 15 or x > 32
            if side and abs(x + 0.5 - 23.5) < 14 - (y - 18) * 0.22:
                put(img, x, y, "hair")
    ellipse(img, 33, 7.5, 5.2, 4.6, "hair")

    # Face over the hair.
    for y in range(N):
        for x in range(N):
            if face_shape(x, y):
                img[y, x] = P["skin"]
    # Fringe swept to the right over the forehead.
    for y in range(10, 18):
        for x in range(12, 36):
            edge = 15.5 + (x - 12) * 0.12 + np.sin(x * 0.9) * 0.8
            if y < edge and abs(x + 0.5 - 23.5) < 12.2:
                put(img, x, y, "hair")
    return img


def shade(img):
    """Light from the left (the fire), shadow on the right; hair gets its own ramp."""
    out = img.copy()
    for y in range(N):
        for x in range(N):
            c = tuple(img[y, x])
            if c == P["skin"]:
                if x >= 29 or (x >= 27 and y >= 29):
                    out[y, x] = P["skinD"]
                elif x <= 16 and 20 <= y <= 30:
                    out[y, x] = P["skinL"]
                if x == 15 and 19 <= y <= 28 and not face_shape(x - 1, y):
                    out[y, x] = P["skinR"]
            elif c == P["hair"]:
                band = np.sin(x * 0.55 + y * 0.35)
                if x >= 30 or y >= 29:
                    out[y, x] = P["hairD"]
                elif band > 0.75 and x < 26:
                    out[y, x] = P["hairL"]
                if x <= 12 and not is_set(img, x - 1, y):
                    out[y, x] = P["hairR"]
    # Bun highlight.
    for x, y in [(31, 5), (32, 5), (30, 6), (31, 6)]:
        out[y, x] = P["hairL"]
    return out


def headset(img):
    # Band over the top of the head, from ear to ear.
    for i in range(0, 61):
        a = np.pi * (1 - i / 60)
        x = 23.5 + np.cos(a) * 13.2
        y = 19.5 - np.sin(a) * 12.8
        put(img, int(round(x)), int(round(y)), "set")
        put(img, int(round(x)), int(round(y)) + 1, "setD" if x > 23.5 else "setL")
    # Ear cups.
    for y in range(19, 28):
        for x in range(8, 13):
            put(img, x, y, "setL" if x == 8 or y == 19 else "set")
        for x in range(35, 39):
            put(img, x, y, "setD")
    put(img, 9, 21, "led")
    # Microphone boom from the left cup to the corner of the mouth.
    boom = [(12, 27), (13, 28), (13, 29), (14, 30), (15, 31), (16, 31), (17, 32), (18, 32)]
    for x, y in boom:
        put(img, x, y, "setD")
    for x, y in [(18, 31), (19, 31), (18, 32), (19, 32), (19, 33)]:
        put(img, x, y, "set")
    put(img, 18, 31, "setL")
    return img


def badge(img):
    # A small flame on the left of the chest.
    flame = ["..o..", ".ooo.", "oogoo", "ogggo", ".ggg."]
    for j, row in enumerate(flame):
        for i, ch in enumerate(row):
            if ch == "o":
                put(img, 12 + i, 40 + j, "orange")
            elif ch == "g":
                put(img, 12 + i, 40 + j, "gold")
    return img


def eyes(img, closed):
    # Tired, kind eyes: heavy lids, a shadow underneath.
    for ex in (18, 27):
        if closed:
            # Closed lids: a line curving down at the outer corner.
            for x in range(ex, ex + 4):
                put(img, x, 23, "ink")
            put(img, ex - 1 if ex == 18 else ex + 4, 24, "ink")
        else:
            for x in range(ex, ex + 4):
                put(img, x, 22, "ink")
            put(img, ex, 23, "eyeW")
            put(img, ex + 1, 23, "eye")
            put(img, ex + 2, 23, "eye")
            put(img, ex + 3, 23, "eyeW")
            put(img, ex + 1, 24, "eye")
            put(img, ex + 2, 24, "eyeW" if ex == 18 else "eye")
        for x in range(ex, ex + 4):
            put(img, x, 25, "skinD")
    # Brows: one a little raised, unimpressed.
    for x, y in [(17, 20), (18, 19), (19, 19), (20, 19), (21, 20)]:
        put(img, x, y, "brow")
    for x, y in [(27, 19), (28, 19), (29, 19), (30, 19)]:
        put(img, x, y, "brow")
    return img


def nose(img):
    for x, y in [(24, 24), (24, 25), (25, 26), (25, 27), (24, 28), (23, 28)]:
        put(img, x, y, "skinD")
    put(img, 22, 27, "skinL")
    return img


def mouth(img, frame):
    if frame == 0:
        for x in range(21, 27):
            put(img, x, 31, "lipD")
        put(img, 20, 30, "skinD")
        put(img, 27, 31, "skinD")
        for x in range(22, 26):
            put(img, x, 32, "lip")
    elif frame == 1:
        for x in range(21, 27):
            put(img, x, 30, "lip")
        for x in range(21, 27):
            put(img, x, 31, "mouth")
        for x in range(22, 26):
            put(img, x, 31, "teeth") if x < 24 else None
        for x in range(22, 26):
            put(img, x, 32, "lip")
    else:
        for x in range(21, 27):
            put(img, x, 29, "lip")
        for y in (30, 31, 32):
            for x in range(21 if y < 32 else 22, 27 if y < 32 else 26):
                put(img, x, y, "mouth")
        for x in range(22, 26):
            put(img, x, 30, "teeth")
        for x in range(22, 26):
            put(img, x, 33, "lip")
    return img


def outline(img):
    out = img.copy()
    for y in range(N):
        for x in range(N):
            if is_set(img, x, y):
                continue
            near = any(is_set(img, x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if near:
                out[y, x] = P["ink"]
    return out


def background():
    img = np.zeros((N, N, 4), dtype=np.uint8)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    for y in range(N):
        for x in range(N):
            # Dark blue, warmer towards the bottom-left where the fire glows.
            d = np.hypot(x - 4, y - 46) / 60
            t = 1 - d
            if t + bayer[y % 4, x % 4] * 0.35 > 0.78:
                c = P["glow"]
            else:
                v = y / N + bayer[y % 4, x % 4] * 0.25
                c = P["bg2"] if v > 0.85 else P["bg1"] if v > 0.45 else P["bg0"]
            img[y, x] = c
    return img


def compose(img, bg):
    out = bg.copy()
    mask = img[:, :, 3] > 0
    out[mask] = img[mask]
    return out


def save(name, img, scale=SCALE):
    os.makedirs(OUT, exist_ok=True)
    pic = Image.fromarray(img, "RGBA")
    if scale != 1:
        pic = pic.resize((img.shape[1] * scale, img.shape[0] * scale), Image.NEAREST)
    pic.save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


def portrait(frame, closed_eyes=False):
    img = draw_base()
    img = shade(img)
    img = headset(img)
    img = badge(img)
    img = eyes(img, closed_eyes)
    img = nose(img)
    img = mouth(img, frame)
    img = outline(img)
    return compose(img, background())


def static_noise():
    size = 128
    rows = np.repeat(rng.random((size, 1)) * 0.5, size, axis=1)
    grain = rng.random((size, size))
    v = np.clip(grain * 0.8 + rows, 0, 1)
    scan = (np.arange(size) % 3 == 0)[:, None]
    v = np.where(scan, v * 0.35, v)
    img = np.zeros((size, size, 4), dtype=np.uint8)
    img[:, :, 0] = img[:, :, 1] = img[:, :, 2] = (v * 255).astype(np.uint8)
    img[:, :, 3] = (np.clip(v * 1.2, 0, 1) * 255).astype(np.uint8)
    return img


def frame(size, border, fill, edge, light, corner_cut=2):
    """A 9-sliceable pixel frame: dark fill, a bright edge line, a light inner bevel, cut corners."""
    img = np.zeros((size, size, 4), dtype=np.uint8)
    for y in range(size):
        for x in range(size):
            d = min(x, y, size - 1 - x, size - 1 - y)
            corner = min(x, size - 1 - x) + min(y, size - 1 - y) < corner_cut
            if corner:
                continue
            if d == 0:
                img[y, x] = P["ink"]
            elif d < border:
                img[y, x] = edge
            elif d == border:
                img[y, x] = light
            else:
                img[y, x] = fill
    return img


def main():
    save("Dispatcher_Idle", portrait(0))
    save("Dispatcher_Talk1", portrait(1))
    save("Dispatcher_Talk2", portrait(2))
    save("Dispatcher_Blink", portrait(0, True))
    save("Static", static_noise(), 1)
    save("PanelFrame", frame(16, 2, hexc("#141a2c", 236), hexc("#e8622a"), hexc("#2f3a5e")), 2)
    save("PortraitFrame", frame(16, 2, (0, 0, 0, 0), hexc("#f2b233"), hexc("#7a5420")), 2)


if __name__ == "__main__":
    main()
