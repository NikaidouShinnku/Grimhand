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
        public void GrowthTiersForFloor_MatchesEveryTwentyLayers()
        {
            Assert.AreEqual(0, RelicGrowthRules.GrowthTiersForFloor(18));
            Assert.AreEqual(0, RelicGrowthRules.GrowthTiersForFloor(19));
            Assert.AreEqual(1, RelicGrowthRules.GrowthTiersForFloor(20));
            Assert.AreEqual(1, RelicGrowthRules.GrowthTiersForFloor(21));
            Assert.AreEqual(1, RelicGrowthRules.GrowthTiersForFloor(39));
            Assert.AreEqual(2, RelicGrowthRules.GrowthTiersForFloor(40));
            Assert.AreEqual(2, RelicGrowthRules.GrowthTiersForFloor(50));
        }

        [Test]
        public void AcquireAtFloor18_StartsAtTier0_ThenSyncAt20()
        {
            var tiers = new Dictionary<string, int>();
            var relics = new List<string> { RelicIds.Bonfire };

            RelicGrowthRules.OnRelicAcquired(tiers, RelicIds.Bonfire, 18);
            Assert.AreEqual(0, tiers[RelicIds.Bonfire]);

            RelicGrowthRules.SyncFloorGrowth(tiers, relics, 20);
            Assert.AreEqual(1, tiers[RelicIds.Bonfire]);

            var mods = RelicDatabase.BuildModifiers(relics, tiers);
            Assert.AreEqual(4f, mods.PostBattleTeamHealPercent);
        }

        [Test]
        public void AcquireAtFloor21_StartsAlreadyUpgraded()
        {
            var tiers = new Dictionary<string, int>();
            RelicGrowthRules.OnRelicAcquired(tiers, RelicIds.Bonfire, 21);
            Assert.AreEqual(1, tiers[RelicIds.Bonfire]);
        }

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

        [Test]
        public void ApplyGrowthBonuses_MatchesTableForAllGrowingRelics()
        {
            AssertGrowth(RelicIds.SunPyramid, m => m.StatusCardTeamBlock, baseValue: 3, perTier: 5);
            AssertGrowth(RelicIds.KnightInCastle, m => m.WarriorFirstHitBlockAmount, baseValue: 12, perTier: 8);
            AssertGrowth(RelicIds.BloodAlter, m => m.SacrificeHpCostReductionPercent, baseValue: 15f, perTier: 5f);
            AssertGrowth(RelicIds.JadeStone, m => m.TurnStartRandomAllyBlock, baseValue: 2, perTier: 2);
            AssertGrowth(RelicIds.JadeRing, m => m.TurnStartTeamBlock, baseValue: 3, perTier: 3);
            AssertGrowth(RelicIds.JadeDagger, m => m.TeamAttackBonusPercent, baseValue: 5f, perTier: 2f);
            AssertGrowth(RelicIds.CrimsonBurningBoots, m => m.TurnStartEnemyBurnStacks, baseValue: 2, perTier: 1);
            AssertGrowth(RelicIds.FlameSword, m => m.TeamAttackBonusPercent, baseValue: 5f, perTier: 2f);
            AssertGrowth(RelicIds.FlameSword, m => m.AttackBurnStacks, baseValue: 5, perTier: 5);
            AssertGrowth(RelicIds.IronArmor, m => m.TeamBlockGainBonusPercent, baseValue: 5f, perTier: 2f);
            AssertGrowth(RelicIds.IronArmor, m => m.BattleStartFrontBlock, baseValue: 15, perTier: 10);
            AssertGrowth(RelicIds.WarriorHelmet, m => m.TeamHpBonus, baseValue: 8, perTier: 8);
            AssertGrowth(RelicIds.WarriorHelmet, m => m.RevengeAttackFlatBonus, baseValue: 4, perTier: 4);
            AssertGrowth(RelicIds.DragonRing, m => m.TeamAttackBonus, baseValue: 0, perTier: 3);
            AssertGrowth(RelicIds.PaladinShield, m => m.FirstHitDamageReductionPercent, baseValue: 30f, perTier: 5f);
            AssertGrowth(RelicIds.SilverMoonPendant, m => m.EndTurnTeamHeal, baseValue: 2, perTier: 2);
            AssertGrowth(RelicIds.TaichiRing, m => m.FirstAttackFlatBonus, baseValue: 5, perTier: 5);
            AssertGrowth(RelicIds.TaichiRing, m => m.FirstDefenseFlatBonus, baseValue: 5, perTier: 5);
            AssertGrowth(RelicIds.TaichiRing, m => m.AttackAndDefenseSameTurnHeal, baseValue: 5, perTier: 5);
            AssertGrowth(RelicIds.LeafOfMiracle, m => m.MiracleLeafReviveHpPercent, baseValue: 20, perTier: 10);
            AssertGrowth(RelicIds.Bonfire, m => m.PostBattleTeamHealPercent, baseValue: 3f, perTier: 1f);
        }

        [Test]
        public void NonGrowingRelics_HaveNoGrowthBranch()
        {
            var noGrowth = new[]
            {
                RelicIds.BurningBoots,
                RelicIds.CatStatue,
                RelicIds.ElfBow,
                RelicIds.BurningLongsword,
                RelicIds.CrystalLongsword,
                RelicIds.Felskull,
                RelicIds.HolysunSpellbook
            };

            foreach (var id in noGrowth)
            {
                var tiers = new Dictionary<string, int> { [id] = 2 };
                var baseMods = RelicDatabase.BuildModifiers(new[] { id }, null);
                var grownMods = RelicDatabase.BuildModifiers(new[] { id }, tiers);
                Assert.AreEqual(
                    SnapshotKey(baseMods),
                    SnapshotKey(grownMods),
                    $"遗物 {id} 不应有成长加成");
            }
        }

        [Test]
        public void Evolve_PreservesTransferredHigherTiers()
        {
            var run = new ExpeditionRunState
            {
                Phase = ExpeditionPhase.EventChoice,
                Map = new ExpeditionMapState { NodesCompleted = 19, ChapterLayerCount = 60 }
            };
            run.Relics.Add(RelicIds.JadeStone);
            run.RelicGrowthTiers[RelicIds.JadeStone] = 2;

            Assert.IsTrue(RelicGrowthRules.TryEvolveRelic(run, RelicIds.JadeStone, RelicIds.JadeRing));
            Assert.IsFalse(run.Relics.Contains(RelicIds.JadeStone));
            Assert.IsTrue(run.Relics.Contains(RelicIds.JadeRing));
            Assert.AreEqual(2, run.RelicGrowthTiers[RelicIds.JadeRing]);
        }

        [Test]
        public void Evolve_OnLayerTwenty_RaisesToTierOne()
        {
            var run = new ExpeditionRunState
            {
                Phase = ExpeditionPhase.EventChoice,
                Map = new ExpeditionMapState { NodesCompleted = 19, ChapterLayerCount = 60 }
            };
            run.Relics.Add(RelicIds.BurningBoots);
            RelicGrowthRules.OnRelicAcquired(run.RelicGrowthTiers, RelicIds.BurningBoots, 10);

            Assert.IsTrue(RelicGrowthRules.TryEvolveRelic(
                run, RelicIds.BurningBoots, RelicIds.CrimsonBurningBoots));
            Assert.AreEqual(1, run.RelicGrowthTiers[RelicIds.CrimsonBurningBoots]);
        }

        static void AssertGrowth<T>(
            string relicId,
            System.Func<RunModifierSnapshot, T> selector,
            T baseValue,
            T perTier)
        {
            var tiers = new Dictionary<string, int> { [relicId] = 1 };
            var mods = RelicDatabase.BuildModifiers(new[] { relicId }, tiers);
            var expected = Add(baseValue, perTier);
            Assert.AreEqual(expected, selector(mods), relicId);
        }

        static T Add<T>(T a, T b)
        {
            if (a is int ai && b is int bi)
                return (T)(object)(ai + bi);
            if (a is float af && b is float bf)
                return (T)(object)(af + bf);
            Assert.Fail($"Unsupported growth type {typeof(T)}");
            return default;
        }

        static string SnapshotKey(RunModifierSnapshot m) =>
            string.Join("|",
                m.TeamAttackBonus,
                m.TeamAttackBonusPercent,
                m.TeamBlockGainBonusPercent,
                m.TeamHpBonus,
                m.StatusCardTeamBlock,
                m.WarriorFirstHitBlockAmount,
                m.SacrificeHpCostReductionPercent,
                m.TurnStartRandomAllyBlock,
                m.TurnStartTeamBlock,
                m.TurnStartEnemyBurnStacks,
                m.AttackBurnStacks,
                m.BattleStartFrontBlock,
                m.RevengeAttackFlatBonus,
                m.FirstHitDamageReductionPercent,
                m.EndTurnTeamHeal,
                m.FirstAttackFlatBonus,
                m.FirstDefenseFlatBonus,
                m.AttackAndDefenseSameTurnHeal,
                m.MiracleLeafReviveHpPercent,
                m.PostBattleTeamHealPercent,
                m.ExtraDrawOnBattleStart,
                m.BattleStartSpeedBonus,
                m.BattleStartSpeedBonusTurns,
                m.HolysunSpellbookBonusUpgradeLevels,
                m.FrontRowBurnTargetDamageMultiplier,
                m.FrontRowIgnoreArmorDamagePercent,
                m.RequiresFelskullChoice);
    }
}
