using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 强制引导的显示层：挖洞遮罩（洞内可点、洞外拦截）+ 底部文案条 + 跳过。
    /// 运行时创建，不落预制体：它跟着教学关走，没有复用场景，做预制体反而要多维护一份引用。
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private const float BarHeight = 132f;
        private const float HintSeconds = 1.4f;

        private RectTransform _root;
        private GuideHighlight _highlight;
        private Text _descText;
        private Button _skipButton;
        private float _hintUntil;

        /// <summary>点了洞外（不允许操作的地方）。</summary>
        public event Action OutsideClicked;

        /// <summary>点了跳过。</summary>
        public event Action Skipped;

        public static TutorialOverlay Attach(RectTransform parent)
        {
            if (parent == null)
                return null;

            var go = new GameObject("TutorialOverlay", typeof(RectTransform));
            var self = go.AddComponent<TutorialOverlay>();
            var root = go.transform as RectTransform;
            root.SetParent(parent, false);
            Stretch(root);
            self._root = root;
            self.Build();
            return self;
        }

        /// <summary>应用一步：设置文案、挖洞。targets 为空则只显示文案（这一步不限制操作）。</summary>
        public void Apply(TutorialStep step, RectTransform[] targets)
        {
            if (_descText != null)
            {
                _descText.text = step != null ? step.Desc : string.Empty;
                _descText.color = UIStyle.TextMain;
            }

            if (_highlight == null)
                return;

            var hasTarget = targets != null && targets.Length > 0 && targets[0] != null;
            if (!hasTarget || step == null || step.Highlight == null || step.Highlight.Length == 0)
            {
                _highlight.Hide();
                return;
            }

            _highlight.SetPassThrough(step.HasAllowed);
            _highlight.Show(targets, GuideHighlightShape.RoundRect);
        }

        /// <summary>点错地方时的短提示，会覆盖文案一会儿再还原。</summary>
        public void ShowHint(string message)
        {
            if (_descText == null || string.IsNullOrEmpty(message))
                return;

            _descText.text = message;
            _descText.color = UIStyle.Danger;
            _hintUntil = Time.unscaledTime + HintSeconds;
        }

        public void RestoreDesc(TutorialStep step)
        {
            if (_descText == null || step == null)
                return;
            _descText.text = step.Desc;
            _descText.color = UIStyle.TextMain;
            _hintUntil = 0f;
        }

        private void Update()
        {
            if (_hintUntil > 0f && Time.unscaledTime >= _hintUntil)
            {
                _hintUntil = 0f;
                if (_descText != null)
                    _descText.color = UIStyle.TextMain;
            }
        }

        private void OnDestroy()
        {
            if (_highlight != null)
                _highlight.Hide();
            if (_skipButton != null)
                _skipButton.onClick.RemoveAllListeners();
        }

        private void Build()
        {
            // 挖洞层放最底（GuideHighlight 内部会 SetAsFirstSibling），洞外点击由它回调
            _highlight = GuideHighlight.Attach(_root, UIStyle.Overlay, OnOutside);
            if (_highlight != null)
                _highlight.SetPassThrough(true);

            var bar = CreateRect("Bar", _root);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(24f, 24f);
            bar.offsetMax = new Vector2(-24f, 24f + BarHeight);

            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = new Color(UIStyle.BgPanel.r, UIStyle.BgPanel.g, UIStyle.BgPanel.b, 0.96f);
            bg.raycastTarget = false;

            var textRect = CreateRect("Desc", bar);
            Stretch(textRect, 140f, 24f, 24f, 24f);
            var text = textRect.gameObject.AddComponent<Text>();
            UIStyle.ApplyTextStyle(text);
            text.supportRichText = true;
            text.fontSize = UIStyle.BodySize + 2;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = UIStyle.TextMain;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            _descText = text;

            var skipRect = CreateRect("Skip", bar);
            skipRect.anchorMin = new Vector2(1f, 0.5f);
            skipRect.anchorMax = new Vector2(1f, 0.5f);
            skipRect.pivot = new Vector2(1f, 0.5f);
            skipRect.anchoredPosition = new Vector2(-24f, 0f);
            skipRect.sizeDelta = new Vector2(160f, 76f);

            var skipImage = skipRect.gameObject.AddComponent<Image>();
            skipImage.color = UIStyle.BgCard;
            var skip = skipRect.gameObject.AddComponent<Button>();
            skip.targetGraphic = skipImage;
            skip.onClick.AddListener(OnSkip);
            _skipButton = skip;

            var skipTextRect = CreateRect("Label", skipRect);
            Stretch(skipTextRect);
            var skipText = skipTextRect.gameObject.AddComponent<Text>();
            UIStyle.ApplyTextStyle(skipText);
            skipText.fontSize = UIStyle.CaptionSize;
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.color = UIStyle.TextMuted;
            skipText.text = "跳过教学";
            skipText.raycastTarget = false;
        }

        private void OnOutside()
        {
            OutsideClicked?.Invoke();
        }

        private void OnSkip()
        {
            Skipped?.Invoke();
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
            Stretch(rect, 0f, 0f, 0f, 0f);
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
