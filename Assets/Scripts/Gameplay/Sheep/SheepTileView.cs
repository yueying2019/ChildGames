using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class SheepTileView : MonoBehaviour
    {
        public Image Thickness;
        public Image Face;
        public Image Icon;
        public Image Dim;
        public Button Button;

        [SerializeField] private int _iconId = -1;
        [SerializeField] private int _layer;

        public int IconId => _iconId;
        public int Layer => _layer;
        public bool Locked { get; private set; }
        public bool InSlot { get; set; }
        public Transform HomeParent { get; private set; }
        public Vector2 HomePosition { get; private set; }
        public int HomeSibling { get; private set; }
        public Vector2 HomeSize { get; private set; }

        public void RememberHome()
        {
            var rect = transform as RectTransform;
            HomeParent = transform.parent;
            HomePosition = rect != null ? rect.anchoredPosition : Vector2.zero;
            HomeSize = rect != null ? rect.sizeDelta : new Vector2(96f, 104f);
            HomeSibling = transform.GetSiblingIndex();
        }

        public void Setup(int iconId, Sprite icon, int layer, bool locked)
        {
            _iconId = iconId;
            _layer = layer;
            ApplyIcon(icon);
            SetLocked(locked);
        }

        public void RestoreFromPrefab(Sprite[] icons)
        {
            if (_iconId < 0)
                ParseIconFromName();
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
                Button = GetComponentInChildren<Button>(true);
        }

        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (Dim != null)
                Dim.enabled = locked;
            if (Button == null)
                Button = GetComponentInChildren<Button>(true);
            if (Button != null)
                Button.interactable = !locked;
            if (Face != null)
                Face.raycastTarget = !locked;
        }

        public void SetIcon(int iconId, Sprite icon)
        {
            _iconId = iconId;
            ApplyIcon(icon);
        }

        private void ApplyIcon(Sprite icon)
        {
            if (Icon == null)
                return;
            Icon.sprite = icon;
            Icon.enabled = icon != null;
            Icon.preserveAspect = true;
        }

        private void ParseIconFromName()
        {
            var parts = name.Split('_');
            if (parts.Length < 3)
                return;
            if (int.TryParse(parts[parts.Length - 1], out var id))
                _iconId = id;
        }
    }
}
