namespace Grimhand.Battle.Rules
{
    public static class CharacterTraitCatalog
    {
        /// <summary>每回合首次受到伤害时获得 10 点护甲。</summary>
        public const string BossFirstHitBlock = "boss_first_hit_block";
        public const int BossFirstHitBlockAmount = 10;

        /// <summary>每回合开始时永久 +1 基础防御。</summary>
        public const string BossTurnDefenseUp = "boss_turn_def_up";

        /// <summary>存活时每回合开始将自爆牌加入手牌（不占抽牌上限）。</summary>
        public const string SkullSelfDestructHand = "skull_self_destruct_hand";

        /// <summary>首次 HP 低于 120 时虚化并下回合获得「幽灵女王之怒」。</summary>
        public const string GhostQueenEnrage = "ghost_queen_enrage";
        public const int GhostQueenEnrageHpThreshold = 120;

        public const string SkullExplodeCardId = "m_skull_explode";
        public const string GhostQueenWrathCardId = "m_queen_wrath";
    }
}
