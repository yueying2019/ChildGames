using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class SheepPrefabBuilder
    {
        private const string ViewPrefabPath = "Assets/Content/Sheep/SheepGameView.prefab";
        public const string AudioAssetPath = "Assets/Content/Sheep/SheepAudio.asset";

        [MenuItem("SmallGames/叠叠消/生成音效配置", false, 1)]
        public static void BuildAudioFromMenu()
        {
            var profile = BuildAudioProfile();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static SheepAudioProfile BuildAudioProfile()
        {
            var created = !File.Exists(AudioAssetPath);
            var profile = GameAudioProfileUtil.LoadOrCreate<SheepAudioProfile>(AudioAssetPath);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Click, "Assets/Sounds/tap-a.ogg", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Match, "Assets/Content/Sounds/Interactions_Pickup_Retro_16.wav", 0.95f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Win, "Assets/Content/Sounds/level_win.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Lose, "Assets/Content/Sounds/level_lost.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Bgm, "Assets/Content/Sounds/Bgm/bgm_sheep.ogg", 0.65f, 1f);

            GameAudioProfileUtil.BindTo<SheepGameView>(ViewPrefabPath, view =>
            {
                if (view.AudioProfile == null)
                    view.AudioProfile = profile;
            });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log(created
                ? "[Sheep] 已生成音效配置：" + AudioAssetPath
                : "[Sheep] 已补全空缺音效：" + AudioAssetPath);
            return profile;
        }
    }
}
