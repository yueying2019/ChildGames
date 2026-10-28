"""给 LianliankanGameView.prefab 补三个道具按钮：加时 / 冻结 / 自动连。

做法（prefab-first，不在运行时搭 UI）：
  1. 复制 HintButton 整棵子树（GO + RectTransform + CanvasRenderer + 脚本 + Button +
     Label + Badge），换掉全部 fileID；
  2. 新按钮挂在同一个 Tools 容器下，Label 改文案，Badge 直接隐藏（数量由运行时
     SetToolLabel 写进 Label，Badge 只有 Shuffle/Hint 两个老按钮用）；
  3. Tools 容器从 150x300 拉高到 150x620，五个按钮重排 y = 280/140/0/-140/-280；
  4. 回填 LianliankanGameView 脚本的 TimeButton / FreezeButton / AutoButton 字段。

用法：
    python add_lianliankan_tool_buttons.py            # 试运行
    python add_lianliankan_tool_buttons.py --apply    # 写入
"""

import os
import random
import re
import shutil
import sys

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\Lianliankan\LianliankanGameView.prefab")
BACKUP_DIR = os.path.join(ROOT, r"Tools\_backup")

SOURCE_GO = "3417807666146954352"        # HintButton
TOOLS_RECT = "1718214435636895897"       # Tools 容器
SHUFFLE_RECT = "6707338486671176386"     # ShuffleButton 的 RectTransform
HINT_RECT = "5000742904247746480"        # HintButton 的 RectTransform
LABEL_RECT = "1675501748822518296"
BADGE_RECT = "2681630427122679032"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"

# (名字, y, Label 文案)
TARGETS = [
    ("TimeButton", 0, r"\u52A0\u65F6"),
    ("FreezeButton", -140, r"\u51BB\u7ED3"),
    ("AutoButton", -280, r"\u81EA\u52A8\u8FDE"),
]

