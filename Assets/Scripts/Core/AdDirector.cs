using System;

namespace SmallGames
{
    /// <summary>
    /// 单机包不接广告。入口仍留给大厅 / 结算 / 续关调用，全部降级为不可用。
    /// </summary>
    public static class AdDirector
    {
        public static bool CanShow => false;

        public static bool RewardedReady => false;

        public static void Initialize()
        {
        }

        public static void ShowLobbyBanner()
        {
        }

        public static void HideBanner()
        {
        }

        public static void ShowInterstitialAfterResult()
        {
        }

        public static void WatchRewarded(Action onRewarded, Action onUnavailable = null, Action onAbandoned = null)
        {
            if (onUnavailable != null)
                onUnavailable();
        }

        public static int DoubleRewardBonus(int coinsGained)
        {
            return 0;
        }
    }
}
