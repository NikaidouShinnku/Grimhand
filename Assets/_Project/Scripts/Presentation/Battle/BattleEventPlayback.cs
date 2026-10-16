using System.Collections.Generic;
using Grimhand.Battle.Events;

namespace Grimhand.Presentation.Battle
{
    public static class BattleEventPlayback
    {
        public static bool IsPresentationKind(BattleEventKind kind) =>
            kind switch
            {
                BattleEventKind.PortraitPoseChanged => true,
                BattleEventKind.PortraitIdleRestored => true,
                BattleEventKind.DamageApplied => true,
                BattleEventKind.StatusTickDamage => true,
                BattleEventKind.CharacterDied => true,
                BattleEventKind.CombatantSpawned => true,
                BattleEventKind.ParryTriggered => true,
                BattleEventKind.HealApplied => true,
                BattleEventKind.CharacterRevived => true,
                BattleEventKind.StatusApplied => true,
                BattleEventKind.StatusRemoved => true,
                BattleEventKind.StatusExpired => true,
                BattleEventKind.IronWallConverted => true,
                BattleEventKind.BlockGained => true,
                BattleEventKind.PositionSwapped => true,
                _ => false
            };

        public static bool ContainsPresentationEvents(IReadOnlyList<BattleEvent> events)
        {
            if (events == null)
                return false;

            foreach (var e in events)
            {
                if (IsPresentationKind(e.Kind))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 按「单次出牌 / 独立受击 / 独立治疗」拆成演出段落。
        /// 出牌前孤立的 StatusApplied/Removed（如应对上毒）并入下一张牌动画，避免脚标早于/晚于卡牌演出。
        /// </summary>
        public static List<List<BattleEvent>> SplitIntoSegments(IReadOnlyList<BattleEvent> events)
        {
            var segments = new List<List<BattleEvent>>();
            if (events == null || events.Count == 0)
                return segments;

            List<BattleEvent> current = null;
            List<BattleEvent> pendingStatus = null;

            foreach (var e in events)
            {
                if (e.Kind == BattleEventKind.PortraitPoseChanged)
                {
                    if (current != null && current.Count > 0)
                        segments.Add(current);

                    current = new List<BattleEvent> { e };
                    if (pendingStatus != null && pendingStatus.Count > 0)
                    {
                        current.AddRange(pendingStatus);
                        pendingStatus.Clear();
                    }

                    continue;
                }

                if (current != null)
                {
                    current.Add(e);
                    if (e.Kind == BattleEventKind.PortraitIdleRestored)
                    {
                        segments.Add(current);
                        current = null;
                    }

                    continue;
                }

                // 尚无出牌段：状态挂起，等下一张牌 Pose 再播（时机对齐卡牌动画）。
                if (e.Kind is BattleEventKind.StatusApplied or BattleEventKind.StatusRemoved or BattleEventKind.StatusExpired)
                {
                    pendingStatus ??= new List<BattleEvent>();
                    pendingStatus.Add(e);
                    continue;
                }

                if (!IsPresentationKind(e.Kind))
                    continue;

                current = new List<BattleEvent> { e };
                if (e.Kind is BattleEventKind.PortraitIdleRestored
                    or BattleEventKind.HealApplied
                    or BattleEventKind.CharacterRevived)
                {
                    segments.Add(current);
                    current = null;
                }
            }

            // 无后续出牌时，独立播出挂起的状态（如仅状态跳字回合）。
            if (pendingStatus != null && pendingStatus.Count > 0)
                segments.Add(pendingStatus);

            if (current != null && current.Count > 0)
                segments.Add(current);

            return segments;
        }

        /// <summary>Batch contiguous hits only; never move healing, death or status changes after later hits.</summary>
        public static (List<BattleEvent> damages, List<BattleEvent> gaps, int lastConsumed) CollectDamageWave(
            IReadOnlyList<BattleEvent> events, int startIndex)
        {
            var first = events[startIndex];
            var batch = new List<BattleEvent> { first };
            var gaps = new List<BattleEvent>();
            var last = startIndex;
            for (var i = startIndex + 1; i < events.Count; i++)
            {
                var next = events[i];
                if (next.Kind == BattleEventKind.DamageApplied
                    && next.CombatantId == first.CombatantId
                    && next.CardInstanceId == first.CardInstanceId
                    && next.IsAoEWave == first.IsAoEWave)
                {
                    batch.Add(next);
                    last = i;
                    continue;
                }
                // Pure bookkeeping can be consumed, but all presentation events retain their order.
                if (next.Kind is BattleEventKind.DeckPolluted or BattleEventKind.CardDrawn
                    or BattleEventKind.EnergyChanged or BattleEventKind.ReactionTriggered
                    || first.IsAoEWave && next.Kind == BattleEventKind.CardDiscarded)
                {
                    gaps.Add(next);
                    last = i;
                    continue;
                }
                break;
            }
            return (batch, gaps, last);
        }
    }
}
