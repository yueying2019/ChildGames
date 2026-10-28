namespace SmallGames
{
    public enum DailyRewardKind
    {
        Coins = 0,
        Energy = 1
    }

    public readonly struct DailyReward
    {
        public DailyRewardKind Kind { get; }
        public int Amount { get; }
        public string Label { get; }

        public DailyReward(DailyRewardKind kind, int amount, string label)
        {
            Kind = kind;
            Amount = amount;
            Label = label;
        }
    }

    public static class DailyLoginCatalog
    {
        public const int Days = 7;

        public static readonly DailyReward[] Rewards =
        {
            new DailyReward(DailyRewardKind.Coins, 50, "50 金币"),
            new DailyReward(DailyRewardKind.Coins, 80, "80 金币"),
            new DailyReward(DailyRewardKind.Energy, 1, "体力 +1"),
            new DailyReward(DailyRewardKind.Coins, 120, "120 金币"),
            new DailyReward(DailyRewardKind.Energy, 2, "体力 +2"),
            new DailyReward(DailyRewardKind.Coins, 180, "180 金币"),
            new DailyReward(DailyRewardKind.Coins, 300, "300 金币")
        };

        public static DailyReward Get(int day)
        {
            var index = ((day - 1) % Days + Days) % Days;
            return Rewards[index];
        }
    }
}
