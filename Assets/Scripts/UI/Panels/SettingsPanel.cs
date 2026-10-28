using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class SettingsPanel : UIPanel
    {
        public Button DimButton;
        public Button ResetButton;
        public Button CloseButton;
        public Button QuitButton;
        public Toggle MusicToggle;
        public Toggle SfxToggle;
        public Image MusicToggleBox;
        public Image SfxToggleBox;
        public Text MusicToggleState;
        public Text SfxToggleState;
        public Slider MusicSlider;
        public Slider SfxSlider;
        public Text MusicValue;
        public Text SfxValue;

    private float _sfxPreviewAt;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, () => UIManager.Instance.Close(UIPanelId.Settings));
            Wire(CloseButton, () => UIManager.Instance.Close(UIPanelId.Settings));
            Wire(ResetButton, ResetDefaults);
            Wire(QuitButton, GameBootstrap.QuitApp);

            BindToggle(MusicToggle, MusicToggleBox, MusicToggleState, on =>
            {
                SettingsRepository.Current.MusicEnabled = on;
                ApplyAndSave();
            });
            BindToggle(SfxToggle, SfxToggleBox, SfxToggleState, on =>
            {
                SettingsRepository.Current.SfxEnabled = on;
                ApplyAndSave();
            });

            BindSlider(MusicSlider, MusicValue, v =>
            {
                SettingsRepository.Current.MusicVolume = v;
                ApplyAndSave();
            });
            BindSlider(SfxSlider, SfxValue, v =>
            {
                SettingsRepository.Current.SfxVolume = v;
                ApplyAndSave();
                if (Time.unscaledTime - _sfxPreviewAt < 0.12f)
                    return;
                _sfxPreviewAt = Time.unscaledTime;
                AudioManager.Instance?.PlayClick();
            });
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            RefreshFromData();
        }

        private void RefreshFromData()
        {
            var data = SettingsRepository.Current;
            SetToggle(MusicToggle, MusicToggleBox, MusicToggleState, data.MusicEnabled);
            SetToggle(SfxToggle, SfxToggleBox, SfxToggleState, data.SfxEnabled);
            if (MusicSlider != null)
                MusicSlider.SetValueWithoutNotify(data.MusicVolume);
            if (SfxSlider != null)
                SfxSlider.SetValueWithoutNotify(data.SfxVolume);
            if (MusicValue != null)
                MusicValue.text = Percent(data.MusicVolume);
            if (SfxValue != null)
                SfxValue.text = Percent(data.SfxVolume);
        }

        private void ResetDefaults()
        {
            SettingsRepository.ResetToDefault();
            RefreshFromData();
            AudioManager.Instance?.ApplySettings();
            UIManager.Instance.ShowToast("已恢复默认设置");
        }

        private static void ApplyAndSave()
        {
            SettingsRepository.Save();
            AudioManager.Instance?.ApplySettings();
        }

        private static void BindToggle(Toggle toggle, Image box, Text state, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            if (toggle == null)
                return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                ApplyToggleVisual(box, state, on);
                onChanged?.Invoke(on);
            });
        }

        private static void BindSlider(Slider slider, Text valueLabel, UnityEngine.Events.UnityAction<float> onChanged)
        {
            if (slider == null)
                return;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(v =>
            {
                if (valueLabel != null)
                    valueLabel.text = Percent(v);
                onChanged?.Invoke(v);
            });
        }

        private static void SetToggle(Toggle toggle, Image box, Text state, bool isOn)
        {
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(isOn);
            ApplyToggleVisual(box, state, isOn);
        }

        private static void ApplyToggleVisual(Image box, Text state, bool isOn)
        {
            if (box != null)
                box.color = isOn ? UIStyle.None : UIStyle.TabSelect;
            if (state != null)
                state.text = isOn ? "开" : "关";
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
