using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class LianliankanSampleLevelBuilder
    {
        private const string Folder = "Assets/Content/Lianliankan/Levels";

        [MenuItem("SmallGames/对对消/生成示例关卡", false, 0)]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var samples = LianliankanLevelCatalog.Samples;
            for (var i = 0; i < samples.Length; i++)
            {
                var spec = samples[i];
                CreateOrUpdate($"Level_{spec.Id:00}.asset", spec);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lianliankan] 已生成示例关卡：" + Folder);
        }

        private static void CreateOrUpdate(string fileName, LianliankanLevelSpec spec)
        {
            var path = $"{Folder}/{fileName}";
            var asset = AssetDatabase.LoadAssetAtPath<LianliankanLevelData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LianliankanLevelData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            LianliankanLevelCatalog.ApplySpec(asset, spec);
            EditorUtility.SetDirty(asset);
        }
    }
}
