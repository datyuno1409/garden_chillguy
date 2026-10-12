"""Hand-painted stone albedo (soft painterly rock study style), computed on the 3D model and written in UV space.

Run with a Python that has numpy (e.g. Blender's bundled one):
    python rock_paint.py <mesh.npz> <out.png> [<preview.png>] [size]

Colour is a function of the 3D position and normal, so the paint is continuous across UV seams.
Look: pale cool-white stone where the light hits, blue-grey (never black) shadows, warm olive bounce light at the
foot, taupe flaky patches built from angular dabs, bristly brush edges, fine paper grain. No dark stains, no moss.
"""
import struct
import sys
import zlib

import numpy as np

SIZE = 2048
PADDING = 16
CHUNK = 300_000
MIN_VALUE = 0.27                                   # nothing in the texture is darker than this
LIGHT = np.array([-0.45, -0.55, 0.70], np.float32)
LIGHT /= np.linalg.norm(LIGHT)

C = {name: np.array(v, np.float32) for name, v in {
    "lit": (0.84, 0.86, 0.78),                     # pale cool white, a little green
    "mid": (0.68, 0.69, 0.62),
    "shadow": (0.53, 0.55, 0.57),                  # neutral blue grey
    "deep": (0.41, 0.42, 0.44),                    # darkest shadow, still neutral
    "olive": (0.83, 0.82, 0.60),                   # warm bounce light
    "taupe": (0.66, 0.61, 0.52),
}.items()}


# ---------------------------------------------------------------- noise
def _hash(ix, iy, iz, seed):
    k = np.uint32((seed * 3266489917) % (2 ** 32))
    h = (ix.astype(np.uint32) * np.uint32(374761393)) ^ (iy.astype(np.uint32) * np.uint32(668265263)) \
        ^ (iz.astype(np.uint32) * np.uint32(2246822519)) ^ k
    h = (h ^ (h >> np.uint32(13))) * np.uint32(1274126177)
    h = h ^ (h >> np.uint32(16))
    return (h & np.uint32(0xFFFFFF)).astype(np.float32) / np.float32(0xFFFFFF)


def vnoise(p, seed):
    i = np.floor(p).astype(np.int32)
    f = (p - i).astype(np.float32)
    u = f * f * f * (f * (f * 6 - 15) + 10)
    x, y, z = i[:, 0], i[:, 1], i[:, 2]
    ux, uy, uz = u[:, 0], u[:, 1], u[:, 2]

    def c(dx, dy, dz):
        return _hash(x + dx, y + dy, z + dz, seed)

    x00 = c(0, 0, 0) + (c(1, 0, 0) - c(0, 0, 0)) * ux
    x10 = c(0, 1, 0) + (c(1, 1, 0) - c(0, 1, 0)) * ux
    x01 = c(0, 0, 1) + (c(1, 0, 1) - c(0, 0, 1)) * ux
    x11 = c(0, 1, 1) + (c(1, 1, 1) - c(0, 1, 1)) * ux
    y0 = x00 + (x10 - x00) * uy
    y1 = x01 + (x11 - x01) * uy
    return y0 + (y1 - y0) * uz


def fbm(p, seed, octaves=4):
    total, amp, norm, freq = np.zeros(len(p), np.float32), 1.0, 0.0, 1.0
    for o in range(octaves):
        total += amp * vnoise(p * freq + o * 17.3, seed + o * 31)
        norm += amp
        amp *= 0.5
        freq *= 2.03
    return total / norm


def cells(p, seed):
    """Cellular noise: (distance to the nearest feature point, random value of that cell)."""
    i = np.floor(p).astype(np.int32)
    f = (p - i).astype(np.float32)
    best = np.full(len(p), 9.0, np.float32)
    value = np.zeros(len(p), np.float32)
    x, y, z = i[:, 0], i[:, 1], i[:, 2]
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                ox = dx + _hash(x + dx, y + dy, z + dz, seed + 1) - f[:, 0]
                oy = dy + _hash(x + dx, y + dy, z + dz, seed + 2) - f[:, 1]
                oz = dz + _hash(x + dx, y + dy, z + dz, seed + 3) - f[:, 2]
                d = ox * ox + oy * oy + oz * oz
                nearer = d < best
                best = np.where(nearer, d, best)
                value = np.where(nearer, _hash(x + dx, y + dy, z + dz, seed + 7), value)
    return np.sqrt(best), value


