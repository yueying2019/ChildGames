using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 三消界面：棋盘、点击、计时和顶栏。
    /// 消除动画在 <see cref="Match3Playback"/>，道具在 <see cref="Match3Tools"/>，开场讲解在 <see cref="Match3OpeningGuide"/>。
    /// </summary>
    public class Match3GameView : MonoBehaviour
    {
        public Text LevelText;
        public Text MovesText;
        public Text GoalText;
        public Button PauseButton;
        public Button HintButton;
        public Button ShuffleButton;
        public Button HammerButton;
        public Button LineHButton;
        public Button LineVButton;
        public Button BombButton;
        public Button MovesButton;
        public Button ColorBombButton;
        public Button ColorClearButton;
        public RectTransform BoardRoot;
        public RectTransform SlotLayer;
        public RectTransform TileLayer;
        public RectTransform OverlayLayer;
        public RectTransform FxLayer;
        public Match3TileView TilePrefab;
        public Sprite[] IconSprites;
        public Match3AudioProfile AudioProfile;

        public event Action Won;
        public event Action Lost;
        public event Action WaveCleared;

        /// <summary>本局是教学关：由 Session 在 Setup 前设置，开启强制引导与操作门控。</summary>
        public bool Tutorial;

        /// <summary>本局是每日挑战：标题加上「每日」，棋盘仍是抽中的那一关。</summary>
        public bool Daily;

        /// <summary>自定义沙盒。页签和涂色层在 <see cref="CustomBar"/>。</summary>
        public bool Custom;

        public Match3CustomBar CustomBar;

        internal Match3Engine _engine;
        internal readonly Dictionary<int, Match3TileView> _views = new Dictionary<int, Match3TileView>();
        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Image> _overlays = new List<Image>();
        private readonly List<GoalChip> _goalChips = new List<GoalChip>();
        private RectTransform _goalRow;
        internal Coroutine _playRoutine;
        internal bool _busy;
        internal bool _ended;
        internal float _animSpeed = 1f;

        /// <summary>动画速度倍率的倒数系数：倍率越高，实际时长越短。</summary>
        private float AnimSpeed => _animSpeed > 0.01f ? _animSpeed : 1f;
        internal float Dur(float seconds)
        {
            return seconds / AnimSpeed;
        }
        internal float _cellSize = 72f;
        private Vector2 _origin;
        private bool _bound;
        private Text _hintLabel;
        private Text _shuffleLabel;
        private ObjectPool<Match3TileView> _tilePool;
        internal ComboPopupView _combo;
        internal Match3AudioProfile _audio;
        internal readonly HashSet<int> _poppedIds = new HashSet<int>();
        private float _timeLimit;
        private float _timeLeft;
        private bool _timerRunning;
        internal CatMascotPlayer _mascot;
        private int _lastShownSeconds = -1;
        private readonly IdleHintTracker _idle = new IdleHintTracker();
        private Match3OpeningGuide _guides;
        private Match3Tools _tools;
        private Match3Playback _playback;

        public bool InputLocked { get; set; }
        public bool IsBusy => _busy;

        /// <summary>本局剩余秒数，限时关评星用。</summary>
        public float TimeLeft => _timeLeft;

        /// <summary>本局总时长，0 表示不限时。</summary>
        public float TimeLimit => _timeLimit;

        /// <summary>时间到导致的失败（步数用尽是另一条判定）。</summary>
        public bool TimedOut => _timeLimit > 0f && _timeLeft <= 0f;

        public void BindButtons()
        {
            if (_bound)
                return;
            _bound = true;
            EnsureParts();

            ApplyRuntimeFont();
            EnsureToolButtons();
            PlaceMascot();
            if (PauseButton != null)
                PauseButton.onClick.AddListener(() => GameManager.Instance?.PauseGame());
            if (HintButton != null)
            {
                HintButton.onClick.AddListener(() => _tools.ShowHint());
                _hintLabel = HintButton.GetComponentInChildren<Text>();
            }

            if (ShuffleButton != null)
            {
                ShuffleButton.onClick.AddListener(() => _tools.RequestShuffle());
                _shuffleLabel = ShuffleButton.GetComponentInChildren<Text>();
            }

            WireTool(HammerButton, () => _tools.Arm(Match3Tool.Hammer, ShopItemId.Match3Hammer, "点一颗棋子消除"));
            WireTool(LineHButton, () => _tools.Arm(Match3Tool.LineH, ShopItemId.Match3LineH, "点一颗棋子变成横消"));
            WireTool(LineVButton, () => _tools.Arm(Match3Tool.LineV, ShopItemId.Match3LineV, "点一颗棋子变成竖消"));
            WireTool(BombButton, () => _tools.Arm(Match3Tool.Bomb, ShopItemId.Match3Bomb, "点一颗棋子变成炸弹"));
            WireTool(ColorBombButton, () => _tools.Arm(Match3Tool.ColorBomb, ShopItemId.Match3ColorBomb, "点一颗棋子变成彩虹球"));
            WireTool(ColorClearButton, () => _tools.Arm(Match3Tool.ColorClear, ShopItemId.Match3ColorClear, "点一颗棋子，消掉全场同色"));
            WireTool(MovesButton, () => _tools.AddMoves());
        }

        private static void WireTool(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private void EnsureToolButtons()
        {
            if (HammerButton != null && LineHButton != null && LineVButton != null && BombButton != null && MovesButton != null)
                return;
            if (HintButton == null)
                return;

            var footer = HintButton.transform.parent as RectTransform;
            if (footer == null)
                return;

            footer.sizeDelta = new Vector2(1000f, 280f);
            footer.anchoredPosition = new Vector2(0f, 148f);
            if (BoardRoot != null)
            {
                BoardRoot.sizeDelta = new Vector2(540f, 540f);
                BoardRoot.anchoredPosition = new Vector2(-CatMascotPlayer.BoardShift, 48f);
            }

            var top = new Vector2(190f, UIStyle.TextBoxHeight(24));
            var bot = new Vector2(160f, UIStyle.TextBoxHeight(22));
            PlaceTool(HintButton, new Vector2(-220f, 108f), top);
            PlaceTool(ShuffleButton, new Vector2(0f, 108f), top);
            MovesButton = CloneTool(HintButton, MovesButton, "MovesButton", UIStyle.AccentGreen, new Vector2(220f, 108f), top);
            HammerButton = CloneTool(HintButton, HammerButton, "HammerButton", UIStyle.Hex("#E07A5F"), new Vector2(-270f, 40f), bot);
            LineHButton = CloneTool(HintButton, LineHButton, "LineHButton", UIStyle.AccentBlue, new Vector2(-90f, 40f), bot);
            LineVButton = CloneTool(HintButton, LineVButton, "LineVButton", UIStyle.AccentBlue, new Vector2(90f, 40f), bot);
            BombButton = CloneTool(HintButton, BombButton, "BombButton", UIStyle.Accent, new Vector2(270f, 40f), bot);
        }

        private void PlaceMascot()
        {
            if (BoardRoot != null && BoardRoot.anchorMax.x > 0.95f && BoardRoot.sizeDelta.x > -400f)
            {
                var pos = BoardRoot.anchoredPosition;
                pos.x = -CatMascotPlayer.BoardShift;
                BoardRoot.anchoredPosition = pos;
                var size = BoardRoot.sizeDelta;
                size.x = -568f;
                BoardRoot.sizeDelta = size;
            }

            if (_mascot == null)
                _mascot = CatMascotPlayer.Attach(transform, BoardRoot, GameModeType.Match3);
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

        private static Button CloneTool(Button template, Button existing, string name, Color color, Vector2 pos, Vector2 size)
        {
            if (existing != null)
            {
                PlaceTool(existing, pos, size);
                return existing;
            }

            var button = Instantiate(template, template.transform.parent);
            button.name = name;
            button.onClick.RemoveAllListeners();
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = color;
            PlaceTool(button, pos, size);
            return button;
        }

        private static void PlaceTool(Button button, Vector2 pos, Vector2 size)
        {
            if (button == null)
                return;
            UIViewFactory.SetAnchored(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), size, pos);
        }

        public void Setup(Match3Engine engine)
        {
            _engine = engine;
            _ended = false;
            _timeLimit = 0f;
            _timeLeft = 0f;
            _timerRunning = false;
            _lastShownSeconds = -1;
            BindButtons();
            BeginCustomUi();
            _combo = ComboPopupView.Attach(transform);
            _audio = AudioProfile != null ? AudioProfile : Match3AudioProfile.Load();
            _audio?.PlayBgm();
            BuildSlots();
            RebuildTiles();
            RefreshHud();
            _tools.RefreshCounts();
        }

        /// <summary>进关名牌关掉之后再弹新内容讲解，避免和关卡名叠在一起。</summary>
        public void BeginOpeningGuides()
        {
            if (Tutorial)
                return;
            EnsureParts();
            StartCoroutine(_guides.PlayOpening());
        }

        /// <summary>
        /// 教学关的强制引导。放在棋盘建好之后调用，resolver 才能拿到牌面的 RectTransform。
        /// 教学关不弹「新内容讲解」，两套引导叠在一起反而乱。
        /// </summary>
        public void StartTutorialGuide()
        {
            if (!Tutorial)
                return;
            EnsureParts();
            _guides.StartForced();
        }

        public void ReloadBoard()
        {
            if (_engine == null)
                return;
            _ended = false;
            _busy = false;
            BuildSlots();
            RebuildTiles();
            RefreshHud();
            EnsureParts();
            _tools.RefreshCounts();
            StartCoroutine(_guides.PlayOpening());
        }

        public void ResumeAfterContinue()
        {
            _ended = false;
            _timerRunning = _timeLimit > 0f;
            _lastShownSeconds = -1;
            RefreshHud();
            EnsureParts();
            _tools.RefreshCounts();
        }

        private void OnEnable()
        {
            EnsureParts();
            CoinWallet.Changed += _tools.RefreshCounts;
        }

        private void OnDisable()
        {
            if (_mascot != null)
                _mascot.Abort();
            if (_tools != null)
                CoinWallet.Changed -= _tools.RefreshCounts;
            if (UIManager.Instance != null && UIManager.Instance.IsOpen(UIPanelId.Guide))
                UIManager.Instance.Close(UIPanelId.Guide);
        }

        private void EnsureParts()
        {
            if (_guides != null)
                return;
            _guides = new Match3OpeningGuide(this);
            _tools = new Match3Tools(this);
            _playback = new Match3Playback(this);
        }

        internal Match3OpeningGuide Guides
        {
            get
            {
                EnsureParts();
                return _guides;
            }
        }

        internal void RaiseWon()
        {
            Won?.Invoke();
        }

        internal void RaiseLost()
        {
            Lost?.Invoke();
        }

        internal void RaiseWaveCleared()
        {
            WaveCleared?.Invoke();
        }

        /// <summary>一轮真消除播完、还没结束时，给猫咪记一点能量。</summary>
        internal void TryChargeAfterClear()
        {
            if (_mascot != null)
                _mascot.TryCharge(1, CastCatSkill);
        }

        public void RefreshHud()
        {
            if (_engine == null)
                return;
            var level = _engine.Level;
            if (LevelText != null)
            {
                if (_engine.Endless)
                    LevelText.text = level != null ? level.DisplayName : EndlessCatalog.Title(1);
                else if (Daily)
                    LevelText.text = level != null ? $"每日 · 第{level.LevelId}关  {level.DisplayName}" : "每日挑战";
                else if (Custom)
                    LevelText.text = "自定义";
                else
                    LevelText.text = level != null ? $"第{level.LevelId}关  {level.DisplayName}" : "三消";
            }
            if (MovesText != null)
            {
                MovesText.text = _timeLimit > 0f
                    ? $"{_engine.MovesLeft} · {Mathf.CeilToInt(Mathf.Max(0f, _timeLeft))}s"
                    : _engine.MovesLeft.ToString();
                if (_timeLimit <= 0f)
                    MovesText.color = UIStyle.TextMain;
            }

            RefreshGoalHud();
        }

        /// <summary>限时关启动倒计时。seconds 小于等于 0 表示不限时。</summary>
        public void BeginTimer(float seconds)
        {
            _timeLimit = seconds <= 0f ? 0f : Mathf.Max(10f, seconds);
            _timeLeft = _timeLimit;
            _timerRunning = _timeLimit > 0f;
            _lastShownSeconds = -1;
            RefreshHud();
        }

        public void AddTimerSeconds(float extra)
        {
            if (_timeLimit <= 0f)
                return;
            _timeLeft = Mathf.Min(_timeLimit, Mathf.Max(0f, _timeLeft + extra));
            if (!_ended)
                _timerRunning = true;
            _lastShownSeconds = -1;
            RefreshHud();
        }

        public void SetTimerRunning(bool running)
        {
            _timerRunning = running && !_ended && _timeLimit > 0f;
        }

        private void Update()
        {
            if (_mascot != null)
                _mascot.Tick(Time.unscaledDeltaTime);
            TickTimer();
            TickIdle();
        }

        /// <summary>
        /// 一段时间没成功消除就闪一步可行的交换。弹窗/结算/动画播放期间不计时。
        /// </summary>
        private void TickIdle()
        {
            // 引导正在闪目标时闲置提示不插手：两套闪烁会打架，
            // 而且这里的 HintFlash.Stop 会把引导的高亮一起掐掉。
            if (TutorialDirector.Active)
            {
                _idle.Reset();
                return;
            }

            if (_busy || _ended || InputLocked || _engine == null || (CustomBar != null && CustomBar.Editing))
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
            if (!_engine.TryGetHint(out var a, out var b))
                return;

            var graphics = new List<Graphic>(2);
            AddHintGraphic(graphics, a);
            AddHintGraphic(graphics, b);
            if (graphics.Count == 0)
                return;

            HintFlash.Play(graphics);
        }

        private void AddHintGraphic(List<Graphic> graphics, Match3Coord coord)
        {
            foreach (var view in _views.Values)
            {
                if (view == null || view.X != coord.X || view.Y != coord.Y)
                    continue;

                var target = view.Icon != null && view.Icon.enabled ? view.Icon : view.Background;
                if (target != null)
                    graphics.Add(target);
                return;
            }
        }

        internal void ResetIdle()
        {
            _idle.Reset();
            HintFlash.Stop();
        }

        private void TickTimer()
        {
            if (!_timerRunning || _ended || _timeLimit <= 0f)
                return;
            if (_busy || InputLocked)
                return;

            _timeLeft -= Time.deltaTime;
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft));
            if (seconds != _lastShownSeconds)
            {
                _lastShownSeconds = seconds;
                if (MovesText != null && _engine != null)
                {
                    MovesText.text = $"{_engine.MovesLeft} · {seconds}s";
                    MovesText.color = seconds <= 10 ? UIStyle.Danger : UIStyle.TextMain;
                }
            }

            if (_timeLeft > 0f)
                return;

            _timeLeft = 0f;
            _timerRunning = false;
            _ended = true;
            _audio?.PlayLose();
            Lost?.Invoke();
        }

        private void BuildSlots()
        {
            if (BoardRoot == null)
                return;

            if (SlotLayer == null)
            {
                SlotLayer = UIViewFactory.CreateRect("SlotLayer", BoardRoot);
                UIViewFactory.StretchFull(SlotLayer);
            }

            for (var i = SlotLayer.childCount - 1; i >= 0; i--)
                Destroy(SlotLayer.GetChild(i).gameObject);
            if (OverlayLayer == null)
            {
                OverlayLayer = UIViewFactory.CreateRect("OverlayLayer", BoardRoot);
                UIViewFactory.StretchFull(OverlayLayer);
            }

            for (var i = OverlayLayer.childCount - 1; i >= 0; i--)
                Destroy(OverlayLayer.GetChild(i).gameObject);
            _slots.Clear();
            _overlays.Clear();

            var width = _engine.Width;
            var height = _engine.Height;
            var board = BoardRoot.rect.size;
            if (board.x < 8f || board.y < 8f)
                board = BoardRoot.sizeDelta;
            if (board.x < 8f || board.y < 8f)
                board = new Vector2(560f, 560f);
            _cellSize = Mathf.Floor(Mathf.Min(board.x / width, board.y / height));
            if (_cellSize < 36f)
                _cellSize = 36f;
            var gridW = _cellSize * width;
            var gridH = _cellSize * height;
            _origin = new Vector2(-gridW * 0.5f + _cellSize * 0.5f, gridH * 0.5f - _cellSize * 0.5f);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var slot = UIViewFactory.CreateImage($"Slot_{x}_{y}", SlotLayer, UIStyle.Hex("#1A2836"));
                    var rect = slot.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(_cellSize - 6f, _cellSize - 6f);
                    rect.anchoredPosition = CellPos(x, y);
                    slot.raycastTarget = false;
                    slot.enabled = _engine.HasSlot(x, y);
                    _slots.Add(slot);

                    var overlay = UIViewFactory.CreateImage($"Overlay_{x}_{y}", OverlayLayer, new Color(1f, 1f, 1f, 0f));
                    overlay.rectTransform.anchorMin = overlay.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    overlay.rectTransform.sizeDelta = new Vector2(_cellSize - 10f, _cellSize - 10f);
                    overlay.rectTransform.anchoredPosition = CellPos(x, y);
                    overlay.raycastTarget = false;
                    overlay.preserveAspect = true;
                    _overlays.Add(overlay);
                    ApplyOverlay(x, y);
                }
            }

            if (TileLayer != null)
                TileLayer.SetAsLastSibling();
            if (OverlayLayer != null)
                OverlayLayer.SetAsLastSibling();
            EnsureFxLayer();
            if (FxLayer != null)
                FxLayer.SetAsLastSibling();
            if (CustomBar != null && CustomBar.Paint != null)
                CustomBar.Paint.transform.SetAsLastSibling();
        }

        private void BeginCustomUi()
        {
            if (!Custom || CustomBar == null)
                return;
            CustomBar.gameObject.SetActive(true);
            CustomBar.Bind(this, IconSprites);
        }

        /// <summary>把屏幕坐标换成棋盘格。涂色层按住拖的时候用。</summary>
        public bool TryCustomCell(Vector2 screen, Camera camera, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (_busy || _ended || _engine == null || BoardRoot == null || _cellSize < 1f)
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(BoardRoot, screen, camera, out var local))
                return false;
            x = Mathf.RoundToInt((local.x - _origin.x) / _cellSize);
            y = Mathf.RoundToInt((_origin.y - local.y) / _cellSize);
            return x >= 0 && y >= 0 && x < _engine.Width && y < _engine.Height;
        }

        /// <summary>左键按当前笔刷替换，右键擦成空格。没选笔刷时左键不动。返回是否改动了格子。</summary>
        public bool PaintCustom(int x, int y, bool erase)
        {
            if (_engine == null || CustomBar == null || !CustomBar.Editing)
                return false;

            var before = _engine.GetTile(x, y);
            var beforeId = before != null ? before.Id : 0;
            var applied = erase ? _engine.EditorErase(x, y) : PaintBrush(x, y);
            if (!applied)
                return false;

            var after = _engine.GetTile(x, y);
            if (beforeId != 0 && (after == null || after.Id != beforeId) && _views.TryGetValue(beforeId, out var old))
            {
                RecycleTile(old);
                _views.Remove(beforeId);
            }

            if (after != null)
            {
                if (_views.TryGetValue(after.Id, out var view) && view != null)
                {
                    view.Bind(x, y, after, IconOf(after));
                    view.SetLocked(!_engine.CanSwapAt(x, y));
                }
                else
                    CreateView(after, x, y, CellPos(x, y));
            }

            ApplyOverlay(x, y);
            return true;
        }

        /// <summary>这一笔涂改松开后，按三消规则把已经形成的消除播完。</summary>
        public void FinishCustomStroke()
        {
            if (_busy || _ended || _engine == null || !Custom)
                return;
            var result = _engine.EditorResolve();
            if (!result.Applied)
                return;
            StartPlay(result);
        }

        private bool PaintBrush(int x, int y)
        {
            if (!CustomBar.TryGetBrush(out var color, out var random, out var obstacle, out var special))
                return false;
            if (random)
                return _engine.EditorPaintRandom(x, y);
            if (color > 0)
                return _engine.EditorPaintColor(x, y, color);
            if (special != Match3Special.None)
                return _engine.EditorPaintSpecial(x, y, special);
            return _engine.EditorPaintObstacle(x, y, obstacle);
        }

        internal void RebuildTiles()
        {
            ClearViews();
            if (TileLayer == null || _engine == null)
                return;

            for (var y = 0; y < _engine.Height; y++)
            {
                for (var x = 0; x < _engine.Width; x++)
                {
                    var tile = _engine.GetTile(x, y);
                    if (tile == null)
                        continue;
                    CreateView(tile, x, y, CellPos(x, y));
                }
            }
        }

        internal Match3TileView CreateView(Match3Tile tile, int x, int y, Vector2 pos)
        {
            if (TilePrefab == null)
                TilePrefab = AssetHub.Load<Match3TileView>("Match3/Match3Tile");
            if (TilePrefab == null)
            {
                Debug.LogError("[Match3] 缺少预制体 Match3/Match3Tile");
                return null;
            }

            var view = TakeTileView(tile, x, y, pos);
            if (view == null)
                return null;

            view.Bind(x, y, tile, IconOf(tile));
            view.SetLocked(_engine != null && !_engine.CanSwapAt(x, y));
            view.Swiped = OnTileSwiped;
            view.Tapped = OnTileTapped;
            if (view.Button != null)
                view.Button.onClick.RemoveAllListeners();

            _views[tile.Id] = view;
            return view;
        }

        /// <summary>从池里取一个棋子 view，取不到再 Instantiate；所有显示状态在这里重置。</summary>
        private Match3TileView TakeTileView(Match3Tile tile, int x, int y, Vector2 pos)
        {
            if (_tilePool == null)
            {
                _tilePool = new ObjectPool<Match3TileView>(
                    () => Instantiate(TilePrefab, TileLayer, false),
                    null,
                    RecycleTileView);
            }

            var view = _tilePool.Take();
            if (view == null)
                return null;

            var rect = view.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_cellSize - 8f, _cellSize - 8f);
                rect.anchoredPosition = pos;
                rect.localScale = Vector3.one;
            }

            view.gameObject.SetActive(true);
            view.transform.SetParent(TileLayer, false);
            return view;
        }

        /// <summary>回收：先掐掉可能还在跑的补间，再交还给池。</summary>
        internal void RecycleTile(Match3TileView view)
        {
            if (view == null)
                return;

            UITween.KillTarget(view);
            if (_tilePool != null)
                _tilePool.Return(view);
            else
                Destroy(view.gameObject);
        }

        private static void RecycleTileView(Match3TileView view)
        {
            if (view == null)
                return;

            if (view.Button != null)
                view.Button.onClick.RemoveAllListeners();
            HintFlash.RestoreIfPlaying(view.Icon);
            HintFlash.RestoreIfPlaying(view.Background);
            view.gameObject.SetActive(false);
        }

        private void OnTileSwiped(Match3TileView view, int dx, int dy)
        {
            if (_busy || _ended || InputLocked || view == null)
                return;
            if (CustomBar != null && CustomBar.Editing)
                return;
            EnsureParts();
            if (_tools.Armed != Match3Tool.None)
                return;

            if (_engine != null && !_engine.CanSwapAt(view.X, view.Y))
                return;
            ClearHighlights();
            PlaySwap(view.X, view.Y, view.X + dx, view.Y + dy);
        }

        private void OnTileTapped(Match3TileView view)
        {
            if (_busy || _ended || InputLocked || view == null || _engine == null)
                return;
            if (CustomBar != null && CustomBar.Editing)
                return;
            EnsureParts();
            if (_tools.Armed == Match3Tool.None)
                return;

            ClearHighlights();
            _tools.UseArmed(view);
        }

        private void PlaySwap(int x0, int y0, int x1, int y1)
        {
            if (_engine == null)
                return;
            if (!TutorialDirector.AllowsPair(x0, y0, x1, y1))
            {
                TutorialDirector.Reject();
                return;
            }

            var result = _engine.TrySwap(x0, y0, x1, y1);
            if (!result.Applied && !result.Reverted)
                return;
            StartPlay(result);
        }

        internal void StartPlay(Match3ResolveResult result)
        {
            HintFlash.Stop();
            if (_playRoutine != null)
                StopCoroutine(_playRoutine);
            EnsureParts();
            _playRoutine = StartCoroutine(_playback.Play(result));
        }

        /// <summary>泡泡：踩掉一颗普通棋子，或加 3 步。放不出来返回 false。</summary>
        private bool CastCatSkill(int skill)
        {
            if (_busy || _ended || InputLocked || _engine == null)
                return false;

            if (skill == 0)
            {
                if (!TryPickPlain(out var x, out var y))
                    return false;
                var result = _engine.TryRemoveAt(x, y);
                if (!result.Applied)
                    return false;
                StartPlay(result);
                if (_mascot != null)
                    _mascot.PlaySkill(0);
                UIManager.Instance?.ShowToast("消除：" + UIStyle.Key("随机消除") + "一颗棋子");
                return true;
            }

            _engine.AddMoves(CatMascotPlayer.Match3SkillMoves);
            RefreshHud();
            if (_mascot != null)
                _mascot.PlaySkill(1);
            UIManager.Instance?.ShowToast("添一口：步数 +3");
            return true;
        }

        private bool TryPickPlain(out int x, out int y)
        {
            x = 0;
            y = 0;
            var count = 0;
            for (var iy = 0; iy < _engine.Height; iy++)
            {
                for (var ix = 0; ix < _engine.Width; ix++)
                {
                    var tile = _engine.GetTile(ix, iy);
                    if (tile == null || tile.Special != Match3Special.None || tile.Drop != Match3Drop.None)
                        continue;
                    if (!_engine.CanSwapAt(ix, iy))
                        continue;
                    count++;
                    if (UnityEngine.Random.Range(0, count) == 0)
                    {
                        x = ix;
                        y = iy;
                    }
                }
            }

            return count > 0;
        }

        internal void EnsureFxLayer()
        {
            if (BoardRoot == null)
                return;
            if (FxLayer == null)
            {
                FxLayer = UIViewFactory.CreateRect("FxLayer", BoardRoot);
                UIViewFactory.StretchFull(FxLayer);
            }

            FxLayer.SetAsLastSibling();
        }

        internal Match3TileView ViewAt(int x, int y)
        {
            foreach (var view in _views.Values)
            {
                if (view != null && view.X == x && view.Y == y)
                    return view;
            }

            return null;
        }

        internal void SyncFromEngine()
        {
            if (_engine == null)
                return;
            RefreshOverlays();

            var seen = new HashSet<int>();
            for (var y = 0; y < _engine.Height; y++)
            {
                for (var x = 0; x < _engine.Width; x++)
                {
                    var tile = _engine.GetTile(x, y);
                    if (tile == null)
                        continue;
                    seen.Add(tile.Id);
                    if (_views.TryGetValue(tile.Id, out var view))
                    {
                        view.Bind(x, y, tile, IconOf(tile));
                        view.SetLocked(!_engine.CanSwapAt(x, y));
                        view.GetComponent<RectTransform>().anchoredPosition = CellPos(x, y);
                        view.GetComponent<RectTransform>().localScale = Vector3.one;
                    }
                    else
                    {
                        CreateView(tile, x, y, CellPos(x, y));
                    }
                }
            }

            var stale = new List<int>();
            foreach (var pair in _views)
            {
                if (!seen.Contains(pair.Key))
                    stale.Add(pair.Key);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                if (_views.TryGetValue(stale[i], out var view) && view != null)
                    RecycleTile(view);
                _views.Remove(stale[i]);
            }
        }

        private void ClearHighlights()
        {
            foreach (var view in _views.Values)
            {
                if (view != null)
                    view.SetSelected(false);
            }
        }

        private void RefreshOverlays()
        {
            if (_engine == null)
                return;
            for (var y = 0; y < _engine.Height; y++)
            {
                for (var x = 0; x < _engine.Width; x++)
                    ApplyOverlay(x, y);
            }
        }

        internal void ApplyOverlay(int x, int y)
        {
            var index = y * _engine.Width + x;
            if (index < 0 || index >= _overlays.Count)
                return;

            var overlay = _overlays[index];
            var slot = index < _slots.Count ? _slots[index] : null;
            if (slot != null)
                slot.enabled = _engine.HasSlot(x, y);

            var kind = _engine.GetKind(x, y);
            var layers = _engine.GetLayers(x, y);
            var sprite = Match3Icons.OverlayOf(IconSprites, kind);
            var show = Match3CellUtil.IsOverlay(kind) && layers > 0 && sprite != null;
            if (overlay == null)
                return;
            overlay.enabled = show;
            if (!show)
                return;
            overlay.sprite = sprite;
            overlay.preserveAspect = true;
            overlay.color = new Color(1f, 1f, 1f, Match3Icons.OverlayAlpha(layers));
        }

        internal Sprite IconOf(Match3Tile tile)
        {
            if (tile == null)
                return null;
            if (tile.Drop == Match3Drop.ExtraMoves)
                return Match3Icons.FromFile(IconSprites, Match3Icons.ExtraMovesFile);
            if (tile.Drop == Match3Drop.GoldBean)
                return Match3Icons.FromFile(IconSprites, Match3Icons.GoldBeanFile);
            if (tile.Special == Match3Special.ColorBomb)
                return Match3Icons.FromFile(IconSprites, 19);
            if (tile.Special == Match3Special.Bomb)
                return Match3Icons.FromFile(IconSprites, Match3Icons.BombFile);
            if (tile.Special == Match3Special.LineH || tile.Special == Match3Special.LineV)
                return null;
            return Match3Icons.TileOf(IconSprites, tile.Color);
        }

        internal void ApplyBonuses(Match3ResolveResult result)
        {
            if (result == null)
                return;
            if (result.BonusCoins > 0)
                CoinWallet.AddCoins(result.BonusCoins);

            if (result.BonusMoves > 0 && result.BonusCoins > 0)
                UIManager.Instance?.ShowToast($"步数 +{result.BonusMoves}  金币 +{result.BonusCoins}");
            else if (result.BonusMoves > 0)
                UIManager.Instance?.ShowToast($"步数 +{result.BonusMoves}");
            else if (result.BonusCoins > 0)
                UIManager.Instance?.ShowToast($"金币 +{result.BonusCoins}");
        }

        internal Vector2 CellPos(int x, int y)
        {
            return _origin + new Vector2(x * _cellSize, -y * _cellSize);
        }

        private void RefreshGoalHud()
        {
            if (GoalText == null)
                return;
            var level = _engine.Level;
            var useIcons = level != null &&
                (level.GoalType == Match3GoalType.Collect || level.GoalType == Match3GoalType.ClearCells);
            GoalText.enabled = !useIcons;
            if (!useIcons)
            {
                HideGoalChips();
                GoalText.text = _engine.GoalLabel();
                return;
            }

            GoalText.text = string.Empty;
            EnsureGoalRow();
            var items = new List<GoalChipData>(6);
            if (level.GoalType == Match3GoalType.ClearCells)
            {
                AddOverlayChip(items, Match3CellKind.Ice);
                AddOverlayChip(items, Match3CellKind.Stone);
                AddOverlayChip(items, Match3CellKind.Jelly);
                AddOverlayChip(items, Match3CellKind.Cage);
            }
            else
            {
                for (var color = 1; color <= _engine.ColorCount; color++)
                {
                    var need = level.CollectCounts != null && color < level.CollectCounts.Length
                        ? level.CollectCounts[color]
                        : 0;
                    if (need <= 0)
                        continue;
                    items.Add(new GoalChipData
                    {
                        Icon = Match3Icons.TileOf(IconSprites, color),
                        Label = $"{Mathf.Min(_engine.Collected[color], need)}/{need}"
                    });
                }
            }

            BindGoalChips(items);
        }

        private void AddOverlayChip(List<GoalChipData> items, Match3CellKind kind)
        {
            var n = _engine.CountRemainingKind(kind);
            if (n <= 0)
                return;
            items.Add(new GoalChipData
            {
                Icon = Match3Icons.OverlayOf(IconSprites, kind),
                Label = n.ToString()
            });
        }

        private void EnsureGoalRow()
        {
            if (_goalRow != null || GoalText == null)
                return;
            var src = GoalText.rectTransform;
            _goalRow = UIViewFactory.CreateRect("GoalRow", src.parent);
            _goalRow.anchorMin = src.anchorMin;
            _goalRow.anchorMax = src.anchorMax;
            _goalRow.pivot = src.pivot;
            _goalRow.sizeDelta = src.sizeDelta;
            _goalRow.anchoredPosition = src.anchoredPosition;
        }

        private void BindGoalChips(List<GoalChipData> items)
        {
            while (_goalChips.Count < items.Count)
            {
                var root = UIViewFactory.CreateRect($"GoalChip{_goalChips.Count}", _goalRow);
                root.sizeDelta = new Vector2(96f, 52f);
                var icon = UIViewFactory.CreateImage("Icon", root, Color.white);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(40f, 40f);
                icon.rectTransform.anchoredPosition = new Vector2(4f, 0f);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                var label = UIViewFactory.CreateText("Count", root, "0", 22, UIStyle.TextMain);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f);
                label.rectTransform.offsetMin = new Vector2(44f, 0f);
                label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.MiddleLeft;
                _goalChips.Add(new GoalChip { Root = root, Icon = icon, Label = label });
            }

            var total = items.Count * 100f;
            var start = -total * 0.5f + 50f;
            for (var i = 0; i < _goalChips.Count; i++)
            {
                var chip = _goalChips[i];
                var show = i < items.Count;
                chip.Root.gameObject.SetActive(show);
                if (!show)
                    continue;
                chip.Root.anchorMin = chip.Root.anchorMax = chip.Root.pivot = new Vector2(0.5f, 0.5f);
                chip.Root.anchoredPosition = new Vector2(start + i * 100f, 0f);
                chip.Icon.sprite = items[i].Icon;
                chip.Icon.enabled = items[i].Icon != null;
                chip.Icon.color = Color.white;
                chip.Label.text = items[i].Label;
            }
        }

        private void HideGoalChips()
        {
            for (var i = 0; i < _goalChips.Count; i++)
            {
                if (_goalChips[i].Root != null)
                    _goalChips[i].Root.gameObject.SetActive(false);
            }
        }

        private struct GoalChipData
        {
            public Sprite Icon;
            public string Label;
        }

        private struct GoalChip
        {
            public RectTransform Root;
            public Image Icon;
            public Text Label;
        }

        private void ClearViews()
        {
            ResetIdle();
            foreach (var view in _views.Values)
            {
                if (view != null)
                    RecycleTile(view);
            }

            _views.Clear();
            _poppedIds.Clear();
            if (FxLayer != null)
            {
                for (var i = FxLayer.childCount - 1; i >= 0; i--)
                    Destroy(FxLayer.GetChild(i).gameObject);
            }

            if (TileLayer == null)
                return;
            for (var i = TileLayer.childCount - 1; i >= 0; i--)
            {
                var child = TileLayer.GetChild(i);
                var leaked = child != null ? child.GetComponent<Match3TileView>() : null;
                if (leaked != null)
                    RecycleTile(leaked);
                else
                    Destroy(child.gameObject);
            }
        }

        private void ApplyRuntimeFont()
        {
            UIStyle.ApplyTextStyle(this);
        }
    }
}
