"""给 SheepGameView 的 Slots 节点补两个默认隐藏的槽位容器（Slot_7 / Slot_8）。

「暂存位」道具会把槽位上限抬到 9，但 Slots 用的是 HorizontalLayoutGroup，
必须有对应的子节点才会分配位置。扩展槽默认隐藏 —— LayoutGroup 只收集
activeInHierarchy 的子节点，所以不放出来时完全不影响原来的 7 槽布局。

用法：
    python add_sheep_slot_frames.py            # 试运行
    python add_sheep_slot_frames.py --apply    # 写入
"""

import os
import random
import re
import sys

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\Sheep\SheepGameView.prefab")

SOURCE_GO = "8605681252493963990"
PARENT_RECT = "4569133010860293668"
NEW_NAMES = ["Slot_7", "Slot_8"]

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
        print("FAIL 找不到 Slot_0 %s" % SOURCE_GO)
        return

    ids = collect_subtree(bmap, SOURCE_GO)
    joined = "\n".join(bmap[SOURCE_GO][2])
    source_rect = None
    for m in COMPONENT.finditer(joined):
        cid = m.group(1)
        if cid in bmap and bmap[cid][0] == "224":
            source_rect = cid
            break
    if source_rect is None:
        print("FAIL Slot_0 没有 RectTransform")
        return

    taken = set(bmap)
    new_blocks = []
    new_rects = []
    for name in NEW_NAMES:
        mapping = {old: fresh_id(taken) for old in ids}
        for old in sorted(ids):
            kind, _fid, body = bmap[old]
            body = remap(body, mapping)
            if old == SOURCE_GO:
                body = [line.replace("m_Name: Slot_0", "m_Name: " + name) for line in body]
                body = [line.replace("m_IsActive: 1", "m_IsActive: 0") for line in body]
            new_blocks.append([kind, mapping[old], body])
        new_rects.append(mapping[source_rect])
        print("  %s rect=%s" % (name, mapping[source_rect]))

    print("复制 %d 块 x %d 个槽 = %d 块" % (len(ids), len(NEW_NAMES), len(new_blocks)))

    if not apply:
        print("-- dry run，未写入 --")
        return

    parent = bmap.get(PARENT_RECT)
    if parent is None:
        print("FAIL 找不到 Slots %s" % PARENT_RECT)
        return

    last_child = -1
    for i, line in enumerate(parent[2]):
        if CHILD.match(line):
            last_child = i
    if last_child < 0:
        print("FAIL Slots 没有 m_Children")
        return
    for rect in new_rects:
        parent[2].insert(last_child + 1, "  - {fileID: %s}" % rect)
        last_child += 1

    blocks.extend(new_blocks)
    out = render(header, blocks)
    if not out.startswith("%YAML"):
        print("FAIL 头部指令丢失，拒绝写入")
        return
    with open(PREFAB, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(out)

    print("已写入 %s" % PREFAB)
    print("  Slots 子节点数: %d" % sum(1 for line in parent[2] if CHILD.match(line)))


if __name__ == "__main__":
    main()
