using UnityEngine;

namespace SmallGames
{
    public static class UiPrefabs
    {
        public const string Root = "UI/UIRoot";
        public const string MainMenu = "UI/MainMenu";
        public const string Settings = "UI/Settings";
        public const string GameHud = "UI/GameHud";
        public const string Pause = "UI/Pause";
        public const string Result = "UI/Result";
        public const string Shop = "UI/Shop";
        public const string LevelSelect = "UI/LevelSelect";
        public const string Continue = "UI/Continue";
        public const string DailyLogin = "UI/DailyLogin";
        public const string DailyTask = "UI/DailyTask";
        public const string Energy = "UI/Energy";
        public const string ComboPopup = "UI/ComboPopup";
        public const string Guide = "UI/Guide";
        public const string Splash = "UI/Splash";
        public const string Privacy = "UI/Privacy";
        public const string Legal = "UI/Legal";
        public const string Loading = "UI/Loading";
        public const string LevelIntro = "UI/LevelIntro";
        public const string ModeRules = "UI/ModeRules";
        public const string TestWindow = "UI/TestWindow";
        public const string DevWindow = "UI/DevWindow";
        public const string Codex = "UI/handbook";
        public const string CatMascot = "UI/CatMascot";
        public const string CjkFont = "Font/NotoSansSC-UI";

        public const string LianliankanView = "Lianliankan/LianliankanGameView";
        public const string SheepView = "Sheep/SheepGameView";
        public const string Match3View = "Match3/Match3GameView";
        public const string JieqiView = "Jieqi/JieqiGameView";

        public static T Instantiate<T>(string path, Transform parent, bool stretch = true) where T : Component
        {
            var prefab = AssetHub.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[UI] 缺少预制体 {path}。请执行 SmallGames/刷新内容索引。");
                return null;
            }

            var go = Object.Instantiate(prefab, parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (stretch && rect != null)
                UIViewFactory.StretchFull(rect);
            return go.GetComponent<T>();
        }
    }
}
