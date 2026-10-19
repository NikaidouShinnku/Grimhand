using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public sealed class BerserkerPolishTests
    {
        [TestCase(19,true,false,false,false)][TestCase(20,false,false,false,false)]
        [TestCase(20,true,true,false,false)][TestCase(20,true,false,true,false)]
        [TestCase(20,true,false,false,true)]
        public void UnlockRequiresFloor20BossVictory(int floor,bool boss,bool training,bool tutorial,bool expected)
        {
            var meta=CampMetaState.CreateNewProfile();Assert.IsFalse(CharacterUnlockRules.IsUnlocked(meta,TalentCatalog.BerserkerId));
            Assert.AreEqual(expected,CharacterUnlockRules.RecordVictory(meta,floor,boss,training,tutorial,"char_skeleton_king"));
            Assert.AreEqual(expected,CharacterUnlockRules.IsUnlocked(meta,TalentCatalog.BerserkerId));
            Assert.IsFalse(CharacterUnlockRules.RecordVictory(meta,19,true,false,false));
        }
        [Test] public void UnlockSurvivesSaveRoundTripAndDoesNotFollowMetaEntryPresence()
        {
            var profile=new PlayerProfileState{Meta=CampMetaState.CreateNewProfile()};
            Assert.IsFalse(CodexProgressRules.HasOwnedCharacter(profile,TalentCatalog.BerserkerId));
            profile.Meta.BerserkerUnlocked=true;
            var dto=SaveDataMapper.ToDto(profile);SaveIntegrity.ApplyHash(dto);Assert.IsTrue(SaveIntegrity.Verify(dto));
            Assert.IsTrue(SaveDataMapper.FromDto(dto).Meta.BerserkerUnlocked);
            dto.berserkerUnlocked=false;Assert.IsFalse(SaveIntegrity.Verify(dto));
        }
        [Test] public void LegacySaveSignatureRemainsValidWithoutNewField()
        {
            var dto=new PlayerProfileSaveData{saveVersion=5};
            var json=JsonUtility.ToJson(dto);
            // Reconstruct the original v5 payload independently of SaveIntegrity:
            // remove the appended v8 suffix before the older unlock fields.
            json=json.Substring(0,json.LastIndexOf(",\"achievementIds\":",System.StringComparison.Ordinal))+"}";
            json=json.Replace(",\"lichUnlocked\":false}","}").Replace(",\"berserkerUnlocked\":false}","}");
            using var hmac=new HMACSHA256(Encoding.UTF8.GetBytes("Grimhand.Save.v1.20260710"));
            dto.integrityHash=System.Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));
            Assert.IsTrue(SaveIntegrity.Verify(dto));Assert.IsTrue(SaveDataMapper.FromDto(dto).Meta.BerserkerUnlocked);Assert.IsTrue(SaveDataMapper.FromDto(dto).Meta.LichUnlocked);
        }
        [Test] public void EightTalentsAndRemovedSelectionsArePruned()
        {
            Assert.AreEqual(4,TalentCatalog.GetSlotTalents(TalentCatalog.BerserkerId,1).Count);
            Assert.AreEqual(4,TalentCatalog.GetSlotTalents(TalentCatalog.BerserkerId,2).Count);
            var p=new CharacterMetaProgress{CharacterDefinitionId=TalentCatalog.BerserkerId,OutOfRunLevel=10,SelectedSlot1TalentId="talent_berserker_s1_lv5",SelectedSlot2TalentId="talent_berserker_s2_lv2"};
            TalentRules.PruneInvalidSelections(p);Assert.IsEmpty(p.SelectedSlot1TalentId);Assert.IsEmpty(p.SelectedSlot2TalentId);
        }
        [Test] public void LockedCharacterCannotAppearInAnyMetaPack()
        {
            var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset").ToExpeditionConfig();
            var meta=CampMetaState.CreateNewProfile();var ids=CharacterUnlockRules.CollectUnlocked(meta,TalentCatalog.PlayableCharacterIds);
            Assert.AreEqual(4,ids.Count);
            foreach(var pack in new[]{CardPackIds.Common,CardPackIds.Advanced,CardPackIds.Master})
                for(var seed=1;seed<=100;seed++)
                {
                    var choices=CardPackRoller.RollMetaChoices(pack,config,ids,new Grimhand.Core.BattleRng(seed));
                    Assert.IsNotEmpty(choices);Assert.IsFalse(choices.Any(c=>c.OwnerCharacterId==TalentCatalog.BerserkerId||c.Template.OwnerCharacterId==TalentCatalog.BerserkerId));
                }
            CharacterUnlockRules.RecordVictory(meta,20,true,false,false,"char_skeleton_king");
            CharacterUnlockRules.RecordVictory(meta,20,true,false,false,"char_ghost_queen");
            Assert.AreEqual(6,CharacterUnlockRules.CollectUnlocked(meta,TalentCatalog.PlayableCharacterIds).Count);
            Assert.IsTrue(CardPackRoller.RollMetaChoices(CardPackIds.Common,config,new[]{TalentCatalog.BerserkerId},new Grimhand.Core.BattleRng(1)).All(c=>c.OwnerCharacterId==TalentCatalog.BerserkerId));
        }
        [TestCase(true,"char_skeleton_king")][TestCase(false,"char_skeleton_king")]
        [TestCase(true,"char_ghost_queen")][TestCase(false,"char_ghost_queen")]
        public void ExpeditionBattleResultActuallyUpdatesUnlock(bool won,string bossId)
        {
            var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset").ToExpeditionConfig();
            var profile=new PlayerProfileState{Meta=CampMetaState.CreateNewProfile()};
            var engine=new ExpeditionEngine(config){MetaProfile=profile};engine.StartRun();
            engine.Run.LastBattleFloor=20;engine.Run.LastBattleWasBoss=true;
            var state=new BattleState{Outcome=won?BattleOutcome.PlayerVictory:BattleOutcome.PlayerDefeat};
            state.Combatants.Add(new CombatantState{Id="p1",Team=TeamSide.Player,CharacterDefinitionId=TalentCatalog.KnightId,Hp=30,MaxHp=40});
            state.Combatants.Add(new CombatantState{Id="boss",Team=TeamSide.Enemy,CharacterDefinitionId=bossId,Hp=won?0:100,MaxHp=100});
            engine.OnBattleFinished(state);
            Assert.AreEqual(won&&bossId=="char_skeleton_king",profile.Meta.BerserkerUnlocked);
            Assert.AreEqual(won&&bossId=="char_ghost_queen",profile.Meta.LichUnlocked);
        }
        [Test] public void BlockFaceHasOneNumberAndColorsChangesWithoutDesignTooltip()
        {
            var state=new BattleState();var owner=new CombatantState{Id="b",CharacterDefinitionId=TalentCatalog.BerserkerId,Team=TeamSide.Player,Hp=76,MaxHp=76};state.Combatants.Add(owner);
            var template=BerserkerCardActions.Create("b_iron_guard");
            var card=new CardInstanceState{DefinitionId=template.DefinitionId,OwnerCombatantId="b",OwnerCharacterId=TalentCatalog.BerserkerId,CardType=CardType.Defense};card.Actions.AddRange(template.Actions);
            Assert.AreEqual("获得10护甲。",BattleUiFormatters.BuildCardStatsLineForHand(state,null,card));
            StringAssert.DoesNotContain("狂怒",BattleUiFormatters.BuildCardKeywordTooltip(state,card));
            StringAssert.DoesNotContain("预计",BattleUiFormatters.BuildCardStatsLineForHand(state,null,card));
            var ev=new System.Collections.Generic.List<Grimhand.Battle.Events.BattleEvent>();
            StatusRules.ApplyStatus(state,owner,Grimhand.Battle.Status.StatusCatalog.ArmorUp,5,2,ev);
            StringAssert.Contains("<color=#1F9E38>15</color>",BattleUiFormatters.BuildCardStatsLineForHand(state,null,card));
            owner.Statuses.Clear();
            StatusRules.ApplyStatus(state,owner,Grimhand.Battle.Status.StatusCatalog.ArmorDown,50,2,ev);
            StringAssert.Contains("<color=#D12E2E>5</color>",BattleUiFormatters.BuildCardStatsLineForHand(state,null,card));
            Assert.IsFalse(string.IsNullOrEmpty(CharacterLoreCatalog.GetTooltipBody(TalentCatalog.BerserkerId)));
        }
        [TestCase(true)][TestCase(false)]
        public void GifCacheRebuildsDestroyedPlayModeFrames(bool fromFile)
        {
            const string relative="The Grimhands Asset/characters/berserker/berserker_anim.gif";
            var bytes=System.IO.File.ReadAllBytes("Assets/"+relative);
            System.Collections.Generic.IReadOnlyList<Sprite> Read()=>fromFile
                ?Grimhand.Content.IdleAnimationGifLoader.GetSprites(relative,100)
                :Grimhand.Content.IdleAnimationGifLoader.GetSpritesFromBytes(bytes,100,"destroyed-frame-regression");
            var previous=Read();Assert.AreEqual(8,previous.Count);
            UnityEngine.Object.DestroyImmediate(previous[0]);
            var rebuilt=Read();Assert.AreEqual(8,rebuilt.Count);Assert.IsTrue(rebuilt.All(s=>s!=null&&s.texture!=null));
            var textures=new System.Collections.Generic.HashSet<Texture2D>();
            foreach(var sprite in previous.Concat(rebuilt))if(sprite!=null){textures.Add(sprite.texture);UnityEngine.Object.DestroyImmediate(sprite);}
            foreach(var texture in textures)if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
