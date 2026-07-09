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

        /// <summary>本场每有一只鼠人死亡，存活鼠人 +20% ATK。</summary>
        public const string RatPackAttackOnAllyDeath = "minion_rat_pack_attack";
        public const string RatCharacterId = "char_rat";
        public const int RatPackAttackBonusPercentPerDeath = 20;

        /// <summary>自身负面状态同步至所有敌人。</summary>
        public const string ChainWraithDebuffShare = "minion_chain_wraith_debuff_share";

        /// <summary>每回合首张牌：攻击 +3 ATK，否则 +3 DEF。</summary>
        public const string GargoyleFirstCardStance = "minion_gargoyle_first_card";
        public const int GargoyleStanceBonus = 3;

        /// <summary>场上有蜘蛛贵妇时，敌人每 5 层中毒额外 +10% 受伤。</summary>
        public const string SpiderLadyPoisonVulnerability = "minion_spider_poison_vuln";
        public const int SpiderPoisonVulnPercentPerFiveStacks = 10;

        /// <summary>回合结束保留一半护甲至下回合。</summary>
        public const string StoneGolemArmorRetain = "minion_stone_golem_armor_retain";

        /// <summary>比同排敌人每多 1 点速度 +10% 攻击（最多 50%）。</summary>
        public const string SeahorseGuardSpeedAttack = "minion_seahorse_speed_attack";

        /// <summary>敌人换位时 +10 最大生命（占位，后续接 PositionRules）。</summary>
        public const string JellyfishCasterSwapMaxHp = "minion_jellyfish_swap_max_hp";

        /// <summary>每使用一张 0 费牌 +5% 攻击。</summary>
        public const string MermaidZeroCostAttack = "minion_mermaid_zero_cost_attack";

        /// <summary>对敌人造成 HP 伤害时附加中毒×5。</summary>
        public const string AbyssCreaturePoisonOnDamage = "minion_abyss_poison_on_damage";
        public const string AbyssCreatureCharacterId = "char_abyss_creature";
        public const int AbyssCreaturePoisonStacks = 5;

        /// <summary>受到伤害时对随机敌人施加中毒×5。</summary>
        public const string CorruptedCrabPoisonOnHit = "minion_crab_poison_on_hit";
        public const int CorruptedCrabPoisonStacks = 5;

        /// <summary>任意敌人低于 25% 生命或死亡时 +33% 攻击、-20% 防御。</summary>
        public const string PhantomCaptainFrenzy = "minion_phantom_captain_frenzy";
        public const int PhantomCaptainFrenzyAttackPercent = 33;
        public const int PhantomCaptainFrenzyDefensePercent = 20;

        public static bool IsAbyssRegionCharacter(string characterDefinitionId) =>
            characterDefinitionId is "char_seahorse_guard"
                or "char_jellyfish_caster"
                or "char_mermaid_warrior"
                or AbyssCreatureCharacterId
                or "char_corrupted_crab"
                or "char_phantom_captain";

        /// <summary>地牢怪 + 幽灵/骷髅精英/蝙蝠：立绘略大于普通洞穴怪。</summary>
        public static bool UsesElevatedPortraitScale(string characterDefinitionId) =>
            characterDefinitionId is "char_rat"
                or "char_chain_wraith"
                or "char_gargoyle"
                or "char_spider_lady"
                or "char_stone_golem"
                or "char_wraith"
                or "char_wraith_elite"
                or "char_skeleton_elite"
                or "char_bat";
    }
}
