#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity / 团结引擎 预制体 YAML 裁剪工具。

背景：在 Unity 外部删除预制体里的 GameObject 子树，需要同时删掉它的
所有组件块，以及父节点 m_Children 里对它的引用，手改极易写坏。
本脚本按 fileID 图关系做这件事。

用法：
  python unity_prefab_prune.py --dump  <prefab>
  python unity_prefab_prune.py --prune <prefab> --names "A,B" [--props "P1,P2"]

--dump  打印层级：class 类型 / fileID / 名字 / 锚点，用于确认要删谁。
--prune 就地裁剪（先写 .bak 备份）。--names 是要删除的 GameObject 名字
        （英文名或 "378QTab" 这类原文都行）。--props 是要从所有块里删掉的
        序列化属性行，例如已从 C# 类里移除的字段。
"""

import argparse
import io
import re
import shutil
import sys

HEADER_RE = re.compile(r"(?m)^--- !u!(\d+) &(\d+)\s*$")
NAME_RE = re.compile(r"(?m)^  m_Name: (.*)$")
OWNER_RE = re.compile(r"(?m)^  m_GameObject: \{fileID: (\d+)\}")
CHILDREN_RE = re.compile(r"(?m)^  m_Children:(.*)$")
CHILD_ITEM_RE = re.compile(r"^  - \{fileID: (\d+)\}")
ANCHOR_RE = re.compile(r"(?m)^  m_AnchoredPosition: (.*)$")

CLASS_NAMES = {
    "1": "GameObject",
    "4": "Transform",
    "114": "MonoBehaviour",
    "222": "CanvasRenderer",
    "223": "MeshRenderer",
    "224": "RectTransform",
}


def read_text(path):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write_text(path, text):
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def decode_name(raw):
    raw = raw.strip()
    if len(raw) >= 2 and raw[0] == '"' and raw[-1] == '"':
        body = raw[1:-1]
        return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), body)
    return raw


def parse(text):
    hits = list(HEADER_RE.finditer(text))
    if not hits:
        raise SystemExit("找不到任何 --- !u! 块，确认这是 Unity YAML 吗？")
    preamble = text[: hits[0].start()]
    blocks = []
    for i, m in enumerate(hits):
        end = hits[i + 1].start() if i + 1 < len(hits) else len(text)
        blocks.append({"cls": m.group(1), "fid": m.group(2), "text": text[m.start():end]})
    return preamble, blocks


def block_name(b):
    if b["cls"] != "1":
        return None
    m = NAME_RE.search(b["text"])
    return decode_name(m.group(1)) if m else None


def block_owner(b):
    m = OWNER_RE.search(b["text"])
    return m.group(1) if m else None


def block_children(b):
    m = CHILDREN_RE.search(b["text"])
    if not m:
        return []
    if m.group(1).strip().startswith("[]"):
        return []
    out = []
    for line in b["text"][m.end():].split("\n")[1:]:
        mm = CHILD_ITEM_RE.match(line)
        if mm:
            out.append(mm.group(1))
        else:
            break
    return out


def build_index(blocks):
    go_by_rect = {}
    rect_by_go = {}
    children = {}
    comps_by_go = {}
    for b in blocks:
        owner = block_owner(b)
        if b["cls"] == "224" and owner:
            go_by_rect[b["fid"]] = owner
            rect_by_go[owner] = b["fid"]
            children[b["fid"]] = block_children(b)
        if owner:
            comps_by_go.setdefault(owner, []).append(b["fid"])
    return go_by_rect, rect_by_go, children, comps_by_go


def dump(path):
    _, blocks = parse(read_text(path))
    go_by_rect, rect_by_go, children, comps_by_go = build_index(blocks)
    by_fid = {b["fid"]: b for b in blocks}

    all_children = set()
    for rect, kids in children.items():
        all_children.update(kids)
    roots = [b for b in blocks
             if b["cls"] == "1" and rect_by_go.get(b["fid"]) not in all_children]

    def walk(go_fid, depth):
        b = by_fid.get(go_fid)
        if not b:
            return
        rect = rect_by_go.get(go_fid)
        anchor = ""
        if rect and rect in by_fid:
            m = ANCHOR_RE.search(by_fid[rect]["text"])
            if m:
                anchor = m.group(1)
        comps = [by_fid[c]["cls"] for c in comps_by_go.get(go_fid, []) if c in by_fid]
        print("{}{} &{}  rect={}  anchor={}  comps=[{}]".format(
            "  " * depth, block_name(b), go_fid, rect, anchor, ",".join(comps)))
        if rect:
            for child_rect in children.get(rect, []):
                cg = go_by_rect.get(child_rect)
                if cg:
                    walk(cg, depth + 1)

    if not roots:
        roots = [b for b in blocks if b["cls"] == "1"][:1]
    for r in roots:
        walk(r["fid"], 0)


def prune(path, names, props, moves, ids=None):
    text = read_text(path)
    preamble, blocks = parse(text)
    go_by_rect, rect_by_go, children, comps_by_go = build_index(blocks)

    targets = set()
    for b in blocks:
        n = block_name(b)
        if n is not None and n in names:
            targets.add(b["fid"])
        if b["cls"] == "1" and b["fid"] in (ids or set()):
            targets.add(b["fid"])
    if not targets:
        raise SystemExit("没找到要删的 GameObject：{} / {}".format(",".join(sorted(names)), ids))

    removed_go = set()
    todo = list(targets)
    while todo:
        gid = todo.pop()
        if gid in removed_go:
            continue
        removed_go.add(gid)
        rect = rect_by_go.get(gid)
        if rect:
            for child_rect in children.get(rect, []):
                cg = go_by_rect.get(child_rect)
                if cg and cg not in removed_go:
                    todo.append(cg)

    removed_fids = set(removed_go)
    for gid in removed_go:
        for c in comps_by_go.get(gid, []):
            removed_fids.add(c)
        rect = rect_by_go.get(gid)
        if rect:
            removed_fids.add(rect)

    kept = []
    for b in blocks:
        if b["fid"] in removed_fids:
            continue
        owner = block_owner(b)
        if owner and owner in removed_go:
            continue
        kept.append(b)

    # 清掉保留下来的 m_Children 里对被删节点的引用
    for b in kept:
        m = CHILDREN_RE.search(b["text"])
        if not m or m.group(1).strip().startswith("[]"):
            continue
        head = b["text"][: m.end()]
        rest = b["text"][m.end():]
        lines = rest.split("\n")
        kept_lines = [lines[0]]
        items = 0
        for line in lines[1:]:
            mm = CHILD_ITEM_RE.match(line)
            if mm:
                if mm.group(1) not in removed_fids:
                    kept_lines.append(line)
                    items += 1
                continue
            kept_lines.append(line)
        body = "\n".join(kept_lines)
        if items == 0:
            body = " []" + body[len(lines[0]):]
        b["text"] = head + body

    if props:
        prop_res = [re.compile(r"(?m)^  " + re.escape(p) + r":.*\n?") for p in props]
        for b in kept:
            for pr in prop_res:
                b["text"] = pr.sub("", b["text"])

    if moves:
        kept_by_fid = {b["fid"]: b for b in kept}
        for spec in moves:
            name, _, coord = spec.partition(":")
            target = None
            for b in kept:
                if block_name(b) == name:
                    target = b
                    break
            if target is None:
                print("移动跳过（没找到）: " + name)
                continue
            rect = rect_by_go.get(target["fid"])
            rblock = kept_by_fid.get(rect)
            if rblock is None:
                print("移动跳过（没有 RectTransform）: " + name)
                continue
            x, y = coord.split(",")
            new_line = "  m_AnchoredPosition: {x: %s, y: %s}" % (x.strip(), y.strip())
            replaced, n = ANCHOR_RE.subn(new_line, rblock["text"], count=1)
            if n:
                rblock["text"] = replaced
                print("已移动 {} 到 {}".format(name, new_line.strip()))

    out = preamble + "".join(b["text"] for b in kept)
    shutil.copyfile(path, path + ".bak")
    write_text(path, out)
    print("已删除 {} 个 GameObject（含组件共 {} 个块），备份：{}".format(
        len(removed_go), len(blocks) - len(kept), path + ".bak"))
    print("删除的对象：{}".format(", ".join(sorted(names))))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump")
    ap.add_argument("--prune")
    ap.add_argument("--names", default="")
    ap.add_argument("--ids", default="")
    ap.add_argument("--props", default="")
    ap.add_argument("--move", action="append", default=[])
    args = ap.parse_args()

    if args.dump:
        dump(args.dump)
        return
    if args.prune:
        names = set(x for x in args.names.split(",") if x)
        props = [x for x in args.props.split(",") if x]
        ids = set(x for x in args.ids.split(",") if x)
        prune(args.prune, names, props, args.move, ids)
        return
    ap.print_help()


if __name__ == "__main__":
    main()
