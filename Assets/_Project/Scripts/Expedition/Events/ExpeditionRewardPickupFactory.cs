using System.Collections.Generic;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public static class ExpeditionRewardPickupFactory
    {
        public static string RollRelicId(ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<string>();
            foreach (var relic in RelicDatabase.All)
            {
                if (run.Relics.Contains(relic.Id))
                    continue;

                if (!RelicDatabase.CanAppearInRewardPool(relic, run.Party))
                    continue;

                pool.Add(relic.Id);
            }

            if (pool.Count == 0)
                return "";

            return pool[rng.NextIndex(pool.Count)];
        }

        public static ExpeditionRewardPickup Relic(string relicId, string header, RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (string.IsNullOrEmpty(relicId))
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                RelicId = relicId
            };
        }

        public static ExpeditionRewardPickup Gold(int amount, string header, RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (amount <= 0)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                Gold = amount
            };
        }
    }
}
