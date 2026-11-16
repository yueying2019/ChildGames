using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 车吃子时从屏下冲向远处。棋步已经落下，这段只盖在棋盘上面，不挡点击。
    /// </summary>
    public class JieqiRookCaptureFx : MonoBehaviour
    {
        public RectTransform Chariot;
        public Image[] Dust;
        public JieqiCaptureBackdrop Backdrop;

        public const float Duration = 2f;
        const float NearScale = 1.75f;
        const float FarScale = 0.16f;

        static readonly float[] Spawn = { 0.04f, 0.16f, 0.3f, 0.46f, 0.62f };
        static readonly float[] Side = { -90f, 70f, -30f, 110f, 20f };

        Image _chariotGraphic;
        RectTransform[] _dustRects;
        bool _ready;
        bool _playing;

        void Awake()
        {
            Ensure();
            if (!_playing)
                Hide();
        }

        void Ensure()
        {
            if (_ready)
                return;
            if (Chariot != null)
                _chariotGraphic = Chariot.GetComponent<Image>();
            var count = Dust == null ? 0 : Dust.Length;
            _dustRects = new RectTransform[count];
            for (var i = 0; i < count; i++)
            {
                if (Dust[i] == null)
                    continue;
                Dust[i].raycastTarget = false;
                _dustRects[i] = Dust[i].rectTransform;
            }

            if (_chariotGraphic != null)
                _chariotGraphic.raycastTarget = false;
            _ready = true;
        }

        /// <summary>上一发还没播完就停掉，再从屏下冲到远处。</summary>
        public void Play()
        {
            if (Chariot == null)
                return;
            Ensure();
            UITween.KillTarget(this);
            _playing = true;
            SetDim(true);
            Apply(0f);
            UITween.Custom(this, Duration, Apply, EaseKind.OutCubic).OnComplete(Hide);
        }

        public void Stop()
        {
            _playing = false;
            UITween.KillTarget(this);
            Hide();
            SetDim(false);
        }

        void OnEnable()
        {
            if (_ready && !_playing)
                Hide();
        }

        void OnDisable()
        {
            _playing = false;
            UITween.KillTarget(this);
            Hide();
            SetDim(false);
        }

        void Hide()
        {
            _playing = false;
            SetGraphic(_chariotGraphic, 0f);
            if (Dust == null)
                return;
            for (var i = 0; i < Dust.Length; i++)
                SetGraphic(Dust[i], 0f);
        }

        void Apply(float u)
        {
            var height = 1920f;
            var rect = transform as RectTransform;
            if (rect != null && rect.rect.height > 10f)
                height = rect.rect.height;

            var y0 = -height * 0.58f;
            var y1 = height * 0.58f;
            var y = Mathf.LerpUnclamped(y0, y1, u);
            var scale = Mathf.LerpUnclamped(NearScale, FarScale, u);
            Chariot.anchoredPosition = new Vector2(0f, y);
            Chariot.localScale = new Vector3(scale, scale, 1f);
            SetGraphic(_chariotGraphic, u > 0.92f ? (1f - u) / 0.08f : 1f);

            var count = Dust == null ? 0 : Dust.Length;
            for (var i = 0; i < count; i++)
            {
                var graphic = Dust[i];
                var dust = i < _dustRects.Length ? _dustRects[i] : null;
                if (graphic == null || dust == null)
                    continue;
                var born = i < Spawn.Length ? Spawn[i] : 0.2f;
                var life = (u - born) / 0.36f;
                if (life <= 0f || life >= 1f)
                {
                    SetGraphic(graphic, 0f);
                    continue;
                }

                var side = i < Side.Length ? Side[i] : 0f;
                var bornY = Mathf.LerpUnclamped(y0, y1, born);
                var bornScale = Mathf.LerpUnclamped(NearScale, FarScale, born);
                dust.anchoredPosition = new Vector2(side * bornScale, bornY - 140f * bornScale - life * 36f);
                var puff = bornScale * (1.05f + life * 0.85f);
                dust.localScale = new Vector3(puff, puff, 1f);
                SetGraphic(graphic, (1f - life) * 0.8f);
            }
        }

        void SetDim(bool on)
        {
            if (Backdrop == null)
                return;
            if (on)
                Backdrop.Show();
            else
                Backdrop.Hide();
        }

        static void SetGraphic(Image graphic, float alpha)
        {
            if (graphic == null)
                return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
            graphic.enabled = alpha > 0.01f;
        }
    }
}
