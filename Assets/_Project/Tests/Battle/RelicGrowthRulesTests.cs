using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class RelicGrowthRulesTests
    {
        [Test]
        public void AcquireAtFloor50_AppliesTwoGrowthTiers()
        {
            var tiers = new Dictionary<string, int>();
            RelicGrowthRules.OnRelicAcquired(tiers, RelicIds.FlameSword, 50);

            Assert.AreEqual(2, tiers[RelicIds.FlameSword]);

            var mods = RelicDatabase.BuildModifiers(new[] { RelicIds.FlameSword }, tiers);
            Assert.AreEqual(0, mods.TeamAttackBonus);
            Assert.AreEqual(9f, mods.TeamAttackBonusPercent);
            Assert.AreEqual(15, mods.AttackBurnStacks);
        }

        [Test]
        public void CrossingFloorTwenty_UpdatesExistingRelic()
        {
            var tiers = new Dictionary<string, int>();
            var relics = new List<string> { RelicIds.IronArmor };

            RelicGrowthRules.OnRelicAcquired(tiers, RelicIds.IronArmor, 10);
            Assert.AreEqual(0, tiers[RelicIds.IronArmor]);

            RelicGrowthRules.SyncFloorGrowth(tiers, relics, 20);
            Assert.AreEqual(1, tiers[RelicIds.IronArmor]);

            var mods = RelicDatabase.BuildModifiers(relics, tiers);
            Assert.AreEqual(0, mods.TeamDefenseBonus);
            Assert.AreEqual(7f, mods.TeamBlockGainBonusPercent);
            Assert.AreEqual(25, mods.BattleStartFrontBlock);
        }
    }
}
