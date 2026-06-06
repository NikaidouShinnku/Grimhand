using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>牌面第一行：机制关键词（【献祭8】【AOE】等；位置标签在效果行，见 CardReachFormatter）。</summary>
    public static class CardFaceKeywordFormatter
    {
        static readonly HashSet<string> ExcludeFromFace = new() { "position" };

        public static string Build(CardInstanceState card, CombatantState owner, BattleState state = null)
        {
            if (card?.Keywords == null || card.Keywords.Count == 0)
                return "";

            var sb = new StringBuilder();
            foreach (var kw in card.Keywords)
            {
                if (string.IsNullOrEmpty(kw) || ExcludeFromFace.Contains(kw))
                    continue;

                if (kw == "sacrifice")
                {
                    var cost = GetSacrificeHpCost(card, owner, state);
                    sb.Append(cost > 0 ? $"【献祭{cost}】" : "【献祭】");
                    continue;
                }

                if (!KeywordCatalog.TryGet(kw, out var def))
                    continue;

                sb.Append('【').Append(def.DisplayName).Append('】');
            }

            return sb.ToString();
        }

        static int GetSacrificeHpCost(CardInstanceState card, CombatantState owner, BattleState state)
        {
            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.DealDamage || action.Target != EffectTarget.Self)
                    continue;

                var value = CardPowerRules.ComputeActionValue(action, owner);
                if (state != null && owner != null)
                {
                    value = RelicEffectRules.AdjustSacrificeSelfDamage(
                        state.Config?.RunModifiers, owner, value);
                }

                return value;
            }

            return 0;
        }
    }
}
