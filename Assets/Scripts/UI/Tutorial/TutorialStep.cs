using System;

namespace SmallGames
{
    /// <summary>
    /// 强制引导的一步：高亮哪些格子、只允许哪些操作、做到什么算完成。
    ///
    /// 坐标是「扁平 int 数组」，语义由玩法自己定，框架不解释：
    /// - 三消：[x0, y0, x1, y1] 表示一次交换的两端
    /// - 对对消：[r0, c0, r1, c1] 表示一对
    /// - 叠叠消：[idx, idx, ...] 表示若干张牌
    ///
    /// Allowed 为空表示这一步不限制操作（只提示，玩家随便玩）。
    /// </summary>
    public sealed class TutorialStep
    {
        public string Key = string.Empty;

        /// <summary>气泡文案。空则不显示气泡。</summary>
        public string Desc = string.Empty;

        /// <summary>高亮目标（扁平坐标）。空则不挖洞，只显示文案。</summary>
        public int[] Highlight = Array.Empty<int>();

        /// <summary>允许的操作。空 = 不限制。</summary>
        public int[] Allowed = Array.Empty<int>();

        /// <summary>完成信号：玩法侧 Notify(同样的字符串) 才进下一步。</summary>
        public string Complete = string.Empty;

        /// <summary>是否给高亮格子加呼吸闪烁（HintFlash）。</summary>
        public bool Flash = true;

        public bool IsValid => !string.IsNullOrEmpty(Key);

        public bool Matches(string signal)
        {
            if (string.IsNullOrEmpty(Complete) || string.IsNullOrEmpty(signal))
                return false;
            return string.Equals(Complete, signal, StringComparison.Ordinal);
        }

        public bool HasAllowed => Allowed != null && Allowed.Length > 0;

        /// <summary>单个格子的操作是否被允许（叠叠消点牌用）。</summary>
        public bool AllowsSingle(int cell)
        {
            if (!HasAllowed)
                return true;
            for (var i = 0; i < Allowed.Length; i++)
            {
                if (Allowed[i] == cell)
                    return true;
            }

            return false;
        }

        /// <summary>这个格子是否出现在允许列表里（对对消先点第一张时用，此时还不知道第二张）。</summary>
        public bool AllowsAny(int a0, int a1)
        {
            if (!HasAllowed)
                return true;
            for (var i = 0; i + 1 < Allowed.Length; i += 2)
            {
                if (Allowed[i] == a0 && Allowed[i + 1] == a1)
                    return true;
            }

            return false;
        }

        /// <summary>一次两端的操作是否被允许（三消交换 / 对对消连线用），两端顺序无关。</summary>
        public bool AllowsPair(int a0, int a1, int b0, int b1)
        {
            if (!HasAllowed)
                return true;
            if (Allowed.Length < 4)
                return false;
            var forward = Allowed[0] == a0 && Allowed[1] == a1 && Allowed[2] == b0 && Allowed[3] == b1;
            var backward = Allowed[0] == b0 && Allowed[1] == b1 && Allowed[2] == a0 && Allowed[3] == a1;
            return forward || backward;
        }
    }
}
