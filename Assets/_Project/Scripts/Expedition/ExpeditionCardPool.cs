using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Core;
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

        public static bool TryRollCardReward(
            ExpeditionConfig config,
            ExpeditionRunState run,
            CardRarity rarity,
            BattleRng rng,
            out CardTemplate picked,
            out PartyMemberSnapshot owner)
        {
            picked = null;
            owner = null;
            if (config == null || run?.Party == null || run.Party.Count == 0 || rng == null)
                return false;

            var pool = new List<CardTemplate>();
            foreach (var template in CollectPlayerCardTemplates(config))
            {
                if (CardRarityTable.GetOrDefault(template.DefinitionId) == rarity)
                    pool.Add(template);
            }

            if (pool.Count == 0)
                return false;

            owner = run.Party[rng.NextIndex(run.Party.Count)];
            picked = pool[rng.NextIndex(pool.Count)];
            return true;
        }

        public static bool IsPlayerCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return false;

            return PlayerCharacterIds.Contains(characterId);
        }
    }
}
