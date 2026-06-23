using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    public static class FelFlameAltarEncounterBuilder
    {
        public const string BattleKey = "event_fel_flame_altar_elite";

        public static BattleConfig BuildEliteBattle(
            BattleConfig standardEncounter,
            int floor,
            BattleRng rng,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates)
        {
            var encounterId = MonsterEncounterCatalog.Roll(floor, isElite: true, rng);
            var encounter = MonsterEncounterCatalog.GetById(encounterId)
                            ?? MonsterEncounterCatalog.GetById(MonsterEncounterCatalog.Roll(floor, isElite: true, rng));
            return MonsterEncounterBuilder.Build(standardEncounter, encounter, monsterTemplates);
        }
    }
}
