"""给三个玩法的道具按钮挂图标、去掉文字标签。

对每个按钮：
  1. 保证存在名为 Icon 的子节点且挂 Image（若原来是 Text 则整块换成 Image），并设置 sprite；
  2. 隐藏名为 Label 的文字节点（三消改为"复用成右下角数量角标"）；
  3. 对对消额外把 Badge（含其子节点）激活，数量写进 Badge/Label。

用法：
  python Tools/apply_tool_icons.py           # 只报告
  python Tools/apply_tool_icons.py --apply   # 写盘（自动备份到 Tools/_backup）
"""
import os
import random
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BACKUP = os.path.join(ROOT, "Tools", "_backup")

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
INT64_MAX = (1 << 63) - 1
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"
SPRITE_FILEID = "21300000"

# prefab -> [(按钮名, 图标 png 相对路径)]
CONFIG = {
    "Assets/Content/Sheep/SheepGameView.prefab": [
        ("RemoveButton", "Assets/UI/Sheep/SheepRemove.png"),
        ("UndoButton", "Assets/UI/Sheep/SheepUndo.png"),
        ("ShuffleButton", "Assets/UI/Sheep/SheepShuffle.png"),
        ("SlotButton", "Assets/UI/Sheep/SheepSlot.png"),
    ],
    "Assets/Content/Lianliankan/LianliankanGameView.prefab": [
        ("ShuffleButton", "Assets/UI/Lianliankan/LianShuffle.png"),
        ("HintButton", "Assets/UI/Lianliankan/LianHint.png"),
        ("TimeButton", "Assets/UI/Lianliankan/LianTime.png"),
        ("FreezeButton", "Assets/UI/Lianliankan/LianFreeze.png"),
        ("AutoButton", "Assets/UI/Lianliankan/LianAuto.png"),
    ],
    "Assets/Content/Match3/Match3GameView.prefab": [
        ("HintButton", "Assets/UI/Match3/Match3Hint.png"),
        ("ShuffleButton", "Assets/UI/Match3/Match3Shuffle.png"),
        ("HammerButton", "Assets/UI/Match3/Match3Hammer.png"),
        ("LineHButton", "Assets/UI/Match3/Match3LineH.png"),
        ("LineVButton", "Assets/UI/Match3/Match3LineV.png"),
        ("BombButton", "Assets/UI/Match3/Match3Bomb.png"),
        ("MovesButton", "Assets/UI/Match3/Match3Moves.png"),
        ("ColorBombButton", "Assets/UI/Match3/Match3ColorBomb.png"),
        ("ColorClearButton", "Assets/UI/Match3/Match3ColorClear.png"),
    ],
}

# 三消：Label 复用成右下角数量角标；其余玩法：Label 直接隐藏
LABEL_BADGE = {"Assets/Content/Match3/Match3GameView.prefab"}
# 对对消：数量写在 Badge/Label，需要保证 Badge 链路激活
BADGE_ON = {"Assets/Content/Lianliankan/LianliankanGameView.prefab"}

FAIL = []


def fail(msg):
    FAIL.append(msg)
    print("FAIL " + msg)


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as fp:
        return fp.read()


def split_blocks(text):
    """返回 (header_lines, blocks)；header 是 %YAML/%TAG 指令，必须原样回写。"""
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    if not starts:
        fail("没有任何 YAML 块")
        return [], []
    header = lines[:starts[0]]
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append([m.group(1), m.group(2), lines[start:end]])
    return header, blocks


def render(header, blocks):
    parts = list(header)
    for _k, _f, body in blocks:
        parts.extend(body)
    return "\n".join(parts)


def guid_of(png_rel):
    meta = read(png_rel + ".meta")
    m = re.search(r"^guid: (\S+)", meta, re.M)
    if not m:
        fail("读不到 guid: " + png_rel)
        return None
    return m.group(1)


def fresh_id(taken):
    while True:
        value = str(random.randint(10 ** 17, INT64_MAX - 1))
        if value not in taken:
            taken.add(value)
            return value


def block_field(block, key):
    for line in block[2]:
        m = re.match(r"^\s+%s: (.*)$" % key, line)
        if m:
            return m.group(1)
    return None


def set_field(block, key, value):
    pat = re.compile(r"^(\s+)%s: .*$" % key)
    for i, line in enumerate(block[2]):
        m = pat.match(line)
        if m:
            block[2][i] = "%s%s: %s" % (m.group(1), key, value)
            return True
    return False


def go_of(block):
    m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", "\n".join(block[2]))
    return m.group(1) if m else None


def components_of(goblock):
    return re.findall(r"- component: \{fileID: (-?\d+)\}", "\n".join(goblock[2]))


def children_of(block):
    kids = []
    body = block[2]
    for i, line in enumerate(body):
        if re.match(r"^\s+m_Children:", line):
            j = i + 1
            while j < len(body) and re.match(r"^\s+- \{fileID:", body[j]):
                kids.append(re.search(r"fileID: (-?\d+)", body[j]).group(1))
                j += 1
            break
    return kids


