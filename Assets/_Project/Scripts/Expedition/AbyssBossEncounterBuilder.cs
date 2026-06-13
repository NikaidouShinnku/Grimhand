using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>海渊第 60 层 Boss：鬼灵海盗船长率两只深渊怪物。</summary>
    public static class AbyssBossEncounterBuilder
    {
        public const string DisplayName = "鬼灵海盗船长";
        public const string CharacterId = "char_phantom_captain";

        public static BattleConfig BuildTemplate(
            BattleConfig standardEncounter,
            System.Collections.Generic.IReadOnlyDictionary<string, CombatantConfig> monsterTemplates = null)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 3,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 3,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = MonsterEncounterBuilder.EnemyDrawPerTurn,
                EnemyTurnEnergyBudget = MonsterEncounterBuilder.EnemyEnergyBudget,
                SkipFloorScaling = true
            };

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            CombatantConfig captainTemplate = null;
            CombatantConfig abyssTemplate = null;
            monsterTemplates?.TryGetValue(CharacterId, out captainTemplate);
            monsterTemplates?.TryGetValue(MinionTraitCatalog.AbyssCreatureCharacterId, out abyssTemplate);

            var captain = captainTemplate != null
                ? ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(captainTemplate)
                : BuildFallbackCaptain();
            captain.Id = "Character_Phantom_Captain_Boss";
            captain.DisplayName = DisplayName;
            captain.MaxHp = 220;
            captain.BaseAttack = 20;
            config.Combatants.Add(captain);

            for (var i = 0; i < 2; i++)
            {
                var abyss = abyssTemplate != null
                    ? ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(abyssTemplate)
                    : BuildFallbackAbyss();
                abyss.Id = $"Character_Abyss_Creature_Boss_{i}";
                config.Combatants.Add(abyss);
            }

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);
            return config;
        }

        static CombatantConfig BuildFallbackCaptain() =>
            new()
            {
                DisplayName = DisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = CharacterId,
                MaxHp = 220,
                BaseAttack = 20,
                BaseDefense = 7,
                Speed = 7
            };

        static CombatantConfig BuildFallbackAbyss() =>
            new()
            {
                DisplayName = "深渊怪物",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = MinionTraitCatalog.AbyssCreatureCharacterId,
                MaxHp = 115,
                BaseAttack = 12,
                BaseDefense = 8,
                Speed = 4
            };
    }
}
