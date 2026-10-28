using NUnit.Framework;
using UnityEngine;

namespace SmallGames.Tests
{
    /// <summary>引导的可配置部分：步骤、流程资产、以及 key → 步骤的解析。</summary>
    public sealed class GuideCatalogTests
    {
        private static GuideStep Step(GameModeType mode, string key)
        {
            return GuideCatalog.Resolve(mode, key);
        }

        [Test]
        public void KeyFor与TryFromKey能来回转换()
        {
            foreach (Match3GuideId id in System.Enum.GetValues(typeof(Match3GuideId)))
            {
                var key = GuideCatalog.KeyFor(id);
                Assert.IsTrue(Match3GuideCatalog.TryFromKey(key, out var parsed), key);
                Assert.AreEqual(id, parsed);
            }
        }

        [Test]
        public void TryFromKey_乱七八糟的key返回false()
        {
            Assert.IsFalse(Match3GuideCatalog.TryFromKey(null, out _));
            Assert.IsFalse(Match3GuideCatalog.TryFromKey(string.Empty, out _));
            Assert.IsFalse(Match3GuideCatalog.TryFromKey("nope", out _));
        }

        [Test]
        public void 内置三消步骤都配好了文案()
        {
            var steps = GuideCatalog.All(GameModeType.Match3);
            Assert.AreEqual(9, steps.Length);
            foreach (var step in steps)
            {
                Assert.IsTrue(step.IsValid, "每条都得有 key，否则无法去重");
                Assert.IsNotEmpty(step.Title, step.Key);
                Assert.IsNotEmpty(step.Desc, step.Key);
            }
        }

        [Test]
        public void Resolve_按key拿到对应步骤()
        {
            var ice = Step(GameModeType.Match3, GuideCatalog.KeyFor(Match3GuideId.Ice));
            Assert.IsNotNull(ice);
            Assert.AreEqual(Match3GuideCatalog.Title(Match3GuideId.Ice), ice.Title);
            Assert.AreEqual(GuideTargets.Board, ice.HighlightTarget);
            Assert.AreEqual(GuideHighlightShape.RoundRect, ice.Shape);
        }

        [Test]
        public void Resolve_加步道具高亮步数区_用圆形()
        {
            var extra = Step(GameModeType.Match3, GuideCatalog.KeyFor(Match3GuideId.ExtraMoves));
            Assert.IsNotNull(extra);
            Assert.AreEqual(GuideTargets.Moves, extra.HighlightTarget);
            Assert.AreEqual(GuideHighlightShape.Circle, extra.Shape);
        }

        [Test]
        public void Resolve_没配过的key或模式返回空()
        {
            Assert.IsNull(Step(GameModeType.Match3, "match3.NotExist"));
            Assert.IsNull(Step(GameModeType.Match3, string.Empty));
            Assert.IsEmpty(GuideCatalog.All(GameModeType.Sheep), "叠叠消还没配内置引导，应该是空的");
        }

        [Test]
        public void 特效类引导走SpecialMade触发()
        {
            Assert.AreEqual(GuideTrigger.SpecialMade, Step(GameModeType.Match3, GuideCatalog.KeyFor(Match3GuideId.Bomb)).Trigger);
            Assert.AreEqual(GuideTrigger.Opening, Step(GameModeType.Match3, GuideCatalog.KeyFor(Match3GuideId.Ice)).Trigger);
            Assert.IsFalse(Step(GameModeType.Match3, GuideCatalog.KeyFor(Match3GuideId.Ice)).MatchesTrigger("ice"));
        }

        [Test]
        public void 新建流程资产可以覆盖内置配置()
        {
            var asset = ScriptableObject.CreateInstance<GuideSequence>();
            asset.Mode = GameModeType.Match3;
            asset.Steps = new[]
            {
                new GuideStep { Key = "match3.Ice", Trigger = GuideTrigger.Custom, TriggerKey = "first-freeze", Title = "改写后的冰层介绍", Desc = "策划改的" }
            };

            try
            {
                GuideCatalog.SetOverride(asset);
                var overridden = Step(GameModeType.Match3, "match3.Ice");
                Assert.AreEqual("改写后的冰层介绍", overridden.Title);
                Assert.IsTrue(overridden.MatchesTrigger("first-freeze"));
                Assert.IsFalse(overridden.MatchesTrigger("other"));
            }
            finally
            {
                GuideCatalog.ClearOverrides();
                Object.DestroyImmediate(asset);
            }

            Assert.AreEqual(Match3GuideCatalog.Title(Match3GuideId.Ice), Step(GameModeType.Match3, "match3.Ice").Title,
                "撤掉覆盖后应该回到内置配置");
        }

        [Test]
        public void 流程资产按触发条件筛选()
        {
            var asset = ScriptableObject.CreateInstance<GuideSequence>();
            asset.Mode = GameModeType.Sheep;
            asset.Steps = new[]
            {
                new GuideStep { Key = "sheep.open", Trigger = GuideTrigger.Opening },
                new GuideStep { Key = "sheep.stuck", Trigger = GuideTrigger.Custom, TriggerKey = "deadlock" },
                new GuideStep { Key = "sheep.open2", Trigger = GuideTrigger.Opening },
            };

            try
            {
                var customs = asset.ByTrigger(GuideTrigger.Custom);
                Assert.AreEqual(1, customs.Length);
                Assert.AreEqual("sheep.stuck", customs[0].Key);
                Assert.AreEqual(2, asset.ByTrigger(GuideTrigger.Opening).Length);
                Assert.IsNull(asset.Find("sheep.none"));
                Assert.IsNotNull(asset.Find("sheep.open"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
