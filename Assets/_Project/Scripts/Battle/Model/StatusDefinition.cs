namespace Grimhand.Battle.Model
{
    public sealed class StatusDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public StatusDurationKind DurationKind { get; set; }
        public int DefaultDuration { get; set; } = 2;
        public int SpeedModifierPerStack { get; set; }
        public int AttackModifierPerStack { get; set; }
        public int DefenseModifierPerStack { get; set; }
        public int TurnStartDamagePerStack { get; set; }
        /// <summary>回合结束时每层造成的伤害（如灼烧）。</summary>
        public int TurnEndDamagePerStack { get; set; }
        /// <summary>状态跳伤忽视护甲（如中毒）。</summary>
        public bool TickIgnoresBlock { get; set; }
        /// <summary>状态跳伤忽视 DEF（如灼烧）。</summary>
        public bool TickIgnoresDefense { get; set; }
        /// <summary>每层使最大生命 +N%（永久类状态，在施加时一次性调整 MaxHp）。</summary>
        public int MaxHpPercentBonusPerStack { get; set; }
        /// <summary>每层使攻击 +N%（乘算，在 RefreshDerivedStats 后应用）。</summary>
        public int AttackPercentBonusPerStack { get; set; }
        /// <summary>每层使防御 +N%（乘算，在 RefreshDerivedStats 后应用）。</summary>
        public int DefensePercentBonusPerStack { get; set; }
    }
}
