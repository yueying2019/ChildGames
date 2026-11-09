using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class LevelSelectPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Text TitleText;
        public RectTransform GridRoot;
        public LevelSelectCellView CellPrefab;

        private readonly List<LevelSelectCellView> _cells = new List<LevelSelectCellView>(16);
        private GameModeType _mode;
        private bool _jieqiEndgames;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, CloseSelf);
            Wire(CloseButton, CloseSelf);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            _mode = payload is GameModeType mode ? mode : GameModeType.Lianliankan;
            _jieqiEndgames = false;
            RefreshTitle();
            Rebuild();
            AdDirector.ShowLobbyBanner();
        }

        private void Update()
        {
            if (!IsOpen)
                return;
        }

        private void CloseSelf()
        {
            if (_mode == GameModeType.Jieqi && _jieqiEndgames)
            {
                _jieqiEndgames = false;
                RefreshTitle();
                Rebuild();
                return;
            }

            UIManager.Instance.Close(UIPanelId.LevelSelect);
        }

        private void RefreshTitle()
        {
            if (TitleText == null)
                return;

            // 顺带展示本玩法的收集进度，把「补星」变成看得见的目标。
            if (_mode == GameModeType.Jieqi && _jieqiEndgames)
            {
                TitleText.text = "残局";
                return;
            }

            var title = GameModeCatalog.Get(_mode).Title;
            var capacity = StarCatalog.Capacity(_mode);
            TitleText.text = capacity > 0
                ? $"{title}  {ProgressRepository.TotalStars(_mode)}/{capacity} 星"
                : title;
        }

        private void Rebuild()
        {
            if (GridRoot == null || CellPrefab == null)
                return;

            CellPrefab.gameObject.SetActive(false);
            if (_mode == GameModeType.Jieqi)
            {
                RebuildJieqi();
                return;
            }

            if (_mode == GameModeType.SkillXiangqi)
            {
                RebuildSkillXiangqi();
                return;
            }

            ApplyGridLayout();
            // 教学关、每日挑战占列表前两格，正式关和无尽整体后移。
            var hasTutorial = TutorialCatalog.Supports(_mode);
            var hasDaily = DailyCatalog.Supports(_mode);
            var campaign = EndlessCatalog.SelectCount(_mode);
            var prefix = (hasTutorial ? 1 : 0) + (hasDaily ? 1 : 0);
            var count = campaign + prefix;
            var dailySlot = hasTutorial ? 1 : 0;
            EnsureCells(count);
            var lastId = ProgressRepository.LastPlayedId(_mode);
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (i >= count)
                {
                    cell.gameObject.SetActive(false);
                    continue;
                }

                cell.gameObject.SetActive(true);
                if (hasTutorial && i == 0)
                {
                    cell.BindTutorial(
                        TutorialCatalog.IsCleared(_mode),
                        () => GameManager.Instance.StartGame(_mode, TutorialCatalog.Index));
                    continue;
                }

                if (hasDaily && i == dailySlot)
                {
                    var today = DailyCatalog.TodayIndex(_mode);
                    var dailyUnlocked = DailyCatalog.IsUnlocked(_mode);
                    cell.BindDaily(
                        dailyUnlocked,
                        ProgressRepository.HasDailyReward(_mode),
                        lastId == DailyCatalog.LevelId,
                        LevelBrowse.Title(_mode, today),
                        () => GameManager.Instance.StartGame(_mode, DailyCatalog.Index));
                    continue;
                }

                var index = i - prefix;
                if (EndlessCatalog.IsIndex(_mode, index))
                {
                    var unlocked = EndlessCatalog.IsUnlocked(_mode);
                    cell.BindEndless(
                        unlocked,
                        lastId == EndlessCatalog.LevelId,
                        ProgressRepository.EndlessBest(_mode),
                        () => GameManager.Instance.StartGame(_mode, index));
                    continue;
                }
                var levelId = LevelBrowse.LevelId(_mode, index);
                var unlockedLevel = ProgressRepository.IsUnlocked(_mode, levelId);
                var cleared = ProgressRepository.IsCleared(_mode, levelId);
                var reward = ProgressRepository.HasClearReward(_mode, levelId)
                    ? ShopCatalog.ReplayClearCoins
                    : ShopCatalog.CoinsForClear(levelId);
                cell.Bind(
                    levelId,
                    LevelBrowse.Title(_mode, index),
                    unlockedLevel,
                    cleared,
                    levelId == lastId,
                    reward,
                    ProgressRepository.Stars(_mode, levelId),
                    () => GameManager.Instance.StartGame(_mode, index));
            }
        }

        private void RebuildJieqi()
        {
            if (_jieqiEndgames)
            {
                RebuildJieqiEndgames();
                return;
            }

            ApplyGridLayout();
            EnsureCells(3);
            for (var i = 3; i < _cells.Count; i++)
                _cells[i].gameObject.SetActive(false);

            _cells[0].gameObject.SetActive(true);
            _cells[1].gameObject.SetActive(true);
            _cells[2].gameObject.SetActive(true);
            _cells[0].BindPlain("双人", "面对面", "两人轮流走",
                () => GameManager.Instance.StartGame(GameModeType.Jieqi, 0));
            _cells[1].BindPlain("人机", "对电脑", "你执红先走",
                () => GameManager.Instance.StartGame(GameModeType.Jieqi, 1));
            _cells[2].BindPlain("残局", "固定局面", "红先走", ShowJieqiEndgames);
        }

        private void RebuildSkillXiangqi()
        {
            ApplyGridLayout();
            EnsureCells(2);
            for (var i = 2; i < _cells.Count; i++)
                _cells[i].gameObject.SetActive(false);

            _cells[0].gameObject.SetActive(true);
            _cells[1].gameObject.SetActive(true);
            _cells[0].BindPlain("双人", "面对面", "两人轮流走",
                () => GameManager.Instance.StartGame(GameModeType.SkillXiangqi, 0));
            _cells[1].BindPlain("人机", "对电脑", "你执红先走",
                () => GameManager.Instance.StartGame(GameModeType.SkillXiangqi, 1));
        }

        private void ShowJieqiEndgames()
        {
            _jieqiEndgames = true;
            RefreshTitle();
            Rebuild();
        }

        private void RebuildJieqiEndgames()
        {
            ApplyGridLayout();
            var count = JieqiEndgameCatalog.Count;
            EnsureCells(count);
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (i >= count)
                {
                    cell.gameObject.SetActive(false);
                    continue;
                }

                cell.gameObject.SetActive(true);
                var index = i;
                cell.BindPlain(
                    (index + 1).ToString(),
                    JieqiEndgameCatalog.Title(index),
                    JieqiEndgameCatalog.Hint(index),
                    () => GameManager.Instance.StartGame(
                        GameModeType.Jieqi,
                        JieqiEndgameCatalog.LevelIndexBase + index));
                cell.SetStars(ProgressRepository.JieqiEndgameStars(index));
            }
        }

        private void ApplyGridLayout()
        {
            if (GridRoot == null)
                return;
            var grid = GridRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;

            grid.constraintCount = 4;
            grid.cellSize = new Vector2(188f, 176f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(8, 8, 8, 8);
        }

        private void EnsureCells(int count)
        {
            while (_cells.Count < count)
            {
                var cell = Instantiate(CellPrefab, GridRoot);
                cell.transform.SetAsLastSibling();
                _cells.Add(cell);
            }
        }
    }
}
