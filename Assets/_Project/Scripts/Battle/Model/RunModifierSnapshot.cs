namespace Grimhand.Battle.Model
{
    /// <summary>远征遗物等 run 级加成，单场战斗内生效。</summary>
    public sealed class RunModifierSnapshot
    {
        public int TeamAttackBonus { get; set; }
        public int FrontDefenseBonus { get; set; }
        public int BackRowExtraDrawPerTurn { get; set; }
        public int BattleStartTeamHeal { get; set; }
        public float GoldBonusPercent { get; set; }
        public float SacrificeDamageBonusPercent { get; set; }
        public float HealBonusPercent { get; set; }
        public int HealGrantsBlock { get; set; }
        public float WarriorBlockChanceOnHit { get; set; }
        public int WarriorBlockAmountOnHit { get; set; }
        public float FirstAttackDamageBonusPercent { get; set; }
        public int ExtraEnergyCap { get; set; }
        public bool RandomDiscardEachTurn { get; set; }
        public bool DeathCardsSkipPolluteTurns { get; set; }
        public int DeathCardsSkipPolluteDuration { get; set; } = 3;
        public int ScryDrawPileCount { get; set; }

        public bool FirstPlayerAttackPending { get; set; } = true;

        public static RunModifierSnapshot Empty { get; } = new();
    }
}
