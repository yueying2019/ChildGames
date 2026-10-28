using System;

namespace SmallGames
{
    /// <summary>
    /// 猫咪能量分两段。到第一段放技能一，能量留着；到第二段放技能二，然后清空重来。
    /// 某一段放不出来就停在那段，不往下涨。
    /// </summary>
    public sealed class CatSkillCharge
    {
        public int Energy { get; private set; }
        public int Stage { get; }
        public int NextSkill { get; private set; }

        public int Total => Stage * 2;
        public float Ratio => Total <= 0 ? 0f : Energy / (float)Total;

        public CatSkillCharge(int stage)
        {
            Stage = stage < 1 ? 1 : stage;
        }

        /// <summary>
        /// 加上本次消除的能量。到当前阶段才调用 cast。技能一成功后进入第二段；技能二成功后清空。
        /// </summary>
        public bool Add(int gain, Func<int, bool> cast)
        {
            if (gain <= 0)
                return false;

            var cap = NextSkill == 0 ? Stage : Total;
            Energy += gain;
            if (Energy < cap)
                return false;

            Energy = cap;
            if (cast == null || !cast(NextSkill))
                return false;

            if (NextSkill == 0)
                NextSkill = 1;
            else
            {
                Energy = 0;
                NextSkill = 0;
            }

            return true;
        }
    }
}
