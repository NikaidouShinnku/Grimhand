using Grimhand.Battle.Consumables;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ConsumableInventoryTests
    {
        [Test]
        public void TryAdd_RespectsFiveSlotLimit()
        {
            var slots = new System.Collections.Generic.List<string>();
            ConsumableInventory.EnsureInitialized(slots);

            Assert.IsTrue(ConsumableInventory.TryAdd(slots, ConsumableIds.SmallHealingPotion, out _));
            Assert.IsTrue(ConsumableInventory.TryAdd(slots, ConsumableIds.SmokeBomb, out _));
            Assert.IsTrue(ConsumableInventory.TryAdd(slots, ConsumableIds.SpringBottle, out _));
            Assert.IsTrue(ConsumableInventory.TryAdd(slots, ConsumableIds.ScrollPage, out _));
            Assert.IsTrue(ConsumableInventory.TryAdd(slots, ConsumableIds.MirrorShard, out _));
            Assert.IsFalse(ConsumableInventory.TryAdd(slots, ConsumableIds.StrengthPotion, out var full));
            Assert.IsTrue(full);
            Assert.AreEqual(5, ConsumableInventory.CountOccupied(slots));
        }

        [Test]
        public void ReplaceAt_SwapsItemInSlot()
        {
            var slots = new System.Collections.Generic.List<string>();
            ConsumableInventory.EnsureInitialized(slots);
            ConsumableInventory.TryAdd(slots, ConsumableIds.SmallHealingPotion, out _);

            ConsumableInventory.ReplaceAt(slots, 0, ConsumableIds.LargeHealingPotion);
            Assert.AreEqual(ConsumableIds.LargeHealingPotion, slots[0]);
        }
    }
}
