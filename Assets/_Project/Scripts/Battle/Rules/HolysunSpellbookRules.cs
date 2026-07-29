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

        /// <summary>结算与卡面展示共用：把额外等级写入数值与 UpgradeLevel。</summary>
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

        /// <summary>手牌/横幅/提示用：按当前战斗修饰符生成展示用卡牌实例。</summary>
        public static CardInstanceState ApplyForDisplay(BattleState state, CardInstanceState card)
        {
            if (state == null || card == null)
                return card;

            var ownerId = PositionRules.GetOwnerCombatantId(state, card);
            var owner = !string.IsNullOrEmpty(ownerId) ? state.GetCombatant(ownerId) : null;
            return ApplyForResolution(state.Config?.RunModifiers, owner, card);
        }
    }
}
