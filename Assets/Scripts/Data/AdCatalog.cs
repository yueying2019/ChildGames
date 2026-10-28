namespace SmallGames
{
    /// <summary>
    /// 单机包关闭广告。广告位 ID 留空，Has* 恒为 false。
    /// </summary>
    public static class AdCatalog
    {
        public const string RewardedAdUnitId = "";
        public const string InterstitialAdUnitId = "";
        public const string BannerAdUnitId = "";
        public const int InterstitialMinIntervalSeconds = 90;
        public const int BannerAdIntervals = 30;
        public const int BannerHeight = 100;
        public const int DoubleRewardMultiplier = 2;

        public static bool HasRewarded => false;
        public static bool HasInterstitial => false;
        public static bool HasBanner => false;
    }
}
