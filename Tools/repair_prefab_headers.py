# -*- coding: utf-8 -*-
"""修复被 Python 改写脚本写丢的 Unity YAML 文件头（%YAML 1.1 / %TAG !u! ...）。

背景：直接拆分 `--- !u!<class> &<fileID>` 再 join 回去的写法会把文件最前面的
两行指令丢掉。团结引擎的 TAG 是 `tag:yousandi.cn,2023:`，缺了它 `!u!114` 这类
标签无法解析，AssetDatabase 导入失败 -> LoadAssetAtPath 返回 null -> Addressables
也查不到 key，于是运行时报「[UI] 缺少预制体 / [AssetHub] 缺少资源」。

本脚本：
  1. 从 Tools/_backup 的同名备份里取回前缀两行；
  2. 校验当前文件的块集合是备份的超集（防止连内容一起丢）；
  3. 补写前缀 + 补回末尾换行 + 重建性检查悬空引用。

用法：
    python repair_prefab_headers.py          # 检查
    python repair_prefab_headers.py --apply  # 写入
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BACKUP_DIR = os.path.join(ROOT, "Tools", "_backup")

TARGETS = [
    (r"Assets\Content\Lianliankan\LianliankanGameView.prefab", "LianliankanGameView.prefab.bak"),
    (r"Assets\Content\Match3\Match3GameView.prefab", "Match3GameView.prefab.bak"),
    (r"Assets\Content\Sheep\SheepGameView.prefab", "SheepGameView.prefab"),
]

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
FID_REF = re.compile(r"fileID: (-?\d+)")


def split(text):
    """返回 (preamble_lines, blocks)，blocks = [(kind, fid, [lines])]。"""
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    if not starts:
        raise ValueError("文件里没有任何文档块：%s" % lines[:2])
    preamble = lines[:starts[0]]
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append((m.group(1), m.group(2), lines[start:end]))
    return preamble, blocks


def dangling_of(blocks):
    known = {f for _k, f, _b in blocks}
    bad = []
    for kind, fid, body in blocks:
        for m in FID_REF.finditer("\n".join(body)):
            ref = m.group(1)
            # 内置资源 fileID 都是 8 位以内（12800000=默认字体，21300000=默认 sprite，
            # 11500000=缺失脚本），它们不对应文件里的块，不能算悬空。
            if ref == "0" or ref == fid or len(ref) <= 8:
                continue
            if ref not in known:
                bad.append((fid, ref))
    return bad


def check(rel_prefab):
    path = os.path.join(ROOT, rel_prefab)
    with open(path, encoding="utf-8", errors="replace") as fp:
        text = fp.read()
    preamble, blocks = split(text)
    if preamble and preamble[0].startswith("%YAML"):
        print("OK   %s 头部正常（%d 块）" % (rel_prefab, len(blocks)))
    else:
        print("BAD  %s 缺头部，当前首行 %r（%d 块）" % (rel_prefab, text.split("\n")[0][:40], len(blocks)))
    bad = dangling_of(blocks)
    print("     悬空引用 %d%s" % (len(bad), (" 例：" + str(bad[:3])) if bad else ""))
    return preamble, blocks, bad


def main():
    apply = "--apply" in sys.argv
    failures = 0
    plans = []

    for rel, backup_name in TARGETS:
        cur_preamble, cur_blocks, bad = check(rel)
        if bad:
            failures += 1
        backup = os.path.join(BACKUP_DIR, backup_name)
        if not os.path.exists(backup):
            print("     无备份：%s" % backup_name)
            continue
        with open(backup, encoding="utf-8", errors="replace") as fp:
            bak_preamble, bak_blocks = split(fp.read())
        if not bak_preamble or not bak_preamble[0].startswith("%YAML"):
            print("     备份也没有头部，跳过")
            continue

        missing = set(f for _k, f, _b in bak_blocks) - set(f for _k, f, _b in cur_blocks)
        print("     备份 %d 块，缺失 %d%s" % (len(bak_blocks), len(missing), (" 例：" + str(list(missing)[:3])) if missing else ""))
        if missing:
            failures += 1

        if cur_preamble and cur_preamble[0].startswith("%YAML"):
            continue

        plans.append((rel, bak_preamble, text_of(os.path.join(ROOT, rel)), cur_blocks))

    print("-" * 60)
    if failures:
        print("注意：有 %d 处结构性问题（缺失块或悬空引用），见上。" % failures)
    if not plans:
        print("没有需要补头的文件")
        return
    if not apply:
        print("待补 ^  ^  ^ 的文件：%s" % ", ".join(p[0].split("\\")[-1] for p in plans))
        print("-- dry run，未写入 --")
        return

    for rel, preamble, text, blocks in plans:
        path = os.path.join(ROOT, rel)
        body = "\n".join(line for _k, _f, b in blocks for line in b)
        out = "\n".join(preamble).rstrip("\n") + "\n" + body.rstrip("\n") + "\n"
        with open(path, "w", encoding="utf-8", newline="\n") as fp:
            fp.write(out)
        print("已修复 %s" % rel)

    print("-" * 60)
    for rel, _b, _t, _p in plans:
        check(rel)


def text_of(path):
    with open(path, encoding="utf-8", errors="replace") as fp:
        return fp.read()


if __name__ == "__main__":
    main()
