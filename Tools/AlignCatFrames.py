# 把猫序列条抠成透明底：边缘做抗锯齿，并按肚子宽度对齐到同一尺寸。
import os
import numpy as np
import cv2

SRC = r"C:\Users\xtt\.cursor\projects\e-UnityGame-SmallGames\assets"
OUT = r"E:\UnityGame\SmallGames\Assets\UI\Cats"
PREVIEW = r"C:\Users\xtt\.cursor\projects\e-UnityGame-SmallGames\assets\preview"

CATS = ("Lianliankan", "Sheep", "Match3")
ACTIONS = ("Scratch", "Eat", "Excited")
PARTS = ("a", "b", "c")
MARGIN = 28
FEATHER = 1.8


def load_rgb(path):
    bgr = cv2.imread(path, cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(path)
    return cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)


def split_six(rgb):
    h, w = rgb.shape[:2]
    frames = []
    for i in range(6):
        x0 = int(round(w * i / 6.0))
        x1 = int(round(w * (i + 1) / 6.0))
        if i > 0:
            x0 += 3
        if i < 5:
            x1 -= 3
        frames.append(rgb[:, x0:x1])
    return frames


def knock_out(rgb, threshold):
    """按和边缘底板的色差抠图。只去掉和底板相连的部分，再补上身体内部被误抠的洞。"""
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]], axis=0)
    bg = np.median(border, axis=0).astype(np.float32)
    dist = np.abs(rgb.astype(np.int16) - bg.astype(np.int16)).sum(axis=2)
    low = (dist <= threshold).astype(np.uint8)
    n, labels = cv2.connectedComponents(low, connectivity=8)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    edge = edge[edge != 0]
    bg_mask = np.isin(labels, edge) if len(edge) else np.zeros(low.shape, np.bool_)
    fg = (~bg_mask).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
    if n > 1:
        biggest = int(stats[1:, cv2.CC_STAT_AREA].max())
        keep = np.zeros(n, np.bool_)
        for i in range(1, n):
            if stats[i, cv2.CC_STAT_AREA] >= max(80, int(biggest * 0.015)):
                keep[i] = True
        fg = keep[labels].astype(np.uint8)
    # 蓝灰色毛和底板很近，窄缝会漏进身体。先封上细缝，再把封在里面的部分补回来。
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, kernel)
    empty = (fg == 0).astype(np.uint8)
    n, labels = cv2.connectedComponents(empty, connectivity=8)
    edge = set(np.unique(np.concatenate([
        labels[0], labels[-1], labels[:, 0], labels[:, -1]
    ])).tolist())
    edge.discard(0)
    for i in range(1, n):
        if i not in edge:
            fg[labels == i] = 1

    dist_in = cv2.distanceTransform(fg, cv2.DIST_L2, 3)
    alpha = np.clip(dist_in / FEATHER, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.6)
    alpha = np.clip(alpha, 0.0, 1.0)

    # 半透明一圈还带着底板颜色，向内补毛色，避免一圈黑边。
    ring = ((alpha > 0.04) & (alpha < 0.82)).astype(np.uint8) * 255
    bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
    if int(ring.sum()) > 0:
        bgr = cv2.inpaint(bgr, ring, 2, cv2.INPAINT_TELEA)
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    return np.dstack([rgb, np.clip(alpha * 255.0, 0, 255).astype(np.uint8)])


def longest_run(cols):
    if len(cols) == 0:
        return 0, 0.0
    splits = np.where(np.diff(cols) > 2)[0]
    start = 0
    best_w = 0
    best_c = 0.0
    stops = list(splits) + [len(cols) - 1]
    for stop in stops:
        run = cols[start:stop + 1]
        start = int(stop) + 1
        if len(run) == 0:
            continue
        rw = int(run.max() - run.min())
        if rw > best_w:
            best_w = rw
            best_c = float(run.min() + run.max()) * 0.5
    return best_w, best_c


def metrics(frame):
    alpha = frame[:, :, 3]
    ys, xs = np.where(alpha > 20)
    if len(xs) < 30:
        raise RuntimeError("frame too empty")
    y0, y1 = int(ys.min()), int(ys.max())
    x0, x1 = int(xs.min()), int(xs.max())
    height = max(1, y1 - y0)
    y_a = y0 + int(height * 0.58)
    y_b = y0 + int(height * 0.80)
    widths = []
    centers = []
    for y in range(y_a, max(y_a + 1, y_b)):
        cols = np.where(alpha[y] > 128)[0]
        rw, rc = longest_run(cols)
        if rw > 8:
            widths.append(rw)
            centers.append(rc)
    if not widths:
        body_cx = (x0 + x1) * 0.5
    else:
        body_cx = float(np.median(centers))
    foot_y = y1
    for y in range(y1, y0, -1):
        if int((alpha[y] > 20).sum()) >= 10:
            foot_y = y
            break
    opaque = int((alpha > 128).sum())
    body_w = float(np.sqrt(max(opaque, 1)))
    return {
        "body_w": max(8.0, body_w),
        "body_cx": body_cx,
        "foot_y": float(foot_y),
        "x0": x0,
        "x1": x1,
        "y0": y0,
        "y1": y1,
    }


