using System;
using UnityEngine;

namespace SmallGames
{
    [Serializable]
    public sealed class WalletData
    {
        public int Coins;
        public int LianShuffle;
        public int LianHint;
        public int SheepRemove;
        public int SheepUndo;
        public int SheepShuffle;
        public int SheepSlot;
        public int Match3Hint;
        public int Match3Shuffle;
        public int Match3Hammer;
        public int Match3LineH;
        public int Match3LineV;
        public int Match3Bomb;
        public int Match3Moves;
        public int LianTime;
        public int LianFreeze;
        public int LianAuto;
        public int Match3ColorBomb;
        public int Match3ColorClear;
    }

    /// <summary>
    /// 金币与已购道具。用 PlayerPrefs 持久化。
    /// </summary>
    public static class CoinWallet
    {
        private const string PlayerPrefsKey = "SmallGames.Wallet";

        public static event Action Changed;

        /// <summary>刚发放、还没在道具角标上跳过的道具。界面刷新时用掉。</summary>
        public static ShopItemId PendingGrantId { get; private set; } = ShopItemId.None;

        public static WalletData Current { get; private set; } = new WalletData();

        public static int Coins => Current != null ? Current.Coins : 0;

        public static WalletData Load()
        {
            if (SecurePrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    var data = JsonUtility.FromJson<WalletData>(SecurePrefs.GetString(PlayerPrefsKey));
                    Current = data ?? new WalletData();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Wallet] 读取失败，使用空钱包: {e.Message}");
                    Current = new WalletData();
                }
            }
            else
            {
                Current = new WalletData();
            }

