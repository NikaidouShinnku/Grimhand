using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>牌面与悬停框中的位置/射程标签（【前/中】等）。</summary>
    public static class CardReachFormatter
    {
        public static bool CardHasReachTooltip(CardInstanceState card)
        {
            if (card?.Actions == null)
                return false;

            var pickSide = CardRules.GetRequiredTargetPick(card);
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!string.IsNullOrEmpty(GetFaceTag(action, pickSide)))
                    return true;
            }

            return false;
        }

        public static string GetFaceTag(EffectActionSpec action, TargetPickSide pickSide)
        {
            if (action == null || !ShouldShowReachTag(action, pickSide))
                return "";

            return GetTagForReach(action.Reach, pickSide);
        }

        public static string BuildReachTooltipRichText(EffectActionSpec action, TargetPickSide pickSide)
        {
            var tag = GetFaceTag(action, pickSide);
            if (string.IsNullOrEmpty(tag))
                return "";

            return $"<b>{tag}</b>\n{GetTooltipDescription(action.Reach, pickSide)}";
        }

        static bool ShouldShowReachTag(EffectActionSpec action, TargetPickSide pickSide)
        {
            if (action.Target == EffectTarget.AllEnemies)
                return false;

            if (action.Target is EffectTarget.RandomEnemy or EffectTarget.RandomEnemies)
                return false;

            if (action.Target == EffectTarget.Self)
                return false;

            if (action.Target is EffectTarget.AllyFrontSlot
                or EffectTarget.AllyMiddleSlot
                or EffectTarget.AllyBackSlot)
                return false;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                case EffectActionType.Heal:
                case EffectActionType.GainBlock:
                case EffectActionType.ApplyStatus:
                case EffectActionType.RemoveStatus:
                    break;
                default:
                    return false;
            }

            if (pickSide == TargetPickSide.Ally)
                return action.Reach != TargetReach.Any;

            if (pickSide == TargetPickSide.Enemy || IsEnemyTarget(action.Target))
                return true;

            return action.Reach != TargetReach.Any;
        }

        static bool IsEnemyTarget(EffectTarget target) =>
            target is EffectTarget.DefaultEnemy
                or EffectTarget.ManualSelected
                or EffectTarget.EnemyFrontSlot
                or EffectTarget.EnemyMiddleSlot
                or EffectTarget.EnemyBackSlot;

        static string GetTagForReach(TargetReach reach, TargetPickSide pickSide)
        {
            if (pickSide == TargetPickSide.Ally && reach == TargetReach.Any)
                return "";

            return reach switch
            {
                TargetReach.FrontAndMiddle => "【前/中】",
                TargetReach.Any => "【前/中/后】",
                TargetReach.BackOnly => "【后排】",
                TargetReach.MiddleAndBack => "【中/后】",
                _ => ""
            };
        }

        static string GetTooltipDescription(TargetReach reach, TargetPickSide pickSide)
        {
            if (pickSide == TargetPickSide.Ally)
            {
                return reach switch
                {
                    TargetReach.FrontAndMiddle => "可以选择前排或中排的一名队友",
                    TargetReach.BackOnly => "可以选择后排的一名队友",
                    TargetReach.MiddleAndBack => "可以选择中排或后排的一名队友",
                    TargetReach.Any => "可以选择任意一名队友",
                    _ => ""
                };
            }

            return reach switch
            {
                TargetReach.FrontAndMiddle => "可以选择前排和中排的一名敌人",
                TargetReach.Any => "可以选择前排、中排或后排的一名敌人",
                TargetReach.BackOnly => "可以选择后排的一名敌人",
                TargetReach.MiddleAndBack => "可以选择中排和后排的一名敌人",
                _ => ""
            };
        }
    }
}