def resize_premul(frame, scale):
    h, w = frame.shape[:2]
    nw = max(1, int(round(w * scale)))
    nh = max(1, int(round(h * scale)))
    if nw == w and nh == h:
        return frame
    rgb = frame[:, :, :3].astype(np.float32)
    alpha = frame[:, :, 3:4].astype(np.float32) / 255.0
    premul = np.dstack([rgb * alpha, frame[:, :, 3].astype(np.float32)])
    interp = cv2.INTER_AREA if scale < 1.0 else cv2.INTER_LINEAR
    out = cv2.resize(premul, (nw, nh), interpolation=interp)
    a = np.maximum(out[:, :, 3:4] / 255.0, 1e-4)
    rgb_out = out[:, :, :3] / a
    rgb_out[out[:, :, 3] < 1] = 0
    rgba = np.zeros((nh, nw, 4), np.uint8)
    rgba[:, :, :3] = np.clip(rgb_out, 0, 255).astype(np.uint8)
    rgba[:, :, 3] = np.clip(out[:, :, 3], 0, 255).astype(np.uint8)
    return rgba


def blit(canvas, frame, scale, body_cx, foot_y, baseline):
    resized = resize_premul(frame, scale)
    h, w = resized.shape[:2]
    left = int(round(canvas.shape[1] * 0.5 - body_cx * scale))
    top = int(round(baseline - foot_y * scale))
    ch, cw = canvas.shape[:2]
    x0 = max(0, left)
    y0 = max(0, top)
    x1 = min(cw, left + w)
    y1 = min(ch, top + h)
    if x1 <= x0 or y1 <= y0:
        return
    src = resized[y0 - top:y0 - top + (y1 - y0), x0 - left:x0 - left + (x1 - x0)]
    dst = canvas[y0:y1, x0:x1]
    sa = src[:, :, 3:4].astype(np.float32) / 255.0
    da = dst[:, :, 3:4].astype(np.float32) / 255.0
    out_a = sa + da * (1.0 - sa)
    out_rgb = src[:, :, :3].astype(np.float32) * sa + dst[:, :, :3].astype(np.float32) * da * (1.0 - sa)
    rgb = out_rgb / np.maximum(out_a, 1e-4)
    rgb[out_a[:, :, 0] < 1e-3] = 0
    dst[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    dst[:, :, 3:4] = np.clip(out_a * 255.0, 0, 255).astype(np.uint8)


def bridge_neck(canvas):
    """蓝灰色脖子和底板几乎同色，会被抠断。头和身体之间的窄缝用上下毛色补上。"""
    alpha = canvas[:, :, 3]
    ys, xs = np.where(alpha > 80)
    if len(ys) < 30:
        return
    y0, y1 = int(ys.min()), int(ys.max())
    x0, x1 = int(xs.min()), int(xs.max())
    height = max(1, y1 - y0)
    neck0 = y0 + height * 0.40
    neck1 = y0 + height * 0.78
    for x in range(x0, x1 + 1):
        y = y0
        while y <= y1:
            if alpha[y, x] > 80:
                y += 1
                continue
            start = y
            while y <= y1 and alpha[y, x] <= 80:
                y += 1
            above = start - 1
            while above > y0 and alpha[above, x] < 160:
                above -= 1
            below = y
            while below < y1 and alpha[below, x] < 160:
                below += 1
            gap = below - above - 1
            if gap < 1 or gap > 14:
                continue
            if above <= y0 or below >= y1:
                continue
            if alpha[above, x] < 160 or alpha[below, x] < 160:
                continue
            mid = (above + below) * 0.5
            if mid < neck0 or mid > neck1:
                continue
            span = float(below - above)
            for yy in range(above + 1, below):
                t = (yy - above) / span
                color = canvas[above, x, :3].astype(np.float32) * (1.0 - t)
                color += canvas[below, x, :3].astype(np.float32) * t
                canvas[yy, x, :3] = np.clip(color, 0, 255).astype(np.uint8)
                canvas[yy, x, 3] = 255


def fill_enclosed(canvas):
    alpha = canvas[:, :, 3]
    empty = (alpha < 16).astype(np.uint8)
    count, labels = cv2.connectedComponents(empty, connectivity=8)
    if count <= 1:
        return
    edge = set(np.unique(np.concatenate([
        labels[0], labels[-1], labels[:, 0], labels[:, -1]
    ])).tolist())
    edge.discard(0)
    hole = np.zeros(empty.shape, np.uint8)
    for i in range(1, count):
        if i not in edge:
            hole[labels == i] = 255
    if int(hole.sum()) == 0:
        return
    bgr = cv2.cvtColor(canvas[:, :, :3], cv2.COLOR_RGB2BGR)
    bgr = cv2.inpaint(bgr, hole, 3, cv2.INPAINT_TELEA)
    canvas[:, :, :3] = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    canvas[hole > 0, 3] = 255


def save_png(path, rgba):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bgr = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2BGR)
    bgra = cv2.merge([bgr[:, :, 0], bgr[:, :, 1], bgr[:, :, 2], rgba[:, :, 3]])
    cv2.imwrite(path, bgra)


