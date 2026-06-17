using System.Collections.Generic;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    /// <summary>远征事件 ID 抽取；地图生成与进入节点时共用同一套过滤规则。</summary>
    public static class ExpeditionEventRoller
    {
        public const string SoulRiftResolvedFlag = "soul_rift_resolved";

        /// <summary>进入事件节点时解析实际事件：已消耗的一次性事件会重新抽取。</summary>
        public static string ResolveEventForVisit(ExpeditionRunState run, string assignedEventId, BattleRng rng)
        {
            if (run == null || rng == null)
                return assignedEventId ?? ExpeditionEventIds.MysteriousTraveler;

            if (!string.IsNullOrEmpty(assignedEventId) && !IsEventBlockedForVisit(run, assignedEventId))
                return assignedEventId;

            return PickEventId(run, rng);
        }

        public static bool IsEventBlockedForVisit(ExpeditionRunState run, string eventId)
        {
            if (run == null || string.IsNullOrEmpty(eventId))
                return false;

            if (eventId == ExpeditionEventIds.SoulRift)
                return run.UsedEventIds.Contains(eventId) || run.EventFlags.Contains(SoulRiftResolvedFlag);

            return run.UsedEventIds.Contains(eventId);
        }

        public static string PickEventId(ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<string>();
            foreach (var evt in ExpeditionEventCatalog.All)
            {
                if (IsEventBlockedForVisit(run, evt.Id))
                    continue;

                if (!string.IsNullOrEmpty(evt.PrerequisiteFlag) &&
                    !run.EventFlags.Contains(evt.PrerequisiteFlag))
                    continue;

                if (!string.IsNullOrEmpty(evt.RequiredRelicId) &&
                    !run.Relics.Contains(evt.RequiredRelicId))
                    continue;

                if (evt.RequiresDemonInParty && !PartyHasCharacter(run, "char_ranger"))
                    continue;

                if (evt.MinGold > 0 && run.Gold < evt.MinGold)
                    continue;

                pool.Add(evt.Id);
            }

            if (pool.Count == 0)
                return ExpeditionEventIds.MysteriousTraveler;

            return pool[rng.NextIndex(pool.Count)];
        }

        static bool PartyHasCharacter(ExpeditionRunState run, string charId)
        {
            foreach (var member in run.Party)
            {
                if (member.CharacterDefinitionId == charId)
                    return true;
            }

            return false;
        }
    }
}
