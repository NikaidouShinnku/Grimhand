using System;
using System.IO;
using System.Reflection;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation.Camp;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    /// <summary>Opt-in scene integration using only a dedicated project-local profile.</summary>
    public static class AchievementRuntimeValidation
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("Grimhand/Validation/Check isolated achievements")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            var flow=UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            if(flow==null)throw new InvalidOperationException("GameFlow missing.");
            var battle=AssetDatabase.LoadAssetAtPath<BattleSetupSO>("Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var setup=AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            var profile=PlayerProfileFactory.CreateNew(battle,setup);
            var path=Path.GetFullPath("Tools/UnityValidation20261018/achievement-runtime-profile");
            var storage=new LocalFileSaveStorage(path);
            var saves=new SaveService(storage,SaveValidationContextBuilder.Build(setup));
            Set(flow,"_saveService",saves);Set(flow,"_profile",profile);Set(flow,"_meta",profile.Meta);
            Set(flow,"_roster",profile.Roster);Set(flow,"_collection",profile.Collection);
            Set(flow,"_trackingExpedition",false);Set(flow,"_trackingTrainingGround",false);
            var run=new ExpeditionRunState { Phase=ExpeditionPhase.RunFailed };
            run.WasAbandoned=true;
            typeof(GameFlowController).GetMethod("FinalizeExpeditionRun",Flags).Invoke(flow,new object[]{run});
            if(profile.Achievements.Unlocked.Contains("ACH_FINISH_EXPEDITION"))throw new InvalidOperationException("Abandonment incorrectly qualified.");
            run.WasAbandoned=false;
            typeof(GameFlowController).GetMethod("FinalizeExpeditionRun",Flags).Invoke(flow,new object[]{run});
            if(!profile.Achievements.Unlocked.Contains("ACH_FINISH_EXPEDITION"))throw new InvalidOperationException("Defeat did not qualify.");
            profile.Meta.GetOrCreate(TalentCatalog.BerserkerId).OutOfRunLevel=MetaProgressionRules.MaxOutOfRunLevel;
            flow.SaveProfile();
            if(!profile.Achievements.Unlocked.Contains("ACH_ONE_CHARACTER_MAX"))throw new InvalidOperationException("Save did not reconcile camp level.");
            if(!storage.TryReadPrimary(out var json))throw new InvalidOperationException("Isolated save missing.");
            var dto=JsonUtility.FromJson<PlayerProfileSaveData>(json);
            if(!SaveIntegrity.Verify(dto))throw new InvalidOperationException("Achievement save signature invalid.");
            var restored=SaveDataMapper.FromDto(dto);
            if(!restored.Achievements.Unlocked.Contains("ACH_FINISH_EXPEDITION")||!restored.Achievements.Unlocked.Contains("ACH_ONE_CHARACTER_MAX"))
                throw new InvalidOperationException("Achievement round trip lost progress.");
            if(!string.IsNullOrEmpty(profile.Achievements.SteamAccountId))throw new InvalidOperationException("Editor claimed a real Steam owner.");
            Debug.Log("[AchievementValidation] Real GameFlow settlement/save passed: abandonment rejected, defeat accepted, camp level reconciled, signed v8 round-trip preserved; isolated profile only, no Steam submissions.");
        }
        static void Set(GameFlowController flow,string name,object value)=>typeof(GameFlowController).GetField(name,Flags).SetValue(flow,value);
    }
}
