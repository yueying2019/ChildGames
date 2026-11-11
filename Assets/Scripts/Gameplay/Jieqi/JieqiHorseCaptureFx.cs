using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 马吃子：先站定，再下蹲蓄力，后腿蹬出时打出击中，然后朝屏幕上方跳远。
    /// 棋步已经落下，这段只盖在棋盘上面，不挡点击。
    /// </summary>
    public class JieqiHorseCaptureFx : MonoBehaviour
    {
        public Image Horse;
        public Image Hit;
        public Sprite Stand;
        public Sprite Gather;
        public Sprite Kick;
        public JieqiCaptureBackdrop Backdrop;

        const float Duration = 2f;
        const float StandEnd = 0.18f;
        const float GatherEnd = 0.36f;
        const float KickEnd = 0.52f;

        RectTransform _horseRect;
        RectTransform _hitRect;
        int _pose = -1;
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
            if (Horse != null)
            {
                Horse.raycastTarget = false;
                _horseRect = Horse.rectTransform;
            }

            if (Hit != null)
            {
                Hit.raycastTarget = false;
                _hitRect = Hit.rectTransform;
            }

            _ready = true;
        }

        /// <summary>上一发还没播完就停掉。站、蓄、蹬大约各占前半秒，剩下的时间用来跳远。</summary>
        public void Play()
        {
            if (Horse == null)
                return;
            Ensure();
            UITween.KillTarget(this);
            _playing = true;
            _pose = -1;
            SetDim(true);
            Apply(0f);
            UITween.Custom(this, Duration, Apply, EaseKind.Linear).OnComplete(Hide);
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
            _pose = -1;
            SetGraphic(Horse, 0f);
            SetGraphic(Hit, 0f);
        }

        void Apply(float u)
        {
            if (Horse == null || _horseRect == null)
                return;

            var height = 1920f;
            var rect = transform as RectTransform;
            if (rect != null && rect.rect.height > 10f)
                height = rect.rect.height;
            var ground = -height * 0.22f;
            var far = height * 0.56f;

            if (u < KickEnd)
            {
                var pose = u < StandEnd ? 0 : (u < GatherEnd ? 1 : 2);
                SetPose(pose);
                var squash = pose == 1 ? 0.86f : 1f;
                _horseRect.anchoredPosition = new Vector2(0f, ground);
                _horseRect.localScale = new Vector3(1.12f, 1.12f * squash, 1f);
                SetGraphic(Horse, 1f);
                ApplyHit(u, ground);
                return;
            }

            SetPose(2);
            var t = (u - KickEnd) / (1f - KickEnd);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            var scale = Mathf.LerpUnclamped(1.12f, 0.16f, eased);
            _horseRect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(ground, far, eased));
            _horseRect.localScale = new Vector3(scale, scale, 1f);
            SetGraphic(Horse, t > 0.9f ? (1f - t) / 0.1f : 1f);
            ApplyHit(u, ground);
        }

        void ApplyHit(float u, float ground)
        {
            if (Hit == null || _hitRect == null)
                return;
            if (u < GatherEnd)
            {
                SetGraphic(Hit, 0f);
                return;
            }

            _hitRect.anchoredPosition = new Vector2(0f, ground);
            if (u < KickEnd)
            {
                var k = (u - GatherEnd) / (KickEnd - GatherEnd);
                _hitRect.localScale = new Vector3(Mathf.LerpUnclamped(0.35f, 1.35f, k), Mathf.LerpUnclamped(0.35f, 1.35f, k), 1f);
                SetGraphic(Hit, k < 0.2f ? k / 0.2f : 1f);
                return;
            }

            var t = (u - KickEnd) / (1f - KickEnd);
            if (t >= 0.4f)
            {
                SetGraphic(Hit, 0f);
                return;
            }

            _hitRect.localScale = new Vector3(1.35f, 1.35f, 1f);
            SetGraphic(Hit, (1f - t / 0.4f) * 0.85f);
        }

        void SetPose(int pose)
        {
            if (_pose == pose || Horse == null)
                return;
            _pose = pose;
            var sprite = Stand;
            if (pose == 1)
                sprite = Gather;
            else if (pose == 2)
                sprite = Kick;
            if (sprite != null)
                Horse.sprite = sprite;
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
