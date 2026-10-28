"""本轮「两个玩法新道具 + 三消限时」改动的自检。

本机没有 C# 编译器，用静态核对代替编译：
  1. 括号配平（防 CS1519 / CS1022 那种"方法尾巴变悬空语句"）；
  2. 契约核对：一处调用的符号，必须在提供方文件里真的存在（防臆造 API）；
  3. 预制体核对：悬空 fileID、新按钮节点、脚本字段回填、Label 文案、Badge 隐藏。

用法：
    python Tools/verify_new_tools.py
"""

import os
import re
import sys

ROOT = r"E:\UnityGame\SmallGames"
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts")

CS_FILES = [
    r"Data\ShopCatalog.cs",
    r"UI\ShopItemView.cs",
    r"UI\Panels\ShopPanel.cs",
    r"Editor\ShopIconBinder.cs",
    r"Data\CoinWallet.cs",
    r"Data\ContinueCatalog.cs",
    r"Gameplay\Match3\Match3Engine.cs",
    r"Gameplay\Match3\Match3LevelData.cs",
    r"Gameplay\Match3\Match3GameView.cs",
    r"Gameplay\Match3\Match3Session.cs",
    r"Gameplay\Lianliankan\LianliankanLevelData.cs",
    r"Gameplay\Lianliankan\LianliankanGameView.cs",
    r"Editor\LevelAddressableSync.cs",
]

# (调用方, 被调用符号, 提供方, 提供方必须出现的定义正则)
CONTRACTS = [
    (r"Gameplay\Match3\Match3GameView.cs", "_engine.TryClearColorAt(",
     r"Gameplay\Match3\Match3Engine.cs", r"public Match3ResolveResult TryClearColorAt\(int x, int y\)"),
    (r"Gameplay\Match3\Match3GameView.cs", "Match3Special.ColorBomb, ShopItemId.Match3ColorBomb",
     r"Data\ShopCatalog.cs", r"Match3ColorBomb = 17"),
    (r"Gameplay\Match3\Match3GameView.cs", "ShopItemId.Match3ColorClear",
     r"Data\ShopCatalog.cs", r"Match3ColorClear = 18"),
    (r"Gameplay\Match3\Match3Session.cs", "_view.BeginTimer(",
     r"Gameplay\Match3\Match3GameView.cs", r"public void BeginTimer\(float seconds\)"),
    (r"Gameplay\Match3\Match3Session.cs", "_view.AddTimerSeconds(",
     r"Gameplay\Match3\Match3GameView.cs", r"public void AddTimerSeconds\(float extra\)"),
    (r"Gameplay\Match3\Match3Session.cs", "_view.SetTimerRunning(",
     r"Gameplay\Match3\Match3GameView.cs", r"public void SetTimerRunning\(bool running\)"),
    (r"Gameplay\Match3\Match3Session.cs", "_view.TimedOut",
     r"Gameplay\Match3\Match3GameView.cs", r"public bool TimedOut"),
    (r"Gameplay\Match3\Match3Session.cs", "_view.TimeLeft",
     r"Gameplay\Match3\Match3GameView.cs", r"public float TimeLeft"),
    (r"Gameplay\Match3\Match3Session.cs", "_level.TimeLimit",
     r"Gameplay\Match3\Match3LevelData.cs", r"public float TimeLimit"),
    (r"Gameplay\Match3\Match3Session.cs", "ContinueCatalog.Match3BonusSeconds",
     r"Data\ContinueCatalog.cs", r"public const int Match3BonusSeconds"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "ShopCatalog.LianBonusSeconds",
     r"Data\ShopCatalog.cs", r"public const int LianBonusSeconds"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "ShopCatalog.LianFreezeSeconds",
     r"Data\ShopCatalog.cs", r"public const int LianFreezeSeconds"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "config.ComboTimeBonus",
     r"Gameplay\Lianliankan\LianliankanLevelData.cs", r"public float ComboTimeBonus"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "AddTimerSeconds(_comboTimeBonus, _timeLimit)",
     r"Gameplay\Lianliankan\LianliankanGameView.cs", r"public void AddTimerSeconds\(float extra, float cap = 0f\)"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "CancelInvoke(nameof(EndFreeze))",
     r"Gameplay\Lianliankan\LianliankanGameView.cs", r"private void EndFreeze\(\)"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "Invoke(nameof(EndFreeze)",
     r"Gameplay\Lianliankan\LianliankanGameView.cs", r"private void EndFreeze\(\)"),
    (r"Gameplay\Lianliankan\LianliankanGameView.cs", "StartCoroutine(PlayLink(a, b))",
     r"Gameplay\Lianliankan\LianliankanGameView.cs", r"private IEnumerator PlayLink\(LianliankanTileView a, LianliankanTileView b\)"),
    (r"UI\Panels\ShopPanel.cs", "row.Bind(info, IconOf(info.Id))",
     r"UI\ShopItemView.cs", r"public void Bind\(ShopItemInfo info, Sprite icon\)"),
    (r"UI\Panels\ShopPanel.cs", "view.Bind(info, IconOf(info.Id))",
     r"UI\ShopItemView.cs", r"public void Bind\(ShopItemInfo info, Sprite icon\)"),
    (r"Editor\ShopIconBinder.cs", 'so.FindProperty("Icons")',
     r"UI\Panels\ShopPanel.cs", r"public ShopIconEntry\[\] Icons;"),
    (r"Editor\ShopIconBinder.cs", 'so.FindProperty("IconImage")',
     r"UI\ShopItemView.cs", r"public Image IconImage;"),
    (r"Editor\ShopIconBinder.cs", "ShopCatalog.All",
     r"Data\ShopCatalog.cs", r"public static readonly ShopItemInfo\[\] All"),
]

