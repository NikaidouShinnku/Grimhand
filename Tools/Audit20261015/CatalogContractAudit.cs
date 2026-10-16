using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle.Model;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    // Runs against actual checked-in YAML fixtures, not hand-authored substitute cards.
    public sealed class CatalogContractAudit
    {
        static CardTemplate Card(string id) => CatalogSmoke.Load("Assets/_Project/Data/Cards/Card_"+id+".asset");

        [TestCase("v_snake_bite",3)]
        [TestCase("v_venom_spit",3)]
        [TestCase("v_python_guard",1)]
        public void ConfirmedPoisonDurationsMatchAssets(string id,int duration) =>
            Assert.That(Card(id).Actions.Single(a=>a.StatusId==StatusCatalog.Poison).Duration,Is.EqualTo(duration));

        [TestCase("m_spider_silk")]
        [TestCase("m_tail_splash")]
        public void MonsterSlowMatchesDescriptionAndContentGenerator(string id)
        {
            var action=Card(id).Actions.Single();
            Assert.That(action.Duration,Is.EqualTo(3)); Assert.That(action.Target,Is.EqualTo(EffectTarget.RandomEnemy));
        }

        static BattleState State()
        {
            var s=new BattleState(); s.Config.HandLimit=5;
            s.Combatants.Add(new CombatantState {Id="p",Team=TeamSide.Player,Hp=60,MaxHp=100,Slot=FormationSlot.Front});
            s.Combatants.Add(new CombatantState {Id="e",Team=TeamSide.Enemy,Hp=100,MaxHp=100,Slot=FormationSlot.Front});
            return s;
        }

        [Test]
        public void RemovedRespondTypesAreAbsentFromAllActualCardsAndDescriptions()
        {
            foreach(var path in System.IO.Directory.GetFiles("Assets/_Project/Data/Cards","*.asset"))
            {
                var t=CatalogSmoke.Load(path);
                Assert.That(t.Keywords,Does.Not.Contain("respond_status"),t.DefinitionId);
                Assert.That(t.Keywords,Does.Not.Contain("respond_defense"),t.DefinitionId);
                Assert.That(t.Actions.Any(a=>a.Condition is ReactionConditionType.LastActionStatusOnTarget or ReactionConditionType.LastActionDefenseOnTarget),Is.False,t.DefinitionId);
                if(Grimhand.Content.CardDescriptionCatalog.TryGetByCardId(t.DefinitionId,out var text))
                {
                    StringAssert.DoesNotContain("应对状态",text,t.DefinitionId);
                    StringAssert.DoesNotContain("应对防御",text,t.DefinitionId);
                }
            }
        }

        [TestCase(false,12)]
        [TestCase(true,24)]
        public void BatAmbushDoublesForAnyStatusIncludingBeneficial(bool hasStatus,int expected)
        {
            var s=State(); var target=s.Combatants[0];
            if(hasStatus) StatusRules.ApplyStatus(s,target,StatusCatalog.AttackUpPercent,1,1,new List<BattleEvent>());
            var c=CatalogSmoke.Instance(Card("m_bat_ambush"),1,"e"); s.ResolutionTargets[1]="p";
            EffectActionExecutor.ExecuteAll(s,s.Combatants[1],c,new List<BattleEvent>(),new BattleRng(1));
            Assert.That(target.Hp,Is.EqualTo(60-expected));
        }

        [Test]
        public void BloodlineRaises100To150WithoutHealing()
        {
            var s=State(); var c=CatalogSmoke.Instance(Card("d_bloodline_legacy"),1,"p");
            EffectActionExecutor.ExecuteAll(s,s.Combatants[0],c,new List<BattleEvent>(),new BattleRng(1));
            Assert.That(s.Combatants[0].MaxHp,Is.EqualTo(150)); Assert.That(s.Combatants[0].Hp,Is.EqualTo(60));
        }

        [Test]
        public void ChaosActuallyDrawsToHandLimitBeforeShuffling()
        {
            var s=State();
            for(var i=0;i<8;i++) s.PlayerDrawPile.Add(new CardInstanceState {InstanceId=i+2,Cost=i%4,OwnerCombatantId="p"});
            var c=CatalogSmoke.Instance(Card("l_summon_chaos_spirit"),1,"p");
            EffectActionExecutor.ExecuteAll(s,s.Combatants[0],c,new List<BattleEvent>(),new BattleRng(1));
            Assert.That(s.PlayerHand.Count,Is.EqualTo(5));
            foreach(var drawn in s.PlayerHand) Assert.That(drawn.CostBeforeHandDiscount,Is.Not.Null);
        }

        [Test]
        public void QueenDrainAndGargoylePenaltyAffectDifferentTeams()
        {
            var s=State(); var events=new List<BattleEvent>();
            EffectActionExecutor.ExecuteAll(s,s.Combatants[1],CatalogSmoke.Instance(Card("m_queen_soul_drain"),1,"e"),events,new BattleRng(1));
            Assert.That(s.PendingPlayerEnergyRegenPenaltyNextTurn,Is.EqualTo(2));
            Assert.That(s.PendingEnemyEnergyRegenPenaltyNextTurn,Is.Zero);
            EffectActionExecutor.ExecuteAll(s,s.Combatants[1],CatalogSmoke.Instance(Card("m_gargoyle_empower"),2,"e"),events,new BattleRng(1));
            Assert.That(s.PendingEnemyEnergyRegenPenaltyNextTurn,Is.EqualTo(2));
            Assert.That(s.PendingPlayerEnergyRegenPenaltyNextTurn,Is.EqualTo(2));
        }

        [Test]
        public void VortexPoisonsExactlyTheTwoSwappedPlayers()
        {
            for(var seed=1;seed<20;seed++)
            {
                var s=State(); s.Combatants.Add(new CombatantState {Id="p2",Team=TeamSide.Player,Hp=100,MaxHp=100,Slot=FormationSlot.Middle});
                s.Combatants.Add(new CombatantState {Id="p3",Team=TeamSide.Player,Hp=100,MaxHp=100,Slot=FormationSlot.Back});
                var before=s.Combatants.ToDictionary(c=>c.Id,c=>c.Slot);
                EffectActionExecutor.ExecuteAll(s,s.Combatants[1],CatalogSmoke.Instance(Card("m_ocean_vortex"),1,"e"),new List<BattleEvent>(),new BattleRng(seed));
                foreach(var unit in s.GetTeam(TeamSide.Player))
                    Assert.That(StatusRules.GetStatusStacks(unit,StatusCatalog.Poison),Is.EqualTo(unit.Slot==before[unit.Id]?0:5));
            }
        }

        [TestCase("p_rot_avatar",StatusCatalog.RotAvatar,3)]
        [TestCase("v_venom_sac_burst",StatusCatalog.VenomSacBurst,2)]
        [TestCase("l_psionic_body",StatusCatalog.PsionicBody,22)]
        public void PassiveUpgradeChangesRealEffect(string id,string status,int expected)
        {
            var t=Card(id); CardInstanceUpgradeApplier.ApplyToTemplate(t,1);
            var s=State(); EffectActionExecutor.ExecuteAll(s,s.Combatants[0],CatalogSmoke.Instance(t,1,"p"),new List<BattleEvent>(),new BattleRng(1));
            if(status==StatusCatalog.RotAvatar)
            {
                PassiveCardMechanicsRules.TryTriggerRotAvatarOnTurnStart(s,new List<BattleEvent>());
                Assert.That(StatusRules.GetStatusStacks(s.Combatants[1],StatusCatalog.Poison),Is.EqualTo(expected));
            }
            else if(status==StatusCatalog.VenomSacBurst)
                Assert.That(Grimhand.Battle.V09.V09NewMechanicsRules.AdjustPoisonStacksForVenomSac(s.Combatants[0],StatusCatalog.Poison,0),Is.EqualTo(expected));
            else
            {
                s.Phase=TurnPhase.Draw;
                Assert.That(Grimhand.Battle.V09.V09NewMechanicsRules.ApplyPsionicBodyBonus(s,TeamSide.Player,100),Is.EqualTo(100+expected));
            }
        }
    }
}
