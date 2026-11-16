using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SmallGames.Tests
{
    public sealed class JieqiEngineTests
    {
        [Test]
        public void Deal_FixesKingsAndMixesTheOtherPieces()
        {
            var engine = new JieqiEngine();
            var first = new JieqiPieceState[JieqiEngine.MaxPieces];
            engine.Deal(7);
            var count = engine.CopySave(first);
            Assert.AreEqual(32, count);
            Assert.AreEqual(JieqiCamp.Red, engine.Turn);

            engine.Deal(7);
            var again = new JieqiPieceState[JieqiEngine.MaxPieces];
            engine.CopySave(again);
            Assert.AreEqual(engine.Hash(), JieqiEngine.HashPieces(first, count, JieqiCamp.Red));

            var printed = new int[3, 8];
            var controllers = new int[3];
            var crossed = false;
            var hiddenDiffers = false;
            for (var i = 0; i < count; i++)
            {
                var piece = again[i];
                Assert.IsTrue(piece.Alive);
                controllers[(int)piece.Controller]++;
                printed[(int)piece.Printed, (int)piece.Role]++;
                if (piece.Role == JieqiRole.King)
                {
                    Assert.IsFalse(piece.FaceDown);
                    if (piece.Controller == JieqiCamp.Black)
                        Assert.AreEqual(0, piece.Y);
                    else
                        Assert.AreEqual(9, piece.Y);
                    Assert.AreEqual(4, piece.X);
                    continue;
                }

                Assert.IsTrue(piece.FaceDown);
                if (piece.Printed != piece.Controller)
                    crossed = true;
                if (piece.Role != piece.SlotRole)
                    hiddenDiffers = true;
            }

            Assert.AreEqual(16, controllers[(int)JieqiCamp.Red]);
            Assert.AreEqual(16, controllers[(int)JieqiCamp.Black]);
            Assert.IsTrue(crossed);
            Assert.IsTrue(hiddenDiffers);
            AssertBag(printed, JieqiCamp.Red);
            AssertBag(printed, JieqiCamp.Black);
        }

        [Test]
        public void TryView_HidesFaceDownIdentity()
        {
            var engine = new JieqiEngine();
            engine.Deal(3);
            var save = new JieqiPieceState[JieqiEngine.MaxPieces];
            var count = engine.CopySave(save);
            var checkedOne = false;
            for (var i = 0; i < count; i++)
            {
                var piece = save[i];
                if (!piece.FaceDown || piece.Role == piece.SlotRole)
                    continue;
                Assert.IsTrue(engine.TryView(piece.X, piece.Y, out var view));
                Assert.IsTrue(view.FaceDown);
                Assert.AreEqual(JieqiCamp.None, view.Printed);
                Assert.AreEqual(piece.SlotRole, view.Role);
                Assert.AreEqual(piece.Controller, view.Controller);
                checkedOne = true;
                break;
            }

            Assert.IsTrue(checkedOne);
        }

        [Test]
        public void DarkPiece_MovesAsItsSquareThenBelongsToItsPrintedSide()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Black, JieqiRole.Horse, JieqiRole.Rook, true, 0, 4);

            Assert.IsTrue(engine.TryMove(0, 4, 0, 6, out var step));
            Assert.IsTrue(step.Flipped);
            Assert.AreEqual(JieqiCamp.Black, step.RevealedCamp);
            Assert.AreEqual(JieqiRole.Horse, step.RevealedRole);
            Assert.IsTrue(engine.TryView(0, 6, out var view));
            Assert.IsFalse(view.FaceDown);
            Assert.AreEqual(JieqiCamp.Black, view.Controller);
            Assert.AreEqual(JieqiCamp.Black, view.Printed);
            Assert.AreEqual(JieqiRole.Horse, view.Role);

            Assert.AreEqual(JieqiCamp.Black, engine.Turn);
            Assert.IsFalse(engine.TryMove(0, 6, 0, 8, out _));
            Assert.IsTrue(engine.TryMove(0, 6, 2, 5, out _));
            Assert.AreEqual(JieqiCamp.Red, engine.Turn);
            Assert.IsFalse(engine.TryMove(2, 5, 0, 6, out _));
        }

        [Test]
        public void RevealedOpponentPawn_ChangesHandsAndForward()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Black);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, true, 4, 3);

            Assert.IsTrue(engine.TryMove(4, 3, 4, 4, out var step));
            Assert.IsTrue(step.Flipped);
            Assert.IsTrue(engine.TryView(4, 4, out var view));
            Assert.AreEqual(JieqiCamp.Red, view.Controller);

            Assert.AreEqual(JieqiCamp.Red, engine.Turn);
            Assert.IsFalse(engine.TryMove(4, 4, 4, 5, out _));
            Assert.IsTrue(engine.TryMove(4, 4, 4, 3, out _));
        }

        [Test]
        public void Pawn_StepsForwardByControllerAndCannotRetreat()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Black);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 3);

            Assert.IsFalse(engine.TryMove(4, 3, 4, 2, out _));
            Assert.IsTrue(engine.TryMove(4, 3, 4, 4, out var step));
            Assert.IsFalse(step.Flipped);
            Assert.AreEqual(JieqiCamp.Red, engine.Turn);
        }

        [Test]
        public void Pawn_CanStepSidewaysBeforeTheRiver()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 6);
            Assert.IsTrue(engine.TryMove(4, 6, 3, 6, out _));
        }

        [Test]
        public void Horse_IsBlockedByItsLeg()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Horse, JieqiRole.Horse, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 8);
            Assert.IsFalse(engine.TryMove(4, 9, 5, 7, out _));
            Assert.IsTrue(engine.TryMove(4, 9, 2, 8, out _));
        }

        [Test]
        public void Cannon_CapturesOnlyAcrossOneScreen()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 3, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Cannon, JieqiRole.Cannon, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 6);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 3);
            Assert.IsFalse(engine.TryMove(4, 9, 4, 6, out _));
            Assert.IsFalse(engine.TryMove(4, 9, 4, 5, out _));
            Assert.IsTrue(engine.TryMove(4, 9, 4, 3, out var step));
            Assert.IsTrue(step.Captured);
            Assert.AreEqual(JieqiRole.Pawn, step.CapturedRole);
        }

        [Test]
        public void AdvisorAndElephant_LoosenAfterTheyFlip()
        {
            var dark = new JieqiEngine();
            dark.BeginCustom(JieqiCamp.Black);
            AddKings(dark, 4, 0, 5, 9);
            dark.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Advisor, JieqiRole.Advisor, true, 5, 0);
            Assert.IsFalse(dark.TryMove(5, 0, 6, 1, out _));
            Assert.IsTrue(dark.TryMove(5, 0, 4, 1, out _));

            var open = new JieqiEngine();
            open.BeginCustom(JieqiCamp.Red);
            AddKings(open, 4, 0, 5, 9);
            open.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 5, 7);
            Assert.IsTrue(open.TryMove(5, 7, 6, 6, out _));

            var river = new JieqiEngine();
            river.BeginCustom(JieqiCamp.Red);
            AddKings(river, 4, 0, 5, 9);
            river.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Elephant, JieqiRole.Elephant, true, 2, 5);
            Assert.IsFalse(river.TryMove(2, 5, 4, 3, out _));
            Assert.IsTrue(river.TryMove(2, 5, 4, 7, out _));

            var crossed = new JieqiEngine();
            crossed.BeginCustom(JieqiCamp.Red);
            AddKings(crossed, 4, 0, 5, 9);
            crossed.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Elephant, JieqiRole.Elephant, false, 2, 5);
            Assert.IsTrue(crossed.TryMove(2, 5, 4, 3, out _));
        }

        [Test]
        public void Advisor_AcrossTheRiverCanStepEveryDiagonal()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 0, 0, 8, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 4);
            var moves = new List<JieqiMove>();
            engine.CollectMoves(moves);
            Assert.IsTrue(HasMove(moves, 4, 4, 3, 3));
            Assert.IsTrue(HasMove(moves, 4, 4, 3, 5));
            Assert.IsTrue(HasMove(moves, 4, 4, 5, 3));
            Assert.IsTrue(HasMove(moves, 4, 4, 5, 5));
            Assert.IsTrue(engine.TryMove(4, 4, 5, 3, out _));
        }

        [Test]
        public void DarkAdvisor_CanStepIntoPalaceEvenIfRevealWouldCheck()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Horse, JieqiRole.Horse, false, 4, 1);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Black, JieqiRole.Pawn, JieqiRole.Advisor, true, 5, 9);

            var moves = new List<JieqiMove>();
            engine.CollectMoves(moves);
            Assert.IsTrue(HasMove(moves, 5, 9, 4, 8));
            Assert.IsFalse(HasMove(moves, 5, 9, 5, 8));
            Assert.IsTrue(engine.TryMove(5, 9, 4, 8, out var step));
            Assert.IsTrue(step.Flipped);
            Assert.AreEqual(JieqiCamp.Black, step.RevealedCamp);
            Assert.IsTrue(engine.TryView(4, 8, out var view));
            Assert.AreEqual(JieqiCamp.Black, view.Controller);
            Assert.AreEqual(JieqiRole.Pawn, view.Role);
        }

        [Test]
        public void Advisor_SoleScreenBetweenKingsCannotMove()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 4);
            var moves = new List<JieqiMove>();
            engine.CollectMoves(moves);
            Assert.IsFalse(HasMove(moves, 4, 4, 3, 3));
            Assert.IsFalse(HasMove(moves, 4, 4, 5, 5));
        }

        [Test]
        public void Kings_CannotFaceOrLeaveThePalace()
        {
            var facing = new JieqiEngine();
            facing.BeginCustom(JieqiCamp.Red);
            facing.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            facing.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            facing.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 5);
            Assert.IsFalse(facing.TryMove(4, 5, 3, 4, out _));
            Assert.IsTrue(facing.TryMove(4, 9, 3, 9, out _));

            var along = new JieqiEngine();
            along.BeginCustom(JieqiCamp.Red);
            along.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            along.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            Assert.IsFalse(along.TryMove(4, 9, 4, 8, out _));
            Assert.IsTrue(along.TryMove(4, 9, 3, 9, out _));

            var palace = new JieqiEngine();
            palace.BeginCustom(JieqiCamp.Red);
            palace.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 3, 0);
            palace.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 7);
            Assert.IsFalse(palace.TryMove(4, 7, 4, 6, out _));
            Assert.IsTrue(palace.TryMove(4, 7, 4, 8, out _));
        }

        [Test]
        public void Check_MustBeAnswered()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 6, 0);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 4, 5);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 0, 6);
            Assert.IsTrue(engine.InCheck());
            Assert.IsFalse(engine.TryMove(0, 6, 0, 5, out _));
            Assert.IsTrue(engine.TryMove(4, 9, 3, 9, out _));
        }

        [Test]
        public void NoLegalMove_Loses()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Black);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 6, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 4, 1);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 5, 1);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 3, 2);
            engine.RefreshTerminal();
            Assert.AreEqual(JieqiOutcome.RedWin, engine.Outcome);
        }

        [Test]
        public void OwnSide_CanCaptureFaceDownButNotFaceUp()
        {
            var hidden = new JieqiEngine();
            hidden.BeginCustom(JieqiCamp.Red);
            AddKings(hidden, 4, 0, 5, 9);
            hidden.AddPiece(JieqiCamp.Black, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Advisor, true, 4, 8);
            hidden.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 4, 9);
            Assert.IsTrue(hidden.TryMove(4, 9, 4, 8, out var step));
            Assert.IsTrue(step.Captured);
            Assert.IsTrue(step.CapturedWasHidden);
            Assert.AreEqual(JieqiRole.Pawn, step.CapturedRole);
            Assert.AreEqual(JieqiCamp.Red, step.CapturedController);

            var shown = new JieqiEngine();
            shown.BeginCustom(JieqiCamp.Red);
            AddKings(shown, 4, 0, 5, 9);
            shown.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 8);
            shown.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 4, 9);
            Assert.IsFalse(shown.TryMove(4, 9, 4, 8, out _));
        }

        [Test]
        public void Capture_RevealsTheVictimAndParksItBehindTheCapturer()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            AddKings(engine, 4, 0, 5, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 0, 0);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Pawn, true, 0, 5);

            Assert.IsTrue(engine.TryMove(0, 0, 0, 5, out var step));
            Assert.IsTrue(step.Captured);
            Assert.IsTrue(step.CapturedWasHidden);
            Assert.AreEqual(JieqiCamp.Red, step.CapturedCamp);
            Assert.AreEqual(JieqiRole.Rook, step.CapturedRole);
            Assert.AreEqual(JieqiCamp.Black, step.CapturedController);
            Assert.IsTrue(engine.TryView(0, 5, out var view));
            Assert.AreEqual(JieqiRole.Rook, view.Role);
            Assert.AreEqual(JieqiCamp.Red, view.Controller);

            var held = new List<JieqiCapturedView>();
            engine.CopyCaptured(held);
            Assert.AreEqual(1, held.Count);
            Assert.AreEqual(JieqiCamp.Red, held[0].CapturedBy);
            Assert.AreEqual(JieqiCamp.Red, held[0].Printed);
            Assert.AreEqual(JieqiRole.Rook, held[0].Role);
        }

        [Test]
        public void Restore_BringsBackTheSamePosition()
        {
            var engine = new JieqiEngine();
            engine.Deal(11);
            var save = new JieqiPieceState[JieqiEngine.MaxPieces];
            var count = engine.CopySave(save);
            var hash = engine.Hash();
            var moves = new List<JieqiMove>();
            engine.CollectMoves(moves);
            Assert.IsTrue(moves.Count > 0);
            Assert.IsTrue(engine.TryMove(moves[0].FromX, moves[0].FromY, moves[0].ToX, moves[0].ToY, out _));
            Assert.AreNotEqual(hash, engine.Hash());
            engine.Restore(save, count, JieqiCamp.Red, JieqiOutcome.Playing, true);
            Assert.AreEqual(hash, engine.Hash());
        }

        [Test]
        public void CloneBlind_PermutesHiddenPiecesWithoutTouchingTheSource()
        {
            var engine = new JieqiEngine();
            engine.Deal(5);
            var before = engine.Hash();
            var blind = engine.CloneBlind(new Random(2));
            Assert.AreEqual(before, engine.Hash());

            var source = new JieqiPieceState[JieqiEngine.MaxPieces];
            var guessed = new JieqiPieceState[JieqiEngine.MaxPieces];
            var count = engine.CopySave(source);
            blind.CopySave(guessed);
            var sourceBag = new int[40];
            var guessBag = new int[40];
            var moved = false;
            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(source[i].Controller, guessed[i].Controller);
                Assert.AreEqual(source[i].SlotRole, guessed[i].SlotRole);
                Assert.AreEqual(source[i].FaceDown, guessed[i].FaceDown);
                Assert.AreEqual(source[i].X, guessed[i].X);
                if (!source[i].FaceDown)
                {
                    Assert.AreEqual(source[i].Role, guessed[i].Role);
                    Assert.AreEqual(source[i].Printed, guessed[i].Printed);
                    continue;
                }

                sourceBag[Code(source[i])]++;
                guessBag[Code(guessed[i])]++;
                if (source[i].Role != guessed[i].Role || source[i].Printed != guessed[i].Printed)
                    moved = true;
            }

            Assert.IsTrue(moved);
            for (var i = 0; i < sourceBag.Length; i++)
                Assert.AreEqual(sourceBag[i], guessBag[i]);
        }

        [Test]
        public void Endgames_MatchPlannedDistance()
        {
            Assert.AreEqual(30, JieqiEndgameCatalog.Count);
            var tt = new Dictionary<ulong, int>(1024);
            var pools = new List<JieqiMove>[8];
            var snaps = new JieqiMove[8][];
            var states = new JieqiPieceState[8][];
            for (var depth = 0; depth < pools.Length; depth++)
            {
                pools[depth] = new List<JieqiMove>(64);
                snaps[depth] = new JieqiMove[64];
                states[depth] = new JieqiPieceState[JieqiEngine.MaxPieces];
            }

            for (var i = 0; i < JieqiEndgameCatalog.Count; i++)
            {
                var title = JieqiEndgameCatalog.Title(i);
                var expect = i < 10 ? 1 : i < 22 ? 2 : 3;
                var engine = new JieqiEngine();
                JieqiEndgameCatalog.Load(engine, i);
                Assert.AreEqual(JieqiOutcome.Playing, engine.Outcome, title);
                Assert.AreEqual(JieqiCamp.Red, engine.Turn, title);
                Assert.IsFalse(engine.InCheck(), title);
                Assert.AreEqual("红先走", JieqiEndgameCatalog.Hint(i), title);

                tt.Clear();
                Assert.AreEqual(expect, Forced(engine, expect, 0), title);
            }

            int Forced(JieqiEngine engine, int redLeft, int depth)
            {
                if (engine.Outcome == JieqiOutcome.RedWin)
                    return 0;
                if (engine.Outcome != JieqiOutcome.Playing || depth >= pools.Length)
                    return 99;

                var key = engine.Hash() ^ ((ulong)redLeft << 56);
                if (tt.TryGetValue(key, out var known))
                    return known;

                var moves = pools[depth];
                engine.CollectMoves(moves);
                if (moves.Count == 0)
                {
                    var lost = engine.Turn == JieqiCamp.Black ? 0 : 99;
                    tt[key] = lost;
                    return lost;
                }

                if (moves.Count > snaps[depth].Length)
                    return 99;

                var count = engine.CopySave(states[depth]);
                var side = engine.Turn;
                var outcome = engine.Outcome;
                for (var n = 0; n < moves.Count; n++)
                    snaps[depth][n] = moves[n];
                var moveCount = moves.Count;

                if (side == JieqiCamp.Red)
                {
                    if (redLeft <= 0)
                    {
                        tt[key] = 99;
                        return 99;
                    }

                    var best = 99;
                    for (var n = 0; n < moveCount; n++)
                    {
                        var move = snaps[depth][n];
                        Assert.IsTrue(engine.TryMove(move.FromX, move.FromY, move.ToX, move.ToY, out _));
                        var next = engine.Outcome == JieqiOutcome.RedWin ? 1 : 1 + Forced(engine, redLeft - 1, depth + 1);
                        engine.Restore(states[depth], count, side, outcome, false);
                        if (next < best)
                            best = next;
                        if (best == 1)
                            break;
                    }

                    tt[key] = best;
                    return best;
                }

                var worst = 0;
                for (var n = 0; n < moveCount; n++)
                {
                    var move = snaps[depth][n];
                    Assert.IsTrue(engine.TryMove(move.FromX, move.FromY, move.ToX, move.ToY, out _));
                    int next;
                    if (engine.Outcome == JieqiOutcome.RedWin)
                        next = 0;
                    else if (engine.Outcome != JieqiOutcome.Playing)
                        next = 99;
                    else
                        next = Forced(engine, redLeft, depth + 1);
                    engine.Restore(states[depth], count, side, outcome, false);
                    if (next > worst)
                        worst = next;
                    if (worst >= 99)
                        break;
                }

                tt[key] = worst;
                return worst;
            }
        }

        [Test]
        public void Ai_CapturesTheKingAndDoesNotDirtyTheBoardWhileSearching()
        {
            var engine = new JieqiEngine();
            engine.BeginCustom(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 4, 1);
            var hash = engine.Hash();
            var ai = new JieqiAi();
            Assert.IsTrue(ai.TryChoose(engine, new Random(1), out var move));
            Assert.AreEqual(hash, engine.Hash());
            Assert.AreEqual(4, move.ToX);
            Assert.AreEqual(0, move.ToY);
            Assert.IsTrue(engine.TryMove(move.FromX, move.FromY, move.ToX, move.ToY, out var step));
            Assert.AreEqual(JieqiOutcome.RedWin, step.Outcome);
        }

        [Test]
        public void TwoPlayer_AlternatesAndDrawsOnTheThirdRepeat()
        {
            var match = new JieqiMatch();
            var engine = match.Engine;
            engine.BeginCustom(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 4);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 0, 5);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 8, 5);
            match.StartOnCurrent(JieqiPlayMode.TwoPlayer, JieqiCamp.Red, 1);

            int[,] cycle =
            {
                { 0, 5, 0, 6 },
                { 8, 5, 8, 6 },
                { 0, 6, 0, 5 },
                { 8, 6, 8, 5 }
            };
            for (var lap = 0; lap < 2; lap++)
            {
                for (var i = 0; i < 4; i++)
                {
                    Assert.IsTrue(match.PlayHuman(cycle[i, 0], cycle[i, 1], cycle[i, 2], cycle[i, 3], out var step), step.Message);
                    if (lap == 0)
                        Assert.AreEqual(JieqiOutcome.Playing, step.Outcome);
                }
            }

            Assert.AreEqual(JieqiOutcome.Draw, engine.Outcome);
            Assert.IsFalse(match.PlayHuman(0, 5, 0, 6, out _));
        }

        [Test]
        public void VersusAi_LetsTheHumanAndTheComputerTakeTurns()
        {
            var match = new JieqiMatch();
            match.Start(JieqiPlayMode.VersusAi, 4, JieqiCamp.Red);
            Assert.IsTrue(match.HumanToMove);
            Assert.IsFalse(match.AiToMove);

            var moves = new List<JieqiMove>();
            match.Engine.CollectMoves(moves);
            Assert.IsTrue(moves.Count > 0);
            var human = moves[0];
            Assert.IsTrue(match.PlayHuman(human.FromX, human.FromY, human.ToX, human.ToY, out _));
            Assert.IsFalse(match.PlayHuman(human.ToX, human.ToY, human.FromX, human.FromY, out var rejected));
            Assert.AreEqual("还没轮到你", rejected.Message);
            Assert.IsTrue(match.PlayAi(out var aiStep), aiStep.Message);
            Assert.IsTrue(aiStep.Ok);
            Assert.AreEqual(JieqiCamp.Red, match.Engine.Turn);
        }

        static bool HasMove(List<JieqiMove> moves, int fromX, int fromY, int toX, int toY)
        {
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.FromX == fromX && move.FromY == fromY && move.ToX == toX && move.ToY == toY)
                    return true;
            }

            return false;
        }

        static void AddKings(JieqiEngine engine, int blackX, int blackY, int redX, int redY)
        {
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, blackX, blackY);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, redX, redY);
        }

        static void AssertBag(int[,] printed, JieqiCamp camp)
        {
            Assert.AreEqual(1, printed[(int)camp, (int)JieqiRole.King]);
            Assert.AreEqual(2, printed[(int)camp, (int)JieqiRole.Rook]);
            Assert.AreEqual(2, printed[(int)camp, (int)JieqiRole.Horse]);
            Assert.AreEqual(2, printed[(int)camp, (int)JieqiRole.Elephant]);
            Assert.AreEqual(2, printed[(int)camp, (int)JieqiRole.Advisor]);
            Assert.AreEqual(2, printed[(int)camp, (int)JieqiRole.Cannon]);
            Assert.AreEqual(5, printed[(int)camp, (int)JieqiRole.Pawn]);
        }

        static int Code(JieqiPieceState piece)
        {
            return ((int)piece.Printed << 4) | (int)piece.Role;
        }

        [Test]
        public void Skill_OpeningIsStandardAndFaceUp()
        {
            var engine = new JieqiEngine();
            engine.DealSkill();
            Assert.AreEqual(JieqiOutcome.Playing, engine.Outcome);
            Assert.AreEqual(JieqiCamp.Red, engine.Turn);
            Assert.IsTrue(engine.TryView(4, 9, out var redKing));
            Assert.AreEqual(JieqiRole.King, redKing.Role);
            Assert.IsFalse(redKing.FaceDown);
            Assert.IsFalse(redKing.HasSkill);
            Assert.IsTrue(engine.TryView(0, 9, out var rook));
            Assert.AreEqual(JieqiRole.Rook, rook.Role);
            Assert.AreEqual(JieqiCamp.Red, rook.Printed);
            Assert.IsTrue(engine.TryView(1, 7, out var cannon));
            Assert.AreEqual(JieqiRole.Cannon, cannon.Role);
            Assert.IsTrue(engine.TryView(4, 6, out var pawn));
            Assert.AreEqual(JieqiRole.Pawn, pawn.Role);
            Assert.IsTrue(HasMove(engine, 4, 6, 4, 5));
            Assert.IsFalse(HasMove(engine, 4, 6, 3, 6));
            Assert.IsFalse(HasMove(engine, 4, 6, 5, 6));
            Assert.IsFalse(HasMove(engine, 3, 9, 2, 8));
            Assert.IsTrue(HasMove(engine, 3, 9, 4, 8));
        }

        [Test]
        public void Skill_HorseEatsRook_GainsRookSlide()
        {
            var engine = SkillKings();
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Horse, JieqiRole.Horse, false, 2, 4);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 4, 5);
            Assert.IsFalse(HasMove(engine, 2, 4, 2, 8));
            Assert.IsTrue(engine.TryMove(2, 4, 4, 5, out _));
            Assert.IsTrue(engine.TryView(4, 5, out var horse));
            Assert.AreEqual(JieqiRole.Horse, horse.Role);
            Assert.AreEqual(JieqiRole.Rook, horse.SkillRole);
            Assert.AreEqual(JieqiCamp.Black, horse.SkillPrinted);
            Assert.IsTrue(engine.TryMove(5, 0, 5, 1, out _));
            Assert.IsTrue(HasMove(engine, 4, 5, 8, 5));
        }

        [Test]
        public void Skill_CannonEatsPawn_CapturesAdjacent()
        {
            var engine = SkillKings();
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Cannon, JieqiRole.Cannon, false, 4, 7);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 6);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Pawn, JieqiRole.Pawn, false, 4, 5);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Advisor, JieqiRole.Advisor, false, 4, 4);
            Assert.IsTrue(engine.TryMove(4, 7, 4, 5, out _));
            Assert.IsTrue(engine.TryView(4, 5, out var cannon));
            Assert.AreEqual(JieqiRole.Pawn, cannon.SkillRole);
            Assert.IsTrue(engine.TryMove(5, 0, 5, 1, out _));
            Assert.IsTrue(HasMove(engine, 4, 5, 4, 4));
            Assert.IsFalse(HasMove(engine, 4, 5, 3, 4));
        }

        [Test]
        public void Skill_CaptureDual_InheritsOriginalOnly()
        {
            var engine = new JieqiEngine();
            engine.BeginSkill(JieqiCamp.Black);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 3, 9);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Horse, JieqiRole.Horse, false, 7, 4);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Cannon, JieqiRole.Cannon, false, 5, 5);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 5, 8);
            Assert.IsTrue(engine.TryMove(7, 4, 5, 5, out _));
            Assert.IsTrue(engine.TryView(5, 5, out var horse));
            Assert.AreEqual(JieqiRole.Cannon, horse.SkillRole);
            Assert.IsTrue(engine.TryMove(5, 8, 5, 5, out _));
            Assert.IsTrue(engine.TryView(5, 5, out var rook));
            Assert.AreEqual(JieqiRole.Rook, rook.Role);
            Assert.AreEqual(JieqiRole.Horse, rook.SkillRole);
            Assert.AreEqual(JieqiCamp.Black, rook.SkillPrinted);
        }

        [Test]
        public void Skill_RookEatsRook_CapturesOverOneScreen()
        {
            var engine = SkillKings();
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Rook, JieqiRole.Rook, false, 3, 6);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 5, 6);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Pawn, JieqiRole.Pawn, false, 5, 4);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Horse, JieqiRole.Horse, false, 5, 2);
            Assert.IsFalse(HasMove(engine, 3, 6, 5, 2));
            Assert.IsTrue(engine.TryMove(3, 6, 5, 6, out _));
            Assert.IsTrue(engine.TryView(5, 6, out var rook));
            Assert.IsTrue(rook.HasSkill);
            Assert.AreEqual(JieqiRole.Rook, rook.Role);
            Assert.AreEqual(JieqiRole.Rook, rook.SkillRole);
            Assert.IsTrue(engine.TryMove(5, 0, 5, 1, out _));
            Assert.IsTrue(HasMove(engine, 5, 6, 5, 2));
        }

        [Test]
        public void Skill_StandardLimitsAndRestore()
        {
            var engine = SkillKings();
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Pawn, JieqiRole.Pawn, false, 0, 6);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Elephant, JieqiRole.Elephant, false, 2, 5);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Advisor, JieqiRole.Advisor, false, 5, 9);
            Assert.IsFalse(HasMove(engine, 0, 6, 1, 6));
            Assert.IsTrue(HasMove(engine, 0, 6, 0, 5));
            Assert.IsFalse(HasMove(engine, 2, 5, 4, 3));
            Assert.IsFalse(HasMove(engine, 5, 9, 6, 8));
            Assert.IsTrue(HasMove(engine, 5, 9, 4, 8));

            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Horse, JieqiRole.Horse, false, 2, 4);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 4, 5);
            var save = new JieqiPieceState[JieqiEngine.MaxPieces];
            var count = engine.CopySave(save);
            Assert.IsTrue(engine.TryMove(2, 4, 4, 5, out _));
            Assert.IsTrue(engine.TryView(4, 5, out var skilled));
            Assert.IsTrue(skilled.HasSkill);
            var after = new JieqiPieceState[JieqiEngine.MaxPieces];
            var afterCount = engine.CopySave(after);
            engine.Restore(save, count, JieqiCamp.Red, JieqiOutcome.Playing, false, true);
            Assert.IsTrue(engine.TryView(2, 4, out var plain));
            Assert.IsFalse(plain.HasSkill);
            Assert.IsFalse(HasMove(engine, 2, 4, 2, 8));
            engine.Restore(after, afterCount, JieqiCamp.Black, JieqiOutcome.Playing, false, true);
            Assert.IsTrue(engine.TryView(4, 5, out var again));
            Assert.AreEqual(JieqiRole.Rook, again.SkillRole);
            Assert.IsTrue(engine.TryMove(5, 0, 5, 1, out _));
            Assert.IsTrue(HasMove(engine, 4, 5, 8, 5));
        }

        [Test]
        public void Undo_RestoresLastPly_AndSkill()
        {
            var match = new JieqiMatch();
            match.Engine.BeginSkill(JieqiCamp.Red);
            match.Engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 3, 9);
            match.Engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 5, 0);
            match.Engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Horse, JieqiRole.Horse, false, 2, 4);
            match.Engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 4, 5);
            match.StartOnCurrent(JieqiPlayMode.TwoPlayer, JieqiCamp.Red, 1);
            Assert.IsFalse(match.TryUndo());
            Assert.IsTrue(match.PlayHuman(2, 4, 4, 5, out _));
            Assert.IsTrue(match.Engine.TryView(4, 5, out var skilled));
            Assert.IsTrue(skilled.HasSkill);
            Assert.AreEqual(JieqiRole.Rook, skilled.SkillRole);
            Assert.IsTrue(match.TryUndo());
            Assert.AreEqual(JieqiCamp.Red, match.Engine.Turn);
            Assert.AreEqual(JieqiOutcome.Playing, match.Engine.Outcome);
            Assert.IsTrue(match.Engine.TryView(2, 4, out var horse));
            Assert.IsFalse(horse.HasSkill);
            Assert.IsTrue(match.Engine.TryView(4, 5, out var rook));
            Assert.AreEqual(JieqiRole.Rook, rook.Role);
        }

        [Test]
        public void Undo_VersusAiReturnsToHumanMove()
        {
            var match = new JieqiMatch();
            match.Engine.BeginSkill(JieqiCamp.Red);
            match.Engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 3, 9);
            match.Engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 5, 0);
            match.Engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.Horse, JieqiRole.Horse, false, 2, 4);
            match.Engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.Rook, JieqiRole.Rook, false, 4, 5);
            match.StartOnCurrent(JieqiPlayMode.VersusAi, JieqiCamp.Red, 3);
            Assert.IsTrue(match.PlayHuman(2, 4, 4, 5, out _));
            Assert.IsTrue(match.PlayAi(out _));
            Assert.AreEqual(JieqiCamp.Red, match.Engine.Turn);
            Assert.IsTrue(match.TryUndo());
            Assert.AreEqual(JieqiCamp.Red, match.Engine.Turn);
            Assert.IsTrue(match.Engine.TryView(2, 4, out var horse));
            Assert.AreEqual(JieqiRole.Horse, horse.Role);
            Assert.IsFalse(horse.HasSkill);
            Assert.IsTrue(match.Engine.TryView(4, 5, out var rook));
            Assert.AreEqual(JieqiRole.Rook, rook.Role);
        }

        static bool HasMove(JieqiEngine engine, int fromX, int fromY, int toX, int toY)
        {
            var moves = new List<JieqiMove>();
            engine.CollectMoves(moves);
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.FromX == fromX && move.FromY == fromY && move.ToX == toX && move.ToY == toY)
                    return true;
            }

            return false;
        }

        static JieqiEngine SkillKings()
        {
            var engine = new JieqiEngine();
            engine.BeginSkill(JieqiCamp.Red);
            engine.AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 3, 9);
            engine.AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 5, 0);
            return engine;
        }

    }
}
