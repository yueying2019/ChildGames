using UnityEngine;

namespace SmallGames
{
    public static class LianliankanEndlessGenerator
    {
        private static readonly LianliankanLayoutKind[] Layouts =
        {
            LianliankanLayoutKind.Ring32,
            LianliankanLayoutKind.Center36,
            LianliankanLayoutKind.Twin40,
            LianliankanLayoutKind.Wide48,
            LianliankanLayoutKind.Hollow48,
            LianliankanLayoutKind.Columns42,
            LianliankanLayoutKind.UShape44,
            LianliankanLayoutKind.Full64
        };

        public static LianliankanLevelData Build(int wave)
        {
            wave = Mathf.Max(1, wave);
            var data = ScriptableObject.CreateInstance<LianliankanLevelData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.name = $"Endless_{wave:00}";
            data.LevelId = wave;
            data.Title = EndlessCatalog.Title(wave);
            data.TypeCount = Mathf.Clamp(6 + (wave - 1) / 2, LianliankanLevelData.MinTypes, LianliankanLevelData.MaxTypes);
            data.TimeLimit = EndlessCatalog.LianStartSeconds;
            data.ApplyTemplate(Layouts[(wave - 1) % Layouts.Length], false);
            return data;
        }
    }
}
