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

                if (!PlayerCardCatalogRules.IsAllowedPlayerCardId(template.DefinitionId))
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

        public static bool IsCardOwnedByCharacter(CardTemplate template, string characterId)
        {
            if (template == null || string.IsNullOrEmpty(characterId))
                return false;

            return template.OwnerCharacterId == characterId;
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

            var eligible = new List<(PartyMemberSnapshot member, List<CardTemplate> cards)>();
            foreach (var member in run.Party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                var owned = FilterPoolForCharacter(pool, member.CharacterDefinitionId);
                if (owned.Count > 0)
                    eligible.Add((member, owned));
            }

            if (eligible.Count == 0)
                return false;

            var choice = eligible[rng.NextIndex(eligible.Count)];
            owner = choice.member;
            var source = choice.cards[rng.NextIndex(choice.cards.Count)];
            picked = ExpeditionBattleConfigBuilder.CloneTemplate(source);
            picked.OwnerCharacterId = owner.CharacterDefinitionId;
            return true;
        }

        public static bool TryRollCardRewardForMember(
            ExpeditionConfig config,
            PartyMemberSnapshot member,
            CardRarity rarity,
            BattleRng rng,
            out CardTemplate picked)
        {
            picked = null;
            if (config == null || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId) || rng == null)
                return false;

            var pool = new List<CardTemplate>();
            foreach (var template in CollectPlayerCardTemplates(config))
            {
                if (CardRarityTable.GetOrDefault(template.DefinitionId) == rarity)
                    pool.Add(template);
            }

            var owned = FilterPoolForCharacter(pool, member.CharacterDefinitionId);
            if (owned.Count == 0)
                return false;

            var source = owned[rng.NextIndex(owned.Count)];
            picked = ExpeditionBattleConfigBuilder.CloneTemplate(source);
            picked.OwnerCharacterId = member.CharacterDefinitionId;
            return true;
        }

        static List<CardTemplate> FilterPoolForCharacter(IReadOnlyList<CardTemplate> pool, string characterId)
        {
            var owned = new List<CardTemplate>();
            foreach (var template in pool)
            {
                if (IsCardOwnedByCharacter(template, characterId))
                    owned.Add(template);
            }

            return owned;
        }

        public static bool IsPlayerCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return false;

            return PlayerCharacterIds.Contains(characterId);
        }
    }
}
