"""按 fileID 或 GameObject 名打印 Unity YAML 块（诊断用）。

用法：
  python Tools/dump_block.py <prefab> <fileID或对象名> [更多...]
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")


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
    rel = sys.argv[1]
    keys = sys.argv[2:]
    text = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read()
    blocks = split_blocks(text)
    for kind, fid, body in blocks:
        name = ""
        for line in body:
            if line.startswith("  m_Name: "):
                name = line[10:]
                break
        hit = fid in keys or name in keys
        if not hit:
            continue
        print("=== class %s id %s name=%s" % (kind, fid, name))
        for line in body:
            print("   " + line)
        print()


if __name__ == "__main__":
    main()
