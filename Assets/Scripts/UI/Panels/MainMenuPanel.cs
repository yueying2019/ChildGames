using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class MainMenuPanel : UIPanel
    {
        public Text TitleText;
        public Button SettingsButton;
        public Button LianliankanButton;
        public Button SheepButton;
        public Button Match3Button;
        public Button JieqiButton;
        public Button SkillButton;
        public Button ShopButton;
        public Button DailyButton;
        public Button TaskButton;
        public Button CodexButton;
        public Button EnergyButton;
        public Text CoinText;
        public Text EnergyText;
        public Text EnergyTimerText;
        public Button VersionButton;

        public override void BindListeners()
        {
            base.BindListeners();
            if (TitleText != null)
                TitleText.text = GameBootstrap.AppName;

            Wire(SettingsButton, () => GameManager.Instance.OpenSettings());
            Wire(ShopButton, () => GameManager.Instance.OpenShop());
            Wire(DailyButton, () => GameManager.Instance.OpenDailyLogin());
            Wire(TaskButton, () => UIManager.Instance?.Open(UIPanelId.DailyTask));
            Wire(CodexButton, () => UIManager.Instance?.Open(UIPanelId.Codex));
            Wire(EnergyButton, () => GameManager.Instance.OpenEnergy());
            Wire(LianliankanButton, () => ClickMode(GameModeType.Lianliankan));
            Wire(SheepButton, () => ClickMode(GameModeType.Sheep));
            Wire(Match3Button, () => ClickMode(GameModeType.Match3));
            Wire(JieqiButton, () => ClickMode(GameModeType.Jieqi));
            Wire(SkillButton, () => ClickMode(GameModeType.SkillXiangqi));
            BindVersionButton();
        }

        private void BindVersionButton()
        {
            if (VersionButton == null)
                return;

            var dbl = VersionButton.GetComponent<UiDoubleClick>();
            if (dbl == null)
                return;

            dbl.onDoubleClick.RemoveListener(OpenTestWindow);
            dbl.onDoubleClick.AddListener(OpenTestWindow);
        }

        private static void OpenTestWindow()
        {
            UIManager.Instance.Open(UIPanelId.TestWindow);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            CoinWallet.Changed += RefreshCoins;
            EnergyRepository.Changed += RefreshEnergy;
            DailyTaskDirector.Changed += RefreshTask;
            RefreshCoins();
            RefreshEnergy();
            RefreshTask();
            AdDirector.ShowLobbyBanner();
        }

        public override void OnClose()
        {
            AdDirector.HideBanner();
            CoinWallet.Changed -= RefreshCoins;
            EnergyRepository.Changed -= RefreshEnergy;
            DailyTaskDirector.Changed -= RefreshTask;
            base.OnClose();
        }

        private void Update()
        {
            if (!IsOpen)
                return;
            RefreshEnergyTimer();
        }

        private void OnDestroy()
        {
            CoinWallet.Changed -= RefreshCoins;
            EnergyRepository.Changed -= RefreshEnergy;
            DailyTaskDirector.Changed -= RefreshTask;
        }

        /// <summary>任务按钮当红点用：有可领奖励时改文案。</summary>
        private void RefreshTask()
        {
            if (TaskButton == null)
                return;
            var label = TaskButton.GetComponentInChildren<Text>();
            if (label == null)
                return;
            label.text = DailyTaskRepository.HasClaimable() ? "可领奖励" : "任务";
        }

        private static void ClickMode(GameModeType mode)
        {
            Analytics.ModeClick(mode);
            // 第一次进某玩法直接上教学关，学完（或跳过）之后才回到选关页。
            if (TutorialCatalog.Supports(mode) && !TutorialCatalog.IsCleared(mode))
                GameManager.Instance.StartTutorial(mode);
            else
                GameManager.Instance.OpenLevelSelect(mode);
        }

        private void RefreshCoins()
        {
            if (CoinText != null)
                CoinText.text = $"金币 {CoinWallet.Coins}";
        }

        private void RefreshEnergy()
        {
            if (EnergyText != null)
                EnergyText.text = EnergyRepository.StatusLabel();
            RefreshEnergyTimer();
        }

        private void RefreshEnergyTimer()
        {
            if (EnergyTimerText != null)
                EnergyTimerText.text = EnergyRepository.TimerLabel();
        }
    }
}
