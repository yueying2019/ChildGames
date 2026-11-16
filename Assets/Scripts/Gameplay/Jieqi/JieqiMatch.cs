using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 同一盘棋的两种下法：面对面轮流走，或红黑有一方交给电脑。
    /// 电脑不会在暗子翻开前知道底牌。吃掉的子留在吃方身后，由局面上的俘虏列表表示。
    /// </summary>
    public sealed class JieqiMatch
    {
        readonly JieqiEngine _engine = new JieqiEngine();
        readonly JieqiAi _ai = new JieqiAi();
        readonly List<ulong> _hashes = new List<ulong>(160);
        readonly List<Mark> _marks = new List<Mark>(160);
        Random _aiRng = new Random(1);

        public JieqiEngine Engine => _engine;
        public JieqiPlayMode Mode { get; private set; }
        public JieqiCamp HumanCamp { get; private set; }

        public bool HumanToMove =>
            _engine.Outcome == JieqiOutcome.Playing &&
            (Mode == JieqiPlayMode.TwoPlayer || _engine.Turn == HumanCamp);

        public bool AiToMove =>
            Mode == JieqiPlayMode.VersusAi &&
            _engine.Outcome == JieqiOutcome.Playing &&
            _engine.Turn != HumanCamp;

        public void Start(JieqiPlayMode mode, int seed, JieqiCamp humanCamp)
        {
            Mode = mode;
            HumanCamp = humanCamp == JieqiCamp.Black ? JieqiCamp.Black : JieqiCamp.Red;
            _aiRng = new Random(seed + 7919);
            _engine.Deal(seed);
            ResetHashes();
        }

        /// <summary>用引擎里已经摆好的局面开始。自定义残局走这里，不要再 Deal。</summary>
        public void StartOnCurrent(JieqiPlayMode mode, JieqiCamp humanCamp, int aiSeed)
        {
            Mode = mode;
            HumanCamp = humanCamp == JieqiCamp.Black ? JieqiCamp.Black : JieqiCamp.Red;
            _aiRng = new Random(aiSeed);
            _engine.RefreshTerminal();
            ResetHashes();
        }

        public bool PlayHuman(int fromX, int fromY, int toX, int toY, out JieqiStep step)
        {
            if (_engine.Outcome != JieqiOutcome.Playing)
            {
                step = Ended();
                return false;
            }

            if (!HumanToMove)
            {
                step = default;
                step.Message = "还没轮到你";
                step.Outcome = _engine.Outcome;
                return false;
            }

            return Commit(fromX, fromY, toX, toY, out step);
        }

        public bool PlayAi(out JieqiStep step)
        {
            if (_engine.Outcome != JieqiOutcome.Playing)
            {
                step = Ended();
                return false;
            }

            if (!AiToMove)
            {
                step = default;
                step.Message = "现在不是电脑回合";
                step.Outcome = _engine.Outcome;
                return false;
            }

            var depth = JieqiAi.Depth;
            if (SettingsRepository.Current != null)
                depth = SettingsRepository.Current.XiangqiSearchDepth();
            if (!_ai.TryChoose(_engine, _aiRng, depth, out var move))
            {
                _engine.RefreshTerminal();
                step = default;
                step.Message = "电脑无子可走";
                step.Outcome = _engine.Outcome;
                return false;
            }

            return Commit(move.FromX, move.FromY, move.ToX, move.ToY, out step);
        }

        /// <summary>
        /// 退回上一步。人机对局如果刚被电脑应过，连你自己的那步一起退，否则电脑会立刻再走。
        /// </summary>
        public bool TryUndo()
        {
            if (!RestoreMark())
                return false;
            if (Mode == JieqiPlayMode.VersusAi && AiToMove)
                RestoreMark();
            return true;
        }

        bool Commit(int fromX, int fromY, int toX, int toY, out JieqiStep step)
        {
            var mark = Capture();
            if (!_engine.TryMove(fromX, fromY, toX, toY, out step))
                return false;
            _marks.Add(mark);
            Remember();
            if (_engine.Outcome == JieqiOutcome.Draw)
                step.Outcome = JieqiOutcome.Draw;
            return true;
        }

        void ResetHashes()
        {
            _hashes.Clear();
            _marks.Clear();
            _hashes.Add(_engine.Hash());
        }

        Mark Capture()
        {
            var pieces = new JieqiPieceState[JieqiEngine.MaxPieces];
            return new Mark
            {
                Pieces = pieces,
                Count = _engine.CopySave(pieces),
                Side = _engine.Turn,
                Outcome = _engine.Outcome,
                Standard = _engine.StandardDeal,
                Skill = _engine.SkillRules,
                HashCount = _hashes.Count
            };
        }

        bool RestoreMark()
        {
            if (_marks.Count == 0)
                return false;
            var mark = _marks[_marks.Count - 1];
            _marks.RemoveAt(_marks.Count - 1);
            _engine.Restore(mark.Pieces, mark.Count, mark.Side, mark.Outcome, mark.Standard, mark.Skill);
            if (_hashes.Count > mark.HashCount)
                _hashes.RemoveRange(mark.HashCount, _hashes.Count - mark.HashCount);
            return true;
        }

        struct Mark
        {
            public JieqiPieceState[] Pieces;
            public int Count;
            public JieqiCamp Side;
            public JieqiOutcome Outcome;
            public bool Standard;
            public bool Skill;
            public int HashCount;
        }

        void Remember()
        {
            if (_engine.Outcome != JieqiOutcome.Playing)
                return;
            _hashes.Add(_engine.Hash());
            var hash = _hashes[_hashes.Count - 1];
            var count = 0;
            for (var i = 0; i < _hashes.Count; i++)
            {
                if (_hashes[i] == hash)
                    count++;
            }

            if (count >= 3)
                _engine.SetOutcome(JieqiOutcome.Draw);
        }

        JieqiStep Ended()
        {
            return new JieqiStep
            {
                Ok = false,
                Message = "对局已经结束",
                Outcome = _engine.Outcome
            };
        }
    }
}
