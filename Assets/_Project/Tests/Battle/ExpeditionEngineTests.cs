using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionEngineTests
    {
        [Test]
        public void StartRun_OpensRouteSelectWithGeneratedMap()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.NotNull(engine.Run.Map);
            Assert.Greater(engine.Run.PendingRoutes.Count, 0);
        }

        [Test]
        public void SkipVictoryOptionalRewards_AdvancesToRouteSelectWithoutTakingCard()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            engine.OnBattleFinished(BuildVictoryState());

            Assert.IsTrue(engine.TryClaimRewardGold());
            var hadCard = engine.Run.PendingRewardPickup?.HasCard == true;
            var bonusBefore = engine.Run.Party[0].BonusCards.Count;

            if (engine.Run.Phase == ExpeditionPhase.RewardPickup)
                Assert.IsTrue(engine.TrySkipVictoryOptionalRewards());

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);

            if (hadCard)
                Assert.AreEqual(bonusBefore, engine.Run.Party[0].BonusCards.Count);
        }

        [Test]
        public void SkipRewardGold_DoesNotAddGold()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            engine.OnBattleFinished(BuildVictoryState());

            var goldAmount = engine.Run.PendingRewardPickup?.Gold ?? 0;
            Assert.Greater(goldAmount, 0);
            Assert.IsTrue(engine.TrySkipRewardGold());
            Assert.AreEqual(0, engine.Run.Gold);

            if (engine.Run.PendingRewardPickup?.HasRelic == true)
                engine.TrySkipRewardRelic();
            if (engine.Run.PendingRewardPickup?.HasCard == true)
                engine.TrySkipRewardCard();

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
        }

        [Test]
        public void VictoryAfterFirstBattle_OpensRewardPickupThenRouteSelect()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = 25,
                MaxHp = 40
            });

            engine.OnBattleFinished(state);

            Assert.AreEqual(ExpeditionPhase.RewardPickup, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.BattlesWon);
            Assert.AreEqual(1, engine.Run.Map.NodesCompleted);
            Assert.AreEqual(25, engine.Run.Party[0].Hp);
            Assert.AreEqual(0, engine.Run.Gold);

            ResolveRewardPickup(engine);
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
        }

        [Test]
        public void SelectRoute_StartsNextBattleWithPartyHp()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = 18,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
            ResolveRewardPickup(engine);
            SelectFirstCombatRoute(engine);

            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            Assert.AreEqual(2, engine.CurrentBattleNumber);

            var next = engine.Run.CurrentBattleConfig;
            var player = FindPlayer(next, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void Victory_AwardsGoldInConfiguredRange()
        {
            var config = BuildConfig();
            config.RunSeed = 7;
            config.GoldMinPerVictory = 15;
            config.GoldMaxPerVictory = 25;

            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);

            Assert.GreaterOrEqual(engine.Run.LastGoldReward, 15);
            Assert.LessOrEqual(engine.Run.LastGoldReward, 25);
            Assert.AreEqual(0, engine.Run.Gold);

            engine.TryClaimRewardGold();
            Assert.AreEqual(engine.Run.LastGoldReward, engine.Run.Gold);
        }

        [Test]
        public void SelectRoute_PreservesPartyLevel()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 3,
                Hp = 18,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
            ResolveRewardPickup(engine);
            SelectFirstCombatRoute(engine);

            var player = FindPlayer(engine.Run.CurrentBattleConfig, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(62, player.MaxHp);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void BossRoute_WithoutBossEncounters_SpawnsOnlySkeletonKing()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            ResolveRewardPickup(engine);

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 20);
            ResolveRewardPickup(engine);

            SelectFirstBossRoute(engine);

            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            Assert.NotNull(engine.Run.CurrentBattleConfig);

            var enemies = 0;
            CombatantConfig king = null;
            foreach (var cc in engine.Run.CurrentBattleConfig.Combatants)
            {
                if (cc.Team != TeamSide.Enemy)
                    continue;

                enemies++;
                if (cc.CharacterDefinitionId == "char_skeleton_king")
                    king = cc;
            }

            Assert.AreEqual(1, enemies);
            Assert.NotNull(king);
            Assert.AreEqual("骷髅王", king.DisplayName);
            Assert.AreEqual(400, king.MaxHp);
            Assert.IsTrue(engine.Run.CurrentBattleConfig.SkipFloorScaling);
        }

        [Test]
        public void BossVictory_CompletesRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            ResolveRewardPickup(engine);

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 20);
            ResolveRewardPickup(engine);

            SelectFirstBossRoute(engine);
            CompleteVictory(engine, 12);
            ResolveRewardPickup(engine);

            Assert.AreEqual(ExpeditionPhase.RunComplete, engine.Run.Phase);
            Assert.AreEqual(3, engine.Run.BattlesWon);
            Assert.AreEqual(3, engine.Run.Map.NodesCompleted);
        }

        [Test]
        public void Defeat_EndsRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = new BattleState { Outcome = BattleOutcome.PlayerDefeat };
            engine.OnBattleFinished(state);

            Assert.AreEqual(ExpeditionPhase.RunFailed, engine.Run.Phase);
        }

        [Test]
        public void Victory_GrantsXpToParty()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);

            Assert.That(engine.Run.LastXpReward, Is.InRange(8, 10));
            Assert.That(engine.Run.Party[0].Xp, Is.InRange(8, 10));
        }

        [Test]
        public void TryAddRelic_AccumulatesForRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            Assert.IsTrue(engine.TryAddRelic(RelicIds.FlameSword));
            Assert.AreEqual(1, engine.Run.Relics.Count);
            Assert.IsFalse(engine.TryAddRelic(RelicIds.FlameSword));
        }

        [Test]
        public void StartRun_ClearsRelicsAndXp()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            engine.TryAddRelic(RelicIds.CatStatue);
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            engine.StartRun();

            Assert.AreEqual(0, engine.Run.Relics.Count);
            Assert.AreEqual(0, engine.Run.LastXpReward);
        }

        [Test]
        public void TreasureRoute_OpensRewardPickupPhase()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Treasure,
                LayerNumber = 1,
                MapOptionIndex = 0,
                DisplayName = "宝箱",
                Description = "测试宝箱"
            });

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.RewardPickup, engine.Run.Phase);
            Assert.NotNull(engine.Run.PendingRewardPickup);
            Assert.AreEqual(RewardPickupKind.Chest, engine.Run.PendingRewardPickup.Kind);
        }

        [Test]
        public void EventChoice_ResolvesAndReturnsToRouteSelect()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Event,
                EventId = Expedition.Events.ExpeditionEventIds.TrainingDummy,
                LayerNumber = 1,
                MapOptionIndex = 0,
                DisplayName = "训练人偶",
                Description = "事件"
            });

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.EventChoice, engine.Run.Phase);
            Assert.IsTrue(engine.TryResolveEventChoice(2));
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.Map.NodesCompleted);
        }

        static void CompleteVictory(ExpeditionEngine engine, int hp)
        {
            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = hp,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
        }

        static void ResolveRewardPickup(ExpeditionEngine engine)
        {
            engine.TryClaimRewardGold();
            if (engine.Run.PendingRewardPickup?.HasRelic == true)
                engine.TryClaimRewardRelic();
            if (engine.Run.PendingRewardPickup?.HasCard == true)
                engine.TryClaimRewardCard();
            if (engine.Run.PendingRewardPickup?.HasConsumable == true)
                engine.TryClaimRewardConsumable();
        }

        static void SelectFirstCombatRoute(ExpeditionEngine engine)
        {
            for (var i = 0; i < engine.Run.PendingRoutes.Count; i++)
            {
                var route = engine.Run.PendingRoutes[i];
                if (route.NodeType is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite or ExpeditionNodeType.Boss)
                {
                    engine.TrySelectRoute(i);
                    return;
                }
            }

            engine.TrySelectRoute(0);
        }

        static void SelectFirstBossRoute(ExpeditionEngine engine)
        {
            for (var i = 0; i < engine.Run.PendingRoutes.Count; i++)
            {
                if (engine.Run.PendingRoutes[i].NodeType == ExpeditionNodeType.Boss)
                {
                    engine.TrySelectRoute(i);
                    return;
                }
            }

            SelectFirstCombatRoute(engine);
        }

        static BattleState BuildVictoryState()
        {
            return new BattleState { Outcome = BattleOutcome.PlayerVictory };
        }

        [Test]
        public void CampDeck_FromRoster_ReplacesDefaultBattleDeck()
        {
            var config = BuildConfigWithKnightDeck(4);
            var roster = new CampRosterState();
            var member = new CampMemberLoadout
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士"
            };
            for (var i = 0; i < CampRosterState.DeckSize; i++)
                member.DeckCardIds.Add($"camp_card_{i}");
            roster.Members.Add(member);

            var engine = new ExpeditionEngine(config);
            engine.StartRun(roster);
            var snap = engine.Run.Party[0];

            Assert.IsTrue(snap.UsesCampDeckAsBattleBase);
            Assert.AreEqual(CampRosterState.DeckSize, ExpeditionRunDeckRules.CountMemberDeck(config, snap));
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, snap))
                StringAssert.StartsWith("camp_card_", entry.Template.DefinitionId);
        }

        [Test]
        public void GrantCardReward_WhenDeckFull_SetsPendingOffer()
        {
            var config = BuildConfigWithKnightDeck(10);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            var member = engine.Run.Party[0];

            Assert.AreEqual(10, ExpeditionRunDeckRules.CountMemberDeck(config, member));
            engine.Run.Phase = ExpeditionPhase.RewardPickup;
            engine.Run.PendingRewardPickup = new ExpeditionRewardPickup
            {
                CardDefinitionId = "extra_card",
                CardOwnerCharacterId = "char_knight",
                CardDisplayName = "额外卡"
            };

            Assert.IsTrue(engine.TryClaimRewardCard());
            Assert.NotNull(engine.Run.PendingCardOffer);
            Assert.AreEqual(10, ExpeditionRunDeckRules.CountMemberDeck(config, member));
        }

        [Test]
        public void CardAltar_ExtractsCollectionCardIntoDeck()
        {
            var config = BuildConfigWithKnightDeck(2);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            AttachCampCollection(engine.Run, "char_knight", "altar_card_a", "altar_card_b");

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Shrine,
                LayerNumber = 1,
                MapOptionIndex = 0
            });
            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.ShrineChoice, engine.Run.Phase);

            engine.SetCardAltarMemberDraft("char_knight", 0, "");
            Assert.IsTrue(engine.TryConfirmCardAltar());
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.AreEqual(3, ExpeditionRunDeckRules.CountMemberDeck(config, engine.Run.Party[0]));
            Assert.IsTrue(engine.Run.Party[0].ExtractedCampCardIndices.Contains(0));
            Assert.IsTrue(CampCollectionProgress.IsExtracted(engine.Run, "char_knight", 0));
        }

        [Test]
        public void CardAltar_ConfirmAppliesAllMemberDrafts()
        {
            var config = BuildConfigWithThreePlayerDecks(2);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            AttachCampCollection(engine.Run, "char_knight", "altar_knight");
            for (var i = 1; i < CampRosterState.DeckSize; i++)
                engine.Run.Party[0].CampDeckCardIds.Add($"char_knight_camp_{i}");
            engine.Run.RunStartCampDecks["char_knight"] = new List<string>(engine.Run.Party[0].CampDeckCardIds);
            AttachCampCollection(engine.Run, "char_mage", "altar_mage");
            for (var i = 1; i < CampRosterState.DeckSize; i++)
                engine.Run.Party[1].CampDeckCardIds.Add($"char_mage_camp_{i}");
            engine.Run.RunStartCampDecks["char_mage"] = new List<string>(engine.Run.Party[1].CampDeckCardIds);
            AttachCampCollection(engine.Run, "char_ranger", "altar_ranger");
            for (var i = 1; i < CampRosterState.DeckSize; i++)
                engine.Run.Party[2].CampDeckCardIds.Add($"char_ranger_camp_{i}");
            engine.Run.RunStartCampDecks["char_ranger"] = new List<string>(engine.Run.Party[2].CampDeckCardIds);

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Shrine,
                LayerNumber = 1,
                MapOptionIndex = 0
            });
            Assert.IsTrue(engine.TrySelectRoute(0));

            engine.SetCardAltarMemberDraft("char_knight", 0, "");
            engine.SetCardAltarMemberDraft("char_mage", 0, "");
            engine.SetCardAltarMemberDraft("char_ranger", 0, "");
            Assert.IsTrue(engine.TryConfirmCardAltar());

            foreach (var memberId in new[] { "char_knight", "char_mage", "char_ranger" })
            {
                Assert.IsTrue(CampCollectionProgress.IsExtracted(engine.Run, memberId, 0), memberId);
                Assert.AreEqual(
                    CampRosterState.DeckSize - 1,
                    ExpeditionRunDeckRules.GetAvailableCollectionIndices(
                        engine.Run,
                        FindMember(engine.Run.Party, memberId)).Count);
            }
        }

        [Test]
        public void CardAltar_AfterBattle_BonusCardsPersist()
        {
            var config = BuildConfigWithKnightDeck(2);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            AttachCampCollection(engine.Run, "char_knight", "altar_card_a", "altar_card_b");

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Shrine,
                LayerNumber = 1,
                MapOptionIndex = 0
            });
            engine.TrySelectRoute(0);
            engine.SetCardAltarMemberDraft("char_knight", 0, "");
            engine.TryConfirmCardAltar();
            Assert.AreEqual(3, ExpeditionRunDeckRules.CountMemberDeck(config, engine.Run.Party[0]));

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
                        Hp = 30,
                        MaxHp = 40
                    }
                }
            };
            engine.OnBattleFinished(state);

            Assert.AreEqual(3, ExpeditionRunDeckRules.CountMemberDeck(config, engine.Run.Party[0]));
            Assert.IsTrue(CampCollectionProgress.IsExtracted(engine.Run, "char_knight", 0));
        }

        [Test]
        public void OnBattleFinished_PreservesBonusCardsWhenReusingPartyList()
        {
            var config = BuildConfigWithKnightDeck(2);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

            engine.Run.Party[0].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "altar_card_a",
                DisplayName = "祭坛卡",
                OwnerCharacterId = "char_knight"
            });
            Assert.AreEqual(3, ExpeditionRunDeckRules.CountMemberDeck(config, engine.Run.Party[0]));

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
                        Hp = 28,
                        MaxHp = 40
                    }
                }
            };

            engine.OnBattleFinished(state);

            Assert.AreEqual(1, engine.Run.Party[0].BonusCards.Count);
            Assert.AreEqual("altar_card_a", engine.Run.Party[0].BonusCards[0].DefinitionId);
            Assert.AreEqual(3, ExpeditionRunDeckRules.CountMemberDeck(config, engine.Run.Party[0]));
        }

        [Test]
        public void ConsumableOffer_DoesNotClearExtractedCampCollection()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            CampCollectionProgress.MarkExtracted(engine.Run, "char_knight", 3);

            engine.Run.Phase = ExpeditionPhase.RewardPickup;
            engine.Run.PendingRewardPickup = new ExpeditionRewardPickup
            {
                ConsumableId = "potion_heal"
            };
            engine.Run.PendingConsumableOfferId = "potion_heal";

            Assert.IsTrue(engine.TryAbandonConsumableOffer());
            Assert.IsTrue(CampCollectionProgress.IsExtracted(engine.Run, "char_knight", 3));
        }

        static void AttachCampCollection(ExpeditionRunState run, string memberId, params string[] cardIds)
        {
            var member = FindMember(run.Party, memberId);
            member.CampDeckCardIds.Clear();
            foreach (var id in cardIds)
                member.CampDeckCardIds.Add(id);
            run.RunStartCampDecks[memberId] = new List<string>(member.CampDeckCardIds);
        }

        static CampRosterState BuildThreeMemberCampRoster()
        {
            var roster = new CampRosterState();
            foreach (var pair in new[]
                     {
                         ("char_knight", "骑士", "altar_knight"),
                         ("char_mage", "法师", "altar_mage"),
                         ("char_ranger", "游侠", "altar_ranger")
                     })
            {
                var loadout = new CampMemberLoadout
                {
                    CharacterDefinitionId = pair.Item1,
                    DisplayName = pair.Item2
                };
                loadout.DeckCardIds.Add(pair.Item3);
                for (var i = 1; i < CampRosterState.DeckSize; i++)
                    loadout.DeckCardIds.Add($"{pair.Item1}_camp_{i}");
                roster.Members.Add(loadout);
            }

            return roster;
        }

        static PartyMemberSnapshot FindMember(IReadOnlyList<PartyMemberSnapshot> party, string memberId)
        {
            foreach (var member in party)
            {
                if (member?.CharacterDefinitionId == memberId)
                    return member;
            }

            Assert.Fail($"Missing party member {memberId}");
            return null;
        }

        static ExpeditionConfig BuildConfigWithThreePlayerDecks(int cardCount)
        {
            var config = BuildConfigWithKnightDeck(cardCount);
            var encounter = config.CombatEncounters[0];

            foreach (var pair in new[]
                     {
                         ("char_mage", "法师"),
                         ("char_ranger", "游侠")
                     })
            {
                var cc = new CombatantConfig
                {
                    Team = TeamSide.Player,
                    CharacterDefinitionId = pair.Item1,
                    DisplayName = pair.Item2,
                    MaxHp = 35
                };
                for (var i = 0; i < cardCount; i++)
                {
                    cc.DeckTemplates.Add(new CardTemplate
                    {
                        DefinitionId = $"{pair.Item1}_base_{i}",
                        DisplayName = $"{pair.Item2}基础{i}",
                        OwnerCharacterId = pair.Item1
                    });
                }

                encounter.Combatants.Add(cc);

                config.PlayerCardCatalog.Add(new CardTemplate
                {
                    DefinitionId = pair.Item1 == "char_mage" ? "altar_mage" : "altar_ranger",
                    DisplayName = pair.Item1 == "char_mage" ? "祭坛法师卡" : "祭坛游侠卡",
                    OwnerCharacterId = pair.Item1
                });
            }

            return config;
        }

        [Test]
        public void TryRollCardReward_OnlyPicksCardsOwnedByRewardRecipient()
        {
            var config = BuildConfig();
            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "knight_only",
                DisplayName = "骑士专属",
                OwnerCharacterId = "char_knight",
                CardType = CardType.Attack
            });
            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "ranger_only",
                DisplayName = "游侠专属",
                OwnerCharacterId = "char_ranger",
                CardType = CardType.Attack
            });
            CardRarityTable.Register("knight_only", CardRarity.Rare);
            CardRarityTable.Register("ranger_only", CardRarity.Rare);

            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot
            {
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士"
            });

            var rng = new Grimhand.Core.BattleRng(42);
            for (var i = 0; i < 20; i++)
            {
                Assert.IsTrue(ExpeditionCardPool.TryRollCardReward(
                    config, run, CardRarity.Rare, rng, out var picked, out var owner));
                Assert.AreEqual("char_knight", owner.CharacterDefinitionId);
                Assert.AreEqual("char_knight", picked.OwnerCharacterId);
                Assert.AreEqual("knight_only", picked.DefinitionId);
            }
        }

        static ExpeditionConfig BuildConfigWithKnightDeck(int cardCount)
        {
            var config = BuildConfig();
            var knight = FindPlayer(config.CombatEncounters[0], "char_knight");
            knight.DeckTemplates.Clear();
            for (var i = 0; i < cardCount; i++)
            {
                knight.DeckTemplates.Add(new CardTemplate
                {
                    DefinitionId = $"base_{i}",
                    DisplayName = $"基础{i}",
                    OwnerCharacterId = "char_knight"
                });
            }

            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "altar_card_a",
                DisplayName = "祭坛卡A",
                OwnerCharacterId = "char_knight"
            });
            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "altar_card_b",
                DisplayName = "祭坛卡B",
                OwnerCharacterId = "char_knight"
            });
            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "extra_card",
                DisplayName = "额外卡",
                OwnerCharacterId = "char_knight"
            });

            return config;
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig
            {
                RunSeed = 1,
                ChapterLayerCount = 3,
                TargetBattleCount = 2
            };

            var encounter = new BattleConfig();
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                MaxHp = 40
            });
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_goblin",
                DisplayName = "哥布林",
                MaxHp = 20
            });

            config.CombatEncounters.Add(encounter);
            return config;
        }

        static CombatantConfig FindPlayer(BattleConfig config, string characterId)
        {
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == TeamSide.Player && cc.CharacterDefinitionId == characterId)
                    return cc;
            }

            return null;
        }

        [Test]
        public void CardUpgradeLevels_AreTrackedPerDeckInstance()
        {
            var member = new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" };
            var first = "char_knight|first";
            var second = "char_knight|second";

            Assert.IsTrue(CardUpgradeRules.TryUpgradeLevel(member, first, "基础斩击", 1));
            Assert.AreEqual(1, CardUpgradeRules.GetLevel(member, first));
            Assert.AreEqual(0, CardUpgradeRules.GetLevel(member, second));

            Assert.IsTrue(CardUpgradeRules.TryUpgradeLevel(member, second, "基础斩击", 1));
            Assert.AreEqual(1, CardUpgradeRules.GetLevel(member, first));
            Assert.AreEqual(1, CardUpgradeRules.GetLevel(member, second));
        }
    }
}
