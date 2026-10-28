using NUnit.Framework;

namespace SmallGames.Tests
{
    public sealed class CatSkillChargeTests
    {
        [Test]
        public void 第一段未满不放技能()
        {
            var charge = new CatSkillCharge(4);
            var fired = 0;
            Assert.IsFalse(charge.Add(3, _ => { fired++; return true; }));
            Assert.AreEqual(3, charge.Energy);
            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, charge.NextSkill);
        }

        [Test]
        public void 第一段放技能一后继续攒到第二段()
        {
            var charge = new CatSkillCharge(4);
            var skill = -1;
            Assert.IsTrue(charge.Add(4, index => { skill = index; return true; }));
            Assert.AreEqual(0, skill);
            Assert.AreEqual(4, charge.Energy);
            Assert.AreEqual(1, charge.NextSkill);

            Assert.IsFalse(charge.Add(3, _ => true));
            Assert.AreEqual(7, charge.Energy);

            Assert.IsTrue(charge.Add(1, index => { skill = index; return true; }));
            Assert.AreEqual(1, skill);
            Assert.AreEqual(0, charge.Energy);
            Assert.AreEqual(0, charge.NextSkill);
        }

        [Test]
        public void 放失败停在当前阶段()
        {
            var charge = new CatSkillCharge(3);
            Assert.IsFalse(charge.Add(5, _ => false));
            Assert.AreEqual(3, charge.Energy);
            Assert.AreEqual(0, charge.NextSkill);

            Assert.IsTrue(charge.Add(1, index => index == 0));
            Assert.AreEqual(3, charge.Energy);
            Assert.AreEqual(1, charge.NextSkill);

            Assert.IsFalse(charge.Add(5, _ => false));
            Assert.AreEqual(6, charge.Energy);
            Assert.AreEqual(1, charge.NextSkill);
        }
    }
}
