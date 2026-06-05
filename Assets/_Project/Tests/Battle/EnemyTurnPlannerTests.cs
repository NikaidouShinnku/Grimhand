using Grimhand.Battle.AI;
using Grimhand.Battle.Model;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class EnemyTurnPlannerTests
    {
        [Test]
        public void PrepareEnemyTurn_RespectsEnergyBudget()
        {
            var state = new BattleState
            {
                Config = new BattleConfig { TurnStartEnergyRegen = 4 }
            };

            var skeleton = new CombatantState
            {
                Id = "e1",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_skeleton",
                Hp = 25,
                MaxHp = 25
            };
            state.Combatants.Add(skeleton);
            state.CharacterOwnerByCombatantId[skeleton.Id] = skeleton.CharacterDefinitionId;

            AddEnemyCard(state, skeleton, 101, "举盾", 1);
            AddEnemyCard(state, skeleton, 102, "骨剑斩", 1);
            AddEnemyCard(state, skeleton, 103, "投骨", 2);

            var plan = EnemyTurnPlanner.PrepareEnemyTurn(state, new BattleRng(1));

            Assert.AreEqual(3, plan.Plan.PlayQueue.Count);
            Assert.AreEqual(4, plan.Plan.EnergySpent);
        }

        [Test]
        public void PrepareEnemyTurn_SkipsCardsThatExceedBudget()
        {
            var state = new BattleState
            {
                Config = new BattleConfig { TurnStartEnergyRegen = 3 }
            };

            var skeleton = new CombatantState
            {
                Id = "e1",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_skeleton",
                Hp = 25,
                MaxHp = 25
            };
            state.Combatants.Add(skeleton);
            state.CharacterOwnerByCombatantId[skeleton.Id] = skeleton.CharacterDefinitionId;

            AddEnemyCard(state, skeleton, 201, "投骨", 2);
            AddEnemyCard(state, skeleton, 202, "骨刺", 2);

            var plan = EnemyTurnPlanner.PrepareEnemyTurn(state, new BattleRng(1));

            Assert.AreEqual(1, plan.Plan.PlayQueue.Count);
            Assert.AreEqual(2, plan.Plan.EnergySpent);
        }

        static void AddEnemyCard(BattleState state, CombatantState owner, int id, string name, int cost)
        {
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = name,
                DisplayName = name,
                OwnerCharacterId = owner.CharacterDefinitionId,
                Cost = cost,
                CardType = CardType.Attack,
                IsUsable = true
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 5,
                ScaleWithAttack = true
            });

            state.CardsById[id] = card;
            state.EnemyHand.Add(card);
        }
    }
}
