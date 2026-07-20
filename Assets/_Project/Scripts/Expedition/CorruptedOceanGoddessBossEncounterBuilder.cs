using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>海渊第 60 层 Boss：腐化海洋女神。</summary>
    public static class CorruptedOceanGoddessBossEncounterBuilder
    {
        public const string DisplayName = "腐化海洋女神";
        public const string CharacterId = CharacterTraitCatalog.OceanGoddessCharacterId;

        public static BattleConfig BuildTemplate(BattleConfig standardEncounter)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 3,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 3,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = 4,
                EnemyTurnEnergyBudget = 4,
                SkipFloorScaling = true
            };

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team == TeamSide.Player)
                        config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            config.Combatants.Add(BuildGoddess());
            return config;
        }

        static CombatantConfig BuildGoddess()
        {
            var goddess = new CombatantConfig
            {
                Id = "Character_Corrupted_Ocean_Goddess",
                DisplayName = DisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = CharacterId,
                MaxHp = 400,
                BaseAttack = 20,
                BaseDefense = 10,
                Speed = 6
            };
            goddess.Traits.Add(CharacterTraitCatalog.OceanGoddessTide);

            AddDeck(goddess, V09BossCardCatalog.CorruptedNet(), 3);
            AddDeck(goddess, V09BossCardCatalog.OceanShield(), 2);
            AddDeck(goddess, V09BossCardCatalog.TidePower(), 2);
            AddDeck(goddess, V09BossCardCatalog.VortexPull(), 2);
            AddDeck(goddess, V09BossCardCatalog.AbyssDevour(), 1);
            AddDeck(goddess, V09BossCardCatalog.GoddessWrath(), 1);
            AddDeck(goddess, V09BossCardCatalog.TideControl(), 3);
            AddDeck(goddess, V09BossCardCatalog.DemonTide(), 1);
            return goddess;
        }

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }
    }
}
