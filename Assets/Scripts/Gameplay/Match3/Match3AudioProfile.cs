using UnityEngine;

namespace SmallGames
{
    [CreateAssetMenu(fileName = "Match3Audio", menuName = "SmallGames/三消/音效配置", order = 16)]
    public class Match3AudioProfile : ScriptableObject
    {
        public const string ResourcePath = "Match3/Match3Audio";

        [Header("棋盘")]
        [Tooltip("交换 / 交换回退")]
        public GameSfx Swap = GameSfx.Default;
        [Tooltip("普通三消")]
        public GameSfx Match = GameSfx.Default;
        [Tooltip("下落 / 补子")]
        public GameSfx Fall = GameSfx.Default;

        [Header("特效")]
        [Tooltip("直线特效引爆")]
        public GameSfx Line = GameSfx.Default;
        [Tooltip("炸弹特效引爆")]
        public GameSfx Bomb = GameSfx.Default;
        [Tooltip("色球特效引爆")]
        public GameSfx ColorBomb = GameSfx.Default;

        [Header("结算")]
        [Tooltip("过关成功")]
        public GameSfx Win = GameSfx.Default;
        [Tooltip("过关失败")]
        public GameSfx Lose = GameSfx.Default;

        [Header("障碍")]
        [Tooltip("冰层被打")]
        public GameSfx Ice = GameSfx.Default;
        [Tooltip("石块被打")]
        public GameSfx Stone = GameSfx.Default;
        [Tooltip("果冻被打")]
        public GameSfx Jelly = GameSfx.Default;
        [Tooltip("锁被打")]
        public GameSfx Cage = GameSfx.Default;

        [Header("音乐")]
        [Tooltip("背景音乐，空则沿用当前BGM")]
        public GameSfx Bgm = GameSfx.Default;

        public static Match3AudioProfile Load()
        {
            return AssetHub.Load<Match3AudioProfile>(ResourcePath);
        }

        public void PlaySwap() => Swap.Play();

        public void PlayMatch() => Match.Play();

        public void PlayFall() => Fall.Play();

        public void PlayWin() => Win.Play();

        public void PlayLose() => Lose.Play();

        public void PlayBgm() => Bgm.PlayBgm();

        public void PlaySpecial(Match3Special special)
        {
            switch (special)
            {
                case Match3Special.LineH:
                case Match3Special.LineV:
                    Line.Play();
                    break;
                case Match3Special.Bomb:
                    Bomb.Play();
                    break;
                case Match3Special.ColorBomb:
                    ColorBomb.Play();
                    break;
            }
        }

        public void PlayOverlay(Match3CellKind kind)
        {
            switch (kind)
            {
                case Match3CellKind.Ice:
                    Ice.Play();
                    break;
                case Match3CellKind.Stone:
                    Stone.Play();
                    break;
                case Match3CellKind.Jelly:
                    Jelly.Play();
                    break;
                case Match3CellKind.Cage:
                    Cage.Play();
                    break;
            }
        }
    }
}
