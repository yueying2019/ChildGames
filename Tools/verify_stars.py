"""星级评价改动的静态自检。

本机没有可用的 C# 编译器，所以做两件低成本但高价值的事：
1. 括号配平 —— 抓「方法尾巴变悬空语句」这类致命改动。
2. 交叉核对新增的成员引用 —— 调用到的名字必须在目标类型里真的定义过。
"""

import os
import re

ROOT = r"E:\UnityGame\SmallGames\Assets\Scripts"

CHANGED = [
    r"Data\StarCatalog.cs",
    r"Data\ProgressRepository.cs",
    r"Gameplay\GameSessionBase.cs",
    r"Gameplay\Match3\Match3Session.cs",
    r"Gameplay\Lianliankan\LianliankanSession.cs",
    r"Gameplay\Sheep\SheepSession.cs",
    r"Gameplay\Lianliankan\LianliankanGameView.cs",
    r"Gameplay\Sheep\SheepGameView.cs",
    r"UI\UIStyle.cs",
    r"UI\Panels\ResultPanel.cs",
    r"UI\Panels\LevelSelectPanel.cs",
    r"UI\LevelSelectCellView.cs",
]

# 类名 -> 该类型必须提供的成员
CONTRACTS = {
    "StarCatalog": ["FromRemaining", "FromCombo", "Remaining", "Label", "MaxStars"],
    "ProgressRepository": ["Stars", "RecordStars", "TotalStars"],
}

PROVIDERS = ["StarCatalog.cs", "ProgressRepository.cs"]


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


def all_sources():
    for dirpath, _dirs, files in os.walk(ROOT):
        for f in files:
            if f.endswith(".cs"):
                yield os.path.join(dirpath, f)


def main():
    problems = []

    for rel in CHANGED:
        path = os.path.join(ROOT, rel)
        if not os.path.isfile(path):
            problems.append("文件不存在: " + rel)
            continue
        with open(path, encoding="utf-8", errors="replace") as fp:
            report = brace_report(rel, fp.read())
        if report:
            problems.append(report)

    provider_text = ""
    for path in all_sources():
        if os.path.basename(path) in PROVIDERS:
            with open(path, encoding="utf-8", errors="replace") as fp:
                provider_text += fp.read() + "\n"

    defined = set(re.findall(r"\b(?:public|private|internal)\s+(?:static\s+)?"
                             r"(?:int|float|string|bool|void|readonly)?\s*"
                             r"(\w+)\s*(?:\(|=>|\{)", provider_text))
    defined |= set(re.findall(r"public\s+(?:const\s+)?\w+\s+(\w+)\s*=", provider_text))

    used = {}
    for path in all_sources():
        with open(path, encoding="utf-8", errors="replace") as fp:
            text = fp.read()
        for cls, members in CONTRACTS.items():
            for member in members:
                if re.search(r"\b%s\.%s\b" % (cls, member), text):
                    used.setdefault(cls, set()).add(member)

    for cls, members in CONTRACTS.items():
        for member in members:
            if member not in defined:
                problems.append("%s.%s 未找到定义" % (cls, member))

    print("=== 括号配平 ===")
    brace_only = [p for p in problems if not p.endswith("未找到定义") and not p.startswith("文件不存在")]
    if brace_only:
        for p in brace_only:
            print("  FAIL " + p)
    else:
        print("  OK   %d 个改动文件全部配平" % len(CHANGED))

    print("=== 成员存在性 ===")
    missing = [p for p in problems if p.endswith("未找到定义")]
    if missing:
        for p in missing:
            print("  FAIL " + p)
    else:
        total = sum(len(v) for v in CONTRACTS.values())
        print("  OK   %d 个契约成员全部有定义" % total)

    print("=== 实际调用到的成员 ===")
    for cls in CONTRACTS:
        got = sorted(used.get(cls, set()))
        print("  %s: %s" % (cls, ", ".join(got) if got else "(无)"))


if __name__ == "__main__":
    main()
