using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>圣阳之书：名字含「阳」或「日」的牌使用时视为等级 +N（可超出升级上限）。</summary>
    public static class HolysunSpellbookRules
    {
        public static bool MatchesSunCardName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;

            return displayName.IndexOf('阳') >= 0 || displayName.IndexOf('日') >= 0;
        }

        public static CardInstanceState ApplyForResolution(
            RunModifierSnapshot mods,
            CombatantState actor,
            CardInstanceState card)
        {
            if (mods == null || card == null || mods.HolysunSpellbookBonusUpgradeLevels <= 0)
                return card;

            if (actor == null || actor.Team != TeamSide.Player)
                return card;

            if (!MatchesSunCardName(card.DisplayName))
                return card;

            return CardInstanceUpgradeApplier.ApplyBonusLevels(
                card,
                mods.HolysunSpellbookBonusUpgradeLevels);
        }
    }
}
