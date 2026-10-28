using UnityEngine;

namespace SmallGames
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "SmallGames/玩法平衡表", order = 1)]
    public class GameBalance : ScriptableObject
    {
        public const string ResourcePath = "GameBalance";

        [Header("三消 · 分数")]
        [Min(1)] public int Match3BaseMatchScore = 30;

        [Header("三消 · 隐藏掉落（1/N 概率）")]
        [Min(1)] public int Match3ExtraMovesChance = 3;
        [Min(1)] public int Match3GoldBeanChance = 6;
        [Min(1)] public int Match3ExtraMovesGain = 5;
        [Min(0)] public int Match3GoldBeanCoins = 100;

        [Header("三消 · 加步")]
        [Min(1)] public int Match3ShopExtraMoves = 5;
        [Min(1)] public int Match3ContinueMoves = 5;
        [Min(1)] public int Match3EndlessBonusMoves = 5;
        [Min(1)] public int Match3EndlessMoves = 25;
        [Min(0)] public int Match3EndlessScoreBase = 1600;
        [Min(0)] public int Match3EndlessScorePerWave = 400;

        [Header("对对消 · 时间")]
        [Min(10f)] public float LianStartSeconds = 180f;
        [Min(0f)] public float LianBonusSeconds = 18f;
        [Min(10f)] public float LianMaxSeconds = 180f;
        [Min(0f)] public float LianContinueSeconds = 30f;

        private static GameBalance _current;
        private static GameBalance _fallback;

        public static GameBalance Current
        {
            get
            {
                if (_current == null)
                    _current = AssetHub.Load<GameBalance>(ResourcePath);
                return _current != null ? _current : Fallback();
            }
        }

        public int Match3EndlessScore(int wave)
        {
            wave = Mathf.Max(1, wave);
            return Match3EndlessScoreBase + (wave - 1) * Match3EndlessScorePerWave;
        }

        public static void ClearCache()
        {
            _current = null;
        }

        private static GameBalance Fallback()
        {
            if (_fallback == null)
            {
                _fallback = CreateInstance<GameBalance>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
                _fallback.name = "GameBalance_Fallback";
            }

            return _fallback;
        }
    }
}
