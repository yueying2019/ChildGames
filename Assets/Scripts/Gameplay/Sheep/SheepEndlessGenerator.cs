using UnityEngine;

namespace SmallGames
{
    public static class SheepEndlessGenerator
    {
        private static readonly SheepMotifKind[] Motifs =
        {
            SheepMotifKind.Pond,
            SheepMotifKind.Sheep,
            SheepMotifKind.Tree,
            SheepMotifKind.Grove,
            SheepMotifKind.Flower,
            SheepMotifKind.Lawn,
            SheepMotifKind.Butterfly
        };

        public static SheepLevelData Build(int wave)
        {
            wave = Mathf.Max(1, wave);
            var data = ScriptableObject.CreateInstance<SheepLevelData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.name = $"Endless_{wave:00}";
            data.LevelId = wave;
            data.Title = EndlessCatalog.Title(wave);
            data.TypeNum = Mathf.Clamp(5 + (wave - 1) / 3, SheepLevelData.MinTypes, 10);
            data.LevelNum = Mathf.Clamp(3 + (wave - 1) / 2, SheepLevelData.MinLayers, 7);
            data.LevelBlockNum = Mathf.Clamp(10 + (wave - 1) / 2, 8, 14);
            data.BorderStep = wave >= 8 ? 2 : 1;
            data.LeftStack = Mathf.Clamp((wave - 1) / 2, 0, 12);
            data.RightStack = Mathf.Clamp(wave >= 2 ? (wave - 1) / 2 : 0, 0, 12);
            data.ComposeNum = 3;
            data.Motif = Motifs[(wave - 1) % Motifs.Length];
            data.EnsureInitialized();
            return data;
        }
    }
}