# 钱包四处一致性
WALLET_FIELDS = ["LianTime", "LianFreeze", "LianAuto", "Match3ColorBomb", "Match3ColorClear"]

# 按钮图标：按钮名 -> 图标 png 相对路径（文字标签已全部改成图标 + 数量角标）
ICON_CHECKS = [
    (r"Assets\Content\Lianliankan\LianliankanGameView.prefab", [
        ("ShuffleButton", r"Assets\UI\Lianliankan\LianShuffle.png"),
        ("HintButton", r"Assets\UI\Lianliankan\LianHint.png"),
        ("TimeButton", r"Assets\UI\Lianliankan\LianTime.png"),
        ("FreezeButton", r"Assets\UI\Lianliankan\LianFreeze.png"),
        ("AutoButton", r"Assets\UI\Lianliankan\LianAuto.png"),
    ]),
    (r"Assets\Content\Match3\Match3GameView.prefab", [
        ("HintButton", r"Assets\UI\Match3\Match3Hint.png"),
        ("ShuffleButton", r"Assets\UI\Match3\Match3Shuffle.png"),
        ("HammerButton", r"Assets\UI\Match3\Match3Hammer.png"),
        ("LineHButton", r"Assets\UI\Match3\Match3LineH.png"),
        ("LineVButton", r"Assets\UI\Match3\Match3LineV.png"),
        ("BombButton", r"Assets\UI\Match3\Match3Bomb.png"),
        ("MovesButton", r"Assets\UI\Match3\Match3Moves.png"),
        ("ColorBombButton", r"Assets\UI\Match3\Match3ColorBomb.png"),
        ("ColorClearButton", r"Assets\UI\Match3\Match3ColorClear.png"),
    ]),
    (r"Assets\Content\Sheep\SheepGameView.prefab", [
        ("RemoveButton", r"Assets\UI\Sheep\SheepRemove.png"),
        ("UndoButton", r"Assets\UI\Sheep\SheepUndo.png"),
        ("ShuffleButton", r"Assets\UI\Sheep\SheepShuffle.png"),
        ("SlotButton", r"Assets\UI\Sheep\SheepSlot.png"),
    ]),
]

