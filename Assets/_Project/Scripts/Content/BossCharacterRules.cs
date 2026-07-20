namespace Grimhand.Content
{
    /// <summary>Boss 角色 ID：卡面椭圆区保留专属 profile，不走通用怪物骷髅。</summary>
    public static class BossCharacterRules
    {
        public const string SkeletonKing = "char_skeleton_king";
        public const string GhostQueen = "char_ghost_queen";
        /// <summary>召唤物，非 Boss；保留常量供召唤逻辑引用。</summary>
        public const string ExplosiveSkull = "char_explosive_skull";
        public const string Warden = "char_warden";
        public const string PrisonCage = "char_prison_cage";
        public const string DarkKnight = "char_dark_knight";
        public const string CorruptedOceanGoddess = "char_corrupted_ocean_goddess";

        public static bool IsBoss(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return false;

            return characterDefinitionId == SkeletonKing
                || characterDefinitionId == GhostQueen
                || characterDefinitionId == Warden
                || characterDefinitionId == PrisonCage
                || characterDefinitionId == DarkKnight
                || characterDefinitionId == CorruptedOceanGoddess;
        }
    }
}
