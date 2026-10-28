using NUnit.Framework;

namespace SmallGames.Tests
{
    public sealed class SheepEngineTests
    {
        [Test]
        public void Insert_GroupsSameIconTogether()
        {
            var engine = new SheepEngine();
            engine.Load(3, 7, 0);
            Assert.AreEqual(0, engine.Insert(1));
            Assert.AreEqual(1, engine.Insert(2));
            Assert.AreEqual(1, engine.Insert(1));
            CollectionAssert.AreEqual(new[] { 1, 1, 2 }, engine.CaptureSlots());
        }

        [Test]
        public void TryTakeMatch_RemovesComposeFromTheTail()
        {
            var engine = new SheepEngine();
            engine.Load(3, 7, 0);
            engine.Insert(1);
            engine.Insert(2);
            engine.Insert(1);
            Assert.IsFalse(engine.TryTakeMatch(out _));

            engine.Insert(1);
            Assert.IsTrue(engine.TryTakeMatch(out var iconId));
            Assert.AreEqual(1, iconId);
            CollectionAssert.AreEqual(new[] { 2 }, engine.CaptureSlots());
        }

        [Test]
        public void TryTakeMatch_PrefersTheEarlierIcon()
        {
            var engine = new SheepEngine();
            engine.Load(2, 7, 0);
            engine.Insert(4);
            engine.Insert(4);
            engine.Insert(9);
            engine.Insert(9);
            Assert.IsTrue(engine.TryTakeMatch(out var iconId));
            Assert.AreEqual(4, iconId);
            CollectionAssert.AreEqual(new[] { 9, 9 }, engine.CaptureSlots());
        }

        [Test]
        public void Evaluate_GoalWinsBeforeAFullSlot()
        {
            var engine = new SheepEngine();
            engine.Load(3, 2, 1);
            engine.Insert(1);
            engine.Insert(1);
            engine.Insert(1);
            Assert.IsTrue(engine.TryTakeMatch(out _));
            engine.NoteMatch();
            Assert.AreEqual(SheepRoundEnd.Win, engine.Evaluate(6));
        }

        [Test]
        public void Evaluate_FullSlotLosesWhenTilesRemain()
        {
            var engine = new SheepEngine();
            engine.Load(3, 2, 0);
            engine.Insert(1);
            engine.Insert(2);
            Assert.AreEqual(SheepRoundEnd.Lose, engine.Evaluate(4));
        }

        [Test]
        public void Evaluate_ClearWinsWhenNothingActive()
        {
            var engine = new SheepEngine();
            engine.Load(3, 7, 0);
            Assert.AreEqual(SheepRoundEnd.Win, engine.Evaluate(0));
        }

        [Test]
        public void NoteMatch_TracksComboAndCanBreak()
        {
            var engine = new SheepEngine();
            engine.Load(3, 7, 0);
            engine.NoteMatch();
            engine.NoteMatch();
            Assert.AreEqual(2, engine.Combo);
            Assert.AreEqual(2, engine.ComboPeak);
            engine.BreakCombo();
            Assert.AreEqual(0, engine.Combo);
            Assert.AreEqual(2, engine.ComboPeak);
            Assert.AreEqual(2, engine.GroupsCleared);
        }

        [Test]
        public void RemoveFrontAndRestore_KeepSlotOrder()
        {
            var engine = new SheepEngine();
            engine.Load(3, 7, 0);
            engine.Insert(3);
            engine.Insert(5);
            engine.Insert(3);
            var before = engine.CaptureSlots();
            Assert.AreEqual(2, engine.RemoveFront(2));
            CollectionAssert.AreEqual(new[] { 3 }, engine.CaptureSlots());
            engine.RestoreSlots(before);
            CollectionAssert.AreEqual(new[] { 3, 3, 5 }, engine.CaptureSlots());
        }
    }
}
