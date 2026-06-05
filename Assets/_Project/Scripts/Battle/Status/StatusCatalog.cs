using System.Collections.Generic;
using Grimhand.Battle.Model;

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
                TurnStartDamagePerStack = 1
            };
            map[Slow] = new StatusDefinition
            {
                Id = Slow,
                DisplayName = "减速",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                SpeedModifierPerStack = -2
            };
            map[Burn] = new StatusDefinition
            {
                Id = Burn,
                DisplayName = "灼烧",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                TurnStartDamagePerStack = 3
            };
            map[AttackUp] = new StatusDefinition
            {
                Id = AttackUp,
                DisplayName = "攻击提升",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                AttackModifierPerStack = 1
            };
            map[AttackDown] = new StatusDefinition
            {
                Id = AttackDown,
                DisplayName = "攻击降低",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                AttackModifierPerStack = -1
            };
            map[DefenseUp] = new StatusDefinition
            {
                Id = DefenseUp,
                DisplayName = "防御提升",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                DefenseModifierPerStack = 1
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
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 3,
                TurnStartDamagePerStack = 5
            };
            return map;
        }
    }
}
