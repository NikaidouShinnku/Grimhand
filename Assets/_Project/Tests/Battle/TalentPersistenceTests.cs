using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Tests.Battle
{
    public sealed class TalentPersistenceTests
    {
        [Test]
        public void CaptureParty_PreservesSelectedTalentIds()
        {
            var existing = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = TalentCatalog.KnightId,
                    SelectedTalentSlot1Id = "talent_knight_s1_lv1",
                    SelectedTalentSlot2Id = "talent_knight_s2_lv6"
                }
            };

            var state = new BattleState();
            state.Combatants.Add(new CombatantState
            {
                Id = "p1",
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentCatalog.KnightId,
                Hp = 20,
                MaxHp = 20
            });

            var captured = ExpeditionBattleConfigBuilder.CaptureParty(state, existing);

            Assert.AreEqual(1, captured.Count);
            Assert.AreEqual("talent_knight_s1_lv1", captured[0].SelectedTalentSlot1Id);
            Assert.AreEqual("talent_knight_s2_lv6", captured[0].SelectedTalentSlot2Id);
        }

        [Test]
        public void BuildEncounter_MergesPartyTalentsIntoBattleConfig()
        {
            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = TalentCatalog.MageId,
                    Hp = 30,
                    MaxHp = 30,
                    SelectedTalentSlot1Id = "talent_mage_s1_lv5"
                }
            };

            var template = new BattleConfig();
            template.Combatants.Add(new CombatantConfig
            {
                Id = "p1",
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentCatalog.MageId,
                MaxHp = 30,
                BaseAttack = 5,
                BaseDefense = 5,
                Speed = 5
            });
            template.Combatants.Add(new CombatantConfig
            {
                Id = "e1",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "mob_slime",
                MaxHp = 10,
                BaseAttack = 3,
                BaseDefense = 1,
                Speed = 3
            });

            var talentRun = new ExpeditionTalentRunState();
            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                party,
                new List<string>(),
                battleSeed: 42,
                applyPartyHp: true,
                talentRunState: talentRun,
                isBossBattle: false);

            Assert.IsTrue(config.Talents.Has("talent_mage_s1_lv5"));
            Assert.IsTrue(config.Talents.MageReviveAvailable);
        }

        [Test]
        public void ApplyTeamHpBonus_KnightSlot2TalentWorksWithoutRelicTeamHpBonus()
        {
            var state = new BattleState
            {
                Config = new BattleConfig
                {
                    Talents = new TalentBattleContext(),
                    RunModifiers = new RunModifierSnapshot()
                }
            };
            state.Config.Talents.ActiveTalentIds.Add("talent_knight_s2_lv6");
            state.Combatants.Add(new CombatantState
            {
                Id = "knight",
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentCatalog.KnightId,
                Hp = 50,
                MaxHp = 50
            });
            state.Combatants.Add(new CombatantState
            {
                Id = "mage",
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentCatalog.MageId,
                Hp = 40,
                MaxHp = 40
            });

            RelicBattleRules.ApplyTeamHpBonus(state, state.Config.RunModifiers);

            Assert.AreEqual(60, state.Combatants[0].MaxHp);
            Assert.AreEqual(60, state.Combatants[0].Hp);
            Assert.AreEqual(50, state.Combatants[1].MaxHp);
            Assert.AreEqual(50, state.Combatants[1].Hp);
        }
    }
}
