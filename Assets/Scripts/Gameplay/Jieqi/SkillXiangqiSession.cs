namespace SmallGames
{
    /// <summary>技能象棋。开局是全部明着的标准象棋，吃子后获得被吃子原来的走法。</summary>
    public class SkillXiangqiSession : JieqiSession
    {
        protected override bool SkillMatch => true;
    }
}
