using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 三消自定义的左侧页签。页签都不选时可以正常交换；
    /// 选中后在右边列出方块、障碍或道具，再点一次同一个页签就退出。
    /// </summary>
    public class Match3CustomBar : MonoBehaviour
    {
        public Button BlockTab;
        public Button ObstacleTab;
        public Button ToolTab;
        public RectTransform PaletteRoot;
        public Match3PaletteCell CellTemplate;
        public Match3BoardPaint Paint;

        private enum Tab
        {
            None = 0,
            Block = 1,
            Obstacle = 2,
            Tool = 3
        }

        private readonly List<Match3PaletteCell> _cells = new List<Match3PaletteCell>(8);
        private Match3GameView _view;
        private Sprite[] _icons;
        private Tab _tab;
        private int _brush = -1;
        private bool _bound;

        public bool Editing => _tab != Tab.None;

        public bool TryGetBrush(out int color, out bool random, out Match3CellKind obstacle, out Match3Special special)
        {
            color = 0;
            random = false;
            obstacle = Match3CellKind.Hole;
            special = Match3Special.None;
            if (_tab == Tab.None || _brush < 0)
                return false;
            if (_tab == Tab.Block)
            {
                if (_brush >= Match3Icons.TileFiles.Length)
                    random = true;
                else
                    color = _brush + 1;
                return true;
            }

            if (_tab == Tab.Tool)
            {
                special = SpecialAt(_brush);
                return special != Match3Special.None;
            }

            obstacle = ObstacleAt(_brush);
            return obstacle != Match3CellKind.Hole;
        }

        public void Bind(Match3GameView view, Sprite[] icons)
        {
            _view = view;
            _icons = icons;
            if (Paint != null)
                Paint.Bind(view);
            if (_bound)
                return;
            _bound = true;
            if (CellTemplate != null)
                CellTemplate.gameObject.SetActive(false);
            if (BlockTab != null)
                BlockTab.onClick.AddListener(OnBlock);
            if (ObstacleTab != null)
                ObstacleTab.onClick.AddListener(OnObstacle);
            if (ToolTab != null)
                ToolTab.onClick.AddListener(OnTool);
            ClearTab();
        }

        private void OnBlock()
        {
            if (_tab == Tab.Block)
            {
                ClearTab();
                return;
            }

            _tab = Tab.Block;
            _brush = -1;
            ShowBlocks();
            ApplyTabLatch();
            SetEditing(true);
        }

        private void OnObstacle()
        {
            if (_tab == Tab.Obstacle)
            {
                ClearTab();
                return;
            }

            _tab = Tab.Obstacle;
            _brush = -1;
            ShowObstacles();
            ApplyTabLatch();
            SetEditing(true);
        }

        private void OnTool()
        {
            if (_tab == Tab.Tool)
            {
                ClearTab();
                return;
            }

            _tab = Tab.Tool;
            _brush = -1;
            ShowTools();
            ApplyTabLatch();
            SetEditing(true);
        }

        private void ClearTab()
        {
            _tab = Tab.None;
            _brush = -1;
            ClearCells();
            ApplyTabLatch();
            SetEditing(false);
        }

        private void SetEditing(bool editing)
        {
            if (Paint != null)
                Paint.SetListening(editing);
            if (_view != null)
                _view.ResetIdle();
        }

        private void ShowBlocks()
        {
            ClearCells();
            var count = Match3Icons.TileFiles.Length;
            for (var i = 0; i < count; i++)
            {
                var cell = Spawn(i, false);
                if (cell.Icon != null)
                {
                    cell.Icon.enabled = true;
                    cell.Icon.sprite = Match3Icons.TileOf(_icons, i + 1);
                }

                if (cell.Label != null)
                    cell.Label.gameObject.SetActive(false);
            }

            var random = Spawn(count, true);
            if (random.Icon != null)
                random.Icon.enabled = false;
            if (random.Label != null)
            {
                random.Label.gameObject.SetActive(true);
                random.Label.text = "随机图标";
            }
        }

        private void ShowObstacles()
        {
            ClearCells();
            var kinds = new[]
            {
                Match3CellKind.Ice,
                Match3CellKind.Stone,
                Match3CellKind.Jelly,
                Match3CellKind.Cage
            };
            for (var i = 0; i < kinds.Length; i++)
            {
                var cell = Spawn(i, false);
                if (cell.Icon != null)
                {
                    cell.Icon.enabled = true;
                    cell.Icon.sprite = Match3Icons.OverlayOf(_icons, kinds[i]);
                }

                if (cell.Label != null)
                    cell.Label.gameObject.SetActive(false);
            }
        }

        private void ShowTools()
        {
            ClearCells();
            for (var i = 0; i < 4; i++)
            {
                var cell = Spawn(i, false);
                var sprite = ToolSprite(i);
                if (cell.Icon != null)
                {
                    cell.Icon.enabled = sprite != null;
                    cell.Icon.sprite = sprite;
                }

                if (cell.Label != null)
                {
                    cell.Label.gameObject.SetActive(sprite == null);
                    if (sprite == null)
                        cell.Label.text = ToolLabel(i);
                }
            }
        }

        private Match3PaletteCell Spawn(int index, bool wide)
        {
            var cell = Instantiate(CellTemplate, PaletteRoot, false);
            cell.gameObject.SetActive(true);
            cell.name = wide ? "Random" : "Brush" + index;
            if (cell.Icon != null)
                cell.Icon.preserveAspect = true;
            var rect = cell.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = wide ? new Vector2(160f, 64f) : new Vector2(72f, 72f);
                rect.anchoredPosition = new Vector2(0f, -index * 74f);
            }

            var captured = index;
            if (cell.Button != null)
                cell.Button.onClick.AddListener(() => SelectBrush(captured));
            Latch(cell, false);
            _cells.Add(cell);
            return cell;
        }

        private void SelectBrush(int index)
        {
            _brush = _brush == index ? -1 : index;
            for (var i = 0; i < _cells.Count; i++)
                Latch(_cells[i], i == _brush);
        }

        private void ClearCells()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null)
                    Destroy(_cells[i].gameObject);
            }

            _cells.Clear();
        }

        private void ApplyTabLatch()
        {
            LatchButton(BlockTab, _tab == Tab.Block);
            LatchButton(ObstacleTab, _tab == Tab.Obstacle);
            LatchButton(ToolTab, _tab == Tab.Tool);
        }

        private static void LatchButton(Button button, bool pressed)
        {
            if (button == null)
                return;
            button.transform.localScale = pressed ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;
            var image = button.targetGraphic as Image;
            if (image == null)
                return;
            image.color = pressed ? button.colors.pressedColor : Color.white;
        }

        private static void Latch(Match3PaletteCell cell, bool pressed)
        {
            if (cell == null)
                return;
            cell.transform.localScale = pressed ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;
            if (cell.Background == null)
                return;
            var pressedColor = cell.Button != null ? cell.Button.colors.pressedColor : new Color(0.78f, 0.78f, 0.78f, 1f);
            cell.Background.color = pressed ? pressedColor : Color.white;
        }

        private static Match3CellKind ObstacleAt(int index)
        {
            switch (index)
            {
                case 0: return Match3CellKind.Ice;
                case 1: return Match3CellKind.Stone;
                case 2: return Match3CellKind.Jelly;
                case 3: return Match3CellKind.Cage;
                default: return Match3CellKind.Hole;
            }
        }

        private static Match3Special SpecialAt(int index)
        {
            switch (index)
            {
                case 0: return Match3Special.LineH;
                case 1: return Match3Special.LineV;
                case 2: return Match3Special.Bomb;
                case 3: return Match3Special.ColorBomb;
                default: return Match3Special.None;
            }
        }

        private static string ToolLabel(int index)
        {
            switch (index)
            {
                case 0: return "横消";
                case 1: return "竖消";
                default: return string.Empty;
            }
        }

        private Sprite ToolSprite(int index)
        {
            if (index == 2)
                return Match3Icons.FromFile(_icons, Match3Icons.BombFile);
            if (index == 3)
                return Match3Icons.FromFile(_icons, 19);
            return null;
        }
    }
}
