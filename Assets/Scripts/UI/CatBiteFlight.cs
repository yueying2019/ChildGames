using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 消除的格子沿二次贝塞尔飞进猫嘴。控制点在弦的法线上随机偏移，避免直飞。
    /// </summary>
    public sealed class CatBiteFlight
    {
        /// <summary>嘴相对猫图中心的本地偏移。猫是坐姿，嘴在中心偏上。</summary>
        private const float MouthOffsetY = 36f;

        private readonly RectTransform _cat;
        private readonly RectTransform _layer;
        private readonly ComponentPool<Image> _pool;
        private readonly List<Image> _live = new List<Image>(12);

        private CatBiteFlight(RectTransform cat, RectTransform layer, ComponentPool<Image> pool)
        {
            _cat = cat;
            _layer = layer;
            _pool = pool;
            _pool.Prewarm(12);
        }

        public static CatBiteFlight Create(Image cat)
        {
            if (cat == null)
                return null;
            var layer = cat.rectTransform;
            if (layer == null)
                return null;
            var template = CreateTemplate(layer);
            if (template == null)
                return null;
            return new CatBiteFlight(layer, layer, new ComponentPool<Image>(template, layer));
        }

        /// <summary>只做一次空图片模板。池里再克隆它，避免每次飞行都复制整只猫和能量条。</summary>
        private static Image CreateTemplate(Transform parent)
        {
            var prefabGo = AssetHub.Load<GameObject>(UiPrefabs.CatMascot);
            if (prefabGo == null)
                return null;

            var copy = UnityEngine.Object.Instantiate(prefabGo, parent, false);
            copy.name = "CatBiteTemplate";
            copy.SetActive(false);
            for (var i = copy.transform.childCount - 1; i >= 0; i--)
            {
                var child = copy.transform.GetChild(i);
                if (child.name == "Energy")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var bar = copy.GetComponent<CatEnergyBar>();
            if (bar != null)
                UnityEngine.Object.DestroyImmediate(bar);

            var image = copy.GetComponent<Image>();
            if (image == null)
                return null;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = null;
            return image;
        }

        /// <summary>复制这张图的样子，从它当前的位置飞向猫嘴。原格子由调用方自己收掉。</summary>
        public void Launch(Image source)
        {
            if (source == null || source.sprite == null || _pool == null || _layer == null || _cat == null)
                return;

            var from = source.rectTransform;
            var size = from.rect.size;
            if (size.x < 8f || size.y < 8f)
                size = new Vector2(72f, 72f);

            var startWorld = from.TransformPoint(from.rect.center);
            var endWorld = _cat.TransformPoint(new Vector3(0f, MouthOffsetY, 0f));
            var start = (Vector2)_layer.InverseTransformPoint(startWorld);
            var end = (Vector2)_layer.InverseTransformPoint(endWorld);
            var control = ControlPoint(start, end, out var spin);

            var image = _pool.Take();
            if (image == null)
                return;

            image.sprite = source.sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.gameObject.name = "CatBite";
            HideEnergy(image.transform);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
            rect.position = startWorld;

            _live.Add(image);
            var duration = UnityEngine.Random.Range(0.45f, 0.62f);
            UITween.KillTarget(image);
            UITween.Custom(image, duration, u =>
            {
                if (rect == null)
                    return;
                var t = u * u * (3f - 2f * u);
                var point = Bezier(start, control, end, t);
                rect.position = _layer.TransformPoint(new Vector3(point.x, point.y, 0f));
                var scale = Mathf.Lerp(1f, 0.18f, u * u);
                rect.localScale = new Vector3(scale, scale, scale);
                rect.localEulerAngles = new Vector3(0f, 0f, spin * Mathf.Sin(t * Mathf.PI));
                var alpha = u < 0.82f ? 1f : 1f - (u - 0.82f) / 0.18f;
                image.color = new Color(1f, 1f, 1f, alpha);
            }, EaseKind.Linear).SetUnscaled(true).OnComplete(() => Finish(image));
        }

        public void Stop()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
                Finish(_live[i]);
        }

        /// <summary>
        /// B(t) = (1-t)² P0 + 2(1-t)t P1 + t² P2。
        /// P1 取弦上一点，再沿法线偏一段随机距离。
        /// </summary>
        private static Vector2 ControlPoint(Vector2 start, Vector2 end, out float spin)
        {
            var chord = end - start;
            var length = chord.magnitude;
            var tangent = length > 1f ? chord / length : Vector2.right;
            var normal = new Vector2(-tangent.y, tangent.x);
            if (normal.y < 0f)
                normal = -normal;

            var sign = UnityEngine.Random.value < 0.7f ? 1f : -1f;
            var amp = sign * Mathf.Clamp(length * UnityEngine.Random.Range(0.28f, 0.55f), 80f, 240f);
            var along = UnityEngine.Random.Range(0.38f, 0.62f);
            spin = sign * UnityEngine.Random.Range(14f, 32f);
            return Vector2.Lerp(start, end, along) + normal * amp;
        }

        private static void HideEnergy(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == "Energy")
                    child.gameObject.SetActive(false);
            }
        }

        private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var u = 1f - t;
            return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
        }

        private void Finish(Image image)
        {
            if (image == null)
                return;

            var found = false;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] != image)
                    continue;
                _live.RemoveAt(i);
                found = true;
                break;
            }

            if (!found)
                return;

            UITween.KillTarget(image);
            var rect = image.rectTransform;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
            image.color = Color.white;
            _pool.Return(image);
        }
    }
}
