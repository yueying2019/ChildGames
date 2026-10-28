"""清掉 Unity YAML 里指向不存在块的本地引用（仅 {fileID: N} 无 guid 的）。

这类引用多半来自"往 m_Children 里加了子节点、但块本身没写进文件"的事故，
会让 Unity 认为 prefab 有重复标识符/结构损坏。

用法：
  python Tools/clean_dangling_refs.py <prefab相对路径> [...]        # 只报告
  python Tools/clean_dangling_refs.py <prefab相对路径> [...] --apply
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
# 形如 {fileID: 123} 且同一对花括号里没有 guid
LOCAL_REF = re.compile(r"\{fileID: (-?\d+)(?!, guid)\}")
LIST_ITEM = re.compile(r"^(\s*)- \{fileID: (-?\d+)\}\s*$")


def split_blocks(text):
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    header = lines[:starts[0]]
    blocks = []
    for k, s in enumerate(starts):
        e = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[s])
        blocks.append([m.group(1), m.group(2), lines[s:e]])
    return header, blocks


def scan(rel):
    text = open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace").read()
    header, blocks = split_blocks(text)
    ids = {b[1] for b in blocks}
    hits = []
    for kind, fid, body in blocks:
        for i, line in enumerate(body):
            m = LIST_ITEM.match(line)
            if m and m.group(2) != "0" and m.group(2) not in ids:
                hits.append((fid, kind, i, m.group(2), line.strip()))
            else:
                for one in LOCAL_REF.finditer(line):
                    if one.group(1) != "0" and one.group(1) not in ids:
                        hits.append((fid, kind, i, one.group(1), line.strip()))
    return header, blocks, ids, hits


def main():
    apply_it = "--apply" in sys.argv
    rels = [a for a in sys.argv[1:] if not a.startswith("--")]
    for rel in rels:
        header, blocks, ids, hits = scan(rel)
        print("=== %s  块 %d  坏引用 %d" % (os.path.basename(rel), len(blocks), len(hits)))
        for fid, kind, _i, ref, line in hits:
            print("   块 %s(%s) -> %s   %s" % (fid, kind, ref, line[:70]))
        if not apply_it or not hits:
            continue
        for fid, _kind, _i, ref, _line in hits:
            for _k, f, body in blocks:
                if f != fid:
                    continue
                body[:] = [l for l in body if not (
                    (LIST_ITEM.match(l) and LIST_ITEM.match(l).group(2) == ref)
                    or (l.strip() == "{fileID: %s}" % ref)
                )]
        out = "\n".join(list(header) + [l for _k, _f, b in blocks for l in b])
        if not out.startswith("%YAML"):
            print("   拒绝写入：丢了 YAML 头")
            continue
        with open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="\n") as fp:
            fp.write(out)
        again = scan(rel)
        print("   已清理，复检坏引用 %d，块 %d" % (len(again[3]), len(again[1])))


if __name__ == "__main__":
    main()
