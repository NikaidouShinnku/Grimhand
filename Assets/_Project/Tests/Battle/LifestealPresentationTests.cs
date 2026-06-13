using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class LifestealPresentationTests
    {
        [Test]
        public void LifestealHeal_MarksHealEventAsLifesteal()
        {
            var state = BuildState();
            var demon = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Front, hp: 20, maxHp: 30, atk: 10);
            var enemy = AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, hp: 30, def: 0);
            var card = CardWith(ActionAtk(4, 50, lifestealPercent: 100));
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, demon, card, events, new BattleRng(1));

            BattleEvent heal = null;
            foreach (var e in events)
            {
                if (e.Kind == BattleEventKind.HealApplied && e.CombatantId == demon.Id)
                    heal = e;
            }

            Assert.NotNull(heal);
            Assert.IsTrue(heal.IsLifesteal);
            Assert.Greater(heal.Amount, 0);
            Assert.Greater(demon.Hp, 20);
        }

        static BattleState BuildState()
        {
            var state = new BattleState();
            state.Config = new BattleConfig();
            return state;
        }

        static CombatantState AddUnit(
            BattleState state,
            string id,
            TeamSide team,
            FormationSlot slot,
            int hp = 20,
            int maxHp = 20,
            int atk = 5,
            int def = 0,
            int speed = 5)
        {
            var unit = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                MaxHp = maxHp,
                Hp = hp,
                BaseAttack = atk,
                BaseDefense = def,
                Speed = speed
            };
            CombatantRules.RefreshDerivedStats(unit);
            state.Combatants.Add(unit);
            return unit;
        }

        static CardInstanceState CardWith(params EffectActionSpec[] actions)
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                CardType = CardType.Attack
            };
            card.Actions.AddRange(actions);
            return card;
        }

        static EffectActionSpec ActionAtk(
            int value,
            int atkPercent,
            int lifestealPercent = 0)
        {
            return new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = value,
                ScaleWithAttack = true,
                AttackScalePercent = atkPercent,
                LifestealPercent = lifestealPercent
            };
        }
    }
}
