using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CardKeywordTooltipBuilder
    {
        public static string BuildRichTooltip(CardInstanceState card, CombatantState owner, BattleState state = null)
        {
            if (card?.Actions == null)
                return "";

            var pickSide = CardRules.GetRequiredTargetPick(card);
            var sb = new StringBuilder();
            AppendReachTooltips(sb, card, pickSide);
            AppendKeywordTooltips(sb, card);
            AppendFormulaLines(sb, card, owner);

            return sb.ToString();
        }

        static void AppendReachTooltips(StringBuilder sb, CardInstanceState card, TargetPickSide pickSide)
        {
            var seen = new HashSet<string>();
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                var line = CardReachFormatter.BuildReachTooltipRichText(action, pickSide);
                if (string.IsNullOrEmpty(line) || !seen.Add(line))
                    continue;

                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append(line);
            }
        }

        static void AppendKeywordTooltips(StringBuilder sb, CardInstanceState card)
        {
            if (card.Keywords == null)
                return;

            foreach (var kw in card.Keywords)
            {
                if (string.IsNullOrEmpty(kw))
                    continue;

                if (!KeywordCatalog.TryGet(kw, out var def))
                    continue;

                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append("<b>").Append(def.DisplayName).Append("</b>\n").Append(def.Description);
            }
        }

        static void AppendFormulaLines(StringBuilder sb, CardInstanceState card, CombatantState owner)
        {
            foreach (var line in BuildFormulaLines(card, owner))
            {
                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append(line);
            }
        }

        static IEnumerable<string> BuildFormulaLines(CardInstanceState card, CombatantState owner)
        {
            var skipSacrificeHp = card.Keywords != null && card.Keywords.Contains("sacrifice");
            var seen = new HashSet<string>();

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                {
                    var respond = FormatRespondFormula(action);
                    if (!string.IsNullOrEmpty(respond) && seen.Add(respond))
                        yield return respond;
                    continue;
                }

                foreach (var line in BuildFormulaLinesForAction(action, owner, skipSacrificeHp))
                {
                    if (seen.Add(line))
                        yield return line;
                }
            }
        }

        static IEnumerable<string> BuildFormulaLinesForAction(
            EffectActionSpec action,
            CombatantState owner,
            bool skipSacrificeHp)
        {
            if (action.Condition != ReactionConditionType.None)
            {
                var respond = FormatRespondFormula(action);
                if (!string.IsNullOrEmpty(respond))
                    yield return respond;
                yield break;
            }

            switch (action.Type)
            {
                case EffectActionType.DealDamage when action.Target != EffectTarget.Self:
                    yield return FormatScaledFormula("伤害", action, owner, useDefense: false);
                    foreach (var note in FormatDamageEffectNotes(action))
                        yield return note;
                    break;
                case EffectActionType.DealDamage when action.Target == EffectTarget.Self:
                    if (!skipSacrificeHp)
                        yield return $"<b>【献祭伤害】</b>\n{action.Value} HP";
                    break;
                case EffectActionType.GainBlock:
                    if (action.ScaleWithDefense)
                        yield return FormatScaledFormula("护甲", action, owner, useDefense: true);
                    else if (action.Value > 0)
                        yield return $"<b>【护甲数值】</b>\n{action.Value}";
                    break;
                case EffectActionType.Heal:
                    if (action.ScaleWithAttack)
                        yield return FormatScaledFormula("治疗", action, owner, useDefense: false);
                    else if (action.Value > 0)
                        yield return $"<b>【治疗数值】</b>\n{action.Value} HP";
                    break;
            }
        }

        static IEnumerable<string> FormatDamageEffectNotes(EffectActionSpec action)
        {
            if (action.LifestealPercent >= 100)
                yield return "<b>【吸血】</b>\n回复造成伤害 100% 的生命（等量 HP）";
            else if (action.LifestealPercent > 0)
                yield return $"<b>【吸血】</b>\n回复造成伤害 {action.LifestealPercent}% 的 HP";

            if (action.OnKillHealAmount > 0)
                yield return $"<b>【击杀回复】</b>\n击杀目标后恢复 {action.OnKillHealAmount} HP";

            if (action.SplashBehindTarget)
                yield return $"<b>【贯通】</b>\n对目标身后敌人造成 {action.SplashPowerPercent}% 伤害";

            if (action.IgnoreDefPercent >= 100)
                yield return "<b>【破防】</b>\n完全无视目标防御";
            else if (action.IgnoreDefPercent > 0)
                yield return $"<b>【破防】</b>\n无视目标 {action.IgnoreDefPercent}% 防御";

            if (action.BonusIfTargetHpBelowPercent > 0 && action.BonusIfTargetHpBelowFlat > 0)
                yield return $"<b>【斩杀】</b>\n目标 HP 低于 {action.BonusIfTargetHpBelowPercent}% 时额外 +{action.BonusIfTargetHpBelowFlat} 伤害";

            if (action.BonusIfTargetHitThisTurnPercent > 0)
                yield return $"<b>【连击】</b>\n目标本回合已被攻击则伤害 +{action.BonusIfTargetHitThisTurnPercent}%";
        }

        static string FormatRespondFormula(EffectActionSpec action)
        {
            return action.Type switch
            {
                EffectActionType.GainBlockFromLastDamagePercent =>
                    $"<b>【减伤比例】</b>\n所受伤害×{action.Value}%",
                EffectActionType.ReflectLastDamageToAttacker =>
                    $"<b>【反击比例】</b>\n所受伤害×{action.Value}%",
                _ => ""
            };
        }

        static string FormatScaledFormula(string label, EffectActionSpec action, CombatantState owner, bool useDefense)
        {
            string formula = CardActionValueText.FormatPlain(action, useDefense);

            var preview = owner != null
                ? $"（当前约 {CardPowerRules.ComputeActionValue(action, owner)}）"
                : "";

            return $"<b>【{label}计算公式】</b>\n{formula}{preview}";
        }
    }
}
