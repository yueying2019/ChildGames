"""生成六个新关卡 .asset：对对消迷宫关/形状关/闪电关，三消形状关/果冻关/收集关。

只写 .asset，不写 .meta —— guid 交给 Unity 导入时生成（猜错的 guid 会让资源打不开）。
登记 Addressables 用编辑器菜单「SmallGames/同步关卡到 Addressables」，
关卡在 mode_* 远程组里，登记完能随热更下发，不用重新提审。

用法：
    python Tools/add_new_levels.py
"""

import os
import struct

ROOT = r"E:\UnityGame\SmallGames"

LIAN_SCRIPT_GUID = "35a3d9af43cea8a428fdecee7f92dd36"
MATCH3_SCRIPT_GUID = "1d8bee4a639d7b042a261eb1876799e5"

EMPTY, TILE, BLOCKER = 0, 1, 2


def esc(text):
    return "".join("\\u%04X" % ord(ch) for ch in text)


def hex_bytes(values):
    return "".join("%02x" % (v & 0xFF) for v in values)


def hex_ints(values):
    return struct.pack("<%di" % len(values), *values).hex()


def blank(rows=10, cols=10):
    return [[EMPTY] * cols for _ in range(rows)]


def write(path, text):
    full = os.path.join(ROOT, path)
    with open(full, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(text)
    print("写入 %s" % path)


def lian_asset(name, level_id, title, cells, type_count, time_limit, combo_bonus):
    flat = [v for row in cells for v in row]
    occupied = [1 if v == TILE else 0 for v in flat]
    return f"""%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {LIAN_SCRIPT_GUID}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
  LevelId: {level_id}
  Title: "{esc(title)}"
  Layout: 8
  TypeCount: {type_count}
  TimeLimit: {time_limit:g}
  ComboTimeBonus: {combo_bonus:g}
  Cells: {hex_bytes(flat)}
  Occupied: {hex_bytes(occupied)}
"""


def match3_asset(name, level_id, title, cells, color_count, move_limit, goal_type,
                 target_score, collect, time_limit=0):
    lines = []
    for row in cells:
        for kind, color, layers in row:
            lines.append("  - Kind: %d\n    Color: %d\n    Layers: %d" % (kind, color, layers))
    return f"""%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MATCH3_SCRIPT_GUID}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
  LevelId: {level_id}
  DisplayName: "{esc(title)}"
  Width: 8
  Height: 8
  ColorCount: {color_count}
  MoveLimit: {move_limit}
  TimeLimit: {time_limit:g}
  GoalType: {goal_type}
  TargetScore: {target_score}
  CollectCounts: {hex_ints(collect)}
  Cells:
""" + "\n".join(lines) + "\n"


def build_lianliankan():
    # 21 石墙迷宫：8x8 满盘，中间竖墙 col 4 断成两段，逼玩家绕行
    maze = blank()
    for r in range(1, 9):
        for c in range(1, 9):
            maze[r][c] = TILE
    for r in (2, 3, 5, 6):
        maze[r][4] = BLOCKER
    tiles = sum(1 for row in maze for v in row if v == TILE)
    print("  石墙迷宫 tile 数 %d" % tiles)
    write(r"Assets\Content\Lianliankan\Levels\Level_21.asset",
          lian_asset("Level_21", 21, "石墙迷宫", maze, 8, 150, 0))

    # 22 十字花园：手绘十字形状，示范连锁回时
    cross = blank()
    for r in range(1, 9):
        for c in (4, 5):
            cross[r][c] = TILE
    for c in range(1, 9):
        for r in (4, 5):
            cross[r][c] = TILE
    tiles = sum(1 for row in cross for v in row if v == TILE)
    print("  十字花园 tile 数 %d" % tiles)
    write(r"Assets\Content\Lianliankan\Levels\Level_22.asset",
          lian_asset("Level_22", 22, "十字花园", cross, 6, 120, 1))

    # 23 极速果园：入门环 + 3 种图案 + 60 秒
    ring = blank()
    for r in range(1, 7):
        for c in range(2, 8):
            if r in (3, 4) and c in (4, 5):
                continue
            ring[r][c] = TILE
    tiles = sum(1 for row in ring for v in row if v == TILE)
    print("  极速果园 tile 数 %d" % tiles)
    write(r"Assets\Content\Lianliankan\Levels\Level_23.asset",
          lian_asset("Level_23", 23, "极速果园", ring, 3, 60, 1.5))


def build_match3():
    def cell(kind=1, color=0, layers=0):
        return (kind, color, layers)

    # 21 菱形甜点：曼哈顿距离 <= 4 的格子保留，四角挖空成菱形（40 格）
    diamond = []
    for r in range(8):
        row = []
        for c in range(8):
            keep = abs(r - 3.5) + abs(c - 3.5) <= 4
            row.append(cell(1) if keep else cell(0))
        diamond.append(row)
    print("  菱形甜点可玩格 %d" % sum(1 for row in diamond for k, _, _ in row if k == 1))
    write(r"Assets\Content\Match3\Levels\Level_21.asset",
          match3_asset("Level_21", 21, "菱形甜点", diamond, 5, 26, 0, 1800, [0] * 7))

    # 22 果冻工坊：中央 4x4 铺 2 层果冻，目标是清覆盖层
    jelly = [[cell(1) for _ in range(8)] for _ in range(8)]
    for r in range(2, 6):
        for c in range(2, 6):
            jelly[r][c] = cell(4, 0, 2)
    write(r"Assets\Content\Match3\Levels\Level_22.asset",
          match3_asset("Level_22", 22, "果冻工坊", jelly, 5, 30, 2, 0, [0] * 7))

    # 23 彩虹订单：收集 20/15/10 个指定颜色
    order = [[cell(1) for _ in range(8)] for _ in range(8)]
    collect = [0, 20, 15, 10, 0, 0, 0]
    write(r"Assets\Content\Match3\Levels\Level_23.asset",
          match3_asset("Level_23", 23, "彩虹订单", order, 5, 32, 1, 0, collect))

    # 24 限时甜点：同样 8x8，加 90 秒倒计时，示范新的 TimeLimit 字段
    write(r"Assets\Content\Match3\Levels\Level_24.asset",
          match3_asset("Level_24", 24, "限时甜点", [[cell(1) for _ in range(8)] for _ in range(8)],
                       5, 40, 0, 2400, [0] * 7, 90))


def main():
    print("对对消：")
    build_lianliankan()
    print("三消：")
    build_match3()
    print()
    print("下一步：在 Unity 里跑菜单 SmallGames/同步关卡到 Addressables，")
    print("      再点 SmallGames/构建 Addressables 内容 即可热更下发。")


if __name__ == "__main__":
    main()
