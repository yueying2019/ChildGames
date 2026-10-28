"""打印某个 RectTransform 的子节点概要（名字 / 尺寸 / 锚点 / 组件类型）。

用法：
  python Tools/dump_children.py <prefab> <rectFileID>
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
GO_REF = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^    - \{fileID: (-?\d+)\}")
SCALAR = re.compile(r"^\s+(m_\w+): (.*)$")

KIND_NAME = {
    "1": "GameObject",
    "224": "RectTransform",
    "222": "CanvasRenderer",
    "114": "MonoBehaviour",
}


def blocks_of(rel):
    lines = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read().split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    out = []
    for k, s in enumerate(starts):
        e = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[s])
        out.append([m.group(1), m.group(2), lines[s:e]])
    return out


def index(blocks):
    return {b[1]: b for b in blocks}


def go_of(block):
    for line in block[2]:
        m = GO_REF.search(line)
        if m:
            return m.group(1)
    return None


def scalar(block, key):
    for line in block[2]:
        m = SCALAR.match(line)
        if m and m.group(1) == key:
            return m.group(2)
    return ""


def describe(blocks, bmap, rect):
    block = bmap.get(rect)
    if block is None:
        return
    go = go_of(block)
    gob = bmap.get(go)
    name = scalar(gob, "m_Name") if gob else "?"
    active = scalar(gob, "m_IsActive") if gob else "?"
    comps = [KIND_NAME.get(bmap[c][0], bmap[c][0]) for c in re.findall(r"- component: \{fileID: (-?\d+)\}", "\n".join(gob[2]))] if gob else []
    scripts = []
    for c in re.findall(r"- component: \{fileID: (-?\d+)\}", "\n".join(gob[2])) if gob else []:
        cb = bmap.get(c)
        if cb and cb[0] == "114":
            for line in cb[2]:
                if "m_Script:" in line:
                    scripts.append(line.strip()[-12:-2])
                    break
    print("  %-22s go=%-20s active=%s size=%s pos=%s" % (
        name.strip(), go, active, scalar(block, "m_SizeDelta"), scalar(block, "m_AnchoredPosition")))
    print("       comps=%s scripts=%s" % (",".join(comps), ",".join(scripts)))
    kids = []
    body = block[2]
    for i, line in enumerate(body):
        if re.match(r"^\s+m_Children:", line):
            j = i + 1
            while j < len(body) and re.match(r"^\s+- \{fileID:", body[j]):
                kids.append(body[j].strip().split("fileID: ")[1].rstrip("}"))
                j += 1
            break
    for k in kids:
        describe(blocks, bmap, k)


def main():
    rel = sys.argv[1]
    blocks = blocks_of(rel)
    bmap = index(blocks)
    for rect in sys.argv[2:]:
        print("### %s" % rect)
        describe(blocks, bmap, rect)
        print()


if __name__ == "__main__":
    main()
