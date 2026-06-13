using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Battle
{
    public struct ActionOrderVisualEntry
    {
        public int OrderIndex;
        public CardInstanceState Card;
        public bool IsHidden;
        public string DisplayName;
    }

    /// <summary>战斗 UI 文本格式化。</summary>
    public static class BattleUiFormatters
    {
        /// <summary>
        /// 牌面描述规则：
        /// 1. 第一行：机制关键词（【献祭8】【AOE】【应对攻击】等）。
        /// 2. 效果行：位置标签 + 白话效果，如「【前/中】造成 6 点伤害，回复等量 HP」。
        /// 3. 献祭自伤只在关键词行体现；需点选且无位置标签时写「选择一名敌人/队友」。
        /// 4. 悬停框：位置说明 + 关键词 + 公式（见 CardKeywordTooltipBuilder）。
        /// </summary>
        public static string SlotLabel(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Front: return "前排";
                case FormationSlot.Middle: return "中排";
                case FormationSlot.Back: return "后排";
                default: return slot.ToString();
            }
        }

        public static string ShortOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return "?";
            if (ownerId.StartsWith("char_"))
                return ownerId.Substring(5);
            return ownerId;
        }

        public static string FormatUnitLine(CombatantState unit)
        {
            var status = FormatStatusList(unit);
            var core = $"{unit.DisplayName}\nHP {unit.Hp}/{unit.MaxHp}  攻{unit.Attack}  速{StatusRules.GetEffectiveSpeed(unit)}";
            return string.IsNullOrEmpty(status) ? core : core + "\n" + status;
        }

        public static string FormatStatusList(CombatantState unit)
        {
            if (unit.Statuses.Count == 0)
                return "";

            var sb = new StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (i > 0) sb.Append(' ');
                sb.Append($"{s.StatusId}x{s.Stacks}");
            }

            return sb.ToString();
        }

        public static string DescribeReach(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Type == EffectActionType.DealDamage)
                    return DescribeReachLabel(action);
            }

            return "";
        }

        public static string BuildCardStatsLine(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            bool preferFormulas = false,
            CombatantState damagePreviewTarget = null)
        {
            if (card == null)
                return "";

            var ownerId = state != null ? PositionRules.GetOwnerCombatantId(state, card) : null;
            var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
            if (TryBuildCurseCardStatsLine(card, out var curseLine))
                return curseLine;

            var pickSide = CardRules.GetRequiredTargetPick(card);
            var previewTarget = ResolveDamagePreviewTarget(state, draft, card, owner, damagePreviewTarget);

            var lines = new List<string>();
            var keywordLine = CardFaceKeywordFormatter.Build(card, owner, state);
            if (!string.IsNullOrEmpty(keywordLine))
                lines.Add(keywordLine);

            var normalActions = CollectNormalActions(card);
            var remaining = normalActions;

            if (TryDescribeRepeatedRandomHits(card, normalActions, owner, state, preferFormulas, out var randomHitsLine, out remaining))
            {
                lines.Add(randomHitsLine);
                normalActions = remaining;
            }
            else if (TryDescribeAoEEnemyDamage(state, card, normalActions, owner, preferFormulas, out var aoeLine, out remaining))
            {
                lines.Add(aoeLine);
                normalActions = remaining;
            }
            else if (TryDescribeAllAllyTeamEffect(normalActions, owner, preferFormulas, out var allyLine, out remaining))
            {
                lines.Add(allyLine);
                normalActions = remaining;
            }
            else
            {
                normalActions = remaining;
            }

            var picked = new List<string>();
            var other = new List<string>();
            foreach (var action in normalActions)
            {
                if (ShouldOmitFromCardFace(card, action))
                    continue;

                var usesPick = UsesManualPick(action, pickSide);
                var clause = DescribeEffectClause(
                    action, owner, usesPick, pickSide, state, card, reaction: false, preferFormulas, previewTarget);
                if (string.IsNullOrEmpty(clause))
                    continue;

                if (usesPick)
                    picked.Add(clause);
                else
                    other.Add(clause);
            }

            if (picked.Count > 0)
            {
                var body = JoinEffectClauses(CollapseDuplicateClauses(picked));
                if (pickSide != TargetPickSide.None && !HasReachTagOnPickActions(card, pickSide))
                {
                    var lead = BuildPickLead(pickSide);
                    lines.Add(string.IsNullOrEmpty(lead) ? body : $"{lead}，{body}");
                }
                else
                    lines.Add(body);
            }

            lines.AddRange(CollapseDuplicateClauses(other));

            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                var clause = DescribeEffectClause(action, owner, usesPick: false, pickSide, state, card, reaction: true, preferFormulas);
                if (!string.IsNullOrEmpty(clause))
                    lines.Add(clause);
            }

            var assignedId = draft?.GetAssignedTarget(card.InstanceId);
            if (!string.IsNullOrEmpty(assignedId))
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null)
                    lines.Add($"→ {assigned.DisplayName}");
            }

            if (card.DefinitionId == PassiveCardMechanicsRules.EndlessBladeCardId)
                lines.Add("使用后此牌伤害在本场战斗中翻倍");

            return string.Join("\n", lines);
        }

        public static string BuildCardStatsLinePreview(CardInstanceState card) =>
            BuildCardStatsLine(state: null, draft: null, card, preferFormulas: true);

        public static string BuildCardStatsLinePreview(
            CardInstanceState card,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions) =>
            BuildCardStatsLine(
                state: null,
                draft: null,
                CardVisualResolver.ResolveForDescription(card, definitions),
                preferFormulas: true);

        public static string BuildCardKeywordTooltip(
            BattleState state,
            CardInstanceState card,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions = null)
        {
            if (card == null)
                return "";

            card = CardVisualResolver.ResolveForDescription(card, definitions);
            var ownerId = state != null ? PositionRules.GetOwnerCombatantId(state, card) : null;
            var owner = ownerId != null && state != null ? state.GetCombatant(ownerId) : null;
            return CardKeywordTooltipBuilder.BuildRichTooltip(card, owner);
        }

        static bool UsesManualPick(EffectActionSpec action, TargetPickSide pickSide)
        {
            if (pickSide == TargetPickSide.None)
                return false;

            switch (action.Target)
            {
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                    return pickSide == TargetPickSide.Enemy;
                case EffectTarget.FrontAlly:
                case EffectTarget.BackAlly:
                    return pickSide == TargetPickSide.Ally;
                default:
                    return false;
            }
        }

        static string BuildPickLead(TargetPickSide pickSide)
        {
            switch (pickSide)
            {
                case TargetPickSide.Enemy:
                    return "选择一名敌人";
                case TargetPickSide.Ally:
                    return "选择一名队友";
                default:
                    return "";
            }
        }

        static bool HasReachTagOnPickActions(CardInstanceState card, TargetPickSide pickSide)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!UsesManualPick(action, pickSide))
                    continue;

                if (!string.IsNullOrEmpty(CardReachFormatter.GetFaceTag(action, pickSide)))
                    return true;
            }

            return false;
        }

        static bool ShouldOmitFromCardFace(CardInstanceState card, EffectActionSpec action)
        {
            if (card?.Keywords == null || !card.Keywords.Contains("sacrifice"))
                return false;

            return action.Type == EffectActionType.DealDamage && action.Target == EffectTarget.Self;
        }

        static string JoinEffectClauses(IReadOnlyList<string> clauses)
        {
            if (clauses == null || clauses.Count == 0)
                return "";

            if (clauses.Count == 1)
                return clauses[0];

            if (clauses.Count == 2)
                return $"{clauses[0]}并{clauses[1]}";

            var sb = new StringBuilder(clauses[0]);
            for (var i = 1; i < clauses.Count; i++)
                sb.Append('，').Append(clauses[i]);
            return sb.ToString();
        }

        static string DescribeEffectClause(
            EffectActionSpec action,
            CombatantState owner,
            bool usesPick,
            TargetPickSide pickSide,
            BattleState state = null,
            CardInstanceState card = null,
            bool reaction = false,
            bool preferFormulas = false,
            CombatantState damagePreviewTarget = null)
        {
            var prefix = "";
            var target = usesPick ? "" : DescribeAutoTarget(action.Target, action.Type);
            var reachTag = CardReachFormatter.GetFaceTag(action, pickSide);

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                {
                    var damageExtras = DescribeDamageExtras(action);
                    var appendExtras = true;
                    if (action.Target == EffectTarget.Self)
                    {
                        string selfDmg;
                        if (action.HealMaxHpPercent > 0)
                        {
                            selfDmg = preferFormulas || owner == null
                                ? $"最大生命 {action.HealMaxHpPercent}%"
                                : CardPowerRules.ComputeActionValue(action, owner).ToString();
                        }
                        else
                        {
                            selfDmg = preferFormulas && owner == null && CardActionValueText.HasScaledComponent(action)
                                ? CardActionValueText.FormatPlain(action, useDefense: false)
                                : (state != null && owner != null && card != null
                                    ? CardPreviewRules.ComputeExpectedDamage(state, owner, card, action)
                                    : CardPowerRules.ComputeActionValue(action, owner)).ToString();
                        }

                        return prefix + $"对自身造成 {selfDmg} 点伤害{damageExtras}";
                    }

                    string body;
                    if (!preferFormulas
                        && damagePreviewTarget != null
                        && state != null
                        && owner != null
                        && card != null
                        && CardPreviewRules.CanPreviewDamageAgainstTarget(state, owner, card, action, damagePreviewTarget))
                    {
                        body = $"造成 {CardPreviewRules.PreviewHpDamageAgainstTarget(state, owner, card, action, damagePreviewTarget)} 点伤害";
                    }
                    else if (owner != null && state != null && card != null && !preferFormulas)
                        body = $"造成 {CardPreviewRules.ComputeExpectedDamage(state, owner, card, action)} 点伤害";
                    else
                    {
                        body = CardActionValueText.DescribeDamage(action, owner, preferFormulas);
                        appendExtras = false;
                    }

                    return prefix + reachTag + PrefixTarget(target, body + (appendExtras ? damageExtras : ""));
                }
                case EffectActionType.GainBlock:
                {
                    var body = CardActionValueText.DescribeBlock(action, owner, preferFormulas);
                    return prefix + reachTag + PrefixTarget(target, body);
                }
                case EffectActionType.GainBlockFromLastDamagePercent:
                    return prefix + (reaction
                        ? $"所受伤害减少 {action.Value}%"
                        : PrefixTarget(target, $"获得相当于所受伤害 {action.Value}% 的护甲"));
                case EffectActionType.ReflectLastDamageToAttacker:
                    return prefix + $"将 {action.Value}% 所受伤害反弹给攻击者";
                case EffectActionType.Heal:
                {
                    if (action.Target == EffectTarget.Self)
                    {
                        if (preferFormulas && owner == null && action.ScaleWithAttack)
                            return prefix + $"恢复自身 {CardActionValueText.FormatPlain(action, useDefense: false)} 的生命";

                        var selfHeal = CardPowerRules.ComputeActionValue(action, owner);
                        return prefix + $"恢复 {selfHeal} 点生命";
                    }

                    var body = CardActionValueText.DescribeHeal(action, owner, preferFormulas);
                    return prefix + reachTag + PrefixTarget(target, body);
                }
                case EffectActionType.DrawCards:
                    return prefix + $"抽 {action.Value} 张牌";
                case EffectActionType.DrawCardsNextTurn:
                    return prefix + $"下回合抽 {action.Value} 张牌";
                case EffectActionType.ApplyStatus:
                    if (action.Target == EffectTarget.RandomEnemies)
                    {
                        var count = action.Value > 0 ? action.Value : 1;
                        if (action.StatusId == StatusCatalog.Slow)
                            return prefix + $"随机使 {count} 名敌人获得减速 {action.Stacks} 层{FormatDurationSuffix(action)}";

                        var def = StatusCatalog.Get(action.StatusId);
                        var name = def?.DisplayName ?? action.StatusId;
                        return prefix + $"随机使 {count} 名敌人获得{name} {action.Stacks} 层{FormatDurationSuffix(action)}";
                    }

                    return prefix + reachTag + DescribeStatusEffectClause(action, target, usesPick);
                case EffectActionType.RemoveStatus:
                    return prefix + PrefixTarget(target, "清除状态");
                case EffectActionType.SwapPositionWithFrontAlly:
                    return prefix + "与前排队友交换位置";
                case EffectActionType.ApplyAnubisAvatar:
                    return prefix + "本场战斗生命上限、攻击、防御 +50%\n接下来 2 回合无法出牌";
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    return prefix + "随机使一名敌人本回合无法继续出牌";
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    return prefix + $"下回合玩家能量回复 -{action.Value}";
                case EffectActionType.ArmRespondDamageRedirect:
                    return prefix + "将所受伤害×2并转嫁给随机一名队友";
                default:
                    return "";
            }
        }

        static string PrefixTarget(string target, string clause)
        {
            if (string.IsNullOrEmpty(target))
                return clause;

            return $"{target}{clause}";
        }

        static string DescribeAutoTarget(EffectTarget target, EffectActionType type)
        {
            switch (target)
            {
                case EffectTarget.Self:
                    return type == EffectActionType.DealDamage ? "对自身" : "自身";
                case EffectTarget.EnemyFrontSlot:
                    return "敌前排";
                case EffectTarget.EnemyMiddleSlot:
                    return "敌中排";
                case EffectTarget.EnemyBackSlot:
                    return "敌后排";
                case EffectTarget.AllyFrontSlot:
                    return "前排队友";
                case EffectTarget.AllyMiddleSlot:
                    return "中排队友";
                case EffectTarget.AllyBackSlot:
                    return "后排队友";
                case EffectTarget.RandomEnemy:
                    return "随机敌人";
                default:
                    return "";
            }
        }

        static bool TryDescribeRepeatedRandomHits(
            CardInstanceState card,
            List<EffectActionSpec> actions,
            CombatantState owner,
            BattleState state,
            bool preferFormulas,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            line = "";
            remaining = actions;
            if (actions == null || actions.Count == 0)
                return false;

            var first = actions[0];
            if (first.Type != EffectActionType.DealDamage || first.Target != EffectTarget.RandomEnemy)
                return false;

            for (var i = 1; i < actions.Count; i++)
            {
                var other = actions[i];
                if (other.Type != EffectActionType.DealDamage || other.Target != EffectTarget.RandomEnemy)
                    return false;

                if (other.Value != first.Value
                    || other.ScaleWithAttack != first.ScaleWithAttack
                    || other.AttackScalePercent != first.AttackScalePercent
                    || other.DefenseScalePercent != first.DefenseScalePercent
                    || other.Reach != first.Reach)
                {
                    return false;
                }
            }

            var clause = DescribeEffectClause(
                first, owner, usesPick: false, TargetPickSide.None, state, card, reaction: false, preferFormulas);
            line = string.IsNullOrEmpty(clause) ? "" : $"{clause}，重复 {actions.Count} 次";
            remaining = new List<EffectActionSpec>();
            return !string.IsNullOrEmpty(line);
        }

        static List<EffectActionSpec> CollectNormalActions(CardInstanceState card)
        {
            var list = new List<EffectActionSpec>();
            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    list.Add(action);
            }

            return list;
        }

        static bool TryDescribeAoEEnemyDamage(
            BattleState state,
            CardInstanceState card,
            List<EffectActionSpec> actions,
            CombatantState owner,
            bool preferFormulas,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            line = "";
            remaining = actions;

            string FormatDamage(EffectActionSpec action)
            {
                if (owner != null && state != null && card != null && !preferFormulas)
                    return $"对全体敌人各造成 {CardPreviewRules.ComputeExpectedDamage(state, owner, card, action)} 点伤害{DescribeDamageExtras(action)}";

                return $"对全体敌人各{CardActionValueText.DescribeDamage(action, owner, preferFormulas)}{DescribeDamageExtras(action)}";
            }

            foreach (var action in actions)
            {
                if (action.Type != EffectActionType.DealDamage || action.Target != EffectTarget.AllEnemies)
                    continue;

                line = FormatDamage(action);
                remaining = new List<EffectActionSpec>();
                foreach (var other in actions)
                {
                    if (other != action)
                        remaining.Add(other);
                }

                return true;
            }

            EffectActionSpec front = null;
            EffectActionSpec middle = null;
            EffectActionSpec back = null;

            foreach (var action in actions)
            {
                if (action.Type != EffectActionType.DealDamage)
                    continue;

                switch (action.Target)
                {
                    case EffectTarget.EnemyFrontSlot: front = action; break;
                    case EffectTarget.EnemyMiddleSlot: middle = action; break;
                    case EffectTarget.EnemyBackSlot: back = action; break;
                }
            }

            if (front == null || middle == null || back == null)
                return false;

            if (front.Value != middle.Value || front.Value != back.Value
                || front.AttackScalePercent != middle.AttackScalePercent
                || front.AttackScalePercent != back.AttackScalePercent)
                return false;

            line = FormatDamage(front);
            remaining = new List<EffectActionSpec>();
            foreach (var action in actions)
            {
                if (action == front || action == middle || action == back)
                    continue;
                remaining.Add(action);
            }

            return true;
        }

        static string DescribeStatusEffectClause(EffectActionSpec action, string target, bool usesPick)
        {
            switch (action.StatusId)
            {
                case StatusCatalog.AttackUp:
                    return PrefixTarget(usesPick ? "" : target, $"攻击 +{action.Stacks}（本回合）");
                case StatusCatalog.DefenseUp:
                    return PrefixTarget(usesPick ? "" : target, $"防御 +{action.Stacks}（本回合）");
                case StatusCatalog.AttackDown:
                    return PrefixTarget(usesPick ? "" : target, $"攻击 -{action.Stacks}（{FormatDuration(action)}）");
                case StatusCatalog.Taunt:
                    return "所有敌人下一行动强制攻击自身";
                case StatusCatalog.Guard:
                    return "本回合队友伤害转移给自身，减伤 40%";
                case StatusCatalog.VampAura:
                    return $"直到本回合结束，攻击回复造成伤害 {action.Stacks}% 的生命";
                case StatusCatalog.ReviveBlessing:
                    return PrefixTarget(usesPick ? "" : target, "附加复活：HP 归零时恢复 25% HP（每场 1 次）");
                case StatusCatalog.Unyielding:
                    return "HP 低于 25% 时恢复 20 HP（每场 1 次，使用后移出牌组）";
                case StatusCatalog.FinalBloodRitual:
                    return "本场战斗中，每当触发【献祭】，抽 1 张牌并回复 5 点生命";
                case StatusCatalog.GodDescends:
                    return "本场战斗中，获得护甲时对全体敌人造成 ATK×1.2+5 伤害";
                case StatusCatalog.NecroticPoison:
                    return PrefixTarget(usesPick ? "" : target, $"附加中毒 {action.Stacks} 层{FormatDurationSuffix(action)}");
                case StatusCatalog.Slow:
                    return PrefixTarget(usesPick ? "" : target, $"施加减速 {action.Stacks} 层{FormatDurationSuffix(action)}");
                default:
                {
                    var def = StatusCatalog.Get(action.StatusId);
                    var name = def?.DisplayName ?? action.StatusId;
                    return PrefixTarget(usesPick ? "" : target, $"施加 {name} {action.Stacks} 层{FormatDurationSuffix(action)}");
                }
            }
        }

        static string FormatDuration(EffectActionSpec action)
        {
            var duration = action.Duration >= 0 ? action.Duration : StatusCatalog.Get(action.StatusId)?.DefaultDuration ?? 1;
            return duration > 0 ? $"{duration} 回合" : "本回合";
        }

        static string FormatDurationSuffix(EffectActionSpec action)
        {
            var text = FormatDuration(action);
            return string.IsNullOrEmpty(text) ? "" : $"（{text}）";
        }

        static bool TryDescribeAllAllyTeamEffect(
            List<EffectActionSpec> actions,
            CombatantState owner,
            bool preferFormulas,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            if (TryDescribeAllAllyBlock(actions, owner, preferFormulas, out line, out remaining))
                return true;

            return TryDescribeAllAllyAttackUp(actions, owner, out line, out remaining);
        }

        static bool TryDescribeAllAllyBlock(
            List<EffectActionSpec> actions,
            CombatantState owner,
            bool preferFormulas,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            line = "";
            remaining = actions;

            EffectActionSpec front = null;
            EffectActionSpec middle = null;
            EffectActionSpec back = null;

            foreach (var action in actions)
            {
                if (action.Type != EffectActionType.GainBlock)
                    continue;

                switch (action.Target)
                {
                    case EffectTarget.AllyFrontSlot: front = action; break;
                    case EffectTarget.AllyMiddleSlot: middle = action; break;
                    case EffectTarget.AllyBackSlot: back = action; break;
                }
            }

            if (front == null || middle == null || back == null)
                return false;

            if (front.Value != middle.Value || front.Value != back.Value)
                return false;

            if (front.ScaleWithDefense != middle.ScaleWithDefense
                || front.ScaleWithDefense != back.ScaleWithDefense)
                return false;

            if (front.DefenseScalePercent != middle.DefenseScalePercent
                || front.DefenseScalePercent != back.DefenseScalePercent)
                return false;

            if (front.AttackScalePercent != middle.AttackScalePercent
                || front.AttackScalePercent != back.AttackScalePercent)
                return false;

            if (preferFormulas && owner == null && front.ScaleWithDefense)
                line = $"三名队友各获得 {CardActionValueText.FormatPlain(front, useDefense: true)} 的护甲";
            else
            {
                var block = CardPowerRules.ComputeActionValue(front, owner);
                line = $"三名队友各获得 {block} 点护甲";
            }
            remaining = new List<EffectActionSpec>();
            foreach (var action in actions)
            {
                if (action == front || action == middle || action == back)
                    continue;
                remaining.Add(action);
            }

            return true;
        }

        static bool TryDescribeAllAllyAttackUp(
            List<EffectActionSpec> actions,
            CombatantState owner,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            line = "";
            remaining = actions;

            EffectActionSpec front = null;
            EffectActionSpec middle = null;
            EffectActionSpec back = null;

            foreach (var action in actions)
            {
                if (action.Type != EffectActionType.ApplyStatus || action.StatusId != StatusCatalog.AttackUp)
                    continue;

                switch (action.Target)
                {
                    case EffectTarget.AllyFrontSlot: front = action; break;
                    case EffectTarget.AllyMiddleSlot: middle = action; break;
                    case EffectTarget.AllyBackSlot: back = action; break;
                }
            }

            if (front == null || middle == null || back == null)
                return false;

            if (front.Stacks != middle.Stacks || front.Stacks != back.Stacks)
                return false;

            line = $"全队攻击 +{front.Stacks}（本回合）";
            remaining = new List<EffectActionSpec>();
            foreach (var action in actions)
            {
                if (action == front || action == middle || action == back)
                    continue;
                remaining.Add(action);
            }

            return true;
        }

        static string DescribeDamageExtras(EffectActionSpec action)
        {
            var parts = new List<string>();
            if (action.SplashBehindTarget)
                parts.Add($"，贯通后方 {action.SplashPowerPercent}% 伤害");
            if (action.BackRowPowerPercent > 0 && action.BackRowPowerPercent < 100)
                parts.Add($"，打后排仅 {action.BackRowPowerPercent}% 威力");
            if (action.IgnoreDefPercent > 0)
                parts.Add(action.IgnoreDefPercent >= 100 ? "，无视目标防御" : $"，无视目标 {action.IgnoreDefPercent}% 防御");
            if (action.BonusIfTargetHpBelowPercent > 0 && action.BonusIfTargetHpBelowFlat > 0)
                parts.Add($"，目标 HP 低于 {action.BonusIfTargetHpBelowPercent}% 时额外 +{action.BonusIfTargetHpBelowFlat}");
            if (action.BonusIfTargetHitThisTurnPercent > 0)
                parts.Add($"，目标本回合已被攻击则伤害 +{action.BonusIfTargetHitThisTurnPercent}%");
            if (action.LifestealPercent >= 100)
                parts.Add("，回复等量 HP");
            else if (action.LifestealPercent > 0)
                parts.Add($"，回复造成伤害 {action.LifestealPercent}% 的 HP");
            if (action.OnKillHealAmount > 0)
                parts.Add($"，击杀回复 {action.OnKillHealAmount} HP");

            return string.Concat(parts);
        }

        static List<string> CollapseDuplicateClauses(IReadOnlyList<string> clauses)
        {
            if (clauses == null || clauses.Count == 0)
                return new List<string>();

            var result = new List<string>();
            var index = 0;
            while (index < clauses.Count)
            {
                var clause = clauses[index];
                var count = 1;
                while (index + count < clauses.Count && clauses[index + count] == clause)
                    count++;

                result.Add(clause);
                index += count;
            }

            return result;
        }

        static string DescribeActionLine(EffectActionSpec action, CombatantState owner)
        {
            var prefix = action.Condition != ReactionConditionType.None ? "【应对攻击】" : "";

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    return DescribeDamageLine(action, owner, prefix);
                case EffectActionType.GainBlock:
                    return prefix + DescribeBlockLine(action, owner);
                case EffectActionType.GainBlockFromLastDamagePercent:
                    return prefix + $"护甲+{action.Value}%所受伤害";
                case EffectActionType.ReflectLastDamageToAttacker:
                    return prefix + $"反射{action.Value}%伤害";
                case EffectActionType.ApplyStatus:
                    return prefix + DescribeStatusLine(action);
                case EffectActionType.Heal:
                    return prefix + $"治疗{action.Value}";
                case EffectActionType.DrawCardsNextTurn:
                    return prefix + $"下回合抽{action.Value}张";
                case EffectActionType.DrawCards:
                    return prefix + $"抽{action.Value}张";
                case EffectActionType.RemoveStatus:
                    return prefix + "清除状态";
                case EffectActionType.SwapPositionWithFrontAlly:
                    return prefix + "与前排队友换位";
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    return prefix + "随机打断一名敌人";
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    return prefix + $"下回合能量-{action.Value}";
                case EffectActionType.ArmRespondDamageRedirect:
                    return prefix + "伤害×2转嫁";
                default:
                    return "";
            }
        }

        static string DescribeDamageLine(EffectActionSpec action, CombatantState owner, string prefix)
        {
            var dmg = CardPowerRules.ComputeActionValue(action, owner);
            var parts = new List<string> { $"伤害{dmg}" };

            var reach = DescribeReachLabel(action);
            if (!string.IsNullOrEmpty(reach))
                parts.Add(reach);

            if (action.SplashBehindTarget)
                parts.Add($"贯通{action.SplashPowerPercent}%");

            if (action.BackRowPowerPercent > 0 && action.BackRowPowerPercent < 100)
                parts.Add($"后排{action.BackRowPowerPercent}%");

            var slotTarget = DescribeSlotTarget(action.Target);
            if (!string.IsNullOrEmpty(slotTarget))
                parts.Add(slotTarget);

            return prefix + string.Join(" · ", parts);
        }

        static string DescribeBlockLine(EffectActionSpec action, CombatantState owner)
        {
            var block = CardPowerRules.ComputeActionValue(action, owner);
            return $"护甲{block}";
        }

        static string DescribeStatusLine(EffectActionSpec action)
        {
            var def = StatusCatalog.Get(action.StatusId);
            var name = def?.DisplayName ?? action.StatusId;
            var parts = new List<string> { $"{name}{action.Stacks}层" };

            var duration = action.Duration >= 0 ? action.Duration : def?.DefaultDuration ?? -1;
            if (duration > 0 && def?.DurationKind == StatusDurationKind.Turns)
                parts.Add($"{duration}回合");

            var slotTarget = DescribeSlotTarget(action.Target);
            if (!string.IsNullOrEmpty(slotTarget))
                parts.Add(slotTarget);

            return string.Join(" · ", parts);
        }

        static string DescribeReachLabel(EffectActionSpec action)
        {
            switch (action.Reach)
            {
                case TargetReach.FrontAndMiddle: return "前中";
                case TargetReach.BackOnly: return "后排";
                case TargetReach.MiddleAndBack: return "中后";
                case TargetReach.Any: return "全排";
                default: return "";
            }
        }

        static string DescribeSlotTarget(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.EnemyFrontSlot: return "敌前排";
                case EffectTarget.EnemyMiddleSlot: return "敌中排";
                case EffectTarget.EnemyBackSlot: return "敌后排";
                case EffectTarget.AllyFrontSlot: return "友前排";
                case EffectTarget.AllyMiddleSlot: return "友中排";
                case EffectTarget.AllyBackSlot: return "友后排";
                default: return "";
            }
        }

        public static string FormatStatusListDisplay(CombatantState unit)
        {
            if (unit == null || unit.Statuses.Count == 0)
                return "";

            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (i > 0) sb.Append(' ');
                var def = Grimhand.Battle.Status.StatusCatalog.Get(s.StatusId);
                var name = def?.DisplayName ?? s.StatusId;
                sb.Append(name).Append('×').Append(s.Stacks);
            }

            return sb.ToString();
        }

        public static string BuildSelectionBadge(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            if (draft == null || card == null)
                return "";

            if (draft.AwaitingTargetCardId == card.InstanceId)
                return "?";

            if (!draft.IsSelected(card.InstanceId))
                return "";

            var global = IndexOfResolutionStep(resolutionSteps, card.InstanceId);
            if (global <= 0)
                return "";

            var playerOrder = CollectPlayerCardIds(state, resolutionSteps);
            TryGetOwnerResolveOrder(state, playerOrder, card, out var ownerOrder, out var ownerTotal);
            return ownerTotal > 1 ? $"#{global} [{ownerOrder}/{ownerTotal}]" : $"#{global}";
        }

        public static List<string> BuildActionOrderSummaryFromSnapshot(
            BattleState state,
            PresentationSnapshot snapshot)
        {
            var lines = new List<string>();
            if (state == null || snapshot == null || !snapshot.HasTurnPresentation)
                return lines;

            var resolutionSteps = snapshot.TurnResolutionSteps;
            var playerOrder = CollectPlayerCardIds(state, resolutionSteps);

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var step = resolutionSteps[i];
                var card = state.GetCard(step.CardInstanceId);
                if (card == null)
                    continue;

                var global = i + 1;
                var owner = state.GetCombatant(step.CombatantId);
                var ownerName = owner?.DisplayName;
                if (string.IsNullOrEmpty(ownerName))
                    ownerName = ShortOwner(card.OwnerCharacterId);

                if (owner != null && owner.Team == TeamSide.Enemy)
                {
                    if (IsEnemyIntentHidden(state, step.CardInstanceId, snapshot.TurnEnemyIntents))
                    {
                        lines.Add($"#{global} ? ({ownerName})");
                        continue;
                    }

                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    var enemyTargetNote = BuildEnemyIntentTargetNote(state, owner, card);
                    lines.Add($"#{global} {ownerName} · {card.DisplayName} 费{card.Cost} {effect}{enemyTargetNote}");
                    continue;
                }

                TryGetOwnerResolveOrder(state, playerOrder, card, out var ownerOrder, out var ownerTotal);

                string assignedId = null;
                if (snapshot.TurnTargetByCardId.TryGetValue(step.CardInstanceId, out var snapshotTarget))
                    assignedId = snapshotTarget;

                var targetNote = BuildQueueTargetNote(state, owner, card, assignedId);

                var ownerOrderNote = ownerTotal > 1 ? $" [{ownerOrder}/{ownerTotal}]" : "";
                lines.Add($"#{global} {ownerName} · {card.DisplayName}{ownerOrderNote}{targetNote}");
            }

            return lines;
        }

        public static List<string> BuildActionOrderSummary(
            BattleState state,
            PlanningDraft draft,
            IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            var lines = new List<string>();
            if (state == null || resolutionSteps == null)
                return lines;

            var playerOrder = CollectPlayerCardIds(state, resolutionSteps);

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var step = resolutionSteps[i];
                var card = state.GetCard(step.CardInstanceId);
                if (card == null)
                    continue;

                var global = i + 1;
                var owner = state.GetCombatant(step.CombatantId);
                var ownerName = owner?.DisplayName;
                if (string.IsNullOrEmpty(ownerName))
                    ownerName = ShortOwner(card.OwnerCharacterId);

                if (owner != null && owner.Team == TeamSide.Enemy)
                {
                    if (IsEnemyIntentHidden(state, step.CardInstanceId))
                    {
                        lines.Add($"#{global} ? ({ownerName})");
                        continue;
                    }

                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    var enemyTargetNote = BuildEnemyIntentTargetNote(state, owner, card);
                    lines.Add($"#{global} {ownerName} · {card.DisplayName} 费{card.Cost} {effect}{enemyTargetNote}");
                    continue;
                }

                TryGetOwnerResolveOrder(state, playerOrder, card, out var ownerOrder, out var ownerTotal);

                var assignedId = draft?.GetAssignedTarget(step.CardInstanceId);
                var targetNote = BuildQueueTargetNote(state, owner, card, assignedId);
                var ownerOrderNote = ownerTotal > 1 ? $" [{ownerOrder}/{ownerTotal}]" : "";
                lines.Add($"#{global} {ownerName} · {card.DisplayName}{ownerOrderNote}{targetNote}");
            }

            return lines;
        }

        public static List<ActionOrderVisualEntry> BuildActionOrderVisualEntriesFromSnapshot(
            BattleState state,
            PresentationSnapshot snapshot)
        {
            var entries = new List<ActionOrderVisualEntry>();
            if (state == null || snapshot == null || !snapshot.HasTurnPresentation)
                return entries;

            var resolutionSteps = snapshot.TurnResolutionSteps;
            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var step = resolutionSteps[i];
                var card = state.GetCard(step.CardInstanceId);
                if (card == null)
                    continue;

                var owner = state.GetCombatant(step.CombatantId);
                var hidden = owner != null
                    && owner.Team == TeamSide.Enemy
                    && IsEnemyIntentHidden(state, step.CardInstanceId, snapshot.TurnEnemyIntents);

                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = i + 1,
                    Card = card,
                    IsHidden = hidden,
                    DisplayName = hidden ? "?" : card.DisplayName
                });
            }

            return entries;
        }

        public static List<ActionOrderVisualEntry> BuildActionOrderVisualEntries(
            BattleState state,
            PlanningDraft draft,
            IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            var entries = new List<ActionOrderVisualEntry>();
            if (state == null || resolutionSteps == null)
                return entries;

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var step = resolutionSteps[i];
                var card = state.GetCard(step.CardInstanceId);
                if (card == null)
                    continue;

                var owner = state.GetCombatant(step.CombatantId);
                var hidden = owner != null
                    && owner.Team == TeamSide.Enemy
                    && IsEnemyIntentHidden(state, step.CardInstanceId);

                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = i + 1,
                    Card = card,
                    IsHidden = hidden,
                    DisplayName = hidden ? "?" : card.DisplayName
                });
            }

            return entries;
        }

        public static List<ActionOrderVisualEntry> BuildActionOrderVisualEntriesFromEnemyIntents(BattleState state)
        {
            var entries = new List<ActionOrderVisualEntry>();
            if (state?.EnemyIntents == null)
                return entries;

            var order = 1;
            foreach (var intent in state.EnemyIntents)
            {
                var card = state.GetCard(intent.CardInstanceId);
                if (card == null)
                    continue;

                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = order++,
                    Card = card,
                    IsHidden = intent.IsHidden,
                    DisplayName = intent.IsHidden ? "?" : card.DisplayName
                });
            }

            return entries;
        }

        static CombatantState ResolveDamagePreviewTarget(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            CombatantState owner,
            CombatantState hoverTarget)
        {
            if (state == null || card == null || !CardPreviewRules.CardUsesSingleTargetEnemyPreview(card))
                return null;

            var assignedId = draft?.GetAssignedTarget(card.InstanceId);
            if (!string.IsNullOrEmpty(assignedId))
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null && assigned.IsAlive)
                    return assigned;
            }

            if (draft?.AwaitingTargetCardId != card.InstanceId || hoverTarget == null || owner == null)
                return null;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None || action.Type != EffectActionType.DealDamage)
                    continue;

                if (CardPreviewRules.CanPreviewDamageAgainstTarget(state, owner, card, action, hoverTarget))
                    return hoverTarget;
            }

            return null;
        }

        static string BuildQueueTargetNote(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            string assignedId)
        {
            if (state == null || card == null || owner == null)
                return "";

            if (CardPreviewRules.CardUsesAoeEnemyPreview(card))
            {
                var aoe = CardPreviewRules.FormatAoeDamagePerEnemy(state, owner, card);
                return string.IsNullOrEmpty(aoe) ? "" : $" → {aoe}";
            }

            if (string.IsNullOrEmpty(assignedId))
                return "";

            var assigned = state.GetCombatant(assignedId);
            if (assigned == null)
                return "";

            var damageNote = FormatPrimaryDamagePreview(state, owner, card, assigned);
            return string.IsNullOrEmpty(damageNote)
                ? $" → {assigned.DisplayName}"
                : $" → {assigned.DisplayName} ({damageNote})";
        }

        static string FormatPrimaryDamagePreview(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            CombatantState target)
        {
            if (state == null || owner == null || card == null || target == null)
                return "";

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None || action.Type != EffectActionType.DealDamage)
                    continue;

                if (!CardPreviewRules.CanPreviewDamageAgainstTarget(state, owner, card, action, target))
                    continue;

                return $"{CardPreviewRules.PreviewHpDamageAgainstTarget(state, owner, card, action, target)}伤";
            }

            return "";
        }

        public static List<string> BuildSelectedQueueSummary(
            BattleState state,
            PlanningDraft draft,
            IReadOnlyList<int> playerResolveOrder)
        {
            var lines = new List<string>();
            if (draft == null || playerResolveOrder == null)
                return lines;

            for (var i = 0; i < playerResolveOrder.Count; i++)
            {
                var id = playerResolveOrder[i];
                var card = state.GetCard(id);
                if (card == null)
                    continue;

                var global = i + 1;
                TryGetOwnerResolveOrder(state, playerResolveOrder, card, out var ownerOrder, out var ownerTotal);

                var ownerCombatantId = PositionRules.GetOwnerCombatantId(state, card);
                var owner = ownerCombatantId != null ? state.GetCombatant(ownerCombatantId) : null;
                var ownerName = owner?.DisplayName;
                if (string.IsNullOrEmpty(ownerName))
                    ownerName = ShortOwner(card.OwnerCharacterId);

                var assignedId = draft.GetAssignedTarget(id);
                var targetNote = BuildQueueTargetNote(state, owner, card, assignedId);

                var ownerOrderNote = ownerTotal > 1 ? $" [{ownerOrder}/{ownerTotal}]" : "";
                lines.Add($"#{global} {ownerName} · {card.DisplayName}{ownerOrderNote}{targetNote}");
            }

            return lines;
        }

        static List<int> CollectPlayerCardIds(BattleState state, IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            var result = new List<int>();
            if (state == null || resolutionSteps == null)
                return result;

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var owner = state.GetCombatant(resolutionSteps[i].CombatantId);
                if (owner != null && owner.Team == TeamSide.Player)
                    result.Add(resolutionSteps[i].CardInstanceId);
            }

            return result;
        }

        static int IndexOfResolutionStep(IReadOnlyList<ResolutionStep> steps, int cardInstanceId)
        {
            if (steps == null)
                return 0;

            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].CardInstanceId == cardInstanceId)
                    return i + 1;
            }

            return 0;
        }

        public static string BuildEnemyIntentDisplayLine(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            bool isHidden)
        {
            var actorName = owner != null ? owner.DisplayName : "敌";
            if (isHidden || card == null)
                return $"{actorName} ？";

            var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
            var targetNote = BuildEnemyIntentTargetNote(state, owner, card);
            return $"{actorName} 使用 {card.DisplayName} {effect}{targetNote}";
        }

        public static string BuildEnemyIntentTargetNote(BattleState state, CombatantState owner, CardInstanceState card)
        {
            if (state == null || owner == null || card == null)
                return "";

            if (CardPreviewRules.CardUsesAoeEnemyPreview(card))
                return " → 全体玩家";

            var target = TargetRules.PredictIntentTarget(state, owner, card);
            if (target == null)
                return "";

            if (target.Id == owner.Id)
                return " → 自身";

            return $" → {target.DisplayName}";
        }

        static bool IsEnemyIntentHidden(
            BattleState state,
            int cardInstanceId,
            IReadOnlyList<EnemyIntentSlot> intentsOverride = null)
        {
            var intents = intentsOverride ?? state.EnemyIntents;
            foreach (var intent in intents)
            {
                if (intent.CardInstanceId == cardInstanceId)
                    return intent.IsHidden;
            }

            return false;
        }

        static int IndexOfCard(IReadOnlyList<int> order, int instanceId)
        {
            if (order == null)
                return -1;

            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] == instanceId)
                    return i;
            }

            return -1;
        }

        static void TryGetOwnerResolveOrder(
            BattleState state,
            IReadOnlyList<int> playerResolveOrder,
            CardInstanceState card,
            out int order,
            out int totalForOwner)
        {
            order = 0;
            totalForOwner = 0;
            if (card == null || playerResolveOrder == null)
                return;

            for (var i = 0; i < playerResolveOrder.Count; i++)
            {
                var other = state.GetCard(playerResolveOrder[i]);
                if (other == null || other.OwnerCharacterId != card.OwnerCharacterId)
                    continue;

                totalForOwner++;
                if (other.InstanceId == card.InstanceId)
                    order = totalForOwner;
            }
        }

        public static string FormatPartyHpLine(IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (party == null || party.Count == 0)
                return "队伍 HP: —";

            var sb = new StringBuilder();
            for (var i = 0; i < party.Count; i++)
            {
                var m = party[i];
                if (i > 0) sb.Append("  ");
                sb.Append(m.DisplayName).Append(' ').Append(m.Hp).Append('/').Append(m.MaxHp);
            }

            return sb.ToString();
        }

        public static string FormatGold(int gold) => $"金币 {gold}";

        public static string FormatPartySummary(IReadOnlyList<PartyMemberSnapshot> party, int gold)
        {
            var hpLine = FormatPartyHpLine(party);
            return gold > 0 ? $"{hpLine}\n{FormatGold(gold)}" : hpLine;
        }

        public static string DescribeNodeType(ExpeditionNodeType type)
        {
            switch (type)
            {
                case ExpeditionNodeType.Combat: return "普通战斗";
                case ExpeditionNodeType.Elite: return "精英";
                case ExpeditionNodeType.Treasure: return "宝箱";
                case ExpeditionNodeType.Event: return "事件";
                case ExpeditionNodeType.Shop: return "商人";
                case ExpeditionNodeType.Shrine: return "祭坛";
                case ExpeditionNodeType.Boss: return "Boss";
                default: return type.ToString();
            }
        }

        static bool TryBuildCurseCardStatsLine(CardInstanceState card, out string line)
        {
            line = "";
            if (card == null || !IsCurseCard(card))
                return false;

            line = card.DefinitionId switch
            {
                "curse_chaos_touch" => "【诅咒】\n打出后无任何效果，浪费1点能量与1个手牌位。",
                _ => "【诅咒】\n占用手牌位，可通过商人删牌或祭坛献祭移除。"
            };
            return true;
        }

        static bool IsCurseCard(CardInstanceState card) =>
            card.Keywords.Contains("curse")
            || (!string.IsNullOrEmpty(card.DefinitionId) && card.DefinitionId.StartsWith("curse_"));
    }
}
