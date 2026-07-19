using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class DrawCardsTests
    {
        [Test]
        public void DrawCardsEffect_WithoutQuickStart_QueuesNextTurn()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            var engine = new BattleEngine(config);
            engine.StartBattle();

            var state = engine.State;
            var actor = state.Combatants[0];
            var card = new CardInstanceState
            {
                InstanceId = 9999,
                DisplayName = "测试抽牌",
                OwnerCharacterId = actor.CharacterDefinitionId,
                CardType = CardType.Status,
                Cost = 0,
                IsUsable = true
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DrawCards,
                Target = EffectTarget.Self,
                Value = 2
            });

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            var handBefore = state.PlayerHand.Count;
            EffectActionExecutor.ExecuteAll(state, actor, card, events, null);

            Assert.AreEqual(handBefore, state.PlayerHand.Count);
            Assert.AreEqual(2, state.PendingDrawNextTurn);
        }

        [Test]
        public void DrawCardsEffect_WithQuickStart_DrawsImmediately()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            var engine = new BattleEngine(config);
            engine.StartBattle();

            var state = engine.State;
            var actor = state.Combatants[0];
            var card = new CardInstanceState
            {
                InstanceId = 9998,
                DisplayName = "快速抽牌",
                OwnerCharacterId = actor.CharacterDefinitionId,
                CardType = CardType.Status,
                Cost = 0,
                IsUsable = true
            };
            card.Keywords.Add("quick_start");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DrawCards,
                Target = EffectTarget.Self,
                Value = 2
            });

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            var handBefore = state.PlayerHand.Count;
            EffectActionExecutor.ExecuteAll(state, actor, card, events, null);

            Assert.AreEqual(handBefore + 2, state.PlayerHand.Count);
            Assert.AreEqual(0, state.PendingDrawNextTurn);
        }

        [Test]
        public void PendingDraw_IncreasesNextTurnHandSize()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            engine.State.PendingDrawNextTurn = 2;
            engine.SkipPlayerTurn();
            engine.FlushPendingEndOfTurn();

            Assert.AreEqual(7, engine.State.PlayerHand.Count);
            Assert.AreEqual(0, engine.State.PendingDrawNextTurn);
        }

        [Test]
        public void TurnStartDraw_DiscardsAtEndOfTurn_OnlyInheritRetains()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            config.HandLimit = 8;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            foreach (var card in engine.State.PlayerHand)
                Assert.IsFalse(CardRules.HasInheritKeyword(card), "回合初抽牌不应带继承");

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            engine.State.Phase = TurnPhase.SpeedResolve;
            DeckRules.DrawCards(engine.State, TeamSide.Player, null, 1, events);
            var battleDrawn = engine.State.PlayerHand[^1];
            Assert.IsFalse(CardRules.HasInheritKeyword(battleDrawn), "战斗阶段即时抽牌默认不带继承");

            battleDrawn.Keywords.Add("inherit");

            DeckRules.DiscardHandAtEndOfTurn(engine.State, TeamSide.Player, events);

            Assert.AreEqual(1, engine.State.PlayerHand.Count);
            Assert.AreSame(battleDrawn, engine.State.PlayerHand[0]);
        }

        [Test]
        public void MemoryFragment_QuickStartDraw_DoesNotCarryToNextTurn()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            config.HandLimit = 8;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            var state = engine.State;
            var mage = state.Combatants.Find(c => c.CharacterDefinitionId == "char_mage");
            Assert.IsNotNull(mage);

            var fragment = new CardInstanceState
            {
                InstanceId = 88001,
                DefinitionId = "p_memory_fragment",
                DisplayName = "记忆残片",
                OwnerCharacterId = "char_mage",
                CardType = CardType.Status,
                Cost = 2,
                IsUsable = true
            };
            fragment.Keywords.Add("quick_start");
            fragment.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DrawCards,
                Target = EffectTarget.Self,
                Value = 2
            });
            state.CardsById[fragment.InstanceId] = fragment;
            state.PlayerHand.Add(fragment);

            var handBeforeQuickStart = state.PlayerHand.Count;
            Assert.True(engine.TryResolveQuickStartCard(fragment.InstanceId));
            Assert.AreEqual(handBeforeQuickStart + 1, state.PlayerHand.Count,
                "快速启动：打出记忆残片后手牌 = 原手牌 -1 +2");

            engine.SkipPlayerTurn();
            engine.FlushPendingEndOfTurn();

            Assert.AreEqual(5, state.PlayerHand.Count,
                "下回合应仅抽到 5 张，不应保留快速启动抽到的牌");
        }

        [Test]
        public void InheritCard_RetainsAcrossTurn_AndStillDrawsFullHand()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            config.HandLimit = 8;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            var state = engine.State;
            var inheritCard = state.PlayerHand[0];
            inheritCard.Keywords.Add("inherit");

            engine.SkipPlayerTurn();
            engine.FlushPendingEndOfTurn();

            Assert.AreEqual(6, state.PlayerHand.Count, "1 张继承 + 下回合抽 5 张");
            Assert.IsTrue(state.PlayerHand.Exists(CardRules.HasInheritKeyword));
        }

        [Test]
        public void PollutedInheritCard_DoesNotRetain_AndCyclesToDiscard()
        {
            var state = new BattleState();
            var inherit = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "test_inherit",
                DisplayName = "继承牌",
                IsUsable = false,
                Keywords = { "inherit" }
            };
            state.PlayerHand.Add(inherit);
            state.CardsById[1] = inherit;

            Assert.IsFalse(CardRules.HasInheritKeyword(inherit));
            DeckRules.DiscardHandAtEndOfTurn(state, TeamSide.Player, new List<BattleEvent>());

            Assert.AreEqual(0, state.PlayerHand.Count);
            Assert.AreEqual(1, state.PlayerDiscardPile.Count);
            Assert.AreEqual(1, state.PlayerDiscardPile[0].InstanceId);
        }
    }
}
