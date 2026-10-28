"""给 SheepGameView.prefab 复制一颗「暂存位」按钮（prefab-first，不在运行时搭 UI）。

做法：整体复制 RemoveButton 的子树（GO + RectTransform + CanvasRenderer + Image
+ Button + Plus/Mark 三个子节点），换掉所有 fileID，改名 SlotButton，
再把新的 RectTransform 挂到 Footer 的 m_Children 末尾，最后回填脚本里的 SlotButton 字段。

用法：
    python add_sheep_slot_button.py            # 试运行，只打印
    python add_sheep_slot_button.py --apply    # 写入
"""

import os
import random
import re
import sys

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\Sheep\SheepGameView.prefab")

SOURCE_GO = "8331879295605057080"
FOOTER_RECT = "5148208632066162217"
SOURCE_NAME = "RemoveButton"
TARGET_NAME = "SlotButton"
SOURCE_TEXT = r'm_Text: "\u79FB\u51FA"'
TARGET_TEXT = r'm_Text: "\u6682\u5B58"'
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
COMPONENT = re.compile(r"- component: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
GAME_OBJECT = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")


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
        match = HEADER.match(lines[start])
        blocks.append([match.group(1), match.group(2), lines[start:end]])
    return header, blocks


def render(header, blocks):
    """把文件头 + 块重新拼回文本，保证结尾恰好一个换行。"""
    lines = list(header)
    for _kind, _fid, body in blocks:
        lines.extend(body)
    return "\n".join(lines).rstrip("\n") + "\n"


def index_blocks(blocks):
    return {block[1]: block for block in blocks}


def collect_subtree(bmap, root):
    """从 GO fileID 出发，递归收集整棵子树的 fileID。"""
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


def main():
    apply = "--apply" in sys.argv
    with open(PREFAB, encoding="utf-8", errors="replace") as fp:
        source = fp.read()

    header, blocks = split_blocks(source)
    bmap = index_blocks(blocks)
    if SOURCE_GO not in bmap:
        print("FAIL 找不到源按钮 fileID %s" % SOURCE_GO)
        return

    ids = collect_subtree(bmap, SOURCE_GO)
    taken = set(bmap)
    mapping = {old: fresh_id(taken) for old in ids}

    source_root = bmap[SOURCE_GO]
    source_rect = None
    source_button = None
    joined = "\n".join(source_root[2])
    for m in COMPONENT.finditer(joined):
        cid = m.group(1)
        if cid not in bmap:
            continue
        kind, _fid, body = bmap[cid]
        text_block = "\n".join(body)
        if kind == "224" and source_rect is None:
            source_rect = cid
        if kind == "114" and BUTTON_GUID in text_block:
            source_button = cid

    if source_rect is None or source_button is None:
        print("FAIL 源按钮结构不完整 rect=%s button=%s" % (source_rect, source_button))
        return

    new_blocks = []
    for old in sorted(ids):
        kind, _fid, body = bmap[old]
        kind_id = kind
        body = remap(body, mapping)
        if old == SOURCE_GO:
            body = [line.replace("m_Name: " + SOURCE_NAME, "m_Name: " + TARGET_NAME) for line in body]
        elif SOURCE_TEXT in "\n".join(body):
            body = [line.replace(SOURCE_TEXT, TARGET_TEXT) for line in body]
        new_blocks.append([kind_id, mapping[old], body])

    footer = bmap.get(FOOTER_RECT)
    if footer is None:
        print("FAIL 找不到 Footer %s" % FOOTER_RECT)
        return

    print("复制子树 %d 块" % len(new_blocks))
    print("  新 GO      %s -> %s" % (SOURCE_GO, mapping[SOURCE_GO]))
    print("  新 Rect    %s -> %s" % (source_rect, mapping[source_rect]))
    print("  新 Button  %s -> %s" % (source_button, mapping[source_button]))

    if not apply:
        print("-- dry run，未写入 --")
        return

    # 1) 把新 RectTransform 挂到 Footer 的 m_Children 末尾
    last_child = -1
    for i, line in enumerate(footer[2]):
        if CHILD.match(line):
            last_child = i
    if last_child < 0:
        print("FAIL Footer 没有 m_Children")
        return
    footer[2].insert(last_child + 1, "  - {fileID: %s}" % mapping[source_rect])

    # 2) 四个按钮挤一行，把 Footer 的 HorizontalLayoutGroup 间距收窄
    for kind, _fid, body in blocks:
        if "m_Spacing: 28" not in "\n".join(body):
            continue
        if kind != "114":
            continue
        for i, line in enumerate(body):
            if line == "  m_Spacing: 28":
                body[i] = "  m_Spacing: 20"
                break

    # 3) 回填脚本里的 SlotButton 引用
    wired = False
    for kind, _fid, body in blocks:
        for i, line in enumerate(body):
            if "ShuffleButton: {fileID:" not in line:
                continue
            indent = line[:len(line) - len(line.lstrip())]
            body.insert(i + 1, "%sSlotButton: {fileID: %s}" % (indent, mapping[source_button]))
            wired = True
            break
        if wired:
            break
    if not wired:
        print("FAIL 没找到 SheepGameView 脚本里的 ShuffleButton 字段")
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
