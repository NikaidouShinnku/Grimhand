using System;
using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Content;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Battle
{
    public struct ActionOrderVisualEntry
    {
        public int OrderIndex;
        public CardInstanceState Card;
        public bool IsHidden;
        /// <summary>卡牌上方标题（卡名；隐藏时为 ?）。</summary>
        public string CardTitle;
        /// <summary>卡牌下方：出卡者→目标（如 哥布林1→AOE）。</summary>
        public string OwnerArrowTarget;
        /// <summary>兼容旧用法；优先用 CardTitle。</summary>
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

        /// <summary>
        /// 同名存活单位在场时追加 1/2/3 后缀（按阵位、再按 Id），便于区分重复敌人的意图。
        /// </summary>
        public static string FormatCombatantDisambiguatedName(BattleState state, CombatantState unit)
        {
            if (unit == null)
                return "?";
            if (state == null)
                return unit.DisplayName;

            var peers = new List<CombatantState>();
            foreach (var c in state.GetTeam(unit.Team))
            {
                if (c.DisplayName != unit.DisplayName)
                    continue;
                peers.Add(c);
            }

            if (peers.Count <= 1)
                return unit.DisplayName;

            peers.Sort((a, b) =>
            {
                var sa = (int)a.Slot;
                var sb = (int)b.Slot;
                var cmp = sa.CompareTo(sb);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Id, b.Id);
            });

            for (var i = 0; i < peers.Count; i++)
            {
                if (peers[i].Id == unit.Id)
                    return $"{unit.DisplayName}{i + 1}";
            }

            return unit.DisplayName;
        }

        static string FormatEnemyActionOrderLabel(BattleState state, CombatantState owner, CardInstanceState card, bool hidden)
        {
            if (hidden)
            {
                var ownerName = owner != null ? FormatCombatantDisambiguatedName(state, owner) : "?";
                return $"? ({ownerName})";
            }

            if (owner == null || card == null)
                return card?.DisplayName ?? "?";

            return $"{FormatCombatantDisambiguatedName(state, owner)} · {card.DisplayName}";
        }

        public static string FormatActionOrderCardTitle(CardInstanceState card, bool hidden) =>
            hidden || card == null ? "?" : (card.DisplayName ?? "?");

        public static string FormatActionOrderOwnerArrowTarget(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            bool hidden,
            string assignedTargetId = null)
        {
            var ownerName = owner != null ? FormatCombatantDisambiguatedName(state, owner) : "?";
            var target = FormatActionOrderTargetLabel(state, owner, card, hidden, assignedTargetId);
            return $"{ownerName}→{target}";
        }

        public static string FormatActionOrderTargetLabel(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            bool hidden,
            string assignedTargetId = null)
        {
            if (hidden)
                return "?";
            if (state == null || card == null)
                return "?";

            if (CardPreviewRules.CardUsesAoeEnemyPreview(card) || CardHasAllEnemiesTarget(card))
                return "AOE";

            if (CardTargetsSelfOnly(card))
                return "自己";

            if (!string.IsNullOrEmpty(assignedTargetId))
            {
                var assigned = state.GetCombatant(assignedTargetId);
                if (assigned != null && assigned.IsAlive)
                {
                    if (owner != null && assigned.Id == owner.Id)
                        return "自己";
                    return FormatCombatantDisambiguatedName(state, assigned);
                }
            }

            if (owner != null && owner.Team == TeamSide.Enemy)
            {
                var predicted = TargetRules.PredictIntentTarget(state, owner, card);
                if (predicted == null)
                    return "?";
                if (predicted.Id == owner.Id)
                    return "自己";
                return FormatCombatantDisambiguatedName(state, predicted);
            }

            // 玩家牌未选目标
            if (CardRules.GetRequiredTargetPick(card) != TargetPickSide.None)
                return "?";

            var fallback = owner != null ? TargetRules.PredictIntentTarget(state, owner, card) : null;
            if (fallback == null)
                return "?";
            if (owner != null && fallback.Id == owner.Id)
                return "自己";
            return FormatCombatantDisambiguatedName(state, fallback);
        }

        static bool CardHasAllEnemiesTarget(CardInstanceState card)
        {
            if (card?.Actions == null)
                return false;
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;
                if (action.Target == EffectTarget.AllEnemies)
                    return true;
            }

            return false;
        }

        static bool CardTargetsSelfOnly(CardInstanceState card)
        {
            if (card?.Actions == null || card.Actions.Count == 0)
                return false;

            var sawSelf = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;
                if (action.Target == EffectTarget.Self)
                {
                    sawSelf = true;
                    continue;
                }

                // 另有指向他人的目标则不算纯自身
                if (action.Target is EffectTarget.DefaultEnemy
                    or EffectTarget.ManualSelected
                    or EffectTarget.AllEnemies
                    or EffectTarget.RandomEnemy
                    or EffectTarget.RandomEnemies
                    or EffectTarget.FrontAlly
                    or EffectTarget.BackAlly
                    or EffectTarget.EnemyFrontSlot
                    or EffectTarget.EnemyMiddleSlot
                    or EffectTarget.EnemyBackSlot)
                    return false;
            }

            return sawSelf;
        }

        public static string FormatUnitLine(CombatantState unit)
        {
            var status = FormatStatusList(unit);
            var core = $"{unit.DisplayName}\nHP {unit.Hp}/{unit.MaxHp}  速{StatusRules.GetEffectiveSpeed(unit)}";
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
            CombatantState damagePreviewTarget = null,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions = null)
        {
            if (card == null)
                return "";

            card = HolysunSpellbookRules.ApplyForDisplay(state, card);

            var ownerId = state != null ? PositionRules.GetOwnerCombatantId(state, card) : null;
            var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
            if (TryBuildCurseCardStatsLine(card, out var curseLine))
                return curseLine;

            // 图鉴文案优先：手牌与图鉴保持一致（含无 Actions 的特殊技卡）。
            // 已升级/改写实例 MatchesDefinitionBaseline 失败时再走动态预览。
            if (TryBuildExcelDescriptionLine(state, draft, card, definitions, out var excelLine))
                return excelLine;

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
            else if (TryDescribeAllAllyTeamEffect(state, normalActions, owner, preferFormulas, out var allyLine, out remaining))
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

            if (card.DefinitionId == PassiveCardMechanicsRules.SandSpearReforgeCardId)
            {
                var hits = PassiveCardMechanicsRules.GetSandSpearExhaustCount(state);
                lines.Add(hits > 0
                    ? $"随机 {PassiveCardMechanicsRules.SandSpearReforgeBaseDamage} 伤 ×{hits}（远征消耗牌计数）"
                    : $"随机 {PassiveCardMechanicsRules.SandSpearReforgeBaseDamage} 伤 ×0（尚未打出消耗牌）");
            }

            var dynamicLine = string.Join("\n", lines);
            if (!string.IsNullOrWhiteSpace(dynamicLine))
                return dynamicLine;

            // 特殊技卡无 Actions 或基线校验失败时，仍回落图鉴文案，避免手牌空白。
            if (!string.IsNullOrEmpty(card.DefinitionId)
                && CardDescriptionCatalog.TryGetByCardId(card.DefinitionId, out var catalogById)
                && !string.IsNullOrWhiteSpace(catalogById))
                return catalogById.Trim();

            if (!string.IsNullOrEmpty(card.DisplayName)
                && CardDescriptionCatalog.TryGetByDisplayName(card.DisplayName, out var catalogByName)
                && !string.IsNullOrWhiteSpace(catalogByName))
                return catalogByName.Trim();

            return "";
        }

        /// <summary>战斗手牌专用：显示加成后数值；单体选目标时悬停敌人可预览最终 HP 伤害。</summary>
        public static string BuildCardStatsLineForHand(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            CombatantState damagePreviewTarget = null,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions = null) =>
            BuildCardStatsLine(state, draft, card, preferFormulas: false, damagePreviewTarget, definitions);

        public static string BuildCardStatsLinePreview(CardInstanceState card) =>
            BuildCardStatsLine(state: null, draft: null, card, preferFormulas: false);

        public static string BuildCardStatsLinePreview(
            CardInstanceState card,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions) =>
            BuildCardStatsLine(
                state: null,
                draft: null,
                CardVisualResolver.ResolveForDescription(card, definitions),
                preferFormulas: false,
                definitions: definitions);

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
            CombatantState previewTarget = null)
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
                        else if (state != null && owner != null && card != null && !preferFormulas)
                        {
                            var expected = CardPreviewRules.ComputeExpectedDamage(state, owner, card, action);
                            var baseline = CardPreviewRules.ComputeBaselineDamage(owner, action);
                            selfDmg = CardFaceNumberFormatter.Format(expected, baseline);
                        }
                        else
                        {
                            selfDmg = preferFormulas && owner == null && CardActionValueText.HasScaledComponent(action)
                                ? CardActionValueText.FormatPlain(action, useDefense: false)
                                : CardPowerRules.ComputeActionValue(action, owner).ToString();
                        }

                        return prefix + $"对自身造成 {selfDmg} 点伤害{damageExtras}";
                    }

                    string body;
                    if (!preferFormulas
                        && previewTarget != null
                        && state != null
                        && owner != null
                        && card != null
                        && CardPreviewRules.CanPreviewDamageAgainstTarget(state, owner, card, action, previewTarget))
                    {
                        var settled = CardPreviewRules.PreviewHpDamageAgainstTarget(
                            state, owner, card, action, previewTarget);
                        var baseline = CardPreviewRules.ComputeBaselineDamage(owner, action);
                        body = $"造成 {CardFaceNumberFormatter.Format(settled, baseline)} 点伤害";
                    }
                    else if (owner != null && state != null && card != null && !preferFormulas)
                    {
                        var expected = CardPreviewRules.ComputeExpectedDamage(state, owner, card, action);
                        var baseline = CardPreviewRules.ComputeBaselineDamage(owner, action);
                        body = $"造成 {CardFaceNumberFormatter.Format(expected, baseline)} 点伤害";
                    }
                    else
                    {
                        body = CardActionValueText.DescribeDamage(action, owner, preferFormulas);
                        appendExtras = false;
                    }

                    return prefix + reachTag + PrefixTarget(target, body + (appendExtras ? damageExtras : ""));
                }
                case EffectActionType.GainBlock:
                {
                    if (owner != null && state != null && !preferFormulas)
                    {
                        var expected = CardPreviewRules.ComputeExpectedBlock(state, owner, action);
                        var baseline = CardPreviewRules.ComputeBaselineBlock(owner, action);
                        var amountText = CardFaceNumberFormatter.Format(expected, baseline);
                        if (owner.TalentDisableBlockGain
                            && TalentBattleRules.HasTalent(state, "talent_knight_s2_lv10"))
                        {
                            return prefix + reachTag + PrefixTarget(
                                target, $"下张攻击伤害 +{amountText}（铁壁转化）");
                        }

                        return prefix + reachTag + PrefixTarget(target, $"获得 {amountText} 点护甲");
                    }

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

                        var selfHeal = CardPreviewRules.ComputeExpectedHeal(state, owner, action);
                        var baseHeal = CardPreviewRules.ComputeBaselineHeal(owner, action);
                        var healText = state != null
                            ? CardFaceNumberFormatter.Format(selfHeal, baseHeal)
                            : selfHeal.ToString();
                        return prefix + $"恢复 {healText} 点生命";
                    }

                    if (owner != null && state != null && !preferFormulas)
                    {
                        var heal = CardPreviewRules.ComputeExpectedHeal(state, owner, action);
                        var baseHeal = CardPreviewRules.ComputeBaselineHeal(owner, action);
                        return prefix + reachTag + PrefixTarget(
                            target, $"恢复 {CardFaceNumberFormatter.Format(heal, baseHeal)} 点生命");
                    }

                    var body = CardActionValueText.DescribeHeal(action, owner, preferFormulas);
                    return prefix + reachTag + PrefixTarget(target, body);
                }
                case EffectActionType.DrawCards:
                    if (card?.Keywords != null && card.Keywords.Contains("quick_start"))
                        return prefix + $"抽 {action.Value} 张牌";
                    return prefix + $"下回合抽 {action.Value} 张牌";
                case EffectActionType.DrawCardsNextTurn:
                {
                    var drawText = $"下回合抽 {action.Value} 张牌";
                    if (action.CostReduction > 0)
                        drawText += $"（费用-{action.CostReduction}）";
                    return prefix + drawText;
                }
                case EffectActionType.ApplyStatus:
                    if (action.Target == EffectTarget.RandomEnemies)
                    {
                        var count = action.Value > 0 ? action.Value : 1;
                        var stacks = PreviewStatusStacks(state, owner, action);
                        var dur = FormatDurationSuffix(action, state, owner);
                        if (action.StatusId == StatusCatalog.Slow)
                            return prefix + $"随机使 {count} 名敌人获得减速 {stacks} 层{dur}";

                        var def = StatusCatalog.Get(action.StatusId);
                        var name = def?.DisplayName ?? action.StatusId;
                        return prefix + $"随机使 {count} 名敌人获得{name} {stacks} 层{dur}";
                    }

                    if (action.Target == EffectTarget.AllAllies
                        && (IsTeamWideAttackBuffStatus(action.StatusId)
                            || IsTeamWideDefenseBuffStatus(action.StatusId)))
                    {
                        return prefix + FormatTeamWideStatusLine(action, state, owner);
                    }

                    return prefix + reachTag + DescribeStatusEffectClause(action, target, usesPick, state, owner);
                case EffectActionType.RemoveStatus:
                    return prefix + PrefixTarget(target, "清除状态");
                case EffectActionType.SwapPositionWithFrontAlly:
                    return prefix + "与前排队友交换位置";
                case EffectActionType.SwapPositionWithSelectedAlly:
                    return prefix + "与选中友方交换位置";
                case EffectActionType.SwapBackEnemyWithRandomOther:
                    return prefix + "使后排敌人与随机另一名敌人交换位置";
                case EffectActionType.SwapRandomEnemies:
                    return prefix + "使随机两名敌人交换位置";
                case EffectActionType.ApplyAnubisAvatar:
                    return prefix + "本场战斗生命上限、攻击、防御 +50%\n接下来 2 回合无法出牌";
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    return prefix + "使随机一名敌人下回合无法使用卡牌";
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    return prefix + $"下回合玩家能量回复 -{action.Value}";
                case EffectActionType.ArmRespondDamageRedirect:
                    return prefix + "应对成功时格挡，并将所受伤害×2转嫁给随机一名队友";
                case EffectActionType.ApplyDelayedDamage:
                {
                    var amount = action.Value;
                    if (action.Target == EffectTarget.AllEnemies)
                        return prefix + reachTag + $"下回合开始时，对全体敌人各造成 {amount} 点伤害";
                    return prefix + reachTag + PrefixTarget(target, $"下回合开始时，造成 {amount} 点伤害");
                }
                case EffectActionType.SealNextEnemyCard:
                    return prefix + "使敌方使用的下一张卡失效";
                case EffectActionType.EtherealCountBonusDamage:
                    return prefix + reachTag + PrefixTarget(
                        target,
                        $"造成 {action.Value} 点伤害（本场远征每进入过一次虚化 +{Math.Max(1, action.Stacks)}）");
                case EffectActionType.BuffAllOtherAllies:
                    return prefix + reachTag + DescribeStatusEffectClause(action, "其他友方", usesPick, state, owner);
                case EffectActionType.DrawCardsIfEthereal:
                    return prefix + $"下回合抽 {action.Value} 张牌（虚化时改为抽 {action.AlternateValue} 张）";
                case EffectActionType.GainEnergy:
                    return prefix + $"获得 {action.Value} 点能量";
                case EffectActionType.DrawToHandLimit:
                    return prefix + "抽牌至手牌上限";
                case EffectActionType.ShuffleHandCosts:
                    return prefix + "将手牌费用随机打乱";
                case EffectActionType.LockSelfCards:
                    return prefix + $"自身 {Math.Max(1, action.Value)} 回合无法出牌";
                case EffectActionType.RevealEnemyIntent:
                    return prefix + "看破敌人意图";
                case EffectActionType.AddTokenCardToHand:
                    return prefix + "将一张牌置入玩家手牌";
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
                case EffectTarget.AllEnemies:
                    return type == EffectActionType.DealDamage ? "全体敌人" : "所有敌人";
                case EffectTarget.AllAllies:
                    return "所有友方";
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
            // 多段只展示单段结算伤害（剑刃风暴等），不写「重复 N 次」。
            line = clause ?? "";
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
                {
                    var perEnemyLine = FormatBattleAoeDamageLine(state, owner, card, action);
                    if (!string.IsNullOrEmpty(perEnemyLine))
                        return perEnemyLine + DescribeDamageExtras(action);

                    var expected = CardPreviewRules.ComputeExpectedDamage(state, owner, card, action);
                    var baseline = CardPreviewRules.ComputeBaselineDamage(owner, action);
                    return $"对全体敌人各造成 {CardFaceNumberFormatter.Format(expected, baseline)} 点伤害{DescribeDamageExtras(action)}";
                }

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

        /// <summary>
        /// AOE 伤害文案：各目标最终值相同时写「各造成 X」；不同时才分列 名(伤)。
        /// </summary>
        static string FormatBattleAoeDamageLine(
            BattleState state,
            CombatantState owner,
            CardInstanceState card,
            EffectActionSpec action)
        {
            if (state == null || owner == null || card == null || action == null)
                return "";

            List<(CombatantState target, int damage)> parts;
            if (action.Target == EffectTarget.AllEnemies)
            {
                parts = CardPreviewRules.PreviewAoeDamagePerEnemy(state, owner, card, action);
            }
            else
            {
                parts = new List<(CombatantState target, int damage)>();
                foreach (var enemy in PositionRules.GetAliveSortedByPhysicalSlot(state, TeamSide.Enemy))
                {
                    if (!CardPreviewRules.CanPreviewDamageAgainstTarget(state, owner, card, action, enemy))
                        continue;
                    parts.Add((enemy, CardPreviewRules.PreviewHpDamageAgainstTarget(state, owner, card, action, enemy)));
                }
            }

            if (parts == null || parts.Count == 0)
                return "";

            var baseline = CardPreviewRules.ComputeBaselineDamage(owner, action);
            var allSame = true;
            for (var i = 1; i < parts.Count; i++)
            {
                if (parts[i].damage != parts[0].damage)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                return $"对全体敌人各造成 {CardFaceNumberFormatter.Format(parts[0].damage, baseline)} 点伤害";
            }

            var sb = new StringBuilder("对全体敌人：");
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(FormatCombatantDisambiguatedName(state, parts[i].target))
                    .Append('(')
                    .Append(CardFaceNumberFormatter.Format(parts[i].damage, baseline))
                    .Append(')');
            }

            return sb.ToString();
        }

        static string DescribeStatusEffectClause(
            EffectActionSpec action,
            string target,
            bool usesPick,
            BattleState state = null,
            CombatantState owner = null)
        {
            var stacks = PreviewStatusStacks(state, owner, action);
            var stacksText = CardFaceNumberFormatter.Format(stacks, action.Stacks);
            var dur = FormatDurationSuffix(action, state, owner);

            switch (action.StatusId)
            {
                case StatusCatalog.AttackUp:
                case StatusCatalog.DamageUp:
                    return PrefixTarget(usesPick ? "" : target, $"攻击伤害 +{stacksText}（本回合）");
                case StatusCatalog.DefenseUp:
                case StatusCatalog.ArmorUp:
                    return PrefixTarget(usesPick ? "" : target, $"护甲获取 +{stacksText}（本回合）");
                case StatusCatalog.AttackDown:
                case StatusCatalog.Weaken:
                    return PrefixTarget(usesPick ? "" : target, $"施加 {stacksText}% 虚弱（造成的伤害减少）{dur}");
                case StatusCatalog.Vulnerable:
                    return PrefixTarget(usesPick ? "" : target, $"施加 {stacksText}% 易伤{dur}");
                case StatusCatalog.Taunt:
                    return "所有敌人下一行动强制攻击自身";
                case StatusCatalog.Guard:
                    return "本回合队友伤害转移给自身，减伤 50%";
                case StatusCatalog.VampAura:
                    return $"直到本回合结束，攻击回复造成伤害 {stacksText}% 的生命";
                case StatusCatalog.ReviveBlessing:
                    return PrefixTarget(usesPick ? "" : target, "附加复活：HP 归零时恢复 25% HP（每场 1 次）");
                case StatusCatalog.Unyielding:
                    return "HP 低于 25% 时恢复 20 HP（每场 1 次，使用后移出牌组）";
                case StatusCatalog.FinalBloodRitual:
                    return "本场战斗中，每当触发【献祭】，回复 5 点生命并在下回合开始时抽 1 张牌";
                case StatusCatalog.GodDescends:
                    return "本场战斗中，获得护甲时对全体敌人造成 8 伤害";
                case StatusCatalog.NecroticPoison:
                case StatusCatalog.Poison:
                    return PrefixTarget(usesPick ? "" : target, $"附加中毒 {stacksText} 层{dur}");
                case StatusCatalog.Slow:
                    return PrefixTarget(usesPick ? "" : target, $"施加减速 {stacksText} 层{dur}");
                case StatusCatalog.Burn:
                    return PrefixTarget(usesPick ? "" : target, $"施加灼烧 {stacksText} 层{dur}");
                default:
                {
                    var def = StatusCatalog.Get(action.StatusId);
                    var name = def?.DisplayName ?? action.StatusId;
                    return PrefixTarget(usesPick ? "" : target, $"施加 {name} {stacksText} 层{dur}");
                }
            }
        }

        static int PreviewStatusStacks(BattleState state, CombatantState owner, EffectActionSpec action)
        {
            if (action == null)
                return 0;

            var stacks = action.Stacks;
            if (state != null && owner != null && action.StatusId == StatusCatalog.Poison)
                TalentBattleRules.AdjustPoisonStacks(state, owner, ref stacks);

            return stacks;
        }

        static string FormatDuration(EffectActionSpec action, BattleState state = null, CombatantState owner = null)
        {
            var baseline = CardPreviewRules.BaselineStatusDurationTurns(action);
            if (baseline < 0)
                return "永久";

            var settled = CardPreviewRules.PreviewStatusDurationTurns(state, action, owner);
            if (settled < 0)
                return CardFaceNumberFormatter.FormatDurationTurns(settled, baseline);

            if (settled <= 0 && baseline <= 0)
                return "本回合";

            return CardFaceNumberFormatter.FormatDurationTurns(settled, baseline);
        }

        static string FormatDurationSuffix(EffectActionSpec action, BattleState state = null, CombatantState owner = null)
        {
            var text = FormatDuration(action, state, owner);
            return string.IsNullOrEmpty(text) ? "" : $"（{text}）";
        }

        static bool TryDescribeAllAllyTeamEffect(
            BattleState state,
            List<EffectActionSpec> actions,
            CombatantState owner,
            bool preferFormulas,
            out string line,
            out List<EffectActionSpec> remaining)
        {
            if (TryDescribeAllAllyBlock(state, actions, owner, preferFormulas, out line, out remaining))
                return true;

            if (TryDescribeAllAllyAttackUp(actions, owner, state, out line, out remaining))
                return true;

            return TryDescribeAllAllyDefenseUp(actions, owner, state, out line, out remaining);
        }

        static bool TryDescribeAllAllyBlock(
            BattleState state,
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
                line = $"全队获得 {CardActionValueText.FormatPlain(front, useDefense: true)} 的护甲";
            else if (state != null && owner != null && !preferFormulas)
            {
                var expected = CardPreviewRules.ComputeExpectedBlock(state, owner, front);
                var baseline = CardPreviewRules.ComputeBaselineBlock(owner, front);
                line = $"全队获得 {CardFaceNumberFormatter.Format(expected, baseline)} 点护甲";
            }
            else
            {
                var block = CardPowerRules.ComputeActionValue(front, owner);
                line = $"全队获得 {block} 点护甲";
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
            BattleState state,
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
                if (action.Type != EffectActionType.ApplyStatus)
                    continue;
                if (!IsTeamWideAttackBuffStatus(action.StatusId))
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

            if (front.StatusId != middle.StatusId || front.StatusId != back.StatusId)
                return false;

            if (front.Stacks != middle.Stacks || front.Stacks != back.Stacks)
                return false;

            if (front.Duration != middle.Duration || front.Duration != back.Duration)
                return false;

            line = FormatTeamWideStatusLine(front, state, owner);
            remaining = new List<EffectActionSpec>();
            foreach (var action in actions)
            {
                if (action == front || action == middle || action == back)
                    continue;
                remaining.Add(action);
            }

            return true;
        }

        static bool IsTeamWideAttackBuffStatus(string statusId) =>
            statusId == StatusCatalog.AttackUp
            || statusId == StatusCatalog.DamageUp
            || statusId == StatusCatalog.AttackUpPercent;

        static bool IsTeamWideDefenseBuffStatus(string statusId) =>
            statusId == StatusCatalog.DefenseUp
            || statusId == StatusCatalog.ArmorUp
            || statusId == StatusCatalog.DefenseUpPercent;

        static string FormatTeamWideStatusLine(
            EffectActionSpec action,
            BattleState state,
            CombatantState owner)
        {
            var stacks = PreviewStatusStacks(state, owner, action);
            var stacksText = CardFaceNumberFormatter.Format(stacks, action.Stacks);
            var dur = FormatDurationSuffix(action, state, owner);

            if (IsTeamWideAttackBuffStatus(action.StatusId))
            {
                if (action.StatusId == StatusCatalog.AttackUpPercent)
                    return $"全队获得 {stacksText}% 增伤{dur}";
                return $"全队攻击伤害 +{stacksText}{dur}";
            }

            if (IsTeamWideDefenseBuffStatus(action.StatusId))
            {
                if (action.StatusId == StatusCatalog.DefenseUpPercent)
                    return $"全队获得 {stacksText}% 强固{dur}";
                return $"全队护甲获取 +{stacksText}{dur}";
            }

            var def = StatusCatalog.Get(action.StatusId);
            var name = def?.DisplayName ?? action.StatusId;
            return $"全队获得 {name} {stacksText} 层{dur}";
        }

        static bool TryDescribeAllAllyDefenseUp(
            List<EffectActionSpec> actions,
            CombatantState owner,
            BattleState state,
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
                if (action.Type != EffectActionType.ApplyStatus)
                    continue;
                if (!IsTeamWideDefenseBuffStatus(action.StatusId))
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

            if (front.StatusId != middle.StatusId || front.StatusId != back.StatusId)
                return false;

            if (front.Stacks != middle.Stacks || front.Stacks != back.Stacks)
                return false;

            if (front.Duration != middle.Duration || front.Duration != back.Duration)
                return false;

            line = FormatTeamWideStatusLine(front, state, owner);
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
                case EffectActionType.SwapPositionWithSelectedAlly:
                    return prefix + "与选中友方换位";
                case EffectActionType.SwapBackEnemyWithRandomOther:
                    return prefix + "后排敌人与随机敌人换位";
                case EffectActionType.SwapRandomEnemies:
                    return prefix + $"随机交换{System.Math.Max(1, action.Value)}对敌人站位";
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    return prefix + "下回合无法出牌";
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    return prefix + $"下回合能量-{action.Value}";
                case EffectActionType.ArmRespondDamageRedirect:
                    return prefix + "格挡并转嫁×2";
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
                if (s == null || s.Stacks <= 0)
                    continue;

                if (sb.Length > 0)
                    sb.Append(' ');

                var def = Grimhand.Battle.Status.StatusCatalog.Get(s.StatusId);
                var name = def?.DisplayName ?? s.StatusId;
                sb.Append(name).Append('×').Append(s.Stacks);
            }

            return sb.ToString();
        }

        public static string FormatStatusHoverDetail(CombatantState unit)
        {
            if (unit == null || unit.Statuses.Count == 0)
                return "";

            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (s == null || s.Stacks <= 0)
                    continue;

                var def = StatusCatalog.Get(s.StatusId);
                var name = def?.DisplayName ?? s.StatusId;
                sb.Append('\n').Append('·').Append(' ')
                    .Append(name)
                    .Append('×')
                    .Append(s.Stacks)
                    .Append(' ')
                    .Append(FormatStatusDuration(s, def));
            }

            return sb.ToString();
        }

        /// <summary>悬停角色时，独立状态框的炉石式描述（含天神下凡等特殊状态）。</summary>
        public static string FormatStatusTooltipDescriptions(CombatantState unit, BattleState state = null)
        {
            if (unit == null)
                return "";

            var sb = new StringBuilder();
            AppendStatusTooltipEntry(sb, unit.CardsLockedTurnsRemaining > 0,
                "禁出牌", $"剩余 {unit.CardsLockedTurnsRemaining} 回合无法出牌");
            AppendStatusTooltipEntry(sb, unit.ConstrictLockTurnsRemaining > 0,
                "缠绕禁牌", $"剩余 {unit.ConstrictLockTurnsRemaining} 回合无法出牌（部分牌可例外）");
            AppendStatusTooltipEntry(sb, unit.AttackCardsLockedTurnsRemaining > 0,
                "禁攻击牌", $"剩余 {unit.AttackCardsLockedTurnsRemaining} 回合无法使用攻击牌");
            AppendStatusTooltipEntry(sb, unit.BloodRageStacks > 0,
                "血怒", $"下一张攻击 +{unit.BloodRageStacks * MinionTraitCatalog.OgreBloodRageDamagePercentPerStack}%");
            AppendStatusTooltipEntry(sb, unit.SacrificeAttackStacks > 0,
                "献祭增伤", $"增伤 +{unit.SacrificeAttackStacks}%（血祭坛等）");
            AppendStatusTooltipEntry(sb, unit.NextAttackFlatBonus > 0,
                "下次攻击", $"额外 +{unit.NextAttackFlatBonus} 伤害");
            AppendStatusTooltipEntry(sb, unit.MermaidZeroCostAttackBonusPercent > 0,
                "增伤",
                $"打出费用为 0 的卡牌时获得（永久，上限 100%）；当前 +{unit.MermaidZeroCostAttackBonusPercent}%，提升所有攻击牌伤害");

            if (FootStatusIconAggregator.IsBurningLongswordActive(state, unit))
            {
                AppendStatusTooltipEntry(sb, true,
                    "烈火长剑",
                    "前排专属：对拥有灼烧的敌人造成 1.5 倍伤害");
            }

            var dodgePct = FootStatusIconAggregator.ResolveDodgeChancePercent(state, unit);
            if (dodgePct > 0)
            {
                var dodgeDesc = unit.FirstHitDodgePending
                    && MinionTraitRules.HasTrait(unit, MinionTraitCatalog.BatFirstHitDodge)
                    ? $"本回合首次受击有 {dodgePct}% 概率完全闪避伤害"
                    : $"受击时有 {dodgePct}% 概率完全闪避伤害";
                AppendStatusTooltipEntry(sb, true, "闪避", dodgeDesc);
            }

            var blockGainPct = 0;
            if (unit.Team == TeamSide.Player
                && state?.Config?.RunModifiers != null
                && state.Config.RunModifiers.TeamBlockGainBonusPercent > 0f)
                blockGainPct = (int)System.Math.Round(state.Config.RunModifiers.TeamBlockGainBonusPercent);
            AppendStatusTooltipEntry(sb, blockGainPct > 0,
                "强固",
                $"获得护甲 +{blockGainPct}%（铁壁战甲/圣骑之盾等）");

            if (unit.Team == TeamSide.Player
                && unit.Block > 0
                && unit.CharacterDefinitionId is RelicEffectRules.WarriorCharacterId or "char_warrior"
                && state?.Config?.RunModifiers != null
                && state.Config.RunModifiers.WarriorBlockDamageReductionPercent > 0f)
            {
                var castlePct = (int)System.Math.Round(state.Config.RunModifiers.WarriorBlockDamageReductionPercent);
                AppendStatusTooltipEntry(sb, true,
                    "城堡骑士",
                    $"拥有护甲期间受伤减少 {castlePct}%");
            }

            if (unit.Team == TeamSide.Player
                && state != null
                && state.TeamFirstHitReductionPending
                && unit.FirstHitReductionPending
                && state.Config?.RunModifiers != null
                && state.Config.RunModifiers.FirstHitDamageReductionPercent > 0f)
            {
                var firstPct = (int)System.Math.Round(state.Config.RunModifiers.FirstHitDamageReductionPercent);
                AppendStatusTooltipEntry(sb, true,
                    "圣骑之盾",
                    $"本回合第一个受到攻击的友方，该次受伤减少 {firstPct}%");
            }

            foreach (var s in unit.Statuses)
            {
                if (s == null || s.Stacks <= 0)
                    continue;

                var def = StatusCatalog.Get(s.StatusId);
                var name = def?.DisplayName ?? s.StatusId;
                var desc = DescribeStatusForTooltip(s, def);
                if (string.IsNullOrEmpty(desc))
                    continue;

                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append("<b>").Append(name).Append(" ×").Append(s.Stacks).Append("</b>\n");
                sb.Append(desc);
            }

            return sb.ToString();
        }

        static void AppendStatusTooltipEntry(StringBuilder sb, bool condition, string title, string body)
        {
            if (!condition || string.IsNullOrEmpty(body))
                return;

            if (sb.Length > 0)
                sb.Append("\n\n");
            sb.Append("<b>").Append(title).Append("</b>\n").Append(body);
        }

        static string DescribeStatusForTooltip(StatusInstance status, StatusDefinition def)
        {
            if (status == null)
                return "";

            switch (status.StatusId)
            {
                case StatusCatalog.AnubisAvatar:
                    return "生命/攻击/防御 +50%；剩余禁出牌由化身状态维护";
                case StatusCatalog.BloodlineLegacy:
                    return "最大生命 +50%（当前生命不变）";
                case StatusCatalog.PlagueSpread:
                    return "敌人因中毒受伤时，30% 概率向相邻敌人传染一半层数";
                case StatusCatalog.HolyInfusionPending:
                    return "（已废弃）旧版神圣灌注待重复状态";
                case StatusCatalog.Poison:
                    return "回合开始每层 1 伤害，无视护甲";
                case StatusCatalog.Burn:
                    return "回合结束每层 2 伤害";
                case StatusCatalog.Weaken:
                    return AppendStatusDurationLine(
                        $"造成的伤害减少 {status.Stacks}%（每层 -1%）",
                        status, def);
                case StatusCatalog.Vulnerable:
                    return AppendStatusDurationLine(
                        $"受到的伤害增加 {status.Stacks}%（每层 +1%）",
                        status, def);
                case StatusCatalog.LastStand:
                    return "增伤 +20%；HP 将降至 0 以下时保留 1 HP";
                case StatusCatalog.Taunt:
                    return "敌人下一行动强制攻击自身";
                case StatusCatalog.ReviveBlessing:
                    return "HP 归零时恢复 25% HP（每场 1 次）";
                case StatusCatalog.GodDescends:
                    return "本场战斗中，每当获得护甲时，对所有敌人造成 8 点伤害";
                case StatusCatalog.MermaidTidalCostCut:
                    return AppendStatusDurationLine(
                        $"劈砍与破浪斩的能量消耗 -{status.Stacks}（每层 -1，可叠加；各层按自身剩余回合独立到期）",
                        status, def);
                case StatusCatalog.SpiderPoisonVulnerable:
                    return AppendStatusDurationLine(
                        $"受到的伤害增加 {status.Stacks}%（蜘蛛贵妇：每 5 层中毒视为 10% 易伤）",
                        status, def);
                case StatusCatalog.DarkKnightPoisonVulnerable:
                    return AppendStatusDurationLine(
                        $"受到的伤害增加 {status.Stacks}%（黑暗骑士：每层中毒视为 1 层易伤）",
                        status, def);
                case StatusCatalog.HandCostZero:
                    return AppendStatusDurationLine("己方手牌费用变为 0", status, def);
                case StatusCatalog.Slow:
                    return AppendStatusDurationLine(
                        $"速度 -{status.Stacks}（每层 -1）",
                        status, def);
                case StatusCatalog.SpeedUp:
                case StatusCatalog.SnakeSwiftness:
                    return AppendStatusDurationLine(
                        $"速度 +{status.Stacks}（每层 +1）",
                        status, def);
                case StatusCatalog.DamageReduction:
                    return AppendStatusDurationLine(
                        $"受到的伤害减少 {status.Stacks}%（每层 -1%）",
                        status, def);
                case StatusCatalog.Unyielding:
                    return "不屈：特定条件下不会被击败或触发对应保命效果";
                case StatusCatalog.BoneWorkshop:
                    return "骨之王座：获得时与每回合开始召唤易爆骷髅头";
                case StatusCatalog.GhostQueenWrath:
                    return "幽灵女王之怒：大幅提升攻击伤害（机制增益，不可驱散）";
                case StatusCatalog.SandSpearReforge:
                    return "沙矛重塑：相关沙矛/重塑效果已激活";
                case StatusCatalog.RatSwarmCall:
                    return "鼠群呼唤：死亡时召唤鼠群克隆";
                case StatusCatalog.SoulDrain:
                    return "摄魂：下回合玩家能量回复减少";
                case StatusCatalog.FinalSummonPending:
                    return AppendStatusDurationLine(
                        "终焉召唤倒计时：到期时召唤终焉造物",
                        status, def);
                case StatusCatalog.RespondStance:
                    return "应对姿态：成功应对时获得 8 护甲";
                case StatusCatalog.BattleWill:
                    return "战意觉醒：受到生命损失时获得 5% 增伤";
                case StatusCatalog.HeavyArmor:
                    return "重甲强化：获得护甲时额外 +20%";
                case StatusCatalog.FinalBulwark:
                    return "最终壁垒：回合结束仅清除 50% 护甲";
                case StatusCatalog.RotAvatar:
                    return "腐朽化身：敌人回合开始时对其施加 2 层中毒";
                case StatusCatalog.BloodFrenzy:
                    return "鲜血狂欢：献祭后获得 5% 增伤";
                case StatusCatalog.BloodSharing:
                    return "分血仪式：回复生命时，其他我方也回复 30%";
                case StatusCatalog.Constrict:
                    return AppendStatusDurationLine(
                        $"缠绕：每回合开始受到 {status.Stacks} 点伤害；期间可能无法出牌",
                        status, def);
                case StatusCatalog.VenomSacBurst:
                    return "毒囊破裂：施加中毒时额外 +1 层";
                case StatusCatalog.ImmortalShed:
                    return AppendStatusDurationLine(
                        "不朽蛇蜕：获得中毒时额外获得 10% 增伤",
                        status, def);
                case StatusCatalog.PrayAncientSnakeGod:
                    return "祈求远古蛇神：每回合注入蛇神的回应相关效果";
                case StatusCatalog.DelayedDamage:
                    return AppendStatusDurationLine(
                        $"延迟伤害：下回合开始受到 {status.Stacks} 点伤害",
                        status, def);
                case StatusCatalog.EtherealOnNextHit:
                    return "两界行者：下次受到伤害后获得虚化";
                case StatusCatalog.PsionicBody:
                    return "灵能体：非战斗回合相关增伤效果已激活";
                case StatusCatalog.SealedNextCard:
                    return "灵界封印：敌方使用的下一张卡将失效";
                case StatusCatalog.SealNextStatusCard:
                    return "状态封印：目标下一张状态牌无法有效使用";
                case StatusCatalog.DespairSoulRecall:
                    return "绝望之魂：获得虚化时从弃牌堆将相关牌加入手牌";
                case StatusCatalog.EternalVoid:
                    return "永恒虚无：永久虚化，每回合受到 25% 最大生命的真实伤害";
                case StatusCatalog.SnakeGodChanneling:
                    return "蛇神降临：蛇神回应链相关标记";
                case StatusCatalog.DebuffImmune:
                    return "减益免疫：不会再受到新的减益状态";
                case StatusCatalog.ThornArmor:
                    return "荆棘护甲：相关反伤/护甲效果已激活";
                case StatusCatalog.BattleRoar:
                    return "战斗咆哮：相关战吼增伤效果已激活";
                case StatusCatalog.DoomProphecy:
                    return "末日预言：使用卡牌（行动）后受到 5 点伤害（永久）";
                case StatusCatalog.LifeSpring:
                    return "生命之泉：相关持续回复效果已激活";
                case StatusCatalog.PainConvert:
                    return "苦痛转化：相关伤害转化效果已激活";
                case StatusCatalog.SnakeNest:
                    return "千蛇窟：相关蛇群效果已激活";
                case StatusCatalog.PsionicArrowRain:
                    return "灵能箭雨：相关灵能箭雨效果已激活";
                case StatusCatalog.PsionicMastery:
                    return "灵能掌握：相关灵能掌握效果已激活";
                case StatusCatalog.SoulBond:
                    return "灵魂纽带：相关灵魂纽带效果已激活";
                case StatusCatalog.NecroticPoison:
                    return AppendStatusDurationLine(
                        "亡灵毒：回合开始造成伤害的特殊中毒",
                        status, def);
                case StatusCatalog.FinalBloodRitual:
                    return "触发【献祭】时回复 5 HP，并在下回合开始时抽 1 张牌";
                case StatusCatalog.VampAura:
                    return $"攻击吸血 {status.Stacks}%";
                case StatusCatalog.AttackUp:
                case StatusCatalog.DamageUp:
                    return AppendStatusDurationLine(
                        $"所有攻击牌伤害 +{status.Stacks * (def?.OutgoingDamageFlatPerStack ?? 1)}（每层 +{def?.OutgoingDamageFlatPerStack ?? 1}）",
                        status, def);
                case StatusCatalog.AttackUpPercent:
                    return AppendStatusDurationLine(
                        $"所有攻击牌伤害 +{status.Stacks * (def?.AttackPercentBonusPerStack ?? 1)}%（每层 +{def?.AttackPercentBonusPerStack ?? 1}%）",
                        status, def);
                case StatusCatalog.DivinePunishmentAtk:
                    return AppendStatusDurationLine(
                        $"所有攻击牌伤害 +{status.Stacks * (def?.AttackPercentBonusPerStack ?? 1)}%（每层 +{def?.AttackPercentBonusPerStack ?? 1}%）",
                        status, def);
                case StatusCatalog.AttackDown:
                    return AppendStatusDurationLine(
                        $"造成的伤害减少 {status.Stacks}%（每层 -1%）",
                        status, def);
                case StatusCatalog.DefenseUp:
                case StatusCatalog.ArmorUp:
                    return AppendStatusDurationLine(
                        $"强固：获得护甲每层 +{def?.BlockGainFlatPerStack ?? 1}",
                        status, def);
                case StatusCatalog.DefenseUpPercent:
                    return AppendStatusDurationLine(
                        $"强固：获得护甲 +{status.Stacks * (def?.DefensePercentBonusPerStack ?? 1)}%（每层 +{def?.DefensePercentBonusPerStack ?? 1}%）",
                        status, def);
                case StatusCatalog.Guard:
                    return "本回合队友受到的伤害转移给自身，并减伤 50%";
                case StatusCatalog.Ethereal:
                    return "受到的攻击伤害最多造成 1 点";
                case StatusCatalog.Invulnerable:
                    return "本回合剩余时间内不会受到伤害";
                case StatusCatalog.Deterrence:
                    return "下回合无法使用卡牌";
                case StatusCatalog.DefenseDownPercent:
                case StatusCatalog.ArmorDown:
                    return AppendStatusDurationLine(
                        $"破损：获得护甲减少 {status.Stacks * (def?.BlockGainReductionPercentPerStack ?? 1)}%（每层 -{def?.BlockGainReductionPercentPerStack ?? 1}%）",
                        status, def);
                case StatusCatalog.RisingTide:
                    return AppendStatusDurationLine(
                        $"减伤 +{status.Stacks * (def?.IncomingDamageReductionPercentPerStack ?? 15)}%，增伤 +{status.Stacks * (def?.AttackPercentBonusPerStack ?? 10)}%（每层 15% 减伤 / 10% 增伤）；达到 {V09BossMechanicsRules.RisingTideEbbThreshold} 层时消耗全部涨潮并获得退潮",
                        status, def);
                case StatusCatalog.WaveSurge:
                    return $"攻击伤害 +{status.Stacks}%（同位置速度优势；无同位置敌人时为 50%）";
                case StatusCatalog.PhantomCaptainFrenzyAtk:
                    return $"攻击伤害 +{status.Stacks}%（敌方有角色死亡或血量低于 25% 时）";
                case StatusCatalog.PhantomCaptainFrenzyVuln:
                    return $"受到的伤害 +{status.Stacks}%（敌方有角色死亡或血量低于 25% 时）";
                case StatusCatalog.KnightAssaultStanceAtk:
                    return $"攻击伤害 +{status.Stacks}%（突击姿态：非前排）";
                case StatusCatalog.KnightAssaultStanceVuln:
                    return $"受到的伤害 +{status.Stacks}%（突击姿态：非前排）";
                case StatusCatalog.KnightBackToWallAtk:
                    return $"攻击伤害 +{status.Stacks}%（背水一战：护甲超过HP）";
                case StatusCatalog.RangerLowHpFuryAtk:
                    return $"攻击伤害 +{status.Stacks}%（低血狂怒：HP低于30%）";
                case StatusCatalog.RangerSoloHuntAtk:
                    return $"攻击伤害 +{status.Stacks}%（孤猎：非Boss且敌方仅剩一人）";
                case StatusCatalog.KnightComboAtk:
                    return AppendStatusDurationLine(
                        $"攻击伤害 +{status.Stacks}%（连击：本回合连续打出三张战士攻击牌）",
                        status, def);
                case StatusCatalog.RangerBloodDebtAtk:
                    return $"攻击伤害 +{status.Stacks}%（血债累击：远征献祭累计）";
                case StatusCatalog.EbbingTide:
                    return AppendStatusDurationLine(
                        $"无法获得涨潮；受到的伤害 +{status.Stacks * (def?.IncomingDamagePercentPerStack ?? 50)}%",
                        status, def);
                case StatusCatalog.TideLocked:
                {
                    // 施加时多写 1 回合抵消当回合末 tick；展示用 RemainingTurns-1 对齐卡面「持续 N 回合」
                    var displayTurns = status.RemainingTurns > 0
                        ? status.RemainingTurns - 1
                        : status.RemainingTurns;
                    var display = new StatusInstance
                    {
                        StatusId = status.StatusId,
                        Stacks = status.Stacks,
                        RemainingTurns = displayTurns
                    };
                    return AppendStatusDurationLine(
                        $"涨潮锁定在 {V09BossMechanicsRules.TideLockedStackCount} 层",
                        display, def);
                }
                case StatusCatalog.TideEmpower:
                    return "魔化潮汐：每层涨潮额外提供 5% 减伤";
                case StatusCatalog.BrandMark:
                    return "累计三层时该角色会即死（引爆并造成等同最大生命的伤害）";
                default:
                    break;
            }

            if (def == null)
                return FormatStatusDuration(status, def);

            var parts = new List<string>();
            if (def.TurnStartDamagePerStack > 0)
                parts.Add($"回合开始每层 {def.TurnStartDamagePerStack} 伤害");
            if (def.TurnEndDamagePerStack > 0)
                parts.Add($"回合结束每层 {def.TurnEndDamagePerStack} 伤害");
            if (def.OutgoingDamageFlatPerStack > 0)
                parts.Add($"所有攻击牌伤害 +{status.Stacks * def.OutgoingDamageFlatPerStack}（每层 +{def.OutgoingDamageFlatPerStack}）");
            if (def.OutgoingDamagePercentPerStack > 0)
                parts.Add($"所有攻击牌伤害 +{status.Stacks * def.OutgoingDamagePercentPerStack}%（每层 +{def.OutgoingDamagePercentPerStack}%）");
            if (def.AttackPercentBonusPerStack > 0)
                parts.Add($"所有攻击牌伤害 +{status.Stacks * def.AttackPercentBonusPerStack}%（每层 +{def.AttackPercentBonusPerStack}%）");
            if (def.IncomingDamagePercentPerStack > 0)
                parts.Add($"受到的伤害每层 +{def.IncomingDamagePercentPerStack}%");
            if (def.IncomingDamageReductionPercentPerStack > 0)
                parts.Add($"减伤 +{status.Stacks * def.IncomingDamageReductionPercentPerStack}%（每层 +{def.IncomingDamageReductionPercentPerStack}%）");
            if (def.BlockGainReductionPercentPerStack > 0)
                parts.Add($"获得护甲减少 {status.Stacks * def.BlockGainReductionPercentPerStack}%（每层 -{def.BlockGainReductionPercentPerStack}%）");
            if (def.MaxHpPercentBonusPerStack > 0)
                parts.Add($"最大生命每层 +{def.MaxHpPercentBonusPerStack}%");
            if (def.BlockGainFlatPerStack > 0)
                parts.Add($"获得护甲每层 +{def.BlockGainFlatPerStack}");
            if (def.BlockGainPercentPerStack > 0)
                parts.Add($"获得护甲每层 +{def.BlockGainPercentPerStack}%");

            var duration = FormatStatusDuration(status, def);
            if (parts.Count == 0)
                return duration;

            var text = string.Join("；", parts);
            return string.IsNullOrEmpty(duration) ? text : $"{text}\n{duration}";
        }

        static string AppendStatusDurationLine(string body, StatusInstance status, StatusDefinition def)
        {
            var duration = FormatStatusDuration(status, def);
            return string.IsNullOrEmpty(duration) ? body : $"{body}\n{duration}";
        }

        public static string FormatStatusDuration(StatusInstance status, StatusDefinition definition = null)
        {
            if (status == null)
                return "";

            // 以实例 RemainingTurns 为准：<0 永久；>0 剩余回合；0 即将在回合开始结算后消失
            if (status.RemainingTurns < 0)
                return "永久";

            if (status.RemainingTurns <= 0)
                return "本回合";

            return $"{status.RemainingTurns}回合";
        }

        public static string FormatBloodRageDisplay(int stacks)
        {
            if (stacks <= 0)
                return "";

            var bonusPercent = stacks * MinionTraitCatalog.OgreBloodRageDamagePercentPerStack;
            return $"血怒×{stacks}  下一张攻击+{bonusPercent}%";
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
                var ownerName = owner != null
                    ? FormatCombatantDisambiguatedName(state, owner)
                    : ShortOwner(card.OwnerCharacterId);

                if (owner != null && owner.Team == TeamSide.Enemy)
                {
                    if (IsEnemyIntentHidden(state, step.CardInstanceId, snapshot.TurnEnemyIntents))
                    {
                        lines.Add($"#{global} ? ({ownerName})");
                        continue;
                    }

                    var effect = CardPreviewRules.DescribeIntentEffect(state, owner, card);
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
                var ownerName = owner != null
                    ? FormatCombatantDisambiguatedName(state, owner)
                    : ShortOwner(card.OwnerCharacterId);

                if (owner != null && owner.Team == TeamSide.Enemy)
                {
                    if (IsEnemyIntentHidden(state, step.CardInstanceId))
                    {
                        lines.Add($"#{global} ? ({ownerName})");
                        continue;
                    }

                    var effect = CardPreviewRules.DescribeIntentEffect(state, owner, card);
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

                string assignedId = null;
                if (snapshot.TurnTargetByCardId != null
                    && snapshot.TurnTargetByCardId.TryGetValue(step.CardInstanceId, out var tid))
                    assignedId = tid;

                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = i + 1,
                    Card = card,
                    IsHidden = hidden,
                    CardTitle = FormatActionOrderCardTitle(card, hidden),
                    OwnerArrowTarget = FormatActionOrderOwnerArrowTarget(state, owner, card, hidden, assignedId),
                    DisplayName = owner != null && owner.Team == TeamSide.Enemy
                        ? FormatEnemyActionOrderLabel(state, owner, card, hidden)
                        : (hidden ? "?" : card.DisplayName)
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

                var assignedId = draft?.GetAssignedTarget(step.CardInstanceId);
                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = i + 1,
                    Card = card,
                    IsHidden = hidden,
                    CardTitle = FormatActionOrderCardTitle(card, hidden),
                    OwnerArrowTarget = FormatActionOrderOwnerArrowTarget(state, owner, card, hidden, assignedId),
                    DisplayName = owner != null && owner.Team == TeamSide.Enemy
                        ? FormatEnemyActionOrderLabel(state, owner, card, hidden)
                        : (hidden ? "?" : card.DisplayName)
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

                var owner = !string.IsNullOrEmpty(intent.OwnerCombatantId)
                    ? state.GetCombatant(intent.OwnerCombatantId)
                    : null;
                if (owner == null)
                {
                    var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                    owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                }

                entries.Add(new ActionOrderVisualEntry
                {
                    OrderIndex = order++,
                    Card = card,
                    IsHidden = intent.IsHidden,
                    CardTitle = FormatActionOrderCardTitle(card, intent.IsHidden),
                    OwnerArrowTarget = FormatActionOrderOwnerArrowTarget(state, owner, card, intent.IsHidden),
                    DisplayName = FormatEnemyActionOrderLabel(state, owner, card, intent.IsHidden)
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
            var targetName = FormatCombatantDisambiguatedName(state, assigned);
            return string.IsNullOrEmpty(damageNote)
                ? $" → {targetName}"
                : $" → {targetName} ({damageNote})";
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
            var actorName = owner != null ? FormatCombatantDisambiguatedName(state, owner) : "敌";
            if (isHidden || card == null)
                return $"{actorName} ？";

            var effect = CardPreviewRules.DescribeIntentEffect(state, owner, card);
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

            return $" → {FormatCombatantDisambiguatedName(state, target)}";
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
                case ExpeditionNodeType.Elite: return "精英战斗";
                case ExpeditionNodeType.Treasure: return "宝箱";
                case ExpeditionNodeType.Event: return "事件";
                case ExpeditionNodeType.Shop: return "流浪商人";
                case ExpeditionNodeType.Shrine: return "祭坛";
                case ExpeditionNodeType.Boss: return "Boss";
                default: return type.ToString();
            }
        }

        static bool TryBuildExcelDescriptionLine(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions,
            out string line)
        {
            line = "";
            if (card == null || string.IsNullOrEmpty(card.DisplayName))
                return false;

            // 目录按 CardId 命中则直接采用（蛇神回应等 token 与 SO 默认 Reach 等可能不一致）。
            // 但已升级/改写过的实例必须走动态描述，否则当前/升级后效果会显示成同一句静态文案。
            var hasIdEntry = CardDescriptionCatalog.TryGetByCardId(card.DefinitionId, out var excelText);
            if (!hasIdEntry
                && !CardDescriptionCatalog.TryGetByDisplayName(card.DisplayName, out excelText))
                return false;

            if (definitions != null
                && definitions.TryGetValue(card.DefinitionId, out var def)
                && def != null
                && !CardVisualResolver.MatchesDefinitionBaseline(card, def))
                return false;

            if (string.IsNullOrWhiteSpace(excelText))
                return false;

            var lines = new List<string> { excelText.Trim() };

            var assignedId = draft?.GetAssignedTarget(card.InstanceId);
            if (!string.IsNullOrEmpty(assignedId) && state != null)
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null)
                    lines.Add($"→ {assigned.DisplayName}");
            }

            if (card.DefinitionId == PassiveCardMechanicsRules.EndlessBladeCardId)
                lines.Add("使用后此牌伤害在本场战斗中翻倍");

            line = string.Join("\n", lines);
            return true;
        }

        static bool TryBuildCurseCardStatsLine(CardInstanceState card, out string line)
        {
            line = "";
            if (card == null || !IsCurseCard(card))
                return false;

            line = card.DefinitionId switch
            {
                "curse_chaos_touch" => "【诅咒】无法被打出，但会被正常抽取和弃牌",
                _ => "【诅咒】无法被打出，但会被正常抽取和弃牌"
            };
            return true;
        }

        static bool IsCurseCard(CardInstanceState card) =>
            card.Keywords.Contains("curse")
            || (!string.IsNullOrEmpty(card.DefinitionId) && card.DefinitionId.StartsWith("curse_"));
    }
}
