using UnityEngine;

namespace SmallGames
{
    public static class ComboCatalog
    {
        public const float LianWindow = 2.4f;

        public static string LianLabel(int combo) => combo <= 1 ? string.Empty : $"连击 x{combo}";

        public static string SheepLabel(int combo) => combo <= 1 ? string.Empty : $"连消 x{combo}";

        public static string Match3Label(int wave)
        {
            if (wave <= 1)
                return string.Empty;
            if (wave == 2)
                return "Good";
            if (wave == 3)
                return "Great";
            if (wave == 4)
                return "Excellent";
            return "Amazing";
        }

        public static Color ColorFor(int combo)
        {
            if (combo >= 5)
                return UIStyle.Accent;
            if (combo >= 4)
                return UIStyle.AccentGreen;
            if (combo >= 3)
                return UIStyle.AccentBlue;
            return UIStyle.TextMain;
        }
    }
}
