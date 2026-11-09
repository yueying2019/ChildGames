namespace SmallGames
{
    /// <summary>
    /// 新规揭棋残局。全部明子，红方先走。
    /// 前十关一步将死，后二十关要两步或三步。关卡序号从 <see cref="LevelIndexBase"/> 起，前面 0、1 仍是双人和人机。
    /// </summary>
    public static class JieqiEndgameCatalog
    {
        public const int LevelIndexBase = 2;

        public static int Count => Levels.Length;

        public static bool IsLevelIndex(int levelIndex)
        {
            var index = levelIndex - LevelIndexBase;
            return index >= 0 && index < Levels.Length;
        }

        public static int IndexFromLevel(int levelIndex)
        {
            return levelIndex - LevelIndexBase;
        }

        public static string Title(int index)
        {
            return Levels[index].Title;
        }

        public static string Hint(int index)
        {
            return Levels[index].Hint;
        }

        public static void Load(JieqiEngine engine, int index)
        {
            var level = Levels[index];
            engine.BeginCustom(level.Side);
            var pieces = level.Pieces;
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                engine.AddPiece(
                    piece.Controller,
                    piece.Printed,
                    piece.Role,
                    piece.SlotRole,
                    piece.FaceDown,
                    piece.X,
                    piece.Y);
            }
        }

        static readonly Level[] Levels =
        {
            // 象不能斜进一步吃子，车才能占住宫心。
            Level.Make("双车抢宫", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Rook, 0, 1),
                P(JieqiCamp.Red, JieqiRole.Rook, 8, 1)),

            // 炮先走到将门这一线，用己方兵做炮架。
            Level.Make("隔山打帅", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Pawn, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 8, 3)),

            // 明士可以出宫，斜到对方九宫旁将军。
            Level.Make("士出宫", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Advisor, 6, 2)),

            // 明象已在河沿，可以再向前踏到对方一侧。
            Level.Make("过河象", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Elephant, 0, 4)),

            // 兵还在己方河沿，只有横走才能让开车。
            Level.Make("未过河兵", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Pawn, 4, 5),
                P(JieqiCamp.Red, JieqiRole.Rook, 4, 7)),

            Level.Make("马踏九宫", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Horse, 0, 2)),

            // 兵横到将前，由士看着这个兵。
            Level.Make("横兵将军", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 5, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Pawn, 3, 1),
                P(JieqiCamp.Red, JieqiRole.Advisor, 5, 2)),

            Level.Make("兵护中车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Pawn, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 0, 1)),

            Level.Make("士护中车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Advisor, 5, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 8, 1)),

            Level.Make("马护中车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 2, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 8, 1)),

            // 马跳到将前，将只能上一步，车再横到同一线。
            Level.Make("马后横车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 8, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 1, 0)),

            Level.Make("左马后车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 0, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 7, 0)),

            // 将上一步后，炮用这匹马当炮架。
            Level.Make("马后炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 8, 2)),

            Level.Make("左马后炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 2)),

            // 士斜到将旁，将上一步，车再横锁。
            Level.Make("士角横车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Advisor, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 1, 2)),

            Level.Make("右士横车", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Advisor, 4, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 7, 2)),

            // 士先占到炮架上，炮再沉下来。
            Level.Make("士架沉炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Advisor, 2, 0),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 3)),

            Level.Make("高士架炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Advisor, 2, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 4)),

            // 前炮照底线，象让开后，后炮用前炮当架子。
            Level.Make("重炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Cannon, 1, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 6)),

            Level.Make("右重炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Cannon, 7, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 8, 2)),

            // 炮照底线，象走开，兵占到象位。将吃这个兵会和自己的帅对脸。
            Level.Make("底兵借帅", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 3),
                P(JieqiCamp.Red, JieqiRole.Pawn, 3, 1)),

            // 炮先沉底，马再补上当炮架。
            Level.Make("马填炮架", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Black, JieqiRole.Advisor, 4, 1),
                P(JieqiCamp.Red, JieqiRole.Horse, 2, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 1, 2)),

            // 马要先跳到能照将的位置，车最后才横过来。
            Level.Make("高马盘宫", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 1, 4),
                P(JieqiCamp.Red, JieqiRole.Rook, 8, 0)),

            // 马照将后，车要先横再向前。
            Level.Make("调车锁将", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 0, 2),
                P(JieqiCamp.Red, JieqiRole.Rook, 0, 3),
                P(JieqiCamp.Red, JieqiRole.Advisor, 4, 2)),

            // 马照将后，炮先横再沉，用马当架子。
            Level.Make("运炮将军", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 0, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 8, 3),
                P(JieqiCamp.Red, JieqiRole.Advisor, 6, 2)),

            // 一马先照，炮绕到另一匹马后面。
            Level.Make("双马叫炮", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 8, 2),
                P(JieqiCamp.Red, JieqiRole.Horse, 7, 3),
                P(JieqiCamp.Red, JieqiRole.Cannon, 0, 5)),

            // 士先照，马连跳两步才将死。
            Level.Make("士引连环", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 8, 2),
                P(JieqiCamp.Red, JieqiRole.Pawn, 6, 1),
                P(JieqiCamp.Red, JieqiRole.Advisor, 6, 2)),

            // 马照将后，炮先平移再向前。
            Level.Make("炮转横", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 8, 2),
                P(JieqiCamp.Red, JieqiRole.Horse, 7, 3),
                P(JieqiCamp.Red, JieqiRole.Cannon, 1, 2)),

            // 马照将后，炮退一步再打到中路，用士当架子。
            Level.Make("回炮打将", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 0, 2),
                P(JieqiCamp.Red, JieqiRole.Cannon, 7, 2),
                P(JieqiCamp.Red, JieqiRole.Advisor, 4, 2)),

            // 马先照，再把兵顶到将前。
            Level.Make("进兵收官", "红先走",
                K(JieqiCamp.Black, 4, 0),
                K(JieqiCamp.Red, 3, 9),
                P(JieqiCamp.Black, JieqiRole.Elephant, 3, 0),
                P(JieqiCamp.Black, JieqiRole.Elephant, 5, 0),
                P(JieqiCamp.Red, JieqiRole.Horse, 8, 2),
                P(JieqiCamp.Red, JieqiRole.Horse, 1, 3),
                P(JieqiCamp.Red, JieqiRole.Pawn, 4, 3))
        };

        static JieqiPieceState K(JieqiCamp camp, int x, int y)
        {
            return Open(camp, JieqiRole.King, x, y);
        }

        static JieqiPieceState P(JieqiCamp camp, JieqiRole role, int x, int y)
        {
            return Open(camp, role, x, y);
        }

        static JieqiPieceState Open(JieqiCamp camp, JieqiRole role, int x, int y)
        {
            return new JieqiPieceState
            {
                Alive = true,
                FaceDown = false,
                Controller = camp,
                Printed = camp,
                Role = role,
                SlotRole = role,
                X = x,
                Y = y,
                CapturedBy = JieqiCamp.None
            };
        }

        sealed class Level
        {
            public string Title;
            public string Hint;
            public JieqiCamp Side;
            public JieqiPieceState[] Pieces;

            public static Level Make(string title, string hint, params JieqiPieceState[] pieces)
            {
                return new Level
                {
                    Title = title,
                    Hint = hint,
                    Side = JieqiCamp.Red,
                    Pieces = pieces
                };
            }
        }
    }
}
