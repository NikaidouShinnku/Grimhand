using Grimhand.Battle.Model;

namespace Grimhand.Battle.Reactions
{
    /// <summary>
    /// 应对卡：绑定敌人本回合首张满足条件的出牌，在其结算前按队列顺序依次生效。
    /// </summary>
    public static class RespondRules
    {
        public static bool IsRespondCard(CardInstanceState card)
        {
            if (card == null)
                return false;

            if (card.Keywords.Contains("parry"))
                return true;

            if (card.Keywords.Contains("respond_defense") || card.Keywords.Contains("respond_status"))
                return true;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    return true;
            }

            return false;
        }
    }
}
