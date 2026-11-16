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
        /// <summary>0 普通、1 快、2 慢。旧存档没有这个字段时是 0，仍按现在的两层搜索。</summary>
        public int XiangqiAiPace;

        public int XiangqiSearchDepth()
        {
            if (XiangqiAiPace == 1)
                return 1;
            if (XiangqiAiPace == 2)
                return 3;
            return 2;
        }

        public void Sanitize()
        {
            MusicVolume = Mathf.Clamp01(MusicVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
            if (XiangqiAiPace < 0 || XiangqiAiPace > 2)
                XiangqiAiPace = 0;
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
            XiangqiAiPace = other.XiangqiAiPace;
            Sanitize();
        }
    }
}
