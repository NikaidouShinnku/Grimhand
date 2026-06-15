using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    public static class GhostQueenBossEncounterBuilder
    {
        public const string CharacterId = "char_ghost_queen";

        public static BattleConfig BuildTemplate(BattleConfig standardEncounter)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter.EnergyCap,
                TurnStartEnergyRegen = standardEncounter.TurnStartEnergyRegen,
                HandLimit = standardEncounter.HandLimit,
                CardsDrawnPerTurn = standardEncounter.CardsDrawnPerTurn,
                EnemyCardsDrawnPerTurn = 4,
                EnemyTurnEnergyBudget = 4,
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

            config.Combatants.Add(BuildGhostQueen());
            return config;
        }

        public static CardTemplate BuildWrathBonusCardTemplate() => QueenWrath();

        static CombatantConfig BuildGhostQueen()
        {
            var queen = new CombatantConfig
            {
                Id = "Character_Ghost_Queen",
                DisplayName = "幽灵女王",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = CharacterId,
                MaxHp = 320,
                BaseAttack = 25,
                BaseDefense = 8,
                Speed = 7
            };

            queen.Traits.Add(CharacterTraitCatalog.GhostQueenEnrage);

            AddDeck(queen, QueenClaw(), 4);
            AddDeck(queen, QueenDeterrence(), 1);
            AddDeck(queen, QueenSoulDrain(), 2);
            AddDeck(queen, QueenCurse(), 2);
            AddDeck(queen, QueenCommand(), 1);
            AddDeck(queen, QueenSpiritGuard(), 2);
            AddDeck(queen, QueenBurst(), 1);
            return queen;
        }

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }

        static CardTemplate QueenClaw() =>
            Dmg("m_queen_claw", "幽灵爪击", 1, 20, TargetReach.Any, "snipe");

        static CardTemplate QueenDeterrence()
        {
            var card = BaseCard("m_queen_deterrence", "女王的威慑", 1, CardType.Status, "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockRandomPlayerPlaysThisTurn,
                Target = EffectTarget.DefaultEnemy
            });
            return card;
        }

        static CardTemplate QueenSoulDrain()
        {
            var card = BaseCard("m_queen_soul_drain", "摄魂", 1, CardType.Status, "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.AllEnemies,
                Value = 2
            });
            return card;
        }

        static CardTemplate QueenCurse()
        {
            var card = BaseCard("m_queen_curse", "女王的诅咒", 2, CardType.Status, "poison", "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 3,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate QueenCommand()
        {
            var card = BaseCard("m_queen_command", "女王的命令", 2, CardType.Defense, "parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ArmRespondDamageRedirect,
                Target = EffectTarget.Self,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            return card;
        }

        static CardTemplate QueenSpiritGuard()
        {
            var card = BaseCard("m_queen_spirit_guard", "灵气护体", 1, CardType.Defense, "guard");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                ScaleWithDefense = true,
                DefenseScalePercent = 200
            });
            return card;
        }

        static CardTemplate QueenBurst()
        {
            var card = BaseCard("m_queen_burst", "幽灵爆发", 4, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 20,
                ScaleWithAttack = true,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate QueenWrath()
        {
            var card = BaseCard("m_queen_wrath", "幽灵女王之怒", 0, CardType.Status, "bonus_hand");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.GhostQueenWrath,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate Dmg(
            string id,
            string name,
            int cost,
            int flat,
            TargetReach reach,
            params string[] keywords)
        {
            var card = BaseCard(id, name, cost, CardType.Attack, keywords);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = flat,
                ScaleWithAttack = true,
                Reach = reach
            });
            return card;
        }

        static CardTemplate BaseCard(
            string id,
            string name,
            int cost,
            CardType type,
            params string[] keywords)
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