# 老按钮上移，给新按钮腾位置
MOVE_OLD = {SHUFFLE_RECT: 280, HINT_RECT: 140}

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
COMPONENT = re.compile(r"- component: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
GAME_OBJECT = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")
TEXT_LINE = re.compile(r'^(\s*)m_Text: ".*"$')


def split_blocks(text):
    """拆成块；同时返回文件头指令行（%YAML 1.1 / %TAG !u! ...）。

    注意：Unity/团结引擎的 YAML 文件开头那两行指令必须原样保留，丢了 !u! 标签
    就无法解析，资源会被 Unity 当成导入失败（AssetDatabase 取到 null）。
    """
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    header = lines[:starts[0]] if starts else []
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append([m.group(1), m.group(2), lines[start:end]])
    return header, blocks


def render(header, blocks):
    """把文件头 + 块重新拼回文本，保证结尾恰好一个换行。"""
    lines = list(header)
    for _kind, _fid, body in blocks:
        lines.extend(body)
    return "\n".join(lines).rstrip("\n") + "\n"


def index_blocks(blocks):
    return {b[1]: b for b in blocks}


def collect_subtree(bmap, root):
    ids = set()
    stack = [root]
    while stack:
        fid = stack.pop()
        if fid in ids or fid not in bmap:
            continue
        ids.add(fid)
        kind, _fid, body = bmap[fid]
        joined = "\n".join(body)
        if kind == "1":
            for m in COMPONENT.finditer(joined):
                stack.append(m.group(1))
        elif kind == "224":
            for line in body:
                m = CHILD.match(line)
                if not m:
                    continue
                child = bmap.get(m.group(1))
                if child is None:
                    continue
                gm = GAME_OBJECT.search("\n".join(child[2]))
                if gm:
                    stack.append(gm.group(1))
    return ids


def fresh_id(taken):
    """新 fileID 必须落在 Int64 范围内。

    血的教训：Unity 的 fileID 是 SInt64，用 randint(10**18, 10**19-1) 会生成超过
    Int64.MaxValue(9223372036854775807) 的值，Unity 读数时截断成同一个 long.MaxValue，
    于是报 Duplicate identifier → BrokenPrefabAsset → Addressables InvalidKeyException。
    """
    while True:
        value = str(random.randint(10 ** 17, (1 << 63) - 2))
        if value not in taken:
            taken.add(value)
            return value


def remap(body, mapping):
    out = []
    for line in body:
        for old, new in mapping.items():
            line = re.sub(r"(?<![\w])%s(?![\w])" % old, new, line)
        out.append(line)
    return out


def find_component(bmap, go, want_kind, guid=None):
    _kind, _fid, body = bmap[go]
    joined = "\n".join(body)
    for m in COMPONENT.finditer(joined):
        cid = m.group(1)
        if cid not in bmap:
            continue
        k2, _f2, body2 = bmap[cid]
        if k2 != want_kind:
            continue
        if guid is not None and guid not in "\n".join(body2):
            continue
        return cid
    return None


def set_anchored_y(body, y):
    out = []
    for line in body:
        if line.strip().startswith("m_AnchoredPosition:"):
            line = "  m_AnchoredPosition: {x: 0, y: %d}" % y
        out.append(line)
    return out


def main():
    apply = "--apply" in sys.argv
    with open(PREFAB, encoding="utf-8", errors="replace") as fp:
        source = fp.read()

    header, blocks = split_blocks(source)
    bmap = index_blocks(blocks)
    if SOURCE_GO not in bmap or TOOLS_RECT not in bmap:
        print("FAIL 源按钮或 Tools 容器缺失")
        return

    def subtree_of_rect(rect):
        gm = GAME_OBJECT.search("\n".join(bmap[rect][2]))
        if gm is None:
            return collect_subtree(bmap, rect)
        return collect_subtree(bmap, gm.group(1))

    ids = collect_subtree(bmap, SOURCE_GO)
    label_ids = subtree_of_rect(LABEL_RECT)
    badge_ids = subtree_of_rect(BADGE_RECT)
    taken = set(bmap)

    plan = []
    for name, y, label in TARGETS:
        mapping = {old: fresh_id(taken) for old in ids}
        plan.append((name, y, label, mapping))

    print("源按钮 HintButton 子树 %d 块，Label %d 块，Badge %d 块" % (len(ids), len(label_ids), len(badge_ids)))
    for name, y, label, mapping in plan:
        print("  %-14s y=%-5d GO=%s rect=%s" % (name, y, mapping[SOURCE_GO], mapping[HINT_RECT]))

    if not apply:
        print("-- dry run，未写入 --")
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    backup = os.path.join(BACKUP_DIR, "LianliankanGameView.prefab.bak")
    if not os.path.exists(backup):
        shutil.copy2(PREFAB, backup)

    # 1) 老按钮上移
    for rect, y in MOVE_OLD.items():
        bmap[rect][2] = set_anchored_y(bmap[rect][2], y)

    # 2) Tools 容器拉高
    tools = bmap[TOOLS_RECT]
    for i, line in enumerate(tools[2]):
        if line.strip().startswith("m_SizeDelta:"):
            tools[2][i] = "  m_SizeDelta: {x: 150, y: 620}"
            break

    # 3) 复制按钮
    new_blocks = []
    new_buttons = []
    for name, y, label, mapping in plan:
        source_button = find_component(bmap, SOURCE_GO, "114", BUTTON_GUID)
        for old in sorted(ids):
            kind, _fid, body = bmap[old]
            body = remap(body, mapping)
            if old == SOURCE_GO:
                body = [line.replace("m_Name: HintButton", "m_Name: " + name) for line in body]
            elif old == HINT_RECT:
                body = set_anchored_y(body, y)
            elif old in label_ids:
                body = [TEXT_LINE.sub(lambda m: '%sm_Text: "%s"' % (m.group(1), label), line) for line in body]
            elif old in badge_ids:
                body = [line.replace("m_IsActive: 1", "m_IsActive: 0") for line in body]
                body = [TEXT_LINE.sub(r'\1m_Text: ""', line) for line in body]
            new_blocks.append([kind, mapping[old], body])
        new_buttons.append((name, mapping[source_button], mapping[HINT_RECT]))

    # 4) 挂到 Tools
    last_child = -1
    for i, line in enumerate(tools[2]):
        if CHILD.match(line):
            last_child = i
    if last_child < 0:
        print("FAIL Tools 没有 m_Children")
        return
    for idx, (_name, _btn, rect) in enumerate(new_buttons):
        tools[2].insert(last_child + 1 + idx, "  - {fileID: %s}" % rect)

    # 5) 回填脚本字段
    wired = False
    for kind, _fid, body in blocks:
        for i, line in enumerate(body):
            if "HintButton: {fileID:" not in line:
                continue
            indent = line[:len(line) - len(line.lstrip())]
            for idx, (name, btn, _rect) in enumerate(new_buttons):
                body.insert(i + 1 + idx, "%s%s: {fileID: %s}" % (indent, name, btn))
            wired = True
            break
        if wired:
            break
    if not wired:
        print("FAIL 没找到 LianliankanGameView 脚本里的 HintButton 字段")
        return

    blocks.extend(new_blocks)
    out = render(header, blocks)
    if not out.startswith("%YAML"):
        print("FAIL 头部指令丢失，拒绝写入")
        return
    with open(PREFAB, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(out)

    print("已写入 %s" % PREFAB)
    print("  Tools 子节点数: %d" % sum(1 for line in tools[2] if CHILD.match(line)))


if __name__ == "__main__":
    main()
