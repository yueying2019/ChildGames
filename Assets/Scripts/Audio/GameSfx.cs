using UnityEngine;

namespace SmallGames
{
    [System.Serializable]
    public struct GameSfx
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
        [Range(0.5f, 2f)] public float Pitch;

        public static GameSfx Default => new GameSfx { Volume = 1f, Pitch = 1f };

        public void Play()
        {
            AudioManager.Instance?.PlaySfx(Clip, Pitch, Volume);
        }

        public void PlayBgm()
        {
            if (Clip == null)
                return;
            var volume = Volume > 0.001f ? Volume : 1f;
            AudioManager.Instance?.PlayBgm(Clip, true, volume);
        }
    }
}
