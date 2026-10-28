"""给关卡选择面板的 Cell 模板补三颗星标 Image。

做法：从 Result.prefab 里把 Star1 的四个 block（GameObject / RectTransform /
CanvasRenderer / Image）整段拷出来，换掉 fileID 与几何后追加到 LevelSelect.prefab，
并把新的 RectTransform 挂到 Cell 节点的 m_Children 上。
这样星星的 Image 脚本 GUID 和贴图 GUID 都跟结算面板一致，不用自己找资源。
"""

import random
import re
import sys

RESULT_PREFAB = r"E:\UnityGame\SmallGames\Assets\Content\UI\Result.prefab"
LEVEL_PREFAB = r"E:\UnityGame\SmallGames\Assets\Content\UI\LevelSelect.prefab"

TMPL_FIRST = 3   # Star1 的 GameObject 起始行（1-based，跳过 %YAML/%TAG）
TMPL_LAST = 78   # Star1 的 Image 结束行

SRC_GO = "409823388265410792"
SRC_RECT = "2172929861460607907"
SRC_CANVAS = "4037162274250876394"
SRC_IMAGE = "8733294064168407024"
SRC_FATHER = "5443521986411593588"

CELL_RECT = "2984404852848354897"

NAMES = ["Star1", "Star2", "Star3"]
POSITIONS = [(-34.0, -62.0), (0.0, -62.0), (34.0, -62.0)]

OLD_IDS = (SRC_GO, SRC_RECT, SRC_CANVAS, SRC_IMAGE, SRC_FATHER, CELL_RECT)


def read_lines(path):
    with open(path, "rb") as fp:
        raw = fp.read().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"
    return raw.replace("\r\n", "\n").split("\n"), nl


def new_id():
    while True:
        # 必须落在 Int64 范围内：Unity 的 fileID 是 SInt64，超限会被截断成同一个
        # long.MaxValue，导致 Duplicate identifier / BrokenPrefabAsset。
        value = str(random.randint(10 ** 17, (1 << 63) - 2))
        if value not in OLD_IDS:
            return value


def swap_ids(block, mapping):
    for old, new in mapping.items():
        block = re.sub(r"\b%s\b" % re.escape(old), new, block)
    return block


def restyle(block, name, x, y):
    block = block.replace("m_Name: Star1", "m_Name: " + name)
    block = block.replace(
        "m_LocalRotation: {x: -0, y: -0, z: 0.17364816, w: 0.9848078}",
        "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
    block = re.sub(r"m_LocalEulerAnglesHint: .*",
                   "m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}", block)
    block = re.sub(r"m_Father: .*",
                   "m_Father: {fileID: %s}" % CELL_RECT, block)
    block = re.sub(r"m_AnchorMin: .*", "m_AnchorMin: {x: 0.5, y: 0.5}", block)
    block = re.sub(r"m_AnchorMax: .*", "m_AnchorMax: {x: 0.5, y: 0.5}", block)
    block = re.sub(r"m_AnchoredPosition: .*",
                   "m_AnchoredPosition: {x: %g, y: %g}" % (x, y), block)
    block = re.sub(r"m_SizeDelta: .*", "m_SizeDelta: {x: 30, y: 30}", block)
    block = re.sub(r"m_Pivot: .*", "m_Pivot: {x: 0.5, y: 0.5}", block)
    return block


def insert_children(lines, rect_ids):
    anchor = "--- !u!224 &%s" % CELL_RECT
    start = None
    for i, line in enumerate(lines):
        if line.strip() == anchor:
            start = i
            break
    if start is None:
        raise SystemExit("找不到 Cell 的 RectTransform: " + anchor)

    child_at = None
    for i in range(start + 1, len(lines)):
        if lines[i] == "  m_Children:":
            child_at = i
            break
        if lines[i].startswith("--- "):
            break
    if child_at is None:
        raise SystemExit("Cell 的 RectTransform 里没有 m_Children")

    last = child_at
    i = child_at + 1
    while i < len(lines) and lines[i].startswith("  - {fileID:"):
        last = i
        i += 1

    tail = ["  - {fileID: %s}" % rid for rid in rect_ids]
    return lines[:last + 1] + tail + lines[last + 1:]


def main():
    apply = "--apply" in sys.argv
    random.seed(20260921)

    result_lines, _ = read_lines(RESULT_PREFAB)
    template = "\n".join(result_lines[TMPL_FIRST - 1:TMPL_LAST])
    if "m_Name: Star1" not in template:
        raise SystemExit("模板提取失败：Result.prefab 的 Star1 位置可能已经变了")

    level_lines, nl = read_lines(LEVEL_PREFAB)

    blocks = []
    rect_ids = []
    for name, (x, y) in zip(NAMES, POSITIONS):
        go, rect, canvas, image = new_id(), new_id(), new_id(), new_id()
        rect_ids.append(rect)
        block = swap_ids(template, {
            SRC_GO: go, SRC_RECT: rect, SRC_CANVAS: canvas, SRC_IMAGE: image,
        })
        blocks.append(restyle(block, name, x, y))

    out = insert_children(level_lines, rect_ids)
    while out and out[-1] == "":
        out = out[:-1]
    for block in blocks:
        out.extend(block.split("\n"))
    out.append("")

    print("模板行数: %d" % (TMPL_LAST - TMPL_FIRST + 1))
    print("新增星标 RectTransform:")
    for name, rid in zip(NAMES, rect_ids):
        print("  %s -> %s" % (name, rid))
    print("挂载父节点 Cell RectTransform: %s" % CELL_RECT)
    print("写入方式: %s" % ("覆盖文件" if apply else "仅预览（加 --apply 落盘）"))

    if not apply:
        return

    with open(LEVEL_PREFAB, "w", newline="", encoding="utf-8") as fp:
        fp.write(nl.join(out))
    print("已写入 %s（共 %d 行）" % (LEVEL_PREFAB, len(out)))


if __name__ == "__main__":
    main()
