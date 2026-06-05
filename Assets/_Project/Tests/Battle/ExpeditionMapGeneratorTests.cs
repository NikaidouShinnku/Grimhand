using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Map;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionMapGeneratorTests
    {
        [Test]
        public void Generate_CreatesTenLayersWithBossOnTop()
        {
            var config = new ExpeditionConfig { ChapterLayerCount = 10, RunSeed = 42 };
            config.CombatEncounters.Add(new BattleConfig());
            var run = new ExpeditionRunState();
            var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(42));

            Assert.AreEqual(10, map.Layers.Count);
            Assert.IsTrue(map.Layers[9].IsBoss);
            Assert.AreEqual(1, map.Layers[0].Options.Count);
            Assert.AreEqual(ExpeditionNodeType.Combat, map.Layers[0].Options[0].NodeType);
            Assert.AreEqual(1, map.Layers[9].Options.Count);
        }

        [Test]
        public void Generate_EarlyLayersHaveThreeOptionsExceptFirst()
        {
            var config = new ExpeditionConfig { ChapterLayerCount = 10, RunSeed = 7 };
            config.CombatEncounters.Add(new BattleConfig());
            var run = new ExpeditionRunState();
            var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(7));

            Assert.AreEqual(1, map.Layers[0].Options.Count);
            Assert.AreEqual(ExpeditionNodeType.Combat, map.Layers[0].Options[0].NodeType);
            Assert.AreEqual(3, map.Layers[1].Options.Count);
            Assert.AreEqual(3, map.Layers[2].Options.Count);
        }
    }
}