def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    return a + (b - a) * t[:, None]


def tile(name, n):
    return np.tile(C[name], (n, 1))


# ---------------------------------------------------------------- brush strokes
_A = np.array([0.80, 0.30, 0.50], np.float32)
_A /= np.linalg.norm(_A)
_B = np.cross(_A, np.array([0.0, 0.0, 1.0], np.float32))
_B /= np.linalg.norm(_B)
_C = np.cross(_A, _B)


def stroke(q, seed, along, across):
    """Noise stretched along one fixed direction, like a painter sweeping the brush the same way."""
    p = np.stack([q @ _A * along, q @ _B * across, q @ _C * across], axis=1)
    return fbm(p.astype(np.float32), seed, 3)


def dabs(q, seed, along, across):
    """Angular flat dabs elongated along the brush direction: (edge distance, random value per dab)."""
    p = np.stack([q @ _A * along, q @ _B * across, q @ _C * across], axis=1).astype(np.float32)
    return cells(p, seed)


# ---------------------------------------------------------------- AI painted tiles (optional)
TILES = []          # relative-colour tiles (mean 1.0), filled by main() when a tiles folder is given
DETAIL_STRENGTH = 1.0
DETAIL_RANGE = (0.78, 1.24)   # keeps tile detail from adding black spots or blown highlights


