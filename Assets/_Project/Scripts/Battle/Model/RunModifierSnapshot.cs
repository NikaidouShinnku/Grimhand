namespace Grimhand.Battle.Model
{
    /// <summary>远征遗物等 run 级加成，单场战斗内生效。</summary>
    public sealed class RunModifierSnapshot
    {
        public int TeamAttackBonus { get; set; }
        public int TeamDefenseBonus { get; set; }
        public int TeamHpBonus { get; set; }
        public int FrontDefenseBonus { get; set; }
        public int BackRowExtraDrawPerTurn { get; set; }
        public int BattleStartTeamHeal { get; set; }
        public int BattleStartFrontBlock { get; set; }
        public int ExtraDrawOnBattleStart { get; set; }
        /// <summary>抽牌时跳过污染牌（猫灵雕像）。</summary>
        public bool SkipPollutedCardsOnDraw { get; set; }
        public float GoldBonusPercent { get; set; }
        public float SacrificeDamageBonusPercent { get; set; }
        public int SacrificeHpCostReduction { get; set; }
        public int SacrificeStackAttackBonus { get; set; }
        public float HealBonusPercent { get; set; }
        public float PharaohBlockGivenBonusPercent { get; set; }
        public float SacrificeHpCostReductionPercent { get; set; }
        /// <summary>献祭血量消耗增加（血怒献祭等）。</summary>
        public float SacrificeHpCostIncreasePercent { get; set; }
        public int HealGrantsBlock { get; set; }
        public int StatusCardTeamBlock { get; set; }
        public float WarriorBlockChanceOnHit { get; set; }
        public int WarriorBlockAmountOnHit { get; set; }
        public int WarriorFirstHitBlockAmount { get; set; }
        public float WarriorTauntDamageReductionPercent { get; set; }
        public float WarriorBlockDamageReductionPercent { get; set; }
        public float FirstAttackDamageBonusPercent { get; set; }
        public int FirstAttackFlatBonus { get; set; }
        public int FirstDefenseFlatBonus { get; set; }
        public int AttackAndDefenseSameTurnHeal { get; set; }
        public float HighCostCardDamageBonusPercent { get; set; }
        public float FirstHitDamageReductionPercent { get; set; }
        public int EndTurnTeamHeal { get; set; }
        public int StatusDurationBonusTurns { get; set; }
        public float AttackBurnProcChance { get; set; }
        public int AttackBurnStacks { get; set; }
        public int AttackBurnDurationTurns { get; set; }
        public int ExtraEnergyCap { get; set; }
        public bool RandomDiscardEachTurn { get; set; }
        public bool DeathCardsSkipPolluteTurns { get; set; }
        public int DeathCardsSkipPolluteDuration { get; set; } = 3;
        public int ScryDrawPileCount { get; set; }

        public int TurnStartRandomAllyBlock { get; set; }
        public int TurnStartTeamBlock { get; set; }
        public float DodgeChanceOnHit { get; set; }
        public int BattleStartSpeedBonusTurns { get; set; }
        public int BattleStartSpeedBonus { get; set; }
        public int EndTurnEnemyFireDamage { get; set; }
        public int RevengeAttackFlatBonus { get; set; }
        public bool BackRowAttackAnyTarget { get; set; }
        public bool JadeDaggerFirstKillBonus { get; set; }

        /// <summary>灵魂裂隙：每场战斗开始随机 1 名队员失去 HP。</summary>
        public int SoulRiftBattleStartRandomHpLoss { get; set; }

        public bool FirstPlayerAttackPending { get; set; } = true;

        public static RunModifierSnapshot Empty { get; } = new();
    }
}
