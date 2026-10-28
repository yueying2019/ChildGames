"""只读探查：打印预制体里某个按钮节点的结构，供改写脚本取 fileID / 坐标。

用法：
    python inspect_prefab_button.py <prefab路径> <按钮名> [更多按钮名...]
"""

import os
import re
import sys

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
COMPONENT = re.compile(r"- component: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
GAME_OBJECT = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")

KEYS = (
    "m_Name", "m_Father", "m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition",
    "m_SizeDelta", "m_Pivot", "m_LocalPosition", "m_Text", "m_Script",
    "m_Spacing", "m_ChildAlignment", "m_IsActive",
)


def split_blocks(text):
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append([m.group(1), m.group(2), lines[start:end]])
    return blocks


def main():
    path = sys.argv[1]
    names = sys.argv[2:]
    with open(path, encoding="utf-8", errors="replace") as fp:
        source = fp.read()

    blocks = split_blocks(source)
    bmap = {b[1]: b for b in blocks}

    name_to_go = {}
    for kind, fid, body in blocks:
        if kind != "1":
            continue
        for line in body:
            if line.startswith("  m_Name: "):
                name_to_go[line[len("  m_Name: "):]] = fid
                break

    for name in names:
        go = name_to_go.get(name)
        print("=== %s  GO=%s" % (name, go))
        if go is None:
            continue
        kind, _fid, body = bmap[go]
        comps = [m.group(1) for m in COMPONENT.finditer("\n".join(body))]
        print("  组件: %s" % ", ".join("%s(%s)" % (c, bmap[c][0]) if c in bmap else c for c in comps))
        for cid in comps:
            if cid not in bmap:
                continue
            k2, f2, body2 = bmap[cid]
            print("  -- class %s id %s" % (k2, f2))
            for line in body2:
                stripped = line.strip()
                for key in KEYS:
                    if stripped.startswith(key + ":") or stripped.startswith("- " + key + ":"):
                        print("     %s" % stripped)
            if k2 == "224":
                kids = []
                for line in body2:
                    m = CHILD.match(line)
                    if m:
                        kids.append(m.group(1))
                for kid in kids:
                    if kid in bmap:
                        gm = GAME_OBJECT.search("\n".join(bmap[kid][2]))
                        kid_go = gm.group(1) if gm else "?"
                        kid_name = "?"
                        for line in bmap[kid_go][2] if kid_go in bmap else []:
                            if line.startswith("  m_Name: "):
                                kid_name = line[len("  m_Name: "):]
                        print("     子: %s (rect %s)" % (kid_name, kid))
        print()


if __name__ == "__main__":
    main()
