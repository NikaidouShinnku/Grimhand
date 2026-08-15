using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.V091;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>Excel 描述无法用通用 Action 表达的卡牌（X 费、随机多效果等）。</summary>
    public static class SpecialCardRules
    {
        public const int SolarGodWrathDamage = 13;
        public const int SolarGodWrathArmorDownStacks = 20;
        public const int SolarGodWrathArmorDownDuration = 2;
        public const int SolarGodWrathVulnerableStacks = 20;
        public const int SolarGodWrathVulnerableDuration = 2;
        public const int SolarGodWrathSlowStacks = 2;
        public const int SolarGodWrathSlowDuration = 2;
        public const int SolarGodWrathBurnStacks = 5;
        public const int SolarGodWrathBurnDuration = 2;
        public const int SolarBlessingBlockPerRepeat = 6;

        public static bool IsSpecialCard(CardInstanceState card) =>
            card != null && (card.DefinitionId == CardPowerRules.SolarGodWrathCardId
                             || card.DefinitionId == CardPowerRules.SolarBlessingCardId
                             || V091MechanicsRules.IsV091SpecialCard(card));

        public static bool TryResolve(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || rng == null)
                return false;

            if (V091MechanicsRules.TryResolveSpecialCard(state, actor, card, events, rng))
                return true;

            return card.DefinitionId switch
            {
                CardPowerRules.SolarGodWrathCardId => ResolveSolarGodWrath(state, actor, card, events, rng),
                CardPowerRules.SolarBlessingCardId => ResolveSolarBlessing(state, actor, card, events, rng),
                _ => false
            };
        }

        static int GetEnergySpent(BattleState state, CardInstanceState card)
        {
            if (state.EnergySpentByCardInstanceId.TryGetValue(card.InstanceId, out var spent) && spent > 0)
                return spent;

            if (state.PlayerPlan.EnergySpentPerCard.TryGetValue(card.InstanceId, out spent) && spent > 0)
                return spent;

            return System.Math.Max(0, card.Cost);
        }

        static bool ResolveSolarGodWrath(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var repeats = GetEnergySpent(state, card) + System.Math.Max(0, card.UpgradeLevel);
            if (repeats <= 0)
            {
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{card.DisplayName} 无能量可消耗")
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                return true;
            }

            for (var i = 0; i < repeats; i++)
            {
                var target = PickRandomAliveEnemy(state, rng);
                if (target == null)
                    break;

                switch (rng.NextInt(0, 5))
                {
                    case 0:
                        DamageRules.ApplyDamage(
                            state, actor, target, SolarGodWrathDamage, card.CardType, events,
                            rng: rng, sourceCardInstanceId: card.InstanceId);
                        break;
                    case 1:
                        StatusRules.ApplyStatus(
                            state, target, StatusCatalog.ArmorDown,
                            SolarGodWrathArmorDownStacks, SolarGodWrathArmorDownDuration, events);
                        break;
                    case 2:
                        StatusRules.ApplyStatus(
                            state, target, StatusCatalog.Vulnerable,
                            SolarGodWrathVulnerableStacks, SolarGodWrathVulnerableDuration, events);
                        break;
                    case 3:
                        StatusRules.ApplyStatus(
                            state, target, StatusCatalog.Slow, SolarGodWrathSlowStacks, SolarGodWrathSlowDuration, events);
                        break;
                    default:
                        StatusRules.ApplyStatus(
                            state, target, StatusCatalog.Burn, SolarGodWrathBurnStacks, SolarGodWrathBurnDuration, events);
                        break;
                }
            }

            return true;
        }

        static bool ResolveSolarBlessing(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var repeats = GetEnergySpent(state, card);
            if (repeats <= 0)
                return true;

            for (var i = 0; i < repeats; i++)
            {
                foreach (var ally in state.GetTeam(TeamSide.Player))
                {
                    if (!ally.IsAlive)
                        continue;

                    DamageRules.ApplyBlock(ally, SolarBlessingBlockPerRepeat, events, state, rng);
                }
            }

            return true;
        }

        static CombatantState PickRandomAliveEnemy(BattleState state, BattleRng rng)
        {
            var pool = new List<CombatantState>();
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (enemy.IsAlive)
                    pool.Add(enemy);
            }

            if (pool.Count == 0)
                return null;

            return pool[rng.NextIndex(pool.Count)];
        }
    }
}
