using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionBattleConfigBuilderTests
    {
        [Test]
        public void CaptureParty_PreservesBonusCardsAndExtractedIndices()
        {
            var previous = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "骑士"
                }
            };
            previous[0].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "altar_card",
                DisplayName = "祭坛卡",
                OwnerCharacterId = "char_knight"
            });
            previous[0].ExtractedCampCardIndices.Add(2);

            var state = new BattleState
            {
                Outcome = BattleOutcome.PlayerVictory,
                Combatants =
                {
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        CharacterDefinitionId = "char_knight",
                        DisplayName = "骑士",
                        Hp = 20,
                        MaxHp = 40
                    }
                }
            };

            var party = ExpeditionBattleConfigBuilder.CaptureParty(state, previous);
            Assert.AreEqual(1, party.Count);
            Assert.AreEqual(1, party[0].BonusCards.Count);
            Assert.AreEqual("altar_card", party[0].BonusCards[0].DefinitionId);
            Assert.IsTrue(party[0].ExtractedCampCardIndices.Contains(2));
        }

        [Test]
        public void CaptureParty_WithEmptyBattleState_PreservesPreviousParty()
        {
            var previous = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "骑士",
                    Hp = 25,
                    MaxHp = 40
                }
            };
            previous[0].BonusCards.Add(new CardTemplate { DefinitionId = "bonus_card" });

            var party = ExpeditionBattleConfigBuilder.CaptureParty(new BattleState(), previous);
            Assert.AreEqual(1, party.Count);
            Assert.AreEqual(1, party[0].BonusCards.Count);
            Assert.AreEqual(25, party[0].Hp);
        }

        [Test]
        public void CaptureParty_BeforeClearingSourceList_PreservesBonusCards()
        {
            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "骑士"
                }
            };
            party[0].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "altar_card",
                DisplayName = "祭坛卡",
                OwnerCharacterId = "char_knight"
            });

            var state = new BattleState
            {
                Outcome = BattleOutcome.PlayerVictory,
                Combatants =
                {
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        CharacterDefinitionId = "char_knight",
                        DisplayName = "骑士",
                        Hp = 20,
                        MaxHp = 40
                    }
                }
            };

            var captured = ExpeditionBattleConfigBuilder.CaptureParty(state, party);
            party.Clear();
            party.AddRange(captured);

            Assert.AreEqual(1, party[0].BonusCards.Count);
            Assert.AreEqual("altar_card", party[0].BonusCards[0].DefinitionId);
        }

        [Test]
        public void BuildEncounter_HighFloorScalesEnemiesOnlyNotPlayers()
        {
            var template = new BattleConfig();
            template.Combatants.Add(new CombatantConfig
            {
                Id = "player_knight",
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                Level = 1,
                MaxHp = 999,
                BaseAttack = 999,
                BaseDefense = 999
            });
            template.Combatants.Add(new CombatantConfig
            {
                Id = "enemy_goblin",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_goblin",
                MaxHp = 20,
                BaseAttack = 4,
                BaseDefense = 1
            });

            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "骑士",
                    Level = 1,
                    Hp = 30,
                    MaxHp = 30
                }
            };

            var floor1 = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, new List<string>(), battleSeed: 11, applyPartyHp: true, floor: 1);
            var floor12 = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, new List<string>(), battleSeed: 11, applyPartyHp: true, floor: 12);

            var player1 = FindCombatant(floor1, TeamSide.Player);
            var player12 = FindCombatant(floor12, TeamSide.Player);
            var enemy1 = FindCombatant(floor1, TeamSide.Enemy);
            var enemy12 = FindCombatant(floor12, TeamSide.Enemy);

            Assert.AreEqual(player1.MaxHp, player12.MaxHp);
            Assert.AreEqual(0, player1.BaseAttack);
            Assert.AreEqual(0, player12.BaseAttack);
            Assert.Greater(enemy12.MaxHp, enemy1.MaxHp);
            Assert.AreEqual(0, enemy1.BaseAttack);
            Assert.AreEqual(0, enemy12.BaseAttack);
        }

        [Test]
        public void GrantXpToMember_PreservesKnightTalentMaxHpBonusOnLevelUp()
        {
            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = TalentCatalog.KnightId,
                    Level = 1,
                    Xp = 0,
                    Hp = 60,
                    MaxHp = 60,
                    SelectedTalentSlot2Id = "talent_knight_s2_lv6"
                }
            };

            ExpeditionBattleConfigBuilder.GrantXpToMember(party[0], 8);

            Assert.AreEqual(2, party[0].Level);
            Assert.AreEqual(66, party[0].MaxHp);
            Assert.AreEqual(66, party[0].Hp);
        }

        [Test]
        public void ApplyPartyProgress_DoesNotResetMemberEffectiveMaxHp()
        {
            var member = new PartyMemberSnapshot
            {
                CharacterDefinitionId = TalentCatalog.KnightId,
                Level = 1,
                Hp = 60,
                MaxHp = 60,
                SelectedTalentSlot2Id = "talent_knight_s2_lv6"
            };
            var cc = new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentCatalog.KnightId
            };

            ExpeditionBattleConfigBuilder.ApplyPartyProgress(cc, member);

            Assert.AreEqual(60, member.MaxHp);
            Assert.AreEqual(50, cc.MaxHp);
        }

        [Test]
        public void CaptureParty_MatchesExistingBySlotIndex_NotCharacterId()
        {
            var previous = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "战士"
                },
                new()
                {
                    CharacterDefinitionId = "char_snake_queen",
                    DisplayName = "毒蛇女王"
                },
                new()
                {
                    CharacterDefinitionId = "char_lich_queen",
                    DisplayName = "巫妖女王"
                }
            };
            previous[1].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "snake_bonus",
                OwnerCharacterId = "char_snake_queen"
            });
            previous[2].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "lich_bonus",
                OwnerCharacterId = "char_lich_queen"
            });

            var state = new BattleState
            {
                Outcome = BattleOutcome.PlayerVictory,
                Combatants =
                {
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Front,
                        CharacterDefinitionId = "char_knight",
                        DisplayName = "恶魔",
                        Hp = 30,
                        MaxHp = 40
                    },
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Middle,
                        CharacterDefinitionId = "char_ranger",
                        DisplayName = "恶魔",
                        Hp = 40,
                        MaxHp = 45
                    },
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Back,
                        CharacterDefinitionId = "char_lich_queen",
                        DisplayName = "恶魔",
                        Hp = 35,
                        MaxHp = 45
                    }
                }
            };

            var party = ExpeditionBattleConfigBuilder.CaptureParty(state, previous);

            Assert.AreEqual(3, party.Count);
            Assert.AreEqual("char_ranger", party[1].CharacterDefinitionId);
            Assert.AreEqual("恶魔", party[1].DisplayName);
            Assert.AreEqual(0, party[1].BonusCards.Count);
            Assert.AreEqual("char_lich_queen", party[2].CharacterDefinitionId);
            Assert.AreEqual("巫妖女王", party[2].DisplayName);
            Assert.AreEqual(1, party[2].BonusCards.Count);
            Assert.AreEqual("lich_bonus", party[2].BonusCards[0].DefinitionId);
        }

        [Test]
        public void CaptureParty_DoesNotAppendFourthMember()
        {
            var previous = new List<PartyMemberSnapshot>
            {
                new() { CharacterDefinitionId = "char_knight", DisplayName = "战士" },
                new() { CharacterDefinitionId = "char_snake_queen", DisplayName = "毒蛇女王" },
                new() { CharacterDefinitionId = "char_lich_queen", DisplayName = "巫妖女王" },
                new() { CharacterDefinitionId = "char_ranger", DisplayName = "恶魔" }
            };

            var state = new BattleState
            {
                Outcome = BattleOutcome.PlayerVictory,
                Combatants =
                {
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Front,
                        CharacterDefinitionId = "char_knight",
                        DisplayName = "战士",
                        Hp = 30,
                        MaxHp = 40
                    },
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Middle,
                        CharacterDefinitionId = "char_snake_queen",
                        DisplayName = "毒蛇女王",
                        Hp = 50,
                        MaxHp = 55
                    },
                    new CombatantState
                    {
                        Team = TeamSide.Player,
                        Slot = FormationSlot.Back,
                        CharacterDefinitionId = "char_lich_queen",
                        DisplayName = "巫妖女王",
                        Hp = 40,
                        MaxHp = 45
                    }
                }
            };

            var party = ExpeditionBattleConfigBuilder.CaptureParty(state, previous);
            Assert.AreEqual(3, party.Count);
        }

        [Test]
        public void BuildEncounter_AppliesPartyBySlotIndex_NotCharacterIdMatch()
        {
            var template = new BattleConfig();
            template.Combatants.Add(new CombatantConfig
            {
                Id = "player_slot1",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_knight",
                DisplayName = "战士",
                Level = 1,
                MaxHp = 80
            });
            template.Combatants.Add(new CombatantConfig
            {
                Id = "player_slot2",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = "char_mage",
                DisplayName = "法老",
                Level = 1,
                MaxHp = 60
            });
            template.Combatants.Add(new CombatantConfig
            {
                Id = "player_slot3",
                Team = TeamSide.Player,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_ranger",
                DisplayName = "恶魔",
                Level = 1,
                MaxHp = 45
            });

            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "战士",
                    Level = 1,
                    Hp = 70,
                    MaxHp = 80
                },
                new()
                {
                    CharacterDefinitionId = "char_snake_queen",
                    DisplayName = "毒蛇女王",
                    Level = 1,
                    Hp = 55,
                    MaxHp = 55
                },
                new()
                {
                    CharacterDefinitionId = "char_ranger",
                    DisplayName = "恶魔",
                    Level = 1,
                    Hp = 40,
                    MaxHp = 45
                }
            };

            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, new List<string>(), battleSeed: 42, applyPartyHp: true, floor: 1);

            var slot2 = FindPlayerBySlot(config, FormationSlot.Middle);
            Assert.AreEqual("char_snake_queen", slot2.CharacterDefinitionId);
            Assert.AreEqual("毒蛇女王", slot2.DisplayName);
            Assert.AreEqual(55, slot2.StartHp);
        }

        static CombatantConfig FindPlayerBySlot(BattleConfig config, FormationSlot slot)
        {
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == TeamSide.Player && cc.Slot == slot)
                    return cc;
            }

            Assert.Fail($"Missing player slot {slot}");
            return null;
        }

        static CombatantConfig FindCombatant(BattleConfig config, TeamSide team)
        {
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == team)
                    return cc;
            }

            Assert.Fail($"Missing combatant for team {team}");
            return null;
        }
    }
}
