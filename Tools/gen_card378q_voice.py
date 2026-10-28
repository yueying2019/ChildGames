import asyncio
import os
import sys
import edge_tts

MALE = (os.environ.get("CARD378Q_VOICE_GENDER") or (sys.argv[1] if len(sys.argv) > 1 else "female")).lower() == "male"
OUT = r"E:\UnityGame\SmallGames\Assets\Resources\Sounds\Card378Q\VoiceMale" if MALE else r"E:\UnityGame\SmallGames\Assets\Resources\Sounds\Card378Q\Voice"
VOICE = "zh-CN-YunxiNeural" if MALE else "zh-CN-XiaoyiNeural"
RATE = "+18%"

RANKS = [
    ("3", "三"),
    ("4", "四"),
    ("5", "五"),
    ("6", "六"),
    ("7", "七"),
    ("8", "八"),
    ("9", "九"),
    ("10", "十"),
    ("j", "勾"),
    ("q", "圈"),
    ("k", "K"),
    ("a", "尖"),
    ("2", "二"),
]

LINES = {
    "buyao": "不要！",
    "yaobuqi": "要不起！",
    "guo": "过！",
    "guanshang": "管上！",
    "pair": "对子！",
    "bomb": "炸弹！",
    "combo": "三七八蛋！",
    "sisters": "连对！",
    "fullhouse": "葫芦！",
    "single_sj": "小王！",
    "single_bj": "大王！",
}

for key, spoken in RANKS:
    LINES[f"single_{key}"] = f"{spoken}！"
    LINES[f"pair_{key}"] = f"对{spoken}！"
    LINES[f"triple_{key}"] = f"三个{spoken}！"


async def synth(name, text):
    path = os.path.join(OUT, name + ".mp3")
    last = None
    for attempt in range(4):
        try:
            communicate = edge_tts.Communicate(text, VOICE, rate=RATE)
            await communicate.save(path)
            size = os.path.getsize(path)
            print(f"ok {name} ({size} bytes) {text}")
            return
        except Exception as ex:
            last = ex
            print(f"retry {name}: {ex}")
            await asyncio.sleep(1.2 * (attempt + 1))
    raise last


async def main():
    os.makedirs(OUT, exist_ok=True)
    for name, text in LINES.items():
        await synth(name, text)
    print(f"done {len(LINES)} clips -> {OUT}")


if __name__ == "__main__":
    asyncio.run(main())
