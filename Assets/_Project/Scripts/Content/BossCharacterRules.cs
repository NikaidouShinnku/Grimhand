namespace Grimhand.Content
{
    /// <summary>Boss 角色 ID：卡面椭圆区保留专属 profile，不走通用怪物骷髅。</summary>
    public static class BossCharacterRules
    {
        public const string SkeletonKing = "char_skeleton_king";
        public const string GhostQueen = "char_ghost_queen";
        public const string ExplosiveSkull = "char_explosive_skull";

        public static bool IsBoss(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return false;

            return characterDefinitionId == SkeletonKing
                || characterDefinitionId == GhostQueen
                || characterDefinitionId == ExplosiveSkull;
        }
    }
}