def make_seamless(img):
    """Cross-fade the tile with a half-shifted copy of itself: both edges end up showing the same pixels."""
    h, w = img.shape
    wy = np.sin(np.pi * (np.arange(h) + 0.5) / h)[:, None]
    wx = np.sin(np.pi * (np.arange(w) + 0.5) / w)[None, :]
    weight = (wy * wx) ** 0.7
    shifted = np.roll(img, (h // 2, w // 2), axis=(0, 1))
    return img * weight + shifted * (1.0 - weight)


def load_tiles(folder):
    from pathlib import Path
    from PIL import Image
    tiles = []
    for path in sorted(Path(folder).glob("tile_*.png")):
        a = np.asarray(Image.open(path).convert("RGB"), np.float32) / 255.0
        luma = make_seamless(a @ np.array([0.299, 0.587, 0.114], np.float32))   # brush shapes only, no green tint
        rel = np.repeat((luma / max(float(luma.mean()), 1e-3))[:, :, None], 3, axis=2)
        tiles.append(np.clip(rel, *DETAIL_RANGE).astype(np.float32))
    return tiles


def bilinear(tile, u, v):
    h, w, _ = tile.shape
    x = (u % 1.0) * w - 0.5
    y = (v % 1.0) * h - 0.5
    x0, y0 = np.floor(x).astype(np.int32), np.floor(y).astype(np.int32)
    fx, fy = (x - x0)[:, None], (y - y0)[:, None]
    x0, y0 = x0 % w, y0 % h
    x1, y1 = (x0 + 1) % w, (y0 + 1) % h
    top = tile[y0, x0] * (1 - fx) + tile[y0, x1] * fx
    bottom = tile[y1, x0] * (1 - fx) + tile[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def triplanar(tile, P, N, scale, angle=0.0):
    w = np.abs(N) ** 4
    w /= np.maximum(w.sum(axis=1, keepdims=True), 1e-9)
    c, s = np.cos(angle), np.sin(angle)
    out = np.zeros((len(P), 3), np.float32)
    for axis, (a, b) in enumerate(((1, 2), (0, 2), (0, 1))):
        u, v = P[:, a] * scale, P[:, b] * scale
        out += w[:, axis:axis + 1] * bilinear(tile, u * c - v * s, u * s + v * c)
    return out


def painted_detail(P, N):
    """Brush-shape detail from the AI tiles: three rotated, differently scaled layers chosen by soft noise masks,
    so the tile never shows up as a repeating pattern or a mirror-symmetric ornament."""
    if not TILES:
        return None
    count = len(TILES)
    layer_a = triplanar(TILES[0], P, N, 2.0, 0.0)
    layer_b = triplanar(TILES[1 % count], P, N, 2.9, 0.95)
    layer_c = triplanar(TILES[2 % count], P, N, 3.7, 2.15)
    m1 = ss(0.46, 0.54, fbm(P * 0.9 + 11.0, 67, 3))
    m2 = ss(0.46, 0.54, fbm(P * 0.9 + 29.0, 71, 3))
    out = layer_a + (layer_b - layer_a) * m1[:, None]
    return out + (layer_c - out) * m2[:, None]


# ---------------------------------------------------------------- painting
def paint(P, N, F, z_min):
    n = len(P)
    q = P.astype(np.float32)
    warp = np.stack([fbm(q * 0.8, 3, 2), fbm(q * 0.8, 5, 2), fbm(q * 0.8, 7, 2)], axis=1) - 0.5
    qw = q + warp * 0.5
    bristle = stroke(q, 71, 1.2, 38.0)

    # light: pale where lit, blue grey in shadow, painted in soft steps with a bristly edge
    ndl = N @ LIGHT + 0.12 * (bristle - 0.5)
    col = tile("deep", n)
    col = mix(col, tile("shadow", n), ss(-0.35, -0.08, ndl))
    col = mix(col, tile("mid", n), ss(0.00, 0.22, ndl))
    col = mix(col, tile("lit", n), ss(0.34, 0.58, ndl))

    # warm olive bounce light near the foot, on the lit side
    low = 1.0 - ss(z_min + 0.15, z_min + 1.45, q[:, 2])
    col = mix(col, tile("olive", n), 0.70 * low * ss(0.05, 0.40, ndl))

    # yellow-green wash drifting over the lit stone
    wash = ss(0.45, 0.68, fbm(qw * 0.9 + 2.0, 13, 3) + 0.10 * (bristle - 0.5))
    col = mix(col, tile("olive", n), 0.38 * wash * ss(0.2, 0.5, ndl))

    # earthy warm foot, whatever the light does, so the underside never turns blue
    col = mix(col, tile("taupe", n) * 0.82, 0.45 * low)

    # large soft taupe patches
    tp = fbm(qw * 1.35 + 5.0, 19, 3) + 0.12 * (bristle - 0.5)
    taupe = C["taupe"][None, :] * (0.72 + 0.40 * ss(-0.2, 0.5, ndl))[:, None]
    col = mix(col, taupe, 0.70 * ss(0.50, 0.57, tp))

    # flaky angular dabs of taupe and pale stone, in wide zones, like the reference study
    zone = ss(0.32, 0.46, fbm(qw * 1.1, 59, 2))
    d1, id1 = dabs(q, 101, 2.2, 7.5)
    d2, id2 = dabs(q, 131, 4.5, 15.0)
    col = col * (1.0 + ((id1 - 0.5) * 0.26 * zone + (id2 - 0.5) * 0.15 * zone))[:, None]
    col = mix(col, taupe, 0.85 * ss(0.62, 0.72, id1) * zone)
    col = mix(col, np.minimum(col * 1.15 + 0.07, 1.0), 0.75 * ss(0.30, 0.18, id1) * zone)
    col = mix(col, taupe, 0.60 * ss(0.64, 0.76, id2) * zone)

    # light chipped edges where the smooth normal departs from the facet normal
    crease = 1.0 - np.clip(np.sum(N * F, axis=1), 0.0, 1.0)
    edge = ss(0.012, 0.05, crease) * (0.6 + 0.4 * fbm(q * 9.0, 37, 2))
    col = mix(col, np.minimum(col * 1.14 + 0.05, 1.0), 0.8 * edge)

    # painted detail from the AI tiles: multiplies the baked light, so the shading stays intact
    detail = painted_detail(q, N)
    if detail is not None:
        # busy where the painter would work hard (thick flaky dabs), smooth elsewhere, stronger in the light
        busy = 0.15 + 1.25 * ss(0.38, 0.62, fbm(qw * 0.7 + 41.0, 79, 3))
        busy = busy * (0.65 + 0.55 * ss(-0.1, 0.5, ndl))
        col = col * (1.0 + DETAIL_STRENGTH * (detail - 1.0) * busy[:, None])

    # fine brush streaks and paper grain
    col = col * (0.95 + 0.10 * stroke(q, 91, 2.0, 70.0))[:, None]
    g = fbm(q * 24.0, 41, 3)
    col = col * (1.0 + 0.05 * (g - 0.5) * 2.0)[:, None]
    return np.clip(np.maximum(col, MIN_VALUE), 0.0, 1.0)


# ---------------------------------------------------------------- raster
def rasterise(P, N, F, UV, size):
    pix_index, pos, nrm, fac = [], [], [], []
    for t in range(len(P)):
        uv = UV[t] * size
        (x0, y0), (x1, y1), (x2, y2) = uv
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-9:
            continue
        lo = np.maximum(np.floor(uv.min(axis=0)).astype(int) - 1, 0)
        hi = np.minimum(np.ceil(uv.max(axis=0)).astype(int) + 1, size - 1)
        xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        px, py = xs.ravel() + 0.5, ys.ravel() + 0.5
        a = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / den
        b = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / den
        c = 1.0 - a - b
        inside = (a >= -1e-4) & (b >= -1e-4) & (c >= -1e-4)
        if not inside.any():
            continue
        a, b, c = a[inside][:, None], b[inside][:, None], c[inside][:, None]
        pix_index.append((ys.ravel()[inside] * size + xs.ravel()[inside]))
        pos.append(a * P[t, 0] + b * P[t, 1] + c * P[t, 2])
        n = a * N[t, 0] + b * N[t, 1] + c * N[t, 2]
        nrm.append(n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9))
        fac.append(np.tile(F[t], (len(a), 1)))
    return (np.concatenate(pix_index), np.concatenate(pos).astype(np.float32),
            np.concatenate(nrm).astype(np.float32), np.concatenate(fac).astype(np.float32))


def dilate(img, filled, iterations):
    img, filled = img.copy(), filled.copy()
    for _ in range(iterations):
        grown, new = img.copy(), filled.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            src_f = np.roll(filled, (dy, dx), axis=(0, 1))
            src_c = np.roll(img, (dy, dx), axis=(0, 1))
            take = src_f & ~new
            grown[take] = src_c[take]
            new |= take
        img, filled = grown, new
    return img, filled


def write_png(path, rgb8):
    h, w, _ = rgb8.shape
    raw = np.concatenate([np.zeros((h, 1), np.uint8), rgb8.reshape(h, w * 3)], axis=1).tobytes()

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def main(argv):
    mesh, out = argv[1], argv[2]
    preview = argv[3] if len(argv) > 3 else None
    size = int(argv[4]) if len(argv) > 4 else SIZE
    if len(argv) > 5:
        TILES.extend(load_tiles(argv[5]))
        print(f"loaded {len(TILES)} painted tiles from {argv[5]}")
    d = np.load(mesh)
    P, N, F, UV = d["P"], d["N"], d["F"], d["UV"]
    z_min = float(P[..., 2].min())
    idx, pos, nrm, fac = rasterise(P, N, F, UV, size)
    colour = np.zeros((len(idx), 3), np.float32)
    for s in range(0, len(idx), CHUNK):
        sl = slice(s, s + CHUNK)
        colour[sl] = paint(pos[sl], nrm[sl], fac[sl], z_min)
    img = np.zeros((size * size, 3), np.float32)
    filled = np.zeros(size * size, bool)
    img[idx] = colour
    filled[idx] = True
    img, filled = dilate(img.reshape(size, size, 3), filled.reshape(size, size), PADDING)
    img[~filled] = C["mid"]
    rgb8 = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    write_png(out, rgb8[::-1])                                 # PNG rows run top to bottom, UV v runs bottom to top
    if preview:
        k = size // 768 if size >= 768 else 1
        small = rgb8[: (size // k) * k, : (size // k) * k].reshape(size // k, k, size // k, k, 3).mean(axis=(1, 3))
        write_png(preview, small.astype(np.uint8)[::-1])
    print(f"painted {len(idx):,} pixels ({100.0 * len(idx) / (size * size):.0f}% of the texture), "
          f"darkest value {rgb8[filled].min()} / 255 -> {out}")


if __name__ == "__main__":
    main(sys.argv)
