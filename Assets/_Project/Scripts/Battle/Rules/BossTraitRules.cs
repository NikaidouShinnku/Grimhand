using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public static class BossTraitRules
    {
        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                ResetTurnTraitFlags(combatant);

                if (HasTrait(combatant, CharacterTraitCatalog.BossTurnDefenseUp))
                    ApplyTurnDefenseGrowth(combatant, events);
            }

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive || combatant.Team != TeamSide.Enemy)
                    continue;

                if (StatusRules.HasStatus(combatant, StatusCatalog.BoneWorkshop))
                    SummonRules.TrySummonExplosiveSkull(state, combatant, events);
            }
        }

        public static void TryTriggerGhostQueenEnrage(
            BattleState state,
            CombatantState combatant,
            int hpBeforeDamage,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || !combatant.IsAlive)
                return;

            if (!HasTrait(combatant, CharacterTraitCatalog.GhostQueenEnrage))
                return;

            if (combatant.GhostQueenEnrageTriggered)
                return;

            if (hpBeforeDamage < CharacterTraitCatalog.GhostQueenEnrageHpThreshold)
                return;

            if (combatant.Hp >= CharacterTraitCatalog.GhostQueenEnrageHpThreshold)
                return;

            combatant.GhostQueenEnrageTriggered = true;
            StatusRules.ApplyStatus(
                state,
                combatant,
                StatusCatalog.Ethereal,
                1,
                1,
                events);
            BossBonusHandRules.QueueBonusHandNextTurn(
                state,
                combatant.Id,
                CharacterTraitCatalog.GhostQueenWrathCardId);
            events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                $"{combatant.DisplayName} 虚化并准备「幽灵女王之怒」")
            {
                CombatantId = combatant.Id
            });
        }

        public static void TryApplyFirstHitBlock(
            BattleState state,
            CombatantState recipient,
            List<BattleEvent> events)
        {
            if (recipient == null || !recipient.IsAlive)
                return;

            if (!HasTrait(recipient, CharacterTraitCatalog.BossFirstHitBlock))
                return;

            if (!recipient.BossFirstHitBlockPending)
                return;

            recipient.BossFirstHitBlockPending = false;
            DamageRules.ApplyBlock(
                recipient,
                CharacterTraitCatalog.BossFirstHitBlockAmount,
                events);
        }

        public static bool HasTrait(CombatantState combatant, string traitId)
        {
            if (combatant?.Traits == null || string.IsNullOrEmpty(traitId))
                return false;

            foreach (var trait in combatant.Traits)
            {
                if (trait == traitId)
                    return true;
            }

            return false;
        }

        static void ResetTurnTraitFlags(CombatantState combatant)
        {
            if (HasTrait(combatant, CharacterTraitCatalog.BossFirstHitBlock))
                combatant.BossFirstHitBlockPending = true;
        }

        static void ApplyTurnDefenseGrowth(CombatantState combatant, List<BattleEvent> events)
        {
            combatant.BaseDefense += 1;
            CombatantRules.RefreshDerivedStats(combatant);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{combatant.DisplayName} 防御 +1")
            {
                CombatantId = combatant.Id,
                Amount = combatant.Defense
            });
        }
    }
}
