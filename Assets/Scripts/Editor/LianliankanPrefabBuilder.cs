using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class LianliankanPrefabBuilder
    {
        private const string ViewPrefabPath = "Assets/Content/Lianliankan/LianliankanGameView.prefab";
        public const string AudioAssetPath = "Assets/Content/Lianliankan/LianliankanAudio.asset";

        [MenuItem("SmallGames/对对消/生成音效配置", false, 0)]
        public static void BuildAudioFromMenu()
        {
            var profile = BuildAudioProfile();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static LianliankanAudioProfile BuildAudioProfile()
        {
            var created = !File.Exists(AudioAssetPath);
            var profile = GameAudioProfileUtil.LoadOrCreate<LianliankanAudioProfile>(AudioAssetPath);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Click, "Assets/Sounds/tap-a.ogg", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Mismatch, "Assets/Sounds/tap-a.ogg", 0.9f, 1.45f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Connect, "Assets/Content/Sounds/Interactions_Pickup_Retro_16.wav", 0.95f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Win, "Assets/Content/Sounds/level_win.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Lose, "Assets/Content/Sounds/level_lost.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Bgm, "Assets/Content/Sounds/Bgm/bgm_lianliankan.ogg", 0.65f, 1f);

            GameAudioProfileUtil.BindTo<LianliankanGameView>(ViewPrefabPath, view =>
            {
                if (view.AudioProfile == null)
                    view.AudioProfile = profile;
            });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log(created
                ? "[Lianliankan] 已生成音效配置：" + AudioAssetPath
                : "[Lianliankan] 已补全空缺音效：" + AudioAssetPath);
            return profile;
        }
    }
}
