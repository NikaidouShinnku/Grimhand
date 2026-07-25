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
        public void Generate_RegionBossLayersAreSingleBossOnly()
        {
            var config = new ExpeditionConfig
            {
                ChapterLayerCount = ExpeditionRegionRules.FullLayerCount,
                RunSeed = 123
            };
            config.CombatEncounters.Add(new BattleConfig());
            var run = new ExpeditionRunState();
            var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(123));

            Assert.AreEqual(ExpeditionRegionRules.FullLayerCount, map.Layers.Count);
            foreach (var bossLayer in new[]
                     {
                         ExpeditionRegionRules.CaveBossLayer,
                         ExpeditionRegionRules.DungeonBossLayer,
                         ExpeditionRegionRules.AbyssBossLayer
                     })
            {
                var row = map.GetLayer(bossLayer);
                Assert.IsNotNull(row);
                Assert.IsTrue(row.IsBoss, $"Layer {bossLayer} should be marked boss.");
                Assert.AreEqual(1, row.Options.Count, $"Layer {bossLayer} must have exactly one route.");
                Assert.AreEqual(ExpeditionNodeType.Boss, row.Options[0].NodeType);
            }
        }

        [Test]
        public void Generate_LayersAfterFirstHaveTwoToFourOptions()
        {
            var config = new ExpeditionConfig { ChapterLayerCount = 10, RunSeed = 7 };
            config.CombatEncounters.Add(new BattleConfig());
            var run = new ExpeditionRunState();
            var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(7));

            Assert.AreEqual(1, map.Layers[0].Options.Count);
            for (var layer = 2; layer < map.ChapterLayerCount; layer++)
            {
                if (ExpeditionRegionRules.IsMandatoryBossLayer(layer))
                    continue;

                var count = map.Layers[layer - 1].Options.Count;
                Assert.GreaterOrEqual(count, 2);
                Assert.LessOrEqual(count, 4);
            }
        }

        [Test]
        public void Generate_ThreeOrMoreOptionsAlwaysIncludeCombat()
        {
            var config = new ExpeditionConfig { ChapterLayerCount = 10, RunSeed = 99 };
            config.CombatEncounters.Add(new BattleConfig());
            var run = new ExpeditionRunState();
            var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(99));

            foreach (var row in map.Layers)
            {
                if (row.IsBoss || row.Options.Count < 3)
                    continue;

                var hasCombat = false;
                foreach (var option in row.Options)
                {
                    if (option.NodeType is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                        hasCombat = true;
                }

                Assert.IsTrue(hasCombat, $"Layer {row.LayerNumber} has {row.Options.Count} options but no combat route.");
            }
        }

        [Test]
        public void Generate_LayerOptionsHaveUniqueNodeTypes()
        {
            var config = new ExpeditionConfig { ChapterLayerCount = 20, RunSeed = 1 };
            config.CombatEncounters.Add(new BattleConfig());

            for (var seed = 0; seed < 200; seed++)
            {
                var run = new ExpeditionRunState();
                var map = ExpeditionMapGenerator.Generate(config, run, new Core.BattleRng(seed));

                foreach (var row in map.Layers)
                {
                    if (row.IsBoss || row.Options.Count <= 1)
                        continue;

                    var seen = new System.Collections.Generic.HashSet<ExpeditionNodeType>();
                    foreach (var option in row.Options)
                    {
                        Assert.IsTrue(
                            seen.Add(option.NodeType),
                            $"Seed {seed}: layer {row.LayerNumber} has duplicate node type {option.NodeType}.");
                    }
                }
            }
        }
    }
}
