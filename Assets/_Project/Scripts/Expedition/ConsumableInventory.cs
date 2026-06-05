using System.Collections.Generic;
using Grimhand.Battle.Consumables;

namespace Grimhand.Expedition
{
    public static class ConsumableInventory
    {
        public const int MaxSlots = 5;

        public static void EnsureInitialized(IList<string> slots)
        {
            while (slots.Count < MaxSlots)
                slots.Add("");
        }

        public static bool HasSpace(IList<string> slots)
        {
            EnsureInitialized(slots);
            for (var i = 0; i < MaxSlots; i++)
            {
                if (string.IsNullOrEmpty(slots[i]))
                    return true;
            }

            return false;
        }

        public static int CountOccupied(IList<string> slots)
        {
            EnsureInitialized(slots);
            var count = 0;
            for (var i = 0; i < MaxSlots; i++)
            {
                if (!string.IsNullOrEmpty(slots[i]))
                    count++;
            }

            return count;
        }

        public static bool TryAdd(IList<string> slots, string consumableId, out bool inventoryFull)
        {
            inventoryFull = false;
            if (string.IsNullOrEmpty(consumableId) || !ConsumableDatabase.TryGet(consumableId, out _))
                return false;

            EnsureInitialized(slots);
            for (var i = 0; i < MaxSlots; i++)
            {
                if (!string.IsNullOrEmpty(slots[i]))
                    continue;

                slots[i] = consumableId;
                return true;
            }

            inventoryFull = true;
            return false;
        }

        public static bool TryAddMany(IList<string> slots, string consumableId, int count, out string pendingOfferId)
        {
            pendingOfferId = "";
            if (count <= 0)
                return true;

            for (var i = 0; i < count; i++)
            {
                if (TryAdd(slots, consumableId, out var full) || !full)
                    continue;

                pendingOfferId = consumableId;
                return false;
            }

            return true;
        }

        public static void RemoveAt(IList<string> slots, int slotIndex)
        {
            EnsureInitialized(slots);
            if (slotIndex < 0 || slotIndex >= MaxSlots)
                return;

            slots[slotIndex] = "";
        }

        public static void ReplaceAt(IList<string> slots, int slotIndex, string consumableId)
        {
            EnsureInitialized(slots);
            if (slotIndex < 0 || slotIndex >= MaxSlots || string.IsNullOrEmpty(consumableId))
                return;

            slots[slotIndex] = consumableId;
        }
    }
}
