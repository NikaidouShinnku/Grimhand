using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Battle;
using UnityEngine;

namespace Grimhand.Presentation.Camp
{
    /// <summary>营地 ↔ 战斗/远征 流程切换。</summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] BattleScreenController battleController;
        [SerializeField] CampScreenView campScreen;
        [SerializeField] ChampionCampOverlayView championCamp;
        [SerializeField] TalentCampOverlayView talentCamp;
        [SerializeField] PortalOverlayView portalOverlay;
        [SerializeField] BattleSetupSO battleSetup;
        [SerializeField] ExpeditionSetupSO expeditionSetup;
        [SerializeField] bool startAtCamp = true;

        CampRosterState _roster;
        CampMetaState _meta;
        Dictionary<string, CardDefinitionSO> _definitions;

        void Awake() => EnsureReferences();

        void Start()
        {
            EnsureReferences();

            if (battleController == null)
            {
                Debug.LogError("[GameFlow] 未绑定 BattleScreenController。");
                return;
            }

            if (battleSetup == null)
                battleSetup = battleController.BattleSetup;
            if (expeditionSetup == null)
                expeditionSetup = battleController.ExpeditionSetup;

            _definitions = CardVisualResolver.BuildDefinitionLookup(battleSetup, expeditionSetup);
            _roster = CampRosterBuilder.CreateDefault(battleSetup, expeditionSetup);
            _meta = CampMetaState.CreateDefaultDemo();

            var cardPrefab = battleController.HandCardPrefab;
            var cardCatalog = battleController.CardVisualCatalog;
            var charCatalog = battleController.CharacterVisualCatalog;
            var uiIcons = battleController.UiIconCatalog;

            campScreen?.ConfigureArt(uiIcons);
            campScreen?.Initialize(OpenChampionCamp, OpenPortal, OpenTalentCamp, ShowComingSoon, uiIcons);
            championCamp?.Initialize(
                battleSetup,
                expeditionSetup,
                cardPrefab,
                cardCatalog,
                charCatalog,
                uiIcons,
                _definitions,
                OnRosterSaved,
                OnOverlayClosed);
            talentCamp?.Initialize(
                battleSetup,
                charCatalog,
                uiIcons,
                OnMetaSaved,
                OnOverlayClosed);
            portalOverlay?.Initialize(battleSetup, charCatalog, BeginExpedition, OnOverlayClosed);

            battleController.SetCampRoster(_roster);
            battleController.SetCampMeta(_meta);

            battleController.PrepareSession(startExpedition: !startAtCamp);

            if (startAtCamp)
                ShowCamp();
            else
                ShowBattle();
        }

        void ShowCamp()
        {
            campScreen?.Show();
            championCamp?.Hide();
            talentCamp?.Hide();
            portalOverlay?.Hide();
            battleController.SetBattleScreenVisible(false);
        }

        void ShowBattle()
        {
            campScreen?.Hide();
            championCamp?.Hide();
            talentCamp?.Hide();
            portalOverlay?.Hide();
            battleController.SetBattleScreenVisible(true);
        }

        void OpenChampionCamp()
        {
            campScreen?.Hide();
            championCamp?.Show(_roster);
        }

        void OpenTalentCamp()
        {
            EnsureReferences();
            if (talentCamp == null)
            {
                Debug.LogError("[GameFlow] 未找到 TalentCampOverlay。请执行 Grimhand → Setup Camp UI in Scene。");
                campScreen?.ShowToast("天赋界面未就绪，请重新 Setup Camp UI。");
                campScreen?.Show();
                return;
            }

            campScreen?.Hide();
            talentCamp.Show(_meta);
        }

        void OpenPortal()
        {
            campScreen?.Hide();
            portalOverlay?.Show(_roster);
        }

        void OnOverlayClosed()
        {
            if (championCamp != null && championCamp.IsOpen)
                return;

            if (talentCamp != null && talentCamp.IsOpen)
                return;

            if (portalOverlay != null && portalOverlay.IsOpen)
                return;

            campScreen?.Show();
        }

        void OnRosterSaved(CampRosterState roster)
        {
            _roster = roster;
            battleController.SetCampRoster(_roster);
        }

        void OnMetaSaved(CampMetaState meta)
        {
            _meta = meta;
            battleController.SetCampMeta(_meta);
        }

        void BeginExpedition()
        {
            if (_roster == null || !_roster.IsReadyForExpedition)
            {
                campScreen?.ShowToast("请先在军营完成编队（3 人 × 10 牌）。");
                ShowCamp();
                return;
            }

            battleController.SetCampRoster(_roster);
            battleController.SetCampMeta(_meta);
            ShowBattle();
            battleController.BeginExpeditionFromCamp(_roster, portalOverlay?.SelectedStartLayer ?? 1);
        }

        void ShowComingSoon(string feature)
        {
            campScreen?.ShowToast($"{feature} — 即将开放");
        }

        void EnsureReferences()
        {
            if (battleController == null)
                battleController = GetComponent<BattleScreenController>();

            if (campScreen == null)
                campScreen = FindAnyObjectByType<CampScreenView>(FindObjectsInactive.Include);

            var canvasRoot = campScreen != null
                ? campScreen.transform.parent
                : FindAnyObjectByType<Canvas>()?.transform;

            if (championCamp == null)
                championCamp = FindAnyObjectByType<ChampionCampOverlayView>(FindObjectsInactive.Include);

            if (talentCamp == null && canvasRoot != null)
                talentCamp = CampOverlayBootstrap.EnsureOverlay<TalentCampOverlayView>(canvasRoot, "TalentCampOverlay");

            if (talentCamp == null)
                talentCamp = FindAnyObjectByType<TalentCampOverlayView>(FindObjectsInactive.Include);

            if (portalOverlay == null)
                portalOverlay = FindAnyObjectByType<PortalOverlayView>(FindObjectsInactive.Include);
        }
    }
}
