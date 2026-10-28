using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 设置读写。当前用 PlayerPrefs 持久化，后续可替换为本地文件或云存档。
    /// </summary>
    public static class SettingsRepository
    {
        private const string PlayerPrefsKey = "SmallGames.Settings";

        public static GameSettingsData Current { get; private set; } = new GameSettingsData();

        public static GameSettingsData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var json = SecurePrefs.GetString(PlayerPrefsKey);
                    var data = JsonUtility.FromJson<GameSettingsData>(json);
                    Current = data ?? new GameSettingsData();
                    if (json != null && !json.Contains("MusicEnabled"))
                    {
                        Current.MusicEnabled = true;
                        Current.SfxEnabled = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Settings] 读取失败，使用默认值: {e.Message}");
                    Current = new GameSettingsData();
                }
            }
            else
            {
                Current = new GameSettingsData();
            }

            Current.Sanitize();
            return Current;
        }

        public static void Save()
        {
            if (Current == null)
                Current = new GameSettingsData();

            Current.Sanitize();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        public static void ResetToDefault()
        {
            Current = new GameSettingsData();
            Save();
        }
    }
}
