using Grimhand.Battle.Model;

namespace Grimhand.Battle.Reactions
{
    /// <summary>
    /// 应对卡识别：带 parry / respond_* 关键词，或带条件效果（Condition != None）。
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
