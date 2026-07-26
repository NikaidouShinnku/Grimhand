using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;
using UnityEditor;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionAltarUpgradeDeckFromAssetsTests
    {
        [Test]
        public void DemoSetup_DefaultParty_AllThreeCharactersHaveUpgradeableCards()
        {
            var setup = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            Assert.IsNotNull(setup, "缺少 ExpeditionSetup_Demo");

            var battleSetup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(
                "Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            Assert.IsNotNull(battleSetup, "缺少 BattleSetup_Demo");

            var config = setup.ToExpeditionConfig();
            Assert.Greater(config.CombatEncounters.Count, 0);

            var party = new System.Collections.Generic.List<PartyMemberSnapshot>();
            foreach (var character in battleSetup.Combatants)
            {
                if (character == null || character.Team != Grimhand.Battle.Model.TeamSide.Player)
                    continue;
                if (party.Count >= CampRosterState.PartySize)
                    break;

                party.Add(new PartyMemberSnapshot
                {
                    CharacterDefinitionId = character.CharacterId,
                    DisplayName = character.DisplayName,
                    Level = 1,
                    Hp = character.MaxHp,
                    MaxHp = character.MaxHp
                });
            }

            Assert.AreEqual(3, party.Count, "默认编队应为 3 人");
            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(config, party);

            var report = "";
            var totalUp = 0;
            foreach (var member in party)
            {
                var entries = ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member);
                var up = 0;
                foreach (var entry in entries)
                {
                    var t = entry.Template;
                    if (CardUpgradeRules.CanUpgrade(member, t.DeckInstanceId, t.DisplayName))
                        up++;
                }

                totalUp += up;
                report += $"{member.CharacterDefinitionId}: deck={entries.Count} up={up}; ";
                Assert.Greater(entries.Count, 0, $"{member.CharacterDefinitionId} 牌组为空");
                Assert.Greater(up, 0, $"{member.CharacterDefinitionId} 无可升级卡。{report}");
            }

            Assert.GreaterOrEqual(totalUp, 15, $"全队可升级卡过少：{report}");
        }

        [Test]
        public void SnakeLichRangerParty_HasUpgradeableCardsForEachMember()
        {
            var setup = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            Assert.IsNotNull(setup);
            var config = setup.ToExpeditionConfig();

            var party = new System.Collections.Generic.List<PartyMemberSnapshot>
            {
                Member("char_snake_queen", "毒蛇女王"),
                Member("char_lich_queen", "巫妖女王"),
                Member("char_ranger", "恶魔"),
            };
            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(config, party);

            foreach (var member in party)
            {
                var up = 0;
                foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
                {
                    var t = entry.Template;
                    if (CardUpgradeRules.CanUpgrade(member, t.DeckInstanceId, t.DisplayName))
                        up++;
                }

                Assert.Greater(up, 0, $"{member.CharacterDefinitionId} 应有可升级卡");
            }
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
    }
}
