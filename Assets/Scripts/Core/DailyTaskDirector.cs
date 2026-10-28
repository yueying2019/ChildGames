using System;

namespace SmallGames
{
    /// <summary>
    /// 玩法侧向每日任务上报的唯一入口。玩法只认识这里，不直接碰存档。
    /// </summary>
    public static class DailyTaskDirector
    {
        public static event Action Changed;

        static DailyTaskDirector()
        {
            DailyTaskRepository.Changed += () => Changed?.Invoke();
        }

        /// <summary>上报一次信号。amount 为增量，会被封顶到任务目标值。</summary>
        public static void Report(GameModeType mode, DailyTaskSignal signal, int amount = 1)
        {
            if (mode == GameModeType.None || signal == DailyTaskSignal.None || amount <= 0)
                return;

            DailyTaskRepository.Add(mode, signal, amount);
        }

        /// <summary>通关一局。</summary>
        public static void ReportClear(GameModeType mode)
        {
            Report(mode, DailyTaskSignal.ModeCleared, 1);
        }

        public static bool HasClaimable()
        {
            return DailyTaskRepository.HasClaimable();
        }

        public static bool Claim(string key)
        {
            return DailyTaskRepository.Claim(key);
        }
    }
}
