using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Persistence;
using Grimhand.Presentation.Battle;
using Grimhand.Presentation.Camp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Battle.Tests
{
    /// <summary>Opt-in Play Mode UI checks using a throwaway profile, never the user's save.</summary>
    public static class BerserkerUiValidation
    {
        const string Output = "Tools/UnityValidation20261018";
        static GameFlowController flow;
        static TalentCampOverlayView talents;
        static ChampionCampOverlayView champions;
        static PlayerProfileState profile;
        static double next;
        static int stage;
        static bool running, background;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Grimhand/Validation/Check berserker UI consistency")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying || running) throw new InvalidOperationException("Enter Play Mode first.");
            flow = Find<GameFlowController>(); talents = Find<TalentCampOverlayView>(); champions = Find<ChampionCampOverlayView>();
            var battle = AssetDatabase.LoadAssetAtPath<BattleSetupSO>("Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            profile = PlayerProfileFactory.CreateNew(battle, expedition);
            profile.Meta.LichUnlocked=true; // This fixture exercises the previous five-character page.
            foreach (var id in TalentCatalog.PlayableCharacterIds) profile.Meta.GetOrCreate(id).OutOfRunLevel = 10;
            Set("_saveService", new SaveService(new LocalFileSaveStorage(Path.GetFullPath(Output + "/ui_profile")), SaveValidationContextBuilder.Build(expedition)));
            Set("_profile", profile); Set("_meta", profile.Meta); Set("_roster", profile.Roster); Set("_collection", profile.Collection);
            var menu = typeof(GameFlowController).GetField("gameMenu", Private).GetValue(flow);
            Invoke(menu, "Hide"); Invoke(flow, "ShowCamp");
            Invoke(flow, "OpenTalentCamp");
            typeof(TalentCampOverlayView).GetField("_selectedIndex", Private).SetValue(talents, 0);
            Invoke(talents, "RefreshAll");
            Directory.CreateDirectory(Output);
            background = Application.runInBackground; Application.runInBackground = true;
            running = true; stage = -6; next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Tick;
        }

        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        static void Set(string name, object value) => typeof(GameFlowController).GetField(name, Private).SetValue(flow, value);
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(target, args);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + name + ".png"));
        static void Tick()
        {
            if (!running || EditorApplication.timeSinceStartup < next) return;
            try
            {
                Require(EditorApplication.isPlaying, "Play Mode stopped.");
                Canvas.ForceUpdateCanvases();
                if (stage == -6)
                {
                    CheckThumbs(5,0);
                    talents.GetComponentsInChildren<Button>().Single(b=>b.name=="CharNext").onClick.Invoke();
                }
                else if(stage == -5){CheckThumbs(5,0);Shot("talents-locked-five");}
                else if(stage == -4){talents.Hide();Invoke(flow,"OpenLibrary");}
                else if(stage == -3){Shot("library-berserker-locked");}
                else if(stage == -2)
                {
                    Find<LibraryCodexOverlayView>().Hide();
                    Require(CharacterUnlockRules.RecordVictory(profile.Meta,20,true,false,false,"char_skeleton_king"),"Unlock failed.");
                    Invoke(flow,"OpenTalentCamp");
                }
                else if(stage == -1){}
                else if (stage == 0)
                {
                    CheckThumbs(5, 0); Shot("talents-page-original-five");
                }
                else if (stage == 1)
                {
                    talents.GetComponentsInChildren<Button>().Single(b => b.name == "CharNext").onClick.Invoke();
                }
                else if (stage == 2)
                {
                    CheckThumbs(1, 5);
                    Require(talents.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Talent_talent_berserker_s1_")) == 4, "Wrong slot-one node count.");
                    Require(!talents.GetComponentsInChildren<Text>().Any(t => t.text.Contains("固有能力")), "Innate label remains.");
                    Shot("talents-page-berserker");
                }
                else if (stage == 3)
                {
                    Invoke(talents, "ShowTooltip", TalentCatalog.Get("talent_berserker_s1_lv9"), TalentCardState.Unlocked);
                }
                else if (stage == 4)
                {
                    var panel=talents.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="TalentTooltip");
                    Require(!panel.blocksRaycasts,"Tooltip intercepts hover and flickers.");
                    Shot("talents-berserker-full-description");
                }
                else if (stage == 5)
                {
                    talents.Hide(); Invoke(flow, "OpenChampionCamp"); Invoke(champions, "ShowTeamPanel"); Invoke(champions, "ShowCharacterPicker", 0);
                }
                else if (stage == 6)
                {
                    Require(!champions.GetComponentsInChildren<Text>().Any(t => t.text.Contains("承压积怒") || t.text.Contains("固有能力")), "Character-specific intro remains.");
                    Shot("team-picker-consistent");
                }
                else if (stage == 7)
                {
                    champions.Hide(); Finish(); RuntimeSmokeValidation.StartBerserker(); return;
                }
                stage++; next = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception ex) { Debug.LogError("[BerserkerUI] FAILED: " + ex); Finish(); }
        }

        static void CheckThumbs(int count, int first)
        {
            var host = talents.GetComponentsInChildren<RectTransform>().Single(t => t.name == "CharThumbs");
            Require(host.childCount == count, "Wrong characters per page: " + host.childCount);
            for (var i = 0; i < count; i++)
            {
                var child = host.GetChild(i) as RectTransform;
                Require(child.name == "Thumb_" + (first + i), "Wrong page ordering.");
                Require(Mathf.Abs((child.anchorMin.x + child.anchorMax.x) * .5f - (i + .5f) / 5) < .001f, "Thumbnail is not centered in its fixed slot.");
                Require(child.GetComponent<Image>().sprite != null, "Missing thumbnail.");
            }
            Debug.Log("[BerserkerUI] Page checked: " + count + " characters, first index " + first);
        }

        static void Finish()
        {
            running = false; EditorApplication.update -= Tick; Application.runInBackground = background;
            Debug.Log("[BerserkerUI] UI validation ended; screenshots saved, player/cloud saves untouched.");
        }
    }
}