def add_child(block, kid):
    body = block[2]
    for i, line in enumerate(body):
        if re.match(r"^\s+m_Children:", line):
            j = i + 1
            while j < len(body) and re.match(r"^\s+- \{fileID:", body[j]):
                j += 1
            if j == i + 1:
                body[j - 1] = body[j - 1].rstrip() + "\n    - {fileID: %s}" % kid
            else:
                body.insert(j, "    - {fileID: %s}" % kid)
            return True
    return False


def script_guid(block):
    for line in block[2]:
        m = re.search(r"m_Script: \{fileID: \d+, guid: (\w+)", line)
        if m:
            return m.group(1)
    return None


def size_of(block):
    m = re.search(r"m_SizeDelta: \{x: (-?[\d.]+), y: (-?[\d.]+)\}", "\n".join(block[2]))
    return (float(m.group(1)), float(m.group(2))) if m else (0.0, 0.0)


def make_image_block(fid, go, sprite_guid):
    body = [
        "--- !u!114 &%s" % fid,
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % IMAGE_GUID,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_Material: {fileID: 0}",
        "  m_Color: {r: 1, g: 1, b: 1, a: 1}",
        "  m_RaycastTarget: 0",
        "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}",
        "  m_Maskable: 1",
        "  m_OnCullStateChanged:",
        "    m_PersistentCalls:",
        "      m_Calls: []",
        "  m_Sprite: {fileID: %s, guid: %s, type: 3}" % (SPRITE_FILEID, sprite_guid),
        "  m_Type: 0",
        "  m_PreserveAspect: 1",
        "  m_FillCenter: 1",
        "  m_FillMethod: 4",
        "  m_FillAmount: 1",
        "  m_FillClockwise: 1",
        "  m_FillOrigin: 0",
        "  m_UseSpriteMesh: 0",
        "  m_PixelsPerUnitMultiplier: 1",
        "",
    ]
    return ["114", fid, body]


def make_rect_block(fid, go, father, size, pos):
    body = [
        "--- !u!224 &%s" % fid,
        "RectTransform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}",
        "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children: []",
        "  m_Father: {fileID: %s}" % father,
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
        "  m_AnchorMin: {x: 0.5, y: 0.5}",
        "  m_AnchorMax: {x: 0.5, y: 0.5}",
        "  m_AnchoredPosition: {x: %s, y: %s}" % pos,
        "  m_SizeDelta: {x: %s, y: %s}" % size,
        "  m_Pivot: {x: 0.5, y: 0.5}",
        "",
    ]
    return ["224", fid, body]


def make_canvas_block(fid, go):
    body = [
        "--- !u!222 &%s" % fid,
        "CanvasRenderer:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_CullTransparentMesh: 1",
        "",
    ]
    return ["222", fid, body]


def make_go_block(fid, name, comps):
    body = ["--- !u!1 &%s" % fid, "GameObject:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
            "  serializedVersion: 7", "  m_Component:"]
    for c in comps:
        body.append("  - component: {fileID: %s}" % c)
    body += [
        "  m_Layer: 0",
        "  m_HasEditorInfo: 1",
        "  m_Name: %s" % name,
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        "  m_IsActive: 1",
        "",
    ]
    return ["1", fid, body]


def ensure_icon(rel, bmap, blocks, button_rect, taken, sprite_guid, size, pos):
    """返回 True/False。按钮下名为 Icon 的节点：存在则改造，不存在则新建。

    新建的块必须同时进 bmap 和 blocks，否则 render 只写旧列表会把它们丢掉。
    """
    kids = children_of(button_rect)
    icon_rect = None
    for k in kids:
        child = bmap.get(k)
        if child is None:
            continue
        gob = bmap.get(go_of(child))
        if gob and block_field(gob, "m_Name") == "Icon":
            icon_rect = child
            break
    if icon_rect is not None:
        for c in components_of(bmap[go_of(icon_rect)]):
            cb = bmap.get(c)
            if cb and cb[0] == "114" and script_guid(cb) in (IMAGE_GUID, TEXT_GUID):
                if script_guid(cb) == TEXT_GUID:
                    cb[2] = make_image_block(cb[1], go_of(icon_rect), sprite_guid)[2]
                else:
                    set_field(cb, "m_Sprite",
                              "{fileID: %s, guid: %s, type: 3}" % (SPRITE_FILEID, sprite_guid))
                    set_field(cb, "m_PreserveAspect", "1")
                    set_field(cb, "m_RaycastTarget", "0")
                break
        else:
            fail("%s 的 Icon 节点上找不到 Image/Text" % rel)
            return False
        set_field(icon_rect, "m_AnchorMin", "{x: 0.5, y: 0.5}")
        set_field(icon_rect, "m_AnchorMax", "{x: 0.5, y: 0.5}")
        set_field(icon_rect, "m_AnchoredPosition", "{x: %s, y: %s}" % pos)
        set_field(icon_rect, "m_SizeDelta", "{x: %s, y: %s}" % size)
        set_field(icon_rect, "m_Pivot", "{x: 0.5, y: 0.5}")
        return True

    go_id = fresh_id(taken)
    rect_id = fresh_id(taken)
    canvas_id = fresh_id(taken)
    image_id = fresh_id(taken)
    bmap[go_id] = make_go_block(go_id, "Icon", [rect_id, canvas_id, image_id])
    bmap[rect_id] = make_rect_block(rect_id, go_id, button_rect[1], size, pos)
    bmap[canvas_id] = make_canvas_block(canvas_id, go_id)
    bmap[image_id] = make_image_block(image_id, go_id, sprite_guid)
    for new_id in (go_id, rect_id, canvas_id, image_id):
        blocks.append(bmap[new_id])
    if not add_child(button_rect, rect_id):
        fail("%s 按钮 %s 没有 m_Children 可挂 Icon" % (rel, button_rect[1]))
        return False
    return True


