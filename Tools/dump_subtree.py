"""按 RectTransform fileID 打印一个 GO 及其组件（诊断用）。

用法：
  python Tools/dump_subtree.py <prefab> <rectFileID> [更多...]
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
GO_REF = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")
KEEP = (
    "m_Name", "m_Text", "m_Sprite", "m_AnchorMin", "m_AnchorMax", "m_SizeDelta",
    "m_AnchoredPosition", "m_Father", "m_Children", "m_IsActive", "m_FontSize",
    "m_Color", "m_Script", "m_font", "m_Alignment", "m_HorizontalOverflow",
    "m_VerticalOverflow", "m_FontColor", "m_LineSpacing", "m_RaycastTarget",
    "m_Type", "m_PreserveAspect", "m_fontSize",
)


def blocks_of(rel):
    lines = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read().split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    out = []
    for k, s in enumerate(starts):
        e = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[s])
        out.append((m.group(1), m.group(2), lines[s:e]))
    return out


def go_of(blocks, rect):
    for _k, fid, body in blocks:
        if fid != rect:
            continue
        for line in body:
            m = GO_REF.search(line)
            if m:
                return m.group(1)
    return None


def main():
    rel = sys.argv[1]
    blocks = blocks_of(rel)
    for rect in sys.argv[2:]:
        go = go_of(blocks, rect)
        print("### rect %s -> GO %s" % (rect, go))
        if go is None:
            continue
        for kind, fid, body in blocks:
            joined = "\n".join(body)
            if "m_GameObject: {fileID: %s}" % go not in joined:
                continue
            print("--- class %s id %s" % (kind, fid))
            for line in body:
                s = line.strip()
                if any(s.startswith(k + ":") for k in KEEP):
                    print("    " + s)
        print()


if __name__ == "__main__":
    main()
