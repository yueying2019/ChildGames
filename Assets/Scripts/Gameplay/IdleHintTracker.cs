namespace SmallGames
{
    /// <summary>
    /// 闲置计时器：一段时间（默认 15 秒）没有成功消除就提醒一次。
    /// 结算 / 输入锁住 / 播放动画时调用 Reset，就不会误触发。
    /// 到点后清零重新计：玩家还是不动的话，隔一会儿会再提示一轮。
    /// </summary>
    public class IdleHintTracker
    {
        public const float DefaultSeconds = 15f;

        public float Seconds { get; private set; }
        public float Elapsed { get; private set; }

        public IdleHintTracker(float seconds = DefaultSeconds)
        {
            Seconds = seconds > 0f ? seconds : DefaultSeconds;
            Elapsed = 0f;
        }

        public void Reset()
        {
            Elapsed = 0f;
        }

        /// <summary>每帧调用。返回 true 表示这一轮闲置到点了，该给提示。</summary>
        public bool Tick(float deltaTime)
        {
            Elapsed += deltaTime;
            if (Elapsed < Seconds)
                return false;

            Elapsed = 0f;
            return true;
        }
    }
}
