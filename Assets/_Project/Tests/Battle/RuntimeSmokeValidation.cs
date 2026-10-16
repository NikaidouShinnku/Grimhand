using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Persistence;
using Grimhand.Presentation.Battle;
using Grimhand.Presentation.Camp;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    /// <summary>Editor-only, opt-in scene smoke. Never writes the player's local/cloud save.</summary>
    public static class RuntimeSmokeValidation
    {
        static BattleScreenController controller;
        static readonly List<float> milliseconds = new();
        static int lastFrame, warmup, turns;
        static double started, nextAction;
        static bool oldRunInBackground, running;

        [MenuItem("Grimhand/Validation/Start isolated runtime smoke")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying || running)
                throw new InvalidOperationException("Enter Play Mode first; do not overlap smoke runs.");
            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            controller = UnityEngine.Object.FindAnyObjectByType<BattleScreenController>();
            if (flow == null || controller == null) throw new InvalidOperationException("Game scene missing.");
            var battle = AssetDatabase.LoadAssetAtPath<BattleSetupSO>("Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            var profile = PlayerProfileFactory.CreateNew(battle, expedition);
            var saveDir = Path.Combine(Directory.GetCurrentDirectory(), "Tools/UnityValidation20261016/runtime_profile");
            Set(flow, "_saveService", new SaveService(new LocalFileSaveStorage(saveDir), SaveValidationContextBuilder.Build(expedition)));
            Set(flow, "_profile", profile);
            Set(flow, "_meta", profile.Meta);
            Set(flow, "_roster", profile.Roster);
            Set(flow, "_collection", profile.Collection);
            controller.Session.BindMetaProfile(profile);
            typeof(GameFlowController).GetMethod("OpenTrainingGround", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
            if (controller.Session.Expedition?.Run.IsTrainingGround != true)
                throw new InvalidOperationException("Training scene did not start.");
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            milliseconds.Clear();
            lastFrame = -1; warmup = 60; turns = 0;
            started = EditorApplication.timeSinceStartup;
            nextAction = started + 3;
            running = true;
            EditorApplication.update += Tick;
            Debug.Log("[RuntimeValidation] Started isolated training UI + 3 attacks; player/cloud saves untouched.");
        }

        static void Set(GameFlowController flow, string name, object value) =>
            typeof(GameFlowController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, value);

        static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) { Finish("stopped"); return; }
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (warmup > 0) warmup--;
                else milliseconds.Add(Time.unscaledDeltaTime * 1000);
                var session = controller.Session;
                var now = EditorApplication.timeSinceStartup;
                if (turns < 3 && now >= nextAction && session.CanInteractWithBattle())
                {
                    var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>("Assets/_Project/Data/Cards/Card_w_basic_slash.asset");
                    if (!session.TryAddCardToHand(card.ToTemplate())) throw new InvalidOperationException("Unable to add smoke attack.");
                    var hand = session.Engine.State.PlayerHand;
                    var hit = hand[hand.Count - 1];
                    if (!session.ToggleCard(hit.InstanceId)) throw new InvalidOperationException("Unable to select smoke attack.");
                    if (session.Engine.Draft.AwaitingTargetCardId.HasValue)
                    {
                        var ownerId = PositionRules.GetOwnerCombatantId(session.Engine.State, hit);
                        var owner = session.Engine.State.GetCombatant(ownerId);
                        var targets = CardRules.GetValidTargetCandidates(session.Engine.State, hit, owner);
                        if (targets.Count == 0 || !session.AssignTarget(targets[0].Id))
                            throw new InvalidOperationException("Unable to assign smoke target.");
                    }
                    if (!session.CommitPlan()) throw new InvalidOperationException("Unable to commit smoke attack.");
                    turns++; nextAction = now + 3;
                }
                if (turns == 3 && milliseconds.Count >= 300 && session.CanInteractWithBattle()) Finish("completed");
                else if (now - started > 90) Finish("timeout");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish("error");
            }
        }

        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Application.runInBackground = oldRunInBackground;
            running = false;
            milliseconds.Sort();
            var sum = 0f;
            foreach (var sample in milliseconds) sum += sample;
            var mean = milliseconds.Count > 0 ? sum / milliseconds.Count : 0;
            var p95 = milliseconds.Count > 0 ? milliseconds[(milliseconds.Count - 1) * 95 / 100] : 0;
            Debug.Log($"[RuntimeValidation] {result}; attacks={turns}; samples={milliseconds.Count}; meanMs={mean:F2}; p95Ms={p95:F2}; editorOnly=true");
        }
    }
}
