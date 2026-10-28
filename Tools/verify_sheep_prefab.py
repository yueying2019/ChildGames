"""校验 SheepGameView.prefab：脚本改动后是否留下悬空引用。

主要防两类事故：
1. 新增/移动节点后，某个 {fileID: N} 指向了不存在的块（Unity 里表现为 Missing Reference）。
2. 批量替换（比如调 Spacing）时误伤了同值的其他布局组。
"""

import os
import re

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\Sheep\SheepGameView.prefab")

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
REF = re.compile(r"\{fileID: (-?\d+)\}")
NAME = re.compile(r"^\s*m_Name: (.*\S)\s*$")
TEXT = re.compile(r"^\s*m_Text: (.*\S)\s*$")
SPACING = re.compile(r"^\s*m_Spacing: ([\d.-]+)\s*$")


def main():
    with open(PREFAB, encoding="utf-8", errors="replace") as fp:
        lines = fp.read().split("\n")

    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    defined = set()
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        defined.add(HEADER.match(lines[start]).group(2))
        blocks.append((start, end))

    print("=== 块总数 ===")
    print("  %d 块" % len(blocks))

    print("=== 悬空引用 ===")
    dangling = {}
    for start, end in blocks:
        owner = HEADER.match(lines[start]).group(2)
        for i in range(start, end):
            for value in REF.findall(lines[i]):
                if value == "0":
                    continue
                if value not in defined:
                    dangling.setdefault(owner, set()).add(value)
    if dangling:
        for owner, refs in sorted(dangling.items()):
            print("  FAIL 块 %s 引用了不存在的: %s" % (owner, ", ".join(sorted(refs))))
    else:
        print("  OK   没有悬空 fileID")

    print("=== 关键节点 ===")
    names, texts = [], []
    for _s, _e in blocks:
        pass
    for start, end in blocks:
        for i in range(start, end):
            m = NAME.match(lines[i])
            if m:
                names.append(m.group(1))
            m = TEXT.match(lines[i])
            if m:
                texts.append(m.group(1))
    for want in ("SlotButton", "RemoveButton", "UndoButton", "ShuffleButton"):
        hits = names.count(want)
        print("  %s %s x%d" % ("OK  " if hits == 1 else "FAIL", want, hits))

    print("=== 按钮文案 ===")
    print("  " + " | ".join(t for t in texts if "u" in t or len(t) > 2))

    print("=== 所有 Spacing ===")
    for start, end in blocks:
        label = HEADER.match(lines[start]).group(2)
        for i in range(start, end):
            m = NAME.match(lines[i])
            if m:
                label = "%s (%s)" % (m.group(1), label)
                break
        for i in range(start, end):
            m = SPACING.match(lines[i])
            if m:
                print("  %s -> %s" % (label, m.group(1)))

    tail = [l for l in lines if "SlotButton: {fileID:" in l]
    print("=== 脚本字段回填 ===")
    print("  " + (tail[0].strip() if tail else "FAIL 未找到 SlotButton 字段"))


if __name__ == "__main__":
    main()
