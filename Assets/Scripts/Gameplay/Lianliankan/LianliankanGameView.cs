using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 对对消局内界面：背景、计时、关卡名、重排/提示、棋盘。
    /// </summary>
    public class LianliankanGameView : MonoBehaviour
    {
        public Image Background;
        public Slider TimeBar;
        public Text TimeText;
        public Text LevelText;
        public Button PauseButton;
        public Button ShuffleButton;
        public Button HintButton;
        public Button TimeButton;
        public Button FreezeButton;
        public Button AutoButton;
        public Text ShuffleCountText;
        public Text HintCountText;
        public RectTransform GridRoot;
        public RectTransform LineRoot;
        public Image LinePrefab;
        public LianliankanTileView TilePrefab;
        public Sprite[] IconSprites;
        public Sprite TileBgA;
        public Sprite TileBgB;
        public Sprite TileSelect;
        public LianliankanAudioProfile AudioProfile;
        public LianliankanCustomBar CustomBar;

        /// <summary>自定义沙盒：不限时、不过关，页签选中时涂格子而不是连线。</summary>
        public bool Custom;

        public const int Columns = 10;
        public const int Rows = 10;

        /// <summary>本局是教学关：由 Session 在 StartPlay 前设置，开启强制引导与操作门控。</summary>
        public bool Tutorial;

        private LianliankanTileView[] _tiles;
        private LianliankanEngine _engine;
        private LianliankanTileView _selected;
        private bool _bound;
        private bool _ended;
        private bool _linking;
        private readonly List<Vector2Int> _linkPath = new List<Vector2Int>(32);
        private readonly List<Image> _linePool = new List<Image>(8);
        private Coroutine _linkRoutine;
        private ComboPopupView _combo;
        private int _comboCount;
        private float _comboUntil;
        private LianliankanAudioProfile _audio;
        private float _timeLeft;
        private float _timeLimit = 180f;
        private bool _timerRunning;
        private bool _frozen;
        private float _comboTimeBonus;
        private readonly IdleHintTracker _idle = new IdleHintTracker();

        /// <summary>本局剩余秒数，结算评星用。</summary>
        public float TimeLeft => _timeLeft;

        /// <summary>本局总时长上限，结算评星用。</summary>
        public float TimeLimit => _timeLimit;

        /// <summary>冻结道具是否正在生效。</summary>
        public bool Frozen => _frozen;

        public bool InputLocked { get; set; }
        public event Action Won;
        public event Action Lost;

        private CatMascotPlayer _mascot;

        private void Awake()
        {
            var board = GridRoot != null ? GridRoot.parent as RectTransform : null;
            var layer = board != null ? board.parent as RectTransform : null;
            if (layer != null)
            {
                var toolWidth = 150f;
                for (var i = 0; i < layer.childCount; i++)
                {
                    var child = layer.GetChild(i) as RectTransform;
                    if (child == null || child.name != "Tools")
                        continue;
                    child.anchorMin = new Vector2(0f, 0.5f);
                    child.anchorMax = new Vector2(0f, 0.5f);
                    child.pivot = new Vector2(0.5f, 0.5f);
                    toolWidth = child.sizeDelta.x;
                    var pos = child.anchoredPosition;
                    pos.x = CatMascotPlayer.EdgeInset + toolWidth * 0.5f;
                    child.anchoredPosition = pos;
                }

                var boardPos = board.anchoredPosition;
                boardPos.x = BoardCenterX(toolWidth);
                board.anchoredPosition = boardPos;
            }

            var host = layer != null ? layer : transform;
            _mascot = CatMascotPlayer.Attach(host, board, GameModeType.Lianliankan);
        }

        /// <summary>道具在左、猫在右时，棋盘在剩余区域里的水平中心。</summary>
        private static float BoardCenterX(float toolWidth)
        {
            var left = CatMascotPlayer.EdgeInset + toolWidth + 16f;
            var right = CatMascotPlayer.EdgeInset + CatMascotPlayer.DisplayWidth + 16f;
            return (left - right) * 0.5f;
        }

        /// <summary>过关兴奋播完再结算。猫没加载到时立刻回调。</summary>
        public void CelebrateWin(Action then)
        {
            if (_mascot == null)
            {
                if (then != null)
                    then();
                return;
            }

            _mascot.PlayExcited(then);
        }

        private void Start()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            CoinWallet.Changed += RefreshToolCounts;
        }

        private void OnDisable()
        {
            if (_mascot != null)
                _mascot.Abort();
            CoinWallet.Changed -= RefreshToolCounts;
            CancelInvoke(nameof(ClearHint));
            if (_linkRoutine != null)
            {
                StopCoroutine(_linkRoutine);
                _linkRoutine = null;
            }

            _linking = false;
            HideLinkLine();
            ResetIdle();
        }

        public void BindButtons()
        {
            if (_bound)
                return;
            _bound = true;

            ApplyRuntimeFont();

            if (PauseButton != null)
                PauseButton.onClick.AddListener(() =>
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.PauseGame();
                });

            if (ShuffleButton != null)
            {
                ShuffleButton.onClick.RemoveAllListeners();
                ShuffleButton.onClick.AddListener(Shuffle);
            }

            if (HintButton != null)
            {
                HintButton.onClick.RemoveAllListeners();
                HintButton.onClick.AddListener(Hint);
            }

            if (TimeButton != null)
            {
                TimeButton.onClick.RemoveAllListeners();
                TimeButton.onClick.AddListener(AddTime);
            }

            if (FreezeButton != null)
            {
                FreezeButton.onClick.RemoveAllListeners();
                FreezeButton.onClick.AddListener(FreezeTimer);
            }

            if (AutoButton != null)
            {
                AutoButton.onClick.RemoveAllListeners();
                AutoButton.onClick.AddListener(AutoLink);
            }

            BindTiles();
            EnsureTimeHud();
        }

        public void StartPlay(LianliankanLevelData config = null)
        {
            if (config == null)
                config = LianliankanLevelCatalog.Get(0);

            _ended = false;
            _selected = null;
            _linking = false;
            _frozen = false;
            _comboCount = 0;
            _comboUntil = 0f;
            _comboTimeBonus = config != null ? config.ComboTimeBonus : 0f;
            _combo = ComboPopupView.Attach(transform);
            _audio = AudioProfile != null ? AudioProfile : LianliankanAudioProfile.Load();
            _audio?.PlayBgm();
            BindButtons();
            CancelInvoke(nameof(ClearHint));
            CancelInvoke(nameof(EndFreeze));
            if (_linkRoutine != null)
            {
                StopCoroutine(_linkRoutine);
                _linkRoutine = null;
            }
            HideLinkLine();
            CollectTiles();
            ApplyDeal(config);
            BindTiles();
            _engine = new LianliankanEngine();
            _engine.Load(_tiles);
            if (!Custom)
                EnsurePlayable(false);
            SetLevel(config.LevelId, config.Title);
            RefreshToolCounts();
            EnsureTimeHud();
            RefreshTimeHud();
            if (Custom)
            {
                BeginCustomUi();
                if (TimeText != null)
                {
                    TimeText.text = "不限时";
                    TimeText.color = UIStyle.TextMain;
                }

                SetTimeNormalized(1f);
            }
        }

        private void BeginCustomUi()
        {
            if (ShuffleButton != null && ShuffleButton.transform.parent != null)
                ShuffleButton.transform.parent.gameObject.SetActive(false);
            if (CustomBar == null)
                return;
            CustomBar.gameObject.SetActive(true);
            CustomBar.Bind(this, IconSprites);
        }

        public bool TryCustomCell(Vector2 screen, Camera camera, out int row, out int col)
        {
            row = -1;
            col = -1;
            if (_tiles == null)
                return false;
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                if (tile == null)
                    continue;
                var rect = tile.transform as RectTransform;
                if (rect == null)
                    continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screen, camera))
                    continue;
                row = tile.Row;
                col = tile.Col;
                return true;
            }

            return false;
        }

        /// <summary>涂一格。没选笔时左键不动，右键仍可擦成空格。返回是否真的改了。</summary>
        public bool PaintCustom(int row, int col, bool erase)
        {
            if (!Custom || _linking || _ended)
                return false;
            var tile = GetTile(row, col);
            if (tile == null)
                return false;
            if (erase)
            {
                if (!tile.Occupied && !tile.Blocked)
                    return false;
                tile.Clear();
                return true;
            }

            if (CustomBar == null || !CustomBar.TryGetBrush(out var iconId, out var random, out var blocker))
                return false;
            var bg = (row + col) % 2 == 0 ? TileBgA : TileBgB;
            if (blocker)
            {
                if (tile.Blocked)
                    return false;
                tile.SetupBlocker(row, col, bg, IconOf(LianliankanLevelData.BlockerIconIndex));
                return true;
            }

            if (random)
                iconId = RandomPlayableIcon();
            if (iconId < 0)
                return false;
            if (tile.Occupied && !tile.Blocked && tile.IconId == iconId)
                return false;
            tile.ApplyState(true, iconId, bg, IconOf(iconId));
            return true;
        }

        /// <summary>一笔涂完后把盘面交给连线规则，不自动消对，也不重排。</summary>
        public void FinishCustomStroke()
        {
            if (!Custom || _engine == null || _tiles == null || _linking)
                return;
            Select(null);
            _engine.Load(_tiles);
        }

        private int RandomPlayableIcon()
        {
            var count = IconSprites != null ? IconSprites.Length : 0;
            var first = count > LianliankanLevelData.FirstPlayableIconIndex
                ? LianliankanLevelData.FirstPlayableIconIndex
                : 0;
            var last = first + LianliankanLevelData.PlayableIconCap(count);
            var cap = first + LianliankanLevelData.MaxTypes;
            if (last > cap)
                last = cap;
            if (last <= first)
                return first;
            return UnityEngine.Random.Range(first, last);
        }

        /// <summary>教学关的强制引导，棋盘建好之后调用。</summary>
        public void StartTutorialGuide()
        {
            if (!Tutorial)
                return;
            TutorialDirector.Start(GameModeType.Lianliankan, ResolveTargets, ResolveGraphics);
        }

        private RectTransform[] ResolveTargets(int[] cells)
        {
            var list = new List<RectTransform>(4);
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var tile = GetTile(cells[i], cells[i + 1]);
                if (tile != null)
                    list.Add(tile.transform as RectTransform);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private Graphic[] ResolveGraphics(int[] cells)
        {
            var list = new List<Graphic>(4);
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var tile = GetTile(cells[i], cells[i + 1]);
                if (tile != null && tile.Icon != null)
                    list.Add(tile.Icon);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        public void BeginTimer(float seconds)
        {
            _timeLimit = Mathf.Max(1f, seconds);
            _timeLeft = _timeLimit;
            _timerRunning = true;
            RefreshTimeHud();
        }

        public void AddTimerSeconds(float extra, float cap = 0f)
        {
            if (Custom)
                return;
            _timeLeft = Mathf.Max(0f, _timeLeft + extra);
            if (cap > 0f)
                _timeLeft = Mathf.Min(_timeLeft, cap);
            else if (_timeLeft > _timeLimit)
                _timeLimit = _timeLeft;
            if (!_ended)
                _timerRunning = true;
            RefreshTimeHud();
        }

        public void SetTimerRunning(bool running)
        {
            if (Custom)
            {
                _timerRunning = false;
                return;
            }

            _timerRunning = running && !_ended && !_frozen;
        }

        private void Update()
        {
            if (_mascot != null)
                _mascot.Tick(Time.unscaledDeltaTime);
            TickIdle();
            if (!_timerRunning || _ended)
                return;

            _timeLeft -= Time.deltaTime;
            RefreshTimeHud();
            if (_timeLeft > 0f)
                return;

            _timeLeft = 0f;
            _timerRunning = false;
            NotifyTimeUp();
        }

        private void ApplyRuntimeFont()
        {
            UIStyle.ApplyTextStyle(this);
        }

        public void SetLevel(int level, string title)
        {
            if (LevelText == null)
                return;
            if (Custom)
            {
                LevelText.text = "自定义";
                return;
            }

            if (!string.IsNullOrEmpty(title) && title.StartsWith("无尽"))
                LevelText.text = title;
            else
                LevelText.text = $"第{level}关：{title}";
        }

        public void SetTimeNormalized(float value)
        {
            if (TimeBar != null)
                TimeBar.value = Mathf.Clamp01(value);
        }

        private void RefreshTimeHud()
        {
            EnsureTimeHud();
            var left = Mathf.Max(0f, _timeLeft);
            SetTimeNormalized(_timeLimit > 0f ? left / _timeLimit : 0f);
            if (TimeText == null)
                return;
            var sec = Mathf.CeilToInt(left);
            TimeText.text = $"{sec / 60}:{sec % 60:00}";
            TimeText.color = left <= 10f ? UIStyle.Danger : UIStyle.TextMain;
        }

        private void EnsureTimeHud()
        {
            if (TimeBar == null)
            {
                var found = transform.Find("Content/TopBar/TimerRow/TimeBar");
                if (found != null)
                    TimeBar = found.GetComponent<Slider>();
            }

            if (TimeText != null)
                return;

            Transform row = null;
            if (TimeBar != null)
                row = TimeBar.transform.parent;
            if (row == null)
                row = transform.Find("Content/TopBar/TimerRow");
            if (row != null)
            {
                var existing = row.Find("TimeText");
                if (existing == null && TimeBar != null)
                    existing = TimeBar.transform.Find("TimeText");
                if (existing != null)
                    TimeText = existing.GetComponent<Text>();
            }

            if (TimeText != null)
            {
                UIStyle.ApplyTextStyle(TimeText);
                return;
            }

            if (TimeBar == null)
                return;

            var rect = UIViewFactory.CreateRect("TimeText", TimeBar.GetComponent<RectTransform>());
            UIViewFactory.StretchFull(rect, -4f);
            TimeText = rect.gameObject.AddComponent<Text>();
            TimeText.alignment = TextAnchor.MiddleCenter;
            TimeText.fontSize = 28;
            TimeText.raycastTarget = false;
            if (LevelText != null)
                TimeText.font = LevelText.font;
            UIStyle.ApplyTextStyle(TimeText);
            var outline = TimeText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.12f, 0.1f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        public void SetToolCounts(int shuffle, int hint)
        {
            SetToolBadge(ShuffleButton, shuffle);
            SetToolBadge(HintButton, hint);
        }

        private void RefreshToolCounts()
        {
            SetToolBadge(ShuffleButton, ShopItemId.LianShuffle);
            SetToolBadge(HintButton, ShopItemId.LianHint);
            SetToolBadge(TimeButton, ShopItemId.LianTime);
            SetToolBadge(FreezeButton, ShopItemId.LianFreeze);
            SetToolBadge(AutoButton, ShopItemId.LianAuto);
        }

        /// <summary>道具按钮改成图标后，数量统一写在 Badge/Label 上。</summary>
        private static void SetToolBadge(Button button, ShopItemId id)
        {
            SetToolBadge(button, CoinWallet.Count(id));
            var badge = button != null ? button.transform.Find("Badge/Label") : null;
            if (badge != null && CoinWallet.ConsumePendingGrant(id))
            {
                UITween.KillTarget(badge);
                UITween.Pop(badge, 1f, 1.4f, 0.28f);
            }
        }

        private static void SetToolBadge(Button button, int count)
        {
            if (button == null)
                return;
            var badge = button.transform.Find("Badge");
            if (badge == null)
                return;
            var node = badge.Find("Label");
            var text = node != null ? node.GetComponent<Text>() : null;
            if (text != null)
                text.text = count.ToString();
        }

        public LianliankanTileView GetTile(int row, int col)
        {
            if (_tiles == null || row < 0 || col < 0 || row >= Rows || col >= Columns)
                return null;
            return _tiles[row * Columns + col];
        }

        public void CollectTiles()
        {
            if (GridRoot == null)
                return;

            _tiles = new LianliankanTileView[Rows * Columns];
            var views = GridRoot.GetComponentsInChildren<LianliankanTileView>(true);
            for (var i = 0; i < views.Length; i++)
            {
                var tile = views[i];
                if (tile == null)
                    continue;
                tile.RestoreFromPrefab(IconSprites);
                var index = tile.Row * Columns + tile.Col;
                if (index >= 0 && index < _tiles.Length)
                    _tiles[index] = tile;
            }
        }

        private void ApplyDeal(LianliankanLevelData config)
        {
            if (_tiles == null)
                return;

            if (config != null)
                config.EnsureInitialized();
            var live = new System.Collections.Generic.List<LianliankanTileView>(64);
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                if (tile == null)
                    continue;
                var kind = config != null
                    ? config.GetCell(tile.Row, tile.Col)
                    : (LianliankanBoardLayout.IsOn(tile.Row, tile.Col)
                        ? LianliankanCellKind.Tile
                        : LianliankanCellKind.Empty);
                var bg = (tile.Row + tile.Col) % 2 == 0 ? TileBgA : TileBgB;
                if (kind == LianliankanCellKind.Blocker)
                {
                    tile.SetupBlocker(tile.Row, tile.Col, bg, IconOf(LianliankanLevelData.BlockerIconIndex));
                    continue;
                }

                if (kind != LianliankanCellKind.Tile)
                {
                    tile.Clear();
                    continue;
                }

                live.Add(tile);
            }

            if (live.Count % 2 != 0 && live.Count > 0)
            {
                live[live.Count - 1].Clear();
                live.RemoveAt(live.Count - 1);
            }

            var maxIcons = IconSprites != null && IconSprites.Length > 0 ? IconSprites.Length : 6;
            var playable = LianliankanLevelData.PlayableIconCap(maxIcons);
            if (playable <= 0)
                playable = Mathf.Max(1, maxIcons);
            var types = Custom
                ? Mathf.Max(1, Mathf.Min(LianliankanLevelData.MaxTypes, playable))
                : Mathf.Max(1, Mathf.Min(config != null ? config.TypeCount : LianliankanBoardLayout.EasyTypeCount, playable));
            var first = maxIcons > LianliankanLevelData.FirstPlayableIconIndex
                ? LianliankanLevelData.FirstPlayableIconIndex
                : 0;
            var bag = new int[live.Count];
            for (var i = 0; i < live.Count; i += 2)
            {
                var id = first + (i / 2) % types;
                bag[i] = id;
                bag[i + 1] = id;
            }

            for (var i = bag.Length - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                var tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }

            for (var i = 0; i < live.Count; i++)
            {
                var tile = live[i];
                // 固定排布优先：教学关要能确定每一格是什么图案，脚本里的坐标才成立。
                var fixedIcon = config != null ? config.FixedIconAt(tile.Row, tile.Col) : 0;
                var id = fixedIcon > 0 ? fixedIcon : bag[i];
                var bg = (tile.Row + tile.Col) % 2 == 0 ? TileBgA : TileBgB;
                tile.ApplyState(true, id, bg, IconOf(id));
            }
        }

        public void NotifyTimeUp()
        {
            if (_ended)
                return;
            _ended = true;
            _audio?.PlayLose();
            Lost?.Invoke();
        }

        public void ResumeAfterContinue()
        {
            _ended = false;
            if (_timeLeft > 0f)
                _timerRunning = true;
            RefreshTimeHud();
        }

        private void BindTiles()
        {
            if (_tiles == null)
                CollectTiles();
            if (_tiles == null)
                return;

            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                if (tile == null)
                    continue;
                if (tile.Button == null)
                    tile.Button = tile.GetComponent<Button>();
                if (tile.Button == null)
                    continue;
                tile.Button.onClick.RemoveAllListeners();
                var captured = tile;
                tile.Button.onClick.AddListener(() => OnTileClicked(captured));
            }
        }

        private void OnTileClicked(LianliankanTileView tile)
        {
            if (InputLocked || _linking || _ended || _engine == null || tile == null || !tile.Occupied)
                return;
            if (CustomBar != null && CustomBar.Editing)
                return;

            if (_selected == null)
            {
                if (!TutorialDirector.AllowsAny(tile.Row, tile.Col))
                {
                    TutorialDirector.Reject();
                    return;
                }

                _audio?.PlayClick();
                Select(tile);
                return;
            }

            if (_selected == tile)
            {
                _audio?.PlayClick();
                Select(null);
                return;
            }

            if (_engine.TryFindPath(_selected.Row, _selected.Col, tile.Row, tile.Col, _linkPath))
            {
                var a = _selected;
                if (!TutorialDirector.AllowsPair(a.Row, a.Col, tile.Row, tile.Col))
                {
                    TutorialDirector.Reject();
                    return;
                }

                Select(null);
                DailyTaskDirector.Report(GameModeType.Lianliankan, DailyTaskSignal.LianliankanPair, 1);
                if (_linkRoutine != null)
                    StopCoroutine(_linkRoutine);
                _linkRoutine = StartCoroutine(PlayLink(a, tile));
                return;
            }

            _audio?.PlayMismatch();
            StartCoroutine(ShakeTile(tile));
            if (_selected != null)
                StartCoroutine(ShakeTile(_selected));
            Select(tile);
        }

        private void Shuffle()
        {
            if (InputLocked || _linking || _ended || _engine == null)
                return;
            if (!CoinWallet.TryUse(ShopItemId.LianShuffle))
            {
                UIManager.Instance?.ShowToast("重排次数用完了，去商城看看");
                return;
            }

            Select(null);
            CancelInvoke(nameof(ClearHint));
            HideLinkLine();
            _engine.Shuffle(_tiles);
            _engine.EnsureHasMove(_tiles);
            ApplyShuffledIcons();
            RefreshToolCounts();
        }

        private void ApplyShuffledIcons()
        {
            if (_tiles == null)
                return;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {
                    var tile = GetTile(r, c);
                    if (tile == null || !tile.Occupied)
                        continue;
                    tile.SetIcon(PeekId(r, c), IconOf(PeekId(r, c)));
                }
            }
        }

        private int PeekId(int r, int c)
        {
            return _engine != null ? _engine.Peek(r, c) : -1;
        }

        private void Hint()
        {
            if (InputLocked || _linking || _ended || _engine == null)
                return;
            if (CoinWallet.Count(ShopItemId.LianHint) <= 0)
            {
                UIManager.Instance?.ShowToast("提示次数用完了，去商城看看");
                return;
            }

            if (!_engine.TryHint(out var r0, out var c0, out var r1, out var c1, _linkPath))
            {
                EnsurePlayable(true);
                if (!_engine.TryHint(out r0, out c0, out r1, out c1, _linkPath))
                    return;
            }

            CoinWallet.TryUse(ShopItemId.LianHint);
            RefreshToolCounts();
            Select(null);
            var a = GetTile(r0, c0);
            var b = GetTile(r1, c1);
            a?.SetSelected(true, TileSelect);
            b?.SetSelected(true, TileSelect);
            ShowLinkLine(_linkPath);
            CancelInvoke(nameof(ClearHint));
            Invoke(nameof(ClearHint), 1.4f);
        }

        /// <summary>
        /// 一段时间没能消掉一对就闪一对可连的牌。连接动画 / 暂停 / 结算时不计时。
        /// </summary>
        private void TickIdle()
        {
            // 引导正在闪目标时闲置提示不插手：两套闪烁会打架，Stop 也会掐掉引导高亮
            if (TutorialDirector.Active || (CustomBar != null && CustomBar.Editing))
            {
                _idle.Reset();
                return;
            }

            if (_ended || _linking || InputLocked || _engine == null)
            {
                _idle.Reset();
                HintFlash.Stop();
                return;
            }

            if (!_idle.Tick(Time.deltaTime))
                return;

            FlashHint();
        }

        private void FlashHint()
        {
            if (_engine == null)
                return;
            if (!_engine.TryHint(out var r0, out var c0, out var r1, out var c1))
            {
                EnsurePlayable(true);
                if (!_engine.TryHint(out r0, out c0, out r1, out c1))
                    return;
            }

            var graphics = new List<Graphic>(2);
            AddHintGraphic(graphics, GetTile(r0, c0));
            AddHintGraphic(graphics, GetTile(r1, c1));
            if (graphics.Count == 0)
                return;

            HintFlash.Play(graphics);
        }

        private static void AddHintGraphic(List<Graphic> graphics, LianliankanTileView tile)
        {
            if (tile == null || !tile.Occupied)
                return;

            var target = tile.Icon != null && tile.Icon.enabled ? tile.Icon : tile.Background;
            if (target != null)
                graphics.Add(target);
        }

        private void ResetIdle()
        {
            _idle.Reset();
            HintFlash.Stop();
        }

        private void AddTime()
        {
            if (Custom)
            {
                UIManager.Instance?.ShowToast("自定义不限时");
                return;
            }

            if (InputLocked || _linking || _ended)
                return;
            if (!CoinWallet.TryUse(ShopItemId.LianTime))
            {
                UIManager.Instance?.ShowToast("加时次数用完了，去商城看看");
                return;
            }

            AddTimerSeconds(ShopCatalog.LianBonusSeconds);
            RefreshToolCounts();
            UIManager.Instance?.ShowToast($"时间 +{ShopCatalog.LianBonusSeconds} 秒");
        }

        private void FreezeTimer()
        {
            if (Custom)
            {
                UIManager.Instance?.ShowToast("自定义不限时");
                return;
            }

            if (_ended || _linking)
                return;
            if (!CoinWallet.TryUse(ShopItemId.LianFreeze))
            {
                UIManager.Instance?.ShowToast("冻结次数用完了，去商城看看");
                return;
            }

            _frozen = true;
            _timerRunning = false;
            CancelInvoke(nameof(EndFreeze));
            Invoke(nameof(EndFreeze), ShopCatalog.LianFreezeSeconds);
            RefreshToolCounts();
            UIManager.Instance?.ShowToast($"倒计时停 {ShopCatalog.LianFreezeSeconds} 秒");
        }

        private void EndFreeze()
        {
            _frozen = false;
            if (!_ended)
                _timerRunning = !InputLocked;
            RefreshTimeHud();
        }

        private void AutoLink()
        {
            if (InputLocked || _linking || _ended || _engine == null)
                return;
            if (CoinWallet.Count(ShopItemId.LianAuto) <= 0)
            {
                UIManager.Instance?.ShowToast("自动连次数用完了，去商城看看");
                return;
            }

            if (!_engine.TryHint(out var r0, out var c0, out var r1, out var c1, _linkPath))
            {
                EnsurePlayable(true);
                if (!_engine.TryHint(out r0, out c0, out r1, out c1, _linkPath))
                {
                    UIManager.Instance?.ShowToast("当前没有可连的一对");
                    return;
                }
            }

            var a = GetTile(r0, c0);
            var b = GetTile(r1, c1);
            if (a == null || b == null)
                return;

            CoinWallet.TryUse(ShopItemId.LianAuto);
            RefreshToolCounts();
            Select(null);
            DailyTaskDirector.Report(GameModeType.Lianliankan, DailyTaskSignal.LianliankanPair, 1);
            if (_linkRoutine != null)
                StopCoroutine(_linkRoutine);
            _linkRoutine = StartCoroutine(PlayLink(a, b));
        }

        private void ClearHint()
        {
            HideLinkLine();
            if (_selected != null)
            {
                RefreshSelection();
                return;
            }

            if (_tiles == null)
                return;
            for (var i = 0; i < _tiles.Length; i++)
                _tiles[i]?.SetSelected(false);
        }

        private void RefreshSelection()
        {
            if (_tiles == null)
                return;
            for (var i = 0; i < _tiles.Length; i++)
                _tiles[i]?.SetSelected(_tiles[i] == _selected, TileSelect);
        }

        private void Select(LianliankanTileView tile)
        {
            _selected = tile;
            RefreshSelection();
        }

        private void ClearTile(LianliankanTileView tile)
        {
            tile.Clear();
        }

        private IEnumerator PlayLink(LianliankanTileView a, LianliankanTileView b)
        {
            _linking = true;
            CancelInvoke(nameof(ClearHint));
            _audio?.PlayConnect();
            ShowLinkLine(_linkPath);
            yield return new WaitForSeconds(0.32f);
            HideLinkLine();
            FeedCleared(a);
            FeedCleared(b);
            _engine.Clear(a.Row, a.Col);
            _engine.Clear(b.Row, b.Col);
            NoteCombo();
            TutorialDirector.NotifyCurrent();
            _linking = false;
            _linkRoutine = null;
            if (_engine.Cleared && !Custom)
            {
                _ended = true;
                _audio?.PlayWin();
                Won?.Invoke();
                yield break;
            }

            if (_mascot != null)
                _mascot.PlayEat();
            EnsurePlayable(true);
            if (!Tutorial && !_ended && _mascot != null)
                _mascot.TryCharge(1, CastCatSkill);
        }

        /// <summary>杏子：瞄一眼闪一对，顺口消自动连掉一对。放不出来返回 false，能量留着。</summary>
        private bool CastCatSkill(int skill)
        {
            if (InputLocked || _linking || _ended || _engine == null)
                return false;
            if (!_engine.TryHint(out var r0, out var c0, out var r1, out var c1, _linkPath))
                return false;

            if (skill == 0)
            {
                var graphics = new List<Graphic>(2);
                AddHintGraphic(graphics, GetTile(r0, c0));
                AddHintGraphic(graphics, GetTile(r1, c1));
                if (graphics.Count == 0)
                    return false;
                HintFlash.Play(graphics);
                if (_mascot != null)
                    _mascot.PlaySkill(0);
                UIManager.Instance?.ShowToast("提示：标出一对" + UIStyle.Key("可连") + "的牌");
                return true;
            }

            var a = GetTile(r0, c0);
            var b = GetTile(r1, c1);
            if (a == null || b == null)
                return false;

            Select(null);
            DailyTaskDirector.Report(GameModeType.Lianliankan, DailyTaskSignal.LianliankanPair, 1);
            if (_linkRoutine != null)
                StopCoroutine(_linkRoutine);
            _linkRoutine = StartCoroutine(PlayLink(a, b));
            if (_mascot != null)
                _mascot.PlaySkill(1);
            UIManager.Instance?.ShowToast("顺口消：自动连掉一对");
            return true;
        }

        private void FeedCleared(LianliankanTileView tile)
        {
            if (tile != null && _mascot != null)
                _mascot.Feed(tile.Icon);
            if (tile != null)
                ClearTile(tile);
        }

        private IEnumerator ShakeTile(LianliankanTileView tile)
        {
            var rect = tile != null ? tile.transform as RectTransform : null;
            if (rect == null)
                yield break;
            var origin = rect.anchoredPosition;
            const float duration = 0.18f;
            UITween.KillTarget(tile);
            var shaking = UITween.Custom(tile, duration, u =>
            {
                if (rect == null)
                    return;
                var falloff = 1f - u;
                rect.anchoredPosition = origin + new Vector2(
                    UnityEngine.Random.Range(-10f, 10f) * falloff,
                    UnityEngine.Random.Range(-6f, 6f) * falloff);
            }, EaseKind.Linear);

            if (shaking != null)
                yield return shaking.WaitForCompletion();

            if (rect != null)
                rect.anchoredPosition = origin;
        }

        private void NoteCombo()
        {
            ResetIdle();
            var now = Time.unscaledTime;
            _comboCount = now <= _comboUntil ? _comboCount + 1 : 1;
            _comboUntil = now + ComboCatalog.LianWindow;
            if (_comboCount >= 2 && _comboTimeBonus > 0f)
                AddTimerSeconds(_comboTimeBonus, _timeLimit);
            var label = ComboCatalog.LianLabel(_comboCount);
            if (!string.IsNullOrEmpty(label))
                _combo?.ShowCombo(_comboCount, label);
        }

        private void EnsurePlayable(bool toastIfShuffled)
        {
            if (Custom || _engine == null || _tiles == null || _ended)
                return;
            if (!_engine.RefreshIfStuck(_tiles))
            {
                ApplyShuffledIcons();
                return;
            }

            Select(null);
            CancelInvoke(nameof(ClearHint));
            HideLinkLine();
            ResetIdle();
            ApplyShuffledIcons();
            if (toastIfShuffled)
                UIManager.Instance?.ShowToast("无解，已自动重排");
        }

        private void ShowLinkLine(List<Vector2Int> path)
        {
            HideLinkLine();
            if (path == null || path.Count < 2)
                return;

            EnsureLineLayer();
            Canvas.ForceUpdateCanvases();
            var corners = CompressCorners(path);
            var points = new Vector2[corners.Count];
            for (var i = 0; i < corners.Count; i++)
                points[i] = CellCenter(corners[i].y, corners[i].x);

            const float thickness = 10f;
            for (var i = 0; i < points.Length - 1; i++)
                PlaceSegment(points[i], points[i + 1], thickness);
            for (var i = 0; i < points.Length; i++)
                PlaceJoint(points[i], thickness + 2f);
        }

        private void HideLinkLine()
        {
            for (var i = 0; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null)
                    _linePool[i].enabled = false;
            }
        }

        private void EnsureLineLayer()
        {
            if (LineRoot == null && GridRoot != null)
            {
                var existing = GridRoot.parent != null ? GridRoot.parent.Find("LineLayer") : null;
                LineRoot = existing as RectTransform;
            }

            if (LineRoot == null && GridRoot != null)
            {
                var parent = GridRoot.parent != null ? GridRoot.parent : GridRoot;
                LineRoot = UIViewFactory.CreateRect("LineLayer", parent);
                if (GridRoot.parent != null)
                    UIViewFactory.StretchFull(LineRoot, 28f);
                else
                    UIViewFactory.StretchFull(LineRoot);
            }

            if (LineRoot != null)
            {
                LineRoot.SetAsLastSibling();
                var graphic = LineRoot.GetComponent<Graphic>();
                if (graphic != null)
                    graphic.raycastTarget = false;
            }

            if (LinePrefab == null)
                LinePrefab = AssetHub.Load<Image>("Lianliankan/LianliankanLine");
        }

        private static List<Vector2Int> CompressCorners(List<Vector2Int> path)
        {
            var corners = new List<Vector2Int>(4);
            if (path.Count <= 2)
            {
                corners.AddRange(path);
                return corners;
            }

            corners.Add(path[0]);
            for (var i = 1; i < path.Count - 1; i++)
            {
                var prev = path[i] - path[i - 1];
                var next = path[i + 1] - path[i];
                if (prev.x != next.x || prev.y != next.y)
                    corners.Add(path[i]);
            }

            corners.Add(path[path.Count - 1]);
            return corners;
        }

        private Vector2 CellCenter(int row, int col)
        {
            var sample = GetTile(0, 0);
            if (sample == null)
            {
                for (var i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i] != null)
                    {
                        sample = _tiles[i];
                        break;
                    }
                }
            }

            if (sample == null || LineRoot == null)
                return Vector2.zero;

            var origin = ToLineLocal(sample);
            var stepX = 86f;
            var stepY = -86f;
            var right = GetTile(sample.Row, sample.Col + 1);
            var down = GetTile(sample.Row + 1, sample.Col);
            if (right != null)
                stepX = ToLineLocal(right).x - origin.x;
            if (down != null)
                stepY = ToLineLocal(down).y - origin.y;

            return origin + new Vector2((col - sample.Col) * stepX, (row - sample.Row) * stepY);
        }

        private Vector2 ToLineLocal(LianliankanTileView tile)
        {
            var rect = tile.transform as RectTransform;
            var world = rect != null ? rect.TransformPoint(rect.rect.center) : tile.transform.position;
            return LineRoot.InverseTransformPoint(world);
        }

        private void PlaceSegment(Vector2 from, Vector2 to, float thickness)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.5f)
                return;

            var image = NextLinePiece();
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(length + thickness * 0.35f, thickness);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            rect.localScale = Vector3.one;
        }

        private void PlaceJoint(Vector2 at, float size)
        {
            var image = NextLinePiece();
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = at;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private Image NextLinePiece()
        {
            for (var i = 0; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null && !_linePool[i].enabled)
                {
                    _linePool[i].enabled = true;
                    return _linePool[i];
                }
            }

            Image image;
            if (LinePrefab != null)
            {
                image = Instantiate(LinePrefab, LineRoot, false);
            }
            else
            {
                var rect = UIViewFactory.CreateRect("LinkLine", LineRoot);
                image = rect.gameObject.AddComponent<Image>();
                image.sprite = UIStyle.WhiteSprite;
                image.raycastTarget = false;
            }

            image.color = new Color(1f, 0.16f, 0.16f, 1f);
            image.raycastTarget = false;
            image.enabled = true;
            _linePool.Add(image);
            return image;
        }

        private Sprite IconOf(int iconId)
        {
            if (IconSprites == null || iconId < 0 || iconId >= IconSprites.Length)
                return null;
            return IconSprites[iconId];
        }
    }
}
