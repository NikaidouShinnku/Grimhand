using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// 卡牌面板预期数值：含攻击力缩放、遗物加成与攻击方站位 outgoing 倍率；
    /// 不含目标站位 incoming、护甲与选敌相关的 Reach/后排衰减。
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
            var isSacrifice = card != null && card.Keywords.Contains("sacrifice");

            return RelicBattleRules.ComputeOutgoingPower(
                state,
                owner,
                cardType,
                basePower,
                isSacrifice,
                cost,
                applyPositionMultiplier: true);
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

            var value = CardPowerRules.ComputeActionValue(action, owner);
            var primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, intendedTarget, value);
            primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, intendedTarget, primaryPower);
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
                primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, target, primaryPower);
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
                        var block = CardPowerRules.ComputeActionValue(action, owner);
                        block += RelicBattleRules.GetOutgoingDefenseFlatBonus(state.Config?.RunModifiers, owner);
                        block = RelicBattleRules.ApplyPharaohBlockBonus(state.Config?.RunModifiers, owner, block);
                        return $"护甲 {block}";
                    }
                    case EffectActionType.Heal:
                        return $"治疗 {CardPowerRules.ComputeActionValue(action, owner)}";
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

            var outgoing = RelicBattleRules.ComputeOutgoingPower(
                state,
                owner,
                card.CardType,
                primaryPower,
                isSacrificeSelfDamage,
                card.Cost,
                applyPositionMultiplier: true);

            var incoming = PositionRules.GetIncomingDamageMultiplier(
                PositionRules.GetEffectiveSlot(state, recipient));
            var raw = (int)System.Math.Round(outgoing * incoming);
            var blocked = System.Math.Min(recipient.Block, raw);
            var afterBlock = raw - blocked;

            var effectiveDef = CombatMechanicsRules.GetEffectiveDefense(state, recipient, action.IgnoreDefPercent);
            var hpDamage = CombatMechanicsRules.ComputeHpDamageAfterDefense(afterBlock, effectiveDef);

            if (redirectedByGuard)
                hpDamage = CombatMechanicsRules.ApplyGuardReduction(hpDamage);

            return System.Math.Max(0, hpDamage);
        }
    }
}
