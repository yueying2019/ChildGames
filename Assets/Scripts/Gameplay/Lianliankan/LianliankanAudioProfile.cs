using UnityEngine;

namespace SmallGames
{
    [CreateAssetMenu(fileName = "LianliankanAudio", menuName = "SmallGames/对对消/音效配置", order = 12)]
    public class LianliankanAudioProfile : ScriptableObject
    {
        public const string ResourcePath = "Lianliankan/LianliankanAudio";

        [Header("棋盘")]
        [Tooltip("点击棋子")]
        public GameSfx Click = GameSfx.Default;
        [Tooltip("连不上 / 点错")]
        public GameSfx Mismatch = GameSfx.Default;
        [Tooltip("成功连接")]
        public GameSfx Connect = GameSfx.Default;

        [Header("结算")]
        [Tooltip("过关成功")]
        public GameSfx Win = GameSfx.Default;
        [Tooltip("过关失败")]
        public GameSfx Lose = GameSfx.Default;

        [Header("音乐")]
        [Tooltip("背景音乐，空则沿用当前BGM")]
        public GameSfx Bgm = GameSfx.Default;

        public static LianliankanAudioProfile Load()
        {
            return AssetHub.Load<LianliankanAudioProfile>(ResourcePath);
        }

        public void PlayClick() => Click.Play();

        public void PlayMismatch()
        {
            if (Mismatch.Clip != null)
                Mismatch.Play();
            else
                Click.Play();
        }

        public void PlayConnect() => Connect.Play();

        public void PlayWin() => Win.Play();

        public void PlayLose() => Lose.Play();

        public void PlayBgm() => Bgm.PlayBgm();
    }
}
