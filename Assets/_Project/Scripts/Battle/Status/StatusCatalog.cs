using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Status
{
    public static class StatusCatalog
    {
        public const string Poison = "poison";
        public const string Slow = "slow";
        public const string Burn = "burn";
        public const string AttackUp = "attack_up";
        public const string AttackDown = "attack_down";
        public const string DefenseUp = "defense_up";
        public const string Taunt = "taunt";
        public const string Guard = "guard";
        public const string VampAura = "vamp_aura";
        public const string ReviveBlessing = "revive_blessing";
        public const string Unyielding = "unyielding";
        public const string NecroticPoison = "necrotic_poison";
        public const string BoneWorkshop = "bone_workshop";
        public const string AnubisAvatar = "anubis_avatar";
        public const string Ethereal = "ethereal";
        public const string GhostQueenWrath = "ghost_queen_wrath";
        public const string FinalBloodRitual = "final_blood_ritual";
        public const string SandSpearReforge = "sand_spear_reforge";
        public const string RatSwarmCall = "rat_swarm_call";
        public const string GodDescends = "god_descends";
        public const string AttackUpPercent = "attack_up_pct";
        public const string DefenseUpPercent = "defense_up_pct";
        public const string DefenseDownPercent = "defense_down_pct";
        public const string DamageUp = "damage_up";
        public const string Weaken = "weaken";
        public const string ArmorUp = "armor_up";
        public const string ArmorDown = "armor_down";
        public const string Vulnerable = "vulnerable";
        public const string DamageReduction = "damage_reduction";
        public const string FinalSummonPending = "final_summon_pending";

        static readonly Dictionary<string, StatusDefinition> Definitions = Build();

        public static StatusDefinition Get(string id)
        {
            Definitions.TryGetValue(id, out var def);
            return def;
        }

        static Dictionary<string, StatusDefinition> Build()
        {
            var map = new Dictionary<string, StatusDefinition>();
            map[Poison] = new StatusDefinition
            {
                Id = Poison,
                DisplayName = "中毒",
                DurationKind = StatusDurationKind.Permanent,
                TurnStartDamagePerStack = 1,
                TickIgnoresBlock = true
            };
            map[Slow] = new StatusDefinition
            {
                Id = Slow,
                DisplayName = "减速",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                SpeedModifierPerStack = -1
            };
            map[Burn] = new StatusDefinition
            {
                Id = Burn,
                DisplayName = "灼烧",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                TurnEndDamagePerStack = 2,
                TickIgnoresDefense = true
            };
            map[AttackUp] = new StatusDefinition
            {
                Id = AttackUp,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                OutgoingDamageFlatPerStack = 1
            };
            map[AttackDown] = new StatusDefinition
            {
                Id = AttackDown,
                DisplayName = "虚弱",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                OutgoingDamageReductionFlatPerStack = 1
            };
            map[DefenseUp] = new StatusDefinition
            {
                Id = DefenseUp,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                BlockGainFlatPerStack = 1
            };
            map[Taunt] = new StatusDefinition
            {
                Id = Taunt,
                DisplayName = "嘲讽",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[Guard] = new StatusDefinition
            {
                Id = Guard,
                DisplayName = "誓死守护",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[VampAura] = new StatusDefinition
            {
                Id = VampAura,
                DisplayName = "吸血光环",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[ReviveBlessing] = new StatusDefinition
            {
                Id = ReviveBlessing,
                DisplayName = "复活祝福",
                DurationKind = StatusDurationKind.Permanent
            };
            map[Unyielding] = new StatusDefinition
            {
                Id = Unyielding,
                DisplayName = "不屈意志",
                DurationKind = StatusDurationKind.Permanent
            };
            map[NecroticPoison] = new StatusDefinition
            {
                Id = NecroticPoison,
                DisplayName = "亡灵毒",
                DurationKind = StatusDurationKind.Permanent,
                TurnStartDamagePerStack = 1,
                TickIgnoresBlock = true
            };
            map[BoneWorkshop] = new StatusDefinition
            {
                Id = BoneWorkshop,
                DisplayName = "骨之王座",
                DurationKind = StatusDurationKind.Permanent
            };
            map[AnubisAvatar] = new StatusDefinition
            {
                Id = AnubisAvatar,
                DisplayName = "阿努比斯化身",
                DurationKind = StatusDurationKind.Permanent,
                MaxHpPercentBonusPerStack = AnubisAvatarRules.StatPercentBonus,
                AttackPercentBonusPerStack = AnubisAvatarRules.StatPercentBonus,
                DefensePercentBonusPerStack = AnubisAvatarRules.StatPercentBonus
            };
            map[Ethereal] = new StatusDefinition
            {
                Id = Ethereal,
                DisplayName = "虚化",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[GhostQueenWrath] = new StatusDefinition
            {
                Id = GhostQueenWrath,
                DisplayName = "幽灵女王之怒",
                DurationKind = StatusDurationKind.Permanent,
                AttackPercentBonusPerStack = 100
            };
            map[FinalBloodRitual] = new StatusDefinition
            {
                Id = FinalBloodRitual,
                DisplayName = "最终鲜血仪式",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SandSpearReforge] = new StatusDefinition
            {
                Id = SandSpearReforge,
                DisplayName = "沙矛重塑",
                DurationKind = StatusDurationKind.Permanent
            };
            map[RatSwarmCall] = new StatusDefinition
            {
                Id = RatSwarmCall,
                DisplayName = "鼠群呼唤",
                DurationKind = StatusDurationKind.Permanent
            };
            map[GodDescends] = new StatusDefinition
            {
                Id = GodDescends,
                DisplayName = "天神下凡",
                DurationKind = StatusDurationKind.Permanent
            };
            map[AttackUpPercent] = new StatusDefinition
            {
                Id = AttackUpPercent,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 3,
                AttackPercentBonusPerStack = 1
            };
            map[DefenseUpPercent] = new StatusDefinition
            {
                Id = DefenseUpPercent,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                DefensePercentBonusPerStack = 1
            };
            map[DefenseDownPercent] = new StatusDefinition
            {
                Id = DefenseDownPercent,
                DisplayName = "破损",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                BlockGainReductionPercentPerStack = 1
            };
            map[DamageUp] = new StatusDefinition
            {
                Id = DamageUp,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                OutgoingDamageFlatPerStack = 1
            };
            map[Weaken] = new StatusDefinition
            {
                Id = Weaken,
                DisplayName = "虚弱",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                OutgoingDamageReductionFlatPerStack = 1
            };
            map[ArmorUp] = new StatusDefinition
            {
                Id = ArmorUp,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                BlockGainFlatPerStack = 1
            };
            map[ArmorDown] = new StatusDefinition
            {
                Id = ArmorDown,
                DisplayName = "破损",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                BlockGainReductionPercentPerStack = 1
            };
            map[Vulnerable] = new StatusDefinition
            {
                Id = Vulnerable,
                DisplayName = "易伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                IncomingDamagePercentPerStack = 1
            };
            map[DamageReduction] = new StatusDefinition
            {
                Id = DamageReduction,
                DisplayName = "减伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                IncomingDamageReductionPercentPerStack = 1
            };
            map[FinalSummonPending] = new StatusDefinition
            {
                Id = FinalSummonPending,
                DisplayName = "终焉召唤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            return map;
        }
    }
}
