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
        public void DevilTouch_TooltipShowsOnlyCatalogKeywords()
        {
            var card = Preview("d_devil_touch", CardType.Attack,
                Kw("sacrifice"),
                Atk(2, 50, lifestealPercent: 100));

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            StringAssert.Contains("献祭", tooltip);
            StringAssert.DoesNotContain("吸血", tooltip);
            StringAssert.DoesNotContain("伤害", tooltip);
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
        public void SolarJudgment_TooltipEmptyWithoutKeywords()
        {
            var card = Preview("p_solar_judgment", CardType.Attack,
                Atk(10, 200, reach: TargetReach.Any));

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            Assert.IsTrue(string.IsNullOrEmpty(tooltip));
        }





        [Test]
        public void BladeStorm_TooltipShowsAoeKeywordOnly()
        {
            var hit = Atk(3, 100, target: EffectTarget.RandomEnemy);
            var card = Preview("w_blade_storm", CardType.Attack, Kw("aoe"), hit, hit, hit, hit, hit);

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            StringAssert.Contains("AOE", tooltip);
            StringAssert.DoesNotContain("伤害计算公式", tooltip);
        }

        [Test]
        public void PierceShot_TooltipOmitsUndocumentedKeywords()
        {
            var card = Preview("r_pierce", CardType.Attack,
                Atk(11, 100, splashBehind: true, splashPercent: 80));

            var tooltip = CardKeywordTooltipBuilder.BuildRichTooltip(card, owner: null);

            Assert.IsTrue(string.IsNullOrEmpty(tooltip));
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
            var card = new CardInstanceState
            {
                DefinitionId = id,
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
                        card.Keywords.AddRange(keywords);
                        break;
                    case EffectActionDefinition action:
                        card.Actions.Add(action.ToSpec());
                        break;
                }
            }

            return card;
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

        static EffectActionDefinition AllyDefBlock(EffectTarget target, int defenseScalePercent) =>
            new()
            {
                Type = EffectActionType.GainBlock,
                Target = target,
                ScaleWithDefense = true,
                DefenseScalePercent = defenseScalePercent
            };
    }
}
