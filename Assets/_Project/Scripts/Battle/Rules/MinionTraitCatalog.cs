namespace Grimhand.Battle.Rules
{
    public static class MinionTraitCatalog
    {
        /// <summary>回合内未受伤时，下回合开始时回复 2 HP。</summary>
        public const string SlimeRegen = "minion_slime_regen";

        /// <summary>每打出 3 张牌 +1 DEF。</summary>
        public const string SkeletonCardDef = "minion_skeleton_card_def";

        /// <summary>每打出 3 张牌 +1 DEF 与 +1 ATK。</summary>
        public const string SkeletonEliteCardStats = "minion_skeleton_elite_card_stats";

        /// <summary>HP 低于 50% 时 +2 速度。</summary>
        public const string WraithLowHpSpeed = "minion_wraith_low_hp_speed";

        /// <summary>HP 低于 50% 时获得 1 回合虚化并 +2 速度（每场一次）。</summary>
        public const string WraithEliteLowHpEthereal = "minion_wraith_elite_low_hp_ethereal";

        /// <summary>受击叠血怒，下张攻击牌 +15%/层并消耗。</summary>
        public const string OgreBloodRage = "minion_ogre_blood_rage";

        /// <summary>每回合首次受击 50% 完全闪避（无论成败均消耗）。</summary>
        public const string BatFirstHitDodge = "minion_bat_first_hit_dodge";

        public const string SkeletonCharacterId = "char_skeleton";
        public const int SlimeRegenAmount = 2;
        public const int CardsPerStatBonus = 3;
        public const int WraithLowHpSpeedBonus = 2;
        public const int OgreBloodRageMaxStacks = 5;
        public const int OgreBloodRageDamagePercentPerStack = 15;
        public const float BatFirstHitDodgeChance = 0.5f;
    }
}
