using Grimhand.Battle.Model;
using Grimhand.Core;
using System.Collections.Generic;

namespace Grimhand.Expedition
{
    public enum Floor40BossKind
    {
        Warden,
        DarkKnight
    }

    public static class Floor40BossEncounterBuilder
    {
        public static Floor40BossKind RollBossKind(BattleRng rng) =>
            rng != null && rng.NextIndex(2) == 1
                ? Floor40BossKind.DarkKnight
                : Floor40BossKind.Warden;

        public static string GetDisplayName(Floor40BossKind kind) =>
            kind switch
            {
                Floor40BossKind.DarkKnight => DarkKnightBossEncounterBuilder.DisplayName,
                _ => WardenBossEncounterBuilder.DisplayName
            };

        public static BattleConfig BuildTemplate(BattleConfig standardEncounter, Floor40BossKind kind) =>
            kind switch
            {
                Floor40BossKind.DarkKnight => DarkKnightBossEncounterBuilder.BuildTemplate(standardEncounter),
                _ => WardenBossEncounterBuilder.BuildTemplate(standardEncounter, monsterTemplates: null)
            };

        public static BattleConfig BuildTemplate(
            BattleConfig standardEncounter,
            Floor40BossKind kind,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates) =>
            kind switch
            {
                Floor40BossKind.DarkKnight => DarkKnightBossEncounterBuilder.BuildTemplate(standardEncounter),
                _ => WardenBossEncounterBuilder.BuildTemplate(standardEncounter, monsterTemplates)
            };

        public static BattleConfig BuildRandomTemplate(BattleConfig standardEncounter, BattleRng rng) =>
            BuildTemplate(standardEncounter, RollBossKind(rng), monsterTemplates: null);

        public static BattleConfig BuildRandomTemplate(
            BattleConfig standardEncounter,
            BattleRng rng,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates) =>
            BuildTemplate(standardEncounter, RollBossKind(rng), monsterTemplates);
    }
}
