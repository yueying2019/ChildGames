"""给 Result.prefab 加一颗「双倍奖励」按钮（prefab-first，不在运行时搭 UI）。

做法：整体复制 Retry 的子树（GO + RectTransform + CanvasRenderer + Image + Button
+ Label 子树），换掉所有 fileID，改名 Double，改成居中摆放，然后把 Retry / Menu
两边挪开、一起收窄成三颗并排，最后回填 ResultPanel 的 DoubleButton 字段。

用法：
    python add_result_double_button.py            # 试运行，只打印
    python add_result_double_button.py --apply    # 写入
"""

import os
import random
import re
import shutil
import sys
import time

ROOT = r"E:\UnityGame\SmallGames"
PREFAB = os.path.join(ROOT, r"Assets\Content\UI\Result.prefab")
BACKUP_DIR = os.path.join(ROOT, "Tools", "_backup")

SOURCE_GO = "2582063931717860011"      # Retry
WINDOW_RECT = "5443521986411593588"    # Window
RETRY_RECT = "3264777286207999836"
MENU_RECT = "5898988507979968259"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"

SOURCE_NAME = "Retry"
TARGET_NAME = "Double"
SOURCE_TEXT = r'm_Text: "\u518D\u6765\u4E00\u5C40"'      # 再来一局
TARGET_TEXT = r'm_Text: "\u53CC\u500D\u5956\u52B1"'      # 双倍奖励

# 三颗并排：Retry 左、Double 中、Menu 右
LAYOUT = {
    RETRY_RECT: ("{x: -186, y: 76}", "{x: 170, y: 80}"),
    MENU_RECT: ("{x: 186, y: 76}", "{x: 170, y: 80}"),
}
NEW_POSITION = "{x: 0, y: 76}"
NEW_SIZE = "{x: 170, y: 80}"

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$")
COMPONENT = re.compile(r"- component: \{fileID: (-?\d+)\}")
CHILD = re.compile(r"^\s*- \{fileID: (-?\d+)\}\s*$")
GAME_OBJECT = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")


def split_blocks(text):
    """拆成块，同时返回文件头指令行（%YAML 1.1 / %TAG !u! ...）。

    这两行指令必须原样保留：丢了 !u! 标签 Unity 无法解析，资源会导入失败。
    """
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    header = lines[:starts[0]] if starts else []
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        match = HEADER.match(lines[start])
        blocks.append([match.group(1), match.group(2), lines[start:end]])
    return header, blocks


def render(header, blocks):
    lines = list(header)
    for _kind, _fid, body in blocks:
        lines.extend(body)
    return "\n".join(lines).rstrip("\n") + "\n"


def index_blocks(blocks):
    return {block[1]: block for block in blocks}


def collect_subtree(bmap, root):
    """从 GO fileID 出发递归收集整棵子树的 fileID。"""
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
    while True:
        # fileID 是 SInt64，超限会被截断成同一个 long.MaxValue → Duplicate identifier
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


def retarget_rect(body, position, size):
    out = []
    for line in body:
        if line.startswith("  m_AnchoredPosition:"):
            out.append("  m_AnchoredPosition: " + position)
        elif line.startswith("  m_SizeDelta:"):
            out.append("  m_SizeDelta: " + size)
        else:
            out.append(line)
    return out


def main():
    apply = "--apply" in sys.argv
    with open(PREFAB, encoding="utf-8", errors="replace") as fp:
        source = fp.read()

    header, blocks = split_blocks(source)
    bmap = index_blocks(blocks)
    for fid in (SOURCE_GO, WINDOW_RECT, RETRY_RECT, MENU_RECT):
        if fid not in bmap:
            print("FAIL 找不到 fileID %s" % fid)
            return

    ids = collect_subtree(bmap, SOURCE_GO)
    taken = set(bmap)
    mapping = {old: fresh_id(taken) for old in ids}

    # 定位源子树里的 RectTransform 和 Button
    source_rect = None
    source_button = None
    joined = "\n".join(bmap[SOURCE_GO][2])
    for m in COMPONENT.finditer(joined):
        cid = m.group(1)
        if cid not in bmap:
            continue
        kind, _fid, body = bmap[cid]
        text_block = "\n".join(body)
        if kind == "224" and source_rect is None:
            source_rect = cid
        if kind == "114" and BUTTON_GUID in text_block:
            source_button = cid
    if source_rect is None or source_button is None:
        print("FAIL 源按钮结构不完整 rect=%s button=%s" % (source_rect, source_button))
        return

    new_blocks = []
    for old in sorted(ids):
        kind, _fid, body = bmap[old]
        body = remap(body, mapping)
        if old == SOURCE_GO:
            body = [line.replace("m_Name: " + SOURCE_NAME, "m_Name: " + TARGET_NAME) for line in body]
        elif SOURCE_TEXT in "\n".join(body):
            body = [line.replace(SOURCE_TEXT, TARGET_TEXT) for line in body]
        if old == source_rect:
            body = retarget_rect(body, NEW_POSITION, NEW_SIZE)
        new_blocks.append([kind, mapping[old], body])

    print("复制子树 %d 块" % len(new_blocks))
    print("  新 GO      %s -> %s" % (SOURCE_GO, mapping[SOURCE_GO]))
    print("  新 Rect    %s -> %s" % (source_rect, mapping[source_rect]))
    print("  新 Button  %s -> %s" % (source_button, mapping[source_button]))

    if not apply:
        print("-- dry run，未写入 --")
        return

    # 1) 备份
    os.makedirs(BACKUP_DIR, exist_ok=True)
    stamp = time.strftime("%Y%m%d_%H%M%S")
    shutil.copy2(PREFAB, os.path.join(BACKUP_DIR, "Result.prefab.%s.double" % stamp))

    # 2) 新 RectTransform 挂到 Window 的 m_Children 末尾
    window = bmap[WINDOW_RECT]
    last_child = -1
    for i, line in enumerate(window[2]):
        if CHILD.match(line):
            last_child = i
    if last_child < 0:
        print("FAIL Window 没有 m_Children")
        return
    window[2].insert(last_child + 1, "  - {fileID: %s}" % mapping[source_rect])

    # 3) Retry / Menu 两边挪开并收窄
    for fid in LAYOUT:
        position, size = LAYOUT[fid]
        bmap[fid][2] = retarget_rect(bmap[fid][2], position, size)

    # 4) 回填 ResultPanel 的 DoubleButton 字段
    wired = False
    for kind, _fid, body in blocks:
        for i, line in enumerate(body):
            if "MenuButton: {fileID:" not in line:
                continue
            indent = line[:len(line) - len(line.lstrip())]
            body.insert(i + 1, "%sDoubleButton: {fileID: %s}" % (indent, mapping[source_button]))
            wired = True
            break
        if wired:
            break
    if not wired:
        print("FAIL 没找到 ResultPanel 脚本里的 MenuButton 字段")
        return

    # 新块必须同时进索引和待写列表：只进其中一个会留下指向不存在块的孤儿子节点引用
    blocks.extend(new_blocks)
    for kind, fid, body in new_blocks:
        bmap[fid] = [kind, fid, body]

    out = render(header, blocks)
    if not out.startswith("%YAML"):
        print("FAIL 头部指令丢失，拒绝写入")
        return
    if len(bmap) != len(blocks):
        print("FAIL 块索引与待写块数量不一致 %d != %d" % (len(bmap), len(blocks)))
        return

    with open(PREFAB, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(out)

    print("已写入 %s" % PREFAB)
    print("  Window 子节点数: %d" % sum(1 for line in window[2] if CHILD.match(line)))


if __name__ == "__main__":
    main()
