using System;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public class GameSettingsData
    {
        [Range(0f, 1f)] public float MusicVolume = 0.8f;
        [Range(0f, 1f)] public float SfxVolume = 1f;
        public bool MusicEnabled = true;
        public bool SfxEnabled = true;
        public bool RemoveXiangqiFx;

        public void Sanitize()
        {
            MusicVolume = Mathf.Clamp01(MusicVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
        }

        public void CopyFrom(GameSettingsData other)
        {
            if (other == null)
                return;

            MusicVolume = other.MusicVolume;
            SfxVolume = other.SfxVolume;
            MusicEnabled = other.MusicEnabled;
            SfxEnabled = other.SfxEnabled;
            RemoveXiangqiFx = other.RemoveXiangqiFx;
            Sanitize();
        }
    }
}
