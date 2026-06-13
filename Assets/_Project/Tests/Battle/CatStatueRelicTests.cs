using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CatStatueRelicTests
    {
        [Test]
        public void CatStatue_GrantsSixCardsOnFirstTurn()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            config.RunModifiers = RelicDatabase.BuildModifiers(new[] { RelicIds.CatStatue });

            var engine = new BattleEngine(config);
            engine.StartBattle();

            Assert.AreEqual(6, engine.State.PlayerHand.Count);
        }

        [Test]
        public void CatStatue_SkipsPollutedCardsWhenDrawing()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 2;
            config.RunModifiers = RelicDatabase.BuildModifiers(new[] { RelicIds.CatStatue });

            var engine = new BattleEngine(config);
            engine.StartBattle();

            foreach (var card in engine.State.PlayerDrawPile)
                card.IsUsable = false;

            engine.State.PlayerHand.Clear();
            engine.State.PlayerDrawPile.Add(new CardInstanceState
            {
                InstanceId = 9001,
                DisplayName = "可用牌",
                IsUsable = true,
                CardType = CardType.Attack,
                OwnerCharacterId = engine.State.Combatants[0].CharacterDefinitionId
            });

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            Rules.DeckRules.DrawCards(engine.State, TeamSide.Player, null, 1, events);

            Assert.AreEqual(1, engine.State.PlayerHand.Count);
            Assert.AreEqual("可用牌", engine.State.PlayerHand[0].DisplayName);
            Assert.AreEqual(0, engine.State.PlayerDrawPile.Count);
        }
    }
}
