using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

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

            AddDeck(warden, PunishmentCombo(), 3);
            AddDeck(warden, BrandMark(), 3);
            AddDeck(warden, IronGate(), 2);
            AddDeck(warden, OpenCage(), 1);
            AddDeck(warden, OppressionAura(), 1);
            AddDeck(warden, IronSanction(), 2);
            AddDeck(warden, LockDown(), 1);
            AddDeck(warden, Judgment(), 1);
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

        static CardTemplate PunishmentCombo()
        {
            var card = BaseCard("m_warden_punishment_combo", "刑法连击", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 30,
                Reach = TargetReach.FrontAndMiddle,
                SplashBehindTarget = true,
                SplashPowerPercent = 50
            });
            return card;
        }

        static CardTemplate BrandMark()
        {
            var card = BaseCard("m_warden_brand", "刻上烙印", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemy,
                StatusId = StatusCatalog.BrandMark,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate IronGate()
        {
            var card = BaseCard("m_warden_iron_gate", "铁壁牢门", 1, CardType.Defense, "parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 70,
                Condition = ReactionConditionType.LastActionAttackOnSelf,
                RespondSideEffectAllyDamage = 30,
                RespondSideEffectAllyCharacterId = CharacterTraitCatalog.PrisonCageCharacterId
            });
            return card;
        }

        static CardTemplate OpenCage()
        {
            var card = BaseCard("m_warden_open_cage", "打开囚笼", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamageRandomCharacterAlly,
                Target = EffectTarget.RandomAllyByCharacterId,
                SummonCharacterId = CharacterTraitCatalog.PrisonCageCharacterId,
                Value = 150
            });
            return card;
        }

        static CardTemplate OppressionAura()
        {
            var card = BaseCard("m_warden_oppression", "压迫气场", 2, CardType.Status, "aoe", "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Slow,
                Stacks = 2,
                Duration = 2,
                Reach = TargetReach.Any
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 20,
                Duration = 2,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate IronSanction()
        {
            var card = BaseCard("m_warden_iron_sanction", "铁腕制裁", 3, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 30,
                Reach = TargetReach.FrontAndMiddle
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Vulnerable,
                Stacks = 100,
                Duration = 2
            });
            return card;
        }

        static CardTemplate LockDown()
        {
            var card = BaseCard("m_warden_lock", "上锁", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 100,
                Duration = 2,
                Reach = TargetReach.FrontAndMiddle
            });
            return card;
        }

        static CardTemplate Judgment()
        {
            var card = BaseCard("m_warden_judgment", "审判裁决", 3, CardType.Status, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.BrandMark,
                Stacks = 1,
                Duration = -1,
                Reach = TargetReach.Any
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
