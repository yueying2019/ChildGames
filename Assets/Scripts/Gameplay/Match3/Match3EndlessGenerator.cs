using UnityEngine;

namespace SmallGames
{
    public static class Match3EndlessGenerator
    {
        public static Match3LevelData Build(int wave)
        {
            wave = Mathf.Max(1, wave);
            var data = ScriptableObject.CreateInstance<Match3LevelData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.name = $"Endless_{wave:00}";
            data.LevelId = wave;
            data.DisplayName = EndlessCatalog.Title(wave);
            data.Width = 8;
            data.Height = 8;
            data.ColorCount = Mathf.Clamp(5 + (wave - 1) / 4, 4, Match3LevelData.MaxColor);
            data.MoveLimit = GameBalance.Current.Match3EndlessMoves;
            data.CollectCounts = new int[7];
            data.Resize(8, 8);
            data.FillPlayable();

            var cycle = (wave - 1) % 6;
            if (cycle == 2 || cycle == 5)
            {
                data.GoalType = Match3GoalType.ClearCells;
                data.TargetScore = 0;
                if (cycle == 2)
                    PaintIce(data, wave);
                else
                    PaintJelly(data, wave);
            }
            else
            {
                data.GoalType = Match3GoalType.Score;
                data.TargetScore = GameBalance.Current.Match3EndlessScore(wave);
                if (cycle == 1)
                    PaintDonut(data);
                else if (cycle == 3)
                    PaintCross(data);
                else if (cycle == 4)
                    PaintIceStone(data);
            }

            return data;
        }

        private static void PaintDonut(Match3LevelData level)
        {
            FillRect(level, 3, 3, 4, 4, Match3CellSpec.Empty);
        }

        private static void PaintCross(Match3LevelData level)
        {
            var cx = level.Width / 2;
            var cy = level.Height / 2;
            level.SetCell(cx, cy, Match3CellSpec.Empty);
            level.SetCell(cx - 1, cy, Match3CellSpec.Empty);
            level.SetCell(cx + 1, cy, Match3CellSpec.Empty);
            level.SetCell(cx, cy - 1, Match3CellSpec.Empty);
            level.SetCell(cx, cy + 1, Match3CellSpec.Empty);
        }

        private static void PaintIce(Match3LevelData level, int wave)
        {
            var layers = wave >= 10 ? 2 : 1;
            FillRect(level, 2, 2, 5, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, layers));
        }

        private static void PaintJelly(Match3LevelData level, int wave)
        {
            FillRect(level, 1, 1, 6, 6, Match3CellSpec.Overlay(Match3CellKind.Jelly, 0, 1));
            if (wave < 8)
                return;
            level.SetCell(2, 2, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(5, 2, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(2, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
            level.SetCell(5, 5, Match3CellSpec.Overlay(Match3CellKind.Cage, 0, 1));
        }

        private static void PaintIceStone(Match3LevelData level)
        {
            for (var x = 2; x <= 5; x++)
            {
                level.SetCell(x, 2, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 1));
                level.SetCell(x, 5, Match3CellSpec.Overlay(Match3CellKind.Ice, 0, 2));
            }

            level.SetCell(1, 3, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            level.SetCell(1, 4, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
            level.SetCell(6, 3, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 1));
            level.SetCell(6, 4, Match3CellSpec.Overlay(Match3CellKind.Stone, 0, 2));
        }

        private static void FillRect(Match3LevelData level, int x0, int y0, int x1, int y1, Match3CellSpec spec)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                    level.SetCell(x, y, spec);
            }
        }
    }
}
