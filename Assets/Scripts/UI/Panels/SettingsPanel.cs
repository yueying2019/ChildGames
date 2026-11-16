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
        public Toggle FxToggle;
        public Image FxToggleBox;
        public Text FxToggleState;
        public Toggle PaceToggle;
        public Text PaceState;

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
            BindToggle(FxToggle, FxToggleBox, FxToggleState, on =>
            {
                SettingsRepository.Current.RemoveXiangqiFx = on;
                SettingsRepository.Save();
            }, "是", "否");
            BindPace();

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
            SetToggle(FxToggle, FxToggleBox, FxToggleState, data.RemoveXiangqiFx, "是", "否");
            ShowPace(data.XiangqiAiPace);
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

        private void BindPace()
        {
            if (PaceToggle == null)
                return;
            PaceToggle.onValueChanged.RemoveAllListeners();
            PaceToggle.onValueChanged.AddListener(_ =>
            {
                var data = SettingsRepository.Current;
                if (data.XiangqiAiPace == 0)
                    data.XiangqiAiPace = 1;
                else if (data.XiangqiAiPace == 1)
                    data.XiangqiAiPace = 2;
                else
                    data.XiangqiAiPace = 0;
                SettingsRepository.Save();
                ShowPace(data.XiangqiAiPace);
            });
        }

        private void ShowPace(int pace)
        {
            if (PaceState == null)
                return;
            if (pace == 1)
                PaceState.text = "快";
            else if (pace == 2)
                PaceState.text = "慢";
            else
                PaceState.text = "普通";
        }

        private static void BindToggle(Toggle toggle, Image box, Text state, UnityEngine.Events.UnityAction<bool> onChanged, string onText = "开", string offText = "关")
        {
            if (toggle == null)
                return;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                ApplyToggleVisual(box, state, on, onText, offText);
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

        private static void SetToggle(Toggle toggle, Image box, Text state, bool isOn, string onText = "开", string offText = "关")
        {
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(isOn);
            ApplyToggleVisual(box, state, isOn, onText, offText);
        }

        private static void ApplyToggleVisual(Image box, Text state, bool isOn, string onText = "开", string offText = "关")
        {
            if (box != null)
                box.color = isOn ? UIStyle.None : UIStyle.TabSelect;
            if (state != null)
                state.text = isOn ? onText : offText;
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
