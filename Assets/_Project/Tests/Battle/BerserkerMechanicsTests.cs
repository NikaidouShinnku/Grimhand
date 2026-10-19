using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class BerserkerMechanicsTests
    {
        static BattleState State()
        {
            var s=new BattleState{Phase=TurnPhase.Planning,EnergyCurrent=10};
            s.Config.Talents.ActiveTalentIds.Add("talent_berserker_s1_lv7");
            s.Combatants.Add(new CombatantState{Id="hero",CharacterDefinitionId="char_berserker",DisplayName="裂斧",Team=TeamSide.Player,Slot=FormationSlot.Front,MaxHp=76,Hp=76,Speed=5});
            s.Combatants.Add(new CombatantState{Id="ally",CharacterDefinitionId="char_knight",Team=TeamSide.Player,Slot=FormationSlot.Middle,MaxHp=80,Hp=80});
            foreach(var i in Enumerable.Range(1,3))s.Combatants.Add(new CombatantState{Id="enemy"+i,Team=TeamSide.Enemy,Slot=(FormationSlot)i,MaxHp=1000,Hp=1000});
            return s;
        }
        static CardInstanceState Card(BattleState s,string id)
        {
            var t=BerserkerCardActions.Create(id);var c=new CardInstanceState{InstanceId=s.NextCardInstanceId++,DefinitionId=id,DisplayName=t.DisplayName,OwnerCharacterId=t.OwnerCharacterId,OwnerCombatantId="hero",Cost=t.Cost,BaseCost=t.Cost,CardType=t.CardType};
            c.Keywords.AddRange(t.Keywords);c.Actions.AddRange(t.Actions);s.CardsById.Add(c.InstanceId,c);s.PlayerHand.Add(c);s.ResolutionTargets[c.InstanceId]="enemy1";return c;
        }
        static void Resolve(BattleState s,CardInstanceState card,List<BattleEvent> events=null)
        {
            events??=new();var owner=s.GetCombatant("hero");var rng=new BattleRng(1);var prepared=BerserkerRules.Prepare(s,owner,card,events);
            if(SpecialCardRules.IsSpecialCard(prepared))SpecialCardRules.TryResolve(s,owner,prepared,events,rng);else EffectActionExecutor.ExecuteAll(s,owner,prepared,events,rng);
            BerserkerRules.After(s,owner,prepared,events,rng);
        }
        [Test] public void CatalogHas32UniqueAllowedCardsAndNoMissingUpgradeDefinitions()
        {
            Assert.AreEqual(32,BerserkerCardCatalog.All.Count);Assert.AreEqual(32,BerserkerCardCatalog.All.Select(c=>c.Id).Distinct().Count());
            foreach(var c in BerserkerCardCatalog.All)
            {Assert.IsTrue(PlayerCardCatalogRules.IsAllowedPlayerCard(c.Id,"char_berserker"),c.Id);Assert.IsTrue(CardUpgradeCatalog.TryGetByDisplayName(c.Name,out _),c.Name);Assert.IsNotEmpty(c.Description);Assert.IsNotNull(BerserkerCardActions.Create(c.Id));}
            Assert.AreEqual(8,TalentCatalog.GetForCharacter("char_berserker").Count);Assert.AreEqual(190,CharacterProgression.GetStatsForCharacter("char_berserker",20).MaxHp);
        }
        [TestCase("b_axe_cut",8)][TestCase("b_counter_cut",14)][TestCase("b_axe_feint",5)]
        [TestCase("b_sweeping_edge",7)][TestCase("b_axe_crush",12)][TestCase("b_short_hack",7)]
        [TestCase("b_split_helm",24)][TestCase("b_two_hacks",12)][TestCase("b_rear_cleave",13)]
        [TestCase("b_bloodied_edge",16)][TestCase("b_axe_execute",20)][TestCase("b_whirlwind",12)][TestCase("b_break_line",19)]
        public void BaseAttacksMatchCardFace(string id,int damage)
        {var s=State();Resolve(s,Card(s,id));Assert.AreEqual(1000-damage,s.GetCombatant("enemy1").Hp,id);}
        [TestCase("b_iron_guard",10)][TestCase("b_stand_firm",8)][TestCase("b_measured_guard",9)][TestCase("b_iron_breath",17)][TestCase("b_last_reserve",14)]
        public void BaseArmorMatchesCardFace(string id,int armor)
        {var s=State();Resolve(s,Card(s,id));Assert.AreEqual(armor,s.GetCombatant("hero").Block);}
        [Test] public void RageIsSpentOnceForAllAoeTargetsAndAllHits()
        {
            var s=State();var b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=4;Resolve(s,Card(s,"b_whirlwind"));
            Assert.AreEqual(0,b.Rage);foreach(var e in s.GetTeam(TeamSide.Enemy))Assert.AreEqual(984,e.Hp);
            s=State();b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=4;Resolve(s,Card(s,"b_two_hacks"));Assert.AreEqual(2,b.Rage);Assert.AreEqual(980,s.GetCombatant("enemy1").Hp);
        }
        [Test] public void StrongStrikeAndSplashUseTheirSeparatePrintedValues()
        {
            var s=State();var b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=4;Resolve(s,Card(s,"b_split_helm"));Assert.AreEqual(961,s.GetCombatant("enemy1").Hp);Assert.AreEqual(1,b.Rage);
            s=State();b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=2;Resolve(s,Card(s,"b_rear_cleave"));Assert.AreEqual(983,s.GetCombatant("enemy1").Hp);Assert.AreEqual(989,s.GetCombatant("enemy2").Hp);
        }
        static CardInstanceState EnemyAttack(BattleState s)
        {var c=new CardInstanceState{InstanceId=s.NextCardInstanceId++,CardType=CardType.Attack,OwnerCombatantId="enemy1"};s.CardsById.Add(c.InstanceId,c);return c;}
        [Test] public void EquippedRageTalentCountsEnemyCardsNotHitsAndRespectsTwoPointLimit()
        {
            var s=State();var hero=s.GetCombatant("hero");var enemy=s.GetCombatant("enemy1");var ev=new List<BattleEvent>();var c=EnemyAttack(s);
            for(var i=0;i<3;i++)DamageRules.ApplyDamage(s,enemy,hero,1,CardType.Attack,ev,sourceCardInstanceId:c.InstanceId);
            Assert.AreEqual(1,BerserkerRules.Get(s,hero).Rage);
            for(var i=0;i<3;i++)DamageRules.ApplyDamage(s,enemy,hero,1,CardType.Attack,ev,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            Assert.AreEqual(2,BerserkerRules.Get(s,hero).Rage);
        }
        [Test] public void ArmorPeriodicSacrificeAndFriendlyDamageDoNotGenerateRage()
        {
            var s=State();var hero=s.GetCombatant("hero");var ev=new List<BattleEvent>();hero.Block=100;
            DamageRules.ApplyDamage(s,s.GetCombatant("enemy1"),hero,5,CardType.Attack,ev,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            hero.Block=0;DamageRules.ApplyPeriodicDamage(s,hero,5,ev,"burn");
            DamageRules.ApplyDamage(s,s.GetCombatant("ally"),hero,5,CardType.Attack,ev);
            DamageRules.ApplyDamage(s,s.GetCombatant("enemy1"),hero,5,CardType.Attack,ev,isSacrificeDamage:true,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            Assert.AreEqual(0,BerserkerRules.Get(s,hero).Rage);
        }
        [Test] public void NoSlotOneTalentMeansNoInnateRageOrResourceIcon()
        {
            var s=State();s.Config.Talents.ActiveTalentIds.Clear();
            var hero=s.GetCombatant("hero");var ev=new List<BattleEvent>();
            BerserkerRules.TurnStart(s,ev);
            DamageRules.ApplyDamage(s,s.GetCombatant("enemy1"),hero,2,CardType.Attack,ev,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            Assert.AreEqual(0,BerserkerRules.Get(s,hero).Rage);
            Assert.IsFalse(FootStatusIconAggregator.Aggregate(hero,s).Any(e=>e.StatusId=="berserker_rage"));
            Resolve(s,Card(s,"b_berserker_soul"));
            Assert.IsTrue(s.BerserkerSoulOwners.Contains(hero.Id), "Card effects must not require a rage talent.");
        }
        [TestCase(1)][TestCase(3)][TestCase(7)][TestCase(9)]
        public void EverySlotOneTalentGrantsAndExplainsBaseRage(int level)
        {
            var s=State();s.Config.Talents.ActiveTalentIds.Clear();
            var id=$"talent_berserker_s1_lv{level}";s.Config.Talents.ActiveTalentIds.Add(id);
            var hero=s.GetCombatant("hero");var ev=new List<BattleEvent>();
            DamageRules.ApplyDamage(s,s.GetCombatant("enemy1"),hero,2,CardType.Attack,ev,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            Assert.AreEqual(1,BerserkerRules.Get(s,hero).Rage);
            StringAssert.Contains(TalentCatalog.BerserkerRageDescription,TalentCatalog.Get(id).Description);
            StringAssert.Contains("额外效果",TalentCatalog.Get(id).Description);
        }
        [Test] public void EtherealOneHpStillProducesRageAndStillCapsStrongAttack()
        {
            var s=State();var hero=s.GetCombatant("hero");var ev=new List<BattleEvent>();StatusRules.ApplyStatus(s,hero,StatusCatalog.Ethereal,1,1,ev);
            DamageRules.ApplyDamage(s,s.GetCombatant("enemy1"),hero,30,CardType.Attack,ev,sourceCardInstanceId:EnemyAttack(s).InstanceId);
            Assert.AreEqual(75,hero.Hp);Assert.AreEqual(1,BerserkerRules.Get(s,hero).Rage);
            StatusRules.ApplyStatus(s,s.GetCombatant("enemy1"),StatusCatalog.Ethereal,1,1,ev);BerserkerRules.Get(s,hero).Rage=4;Resolve(s,Card(s,"b_split_helm"));Assert.AreEqual(999,s.GetCombatant("enemy1").Hp);
        }
        [Test] public void DecayStartsOnSecondTurnAndBannerOnlyActivatesNextTurn()
        {
            var s=State();var hero=s.GetCombatant("hero");var ev=new List<BattleEvent>();BerserkerRules.TurnStart(s,ev);var b=BerserkerRules.Get(s,hero);b.Rage=4;
            Resolve(s,Card(s,"b_burning_standard"));Assert.IsFalse(BerserkerRules.BannerActive(s,b));s.TurnNumber=2;BerserkerRules.TurnStart(s,ev);Assert.AreEqual(4,b.Rage);Assert.IsTrue(BerserkerRules.BannerActive(s,b));
            Resolve(s,Card(s,"b_split_helm"));Assert.AreEqual(976,s.GetCombatant("enemy1").Hp);Assert.AreEqual(4,b.Rage);
            Assert.IsFalse(BerserkerRules.CanPlay(s,hero,Card(s,"b_hunters_breath")));
        }
        [Test] public void BannerReservationsAreExplicitBoundedRefundableAndNotFreeEnergy()
        {
            var s=State();var b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=4;b.BannerStartTurn=1;var d=new PlanningDraft(s,new List<BattleEvent>());var c=Card(s,"b_split_helm");
            Assert.AreEqual(1,d.GetPlayCost(c));Assert.IsTrue(d.TrySetUseRageForCosts(true));Assert.AreEqual(1,d.GetPlayCost(c));
            Assert.IsTrue(d.TrySelectCard(c.InstanceId));Assert.IsTrue(d.TryAssignTargetAndSelect("enemy1"));Assert.AreEqual(2,d.ReservedRage);Assert.AreEqual(4,b.Rage);Assert.AreEqual(9,s.EnergyCurrent);
            Assert.IsFalse(d.TrySetUseRageForCosts(false));Assert.IsTrue(d.TryDeselectCard(c.InstanceId));Assert.AreEqual(0,d.ReservedRage);Assert.AreEqual(10,s.EnergyCurrent);Assert.AreEqual(4,b.Rage);
        }
        [Test] public void PayingTwoCardsCannotOverspendSameRage()
        {
            var s=State();var b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=3;b.BannerStartTurn=1;var d=new PlanningDraft(s,new List<BattleEvent>());d.TrySetUseRageForCosts(true);
            var c=Card(s,"b_split_helm");var second=Card(s,"b_split_helm");d.TrySelectCard(c.InstanceId);d.TryAssignTargetAndSelect("enemy1");Assert.AreEqual(2,d.GetPlayCost(second));d.TrySelectCard(second.InstanceId);d.TryAssignTargetAndSelect("enemy2");
            Assert.AreEqual(3,d.ReservedRage);Assert.AreEqual(3,b.Rage);d.PayRageReservations();Assert.AreEqual(0,b.Rage);Assert.AreEqual(7,s.EnergyCurrent);Assert.AreEqual(3,d.CommitToPlan().EnergySpent);
        }
        [Test] public void RecallMovesExistingDiscardCardsOnlyAfterDrawAndHonorsHandCapacity()
        {
            var s=State();var c=Card(s,"b_axe_cut");s.PlayerHand.Remove(c);s.PlayerDiscardPile.Add(c);BerserkerRules.QueueRecall(s,s.GetCombatant("hero"),"死斗",CardType.Attack,3,free:true);
            Assert.IsNull(BerserkerRules.PendingRecall(s));s.TurnNumber=2;Assert.IsNotNull(BerserkerRules.PendingRecall(s));Assert.IsTrue(BerserkerRules.ChooseRecall(s,c.InstanceId,new List<BattleEvent>()));
            Assert.AreEqual(0,c.Cost);Assert.Contains(c,s.PlayerHand);Assert.IsFalse(s.PlayerDiscardPile.Contains(c));DeckRules.MovePlayedCardToDiscard(s,TeamSide.Player,c,new List<BattleEvent>());Assert.AreEqual(1,c.Cost);
            s.Config.HandLimit=0;Assert.IsNull(BerserkerRules.PendingRecall(s));
        }
        [Test] public void DuelRestrictsSingleTargetButNotAoeAndClearsOnDeath()
        {
            var s=State();var duel=Card(s,"b_duel_to_end");s.ResolutionTargets[duel.InstanceId]="enemy3";Resolve(s,duel);var attack=Card(s,"b_split_helm");
            var candidates=CardRules.GetValidTargetCandidates(s,attack,s.GetCombatant("hero"));Assert.AreEqual(1,candidates.Count);Assert.AreEqual("enemy3",candidates[0].Id);
            Assert.IsNull(BerserkerRules.DuelTarget(s,s.GetCombatant("hero"),Card(s,"b_whirlwind")));
            s.GetCombatant("enemy3").Hp=0;BerserkerRules.Died(s,s.GetCombatant("enemy3"));Assert.IsNull(BerserkerRules.Get(s,s.GetCombatant("hero")).DuelTargetId);Assert.AreEqual(1,s.BerserkerRecalls.Count);
        }
        [Test] public void AdvanceMovesBackRowDirectlyToFrontAndCleansesRemoveAllStacks()
        {
            var s=State();var hero=s.GetCombatant("hero");hero.Slot=FormationSlot.Back;s.GetCombatant("ally").Slot=FormationSlot.Front;Resolve(s,Card(s,"b_advance"));Assert.AreEqual(FormationSlot.Front,hero.Slot);Assert.AreEqual(5,hero.Block);
            var ev=new List<BattleEvent>();StatusRules.ApplyStatus(s,hero,StatusCatalog.Weaken,25,2,ev);Resolve(s,Card(s,"b_iron_breath"));Assert.AreEqual(0,StatusRules.GetStatusStacks(hero,StatusCatalog.Weaken));
        }
        [Test] public void UpgradedDamageAndCardFaceAgreeWithoutMutatingOriginalOnRageSpend()
        {
            var s=State();var c=Card(s,"b_split_helm");var t=BerserkerCardActions.Create(c.DefinitionId);CardInstanceUpgradeApplier.ApplyToTemplate(t,2);c.Actions.Clear();c.Actions.AddRange(t.Actions);c.UpgradeLevel=2;BerserkerRules.Get(s,s.GetCombatant("hero")).Rage=3;
            Assert.IsTrue(BattleUiFormatters.BuildCardStatsLine(null,null,c).Contains("造成28伤害"));Resolve(s,c);Assert.AreEqual(957,s.GetCombatant("enemy1").Hp);Assert.AreEqual(28,c.Actions[0].Value);
        }
        [Test] public void TalentSourcesAndEmptiedResourceRewardsAreBounded()
        {
            var s=State();s.Config.Talents.ActiveTalentIds.Add("talent_berserker_s2_lv10");var hero=s.GetCombatant("hero");hero.Hp=30;var ev=new List<BattleEvent>();
            BerserkerRules.IncomingAttack(s,s.GetCombatant("enemy1"),hero,1,EnemyAttack(s).InstanceId,false,ev);var b=BerserkerRules.Get(s,hero);Assert.AreEqual(1,b.Rage);
            b.Tempered=true;Resolve(s,Card(s,"b_counter_cut"));Assert.AreEqual(1,s.PendingDrawNextTurn);Assert.AreEqual(1,s.BerserkerRecalls.Count);
            b.Rage=2;Resolve(s,Card(s,"b_counter_cut"));Assert.AreEqual(1,s.PendingDrawNextTurn);Assert.AreEqual(1,s.BerserkerRecalls.Count);
        }
        [Test] public void PreviewAllocatesExistingRageInSelectedOrderWithoutSpendingAnything()
        {
            var s=State();var hero=s.GetCombatant("hero");BerserkerSoulRules.Activate(s,hero,new List<BattleEvent>());var b=BerserkerRules.Get(s,hero);b.Rage=4;
            var c=Card(s,"b_split_helm");var d=new PlanningDraft(s,new List<BattleEvent>());d.TrySelectCard(c.InstanceId);d.TryAssignTargetAndSelect("enemy1");var g=d.GetDisplayedHand().Single(x=>x.SpectralRepeatSourceId>0);
            Assert.AreEqual(39,BerserkerRules.PreviewForPlanning(s,d,c).Actions[0].Value);Assert.AreEqual(29,BerserkerRules.PreviewForPlanning(s,d,g).Actions[0].Value);
            Assert.AreEqual(4,b.Rage);Assert.IsFalse(b.AttackedThisTurn);Assert.AreEqual(0,b.SpentByPlay.Count);Assert.AreEqual(24,c.Actions[0].Value);
            Assert.IsTrue(BattleUiFormatters.BuildCardStatsLineForHand(s,d,c,s.GetCombatant("enemy1")).Contains("39"));
        }
        [Test] public void InvalidatedSupplierCancelsReservationsInsteadOfGivingFreePlays()
        {
            var s=State();var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);b.Rage=4;b.BannerStartTurn=1;
            var d=new PlanningDraft(s,new List<BattleEvent>());d.TrySetUseRageForCosts(true);var c=Card(s,"b_split_helm");d.TrySelectCard(c.InstanceId);d.TryAssignTargetAndSelect("enemy1");
            hero.Hp=0;BerserkerRules.Died(s,hero);Assert.IsFalse(d.ValidateRageReservations());d.RevalidateAfterQuickStart();Assert.AreEqual(10,s.EnergyCurrent);Assert.AreEqual(0,d.SelectedQueue.Count);Assert.AreEqual(0,d.ReservedRage);
        }
        [Test] public void RagePaymentCanOnlyBeCommittedOnce()
        {
            var s=State();var b=BerserkerRules.Get(s,s.GetCombatant("hero"));b.Rage=4;b.BannerStartTurn=1;var d=new PlanningDraft(s,new List<BattleEvent>());d.TrySetUseRageForCosts(true);var c=Card(s,"b_split_helm");d.TrySelectCard(c.InstanceId);d.TryAssignTargetAndSelect("enemy1");
            d.PayRageReservations();d.PayRageReservations();Assert.AreEqual(2,b.Rage);Assert.AreEqual(1,s.BerserkerRagePayments.Count);
        }
        [Test] public void SteelResponseAddsIndependentRageOnTopOfHpLossAndCardReward()
        {
            var s=State();s.Config.Talents.ActiveTalentIds.Add("talent_berserker_s1_lv3");var hero=s.GetCombatant("hero");var enemy=s.GetCombatant("enemy1");var incoming=EnemyAttack(s);var ev=new List<BattleEvent>();var response=Card(s,"b_turn_aside");
            BerserkerRules.IncomingAttack(s,enemy,hero,1,incoming.InstanceId,false,ev);BerserkerRules.Responded(s,hero,response,incoming.InstanceId,ev);BerserkerRules.Responded(s,hero,response,incoming.InstanceId,ev);
            Assert.AreEqual(3,BerserkerRules.Get(s,hero).Rage);Assert.AreEqual(1,BerserkerRules.Get(s,hero).BaseGainThisTurn);
        }
        [Test] public void ConcentratedFuryAddsOneBeyondBaseQuotaOnlyOnce()
        {
            var s=State();var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);b.Concentrated=true;var ev=new List<BattleEvent>();
            for(var i=0;i<3;i++)BerserkerRules.IncomingAttack(s,s.GetCombatant("enemy1"),hero,1,EnemyAttack(s).InstanceId,false,ev);
            Assert.AreEqual(3,b.Rage);Assert.AreEqual(2,b.BaseGainThisTurn);
        }
        [Test] public void ConditionalStrikesAndPartnerProtectionMatchDescriptions()
        {
            var s=State();s.GetCombatant("enemy1").HitThisTurn=true;Resolve(s,Card(s,"b_short_hack"));Assert.AreEqual(989,s.GetCombatant("enemy1").Hp);
            s=State();s.GetCombatant("hero").Hp=38;Resolve(s,Card(s,"b_bloodied_edge"));Assert.AreEqual(978,s.GetCombatant("enemy1").Hp);
            s=State();var c=Card(s,"b_guard_partner");s.ResolutionTargets[c.InstanceId]="ally";Resolve(s,c);Assert.AreEqual(12,s.GetCombatant("hero").Block);Assert.AreEqual(12,s.GetCombatant("ally").Block);
            Assert.IsFalse(CardRules.GetValidTargetCandidates(s,c,s.GetCombatant("hero")).Any(x=>x.Id=="hero"));
        }
        [Test] public void HuntersBreathUsesUpgradeOncePerRageAndHealsWithoutGeneratingRage()
        {
            var s=State();var hero=s.GetCombatant("hero");hero.Hp=1;var b=BerserkerRules.Get(s,hero);b.Rage=3;var c=Card(s,"b_hunters_breath");c.UpgradeLevel=3;Resolve(s,c);
            Assert.AreEqual(22,hero.Hp);Assert.AreEqual(0,b.Rage);Assert.IsTrue(BerserkerCardCatalog.Describe(c).Contains("每点回复7HP"));
        }
        [Test] public void EnemyDuelPriorityDoesNotOverrideExplicitSlotOrRandomTargets()
        {
            var s=State();var hero=s.GetCombatant("hero");BerserkerRules.Get(s,hero).DuelTargetId="enemy1";var attacker=s.GetCombatant("enemy1");
            var action=new EffectActionSpec{Type=EffectActionType.DealDamage,Target=EffectTarget.DefaultEnemy,Reach=TargetReach.Any};
            Assert.AreEqual(hero,BerserkerRules.EnemyDuelTarget(s,attacker,action));action.Target=EffectTarget.EnemyBackSlot;Assert.IsNull(BerserkerRules.EnemyDuelTarget(s,attacker,action));
            action.Target=EffectTarget.RandomEnemy;Assert.IsNull(BerserkerRules.EnemyDuelTarget(s,attacker,action));
        }
        [Test] public void AllyCanWinDuelButDeathAndBattleEndPreventCrossBattleRecall()
        {
            var s=State();var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);b.DuelTargetId="enemy1";var target=s.GetCombatant("enemy1");target.Hp=1;
            DamageRules.ApplyDamage(s,s.GetCombatant("ally"),target,10,CardType.Attack,new List<BattleEvent>());Assert.IsNull(b.DuelTargetId);Assert.AreEqual(1,s.BerserkerRecalls.Count);
            hero.Hp=0;BerserkerRules.Died(s,hero);Assert.AreEqual(0,s.BerserkerRecalls.Count);Assert.AreEqual(0,b.Rage);
            s.Outcome=BattleOutcome.PlayerVictory;Assert.IsNull(BerserkerRules.PendingRecall(s));
        }
        [Test] public void InstalledAssetsHaveFivePosesAndProfileAndAll32CardsAvailable()
        {
            var hero=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.CharacterDefinitionSO>("Assets/_Project/Data/Characters/Character_Berserker.asset");Assert.IsNotNull(hero);Assert.AreEqual(7,hero.Deck.Count);Assert.AreEqual(76,hero.MaxHp);Assert.AreEqual(5,hero.Speed);
            var visuals=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.CharacterVisualCatalogSO>("Assets/_Project/Data/CharacterVisualCatalog_Demo.asset");var v=visuals.GetEntry("char_berserker");Assert.IsNotNull(v);
            foreach(var sprite in new[]{v.IdlePortrait,v.AttackPortrait,v.DefensePortrait,v.HitPortrait,v.DeathPortrait,v.CardProfilePortrait})Assert.IsNotNull(sprite);
            Assert.IsTrue(v.PreserveOriginalFacing);Assert.IsTrue(v.HitPortraitFacesRight);
            Assert.IsFalse(hero.Traits.Contains("pressure_to_rage"));
            var effects=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.BattleActionEffectCatalogSO>("Assets/_Project/Data/BattleActionEffectCatalog_Demo.asset");
            Assert.IsNotNull(effects.BerserkerDamage);
            Assert.AreNotSame(effects.WarriorDamage,effects.BerserkerDamage);
            Assert.AreSame(effects.BerserkerDamage,BattleActionEffectResolver.ResolveDamageEffect(effects,"char_berserker"));
            Assert.AreEqual("The Grimhands Asset/characters/berserker/berserker_anim.gif",v.IdleAnimationGifPath);
            Assert.AreEqual(1024,v.IdlePortrait.texture.width);Assert.AreEqual(1024,v.CardProfilePortrait.texture.width);
            Assert.AreEqual(762,v.IdlePortrait.rect.width);Assert.AreEqual(946,v.IdlePortrait.rect.height);
            Assert.AreEqual(306,v.CardProfilePortrait.rect.width);Assert.AreEqual(362,v.CardProfilePortrait.rect.height);
            var frames=visuals.GetIdleAnimationFrames("char_berserker");Assert.AreEqual(8,frames.Count);
            Assert.AreEqual(256,frames[0].rect.width);Assert.AreEqual(256,frames[0].rect.height);
            var setup=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            Assert.AreEqual(32,setup.PlayerCardCatalog.Count(x=>x!=null&&x.OwnerCharacterId=="char_berserker"));
            foreach(var c in setup.PlayerCardCatalog.Where(x=>x!=null&&x.OwnerCharacterId=="char_berserker"))
            {var t=c.ToTemplate();Assert.AreEqual(BerserkerCardActions.Create(c.CardId).Actions.Count,t.Actions.Count,c.CardId);}
        }
        [Test] public void ZeroRageRemainsVisibleAsAResourceNotAnInvisibleDamageBuff()
        {
            var s=State();var entries=FootStatusIconAggregator.Aggregate(s.GetCombatant("hero"),s);
            Assert.IsTrue(entries.Any(e=>e.StatusId=="berserker_rage"&&e.Stacks==0));
        }
        [Test] public void ArmorTalentOnAoeStripsEveryTargetOnce()
        {
            var s=State();var hero=s.GetCombatant("hero");s.Config.Talents.ActiveTalentIds.Add("talent_berserker_s2_lv6");BerserkerRules.Get(s,hero).Rage=4;
            foreach(var enemy in s.GetTeam(TeamSide.Enemy))enemy.Block=100;
            var card=Card(s,"b_whirlwind");s.ResolutionTargets.Remove(card.InstanceId);Resolve(s,card);
            foreach(var enemy in s.GetTeam(TeamSide.Enemy))Assert.AreEqual(78,enemy.Block);
        }
        [TestCase(1,1,36)][TestCase(2,1,26)][TestCase(3,1,16)]
        [TestCase(1,4,36)][TestCase(1,0,28)]
        public void WhirlwindUsesMissingEnemiesAndOneFlatRageMultiplier(int enemies,int rage,int damage)
        {
            var s=State();for(var i=enemies+1;i<=3;i++)s.GetCombatant("enemy"+i).Hp=0;
            var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);b.Rage=rage;
            var card=Card(s,"b_whirlwind");var preview=BerserkerRules.PreviewForPlanning(s,null,card);
            Assert.AreEqual(damage,preview.Actions[0].Value);Assert.AreEqual(rage,b.Rage);
            Resolve(s,card);Assert.AreEqual(1000-damage,s.GetCombatant("enemy1").Hp);Assert.AreEqual(0,b.Rage);
        }
        [TestCase(0,9)][TestCase(20,29)][TestCase(30,39)][TestCase(80,39)]
        public void MeasuredGuardExtraArmorIsCappedAtThirtyInPreviewAndResolution(int armor,int gain)
        {
            var s=State();var hero=s.GetCombatant("hero");hero.Block=armor;
            var b=BerserkerRules.Get(s,hero);b.Rage=3;
            var card=Card(s,"b_measured_guard");
            Assert.AreEqual(gain,BerserkerRules.PreviewForPlanning(s,null,card).Actions[0].Value);
            Assert.AreEqual(armor,hero.Block);Assert.AreEqual(3,b.Rage);
            Resolve(s,card);Assert.AreEqual(armor+gain,hero.Block);Assert.AreEqual(0,b.Rage);
        }
        [Test] public void RevisedDefenseCopiesArmorAndMovesFrontHeroBehindAlly()
        {
            var s=State();var hero=s.GetCombatant("hero");hero.Block=20;
            var b=BerserkerRules.Get(s,hero);b.Rage=1;
            Resolve(s,Card(s,"b_measured_guard"));Assert.AreEqual(49,hero.Block);Assert.AreEqual(0,b.Rage);
            Resolve(s,Card(s,"b_stand_firm"));Assert.AreEqual(FormationSlot.Middle,hero.Slot);Assert.AreEqual(1,b.Rage);
            PositionRules.SwapCombatants(s,hero,s.GetCombatant("ally"),new(),"test");Assert.AreEqual(2,b.Rage);
        }
        [Test] public void RevisedAxeCrushConvertsRemovedArmorAndSweepCleansesOnlyWithRage()
        {
            var s=State();var target=s.GetCombatant("enemy1");target.Block=21;
            var crush=Card(s,"b_axe_crush");
            var draft=new PlanningDraft(s,new());Assert.IsTrue(draft.TrySelectCard(crush.InstanceId));
            var face=BattleUiFormatters.BuildCardStatsLineForHand(s,draft,crush,target);
            Assert.IsTrue(face.Contains("6</color>伤害"),face);
            Assert.AreEqual(21,target.Block);
            Resolve(s,crush);Assert.AreEqual(0,target.Block);Assert.AreEqual(994,target.Hp);
            var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);b.Rage=1;
            StatusRules.ApplyStatus(s,hero,StatusCatalog.Weaken,10,2,new());
            StatusRules.ApplyStatus(s,hero,StatusCatalog.Vulnerable,10,2,new());
            var before=target.Hp;Resolve(s,Card(s,"b_sweeping_edge"));Assert.AreEqual(0,b.Rage);
            Assert.AreEqual(6,before-target.Hp,"Sweep attacks before cleansing weakness, matching its face preview.");
            Assert.IsFalse(hero.Statuses.Any(x=>x.StatusId==StatusCatalog.Weaken||x.StatusId==StatusCatalog.Vulnerable));
            b.BannerStartTurn=1;b.Rage=2;
            StatusRules.ApplyStatus(s,hero,StatusCatalog.Weaken,10,2,new());
            var fueled=Card(s,"b_sweeping_edge");
            s.BerserkerRagePayments[fueled.InstanceId]=new BerserkerRagePayment{OwnerId=hero.Id,Amount=2,Emptied=true};
            Resolve(s,fueled);Assert.IsTrue(hero.Statuses.Any(x=>x.StatusId==StatusCatalog.Weaken),"Banner payment must not also grant the rage cleanse.");
        }
        [Test] public void ActiveBerserkerEffectsHaveIconsAndHoverDescriptions()
        {
            var s=State();var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);
            b.BannerStartTurn=s.TurnNumber+1;b.Concentrated=b.Tempered=true;
            BerserkerSoulRules.Activate(s,hero,new());b.DuelTargetId="enemy1";
            var entries=FootStatusIconAggregator.Aggregate(hero,s);
            foreach(var id in new[]{"berserker_banner","berserker_concentrated","berserker_tempered","berserker_soul","berserker_duel"})
                Assert.IsTrue(entries.Any(x=>x.StatusId==id),id);
            var text=BattleUiFormatters.FormatStatusTooltipDescriptions(hero,s);
            foreach(var name in new[]{"燃尽战旗","下回合","专注怒意","淬怒","狂战之魂","死斗"})Assert.IsTrue(text.Contains(name),name);
            Assert.IsTrue(FootStatusIconAggregator.Aggregate(s.GetCombatant("enemy1"),s).Any(x=>x.StatusId=="berserker_duel"));
        }
        [Test] public void RepeatingSameInstanceWithoutRageDoesNotReuseOldSpendMetadata()
        {
            var s=State();var hero=s.GetCombatant("hero");var b=BerserkerRules.Get(s,hero);var c=Card(s,"b_split_helm");b.Rage=3;Resolve(s,c);Assert.AreEqual(3,b.SpentByPlay[c.InstanceId]);
            Resolve(s,c);Assert.IsFalse(b.SpentByPlay.ContainsKey(c.InstanceId));
            s.BerserkerRagePayments[c.InstanceId]=new BerserkerRagePayment{OwnerId=hero.Id,Amount=2};s.TurnNumber=2;BerserkerRules.TurnStart(s,new List<BattleEvent>());Assert.AreEqual(0,s.BerserkerRagePayments.Count);
        }
        [Test] public void DefenseCannotBeQueuedAfterSoulActivationButCanBePlayedBeforeIt()
        {
            var s=State();var d=new PlanningDraft(s,new List<BattleEvent>());var soul=Card(s,"b_berserker_soul");var defense=Card(s,"b_iron_guard");
            Assert.IsTrue(d.TrySelectCard(soul.InstanceId));Assert.IsFalse(d.IsCardSelectable(defense.InstanceId));d.RefundAllSelections();
            Assert.IsTrue(d.TrySelectCard(defense.InstanceId));Assert.IsTrue(d.TrySelectCard(soul.InstanceId));
        }
        [Test] public void SingleCardRecallRequiresChoiceButUpToThreeMayBeSkipped()
        {
            var s=State();var c=Card(s,"b_iron_guard");s.PlayerHand.Remove(c);s.PlayerDiscardPile.Add(c);BerserkerRules.QueueRecall(s,s.GetCombatant("hero"),"淬怒",CardType.Defense,1);s.TurnNumber=2;
            Assert.IsFalse(BerserkerRules.ChooseRecall(s,0,new List<BattleEvent>()));Assert.IsTrue(BerserkerRules.ChooseRecall(s,c.InstanceId,new List<BattleEvent>()));
        }
    }
}
