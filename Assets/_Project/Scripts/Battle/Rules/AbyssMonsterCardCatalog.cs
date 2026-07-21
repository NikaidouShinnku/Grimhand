using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// 深渊怪卡权威定义，避免 SO 脏数据导致终焉守护/召唤/麻痹之电/潮汐/凝视/贯穿触手无效。
    /// </summary>
    public static class AbyssMonsterCardCatalog
    {
        public const string MermaidSlashCardId = "m_mermaid_slash";
        public const string WaveCleaveCardId = "m_wave_cleave";
        public const string TidalPowerCardId = "m_tidal_power";
        public const string AbyssCreatureGazeCardId = "m_abyss_creature_gaze";
        public const string PiercingTentacleCardId = "m_piercing_tentacle";
        public const string PinchArmorCardId = "m_pinch_armor";
        public const string FesterClawCardId = "m_fester_claw";

        public static bool TryApplyCanonical(CardTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                return false;

            CardTemplate canonical = template.DefinitionId switch
            {
                PassiveCardMechanicsRules.FinalGuardCardId => BuildFinalGuard(),
                "m_final_summon" => BuildFinalSummon(),
                "m_paralyze_sting" => BuildParalyzeSting(),
                TidalPowerCardId => BuildTidalPower(),
                AbyssCreatureGazeCardId => BuildAbyssCreatureGaze(),
                "m_abyss_gaze" => BuildAbyssGaze(),
                PiercingTentacleCardId => BuildPiercingTentacle(),
                PinchArmorCardId => BuildPinchArmor(),
                FesterClawCardId => BuildFesterClaw(),
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

        static CardTemplate BuildTidalPower()
        {
            var card = Base(
                TidalPowerCardId,
                "潮汐之力",
                "char_mermaid_warrior",
                3,
                CardType.Status);
            // 自身获得 30% 增伤（3 回合）
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 30,
                Duration = 3,
                Condition = ReactionConditionType.None
            });
            // 劈砍 / 破浪斩费用 -1（2 回合）
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.MermaidTidalCostCut,
                Stacks = 1,
                Duration = 2,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildAbyssCreatureGaze()
        {
            var card = Base(
                AbyssCreatureGazeCardId,
                "深渊凝视",
                MinionTraitCatalog.AbyssCreatureCharacterId,
                3,
                CardType.Status,
                "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 50,
                Duration = 2,
                Reach = TargetReach.Any,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildAbyssGaze()
        {
            var card = Base(
                "m_abyss_gaze",
                "深渊凝视",
                "char_corrupted_crab",
                3,
                CardType.Status,
                "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 50,
                Duration = 2,
                Reach = TargetReach.Any,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildPiercingTentacle()
        {
            var card = Base(
                PiercingTentacleCardId,
                "贯穿之触手",
                MinionTraitCatalog.AbyssCreatureCharacterId,
                2,
                CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 17,
                Reach = TargetReach.Any,
                Condition = ReactionConditionType.None
            });
            // 按命中前中毒层数结算真实伤害（主伤害被动上毒不计入本段）
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealTrueDamagePerStatusStack,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Poison,
                Stacks = 1,
                Reach = TargetReach.Any,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildPinchArmor()
        {
            var card = Base(
                PinchArmorCardId,
                "夹断护甲",
                "char_corrupted_crab",
                2,
                CardType.Status);
            // 移除目标全部护甲；选敌优先有护甲者（见 EffectActionExecutor）
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.StripBlockThenDealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 0,
                Stacks = 0,
                Reach = TargetReach.Any,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildFesterClaw()
        {
            var card = Base(
                FesterClawCardId,
                "溃烂钳击",
                "char_corrupted_crab",
                2,
                CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 9,
                Reach = TargetReach.FrontAndMiddle,
                Condition = ReactionConditionType.None
            });
            // 仅当上一击被成功应对时翻倍减益（见 EffectActionExecutor）
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DoubleAllDebuffStacksAndDuration,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle,
                Condition = ReactionConditionType.None
            });
            return card;
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
