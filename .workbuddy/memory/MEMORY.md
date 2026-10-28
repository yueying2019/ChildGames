# SmallGames 项目记忆

## 环境
- 团结引擎（微信小游戏，2022.3 内核），无 Input System → StandaloneInputModule。
- UGUI + 旧版 Text；字体 `Assets/Font/NotoSansSC-UI.otf`；样式统一走 `UIStyle`。
- 无 XLua/DOTween/CI/git；存档 PlayerPrefs + JsonUtility（前缀 `SmallGames.`），加密 `SecurePrefs`(XXTEA)。
- 本机坑：Bash shim 坏；PowerShell stdout 不回显 →「写文件 + Read 读回」；`Remove-Item` 被拦 → `[System.IO.File]::Delete`；**无 C# 编译器**。

## 规范
- 编辑器脚本 → `Assets/Scripts/Editor/`，命名空间 `SmallGames.Editor`，菜单「SmallGames/中文名」。
- **同文件多处改必须串行**（并行 Edit 会静默丢改动），改完复读确认。
- 跨边界调 API 先验证存在性与签名，严禁臆造；Edit 用整方法替换（`old_string` 含完整方法）。
- **新增私有辅助方法前先 grep 同名成员**：大文件里同名 helper 极易撞（踩过 `Match3GameView.ViewAt` → CS0111）。
  可跑「方法名+参数类型」全项目查重脚本防复发。
- **给 Session 基类加对外属性时同步在 `IGameSession` 接口声明**：`GameManager` 一律走接口访问，漏声明即 CS1061。
- 新建 `Data/` 下的纯静态类容易漏 `using UnityEngine;`（`Mathf`/`Debug`），SO 文件用 `Array.Empty` 要 `using System;`。
- prefab-first：结构用预制体，不代码 Instantiate；列表行 = 面板持 `public XxxCellView CellPrefab;`，范例 `LevelSelectPanel`。

