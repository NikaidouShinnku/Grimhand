using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class RelicBattleRules
    {
        public static void RefreshAllDerivedStats(BattleState state)
        {
            if (state == null)
                return;

            var mods = state.Config?.RunModifiers;
            foreach (var combatant in state.Combatants)
                RefreshDerivedStats(state, combatant, mods);
        }

        public static void RefreshDerivedStats(
            BattleState state,
            CombatantState combatant,
            RunModifierSnapshot mods)
        {
            CombatantRules.RefreshDerivedStats(combatant);
            if (mods == null || combatant == null || !combatant.IsAlive)
                return;

            if (combatant.Team == TeamSide.Player)
            {
                if (mods.TeamAttackBonus != 0)
                    combatant.Attack += mods.TeamAttackBonus;

                var effective = state != null
                    ? PositionRules.GetEffectiveSlot(state, combatant)
                    : combatant.Slot;

                if (mods.FrontDefenseBonus != 0 && effective == FormationSlot.Front)
                    combatant.Defense += mods.FrontDefenseBonus;
            }
        }

        public static int GetBackRowExtraDraw(
            BattleState state,
            CombatantState combatant,
            RunModifierSnapshot mods)
        {
            if (mods == null || combatant == null || combatant.Team != TeamSide.Player)
                return 0;

            if (mods.BackRowExtraDrawPerTurn <= 0)
                return 0;

            var effective = state != null
                ? PositionRules.GetEffectiveSlot(state, combatant)
                : combatant.Slot;

            return effective == FormationSlot.Back ? mods.BackRowExtraDrawPerTurn : 0;
        }

        public static float GetOutgoingDamageMultiplier(
            BattleState state,
            CombatantState actor,
            CardType cardType,
            bool isSacrificeDamage)
        {
            var mods = state.Config?.RunModifiers;
            if (mods == null || actor == null)
                return 1f;

            var mul = 1f;

            if (isSacrificeDamage && mods.SacrificeDamageBonusPercent > 0f)
                mul *= 1f + mods.SacrificeDamageBonusPercent / 100f;

            if (cardType == CardType.Attack
                && actor.Team == TeamSide.Player
                && mods.FirstPlayerAttackPending
                && mods.FirstAttackDamageBonusPercent > 0f)
            {
                mul *= 1f + mods.FirstAttackDamageBonusPercent / 100f;
            }

            return mul;
        }

        public static void MarkFirstAttackConsumed(BattleState state, CombatantState actor, CardType cardType)
        {
            var mods = state.Config?.RunModifiers;
            if (mods == null || actor == null || actor.Team != TeamSide.Player)
                return;

            if (cardType == CardType.Attack && mods.FirstPlayerAttackPending)
                mods.FirstPlayerAttackPending = false;
        }

        public static int ApplyHealBonus(RunModifierSnapshot mods, int amount) =>
            mods == null || mods.HealBonusPercent <= 0f
                ? amount
                : (int)System.Math.Round(amount * (1f + mods.HealBonusPercent / 100f));

        public static bool TryWarriorBlockOnHit(CombatantState target, RunModifierSnapshot mods, BattleRng rng)
        {
            if (mods == null || target == null || mods.WarriorBlockAmountOnHit <= 0 || rng == null)
                return false;

            if (target.CharacterDefinitionId is not ("char_knight" or "char_warrior"))
                return false;

            if (mods.WarriorBlockChanceOnHit <= 0f)
                return false;

            var roll = rng.NextUInt() % 1000u / 1000f;
            return roll < mods.WarriorBlockChanceOnHit;
        }
    }
}
