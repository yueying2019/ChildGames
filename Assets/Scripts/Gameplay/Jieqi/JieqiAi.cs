using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 电脑只看明子和格子兵种。暗子底牌按剩余棋子随机摊到各暗格上再搜索，不读取真实落点。
    /// 每个样本走两层：自己一步、对方应一步。
    /// </summary>
    public sealed class JieqiAi
    {
        public const int Depth = 2;
        public const int Samples = 4;
        const int Mate = 30000;

        readonly List<JieqiMove>[] _levels;
        readonly List<JieqiMove> _root = new List<JieqiMove>(160);
        int[] _scores = new int[160];

        public JieqiAi()
        {
            _levels = new List<JieqiMove>[Depth + 1];
            for (var i = 0; i < _levels.Length; i++)
                _levels[i] = new List<JieqiMove>(160);
        }

        public bool TryChoose(JieqiEngine source, Random rng, out JieqiMove move)
        {
            move = default;
            if (source == null || source.Outcome != JieqiOutcome.Playing)
                return false;
            if (rng == null)
                rng = new Random(1);

            _root.Clear();
            source.CollectMoves(_root);
            if (_root.Count == 0)
                return false;

            if (_scores.Length < _root.Count)
                _scores = new int[_root.Count];
            for (var i = 0; i < _root.Count; i++)
                _scores[i] = 0;

            var samples = source.StandardDeal ? Samples : 1;
            for (var sample = 0; sample < samples; sample++)
            {
                var blind = source.CloneBlind(rng);
                for (var i = 0; i < _root.Count; i++)
                {
                    var undo = blind.Make(_root[i]);
                    var score = -Negamax(blind, Depth - 1, -Mate - 1000, Mate + 1000);
                    blind.Unmake(undo);
                    _scores[i] += score;
                }
            }

            var best = 0;
            for (var i = 1; i < _root.Count; i++)
            {
                if (_scores[i] > _scores[best])
                {
                    best = i;
                    continue;
                }

                if (_scores[i] == _scores[best] && _root[i].Captured >= 0 && _root[best].Captured < 0)
                    best = i;
            }

            move = _root[best];
            return true;
        }

        int Negamax(JieqiEngine engine, int depth, int alpha, int beta)
        {
            if (engine.Outcome == JieqiOutcome.RedWin)
                return engine.Turn == JieqiCamp.Red ? Mate : -Mate;
            if (engine.Outcome == JieqiOutcome.BlackWin)
                return engine.Turn == JieqiCamp.Black ? Mate : -Mate;
            if (engine.Outcome == JieqiOutcome.Draw)
                return 0;
            if (depth <= 0)
                return Evaluate(engine);

            var moves = _levels[depth];
            engine.CollectMoves(moves);
            if (moves.Count == 0)
                return -Mate;

            CapturesFirst(moves);
            var best = -Mate - 1000;
            for (var i = 0; i < moves.Count; i++)
            {
                var undo = engine.Make(moves[i]);
                var score = -Negamax(engine, depth - 1, -beta, -alpha);
                engine.Unmake(undo);
                if (score > best)
                    best = score;
                if (score > alpha)
                    alpha = score;
                if (alpha >= beta)
                    break;
            }

            return best;
        }

        static int Evaluate(JieqiEngine engine)
        {
            var me = engine.Turn;
            var score = 0;
            var pieces = engine.Pieces;
            for (var i = 0; i < engine.PieceCount; i++)
            {
                var piece = pieces[i];
                if (!piece.Alive || piece.Role == JieqiRole.King)
                    continue;
                var value = Material(piece.Role);
                if (!piece.FaceDown && piece.Role == JieqiRole.Pawn && Advanced(piece))
                    value += 1;
                if (piece.Controller == me)
                    score += value;
                else
                    score -= value;
            }

            if (engine.KingInCheck(me))
                score -= 3;
            return score;
        }

        static int Material(JieqiRole role)
        {
            switch (role)
            {
                case JieqiRole.Rook: return 18;
                case JieqiRole.Cannon: return 9;
                case JieqiRole.Horse: return 8;
                case JieqiRole.Elephant: return 4;
                case JieqiRole.Advisor: return 4;
                case JieqiRole.Pawn: return 2;
                default: return 0;
            }
        }

        static bool Advanced(JieqiEngine.JieqiPiece piece)
        {
            if (piece.Controller == JieqiCamp.Red)
                return piece.Y <= 4;
            return piece.Y >= 5;
        }

        static void CapturesFirst(List<JieqiMove> moves)
        {
            var write = 0;
            for (var i = 0; i < moves.Count; i++)
            {
                if (moves[i].Captured < 0)
                    continue;
                var tmp = moves[write];
                moves[write] = moves[i];
                moves[i] = tmp;
                write++;
            }
        }
    }
}
