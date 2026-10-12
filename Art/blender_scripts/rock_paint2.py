"""Hand-painted rock, painted the way an artist does it (see the step-by-step rock tutorial):

1. block in flat planes: the mesh is split into facets and every facet gets its own value from the light direction,
2. soft gradient inside each facet and dark shadow tucked into the concave creases,
3. hard-edged, broken highlight strokes along convex edges, textured-brush grain and streaks,
4. a few straight cracks and chips, earthy foot. No moss, no black.

    python rock_paint2.py <mesh.npz> <out.png> [<preview.png>] [size]
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rock_paint as rp                                    # noqa: E402  (noise, dilate, png writer)

SIZE = 2048
PADDING = 16
CHUNK = 250_000
FACET_ANGLE_DEG = 21.0
MIN_FACET_AREA_FRACTION = 0.012
MIN_VALUE = 0.24
LIGHT = np.array([-0.45, -0.55, 0.70], np.float32)
LIGHT /= np.linalg.norm(LIGHT)

PAL = {k: np.array(v, np.float32) for k, v in {
    "shadow": (0.42, 0.42, 0.43),
    "mid": (0.58, 0.58, 0.56),
    "light": (0.79, 0.78, 0.73),
    "highlight": (0.90, 0.90, 0.85),
    "earth": (0.50, 0.46, 0.38),
    "warm": (0.70, 0.64, 0.52),
}.items()}


# ---------------------------------------------------------------- facets
def edge_graph(P):
    """neighbour[t][k]: triangle across the edge opposite vertex k (or -1)."""
    def key(v):
        return tuple(np.round(v * 1e4).astype(np.int64))
    edges = {}
    for t in range(len(P)):
        ks = [key(P[t, k]) for k in range(3)]
        for k in range(3):
            a, b = ks[(k + 1) % 3], ks[(k + 2) % 3]
            edges.setdefault((min(a, b), max(a, b)), []).append((t, k))
    neighbour = -np.ones((len(P), 3), np.int32)
    for pair in edges.values():
        if len(pair) == 2:
            (t0, k0), (t1, k1) = pair
            neighbour[t0, k0], neighbour[t1, k1] = t1, t0
    return neighbour


def segment_facets(P, F, neighbour):
    area = 0.5 * np.linalg.norm(np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]), axis=1)
    label = -np.ones(len(P), np.int32)
    limit = math.cos(math.radians(FACET_ANGLE_DEG))
    count = 0
    for seed in np.argsort(-area):
        if label[seed] >= 0:
            continue
        label[seed] = count
        mean = F[seed] * area[seed]
        stack = [seed]
        while stack:
            t = stack.pop()
            for nb in neighbour[t]:
                if nb < 0 or label[nb] >= 0:
                    continue
                if float(F[nb] @ (mean / np.linalg.norm(mean))) >= limit:
                    label[nb] = count
                    mean = mean + F[nb] * area[nb]
                    stack.append(nb)
        count += 1
    total = float(area.sum())
    while True:                                                     # fold tiny facets into their best neighbour
        areas = np.bincount(label, weights=area)
        normals = np.zeros((label.max() + 1, 3))
        for t in range(len(P)):
            normals[label[t]] += F[t] * area[t]
        small = [l for l in np.argsort(areas) if 0 < areas[l] < total * MIN_FACET_AREA_FRACTION]
        if not small:
            break
        l = small[0]
        votes = {}
        for t in np.where(label == l)[0]:
            for nb in neighbour[t]:
                if nb >= 0 and label[nb] != l:
                    votes[label[nb]] = votes.get(label[nb], 0) + 1
        if not votes:
            areas[l] = total
            break
        mine = normals[l] / max(np.linalg.norm(normals[l]), 1e-9)
        target = max(votes, key=lambda o: float(normals[o] / max(np.linalg.norm(normals[o]), 1e-9) @ mine) + 0.01 * votes[o])
        label[label == l] = target
        _, label = np.unique(label, return_inverse=True)
    n = label.max() + 1
    normals = np.zeros((n, 3), np.float32)
    for t in range(len(P)):
        normals[label[t]] += F[t] * area[t]
    normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
    return label, normals


def edge_flags(P, F, neighbour, label, facet_normal):
    """For every triangle edge: facet border?, convex?, altitude of the triangle over that edge, and how much
    lighter this facet is than the one across the edge (a highlight belongs on the lighter side)."""
    T = len(P)
    border = np.zeros((T, 3), bool)
    convex = np.zeros((T, 3), bool)
    lit_step = np.zeros((T, 3), np.float32)
    altitude = np.zeros((T, 3), np.float32)
    centre = P.mean(axis=1)
    area2 = np.linalg.norm(np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]), axis=1)
    for t in range(T):
        for k in range(3):
            length = np.linalg.norm(P[t, (k + 1) % 3] - P[t, (k + 2) % 3])
            altitude[t, k] = area2[t] / max(length, 1e-9)
            nb = neighbour[t, k]
            if nb >= 0 and label[nb] != label[t]:
                border[t, k] = True
                convex[t, k] = float((centre[nb] - centre[t]) @ F[t]) < 0.0
                lit_step[t, k] = float((facet_normal[label[t]] - facet_normal[label[nb]]) @ LIGHT)
    return border, convex, altitude, lit_step


# ---------------------------------------------------------------- raster
def rasterise(P, N, UV, size):
    index, tri, bary = [], [], []
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
        index.append(ys.ravel()[inside] * size + xs.ravel()[inside])
        tri.append(np.full(int(inside.sum()), t, np.int32))
        bary.append(np.stack([a[inside], b[inside], c[inside]], axis=1).astype(np.float32))
    return np.concatenate(index), np.concatenate(tri), np.concatenate(bary)


def cells2(p, seed):
    """Cellular noise returning (F1, F2); F2 - F1 is small on the straight borders between cells."""
    i = np.floor(p).astype(np.int32)
    f = (p - i).astype(np.float32)
    f1 = np.full(len(p), 9.0, np.float32)
    f2 = np.full(len(p), 9.0, np.float32)
    x, y, z = i[:, 0], i[:, 1], i[:, 2]
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                ox = dx + rp._hash(x + dx, y + dy, z + dz, seed + 1) - f[:, 0]
                oy = dy + rp._hash(x + dx, y + dy, z + dz, seed + 2) - f[:, 1]
                oz = dz + rp._hash(x + dx, y + dy, z + dz, seed + 3) - f[:, 2]
                d = np.sqrt(ox * ox + oy * oy + oz * oz)
                closer = d < f1
                f2 = np.where(closer, f1, np.minimum(f2, d))
                f1 = np.where(closer, d, f1)
    return f1, f2


# ---------------------------------------------------------------- painting
def palette(v):
    """v in 0..1 -> shadow / mid / light colours."""
    n = len(v)
    col = np.tile(PAL["shadow"], (n, 1))
    col = rp.mix(col, np.tile(PAL["mid"], (n, 1)), rp.ss(0.0, 0.5, v))
    return rp.mix(col, np.tile(PAL["light"], (n, 1)), rp.ss(0.5, 1.0, v))


def paint(q, N, tri, bary, scene):
    n = len(q)
    label = scene["label"][tri]
    Nf = scene["facet_normal"][label]
    zmin, zmax = scene["zmin"], scene["zmax"]

    # 1. flat value per facet from the light, with a little random variety per facet
    ndl_f = Nf @ LIGHT
    jitter = (rp._hash(label.astype(np.int32), label.astype(np.int32) * 7, label.astype(np.int32) * 13, 5) - 0.5) * 0.10
    v_f = np.clip(rp.ss(-0.45, 0.85, ndl_f) + jitter, 0.0, 1.0)
    col = palette(v_f)

    # 2. gradient inside the facet: follow the smooth normal and get darker towards the ground
    v_s = rp.ss(-0.45, 0.85, N @ LIGHT)
    col = col * (1.0 + 0.45 * (v_s - rp.ss(-0.45, 0.85, ndl_f)))[:, None]
    height = np.clip((q[:, 2] - zmin) / max(zmax - zmin, 1e-6), 0.0, 1.0)
    col = col * (0.86 + 0.20 * height)[:, None]

    # distance to the nearest convex / concave facet border (metres)
    inf = np.float32(9.0)
    d_convex = np.full(n, inf, np.float32)
    d_concave = np.full(n, inf, np.float32)
    step_convex = np.zeros(n, np.float32)
    for k in range(3):
        d = bary[:, k] * scene["altitude"][tri, k]
        border = scene["border"][tri, k]
        cvx = scene["convex"][tri, k]
        nearer = border & cvx & (d < d_convex)
        d_convex = np.where(nearer, d, d_convex)
        step_convex = np.where(nearer, scene["lit_step"][tri, k], step_convex)
        d_concave = np.where(border & ~cvx, np.minimum(d_concave, d), d_concave)

    # brush direction per facet: streaks run along a random direction inside the facet plane
    ang = rp._hash(label.astype(np.int32), label.astype(np.int32) * 3, label.astype(np.int32) * 11, 9) * math.pi
    ref = np.where((np.abs(Nf[:, 2]) < 0.9)[:, None], np.array([0, 0, 1], np.float32), np.array([1, 0, 0], np.float32))
    ta = np.cross(Nf, ref)
    ta /= np.maximum(np.linalg.norm(ta, axis=1, keepdims=True), 1e-9)
    tb = np.cross(Nf, ta)
    t1 = ta * np.cos(ang)[:, None] + tb * np.sin(ang)[:, None]
    t2 = np.cross(Nf, t1)
    streak = rp.fbm(np.stack([np.sum(q * t1, 1) * 1.5, np.sum(q * t2, 1) * 22.0, np.sum(q * Nf, 1) * 22.0], axis=1)
                    .astype(np.float32), 17, 3)
    col = col * (0.89 + 0.22 * streak)[:, None]

    # soft warm / cool mottling so the planes are not sterile
    drift = rp.fbm(q * 1.3 + 8.0, 23, 3)
    col = rp.mix(col, col * np.array([1.10, 1.02, 0.88], np.float32), 0.55 * rp.ss(0.45, 0.65, drift))

    # 3. shadow tucked into concave creases
    col = col * (1.0 - 0.40 * (1.0 - rp.ss(0.0, 0.12, d_concave)))[:, None]

    # 4. hard-edged highlight on convex edges, broken like a dry brush
    broken = rp.stroke(q, 71, 0.9, 12.0) - 0.5               # slow variation along the edge: dashes, not beads
    width = 0.014 + 0.022 * v_f
    band = 1.0 - rp.ss(0.0, 0.005, d_convex - width - 0.012 * broken)
    dashes = rp.ss(0.40, 0.55, rp.stroke(q, 83, 1.2, 18.0))   # the stroke lifts off the paper here and there
    lighter_side = 0.10 + 0.90 * rp.ss(0.02, 0.35, step_convex)
    glow = 1.0 - rp.ss(0.0, 0.16, d_convex)
    col = col * (1.0 + 0.10 * glow * lighter_side)[:, None]
    col = rp.mix(col, np.tile(PAL["highlight"], (n, 1)), 0.85 * band * dashes * lighter_side * (0.35 + 0.65 * v_f))

    # 5. straight cracks and chips: borders between big cells, only in some zones
    f1, f2 = cells2(np.stack([q[:, 0], q[:, 1], q[:, 2]], axis=1).astype(np.float32) * 1.7, 211)
    zone = rp.ss(0.50, 0.62, rp.fbm(q * 0.9 + 3.0, 59, 2))
    crack = (1.0 - rp.ss(0.0, 0.045, f2 - f1)) * zone
    col = col * (1.0 - 0.50 * crack)[:, None]
    chip = rp.ss(0.965, 0.985, rp.vnoise(q * 55.0, 61))
    col = rp.mix(col, np.minimum(col * 1.25 + 0.06, 1.0), 0.6 * chip)

    # 6. earthy foot
    foot = 1.0 - rp.ss(zmin + 0.04, zmin + 0.40, q[:, 2])
    col = rp.mix(col, np.tile(PAL["earth"], (n, 1)) * (0.8 + 0.3 * height)[:, None], 0.5 * foot)

    # 7. textured-brush grain over everything
    grain = rp.fbm(q * 55.0, 5, 2) - 0.5
    col = col * (1.0 + 0.18 * grain)[:, None]
    return np.clip(np.maximum(col, MIN_VALUE), 0.0, 1.0)


def main(argv):
    mesh, out = argv[1], argv[2]
    preview = argv[3] if len(argv) > 3 else None
    size = int(argv[4]) if len(argv) > 4 else SIZE
    d = np.load(mesh)
    P, N, F, UV = d["P"].astype(np.float32), d["N"].astype(np.float32), d["F"].astype(np.float32), d["UV"]
    neighbour = edge_graph(P)
    label, facet_normal = segment_facets(P, F, neighbour)
    border, convex, altitude, lit_step = edge_flags(P, F, neighbour, label, facet_normal)
    scene = {"label": label, "facet_normal": facet_normal, "border": border, "convex": convex, "altitude": altitude,
             "lit_step": lit_step,
             "zmin": float(P[..., 2].min()), "zmax": float(P[..., 2].max())}
    print(f"{len(P)} triangles -> {len(facet_normal)} facets, {int(border.sum()) // 2} border edges "
          f"({int((border & convex).sum()) // 2} convex)")
    idx, tri, bary = rasterise(P, N, UV, size)
    pos = np.einsum("mk,mkj->mj", bary, P[tri])
    nrm = np.einsum("mk,mkj->mj", bary, N[tri])
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    colour = np.zeros((len(idx), 3), np.float32)
    for s in range(0, len(idx), CHUNK):
        sl = slice(s, s + CHUNK)
        colour[sl] = paint(pos[sl], nrm[sl], tri[sl], bary[sl], scene)
    img = np.zeros((size * size, 3), np.float32)
    filled = np.zeros(size * size, bool)
    img[idx] = colour
    filled[idx] = True
    img, filled = rp.dilate(img.reshape(size, size, 3), filled.reshape(size, size), PADDING)
    img[~filled] = PAL["mid"]
    rgb8 = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    rp.write_png(out, rgb8[::-1])
    if preview:
        k = max(size // 768, 1)
        small = rgb8[: (size // k) * k, : (size // k) * k].reshape(size // k, k, size // k, k, 3).mean(axis=(1, 3))
        rp.write_png(preview, small.astype(np.uint8)[::-1])
    print(f"painted {len(idx):,} pixels, darkest {rgb8[filled].min()} / 255 -> {out}")


if __name__ == "__main__":
    main(sys.argv)
