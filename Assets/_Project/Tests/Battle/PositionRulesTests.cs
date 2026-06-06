using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class PositionRulesTests
    {
        [Test]
        public void EffectiveSlot_WhenFrontDies_MiddleBecomesFront_BackBecomesMiddle()
        {
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle, hp: 20),
                Unit("back", FormationSlot.Back, hp: 20));

            Assert.AreEqual(FormationSlot.Front, PositionRules.GetEffectiveSlot(state, state.GetCombatant("middle")));
            Assert.AreEqual(FormationSlot.Middle, PositionRules.GetEffectiveSlot(state, state.GetCombatant("back")));
        }

        [Test]
        public void EffectiveSlot_WhenMiddleDies_FrontStaysFront_BackBecomesMiddle()
        {
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 20),
                Unit("middle", FormationSlot.Middle, hp: 0),
                Unit("back", FormationSlot.Back, hp: 20));

            Assert.AreEqual(FormationSlot.Front, PositionRules.GetEffectiveSlot(state, state.GetCombatant("front")));
            Assert.AreEqual(FormationSlot.Middle, PositionRules.GetEffectiveSlot(state, state.GetCombatant("back")));
        }

        [Test]
        public void PickCombatantInSlot_UsesEffectiveRows()
        {
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle, hp: 20),
                Unit("back", FormationSlot.Back, hp: 20));

            Assert.AreEqual("middle", PositionRules.PickCombatantInSlot(state, TeamSide.Enemy, FormationSlot.Front).Id);
            Assert.AreEqual("back", PositionRules.PickCombatantInSlot(state, TeamSide.Enemy, FormationSlot.Middle).Id);
            Assert.IsNull(PositionRules.PickCombatantInSlot(state, TeamSide.Enemy, FormationSlot.Back));
        }

        [Test]
        public void GetCombatantBehind_UsesEffectiveOrder()
        {
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 20),
                Unit("middle", FormationSlot.Middle, hp: 0),
                Unit("back", FormationSlot.Back, hp: 20));

            var front = state.GetCombatant("front");
            Assert.AreEqual("back", PositionRules.GetCombatantBehind(state, front).Id);
        }

        [Test]
        public void AssignUniqueSlotsPerTeam_SplitsDuplicateBackRowEnemies()
        {
            var config = new BattleConfig();
            config.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_wraith"
            });
            config.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_wraith_elite"
            });
            config.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_slime"
            });

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);

            Assert.AreEqual(FormationSlot.Front, config.Combatants[0].Slot);
            Assert.AreEqual(FormationSlot.Middle, config.Combatants[1].Slot);
            Assert.AreEqual(FormationSlot.Back, config.Combatants[2].Slot);
        }

        static BattleState Team(params CombatantState[] units)
        {
            var state = new BattleState();
            foreach (var unit in units)
            {
                unit.Team = TeamSide.Enemy;
                state.Combatants.Add(unit);
            }

            return state;
        }

        static CombatantState Unit(string id, FormationSlot slot, int hp, string charId = null) =>
            new()
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = charId ?? id,
                Slot = slot,
                Hp = hp,
                MaxHp = 20,
                BaseAttack = 5,
                BaseDefense = 3,
                Speed = 4
            };
    }
}
