using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Tests.Battle
{
    public class MonsterEncounterCatalogTests
    {
        [Test]
        public void Roll_ReturnsEncounterInFloorRange()
        {
            var rng = new BattleRng(42);
            var id = MonsterEncounterCatalog.Roll(3, isElite: false, rng);
            var def = MonsterEncounterCatalog.GetById(id);
            Assert.NotNull(def);
            Assert.IsFalse(def.IsElite);
            Assert.LessOrEqual(def.MinFloor, 3);
            Assert.GreaterOrEqual(def.MaxFloor, 3);
        }

        [Test]
        public void Roll_EliteOgreOnlyAfterFloor10()
        {
            var rng = new BattleRng(99);
            var foundOgre = false;
            for (var i = 0; i < 50; i++)
            {
                var id = MonsterEncounterCatalog.Roll(15, isElite: true, rng);
                if (id == MonsterEncounterCatalog.EliteOgreSolo)
                    foundOgre = true;
            }

            Assert.IsTrue(foundOgre);
        }
    }
}
