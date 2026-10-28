using System;
using System.Globalization;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class DailyLoginData
    {
        public string LastClaimDate = string.Empty;
        public int LastClaimDay;
    }

    /// <summary>
    /// 每日登录。连续领到第 7 天循环，中断则从第 1 天重新开始。
    /// </summary>
    public static class DailyLoginRepository
    {
        private const string PlayerPrefsKey = "SmallGames.DailyLogin";
        private const string DateFormat = "yyyyMMdd";

        public static event Action Changed;

        public static DailyLoginData Current { get; private set; } = new DailyLoginData();

        public static DailyLoginData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var data = JsonUtility.FromJson<DailyLoginData>(SecurePrefs.GetString(PlayerPrefsKey));
                    Current = data ?? new DailyLoginData();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Daily] 读取失败，重置登录: {e.Message}");
                    Current = new DailyLoginData();
                }
            }
            else
            {
                Current = new DailyLoginData();
            }

            if (Current.LastClaimDay < 0 || Current.LastClaimDay > DailyLoginCatalog.Days)
                Current.LastClaimDay = 0;
            return Current;
        }

        public static void Save()
        {
            if (Current == null)
                Current = new DailyLoginData();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        public static bool ClaimedToday
        {
            get
            {
                Ensure();
                return Current.LastClaimDate == TodayKey();
            }
        }

        public static int TodayDay
        {
            get
            {
                Ensure();
                if (ClaimedToday)
                    return Mathf.Clamp(Current.LastClaimDay, 1, DailyLoginCatalog.Days);
                return NextDay();
            }
        }

        public static bool CanClaim => !ClaimedToday;

        public static bool TryClaim()
        {
            Ensure();
            if (ClaimedToday)
                return false;

            var day = NextDay();
            var reward = DailyLoginCatalog.Get(day);
            if (reward.Kind == DailyRewardKind.Energy)
                EnergyRepository.Add(reward.Amount);
            else
                CoinWallet.AddCoins(reward.Amount);

            Current.LastClaimDate = TodayKey();
            Current.LastClaimDay = day;
            Save();
            Changed?.Invoke();
            return true;
        }

        public static bool IsClaimed(int day)
        {
            Ensure();
            if (day < 1 || day > DailyLoginCatalog.Days)
                return false;
            if (string.IsNullOrEmpty(Current.LastClaimDate))
                return false;
            if (MissedStreak())
                return false;
            if (ClaimedToday)
                return day <= Current.LastClaimDay;
            return day < NextDay();
        }

        private static int NextDay()
        {
            Ensure();
            if (string.IsNullOrEmpty(Current.LastClaimDate) || Current.LastClaimDay <= 0)
                return 1;
            if (ClaimedToday)
                return Current.LastClaimDay;
            if (MissedStreak())
                return 1;
            return Current.LastClaimDay >= DailyLoginCatalog.Days ? 1 : Current.LastClaimDay + 1;
        }

        private static bool MissedStreak()
        {
            Ensure();
            if (string.IsNullOrEmpty(Current.LastClaimDate))
                return false;
            if (!DateTime.TryParseExact(Current.LastClaimDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
                return true;
            var gap = (DateTime.Today - last.Date).Days;
            return gap > 1;
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }

        private static string TodayKey()
        {
            return DateTime.Today.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
    }
}
