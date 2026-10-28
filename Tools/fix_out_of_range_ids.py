# -*- coding: utf-8 -*-
"""把 Unity YAML 里超出 Int64 范围的 fileID 换成合法值。

事故背景：批量改预制体时新 ID 用 random.randint(10**18, 10**19-1) 生成，上限超过
Int64.MaxValue(9223372036854775807)。Unity 读进来会把它们截断成同一个 long.MaxValue，
于是日志报：
    Duplicate identifier 9223372036854775807. File: "xxx.prefab".
    Error loading the file 'xxx.prefab'. File has multiple objects with same identifiers.
结果该资源被当成 BrokenPrefabAsset，Addressables 加载时抛 InvalidKeyException。

本脚本：找出所有 >Int64.MaxValue 或 <Int64.MinValue 的 fileID（块头 + 引用处统一替换），
逐个换成 [10**17, Int64.MaxValue) 内且不与现有 ID 冲突的新值。

用法：
    python fix_out_of_range_ids.py              # 检查
    python fix_out_of_range_ids.py --apply      # 写入
"""

import os
import random
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INT64_MAX = (1 << 63) - 1
INT64_MIN = -(1 << 63)

TARGETS = [
    r"Assets\Content\Lianliankan\LianliankanGameView.prefab",
    r"Assets\Content\Match3\Match3GameView.prefab",
    r"Assets\Content\Sheep\SheepGameView.prefab",
]

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
FID_REF = re.compile(r"fileID: (-?\d+)")


def out_of_range(value):
    return value > INT64_MAX or value < INT64_MIN


def scan(rel):
    path = os.path.join(ROOT, rel)
    with open(path, encoding="utf-8", errors="replace") as fp:
        text = fp.read()
    heads = [int(m.group(2)) for m in HEADER.finditer(text)]
    refs = [int(v) for v in FID_REF.findall(text)]
    bad = sorted({v for v in heads + refs if out_of_range(v)})
    return text, set(heads) | set(refs), bad


def fresh(taken):
    while True:
        value = random.randint(10 ** 17, INT64_MAX - 1)
        if value not in taken:
            taken.add(value)
            return value


def main():
    apply = "--apply" in sys.argv
    for rel in TARGETS:
        if not os.path.exists(os.path.join(ROOT, rel)):
            print("SKIP  %s（不存在）" % rel)
            continue
        text, taken, bad = scan(rel)
        print("%s  越界 ID %d 个%s" % (rel.split("\\")[-1], len(bad), (" -> " + str(bad)) if bad else ""))
        if not bad or not apply:
            continue

        out = text
        for old in bad:
            new = fresh(taken)
            pattern = re.compile(r"(?<![\w-])%d(?![\w])" % old)
            hits = len(pattern.findall(out))
            out = pattern.sub(str(new), out)
            print("     %d -> %d（替换 %d 处）" % (old, new, hits))

        if not out.startswith("%YAML"):
            print("FAIL 头部丢失，拒绝写入")
            continue
        with open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="\n") as fp:
            fp.write(out.rstrip("\n") + "\n")
        print("     已写入")

    print("-" * 60)
    for rel in TARGETS:
        if not os.path.exists(os.path.join(ROOT, rel)):
            continue
        _text, _taken, bad = scan(rel)
        print("复检 %-32s 越界 %d" % (rel.split("\\")[-1], len(bad)))


if __name__ == "__main__":
    random.seed(20260922)
    main()
