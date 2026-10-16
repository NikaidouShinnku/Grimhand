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

    }
}
