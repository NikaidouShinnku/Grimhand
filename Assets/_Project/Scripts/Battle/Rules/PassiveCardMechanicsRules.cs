using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>策划案中的被动/触发型卡牌机制（天神下凡、最终鲜血仪式、无尽血刃等）。</summary>
    public static class PassiveCardMechanicsRules
    {
        public const string EndlessBladeCardId = "d_endless_blade";
        public const string SandSpearReforgeCardId = "p_sand_spear_reforge";
        public const string SpiderFatalBindCardId = "m_spider_fatal_bind";
        public const string GargoyleSunderCardId = "m_gargoyle_sunder";
        public const string FinalBindCardId = "m_final_bind";
        public const int GodDescendsFlatDamage = 5;
        public const int GodDescendsAttackScalePercent = 120;
        public const int FinalBloodRitualDraw = 1;
        public const int FinalBloodRitualHeal = 5;
        public const int SandSpearReforgeBaseDamage = 4;
        public const int FinalBindBonusPoisonStacks = 30;

        public static int GetEndlessBladeDamageMultiplierPercent(BattleState state, int cardInstanceId)
        {
            if (state == null || cardInstanceId <= 0)
                return 100;

            return state.CardInstanceDamageMultiplierPercent.TryGetValue(cardInstanceId, out var pct)
                ? Math.Max(100, pct)
                : 100;
        }

        public static int ApplyEndlessBladeMultiplier(BattleState state, CardInstanceState card, int power)
        {
            if (state == null || card == null || card.DefinitionId != EndlessBladeCardId || power <= 0)
                return power;

            var mul = GetEndlessBladeDamageMultiplierPercent(state, card.InstanceId);
            return Math.Max(1, (int)Math.Round(power * mul / 100f));
        }

        public static void OnEndlessBladeResolved(BattleState state, CardInstanceState card, List<BattleEvent> events)
        {
            if (state == null || card == null || card.DefinitionId != EndlessBladeCardId)
                return;

            var current = GetEndlessBladeDamageMultiplierPercent(state, card.InstanceId);
            state.CardInstanceDamageMultiplierPercent[card.InstanceId] = current * 2;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{card.DisplayName} 伤害翻倍")
            {
                CardInstanceId = card.InstanceId,
                Amount = state.CardInstanceDamageMultiplierPercent[card.InstanceId]
            });
        }

        public static void TryTriggerFinalBloodRitualOnSacrifice(
            BattleState state,
            CombatantState actor,
            CardInstanceState sacrificeCard,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || sacrificeCard == null || rng == null)
                return;

            if (!sacrificeCard.Keywords.Contains("sacrifice"))
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.FinalBloodRitual))
                return;

            DeckRules.DrawCards(state, actor.Team, rng, FinalBloodRitualDraw, events);
            DamageRules.ApplyHeal(state, actor, FinalBloodRitualHeal, events, actor);
        }

        public static void TryTriggerGodDescendsOnBlockGain(
            BattleState state,
            CombatantState actor,
            int blockGained,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || blockGained <= 0 || rng == null)
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.GodDescends))
                return;

            var power = GodDescendsFlatDamage;
            if (actor.Attack > 0)
                power += (int)Math.Round(actor.Attack * GodDescendsAttackScalePercent / 100f);

            if (power <= 0)
                return;

            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                DamageRules.ApplyDamage(
                    state,
                    actor,
                    enemy,
                    power,
                    CardType.Attack,
                    events,
                    rng: rng,
                    logSuffix: "（天神下凡）",
                    isAoEWave: true);
            }
        }

        public static void TryTriggerSandSpearReforgeOnExhaust(
            BattleState state,
            CombatantState actor,
            CardInstanceState exhaustedCard,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || exhaustedCard == null || rng == null)
                return;

            if (actor.Team != TeamSide.Player)
                return;

            foreach (var ally in state.Combatants)
            {
                if (!ally.IsAlive || ally.Team != TeamSide.Player)
                    continue;

                if (!StatusRules.HasStatus(ally, StatusCatalog.SandSpearReforge))
                    continue;

                var power = StatusRules.GetStatusStacks(ally, StatusCatalog.SandSpearReforge);
                if (power <= 0)
                    power = SandSpearReforgeBaseDamage;

                var target = PickRandomAliveEnemy(state, rng);
                if (target == null)
                    return;

                DamageRules.ApplyDamage(
                    state, ally, target, power, CardType.Attack, events,
                    rng: rng, logSuffix: "（沙矛重塑）");
                return;
            }
        }

        public static void OnSandSpearReforgePlayed(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || actor == null)
                return;

            StatusRules.ApplyStatus(
                state, actor, StatusCatalog.SandSpearReforge, SandSpearReforgeBaseDamage, -1, events);
        }

        public static void OnSpiderFatalBindResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || !actor.IsAlive)
                return;

            if (card.DefinitionId != SpiderFatalBindCardId)
                return;

            var selfDamage = Math.Max(1, actor.Hp / 2);
            DamageRules.ApplyDamage(
                state, actor, actor, selfDamage, CardType.Attack, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: card.InstanceId);
        }

        public static void PrepareGargoyleSunderTarget(
            BattleState state,
            CombatantState target,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || target == null || card == null)
                return;

            if (card.DefinitionId != GargoyleSunderCardId || target.Block <= 0)
                return;

            events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{target.DisplayName} 护甲被移除")
            {
                CombatantId = target.Id,
                Amount = target.Block
            });
            target.Block = 0;
        }

        public static int ResolveFinalBindPoisonStacks(BattleState state, CombatantState target, int defaultStacks)
        {
            if (state == null || target == null)
                return defaultStacks;

            var hasPoison = StatusRules.GetStatusStacks(target, StatusCatalog.Poison) > 0;
            var hasSlow = StatusRules.GetStatusStacks(target, StatusCatalog.Slow) > 0;
            return hasPoison && hasSlow ? FinalBindBonusPoisonStacks : defaultStacks;
        }

        static CombatantState PickRandomAliveEnemy(BattleState state, BattleRng rng)
        {
            if (state == null || rng == null)
                return null;

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
