using System.Collections.Generic;

namespace Grimhand.Expedition
{
    /// <summary>正式玩家卡牌白名单：仅 w_/p_/d_ 前缀（对照内容总览表），排除旧 demo 的 k_/r_/g_ 卡。</summary>
    public static class PlayerCardCatalogRules
    {
        static readonly HashSet<string> AllowedPrefixes = new() { "w_", "p_", "d_" };

        public static bool IsAllowedPlayerCard(string cardId, string ownerCharacterId)
        {
            if (!IsAllowedPlayerCardId(cardId))
                return false;

            return ExpeditionCardPool.IsPlayerCharacterId(ownerCharacterId);
        }

        public static bool IsAllowedPlayerCardId(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
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
