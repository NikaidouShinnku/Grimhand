using System;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Effects
{
    public static class DamageRules
    {
        public static void ApplyDamage(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int power,
            CardType cardType,
            System.Collections.Generic.List<BattleEvent> events,
            bool canTriggerParry = true,
            string logSuffix = "")
        {
            if (target == null)
                return;

            var outgoing = PositionRules.GetDamageMultiplier(actor.Slot);
            var incoming = PositionRules.GetIncomingDamageMultiplier(target.Slot);
            var raw = (int)Math.Round(power * outgoing * incoming);
            var blocked = Math.Min(target.Block, raw);
            target.Block -= blocked;
            var hpDamage = raw - blocked;

            var reflectPower = 0;
            if (canTriggerParry && hpDamage > 0 && target.ActiveParry != null)
            {
                var stance = target.ActiveParry;
                target.ActiveParry = null;
                var beforeReduction = hpDamage;
                if (stance.DamageReductionPercent > 0)
                    hpDamage = (int)Math.Round(beforeReduction * (100 - stance.DamageReductionPercent) / 100f);
                if (stance.ReflectPercent > 0)
                    reflectPower = (int)Math.Round(power * stance.ReflectPercent / 100f);
            }

            var wasAlive = target.IsAlive;
            target.Hp = Math.Max(0, target.Hp - hpDamage);
            var killed = wasAlive && !target.IsAlive;

            events.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{actor.DisplayName} -> {target.DisplayName}{logSuffix}")
            {
                CombatantId = actor.Id,
                TargetId = target.Id,
                Amount = hpDamage,
                BlockedAmount = blocked,
                CardType = cardType
            });

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target.Id, killed, hpDamage);

            if (reflectPower > 0 && actor.IsAlive)
            {
                ApplyDamage(state, target, actor, reflectPower, cardType, events,
                    canTriggerParry: false, logSuffix: " (反射)");
            }

            if (killed)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, target.DisplayName)
                {
                    CombatantId = target.Id
                });
                CombatantDeathRules.OnCharacterDied(state, target, events);
            }
        }

        public static void ApplyBlock(CombatantState actor, int amount, System.Collections.Generic.List<BattleEvent> events)
        {
            actor.Block += amount;
            events.Add(new BattleEvent(BattleEventKind.BlockGained, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = amount
            });
        }

        public static void ApplyHeal(CombatantState actor, int amount, System.Collections.Generic.List<BattleEvent> events)
        {
            var before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + amount);
            var healed = actor.Hp - before;
            events.Add(new BattleEvent(BattleEventKind.HealApplied, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = healed
            });
        }
    }
}
