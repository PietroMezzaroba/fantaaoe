"""Trasforma l'elmo del logo AoE Italia in una nuvola di punti 3D per la landing.

Uso: python3 genera_punti.py logo-aoe-italia.png ../public/elmo-punti.json
Uscita: {"n": N, "p": [x, y, z, classe, ...]} con coordinate intere centrate.
Classi: 0 acciaio dell'elmo, 1 verde, 2 bianco, 3 rosso del pennacchio.
"""
import json, random, sys
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
random.seed(7)
im = Image.open(src).convert("RGB").crop((170, 250, 1690, 1520))
w, h = im.size
bg = (23, 33, 45)
px = im.load()

def classe(r, g, b):
    if ((r - bg[0])**2 + (g - bg[1])**2 + (b - bg[2])**2) ** 0.5 < 60:
        return None
    if r > 150 and g < 70 and b < 70: return 3
    if g > 80 and r < 60 and b < 90: return 1
    if r > 215 and g > 215 and b > 215: return 2
    return 0

# maschera dell'elmo, sfocata: dà lo spessore (più alto al centro, sottile ai bordi)
maschera = Image.new("L", (w, h))
mp = maschera.load()
for y in range(h):
    for x in range(w):
        mp[x, y] = 0 if classe(*px[x, y]) is None else 255
rilievo = maschera.filter(ImageFilter.GaussianBlur(55)).load()

passo = 9
punti = []
for y in range(0, h, passo):
    for x in range(0, w, passo):
        jx, jy = x + random.randint(-3, 3), y + random.randint(-3, 3)
        if not (0 <= jx < w and 0 <= jy < h): continue
        c = classe(*px[jx, jy])
        if c is None: continue
        z = (rilievo[jx, jy] / 255) ** 0.7 * 150 + random.uniform(-6, 6)
        if c: z += 25  # il pennacchio sta davanti
        punti += [round(jx - w / 2), round(jy - h / 2), round(z - 75), c]
n = len(punti) // 4
json.dump({"n": n, "p": punti}, open(out, "w"), separators=(",", ":"))
print(n, "punti")
