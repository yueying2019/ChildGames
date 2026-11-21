using System;
using System.Collections.Generic;

namespace SmallGames
{
    /// <summary>
    /// 揭棋。红方在下（y 越大越靠近红方），红方先行。
    /// 除将、帅外，双方各 15 子混在一起，随机扣在双方全部开局格上，红子可以落在黑方一侧。
    /// 暗子由扣下去时所在的阵营走，走完翻开。翻开后改按棋子颜色归属，对方的子只能由对方走。
    /// 暗子按所在格的兵种走一步就翻开，之后按真实兵种走。
    /// 暗象不过河，暗士不出九宫；翻开后象仍走田、塞象眼，但可以过河；士仍斜走一格，但可以出宫。
    /// 将、帅不出九宫，不能照面。兵、卒按当前控制方前进：红向上、黑向下，过河前不能横走，过河后可左右各一格，不能后退。
    /// 没翻开的子，对方能吃，己方也能吃。翻开后的己方子不能吃。
    /// 被吃的子无论明暗都翻给双方看，记在吃方身后，不再回到棋盘。
    /// 被将军必须应将。无子可走判负。同一局面第三次出现判和。不判长将、长捉。
    /// </summary>
    public sealed class JieqiEngine
    {
        public const int Files = 9;
        public const int Ranks = 10;
        public const int MaxPieces = 32;

        readonly JieqiPiece[] _pieces = new JieqiPiece[MaxPieces];
        readonly int[] _board = new int[Files * Ranks];
        readonly int[] _bag = new int[MaxPieces];
        readonly int[] _hiddenIds = new int[MaxPieces];
        readonly List<JieqiMove> _moveBuf = new List<JieqiMove>(160);
        readonly List<EditorMark> _editorMarks = new List<EditorMark>(16);
        EditorMark _origin;
        bool _hasOrigin;

        int _pieceCount;
        JieqiCamp _side;
        JieqiOutcome _outcome;
        bool _standard;
        bool _skillRules;
        bool _addingSkill;

        public JieqiCamp Turn => _side;
        public JieqiOutcome Outcome => _outcome;
        public bool StandardDeal => _standard;
        public bool SkillRules => _skillRules;
        public int PieceCount => _pieceCount;

        public JieqiEngine()
        {
            Clear();
        }

        public static int Index(int x, int y)
        {
            return y * Files + x;
        }

        public void Deal(int seed)
        {
            BeginCustom(JieqiCamp.Red);
            var rng = new Random(seed);
            var n = 0;
            n = AddCampBag(n, JieqiCamp.Red);
            n = AddCampBag(n, JieqiCamp.Black);
            for (var i = n - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var tmp = _bag[i];
                _bag[i] = _bag[j];
                _bag[j] = tmp;
            }

            var cursor = 0;
            cursor = PlaceRank(cursor, 0, JieqiCamp.Black, BackRank);
            cursor = PlaceCannons(cursor, 2, JieqiCamp.Black);
            cursor = PlacePawns(cursor, 3, JieqiCamp.Black);
            cursor = PlacePawns(cursor, 6, JieqiCamp.Red);
            cursor = PlaceCannons(cursor, 7, JieqiCamp.Red);
            PlaceRank(cursor, 9, JieqiCamp.Red, BackRank);

            AddPiece(JieqiCamp.Black, JieqiCamp.Black, JieqiRole.King, JieqiRole.King, false, 4, 0);
            AddPiece(JieqiCamp.Red, JieqiCamp.Red, JieqiRole.King, JieqiRole.King, false, 4, 9);
            _side = JieqiCamp.Red;
            _standard = true;
            RefreshTerminal();
        }

        /// <summary>
        /// 技能象棋开局：棋子全部明着，按标准象棋摆好。走子用标准规则，吃子后再获得技能。
        /// </summary>
        public void DealSkill()
        {
            BeginSkill(JieqiCamp.Red);
            for (var x = 0; x < Files; x++)
            {
                PlaceOpen(JieqiCamp.Black, BackRank[x], x, 0);
                PlaceOpen(JieqiCamp.Red, BackRank[x], x, 9);
            }

            PlaceOpen(JieqiCamp.Black, JieqiRole.Cannon, 1, 2);
            PlaceOpen(JieqiCamp.Black, JieqiRole.Cannon, 7, 2);
            PlaceOpen(JieqiCamp.Red, JieqiRole.Cannon, 1, 7);
            PlaceOpen(JieqiCamp.Red, JieqiRole.Cannon, 7, 7);
            for (var i = 0; i < 5; i++)
            {
                PlaceOpen(JieqiCamp.Black, JieqiRole.Pawn, i * 2, 3);
                PlaceOpen(JieqiCamp.Red, JieqiRole.Pawn, i * 2, 6);
            }

            RefreshTerminal();
        }

        /// <summary>清空棋盘并打开技能规则，用来摆测试局面或残局。必须先于 AddPiece。</summary>
        public void BeginSkill(JieqiCamp sideToMove)
        {
            BeginCustom(sideToMove);
            _skillRules = true;
        }

        public void BeginCustom(JieqiCamp sideToMove)
        {
            Clear();
            _editorMarks.Clear();
            _hasOrigin = false;
            _side = sideToMove == JieqiCamp.Black ? JieqiCamp.Black : JieqiCamp.Red;
            _outcome = JieqiOutcome.Playing;
            _standard = false;
        }

        public bool AddPiece(
            JieqiCamp controller,
            JieqiCamp printed,
            JieqiRole role,
            JieqiRole slotRole,
            bool faceDown,
            int x,
            int y)
        {
            if (_pieceCount >= MaxPieces || !OnBoard(x, y))
                return false;
            if (controller != JieqiCamp.Red && controller != JieqiCamp.Black)
                return false;
            if (_board[Index(x, y)] >= 0)
                return false;
            if (role == JieqiRole.King)
            {
                faceDown = false;
                slotRole = JieqiRole.King;
                printed = controller;
            }

            var id = _pieceCount++;
            _pieces[id] = new JieqiPiece
            {
                Alive = true,
                FaceDown = faceDown,
                Controller = controller,
                Printed = printed,
                Role = role,
                SlotRole = slotRole,
                X = x,
                Y = y,
                CapturedBy = JieqiCamp.None
            };
            _board[Index(x, y)] = id;
            return true;
        }

