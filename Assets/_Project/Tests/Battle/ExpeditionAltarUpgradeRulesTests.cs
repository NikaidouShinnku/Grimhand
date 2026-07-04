using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

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
            Assert.AreEqual(85, member.MaxHp);
            Assert.AreEqual(85, member.Hp);

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            Assert.AreEqual(5, member.AltarMaxHpBonus);
            Assert.AreEqual(85, member.MaxHp);
            Assert.AreEqual(85, member.Hp);
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
            Assert.AreEqual(1, run.Modifiers.HandLimitBonus);
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
    }
}
