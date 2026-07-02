using System.Collections.Generic;

namespace Grimhand.Expedition
{
    /// <summary>玩家角色 ID → 显示名（军营/战斗配置同步用）。</summary>
    public static class CharacterDisplayNames
    {
        static readonly Dictionary<string, string> ById = new()
        {
            [TalentCatalog.KnightId] = "战士",
            [TalentCatalog.MageId] = "法老",
            [TalentCatalog.RangerId] = "恶魔",
            [TalentCatalog.SnakeQueenId] = "毒蛇女王",
            [TalentCatalog.LichQueenId] = "巫妖女王",
        };

        public static string Get(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return "";

            return ById.TryGetValue(characterDefinitionId, out var name) ? name : "";
        }

        public static string GetOrFallback(string characterDefinitionId, string fallback = "")
        {
            var resolved = Get(characterDefinitionId);
            return string.IsNullOrEmpty(resolved) ? fallback ?? "" : resolved;
        }
    }
}
