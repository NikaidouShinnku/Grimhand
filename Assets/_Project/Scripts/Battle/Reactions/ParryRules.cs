using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Reactions
{
    public static class ParryRules
    {
        public static bool TryReadParryConfig(CardInstanceState card, out int damageReductionPercent, out int reflectPercent)
        {
            damageReductionPercent = 0;
            reflectPercent = 0;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.LastActionAttackOnSelf)
                    continue;

                if (action.Type == EffectActionType.GainBlockFromLastDamagePercent)
                    damageReductionPercent = action.Value;
                else if (action.Type == EffectActionType.ReflectLastDamageToAttacker)
                    reflectPercent = action.Value;
            }

            return damageReductionPercent > 0 || reflectPercent > 0;
        }

        public static void Arm(CombatantState defender, int damageReductionPercent, int reflectPercent, List<BattleEvent> events)
        {
            defender.ActiveParry = new ParryStance
            {
                DamageReductionPercent = damageReductionPercent,
                ReflectPercent = reflectPercent
            };

            events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                $"弹反就绪 减伤{damageReductionPercent}% 反射{reflectPercent}%")
            {
                CombatantId = defender.Id
            });
        }

        public static void ClearAll(BattleState state)
        {
            foreach (var combatant in state.Combatants)
                combatant.ActiveParry = null;
        }
    }
}
