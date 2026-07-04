using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public sealed class CardPackChoice
    {
        public string OwnerCharacterId { get; set; } = "";
        public CardTemplate Template { get; set; }
    }

    /// <summary>卡包开包：按 v0.9 总览表稀有度权重，从场上角色卡池中 roll 三选一。</summary>
    public static class CardPackRoller
    {
        public const int ChoiceCount = 3;

        static readonly (CardRarity rarity, int weight)[] CommonWeights =
        {
            (CardRarity.Common, 40),
            (CardRarity.Rare, 40),
            (CardRarity.SuperRare, 15),
            (CardRarity.Epic, 5)
        };

        static readonly (CardRarity rarity, int weight)[] AdvancedWeights =
        {
            (CardRarity.Common, 15),
            (CardRarity.Rare, 30),
            (CardRarity.SuperRare, 35),
            (CardRarity.Epic, 17),
            (CardRarity.Legendary, 3)
        };

        static readonly (CardRarity rarity, int weight)[] MasterWeights =
        {
            (CardRarity.Rare, 10),
            (CardRarity.SuperRare, 30),
            (CardRarity.Epic, 40),
            (CardRarity.Legendary, 20)
        };

        public static CardRarity RollRarity(string packId, BattleRng rng)
        {
            var weights = packId switch
            {
                CardPackIds.Advanced => AdvancedWeights,
                CardPackIds.Master => MasterWeights,
                _ => CommonWeights
            };

            var total = 0;
            foreach (var entry in weights)
                total += entry.weight;

            var roll = rng.NextInt(0, total);
            foreach (var entry in weights)
            {
                roll -= entry.weight;
                if (roll < 0)
                    return entry.rarity;
            }

            return weights[weights.Length - 1].rarity;
        }

        public static List<CardPackChoice> RollChoices(
            string packId,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            var choices = new List<CardPackChoice>();
            if (config == null || run?.Party == null || run.Party.Count == 0 || rng == null
                || !CardPackIds.IsValid(packId))
                return choices;

            var seen = new HashSet<string>();
            for (var attempt = 0; attempt < ChoiceCount * 8 && choices.Count < ChoiceCount; attempt++)
            {
                var rarity = RollRarity(packId, rng);
                if (!TryRollChoiceAtRarity(config, run, rarity, rng, seen, out var choice)
                    && !TryRollChoiceAnyRarity(config, run, rng, seen, out choice))
                    continue;

                var key = $"{choice.OwnerCharacterId}:{choice.Template.DefinitionId}";
                if (!seen.Add(key))
                    continue;

                choices.Add(choice);
            }

            while (choices.Count < ChoiceCount
                   && TryRollChoiceAnyRarity(config, run, rng, seen, out var fallback))
            {
                var key = $"{fallback.OwnerCharacterId}:{fallback.Template.DefinitionId}";
                if (!seen.Add(key))
                    continue;

                choices.Add(fallback);
            }

            return choices;
        }

        static bool TryRollChoiceAtRarity(
            ExpeditionConfig config,
            ExpeditionRunState run,
            CardRarity rarity,
            BattleRng rng,
            HashSet<string> seen,
            out CardPackChoice choice)
        {
            choice = null;
            for (var i = 0; i < 12; i++)
            {
                if (!ExpeditionCardPool.TryRollCardReward(config, run, rarity, rng, out var template, out var owner))
                    continue;

                var key = $"{owner.CharacterDefinitionId}:{template.DefinitionId}";
                if (seen.Contains(key))
                    continue;

                choice = new CardPackChoice
                {
                    OwnerCharacterId = owner.CharacterDefinitionId,
                    Template = template
                };
                return true;
            }

            return false;
        }

        static bool TryRollChoiceAnyRarity(
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng,
            HashSet<string> seen,
            out CardPackChoice choice)
        {
            choice = null;
            foreach (var rarity in new[]
                     {
                         CardRarity.Common, CardRarity.Rare, CardRarity.SuperRare, CardRarity.Epic,
                         CardRarity.Legendary
                     })
            {
                if (TryRollChoiceAtRarity(config, run, rarity, rng, seen, out choice))
                    return true;
            }

            return false;
        }
    }
}
