using UnityEngine;

namespace SmallGames
{
    [CreateAssetMenu(fileName = "SheepAudio", menuName = "SmallGames/叠叠消/音效配置", order = 14)]
    public class SheepAudioProfile : ScriptableObject
    {
        public const string ResourcePath = "Sheep/SheepAudio";

        [Header("棋盘")]
        [Tooltip("点击棋子")]
        public GameSfx Click = GameSfx.Default;
        [Tooltip("三消消除")]
        public GameSfx Match = GameSfx.Default;

        [Header("结算")]
        [Tooltip("过关成功")]
        public GameSfx Win = GameSfx.Default;
        [Tooltip("过关失败")]
        public GameSfx Lose = GameSfx.Default;

        [Header("音乐")]
        [Tooltip("背景音乐，空则沿用当前BGM")]
        public GameSfx Bgm = GameSfx.Default;

        public static SheepAudioProfile Load()
        {
            return AssetHub.Load<SheepAudioProfile>(ResourcePath);
        }

        public void PlayClick() => Click.Play();

        public void PlayMatch() => Match.Play();

        public void PlayWin() => Win.Play();

        public void PlayLose() => Lose.Play();

        public void PlayBgm() => Bgm.PlayBgm();
    }
}
