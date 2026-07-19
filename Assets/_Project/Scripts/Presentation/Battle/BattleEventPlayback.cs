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
                if (e.Kind is BattleEventKind.StatusApplied or BattleEventKind.StatusRemoved)
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
    }
}
