using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class SplashPanel : UIPanel
    {
        public Button ContinueButton;
        public Text TitleText;
        public Text HealthTitleText;
        public Text HealthBodyText;
        public Text ABDetail;
        public Slider ABSlider;

        private Coroutine _wait;
        private bool _ready;

        public override void BindListeners()
        {
            base.BindListeners();
            if (TitleText != null)
                TitleText.text = GameBootstrap.AppName;
            if (HealthTitleText != null)
                HealthTitleText.text = ComplianceCatalog.HealthTitle;
            if (HealthBodyText != null)
                HealthBodyText.text = ComplianceCatalog.HealthBody;
            Wire(ContinueButton, TryContinue);
            if (ABSlider != null)
            {
                ABSlider.minValue = 0f;
                ABSlider.maxValue = 1f;
                ABSlider.wholeNumbers = false;
                ABSlider.interactable = false;
                ABSlider.value = 0f;
            }

            SetAbProgress(1f, string.Empty);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            _ready = false;
            if (ContinueButton != null)
                ContinueButton.interactable = false;
            SetAbProgress(1f, string.Empty);
            if (_wait != null)
                StopCoroutine(_wait);
            _wait = StartCoroutine(WaitThenContinue());
            Analytics.Track("splash_show");
        }

        public override void OnClose()
        {
            if (_wait != null)
            {
                StopCoroutine(_wait);
                _wait = null;
            }

            _ready = false;
            base.OnClose();
        }

        private IEnumerator WaitThenContinue()
        {
            var started = Time.unscaledTime;
            var remain = ComplianceCatalog.SplashSeconds - (Time.unscaledTime - started);
            if (remain > 0f)
                yield return new WaitForSecondsRealtime(remain);

            _ready = true;
            SetAbProgress(1f, string.Empty);
            _wait = null;
            GameBootstrap.ContinueFromSplash();
        }

        private void SetAbProgress(float progress, string detail)
        {
            if (ABSlider != null)
                ABSlider.value = Mathf.Clamp01(progress);
            if (ABDetail != null)
                ABDetail.text = string.IsNullOrEmpty(detail) ? string.Empty : detail;
        }

        private void TryContinue()
        {
            if (!_ready)
                return;
            GameBootstrap.ContinueFromSplash();
        }
    }
}
