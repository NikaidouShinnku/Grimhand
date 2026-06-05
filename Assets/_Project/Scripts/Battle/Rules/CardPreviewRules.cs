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
    }
}
