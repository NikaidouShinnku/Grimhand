using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

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

            AddDeck(goddess, CorruptedNet(), 3);
            AddDeck(goddess, OceanShield(), 2);
            AddDeck(goddess, TidePower(), 2);
            AddDeck(goddess, VortexPull(), 2);
            AddDeck(goddess, AbyssDevour(), 1);
            AddDeck(goddess, GoddessWrath(), 1);
            AddDeck(goddess, TideControl(), 3);
            AddDeck(goddess, DemonTide(), 1);
            return goddess;
        }

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }

        static CardTemplate CorruptedNet()
        {
            var card = BaseCard("m_ocean_corrupted_net", "腐化电网", 1, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 20,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate OceanShield()
        {
            var card = BaseCard("m_ocean_shield", "海洋神盾", 1, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 30
            });
            return card;
        }

        static CardTemplate TidePower()
        {
            var card = BaseCard("m_ocean_tide_power", "潮汐神力", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyAttackUpPerSelfStatusStack,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 20,
                Duration = 2,
                RepeatPerStatusId = StatusCatalog.RisingTide
            });
            return card;
        }

        static CardTemplate VortexPull()
        {
            var card = BaseCard("m_ocean_vortex", "漩涡吸引", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SwapRandomEnemies,
                Target = EffectTarget.AllEnemies,
                Value = 1
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemies,
                Value = 2,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1
            });
            return card;
        }

        static CardTemplate AbyssDevour()
        {
            var card = BaseCard("m_ocean_abyss_devour", "深渊吞噬", 2, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.StripBlockThenDealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 12,
                Stacks = 5,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate GoddessWrath()
        {
            var card = BaseCard("m_ocean_goddess_wrath", "女神之怒", 3, CardType.Status, "orange");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockRisingTideStacks,
                Target = EffectTarget.Self,
                Duration = 2
            });
            return card;
        }

        static CardTemplate TideControl()
        {
            var card = BaseCard("m_ocean_tide_control", "潮汐掌握", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.AdjustSelfStatusRandom,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.RisingTide
            });
            return card;
        }

        static CardTemplate DemonTide()
        {
            var card = BaseCard("m_ocean_demon_tide", "魔化潮汐", 2, CardType.Status, "exhaust");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.TideEmpower,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate BaseCard(string id, string name, int cost, CardType type, params string[] keywords)
        {
            var card = new CardTemplate
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = CharacterId,
                Cost = cost,
                CardType = type
            };
            foreach (var keyword in keywords)
                card.Keywords.Add(keyword);
            return card;
        }
    }
}
