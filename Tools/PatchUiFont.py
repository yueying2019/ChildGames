# -*- coding: utf-8 -*-
"""把缺字从 Noto Sans SC 补进 Assets/Font/NotoSansSC-UI.otf。

不改 .meta，避免预制体丢掉字体引用。
完整字体缓存在 Tools/FontCache/，不进仓库。下载失败时，把 NotoSansSC-Regular.otf
或 .ttf 放进该目录再运行即可。

用法：
  python Tools/PatchUiFont.py --scan
  python Tools/PatchUiFont.py --text-file Tools/FontCache/input.txt
  python Tools/PatchUiFont.py --scan --check
"""
import os
import re
import shutil
import subprocess
import sys
import unicodedata
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_PATH = os.path.join(ROOT, "Assets", "Font", "NotoSansSC-UI.otf")
CACHE_DIR = os.path.join(ROOT, "Tools", "FontCache")
SOURCE_NAMES = ("NotoSansSC-Regular.otf", "NotoSansSC-Regular.ttf")
SOURCE_URL = (
    "https://fonts.gstatic.com/s/notosanssc/v41/"
    "k3kCo84MPvpLmixcA63oeAL7Iqp5IZJF9bmaG9_FnYw.ttf"
)
EXTS = {".prefab", ".json", ".cs", ".txt", ".asset", ".xml", ".uxml", ".csv"}
SKIP_DIRS = {"AutoStreamingData", "Library", "FontCache"}
ESCAPE = re.compile(r"\\u([0-9a-fA-F]{4})")


def is_visible(code):
    if code < 0x20 or code == 0xFEFF:
        return False
    ch = chr(code)
    if ch.isspace():
        return False
    return not unicodedata.category(ch).startswith("C")


def option(flag):
    args = sys.argv[1:]
    for index, arg in enumerate(args):
        if arg == flag and index + 1 < len(args):
            return args[index + 1]
        prefix = flag + "="
        if arg.startswith(prefix):
            return arg[len(prefix):]
    return None


def collect_text(path):
    return collect_string(open(path, "r", encoding="utf-8").read())


def collect_string(text):
    found = set()
    for ch in text:
        code = ord(ch)
        if is_visible(code):
            found.add(code)
    for match in ESCAPE.finditer(text):
        code = int(match.group(1), 16)
        if is_visible(code):
            found.add(code)
    return found


def collect_used():
    found = set()
    assets = os.path.join(ROOT, "Assets")
    for dirpath, dirnames, filenames in os.walk(assets):
        dirnames[:] = [name for name in dirnames if name not in SKIP_DIRS]
        for name in filenames:
            if os.path.splitext(name)[1].lower() not in EXTS:
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, "r", encoding="utf-8", errors="ignore").read()
            except OSError:
                continue
            found.update(collect_string(text))
    return found


def progress(message):
    print("PROGRESS " + message, flush=True)


def find_cached_source():
    for name in SOURCE_NAMES:
        path = os.path.join(CACHE_DIR, name)
        if os.path.isfile(path) and os.path.getsize(path) > 1000000:
            return path
    return None


