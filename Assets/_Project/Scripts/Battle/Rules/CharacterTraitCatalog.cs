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

        public const string SkullExplodeCardId = "m_skull_explode";
    }
}
