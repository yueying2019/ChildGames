using NUnit.Framework;

namespace SmallGames.Tests
{
    public sealed class DailyCatalogTests
    {
        [Test]
        public void 同一天同一模式抽到同一关()
        {
            var a = DailyCatalog.PickIndex(20261028, GameModeType.Match3, 36);
            var b = DailyCatalog.PickIndex(20261028, GameModeType.Match3, 36);
            Assert.AreEqual(a, b);
            Assert.GreaterOrEqual(a, 0);
            Assert.Less(a, 36);
        }

        [Test]
        public void 没有关卡时下标是0()
        {
            Assert.AreEqual(0, DailyCatalog.PickIndex(20261028, GameModeType.Lianliankan, 0));
            Assert.AreEqual(0, DailyCatalog.PickIndex(20261028, GameModeType.Sheep, 1));
        }

        [Test]
        public void 每日奖励同一天只记一次()
        {
            const string key = "SmallGames.Progress";
            var had = SecurePrefs.HasKey(key);
            var backup = SecurePrefs.GetString(key);
            try
            {
                ProgressRepository.ResetProgress();
                Assert.IsFalse(ProgressRepository.HasDailyReward(GameModeType.Match3));
                ProgressRepository.MarkDailyReward(GameModeType.Match3);
                Assert.IsTrue(ProgressRepository.HasDailyReward(GameModeType.Match3));
                Assert.IsFalse(ProgressRepository.HasDailyReward(GameModeType.Sheep));
                ProgressRepository.MarkDailyReward(GameModeType.Match3);
                Assert.IsTrue(ProgressRepository.HasDailyReward(GameModeType.Match3));
            }
            finally
            {
                if (had && backup != null)
                    SecurePrefs.SetString(key, backup);
                else
                    SecurePrefs.DeleteKey(key);
                SecurePrefs.Save();
                ProgressRepository.Load();
            }
        }

        [Test]
        public void 再来一局不会抽回同一关()
        {
            const int count = 36;
            for (var exclude = 0; exclude < count; exclude++)
            {
                for (var roll = 0; roll < count - 1; roll++)
                {
                    var pick = DailyCatalog.OtherIndex(exclude, count, roll);
                    Assert.AreNotEqual(exclude, pick);
                    Assert.GreaterOrEqual(pick, 0);
                    Assert.Less(pick, count);
                }
            }
        }

        [Test]
        public void 三种模式同一天不会抽到同一下标()
        {
            const int stamp = 20261028;
            var lian = DailyCatalog.PickIndex(stamp, GameModeType.Lianliankan, 36);
            var sheep = DailyCatalog.PickIndex(stamp, GameModeType.Sheep, 36);
            var match = DailyCatalog.PickIndex(stamp, GameModeType.Match3, 36);
            Assert.IsFalse(lian == sheep && sheep == match);
        }

        [Test]
        public void 提前通关后面的关不跳过中间关()
        {
            const string key = "SmallGames.Progress";
            var had = SecurePrefs.HasKey(key);
            var backup = SecurePrefs.GetString(key);
            try
            {
                ProgressRepository.ResetProgress();
                var mode = GameModeType.Lianliankan;
                var count = LevelBrowse.Count(mode);
                Assert.Greater(count, 1);

                var farId = LevelBrowse.LevelId(mode, count - 1);
                Assert.IsTrue(ProgressRepository.MarkCleared(mode, farId, true));
                Assert.AreEqual(1, ProgressRepository.UnlockedId(mode));
                Assert.IsTrue(ProgressRepository.HasClearReward(mode, farId));
                Assert.IsFalse(ProgressRepository.MarkCleared(mode, farId, true));
                Assert.AreEqual(1, ProgressRepository.UnlockedId(mode));

                var firstId = LevelBrowse.LevelId(mode, 0);
                Assert.IsTrue(ProgressRepository.MarkCleared(mode, firstId, true));
                Assert.AreEqual(2, ProgressRepository.UnlockedId(mode));
            }
            finally
            {
                if (had && backup != null)
                    SecurePrefs.SetString(key, backup);
                else
                    SecurePrefs.DeleteKey(key);
                SecurePrefs.Save();
                ProgressRepository.Load();
            }
        }
    }
}
