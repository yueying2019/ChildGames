using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 叠叠消局内表现：遮挡、飞入槽、消除动画。
    /// 槽位顺序、凑组消除、连击和胜负在 <see cref="SheepEngine"/>。
    /// </summary>
    public class SheepGameView : MonoBehaviour
    {
        public const int SlotCount = 7;

        /// <summary>槽位上限的天花板。关卡自带 SlotBonus 与道具「暂存位」共用这个额度。</summary>
        public const int MaxSlotLimit = SlotCount + SheepLevelData.MaxSlotBonus;

        /// <summary>槽内牌的原始尺寸。</summary>
        public const float BaseTileWidth = 80f;
        public const float BaseTileHeight = 86f;

        /// <summary>与 Slots 节点上 HorizontalLayoutGroup 的参数保持一致（间距 8、左右留白 6）。</summary>
        public const float SlotSpacing = 8f;
        public const float SlotPadding = 6f;

        public Image Background;
        public Text DateText;
        public Slider ProgressBar;
        public Button PauseButton;
        public Button RemoveButton;
        public Button UndoButton;
        public Button ShuffleButton;
        public Button SlotButton;
        public RectTransform BoardRoot;
        public RectTransform SlotRoot;
        public RectTransform LeftStack;
        public RectTransform RightStack;
        public SheepTileView TilePrefab;
        public Sprite[] IconSprites;
        public SheepAudioProfile AudioProfile;

        /// <summary>本局连击峰值。叠叠消的评星口径。</summary>
        public int ComboPeak => _engine.ComboPeak;

        public bool InputLocked { get; set; }
        public event Action Won;
        public event Action Lost;

        private readonly List<SheepTileView> _boardTiles = new List<SheepTileView>(64);
        private readonly List<SheepTileView> _slots = new List<SheepTileView>(SlotCount);
        private readonly Stack<UndoRecord> _undos = new Stack<UndoRecord>(32);
        private readonly List<SheepTileView> _removed = new List<SheepTileView>(8);
        private bool _bound;
        private bool _ended;
        private bool _matching;
        private int _totalTiles;
        private readonly SheepEngine _engine = new SheepEngine();
        private ComboPopupView _comboPopup;
        private SheepAudioProfile _audio;
        private ObjectPool<SheepTileView> _tilePool;
        private RectTransform _poolRoot;

        /// <summary>本局槽位上限。基础值 SlotCount，可被关卡 SlotBonus 与道具抬高。</summary>
        private int _slotLimit = SlotCount;

        private float _timeLimit;
        private float _timeLeft;
        private int _shownSecond = -1;
        private string _header = string.Empty;
        private CatMascotPlayer _mascot;

        private void Awake()
        {
            if (BoardRoot != null && BoardRoot.anchorMax.x > 0.8f)
            {
                var max = BoardRoot.anchorMax;
                max.x = 0.78f;
                BoardRoot.anchorMax = max;
            }

            _mascot = CatMascotPlayer.Attach(transform, BoardRoot, GameModeType.Sheep);
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

        private void Update()
        {
            if (_mascot != null)
                _mascot.Tick(Time.unscaledDeltaTime);
            TickTimer();
            TickIdle();
        }

        /// <summary>本局限时倒计时，剩余秒。不限时为 0。</summary>
        public float TimeLeft => _timeLimit > 0f ? _timeLeft : 0f;

        /// <summary>本局限时总长，0 表示不限时。</summary>
        public float TimeLimit => _timeLimit;

        /// <summary>本局还剩多少组可达成目标。非目标关返回 0。</summary>
        public int GoalRemain => _engine.GoalCount > 0
            ? Mathf.Max(0, _engine.GoalCount - _engine.GroupsCleared)
            : 0;

        /// <summary>本局是否因超时而结束。从剩余时间直接推出来，不需要额外状态。</summary>
        public bool TimedOut => _timeLimit > 0f && _timeLeft <= 0f;

        private void TickTimer()
        {
            if (_timeLimit <= 0f || _ended)
                return;
            if (InputLocked || _matching)
                return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft > 0f)
            {
                RefreshHeader();
                return;
            }

            _timeLeft = 0f;
            RefreshHeader();
            _ended = true;
            _audio?.PlayLose();
            Lost?.Invoke();
        }

        /// <summary>
        /// 一段时间没能消掉一组就闪一组「点下去就有戏」的牌。结算 / 输入锁定 / 消除动画时不计时。
        /// </summary>
        private void TickIdle()
        {
            // 引导正在闪目标时闲置提示不插手：两套闪烁会打架，Stop 也会掐掉引导高亮
            if (TutorialDirector.Active)
            {
                _idle.Reset();
                return;
            }

            if (_ended || _matching || InputLocked || _boardTiles.Count == 0)
            {
                _idle.Reset();
                HintFlash.Stop();
                return;
            }

            if (!_idle.Tick(Time.deltaTime))
                return;

            var graphics = FindHintGraphics();
            if (graphics == null || graphics.Count == 0)
                return;

            HintFlash.Play(graphics);
        }

        /// <summary>与本局消除门槛对齐，取关卡 ComposeNum。</summary>
        private int MatchUnit => _engine.Compose;

        private void ResetIdle()
        {
            _idle.Reset();
            HintFlash.Stop();
        }

        private static bool CanTap(SheepTileView tile)
        {
            return tile != null && tile.gameObject.activeSelf && !tile.InSlot && !tile.Locked;
        }

        private static Graphic FaceGraphic(SheepTileView tile)
        {
            if (tile == null)
                return null;
            if (tile.Icon != null && tile.Icon.enabled)
                return tile.Icon;
            return tile.Face;
        }

        private void CollectSlotTiles(int iconId, List<SheepTileView> result)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null && _slots[i].IconId == iconId)
                    result.Add(_slots[i]);
            }
        }

        /// <summary>
        /// 找一组值得闪的牌：优先「槽里差一张」的组合，其次场上能凑够一组的可点牌。
        /// 找不到（全被压住 / 槽快满了）就返回 null，不做无意义的闪。
        /// </summary>
        private List<Graphic> FindHintGraphics()
        {
            var slotCounts = new Dictionary<int, int>();
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    continue;
                var id = _slots[i].IconId;
                slotCounts.TryGetValue(id, out var n);
                slotCounts[id] = n + 1;
            }

            var open = new Dictionary<int, List<SheepTileView>>();
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var tile = _boardTiles[i];
                if (!CanTap(tile))
                    continue;

                List<SheepTileView> same;
                if (!open.TryGetValue(tile.IconId, out same))
                {
                    same = new List<SheepTileView>(MatchUnit);
                    open.Add(tile.IconId, same);
                }

                same.Add(tile);
            }

            var picks = new List<SheepTileView>(MatchUnit);
            foreach (var pair in slotCounts)
            {
                if (pair.Value != MatchUnit - 1)
                    continue;

                List<SheepTileView> same;
                if (!open.TryGetValue(pair.Key, out same) || same.Count == 0)
                    continue;

                CollectSlotTiles(pair.Key, picks);
                picks.Add(same[0]);
                return ToGraphics(picks);
            }

            picks.Clear();
            foreach (var pair in open)
            {
                slotCounts.TryGetValue(pair.Key, out var inSlot);
                var need = MatchUnit - inSlot;
                if (need <= 0 || pair.Value.Count < need)
                    continue;
                if (_slots.Count + need > _slotLimit)
                    continue;

                CollectSlotTiles(pair.Key, picks);
                for (var i = 0; i < need && i < pair.Value.Count; i++)
                    picks.Add(pair.Value[i]);
                return ToGraphics(picks);
            }

            return null;
        }

        private static List<Graphic> ToGraphics(List<SheepTileView> tiles)
        {
            var graphics = new List<Graphic>(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                var graphic = FaceGraphic(tiles[i]);
                if (graphic != null)
                    graphics.Add(graphic);
            }

            return graphics;
        }

        public void BindButtons()
        {
            if (_bound)
                return;
            _bound = true;

            ApplyRuntimeFont();

            if (PauseButton != null)
            {
                PauseButton.onClick.RemoveAllListeners();
                PauseButton.onClick.AddListener(() => GameManager.Instance?.PauseGame());
            }

            if (RemoveButton != null)
            {
                RemoveButton.onClick.RemoveAllListeners();
                RemoveButton.onClick.AddListener(RemoveFromSlot);
            }

            if (UndoButton != null)
            {
                UndoButton.onClick.RemoveAllListeners();
                UndoButton.onClick.AddListener(Undo);
            }

            if (ShuffleButton != null)
            {
                ShuffleButton.onClick.RemoveAllListeners();
                ShuffleButton.onClick.AddListener(Shuffle);
            }

            if (SlotButton != null)
            {
                SlotButton.onClick.RemoveAllListeners();
                SlotButton.onClick.AddListener(ExpandSlot);
            }
        }

        private readonly IdleHintTracker _idle = new IdleHintTracker();

        /// <summary>本局是教学关：由 Session 在 StartPlay 前设置，开启强制引导与操作门控。</summary>
        public bool Tutorial;

        /// <summary>教学关的强制引导，棋盘建好之后调用。</summary>
        public void StartTutorialGuide()
        {
            if (!Tutorial)
                return;
            TutorialDirector.Start(GameModeType.Sheep, ResolveTargets, ResolveGraphics);
        }

        /// <summary>坐标语义是图案 id：返回棋盘上所有该图案的牌。</summary>
        private RectTransform[] ResolveTargets(int[] cells)
        {
            var list = new List<RectTransform>(8);
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                if (_boardTiles[i] != null && MatchesCell(_boardTiles[i], cells))
                    list.Add(_boardTiles[i].transform as RectTransform);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private Graphic[] ResolveGraphics(int[] cells)
        {
            var list = new List<Graphic>(8);
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var tile = _boardTiles[i];
                if (tile != null && tile.Icon != null && MatchesCell(tile, cells))
                    list.Add(tile.Icon);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private static bool MatchesCell(SheepTileView tile, int[] cells)
        {
            if (cells == null || cells.Length == 0)
                return false;
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == tile.IconId)
                    return true;
            }

            return false;
        }

        public void StartPlay(SheepLevelData config = null)
        {
            if (config == null)
                config = SheepLevelCatalog.Get(0);

            ResetIdle();
            _ended = false;
            _matching = false;
            _slots.Clear();
            ApplyRules(config);
            _undos.Clear();
            _removed.Clear();
            _comboPopup = ComboPopupView.Attach(transform);
            _audio = AudioProfile != null ? AudioProfile : SheepAudioProfile.Load();
            _audio?.PlayBgm();
            BindButtons();
            if (HasTilePrefab())
            {
                DestroyExistingTiles();
                SheepBoardBuilder.Build(this, config);
            }

            CollectTiles();
            EnsureMatchableSet(config != null ? config.TypeNum : 4);
            BindTiles();
            RefreshLocks();
            RefreshProgress();
            RefreshToolCounts();
        }

        /// <summary>
        /// 把关卡的规则字段落到本局运行时状态上。
        /// 新增规则字段时改这里即可，关卡侧只需要配 .asset（mode_sheep 是远程包，能热更）。
        /// </summary>
        private void ApplyRules(SheepLevelData config)
        {
            if (config != null)
                config.EnsureInitialized();

            var compose = Mathf.Max(
                SheepLevelData.MinCompose,
                config != null ? config.ComposeNum : SheepLevelData.DefaultCompose);

            var bonus = config != null ? config.SlotBonus : 0;
            _slotLimit = Mathf.Clamp(SlotCount + bonus, SlotCount, MaxSlotLimit);
            SyncSlotFrames();

            _timeLimit = config != null ? config.TimeLimit : 0f;
            if (_timeLimit < 0f)
                _timeLimit = 0f;
            _timeLeft = _timeLimit;
            _shownSecond = -1;

            var goal = config != null ? config.GoalCount : 0;
            _engine.Load(compose, _slotLimit, goal);
            RefreshHeader();
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
        }

        private bool HasTilePrefab()
        {
            if (TilePrefab != null)
                return true;
            TilePrefab = AssetHub.Load<SheepTileView>("Sheep/SheepTile");
            return TilePrefab != null;
        }

        /// <summary>
        /// 回收池根节点。刻意放在 BoardRoot / 左右堆之外，
        /// 这样 CollectTiles() 不会把空闲棋子当成棋盘上的牌。
        /// </summary>
        private RectTransform PoolRoot
        {
            get
            {
                if (_poolRoot != null)
                    return _poolRoot;

                var go = new GameObject("TilePool", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _poolRoot = go.transform as RectTransform;
                UIViewFactory.StretchFull(_poolRoot);
                return _poolRoot;
            }
        }

        private void EnsureTilePool()
        {
            if (_tilePool != null || TilePrefab == null)
                return;

            var prefab = TilePrefab;
            var poolRoot = PoolRoot;
            _tilePool = new ObjectPool<SheepTileView>(
                () => Instantiate(prefab, poolRoot, false),
                item =>
                {
                    if (item != null)
                        item.gameObject.SetActive(true);
                },
                item =>
                {
                    if (item != null)
                        item.gameObject.SetActive(false);
                });
        }

        /// <summary>从池里取一枚棋子重新上场（没有就 Instantiate）。</summary>
        public SheepTileView TakeTile(Transform parent)
        {
            if (TilePrefab == null)
                HasTilePrefab();
            EnsureTilePool();
            if (_tilePool == null)
                return null;

            var tile = _tilePool.Take();
            if (tile == null)
                return null;

            UITween.KillTarget(tile);
            var rect = tile.transform as RectTransform;
            tile.transform.SetParent(parent != null ? parent : (Transform)PoolRoot, false);
            tile.transform.localScale = Vector3.one;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            tile.InSlot = false;
            return tile;
        }

        /// <summary>归还一枚棋子。已经在场上就用 Return，场景预摆的用 Adopt 收编。</summary>
        public bool ReturnTile(SheepTileView tile)
        {
            if (tile == null)
                return false;

            UITween.KillTarget(tile);
            HintFlash.RestoreIfPlaying(tile.Icon);
            HintFlash.RestoreIfPlaying(tile.Face);
            tile.InSlot = false;
            tile.transform.SetParent(PoolRoot, false);
            tile.transform.localScale = Vector3.one;
            var rect = tile.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = Vector2.zero;

            if (_tilePool == null)
            {
                tile.gameObject.SetActive(false);
                EnsureTilePool();
            }

            if (_tilePool == null)
                return false;
            if (_tilePool.Return(tile))
                return true;
            tile.gameObject.SetActive(false);
            return _tilePool.Adopt(tile);
        }

        private bool IsPooled(SheepTileView tile)
        {
            return tile != null && _poolRoot != null && tile.transform.parent == _poolRoot;
        }

        private void DestroyExistingTiles()
        {
            if (TilePrefab == null)
                HasTilePrefab();
            EnsureTilePool();

            // 关卡重开时不再 Destroy 重造，整批回到池子里，下一局直接复用
            var tiles = GetComponentsInChildren<SheepTileView>(true);
            for (var i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                    continue;
                if (_tilePool == null)
                {
                    Destroy(tiles[i].gameObject);
                    continue;
                }

                ReturnTile(tiles[i]);
            }

            _boardTiles.Clear();
        }

        public void SetDate(string text)
        {
            _header = text ?? string.Empty;
            RefreshHeader();
        }

        /// <summary>
        /// 标题栏 = 关卡文案 + 剩余秒数。只在整数秒变化时写 Text，
        /// 避免每帧 dirty 整个 Canvas。
        /// </summary>
        private void RefreshHeader(bool force = false)
        {
            if (DateText == null)
                return;

            if (_timeLimit <= 0f)
            {
                if (!force && DateText.text == _header)
                    return;
                DateText.text = _header;
                _shownSecond = -1;
                return;
            }

            var second = Mathf.CeilToInt(_timeLeft);
            if (!force && second == _shownSecond)
                return;
            _shownSecond = second;
            DateText.text = _engine.GoalCount > 0
                ? $"{_header}  {second}s  还需 {GoalRemain} 组"
                : $"{_header}  {second}s";
        }

        public void SetProgress(float value)
        {
            if (ProgressBar != null)
                ProgressBar.value = Mathf.Clamp01(value);
        }

        private void CollectTiles()
        {
            _boardTiles.Clear();
            CollectUnder(BoardRoot);
            CollectUnder(LeftStack);
            CollectUnder(RightStack);
            _totalTiles = _boardTiles.Count;
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                _boardTiles[i].RestoreFromPrefab(IconSprites);
                _boardTiles[i].RememberHome();
            }
        }

        private void EnsureMatchableSet(int typeCount)
        {
            var unit = Mathf.Max(1, _engine.Compose);
            var usable = _boardTiles.Count / unit * unit;
            for (var i = _boardTiles.Count - 1; i >= usable; i--)
            {
                if (_boardTiles[i] != null)
                    _boardTiles[i].gameObject.SetActive(false);
                _boardTiles.RemoveAt(i);
            }

            _totalTiles = _boardTiles.Count;
            if (_totalTiles == 0 || IconsBalanced())
                return;
            RestoreAndBalance(typeCount);
        }

        private bool IconsBalanced()
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var id = _boardTiles[i].IconId;
                counts.TryGetValue(id, out var n);
                counts[id] = n + 1;
            }

            foreach (var pair in counts)
            {
                if (pair.Value % Mathf.Max(1, _engine.Compose) != 0)
                    return false;
            }

            return true;
        }

        private void RestoreAndBalance(int typeCount)
        {
            _boardTiles.Sort((a, b) => b.Layer.CompareTo(a.Layer));
            var unit = Mathf.Max(1, _engine.Compose);
            var usable = _boardTiles.Count / unit * unit;
            for (var i = _boardTiles.Count - 1; i >= usable; i--)
            {
                _boardTiles[i].gameObject.SetActive(false);
                _boardTiles.RemoveAt(i);
            }

            _totalTiles = _boardTiles.Count;
            if (_totalTiles == 0)
                return;

            typeCount = Mathf.Max(1, typeCount);
            var bag = new List<int>(_totalTiles);
            for (var i = 0; i < _totalTiles / unit; i++)
            {
                var id = i % typeCount;
                for (var k = 0; k < unit; k++)
                    bag.Add(id);
            }

            for (var i = bag.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                var tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }

            for (var i = 0; i < _boardTiles.Count; i++)
                _boardTiles[i].SetIcon(bag[i], IconOf(bag[i]));
        }

        private void CollectUnder(Transform root)
        {
            if (root == null)
                return;
            var tiles = root.GetComponentsInChildren<SheepTileView>(true);
            for (var i = 0; i < tiles.Length; i++)
            {
                if (!_boardTiles.Contains(tiles[i]))
                    _boardTiles.Add(tiles[i]);
            }
        }

        private void BindTiles()
        {
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var tile = _boardTiles[i];
                if (tile.Button == null)
                    tile.Button = tile.GetComponentInChildren<Button>(true);
                if (tile.Button == null)
                    continue;
                tile.Button.onClick.RemoveAllListeners();
                var captured = tile;
                tile.Button.onClick.AddListener(() => OnTileClicked(captured));
            }
        }

        private void OnTileClicked(SheepTileView tile)
        {
            if (InputLocked || _matching || _ended || tile == null || tile.InSlot || !tile.gameObject.activeSelf)
                return;
            if (tile.Locked)
                return;
            if (_slots.Count >= _slotLimit)
            {
                UIManager.Instance?.ShowToast("槽位已满");
                return;
            }

            // 教学关门控用图案 id：牌一进槽就从棋盘列表摘掉，索引会前移，用索引会错位。
            if (!TutorialDirector.AllowsSingle(tile.IconId))
            {
                TutorialDirector.Reject("先点这几张一样的牌");
                return;
            }

            PushUndo();
            _audio?.PlayClick();
            StartCoroutine(TakeIntoSlot(tile));
        }

        private IEnumerator TakeIntoSlot(SheepTileView tile)
        {
            _matching = true;
            yield return PlaceInSlot(tile);
            if (TryTakeMatch(out var matched))
            {
                _engine.NoteMatch();
                RefreshHeader(true);
                TutorialDirector.NotifyCurrent();
                yield return PlayMatch(matched);
                yield break;
            }

            _engine.BreakCombo();
            RefreshLocks();
            RefreshProgress();
            CheckEnd();
            _matching = false;
        }

        private IEnumerator PlaceInSlot(SheepTileView tile)
        {
            tile.InSlot = true;
            tile.SetLocked(false);
            _boardTiles.Remove(tile);
            InsertGrouped(tile);
            yield return FlyIntoSlot(tile);
        }

        private void InsertGrouped(SheepTileView tile)
        {
            var index = _engine.Insert(tile.IconId);
            if (index < 0 || index >= _slots.Count)
                _slots.Add(tile);
            else
                _slots.Insert(index, tile);
        }

        private IEnumerator FlyIntoSlot(SheepTileView tile)
        {
            LayoutSlots(tile);
            var rect = tile.transform as RectTransform;
            if (rect == null)
                yield break;

            var from = rect.anchoredPosition;
            const float duration = 0.22f;
            UITween.KillTarget(tile);
            var flying = UITween.Custom(tile, duration, u =>
            {
                if (rect == null)
                    return;
                rect.anchoredPosition = Vector2.Lerp(from, Vector2.zero, u);
                rect.localScale = Vector3.Lerp(Vector3.one * 1.08f, Vector3.one, u);
            }, EaseKind.InOutQuad);

            if (flying != null)
                yield return flying.WaitForCompletion();

            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        private bool TryTakeMatch(out List<SheepTileView> matched)
        {
            matched = null;
            if (!_engine.TryTakeMatch(out var matchId))
                return false;

            matched = new List<SheepTileView>(_engine.Compose);
            for (var i = _slots.Count - 1; i >= 0 && matched.Count < _engine.Compose; i--)
            {
                if (_slots[i] == null || _slots[i].IconId != matchId)
                    continue;
                matched.Add(_slots[i]);
                _slots.RemoveAt(i);
            }

            return matched.Count >= _engine.Compose;
        }

        private bool MatchClearsLevel(List<SheepTileView> matched)
        {
            var matchedCount = matched != null ? matched.Count : 0;
            return _engine.MatchClearsLevel(CountActiveTiles(), matchedCount);
        }

        private IEnumerator PlayMatch(List<SheepTileView> matched)
        {
            _matching = true;
            ResetIdle();
            var label = ComboCatalog.SheepLabel(_engine.Combo);
            if (!string.IsNullOrEmpty(label))
                _comboPopup?.ShowCombo(_engine.Combo, label);
            _audio?.PlayMatch();
            var clearsLevel = MatchClearsLevel(matched);
            if (_mascot != null && !clearsLevel)
                _mascot.PlayEat();
            DailyTaskDirector.Report(GameModeType.Sheep, DailyTaskSignal.SheepTriple, 1);

            for (var i = 0; i < matched.Count; i++)
            {
                var tile = matched[i];
                if (tile == null)
                    continue;
                if (_mascot != null)
                    _mascot.Feed(tile.Icon);
                tile.gameObject.SetActive(false);
            }

            LayoutSlots();
            RefreshLocks();
            RefreshProgress();
            RefreshToolCounts();
            CheckEnd();
            _matching = false;
            if (!Tutorial && !clearsLevel && !_ended && _mascot != null)
                _mascot.TryCharge(1, CastCatSkill);
            yield break;
        }

        /// <summary>豆豆：吐出槽里最早的一张，或洗还在牌面上的牌。放不出来返回 false。</summary>
        private bool CastCatSkill(int skill)
        {
            if (InputLocked || _matching || _ended)
                return false;

            if (skill == 0)
            {
                if (_slots.Count == 0)
                    return false;
                PushUndo();
                RelocateSlotsToBoard(1);
                LayoutSlots();
                RefreshLocks();
                RefreshProgress();
                if (_mascot != null)
                    _mascot.PlaySkill(0);
                UIManager.Instance?.ShowToast("移出：" + UIStyle.Key("最早的一张") + "回到场上");
                return true;
            }

            if (_boardTiles.Count < 2)
                return false;
            PushUndo();
            ShuffleBoardIcons();
            if (_mascot != null)
                _mascot.PlaySkill(1);
            UIManager.Instance?.ShowToast("拨乱：洗一洗牌面上的牌");
            return true;
        }

        /// <summary>
        /// Slots 节点下预留了 SlotCount + MaxSlotBonus 个槽位容器，超出基础数量的默认是隐藏的。
        /// LayoutGroup 只收集 activeInHierarchy 的子节点，所以按本局上限决定放几个出来。
        /// </summary>
        private void SyncSlotFrames()
        {
            if (SlotRoot == null)
                return;
            var want = Mathf.Clamp(_slotLimit, SlotCount, SlotRoot.childCount);
            for (var i = 0; i < SlotRoot.childCount; i++)
            {
                var child = SlotRoot.GetChild(i);
                if (child != null)
                    child.gameObject.SetActive(i < want);
            }
        }

        /// <summary>
        /// 单个槽位能容纳的牌宽。槽是 LayoutGroup 等分的，槽越多每张牌越窄。
        /// Shape: 上一行 m_Spacing/m_Padding 取自 Slots 的 HorizontalLayoutGroup（8 / 左右各 6）。
        /// 宽度拿不到时（Canvas 还没重建）退回原始尺寸，不影响正确性。
        /// </summary>
        private float SlotCellWidth()
        {
            if (SlotRoot == null)
                return BaseTileWidth;
            var width = SlotRoot.rect.width;
            if (width <= 1f)
                return BaseTileWidth;
            var count = Mathf.Max(1, Mathf.Clamp(_slotLimit, 1, SlotRoot.childCount));
            var cell = (width - SlotPadding * 2f - SlotSpacing * (count - 1)) / count;
            if (cell <= 1f || cell >= BaseTileWidth)
                return BaseTileWidth;
            return cell;
        }

        private void LayoutSlots(SheepTileView flying = null)
        {
            if (SlotRoot == null)
                return;

            var cellWidth = SlotCellWidth();
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = i < SlotRoot.childCount ? SlotRoot.GetChild(i) : SlotRoot;
                var tile = _slots[i];
                var rect = tile.transform as RectTransform;
                var keepWorld = flying != null && tile == flying;
                tile.transform.SetParent(slot, keepWorld);
                if (rect == null)
                    continue;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                if (!keepWorld)
                    rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(cellWidth, cellWidth * BaseTileHeight / BaseTileWidth);
                if (!keepWorld)
                    rect.localScale = Vector3.one;
                tile.SetLocked(false);
                if (tile.Button != null)
                    tile.Button.interactable = false;
                if (tile.Face != null)
                    tile.Face.raycastTarget = false;
            }
        }

        private void RemoveFromSlot()
        {
            if (InputLocked || _matching || _ended)
                return;
            if (_slots.Count == 0)
            {
                UIManager.Instance?.ShowToast("槽里没有牌");
                return;
            }

            if (!CoinWallet.TryUse(ShopItemId.SheepRemove))
            {
                UIManager.Instance?.ShowToast("移出次数用完了，去商城看看");
                return;
            }

            _engine.BreakCombo();
            PushUndo();
            RelocateSlotsToBoard(Mathf.Min(3, _slots.Count));
            LayoutSlots();
            RefreshLocks();
            RefreshProgress();
            RefreshToolCounts();
        }

        public void ResumeAfterContinue()
        {
            _ended = false;
            _engine.BreakCombo();

            // 续关必须补时。否则 _timeLeft 停在 0，复活的下一帧 TickTimer 又会判负。
            if (_timeLimit > 0f)
            {
                _timeLeft = Mathf.Max(_timeLeft, ContinueCatalog.SheepBonusSeconds);
                RefreshHeader(true);
            }

            if (_slots.Count == 0)
            {
                RefreshLocks();
                RefreshProgress();
                return;
            }

            PushUndo();
            RelocateSlotsToBoard(Mathf.Min(ContinueCatalog.SheepClearSlots, _slots.Count));
            LayoutSlots();
            RefreshLocks();
            RefreshProgress();
            RefreshToolCounts();
        }

        private void RelocateSlotsToBoard(int count)
        {
            _engine.RemoveFront(count);
            var host = BoardRoot != null ? BoardRoot : transform as RectTransform;
            for (var i = 0; i < count; i++)
            {
                var tile = _slots[0];
                _slots.RemoveAt(0);
                tile.InSlot = false;
                tile.gameObject.SetActive(true);
                tile.transform.SetParent(host, false);
                var rect = tile.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = tile.HomeSize;
                    rect.anchoredPosition = new Vector2((i - 1) * 70f, 36f + i * 8f);
                    rect.SetAsLastSibling();
                }

                _boardTiles.Add(tile);
                _removed.Add(tile);
            }
        }

        private void Undo()
        {
            if (InputLocked || _matching || _ended)
                return;
            if (_undos.Count == 0)
            {
                UIManager.Instance?.ShowToast("没有可撤回的操作");
                return;
            }

            if (!CoinWallet.TryUse(ShopItemId.SheepUndo))
            {
                UIManager.Instance?.ShowToast("撤回次数用完了，去商城看看");
                return;
            }

            _engine.BreakCombo();
            var record = _undos.Pop();
            Restore(record);
            RefreshLocks();
            RefreshProgress();
            RefreshToolCounts();
        }

        private void Shuffle()
        {
            if (InputLocked || _matching || _ended)
                return;
            if (_boardTiles.Count < 2)
            {
                UIManager.Instance?.ShowToast("没有可洗的牌");
                return;
            }

            if (!CoinWallet.TryUse(ShopItemId.SheepShuffle))
            {
                UIManager.Instance?.ShowToast("洗牌次数用完了，去商城看看");
                return;
            }

            PushUndo();
            ShuffleBoardIcons();
            RefreshToolCounts();
        }

        private void ShuffleBoardIcons()
        {
            var ids = new List<int>(_boardTiles.Count);
            for (var i = 0; i < _boardTiles.Count; i++)
                ids.Add(_boardTiles[i].IconId);
            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                var tmp = ids[i];
                ids[i] = ids[j];
                ids[j] = tmp;
            }

            for (var i = 0; i < _boardTiles.Count; i++)
                _boardTiles[i].SetIcon(ids[i], IconOf(ids[i]));
        }

        /// <summary>
        /// 道具「暂存位」：本局槽位上限 +1，只在本局有效。
        /// 这是唯一直接缓解「差一张就死」的手段 —— 叠叠消最大的挫败点。
        /// </summary>
        private void ExpandSlot()
        {
            if (InputLocked || _matching || _ended)
                return;
            if (_slotLimit >= MaxSlotLimit)
            {
                UIManager.Instance?.ShowToast("槽位已到上限");
                return;
            }

            if (!CoinWallet.TryUse(ShopItemId.SheepSlot))
            {
                UIManager.Instance?.ShowToast("暂存次数用完了，去商城看看");
                return;
            }

            _slotLimit++;
            _engine.SetSlotLimit(_slotLimit);
            SyncSlotFrames();
            LayoutSlots();
            RefreshToolCounts();
        }

        private void RefreshToolCounts()
        {
            SetToolMark(RemoveButton, ShopItemId.SheepRemove);
            SetToolMark(UndoButton, ShopItemId.SheepUndo);
            SetToolMark(ShuffleButton, ShopItemId.SheepShuffle);
            SetToolMark(SlotButton, ShopItemId.SheepSlot);
        }

        private static void SetToolMark(Button button, ShopItemId id)
        {
            if (button == null)
                return;
            var mark = button.transform.Find("Plus/Mark");
            var text = mark != null ? mark.GetComponent<Text>() : null;
            if (text != null)
                text.text = CoinWallet.Count(id).ToString();
            if (mark != null && CoinWallet.ConsumePendingGrant(id))
            {
                UITween.KillTarget(mark);
                UITween.Pop(mark, 1f, 1.4f, 0.28f);
            }
        }

        private void PushUndo()
        {
            var record = new UndoRecord
            {
                Slots = new List<SlotSnap>(_slots.Count),
                Board = new List<BoardSnap>(_boardTiles.Count),
                SlotIcons = _engine.CaptureSlots()
            };

            for (var i = 0; i < _slots.Count; i++)
                record.Slots.Add(SnapSlot(_slots[i]));
            for (var i = 0; i < _boardTiles.Count; i++)
                record.Board.Add(SnapBoard(_boardTiles[i]));

            var all = GetComponentsInChildren<SheepTileView>(true);
            record.Hidden = new List<HiddenSnap>();
            for (var i = 0; i < all.Length; i++)
            {
                if (!all[i].gameObject.activeSelf)
                    record.Hidden.Add(new HiddenSnap { Tile = all[i], IconId = all[i].IconId });
            }

            _undos.Push(record);
        }

        private void Restore(UndoRecord record)
        {
            _slots.Clear();
            _boardTiles.Clear();

            var all = GetComponentsInChildren<SheepTileView>(true);
            for (var i = 0; i < all.Length; i++)
            {
                // 池子里的空闲棋子不能被这次"全部复活"带回来
                if (all[i] == null || IsPooled(all[i]))
                    continue;
                all[i].gameObject.SetActive(true);
            }

            for (var i = 0; i < record.Hidden.Count; i++)
            {
                if (record.Hidden[i].Tile != null)
                    record.Hidden[i].Tile.gameObject.SetActive(false);
            }

            for (var i = 0; i < record.Board.Count; i++)
            {
                var snap = record.Board[i];
                var tile = snap.Tile;
                if (tile == null)
                    continue;
                tile.InSlot = false;
                tile.gameObject.SetActive(true);
                tile.transform.SetParent(snap.Parent, false);
                var rect = tile.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = snap.Position;
                    rect.sizeDelta = snap.Size;
                    rect.SetSiblingIndex(snap.Sibling);
                }

                tile.SetIcon(snap.IconId, IconOf(snap.IconId));
                _boardTiles.Add(tile);
            }

            for (var i = 0; i < record.Slots.Count; i++)
            {
                var snap = record.Slots[i];
                var tile = snap.Tile;
                if (tile == null)
                    continue;
                tile.gameObject.SetActive(true);
                tile.InSlot = true;
                tile.SetIcon(snap.IconId, IconOf(snap.IconId));
                _slots.Add(tile);
            }

            LayoutSlots();
            _engine.RestoreSlots(record.SlotIcons);
        }

        private void RefreshLocks()
        {
            Canvas.ForceUpdateCanvases();
            ApplyLocks();
            if (_ended || _boardTiles.Count == 0)
                return;
            if (CountUnlocked() > 0)
                return;

            var raise = Mathf.Min(3, _boardTiles.Count);
            for (var i = 0; i < raise; i++)
            {
                var tile = _boardTiles[_boardTiles.Count - 1 - i];
                if (tile != null)
                    tile.transform.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
            ApplyLocks();
            if (CountUnlocked() > 0)
                return;

            for (var i = _boardTiles.Count - 1; i >= 0; i--)
            {
                var tile = _boardTiles[i];
                if (tile == null || !tile.gameObject.activeSelf || tile.InSlot)
                    continue;
                tile.SetLocked(false);
                return;
            }
        }

        private void ApplyLocks()
        {
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var tile = _boardTiles[i];
                if (tile == null || !tile.gameObject.activeSelf || tile.InSlot)
                    continue;
                tile.SetLocked(IsCovered(tile));
            }
        }

        private int CountUnlocked()
        {
            var n = 0;
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var tile = _boardTiles[i];
                if (tile == null || !tile.gameObject.activeSelf || tile.InSlot || tile.Locked)
                    continue;
                n++;
            }

            return n;
        }

        private bool IsCovered(SheepTileView tile)
        {
            var self = CoverRect(tile);
            if (self.width < 1f || self.height < 1f)
                return false;

            const float axis = 0.08f;
            var minW = self.width * axis;
            var minH = self.height * axis;
            for (var i = 0; i < _boardTiles.Count; i++)
            {
                var other = _boardTiles[i];
                if (other == null || other == tile || other.InSlot || !other.gameObject.activeSelf)
                    continue;
                if (!IsDrawnAbove(other.transform, tile.transform))
                    continue;
                var otherRect = CoverRect(other);
                var inter = Intersect(self, otherRect);
                if (inter.width > minW && inter.height > minH)
                    return true;
            }

            return false;
        }

        private static Rect CoverRect(SheepTileView tile)
        {
            if (tile == null)
                return new Rect();
            var face = tile.Face != null ? tile.Face.rectTransform : tile.transform as RectTransform;
            return WorldRect(face);
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return new Rect(0f, 0f, 0f, 0f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            if (rect == null)
                return new Rect();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool IsDrawnAbove(Transform a, Transform b)
        {
            var pathA = HierarchyPath(a);
            var pathB = HierarchyPath(b);
            var n = Mathf.Min(pathA.Count, pathB.Count);
            for (var i = 0; i < n; i++)
            {
                if (pathA[i] != pathB[i])
                    return pathA[i] > pathB[i];
            }

            return pathA.Count > pathB.Count;
        }

        private static List<int> HierarchyPath(Transform t)
        {
            var path = new List<int>(8);
            while (t != null)
            {
                path.Add(t.GetSiblingIndex());
                t = t.parent;
            }

            path.Reverse();
            return path;
        }

        private void RefreshProgress()
        {
            var left = 0;
            var all = GetComponentsInChildren<SheepTileView>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.activeSelf)
                    left++;
            }

            var total = Mathf.Max(1, _totalTiles);
            SetProgress(1f - left / (float)total);
        }

        private void CheckEnd()
        {
            if (_ended)
                return;

            var end = _engine.Evaluate(CountActiveTiles());
            if (end == SheepRoundEnd.Win)
            {
                _ended = true;
                _audio?.PlayWin();
                Won?.Invoke();
                return;
            }

            if (end == SheepRoundEnd.Lose)
            {
                _ended = true;
                _audio?.PlayLose();
                Lost?.Invoke();
            }
        }

        private int CountActiveTiles()
        {
            var left = 0;
            var all = GetComponentsInChildren<SheepTileView>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.activeSelf)
                    left++;
            }

            return left;
        }

        private Sprite IconOf(int iconId)
        {
            if (IconSprites == null || iconId < 0 || iconId >= IconSprites.Length)
                return null;
            return IconSprites[iconId];
        }

        private void ApplyRuntimeFont()
        {
            UIStyle.ApplyTextStyle(this);
        }

        private static SlotSnap SnapSlot(SheepTileView tile)
        {
            return new SlotSnap { Tile = tile, IconId = tile.IconId };
        }

        private static BoardSnap SnapBoard(SheepTileView tile)
        {
            var rect = tile.transform as RectTransform;
            return new BoardSnap
            {
                Tile = tile,
                IconId = tile.IconId,
                Parent = tile.transform.parent,
                Position = rect != null ? rect.anchoredPosition : Vector2.zero,
                Size = rect != null ? rect.sizeDelta : tile.HomeSize,
                Sibling = tile.transform.GetSiblingIndex()
            };
        }

        private struct UndoRecord
        {
            public List<SlotSnap> Slots;
            public List<BoardSnap> Board;
            public List<HiddenSnap> Hidden;
            public int[] SlotIcons;
        }

        private struct SlotSnap
        {
            public SheepTileView Tile;
            public int IconId;
        }

        private struct BoardSnap
        {
            public SheepTileView Tile;
            public int IconId;
            public Transform Parent;
            public Vector2 Position;
            public Vector2 Size;
            public int Sibling;
        }

        private struct HiddenSnap
        {
            public SheepTileView Tile;
            public int IconId;
        }
    }
}
