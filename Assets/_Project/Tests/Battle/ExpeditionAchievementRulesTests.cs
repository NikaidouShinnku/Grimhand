using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionAchievementRulesTests
    {
        sealed class Profile : IExpeditionMetaProfile, IAchievementProfile
        {
            public int AccountGold { get; set; }
            public CampCollectionState Collection { get; } = new();
            public int CollectionCapacity => 100;
            public AchievementProgressState Achievements { get; } = new();
        }

        static BattleState Battle(int players=3,int survivors=3)
        {
            var state=new BattleState { Outcome=BattleOutcome.PlayerVictory };
            for(var i=0;i<players;i++)
            {
                var id="hero_"+i;
                var hero=AchievementRules.OriginalSix[i];
                state.Config.Combatants.Add(new CombatantConfig { Id=id, CharacterDefinitionId=hero, Team=TeamSide.Player,MaxHp=10 });
                state.Combatants.Add(new CombatantState { Id=id, CharacterDefinitionId=hero, Team=TeamSide.Player, Hp=i<survivors?10:0 });
            }
            state.Combatants.Add(new CombatantState { Id="boss", CharacterDefinitionId="char_skeleton_king", Team=TeamSide.Enemy });
            return state;
        }

        [Test]
        public void TelemetryUnlocksImmediatelyButAllRespondRequiresVictory()
        {
            var run=new ExpeditionRunState();var profile=new Profile();var state=Battle();
            state.Outcome=BattleOutcome.Ongoing;
            var t=state.AchievementTelemetry;
            t.ThreeResponsesInTurn=t.ThreePoisonKills=t.PoisonBurnKill=t.ThreeKillsOneAttack=t.FrontBackKillsInTurn=t.RetainedThenUsed=true;
            foreach(var hero in state.Config.Combatants)t.RespondedPlayerIds.Add(hero.Id);
            AchievementRules.OnBattleProgress(run,profile,state);
            Assert.AreEqual(6,profile.Achievements.Unlocked.Count);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_ALL_RESPOND"));
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_FIRST_VICTORY"));
            state.Outcome=BattleOutcome.PlayerVictory;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_ALL_RESPOND"));
        }

        [TestCase(20,true,true)]
        [TestCase(40,true,false)]
        [TestCase(20,false,false)]
        public void BossIdentityMustMatchFloorAndBossContext(int floor,bool boss,bool expected)
        {
            var profile=new Profile();var run=new ExpeditionRunState { LastBattleFloor=floor,LastBattleWasBoss=boss,AchievementBattleStartConsumableCount=3 };
            AchievementRules.OnBattleFinished(run,profile,Battle());
            Assert.AreEqual(expected,profile.Achievements.Unlocked.Contains("ACH_DEFEAT_SKELETON_KING"));
            Assert.AreEqual(boss,profile.Achievements.Unlocked.Contains("ACH_BOSS_ALL_ALIVE"));
            Assert.AreEqual(boss,profile.Achievements.Unlocked.Contains("ACH_BOSS_NO_CONSUMABLES"));
        }

        [TestCase(1,1,false)]
        [TestCase(2,1,true)]
        [TestCase(3,1,true)]
        [TestCase(3,2,false)]
        public void LastSurvivorNeedsAtLeastTwoStartingHeroes(int initial,int survivors,bool expected)
        {
            var profile=new Profile();
            var state=Battle(initial,survivors);
            state.Combatants.Add(new CombatantState { Id="summon_hero",CharacterDefinitionId="char_knight",Team=TeamSide.Player,Hp=10 });
            AchievementRules.OnBattleFinished(new ExpeditionRunState { LastBattleWasBoss=true },profile,state);
            Assert.AreEqual(expected,profile.Achievements.Unlocked.Contains("ACH_LAST_SURVIVOR"));
        }

        [TestCase(true,false,false)]
        [TestCase(false,true,false)]
        [TestCase(false,false,true)]
        public void IneligibleRunsCannotEarnProgress(bool training,bool tutorial,bool disabled)
        {
            var run=new ExpeditionRunState { IsTrainingGround=training,IsTutorialRun=tutorial,AchievementsDisabled=disabled,AchievementRelicEvolved=true,Phase=ExpeditionPhase.RunComplete };
            var profile=new Profile();var state=Battle();state.AchievementTelemetry.ThreeResponsesInTurn=true;
            AchievementRules.OnBattleFinished(run,profile,state);
            AchievementRules.OnRunEnded(run,profile);
            AchievementRules.CheckRunProgress(run,profile);
            AchievementRules.OnEngraved(run,profile,"orange");
            Assert.IsEmpty(profile.Achievements.Unlocked);
            Assert.AreEqual(0,profile.Achievements.CompletedEngravings);
        }

        [Test]
        public void EngravingIsCountedOncePerCompletedInstance()
        {
            const string card="achievement_test_orange";
            CardRarityTable.Register(card,CardRarity.Legendary);
            var run=new ExpeditionRunState();var profile=new Profile();
            profile.Achievements.CompletedEngravings=4;
            Assert.IsTrue(CardEngravingRules.TryCompleteEngraveToCollection(run,profile,card,"instance",out _));
            Assert.IsFalse(CardEngravingRules.TryCompleteEngraveToCollection(run,profile,card,"instance",out _));
            Assert.AreEqual(1,profile.Collection.Count);
            Assert.AreEqual(5,profile.Achievements.CompletedEngravings);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_FIVE_ENGRAVINGS"));
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_ORANGE_ENGRAVING"));
        }

        [Test]
        public void PermanentProgressRequiresDistinctCardsAndAllFixedSixHeroes()
        {
            var progress=new AchievementProgressState();var meta=CampMetaState.CreateNewProfile();var collection=new CampCollectionState();
            for(var i=0;i<30;i++)collection.TryAddEntry("same");
            AchievementRules.ReconcilePermanent(progress,meta,collection);
            Assert.IsEmpty(progress.Unlocked);
            for(var i=0;i<30;i++)
            {
                var id="achievement_distinct_"+i;
                collection.TryAddEntry(id);
                if(i<5)CardRarityTable.Register(id,CardRarity.Legendary);
            }
            meta.GetOrCreate("future_hero").OutOfRunLevel=10;
            foreach(var id in AchievementRules.OriginalSix)meta.GetOrCreate(id).OutOfRunLevel=10;
            AchievementRules.ReconcilePermanent(progress,meta,collection);
            Assert.IsTrue(progress.Unlocked.Contains("ACH_COLLECTION_30"));
            Assert.IsTrue(progress.Unlocked.Contains("ACH_COLLECTION_FIVE_ORANGE"));
            Assert.IsTrue(progress.Unlocked.Contains("ACH_ALL_CHARACTERS_MAX"));
        }

        [Test]
        public void AbandonedRunCannotUnlockFinishAndCopyKeepsEligibility()
        {
            var profile=new Profile();var run=new ExpeditionRunState { Phase=ExpeditionPhase.RunFailed,WasAbandoned=true,AchievementsDisabled=true,AchievementRelicEvolved=true,AchievementBattleStartConsumableCount=3 };
            var copy=ExpeditionRunStateCopy.Clone(run);
            Assert.IsTrue(copy.WasAbandoned && copy.AchievementsDisabled && copy.AchievementRelicEvolved);
            Assert.AreEqual(3,copy.AchievementBattleStartConsumableCount);
            copy.AchievementsDisabled=false;
            AchievementRules.OnRunEnded(copy,profile);Assert.IsEmpty(profile.Achievements.Unlocked);
            copy.WasAbandoned=false;
            AchievementRules.OnRunEnded(copy,profile);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_FINISH_EXPEDITION"));
        }

        [Test]
        public void RelicEvolutionFlagIsCheckedAndBaseDeckMaxUpgradeUsesRealCatalog()
        {
            var run=new ExpeditionRunState();var profile=new Profile();
            Assert.IsFalse(RelicGrowthRules.TryEvolveRelic(run,RelicIds.Bonfire,RelicIds.FlameSword));
            Assert.IsFalse(run.AchievementRelicEvolved);
            run.Relics.Add(RelicIds.Bonfire);
            Assert.IsTrue(RelicGrowthRules.TryEvolveRelic(run,RelicIds.Bonfire,RelicIds.FlameSword));
            AchievementRules.CheckRunProgress(run,profile);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_RELIC_EVOLUTION"));

            var config=new ExpeditionConfig();var encounter=new BattleConfig();
            var hero=new CombatantConfig { Id="hero",CharacterDefinitionId="char_berserker",Team=TeamSide.Player };
            hero.DeckTemplates.Add(new CardTemplate { DefinitionId="b_axe_cut",DisplayName="裂斧斩",OwnerCharacterId="char_berserker" });
            encounter.Combatants.Add(hero);config.CombatEncounters.Add(encounter);
            var member=new PartyMemberSnapshot { CharacterDefinitionId="char_berserker" };
            member.BaseDeckInstanceIds.Add("base-card");
            run.Party.Add(member);
            member.CardUpgradeLevels["base-card"]=CardUpgradeRules.GetMaxLevel("裂斧斩")-1;
            AchievementRules.CheckRunProgress(run,profile,config);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_CARD_MAX_UPGRADE"));
            member.CardUpgradeLevels["base-card"]++;
            AchievementRules.CheckRunProgress(run,profile,config);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_CARD_MAX_UPGRADE"));
        }

        [TestCase(2,false,false)]
        [TestCase(3,true,false)]
        [TestCase(3,false,true)]
        public void BossConsumablesRequireThreeAtStartAndNoneUsed(int count,bool used,bool expected)
        {
            var run=new ExpeditionRunState { LastBattleWasBoss=true,AchievementBattleStartConsumableCount=count };
            var profile=new Profile();var state=Battle();state.ConsumableUsedThisBattle=used;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.AreEqual(expected,profile.Achievements.Unlocked.Contains("ACH_BOSS_NO_CONSUMABLES"));
        }

        [Test]
        public void LastSurvivorIsBossOnlyAndZeroStartHpDoesNotCountButRevivedOneHpDoes()
        {
            var profile=new Profile();var run=new ExpeditionRunState();var state=Battle(2,1);
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_LAST_SURVIVOR"));
            run.LastBattleWasBoss=true;
            state.Config.Combatants[1].StartHp=0;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_LAST_SURVIVOR"));
            state.Config.Combatants[1].StartHp=1;
            state.Config.Combatants[1].EnteredFromExpeditionDeath=true;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_LAST_SURVIVOR"));
        }

        [Test]
        public void RetainedOrangeEngravingBackfillsAchievementWithoutIncrementingCounter()
        {
            const string id="achievement_legacy_orange";
            CardRarityTable.Register(id,CardRarity.Legendary);
            var collection=new CampCollectionState();
            collection.TryAddEntry(id,false);
            var progress=new AchievementProgressState { CompletedEngravings=2 };
            AchievementRules.ReconcilePermanent(progress,null,collection);
            Assert.IsFalse(progress.Unlocked.Contains("ACH_ORANGE_ENGRAVING"));
            collection.EngravedFlags[0]=true;
            AchievementRules.ReconcilePermanent(progress,null,collection);
            AchievementRules.ReconcilePermanent(progress,null,collection);
            Assert.IsTrue(progress.Unlocked.Contains("ACH_ORANGE_ENGRAVING"));
            Assert.AreEqual(2,progress.CompletedEngravings);
        }

        [Test]
        public void ForcedVictoryWithLivingBossDoesNotAwardBossDefeat()
        {
            var run=new ExpeditionRunState { LastBattleFloor=20,LastBattleWasBoss=true };
            var profile=new Profile();var state=Battle();
            state.Combatants[state.Combatants.Count-1].Hp=10;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsFalse(profile.Achievements.Unlocked.Contains("ACH_DEFEAT_SKELETON_KING"));
            state.Combatants[state.Combatants.Count-1].Hp=0;
            AchievementRules.OnBattleFinished(run,profile,state);
            Assert.IsTrue(profile.Achievements.Unlocked.Contains("ACH_DEFEAT_SKELETON_KING"));
        }
    }
}
