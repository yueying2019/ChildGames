using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 对对消单格：底图 + 图标 + 选中框。行列和图案必须序列化，否则预制体实例化后全是默认值。
    /// </summary>
    public class LianliankanTileView : MonoBehaviour
    {
        public Image Background;
        public Image Icon;
        public Image SelectFrame;
        public Button Button;

        [SerializeField] private int _row;
        [SerializeField] private int _col;
        [SerializeField] private int _iconId = -1;
        [SerializeField] private bool _occupied;
        [SerializeField] private bool _blocked;

        public int Row => _row;
        public int Col => _col;
        public int IconId => _iconId;
        public bool Occupied => _occupied;
        public bool Blocked => _blocked;

        public void Setup(int row, int col, bool occupied, Sprite bg, Sprite icon, int iconId)
        {
            _row = row;
            _col = col;
            _blocked = false;
            _occupied = occupied;
            _iconId = occupied ? iconId : -1;
            ApplyVisual(bg, icon);
        }

        public void SetupBlocker(int row, int col, Sprite bg, Sprite icon)
        {
            _row = row;
            _col = col;
            _blocked = true;
            _occupied = false;
            _iconId = LianliankanLevelData.BlockerIconIndex;
            ApplyVisual(bg, icon);
        }

        public void RestoreFromPrefab(Sprite[] icons)
        {
            ParseName();
            if (_blocked)
            {
                _occupied = false;
                if (Button == null)
                    Button = GetComponent<Button>();
                if (Button != null)
                    Button.interactable = false;
                return;
            }

            if (!_occupied && Background != null)
                _occupied = Background.enabled;
            if (_iconId < 0 && Icon != null && Icon.sprite != null && icons != null)
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    if (icons[i] == Icon.sprite)
                    {
                        _iconId = i;
                        break;
                    }
                }
            }

            if (Button == null)
                Button = GetComponent<Button>();
            if (Button != null)
                Button.interactable = _occupied;
        }

        public void ApplyState(bool occupied, int iconId, Sprite bg, Sprite icon)
        {
            _blocked = false;
            _occupied = occupied;
            _iconId = occupied ? iconId : -1;
            ApplyVisual(bg, icon);
        }

        public void SetIcon(int iconId, Sprite sprite)
        {
            _iconId = iconId;
            if (Icon == null)
                return;
            Icon.sprite = sprite;
            Icon.enabled = _occupied && sprite != null;
        }

        public void SetSelected(bool selected, Sprite selectSprite = null)
        {
            if (SelectFrame == null)
                return;
            if (selectSprite != null)
                SelectFrame.sprite = selectSprite;
            SelectFrame.enabled = selected && _occupied;
        }

        public void Clear()
        {
            _blocked = false;
            _occupied = false;
            _iconId = -1;
            ResetMotion();
            if (Background != null)
                Background.enabled = false;
            if (Icon != null)
                Icon.enabled = false;
            if (Button != null)
                Button.interactable = false;
            SetSelected(false);
        }

        public void ResetMotion()
        {
            transform.localScale = Vector3.one;
            if (Background != null)
            {
                var c = Background.color;
                c.a = 1f;
                Background.color = c;
            }

            if (Icon != null)
            {
                var c = Icon.color;
                c.a = 1f;
                Icon.color = c;
            }
        }

        private void ApplyVisual(Sprite bg, Sprite icon)
        {
            ResetMotion();
            var show = _occupied || _blocked;
            if (Background != null)
            {
                if (bg != null)
                    Background.sprite = bg;
                Background.enabled = show;
            }

            if (Icon != null)
            {
                Icon.sprite = icon;
                Icon.enabled = show && icon != null;
                Icon.preserveAspect = true;
            }

            if (Button == null)
                Button = GetComponent<Button>();
            if (Button != null)
                Button.interactable = _occupied && !_blocked;
            SetSelected(false);
        }

        private void ParseName()
        {
            var parts = name.Split('_');
            if (parts.Length < 3)
                return;
            if (int.TryParse(parts[1], out var r) && int.TryParse(parts[2], out var c))
            {
                _row = r;
                _col = c;
            }
        }
    }
}
