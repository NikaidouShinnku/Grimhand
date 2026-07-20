using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// 深渊怪卡权威定义，避免 SO 脏数据导致终焉守护/召唤/麻痹之电完全无效。
    /// </summary>
    public static class AbyssMonsterCardCatalog
    {
        public static bool TryApplyCanonical(CardTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                return false;

            CardTemplate canonical = template.DefinitionId switch
            {
                PassiveCardMechanicsRules.FinalGuardCardId => BuildFinalGuard(),
                "m_final_summon" => BuildFinalSummon(),
                "m_paralyze_sting" => BuildParalyzeSting(),
                _ => null
            };

            if (canonical == null)
                return false;

            template.DisplayName = canonical.DisplayName;
            template.OwnerCharacterId = canonical.OwnerCharacterId;
            template.Cost = canonical.Cost;
            template.CardType = canonical.CardType;
            template.Keywords.Clear();
            template.Keywords.AddRange(canonical.Keywords);
            template.Actions.Clear();
            foreach (var action in canonical.Actions)
                template.Actions.Add(EffectActionSpec.Clone(action));
            return true;
        }

        static CardTemplate BuildFinalGuard()
        {
            var card = Base(
                PassiveCardMechanicsRules.FinalGuardCardId,
                "终焉守护",
                "char_seahorse_guard",
                4,
                CardType.Defense,
                "parry");
            // 护甲由 ApplyFinalGuardBlock 统一发放；此处只保留应对减伤
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.Self,
                Value = 99,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            return card;
        }

        static CardTemplate BuildFinalSummon()
        {
            var card = Base(
                "m_final_summon",
                "终焉召唤",
                "char_jellyfish_caster",
                4,
                CardType.Status,
                "exhaust",
                "summon");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.FinalSummonPending,
                Stacks = 1,
                Duration = 3,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildParalyzeSting()
        {
            var card = Base(
                "m_paralyze_sting",
                "麻痹之电",
                "char_jellyfish_caster",
                3,
                CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 15,
                Reach = TargetReach.FrontAndMiddle,
                Condition = ReactionConditionType.None
            });
            // 换位目标在执行时取上一击伤害目标
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SwapTargetWithBehind,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate Base(
            string id,
            string name,
            string owner,
            int cost,
            CardType type,
            params string[] keywords)
        {
            var card = new CardTemplate
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = owner,
                Cost = cost,
                CardType = type
            };
            if (keywords != null)
            {
                foreach (var kw in keywords)
                {
                    if (!string.IsNullOrEmpty(kw))
                        card.Keywords.Add(kw);
                }
            }

            return card;
        }
    }
}