PREFAB_CHECKS = [
    {
        "path": r"Assets\Content\Lianliankan\LianliankanGameView.prefab",
        "names": ["TimeButton", "FreezeButton", "AutoButton"],
        "fields": ["TimeButton", "FreezeButton", "AutoButton"],
        "labels": [],
    },
    {
        "path": r"Assets\Content\Match3\Match3GameView.prefab",
        "names": ["ColorBombButton", "ColorClearButton"],
        "fields": ["ColorBombButton", "ColorClearButton"],
        "labels": [],
    },
]

BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
# Item.prefab（Assets/Content/UI/Item.prefab）在引用里用的 32 位 guid
ITEM_PREFAB_GUID = "b5a99db6c5c80764d8d31ce8758939b1"
ITEM_ROOT_FID = "5934839101267530661"   # Item 根 RectTransform
ITEM_ICON_FID = "5572156852717320087"   # Item 里名为 Icon 的 Image

# 注意：嵌套预制体实例里的对象会被写成 `--- !u!224 &123 stripped`，只保留三行来源信息，
# 没有 m_GameObject / m_Children 等字段。块切分必须认这类块，否则全部被误判成悬空引用。
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$")
# 预制体内部引用：{fileID: N}
REF_LOCAL = re.compile(r"\{fileID: (-?\d+)\}")
# 外部资源引用：{fileID: N, guid: xxx, type: n}，只有 guid 全 0 才是真丢失
REF_EXTERNAL = re.compile(r"\{fileID: (-?\d+), guid: ([0-9a-fA-F]{32}), type: \d+\}")

errors = []


def fail(msg):
    errors.append(msg)
    print("FAIL %s" % msg)


def read(rel):
    path = rel if rel.startswith("Assets") else os.path.join("Assets", "Scripts", rel)
    with open(os.path.join(ROOT, path), encoding="utf-8", errors="replace") as fp:
        return fp.read()


def strip_strings(text):
    text = re.sub(r'"(?:\\.|[^"\\])*"', '""', text)
    text = re.sub(r"//[^\n]*", "", text)
    return text


def check_balance(rel):
    text = strip_strings(read(rel))
    pairs = {"(": ")", "{": "}", "[": "]"}
    stack = []
    for ch in text:
        if ch in pairs:
            stack.append(ch)
        elif ch in pairs.values():
            if not stack or pairs[stack.pop()] != ch:
                fail("%s 括号不配平" % rel)
                return
    if stack:
        fail("%s 括号不配平，剩余 %d 个未闭合" % (rel, len(stack)))
        return
    print("OK   配平 %s" % rel)


def check_contracts():
    cache = {}
    for caller, symbol, provider, pattern in CONTRACTS:
        if caller not in cache:
            cache[caller] = read(caller)
        if provider not in cache:
            cache[provider] = read(provider)
        if symbol not in cache[caller]:
            fail("契约：%s 里找不到调用 %s" % (caller, symbol))
        elif not re.search(pattern, cache[provider]):
            fail("契约：%s 缺少定义 %s" % (provider, pattern))
        else:
            print("OK   契约 %s -> %s" % (os.path.basename(provider), symbol.strip()))


def check_wallet():
    text = read(r"Data\CoinWallet.cs")
    for field in WALLET_FIELDS:
        declared = re.search(r"public int %s;" % field, text)
        counted = re.search(r"case ShopItemId\.%s: return Current\.%s;" % (field, field), text)
        added = re.search(r"case ShopItemId\.%s: Current\.%s = " % (field, field), text)
        clamped = re.search(r"Current\.%s = Mathf\.Max\(0, Current\.%s\);" % (field, field), text)
        for label, hit in (("字段", declared), ("Count", counted), ("AddItem", added), ("Clamp", clamped)):
            if not hit:
                fail("钱包缺 %s 的 %s" % (field, label))
    if not errors:
        print("OK   钱包五道具四处齐全")


