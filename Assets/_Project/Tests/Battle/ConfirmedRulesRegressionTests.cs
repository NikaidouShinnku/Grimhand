using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle.AI;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ConfirmedRulesRegressionTests
    {
        static BattleState CreateState()
        {
            var s = new BattleState();
            s.Combatants.Add(new CombatantState { Id="p", CharacterDefinitionId="char_knight", Team=TeamSide.Player, Slot=FormationSlot.Front, Hp=100, MaxHp=100 });
            s.Combatants.Add(new CombatantState { Id="e", CharacterDefinitionId="enemy", Team=TeamSide.Enemy, Slot=FormationSlot.Front, Hp=100, MaxHp=100 });
            return s;
        }

        static CardInstanceState AddCard(BattleState s, int id, int cost, TeamSide team)
        {
            var c = new CardInstanceState { InstanceId=id, DefinitionId="regression_"+id, DisplayName="Card"+id, Cost=cost, BaseCost=cost,
                CardType=CardType.Attack, OwnerCombatantId=team==TeamSide.Player?"p":"e" };
            c.Actions.Add(new EffectActionSpec { Type=EffectActionType.DealDamage, Target=EffectTarget.DefaultEnemy, Value=10 });
            s.CardsById[id]=c;
            s.GetHand(team).Add(c);
            return c;
        }

        static void Hit(BattleState s, string source, List<BattleEvent> events)
        {
            var target=s.Combatants[0];
            if (source=="attack" || source=="sacrifice")
                DamageRules.ApplyDamage(s,source=="attack"?s.Combatants[1]:target,target,20,CardType.Attack,events,
                    isSacrificeDamage:source=="sacrifice");
            else if (source=="true")
                DamageRules.ApplyTrueDamage(s,s.Combatants[1],target,20,events);
            else if (source=="poison" || source=="burn")
            {
                StatusRules.ApplyStatus(s,target,source=="poison"?StatusCatalog.Poison:StatusCatalog.Burn,20,2,events);
                StatusRules.ProcessTurnStartStatuses(s,events,new BattleRng(1));
            }
            else if (source=="delayed")
            {
                StatusRules.ApplyStatus(s,target,StatusCatalog.DelayedDamage,20,1,events);
                V09NewMechanicsRules.ProcessTurnStart(s,events,new BattleRng(1));
            }
            else
                DamageRules.ApplyPeriodicDamage(s,target,20,events,"pulse");
        }

        [TestCase("attack")][TestCase("sacrifice")][TestCase("true")]
        [TestCase("poison")][TestCase("burn")][TestCase("delayed")][TestCase("pulse")]
        public void EtherealCapsEveryDamageSource(string source)
        {
            var s=CreateState(); var events=new List<BattleEvent>();
            StatusRules.ApplyStatus(s,s.Combatants[0],StatusCatalog.Ethereal,1,2,events);
            Hit(s,source,events);
            Assert.That(s.Combatants[0].Hp,Is.EqualTo(99));
        }

        [TestCase("attack")][TestCase("sacrifice")][TestCase("true")]
        [TestCase("poison")][TestCase("burn")][TestCase("delayed")][TestCase("pulse")]
        public void LastStandProtectsRepeatedLethalHitsWithoutUsingRevival(string source)
        {
            var s=CreateState(); var events=new List<BattleEvent>(); s.Combatants[0].Hp=3;
            s.MiracleLeafRevivesRemaining=1;
            StatusRules.ApplyStatus(s,s.Combatants[0],StatusCatalog.LastStand,1,2,events);
            Hit(s,source,events); Hit(s,source,events);
            Assert.That(s.Combatants[0].Hp,Is.EqualTo(1));
            Assert.That(s.MiracleLeafRevivesRemaining,Is.EqualTo(1));
            Assert.That(events.Any(e=>e.Kind==BattleEventKind.CharacterDied),Is.False);
        }

        [TestCase("poison")][TestCase("true")][TestCase("pulse")]
        public void LichTalentReplacesEtherealForEverySource(string source)
        {
            var s=CreateState(); var events=new List<BattleEvent>(); var p=s.Combatants[0];
            p.CharacterDefinitionId=TalentBattleRules.LichQueenId; p.Hp=50;
            s.Config.Talents.ActiveTalentIds.Add("talent_lich_s1_lv4");
            StatusRules.ApplyStatus(s,p,StatusCatalog.Ethereal,1,2,events);
            Hit(s,source,events);
            Assert.That(p.Hp,Is.EqualTo(52));
        }

        [TestCase(StatusCatalog.Burn,95,0)]
        [TestCase(StatusCatalog.Poison,90,5)]
        public void BurnConsumesArmorButPoisonBypassesIt(string status,int hp,int block)
        {
            var s=CreateState(); var events=new List<BattleEvent>(); s.Combatants[0].Block=5;
            StatusRules.ApplyStatus(s,s.Combatants[0],status,status==StatusCatalog.Burn?5:10,2,events);
            StatusRules.ProcessTurnStartStatuses(s,events,new BattleRng(2));
            Assert.That(s.Combatants[0].Hp,Is.EqualTo(hp));
            Assert.That(s.Combatants[0].Block,Is.EqualTo(block));
        }

        [TestCase(7,1,8)][TestCase(8,1,8)][TestCase(12,1,12)]
        public void EnergyRestorationNeverRemovesTemporaryEnergy(int current,int restore,int expected)
        {
            var s=CreateState(); s.EnergyMax=8; s.EnergyCurrent=current;
            Assert.That(EnergyRules.Restore(s,restore),Is.GreaterThanOrEqualTo(0));
            Assert.That(s.EnergyCurrent,Is.EqualTo(expected));
        }

        [Test]
        public void FirstAttackBonusCoversMultipleHitsButNotARepeatedPlay()
        {
            var s=CreateState(); s.Config.RunModifiers.FirstAttackFlatBonus=5;
            var card=AddCard(s,1,1,TeamSide.Player); var events=new List<BattleEvent>();
            RelicBattleRules.BeginCardResolution(s,s.Combatants[0],card);
            for(var i=0;i<3;i++) DamageRules.ApplyDamage(s,s.Combatants[0],s.Combatants[1],10,CardType.Attack,events,sourceCardInstanceId:1);
            Assert.That(s.Combatants[1].Hp,Is.EqualTo(55));
            RelicEffectRules.OnCardResolved(s,s.Combatants[0],card,events,new BattleRng(1));
            RelicBattleRules.BeginCardResolution(s,s.Combatants[0],card);
            DamageRules.ApplyDamage(s,s.Combatants[0],s.Combatants[1],10,CardType.Attack,events,sourceCardInstanceId:1);
            Assert.That(s.Combatants[1].Hp,Is.EqualTo(45));
        }

        [Test]
        public void OneAoeAttackPairsOnlyWithTheFirstPlayerResponse()
        {
            var s=CreateState(); s.Combatants.Add(new CombatantState {Id="p2",Team=TeamSide.Player,Hp=100,MaxHp=100,Slot=FormationSlot.Middle,Speed=5});
            var attack=AddCard(s,1,1,TeamSide.Enemy); attack.Actions[0].Target=EffectTarget.AllEnemies; attack.Actions[0].Reach=TargetReach.Any;
            var first=AddCard(s,2,1,TeamSide.Player); first.Keywords.Add("parry"); first.OwnerCombatantId="p2";
            var second=AddCard(s,3,1,TeamSide.Player); second.Keywords.Add("parry");
            s.PlayerPlan.PlayQueue.AddRange(new[]{2,3}); s.EnemyPlan.PlayQueue.Add(1);
            var baseline=SpeedResolver.BuildResolutionOrder(s,s.PlayerPlan,s.EnemyPlan,new BattleRng(1));
            var schedule=Grimhand.Battle.Reactions.RespondResolutionPlanner.BuildSchedule(s,baseline);
            Assert.That(schedule.Count(e=>e.PairedRespondCardInstanceId>0),Is.EqualTo(1));
            Assert.That(schedule.Single(e=>e.Step.CardInstanceId==1).PairedRespondCardInstanceId,Is.EqualTo(2));
        }

        [Test]
        public void GenericPercentageModifiersKeepTheConfirmedOneDamageMinimum()
        {
            Assert.That(CombatModifierRules.ApplyIncomingDamageModifiers(new CombatantState {IncomingDamageReductionPercent=100},10,0),Is.EqualTo(1));
            Assert.That(CombatModifierRules.ApplyOutgoingDamageModifiers(new CombatantState {OutgoingDamagePercentBonus=-100},CardType.Attack,10),Is.EqualTo(1));
        }

        [Test]
        public void ExtraDrawDiscountAppliesOnlyToItsOwnBatch()
        {
            var config=new BattleConfig {CardsDrawnPerTurn=0,EnemyCardsDrawnPerTurn=0,ManualEnemyIntentsOnly=true};
            config.Combatants.Add(new CombatantConfig {Id="p",Team=TeamSide.Player,CharacterDefinitionId="char_knight",MaxHp=100,Slot=FormationSlot.Front});
            config.Combatants.Add(new CombatantConfig {Id="e",Team=TeamSide.Enemy,CharacterDefinitionId="enemy",MaxHp=100,Slot=FormationSlot.Front});
            var engine=new BattleEngine(config); engine.StartBattle(); var s=engine.State;
            for(var i=1;i<=3;i++) { var c=AddCard(s,i,4,TeamSide.Player); s.PlayerHand.Remove(c); s.PlayerDrawPile.Add(c); }
            s.PendingDrawNextTurn=3; s.PendingDrawNextTurnCostReduction=2;
            s.PendingDrawBatches.Add(new PendingDrawBatch {Count=1,CostReduction=0});
            s.PendingDrawBatches.Add(new PendingDrawBatch {Count=2,CostReduction=2});
            engine.SkipPlayerTurn(); engine.FlushPendingEndOfTurn();
            Assert.That(s.PlayerHand.Select(c=>c.Cost).ToArray(),Is.EqualTo(new[]{4,2,2}));
            Assert.That(s.PendingDrawBatches,Is.Empty);
        }

        [Test]
        public void NegatedHitDoesNotReusePreviousDamageForLifestealOrKillHealing()
        {
            var s=CreateState(); var card=AddCard(s,1,1,TeamSide.Player); s.Combatants[0].Hp=50;
            card.Actions[0].LifestealPercent=100; card.Actions[0].OnKillHealAmount=20;
            s.Combatants[1].InvulnerableTurnsRemaining=1;
            s.LastAction=new LastActionSnapshot("p",ActionKind.Attack,"old",true,30);
            EffectActionExecutor.ExecuteOne(s,s.Combatants[0],card,card.Actions[0],new List<BattleEvent>(),new BattleRng(1),1,s.Combatants[1]);
            Assert.That(s.Combatants[0].Hp,Is.EqualTo(50));
            Assert.That(s.LastAction.DamageAmount,Is.Zero); Assert.That(s.LastAction.WasKill,Is.False);
        }

        [Test]
        public void FirstAttackRelicBonusCoversEveryAoeTarget()
        {
            var config=new BattleConfig {CardsDrawnPerTurn=0,ManualEnemyIntentsOnly=true};
            config.RunModifiers.FirstAttackFlatBonus=5;
            config.Combatants.Add(new CombatantConfig {Id="p",Team=TeamSide.Player,CharacterDefinitionId="char_knight",MaxHp=100,Slot=FormationSlot.Front});
            for(var i=0;i<3;i++) config.Combatants.Add(new CombatantConfig {Id="e"+i,Team=TeamSide.Enemy,CharacterDefinitionId="enemy"+i,MaxHp=100,Slot=(FormationSlot)(i+1)});
            var engine=new BattleEngine(config); engine.StartBattle();
            var template=CardTemplate.Create("w_test_aoe","AOE","char_knight",1,CardType.Attack,
                new EffectActionSpec {Type=EffectActionType.DealDamage,Target=EffectTarget.AllEnemies,Value=10,Reach=TargetReach.Any});
            var card=engine.AddCardTemplateToHand(template);
            Assert.That(engine.Draft.TrySelectCard(card.InstanceId),Is.True); Assert.That(engine.CommitPlayerPlan(),Is.True);
            for(var i=0;i<3;i++) Assert.That(engine.State.GetCombatant("e"+i).Hp,Is.EqualTo(85));
        }

        [Test]
        public void PoisonVictoryDoesNotReturnToPlanningOrDrawAnotherHand()
        {
            var config=new BattleConfig {CardsDrawnPerTurn=5};
            config.Combatants.Add(new CombatantConfig {Id="p",Team=TeamSide.Player,CharacterDefinitionId="char_knight",MaxHp=100,Slot=FormationSlot.Front});
            config.Combatants.Add(new CombatantConfig {Id="e",Team=TeamSide.Enemy,CharacterDefinitionId="enemy",MaxHp=1,Slot=FormationSlot.Front});
            var engine=new BattleEngine(config);
            StatusRules.ApplyStatus(engine.State,engine.State.GetCombatant("e"),StatusCatalog.Poison,5,1,new List<BattleEvent>());
            engine.StartBattle();
            Assert.That(engine.State.Outcome,Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(engine.State.Phase,Is.EqualTo(TurnPhase.BattleEnd));
            Assert.That(engine.State.EnemyPlan.PlayQueue,Is.Empty);
        }

        [Test]
        public void ComboAppliesToAllAttacksOnlyForAConsecutiveTriple()
        {
            var s=CreateState(); s.Config.Talents.ActiveTalentIds.Add("talent_knight_s2_lv8");
            var a=AddCard(s,1,1,TeamSide.Player); var b=AddCard(s,2,1,TeamSide.Player);
            var c=AddCard(s,3,1,TeamSide.Player); var defense=AddCard(s,4,1,TeamSide.Player); defense.CardType=CardType.Defense;
            s.PlayerPlan.PlayQueue.AddRange(new[]{1,4,2,3});
            TalentBattleRules.TryApplyComboFromCommittedPlan(s,new List<BattleEvent>());
            Assert.That(StatusRules.HasStatus(s.Combatants[0],StatusCatalog.KnightComboAtk),Is.False);
            s.PlayerPlan.PlayQueue.Clear(); s.PlayerPlan.PlayQueue.AddRange(new[]{1,2,3,4});
            TalentBattleRules.TryApplyComboFromCommittedPlan(s,new List<BattleEvent>());
            Assert.That(StatusRules.GetStatusStacks(s.Combatants[0],StatusCatalog.KnightComboAtk),Is.EqualTo(33));
            Assert.That(s.Combatants[0].TalentAttackCardsThisTurn,Is.Zero,"Bonus is armed before the first attack resolves");
        }

        [Test]
        public void DefaultBattleModifiersAreNotSharedBetweenBattles()
        {
            var first=new BattleConfig(); var second=new BattleConfig();
            first.RunModifiers.FirstAttackFlatBonus=99;
            Assert.That(second.RunModifiers.FirstAttackFlatBonus,Is.Zero);
            Assert.That(ReferenceEquals(first.RunModifiers,second.RunModifiers),Is.False);
        }

        [Test]
        public void SoulBondSharesOnceInsteadOfBouncingBack()
        {
            var s=CreateState(); var a=s.Combatants[0]; var b=s.Combatants[1]; b.Team=TeamSide.Player; a.Hp=b.Hp=50;
            s.SoulBondPartnerByCombatantId[a.Id]=b.Id; s.SoulBondPartnerByCombatantId[b.Id]=a.Id;
            DamageRules.ApplyHeal(s,a,10,new List<BattleEvent>(),a);
            Assert.That(a.Hp,Is.EqualTo(60)); Assert.That(b.Hp,Is.EqualTo(55));
            Assert.That(s.ActiveHealingRecipients,Is.Empty);
        }

        [Test]
        public void RevivalRestoresOnlyDeathPollution()
        {
            var s=CreateState(); var a=AddCard(s,1,1,TeamSide.Player); var curse=AddCard(s,2,1,TeamSide.Player);
            curse.Keywords.Add("curse"); curse.IsUsable=false;
            var locked=AddCard(s,3,1,TeamSide.Player); locked.Keywords.Add("engraving_lock"); locked.IsUsable=false;
            var exhausted=AddCard(s,4,1,TeamSide.Player); DeckRules.ExhaustCard(s,TeamSide.Player,exhausted,new List<BattleEvent>());
            s.Combatants[0].Hp=0; CombatantDeathRules.OnCharacterDied(s,s.Combatants[0],new List<BattleEvent>());
            DamageRules.ApplyRevive(s,s.Combatants[0],20,new List<BattleEvent>());
            Assert.That(a.IsUsable,Is.True); Assert.That(a.DisabledByDeath,Is.False);
            Assert.That(curse.IsUsable,Is.False); Assert.That(locked.IsUsable,Is.False); Assert.That(exhausted.IsUsable,Is.False);
        }

        [Test]
        public void MusketFallbackParticipatesInEnemyPlanning()
        {
            var s=CreateState(); var c=AddCard(s,1,1,TeamSide.Enemy); c.DefinitionId="m_musket_shot"; c.Actions[0].Reach=TargetReach.BackOnly;
            Assert.That(EnemyTurnPlanner.PrepareEnemyTurn(s,new BattleRng(1),4).Plan.PlayQueue,Does.Contain(1));
        }

        [Test]
        public void EnemyPlannerMaximizesBudgetAndKeepsFreeCards()
        {
            var s=CreateState(); AddCard(s,1,1,TeamSide.Enemy); AddCard(s,2,2,TeamSide.Enemy); AddCard(s,3,2,TeamSide.Enemy); AddCard(s,4,0,TeamSide.Enemy);
            var plan=EnemyTurnPlanner.PrepareEnemyTurn(s,new BattleRng(1),4).Plan;
            Assert.That(plan.EnergySpent,Is.EqualTo(4)); Assert.That(plan.PlayQueue,Does.Contain(4));
            Assert.That(plan.PlayQueue.Distinct().Count(),Is.EqualTo(plan.PlayQueue.Count));
        }

        [TestCase(StatusCatalog.Poison)][TestCase(StatusCatalog.Burn)]
        public void QueenThresholdWorksForStatusTicks(string status)
        {
            var s=CreateState(); var q=s.Combatants[1]; q.MaxHp=220; q.Hp=130; q.Traits.Add(CharacterTraitCatalog.GhostQueenEnrage);
            var events=new List<BattleEvent>(); StatusRules.ApplyStatus(s,q,status,status==StatusCatalog.Burn?10:20,2,events);
            StatusRules.ProcessTurnStartStatuses(s,events,new BattleRng(1));
            Assert.That(q.GhostQueenEnrageTriggered,Is.True); Assert.That(s.PendingBossBonusHandsNextTurn.Count,Is.EqualTo(1));
        }

        [Test]
        public void PreviewMatchesInvulnerabilityAndEtherealWithoutConsumingFlags()
        {
            var s=CreateState(); var c=AddCard(s,1,1,TeamSide.Player); var e=s.Combatants[1];
            e.InvulnerableTurnsRemaining=1;
            Assert.That(CardPreviewRules.PreviewHpDamageAgainstTarget(s,s.Combatants[0],c,c.Actions[0],e),Is.Zero);
            e.InvulnerableTurnsRemaining=0;
            StatusRules.ApplyStatus(s,e,StatusCatalog.Ethereal,1,2,new List<BattleEvent>());
            Assert.That(CardPreviewRules.PreviewHpDamageAgainstTarget(s,s.Combatants[0],c,c.Actions[0],e),Is.EqualTo(1));
            Assert.That(s.Combatants[0].FirstAttackBonusPending,Is.True); Assert.That(e.Hp,Is.EqualTo(100));
        }

        [Test]
        public void HealPreviewIncludesPharaohRelicBonus()
        {
            var s=CreateState(); s.Config.RunModifiers.HealBonusPercent=50; s.Combatants[0].CharacterDefinitionId=RelicBattleRules.PharaohCharacterId;
            Assert.That(CardPreviewRules.ComputeExpectedHeal(s,s.Combatants[0],new EffectActionSpec { Type=EffectActionType.Heal,Value=10 }),Is.EqualTo(15));
        }

        [Test]
        public void ResolutionCloneMovesTheRegisteredCardOnlyOnce()
        {
            var s=CreateState(); var original=AddCard(s,1,2,TeamSide.Player);
            var resolutionClone=new CardInstanceState { InstanceId=1, DefinitionId=original.DefinitionId };
            DeckRules.MovePlayedCardToDiscard(s,TeamSide.Player,resolutionClone,new List<BattleEvent>());
            DeckRules.MovePlayedCardToDiscard(s,TeamSide.Player,resolutionClone,new List<BattleEvent>());
            Assert.That(s.PlayerHand,Is.Empty); Assert.That(s.PlayerDiscardPile.Count,Is.EqualTo(1));
            Assert.That(s.PlayerDiscardPile[0],Is.SameAs(original));
            DeckRules.ExhaustCard(s,TeamSide.Player,resolutionClone,new List<BattleEvent>());
            Assert.That(s.PlayerDiscardPile,Is.Empty); Assert.That(original.IsUsable,Is.False);
            Assert.That(s.PlayerExhaustPile[0],Is.SameAs(original));
        }

        [Test]
        public void HandDiscountSurvivesRetainAndExpiresOnDiscard()
        {
            var s=CreateState(); var c=AddCard(s,1,4,TeamSide.Player); c.Keywords.Add("inherit");
            DeckRules.ApplyHandCostReduction(c,2);
            DeckRules.DiscardHandAtEndOfTurn(s,TeamSide.Player,new List<BattleEvent>());
            Assert.That(c.Cost,Is.EqualTo(2)); Assert.That(s.PlayerHand,Does.Contain(c));
            DeckRules.MovePlayedCardToDiscard(s,TeamSide.Player,c,new List<BattleEvent>());
            Assert.That(c.Cost,Is.EqualTo(4)); Assert.That(c.CostBeforeHandDiscount,Is.Null);
        }

        [Test]
        public void ShuffledCostsIncludeDiscountAndRestoreEachCardsOwnCost()
        {
            var swapped = false;
            for (var seed = 0; seed < 100; seed++)
            {
                var s = CreateState();
                var a = AddCard(s, 1, 3, TeamSide.Player);
                var b = AddCard(s, 2, 4, TeamSide.Player);
                DeckRules.ApplyHandCostReduction(a, 2);
                Grimhand.Battle.V09.V09NewMechanicsRules.ShuffleHandCosts(s, s.Combatants[0], new BattleRng(seed));
                if (a.Cost != 4) continue;
                swapped = true;
                Assert.That(b.Cost, Is.EqualTo(1));
                DeckRules.MovePlayedCardToDiscard(s, TeamSide.Player, a, new List<BattleEvent>());
                DeckRules.MovePlayedCardToDiscard(s, TeamSide.Player, b, new List<BattleEvent>());
                Assert.That(a.Cost, Is.EqualTo(3));
                Assert.That(b.Cost, Is.EqualTo(4));
                Assert.That(a.CostBeforeHandDiscount, Is.Null);
                Assert.That(b.CostBeforeHandDiscount, Is.Null);
                break;
            }
            Assert.That(swapped, Is.True);
        }

        [Test]
        public void ScryReturnsChosenOrderAndIgnoresDuplicateOrForeignIds()
        {
            var s=CreateState();
            var a=new CardInstanceState {InstanceId=1}; var b=new CardInstanceState {InstanceId=2};
            var c=new CardInstanceState {InstanceId=3}; var tail=new CardInstanceState {InstanceId=4};
            s.PendingPsionicScryCards.AddRange(new[]{a,b,c}); s.PlayerDrawPile.Add(tail); s.AwaitingPsionicScry=true;
            Grimhand.Battle.V091.V091MechanicsRules.ApplyPsionicScryChoice(s,new[]{2},new List<BattleEvent>(),new[]{3,3,999,2,1});
            Assert.That(s.PlayerDrawPile.Select(x=>x.InstanceId),Is.EqualTo(new[]{3,1,4}));
            Assert.That(s.PlayerDiscardPile.Select(x=>x.InstanceId),Is.EqualTo(new[]{2}));
            Assert.That(s.AwaitingPsionicScry,Is.False); Assert.That(s.PendingPsionicScryCards,Is.Empty);
        }

        [Test]
        public void ScryCanDiscardAllOrKeepAllWithoutLosingCards()
        {
            var s=CreateState(); s.PendingPsionicScryCards.Add(new CardInstanceState {InstanceId=1}); s.AwaitingPsionicScry=true;
            Grimhand.Battle.V091.V091MechanicsRules.ApplyPsionicScryChoice(s,new[]{1},new List<BattleEvent>(),new[]{1});
            Assert.That(s.PlayerDrawPile,Is.Empty); Assert.That(s.PlayerDiscardPile.Count,Is.EqualTo(1));
            s.PendingPsionicScryCards.Add(new CardInstanceState {InstanceId=2}); s.AwaitingPsionicScry=true;
            Grimhand.Battle.V091.V091MechanicsRules.ApplyPsionicScryChoice(s,null,new List<BattleEvent>(),null);
            Assert.That(s.PlayerDrawPile[0].InstanceId,Is.EqualTo(2));
        }

        [Test]
        public void BonusUpgradeSupportsDelayedDamageAndPreservesHandAndDeathMetadata()
        {
            var c=new CardInstanceState {DisplayName="灵能炮",Cost=3,BaseCost=3,DisabledByDeath=true};
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.ApplyDelayedDamage,Value=13});
            DeckRules.ApplyHandCostReduction(c,2);
            var upgraded=CardInstanceUpgradeApplier.ApplyBonusLevels(c,1);
            Assert.That(upgraded.Actions[0].Value,Is.EqualTo(14)); Assert.That(c.Actions[0].Value,Is.EqualTo(13));
            Assert.That(upgraded.DisabledByDeath,Is.True);
            DeckRules.RestoreCostAfterLeavingHand(upgraded); Assert.That(upgraded.Cost,Is.EqualTo(3));
        }

        [Test]
        public void UpgradeImprovesAttackWithoutIncreasingSacrificeCost()
        {
            var c=new CardInstanceState {DisplayName="血焰爆发"};
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamage,Target=EffectTarget.Self,Value=8});
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamage,Value=22});
            var upgraded=CardInstanceUpgradeApplier.ApplyBonusLevels(c,1);
            Assert.That(upgraded.Actions[0].Value,Is.EqualTo(8)); Assert.That(upgraded.Actions[1].Value,Is.EqualTo(24));
        }

        [Test]
        public void UpgradedBloodBiteStillDoublesAfterHealing()
        {
            var c=new CardInstanceState {DisplayName="鲜血撕咬"};
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamageAlternateIfHealedThisTurn,Value=7,AlternateValueIfHealed=14});
            var upgraded=CardInstanceUpgradeApplier.ApplyBonusLevels(c,1);
            Assert.That(upgraded.Actions[0].Value,Is.EqualTo(8)); Assert.That(upgraded.Actions[0].AlternateValueIfHealed,Is.EqualTo(16));
        }

        [Test]
        public void WarCryUpgradeAddsTwoPercentagePoints()
        {
            var c=new CardInstanceState {DisplayName="战吼鼓舞"};
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.ApplyStatus,StatusId=StatusCatalog.AttackUpPercent,Stacks=10});
            Assert.That(CardInstanceUpgradeApplier.ApplyBonusLevels(c,1).Actions[0].Stacks,Is.EqualTo(12));
        }

        [Test]
        public void CrabRandomTargetCanReachEveryAlivePlayer()
        {
            var s=CreateState(); var e=s.Combatants[1]; e.Traits.Add(MinionTraitCatalog.CorruptedCrabPoisonOnHit);
            s.Combatants.Add(new CombatantState { Id="p2", Team=TeamSide.Player, Hp=100, MaxHp=100, Slot=FormationSlot.Middle });
            var rng=new BattleRng(123); var events=new List<BattleEvent>();
            for(var i=0;i<100;i++) MinionTraitRules.OnIncomingDamageHit(s,s.Combatants[0],e,events,rng);
            Assert.That(StatusRules.GetStatusStacks(s.Combatants[0],StatusCatalog.Poison),Is.GreaterThan(0));
            Assert.That(StatusRules.GetStatusStacks(s.Combatants[2],StatusCatalog.Poison),Is.GreaterThan(0));
        }

        [Test]
        public void UpgradedSolarBlessingAddsBlockPerEnergyPoint()
        {
            var s=CreateState(); var c=new CardInstanceState {InstanceId=1,DefinitionId=CardPowerRules.SolarBlessingCardId,UpgradeLevel=1};
            s.EnergySpentByCardInstanceId[1]=2;
            SpecialCardRules.TryResolve(s,s.Combatants[0],c,new List<BattleEvent>(),new BattleRng(1));
            Assert.That(s.Combatants[0].Block,Is.EqualTo(14));
        }

        [Test]
        public void HolyInfusionUpgradeReducesItsDynamicCost()
        {
            var s=CreateState(); s.Phase=TurnPhase.Planning; s.EnergyCurrent=10; s.EnergyMax=10;
            var previous=AddCard(s,1,2,TeamSide.Player); previous.Actions[0].Target=EffectTarget.RandomEnemy;
            var draft=new Grimhand.Battle.Planning.PlanningDraft(s,new List<BattleEvent>());
            Assert.That(draft.TrySelectCard(1),Is.True);
            var infusion=new CardInstanceState {DefinitionId=PassiveCardMechanicsRules.HolyInfusionCardId,UpgradeLevel=1};
            Assert.That(PassiveCardMechanicsRules.GetHolyInfusionPlayCost(s,draft,infusion),Is.EqualTo(2));
        }
    }
}
