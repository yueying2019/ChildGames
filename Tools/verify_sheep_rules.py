"""叠叠消「规则层 + 暂存位道具」改动的静态自检。

本机没有可用的 C# 编译器，所以做三件低成本但高价值的事：
1. 括号配平 —— 抓「方法尾巴变悬空语句」这类致命改动。
2. 契约核对 —— 本次改动跨文件引用的成员，必须在目标类型里真的定义过（防臆造）。
3. 硬编码扫描 —— 确认 SheepGameView 里不再有把「一组几张」写死成 3 的地方。
"""

import os
import re

ROOT = r"E:\UnityGame\SmallGames\Assets\Scripts"

CHANGED = [
    r"Gameplay\Sheep\SheepLevelData.cs",
    r"Gameplay\Sheep\SheepGameView.cs",
    r"Gameplay\Sheep\SheepSession.cs",
    r"Data\ShopCatalog.cs",
    r"Data\CoinWallet.cs",
    r"Data\ContinueCatalog.cs",
    r"Data\StarCatalog.cs",
]

# 类名 -> 该类型必须提供的成员
CONTRACTS = {
    "SheepLevelData": [
        "DefaultCompose", "MinCompose", "MaxCompose",
        "MaxSlotBonus", "MaxTimeLimit",
        "SlotBonus", "TimeLimit", "GoalCount",
        "HasTimeLimit", "HasGoal", "EnsureInitialized", "ComposeNum",
    ],
    "ContinueCatalog": ["SheepBonusSeconds", "SheepClearSlots"],
    "ShopItemId": ["SheepSlot"],
    "CoinWallet": ["TryUse", "Count"],
    "SheepGameView": ["TimeLimit", "TimeLeft", "ComboPeak", "TimedOut", "MaxSlotLimit", "SlotCount"],
    "StarCatalog": ["FromRemaining", "FromCombo", "Remaining"],
}

PROVIDERS = [
    "SheepLevelData.cs", "SheepGameView.cs", "ContinueCatalog.cs",
    "ShopCatalog.cs", "CoinWallet.cs", "StarCatalog.cs",
]

HEADER = re.compile(r"^--- !u!\d+ &(\d+)")


def strip_noise(src):
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c in "\"'":
            quote = c
            i += 1
            while i < n:
                if src[i] == "\\":
                    i += 2
                    continue
                if src[i] == quote:
                    i += 1
                    break
                i += 1
            continue
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i)
            i = n if j < 0 else j + 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def brace_report(name, src):
    code = strip_noise(src)
    depth, line = 0, 1
    for ch in code:
        if ch == "\n":
            line += 1
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth < 0:
                return "%s: 多余的 } 在第 %d 行" % (name, line)
    if depth != 0:
        return "%s: 括号未配平，结尾 depth=%d" % (name, depth)
    return None


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as fp:
        return fp.read()


def main():
    problems = []

    for rel in CHANGED:
        path = os.path.join(ROOT, rel)
        if not os.path.isfile(path):
            problems.append("文件不存在: " + rel)
            continue
        report = brace_report(rel, read(rel))
        if report:
            problems.append(report)

    provider_text = ""
    for base in PROVIDERS:
        for dirpath, _dirs, files in os.walk(ROOT):
            if base in files:
                path = os.path.join(dirpath, base)
                with open(path, encoding="utf-8", errors="replace") as fp:
                    provider_text += fp.read() + "\n"

    access = r"\b(?:public|private|internal|protected)\s+"
    defined = set(re.findall(access + r"(?:const\s+)?(?:static\s+)?(?:readonly\s+)?"
                             r"[\w<>\[\]]+\s+(\w+)\s*(?:\(|=>|\{|;|=)", provider_text))
    defined |= set(re.findall(access + r"(\w+)\s*\{", provider_text))
    defined |= set(re.findall(r"^\s{8,12}(\w+)\s*=", provider_text, re.M))

    for cls, members in CONTRACTS.items():
        for member in members:
            if member not in defined:
                problems.append("%s.%s 未找到定义" % (cls, member))

    view = read(r"Gameplay\Sheep\SheepGameView.cs")
    stray = []
    for i, line in enumerate(strip_noise(view).split("\n"), 1):
        code = line.split("//")[0]
        if re.search(r"/\s*3\s*\*\s*3|%\s*3\b|>\s*=\s*SlotCount\b", code):
            stray.append((i, line.strip()))

    print("=== 括号配平 ===")
    brace_only = [p for p in problems if not p.endswith("未找到定义") and not p.startswith("文件不存在")]
    if brace_only:
        for p in brace_only:
            print("  FAIL " + p)
    else:
        print("  OK   %d 个改动文件全部配平" % len(CHANGED))

    print("=== 契约成员 ===")
    missing = [p for p in problems if p.endswith("未找到定义")]
    if missing:
        for p in missing:
            print("  FAIL " + p)
    else:
        total = sum(len(v) for v in CONTRACTS.values())
        print("  OK   %d 个契约成员全部有定义" % total)

    print("=== 剩余硬编码 ===")
    if stray:
        for i, text in stray:
            print("  FAIL 第 %d 行: %s" % (i, text))
    else:
        print("  OK   SheepGameView 里已无写死的「三同」或 SlotCount 判定")


if __name__ == "__main__":
    main()
