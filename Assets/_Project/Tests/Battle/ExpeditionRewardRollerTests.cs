using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionRewardRollerTests
    {
        [Test]
        public void RollChestReward_AlwaysHasGoldAndCommonPack()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            for (var seed = 1; seed <= 20; seed++)
            {
                var reward = ExpeditionRewardRoller.RollChestReward(config, run, new BattleRng(seed));

                Assert.Greater(reward.Gold, 0, $"seed {seed}");
                Assert.IsTrue(reward.HasCardPacks, $"seed {seed}");
                Assert.AreEqual(CardPackIds.Common, reward.CardPacks[0].PackId, $"seed {seed}");
            }
        }

        [Test]
        public void RollVictoryRewards_CaveNormal_IncludesCommonPack()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            for (var seed = 1; seed <= 20; seed++)
            {
                var reward = ExpeditionRewardRoller.RollVictoryRewards(
                    config,
                    run,
                    new BattleRng(seed),
                    floor: 3,
                    isElite: false,
                    isBoss: false);

                Assert.That(reward.Gold, Is.InRange(15, 20), $"seed {seed}");
                Assert.IsTrue(reward.HasCardPacks, $"seed {seed}");
                Assert.AreEqual(CardPackIds.Common, reward.CardPacks[0].PackId, $"seed {seed}");
            }
        }

        [Test]
        public void RollVictoryRewards_Boss_IncludesMasterPack()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            var reward = ExpeditionRewardRoller.RollVictoryRewards(
                config,
                run,
                new BattleRng(42),
                floor: 20,
                isElite: false,
                isBoss: true);

            Assert.AreEqual(40, reward.Gold);
            Assert.IsTrue(reward.HasRelic);
            Assert.IsTrue(reward.HasConsumable);
            Assert.IsTrue(reward.HasCardPacks);
            Assert.AreEqual(CardPackIds.Master, reward.CardPacks[0].PackId);
        }

        [Test]
        public void RollChestReward_ConsumableIsOptional()
        {
            var config = BuildConfig();
            config.TreasureConsumableChancePercent = 0;
            var run = new ExpeditionRunState();
            var reward = ExpeditionRewardRoller.RollChestReward(config, run, new BattleRng(42));

            Assert.IsFalse(reward.HasConsumable);
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig();
            var encounter = new BattleConfig();
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "战士",
                MaxHp = 50,
                DeckTemplates =
                {
                    new CardTemplate
                    {
                        DefinitionId = "w_basic_slash",
                        DisplayName = "基础斩击",
                        OwnerCharacterId = "char_knight"
                    }
                }
            });
            config.CombatEncounters.Add(encounter);
            return config;
        }
    }
}