def split_blocks(text):
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if HEADER.match(line)]
    blocks = []
    for k, start in enumerate(starts):
        end = starts[k + 1] if k + 1 < len(starts) else len(lines)
        m = HEADER.match(lines[start])
        blocks.append([m.group(1), m.group(2), lines[start:end]])
    return blocks


def check_prefab(spec):
    rel = spec["path"]
    text = read(rel)
    # Unity/团结引擎 YAML 必须保留 %YAML 1.1 + %TAG !u! 两行指令，否则 !u! 标签
    # 无法解析 -> 资源导入失败 -> AssetDatabase 取值为 null（曾导致 GameView 找不到）。
    if not text.startswith("%YAML"):
        fail("%s 缺少 YAML 头部指令（首行 %r）" % (rel, text.split("\n")[0][:40]))
    elif "%TAG !u!" not in text.split("\n")[1]:
        fail("%s 缺少 %TAG !u! 声明" % rel)
    # Unity 的 fileID 是 SInt64，越界值会被截断成同一个 long.MaxValue，导致
    # Duplicate identifier → BrokenPrefabAsset。
    overflow = sorted({int(v) for v in re.findall(r"fileID: (-?\d+)", text) if int(v) > (1 << 63) - 1})
    if overflow:
        fail("%s 有超出 Int64 的 fileID：%s" % (rel, overflow[:3]))
    blocks = split_blocks(text)
    bmap = {b[1]: b for b in blocks}
    body_of = {b[1]: "\n".join(b[2]) for b in blocks}

    dangling = 0
    for fid, text in body_of.items():
        for m in REF_LOCAL.finditer(text):
            ref = m.group(1)
            if ref == "0" or ref == fid:
                continue
            if ref not in bmap:
                dangling += 1
                if dangling <= 3:
                    fail("%s 悬空引用 %s（在 %s 里）" % (rel, ref, fid))
        for m in REF_EXTERNAL.finditer(text):
            if m.group(1) == "0" or set(m.group(2)) != {"0"}:
                continue
            dangling += 1
            if dangling <= 3:
                fail("%s 引用丢失资源（在 %s 里）" % (rel, fid))
    print("OK   %s 悬空/丢失引用 %d" % (os.path.basename(rel), dangling))

    names = {}
    for kind, fid, body in blocks:
        if kind != "1":
            continue
        for line in body:
            if line.startswith("  m_Name: "):
                names[line[len("  m_Name: "):]] = fid
    for name in spec["names"]:
        if name not in names:
            fail("%s 缺少节点 %s" % (rel, name))
    print("OK   %s 节点 %s" % (os.path.basename(rel), ",".join(spec["names"])))

    for field in spec["fields"]:
        m = re.search(r"^\s*%s: \{fileID: (-?\d+)\}" % field, read(rel), re.M)
        if not m:
            fail("%s 脚本字段 %s 没回填" % (rel, field))
            continue
        target = m.group(1)
        if target not in bmap or bmap[target][0] != "114" or BUTTON_GUID not in body_of[target]:
            fail("%s 字段 %s 指向的不是 Button" % (rel, field))
    print("OK   %s 字段回填 %s" % (os.path.basename(rel), ",".join(spec["fields"])))

    for label in spec["labels"]:
        if 'm_Text: "%s"' % label not in read(rel):
            fail("%s 缺少文案 %s" % (rel, label))
    print("OK   %s 文案 %d 条" % (os.path.basename(rel), len(spec["labels"])))


def guid_of_png(rel):
    with open(os.path.join(ROOT, rel + ".meta"), encoding="utf-8", errors="replace") as fp:
        m = re.search(r"^guid: (\S+)", fp.read(), re.M)
    return m.group(1) if m else None


def block_field(block, key):
    for line in block[2]:
        m = re.match(r"^\s+%s: (.*)$" % key, line)
        if m:
            return m.group(1)
    return None


