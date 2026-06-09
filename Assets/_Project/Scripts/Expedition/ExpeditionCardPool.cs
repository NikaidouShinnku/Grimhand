using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征奖励/商店共用的玩家卡牌池（含非初始牌组的全部玩家卡）。</summary>
    public static class ExpeditionCardPool
    {
        static readonly HashSet<string> PlayerCharacterIds = new()
        {
            "char_knight", "char_warrior",
            "char_mage", "char_pharaoh",
            "char_ranger", "char_demon"
        };

        public static List<CardTemplate> CollectPlayerCardTemplates(ExpeditionConfig config)
        {
            var result = new List<CardTemplate>();
            var seen = new HashSet<string>();
            if (config == null)
                return result;

            foreach (var template in config.PlayerCardCatalog)
            {
                if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                    continue;

                if (seen.Add(template.DefinitionId))
                    result.Add(ExpeditionBattleConfigBuilder.CloneTemplate(template));
            }

            if (result.Count > 0)
                return result;

            foreach (var encounter in config.CombatEncounters)
            {
                foreach (var cc in encounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    foreach (var template in cc.DeckTemplates)
                    {
                        if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                            continue;

                        if (seen.Add(template.DefinitionId))
                            result.Add(ExpeditionBattleConfigBuilder.CloneTemplate(template));
                    }
                }
            }

            return result;
        }

        public static bool IsPlayerCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return false;

            return PlayerCharacterIds.Contains(characterId);
        }
    }
}
