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
                    events.Add(new BattleEvent(BattleEventKind.BlockGained,
                        $"{actor.DisplayName} 应对减伤 {action.Value}%")
                    {
                        CombatantId = actor.Id,
                        CardInstanceId = card.InstanceId
                    });
                    break;

                case EffectActionType.ReflectLastDamageToAttacker:
                    var attacker = state.GetCombatant(context.EnemyCombatantId);
                    if (attacker == null || !attacker.IsAlive || incomingPower <= 0)
                        break;

                    var reflected = incomingPower * action.Value / 100;
                    if (reflected <= 0)
                        break;

                    events.Add(new BattleEvent(BattleEventKind.ParryTriggered,
                        $"{actor.DisplayName} 应对反击 {attacker.DisplayName}")
                    {
                        CombatantId = actor.Id,
                        TargetId = attacker.Id,
                        Amount = reflected,
                        CardInstanceId = card.InstanceId
                    });

                    DamageRules.ApplyDamage(
                        state,
                        actor,
                        attacker,
                        reflected,
                        card.CardType,
                        events,
                        canTriggerParry: false,
                        rng: rng,
                        logSuffix: " (应对反击)",
                        sourceCardInstanceId: card.InstanceId);
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
    }
}
