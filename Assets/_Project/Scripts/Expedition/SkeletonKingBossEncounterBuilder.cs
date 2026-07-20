using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

namespace Grimhand.Expedition
{
    /// <summary>
    /// 远征 Boss 遭遇回退：当 ExpeditionSetup 未配置 BossEncounters 时使用（无需预生成 SO）。
    /// </summary>
    public static class SkeletonKingBossEncounterBuilder
    {
        public static BattleConfig BuildTemplate(BattleConfig standardEncounter)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter.EnergyCap,
                TurnStartEnergyRegen = standardEncounter.TurnStartEnergyRegen,
                HandLimit = standardEncounter.HandLimit,
                CardsDrawnPerTurn = standardEncounter.CardsDrawnPerTurn,
                EnemyCardsDrawnPerTurn = 3,
                EnemyTurnEnergyBudget = 3,
                SkipFloorScaling = true
            };

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            config.Combatants.Add(BuildSkeletonKing());
            config.SummonTemplates[SummonRules.ExplosiveSkullCharacterId] = BuildExplosiveSkullTemplate();
            return config;
        }

        static CombatantConfig BuildSkeletonKing()
        {
            var king = new CombatantConfig
            {
                Id = "Character_Skeleton_King",
                DisplayName = "骷髅王",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_skeleton_king",
                MaxHp = 350,
                BaseAttack = 30,
                BaseDefense = 10,
                Speed = 6
            };

            king.Traits.Add(CharacterTraitCatalog.BossFirstHitBlock);

            AddDeck(king, KingBoneSlash(), 4);
            AddDeck(king, KingBoneRoar(), 1);
            AddDeck(king, KingBoneSpear(), 2);
            AddDeck(king, KingSummonThrone(), 1);
            AddDeck(king, KingBoneBlock(), 1);
            AddDeck(king, KingBoneShield(), 2);
            AddDeck(king, KingWhiteStorm(), 1);
            return king;
        }

        static CombatantConfig BuildExplosiveSkullTemplate()
        {
            var skull = new CombatantConfig
            {
                Id = "Character_Explosive_Skull",
                DisplayName = "易爆骷髅头",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = SummonRules.ExplosiveSkullCharacterId,
                MaxHp = 20,
                BaseAttack = 0,
                BaseDefense = 5,
                Speed = 2
            };

            skull.Traits.Add(CharacterTraitCatalog.SkullSelfDestructHand);
            skull.DeckTemplates.Add(SkullExplode());
            return skull;
        }

        static void AddDeck(CombatantConfig cc, CardTemplate card, int count)
        {
            for (var i = 0; i < count; i++)
                cc.DeckTemplates.Add(ExpeditionBattleConfigBuilder.CloneTemplate(card));
        }

        static CardTemplate KingBoneSlash() =>
            Dmg("m_king_bone_slash", "骨王斩击", 1, 15, TargetReach.FrontAndMiddle);

        static CardTemplate KingBoneRoar()
        {
            var card = BaseCard("m_king_bone_roar", "骨王怒吼", 1, CardType.Status, "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemies,
                Value = 2,
                StatusId = StatusCatalog.Slow,
                Stacks = 2,
                Duration = 4
            });
            return card;
        }

        static CardTemplate KingBoneSpear() =>
            Dmg("m_king_bone_spear", "投掷骨矛", 1, 15, TargetReach.MiddleAndBack);

        static CardTemplate KingSummonThrone()
        {
            var card = BaseCard("m_king_summon_workshop", "召唤骨之王座", 3, CardType.Status, "exhaust", "summon");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.BoneWorkshop,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate KingBoneBlock()
        {
            var card = BaseCard("m_king_bone_block", "骨甲格挡", 1, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 80,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            return card;
        }

        static CardTemplate KingBoneShield()
        {
            var card = BaseCard("m_king_bone_shield", "召唤骨盾", 2, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                ScaleWithDefense = true,
                DefenseScalePercent = 200
            });
            return card;
        }

        static CardTemplate KingWhiteStorm()
        {
            var card = BaseCard("m_king_white_storm", "白骨风暴", 3, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 12,
                ScaleWithAttack = true,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate SkullExplode()
        {
            var card = BaseCard("m_skull_explode", "骷髅自爆", 0, CardType.Attack, "self_destruct", "bonus_hand");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.RandomEnemy,
                Value = 24
            });
            return card;
        }

        static CardTemplate Dmg(
            string id,
            string name,
            int cost,
            int flat,
            TargetReach reach,
            string keyword) =>
            Dmg(id, name, cost, flat, reach, new[] { keyword });

        static CardTemplate Dmg(
            string id,
            string name,
            int cost,
            int flat,
            TargetReach reach,
            params string[] keywords)
        {
            var card = BaseCard(id, name, cost, CardType.Attack, keywords);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = flat,
                ScaleWithAttack = true,
                Reach = reach
            });
            return card;
        }

        static CardTemplate StatusCard(string id, string name, int cost, params string[] keywords)
        {
            var card = BaseCard(id, name, cost, CardType.Status, keywords);
            card.Actions.Add(SlowAction(EffectTarget.DefaultEnemy, 2));
            return card;
        }

        static EffectActionSpec SlowAction(EffectTarget target, int stacks) =>
            new()
            {
                Type = EffectActionType.ApplyStatus,
                Target = target,
                StatusId = StatusCatalog.Slow,
                Stacks = stacks,
                Duration = 2
            };

        static CardTemplate BaseCard(
            string id,
            string name,
            int cost,
            CardType type,
            params string[] keywords)
        {
            var card = new CardTemplate
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = id.StartsWith("m_skull") ? SummonRules.ExplosiveSkullCharacterId : "char_skeleton_king",
                Cost = cost,
                CardType = type
            };

            foreach (var keyword in keywords)
                card.Keywords.Add(keyword);

            return card;
        }
    }
}
