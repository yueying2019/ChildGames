"""从三个 GameView 预制体里读出「按钮 -> 图标 sprite guid」的映射。

用户在编辑器里手工给每个道具按钮的 Item 实例指定了 Icon 图标，
这些引用是 32 位十六进制 guid，跟 .png.meta 里的长 base64 guid 不是一回事。
本脚本把它们抽出来，供商店图标表使用。
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HDR = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$")
ITEM_GUID = "b5a99db6c5c80764d8d31ce8758939b1"

TARGETS = [
    ("Assets/Content/Lianliankan/LianliankanGameView.prefab", "Lian"),
    ("Assets/Content/Match3/Match3GameView.prefab", "Match3"),
    ("Assets/Content/Sheep/SheepGameView.prefab", "Sheep"),
]


def blocks_of(rel):
    lines = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read().split("\n")
    starts = [i for i, l in enumerate(lines) if HDR.match(l)]
    blocks = []
    for k, st in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HDR.match(lines[st])
        blocks.append({"class": m.group(1), "fid": m.group(2), "body": lines[st:end]})
    return blocks


def index(blocks):
    return {b["fid"]: b for b in blocks}


def field(block, key):
    for line in block["body"]:
        m = re.match(r"^\s*%s: (.*)$" % key, line)
        if m:
            return m.group(1).strip()
    return None


def go_of(bmap, rect):
    b = bmap.get(rect)
    if b is None:
        return None
    go = field(b, "m_GameObject")
    if not go:
        return None
    return re.search(r"fileID: (-?\d+)", go).group(1)


def rect_of(bmap, go):
    gb = bmap.get(go)
    if gb is None:
        return None
    for line in gb["body"]:
        m = re.match(r"^\s*- component: \{fileID: (-?\d+)\}", line)
        if not m:
            continue
        comp = bmap.get(m.group(1))
        if comp and comp["class"] == "224":
            return comp["fid"]
    return None


def name_of(bmap, go):
    gb = bmap.get(go)
    if gb is None:
        return "?"
    value = field(gb, "m_Name")
    return value.strip('"') if value else "?"


def sprite_of_instance(block):
    """取 PrefabInstance 里对 Icon 图片（m_Sprite）的覆盖值。"""
    body = "\n".join(block["body"])
    mods = re.findall(
        r"- target: \{fileID: (\d+), guid: %s, type: 3\}\n\s*propertyPath: (\S+)\n\s*value: (.*)\n\s*objectReference: \{fileID: (-?\d+), guid: (\S+), type: 3\}" % ITEM_GUID,
        body,
    )
    for target, path, _value, ref, guid in mods:
        if path == "m_Sprite" and target == "5572156852717320087":
            return guid
    return None


def parent_button(bmap, rect):
    cur = rect
    for _ in range(8):
        go = go_of(bmap, cur)
        if go is None:
            return None
        name = name_of(bmap, go)
        if name.endswith("Button"):
            return name
        parent = field(bmap[cur], "m_Father")
        if not parent:
            return None
        cur = re.search(r"fileID: (-?\d+)", parent).group(1)
        if cur == "0":
            return None
    return None


def main():
    mapping = {}
    for rel, _prefix in TARGETS:
        blocks = blocks_of(rel)
        bmap = index(blocks)
        for b in blocks:
            if b["class"] != "1001":
                continue
            if ITEM_GUID not in "\n".join(b["body"]):
                continue
            parent = field(b, "m_TransformParent")
            if not parent:
                continue
            rect = re.search(r"fileID: (-?\d+)", parent).group(1)
            button = parent_button(bmap, rect)
            sprite = sprite_of_instance(b)
            print("%-24s %-20s %s" % (os.path.basename(rel), button, sprite))
            if button and sprite:
                mapping[button] = sprite
    print()
    print("collected", len(mapping))


if __name__ == "__main__":
    main()
