using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    /// <summary>「下回合开始」扳机：延迟伤害 / 蓄能增伤。</summary>
    public class LichQueenNextTurnTriggerTests
    {
        [Test]
        public void SoulStorm_AppliesDelayedDamage_NotImmediate()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 50, 8);
            var a = Unit("a", TeamSide.Enemy, FormationSlot.Front, 40, 3);
            var b = Unit("b", TeamSide.Enemy, FormationSlot.Middle, 40, 3);
            state.Combatants.Add(lich);
            state.Combatants.Add(a);
            state.Combatants.Add(b);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_soul_storm",
                CardType = CardType.Attack,
                Keywords = { "aoe" }
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyDelayedDamage,
                Target = EffectTarget.AllEnemies,
                Value = 10
            });

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.AreEqual(40, a.Hp);
            Assert.AreEqual(40, b.Hp);
            Assert.AreEqual(10, StatusRules.FindStatus(a, StatusCatalog.DelayedDamage).Stacks);
            Assert.AreEqual(10, StatusRules.FindStatus(b, StatusCatalog.DelayedDamage).Stacks);

            V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(30, a.Hp);
            Assert.AreEqual(30, b.Hp);
        }

        [Test]
        public void PsionicCannon_AppliesDelayedDamage_NotImmediate()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 50, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 40, 3);
            state.Combatants.Add(lich);
            state.Combatants.Add(enemy);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_psionic_cannon",
                CardType = CardType.Attack
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyDelayedDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 13,
                Reach = TargetReach.Any
            });
            state.CardsById[1] = card;
            state.ResolutionTargets[1] = enemy.Id;

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.AreEqual(40, enemy.Hp);
            Assert.AreEqual(13, StatusRules.FindStatus(enemy, StatusCatalog.DelayedDamage).Stacks);

            V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(27, enemy.Hp);
        }

        [Test]
        public void Charge_BuffAppliesNextTurn_NotThisTurn()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 50, 8);
            state.Combatants.Add(lich);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_charge",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatusNextTurn,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 20,
                Duration = 3
            });

            var events = new List<BattleEvent>();
            EffectActionExecutor.ExecuteAll(state, lich, card, events, new BattleRng(1));

            Assert.IsFalse(StatusRules.HasStatus(lich, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(1, state.PendingStatusesNextTurn.Count);
            Assert.IsFalse(
                events.Exists(e => e.Kind == BattleEventKind.StatusApplied
                                   && e.TargetId == StatusCatalog.AttackUpPercent),
                "蓄能不得发 StatusApplied，否则脚标会假显示增伤");

            StatusRules.ProcessTurnStartDurations(state, new List<BattleEvent>());
            V09NewMechanicsRules.ProcessPendingStatusesNextTurn(state, new List<BattleEvent>());

            Assert.IsTrue(StatusRules.HasStatus(lich, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(20, StatusRules.FindStatus(lich, StatusCatalog.AttackUpPercent).Stacks);
            Assert.AreEqual(3, StatusRules.FindStatus(lich, StatusCatalog.AttackUpPercent).RemainingTurns);
            Assert.AreEqual(0, state.PendingStatusesNextTurn.Count);
        }

        [Test]
        public void PsionicFocus_BuffImmediate_DamageDelayed()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 50, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 40, 3);
            state.Combatants.Add(lich);
            state.Combatants.Add(enemy);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_psionic_focus",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 20,
                Duration = 2
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyDelayedDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 12,
                Reach = TargetReach.FrontAndMiddle
            });
            state.CardsById[1] = card;
            state.ResolutionTargets[1] = enemy.Id;

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.IsTrue(StatusRules.HasStatus(lich, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(2, StatusRules.FindStatus(lich, StatusCatalog.AttackUpPercent).RemainingTurns);
            Assert.AreEqual(40, enemy.Hp);
            Assert.AreEqual(12, StatusRules.FindStatus(enemy, StatusCatalog.DelayedDamage).Stacks);

            V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(28, enemy.Hp);
        }

        static CombatantState Unit(string id, TeamSide team, FormationSlot slot, int hp, int atk) =>
            new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = "char_lich_queen",
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                BaseAttack = atk,
                Attack = atk,
                Defense = 0,
                Speed = 5
            };
    }
}
