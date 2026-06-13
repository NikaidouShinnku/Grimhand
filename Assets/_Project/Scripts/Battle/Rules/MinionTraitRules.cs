using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class MinionTraitRules
    {
        public static bool HasTrait(CombatantState combatant, string traitId)
        {
            if (combatant == null || string.IsNullOrEmpty(traitId))
                return false;

            return combatant.Traits.Contains(traitId);
        }

        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                if (HasTrait(combatant, MinionTraitCatalog.SlimeRegen) && !combatant.TookDamageLastTurn)
                    DamageRules.ApplyHeal(state, combatant, MinionTraitCatalog.SlimeRegenAmount, events);

                combatant.TookDamageLastTurn = false;
                combatant.FirstHitDodgePending = HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge);
                combatant.InvulnerableRestOfTurn = false;
                combatant.RespondArmedThisTurn = false;
                combatant.DodgeChanceBonus = 0f;
            }

            state.EnemyAttackCardsPlayedThisTurn = 0;
        }

        public static void OnDamageTaken(
            BattleState state,
            CombatantState recipient,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (recipient == null || hpDamage <= 0)
                return;

            recipient.TookDamageLastTurn = true;

            if (HasTrait(recipient, MinionTraitCatalog.OgreBloodRage))
            {
                recipient.BloodRageStacks = System.Math.Min(
                    MinionTraitCatalog.OgreBloodRageMaxStacks,
                    recipient.BloodRageStacks + 1);
            }

            TryTriggerWraithEliteEnrage(state, recipient, events);
            RefreshLowHpSpeed(state, recipient);
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || card == null || !actor.IsAlive)
                return;

            if (actor.Team == TeamSide.Enemy && card.CardType == CardType.Attack)
                state.EnemyAttackCardsPlayedThisTurn++;

            actor.CardsResolvedCount++;
            if (actor.CardsResolvedCount % MinionTraitCatalog.CardsPerStatBonus != 0)
                return;

            if (HasTrait(actor, MinionTraitCatalog.SkeletonCardDef))
            {
                actor.BaseDefense += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 防御")
                {
                    CombatantId = actor.Id
                });
            }

            if (HasTrait(actor, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                actor.BaseDefense += 1;
                actor.BaseAttack += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 攻击 +1 防御")
                {
                    CombatantId = actor.Id
                });
            }
        }

        public static int ApplyBloodRageOutgoingBonus(CombatantState actor, CardType cardType, int power)
        {
            if (actor == null || cardType != CardType.Attack || power <= 0)
                return power;

            if (!HasTrait(actor, MinionTraitCatalog.OgreBloodRage) || actor.BloodRageStacks <= 0)
                return power;

            var bonusPercent = actor.BloodRageStacks * MinionTraitCatalog.OgreBloodRageDamagePercentPerStack;
            var boosted = (int)System.Math.Round(power * (100 + bonusPercent) / 100f);
            return System.Math.Max(1, boosted);
        }

        public static void ConsumeBloodRageAfterAttack(CombatantState actor, CardType cardType)
        {
            if (actor == null || cardType != CardType.Attack)
                return;

            if (!HasTrait(actor, MinionTraitCatalog.OgreBloodRage))
                return;

            actor.BloodRageStacks = 0;
        }

        public static bool TryFirstHitDodge(
            BattleState state,
            CombatantState target,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (target == null || rng == null || !target.FirstHitDodgePending)
                return false;

            if (!HasTrait(target, MinionTraitCatalog.BatFirstHitDodge))
                return false;

            target.FirstHitDodgePending = false;
            var roll = rng.NextUInt() % 1000u / 1000f;
            if (roll >= MinionTraitCatalog.BatFirstHitDodgeChance)
                return false;

            events?.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{target.DisplayName} 闪避")
            {
                TargetId = target.Id,
                Amount = 0
            });
            return true;
        }

        public static void RefreshLowHpSpeed(BattleState state, CombatantState combatant)
        {
            if (combatant == null || !combatant.IsAlive || combatant.MaxHp <= 0)
                return;

            var belowHalf = combatant.Hp * 100 / combatant.MaxHp < 50;
            var wantsBonus = belowHalf
                && (HasTrait(combatant, MinionTraitCatalog.WraithLowHpSpeed)
                    || HasTrait(combatant, MinionTraitCatalog.WraithEliteLowHpEthereal));

            var desiredBonus = wantsBonus ? MinionTraitCatalog.WraithLowHpSpeedBonus : 0;
            if (desiredBonus == combatant.LowHpSpeedBonusApplied)
                return;

            combatant.Speed += desiredBonus - combatant.LowHpSpeedBonusApplied;
            combatant.LowHpSpeedBonusApplied = desiredBonus;
        }

        static void TryTriggerWraithEliteEnrage(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || !combatant.IsAlive)
                return;

            if (!HasTrait(combatant, MinionTraitCatalog.WraithEliteLowHpEthereal)
                || combatant.WraithEliteEnrageTriggered
                || combatant.MaxHp <= 0)
                return;

            if (combatant.Hp * 100 / combatant.MaxHp >= 50)
                return;

            combatant.WraithEliteEnrageTriggered = true;
            StatusRules.ApplyStatus(state, combatant, StatusCatalog.Ethereal, 1, 1, events);
            RefreshLowHpSpeed(state, combatant);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{combatant.DisplayName} 虚化")
            {
                CombatantId = combatant.Id
            });
        }
    }
}
