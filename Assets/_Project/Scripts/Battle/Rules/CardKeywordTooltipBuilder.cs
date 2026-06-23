using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CardKeywordTooltipBuilder
    {
        public static string BuildRichTooltip(CardInstanceState card, CombatantState owner, BattleState state = null)
        {
            if (card?.Keywords == null || card.Keywords.Count == 0)
                return "";

            return KeywordCatalog.BuildRichTooltipText(card.Keywords);
        }
    }
}
