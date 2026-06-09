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
        public void RollChestReward_AlwaysHasGoldAndRelicOrCard()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            for (var seed = 1; seed <= 20; seed++)
            {
                var reward = ExpeditionRewardRoller.RollChestReward(config, run, new BattleRng(seed));

                Assert.Greater(reward.Gold, 0, $"seed {seed}");
                Assert.IsTrue(reward.HasRelic || reward.HasCard, $"seed {seed} should have relic or card");
            }
        }

        [Test]
        public void RollChestReward_CardAndRelicAreMutuallyExclusive()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            for (var seed = 1; seed <= 30; seed++)
            {
                var reward = ExpeditionRewardRoller.RollChestReward(config, run, new BattleRng(seed));
                Assert.IsFalse(reward.HasRelic && reward.HasCard, $"seed {seed}");
            }
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
