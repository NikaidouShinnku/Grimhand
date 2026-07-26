using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionAltarCardUpgradeListTests
    {
        [Test]
        public void SnakeAndLichStarterCards_AreUpgradeableInCatalog()
        {
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("蛇牙撕咬", 0));
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("蟒蛇守护", 0));
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("剧毒之触", 0));
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("女王威信", 0));
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("幽灵爪击", 0));
            Assert.IsTrue(CardUpgradeCatalog.CanUpgrade("灵魂风暴", 0));
            Assert.IsFalse(CardUpgradeCatalog.CanUpgrade("虚化形态", 0));
            Assert.IsFalse(CardUpgradeCatalog.CanUpgrade("聚能", 0));
        }

        [Test]
        public void CollectUpgradeableCards_IncludesAllPartyMembers_EvenWhenTemplateOwnerMismatches()
        {
            var config = BuildThreeMemberConfigWithMismatchedOwners();
            var party = new List<PartyMemberSnapshot>
            {
                Member("char_knight", "战士"),
                Member("char_mage", "法老"),
                Member("char_ranger", "恶魔"),
            };

            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(config, party);

            var byOwner = new Dictionary<string, int>();
            foreach (var member in party)
            {
                var count = 0;
                foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
                {
                    var template = entry.Template;
                    Assert.IsNotNull(template);
                    Assert.AreEqual(member.CharacterDefinitionId, template.OwnerCharacterId);
                    if (CardUpgradeRules.CanUpgrade(member, template.DeckInstanceId, template.DisplayName))
                        count++;
                }

                byOwner[member.CharacterDefinitionId] = count;
            }

            Assert.Greater(byOwner["char_knight"], 0, "战士应有可强化卡");
            Assert.Greater(byOwner["char_mage"], 0, "法老应有可强化卡");
            Assert.Greater(byOwner["char_ranger"], 0, "恶魔应有可强化卡");
        }

        [Test]
        public void CollectMemberDeck_FallsBackToCampDeck_WhenEncounterMissingCharacter()
        {
            var config = new ExpeditionConfig();
            var encounter = new BattleConfig();
            encounter.Combatants.Add(Player("char_ranger",
                Card("d_blood_tail", "血尾贯穿", "char_ranger")));
            config.CombatEncounters.Add(encounter);
            config.PlayerCardCatalog.Add(Card("w_basic_slash", "基础斩击", "char_knight"));
            config.PlayerCardCatalog.Add(Card("w_shield_block", "举盾格挡", "char_knight"));

            var knight = Member("char_knight", "战士");
            knight.CampDeckCardIds.Add("w_basic_slash");
            knight.CampDeckCardIds.Add("w_shield_block");

            ExpeditionDeckInstanceRules.EnsureBaseDeckInstances(config, knight);
            var entries = ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, knight);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("char_knight", entries[0].Template.OwnerCharacterId);
        }

        [Test]
        public void CaptureParty_PreservesMembersMissingFromBattleState()
        {
            var existing = new List<PartyMemberSnapshot>
            {
                Member("char_knight", "战士"),
                Member("char_mage", "法老"),
                Member("char_ranger", "恶魔"),
            };
            existing[0].BonusCards.Add(Card("w_basic_slash", "基础斩击", "char_knight"));

            var state = new BattleState
            {
                Combatants =
                {
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        CharacterDefinitionId = "char_ranger",
                        DisplayName = "恶魔",
                        Hp = 20,
                        MaxHp = 40,
                        Slot = FormationSlot.Back
                    }
                }
            };

            var party = ExpeditionBattleConfigBuilder.CaptureParty(state, existing);
            Assert.AreEqual(3, party.Count);
            Assert.NotNull(party.Find(m => m.CharacterDefinitionId == "char_knight"));
            Assert.NotNull(party.Find(m => m.CharacterDefinitionId == "char_mage"));
            Assert.NotNull(party.Find(m => m.CharacterDefinitionId == "char_ranger"));
            Assert.AreEqual(1, party.Find(m => m.CharacterDefinitionId == "char_knight").BonusCards.Count);
        }

        static PartyMemberSnapshot Member(string id, string name) =>
            new()
            {
                CharacterDefinitionId = id,
                DisplayName = name,
                Level = 1,
                Hp = 40,
                MaxHp = 40
            };

        static ExpeditionConfig BuildThreeMemberConfigWithMismatchedOwners()
        {
            var config = new ExpeditionConfig();
            var encounter = new BattleConfig();
            encounter.Combatants.Add(Player("char_knight",
                Card("w_basic_slash", "基础斩击", "char_ranger"),
                Card("w_shield_block", "举盾格挡", "char_ranger")));
            encounter.Combatants.Add(Player("char_mage",
                Card("p_sand_ray", "沙暴射线", "char_ranger"),
                Card("p_bless", "祈祷祝福", "char_ranger")));
            encounter.Combatants.Add(Player("char_ranger",
                Card("d_blood_tail", "血尾贯穿", "char_ranger"),
                Card("d_blood_armor", "鲜血铠甲", "char_ranger")));
            config.CombatEncounters.Add(encounter);
            return config;
        }

        static CombatantConfig Player(string characterId, params CardTemplate[] cards)
        {
            var cc = new CombatantConfig
            {
                Id = characterId,
                DisplayName = characterId,
                Team = TeamSide.Player,
                CharacterDefinitionId = characterId,
                Level = 1,
                MaxHp = 40
            };
            cc.DeckTemplates.AddRange(cards);
            return cc;
        }

        static CardTemplate Card(string id, string name, string owner) =>
            new()
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = owner,
                Cost = 1,
                CardType = CardType.Attack,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = 5
                    }
                }
            };
    }
}
