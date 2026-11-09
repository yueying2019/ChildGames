# 咔滋盒（SmallGames）

儿童向休闲合集。三种消除玩法共用选关、教学、每日挑战、商店和结算；新规揭棋和技能象棋是独立对局，不进金币、商店和星星进度。关卡和界面都做成资源，规则与表现分开。

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
| 新规揭棋 | 将、帅明置。其余棋子混洗后暗置，走一步翻开，再按真实兵种和棋面颜色走 |
| 技能象棋 | 标准明子布局。吃子后额外获得被吃子的原兵种走法 |

对对消、叠叠消、三消有教学关、正式关、无尽，以及按日期抽关的每日挑战。每日奖励一天领取一次。三消有直线、炸弹、彩虹，以及冰、石、果冻、牢笼。

新规揭棋选关为双人、人机、残局。残局红方先走。技能象棋只有双人和人机。两种象棋都支持悔棋：人机对局一次退回己方上一步和电脑应着。首进弹出规则，局内也可再看。两种象棋共用揭棋的棋子、棋盘和资源标签，不调用消除玩法的结算发奖。

## 结构

消除玩法放在 `Assets/Scripts/Gameplay/{Mode}/`，固定拆开：

| 部分 | 职责 |
|---|---|
| `{Mode}Engine` | 纯 C# 规则，不碰 `GameObject`，一步操作返回事件 |
| `{Mode}LevelData` / `{Mode}LevelCatalog` | 关卡 `ScriptableObject` 与取关 |
| `{Mode}Session` | 继承 `GameSessionBase`，只编排流程 |
| `{Mode}GameView` / `{Mode}TileView` | 按事件播放，棋子实例化已有预制体 |
| `{Mode}AudioProfile` | 本玩法音效 |

新规揭棋和技能象棋在 `Assets/Scripts/Gameplay/Jieqi/`。`JieqiEngine` 同时承载揭棋规则和技能规则。`SkillXiangqiSession` 继承 `JieqiSession`，开局改为标准明子布局。

消除玩法的胜负、星级、金币和续关走 `GameSessionBase`。界面预制体在 `Assets/Content/`，运行时经 `AssetHub` 加载，不使用 `Resources`。

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

关卡编辑器可画格、撤销，并试玩当前关。揭棋残局写在 `JieqiEndgameCatalog`，不走关卡编辑器。

## 测试

规则测试在 `Assets/Tests/EditMode`，用团结编辑器的 Test Runner 跑 EditMode。覆盖三消、对对消、叠叠消、揭棋与技能象棋、每日抽关和商店消耗。

## 目录

```
Assets/Scripts/Core        启动、资源、模式切换
Assets/Scripts/Gameplay    五种玩法
Assets/Scripts/UI          界面、引导、补间
Assets/Scripts/Data        存档、商店、进度、每日
Assets/Scripts/Editor      关卡编辑器与生成器
Assets/Content             预制体、关卡、音效
Assets/Tests/EditMode      编辑器测试
Apk                        当前 Android 安装包
```