def handle_label(rel, bmap, button_rect, as_badge):
    kids = children_of(button_rect)
    for k in kids:
        child = bmap.get(k)
        if child is None:
            continue
        gob = bmap.get(go_of(child))
        if not gob or block_field(gob, "m_Name") != "Label":
            continue
        if not as_badge:
            set_field(gob, "m_IsActive", "0")
            return True
        rect = child
        set_field(rect, "m_AnchorMin", "{x: 1, y: 0}")
        set_field(rect, "m_AnchorMax", "{x: 1, y: 0}")
        set_field(rect, "m_AnchoredPosition", "{x: -6, y: 6}")
        set_field(rect, "m_SizeDelta", "{x: 46, y: 46}")
        set_field(rect, "m_Pivot", "{x: 1, y: 0}")
        for c in components_of(gob):
            cb = bmap.get(c)
            if cb and cb[0] == "114" and script_guid(cb) == TEXT_GUID:
                set_field(cb, "m_FontSize", "30")
                set_field(cb, "m_Text", "0")
                break
        return True
    fail("%s 按钮 %s 下没有 Label" % (rel, button_rect[1]))
    return False


def activate_badge(rel, bmap, button_rect):
    kids = children_of(button_rect)
    for k in kids:
        child = bmap.get(k)
        if child is None:
            continue
        gob = bmap.get(go_of(child))
        if not gob or block_field(gob, "m_Name") != "Badge":
            continue
        set_field(gob, "m_IsActive", "1")
        for sub in children_of(child):
            sub_child = bmap.get(sub)
            if sub_child is None:
                continue
            sub_go = bmap.get(go_of(sub_child))
            if sub_go:
                set_field(sub_go, "m_IsActive", "1")
        return True
    fail("%s 按钮 %s 下没有 Badge" % (rel, button_rect[1]))
    return False


def process(rel, apply_it):
    print("=== %s" % os.path.basename(rel))
    text = read(rel)
    header, blocks = split_blocks(text)
    bmap = {b[1]: b for b in blocks}
    taken = set(bmap)
    for name, png in CONFIG[rel]:
        button_go = None
        for b in blocks:
            if b[0] == "1" and block_field(b, "m_Name") == name:
                button_go = b
                break
        if button_go is None:
            fail("%s 找不到按钮 %s" % (rel, name))
            continue
        button_rect = None
        for c in components_of(button_go):
            cb = bmap.get(c)
            if cb and cb[0] == "224":
                button_rect = cb
                break
        if button_rect is None:
            fail("%s 按钮 %s 没有 RectTransform" % (rel, name))
            continue
        w, h = size_of(button_rect)
        side = int(round(h * 0.78)) if h > 20 else 104
        size = (side, side)
        sprite = guid_of(png)
        if sprite is None:
            continue
        print("   %-18s 按钮 %sx%s -> 图标 %s  %s" % (name, w, h, side, os.path.basename(png)))
        if not apply_it:
            continue
        if not ensure_icon(rel, bmap, blocks, button_rect, taken, sprite, size, (0, 0)):
            continue
        if not handle_label(rel, bmap, button_rect, rel in LABEL_BADGE):
            continue
        if rel in BADGE_ON:
            activate_badge(rel, bmap, button_rect)
    if not apply_it:
        return
    out = render(header, blocks)
    if not out.startswith("%YAML"):
        fail("%s 渲染结果丢了 YAML 头，拒绝写入" % rel)
        return
    missing = [k for k in bmap if k not in {b[1] for b in blocks}]
    if missing:
        fail("%s 有 %d 个块没进渲染列表: %s" % (rel, len(missing), missing[:3]))
        return
    os.makedirs(BACKUP, exist_ok=True)
    dst = os.path.join(BACKUP, os.path.basename(rel) + ".icons")
    if not os.path.exists(dst):
        shutil.copy2(os.path.join(ROOT, rel), dst)
    with open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="\n") as fp:
        fp.write(out)
    print("   已写入（块 %d -> %d），备份 %s" % (len(blocks), len(bmap), os.path.basename(dst)))


def main():
    apply_it = "--apply" in sys.argv
    for rel in CONFIG:
        process(rel, apply_it)
    print()
    if FAIL:
        print("失败 %d 项" % len(FAIL))
        sys.exit(1)
    print("全部通过")


if __name__ == "__main__":
    main()