        public bool TryMove(int fromX, int fromY, int toX, int toY, out JieqiStep step)
        {
            step = default;
            if (_outcome != JieqiOutcome.Playing)
            {
                step.Message = "对局已经结束";
                step.Outcome = _outcome;
                return false;
            }

            if (!OnBoard(fromX, fromY) || !OnBoard(toX, toY))
            {
                step.Message = "位置不在棋盘上";
                return false;
            }

            var id = _board[Index(fromX, fromY)];
            if (id < 0)
            {
                step.Message = "这里没有棋子";
                return false;
            }

            if (_pieces[id].Controller != _side)
            {
                step.Message = "还没轮到这方";
                return false;
            }

            CollectMoves(_moveBuf);
            if (_moveBuf.Count == 0)
            {
                _outcome = _side == JieqiCamp.Red ? JieqiOutcome.BlackWin : JieqiOutcome.RedWin;
                step.Outcome = _outcome;
                step.Message = "无子可走";
                return false;
            }

            var found = false;
            var move = default(JieqiMove);
            for (var i = 0; i < _moveBuf.Count; i++)
            {
                var item = _moveBuf[i];
                if (item.FromX != fromX || item.FromY != fromY || item.ToX != toX || item.ToY != toY)
                    continue;
                move = item;
                found = true;
                break;
            }

            if (!found)
            {
                step.Message = "这一步不能走";
                return false;
            }

            var before = _pieces[move.Mover];
            var hadCapture = move.Captured >= 0;
            var victim = hadCapture ? _pieces[move.Captured] : default;
            Make(move);
            RefreshTerminal();

            step.Ok = true;
            step.Move = move;
            step.Flipped = before.FaceDown;
            step.RevealedCamp = before.Printed;
            step.RevealedRole = before.Role;
            step.MoverRole = before.FaceDown ? before.SlotRole : before.Role;
            step.MoverRookSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Rook;
            step.MoverHorseSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Horse;
            step.MoverCannonSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Cannon;
            step.MoverElephantSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Elephant;
            step.MoverAdvisorSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Advisor;
            step.MoverPawnSkill = !before.FaceDown && before.HasSkill && before.SkillRole == JieqiRole.Pawn;
            step.Captured = hadCapture;
            if (hadCapture)
            {
                step.CapturedWasHidden = victim.FaceDown;
                step.CapturedCamp = victim.Printed;
                step.CapturedRole = victim.Role;
                step.CapturedController = victim.Controller;
            }

            step.Outcome = _outcome;
            return true;
        }

        /// <summary>当前行棋方是否正被将军。点了走不了的子时用来决定要不要提示应将。</summary>
        public bool InCheck()
        {
            return _outcome == JieqiOutcome.Playing && KingInCheck(_side);
        }

        /// <summary>刚走完的一步是否将军。将帅被吃掉不算，那一步只算吃子。</summary>
        public bool DefenderInCheck()
        {
            var king = FindKing(_side);
            if (!king.Alive)
                return false;
            return KingInCheck(_side);
        }

        public void CollectMoves(List<JieqiMove> moves)
        {
            moves.Clear();
            if (_outcome != JieqiOutcome.Playing)
                return;

            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                if (!piece.Alive || piece.Controller != _side)
                    continue;
                AddPieceMoves(piece, i, moves);
            }

            var write = 0;
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (OpponentCanTakeKing(move))
                    continue;
                moves[write++] = move;
            }

