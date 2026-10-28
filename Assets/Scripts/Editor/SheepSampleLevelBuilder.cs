using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class SheepSampleLevelBuilder
    {
        private const string Folder = "Assets/Content/Sheep/Levels";

        [MenuItem("SmallGames/叠叠消/生成示例关卡", false, 1)]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var samples = SheepLevelCatalog.Samples;
            for (var i = 0; i < samples.Length; i++)
            {
                var spec = samples[i];
                CreateOrUpdate($"Level_{spec.Id:00}.asset", spec);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sheep] 已生成示例关卡：" + Folder);
        }

        private static void CreateOrUpdate(string fileName, SheepLevelSpec spec)
        {
            var path = $"{Folder}/{fileName}";
            var asset = AssetDatabase.LoadAssetAtPath<SheepLevelData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SheepLevelData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SheepLevelCatalog.ApplySpec(asset, spec);
            EditorUtility.SetDirty(asset);
        }
    }
}
