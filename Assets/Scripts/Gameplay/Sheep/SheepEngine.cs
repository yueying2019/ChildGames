using System.Collections.Generic;

namespace SmallGames
{
    public enum SheepRoundEnd
    {
        None = 0,
        Win = 1,
        Lose = 2
    }

    /// <summary>
    /// 叠叠消规则：相同图案在槽里并排，凑够一组就消除。
    /// 目标组数先于「槽满」判胜；不限目标时，场上没有还亮着的牌即胜。
    /// 不碰棋子物体，遮挡和飞行动画留在界面。
    /// </summary>
    public sealed class SheepEngine
    {
        private readonly List<int> _slots = new List<int>(8);

        public int Compose { get; private set; } = SheepLevelData.DefaultCompose;
        public int SlotLimit { get; private set; } = 7;
        public int GoalCount { get; private set; }
        public int GroupsCleared { get; private set; }
        public int Combo { get; private set; }
        public int ComboPeak { get; private set; }
        public int SlotCount => _slots.Count;
        public bool SlotFull => _slots.Count >= SlotLimit;

        public void Load(int compose, int slotLimit, int goalCount)
        {
            if (compose < SheepLevelData.MinCompose || compose > SheepLevelData.MaxCompose)
                compose = SheepLevelData.DefaultCompose;
            Compose = compose;
            SlotLimit = slotLimit < 1 ? 1 : slotLimit;
            GoalCount = goalCount < 0 ? 0 : goalCount;
            GroupsCleared = 0;
            Combo = 0;
            ComboPeak = 0;
            _slots.Clear();
        }

        public void SetSlotLimit(int slotLimit)
        {
            SlotLimit = slotLimit < 1 ? 1 : slotLimit;
        }

        /// <summary>撤回前记下槽里的图案顺序。组数和连击用 <see cref="CaptureProgress"/> 一起记。</summary>
        public int[] CaptureSlots()
        {
            return _slots.ToArray();
        }

        public void RestoreSlots(int[] slots)
        {
            _slots.Clear();
            if (slots == null)
                return;
            for (var i = 0; i < slots.Length; i++)
                _slots.Add(slots[i]);
        }

        /// <summary>撤回前记下已经消除的组数和连击。峰值只升不降，撤回后仍保留本局最高连击。</summary>
        public void CaptureProgress(out int groups, out int combo, out int peak)
        {
            groups = GroupsCleared;
            combo = Combo;
            peak = ComboPeak;
        }

        public void RestoreProgress(int groups, int combo, int peak)
        {
            GroupsCleared = groups < 0 ? 0 : groups;
            Combo = combo < 0 ? 0 : combo;
            if (peak < ComboPeak)
                peak = ComboPeak;
            if (peak < Combo)
                peak = Combo;
            ComboPeak = peak;
        }

        /// <summary>插到同图案最后一张的后面。返回插入下标。</summary>
        public int Insert(int iconId)
        {
            var last = -1;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == iconId)
                    last = i;
            }

            var index = last < 0 ? _slots.Count : last + 1;
            _slots.Insert(index, iconId);
            return index;
        }

        /// <summary>
        /// 从槽尾取走最先凑齐的一组。
        /// 同时有多种图案凑齐时，取槽里更靠前的那种。
        /// </summary>
        public bool TryTakeMatch(out int iconId)
        {
            iconId = -1;
            if (_slots.Count < Compose)
                return false;

            var matchId = -1;
            for (var i = 0; i < _slots.Count; i++)
            {
                var id = _slots[i];
                var count = 0;
                for (var j = 0; j < _slots.Count; j++)
                {
                    if (_slots[j] == id)
                        count++;
                }

                if (count < Compose)
                    continue;
                matchId = id;
                break;
            }

            if (matchId < 0)
                return false;

            var removed = 0;
            for (var i = _slots.Count - 1; i >= 0 && removed < Compose; i--)
            {
                if (_slots[i] != matchId)
                    continue;
                _slots.RemoveAt(i);
                removed++;
            }

            if (removed < Compose)
                return false;

            iconId = matchId;
            return true;
        }

        public void NoteMatch()
        {
            Combo++;
            if (Combo > ComboPeak)
                ComboPeak = Combo;
            GroupsCleared++;
        }

        public void BreakCombo()
        {
            Combo = 0;
        }

        /// <summary>从槽头移走若干张。移出道具和「吐出来」都走这里。</summary>
        public int RemoveFront(int count)
        {
            if (count < 0)
                count = 0;
            if (count > _slots.Count)
                count = _slots.Count;
            if (count > 0)
                _slots.RemoveRange(0, count);
            return count;
        }

        public SheepRoundEnd Evaluate(int activeTiles)
        {
            if (GoalCount > 0)
            {
                if (GroupsCleared >= GoalCount)
                    return SheepRoundEnd.Win;
            }
            else if (activeTiles <= 0)
            {
                return SheepRoundEnd.Win;
            }

            if (_slots.Count >= SlotLimit)
                return SheepRoundEnd.Lose;
            return SheepRoundEnd.None;
        }

        /// <summary>
        /// 这一组落地后是否通关。activeTiles 含尚未关掉的消除牌。
        /// 必须在 <see cref="NoteMatch"/> 之后调用。
        /// </summary>
        public bool MatchClearsLevel(int activeTiles, int matchedCount)
        {
            if (GoalCount > 0)
                return GroupsCleared >= GoalCount;
            if (matchedCount < 0)
                matchedCount = 0;
            return activeTiles - matchedCount <= 0;
        }
    }
}
