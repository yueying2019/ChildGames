using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// BGM / SFX 分轨。音量与开关始终读 <see cref="SettingsRepository.Current"/>。
    /// </summary>
    public class AudioManager : SingletonBehaviour<AudioManager>
    {
        public const string ClickClipResource = "Sounds/click-a";
        public const string ClickClipAssetPath = "Assets/Sounds/click-a.ogg";
        public const string BgmClipResource = "Sounds/Bgm/bgm_lobby";
        public const string BgmClipAssetPath = "Assets/Content/Sounds/Bgm/bgm_lobby.ogg";

        private AudioSource _bgm;
        private AudioSource _sfx;
        private AudioClip _clickA;
        private AudioClip _defaultBgm;
        private float _bgmVolumeScale = 1f;

        private GameSettingsData Settings => SettingsRepository.Current ?? new GameSettingsData();

        public void Initialize(GameSettingsData settings)
        {
            if (settings != null)
                SettingsRepository.Current.CopyFrom(settings);

            EnsureListener();
            _bgm = CreateSource("BGM", true);
            _sfx = CreateSource("SFX", false);
            _clickA = AssetHub.Ready ? LoadClip(ClickClipResource, ClickClipAssetPath) : null;
            _defaultBgm = (AssetHub.Ready ? LoadClip(BgmClipResource, BgmClipAssetPath) : null) ?? CreateSoftLoop();
            _bgm.clip = _defaultBgm;
            _bgm.loop = true;
            _bgmVolumeScale = 1f;
            if (GetComponent<UIClickSound>() == null)
                gameObject.AddComponent<UIClickSound>();
            ApplySettings();
        }

        public void PlayClick()
        {
            if (_clickA == null)
                _clickA = LoadClip(ClickClipResource, ClickClipAssetPath);
            PlaySfx(_clickA);
        }

        public static void PlayUiClick()
        {
            if (Instance != null)
                Instance.PlayClick();
        }

        public void ApplySettings()
        {
            var data = Settings;
            data.Sanitize();

            if (_sfx != null)
            {
                _sfx.volume = 1f;
                _sfx.mute = !data.SfxEnabled;
            }

            if (_bgm == null)
                return;

            _bgm.mute = !data.MusicEnabled;
            _bgm.volume = data.MusicEnabled ? data.MusicVolume * Mathf.Clamp01(_bgmVolumeScale) : 0f;
            if (data.MusicEnabled)
            {
                if (_bgm.clip != null && !_bgm.isPlaying)
                    _bgm.Play();
            }
            else if (_bgm.isPlaying)
            {
                _bgm.Pause();
            }
        }

        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            PlayBgm(clip, loop, 1f);
        }

        public void PlayBgm(AudioClip clip, bool loop, float volumeScale)
        {
            if (clip == null || _bgm == null)
                return;

            _bgmVolumeScale = volumeScale > 0.001f ? Mathf.Clamp01(volumeScale) : 1f;
            _bgm.loop = loop;
            if (_bgm.clip != clip)
            {
                _bgm.clip = clip;
                ApplySettings();
                if (Settings.MusicEnabled)
                    _bgm.Play();
                return;
            }

            ApplySettings();
            if (Settings.MusicEnabled && !_bgm.isPlaying)
                _bgm.Play();
        }

        public void PlayDefaultBgm()
        {
            if (_defaultBgm == null)
                _defaultBgm = LoadClip(BgmClipResource, BgmClipAssetPath) ?? CreateSoftLoop();
            PlayBgm(_defaultBgm);
        }

        public void StopBgm()
        {
            if (_bgm != null)
                _bgm.Stop();
        }

        public void PlaySfx(AudioClip clip)
        {
            PlaySfx(clip, 1f, 1f);
        }

        public void PlayCombo(int combo)
        {
            if (_clickA == null)
                _clickA = LoadClip(ClickClipResource, ClickClipAssetPath);
            var pitch = 1f + Mathf.Min(0.45f, Mathf.Max(0, combo - 1) * 0.08f);
            PlaySfx(_clickA, pitch, 1f);
        }

        public void PlaySfx(AudioClip clip, float pitch)
        {
            PlaySfx(clip, pitch, 1f);
        }

        public void PlaySfx(AudioClip clip, float pitch, float volume)
        {
            var data = Settings;
            if (clip == null || _sfx == null || !data.SfxEnabled)
                return;

            var previous = _sfx.pitch;
            _sfx.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            _sfx.PlayOneShot(clip, data.SfxVolume * Mathf.Clamp01(volume));
            _sfx.pitch = previous;
        }

        private static AudioClip LoadClip(string resourcePath, string assetPath)
        {
            var clip = AssetHub.Load<AudioClip>(resourcePath);
#if UNITY_EDITOR
            if (clip == null)
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
            return clip;
        }

        private static void EnsureListener()
        {
            if (Object.FindObjectOfType<AudioListener>() != null)
                return;

            var camera = Camera.main;
            if (camera != null)
                camera.gameObject.AddComponent<AudioListener>();
        }

        private static AudioClip CreateSoftLoop()
        {
            const int hz = 22050;
            const int seconds = 8;
            var count = hz * seconds;
            var samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)hz;
                var wobble = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                samples[i] = (
                    Mathf.Sin(2f * Mathf.PI * 196f * t) * 0.22f +
                    Mathf.Sin(2f * Mathf.PI * 246.94f * t) * 0.14f +
                    Mathf.Sin(2f * Mathf.PI * 293.66f * t) * 0.09f
                ) * wobble * 0.16f;
            }

            var clip = AudioClip.Create("SoftLoop", count, 1, hz, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }
    }
}
