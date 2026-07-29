using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class RelicBattleRules
    {
        public const string PharaohCharacterId = "char_mage";

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
            CombatModifierRules.RefreshCombatantModifiers(state, combatant, mods);
        }

        public static void ApplyTeamHpBonus(BattleState state, RunModifierSnapshot mods)
        {
            if (state == null)
                return;

            // TeamHpBonus（角斗士之盔等）已在远征 SyncPartyEffectiveMaxHp 写入队伍 MaxHp，
            // 开战 CombatantConfig.MaxHp 已含该加成；此处再加会变成 +16。
            // 局内新获遗物的 HP 增量由 BattleSession.SyncBattleRunModifiersFromExpeditionRelics 按差值补上。

            TalentBattleRules.ApplyTeamHpBonus(state, mods);
        }

        /// <summary>局内遗物 TeamHpBonus 变化时，把差值一次性同步到场上我方 MaxHp/Hp。</summary>
        public static void ApplyTeamHpBonusDelta(BattleState state, int hpDelta)
        {
            if (state == null || hpDelta == 0)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || combatant.Team != TeamSide.Player || !combatant.IsAlive)
                    continue;

                combatant.MaxHp = System.Math.Max(1, combatant.MaxHp + hpDelta);
                if (hpDelta > 0 && !combatant.EnteredFromExpeditionDeath)
                    combatant.Hp = System.Math.Min(combatant.MaxHp, combatant.Hp + hpDelta);
                else if (combatant.Hp > combatant.MaxHp)
                    combatant.Hp = combatant.MaxHp;
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
            bool isSacrificeDamage,
            int cardCost = 0)
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

            if (cardType == CardType.Attack
                && cardCost >= 3
                && mods.HighCostCardDamageBonusPercent > 0f)
            {
                mul *= 1f + mods.HighCostCardDamageBonusPercent / 100f;
            }

            return mul;
        }

        public static int GetOutgoingDamageFlatBonus(
            BattleState state,
            CombatantState actor,
            CardType cardType)
        {
            var mods = state?.Config?.RunModifiers;
            if (mods == null || actor == null || actor.Team != TeamSide.Player)
                return 0;

            var bonus = 0;

            if (cardType == CardType.Attack
                && mods.FirstAttackFlatBonus > 0
                && actor.FirstAttackBonusPending)
            {
                bonus += mods.FirstAttackFlatBonus;
            }

            if (cardType == CardType.Attack && actor.PendingRevengeAttackBonus > 0)
            {
                bonus += actor.PendingRevengeAttackBonus;
                actor.PendingRevengeAttackBonus = 0;
            }

            return bonus;
        }

        public static int GetOutgoingDefenseFlatBonus(
            RunModifierSnapshot mods,
            CombatantState actor)
        {
            if (mods == null || actor == null || actor.Team != TeamSide.Player)
                return 0;

            if (mods.FirstDefenseFlatBonus > 0 && actor.FirstDefenseBonusPending)
                return mods.FirstDefenseFlatBonus;

            return 0;
        }

        /// <summary>遗物倍率/固定加值 + 攻击方站位 outgoing；不含目标 incoming。</summary>
        public static int ComputeOutgoingPower(
            BattleState state,
            CombatantState actor,
            CardType cardType,
            int basePower,
            bool isSacrificeDamage,
            int cardCost,
            bool applyPositionMultiplier)
        {
            if (basePower <= 0)
                return 0;

            var mul = GetOutgoingDamageMultiplier(state, actor, cardType, isSacrificeDamage, cardCost);
            mul *= TalentBattleRules.GetOutgoingDamageMultiplier(state, actor, cardType);
            // 灵界专注：卡牌伤害也走非战斗增伤（灵能体仍只走 DoT 钩子，保持原行为）
            if (actor != null && actor.Team == TeamSide.Player)
                mul *= V09NewMechanicsRules.GetLichSpiritFocusDamageMultiplier(state, TeamSide.Player);
            var power = (int)System.Math.Round(basePower * mul);
            power += GetOutgoingDamageFlatBonus(state, actor, cardType);
            power += TalentBattleRules.GetOutgoingDamageFlatBonus(state, actor, cardType);

            if (power < 1)
                power = 1;

            power = CombatModifierRules.ApplyOutgoingDamageModifiers(actor, cardType, power);

            return power;
        }

        public static void MarkFirstAttackConsumed(BattleState state, CombatantState actor, CardType cardType)
        {
            if (actor == null || cardType != CardType.Attack)
                return;

            actor.NextAttackFlatBonus = 0;
            actor.FirstAttackBonusPending = false;

            var mods = state?.Config?.RunModifiers;
            if (mods != null && actor.Team == TeamSide.Player && mods.FirstPlayerAttackPending)
                mods.FirstPlayerAttackPending = false;
        }

        public static int ApplyPharaohBlockBonus(
            RunModifierSnapshot mods,
            CombatantState actor,
            int block)
        {
            if (mods == null || actor == null || block <= 0 || mods.PharaohBlockGivenBonusPercent <= 0f)
                return block;

            if (actor.CharacterDefinitionId != PharaohCharacterId)
                return block;

            return (int)System.Math.Round(block * (1f + mods.PharaohBlockGivenBonusPercent / 100f));
        }

        /// <summary>
        /// 圣骑之盾：本回合第一个受到攻击（攻击牌命中）的友方，该次伤害 -N%。
        /// 在扣甲前作用于 raw，使「受伤减少」覆盖打甲与掉血。
        /// </summary>
        public static int ApplyTeamFirstAttackDamageReduction(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            CardType cardType,
            bool isSacrificeDamage,
            int rawDamage)
        {
            if (state == null || target == null || rawDamage <= 0 || isSacrificeDamage)
                return rawDamage;

            if (cardType != CardType.Attack || target.Team != TeamSide.Player || !target.IsAlive)
                return rawDamage;

            if (actor != null && actor.Team == target.Team)
                return rawDamage;

            var mods = state.Config?.RunModifiers;
            if (mods == null
                || mods.FirstHitDamageReductionPercent <= 0f
                || !state.TeamFirstHitReductionPending
                || !target.FirstHitReductionPending)
                return rawDamage;

            target.FirstHitReductionPending = false;
            state.TeamFirstHitReductionPending = false;
            return System.Math.Max(
                0,
                (int)System.Math.Round(rawDamage * (100f - mods.FirstHitDamageReductionPercent) / 100f));
        }

        public static int ApplyHealBonus(RunModifierSnapshot mods, CombatantState healer, int amount)
        {
            if (mods == null || mods.HealBonusPercent <= 0f)
                return amount;

            if (healer == null || healer.CharacterDefinitionId != PharaohCharacterId)
                return amount;

            return (int)System.Math.Round(amount * (1f + mods.HealBonusPercent / 100f));
        }

        public static int ApplyIncomingDamageRelics(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int hpDamage,
            BattleRng rng,
            System.Collections.Generic.List<BattleEvent> events,
            int blockBeforeHit = -1)
        {
            if (target == null || hpDamage <= 0)
                return hpDamage;

            var mods = state?.Config?.RunModifiers;

            if (target.InvulnerableTurnsRemaining > 0 || target.InvulnerableRestOfTurn)
                return 0;

            if (MinionTraitRules.TryFirstHitDodge(state, target, rng, events))
                return 0;

            if (rng != null && RelicEffectRules.TryDodgeIncoming(state, mods, target, rng))
            {
                events?.Add(new BattleEvent(BattleEventKind.DamageApplied,
                    $"{target.DisplayName} 闪避")
                {
                    TargetId = target.Id,
                    Amount = 0
                });
                return 0;
            }

            // 圣骑之盾首击减伤已在 DamageRules 对 raw 结算（按「受到攻击」）

            var hadBlock = blockBeforeHit >= 0 ? blockBeforeHit > 0 : target.Block > 0;
            if (mods != null
                && mods.WarriorBlockDamageReductionPercent > 0f
                && hpDamage > 0
                && target.CharacterDefinitionId is RelicEffectRules.WarriorCharacterId or "char_warrior"
                && hadBlock)
            {
                hpDamage = (int)System.Math.Round(
                    hpDamage * (100f - mods.WarriorBlockDamageReductionPercent) / 100f);
            }

            if (mods != null
                && mods.WarriorTauntDamageReductionPercent > 0f
                && hpDamage > 0
                && target.CharacterDefinitionId == RelicEffectRules.WarriorCharacterId
                && StatusRules.HasStatus(target, StatusCatalog.Taunt))
            {
                hpDamage = (int)System.Math.Round(
                    hpDamage * (100f - mods.WarriorTauntDamageReductionPercent) / 100f);
            }

            if (target.WarriorFirstHitBlockPending
                && mods != null
                && mods.WarriorFirstHitBlockAmount > 0
                && target.CharacterDefinitionId == RelicEffectRules.WarriorCharacterId
                && hpDamage > 0)
            {
                target.WarriorFirstHitBlockPending = false;
                var block = System.Math.Min(hpDamage, mods.WarriorFirstHitBlockAmount);
                hpDamage -= block;
                DamageRules.ApplyBlock(target, block, events, state);
            }

            if (mods != null
                && mods.RevengeAttackFlatBonus > 0
                && target.Team == TeamSide.Player
                && hpDamage > 0)
            {
                target.PendingRevengeAttackBonus = mods.RevengeAttackFlatBonus;
            }

            RelicEffectRules.TryMiracleLeafSave(state, target, events, ref hpDamage);
            TalentBattleRules.TryMageRevive(state, target, events, ref hpDamage);
            hpDamage = TalentBattleRules.ApplyIncomingDamageTalents(state, target, hpDamage, events);

            return hpDamage;
        }

        public static void TryApplyStatusCardTeamBlock(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            System.Collections.Generic.List<BattleEvent> events)
        {
            var mods = state?.Config?.RunModifiers;
            if (mods == null || mods.StatusCardTeamBlock <= 0 || actor == null || card == null)
                return;

            if (card.CardType != CardType.Status)
                return;

            if (actor.CharacterDefinitionId != PharaohCharacterId)
                return;

            var amount = ApplyPharaohBlockBonus(mods, actor, mods.StatusCardTeamBlock);
            if (amount <= 0)
                return;

            foreach (var ally in state.Combatants)
            {
                if (ally.Team != TeamSide.Player || !ally.IsAlive)
                    continue;

                DamageRules.ApplyBlock(ally, amount, events, state);
            }

            // 勿再发 CombatantId=法老 的 BlockGained：foreach 已给全队各加一次，
            // 否则演出会把摘要事件再给法老叠一层护甲。
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{actor.DisplayName} 太阳金字塔：全队 +{amount} 护甲")
            {
                CombatantId = actor.Id,
                Amount = amount
            });
        }

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
