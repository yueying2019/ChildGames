using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 进关前的关卡名。全屏浅黑遮罩挡住操作，2 秒后自己关掉，再由调用方正式开局。
    /// </summary>
    public class LevelIntroPanel : UIPanel
    {
        public const float HoldSeconds = 2f;

        public Text Title;
        public Text Name;

        private Action _onDone;
        private Coroutine _hold;

        /// <summary>title 写「第X关」，name 写关卡名称。</summary>
        public static void Present(int levelNumber, string levelName, Action onEntered)
        {
            if (UIManager.Instance == null || AssetHub.Load<GameObject>(UiPrefabs.LevelIntro) == null)
            {
                if (onEntered != null)
                    onEntered();
                return;
            }

            UIManager.Instance.Open(UIPanelId.LevelIntro, new Request
            {
                LevelNumber = levelNumber,
                LevelName = levelName,
                OnDone = onEntered
            });
        }

        /// <summary>退出对局时清掉还没到期的开局回调，避免关掉界面后又把计时打开。</summary>
        public static void Dismiss()
        {
            if (UIManager.Instance == null || !UIManager.Instance.IsOpen(UIPanelId.LevelIntro))
                return;

            var panel = UIManager.Instance.Get<LevelIntroPanel>(UIPanelId.LevelIntro);
            if (panel != null)
                panel._onDone = null;
            UIManager.Instance.Close(UIPanelId.LevelIntro);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            var request = payload as Request;
            _onDone = request != null ? request.OnDone : null;
            if (Title != null)
                Title.text = request != null ? "第" + request.LevelNumber + "关" : string.Empty;
            if (Name != null)
                Name.text = request != null && !string.IsNullOrEmpty(request.LevelName) ? request.LevelName : string.Empty;
            if (_hold != null)
                StopCoroutine(_hold);
            _hold = StartCoroutine(Hold());
        }

        public override void OnClose()
        {
            if (_hold != null)
            {
                StopCoroutine(_hold);
                _hold = null;
            }

            base.OnClose();
        }

        private IEnumerator Hold()
        {
            yield return new WaitForSecondsRealtime(HoldSeconds);
            _hold = null;
            var done = _onDone;
            _onDone = null;
            if (UIManager.Instance != null)
                UIManager.Instance.Close(UIPanelId.LevelIntro);
            if (done != null)
                done();
        }

        private sealed class Request
        {
            public int LevelNumber;
            public string LevelName;
            public Action OnDone;
        }
    }
}
