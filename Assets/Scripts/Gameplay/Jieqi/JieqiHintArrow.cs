using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 提示用的弯曲箭头。控制点由棋盘算好，这里只把一条带状箭身和箭头画出来。
    /// </summary>
    public class JieqiHintArrow : MaskableGraphic
    {
        const int Steps = 14;

        Vector2 _from;
        Vector2 _control;
        Vector2 _to;
        bool _ready;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>二次曲线。from 是要走的子，to 是落点，control 决定弯向哪一侧。</summary>
        public void Show(Vector2 from, Vector2 control, Vector2 to)
        {
            _from = from;
            _control = control;
            _to = to;
            _ready = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public void Hide()
        {
            _ready = false;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_ready)
                return;

            var fill = (Color32)color;
            var edge = new Color32(
                (byte)(fill.r * 0.35f),
                (byte)(fill.g * 0.35f),
                (byte)(fill.b * 0.35f),
                fill.a);
            Ribbon(vh, 0.08f, 0.74f, 13f, edge);
            Ribbon(vh, 0.08f, 0.74f, 7f, fill);
            Head(vh, 22f, edge);
            Head(vh, 14f, fill);
        }

        void Ribbon(VertexHelper vh, float t0, float t1, float half, Color32 tint)
        {
            var prev = Point(t0);
            var prevN = Normal(t0);
            for (var i = 1; i <= Steps; i++)
            {
                var t = t0 + (t1 - t0) * (i / (float)Steps);
                var p = Point(t);
                var n = Normal(t);
                Quad(vh, prev + prevN * half, p + n * half, p - n * half, prev - prevN * half, tint);
                prev = p;
                prevN = n;
            }
        }

        void Head(VertexHelper vh, float half, Color32 tint)
        {
            var n = Normal(0.74f);
            var tip = Point(0.96f);
            var neck = Point(0.72f);
            var i = vh.currentVertCount;
            vh.AddVert(tip, tint, new Vector2(0.5f, 0.5f));
            vh.AddVert(neck + n * half, tint, new Vector2(0.5f, 0.5f));
            vh.AddVert(neck - n * half, tint, new Vector2(0.5f, 0.5f));
            vh.AddTriangle(i, i + 1, i + 2);
        }

        Vector2 Point(float t)
        {
            var u = 1f - t;
            return u * u * _from + 2f * u * t * _control + t * t * _to;
        }

        Vector2 Normal(float t)
        {
            var u = 1f - t;
            var tangent = 2f * u * (_control - _from) + 2f * t * (_to - _control);
            var len = tangent.magnitude;
            if (len < 0.001f)
                return Vector2.up;
            return new Vector2(-tangent.y / len, tangent.x / len);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            var i = vh.currentVertCount;
            var uv = new Vector2(0.5f, 0.5f);
            vh.AddVert(a, tint, uv);
            vh.AddVert(b, tint, uv);
            vh.AddVert(c, tint, uv);
            vh.AddVert(d, tint, uv);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