## 架构
- 单场景 `GameScene`；入口 `Core/GameBootstrap.cs`（`RuntimeInitializeOnLoadMethod`，DDOL）。
- Addressables：`Core/AssetHub.cs`（缓存 + 显式释放；禁 `WaitForCompletion`）。`core_ui` 本地 + 3 个 `mode_*` 远程热更。
- 微信：能力走 `Platform/WeChatMiniGameBridge.cs`（反射 WX），广告走 `Platform/WeChatAdBridge.cs`（强类型）。
- 单 Canvas 六层：Background/Gameplay/Normal/Hud/Popup/Toast。
- asmdef：`SmallGames.Runtime`（引 `Unity.Addressables`/`Unity.ResourceManager`/**`Wx`**，绝不引 `.Editor`）/ `Editor` / `EditMode.Tests`。

## 玩法模板
`Gameplay/<Mode>/`：Types + Engine（纯逻辑）+ LevelData(SO) + LevelCatalog（资源→内置 Spec→EditorPrefs 兜底）+ Session(:`GameSessionBase`) + GameView/TileView（动画 `UITween`）+ AudioProfile(SO)。
三玩法 Match3 / Lianliankan（BFS 最小转弯）/ Sheep；`GameModeType` 到 Match3=3，新玩法从 4 起。
统一钩子：`ReportResult` 唯一结算出口；`ReportDailyStats` 各玩法重写；`ComputeStars` virtual；`OfferContinue`/`TryContinue`/`AdContinue`/`ApplyContinue`；续关降星在基类（`ContinuesUsed>0` 降 1 保底 1 星）。

## 星级
`Data/StarCatalog`：`FromRemaining`（三消步数/对对消时间/叠叠消限时）、`FromCombo`（叠叠消常规）。阈值写 const（GameBalance SO Fallback 时新字段为 0 会除零）。
存档三个 `List<int>` 按关序号存星（JsonUtility 不支持 Dictionary），`RecordStars` 只升不降。展示：`ResultPanel.ApplyStars` / `LevelSelectCellView.SetStars`。
待办：`ProgressRepository.TotalStars()` 可作好友排行榜分数源。

## Sheep 规则（其他玩法照抄的范本）
`SheepLevelData` 四字段 → `SheepGameView.ApplyRules` 一处落地：`ComposeNum`(3/4)、`SlotBonus`、`TimeLimit`(0=不限)、`GoalCount`(0=清空)。`MaxSlotBonus=2`，扩展槽 `Slot_7/8` 由 `SyncSlotFrames()` + `SlotCellWidth()` 放出。
限时续关必须补时（`ResumeAfterContinue` 取 `Max(_timeLeft, ContinueCatalog.SheepBonusSeconds)`）。`Update→TickTimer`；`InputLocked||_matching` 停表。
加规则要提审；只调字段值的新关卡走 `.asset` 热更。
坑：`TryTakeMatch` 门槛写死 3 未用 `_compose`；`MatchUnit` 与之对齐，改要一起改。

## 商店 / 道具
`Data/ShopCatalog`：`ShopItemId` + `All[]` + `ModeOfPage/PageOf/CountOnPage/GetOnPage`；`ShopPanel` 数据驱动。**`ShopItemId` 只追加不插入**。
`CoinWallet` 加道具改四处：`WalletData` 字段 / `Count()` / `AddItem()` / `Clamp()`。
18 个道具按钮 = 图标 + 数量角标，无文字。角标三处不统一：Sheep `Plus/Mark`(`SetToolMark`)、LLK `Badge/Label`(`SetToolBadge`)、Match3 `Label` 复用为角标 + `Icon.color=ArmedIconColor`(`SetToolButton`)。
图标 `Assets/UI/<玩法>/<ShopItemId>.png`；商店行图标表 `ShopPanel.Icons` 由菜单「SmallGames/绑定商店道具图标」填（`AssetDatabase` 解析，别手算 guid）。

## 教学关 / 引导（2026-09-25）
- 三玩法各有固定排布的教学关（第 0 关），数据在 `Gameplay/TutorialCatalog.cs`（代码内置）。
  不评星、不计解锁、不扣体力、失败不弹续关、`LevelIndex=-1` 使「下一关」正好接第 1 关。
  **三个 Session 的 `StartNextLevel()` 必须 `IsTutorial=false`**，否则循环进教学关。
- 强制引导：`UI/Tutorial/`（Step / Script / Director / Overlay）。`GuideHighlight.SetPassThrough(true)`
  让洞内可点、洞外拦截；`HintFlash.PlayLoop` 持续闪到 Stop。
- 门控：三消 `AllowsPair`、对对消 `AllowsAny`+`AllowsPair`、叠叠消 `AllowsSingle(**图案 id**)`。
  **叠叠消不能用格子索引做门控**（牌进槽后从 `_boardTiles` 摘掉，索引前移）。
- 每份脚本只锁第一步：掉落补块后棋盘不再确定，锁死会变成"点不动"。
- 固定排布字段：`Match3LevelData.FixedBoard`、`LianliankanLevelData.IconIds/FixedIconAt`、
  `SheepLevelData.FixedCells` + `SheepBoardBuilder.PlanFromFixed`。
- **叠叠消可解性硬约束**：每种图案张数必须是 `ComposeNum` 的整数倍（4 张除不尽 3 会留残局卡死）。
- 引导与闲置提示互斥：三个 `TickIdle` 开头 `if (TutorialDirector.Active) { Reset; return; }`（**不能调 HintFlash.Stop**，会掐掉引导高亮）。
- 教学关不弹玩法规则弹窗（`TryShowFirstModeRules` 里判 `IsTutorial`）。

## 星级补充（2026-09-25）
- 三星条件展示：`StarCatalog.ThreeStarMoves/ThreeStarSeconds`；各 Session 重写 `StarConditionText/StarProgressText`；
  **只在 Stars<3 时显示**（失败页 + 未满星页），口径与 `ComputeStars()` 一致。
- 首次三星满体力：`GameSessionBase.TryGrantPerfectReward()`，**必须在 `RecordStars()` 之前调**
  （RecordStars 只升不降，写完分不清是否首次）；`ProgressRepository.HasPerfectReward/MarkPerfectReward` 去重。

## 图集
`Editor/SpriteAtlasBuilder.cs`（窗口 + 右键菜单）。别用 `SpriteAtlasExtensions`（在 `Unity.InstantGame.Editor.dll`）。纯文本：写 `.spriteatlasv2`（只有 packables）+ 改 `.meta` 的 `SpriteAtlasImporter` 段（padding/maxSize/压缩/旋转/bindAsDefault）。更新时保留 `.meta`（guid 不变 → Addressables 不断）。

## 闲置提示（15 秒）
`UI/HintFlash.cs`（周期 .42s / 最低 alpha .25 / 默认 3.2s，`UITween.Custom` 驱动，回池前 `RestoreIfPlaying`）+ `Gameplay/IdleHintTracker.cs`（15s，`Tick` 到点返回 true 并重计）。
三 GameView 在 `Update` 挂 `TickIdle()`；`_busy/_matching/_linking || _ended || InputLocked` 时 Reset + Stop。
只在真消除时重置：Match3 `HasClear`、LLK `NoteCombo`、Sheep `PlayMatch`。目标：`TryGetHint` / `TryHint`+`EnsurePlayable` / `FindHintGraphics`。

## 每日任务
`DailyTaskCatalog` × `DailyTaskRepository`(yyyyMMdd 重置) × `DailyTaskDirector`(唯一上报入口)。UI `UIPanelId.DailyTask`，行 `TaskCellTemplate`。
坑：滚动行必须挂 `ScrollRect.content`；`VerticalLayoutGroup` 要 `childControlHeight=false` + `childForceExpandHeight=false`。

## 其他子系统
`UIManager.BackPriorities` 优先级表；`HeavyPanels` 预热；`UIClickSound` 借 `ExecuteEvents` 广播；
`UITween` 静态类 + DDOL，`EaseValue` 纯函数，重复触发前 `KillTarget`；`ObjectPool`（Sheep `Restore()` 用 `GetComponentsInChildren<SheepTileView>(true)` 先 `IsPooled()` 过滤）；
新手引导数据驱动 `UI/Guide/`（Sheep 未接）。

## 测试
`Assets/Tests/EditMode/`；坑：`optionalUnityReferences:["TestAssemblies"]` 展开后不能再写进 references。
已覆盖 Match3Engine/LianliankanEngine/SheepBoardBuilder/UiToolkit/GuideCatalog。写用例前先 grep public 成员；优先改测试适配公开 API。
CI：`Unity -batchmode -quit -runTests -testPlatform editmode -testResults results.xml`。

## 广告（2026-09-24 接入完成）
链路：`AdCatalog`（广告位 ID + 参数，留空即关闭）→ `AdDirector`（隐私门禁 + 点位封装，业务只调它）→ `WeChatAdBridge`（强类型直连）。
`CanShow = PrivacyRepository.HasAgreed && WeChatAdBridge.Available`；`Initialize` 在 `GameBootstrap.EnterMainMenu`。
点位：激励=续关次数用尽后的广告复活 + 结算双倍金币；插屏=**只在返回选关时**弹（点"下一关"后弹命中运营规范 2.10.2）；Banner=主菜单/选关显示，`GameHud.OnOpen` 与 `MainMenu.OnClose` 隐藏。
`AdContinue()` 计入 `ContinuesUsed`（否则可刷广告保三星）。编辑器 `Available=false` 属设计如此。
SDK 签名以 `Library/PackageCache/com.qq.weixin.minigame@*/Runtime/Plugins/wx-runtime*.xml` 为准：广告类继承 `WXBaseAd`；**`Show` 只有 2 参/4 参**，没有 `Show()`；失败响应 `WXTextResponse` 只有 `errMsg`；激励 `OnClose` 响应有 `isEnded`；插屏 `OnClose` 是非泛型 `Action`；Banner `Hide()` 0 参。

## 上架合规
微信审核不接受牌类（378Q 已删，备份 `_Backup_378Q_20260919/`）。删玩法三经验：预制体节点跟着删 / 枚举只删不重编号 / Addressables 收尾。
隐私同意流程已恢复（`GameBootstrap`）。个人主体不能开虚拟支付 → 只能广告变现（流量主需 UV 门槛）。
ICP 备案「小程序备注」**必填、20–200 字**，是主管部门审核依据，填在「备案小程序信息-备注」。
个人主体**禁词**：经营/销售/商城/商贸/公司/集团（不得出现经营性表述）；「不涉及人民币充值」这类否定式表述允许。
服务内容标识：个人主体不得选经营性/单位性质/需审批类目；有版号或已完成「小游戏备案」的由平台默认填「休闲娱乐-游戏」，无版号其他小游戏选「生活服务-工具」。
各省特殊：河南要玩法写细；天津备注挪到「前置审批备注」且只写应急电话使用人（个人主体须直系亲属+关系）；广东要最新模板承诺书。
文案已备三版（191/197/143 字），见 `2026-10-09.md`。

## 工具脚本（Tools/）
自检：`verify_stars.py` / `verify_sheep_rules.py` / `verify_sheep_prefab.py` / `verify_new_tools.py`（YAML 头 + 越界 ID + 悬空引用 + 图标挂载）。
改写：`add_level_stars.py` / `add_sheep_slot_button.py` / `add_sheep_slot_frames.py` / `add_lianliankan_tool_buttons.py` / `add_match3_tool_buttons.py` / `apply_tool_icons.py` / `add_result_double_button.py`，备份 `Tools/_backup/`。
诊断：`dump_children.py` / `dump_subtree.py` / `dump_block.py` / `audit_prefab_graph.py` / `clean_dangling_refs.py` / `collect_tool_icon_guids.py` / `fix_out_of_range_ids.py` / `repair_prefab_headers.py`。

### 硬约束（顺序即排查顺序）
0. **先看 Editor.log**：`C:\Users\xtt\AppData\Local\Tuanjie\Editor\Editor.log`，真实原因比静态分析准。
1. 新 fileID 必须落在 SInt64：`random.randint(10**17,(1<<63)-2)`。超限被截断成 `long.MaxValue` → 撞 ID → BrokenPrefabAsset（结构校验查不出）。
2. 改写 YAML 必须保留 `%YAML 1.1` + `%TAG !u! tag:yousandi.cn,2023:`（团结 TAG 不是 unity3d.com）。脚本统一 `render(header,blocks)` + 写盘 `startswith("%YAML")` 守卫。
3. 新建块必须同时进 `bmap` 和 `blocks` 列表（否则新块被丢但 `m_Children` 已写 → 孤儿引用）。判据 `len(bmap)!=len(blocks)`。
4. 块切分正则要认 `--- !u!N &id stripped`（嵌套预制体内部对象）。
5. 引用用 32 位 hex guid，与 `.meta` 的 56 位 base64 无关、不可互推 → 写资源引用用编辑器脚本 `AssetDatabase`。
6. 悬空判据排除 ≤8 位内置 id（12800000 字体/21300000 sprite/11500000 缺失脚本）；遍历 `m_Children` 要 `bmap.get` 判空。
7. `[UI] 缺少预制体` + `[AssetHub] 缺少资源` 成对出现 = 文件被改坏；只有前者且 Editor 兜底能翻到 = 没登记 Addressables（跑同步菜单）。
