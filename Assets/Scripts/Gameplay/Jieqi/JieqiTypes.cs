namespace SmallGames
{
    public enum JieqiCamp
    {
        None = 0,
        Red = 1,
        Black = 2
    }

    public enum JieqiRole
    {
        King = 0,
        Advisor = 1,
        Elephant = 2,
        Horse = 3,
        Rook = 4,
        Cannon = 5,
        Pawn = 6
    }

    public enum JieqiOutcome
    {
        Playing = 0,
        RedWin = 1,
        BlackWin = 2,
        Draw = 3
    }

    public enum JieqiPlayMode
    {
        TwoPlayer = 0,
        VersusAi = 1
    }

    public struct JieqiMove
    {
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
        public int Mover;
        public int Captured;
    }

    public struct JieqiStep
    {
        public bool Ok;
        public string Message;
        public JieqiMove Move;
        public bool Flipped;
        public JieqiCamp RevealedCamp;
        public JieqiRole RevealedRole;
        public bool Captured;
        public bool CapturedWasHidden;
        public JieqiCamp CapturedCamp;
        public JieqiRole CapturedRole;
        public JieqiCamp CapturedController;
        public JieqiOutcome Outcome;
    }

    /// <summary>界面可见的一格。暗子不带底牌。</summary>
    public struct JieqiViewSquare
    {
        public bool Occupied;
        public int PieceId;
        public bool FaceDown;
        public JieqiCamp Controller;
        public JieqiCamp Printed;
        public JieqiRole Role;
        public bool HasSkill;
        public JieqiRole SkillRole;
        public JieqiCamp SkillPrinted;
    }

    public struct JieqiCapturedView
    {
        public int PieceId;
        public JieqiCamp CapturedBy;
        public JieqiCamp Controller;
        public JieqiCamp Printed;
        public JieqiRole Role;
    }

    /// <summary>存档用，含未翻开的底牌。界面不要拿它来显示暗子。</summary>
    public struct JieqiPieceState
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
}
