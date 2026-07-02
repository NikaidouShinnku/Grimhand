using System.Collections.Generic;

namespace Grimhand.Expedition
{
    /// <summary>正式玩家卡牌白名单：仅 w_/p_/d_/v_/l_ 前缀（对照内容总览表），排除旧 demo 的 k_/r_/g_ 卡。</summary>
    public static class PlayerCardCatalogRules
    {
        static readonly HashSet<string> AllowedPrefixes = new() { "w_", "p_", "d_", "v_", "l_" };

        /// <summary>衍生 token 卡（如蛇神的回应）：不计入奖励/商店卡池。</summary>
        static readonly HashSet<string> ExcludedTokenIds = new() { "v_snake_god_response" };

        public static bool IsAllowedPlayerCard(string cardId, string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(cardId))
                return false;

            // 诅咒牌（如混沌之触）无归属角色，作为额外污染牌入池，不占用任何角色卡位。
            if (cardId.StartsWith("curse_"))
                return true;

            if (!IsAllowedPlayerCardId(cardId))
                return false;

            return ExpeditionCardPool.IsPlayerCharacterId(ownerCharacterId);
        }

        public static bool IsAllowedPlayerCardId(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
                return false;

            if (ExcludedTokenIds.Contains(definitionId))
                return false;

            foreach (var prefix in AllowedPrefixes)
            {
                if (definitionId.StartsWith(prefix))
                    return true;
            }

            return definitionId.StartsWith("curse_");
        }
    }
}
