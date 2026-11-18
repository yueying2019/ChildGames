using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 对对消自定义的左侧页签。都不选时按连线规则玩；
    /// 选中后列出图案或挡墙，再点一次同一个页签就退出。
    /// </summary>
    public class LianliankanCustomBar : MonoBehaviour
    {
        public Button BlockTab;
        public Button ObstacleTab;
        public RectTransform PaletteRoot;
        public Match3PaletteCell CellTemplate;
        public LianliankanBoardPaint Paint;

        private enum Tab
        {
            None = 0,
            Block = 1,
            Obstacle = 2
        }

        private readonly List<Match3PaletteCell> _cells = new List<Match3PaletteCell>(16);
        private LianliankanGameView _view;
        private Sprite[] _icons;
        private Tab _tab;
        private int _brush = -1;
        private bool _bound;

        public bool Editing => _tab != Tab.None;

        public bool TryGetBrush(out int iconId, out bool random, out bool blocker)
        {
            iconId = -1;
            random = false;
            blocker = false;
            if (_tab == Tab.None || _brush < 0)
                return false;
            if (_tab == Tab.Obstacle)
            {
                blocker = true;
                return true;
            }

            var count = PlayableCount();
            if (_brush >= count)
                random = true;
            else
                iconId = FirstIcon() + _brush;
            return true;
        }

        public void Bind(LianliankanGameView view, Sprite[] icons)
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

        private void SetEditing(bool editing)
        {
            if (Paint != null)
                Paint.SetListening(editing);
        }

        private void ClearTab()
        {
            _tab = Tab.None;
            _brush = -1;
            ClearCells();
            ApplyTabLatch();
            SetEditing(false);
        }

        private void ShowBlocks()
        {
            ClearCells();
            var count = PlayableCount();
            var first = FirstIcon();
            for (var i = 0; i < count; i++)
            {
                var cell = Spawn(i, false);
                if (cell.Icon != null)
                {
                    cell.Icon.enabled = true;
                    cell.Icon.sprite = IconAt(first + i);
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
            var cell = Spawn(0, false);
            if (cell.Icon != null)
            {
                cell.Icon.enabled = true;
                cell.Icon.sprite = IconAt(LianliankanLevelData.BlockerIconIndex);
            }

            if (cell.Label != null)
                cell.Label.gameObject.SetActive(false);
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
                rect.sizeDelta = wide ? new Vector2(168f, 64f) : new Vector2(60f, 60f);
                rect.anchoredPosition = new Vector2(0f, -index * 64f);
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
        }

        private int PlayableCount()
        {
            var icons = _icons != null ? _icons.Length : 0;
            var cap = LianliankanLevelData.PlayableIconCap(icons);
            if (cap > LianliankanLevelData.MaxTypes)
                cap = LianliankanLevelData.MaxTypes;
            return cap > 0 ? cap : 0;
        }

        private int FirstIcon()
        {
            var icons = _icons != null ? _icons.Length : 0;
            return icons > LianliankanLevelData.FirstPlayableIconIndex
                ? LianliankanLevelData.FirstPlayableIconIndex
                : 0;
        }

        private Sprite IconAt(int index)
        {
            if (_icons == null || index < 0 || index >= _icons.Length)
                return null;
            return _icons[index];
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
    }
}
