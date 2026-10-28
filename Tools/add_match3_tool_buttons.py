"""给 Match3GameView.prefab 补两枚道具按钮：彩球笔 / 同色消。

沿用现有 Footer 的手工排布（没有 LayoutGroup）：
  第一行 y=160 宽 190：提示 / 重排 / 加步
  第二行 y=60  宽 160：消除 / 横消 / 竖消 / 炸弹
  新增第三行 y=-30 宽 190：彩球 / 同色消（Footer 高 280，仍在范围内）

做法与 add_lianliankan_tool_buttons.py 一致：复制 MovesButton 子树、换 fileID、
改名字与坐标、改 Label 文案、挂回 Footer、回填脚本字段。

用法：
    python add_match3_tool_buttons.py            # 试运行
    python add_match3_tool_buttons.py --apply    # 写入
"""

import os
import random
import re
import shutil
import sys

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\Match3\Match3GameView.prefab")
BACKUP_DIR = os.path.join(ROOT, r"Tools\_backup")

SOURCE_GO = "2136965071585337007"        # MovesButton
SOURCE_RECT = "7312779361502777724"
LABEL_RECT = "8522943218715205477"
FOOTER_RECT = "8910680985625196647"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"

# (名字, x, y, Label 文案)
TARGETS = [
    ("ColorBombButton", -110, -30, r"\u5F69\u7403"),
    ("ColorClearButton", 110, -30, r"\u540C\u8272\u6D88"),
]

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
COMPONENT = re.compile(r"- component: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
GAME_OBJECT = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")
TEXT_LINE = re.compile(r'^(\s*)m_Text: ".*"$')


def split_blocks(text):
    """拆成块；同时返回文件头指令行（%YAML 1.1 / %TAG !u! ...）。

    注意：Unity/团结引擎的 YAML 文件开头那两行指令必须原样保留，丢了 !u! 标签
    就无法解析，资源会被 Unity 当成导入失败（AssetDatabase 取到 null）。
    """
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    header = lines[:starts[0]] if starts else []
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append([m.group(1), m.group(2), lines[start:end]])
    return header, blocks


def render(header, blocks):
    """把文件头 + 块重新拼回文本，保证结尾恰好一个换行。"""
    lines = list(header)
    for _kind, _fid, body in blocks:
        lines.extend(body)
    return "\n".join(lines).rstrip("\n") + "\n"


def index_blocks(blocks):
    return {b[1]: b for b in blocks}


def collect_subtree(bmap, root):
    ids = set()
    stack = [root]
    while stack:
        fid = stack.pop()
        if fid in ids or fid not in bmap:
            continue
        ids.add(fid)
        kind, _fid, body = bmap[fid]
        joined = "\n".join(body)
        if kind == "1":
            for m in COMPONENT.finditer(joined):
                stack.append(m.group(1))
        elif kind == "224":
            for line in body:
                m = CHILD.match(line)
                if not m:
                    continue
                child = bmap.get(m.group(1))
                if child is None:
                    continue
                gm = GAME_OBJECT.search("\n".join(child[2]))
                if gm:
                    stack.append(gm.group(1))
    return ids


def fresh_id(taken):
    while True:
        # 必须落在 Int64 范围内：Unity 的 fileID 是 SInt64，超限会被截断成同一个
        # long.MaxValue，导致 Duplicate identifier / BrokenPrefabAsset。
        value = str(random.randint(10 ** 17, (1 << 63) - 2))
        if value not in taken:
            taken.add(value)
            return value


def remap(body, mapping):
    out = []
    for line in body:
        for old, new in mapping.items():
            line = re.sub(r"(?<![\w])%s(?![\w])" % old, new, line)
        out.append(line)
    return out


def find_component(bmap, go, want_kind, guid=None):
    _kind, _fid, body = bmap[go]
    for m in COMPONENT.finditer("\n".join(body)):
        cid = m.group(1)
        if cid not in bmap:
            continue
        k2, _f2, body2 = bmap[cid]
        if k2 != want_kind:
            continue
        if guid is not None and guid not in "\n".join(body2):
            continue
        return cid
    return None


def set_anchored(body, x, y):
    out = []
    for line in body:
        if line.strip().startswith("m_AnchoredPosition:"):
            line = "  m_AnchoredPosition: {x: %d, y: %d}" % (x, y)
        out.append(line)
    return out


def main():
    apply = "--apply" in sys.argv
    with open(PREFAB, encoding="utf-8", errors="replace") as fp:
        source = fp.read()

    header, blocks = split_blocks(source)
    bmap = index_blocks(blocks)
    if SOURCE_GO not in bmap or FOOTER_RECT not in bmap:
        print("FAIL 源按钮或 Footer 缺失")
        return

    def subtree_of_rect(rect):
        gm = GAME_OBJECT.search("\n".join(bmap[rect][2]))
        if gm is None:
            return collect_subtree(bmap, rect)
        return collect_subtree(bmap, gm.group(1))

    ids = collect_subtree(bmap, SOURCE_GO)
    label_ids = subtree_of_rect(LABEL_RECT)
    taken = set(bmap)

    plan = []
    for name, x, y, label in TARGETS:
        plan.append((name, x, y, label, {old: fresh_id(taken) for old in ids}))

    print("源按钮 MovesButton 子树 %d 块，Label %d 块" % (len(ids), len(label_ids)))
    for name, x, y, label, mapping in plan:
        print("  %-18s (%d, %d) GO=%s rect=%s" % (name, x, y, mapping[SOURCE_GO], mapping[SOURCE_RECT]))

    if not apply:
        print("-- dry run，未写入 --")
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    backup = os.path.join(BACKUP_DIR, "Match3GameView.prefab.bak")
    if not os.path.exists(backup):
        shutil.copy2(PREFAB, backup)

    footer = bmap[FOOTER_RECT]
    source_button = find_component(bmap, SOURCE_GO, "114", BUTTON_GUID)
    if source_button is None:
        print("FAIL 源按钮上找不到 Button 组件")
        return

    new_blocks = []
    new_buttons = []
    for name, x, y, label, mapping in plan:
        for old in sorted(ids):
            kind, _fid, body = bmap[old]
            body = remap(body, mapping)
            if old == SOURCE_GO:
                body = [line.replace("m_Name: MovesButton", "m_Name: " + name) for line in body]
            elif old == SOURCE_RECT:
                body = set_anchored(body, x, y)
            elif old in label_ids:
                body = [TEXT_LINE.sub(lambda m: '%sm_Text: "%s"' % (m.group(1), label), line) for line in body]
            new_blocks.append([kind, mapping[old], body])
        new_buttons.append((name, mapping[source_button], mapping[SOURCE_RECT]))

    last_child = -1
    for i, line in enumerate(footer[2]):
        if CHILD.match(line):
            last_child = i
    if last_child < 0:
        print("FAIL Footer 没有 m_Children")
        return
    for idx, (_name, _btn, rect) in enumerate(new_buttons):
        footer[2].insert(last_child + 1 + idx, "  - {fileID: %s}" % rect)

    wired = False
    for kind, _fid, body in blocks:
        for i, line in enumerate(body):
            if "MovesButton: {fileID:" not in line:
                continue
            indent = line[:len(line) - len(line.lstrip())]
            for idx, (name, btn, _rect) in enumerate(new_buttons):
                body.insert(i + 1 + idx, "%s%s: {fileID: %s}" % (indent, name, btn))
            wired = True
            break
        if wired:
            break
    if not wired:
        print("FAIL 没找到 Match3GameView 脚本里的 MovesButton 字段")
        return

    blocks.extend(new_blocks)
    out = render(header, blocks)
    if not out.startswith("%YAML"):
        print("FAIL 头部指令丢失，拒绝写入")
        return
    with open(PREFAB, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(out)

    print("已写入 %s" % PREFAB)
    print("  Footer 子节点数: %d" % sum(1 for line in footer[2] if CHILD.match(line)))


if __name__ == "__main__":
    main()
