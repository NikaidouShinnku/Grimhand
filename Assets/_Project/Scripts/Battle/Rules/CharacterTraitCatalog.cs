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

        /// <summary>典狱长：开战召唤囚笼；无囚笼时永久 +50% 增伤。</summary>
        public const string WardenCageMaster = "warden_cage_master";

        /// <summary>囚笼：回合开始自伤；死亡时召唤精英并清除玩家烙印。</summary>
        public const string PrisonCage = "prison_cage";

        /// <summary>黑暗骑士：回合开始全体玩家 +1 永久中毒；玩家中毒层数视为易伤。</summary>
        public const string DarkKnightPoisonAura = "dark_knight_poison_aura";

        /// <summary>腐化海洋女神：涨潮/退潮机制。</summary>
        public const string OceanGoddessTide = "ocean_goddess_tide";

        public const string WardenCharacterId = "char_warden";
        public const string PrisonCageCharacterId = "char_prison_cage";
        public const string DarkKnightCharacterId = "char_dark_knight";
        public const string OceanGoddessCharacterId = "char_corrupted_ocean_goddess";
    }
}
