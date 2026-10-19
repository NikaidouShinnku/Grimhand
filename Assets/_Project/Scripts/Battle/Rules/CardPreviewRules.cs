using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// Target-independent card values and deterministic single-hit HP previews.
    /// Formation has no universal damage multiplier. Random dodges are not predicted.
    /// </summary>
    public static class CardPreviewRules
    {
        public static int ComputeExpectedDamage(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action)
        {
            if (action == null || action.Type != EffectActionType.DealDamage)
                return 0;

            var basePower = CardPowerRules.ComputeActionValue(action, owner);
            if (state == null || owner == null)
                return basePower;

            if (card != null)
                basePower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, basePower);

            var cardType = card?.CardType ?? CardType.Attack;
            var cost = card?.Cost ?? 0;
            var isSacrificeSelf = card != null
                && action.Target == EffectTarget.Self
                && card.Keywords != null
                && card.Keywords.Contains("sacrifice");

            return PeekOutgoingPower(state, owner, cardType, basePower, isSacrificeSelf, cost, card);
        }

        /// <summary>预览用：不消耗铁壁转化/复仇等一次性 flat bonus。</summary>
        static int PeekOutgoingPower(
            BattleState state,
            CombatantState owner,
            CardType cardType,
            int basePower,
            bool isSacrificeSelfDamage,
            int cost,
            CardInstanceState card = null)
        {
            if (owner == null)
            {
                return RelicBattleRules.ComputeOutgoingPower(
                    state, owner, cardType, basePower, isSacrificeSelfDamage, cost,
                    applyPositionMultiplier: false, card);
            }

            var ironWall = owner.TalentIronWallPendingDamageBonus;
            var revenge = owner.PendingRevengeAttackBonus;
            try
            {
                return RelicBattleRules.ComputeOutgoingPower(
                    state, owner, cardType, basePower, isSacrificeSelfDamage, cost,
                    applyPositionMultiplier: false, card);
            }
            finally
            {
                owner.TalentIronWallPendingDamageBonus = ironWall;
                owner.PendingRevengeAttackBonus = revenge;
            }
        }

        /// <summary>卡面基础伤害（不含遗物/状态 outgoing 加成）。</summary>
        public static int ComputeBaselineDamage(CombatantState owner, EffectActionSpec action) =>
            action == null || action.Type != EffectActionType.DealDamage
                ? 0
                : CardPowerRules.ComputeActionValue(action, owner);

        /// <summary>预期获得护甲（含强固、太极首防、法老加成等；铁壁转化时返回将转化的数值）。</summary>
        public static int ComputeExpectedBlock(
            BattleState state,
            CombatantState owner,
            EffectActionSpec action)
        {
            if (action == null || action.Type != EffectActionType.GainBlock)
                return 0;

            var block = CardPowerRules.ComputeActionValue(action, owner);
            if (state == null || owner == null)
                return block;

            block += RelicBattleRules.GetOutgoingDefenseFlatBonus(state.Config?.RunModifiers, owner);
            block = RelicBattleRules.ApplyPharaohBlockBonus(state.Config?.RunModifiers, owner, block);
            block = CombatModifierRules.ApplyBlockGainModifiers(owner, block);
            block = PassiveCardMechanicsRules.ApplyHeavyArmorBlockBonus(owner, block);
            return System.Math.Max(0, block);
        }

        public static int ComputeBaselineBlock(CombatantState owner, EffectActionSpec action) =>
            action == null || action.Type != EffectActionType.GainBlock
                ? 0
                : CardPowerRules.ComputeActionValue(action, owner);

        public static int ComputeExpectedHeal(
            BattleState state,
            CombatantState owner,
            EffectActionSpec action)
        {
            if (action == null || action.Type != EffectActionType.Heal)
                return 0;

            return RelicBattleRules.ApplyHealBonus(
                state?.Config?.RunModifiers, owner, CardPowerRules.ComputeActionValue(action, owner));
        }

        public static int ComputeBaselineHeal(CombatantState owner, EffectActionSpec action) =>
            ComputeExpectedHeal(null, owner, action);

        /// <summary>状态持续回合预览（含银月等 StatusDurationBonusTurns）。永久为 -1。</summary>
        public static int PreviewStatusDurationTurns(
            BattleState state,
            EffectActionSpec action,
            CombatantState owner = null)
        {
            if (action == null)
                return 1;

            // Duration < 0 is explicitly permanent, exactly as StatusRules applies it.
            var baseline = action.Duration;

            if (baseline < 0)
                return -1;

            if (action.StatusId == StatusCatalog.Poison && state != null && owner != null)
            {
                var adjusted = TalentBattleRules.AdjustPoisonDuration(state, owner, baseline);
                if (adjusted < 0)
                    return -1;
                baseline = adjusted;
            }

            if (baseline == 0)
                return 0;

            var bonus = state?.Config?.RunModifiers?.StatusDurationBonusTurns ?? 0;
            return baseline + bonus;
        }

        public static int BaselineStatusDurationTurns(EffectActionSpec action)
        {
            if (action == null)
                return 1;

            if (action.Duration >= 0)
                return action.Duration;

            return -1;
        }

        /// <summary>对指定目标预览最终 HP 伤害（含站位 incoming、护甲、DEF；不修改战斗状态）。</summary>
        public static int PreviewHpDamageAgainstTarget(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState intendedTarget)
        {
            if (state == null || owner == null || card == null || action == null || intendedTarget == null)
                return 0;

            if (action.Type != EffectActionType.DealDamage || action.Target == EffectTarget.Self)
                return 0;

            if (!CanPreviewDamageAgainstTarget(state, owner, card, action, intendedTarget))
                return 0;

            var value = CombatMechanicsRules.ComputeActionValueForTarget(state, action, owner, intendedTarget);
            var primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, intendedTarget, value);
            primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(
                state, action, intendedTarget, primaryPower, owner);
            primaryPower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, primaryPower);

            return PreviewHpDamageFromPower(
                state,
                owner,
                card,
                action,
                intendedTarget,
                primaryPower,
                isSacrificeSelfDamage: false);
        }

        public static bool CanPreviewDamageAgainstTarget(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState target)
        {
            if (card == null || action == null || target == null || !target.IsAlive)
                return false;

            if (action.Type != EffectActionType.DealDamage)
                return false;

            if (action.Target == EffectTarget.Self)
                return false;

            var victimTeam = ResolveVictimTeam(owner);

            if (action.Target == EffectTarget.AllEnemies)
                return target.Team == victimTeam;

            var pickSide = CardRules.GetRequiredTargetPick(card);
            if (pickSide == TargetPickSide.Enemy)
            {
                if (target.Team != victimTeam)
                    return false;

                // 已锁定目标：预览与结算一致，不因换位失去 Reach
                if (state?.ResolutionTargets != null
                    && state.ResolutionTargets.TryGetValue(card.InstanceId, out var lockedId)
                    && lockedId == target.Id)
                    return true;

                return state == null || owner == null
                    || TargetReachRules.CanPickUnit(state, card, target, owner);
            }

            if (pickSide == TargetPickSide.Ally)
                return owner != null && target.Team == owner.Team;

            if (action.Target == EffectTarget.DefaultEnemy)
                return target.Team == victimTeam;

            return false;
        }

        static TeamSide ResolveVictimTeam(CombatantState owner) =>
            owner?.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;

        public static bool CardUsesSingleTargetEnemyPreview(CardInstanceState card)
        {
            if (card == null)
                return false;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (action.Type != EffectActionType.DealDamage)
                    continue;

                if (action.Target == EffectTarget.AllEnemies || action.Target == EffectTarget.Self)
                    continue;

                var pickSide = CardRules.GetRequiredTargetPick(card);
                if (pickSide == TargetPickSide.Enemy || action.Target == EffectTarget.DefaultEnemy)
                    return true;
            }

            return false;
        }

        public static bool CardUsesAoeEnemyPreview(CardInstanceState card)
        {
            if (card == null)
                return false;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (action.Type == EffectActionType.DealDamage && action.Target == EffectTarget.AllEnemies)
                    return true;
            }

            return false;
        }

        public static List<(CombatantState target, int damage)> PreviewAoeDamagePerEnemy(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action)
        {
            var result = new List<(CombatantState, int)>();
            if (state == null || owner == null || card == null || action == null)
                return result;

            if (action.Type != EffectActionType.DealDamage || action.Target != EffectTarget.AllEnemies)
                return result;

            var enemyTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            foreach (var target in PositionRules.GetAliveSortedByPhysicalSlot(state, enemyTeam))
            {
                var value = CardPowerRules.ComputeActionValue(action, owner);
                var primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, value);
                primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(
                    state, action, target, primaryPower, owner);
                var hpDamage = PreviewHpDamageFromPower(
                    state,
                    owner,
                    card,
                    action,
                    target,
                    primaryPower,
                    isSacrificeSelfDamage: false);
                result.Add((target, hpDamage));
            }

            return result;
        }

        public static string FormatAoeDamagePerEnemy(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            EffectActionSpec aoeAction = null;
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (action.Type == EffectActionType.DealDamage && action.Target == EffectTarget.AllEnemies)
                {
                    aoeAction = action;
                    break;
                }
            }

            if (aoeAction == null)
                return "";

            var parts = PreviewAoeDamagePerEnemy(state, owner, card, aoeAction);
            if (parts.Count == 0)
                return "";

            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(parts[i].target.DisplayName).Append('(').Append(parts[i].damage).Append(')');
            }

            return sb.ToString();
        }

        public static string DescribeIntentEffect(BattleState state, CombatantState owner, CardInstanceState card)
        {
            if (card == null)
                return "";

            if (state == null || owner == null)
                return CardPowerRules.DescribeCardEffect(card, owner, false);

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                switch (action.Type)
                {
                    case EffectActionType.DealDamage:
                        if (action.Target == EffectTarget.AllEnemies)
                        {
                            var victimTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                            var parts = new List<(CombatantState target, int damage)>();
                            foreach (var target in PositionRules.GetAliveSortedByPhysicalSlot(state, victimTeam))
                            {
                                var dmg = PreviewHpDamageAgainstTarget(state, owner, card, action, target);
                                parts.Add((target, dmg));
                            }

                            if (parts.Count == 0)
                                return $"伤害 {ComputeExpectedDamage(state, owner, card, action)}";

                            if (parts.Count == 1)
                                return $"伤害 {parts[0].damage}";

                            var sb = new System.Text.StringBuilder("伤害 ");
                            for (var i = 0; i < parts.Count; i++)
                            {
                                if (i > 0)
                                    sb.Append(' ');
                                sb.Append(parts[i].target.DisplayName).Append('(').Append(parts[i].damage).Append(')');
                            }

                            return sb.ToString();
                        }

                        var predicted = TargetRules.PredictIntentTarget(state, owner, card);
                        if (predicted != null)
                            return $"伤害 {PreviewHpDamageAgainstTarget(state, owner, card, action, predicted)}";

                        return $"伤害 {ComputeExpectedDamage(state, owner, card, action)}";
                    case EffectActionType.GainBlock:
                    {
                        return $"护甲 {ComputeExpectedBlock(state, owner, action)}";
                    }
                    case EffectActionType.Heal:
                        return $"治疗 {ComputeExpectedHeal(state, owner, action)}";
                    case EffectActionType.ApplyStatus:
                        return $"状态 {action.Stacks}";
                    case EffectActionType.DrawCards:
                    case EffectActionType.DrawCardsNextTurn:
                        return $"抽牌 {action.Value}";
                }
            }

            return CardPowerRules.DescribeCardEffect(card, owner, false);
        }

        static int PreviewHpDamageFromPower(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState intendedTarget,
            int primaryPower,
            bool isSacrificeSelfDamage)
        {
            var recipient = CombatMechanicsRules.ResolveDamageRecipient(state, owner, intendedTarget);
            var redirectedByGuard = recipient.Id != intendedTarget.Id;
            if (!isSacrificeSelfDamage
                && (recipient.InvulnerableTurnsRemaining > 0 || recipient.InvulnerableRestOfTurn))
                return 0;

            var outgoing = PeekOutgoingPower(
                state,
                owner,
                card.CardType,
                primaryPower,
                isSacrificeSelfDamage,
                card.Cost,
                card);

            var ignore = System.Math.Max(action.IgnoreDefPercent,
                Grimhand.Battle.V091.V091MechanicsRules.GetIgnoreDefPercentForAttacker(owner));
            RelicEffectRules.AdjustRelicOutgoingDamage(state, owner, recipient, card.CardType, ref outgoing, ref ignore);
            var mods = state.Config?.RunModifiers;
            var previewBlock = recipient.Block;
            // The axe removes armor before this hit. Preview without mutating the
            // real target, and do not remove armor from a redirected recipient.
            if(card.DefinitionId=="b_axe_crush" && !redirectedByGuard)
                previewBlock-=System.Math.Max(0,intendedTarget.Block)/2;
            if (outgoing > 0 && recipient.BossFirstHitBlockPending
                && BossTraitRules.HasTrait(recipient, CharacterTraitCatalog.BossFirstHitBlock))
                previewBlock += PassiveCardMechanicsRules.ApplyHeavyArmorBlockBonus(recipient,
                    CombatModifierRules.ApplyBlockGainModifiers(recipient, CharacterTraitCatalog.BossFirstHitBlockAmount));
            if (outgoing > 0 && !isSacrificeSelfDamage && card.CardType == CardType.Attack
                && owner.Team != recipient.Team && recipient.WarriorFirstHitBlockPending
                && recipient.CharacterDefinitionId is RelicEffectRules.WarriorCharacterId or "char_warrior"
                && mods != null && mods.WarriorFirstHitBlockAmount > 0)
                previewBlock += PassiveCardMechanicsRules.ApplyHeavyArmorBlockBonus(recipient,
                    CombatModifierRules.ApplyBlockGainModifiers(recipient, mods.WarriorFirstHitBlockAmount));
            if (!isSacrificeSelfDamage && card.CardType == CardType.Attack
                && recipient.Team == TeamSide.Player && owner.Team != recipient.Team
                && state.TeamFirstHitReductionPending && recipient.FirstHitReductionPending
                && mods != null && mods.FirstHitDamageReductionPercent > 0)
                outgoing = System.Math.Max(0, (int)System.Math.Round(
                    outgoing * (100f - mods.FirstHitDamageReductionPercent) / 100f));
            var effectiveBlock = ignore <= 0 ? previewBlock : System.Math.Max(0,
                (int)System.Math.Round(previewBlock * (100 - System.Math.Min(100, ignore)) / 100f));
            var blocked = System.Math.Min(effectiveBlock, outgoing);
            var afterBlock = outgoing - blocked;

            var hpDamage = CombatModifierRules.ApplyIncomingDamageModifiers(
                recipient, afterBlock, ignore);

            if (redirectedByGuard)
                hpDamage = CombatMechanicsRules.ApplyGuardReduction(hpDamage, recipient);

            if (mods != null && previewBlock > 0
                && recipient.CharacterDefinitionId is RelicEffectRules.WarriorCharacterId or "char_warrior")
                hpDamage = (int)System.Math.Round(hpDamage * (100f - mods.WarriorBlockDamageReductionPercent) / 100f);
            if (mods != null && recipient.CharacterDefinitionId == RelicEffectRules.WarriorCharacterId
                && StatusRules.HasStatus(recipient, StatusCatalog.Taunt))
                hpDamage = (int)System.Math.Round(hpDamage * (100f - mods.WarriorTauntDamageReductionPercent) / 100f);
            hpDamage = Grimhand.Battle.Reactions.RespondEffectExecutor.ApplyMitigation(state, card.InstanceId, recipient.Id, hpDamage);
            if (hpDamage > 0 && StatusRules.HasStatus(recipient, StatusCatalog.Ethereal))
                hpDamage = recipient.CharacterDefinitionId == TalentBattleRules.LichQueenId
                    && TalentBattleRules.HasTalent(state, "talent_lich_s1_lv4") ? 0 : 1;
            if (StatusRules.HasStatus(recipient, StatusCatalog.LastStand))
                hpDamage = System.Math.Min(hpDamage, System.Math.Max(0, recipient.Hp - 1));

            return System.Math.Max(0, hpDamage);
        }
    }
}