            if (write < moves.Count)
                moves.RemoveRange(write, moves.Count - write);
        }

        public bool TryView(int x, int y, out JieqiViewSquare view, bool revealHidden = false)
        {
            view = default;
            if (!OnBoard(x, y))
                return false;
            var id = _board[Index(x, y)];
            if (id < 0)
                return true;

            var piece = _pieces[id];
            view.Occupied = true;
            view.PieceId = id;
            view.FaceDown = piece.FaceDown;
            view.Controller = piece.Controller;
            // 对局中暗子不带底牌。终局演出才把真身交给界面，FaceDown 仍为真，方便涂灰。
            if (piece.FaceDown)
            {
                if (revealHidden && _outcome != JieqiOutcome.Playing)
                {
                    view.Printed = piece.Printed;
                    view.Role = piece.Role;
                }
                else
                {
                    view.Printed = JieqiCamp.None;
                    view.Role = piece.SlotRole;
                }
            }
            else
            {
                view.Printed = piece.Printed;
                view.Role = piece.Role;
                if (piece.HasSkill)
                {
                    view.HasSkill = true;
                    view.SkillRole = piece.SkillRole;
                    view.SkillPrinted = piece.SkillPrinted;
                }
            }

            return true;
        }

        public void CopyCaptured(List<JieqiCapturedView> dest)
        {
            dest.Clear();
            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                if (piece.Alive)
                    continue;
                dest.Add(new JieqiCapturedView
                {
                    PieceId = i,
                    CapturedBy = piece.CapturedBy,
                    Controller = piece.Controller,
                    Printed = piece.Printed,
                    Role = piece.Role
                });
            }
        }

        public int CopySave(JieqiPieceState[] dest)
        {
            if (dest == null || dest.Length < _pieceCount)
                return 0;
            for (var i = 0; i < _pieceCount; i++)
                dest[i] = ToState(_pieces[i]);
            return _pieceCount;
        }

        public void Restore(JieqiPieceState[] src, int count, JieqiCamp side, JieqiOutcome outcome, bool standard, bool skillRules = false)
        {
            Clear();
            if (src == null || count < 0)
                return;
            if (count > src.Length)
                count = src.Length;
            if (count > MaxPieces)
                count = MaxPieces;

            _pieceCount = count;
            _side = side == JieqiCamp.Black ? JieqiCamp.Black : JieqiCamp.Red;
            _outcome = outcome;
            _standard = standard;
            _skillRules = skillRules;
            for (var i = 0; i < count; i++)
            {
                var state = src[i];
                _pieces[i] = FromState(state);
                if (!state.Alive || !OnBoard(state.X, state.Y))
                    continue;
                _board[Index(state.X, state.Y)] = i;
            }
        }

        /// <summary>记下当前局面，供编辑里的恢复使用。重新开局会丢掉这份记录。</summary>
        public void EditorCaptureOrigin()
        {
            var pieces = new JieqiPieceState[MaxPieces];
            _origin = new EditorMark
            {
                Pieces = pieces,
                Count = CopySave(pieces),
                Side = _side,
                Standard = _standard,
                Skill = _skillRules
            };
            _hasOrigin = true;
        }

        /// <summary>回到记下的初始局面，并清掉编辑撤销。不判胜负。</summary>
        public bool EditorRestoreOrigin()
        {
            if (!_hasOrigin || _origin.Pieces == null)
                return false;
            var mark = _origin;
            _editorMarks.Clear();
            Restore(mark.Pieces, mark.Count, mark.Side, JieqiOutcome.Playing, mark.Standard, mark.Skill);
            return true;
        }

        /// <summary>把还扣着的子全部翻开。没有暗子时不记撤销。</summary>
        public bool EditorRevealAll()
        {
            var any = false;
            for (var i = 0; i < _pieceCount; i++)
            {
                if (_pieces[i].Alive && _pieces[i].FaceDown)
                {
                    any = true;
                    break;
                }
            }

            if (!any)
                return false;

            PushEditor();
            for (var i = 0; i < _pieceCount; i++)
            {
                if (!_pieces[i].Alive || !_pieces[i].FaceDown)
                    continue;
                var piece = _pieces[i];
                piece.FaceDown = false;
                if (piece.Printed == JieqiCamp.Red || piece.Printed == JieqiCamp.Black)
                    piece.Controller = piece.Printed;
                _pieces[i] = piece;
            }

            return true;
        }

        /// <summary>吃掉这一格的子，放到它自己颜色的身后。将、帅不能吃。</summary>
        public bool EditorCapture(int x, int y, out string message)
        {
            message = null;
            if (!OnBoard(x, y))
                return false;
            var id = _board[Index(x, y)];
            if (id < 0)
                return false;
            var piece = _pieces[id];
            if (!piece.Alive)
                return false;
            if (piece.Role == JieqiRole.King)
            {
                message = "将帅不能被吃";
                return false;
            }

            PushEditor();
            _board[Index(x, y)] = -1;
            var side = piece.Printed == JieqiCamp.Red || piece.Printed == JieqiCamp.Black
                ? piece.Printed
                : piece.Controller;
            piece.Alive = false;
            piece.FaceDown = false;
            piece.CapturedBy = side;
            piece.X = -1;
            piece.Y = -1;
            _pieces[id] = piece;
            return true;
        }

        /// <summary>
        /// 选中的子吃掉对方这一格，并走到那里。将、帅不能被吃。
        /// 将、帅也不能吃九宫以外的子。技能局吃子后获得被吃子原来的兵种，将、帅不获得。
        /// </summary>
        public bool EditorTake(int fromX, int fromY, int toX, int toY, out string message)
        {
            message = null;
            if (!OnBoard(fromX, fromY) || !OnBoard(toX, toY))
                return false;
            if (fromX == toX && fromY == toY)
                return false;
            var fromId = _board[Index(fromX, fromY)];
            var toId = _board[Index(toX, toY)];
            if (fromId < 0 || toId < 0 || fromId == toId)
                return false;

            var mover = _pieces[fromId];
            var victim = _pieces[toId];
            if (!mover.Alive || !victim.Alive)
                return false;
            if (mover.Controller == victim.Controller)
                return false;
            if (victim.Role == JieqiRole.King)
            {
                message = "将帅不能被吃";
                return false;
            }

            if (mover.Role == JieqiRole.King && !InPalace(mover.Controller, toX, toY))
            {
                message = "将帅不能吃城池外的棋子";
                return false;
            }

            PushEditor();
            _board[Index(fromX, fromY)] = -1;
            victim.Alive = false;
            victim.FaceDown = false;
            victim.CapturedBy = mover.Controller;
            victim.X = -1;
            victim.Y = -1;
            _pieces[toId] = victim;
            mover.X = toX;
            mover.Y = toY;
            if (_skillRules && mover.Role != JieqiRole.King)
                GrantSkill(ref mover, victim);
            _pieces[fromId] = mover;
            _board[Index(toX, toY)] = fromId;
            return true;
        }

        /// <summary>把子挪到空位。不翻开，也不走正常走法。将、帅不能出九宫。</summary>
        public bool EditorMove(int fromX, int fromY, int toX, int toY, out string message)
        {
            message = null;
            if (!OnBoard(fromX, fromY) || !OnBoard(toX, toY))
                return false;
            if (fromX == toX && fromY == toY)
                return false;
            var id = _board[Index(fromX, fromY)];
            if (id < 0)
                return false;
            if (_board[Index(toX, toY)] >= 0)
                return false;

            var piece = _pieces[id];
            if (piece.Role == JieqiRole.King && !InPalace(piece.Controller, toX, toY))
            {
                message = "将帅不能出九宫";
                return false;
            }

            PushEditor();
            _board[Index(fromX, fromY)] = -1;
            piece.X = toX;
            piece.Y = toY;
            _pieces[id] = piece;
            _board[Index(toX, toY)] = id;
            return true;
        }

        /// <summary>正常走了一步之后，编辑撤销不再跨过这步棋。</summary>
        public void EditorClearUndo()
        {
            _editorMarks.Clear();
        }

        public bool EditorUndo()
        {
            if (_editorMarks.Count == 0)
                return false;
            var mark = _editorMarks[_editorMarks.Count - 1];
            _editorMarks.RemoveAt(_editorMarks.Count - 1);
            Restore(mark.Pieces, mark.Count, mark.Side, JieqiOutcome.Playing, mark.Standard, mark.Skill);
            return true;
        }

        void PushEditor()
        {
            var pieces = new JieqiPieceState[MaxPieces];
            _editorMarks.Add(new EditorMark
            {
                Pieces = pieces,
                Count = CopySave(pieces),
                Side = _side,
                Standard = _standard,
                Skill = _skillRules
            });
        }

        public void RefreshTerminal()
        {
            if (_outcome != JieqiOutcome.Playing)
                return;
            if (!KingAlive(JieqiCamp.Red))
            {
                _outcome = JieqiOutcome.BlackWin;
                return;
            }

            if (!KingAlive(JieqiCamp.Black))
            {
                _outcome = JieqiOutcome.RedWin;
                return;
            }

            CollectMoves(_moveBuf);
            if (_moveBuf.Count == 0)
                _outcome = _side == JieqiCamp.Red ? JieqiOutcome.BlackWin : JieqiOutcome.RedWin;
        }

        /// <summary>
        /// 给电脑搜索用的副本。标准局把未翻开的子按剩余底牌重新分配，不保留真实落点。
        /// </summary>
        public JieqiEngine CloneBlind(Random rng)
        {
            var copy = Copy();
            copy.HideSecrets(rng);
            return copy;
        }

        public ulong Hash()
        {
            var hash = Zobrist(31, 0, _side == JieqiCamp.Black ? 2 : 1);
            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                hash ^= MixPiece(i, piece.Alive, piece.X, piece.Y, piece.FaceDown, (int)piece.Role,
                    (int)piece.SlotRole, (int)piece.Printed, (int)piece.Controller, (int)piece.CapturedBy,
                    piece.HasSkill, (int)piece.SkillRole, (int)piece.SkillPrinted, piece.SkillBoost);
            }

            return hash;
        }

        /// <summary>
        /// 局面哈希。按棋子编号、格子和明暗等字段各取一个固定随机数再异或，三次重复用它比对。
        /// </summary>
        public static ulong HashPieces(JieqiPieceState[] pieces, int count, JieqiCamp side)
        {
            var hash = Zobrist(31, 0, side == JieqiCamp.Black ? 2 : 1);
            if (pieces == null || count < 0)
                return hash;
            if (count > pieces.Length)
                count = pieces.Length;
            for (var i = 0; i < count; i++)
            {
                var piece = pieces[i];
                hash ^= MixPiece(i, piece.Alive, piece.X, piece.Y, piece.FaceDown, (int)piece.Role,
                    (int)piece.SlotRole, (int)piece.Printed, (int)piece.Controller, (int)piece.CapturedBy,
                    piece.HasSkill, (int)piece.SkillRole, (int)piece.SkillPrinted, piece.SkillBoost);
            }

            return hash;
        }

        static ulong MixPiece(
            int index, bool alive, int x, int y, bool faceDown, int role, int slot, int printed,
            int controller, int capturedBy, bool hasSkill, int skillRole, int skillPrinted, bool skillBoost)
        {
            var hash = Zobrist(index, 1, alive ? 1 : 0);
            hash ^= Zobrist(index, 2, x + 3);
            hash ^= Zobrist(index, 3, y + 3);
            hash ^= Zobrist(index, 4, faceDown ? 1 : 0);
            hash ^= Zobrist(index, 5, role);
            hash ^= Zobrist(index, 6, slot);
            hash ^= Zobrist(index, 7, printed);
            hash ^= Zobrist(index, 8, controller);
            hash ^= Zobrist(index, 9, capturedBy);
            hash ^= Zobrist(index, 10, hasSkill ? 1 : 0);
            hash ^= Zobrist(index, 11, skillRole);
            hash ^= Zobrist(index, 12, skillPrinted);
            hash ^= Zobrist(index, 13, skillBoost ? 1 : 0);
            return hash;
        }

        static ulong Zobrist(int piece, int field, int value)
        {
            ulong x = (ulong)(piece + 1) * 0x9E3779B97F4A7C15UL;
            x ^= (ulong)(field + 3) * 0xBF58476D1CE4E5B9UL;
            x ^= (ulong)(value + 17) * 0x94D049BB133111EBUL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }

        internal JieqiPiece[] Pieces => _pieces;

        /// <summary>
        /// 只改走子和被吃子的位置，看对方下一步能不能吃到己方将帅。不结算胜负，也不发技能。
        /// 暗子翻开后若改归对方，不拿这枚刚翻开的子来判自己送将。
        /// </summary>
        bool OpponentCanTakeKing(JieqiMove move)
        {
            var saved = _pieces[move.Mover];
            var had = move.Captured >= 0;
            JieqiPiece captured = default;
            if (had)
                captured = _pieces[move.Captured];

            _board[Index(saved.X, saved.Y)] = -1;
            if (had)
            {
                var victim = captured;
                victim.Alive = false;
                victim.X = -1;
                victim.Y = -1;
                _pieces[move.Captured] = victim;
            }

            var mover = saved;
            mover.X = move.ToX;
            mover.Y = move.ToY;
            var flippedAway = false;
            if (mover.FaceDown)
            {
                mover.FaceDown = false;
                if (mover.Printed == JieqiCamp.Red || mover.Printed == JieqiCamp.Black)
                {
                    if (mover.Controller != mover.Printed)
                        flippedAway = true;
                    mover.Controller = mover.Printed;
                }
            }

            _pieces[move.Mover] = mover;
            _board[Index(move.ToX, move.ToY)] = move.Mover;
            var ignore = flippedAway ? move.Mover : -1;
            var leaves = KingInCheck(_side, ignore);

            _pieces[move.Mover] = saved;
            _board[Index(saved.X, saved.Y)] = move.Mover;
            _board[Index(move.ToX, move.ToY)] = had ? move.Captured : -1;
            if (had)
                _pieces[move.Captured] = captured;
            return leaves;
        }

        internal Undo Make(JieqiMove move)
        {
            var undo = new Undo
            {
                MoverId = move.Mover,
                MoverBefore = _pieces[move.Mover],
                CapturedId = move.Captured,
                HadCapture = move.Captured >= 0,
                SideBefore = _side,
                OutcomeBefore = _outcome,
                ToX = move.ToX,
                ToY = move.ToY
            };

            var mover = _pieces[move.Mover];
            _board[Index(mover.X, mover.Y)] = -1;
            if (undo.HadCapture)
            {
                undo.CapturedBefore = _pieces[move.Captured];
                var victim = _pieces[move.Captured];
                victim.Alive = false;
                victim.FaceDown = false;
                victim.CapturedBy = mover.Controller;
                victim.X = -1;
                victim.Y = -1;
                _pieces[move.Captured] = victim;
                if (victim.Role == JieqiRole.King)
                {
                    _outcome = mover.Controller == JieqiCamp.Red
                        ? JieqiOutcome.RedWin
                        : JieqiOutcome.BlackWin;
                }
            }

            mover.X = move.ToX;
            mover.Y = move.ToY;
            if (mover.FaceDown)
            {
                mover.FaceDown = false;
                if (mover.Printed == JieqiCamp.Red || mover.Printed == JieqiCamp.Black)
                    mover.Controller = mover.Printed;
            }

            if (_skillRules && undo.HadCapture && undo.CapturedBefore.Role != JieqiRole.King && mover.Role != JieqiRole.King)
                GrantSkill(ref mover, undo.CapturedBefore);

            _pieces[move.Mover] = mover;
            _board[Index(move.ToX, move.ToY)] = move.Mover;
            _side = Opposite(_side);
            return undo;
        }

        internal void Unmake(Undo undo)
        {
            var before = undo.MoverBefore;
            _pieces[undo.MoverId] = before;
            _board[Index(before.X, before.Y)] = undo.MoverId;
            _board[Index(undo.ToX, undo.ToY)] = undo.HadCapture ? undo.CapturedId : -1;
            if (undo.HadCapture)
                _pieces[undo.CapturedId] = undo.CapturedBefore;
            _side = undo.SideBefore;
            _outcome = undo.OutcomeBefore;
        }

        internal bool KingInCheck(JieqiCamp camp, int ignoreId = -1)
        {
            var king = FindKing(camp);
            if (!king.Alive)
                return true;

            var enemy = Opposite(camp);
            for (var i = 0; i < _pieceCount; i++)
            {
                if (i == ignoreId)
                    continue;
                var piece = _pieces[i];
                if (!piece.Alive || piece.Controller != enemy)
                    continue;
                if (Attacks(piece, king.X, king.Y))
                    return true;
            }

            return KingsFace(camp);
        }

        internal void SetOutcome(JieqiOutcome outcome)
        {
            _outcome = outcome;
        }

        void HideSecrets(Random rng)
        {
            if (rng == null)
                rng = new Random(1);
            if (_standard && TryAssignFromBag(rng))
                return;
            BlankHidden();
        }

        bool TryAssignFromBag(Random rng)
        {
            var n = 0;
            n = AddCampBag(n, JieqiCamp.Red);
            n = AddCampBag(n, JieqiCamp.Black);
            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                if (piece.Role == JieqiRole.King || piece.FaceDown)
                    continue;
                if (!TakeBag(ref n, piece.Printed, piece.Role))
                    return false;
            }

            var hidden = 0;
            for (var i = 0; i < _pieceCount; i++)
            {
                if (!_pieces[i].FaceDown)
                    continue;
                _hiddenIds[hidden++] = i;
            }

            if (hidden != n)
                return false;

            for (var i = n - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                var tmp = _bag[i];
                _bag[i] = _bag[j];
                _bag[j] = tmp;
            }

            for (var i = 0; i < hidden; i++)
            {
                var id = _hiddenIds[i];
                var piece = _pieces[id];
                Decode(_bag[i], out piece.Printed, out piece.Role);
                _pieces[id] = piece;
            }

            return true;
        }

        void BlankHidden()
        {
            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                if (!piece.FaceDown)
                    continue;
                piece.Printed = piece.Controller;
                piece.Role = piece.SlotRole;
                _pieces[i] = piece;
            }
        }

        JieqiEngine Copy()
        {
            var copy = new JieqiEngine();
            copy._pieceCount = _pieceCount;
            copy._side = _side;
            copy._outcome = _outcome;
            copy._standard = _standard;
            copy._skillRules = _skillRules;
            Array.Copy(_pieces, copy._pieces, MaxPieces);
            Array.Copy(_board, copy._board, _board.Length);
            return copy;
        }

        void Clear()
        {
            Array.Clear(_pieces, 0, _pieces.Length);
            for (var i = 0; i < _board.Length; i++)
                _board[i] = -1;
            _pieceCount = 0;
            _outcome = JieqiOutcome.Playing;
            _standard = false;
            _skillRules = false;
            _addingSkill = false;
            _side = JieqiCamp.Red;
        }

        int PlaceRank(int cursor, int y, JieqiCamp controller, JieqiRole[] roles)
        {
            for (var x = 0; x < roles.Length; x++)
            {
                if (x == 4)
                    continue;
                cursor = PlaceDark(cursor, controller, roles[x], x, y);
            }

            return cursor;
        }

        int PlaceCannons(int cursor, int y, JieqiCamp controller)
        {
            cursor = PlaceDark(cursor, controller, JieqiRole.Cannon, 1, y);
            return PlaceDark(cursor, controller, JieqiRole.Cannon, 7, y);
        }

        int PlacePawns(int cursor, int y, JieqiCamp controller)
        {
            for (var i = 0; i < 5; i++)
                cursor = PlaceDark(cursor, controller, JieqiRole.Pawn, i * 2, y);
            return cursor;
        }

        void PlaceOpen(JieqiCamp camp, JieqiRole role, int x, int y)
        {
            AddPiece(camp, camp, role, role, false, x, y);
        }

        int PlaceDark(int cursor, JieqiCamp controller, JieqiRole slot, int x, int y)
        {
            Decode(_bag[cursor], out var printed, out var role);
            AddPiece(controller, printed, role, slot, true, x, y);
            return cursor + 1;
        }

        int AddCampBag(int n, JieqiCamp camp)
        {
            n = PushBag(n, camp, JieqiRole.Rook, 2);
            n = PushBag(n, camp, JieqiRole.Horse, 2);
            n = PushBag(n, camp, JieqiRole.Elephant, 2);
            n = PushBag(n, camp, JieqiRole.Advisor, 2);
            n = PushBag(n, camp, JieqiRole.Cannon, 2);
            return PushBag(n, camp, JieqiRole.Pawn, 5);
        }

        int PushBag(int n, JieqiCamp camp, JieqiRole role, int count)
        {
            var code = Code(camp, role);
            for (var i = 0; i < count; i++)
                _bag[n++] = code;
            return n;
        }

        bool TakeBag(ref int n, JieqiCamp camp, JieqiRole role)
        {
            var code = Code(camp, role);
            for (var i = 0; i < n; i++)
            {
                if (_bag[i] != code)
                    continue;
                _bag[i] = _bag[n - 1];
                n--;
                return true;
            }

            return false;
        }

        void AddPieceMoves(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            var role = piece.FaceDown ? piece.SlotRole : piece.Role;
            switch (role)
            {
                case JieqiRole.King:
                    AddKing(piece, id, moves);
                    break;
                case JieqiRole.Advisor:
                    AddAdvisor(piece, id, moves);
                    break;
                case JieqiRole.Elephant:
                    AddElephant(piece, id, moves);
                    break;
                case JieqiRole.Horse:
                    AddHorse(piece, id, moves);
                    break;
                case JieqiRole.Rook:
                    AddRook(piece, id, moves);
                    break;
                case JieqiRole.Cannon:
                    AddCannon(piece, id, moves);
                    break;
                case JieqiRole.Pawn:
                    AddPawn(piece, id, moves);
                    break;
            }

            if (_skillRules && piece.HasSkill && role != JieqiRole.King)
                AddSkillMoves(piece, id, moves);
        }

        void AddSkillMoves(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            _addingSkill = true;
            if (piece.SkillBoost)
            {
                switch (piece.Role)
                {
                    case JieqiRole.Rook:
                        AddScreenCaptures(piece, id, 1, moves);
                        break;
                    case JieqiRole.Horse:
                        AddHorse(piece, id, moves, true);
                        break;
                    case JieqiRole.Cannon:
                        AddScreenCaptures(piece, id, 2, moves);
                        break;
                    case JieqiRole.Pawn:
                        TryAdd(piece, id, piece.X, piece.Y + (piece.Controller == JieqiCamp.Red ? 1 : -1), moves, false);
                        break;
                    case JieqiRole.Advisor:
                        AddAdvisorLeap(piece, id, moves);
                        break;
                    case JieqiRole.Elephant:
                        AddElephant(piece, id, moves, true);
                        break;
                }
            }
            else
            {
                switch (piece.SkillRole)
                {
                    case JieqiRole.Rook:
                        AddRook(piece, id, moves);
                        break;
                    case JieqiRole.Horse:
                        AddHorse(piece, id, moves, false);
                        break;
                    case JieqiRole.Cannon:
                        AddScreenCaptures(piece, id, 1, moves);
                        break;
                    case JieqiRole.Pawn:
                        TryCaptureOnly(piece, id, piece.X, piece.Y + 1, moves);
                        TryCaptureOnly(piece, id, piece.X, piece.Y - 1, moves);
                        TryCaptureOnly(piece, id, piece.X - 1, piece.Y, moves);
                        TryCaptureOnly(piece, id, piece.X + 1, piece.Y, moves);
                        break;
                    case JieqiRole.Advisor:
                        TryAdd(piece, id, piece.X + 1, piece.Y + 1, moves, false);
                        TryAdd(piece, id, piece.X + 1, piece.Y - 1, moves, false);
                        TryAdd(piece, id, piece.X - 1, piece.Y + 1, moves, false);
                        TryAdd(piece, id, piece.X - 1, piece.Y - 1, moves, false);
                        break;
                    case JieqiRole.Elephant:
                        TryElephantSkill(piece, id, 2, 2, moves);
                        TryElephantSkill(piece, id, 2, -2, moves);
                        TryElephantSkill(piece, id, -2, 2, moves);
                        TryElephantSkill(piece, id, -2, -2, moves);
                        break;
                }
            }

            _addingSkill = false;
        }

        void AddKing(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            TryAdd(piece, id, piece.X + 1, piece.Y, moves, true);
            TryAdd(piece, id, piece.X - 1, piece.Y, moves, true);
            TryAdd(piece, id, piece.X, piece.Y + 1, moves, true);
            TryAdd(piece, id, piece.X, piece.Y - 1, moves, true);
        }

        void AddAdvisor(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            var palace = _skillRules;
            TryAdd(piece, id, piece.X + 1, piece.Y + 1, moves, palace);
            TryAdd(piece, id, piece.X + 1, piece.Y - 1, moves, palace);
            TryAdd(piece, id, piece.X - 1, piece.Y + 1, moves, palace);
            TryAdd(piece, id, piece.X - 1, piece.Y - 1, moves, palace);
        }

        void AddAdvisorLeap(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            TryAdvisorLeap(piece, id, 1, 1, moves);
            TryAdvisorLeap(piece, id, 1, -1, moves);
            TryAdvisorLeap(piece, id, -1, 1, moves);
            TryAdvisorLeap(piece, id, -1, -1, moves);
        }

        void TryAdvisorLeap(JieqiPiece piece, int id, int sx, int sy, List<JieqiMove> moves)
        {
            var mx = piece.X + sx;
            var my = piece.Y + sy;
            var tx = piece.X + sx * 2;
            var ty = piece.Y + sy * 2;
            if (!OnBoard(mx, my) || !OnBoard(tx, ty))
                return;
            if (_board[Index(mx, my)] >= 0)
                return;
            TryAdd(piece, id, tx, ty, moves, false);
        }

        void AddElephant(JieqiPiece piece, int id, List<JieqiMove> moves, bool onlyBlockedEye = false)
        {
            TryElephant(piece, id, 2, 2, moves, onlyBlockedEye);
            TryElephant(piece, id, 2, -2, moves, onlyBlockedEye);
            TryElephant(piece, id, -2, 2, moves, onlyBlockedEye);
            TryElephant(piece, id, -2, -2, moves, onlyBlockedEye);
        }

        void TryElephant(JieqiPiece piece, int id, int dx, int dy, List<JieqiMove> moves, bool onlyBlockedEye = false)
        {
            var tx = piece.X + dx;
            var ty = piece.Y + dy;
            if (!OnBoard(tx, ty))
                return;
            if ((piece.FaceDown || _skillRules) && !OnOwnHalf(piece.Controller, ty))
                return;
            var blocked = _board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] >= 0;
            if (onlyBlockedEye)
            {
                if (!blocked)
                    return;
            }
            else if (blocked)
            {
                return;
            }

            TryAdd(piece, id, tx, ty, moves, false);
        }

        void TryElephantSkill(JieqiPiece piece, int id, int dx, int dy, List<JieqiMove> moves)
        {
            var tx = piece.X + dx;
            var ty = piece.Y + dy;
            if (!OnBoard(tx, ty))
                return;
            if (_board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] >= 0)
                return;
            TryAdd(piece, id, tx, ty, moves, false);
        }

        void AddHorse(JieqiPiece piece, int id, List<JieqiMove> moves, bool onlyBlockedLeg = false)
        {
            TryHorse(piece, id, 1, 2, 0, 1, moves, onlyBlockedLeg);
            TryHorse(piece, id, 1, -2, 0, -1, moves, onlyBlockedLeg);
            TryHorse(piece, id, -1, 2, 0, 1, moves, onlyBlockedLeg);
            TryHorse(piece, id, -1, -2, 0, -1, moves, onlyBlockedLeg);
            TryHorse(piece, id, 2, 1, 1, 0, moves, onlyBlockedLeg);
            TryHorse(piece, id, 2, -1, 1, 0, moves, onlyBlockedLeg);
            TryHorse(piece, id, -2, 1, -1, 0, moves, onlyBlockedLeg);
            TryHorse(piece, id, -2, -1, -1, 0, moves, onlyBlockedLeg);
        }

        void TryHorse(JieqiPiece piece, int id, int dx, int dy, int legX, int legY, List<JieqiMove> moves, bool onlyBlockedLeg = false)
        {
            var lx = piece.X + legX;
            var ly = piece.Y + legY;
            var blocked = !OnBoard(lx, ly) || _board[Index(lx, ly)] >= 0;
            if (onlyBlockedLeg)
            {
                if (!blocked)
                    return;
            }
            else if (blocked)
            {
                return;
            }

            TryAdd(piece, id, piece.X + dx, piece.Y + dy, moves, false);
        }

        void AddRook(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            AddRookDir(piece, id, 1, 0, moves);
            AddRookDir(piece, id, -1, 0, moves);
            AddRookDir(piece, id, 0, 1, moves);
            AddRookDir(piece, id, 0, -1, moves);
        }

        void AddRookDir(JieqiPiece piece, int id, int dx, int dy, List<JieqiMove> moves)
        {
            var x = piece.X + dx;
            var y = piece.Y + dy;
            while (OnBoard(x, y))
            {
                var occ = _board[Index(x, y)];
                if (occ < 0)
                {
                    AddMove(piece, id, x, y, -1, moves);
                }
                else
                {
                    if (CanCapture(piece, occ))
                        AddMove(piece, id, x, y, occ, moves);
                    break;
                }

                x += dx;
                y += dy;
            }
        }

        void AddCannon(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            AddCannonDir(piece, id, 1, 0, moves);
            AddCannonDir(piece, id, -1, 0, moves);
            AddCannonDir(piece, id, 0, 1, moves);
            AddCannonDir(piece, id, 0, -1, moves);
        }

        void AddCannonDir(JieqiPiece piece, int id, int dx, int dy, List<JieqiMove> moves)
        {
            var jumped = false;
            var x = piece.X + dx;
            var y = piece.Y + dy;
            while (OnBoard(x, y))
            {
                var occ = _board[Index(x, y)];
                if (occ < 0)
                {
                    if (!jumped)
                        AddMove(piece, id, x, y, -1, moves);
                }
                else if (!jumped)
                {
                    jumped = true;
                }
                else
                {
                    if (CanCapture(piece, occ))
                        AddMove(piece, id, x, y, occ, moves);
                    break;
                }

                x += dx;
                y += dy;
            }
        }

        void AddScreenCaptures(JieqiPiece piece, int id, int screensNeeded, List<JieqiMove> moves)
        {
            AddScreenDir(piece, id, 1, 0, screensNeeded, moves);
            AddScreenDir(piece, id, -1, 0, screensNeeded, moves);
            AddScreenDir(piece, id, 0, 1, screensNeeded, moves);
            AddScreenDir(piece, id, 0, -1, screensNeeded, moves);
        }

        void AddScreenDir(JieqiPiece piece, int id, int dx, int dy, int screensNeeded, List<JieqiMove> moves)
        {
            var screens = 0;
            var x = piece.X + dx;
            var y = piece.Y + dy;
            while (OnBoard(x, y))
            {
                var occ = _board[Index(x, y)];
                if (occ < 0)
                {
                    x += dx;
                    y += dy;
                    continue;
                }

                if (screens == screensNeeded)
                {
                    if (CanCapture(piece, occ))
                        AddMove(piece, id, x, y, occ, moves);
                    break;
                }

                screens++;
                x += dx;
                y += dy;
            }
        }

        void AddPawn(JieqiPiece piece, int id, List<JieqiMove> moves)
        {
            var forward = piece.Controller == JieqiCamp.Red ? -1 : 1;
            TryAdd(piece, id, piece.X, piece.Y + forward, moves, false);
            if (CrossedRiver(piece))
            {
                TryAdd(piece, id, piece.X - 1, piece.Y, moves, false);
                TryAdd(piece, id, piece.X + 1, piece.Y, moves, false);
            }
        }

        void TryAdd(JieqiPiece piece, int id, int x, int y, List<JieqiMove> moves, bool palace)
        {
            if (!OnBoard(x, y))
                return;
            if (palace && !InPalace(piece.Controller, x, y))
                return;
            if (!palace && piece.FaceDown && piece.SlotRole == JieqiRole.Advisor && !InPalace(piece.Controller, x, y))
                return;

            var occ = _board[Index(x, y)];
            if (occ >= 0 && !CanCapture(piece, occ))
                return;
            AddMove(piece, id, x, y, occ, moves);
        }

        void TryCaptureOnly(JieqiPiece piece, int id, int x, int y, List<JieqiMove> moves)
        {
            if (!OnBoard(x, y))
                return;
            var occ = _board[Index(x, y)];
            if (occ < 0 || !CanCapture(piece, occ))
                return;
            AddMove(piece, id, x, y, occ, moves);
        }

        /// <summary>对方的子都能吃。没翻开的己方子也能吃，翻开后的不能吃。</summary>
        bool CanCapture(JieqiPiece mover, int occ)
        {
            var victim = _pieces[occ];
            if (victim.Controller != mover.Controller)
                return true;
            return victim.FaceDown;
        }

        void AddMove(JieqiPiece piece, int id, int x, int y, int captured, List<JieqiMove> moves)
        {
            if (_addingSkill)
            {
                for (var i = 0; i < moves.Count; i++)
                {
                    var existing = moves[i];
                    if (existing.Mover == id && existing.ToX == x && existing.ToY == y)
                        return;
                }
            }

            moves.Add(new JieqiMove
            {
                FromX = piece.X,
                FromY = piece.Y,
                ToX = x,
                ToY = y,
                Mover = id,
                Captured = captured
            });
        }

        bool Attacks(JieqiPiece piece, int tx, int ty)
        {
            if (NativeAttacks(piece, tx, ty))
                return true;
            if (!_skillRules || !piece.HasSkill || piece.Role == JieqiRole.King)
                return false;
            return SkillAttacks(piece, tx, ty);
        }

        bool NativeAttacks(JieqiPiece piece, int tx, int ty)
        {
            if (!OnBoard(tx, ty))
                return false;
            var role = piece.FaceDown ? piece.SlotRole : piece.Role;
            var dx = tx - piece.X;
            var dy = ty - piece.Y;
            switch (role)
            {
                case JieqiRole.King:
                    if (Abs(dx) + Abs(dy) != 1)
                        return false;
                    return InPalace(piece.Controller, tx, ty);
                case JieqiRole.Advisor:
                    if (Abs(dx) != 1 || Abs(dy) != 1)
                        return false;
                    if ((piece.FaceDown || _skillRules) && !InPalace(piece.Controller, tx, ty))
                        return false;
                    return true;
                case JieqiRole.Elephant:
                    if (Abs(dx) != 2 || Abs(dy) != 2)
                        return false;
                    if ((piece.FaceDown || _skillRules) && !OnOwnHalf(piece.Controller, ty))
                        return false;
                    return _board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] < 0;
                case JieqiRole.Horse:
                    return HorseAttacks(piece.X, piece.Y, tx, ty);
                case JieqiRole.Rook:
                    return SlideAttacks(piece.X, piece.Y, tx, ty, false);
                case JieqiRole.Cannon:
                    return SlideAttacks(piece.X, piece.Y, tx, ty, true);
                case JieqiRole.Pawn:
                    return PawnStep(piece.Controller, dx, dy, CrossedRiver(piece));
                default:
                    return false;
            }
        }

        bool SkillAttacks(JieqiPiece piece, int tx, int ty)
        {
            if (!OnBoard(tx, ty))
                return false;
            var dx = tx - piece.X;
            var dy = ty - piece.Y;
            if (piece.SkillBoost)
            {
                switch (piece.Role)
                {
                    case JieqiRole.Rook:
                        return ScreensBetween(piece.X, piece.Y, tx, ty) == 1;
                    case JieqiRole.Horse:
                        return HorseAttacksIgnoringLeg(piece.X, piece.Y, tx, ty);
                    case JieqiRole.Cannon:
                        return ScreensBetween(piece.X, piece.Y, tx, ty) == 2;
                    case JieqiRole.Pawn:
                        var back = piece.Controller == JieqiCamp.Red ? 1 : -1;
                        return dx == 0 && dy == back;
                    case JieqiRole.Advisor:
                        return Abs(dx) == 2 && Abs(dy) == 2 &&
                               _board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] < 0;
                    case JieqiRole.Elephant:
                        if (Abs(dx) != 2 || Abs(dy) != 2 || !OnOwnHalf(piece.Controller, ty))
                            return false;
                        return _board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] >= 0;
                    default:
                        return false;
                }
            }

            switch (piece.SkillRole)
            {
                case JieqiRole.Rook:
                    return SlideAttacks(piece.X, piece.Y, tx, ty, false);
                case JieqiRole.Horse:
                    return HorseAttacks(piece.X, piece.Y, tx, ty);
                case JieqiRole.Cannon:
                    return ScreensBetween(piece.X, piece.Y, tx, ty) == 1;
                case JieqiRole.Pawn:
                    return Abs(dx) + Abs(dy) == 1;
                case JieqiRole.Advisor:
                    return Abs(dx) == 1 && Abs(dy) == 1;
                case JieqiRole.Elephant:
                    if (Abs(dx) != 2 || Abs(dy) != 2)
                        return false;
                    return _board[Index(piece.X + Sign(dx), piece.Y + Sign(dy))] < 0;
                default:
                    return false;
            }
        }

        bool HorseAttacks(int x, int y, int tx, int ty)
        {
            if (!HorseGeometry(x, y, tx, ty, out var legX, out var legY))
                return false;
            return OnBoard(legX, legY) && _board[Index(legX, legY)] < 0;
        }

        bool HorseAttacksIgnoringLeg(int x, int y, int tx, int ty)
        {
            if (!HorseGeometry(x, y, tx, ty, out var legX, out var legY))
                return false;
            return OnBoard(legX, legY) && _board[Index(legX, legY)] >= 0;
        }

        static bool HorseGeometry(int x, int y, int tx, int ty, out int legX, out int legY)
        {
            var dx = tx - x;
            var dy = ty - y;
            var adx = Abs(dx);
            var ady = Abs(dy);
            legX = x + (adx == 2 ? Sign(dx) : 0);
            legY = y + (ady == 2 ? Sign(dy) : 0);
            return (adx == 1 && ady == 2) || (adx == 2 && ady == 1);
        }

        bool SlideAttacks(int x, int y, int tx, int ty, bool cannon)
        {
            var screens = ScreensBetween(x, y, tx, ty);
            if (screens < 0)
                return false;
            return cannon ? screens == 1 : screens == 0;
        }

        int ScreensBetween(int x, int y, int tx, int ty)
        {
            if (x != tx && y != ty)
                return -1;
            var dx = Sign(tx - x);
            var dy = Sign(ty - y);
            if (dx == 0 && dy == 0)
                return -1;

            var screens = 0;
            var cx = x + dx;
            var cy = y + dy;
            while (cx != tx || cy != ty)
            {
                if (!OnBoard(cx, cy))
                    return -1;
                if (_board[Index(cx, cy)] >= 0)
                    screens++;
                cx += dx;
                cy += dy;
            }

            return screens;
        }

        static bool PawnStep(JieqiCamp controller, int dx, int dy, bool allowSide)
        {
            var forward = controller == JieqiCamp.Red ? -1 : 1;
            if (dx == 0 && dy == forward)
                return true;
            return allowSide && dy == 0 && (dx == 1 || dx == -1);
        }

        static bool CrossedRiver(JieqiPiece piece)
        {
            if (piece.Controller == JieqiCamp.Red)
                return piece.Y <= 4;
            if (piece.Controller == JieqiCamp.Black)
                return piece.Y >= 5;
            return false;
        }

        bool KingsFace(JieqiCamp camp)
        {
            var king = FindKing(camp);
            var other = FindKing(Opposite(camp));
            if (!king.Alive || !other.Alive || king.X != other.X)
                return false;
            return ClearFile(king.X, king.Y, other.Y);
        }

        bool ClearFile(int x, int y0, int y1)
        {
            if (y0 > y1)
            {
                var tmp = y0;
                y0 = y1;
                y1 = tmp;
            }

            for (var y = y0 + 1; y < y1; y++)
            {
                if (_board[Index(x, y)] >= 0)
                    return false;
            }

            return true;
        }

        JieqiPiece FindKing(JieqiCamp camp)
        {
            for (var i = 0; i < _pieceCount; i++)
            {
                var piece = _pieces[i];
                if (piece.Alive && piece.Controller == camp && piece.Role == JieqiRole.King)
                    return piece;
            }

            return default;
        }

        bool KingAlive(JieqiCamp camp)
        {
            return FindKing(camp).Alive;
        }

        static JieqiPieceState ToState(JieqiPiece piece)
        {
            return new JieqiPieceState
            {
                Alive = piece.Alive,
                FaceDown = piece.FaceDown,
                Controller = piece.Controller,
                Printed = piece.Printed,
                Role = piece.Role,
                SlotRole = piece.SlotRole,
                X = piece.X,
                Y = piece.Y,
                CapturedBy = piece.CapturedBy,
                HasSkill = piece.HasSkill,
                SkillRole = piece.SkillRole,
                SkillPrinted = piece.SkillPrinted,
                SkillBoost = piece.SkillBoost
            };
        }

        static JieqiPiece FromState(JieqiPieceState state)
        {
            return new JieqiPiece
            {
                Alive = state.Alive,
                FaceDown = state.FaceDown,
                Controller = state.Controller,
                Printed = state.Printed,
                Role = state.Role,
                SlotRole = state.SlotRole,
                X = state.X,
                Y = state.Y,
                CapturedBy = state.CapturedBy,
                HasSkill = state.HasSkill,
                SkillRole = state.SkillRole,
                SkillPrinted = state.SkillPrinted,
                SkillBoost = state.SkillBoost
            };
        }

        static void GrantSkill(ref JieqiPiece mover, JieqiPiece victim)
        {
            mover.HasSkill = true;
            mover.SkillRole = victim.Role;
            mover.SkillPrinted = victim.Printed;
            mover.SkillBoost = victim.Role == mover.Role;
        }

        static int Code(JieqiCamp camp, JieqiRole role)
        {
            return ((int)camp << 4) | (int)role;
        }

        static void Decode(int code, out JieqiCamp camp, out JieqiRole role)
        {
            camp = (JieqiCamp)(code >> 4);
            role = (JieqiRole)(code & 15);
        }

        static JieqiCamp Opposite(JieqiCamp camp)
        {
            return camp == JieqiCamp.Red ? JieqiCamp.Black : JieqiCamp.Red;
        }

        static bool OnBoard(int x, int y)
        {
            return (uint)x < Files && (uint)y < Ranks;
        }

        static bool InPalace(JieqiCamp camp, int x, int y)
        {
            if (x < 3 || x > 5)
                return false;
            if (camp == JieqiCamp.Red)
                return y >= 7 && y <= 9;
            if (camp == JieqiCamp.Black)
                return y >= 0 && y <= 2;
            return false;
        }

        static bool OnOwnHalf(JieqiCamp camp, int y)
        {
            if (camp == JieqiCamp.Red)
                return y >= 5;
            if (camp == JieqiCamp.Black)
                return y <= 4;
            return false;
        }

        static int Sign(int value)
        {
            if (value > 0)
                return 1;
            if (value < 0)
                return -1;
            return 0;
        }

        static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }

        static readonly JieqiRole[] BackRank =
        {
            JieqiRole.Rook,
            JieqiRole.Horse,
            JieqiRole.Elephant,
            JieqiRole.Advisor,
            JieqiRole.King,
            JieqiRole.Advisor,
            JieqiRole.Elephant,
            JieqiRole.Horse,
            JieqiRole.Rook
        };

        internal struct JieqiPiece
        {
            public bool Alive;
            public bool FaceDown;
            public JieqiCamp Controller;
            public JieqiCamp Printed;
            public JieqiRole Role;
            public JieqiRole SlotRole;
            public int X;
            public int Y;
            public JieqiCamp CapturedBy;
            public bool HasSkill;
            public JieqiRole SkillRole;
            public JieqiCamp SkillPrinted;
            public bool SkillBoost;
        }

        struct EditorMark
        {
            public JieqiPieceState[] Pieces;
            public int Count;
            public JieqiCamp Side;
            public bool Standard;
            public bool Skill;
        }

        internal struct Undo
        {
            public int MoverId;
            public JieqiPiece MoverBefore;
            public int CapturedId;
            public JieqiPiece CapturedBefore;
            public bool HadCapture;
            public JieqiCamp SideBefore;
            public JieqiOutcome OutcomeBefore;
            public int ToX;
            public int ToY;
        }
    }
}
