using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmallGames.Editor
{
    /// <summary>
    /// 一键在当前场景创建 UGUI 画布。
    /// 菜单：SmallGames/创建UI画布（横屏 1920×1080）/（竖屏 1080×1920）
    /// 自动配置 CanvasScaler（ScaleWithScreenSize）+ GraphicRaycaster，
    /// 场景里没有 EventSystem 时会一并补上。
    /// </summary>
    public static class UICanvasCreator
    {
        private const string CanvasRootName = "Canvas";
        private const string EventSystemName = "EventSystem";

        [MenuItem("SmallGames/创建UI画布（横屏 1920×1080）")]
        public static void CreateLandscapeCanvas()
        {
            CreateCanvas(new Vector2(1920, 1080));
        }

        [MenuItem("SmallGames/创建UI画布（竖屏 1080×1920）")]
        public static void CreatePortraitCanvas()
        {
            CreateCanvas(new Vector2(1080, 1920));
        }

        public static Canvas CreateCanvas(Vector2 referenceResolution)
        {
            var canvasGo = new GameObject(
                UniqueName(CanvasRootName),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create UI Canvas");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 宽高各占一半权重，异形屏下更均衡

            EnsureEventSystem();

            Selection.activeGameObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[UI] 已创建画布 {canvasGo.name}，参考分辨率 {referenceResolution.x}×{referenceResolution.y}。");
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;

            var esGo = new GameObject(
                UniqueName(EventSystemName),
                typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            Debug.Log("[UI] 场景中没有 EventSystem，已自动创建。");
        }

        private static string UniqueName(string baseName)
        {
            // GetRootGameObjects 包含未激活对象，比 GameObject.Find 更可靠
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var name = baseName;
            var index = 1;
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != name)
                    continue;
                index++;
                name = $"{baseName} ({index})";
                i = -1; // 换名后从头再查一遍，避免与更早的重名冲突
            }

            return name;
        }
    }
}