            Clamp();
            return Current;
        }

        public static void Save()
        {
            if (Current == null)
                Current = new WalletData();
            Clamp();
            SecurePrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(Current));
            SecurePrefs.Save();
        }

        public static int Count(ShopItemId id)
        {
            Ensure();
            switch (id)
            {
                case ShopItemId.LianShuffle: return Current.LianShuffle;
                case ShopItemId.LianHint: return Current.LianHint;
                case ShopItemId.SheepRemove: return Current.SheepRemove;
                case ShopItemId.SheepUndo: return Current.SheepUndo;
                case ShopItemId.SheepShuffle: return Current.SheepShuffle;
                case ShopItemId.SheepSlot: return Current.SheepSlot;
                case ShopItemId.Match3Hint: return Current.Match3Hint;
                case ShopItemId.Match3Shuffle: return Current.Match3Shuffle;
                case ShopItemId.Match3Hammer: return Current.Match3Hammer;
                case ShopItemId.Match3LineH: return Current.Match3LineH;
                case ShopItemId.Match3LineV: return Current.Match3LineV;
                case ShopItemId.Match3Bomb: return Current.Match3Bomb;
                case ShopItemId.Match3Moves: return Current.Match3Moves;
                case ShopItemId.LianTime: return Current.LianTime;
                case ShopItemId.LianFreeze: return Current.LianFreeze;
                case ShopItemId.LianAuto: return Current.LianAuto;
                case ShopItemId.Match3ColorBomb: return Current.Match3ColorBomb;
                case ShopItemId.Match3ColorClear: return Current.Match3ColorClear;
                default: return 0;
            }
        }

        public static int AddCoins(int amount)
        {
            Ensure();
            if (amount == 0)
                return Current.Coins;
            Current.Coins = Mathf.Max(0, Current.Coins + amount);
            Save();
            Changed?.Invoke();
            return Current.Coins;
        }

        public static bool TryBuy(ShopItemId id)
        {
            if (!ShopCatalog.TryGet(id, out var info))
                return false;
            Ensure();
            if (Current.Coins < info.Price)
                return false;
            Current.Coins -= info.Price;
            AddItem(id, 1);
            Save();
            Changed?.Invoke();
            Analytics.ItemBuy(id, info.Price);
            return true;
        }

        public static bool TryConsume(ShopItemId id)
        {
            Ensure();
            if (Count(id) <= 0)
                return false;
            AddItem(id, -1);
            Save();
            Changed?.Invoke();
            Analytics.ItemUse(id);
            return true;
        }

        public static int Available(ShopItemId id) => Count(id);

        public static bool TryUse(ShopItemId id) => TryConsume(id);

        /// <summary>直接发放道具，不扣金币。过关奖励走这里。</summary>
        public static void GrantItem(ShopItemId id, int count)
        {
            if (count <= 0 || id == ShopItemId.None)
                return;
            Ensure();
            AddItem(id, count);
            PendingGrantId = id;
            Save();
            Changed?.Invoke();
        }

        /// <summary>道具角标弹出后清掉，避免下一次刷新再跳一次。</summary>
        public static bool ConsumePendingGrant(ShopItemId id)
        {
            if (PendingGrantId != id)
                return false;
            PendingGrantId = ShopItemId.None;
            return true;
        }

        private static void AddItem(ShopItemId id, int delta)
        {
            switch (id)
            {
                case ShopItemId.LianShuffle: Current.LianShuffle = Mathf.Max(0, Current.LianShuffle + delta); break;
                case ShopItemId.LianHint: Current.LianHint = Mathf.Max(0, Current.LianHint + delta); break;
                case ShopItemId.SheepRemove: Current.SheepRemove = Mathf.Max(0, Current.SheepRemove + delta); break;
                case ShopItemId.SheepUndo: Current.SheepUndo = Mathf.Max(0, Current.SheepUndo + delta); break;
                case ShopItemId.SheepShuffle: Current.SheepShuffle = Mathf.Max(0, Current.SheepShuffle + delta); break;
                case ShopItemId.SheepSlot: Current.SheepSlot = Mathf.Max(0, Current.SheepSlot + delta); break;
                case ShopItemId.Match3Hint: Current.Match3Hint = Mathf.Max(0, Current.Match3Hint + delta); break;
                case ShopItemId.Match3Shuffle: Current.Match3Shuffle = Mathf.Max(0, Current.Match3Shuffle + delta); break;
                case ShopItemId.Match3Hammer: Current.Match3Hammer = Mathf.Max(0, Current.Match3Hammer + delta); break;
                case ShopItemId.Match3LineH: Current.Match3LineH = Mathf.Max(0, Current.Match3LineH + delta); break;
                case ShopItemId.Match3LineV: Current.Match3LineV = Mathf.Max(0, Current.Match3LineV + delta); break;
                case ShopItemId.Match3Bomb: Current.Match3Bomb = Mathf.Max(0, Current.Match3Bomb + delta); break;
                case ShopItemId.Match3Moves: Current.Match3Moves = Mathf.Max(0, Current.Match3Moves + delta); break;
                case ShopItemId.LianTime: Current.LianTime = Mathf.Max(0, Current.LianTime + delta); break;
                case ShopItemId.LianFreeze: Current.LianFreeze = Mathf.Max(0, Current.LianFreeze + delta); break;
                case ShopItemId.LianAuto: Current.LianAuto = Mathf.Max(0, Current.LianAuto + delta); break;
                case ShopItemId.Match3ColorBomb: Current.Match3ColorBomb = Mathf.Max(0, Current.Match3ColorBomb + delta); break;
                case ShopItemId.Match3ColorClear: Current.Match3ColorClear = Mathf.Max(0, Current.Match3ColorClear + delta); break;
            }
        }

        private static void Ensure()
        {
            if (Current == null)
                Load();
        }

        private static void Clamp()
        {
            if (Current == null)
                Current = new WalletData();
            Current.Coins = Mathf.Max(0, Current.Coins);
            Current.LianShuffle = Mathf.Max(0, Current.LianShuffle);
            Current.LianHint = Mathf.Max(0, Current.LianHint);
            Current.SheepRemove = Mathf.Max(0, Current.SheepRemove);
            Current.SheepUndo = Mathf.Max(0, Current.SheepUndo);
            Current.SheepShuffle = Mathf.Max(0, Current.SheepShuffle);
            Current.SheepSlot = Mathf.Max(0, Current.SheepSlot);
            Current.Match3Hint = Mathf.Max(0, Current.Match3Hint);
            Current.Match3Shuffle = Mathf.Max(0, Current.Match3Shuffle);
            Current.Match3Hammer = Mathf.Max(0, Current.Match3Hammer);
            Current.Match3LineH = Mathf.Max(0, Current.Match3LineH);
            Current.Match3LineV = Mathf.Max(0, Current.Match3LineV);
            Current.Match3Bomb = Mathf.Max(0, Current.Match3Bomb);
            Current.Match3Moves = Mathf.Max(0, Current.Match3Moves);
            Current.LianTime = Mathf.Max(0, Current.LianTime);
            Current.LianFreeze = Mathf.Max(0, Current.LianFreeze);
            Current.LianAuto = Mathf.Max(0, Current.LianAuto);
            Current.Match3ColorBomb = Mathf.Max(0, Current.Match3ColorBomb);
            Current.Match3ColorClear = Mathf.Max(0, Current.Match3ColorClear);
        }
    }
}
