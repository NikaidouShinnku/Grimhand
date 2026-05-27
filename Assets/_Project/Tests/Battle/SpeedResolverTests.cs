using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class SpeedResolverTests
    {
        [Test]
        public void GddExample_OrderIsCorrect()
        {
            var state = BuildGddExampleState();
            var playerPlan = new BattlePlan();
            playerPlan.PlayQueue.AddRange(new[] { 101, 102, 103, 104 });
            var enemyPlan = new BattlePlan();
            enemyPlan.PlayQueue.AddRange(new[] { 201, 202 });

            var steps = SpeedResolver.BuildResolutionOrder(state, playerPlan, enemyPlan, new BattleRng(1));

            var order = string.Join("->", steps.ConvertAll(s => CombatantLabel(s.CombatantId)));
            Assert.AreEqual("A->X->Y->B->A->A", order);
        }

        [Test]
        public void SameSpeed_UsesRandomOrderWithSeed()
        {
            var state = new BattleState();
            state.Combatants.Add(MakeCombatant("a", 5));
            state.Combatants.Add(MakeCombatant("b", 5));

            var plan = new BattlePlan();
            RegisterCard(state, 1, "char_a");
            RegisterCard(state, 2, "char_b");
            plan.PlayQueue.Add(1);
            plan.PlayQueue.Add(2);

            var stepsA = SpeedResolver.BuildResolutionOrder(state, plan, new BattlePlan(), new BattleRng(100));
            var stepsB = SpeedResolver.BuildResolutionOrder(state, plan, new BattlePlan(), new BattleRng(100));

            Assert.AreEqual(stepsA.Count, 1);
            Assert.AreEqual(stepsA[0].CombatantId, stepsB[0].CombatantId);
        }

        static BattleState BuildGddExampleState()
        {
            var state = new BattleState();
            state.Combatants.Add(MakeCombatant("A", 10, "char_a"));
            state.Combatants.Add(MakeCombatant("B", 5, "char_b"));
            state.Combatants.Add(MakeCombatant("X", 9, "char_x", TeamSide.Enemy));
            state.Combatants.Add(MakeCombatant("Y", 7, "char_y", TeamSide.Enemy));

            RegisterCard(state, 101, "char_a");
            RegisterCard(state, 102, "char_a");
            RegisterCard(state, 103, "char_a");
            RegisterCard(state, 104, "char_b");
            RegisterCard(state, 201, "char_x");
            RegisterCard(state, 202, "char_y");

            return state;
        }

        static CombatantState MakeCombatant(
            string id,
            int speed,
            string charId = "char",
            TeamSide team = TeamSide.Player)
        {
            return new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = charId,
                Speed = speed,
                MaxHp = 20,
                Hp = 20
            };
        }

        static void RegisterCard(BattleState state, int instanceId, string ownerCharId)
        {
            var card = new CardInstanceState
            {
                InstanceId = instanceId,
                OwnerCharacterId = ownerCharId,
                Cost = 1,
                IsUsable = true,
                DisplayName = instanceId.ToString()
            };
            state.CardsById[instanceId] = card;
        }

        static string CombatantLabel(string id) => id;
    }
}
