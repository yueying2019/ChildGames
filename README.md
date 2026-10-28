# 咔滋盒（SmallGames）

儿童向休闲消除合集。三种玩法共用选关、教学、每日挑战、商店和结算，关卡和界面都做成资源，规则与表现分开。

## 环境

- 团结引擎 1.9（Unity `2022.3.62t8`）
- 内置渲染管线，UGUI + `UnityEngine.UI.Text`
- C# 9 / .NET Standard 2.1
- 单场景：`Assets/Scenes/GameScene.scene`

用团结引擎打开工程后，直接播放 `GameScene`。运行时从 `Assets/Content/ContentIndex.asset` 按短路径取资源。索引缺失时，在编辑器执行 **SmallGames / 刷新内容索引**。

## 玩法

| 模式 | 规则 |
|---|---|
| 对对消 | 相同图案相连即可消除，通路最多拐两个弯 |
| 叠叠消 | 分层叠放，槽里凑满一组相同图案即消除 |
| 三消 | 交换相邻格子，三个以上相同即消除 |

每种模式有教学关、36 个正式关、无尽，以及按日期抽关的每日挑战。每日奖励一天领取一次；换一关不会跳过正式关的顺序解锁。

三消支持横竖消、炸弹、彩虹，以及冰、石、果冻、笼子。过关目标可以是分数、收集或清格。对对消和叠叠消在生成棋盘时保证成组可解。

外围包括金币商店、分模式星星兑换、体力、签到、每日任务、图鉴和结算评星。

## 结构

每个玩法放在 `Assets/Scripts/Gameplay/{Mode}/`，固定拆开：

| 部分 | 职责 |
|---|---|
| `{Mode}Engine` | 纯 C# 规则，不碰 `GameObject`，一步操作返回事件 |
| `{Mode}LevelData` / `{Mode}LevelCatalog` | 关卡 `ScriptableObject` 与取关 |
| `{Mode}Session` | 继承 `GameSessionBase`，只编排流程 |
| `{Mode}GameView` / `{Mode}TileView` | 按事件播放，棋子实例化已有预制体 |
| `{Mode}AudioProfile` | 本玩法音效 |

胜负、星级、金币和续关只走 `GameSessionBase`。界面预制体在 `Assets/Content/`，运行时经 `AssetHub` 加载，不使用 `Resources`。

程序集：

| 程序集 | 目录 |
|---|---|
| `SmallGames.Runtime` | `Assets/Scripts`（不含 Editor） |
| `SmallGames.Editor` | `Assets/Scripts/Editor` |
| `SmallGames.EditMode.Tests` | `Assets/Tests/EditMode` |

## 编辑器

菜单挂在 `SmallGames/` 下，按玩法分组：

- **对对消 / 叠叠消 / 三消**：关卡编辑器、生成示例关卡、生成音效配置
- **刷新内容索引**：按 `Assets/Content` 登记直接引用
- **图集打包工具**、**绑定商店道具图标**、**补全界面字体**

关卡编辑器可画格、撤销，并试玩当前关。示例关生成器覆盖 36 关，数值与现有关卡对齐。

## 测试

规则测试在 `Assets/Tests/EditMode`，用团结编辑器的 Test Runner 跑 EditMode。覆盖三消引擎、对对消寻路、叠叠消生成与槽位、每日抽关和商店消耗。

## 目录

```
Assets/Scripts/Core        启动、资源、模式切换
Assets/Scripts/Gameplay    三种玩法
Assets/Scripts/UI          界面、引导、补间
Assets/Scripts/Data        存档、商店、进度、每日
Assets/Scripts/Editor      关卡编辑器与生成器
Assets/Content             预制体、关卡、音效
Assets/Tests/EditMode      编辑器测试
```
