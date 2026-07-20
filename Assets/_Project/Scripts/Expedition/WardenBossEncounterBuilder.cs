using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>地牢第 40 层 Boss：典狱长 + 开战双囚笼。</summary>
    public static class WardenBossEncounterBuilder
    {
        public const string DisplayName = "典狱长";
        public const string CharacterId = CharacterTraitCatalog.WardenCharacterId;

        public static BattleConfig BuildTemplate(BattleConfig standardEncounter) =>
            BuildTemplate(standardEncounter, monsterTemplates: null);

        public static BattleConfig BuildTemplate(
            BattleConfig standardEncounter,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 3,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 3,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = 4,
                EnemyTurnEnergyBudget = 4,
                SkipFloorScaling = true,
                VictoryOnCharacterDeathId = CharacterId
            };

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team == TeamSide.Player)
                        config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            config.Combatants.Add(BuildWarden());
            config.SummonTemplates[CharacterTraitCatalog.PrisonCageCharacterId] = BuildPrisonCageTemplate();
            AddCageReplacementTemplate(config, monsterTemplates, "char_skeleton_elite", "骷髅精英", 45, 9, 5, 5);
            AddCageReplacementTemplate(config, monsterTemplates, "char_wraith_elite", "幽灵精英", 35, 10, 2, 8);
            AddCageReplacementTemplate(config, monsterTemplates, "char_bat", "巨翼蝙蝠", 55, 10, 3, 9);
            return config;
        }

        static void AddCageReplacementTemplate(
            BattleConfig config,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates,
            string characterId,
            string displayName,
            int hp,
            int atk,
            int def,
            int spd)
        {
            if (monsterTemplates != null
                && monsterTemplates.TryGetValue(characterId, out var template)
                && template != null)
            {
                config.SummonTemplates[characterId] =
                    ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template);
                return;
            }

            config.SummonTemplates[characterId] = BuildEliteStub(characterId, displayName, hp, atk, def, spd);
        }

        static CombatantConfig BuildWarden()
        {
            var warden = new CombatantConfig
            {
                Id = "Character_Warden",
                DisplayName = DisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = CharacterId,
                MaxHp = 250,
                BaseAttack = 22,
                BaseDefense = 8,
                Speed = 5
            };
            warden.Traits.Add(CharacterTraitCatalog.WardenCageMaster);

            AddDeck(warden, V09BossCardCatalog.PunishmentCombo(), 3);
            AddDeck(warden, V09BossCardCatalog.BrandMark(), 3);
            AddDeck(warden, V09BossCardCatalog.IronGate(), 2);
            AddDeck(warden, V09BossCardCatalog.OpenCage(), 1);
            AddDeck(warden, V09BossCardCatalog.OppressionAura(), 1);
            AddDeck(warden, V09BossCardCatalog.IronSanction(), 2);
            AddDeck(warden, V09BossCardCatalog.LockDown(), 1);
            AddDeck(warden, V09BossCardCatalog.Judgment(), 1);
            return warden;
        }

        static CombatantConfig BuildPrisonCageTemplate()
        {
            var cage = new CombatantConfig
            {
                DisplayName = "囚笼",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = CharacterTraitCatalog.PrisonCageCharacterId,
                MaxHp = 150,
                BaseAttack = 0,
                BaseDefense = 5,
                Speed = 5
            };
            cage.Traits.Add(CharacterTraitCatalog.PrisonCage);
            return cage;
        }

        static CombatantConfig BuildEliteStub(
            string id, string name, int hp, int atk, int def, int spd) =>
            new()
            {
                DisplayName = name,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = id,
                MaxHp = hp,
                BaseAttack = atk,
                BaseDefense = def,
                Speed = spd,
                UseSkillPool = true
            };

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }
    }
}
