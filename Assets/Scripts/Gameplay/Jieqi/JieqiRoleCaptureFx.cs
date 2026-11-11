using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 炮、象、士、兵、将印和城旗。棋步已经落下，这里只盖在黄沙上面，不改规则。
    /// </summary>
    public class JieqiRoleCaptureFx : MonoBehaviour
    {
        public JieqiCaptureBackdrop Backdrop;
        public Image Cannon;
        public Image Smoke;
        public Image Ball;
        public Image Burst;
        public Image Elephant;
        public Image Stomp;
        public Image Advisor;
        public Image Soldier;
        public Image Spark;
        public Image Seal;
        public Image Flag;
        public Sprite SoldierStand;
        public Sprite SoldierRun;
        public Sprite SoldierThrust;
        public Sprite ElephantStand;
        public Sprite ElephantRunA;
        public Sprite ElephantRunB;

        const int KindCannon = 1;
        const int KindElephant = 2;
        const int KindAdvisor = 3;
        const int KindPawn = 4;
        const int KindSeal = 5;
        const int KindFlag = 6;

        RectTransform _cannon;
        RectTransform _smoke;
        RectTransform _ball;
        RectTransform _burst;
        RectTransform _elephant;
        RectTransform _stomp;
        RectTransform _advisor;
        RectTransform _soldier;
        RectTransform _spark;
        RectTransform _seal;
        RectTransform _flag;
        int _kind;
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
            _cannon = Cache(Cannon);
            _smoke = Cache(Smoke);
            _ball = Cache(Ball);
            _burst = Cache(Burst);
            _elephant = Cache(Elephant);
            _stomp = Cache(Stomp);
            _advisor = Cache(Advisor);
            _soldier = Cache(Soldier);
            _spark = Cache(Spark);
            _seal = Cache(Seal);
            _flag = Cache(Flag);
            _ready = true;
        }

        static RectTransform Cache(Image image)
        {
            if (image == null)
                return null;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        public void PlayCannon()
        {
            Begin(KindCannon, 2f);
        }

        public void PlayElephant()
        {
            Begin(KindElephant, 2f);
        }

        public void PlayAdvisor()
        {
            Begin(KindAdvisor, 2f);
        }

        /// <summary>兵卒持枪冲上来再突刺，大约 1.6 秒。</summary>
        public void PlayPawn()
        {
            Begin(KindPawn, 1.6f);
        }

        public void PlaySeal()
        {
            Begin(KindSeal, 2f);
        }

        public void PlayFlag()
        {
            Begin(KindFlag, 2f);
        }

        public void Stop()
        {
            _playing = false;
            UITween.KillTarget(this);
            Hide();
            HideBackdrop();
        }

        void Begin(int kind, float duration)
        {
            Ensure();
            UITween.KillTarget(this);
            _playing = true;
            _kind = kind;
            _pose = -1;
            if (Backdrop != null)
                Backdrop.Show();
            HideGraphics();
            Apply(0f);
            UITween.Custom(this, duration, Apply, EaseKind.Linear).OnComplete(Hide);
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
            HideBackdrop();
        }

        void Hide()
        {
            _playing = false;
            _pose = -1;
            HideGraphics();
        }

        void HideBackdrop()
        {
            if (Backdrop != null)
                Backdrop.Hide();
        }

        void HideGraphics()
        {
            SetGraphic(Cannon, 0f);
            SetGraphic(Smoke, 0f);
            SetGraphic(Ball, 0f);
            SetGraphic(Burst, 0f);
            SetGraphic(Elephant, 0f);
            SetGraphic(Stomp, 0f);
            SetGraphic(Advisor, 0f);
            SetGraphic(Soldier, 0f);
            SetGraphic(Spark, 0f);
            SetGraphic(Seal, 0f);
            SetGraphic(Flag, 0f);
            if (_flag != null)
                _flag.localRotation = Quaternion.identity;
        }

        void Apply(float u)
        {
            if (_kind == KindCannon)
                ApplyCannon(u);
            else if (_kind == KindElephant)
                ApplyElephant(u);
            else if (_kind == KindAdvisor)
                ApplyAdvisor(u);
            else if (_kind == KindPawn)
                ApplyPawn(u);
            else if (_kind == KindSeal)
                ApplySeal(u);
            else if (_kind == KindFlag)
                ApplyFlag(u);
        }

        void ApplyCannon(float u)
        {
            Place(_cannon, Cannon, -240f, -60f, 1.05f, 1.05f, u > 0.9f ? (1f - u) / 0.1f : 1f);
            if (u < 0.28f)
            {
                var k = u / 0.28f;
                Place(_smoke, Smoke, 20f, 10f, Mathf.Lerp(0.35f, 1.35f, k), Mathf.Lerp(0.35f, 1.35f, k), 1f - k);
            }
            else
            {
                SetGraphic(Smoke, 0f);
            }

            if (u >= 0.08f && u < 0.76f)
            {
                var k = (u - 0.08f) / 0.68f;
                var ball = Mathf.Lerp(0.68f, 0.58f, k);
                Place(_ball, Ball, Mathf.Lerp(30f, 460f, k), Mathf.Lerp(6f, 120f, k), ball, ball, 1f);
            }
            else
            {
                SetGraphic(Ball, 0f);
            }

            if (u >= 0.7f)
            {
                var k = (u - 0.7f) / 0.3f;
                Place(_burst, Burst, 460f, 120f, Mathf.Lerp(0.45f, 2.4f, k), Mathf.Lerp(0.45f, 2.4f, k), 1f - k);
            }
            else
            {
                SetGraphic(Burst, 0f);
            }
        }

        void ApplyElephant(float u)
        {
            var ground = Ground();
            if (u < 0.12f)
            {
                Pose(Elephant, ElephantStand, 1);
                var squat = Mathf.Lerp(1f, 0.9f, u / 0.12f);
                Place(_elephant, Elephant, -16f, ground, 1.02f, 1.02f * squat, 1f);
                SetGraphic(Stomp, 0f);
                return;
            }

            var t = (u - 0.12f) / 0.88f;
            var step = (int)(t * 9f);
            Pose(Elephant, (step & 1) == 0 ? ElephantRunA : ElephantRunB, 2 + (step & 1));
            var run = t < 0.7f ? t / 0.7f : 1f;
            var depart = t < 0.7f ? 0f : (t - 0.7f) / 0.3f;
            var eased = depart * depart;
            var x = Mathf.Lerp(-24f, 36f, run);
            var y = Mathf.Lerp(ground, ground + 28f, run) + Mathf.Abs(Mathf.Sin(t * 31.4f)) * 12f;
            if (depart > 0f)
                y = Mathf.Lerp(y, Far(), eased);
            var scale = Mathf.Lerp(1.05f, 0.2f, eased);
            var alpha = depart > 0.82f ? (1f - depart) / 0.18f : 1f;
            Place(_elephant, Elephant, x, y, scale, scale, alpha);
            if (depart < 0.35f)
            {
                var dust = (t * 4.5f) % 1f;
                Place(_stomp, Stomp, x, ground - 4f, Mathf.Lerp(0.28f, 0.85f, dust), Mathf.Lerp(0.16f, 0.42f, dust), (1f - dust) * 0.8f);
            }
            else
            {
                SetGraphic(Stomp, 0f);
            }
        }

        void ApplyAdvisor(float u)
        {
            var ground = Ground();
            var x = 0f;
            if (u >= 0.28f && u < 0.52f)
                x = Mathf.Lerp(0f, 90f, (u - 0.28f) / 0.24f);
            else if (u >= 0.52f)
                x = 90f;

            if (u < 0.55f)
            {
                Place(_advisor, Advisor, x, ground, 0.92f, 0.92f, 1f);
            }
            else
            {
                var t = (u - 0.55f) / 0.45f;
                var eased = 1f - (1f - t) * (1f - t);
                var scale = Mathf.Lerp(0.92f, 0.16f, eased);
                Place(_advisor, Advisor, x, Mathf.Lerp(ground, Far(), eased), scale, scale, t > 0.9f ? (1f - t) / 0.1f : 1f);
            }

            if (u >= 0.3f && u < 0.62f)
            {
                var k = (u - 0.3f) / 0.32f;
                var pop = k < 0.35f ? Mathf.Lerp(0.2f, 0.55f, k / 0.35f) : Mathf.Lerp(0.55f, 0.35f, (k - 0.35f) / 0.65f);
                Place(_seal, Seal, x + 10f, ground + 150f, pop, pop, k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
            }
            else
            {
                SetGraphic(Seal, 0f);
            }
        }

        void ApplyPawn(float u)
        {
            var ground = Ground();
            if (u < 0.12f)
            {
                Pose(Soldier, SoldierStand, 1);
                var squat = Mathf.Lerp(1f, 0.9f, u / 0.12f);
                Place(_soldier, Soldier, -220f, ground, 1.02f, 1.02f * squat, 1f);
                SetGraphic(Spark, 0f);
                SetGraphic(Burst, 0f);
                SetGraphic(Stomp, 0f);
                return;
            }

            if (u < 0.68f)
            {
                var k = (u - 0.12f) / 0.56f;
                var step = (int)(k * 6f);
                Pose(Soldier, SoldierRun, 2);
                var flip = (step & 1) == 0 ? 1f : -1f;
                var x = Mathf.Lerp(-220f, 40f, k);
                var y = ground + Mathf.Abs(Mathf.Sin(k * 18.84f)) * 18f;
                Place(_soldier, Soldier, x, y, 1.08f * flip, 1.08f, 1f);
                var dust = (k * 6f) % 1f;
                Place(_stomp, Stomp, x, ground - 6f, Mathf.Lerp(0.22f, 0.7f, dust), Mathf.Lerp(0.12f, 0.34f, dust), (1f - dust) * 0.75f);
                SetGraphic(Spark, 0f);
                SetGraphic(Burst, 0f);
                return;
            }

            var t = (u - 0.68f) / 0.32f;
            Pose(Soldier, SoldierThrust, 3);
            var lunge = t < 0.4f ? t / 0.4f : 1f;
            var x2 = Mathf.Lerp(40f, 130f, lunge);
            var alpha = t > 0.78f ? (1f - t) / 0.22f : 1f;
            const float scale = 1.12f;
            Place(_soldier, Soldier, x2, ground + 8f, scale, scale, alpha);
            SetGraphic(Stomp, 0f);
            if (_soldier != null && t < 0.72f)
            {
                var tipX = x2 + _soldier.sizeDelta.x * 0.5f * scale;
                var tipY = ground + 8f + _soldier.sizeDelta.y * 0.93f * scale;
                var pop = t < 0.28f ? t / 0.28f : 1f - (t - 0.28f) / 0.44f;
                Place(_spark, Spark, tipX, tipY, Mathf.Lerp(0.45f, 1.55f, t), Mathf.Lerp(0.45f, 1.55f, t), Mathf.Clamp01(pop));
                if (t > 0.18f)
                {
                    var bk = (t - 0.18f) / 0.54f;
                    Place(_burst, Burst, tipX, tipY, Mathf.Lerp(0.35f, 1.7f, bk), Mathf.Lerp(0.35f, 1.7f, bk), (1f - bk) * 0.85f);
                }
                else
                {
                    SetGraphic(Burst, 0f);
                }
            }
            else
            {
                SetGraphic(Spark, 0f);
                SetGraphic(Burst, 0f);
            }
        }

        void ApplySeal(float u)
        {
            var y = 0f;
            var sx = 1.2f;
            var sy = 1.2f;
            if (u < 0.34f)
            {
                var k = u / 0.34f;
                var drop = k * k;
                y = Mathf.Lerp(Height() * 0.34f, 10f, drop);
                sx = Mathf.Lerp(1.7f, 1.15f, drop);
                sy = sx;
            }
            else if (u < 0.48f)
            {
                var k = (u - 0.34f) / 0.14f;
                y = 10f;
                sx = Mathf.Lerp(1.15f, 1.35f, k);
                sy = Mathf.Lerp(1.15f, 0.78f, k);
            }
            else
            {
                y = 10f;
                sx = 1.2f;
                sy = 1.05f;
            }

            var alpha = u > 0.78f ? (1f - u) / 0.22f : 1f;
            Place(_seal, Seal, 0f, y, sx, sy, alpha);
            if (u >= 0.32f && u < 0.62f)
            {
                var k = (u - 0.32f) / 0.3f;
                Place(_burst, Burst, 0f, -20f, Mathf.Lerp(0.2f, 1.8f, k), Mathf.Lerp(0.2f, 1.8f, k), (1f - k) * 0.85f);
            }
            else
            {
                SetGraphic(Burst, 0f);
            }
        }

        void ApplyFlag(float u)
        {
            if (_flag == null)
                return;
            var ground = -Height() * 0.08f;
            var tilt = 0f;
            var y = ground;
            if (u >= 0.22f)
            {
                var k = Mathf.Clamp01((u - 0.22f) / 0.58f);
                var eased = k * k;
                tilt = Mathf.Lerp(0f, 76f, eased);
                y = Mathf.Lerp(ground, ground - 30f, eased);
            }

            _flag.anchoredPosition = new Vector2(0f, y);
            _flag.localScale = new Vector3(1.15f, 1.15f, 1f);
            _flag.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            SetGraphic(Flag, u > 0.86f ? (1f - u) / 0.14f : 1f);
        }

        float Height()
        {
            var rect = transform as RectTransform;
            if (rect != null && rect.rect.height > 10f)
                return rect.rect.height;
            return 1080f;
        }

        float Ground()
        {
            return -Height() * 0.2f;
        }

        float Far()
        {
            return Height() * 0.42f;
        }

        void Pose(Image image, Sprite sprite, int id)
        {
            if (_pose == id || image == null || sprite == null)
                return;
            _pose = id;
            image.sprite = sprite;
        }

        static void Place(RectTransform rect, Image graphic, float x, float y, float sx, float sy, float alpha)
        {
            if (rect == null)
                return;
            rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = new Vector3(sx, sy, 1f);
            rect.localRotation = Quaternion.identity;
            SetGraphic(graphic, alpha);
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
