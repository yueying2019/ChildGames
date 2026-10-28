using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 引导高亮：在最上层铺一层半透明遮罩，在目标周围"挖洞"，再描一圈会呼吸的光边。
    /// 用四块拉伸的 Image 拼出中间的透明区域，因此不需要额外 shader / mask。
    /// 用法：GuideHighlight.Attach(parent) 拿到实例，Show(target, shape) / Hide()。
    /// </summary>
    public sealed class GuideHighlight : MonoBehaviour
    {
        public const float Padding = 14f;
        public const float EdgeThickness = 6f;
        private const float PulsePeriod = 0.9f;
        private const float PulseMinAlpha = 0.35f;

        private static Sprite _ringSprite;

        private RectTransform _root;
        private RectTransform[] _blocks = new RectTransform[4];
        private readonly Graphic[] _blockImages = new Graphic[4];
        private readonly Graphic[] _edges = new Graphic[4];
        private Graphic _catcher;
        private RectTransform _ring;
        private Image _ringImage;
        private RectTransform[] _targets;
        private GuideHighlightShape _shape;
        private TweenHandle _pulse;
        private bool _visible;

        public bool Visible => _visible;

        /// <summary>
        /// 洞内可穿透：关掉全屏 Catcher，改由四块遮罩吃点击。
        /// 教学关的强制引导要玩家能点到被挖出来的格子，同时点洞外要有反馈，所以必须开这个。
        /// 关掉时是老行为（整屏吃点击，点哪都算"继续"）。
        /// </summary>
        public void SetPassThrough(bool on)
        {
            if (_catcher != null)
                _catcher.raycastTarget = !on;

            for (var i = 0; i < _blockImages.Length; i++)
            {
                if (_blockImages[i] != null)
                    _blockImages[i].raycastTarget = on;
            }
        }

        /// <summary>在 full-stretch 的宿主（一般是引导面板根节点）上挂一个高亮层。</summary>
        public static GuideHighlight Attach(RectTransform parent, Color dim, UnityAction onClickOutside)
        {
            if (parent == null)
                return null;

            var go = new GameObject("GuideHighlight", typeof(RectTransform));
            var self = go.AddComponent<GuideHighlight>();
            var root = go.transform as RectTransform;
            root.SetParent(parent, false);
            root.SetAsFirstSibling();
            Stretch(root);

            self._root = root;
            self.Build(dim, onClickOutside);
            return self;
        }

        public void Show(RectTransform target, GuideHighlightShape shape)
        {
            Show(target != null ? new[] { target } : null, shape);
        }

        /// <summary>多目标时挖一个包住所有目标的大洞（教学关要同时露出要交换的两格）。</summary>
        public void Show(RectTransform[] targets, GuideHighlightShape shape)
        {
            _targets = targets;
            _shape = shape;
            if (targets == null || targets.Length == 0 || targets[0] == null || shape == GuideHighlightShape.None)
            {
                Hide();
                return;
            }

            SetVisible(true);
            Layout();
            PlayPulse();
        }

        public void Hide()
        {
            _targets = null;
            StopPulse();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_visible)
                return;
            if (_targets == null || _targets.Length == 0 || _targets[0] == null)
            {
                Hide();
                return;
            }

            // 棋盘重建 / 目标滚动时跟着走
            Layout();
        }

        private void OnDisable()
        {
            StopPulse();
        }

        private void SetVisible(bool on)
        {
            _visible = on;
            if (_root == null)
                return;
            if (_root.gameObject.activeSelf != on)
                _root.gameObject.SetActive(on);
        }

        private void Build(Color dim, UnityAction onClickOutside)
        {
            // 兜底点击层：任何位置点击都走同一个回调（"点任意处继续"）
            _catcher = AddButton(CreateRect("Catcher", _root), Color.clear, onClickOutside);

            var ringFs = CreateRect("MaskRoot", _root);
            Stretch(ringFs);

            for (var i = 0; i < 4; i++)
            {
                var block = CreateRect(i == 0 ? "MaskTop" : i == 1 ? "MaskBottom" : i == 2 ? "MaskLeft" : "MaskRight", ringFs);
                var image = block.gameObject.AddComponent<Image>();
                image.color = dim;
                // 平时不挡射线；SetPassThrough(true) 后改由这四块吃点击，洞内才能穿透到棋盘。
                image.raycastTarget = false;
                _blockImages[i] = image;
                var blocker = block.gameObject.AddComponent<Button>();
                blocker.targetGraphic = image;
                blocker.transition = Selectable.Transition.None;
                if (onClickOutside != null)
                    blocker.onClick.AddListener(onClickOutside);
                _blocks[i] = block;
            }

            var edges = CreateRect("EdgeRoot", _root);
            Stretch(edges);
            for (var i = 0; i < 4; i++)
            {
                var edge = CreateRect("Edge" + i, edges);
                var image = edge.gameObject.AddComponent<Image>();
                image.color = UIStyle.Accent;
                image.raycastTarget = false;
                _edges[i] = image;
            }

            var ringRect = CreateRect("CircleRing", _root);
            var ringImage = ringRect.gameObject.AddComponent<Image>();
            ringImage.sprite = RingSprite();
            ringImage.color = UIStyle.Accent;
            ringImage.raycastTarget = false;
            ringImage.preserveAspect = true;
            _ring = ringRect;
            _ringImage = ringImage;

            SetVisible(false);
        }

        private void Layout()
        {
            if (_root == null || _targets == null || _targets.Length == 0 || _targets[0] == null)
                return;

            var rect = _root.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var corners = new Vector3[4];
            for (var t = 0; t < _targets.Length; t++)
            {
                if (_targets[t] == null)
                    continue;
                _targets[t].GetWorldCorners(corners);
                for (var i = 0; i < 4; i++)
                {
                    var local = _root.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }

            if (min.x > max.x)
            {
                Hide();
                return;
            }

            min -= Vector2.one * Padding;
            max += Vector2.one * Padding;
            min.x = Mathf.Clamp(min.x, rect.xMin, rect.xMax);
            min.y = Mathf.Clamp(min.y, rect.yMin, rect.yMax);
            max.x = Mathf.Clamp(max.x, rect.xMin, rect.xMax);
            max.y = Mathf.Clamp(max.y, rect.yMin, rect.yMax);

            var x0 = Mathf.Clamp01((min.x - rect.xMin) / rect.width);
            var x1 = Mathf.Clamp01((max.x - rect.xMin) / rect.width);
            var y0 = Mathf.Clamp01((min.y - rect.yMin) / rect.height);
            var y1 = Mathf.Clamp01((max.y - rect.yMin) / rect.height);

            SetAnchors(_blocks[0], new Vector2(0f, y1), new Vector2(1f, 1f));
            SetAnchors(_blocks[1], new Vector2(0f, 0f), new Vector2(1f, y0));
            SetAnchors(_blocks[2], new Vector2(0f, y0), new Vector2(x0, y1));
            SetAnchors(_blocks[3], new Vector2(x1, y0), new Vector2(1f, y1));

            SetEdge(_edges[0], new Vector2(x0, y1), new Vector2(x1, y1));
            SetEdge(_edges[1], new Vector2(x0, y0), new Vector2(x1, y0));
            SetEdge(_edges[2], new Vector2(x0, y0), new Vector2(x0, y1));
            SetEdge(_edges[3], new Vector2(x1, y0), new Vector2(x1, y1));

            var useRing = _shape == GuideHighlightShape.Circle;
            if (_ring != null && _ring.gameObject.activeSelf != useRing)
                _ring.gameObject.SetActive(useRing);

            if (useRing && _ring != null)
            {
                var centerX = rect.xMin + (x0 + x1) * 0.5f * rect.width;
                var centerY = rect.yMin + (y0 + y1) * 0.5f * rect.height;
                var diameter = Mathf.Max(max.x - min.x, max.y - min.y) * 1.06f;
                _ring.pivot = new Vector2(0.5f, 0.5f);
                _ring.anchorMin = new Vector2(0.5f, 0.5f);
                _ring.anchorMax = new Vector2(0.5f, 0.5f);
                _ring.anchoredPosition = new Vector2(centerX - (rect.xMin + rect.width * 0.5f), centerY - (rect.yMin + rect.height * 0.5f));
                _ring.sizeDelta = new Vector2(diameter, diameter);
            }
        }

        private void PlayPulse()
        {
            StopPulse();
            _pulse = UITween.Custom(this, PulsePeriod, u =>
            {
                var alpha = Mathf.Lerp(PulseMinAlpha, 1f, Mathf.PingPong(u * 2f, 1f));
                ApplyEdgeAlpha(alpha);
            }, EaseKind.Linear)
                .SetUnscaled(true)
                .OnComplete(PlayPulse);
        }

        private void StopPulse()
        {
            if (_pulse == null)
                return;
            _pulse.Kill();
            _pulse = null;
        }

        private void ApplyEdgeAlpha(float alpha)
        {
            for (var i = 0; i < _edges.Length; i++)
            {
                if (_edges[i] == null)
                    continue;
                _edges[i].color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, alpha);
            }

            if (_ringImage != null)
                _ringImage.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, alpha);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Graphic AddButton(RectTransform rect, Color color, UnityAction action)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Stretch(rect);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (action != null)
                button.onClick.AddListener(action);
            return image;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetEdge(Graphic graphic, Vector2 min, Vector2 max)
        {
            if (graphic == null)
                return;
            var rect = graphic.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, EdgeThickness);
            if (min.x == max.x)
                rect.sizeDelta = new Vector2(EdgeThickness, 0f);
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f;
            var inner = outer - size * 0.05f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = 0f;
                    if (dist <= outer && dist >= inner)
                        alpha = Mathf.Clamp01((dist - inner) / Mathf.Max(1f, outer - inner));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            _ringSprite.name = "GuideRing";
            return _ringSprite;
        }
    }
}
