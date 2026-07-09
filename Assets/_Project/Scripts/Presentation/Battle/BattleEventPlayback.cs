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

        /// <summary>按「单次出牌 / 独立受击 / 独立治疗」拆成演出段落；段落内保留 BlockGained 等非立绘事件以便同步护甲 UI。</summary>
        public static List<List<BattleEvent>> SplitIntoSegments(IReadOnlyList<BattleEvent> events)
        {
            var segments = new List<List<BattleEvent>>();
            if (events == null || events.Count == 0)
                return segments;

            List<BattleEvent> current = null;

            foreach (var e in events)
            {
                if (e.Kind == BattleEventKind.PortraitPoseChanged)
                {
                    if (current != null && current.Count > 0)
                        segments.Add(current);

                    current = new List<BattleEvent> { e };
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

            if (current != null && current.Count > 0)
                segments.Add(current);

            return segments;
        }
    }
}