def check_icons():
    """按钮里挂的是 Item 预制体实例，图标靠 PrefabInstance 的 m_Sprite 覆盖写到实例的 Icon 上。

    实例内部对象在 YAML 里是 stripped 块（只有来源信息），所以只能从 1001 块读覆盖值。
    """
    for rel, pairs in ICON_CHECKS:
        blocks = split_blocks(read(rel))
        bmap = {b[1]: b for b in blocks}
        names = {}
        for b in blocks:
            if b[0] == "1":
                names[b[1]] = block_field(b, "m_Name")
        # RectTransform -> 所属节点名
        rect_name = {}
        for b in blocks:
            if b[0] == "224":
                rect_name[b[1]] = names.get(go_of(b), "?")
        # 父 RectTransform -> 该实例对 Icon 的 sprite 覆盖
        instance_sprite = {}
        for b in blocks:
            if b[0] != "1001":
                continue
            body = "\n".join(b[2])
            if "guid: %s" % ITEM_PREFAB_GUID not in body:
                continue
            parent = re.search(r"m_TransformParent: \{fileID: (-?\d+)\}", body)
            if not parent:
                continue
            mods = re.findall(
                r"- target: \{fileID: %s, guid: %s, type: 3\}\n\s*propertyPath: m_Sprite\n\s*value: \S*\n\s*objectReference: \{fileID: (-?\d+), guid: (\S+), type: 3\}"
                % (ITEM_ICON_FID, ITEM_PREFAB_GUID), body)
            instance_sprite.setdefault(parent.group(1), []).extend(mods)

        ok = 0
        for name, png in pairs:
            rect = None
            for fid, label in rect_name.items():
                if label == name:
                    rect = fid
                    break
            if rect is None:
                fail("%s 找不到按钮 %s" % (rel, name))
                continue
            mods = instance_sprite.get(rect, [])
            if len(mods) != 1:
                fail("%s 按钮 %s 下挂的 Item 实例有 %d 条 Icon 覆盖（应为 1）" % (rel, name, len(mods)))
                continue
            ref_id, guid = mods[0]
            if ref_id != "21300000" or set(guid) == {"0"}:
                fail("%s 按钮 %s 的 Icon 覆盖指向空资源" % (rel, name))
                continue
            if not os.path.exists(os.path.join(ROOT, png)):
                fail("图标文件不存在: %s" % png)
                continue
            ok += 1
        print("OK   %s 图标实例 %d/%d" % (os.path.basename(rel), ok, len(pairs)))


def check_shop_icon_files():
    """编辑器脚本按 Assets/UI/<玩法>/<ShopItemId>.png 找图，这里核对每个道具都有对应文件。"""
    text = read(r"Data\ShopCatalog.cs")
    entries = re.findall(r"new ShopItemInfo\(ShopItemId\.(\w+), GameModeType\.(\w+),", text)
    if not entries:
        fail("ShopCatalog 里解析不出道具条目")
        return
    missing = []
    for item_id, mode in entries:
        path = os.path.join(ROOT, "Assets", "UI", mode, item_id + ".png")
        if not os.path.exists(path):
            missing.append(os.path.join("Assets", "UI", mode, item_id + ".png"))
    if missing:
        for m in missing:
            fail("商店图标缺文件: %s" % m)
        return
    print("OK   商店图标文件 %d 个齐全" % len(entries))


def go_of(block):
    m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", "\n".join(block[2]))
    return m.group(1) if m else ""


def main():
    for rel in CS_FILES:
        check_balance(rel)
    check_contracts()
    check_wallet()
    for spec in PREFAB_CHECKS:
        check_prefab(spec)
    check_icons()
    check_shop_icon_files()

    print()
    if errors:
        print("共 %d 项失败" % len(errors))
        sys.exit(1)
    print("全部通过")


if __name__ == "__main__":
    main()
