using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>地牢第 40 层 Boss（轮换）：黑暗骑士。</summary>
    public static class DarkKnightBossEncounterBuilder
    {
        public const string DisplayName = "黑暗骑士";
        public const string CharacterId = CharacterTraitCatalog.DarkKnightCharacterId;

        public static BattleConfig BuildTemplate(BattleConfig standardEncounter)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 3,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 3,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = 3,
                EnemyTurnEnergyBudget = 3,
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

            config.Combatants.Add(BuildDarkKnight());
            config.SummonTemplates["char_spider_lady"] = BuildSpiderLadyStub();
            return config;
        }

        static CombatantConfig BuildDarkKnight()
        {
            var knight = new CombatantConfig
            {
                Id = "Character_Dark_Knight",
                DisplayName = DisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = CharacterId,
                MaxHp = 350,
                BaseAttack = 25,
                BaseDefense = 10,
                Speed = 8
            };
            knight.Traits.Add(CharacterTraitCatalog.DarkKnightPoisonAura);

            AddDeck(knight, V09BossCardCatalog.WitherStrike(), 3);
            AddDeck(knight, V09BossCardCatalog.SoulDrain(), 3);
            AddDeck(knight, V09BossCardCatalog.DarkShield(), 2);
            AddDeck(knight, V09BossCardCatalog.PlagueTide(), 2);
            AddDeck(knight, V09BossCardCatalog.CommandDead(), 1);
            AddDeck(knight, V09BossCardCatalog.Snowball(), 2);
            return knight;
        }

        static CombatantConfig BuildSpiderLadyStub() =>
            new()
            {
                DisplayName = "蜘蛛贵妇",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_spider_lady",
                MaxHp = 60,
                BaseAttack = 9,
                BaseDefense = 4,
                Speed = 7,
                UseSkillPool = true
            };

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }
    }
}
