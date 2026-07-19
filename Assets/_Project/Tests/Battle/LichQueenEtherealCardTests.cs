using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class LichQueenEtherealCardTests
    {
        [Test]
        public void SpiritWalk_DamagesSelfAndAppliesEthereal()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_spirit_walk",
                CardType = CardType.Defense
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = 8
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.Ethereal,
                Stacks = 1,
                Duration = 1
            });

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.AreEqual(32, lich.Hp);
            Assert.IsTrue(StatusRules.HasStatus(lich, StatusCatalog.Ethereal));
        }

        [Test]
        public void VoidGaze_DrawsExtraNextTurnWhenEthereal()
        {
            var state = new BattleState { Config = new BattleConfig { HandLimit = 10 } };
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_void_gaze",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DrawCardsIfEthereal,
                Target = EffectTarget.Self,
                Value = 1,
                AlternateValue = 2
            });

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(2, state.PendingDrawNextTurn);
        }

        [Test]
        public void WallOfSighs_OnRespond_AppliesEtherealToRandomAlly()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            var ally = Unit("ally", TeamSide.Player, FormationSlot.Front, 40, 5);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 40, 5);
            state.Combatants.Add(lich);
            state.Combatants.Add(ally);
            state.Combatants.Add(enemy);

            var respond = new CardInstanceState
            {
                InstanceId = 10,
                DefinitionId = "l_wall_of_sighs",
                CardType = CardType.Defense,
                Keywords = { "parry" }
            };
            respond.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 80,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            respond.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomAlly,
                StatusId = StatusCatalog.Ethereal,
                Stacks = 1,
                Duration = 1,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });

            var enemyAtk = new CardInstanceState
            {
                InstanceId = 20,
                DefinitionId = "m_basic",
                CardType = CardType.Attack
            };
            enemyAtk.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10
            });
            state.CardsById[20] = enemyAtk;
            state.LastAction = new LastActionSnapshot(enemy.Id, ActionKind.Attack, lich.Id, false, 10);

            RespondEffectExecutor.Execute(
                state,
                lich,
                respond,
                new RespondTriggerContext(enemy.Id, 20),
                new List<BattleEvent>(),
                new BattleRng(1));

            var etherealCount = 0;
            if (StatusRules.HasStatus(lich, StatusCatalog.Ethereal))
                etherealCount++;
            if (StatusRules.HasStatus(ally, StatusCatalog.Ethereal))
                etherealCount++;
            Assert.AreEqual(1, etherealCount);
        }

        [Test]
        public void DespairSoul_RecallMarker_RecallsFromDiscardOnEthereal()
        {
            var state = new BattleState
            {
                Config = new BattleConfig { HandLimit = 10 },
                Phase = TurnPhase.Planning
            };
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);

            var despair = new CardInstanceState
            {
                InstanceId = 7,
                DefinitionId = V09NewMechanicsRules.DespairSoulCardId,
                DisplayName = "绝望之魂",
                OwnerCharacterId = "char_lich_queen",
                CardType = CardType.Attack
            };
            state.PlayerDiscardPile.Add(despair);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.DespairSoulRecall, 1, -1, new List<BattleEvent>());

            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            Assert.AreEqual(1, state.PlayerHand.Count);
            Assert.AreEqual(V09NewMechanicsRules.DespairSoulCardId, state.PlayerHand[0].DefinitionId);
            Assert.AreEqual(0, state.PlayerDiscardPile.Count);
            Assert.IsFalse(state.PendingDespairSoulRecallNextTurn);
        }

        [Test]
        public void DespairSoul_DuringCombat_RecallsNextTurnStart()
        {
            var state = new BattleState
            {
                Config = new BattleConfig { HandLimit = 10 },
                Phase = TurnPhase.SpeedResolve
            };
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);

            var despair = new CardInstanceState
            {
                InstanceId = 7,
                DefinitionId = V09NewMechanicsRules.DespairSoulCardId,
                DisplayName = "绝望之魂",
                OwnerCharacterId = "char_lich_queen",
                CardType = CardType.Attack
            };
            state.PlayerDiscardPile.Add(despair);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.DespairSoulRecall, 1, -1, new List<BattleEvent>());

            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            Assert.AreEqual(0, state.PlayerHand.Count);
            Assert.AreEqual(1, state.PlayerDiscardPile.Count);
            Assert.IsTrue(state.PendingDespairSoulRecallNextTurn);

            V09NewMechanicsRules.ProcessPendingDespairSoulRecall(state, new List<BattleEvent>());
            Assert.AreEqual(1, state.PlayerHand.Count);
            Assert.AreEqual(V09NewMechanicsRules.DespairSoulCardId, state.PlayerHand[0].DefinitionId);
            Assert.AreEqual(0, state.PlayerDiscardPile.Count);
            Assert.IsFalse(state.PendingDespairSoulRecallNextTurn);
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
