using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.Events;
using Grimhand.Content;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class CardPresentationConsistencyTests
    {
        [TestCase(BattleEventKind.HealApplied)]
        [TestCase(BattleEventKind.CharacterDied)]
        [TestCase(BattleEventKind.StatusApplied)]
        [TestCase(BattleEventKind.StatusExpired)]
        [TestCase(BattleEventKind.BlockGained)]
        [TestCase(BattleEventKind.CharacterRevived)]
        public void DamageBatchNeverSkipsIntermediatePresentation(BattleEventKind gap)
        {
            var events=new List<BattleEvent> {
                new(BattleEventKind.DamageApplied,"hit") {CombatantId="p",TargetId="e",CardInstanceId=1},
                new(gap,"gap"),
                new(BattleEventKind.DamageApplied,"hit") {CombatantId="p",TargetId="e",CardInstanceId=1} };
            var wave=BattleEventPlayback.CollectDamageWave(events,0);
            Assert.That(wave.damages.Count,Is.EqualTo(1)); Assert.That(wave.lastConsumed,Is.EqualTo(0));
        }

        [Test]
        public void ContiguousAoeHitsRemainOneWave()
        {
            var events=new List<BattleEvent> {
                new(BattleEventKind.DamageApplied,"hit") {CombatantId="p",TargetId="e1",CardInstanceId=1,IsAoEWave=true},
                new(BattleEventKind.DamageApplied,"hit") {CombatantId="p",TargetId="e2",CardInstanceId=1,IsAoEWave=true} };
            Assert.That(BattleEventPlayback.CollectDamageWave(events,0).damages.Count,Is.EqualTo(2));
            Assert.That(BattleEventPlayback.IsPresentationKind(BattleEventKind.StatusExpired),Is.True);
        }

        static CardInstanceState Special(string id) => new()
            { DefinitionId=id,DisplayName=id,UpgradeLevel=1 };

        [Test]
        public void HandKeepsPrintedMechanicsButShowsBuffedDamageEstimate()
        {
            var s=new BattleState();
            s.Combatants.Add(new CombatantState {Id="p",Team=TeamSide.Player,Hp=100,MaxHp=100,Slot=FormationSlot.Front});
            var c=new CardInstanceState {InstanceId=1,DefinitionId="d_devil_touch",DisplayName="恶魔之触",OwnerCombatantId="p",CardType=CardType.Attack};
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamage,Value=6});
            StatusRules.ApplyStatus(s,s.Combatants[0],StatusCatalog.AttackUpPercent,100,1,new List<BattleEvent>());
            var text=BattleUiFormatters.BuildCardStatsLineForHand(s,null,c);
            StringAssert.Contains("回复等量HP",text); StringAssert.Contains("当前预估",text); StringAssert.Contains("12",text);
        }

        [TestCase("d_pain_convert", "本回合")]
        [TestCase("v_poison_mist", "易伤")]
        [TestCase("v_poison_mist", "若")]
        [TestCase("v_queen_kiss", "转换")]
        [TestCase("v_queen_kiss", "随机")]
        [TestCase("l_ethereal_shield", "下回合")]
        [TestCase("l_psionic_arrow_rain", "随机")]
        [TestCase("p_holy_infusion", "上一张")]
        [TestCase("p_life_spring", "消耗")]
        [TestCase("p_solar_god_wrath", "13")]
        public void SpecialDescriptionKeepsEssentialMechanics(string id,string required) =>
            StringAssert.Contains(required,BattleUiFormatters.BuildCardStatsLinePreview(Special(id)));

        [Test]
        public void HealedAttackDescriptionUsesRealAlternateField()
        {
            var c=Special("d_blood_bite");
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamageAlternateIfHealedThisTurn,
                Value=8,AlternateValueIfHealed=15});
            StringAssert.Contains("15",BattleUiFormatters.BuildCardStatsLinePreview(c));
        }

        [Test]
        public void PermanentPoisonPreviewDoesNotBecomeTwoTurns()
        {
            var a=new EffectActionSpec {Type=EffectActionType.ApplyStatus,StatusId=StatusCatalog.Poison,Duration=-1};
            Assert.That(CardPreviewRules.BaselineStatusDurationTurns(a),Is.EqualTo(-1));
            Assert.That(CardPreviewRules.PreviewStatusDurationTurns(new BattleState(),a),Is.EqualTo(-1));
        }

        [Test]
        public void RepeatedRandomAttackDisplaysHitCount()
        {
            var c=Special("w_blade_storm");
            c.Actions.Add(new EffectActionSpec {Type=EffectActionType.DealDamage,Target=EffectTarget.RandomEnemy,Value=6,HitCount=5});
            StringAssert.Contains("5",BattleUiFormatters.BuildCardStatsLinePreview(c));
        }

        [Test]
        public void DefinitionBaselineIncludesKeywordsAndConditionalFields()
        {
            var def=UnityEngine.ScriptableObject.CreateInstance<CardDefinitionSO>();
            def.CardId="x"; def.DisplayName="x"; def.Cost=1;
            try
            {
            def.Actions.Add(new EffectActionDefinition {Type=EffectActionType.DealDamage,Value=5});
            var card=CardVisualResolver.CreatePreviewInstance("x","char_knight","x",def);
            card.Actions[0].HitCount=5;
            Assert.That(CardVisualResolver.MatchesDefinitionBaseline(card,def),Is.False);
            card.Actions[0].HitCount=1; card.Keywords.Add("exhaust");
            Assert.That(CardVisualResolver.MatchesDefinitionBaseline(card,def),Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void CanonicalPreviewUsesRuntimeTemplate()
        {
            var def=UnityEngine.ScriptableObject.CreateInstance<CardDefinitionSO>();
            def.CardId="m_queen_curse"; def.DisplayName="女王的诅咒"; def.Cost=1;
            try
            {
            def.Actions.Add(new EffectActionDefinition {Type=EffectActionType.DealDamage,Value=999});
            var runtime=def.ToTemplate();
            var preview=CardVisualResolver.CreatePreviewInstance(def.CardId,def.OwnerCharacterId,def.DisplayName,def);
            Assert.That(preview.Actions[0].Type,Is.EqualTo(runtime.Actions[0].Type));
            Assert.That(preview.Actions[0].Stacks,Is.EqualTo(runtime.Actions[0].Stacks));
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void TemplatePreviewDoesNotReintroduceRemovedExhaustKeyword()
        {
            var def=UnityEngine.ScriptableObject.CreateInstance<CardDefinitionSO>();
            try
            {
                def.CardId="p_sand_arrow"; def.Keywords.Add("exhaust");
                var template=new CardTemplate {DefinitionId=def.CardId,DisplayName="沙之箭"};
                var preview=CardVisualResolver.CreatePreviewInstanceFromTemplate(template,def);
                Assert.That(preview.Keywords,Does.Not.Contain("exhaust"));
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void ActionCloneAndAssetConversionPreserveEveryField()
        {
            var source=new EffectActionSpec();
            foreach(var property in typeof(EffectActionSpec).GetProperties())
            {
                if(property.PropertyType==typeof(string)) property.SetValue(source,property.Name);
                else if(property.PropertyType==typeof(bool)) property.SetValue(source,true);
                else if(property.PropertyType==typeof(int)) property.SetValue(source,17);
                else if(property.PropertyType.IsEnum) property.SetValue(source,System.Enum.ToObject(property.PropertyType,1));
            }
            var clone=EffectActionSpec.Clone(source);
            var roundTrip=EffectActionDefinition.FromSpec(source).ToSpec();
            foreach(var property in typeof(EffectActionSpec).GetProperties())
            {
                Assert.That(property.GetValue(clone),Is.EqualTo(property.GetValue(source)),property.Name+" clone");
                Assert.That(property.GetValue(roundTrip),Is.EqualTo(property.GetValue(source)),property.Name+" SO conversion");
            }
        }
    }
}
