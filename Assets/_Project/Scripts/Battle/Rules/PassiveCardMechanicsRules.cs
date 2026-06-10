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
        public const int GodDescendsFlatDamage = 5;
        public const int GodDescendsAttackScalePercent = 120;
        public const int FinalBloodRitualDraw = 1;
        public const int FinalBloodRitualHeal = 5;

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
    }
}
