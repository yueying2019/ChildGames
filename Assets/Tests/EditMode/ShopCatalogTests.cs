using NUnit.Framework;

namespace SmallGames.Tests
{
    public sealed class ShopCatalogTests
    {
        [Test]
        public void 随机道具属于当前模式()
        {
            AssertMode(GameModeType.Lianliankan);
            AssertMode(GameModeType.Sheep);
            AssertMode(GameModeType.Match3);
        }

        [Test]
        public void 星星价格按标价折算且至少十星()
        {
            for (var i = 0; i < ShopCatalog.All.Length; i++)
            {
                var cost = ShopCatalog.StarCost(ShopCatalog.All[i]);
                Assert.GreaterOrEqual(cost, 10);
                Assert.AreEqual(0, cost % 5);
            }
        }

        private static void AssertMode(GameModeType mode)
        {
            for (var i = 0; i < 24; i++)
            {
                var id = ShopCatalog.RandomItem(mode);
                Assert.IsTrue(ShopCatalog.TryGet(id, out var info), mode.ToString());
                Assert.AreEqual(mode, info.Mode);
            }
        }
    }
}
