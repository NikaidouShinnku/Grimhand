using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class CardDescriptionTests
    {
        [Test]
        public void DevilTouch_FaceShowsReachTagAndLifesteal()
        {
            var card = Preview("d_devil_touch", CardType.Attack,
                Atk(2, 50, lifestealPercent: 100));

            var text = BattleUiFormatters.BuildCardStatsLinePreview(card);

            StringAssert.Contains("【前/中】造成", text);
            StringAssert.Contains("回复等量 HP", text);
            StringAssert.DoesNotContain("选择一名敌人", text);
        }

        [Test]
        public void DevilTouch_TooltipExplainsReachAndFormula()
        {
            var card = Preview("d_devil_touch", CardType.Attack,
                Atk(2, 50, lifestealPercent: 100));

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            StringAssert.Contains("【前/中】", tooltip);
            StringAssert.Contains("可以选择前排和中排的一名敌人", tooltip);
            StringAssert.Contains("伤害计算公式", tooltip);
            StringAssert.Contains("吸血", tooltip);
        }

        [Test]
        public void SolarJudgment_FaceShowsFullReach()
        {
            var card = Preview("p_solar_judgment", CardType.Attack,
                Atk(10, 200, reach: TargetReach.Any));

            var text = BattleUiFormatters.BuildCardStatsLinePreview(card);

            StringAssert.Contains("【前/中/后】造成", text);
        }

        [Test]
        public void SolarJudgment_TooltipExplainsFullReach()
        {
            var card = Preview("p_solar_judgment", CardType.Attack,
                Atk(10, 200, reach: TargetReach.Any));

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            StringAssert.Contains("可以选择前排、中排或后排的一名敌人", tooltip);
        }

        [Test]
        public void Bless_UsesPickLeadWithoutEnemyReachTag()
        {
            var card = Preview("p_bless", CardType.Status, Heal(2, 100, EffectTarget.FrontAlly));

            var text = BattleUiFormatters.BuildCardStatsLinePreview(card);

            StringAssert.Contains("选择一名队友", text);
            StringAssert.DoesNotContain("【前/中】", text);
        }

        [Test]
        public void HellFire_AoeNoReachTag()
        {
            var card = Preview("d_hell_fire", CardType.Attack,
                Kw("aoe", "sacrifice"),
                SelfDmg(8),
                Atk(5, 100, target: EffectTarget.AllEnemies));

            var text = BattleUiFormatters.BuildCardStatsLinePreview(card);

            StringAssert.Contains("【AOE】", text);
            StringAssert.Contains("对全体敌人各造成", text);
            StringAssert.DoesNotContain("【前/中】", text);
        }

        [Test]
        public void IronParry_KeywordOnce_ReactionWithoutDuplicatePrefix()
        {
            var card = Preview("w_iron_parry", CardType.Defense, Kw("parry"),
                RespondReduce(30),
                RespondReflect(100));

            var text = BattleUiFormatters.BuildCardStatsLinePreview(card);

            Assert.AreEqual(1, CountOccurrences(text, "【应对攻击】"));
            StringAssert.Contains("所受伤害减少 30%", text);
        }

        static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var idx = 0;
            while ((idx = text.IndexOf(value, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += value.Length;
            }

            return count;
        }

        static CardInstanceState Preview(string id, CardType type, params object[] parts)
        {
            var def = new CardDefinitionSO
            {
                CardId = id,
                DisplayName = id,
                OwnerCharacterId = "char_ranger",
                Cost = 1,
                CardType = type
            };

            foreach (var part in parts)
            {
                switch (part)
                {
                    case string[] keywords:
                        def.Keywords.AddRange(keywords);
                        break;
                    case EffectActionDefinition action:
                        def.Actions.Add(action);
                        break;
                }
            }

            return CardVisualResolver.CreatePreviewInstance(id, def.OwnerCharacterId, def.DisplayName, def);
        }

        static string[] Kw(params string[] ids) => ids;

        static EffectActionDefinition Atk(
            int flat,
            int atkPct,
            EffectTarget target = EffectTarget.DefaultEnemy,
            TargetReach reach = TargetReach.FrontAndMiddle,
            int lifestealPercent = 0,
            int onKillHeal = 0,
            bool splashBehind = false,
            int splashPercent = 100,
            int bonusHpBelowPercent = 0,
            int bonusHpBelowFlat = 0)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = target,
                Value = flat,
                ScaleWithAttack = true,
                AttackScalePercent = atkPct,
                Reach = target == EffectTarget.AllEnemies ? TargetReach.Any : reach,
                LifestealPercent = lifestealPercent,
                OnKillHealAmount = onKillHeal,
                SplashBehindTarget = splashBehind,
                SplashPowerPercent = splashPercent,
                BonusIfTargetHpBelowPercent = bonusHpBelowPercent,
                BonusIfTargetHpBelowFlat = bonusHpBelowFlat
            };
        }

        static EffectActionDefinition Heal(int flat, int atkPct, EffectTarget target) =>
            new()
            {
                Type = EffectActionType.Heal,
                Target = target,
                Value = flat,
                ScaleWithAttack = true,
                AttackScalePercent = atkPct,
                Reach = TargetReach.Any
            };

        static EffectActionDefinition SelfDmg(int amount) =>
            new()
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = amount
            };

        static EffectActionDefinition RespondReduce(int pct) =>
            new()
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = pct,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            };

        static EffectActionDefinition RespondReflect(int pct) =>
            new()
            {
                Type = EffectActionType.ReflectLastDamageToAttacker,
                Target = EffectTarget.LastActionActor,
                Value = pct,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            };
    }
}
