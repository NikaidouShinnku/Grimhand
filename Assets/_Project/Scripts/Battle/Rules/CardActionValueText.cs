using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>牌面/预览用数值与公式文本（纯文本，无富文本标签）。</summary>
    public static class CardActionValueText
    {
        public static bool HasScaledComponent(EffectActionSpec action)
        {
            if (action == null)
                return false;

            if (action.ScaleWithAttack)
                return true;

            return action.ScaleWithDefense;
        }

        public static string FormatPlain(EffectActionSpec action, bool useDefense)
        {
            if (action == null)
                return "0";

            var percent = useDefense ? action.DefenseScalePercent : action.AttackScalePercent;
            var flat = action.Value;
            var pct = percent / 100f;
            var statName = useDefense ? "DEF" : "ATK";

            if (flat == 0 && pct == 1f)
                return $"{statName}×1";
            if (flat == 0)
                return $"{statName}×{FormatMultiplier(pct)}";
            if (pct == 1f)
                return $"{statName}×1+{flat}";
            return $"{statName}×{FormatMultiplier(pct)}+{flat}";
        }

        public static string DescribeDamage(EffectActionSpec action, CombatantState owner, bool preferFormulas)
        {
            var extra = FormatDamageExtras(action);
            if (preferFormulas && HasScaledComponent(action))
                return $"造成 {FormatPlain(action, useDefense: false)} 的伤害{extra}";

            var dmg = CardPowerRules.ComputeActionValue(action, owner);
            return $"造成 {dmg} 点伤害{extra}";
        }

        public static string FormatDamageExtras(EffectActionSpec action)
        {
            if (action == null)
                return "";

            var parts = new System.Collections.Generic.List<string>();
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

        public static string DescribeHeal(EffectActionSpec action, CombatantState owner, bool preferFormulas)
        {
            if (preferFormulas && owner == null && action.ScaleWithAttack)
                return $"恢复 {FormatPlain(action, useDefense: false)} 的生命";

            var heal = CardPowerRules.ComputeActionValue(action, owner);
            return $"恢复 {heal} 点生命";
        }

        public static string DescribeBlock(EffectActionSpec action, CombatantState owner, bool preferFormulas)
        {
            if (preferFormulas && owner == null && action.ScaleWithDefense)
                return $"获得 {FormatPlain(action, useDefense: true)} 的护甲";

            var block = CardPowerRules.ComputeActionValue(action, owner);
            return $"获得 {block} 点护甲";
        }

        public static string FormatMultiplier(float m)
        {
            if (System.Math.Abs(m - 0.8f) < 0.001f) return "0.8";
            if (System.Math.Abs(m - 1.2f) < 0.001f) return "1.2";
            if (System.Math.Abs(m - 1.5f) < 0.001f) return "1.5";
            if (System.Math.Abs(m - 1.6f) < 0.001f) return "1.6";
            if (System.Math.Abs(m - 1.7f) < 0.001f) return "1.7";
            if (System.Math.Abs(m - 1.8f) < 0.001f) return "1.8";
            if (System.Math.Abs(m - 2f) < 0.001f) return "2.0";
            if (System.Math.Abs(m - 0.5f) < 0.001f) return "0.5";
            if (System.Math.Abs(m - 0.7f) < 0.001f) return "0.7";
            if (System.Math.Abs(m - 1f) < 0.001f) return "1.0";
            if (System.Math.Abs(m - 1.3f) < 0.001f) return "1.3";
            return m.ToString("0.##");
        }
    }
}
