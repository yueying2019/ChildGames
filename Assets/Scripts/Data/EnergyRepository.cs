using System;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class EnergyData
    {
        public int Energy = EnergyCatalog.Max;
        public long RegenAnchorTicks;
    }

    /// <summary>
    /// 体力：开局消耗，低于上限后按时间回复。
    /// </summary>
    public static class EnergyRepository
    {
        private const string PlayerPrefsKey = "SmallGames.Energy";

        public static event Action Changed;

        public static EnergyData Current { get; private set; } = new EnergyData();

        public static int Energy
        {
            get
            {
                ApplyRegen();
                return Current != null ? Current.Energy : EnergyCatalog.Max;
            }
        }

        public static bool IsFull
        {
            get
            {
                ApplyRegen();
                return Current.Energy >= EnergyCatalog.Max;
            }
        }

        public static EnergyData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var data = JsonUtility.FromJson<EnergyData>(SecurePrefs.GetString(PlayerPrefsKey));
                    Current = data ?? new EnergyData();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Energy] 读取失败，使用满体力: {e.Message}");
                    Current = new EnergyData();
                }
            }
            else
            {
                Current = new EnergyData();
            }

            if (Current.Energy < 0)
                Current.Energy = 0;
            ApplyRegen();
            return Current;
        }

        public static void Save()
        {
            if (Current == null)
                Current = new EnergyData();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        public static void Tick()
        {
            var before = Current != null ? Current.Energy : 0;
            var anchor = Current != null ? Current.RegenAnchorTicks : 0L;
            ApplyRegen();
            if (Current == null)
                return;
            if (Current.Energy != before || Current.RegenAnchorTicks != anchor)
                Changed?.Invoke();
        }

        public static bool TrySpend(int amount = EnergyCatalog.CostPerPlay)
        {
            ApplyRegen();
            Ensure();
            if (amount <= 0)
                return true;
            if (Current.Energy < amount)
                return false;
            Current.Energy -= amount;
            if (Current.Energy < EnergyCatalog.Max && Current.RegenAnchorTicks <= 0)
                Current.RegenAnchorTicks = NowTicks();
            Save();
            Changed?.Invoke();
            return true;
        }

        public static void Add(int amount)
        {
            ApplyRegen();
            Ensure();
            if (amount == 0)
                return;
            Current.Energy = Mathf.Max(0, Current.Energy + amount);
            if (Current.Energy >= EnergyCatalog.Max)
                Current.RegenAnchorTicks = 0;
            else if (Current.RegenAnchorTicks <= 0)
                Current.RegenAnchorTicks = NowTicks();
            Save();
            Changed?.Invoke();
        }

        public static bool TryBuyOne()
        {
            if (CoinWallet.Coins < EnergyCatalog.BuyOnePrice)
                return false;
            CoinWallet.AddCoins(-EnergyCatalog.BuyOnePrice);
            Add(EnergyCatalog.BuyOneAmount);
            return true;
        }

        public static bool TryBuyFull()
        {
            ApplyRegen();
            Ensure();
            if (Current.Energy >= EnergyCatalog.Max)
                return true;
            if (CoinWallet.Coins < EnergyCatalog.BuyFullPrice)
                return false;
            CoinWallet.AddCoins(-EnergyCatalog.BuyFullPrice);
            Add(EnergyCatalog.Max - Current.Energy);
            return true;
        }

        public static string StatusLabel()
        {
            return $"体力 {Energy}/{EnergyCatalog.Max}";
        }

        public static string TimerLabel()
        {
            ApplyRegen();
            if (Current.Energy >= EnergyCatalog.Max)
                return "已满";
            var left = NextRegenSeconds();
            var minutes = left / 60;
            var seconds = left % 60;
            return $"{minutes:00}:{seconds:00} 后 +1";
        }

        public static int NextRegenSeconds()
        {
            ApplyRegen();
            Ensure();
            if (Current.Energy >= EnergyCatalog.Max || Current.RegenAnchorTicks <= 0)
                return 0;
            var elapsed = NowTicks() - Current.RegenAnchorTicks;
            var remain = EnergyCatalog.RegenSeconds - (int)(elapsed % EnergyCatalog.RegenSeconds);
            if (remain <= 0)
                remain = EnergyCatalog.RegenSeconds;
            return remain;
        }

        public static void ApplyRegen()
        {
            Ensure();
            if (Current.Energy >= EnergyCatalog.Max)
            {
                Current.RegenAnchorTicks = 0;
                return;
            }

            if (Current.RegenAnchorTicks <= 0)
            {
                Current.RegenAnchorTicks = NowTicks();
                Save();
                return;
            }

            var elapsed = NowTicks() - Current.RegenAnchorTicks;
            if (elapsed < EnergyCatalog.RegenSeconds)
                return;

            var gained = (int)(elapsed / EnergyCatalog.RegenSeconds);
            if (gained <= 0)
                return;

            Current.Energy = Mathf.Min(EnergyCatalog.Max, Current.Energy + gained);
            if (Current.Energy >= EnergyCatalog.Max)
                Current.RegenAnchorTicks = 0;
            else
                Current.RegenAnchorTicks += (long)gained * EnergyCatalog.RegenSeconds;
            Save();
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }

        private static long NowTicks()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
