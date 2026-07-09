using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

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

            AddDeck(knight, WitherStrike(), 3);
            AddDeck(knight, SoulDrain(), 3);
            AddDeck(knight, DarkShield(), 2);
            AddDeck(knight, PlagueTide(), 2);
            AddDeck(knight, CommandDead(), 1);
            AddDeck(knight, Snowball(), 2);
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

        static CardTemplate WitherStrike()
        {
            var card = BaseCard("m_dark_knight_wither", "凋零刺击", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 25,
                Reach = TargetReach.FrontAndMiddle,
                BonusIfTargetHasStatusId = StatusCatalog.Poison,
                BonusIfTargetHasStatusFlat = 15
            });
            return card;
        }

        static CardTemplate SoulDrain()
        {
            var card = BaseCard("m_dark_knight_soul_drain", "灵魂吸取", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 15,
                Reach = TargetReach.FrontAndMiddle,
                LifestealPercent = 100
            });
            return card;
        }

        static CardTemplate DarkShield()
        {
            var card = BaseCard("m_dark_knight_shield", "黑暗护盾", 1, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 20
            });
            return card;
        }

        static CardTemplate PlagueTide()
        {
            var card = BaseCard("m_dark_knight_plague", "瘟疫之潮", 2, CardType.Status, "aoe", "poison");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate CommandDead()
        {
            var card = BaseCard("m_dark_knight_command_dead", "号令亡者", 2, CardType.Status, "exhaust", "summon");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SummonOrGainBlock,
                Target = EffectTarget.Self,
                SummonCharacterId = "char_spider_lady",
                FallbackBlockValue = 15
            });
            return card;
        }

        static CardTemplate Snowball()
        {
            var card = BaseCard("m_dark_knight_snowball", "雪上加霜", 2, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 10,
                Reach = TargetReach.Any,
                BonusIfTargetHasStatusId = StatusCatalog.Poison,
                BonusIfTargetHasStatusFlat = 10
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