def main():
    clips = []
    for cat in CATS:
        key = "lianliankan" if cat == "Lianliankan" else ("match3" if cat == "Match3" else "sheep")
        for action in ACTIONS:
            frames = []
            for part in PARTS:
                path = os.path.join(SRC, "cat-%s-%s-%s.jpg" % (key, action.lower(), part))
                # 蓝灰色猫和底板接近，阈值收紧，避免肚子和胸口被抠穿。
                threshold = 48 if cat == "Match3" else 68
                for cell in split_six(load_rgb(path)):
                    frames.append(knock_out(cell, threshold))
            if len(frames) != 18:
                raise RuntimeError("%s %s frame count %d" % (cat, action, len(frames)))
            measured = [metrics(frame) for frame in frames]
            clips.append((cat, action, frames, measured))
            widths = [m["body_w"] for m in measured]
            print(cat, action, "body", int(np.median(widths)), "range", int(min(widths)), int(max(widths)))

    # 按不透明面积的平方根对齐，举起的爪子不会把某一帧突然放大。
    all_w = [m["body_w"] for _, _, _, measured in clips for m in measured]
    target = float(np.median(all_w))
    print("target body", round(target, 1))

    extents = []
    placed = []
    for cat, action, frames, measured in clips:
        scales = []
        widths = np.array([m["body_w"] for m in measured], np.float32)
        med = float(np.median(widths))
        for width in widths:
            use = float(width)
            if use < med * 0.72 or use > med * 1.35:
                use = med
            scales.append(target / use)
        for frame, m, scale in zip(frames, measured, scales):
            above = (m["foot_y"] - m["y0"]) * scale
            below = (m["y1"] - m["foot_y"]) * scale
            left = (m["body_cx"] - m["x0"]) * scale
            right = (m["x1"] - m["body_cx"]) * scale
            extents.append((above, below, left, right))
        placed.append((cat, action, frames, measured, scales))

    max_above = max(e[0] for e in extents) + MARGIN
    max_below = max(e[1] for e in extents) + MARGIN
    max_left = max(e[2] for e in extents) + MARGIN
    max_right = max(e[3] for e in extents) + MARGIN
    canvas_w = int(np.ceil(max_left + max_right))
    canvas_h = int(np.ceil(max_above + max_below))
    # 团结纹理流送要求宽高都是 4 的倍数，否则压不了。
    canvas_w = (canvas_w + 3) // 4 * 4
    canvas_h = (canvas_h + 3) // 4 * 4
    baseline = max_above
    print("canvas", canvas_w, canvas_h, "baseline", round(float(baseline), 1))

    os.makedirs(PREVIEW, exist_ok=True)
    for cat, action, frames, measured, scales in placed:
        folder = os.path.join(OUT, cat, action)
        cells = []
        for i, (frame, m, scale) in enumerate(zip(frames, measured, scales)):
            canvas = np.zeros((canvas_h, canvas_w, 4), np.uint8)
            blit(canvas, frame, scale, m["body_cx"], m["foot_y"], baseline)
            if cat == "Match3":
                bridge_neck(canvas)
            fill_enclosed(canvas)
            save_png(os.path.join(folder, "frame_%02d.png" % i), canvas)
            cells.append(canvas)
        sheet = np.zeros((canvas_h * 3, canvas_w * 6, 4), np.uint8)
        for i, cell in enumerate(cells):
            r, c = divmod(i, 6)
            sheet[r * canvas_h:(r + 1) * canvas_h, c * canvas_w:(c + 1) * canvas_w] = cell
        save_png(os.path.join(folder, "sheet.png"), sheet)
        small = cv2.resize(sheet, (sheet.shape[1] // 3, sheet.shape[0] // 3), interpolation=cv2.INTER_AREA)
        ch, cw = small.shape[:2]
        yy, xx = np.mgrid[0:ch, 0:cw]
        board = ((yy // 10 + xx // 10) % 2).astype(np.uint8)
        base = np.where(board[:, :, None] == 0, 48, 78).astype(np.uint8)
        base = np.repeat(base, 3, axis=2)
        a = small[:, :, 3:4].astype(np.float32) / 255.0
        rgb = small[:, :, :3].astype(np.float32) * a + base.astype(np.float32) * (1.0 - a)
        cv2.imwrite(
            os.path.join(PREVIEW, "%s-%s.png" % (cat, action)),
            cv2.cvtColor(np.clip(rgb, 0, 255).astype(np.uint8), cv2.COLOR_RGB2BGR))
        print("wrote", folder)


if __name__ == "__main__":
    main()
