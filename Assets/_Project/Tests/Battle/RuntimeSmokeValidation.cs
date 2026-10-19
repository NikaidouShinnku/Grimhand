using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Grimhand.Battle.Events;
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
        static bool berserker;
        static int berserkerStage, hpBefore;

        [MenuItem("Grimhand/Validation/Start isolated runtime smoke")]
        public static void Start()
        {
            StartInternal(false);
        }

        [MenuItem("Grimhand/Validation/Start isolated berserker smoke")]
        public static void StartBerserker() => StartInternal(true);

        [MenuItem("Grimhand/Validation/Check isolated formal expedition start")]
        public static void CheckFormalExpedition()
        {
            // In an unfocused editor the first player update can still be pending
            // when an MCP menu command arrives immediately after entering Play Mode.
            var previousBackground=Application.runInBackground;
            Application.runInBackground=true;
            var deadline=EditorApplication.timeSinceStartup+30;
            void WhenReady()
            {
                var screen=UnityEngine.Object.FindAnyObjectByType<BattleScreenController>();
                if(screen?.Session?.ExpeditionSetup==null)
                {
                    if(EditorApplication.timeSinceStartup<deadline)return;
                    EditorApplication.update-=WhenReady;Application.runInBackground=previousBackground;
                    Debug.LogError("[RuntimeValidation] GameFlow initialization timed out.");return;
                }
                EditorApplication.update-=WhenReady;
                try{CheckFormalExpeditionReady();}
                catch(Exception error){Debug.LogException(error);}
                finally{Application.runInBackground=previousBackground;}
            }
            EditorApplication.update+=WhenReady;
        }
        static void CheckFormalExpeditionReady()
        {
            StartInternal(false);
            Finish("training-preflight");
            var flow=UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(GameFlowController).GetMethod("OpenPortal",flags).Invoke(flow,null);
            var portal=(PortalOverlayView)typeof(GameFlowController).GetField("portalOverlay",flags).GetValue(flow);
            var button=(UnityEngine.UI.Button)typeof(PortalOverlayView).GetField("_startButton",flags).GetValue(portal);
            button.onClick.Invoke();
            var run=controller.Session.Expedition?.Run;
            if(run==null||run.IsTrainingGround||run.Map==null||!controller.Session.IsExpeditionMode)
                throw new InvalidOperationException("Formal expedition failed to start through the real portal button.");
            Debug.Log("[RuntimeValidation] Formal expedition portal button passed: "+run.Phase+"; player/cloud saves untouched.");
            var profile=(PlayerProfileState)typeof(GameFlowController).GetField("_profile",flags).GetValue(flow);
            var original=profile.Roster.Members[0];
            profile.Roster.Members[0]=CampRosterBuilder.CreateEmptyMember(AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>("Assets/_Project/Data/Characters/Character_Berserker.asset"));
            typeof(GameFlowController).GetMethod("OpenPortal",flags).Invoke(flow,null);
            button.onClick.Invoke();
            var camp=(CampScreenView)typeof(GameFlowController).GetField("campScreen",flags).GetValue(flow);
            var toast=(GameObject)typeof(CampScreenView).GetField("_toastPanel",flags).GetValue(camp);
            if(portal.IsOpen||toast==null||!toast.activeInHierarchy)
                throw new InvalidOperationException("Locked roster failure did not show a visible explanation.");
            profile.Roster.Members[0]=original;
            Debug.Log("[RuntimeValidation] Locked roster returns to camp with visible explanation; no save migration or unlock changes.");
        }

        static void StartInternal(bool useBerserker)
        {
            if (!EditorApplication.isPlaying || running)
                throw new InvalidOperationException("Enter Play Mode first; do not overlap smoke runs.");
            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            controller = UnityEngine.Object.FindAnyObjectByType<BattleScreenController>();
            if (flow == null || controller == null) throw new InvalidOperationException("Game scene missing.");
            var battle = AssetDatabase.LoadAssetAtPath<BattleSetupSO>("Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            var profile = PlayerProfileFactory.CreateNew(battle, expedition);
            berserker = useBerserker; berserkerStage = 0;
            if (berserker)
            {
                profile.Meta.BerserkerUnlocked = true;
                var hero = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>("Assets/_Project/Data/Characters/Character_Berserker.asset");
                if (hero == null) throw new InvalidOperationException("Install berserker content first.");
                profile.Roster.Members[0] = CampRosterBuilder.CreateEmptyMember(hero);
            }
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
                if (berserker)
                {
                    if (now-started>120) { Finish("berserker-timeout"); return; }
                    if (now>=nextAction && !session.PresentationLocked
                        && (session.CanInteractWithBattle() || berserkerStage>=6 && BerserkerRules.PendingRecall(session.Engine.State)!=null || berserkerStage>=8 && session.Engine.State.AwaitingPsionicScry)) TickBerserker(session,now);
                    return;
                }
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
            Debug.Log($"[RuntimeValidation] {result}; berserker={berserker}; stage={berserkerStage}; attacks={turns}; samples={milliseconds.Count}; meanMs={mean:F2}; p95Ms={p95:F2}; editorOnly=true");
        }

        static void TickBerserker(BattleSession session,double now)
        {
            var state=session.Engine.State;
            if(berserkerStage==2)
            {
                Directory.CreateDirectory("Tools/UnityValidation20261018");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Tools/UnityValidation20261018/berserker-spectral-planning.png"));
                berserkerStage++;nextAction=now+2;return;
            }
            if(berserkerStage==3){if(!session.CommitPlan())throw new InvalidOperationException("Spectral plan commit failed");turns++;berserkerStage++;nextAction=now+3;return;}
            if(berserkerStage==4 && hpBefore-EnemyHp(state)!=68)throw new InvalidOperationException("Rage attacks did not resolve as 39 + 29");
            if(berserkerStage==6)
            {
                if(hpBefore-EnemyHp(state)!=48)throw new InvalidOperationException("Banner repeated attacks must be 24 + 24, not rage-enhanced");
                if(BerserkerRules.PendingRecall(state)==null)throw new InvalidOperationException($"Recall choices did not open: turn={state.TurnNumber}, hand={state.PlayerHand.Count}/{state.Config.HandLimit}, discard={state.PlayerDiscardPile.Count}, requests={state.BerserkerRecalls.Count}, attacks={state.PlayerDiscardPile.Count(c=>c.OwnerCharacterId==BerserkerSoulRules.CharacterId&&c.CardType==CardType.Attack)}, owner={heroIdForLog(state)}");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Tools/UnityValidation20261018/berserker-recall-choices.png"));
                berserkerStage++;nextAction=now+2;return;
            }
            if(berserkerStage==7)
            {
                var root=UnityEngine.Object.FindAnyObjectByType<BattleScreenView>().transform.Find("ExpeditionMapOverlayLayer/BerserkerRecall");
                var view=root.GetComponentsInChildren<CardView>().First(v=>v.gameObject.activeInHierarchy);
                var id=view.InstanceId;
                UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                if(!state.PlayerHand.Any(c=>c.InstanceId==id&&c.Cost==0)||BerserkerRules.PendingRecall(state)!=null||!session.CanInteractWithBattle())throw new InvalidOperationException("Recall UI click did not move real card into hand");
                berserkerStage=8;nextAction=now+1;return;
            }
            if(berserkerStage==8)
            {
                foreach(var c in state.PlayerHand.Take(3).ToArray()){state.PlayerHand.Remove(c);state.PendingPsionicScryCards.Add(c);}
                state.AwaitingPsionicScry=true;
                UnityEngine.Object.FindAnyObjectByType<BattleScreenView>().Refresh();
                berserkerStage=9;nextAction=now+2;return;
            }
            if(berserkerStage==9)
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Tools/UnityValidation20261018/psionic-scry-material-ui.png"));
                var scryRoot=UnityEngine.Object.FindAnyObjectByType<BattleScreenView>().transform.Find("ExpeditionMapOverlayLayer/PsionicScryOverlay");
                if(!scryRoot.gameObject.activeInHierarchy)throw new InvalidOperationException("Scry window is hidden.");
                berserkerStage=10;nextAction=now+10;return;
            }
            if(berserkerStage==10)
            {
                var root=UnityEngine.Object.FindAnyObjectByType<BattleScreenView>().transform.Find("ExpeditionMapOverlayLayer/PsionicScryOverlay");
                root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="确认").onClick.Invoke();
                if(state.AwaitingPsionicScry)throw new InvalidOperationException("Scry material button failed.");
                var views=UnityEngine.Object.FindObjectsByType<CombatantSlotView>(FindObjectsSortMode.None)
                    .Where(v=>!string.IsNullOrEmpty(v.CombatantId)).ToArray();
                var playerFoot=views.First(v=>state.GetCombatant(v.CombatantId)?.Team==TeamSide.Player).GetFeetWorldPosition().y;
                foreach(var enemy in views.Where(v=>state.GetCombatant(v.CombatantId)?.Team==TeamSide.Enemy))
                    if(Math.Abs(enemy.GetFeetWorldPosition().y-playerFoot)>1f)
                        throw new InvalidOperationException("Enemy feet drifted after poses/target selection.");
                Finish("berserker-completed");return;
            }
            var hero=state.Combatants.Find(c=>c.CharacterDefinitionId==BerserkerSoulRules.CharacterId);
            if(hero==null)throw new InvalidOperationException("Berserker missing from training party");
            var events=new List<BattleEvent>();
            foreach(var old in new List<CardInstanceState>(state.PlayerHand))DeckRules.MovePlayedCardToDiscard(state,TeamSide.Player,old,events);
            state.EnergyCurrent=8;
            if(berserkerStage==0){SelectNew(session,"b_berserker_soul");if(!session.CommitPlan())throw new InvalidOperationException("Soul commit failed");}
            else if(berserkerStage==1)
            {
                var slots=UnityEngine.Object.FindObjectsByType<CombatantSlotView>(FindObjectsSortMode.None)
                    .Where(v=>state.GetCombatant(v.CombatantId)?.Team==TeamSide.Player).ToArray();
                if(slots.Length!=3)throw new InvalidOperationException("Player layout needs three visible members.");
                var feet=slots.Select(v=>v.GetFeetWorldPosition().y).ToArray();
                var hp=slots.Select(v=>v.GetHpBarWorldY()).ToArray();
                if(feet.Max()-feet.Min()>1f||hp.Max()-hp.Min()>1f)
                    throw new InvalidOperationException($"Player baseline mismatch: feet={string.Join(",",feet)}; hp={string.Join(",",hp)}");
                foreach(var enemySlot in UnityEngine.Object.FindObjectsByType<CombatantSlotView>(FindObjectsSortMode.None)
                    .Where(v=>!string.IsNullOrEmpty(v.CombatantId)&&state.GetCombatant(v.CombatantId)?.Team==TeamSide.Enemy))
                    if(Math.Abs(enemySlot.GetFeetWorldPosition().y-feet[0])>1f||Math.Abs(enemySlot.GetHpBarWorldY()-hp[0])>1f)
                        throw new InvalidOperationException($"Enemy actual feet/HUD are not aligned: {enemySlot.CombatantId}");
                Debug.Log("[RuntimeValidation] Enemy visible feet and HP row share the player baselines.");
                Debug.Log("[RuntimeValidation] Three player feet and HP rows share one baseline (including berserker).");
                BerserkerRules.Get(state,hero).Rage=4;hpBefore=EnemyHp(state);SelectNew(session,"b_split_helm");
                var ghost=session.Engine.Draft.GetDisplayedHand().Single(c=>c.SpectralRepeatSourceId>0);
                Select(session,ghost); // Leave this visible for the screenshot on a later rendered frame.
            }
            else if(berserkerStage==4)
            {
                BerserkerRules.Get(state,hero).Rage=4;SelectNew(session,"b_burning_standard");if(!session.CommitPlan())throw new InvalidOperationException("Banner commit failed");turns++;
            }
            else if(berserkerStage==5)
            {
                state.EnergyCurrent=3;hpBefore=EnemyHp(state);SelectNew(session,"b_split_helm");
                Select(session,session.Engine.Draft.GetDisplayedHand().Single(c=>c.SpectralRepeatSourceId>0));
                if(session.Engine.Draft.ReservedRage!=4||state.EnergyCurrent!=1)throw new InvalidOperationException("Banner costs/reservations incorrect");
                // Guarantee at least one real attack remains in discard after the normal five-card draw.
                // A fresh training loadout can otherwise contain only the two injected attacks.
                for(var i=0;i<6;i++)
                {
                    var candidate=session.Engine.AddCardTemplateToHand(BerserkerCardActions.Create("b_axe_cut"));
                    DeckRules.MovePlayedCardToDiscard(state,TeamSide.Player,candidate,new List<BattleEvent>());
                }
                // Keep enough cards in draw: otherwise normal draw reshuffles the entire discard pile,
                // correctly leaving nothing for an AFTER-draw discard recall to select.
                for(var i=0;i<6;i++)
                {
                    var draw=session.Engine.AddCardTemplateToHand(BerserkerCardActions.Create("b_iron_guard"));
                    state.PlayerHand.Remove(draw);state.PlayerDrawPile.Add(draw);
                }
                state.BerserkerRecalls.Add(new BerserkerRecall{OwnerId=hero.Id,Label="死斗回收交互验收",Type=CardType.Attack,Remaining=1,DueTurn=state.TurnNumber+1,FreeInHand=true});
                if(!session.CommitPlan())throw new InvalidOperationException("Banner attack commit failed");turns++;
            }
            berserkerStage++;nextAction=now+3;
        }
        static int EnemyHp(BattleState state)=>state.GetTeam(TeamSide.Enemy).Sum(c=>c.Hp);
        static string heroIdForLog(BattleState state)=>string.Join(",",state.Combatants.Where(c=>c.CharacterDefinitionId==BerserkerSoulRules.CharacterId).Select(c=>$"{c.Id}:{c.Hp}"));
        static void SelectNew(BattleSession session,string id)
        {
            if(!session.TryAddCardToHand(BerserkerCardActions.Create(id)))throw new InvalidOperationException("Unable to add "+id);
            Select(session,session.Engine.State.PlayerHand.Last());
        }
        static void Select(BattleSession session,CardInstanceState card)
        {
            if(!session.ToggleCard(card.InstanceId))throw new InvalidOperationException("Unable to select "+card.DisplayName);
            if(session.Engine.Draft.AwaitingTargetCardId.HasValue)
            {
                var state=session.Engine.State;var owner=state.GetCombatant(PositionRules.GetOwnerCombatantId(state,card));var targets=CardRules.GetValidTargetCandidates(state,card,owner);
                if(targets.Count==0||!session.AssignTarget(targets[0].Id))throw new InvalidOperationException("Target selection failed");
            }
        }
    }
}
