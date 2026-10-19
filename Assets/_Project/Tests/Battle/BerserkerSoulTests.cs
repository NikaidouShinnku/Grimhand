using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class BerserkerSoulTests
    {
        static BattleEngine Create()
        {
            var config = new BattleConfig { ManualEnemyIntentsOnly = true, CardsDrawnPerTurn = 0 };
            config.Combatants.Add(new CombatantConfig { Id = "hero", CharacterDefinitionId = "char_berserker", Team = TeamSide.Player, MaxHp = 76, Speed = 5 });
            config.Combatants.Add(new CombatantConfig { Id = "enemy", Team = TeamSide.Enemy, MaxHp = 1000, Speed = 1 });
            config.Combatants.Add(new CombatantConfig { Id = "enemy2", Team = TeamSide.Enemy, Slot = FormationSlot.Middle, MaxHp = 1000, Speed = 1 });
            var engine = new BattleEngine(config);engine.StartBattle();engine.State.EnergyCurrent = 20;
            BerserkerSoulRules.Activate(engine.State, engine.State.GetCombatant("hero"), new List<BattleEvent>());
            return engine;
        }

        static CardInstanceState Add(BattleEngine engine, int id = 100, int cost = 3)
        {
            var c = new CardInstanceState { InstanceId = id, DefinitionId = "test_attack", OwnerCharacterId = "char_berserker", OwnerCombatantId = "hero", DisplayName = "重击", Cost = cost, BaseCost = 3, CardType = CardType.Attack };
            c.Actions.Add(new EffectActionSpec { Type = EffectActionType.DealDamage, Value = 24, Target = EffectTarget.DefaultEnemy });
            engine.State.CardsById.Add(id,c);engine.State.PlayerHand.Add(c);return c;
        }

        static void Select(BattleEngine e, CardInstanceState c, string target = "enemy")
        {
            Assert.IsTrue(e.Draft.TrySelectCard(c.InstanceId));
            Assert.IsTrue(e.Draft.TryAssignTargetAndSelect(target));
        }
        static CardInstanceState Ghost(BattleEngine e) => e.Draft.GetDisplayedHand().Single(c=>c.SpectralRepeatSourceId>0);

        [Test] public void FirstAttackCreatesOnlyOneIndependentSpectralPlay()
        {
            var e=Create();var a=Add(e);var b=Add(e,101);Select(e,a);var g=Ghost(e);Select(e,b);
            Assert.AreEqual(a.InstanceId,Ghost(e).SpectralRepeatSourceId);Assert.IsFalse(e.State.PlayerHand.Contains(g));
            Assert.IsTrue(e.Draft.IsCardSelectable(g.InstanceId));Select(e,g,"enemy2");
            Assert.AreEqual("enemy",e.Draft.GetAssignedTarget(a.InstanceId));Assert.AreEqual("enemy2",e.Draft.GetAssignedTarget(g.InstanceId));
            Assert.AreEqual(11,e.State.EnergyCurrent);Assert.AreEqual(3,e.Draft.CommitToPlan().PlayQueue.Count);
        }
        [Test] public void DeselectOriginalRemovesGhostAndRefundsBoth()
        {
            var e=Create();var a=Add(e);Select(e,a);var g=Ghost(e);Select(e,g);
            Assert.IsTrue(e.Draft.TryDeselectCard(a.InstanceId));Assert.AreEqual(20,e.State.EnergyCurrent);
            Assert.IsNull(e.State.GetCard(g.InstanceId));Assert.AreEqual(0,e.Draft.SelectedQueue.Count);
        }
        [Test] public void DeselectGhostKeepsOriginalAndRefundsOnlySecondPayment()
        {
            var e=Create();var a=Add(e);Select(e,a);var g=Ghost(e);Select(e,g);
            Assert.IsTrue(e.Draft.TryDeselectCard(g.InstanceId));Assert.AreEqual(17,e.State.EnergyCurrent);
            Assert.IsTrue(e.Draft.IsSelected(a.InstanceId));Assert.IsTrue(e.Draft.IsCardSelectable(g.InstanceId));
        }
        [Test] public void RepeatDoesNotInheritTemporaryZeroCost()
        {
            var e=Create();var a=Add(e,cost:0);a.CostBeforeHandDiscount=3;Select(e,a);
            Assert.AreEqual(3,Ghost(e).Cost);Assert.AreEqual(20,e.State.EnergyCurrent);
        }
        [Test] public void ChangingFirstAttackRebuildsGhostAndCancelsItsPendingTarget()
        {
            var e=Create();var a=Add(e);var b=Add(e,101);Select(e,a);Select(e,b);var g=Ghost(e);
            e.Draft.TrySelectCard(g.InstanceId);Assert.IsNotNull(e.Draft.AwaitingTargetCardId);
            e.Draft.TryDeselectCard(a.InstanceId);Assert.IsNull(e.Draft.AwaitingTargetCardId);
            Assert.AreEqual(b.InstanceId,Ghost(e).SpectralRepeatSourceId);
        }
        [Test] public void ExhaustAndQuickStartAreNotRepeatCandidates()
        {
            var e=Create();var a=Add(e);a.Keywords.Add("exhaust");Select(e,a);
            Assert.IsFalse(e.Draft.GetDisplayedHand().Any(c=>c.SpectralRepeatSourceId>0));
            a.Keywords.Clear();a.Keywords.Add("quick_start");Assert.IsFalse(BerserkerSoulRules.IsEligible(a));
        }
        [Test] public void RepeatCannotCreateAnotherRepeat()
        {
            var e=Create();var a=Add(e);Select(e,a);var g=Ghost(e);Select(e,g);
            Assert.AreEqual(1,e.Draft.GetDisplayedHand().Count(c=>c.SpectralRepeatSourceId>0));
        }
        [Test] public void BothUsesResolveButOnlyOneRealCardEntersDiscard()
        {
            var e=Create();var a=Add(e);Select(e,a);var g=Ghost(e);Select(e,g,"enemy2");
            Assert.IsTrue(e.CommitPlayerPlan());
            Assert.AreEqual(976,e.State.GetCombatant("enemy").Hp);Assert.AreEqual(976,e.State.GetCombatant("enemy2").Hp);
            Assert.AreEqual(1,e.State.PlayerDiscardPile.Count(c=>c.InstanceId==a.InstanceId));
            Assert.IsFalse(e.State.PlayerDiscardPile.Any(c=>c.SpectralRepeatSourceId>0));
            Assert.IsFalse(e.State.PlayerExhaustPile.Any(c=>c.SpectralRepeatSourceId>0));
        }
        [Test] public void InheritRetainsOnlyUnplayedAttacks()
        {
            var e=Create();var a=Add(e);BerserkerSoulRules.Activate(e.State,e.State.GetCombatant("hero"),new List<BattleEvent>());
            DeckRules.DiscardHandAtEndOfTurn(e.State,TeamSide.Player,new List<BattleEvent>());Assert.Contains(a,e.State.PlayerHand);
            DeckRules.MovePlayedCardToDiscard(e.State,TeamSide.Player,a,new List<BattleEvent>());
            Assert.IsFalse(e.State.PlayerHand.Contains(a));Assert.Contains(a,e.State.PlayerDiscardPile);
        }
        [Test] public void SoulBlocksOwnDefenseButDoesNotBlockOtherOwners()
        {
            var e=Create();var card=Add(e);card.CardType=CardType.Defense;
            Assert.IsFalse(e.Draft.IsCardSelectable(card.InstanceId));
            Assert.IsFalse(BerserkerSoulRules.BlocksDefense(new CombatantState(),card));
        }
        [Test] public void NoEnergyPreventsSecondSelectionAndDoesNotDuplicateDeck()
        {
            var e=Create();var a=Add(e);e.State.EnergyCurrent=3;Select(e,a);var g=Ghost(e);
            Assert.IsFalse(e.Draft.IsCardSelectable(g.InstanceId));Assert.IsFalse(e.Draft.TrySelectCard(g.InstanceId));
            Assert.AreEqual(1,e.State.PlayerHand.Count);Assert.AreEqual(1,e.Draft.SelectedQueue.Count);
        }
        [Test] public void TurnCleanupRemovesTransientCardsAndResolutionHistory()
        {
            var e=Create();var a=Add(e);Select(e,a);var g=Ghost(e);BerserkerSoulRules.RecordResolved(e.State,a);
            BerserkerSoulRules.ClearTurn(e.State);Assert.IsNull(e.State.GetCard(g.InstanceId));
            Assert.AreEqual(0,e.State.BerserkerResolvedAttackIdsThisTurn.Count);
        }
        [Test] public void CardsCreatedAfterActivationAlsoReceiveInherit()
        {
            var e=Create();var c=e.AddCardTemplateToHand(BerserkerCardActions.Create("b_split_helm"));
            Assert.IsTrue(CardRules.HasInheritKeyword(c));
        }
        [Test] public void SpectralPlayReadsCurrentSourceActionsButNeverGetsTemporaryHandDiscount()
        {
            var e=Create();var c=Add(e,cost:0);c.CostBeforeHandDiscount=3;Select(e,c);var g=Ghost(e);
            c.Actions[0].Value=40;c.UpgradeLevel=2;e.Draft.RevalidateAfterQuickStart();
            Assert.AreEqual(40,g.Actions[0].Value);Assert.AreEqual(2,g.UpgradeLevel);Assert.AreEqual(3,g.Cost);
        }
        [Test] public void QuickStartCanUseUnreservedBannerRageAndDoesNotStealReservedRage()
        {
            var e=Create();var b=BerserkerRules.Get(e.State,e.State.GetCombatant("hero"));b.Rage=4;b.BannerStartTurn=1;e.State.EnergyCurrent=0;
            Assert.IsTrue(e.Draft.TrySetUseRageForCosts(true));var c=e.AddCardTemplateToHand(BerserkerCardActions.Create("b_hold_anger"));
            Assert.IsTrue(e.TryResolveQuickStartCard(c.InstanceId));Assert.AreEqual(0,e.State.EnergyCurrent);Assert.AreEqual(4,b.Rage);Assert.AreEqual(0,e.State.BerserkerRagePayments.Count);
            Assert.Contains(c,e.State.PlayerExhaustPile);
        }
        [Test,Timeout(2000)] public void CleanupFollowedByRefundCannotLeaveAnOrphanSpectralSelection()
        {
            var e=Create();var c=Add(e);Select(e,c);Select(e,Ghost(e));BerserkerSoulRules.ClearTurn(e.State);
            e.Draft.RefundAllSelections();Assert.AreEqual(20,e.State.EnergyCurrent);Assert.AreEqual(0,e.Draft.SelectedQueue.Count);
        }
    }
}
