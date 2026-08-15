using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionAltarUpgradeRulesTests
    {
        [Test]
        public void AltarHpUpgrade_PersistsAfterSyncPartyEffectiveMaxHp()
        {
            var run = new ExpeditionRunState
            {
                SharedXpPool = 100
            };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 80,
                MaxHp = 80
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryUpgradeMemberHp(run, member));
            Assert.AreEqual(5, member.AltarMaxHpBonus);
            Assert.AreEqual(1, member.AltarHpPlus5Purchases);
            Assert.AreEqual(85, member.MaxHp);
            Assert.AreEqual(85, member.Hp);

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            Assert.AreEqual(5, member.AltarMaxHpBonus);
            Assert.AreEqual(85, member.MaxHp);
            Assert.AreEqual(85, member.Hp);
        }

        [Test]
        public void AltarHpUpgrade_CostIncreasesPerMemberIndependently()
        {
            var run = new ExpeditionRunState { SharedXpPool = 200 };
            var a = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "战士",
                Level = 1,
                Hp = 80,
                MaxHp = 80
            };
            var b = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_mage",
                DisplayName = "法老",
                Level = 1,
                Hp = 60,
                MaxHp = 60
            };
            run.Party.Add(a);
            run.Party.Add(b);

            Assert.AreEqual(8, ExpeditionAltarUpgradeRules.GetHpPlus5Cost(a));
            Assert.AreEqual(8, ExpeditionAltarUpgradeRules.GetHpPlus5Cost(b));
            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryUpgradeMemberHp(run, a));
            Assert.AreEqual(10, ExpeditionAltarUpgradeRules.GetHpPlus5Cost(a));
            Assert.AreEqual(8, ExpeditionAltarUpgradeRules.GetHpPlus5Cost(b));
        }

        [Test]
        public void ApplyPartyProgress_UsesMemberMaxHpIncludingAltarBonus()
        {
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 80,
                MaxHp = 85,
                AltarMaxHpBonus = 5
            };
            var cc = new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = member.CharacterDefinitionId,
                Level = member.Level
            };

            ExpeditionBattleConfigBuilder.ApplyPartyProgress(cc, member);

            Assert.AreEqual(85, cc.MaxHp);
            Assert.AreEqual(80, cc.StartHp);
        }

        [Test]
        public void EnergyAndHandLimitUpgrades_ApplyToBattleConfig()
        {
            var run = new ExpeditionRunState { SharedXpPool = 500 };
            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryUpgradeEnergyCap(run));
            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryUpgradeHandLimit(run));
            Assert.AreEqual(1, run.Modifiers.EnergyCapBonus);
            Assert.AreEqual(1, run.Modifiers.DrawPerTurnBonus);
        }

        [Test]
        public void CardUpgrade_IncreasesLevelAndAppliesToTemplate()
        {
            var run = new ExpeditionRunState { SharedXpPool = 200 };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士"
            };
            const string deckInstanceId = "deck_test_1";
            const string displayName = "基础斩击";

            if (CardUpgradeRules.GetMaxLevel(displayName) <= 0)
                Assert.Ignore($"测试卡牌「{displayName}」未在升级表中配置。");

            Assert.IsTrue(
                ExpeditionAltarUpgradeRules.TryUpgradeMemberCard(run, member, deckInstanceId, displayName));
            Assert.AreEqual(1, CardUpgradeRules.GetLevel(member, deckInstanceId));

            var template = new CardTemplate
            {
                DeckInstanceId = deckInstanceId,
                DisplayName = displayName,
                Actions = { new EffectActionSpec { Type = EffectActionType.DealDamage, Value = 6 } }
            };
            var before = template.Actions[0].Value;
            CardUpgradeRules.ApplyToTemplate(template, member);
            Assert.Greater(template.Actions[0].Value, before);
        }

        [Test]
        public void IronParry_UpgradeIncreasesMitigationAndReflect()
        {
            var template = new CardTemplate
            {
                DefinitionId = "w_iron_parry",
                DisplayName = "铁壁弹反",
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.GainBlockFromLastDamagePercent,
                        Value = 30
                    },
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ReflectLastDamageToAttacker,
                        Value = 100
                    }
                }
            };

            CardUpgradeRules.ApplyToTemplate(template, 1);
            Assert.AreEqual(35, template.Actions[0].Value);
            Assert.AreEqual(110, template.Actions[1].Value);
        }

        [Test]
        public void UpgradedCardPreview_DoesNotReuseStaticCatalogTextWhenDefinitionsProvided()
        {
            var def = ScriptableObject.CreateInstance<CardDefinitionSO>();
            def.CardId = "l_ghost_claw";
            def.DisplayName = "幽灵爪击";
            def.Cost = 1;
            def.CardType = CardType.Attack;
            def.Actions.Add(new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 7,
                Reach = TargetReach.Any
            });

            var definitions = new Dictionary<string, CardDefinitionSO>
            {
                [def.CardId] = def
            };

            var baseTemplate = def.ToTemplate();
            var basePreview = CardVisualResolver.CreatePreviewInstanceFromTemplate(baseTemplate, def);
            var baseText = BattleUiFormatters.BuildCardStatsLinePreview(basePreview, definitions);

            var upgraded = def.ToTemplate();
            CardUpgradeRules.ApplyToTemplate(upgraded, 1);
            var upgradedPreview = CardVisualResolver.CreatePreviewInstanceFromTemplate(upgraded, def);
            var upgradedText = BattleUiFormatters.BuildCardStatsLinePreview(upgradedPreview, definitions);

            StringAssert.Contains("7", baseText);
            StringAssert.Contains("8", upgradedText);
            Assert.AreNotEqual(baseText, upgradedText);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void RestHeal_HealsDownedMember()
        {
            var run = new ExpeditionRunState
            {
                Gold = 50
            };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 0,
                MaxHp = 100
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            Assert.IsTrue(ExpeditionAltarUpgradeRules.PartyHasRestHealableMember(run));
            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryRestHealWithGold(run));
            Assert.Greater(member.Hp, 0);
            Assert.LessOrEqual(member.Hp, member.MaxHp);
        }

        [Test]
        public void RestHeal_WithGold_HealsPartyWithoutLeavingAltar()
        {
            var run = new ExpeditionRunState
            {
                Gold = 50,
                SharedXpPool = 100
            };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 40,
                MaxHp = 100
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryRestHealWithGold(run));
            Assert.AreEqual(20, run.Gold);
            Assert.Greater(member.Hp, 40);
            Assert.LessOrEqual(member.Hp, member.MaxHp);
        }

        [Test]
        public void RestHeal_WithXp_HealsParty()
        {
            var run = new ExpeditionRunState
            {
                Gold = 0,
                SharedXpPool = 30
            };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 50,
                MaxHp = 100
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            var hpBefore = member.Hp;

            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryRestHealWithXp(run));
            Assert.AreEqual(10, run.SharedXpPool);
            Assert.Greater(member.Hp, hpBefore);
        }

        [Test]
        public void RestHeal_DoesNothingWhenPartyFullHp()
        {
            var run = new ExpeditionRunState { Gold = 100, SharedXpPool = 100 };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 100,
                MaxHp = 100
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            Assert.IsFalse(ExpeditionAltarUpgradeRules.TryRestHealWithGold(run));
            Assert.AreEqual(100, run.Gold);
        }

        [Test]
        public void RestHeal_AfterGoldHealToFull_BlocksXpHeal()
        {
            var run = new ExpeditionRunState
            {
                Gold = 50,
                SharedXpPool = 30
            };
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 1,
                Hp = 78,
                MaxHp = 80
            };
            run.Party.Add(member);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            Assert.IsTrue(ExpeditionAltarUpgradeRules.TryRestHealWithGold(run));
            Assert.AreEqual(80, member.Hp);
            Assert.IsFalse(ExpeditionAltarUpgradeRules.PartyHasRestHealableMember(run));
            Assert.IsFalse(ExpeditionAltarUpgradeRules.CanRestHealWithXp(run));
            Assert.IsFalse(ExpeditionAltarUpgradeRules.TryRestHealWithXp(run));
            Assert.AreEqual(30, run.SharedXpPool);
        }
    }
}
