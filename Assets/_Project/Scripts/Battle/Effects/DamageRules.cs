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
            string logSuffix = "",
            int cardCost = 0,
            int ignoreDefPercent = 0,
            bool redirectedByGuard = false,
            int sourceCardInstanceId = 0,
            bool isAoEWave = false)
        {
            if (target == null)
                return;

            var recipient = CombatMechanicsRules.ResolveDamageRecipient(state, actor, target);
            var isGuardRedirect = recipient.Id != target.Id;
            if (isGuardRedirect)
                redirectedByGuard = true;

            var outgoingPower = RelicBattleRules.ComputeOutgoingPower(
                state,
                actor,
                cardType,
                power,
                isSacrificeDamage,
                cardCost,
                applyPositionMultiplier: true);

            RelicBattleRules.MarkFirstAttackConsumed(state, actor, cardType);

            var incoming = PositionRules.GetIncomingDamageMultiplier(PositionRules.GetEffectiveSlot(state, recipient));
            var raw = (int)Math.Round(outgoingPower * incoming);
            var blocked = Math.Min(recipient.Block, raw);
            recipient.Block -= blocked;
            var afterBlock = raw - blocked;

            var effectiveDef = CombatMechanicsRules.GetEffectiveDefense(state, recipient, ignoreDefPercent);
            var hpDamage = CombatMechanicsRules.ComputeHpDamageAfterDefense(afterBlock, effectiveDef);

            if (redirectedByGuard)
                hpDamage = CombatMechanicsRules.ApplyGuardReduction(hpDamage);

            hpDamage = RelicBattleRules.ApplyIncomingDamageRelics(
                state, actor, recipient, hpDamage, rng, events);

            var beforeRespondMitigation = hpDamage;
            hpDamage = RespondEffectExecutor.ApplyMitigation(
                state, sourceCardInstanceId, recipient.Id, hpDamage);
            var respondMitigated = beforeRespondMitigation - hpDamage;

            if (hpDamage > 0 && rng != null)
            {
                var mods = state.Config?.RunModifiers;
                if (RelicBattleRules.TryWarriorBlockOnHit(recipient, mods, rng))
                {
                    var relicBlock = Math.Min(hpDamage, mods.WarriorBlockAmountOnHit);
                    hpDamage -= relicBlock;
                    blocked += relicBlock;
                    events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{recipient.DisplayName} 不动明王格挡")
                    {
                        CombatantId = recipient.Id,
                        Amount = relicBlock
                    });
                }
            }

            if (hpDamage > 0)
                recipient.HitThisTurn = true;

            var wasAlive = recipient.IsAlive;
            if (hpDamage > 0)
                recipient.Hp = Math.Max(0, recipient.Hp - hpDamage);

            if (!recipient.IsAlive && wasAlive
                && CombatMechanicsRules.TryPreventDeathWithReviveBlessing(state, recipient, events))
            {
                wasAlive = true;
            }

            var killed = wasAlive && !recipient.IsAlive;

            events.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{actor.DisplayName} -> {recipient.DisplayName}{logSuffix}")
            {
                CombatantId = actor.Id,
                TargetId = recipient.Id,
                Amount = hpDamage,
                BlockedAmount = blocked,
                RespondMitigatedAmount = respondMitigated,
                IsSacrificeDamage = isSacrificeDamage,
                IsAoEWave = isAoEWave,
                CardType = cardType,
                CardInstanceId = sourceCardInstanceId
            });

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, recipient.Id, killed, hpDamage);

            if (hpDamage > 0)
                CombatMechanicsRules.TryTriggerUnyielding(state, recipient, events);

            if (killed)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, recipient.DisplayName)
                {
                    CombatantId = recipient.Id
                });
                CombatantDeathRules.OnCharacterDied(state, recipient, events);

                if (recipient.Team == TeamSide.Enemy && actor != null)
                    RelicEffectRules.OnEnemyKilled(state, actor, events, rng);
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
            System.Collections.Generic.List<BattleEvent> events,
            CombatantState healer = null)
        {
            if (actor == null || !actor.IsAlive)
                return;

            var mods = state?.Config?.RunModifiers;
            var boosted = RelicBattleRules.ApplyHealBonus(mods, healer ?? actor, amount);
            var before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + boosted);
            var healed = actor.Hp - before;
            if (healed <= 0)
                return;

            events.Add(new BattleEvent(BattleEventKind.HealApplied, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = healed
            });

            if (mods != null && mods.HealGrantsBlock > 0)
                ApplyBlock(actor, mods.HealGrantsBlock, events);
        }

        public static void ApplyRevive(
            BattleState state,
            CombatantState actor,
            int amount,
            System.Collections.Generic.List<BattleEvent> events,
            CombatantState healer = null)
        {
            if (actor == null || actor.IsAlive)
                return;

            var mods = state?.Config?.RunModifiers;
            var boosted = RelicBattleRules.ApplyHealBonus(mods, healer ?? actor, amount);
            var restored = Math.Max(1, Math.Min(actor.MaxHp, boosted));
            actor.Hp = restored;

            CombatantDeathRules.RestoreUsableCards(state, actor);
            RelicBattleRules.RefreshDerivedStats(state, actor, mods);

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = restored
            });

            if (mods != null && mods.HealGrantsBlock > 0)
                ApplyBlock(actor, mods.HealGrantsBlock, events);
        }
    }
}
