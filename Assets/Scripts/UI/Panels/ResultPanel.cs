using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmallGames
{
    public class ResultPanel : UIPanel
    {
        public const string FilledStarPath = "Assets/UI/PNG/Yellow/Default/star.png";
        public const string OutlineStarPath = "Assets/UI/PNG/Yellow/Default/star_outline.png";

        public Text TitleText;
        public Text MessageText;
        public Button RetryButton;
        public Button MenuButton;
        public Button DoubleButton;
        public Image[] Stars;
        public Sprite FilledStar;
        public Sprite OutlineStar;

        private bool _leaveToLobby;
        private int _doubleBonus;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(MenuButton, () =>
            {
                _leaveToLobby = true;
                GameManager.Instance.ReturnToLevelSelect();
            });
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            _leaveToLobby = false;
            var data = payload as ResultPayload;
            var win = data != null && data.IsWin;
            var endless = data != null && data.IsEndless;
            var daily = data != null && data.IsDaily;
            var next = win && data != null && data.HasNextLevel && !endless && !daily;
            if (TitleText != null)
            {
                if (endless)
                    TitleText.text = "无尽结束";
                else
                    TitleText.text = win ? (next || daily ? "过关" : "通关") : "未完成";
                TitleText.color = win ? UIStyle.AccentGreen : UIStyle.Accent;
            }

            if (MessageText != null)
            {
                // 过关不写「已连通 / 已清空 / 得分」和「评级」，星星已经表达评级。
                var body = win
                    ? string.Empty
                    : (data != null && !string.IsNullOrEmpty(data.Message) ? data.Message : "再试一次吧");
                if (data != null && data.CoinsGained > 0)
                {
                    body = AppendLine(body, win && data.FirstClear
                        ? $"首次通关 {data.CoinsGained} 金币"
                        : $"获得 {data.CoinsGained} 金币");
                }

                // 三星条件只在「没拿到三星」时出现：失败告诉玩家目标是什么，未满星告诉他差多少。
                if (data != null && !data.IsTutorial && !endless && data.Stars < StarCatalog.MaxStars)
                {
                    if (!string.IsNullOrEmpty(data.StarCondition))
                        body = AppendLine(body, "三星：" + data.StarCondition);
                    if (!string.IsNullOrEmpty(data.StarProgress))
                        body = AppendLine(body, data.StarProgress);
                }

                if (data != null && data.IsTutorial && win)
                    body = AppendLine(body, "教学完成，去玩第 1 关吧");

                if (data != null && data.PerfectReward)
                    body = AppendLine(body, $"首次满星！体力已回满（{EnergyCatalog.Max}/{EnergyCatalog.Max}）");

                MessageText.text = body;
            }

            var retryLabel = RetryButton != null ? RetryButton.GetComponentInChildren<Text>() : null;
            if (retryLabel != null)
                retryLabel.text = next ? "下一关" : (daily ? "换一关" : "再来一局");
            var menuLabel = MenuButton != null ? MenuButton.GetComponentInChildren<Text>() : null;
            if (menuLabel != null)
                menuLabel.text = "选关";
            RefreshDoubleButton(data);
            UnityAction retry = next
                ? (UnityAction)(() => GameManager.Instance.StartNextLevel())
                : () => GameManager.Instance.RestartGame(daily);
            Wire(RetryButton, retry);
            ApplyStars(data != null ? data.Stars : 0);
        }

        private static string AppendLine(string body, string line)
        {
            if (string.IsNullOrEmpty(line))
                return body;
            if (string.IsNullOrEmpty(body))
                return line;
            return body + "\n" + line;
        }

        /// <summary>双倍奖励按钮只在「有金币可翻倍且激励视频可用」时出现，用不了就整颗藏起来。</summary>
        private void RefreshDoubleButton(ResultPayload data)
        {
            _doubleBonus = 0;
            if (DoubleButton == null)
                return;

            var bonus = AdDirector.DoubleRewardBonus(data != null ? data.CoinsGained : 0);
            var usable = bonus > 0 && AdDirector.RewardedReady;
            DoubleButton.gameObject.SetActive(usable);
            if (!usable)
                return;

            _doubleBonus = bonus;
            var label = DoubleButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = $"看广告\n双倍 +{bonus}";
            Wire(DoubleButton, OnDoubleClicked);
        }

        private void OnDoubleClicked()
        {
            var bonus = _doubleBonus;
            if (bonus <= 0)
                return;

            AdDirector.WatchRewarded(
                onRewarded: () =>
                {
                    // 看完立刻清零并藏按钮，避免连点重复发奖
                    _doubleBonus = 0;
                    if (DoubleButton != null)
                        DoubleButton.gameObject.SetActive(false);
                    CoinWallet.AddCoins(bonus);
                    if (MessageText != null)
                        MessageText.text += $"\n双倍奖励 +{bonus} 金币";
                    UIManager.Instance?.ShowToast($"双倍奖励 +{bonus} 金币");
                },
                onUnavailable: () => UIManager.Instance?.ShowToast("广告还没准备好，稍后再试"));
        }

        public override void OnClose()
        {
            base.OnClose();
            // 只在「返回选关」这条路径弹插屏：点「下一关 / 再来一局」时弹广告，
            // 会命中运营规范里「点击开始/继续游戏按钮却打开广告」的违规场景。
            if (_leaveToLobby)
                AdDirector.ShowInterstitialAfterResult();
        }

        private void ApplyStars(int stars)
        {
            var images = Stars != null && Stars.Length > 0 ? Stars : CollectStars();
            CacheFilledFrom(images);
            var filled = ResolveFilled();
            var outline = ResolveOutline();
            // 两个贴图在真机上都可能取不到（只能靠 prefab 序列化好的那份），
            // 因此逐个 null 判断，能点亮哪一侧就点哪一侧，不能双双缺席才放弃。
            if (filled == null && outline == null)
                return;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == null)
                    continue;
                if (i < stars)
                {
                    if (filled != null)
                        images[i].sprite = filled;
                }
                else if (outline != null)
                {
                    images[i].sprite = outline;
                }
            }
        }

        private Image[] CollectStars()
        {
            return new[]
            {
                FindStar("Star1"),
                FindStar("Star2"),
                FindStar("Star3")
            };
        }

        private Image FindStar(string name)
        {
            var child = transform.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void CacheFilledFrom(Image[] stars)
        {
            if (FilledStar != null || stars == null)
                return;
            for (var i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null && stars[i].sprite != null)
                {
                    FilledStar = stars[i].sprite;
                    return;
                }
            }
        }

        private Sprite ResolveFilled()
        {
            return FilledStar != null ? FilledStar : LoadSprite(FilledStarPath);
        }

        private Sprite ResolveOutline()
        {
            if (OutlineStar != null)
                return OutlineStar;
            OutlineStar = LoadSprite(OutlineStarPath);
            return OutlineStar;
        }

        private static Sprite LoadSprite(string assetPath)
        {
#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null)
                return null;
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite found)
                    return found;
            }
#endif
            return null;
        }
    }
}
