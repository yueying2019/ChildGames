using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class GameAudioProfileUtil
    {
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void AssignIfEmpty(ref GameSfx sfx, string clipPath, float volume, float pitch)
        {
            if (sfx.Clip != null)
                return;
            sfx.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            sfx.Volume = volume;
            sfx.Pitch = pitch;
        }

        public static void Assign(ref GameSfx sfx, string clipPath, float volume, float pitch)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
                return;
            sfx.Clip = clip;
            sfx.Volume = volume;
            sfx.Pitch = pitch;
        }

        public static void ReplaceIfNamed(ref GameSfx sfx, string clipPath, float volume, float pitch, params string[] names)
        {
            if (sfx.Clip != null && names != null)
            {
                var current = sfx.Clip.name;
                var match = false;
                for (var i = 0; i < names.Length; i++)
                {
                    if (current == names[i])
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                    return;
            }

            Assign(ref sfx, clipPath, volume, pitch);
        }

        public static void BindTo<TView>(string prefabPath, System.Action<TView> bind) where TView : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;
            var view = prefab.GetComponent<TView>();
            if (view == null)
                return;
            bind(view);
            EditorUtility.SetDirty(prefab);
        }
    }
}
