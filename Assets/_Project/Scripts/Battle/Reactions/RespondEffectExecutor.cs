using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle.Reactions
{
    public static class RespondEffectExecutor
    {
        public static void Execute(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            RespondTriggerContext context,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var attacker = state.GetCombatant(context.EnemyCombatantId);
            var enemyCard = state.GetCard(context.EnemyCardInstanceId);
            var incomingPower = RespondTriggerMatcher.EstimateIncomingPower(state, attacker, enemyCard, actor);

            var triggered = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (!ReactionRules.MeetsRespondCondition(state, action.Condition, actor.Id,
                        new ResolutionStep(context.EnemyCombatantId, context.EnemyCardInstanceId, 0)))
                    continue;

                triggered = true;
                ExecuteAction(state, actor, card, action, context, incomingPower, events, rng);
            }

            if (triggered)
            {
                events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, card.DisplayName)
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                TalentBattleRules.OnRespondSuccess(state, actor);
            }
        }

        static void ExecuteAction(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            RespondTriggerContext context,
            int incomingPower,
            List<BattleEvent> events,
            BattleRng rng)
        {
            switch (action.Type)
            {
                case EffectActionType.GainBlockFromLastDamagePercent:
                    RegisterMitigation(state, context.EnemyCardInstanceId, actor.Id, action.Value);
                    break;

                case EffectActionType.ReflectLastDamageToAttacker:
                    var attacker = state.GetCombatant(context.EnemyCombatantId);
                    if (attacker == null || !attacker.IsAlive || incomingPower <= 0)
                        break;

                    var reflected = incomingPower * action.Value / 100;
                    if (reflected <= 0)
                        break;

                    state.PendingParryStrikes.Add(new PendingParryStrike
                    {
                        TriggerEnemyCardInstanceId = context.EnemyCardInstanceId,
                        DefenderId = actor.Id,
                        AttackerId = attacker.Id,
                        Damage = reflected,
                        RespondCardInstanceId = card.InstanceId,
                        RespondCardType = card.CardType
                    });
                    break;
            }
        }

        public static void RegisterMitigation(
            BattleState state,
            int enemyCardInstanceId,
            string targetCombatantId,
            int reductionPercent)
        {
            if (reductionPercent <= 0)
                return;

            if (!state.RespondMitigationByEnemyCard.TryGetValue(enemyCardInstanceId, out var layers))
            {
                layers = new List<RespondMitigationLayer>();
                state.RespondMitigationByEnemyCard[enemyCardInstanceId] = layers;
            }

            layers.Add(new RespondMitigationLayer
            {
                TargetCombatantId = targetCombatantId,
                DamageReductionPercent = reductionPercent,
                ResponderCombatantId = targetCombatantId
            });
        }

        public static void ResolvePendingParriesForEnemyCard(
            BattleState state,
            int enemyCardInstanceId,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || events == null || enemyCardInstanceId <= 0)
                return;

            for (var i = state.PendingParryStrikes.Count - 1; i >= 0; i--)
            {
                var pending = state.PendingParryStrikes[i];
                if (pending.TriggerEnemyCardInstanceId != enemyCardInstanceId)
                    continue;

                state.PendingParryStrikes.RemoveAt(i);

                var defender = state.GetCombatant(pending.DefenderId);
                var attacker = state.GetCombatant(pending.AttackerId);
                if (defender == null || attacker == null || !defender.IsAlive || pending.Damage <= 0)
                    continue;

                events.Add(new BattleEvent(BattleEventKind.PortraitPoseChanged, defender.DisplayName)
                {
                    CombatantId = defender.Id,
                    CardType = CardType.Attack,
                    CardInstanceId = pending.RespondCardInstanceId
                });

                events.Add(new BattleEvent(BattleEventKind.ParryTriggered,
                    $"{defender.DisplayName} 应对反击 {attacker.DisplayName}")
                {
                    CombatantId = defender.Id,
                    TargetId = attacker.Id,
                    Amount = pending.Damage,
                    CardInstanceId = pending.RespondCardInstanceId
                });

                DamageRules.ApplyDamage(
                    state,
                    defender,
                    attacker,
                    pending.Damage,
                    pending.RespondCardType,
                    events,
                    canTriggerParry: false,
                    rng: rng,
                    logSuffix: " (应对反击)",
                    sourceCardInstanceId: pending.RespondCardInstanceId);

                events.Add(new BattleEvent(BattleEventKind.PortraitIdleRestored, defender.DisplayName)
                {
                    CombatantId = defender.Id
                });
            }
        }

        public static int ApplyMitigation(
            BattleState state,
            int sourceCardInstanceId,
            string targetCombatantId,
            int hpDamage)
        {
            if (hpDamage <= 0
                || sourceCardInstanceId <= 0
                || !state.RespondMitigationByEnemyCard.TryGetValue(sourceCardInstanceId, out var layers))
                return hpDamage;

            var result = hpDamage;
            foreach (var layer in layers)
            {
                if (layer.TargetCombatantId != targetCombatantId || layer.DamageReductionPercent <= 0)
                    continue;

                result = (int)System.Math.Round(result * (100 - layer.DamageReductionPercent) / 100f);
            }

            return System.Math.Max(0, result);
        }

        public static bool HasRespondDefenseForHit(
            BattleState state,
            int sourceCardInstanceId,
            string targetCombatantId)
        {
            if (state == null || sourceCardInstanceId <= 0 || string.IsNullOrEmpty(targetCombatantId))
                return false;

            if (state.RespondMitigationByEnemyCard.TryGetValue(sourceCardInstanceId, out var layers))
            {
                foreach (var layer in layers)
                {
                    if (layer.TargetCombatantId == targetCombatantId)
                        return true;
                }
            }

            foreach (var pending in state.PendingParryStrikes)
            {
                if (pending.TriggerEnemyCardInstanceId == sourceCardInstanceId
                    && pending.DefenderId == targetCombatantId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
