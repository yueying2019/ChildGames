# -*- coding: utf-8 -*-
"""严格校验 Unity 预制体的对象图一致性，定位 BrokenPrefabAsset 成因。

检查项（任何一条不满足都可能让 Unity 把 prefab 判为 broken）：
  1. GO 的 m_Component 列表里每一项必须存在，且那个组件的 m_GameObject 指回本 GO；
  2. 组件块的 m_GameObject 必须存在，且被登记在对应 GO 的 m_Component 里；
  3. Transform(224) 的 m_Children 里每一项必须是 224，且它的 m_Father 指回本 Transform；
  4. 224 的 m_Father（非根）必须反查得到——父的 m_Children 里必须有它；
  5. 任何对象不能被两个父节点同时挂载；
  6. 同一父节点下不能有重复子节点；
  7. 从根节点出发 BFS 必须覆盖全部对象，无环。

用法：
    python audit_prefab_graph.py <prefab 路径> [对照路径]
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
COMPONENT = re.compile(r"^\s*- component: \{fileID: (-?\d+)\}\s*$")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
FATHER = re.compile(r"^\s*m_Father: \{fileID: (-?\d+)\}\s*$")
GO_OF = re.compile(r"^\s*m_GameObject: \{fileID: (-?\d+)\}\s*$")


def split(text):
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    blocks = {}
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks[m.group(2)] = (m.group(1), lines[start:end])
    return blocks


def parse(blocks):
    info = {}
    for fid, (kind, body) in blocks.items():
        comps = [m.group(1) for line in body if (m := COMPONENT.match(line))]
        go = None
        for line in body:
            m = GO_OF.match(line)
            if m:
                go = m.group(1)
                break
        father = None
        children = []
        if kind == "224":
            for line in body:
                m = FATHER.match(line)
                if m:
                    father = m.group(1)
                m2 = CHILD.match(line)
                if m2:
                    children.append(m2.group(1))
        info[fid] = {"kind": kind, "comps": comps, "go": go, "father": father, "children": children}
    return info


def audit(path):
    with open(path, encoding="utf-8", errors="replace") as fp:
        text = fp.read()
    blocks = split(text)
    info = parse(blocks)
    problems = []

    def hits():
        return len(problems)

    # 1/2 组件与 GO 互指
    for fid, node in info.items():
        if node["kind"] == "1":
            for cid in node["comps"]:
                other = info.get(cid)
                if other is None:
                    problems.append("GO %s 的 component 列表指向不存在的 %s" % (fid, cid))
                    continue
                if other["go"] != fid:
                    problems.append("GO %s 挂了组件 %s，但该组件的 m_GameObject=%s" % (fid, cid, other["go"]))
        else:
            owner = node["go"]
            if owner is None:
                problems.append("组件 %s(class %s) 没有 m_GameObject" % (fid, node["kind"]))
                continue
            owner_node = info.get(owner)
            if owner_node is None:
                problems.append("组件 %s 的 m_GameObject %s 不存在" % (fid, owner))
            elif fid not in owner_node["comps"]:
                problems.append("组件 %s 声称属于 GO %s，但 GO 没在 m_Component 里登记它" % (fid, owner))

    # 3/4 Transform 父子互指
    for fid, node in info.items():
        if node["kind"] != "224":
            continue
        for cid in node["children"]:
            child = info.get(cid)
            if child is None:
                problems.append("Transform %s 的子节点 %s 不存在" % (fid, cid))
                continue
            if child["kind"] != "224":
                problems.append("Transform %s 的子节点 %s 不是 Transform（class %s）" % (fid, cid, child["kind"]))
            elif child["father"] != fid:
                problems.append("Transform %s 把 %s 当子节点，但它的 m_Father=%s" % (fid, cid, child["father"]))
        f = node["father"]
        if f and f != "0" and f in info:
            parent = info[f]
            if parent["kind"] == "224" and fid not in parent["children"]:
                problems.append("Transform %s 的 m_Father=%s，但父节点没在 m_Children 里登记它" % (fid, f))

    # 5 重复挂载
    seen = {}
    for fid, node in info.items():
        if node["kind"] != "224":
            continue
        for cid in node["children"]:
            seen.setdefault(cid, []).append(fid)
    for cid, parents in seen.items():
        if len(set(parents)) > 1:
            problems.append("Transform %s 被多个父节点挂载：%s" % (cid, parents))

    # 6 同一父下重复子节点
    for fid, node in info.items():
        if node["kind"] != "224":
            continue
        dup = [c for c in node["children"] if node["children"].count(c) > 1]
        if dup:
            problems.append("Transform %s 的子节点重复：%s" % (fid, sorted(set(dup))))

    # 7 从根 GO 出发覆盖性 + 环检测
    roots = [fid for fid, node in info.items() if node["kind"] == "224" and (node["father"] in (None, "0") or node["father"] not in info)]
    reachable = set()
    for root in roots:
        stack = [root]
        while stack:
            cur = stack.pop()
            if cur in reachable:
                problems.append("对象图存在环，%s 被再次访问" % cur)
                continue
            reachable.add(cur)
            node = info.get(cur)
            if node is None:
                continue
            for cid in node["children"]:
                stack.append(cid)
    missing = [fid for fid, node in info.items() if node["kind"] == "224" and fid not in reachable]
    if missing:
        problems.append("有 %d 个 Transform 从根不可达：%s" % (len(missing), missing[:5]))

    print("=== %s" % os.path.basename(path))
    print("   对象 %d  其中 Transform %d  GO %d  根 %d" % (
        len(info), sum(1 for n in info.values() if n["kind"] == "224"),
        sum(1 for n in info.values() if n["kind"] == "1"), len(roots)))
    print("   问题 %d 条" % len(problems))
    for p in problems[:12]:
        print("     - " + p)
    return set(problems)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    target = sys.argv[1]
    cur = audit(target)
    if len(sys.argv) > 2:
        base = audit(sys.argv[2])
        new = cur - base
        print("-" * 60)
        print("相对对照组新增问题 %d 条：" % len(new))
        for p in sorted(new)[:20]:
            print("     + " + p)


if __name__ == "__main__":
    main()
