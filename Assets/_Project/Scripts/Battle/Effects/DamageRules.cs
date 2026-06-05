using System;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Core;

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
            bool isSacrificeDamage = false,
            BattleRng rng = null,
            string logSuffix = "")
        {
            if (target == null)
                return;

            var relicMul = RelicBattleRules.GetOutgoingDamageMultiplier(state, actor, cardType, isSacrificeDamage);
            var adjustedPower = (int)Math.Round(power * relicMul);
            if (adjustedPower < 1 && power > 0)
                adjustedPower = 1;

            RelicBattleRules.MarkFirstAttackConsumed(state, actor, cardType);

            var outgoing = PositionRules.GetDamageMultiplier(PositionRules.GetEffectiveSlot(state, actor));
            var incoming = PositionRules.GetIncomingDamageMultiplier(PositionRules.GetEffectiveSlot(state, target));
            var raw = (int)Math.Round(adjustedPower * outgoing * incoming);
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
                    reflectPower = (int)Math.Round(adjustedPower * stance.ReflectPercent / 100f);
            }

            if (hpDamage > 0 && rng != null)
            {
                var mods = state.Config?.RunModifiers;
                if (RelicBattleRules.TryWarriorBlockOnHit(target, mods, rng))
                {
                    var relicBlock = Math.Min(hpDamage, mods.WarriorBlockAmountOnHit);
                    hpDamage -= relicBlock;
                    blocked += relicBlock;
                    events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{target.DisplayName} 不动明王格挡")
                    {
                        CombatantId = target.Id,
                        Amount = relicBlock
                    });
                }
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
                events.Add(new BattleEvent(BattleEventKind.ParryTriggered,
                    $"{target.DisplayName} 弹反击退 {actor.DisplayName}")
                {
                    CombatantId = target.Id,
                    TargetId = actor.Id,
                    Amount = reflectPower
                });

                ApplyDamage(state, target, actor, reflectPower, cardType, events,
                    canTriggerParry: false, rng: rng, logSuffix: " (反射)");
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

        public static void ApplyHeal(
            BattleState state,
            CombatantState actor,
            int amount,
            System.Collections.Generic.List<BattleEvent> events)
        {
            var mods = state?.Config?.RunModifiers;
            var boosted = RelicBattleRules.ApplyHealBonus(mods, amount);
            var before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + boosted);
            var healed = actor.Hp - before;
            events.Add(new BattleEvent(BattleEventKind.HealApplied, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = healed
            });

            if (healed > 0 && mods != null && mods.HealGrantsBlock > 0)
                ApplyBlock(actor, mods.HealGrantsBlock, events);
        }
    }
}
