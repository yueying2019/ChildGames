using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class GameBalanceBuilder
    {
        public const string AssetPath = "Assets/Content/GameBalance.asset";

        [MenuItem("SmallGames/生成玩法平衡表")]
        public static void BuildFromMenu()
        {
            var asset = Build();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static void PingOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetPath);
            if (asset == null)
                asset = Build();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static GameBalance Build()
        {
            Directory.CreateDirectory("Assets/Content");
            var created = !File.Exists(AssetPath);
            var asset = GameAudioProfileUtil.LoadOrCreate<GameBalance>(AssetPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            GameBalance.ClearCache();
            Debug.Log(created
                ? "[GameBalance] 已生成：" + AssetPath
                : "[GameBalance] 已存在：" + AssetPath);
            return asset;
        }
    }
}
