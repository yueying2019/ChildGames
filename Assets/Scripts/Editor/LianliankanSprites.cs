using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmallGames.Editor
{
    public static class LianliankanSprites
    {
        public sealed class Pack
        {
            public Sprite Background;
            public Sprite TileA;
            public Sprite TileB;
            public Sprite Select;
            public Sprite[] Icons = new Sprite[0];
        }

        public static Pack LoadAll()
        {
            var pack = new Pack
            {
                Background = Load("Assets/UI/PNG/bg.png"),
                TileA = Load("Assets/UI/PNG/UI/bg3.png"),
                TileB = Load("Assets/UI/PNG/UI/bg4.png"),
                Select = Load("Assets/UI/PNG/UI/bg1.png")
            };

            var icons = new List<Sprite>();
            for (var i = 1; i <= 21; i++)
            {
                var sprite = Load($"Assets/UI/PNG/ico/{i}.png");
                if (sprite != null)
                    icons.Add(sprite);
            }

            pack.Icons = icons.ToArray();
            return pack;
        }

        public static Sprite Load(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets != null)
            {
                for (var i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite found)
                        return found;
                }
            }

            return null;
        }
    }
}