def download_with_curl(partial):
    curl = shutil.which("curl") or shutil.which("curl.exe")
    if not curl:
        return False
    result = subprocess.run(
        [curl, "-L", "--ssl-no-revoke", "--fail", "--retry", "2", "--connect-timeout", "25", "-o", partial, SOURCE_URL],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    return result.returncode == 0 and os.path.isfile(partial)


def download_with_urllib(partial):
    request = urllib.request.Request(SOURCE_URL, headers={"User-Agent": "SmallGames-UiFontPatcher"})
    with urllib.request.urlopen(request, timeout=60) as response, open(partial, "wb") as handle:
        while True:
            chunk = response.read(256 * 1024)
            if not chunk:
                break
            handle.write(chunk)
    return True


def download_source():
    os.makedirs(CACHE_DIR, exist_ok=True)
    dest = os.path.join(CACHE_DIR, "NotoSansSC-Regular.ttf")
    partial = dest + ".part"
    if os.path.isfile(partial):
        os.remove(partial)
    progress("正在下载完整 Noto Sans SC")
    downloaded = download_with_curl(partial)
    if not downloaded:
        download_with_urllib(partial)
    if not os.path.isfile(partial) or os.path.getsize(partial) < 1000000:
        if os.path.isfile(partial):
            os.remove(partial)
        raise RuntimeError("下载到的字体太小，不是完整的 Noto Sans SC。")
    os.replace(partial, dest)
    return dest


def subset(source_path, wanted, dest):
    from fontTools.subset import Options, Subsetter
    from fontTools.ttLib import TTFont

    font = TTFont(source_path)
    options = Options()
    options.layout_features = ["*"]
    options.glyph_names = True
    options.symbol_cmap = True
    options.legacy_cmap = True
    options.notdef_glyph = True
    options.notdef_outline = True
    options.recommended_glyphs = True
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.hinting = True
    subsetter = Subsetter(options=options)
    subsetter.populate(unicodes=wanted)
    subsetter.subset(font)
    font.save(dest)


def report(added_codes, already_codes, uncovered):
    print("ADDED " + str(len(added_codes)))
    print("CHARS " + "".join(chr(code) for code in added_codes))
    print("ALREADY " + "".join(chr(code) for code in already_codes))
    print("UNCOVERED " + "".join(chr(code) for code in uncovered))
    print("BYTES " + str(os.path.getsize(FONT_PATH)))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    check_only = "--check" in sys.argv
    scan = "--scan" in sys.argv
    text_file = option("--text-file")
    if not os.path.isfile(FONT_PATH):
        print("ERROR 找不到 " + FONT_PATH, file=sys.stderr)
        return 1
    if not scan and not text_file:
        print("ERROR 请用 --scan 扫描界面，或用 --text-file 传入要补的文字。", file=sys.stderr)
        return 1
    if text_file and not os.path.isfile(text_file):
        print("ERROR 找不到文字文件 " + text_file, file=sys.stderr)
        return 1

    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        print("ERROR 缺少 fontTools。请先运行：python -m pip install fonttools", file=sys.stderr)
        return 1

    progress("正在扫描界面文字" if scan else "正在读取要补的文字")
    current = TTFont(FONT_PATH)
    have = set(current.getBestCmap().keys())
    used = collect_used() if scan else collect_text(text_file)
    if not used:
        print("ERROR 没有可补的字。", file=sys.stderr)
        return 1

    already_codes = [] if scan else sorted(code for code in used if code in have)
    missing = [code for code in used if code not in have]
    if not missing:
        report([], already_codes, [])
        return 0

    source_path = find_cached_source()
    if source_path is None and not check_only:
        try:
            source_path = download_source()
        except Exception as error:
            print("ERROR 下载完整字体失败：" + str(error), file=sys.stderr)
            print("ERROR 也可把 NotoSansSC-Regular.otf 放到 Tools/FontCache/ 后再运行。", file=sys.stderr)
            return 1
    if source_path is None:
        print("ERROR 还没有完整字体，无法判断缺字。", file=sys.stderr)
        return 1

    source = TTFont(source_path)
    source_cmap = source.getBestCmap()
    added_codes = sorted(code for code in missing if code in source_cmap)
    uncovered = sorted(code for code in missing if code not in source_cmap)
    if check_only or not added_codes:
        report(added_codes, already_codes, uncovered)
        return 0

    progress("正在写入字体，共 " + str(len(added_codes)) + " 个新字")
    wanted = have | set(added_codes)
    temp = FONT_PATH + ".patching"
    subset(source_path, wanted, temp)
    patched = TTFont(temp)
    patched_cmap = patched.getBestCmap()
    for code in added_codes:
        if code not in patched_cmap:
            os.remove(temp)
            print("ERROR 写入后仍缺少 " + chr(code), file=sys.stderr)
            return 1
    os.replace(temp, FONT_PATH)
    report(added_codes, already_codes, uncovered)
    return 0


if __name__ == "__main__":
    sys.exit(main())
