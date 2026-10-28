using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class Match3PrefabBuilder
    {
        private const string ViewPrefabPath = "Assets/Content/Match3/Match3GameView.prefab";
        public const string AudioAssetPath = "Assets/Content/Match3/Match3Audio.asset";

        [MenuItem("SmallGames/三消/生成音效配置", false, 2)]
        public static void BuildAudioFromMenu()
        {
            var profile = BuildAudioProfile();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static Match3AudioProfile BuildAudioProfile()
        {
            Directory.CreateDirectory("Assets/Content/Match3");
            var profile = AssetDatabase.LoadAssetAtPath<Match3AudioProfile>(AudioAssetPath);
            var created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<Match3AudioProfile>();
                AssetDatabase.CreateAsset(profile, AudioAssetPath);
            }

            GameAudioProfileUtil.AssignIfEmpty(ref profile.Swap, "Assets/Sounds/switch-a.ogg", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Match, "Assets/Content/Sounds/Interactions_Pickup_Retro_16.wav", 0.9f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Fall, "Assets/Content/Sounds/Interactions_Suction_01.wav", 0.6f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Line, "Assets/Content/Sounds/Interactions_Steam_Spit_02.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Bomb, "Assets/Content/Sounds/Interactions_Steam_Spit_05.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.ColorBomb, "Assets/Content/Sounds/HUD_Notification_Misc_01.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Win, "Assets/Content/Sounds/level_win.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Lose, "Assets/Content/Sounds/level_lost.wav", 1f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Ice, "Assets/Content/Sounds/Interactions_Pickup_Key_02.wav", 0.9f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Stone, "Assets/Content/Sounds/Error_Generic_Thud_01.wav", 0.95f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Jelly, "Assets/Content/Sounds/Interactions_Pickup_Paper_01.wav", 0.9f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Cage, "Assets/Content/Sounds/Interactions_Wooden_Switch_02.wav", 0.9f, 1f);
            GameAudioProfileUtil.AssignIfEmpty(ref profile.Bgm, "Assets/Content/Sounds/Bgm/bgm_match3.mp3", 0.65f, 1f);

            GameAudioProfileUtil.BindTo<Match3GameView>(ViewPrefabPath, view =>
            {
                if (view.AudioProfile == null)
                    view.AudioProfile = profile;
            });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log(created
                ? "[Match3] 已生成音效配置：" + AudioAssetPath
                : "[Match3] 已补全空缺音效：" + AudioAssetPath);
            return profile;
        }
    }
}
