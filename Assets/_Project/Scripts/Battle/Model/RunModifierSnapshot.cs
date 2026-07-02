namespace Grimhand.Battle.Model
{
    /// <summary>远征遗物等 run 级加成，单场战斗内生效。</summary>
    public sealed class RunModifierSnapshot
    {
        public int TeamAttackBonus { get; set; }
        public int TeamDefenseBonus { get; set; }
        public int TeamHpBonus { get; set; }
        /// <summary>v0.81：全队获得 X% 增伤（翡翠短刀/烈焰之剑/龙纹指环）。</summary>
        public float TeamAttackBonusPercent { get; set; }
        /// <summary>v0.81：全队获得 X% 强固（护甲获取加成，铁壁战甲/圣骑之盾）。</summary>
        public float TeamBlockGainBonusPercent { get; set; }
        /// <summary>v0.81：每回合开始时给予所有敌人 N 层灼烧（永久）（赤红烈焰靴）。</summary>
        public int TurnStartEnemyBurnStacks { get; set; }
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
        /// <summary>每回合开始时对全体敌人造成伤害（赤红烈焰靴）。</summary>
        public int TurnStartEnemyDamage { get; set; }
        public int RevengeAttackFlatBonus { get; set; }
        public bool BackRowAttackAnyTarget { get; set; }
        public bool JadeDaggerFirstKillBonus { get; set; }

        /// <summary>奇迹之叶：触发时恢复的最大 HP 百分比（默认 20）。</summary>
        public int MiracleLeafReviveHpPercent { get; set; } = 20;

        /// <summary>灵魂裂隙：每场战斗开始随机 1 名队员失去 HP。</summary>
        public int SoulRiftBattleStartRandomHpLoss { get; set; }

        /// <summary>便携篝火：战斗胜利后全队回复 HP 百分比。</summary>
        public float PostBattleTeamHealPercent { get; set; }

        /// <summary>烈火长剑：前排对灼烧目标伤害倍率。</summary>
        public float FrontRowBurnTargetDamageMultiplier { get; set; } = 1f;

        /// <summary>水晶剑：前排攻击无视护甲时的伤害百分比（如 75）。</summary>
        public int FrontRowIgnoreArmorDamagePercent { get; set; }

        /// <summary>魔焰颅骨：战斗开始需二选一。</summary>
        public bool RequiresFelskullChoice { get; set; }

        /// <summary>魔焰颅骨 B 选项：全队攻击牌 +N% 伤害。</summary>
        public int FelskullOutgoingDamagePercentBonus { get; set; }

        public bool FirstPlayerAttackPending { get; set; } = true;

        /// <summary>圣阳之书：含「阳/日」牌使用时额外视为 +N 级（可超出升级上限）。</summary>
        public int HolysunSpellbookBonusUpgradeLevels { get; set; }

        /// <summary>巫妖女王灵魂挽歌：本场远征中累计进入虚化状态的次数（每进入一次 +1）。</summary>
        public int EtherealEntryCount { get; set; }

        public static RunModifierSnapshot Empty { get; } = new();
    }
}
