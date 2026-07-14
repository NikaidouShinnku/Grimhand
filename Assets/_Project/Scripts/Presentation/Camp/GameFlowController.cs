using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Presentation.Camp
{
    /// <summary>主菜单 ↔ 营地 ↔ 战斗/远征 流程切换。</summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] BattleScreenController battleController;
        [SerializeField] GameMenuView gameMenu;
        [SerializeField] ExpeditionEscMenuView escMenu;
        [SerializeField] GameSettingsOverlayView settingsOverlay;
        [SerializeField] CampScreenView campScreen;
        [SerializeField] ChampionCampOverlayView championCamp;
        [SerializeField] TalentCampOverlayView talentCamp;
        [SerializeField] PortalOverlayView portalOverlay;
        [SerializeField] MetaShopOverlayView metaShop;
        [SerializeField] BattleSetupSO battleSetup;
        [SerializeField] ExpeditionSetupSO expeditionSetup;

        CampRosterState _roster;
        CampMetaState _meta;
        CampCollectionState _collection;
        PlayerProfileState _profile;
        SaveService _saveService;
        Dictionary<string, CardDefinitionSO> _definitions;

        bool _trackingExpedition;
        bool _runEndHandled;
        ExpeditionPhase? _lastCheckpointPhase;
        int _activeMapStartLayer = 1;

        void Awake() => EnsureReferences();

        void Start()
        {
            EnsureReferences();
            InitializeAudio();
            GameSettings.ApplyAudioVolumes();
            GameSettings.ApplyDisplaySettings();

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
            var validationContext = SaveValidationContextBuilder.Build(expeditionSetup);
            _saveService = new SaveService(
                new LocalFileSaveStorage(SaveService.DefaultSaveDirectory),
                validationContext);

            var loadResult = _saveService.LoadOrCreate(() =>
                PlayerProfileFactory.CreateNew(battleSetup, expeditionSetup));
            _profile = loadResult.Profile;
            _meta = _profile.Meta;
            _roster = _profile.Roster;
            _collection = _profile.Collection;

            if (loadResult.Source != SaveLoadSource.Primary)
                Debug.Log($"[GameFlow] 读档: {loadResult.Message} ({loadResult.Source})");

            var cardPrefab = battleController.HandCardPrefab;
            var cardCatalog = battleController.CardVisualCatalog;
            var charCatalog = battleController.CharacterVisualCatalog;
            var uiIcons = battleController.UiIconCatalog;

            gameMenu?.ConfigureArt(uiIcons);
            escMenu?.ConfigureArt(uiIcons);
            gameMenu?.Initialize(
                EnterCampFromMenu,
                AbandonActiveRunAndEnterCamp,
                ContinueExpedition,
                OpenSettings,
                QuitGame,
                uiIcons);
            escMenu?.Initialize(
                CloseEscMenu,
                OpenSettings,
                ForfeitExpeditionFromEsc,
                QuitGame,
                uiIcons);
            settingsOverlay?.Initialize(CloseSettings);

            campScreen?.ConfigureArt(uiIcons);
            campScreen?.Initialize(OpenChampionCamp, OpenPortal, OpenTalentCamp, OpenMetaShop, uiIcons, ShowComingSoon);
            championCamp?.Initialize(
                battleSetup,
                expeditionSetup,
                cardPrefab,
                cardCatalog,
                charCatalog,
                uiIcons,
                _definitions,
                OnRosterSaved,
                OnCollectionSaved,
                OnAccountGoldChanged,
                OnOverlayClosed);
            talentCamp?.Initialize(
                battleSetup,
                charCatalog,
                uiIcons,
                OnMetaSaved,
                OnOverlayClosed);
            portalOverlay?.Initialize(battleSetup, charCatalog, BeginExpedition, OnOverlayClosed);
            metaShop?.Initialize(
                expeditionSetup,
                battleSetup,
                cardPrefab,
                cardCatalog,
                charCatalog,
                uiIcons,
                _definitions,
                OnShopProfileChanged,
                OnOverlayClosed);

            battleController.SetCampRoster(_roster);
            battleController.SetCampMeta(_meta);
            battleController.PrepareSession(startExpedition: false);
            battleController.Session.Changed += OnSessionChanged;
            battleController.Session.ReturnToCampRequested = ReturnToCampFromRunEnd;

            ShowMainMenu();
        }

        void OnDestroy()
        {
            if (battleController?.Session != null)
                battleController.Session.Changed -= OnSessionChanged;
        }

        void ShowMainMenu()
        {
            campScreen?.Hide();
            championCamp?.Hide();
            talentCamp?.Hide();
            portalOverlay?.Hide();
            metaShop?.Hide();
            settingsOverlay?.Hide();
            battleController.SetBattleScreenVisible(false);
            GameAudioService.Instance.PlayCampBgm();
            escMenu?.Hide();
            gameMenu?.Show(_profile.HasActiveRun);
        }

        void EnterCampFromMenu()
        {
            gameMenu?.Hide();
            escMenu?.Hide();
            ShowCamp();
        }

        void AbandonActiveRunAndEnterCamp()
        {
            if (_profile != null && _profile.HasActiveRun)
            {
                var mapStart = _profile.ActiveRun.MapStartLayer > 0
                    ? _profile.ActiveRun.MapStartLayer
                    : 1;
                var config = battleController?.Session?.BuildExpeditionConfig(mapStart);
                ActiveRunPersistence.TryAbandonAndSettle(_profile, _meta, config);
                SaveProfile();
            }

            _trackingExpedition = false;
            _runEndHandled = false;
            _lastCheckpointPhase = null;
            gameMenu?.Hide();
            ShowCamp();
            campScreen?.RefreshAccountGold(_profile.AccountGold);
            campScreen?.ShowToast("已放弃上次远征并完成结算。");
        }

        void ShowCamp()
        {
            escMenu?.Hide();
            campScreen?.Show(_profile.AccountGold);
            championCamp?.Hide();
            talentCamp?.Hide();
            portalOverlay?.Hide();
            metaShop?.Hide();
            settingsOverlay?.Hide();
            battleController.SetBattleScreenVisible(false);
            GameAudioService.Instance.PlayCampBgm();
        }

        void ReturnToCampFromRunEnd()
        {
            _trackingExpedition = false;
            escMenu?.Hide();
            battleController.SetBattleScreenVisible(false);
            ShowCamp();
            campScreen?.RefreshAccountGold(_profile.AccountGold);
        }

        void ShowBattle()
        {
            gameMenu?.Hide();
            escMenu?.Hide();
            campScreen?.Hide();
            championCamp?.Hide();
            talentCamp?.Hide();
            portalOverlay?.Hide();
            metaShop?.Hide();
            settingsOverlay?.Hide();
            battleController.SetBattleScreenVisible(true);
        }

        void OpenSettings()
        {
            if (settingsOverlay == null)
            {
                Debug.LogWarning("[GameFlow] Settings overlay missing。");
                return;
            }

            settingsOverlay.Show();
        }

        void CloseSettings()
        {
            if (escMenu != null && escMenu.IsOpen)
                return;

            if (campScreen != null && campScreen.gameObject.activeSelf)
                return;

            if (IsBattleUiVisible())
                return;

            gameMenu?.Show(_profile.HasActiveRun);
        }

            void CloseEscMenu()
        {
            FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include)?.SetEscUiSuppressed(false);
            escMenu?.Hide();
        }

        void ForfeitExpeditionFromEsc()
        {
            settingsOverlay?.Hide();
            FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include)?.SetEscUiSuppressed(false);
            escMenu?.Hide();

            var liveRun = battleController?.Session?.Expedition?.Run;
            if (_trackingExpedition && liveRun != null)
            {
                FinalizeExpeditionRun(liveRun);
            }
            else if (_profile != null && _profile.HasActiveRun)
            {
                var mapStart = _profile.ActiveRun.MapStartLayer > 0
                    ? _profile.ActiveRun.MapStartLayer
                    : 1;
                var config = battleController?.Session?.BuildExpeditionConfig(mapStart);
                ActiveRunPersistence.TryAbandonAndSettle(_profile, _meta, config);
                SaveProfile();
            }

            _trackingExpedition = false;
            _runEndHandled = true;
            _lastCheckpointPhase = null;
            battleController.SetBattleScreenVisible(false);
            ShowCamp();
            campScreen?.RefreshAccountGold(_profile.AccountGold);
            campScreen?.ShowToast("已放弃远征并完成结算。");
        }

        void QuitGame()
        {
            SaveProfile();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            HandleEscapePressed();
        }

        void HandleEscapePressed()
        {
            if (settingsOverlay != null && settingsOverlay.IsOpen)
            {
                settingsOverlay.Hide();
                return;
            }

            if (escMenu != null && escMenu.IsOpen)
            {
                if (escMenu.IsForfeitConfirmOpen)
                    escMenu.HideForfeitConfirm();
                else
                {
                    FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include)?.SetEscUiSuppressed(false);
                    escMenu.Hide();
                }
                return;
            }

            if (!CanOpenEscMenu())
                return;

            OpenEscMenu();
        }

        bool CanOpenEscMenu() =>
            _trackingExpedition && battleController != null && IsBattleUiVisible();

        bool IsBattleUiVisible()
        {
            var screen = FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include);
            return screen != null && screen.gameObject.activeInHierarchy;
        }

        void OpenEscMenu()
        {
            EnsureReferences();
            if (escMenu == null)
            {
                Debug.LogWarning("[GameFlow] Esc menu missing。");
                return;
            }

            var uiIcons = battleController?.UiIconCatalog;
            var layer = battleController?.Session?.Expedition?.Run?.Map?.NodesCompleted + 1 ?? 1;
            var bg = ExpeditionPathArt.ResolveBackground(uiIcons, layer);
            FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include)?.SetEscUiSuppressed(true);
            escMenu.Show(bg ?? uiIcons?.CaveBackground);
        }

        void ContinueExpedition()
        {
            if (!_profile.HasActiveRun)
            {
                gameMenu?.Show(false);
                return;
            }

            if (!battleController.ResumeExpeditionFromCamp(_profile.ActiveRun))
            {
                Debug.LogWarning("[GameFlow] 断线存档损坏，已清除。");
                ActiveRunPersistence.Clear(_profile);
                SaveProfile();
                gameMenu?.Show(false);
                return;
            }

            _trackingExpedition = true;
            _runEndHandled = false;
            _lastCheckpointPhase = battleController.Session.Expedition?.Run.Phase;
            ShowBattle();
        }

        void OpenChampionCamp()
        {
            campScreen?.Hide();
            championCamp?.Show(_roster, _meta, _profile.AccountGold, _collection, _profile.CollectionCapacity);
        }

        void OpenTalentCamp()
        {
            EnsureReferences();
            if (talentCamp == null)
            {
                Debug.LogError("[GameFlow] 未找到 TalentCampOverlay。请执行 Grimhand → Setup Camp UI in Scene。");
                campScreen?.ShowToast("天赋界面未就绪，请重新 Setup Camp UI。");
                campScreen?.Show(_profile.AccountGold);
                return;
            }

            campScreen?.Hide();
            talentCamp.Show(_meta);
        }

        void OpenMetaShop()
        {
            EnsureReferences();
            if (metaShop == null)
            {
                Debug.LogError("[GameFlow] 未找到 MetaShopOverlay。请执行 Grimhand → Setup Camp UI in Scene。");
                campScreen?.ShowToast("商店界面未就绪，请重新 Setup Camp UI。");
                campScreen?.Show(_profile.AccountGold);
                return;
            }

            campScreen?.Hide();
            metaShop.Show(_profile);
            GameAudioService.Instance.PlayUiShopEnter();
        }

        void OnShopProfileChanged()
        {
            SaveProfile();
            campScreen?.RefreshAccountGold(_profile.AccountGold);
            metaShop?.Refresh();
        }

        void OpenPortal()
        {
            CampRosterLoadoutRules.EnsureRosterStructure(_roster);
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

            if (metaShop != null && metaShop.IsOpen)
                return;

            if (settingsOverlay != null && settingsOverlay.IsOpen)
                return;

            if (escMenu != null && escMenu.IsOpen)
                return;

            campScreen?.Show(_profile.AccountGold);
        }

        void OnRosterSaved(CampRosterState roster)
        {
            _roster = roster;
            _profile.Roster = roster;
            battleController.SetCampRoster(_roster);
            SaveProfile();
        }

        void OnCollectionSaved(CampCollectionState collection)
        {
            _collection = collection;
            _profile.Collection = collection;
            SaveProfile();
        }

        void OnAccountGoldChanged(int accountGold)
        {
            _profile.AccountGold = accountGold;
            SaveProfile();
            campScreen?.RefreshAccountGold(accountGold);
        }

        void OnMetaSaved(CampMetaState meta)
        {
            _meta = meta;
            _profile.Meta = meta;
            battleController.SetCampMeta(_meta);
            SaveProfile();
        }

        void BeginExpedition()
        {
            if (_roster == null || !_roster.IsReadyForExpedition)
            {
                campScreen?.ShowToast("请先在军营配置 3 名不同角色。");
                ShowCamp();
                return;
            }

            if (CampCollectionRules.BlocksExpeditionStart(_collection, _profile.CollectionCapacity))
            {
                campScreen?.ShowToast(
                    $"军营收藏 {_collection.Count}/{_profile.CollectionCapacity} 超出上限，请整理后再出发。");
                ShowCamp();
                return;
            }

            _activeMapStartLayer = portalOverlay?.SelectedStartLayer ?? 1;
            battleController.SetCampRoster(_roster);
            battleController.SetCampMeta(_meta);
            ShowBattle();
            battleController.BeginExpeditionFromCamp(_roster, _activeMapStartLayer);

            var engine = battleController.Session.Expedition;
            if (engine != null)
            {
                ActiveRunPersistence.BeginNewRun(_profile, engine, _activeMapStartLayer);
                _trackingExpedition = true;
                _runEndHandled = false;
                _lastCheckpointPhase = engine.Run.Phase;
                SaveProfile();
            }
        }

        void ShowComingSoon(string feature)
        {
            campScreen?.ShowToast($"{feature} — 即将开放");
        }

        void OnSessionChanged()
        {
            if (!_trackingExpedition)
                return;

            var expedition = battleController.Session.Expedition;
            if (expedition == null)
                return;

            var phase = expedition.Run.Phase;
            if (phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed)
            {
                if (!_runEndHandled)
                {
                    _runEndHandled = true;
                    FinalizeExpeditionRun(expedition.Run);
                    campScreen?.RefreshAccountGold(_profile.AccountGold);
                }

                return;
            }

            if (_lastCheckpointPhase != phase)
            {
                _lastCheckpointPhase = phase;
                SaveActiveRunCheckpoint();
                return;
            }

            if (phase is not ExpeditionPhase.InBattle)
                SaveActiveRunCheckpoint();
        }

        void FinalizeExpeditionRun(ExpeditionRunState run)
        {
            MetaEconomySync.SyncMetaGoldFromRun(_profile, run);
            RunSettlementRules.ApplyRunEndMetaRewards(run, _meta);
            ActiveRunPersistence.Clear(_profile);
            _trackingExpedition = false;
            _lastCheckpointPhase = null;
            SaveProfile();
        }

        void SaveActiveRunCheckpoint()
        {
            var engine = battleController.Session.Expedition;
            if (engine == null || engine.Run.Phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed)
                return;

            ActiveRunPersistence.UpdateCheckpoint(_profile, engine);
            SaveProfile();
        }

        public void SaveProfile()
        {
            if (_saveService == null || _profile == null)
                return;

            if (!_saveService.TrySave(_profile, out var error))
                Debug.LogWarning($"[GameFlow] 存档失败: {error}");
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (_trackingExpedition)
                    SaveActiveRunCheckpoint();
                SaveProfile();
            }
        }

        void OnApplicationQuit()
        {
            if (_trackingExpedition)
                SaveActiveRunCheckpoint();
            SaveProfile();
        }

        void EnsureReferences()
        {
            if (battleController == null)
                battleController = GetComponent<BattleScreenController>();

            if (gameMenu == null)
                gameMenu = FindAnyObjectByType<GameMenuView>(FindObjectsInactive.Include);

            if (escMenu == null)
                escMenu = FindAnyObjectByType<ExpeditionEscMenuView>(FindObjectsInactive.Include);

            if (settingsOverlay == null)
                settingsOverlay = FindAnyObjectByType<GameSettingsOverlayView>(FindObjectsInactive.Include);

            if (campScreen == null)
                campScreen = FindAnyObjectByType<CampScreenView>(FindObjectsInactive.Include);

            var canvasRoot = campScreen != null
                ? campScreen.transform.parent
                : FindAnyObjectByType<Canvas>()?.transform;

            if (gameMenu == null && canvasRoot != null)
                gameMenu = CampOverlayBootstrap.EnsureOverlay<GameMenuView>(canvasRoot, "GameMenu");

            if (escMenu == null && canvasRoot != null)
                escMenu = CampOverlayBootstrap.EnsureOverlay<ExpeditionEscMenuView>(
                    canvasRoot, "ExpeditionEscMenu");

            if (settingsOverlay == null && canvasRoot != null)
                settingsOverlay = CampOverlayBootstrap.EnsureOverlay<GameSettingsOverlayView>(
                    canvasRoot, "GameSettingsOverlay");

            if (championCamp == null)
                championCamp = FindAnyObjectByType<ChampionCampOverlayView>(FindObjectsInactive.Include);

            if (talentCamp == null && canvasRoot != null)
                talentCamp = CampOverlayBootstrap.EnsureOverlay<TalentCampOverlayView>(canvasRoot, "TalentCampOverlay");

            if (talentCamp == null)
                talentCamp = FindAnyObjectByType<TalentCampOverlayView>(FindObjectsInactive.Include);

            if (portalOverlay == null)
                portalOverlay = FindAnyObjectByType<PortalOverlayView>(FindObjectsInactive.Include);

            if (metaShop == null && canvasRoot != null)
                metaShop = CampOverlayBootstrap.EnsureOverlay<MetaShopOverlayView>(canvasRoot, "MetaShopOverlay");

            if (metaShop == null)
                metaShop = FindAnyObjectByType<MetaShopOverlayView>(FindObjectsInactive.Include);
        }

        void InitializeAudio()
        {
            AudioCatalogSO catalog = null;
#if UNITY_EDITOR
            catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(
                "Assets/_Project/Resources/AudioCatalog_Demo.asset");
#endif
            if (catalog == null)
                catalog = Resources.Load<AudioCatalogSO>("AudioCatalog_Demo");

            GameAudioService.Ensure(catalog);
        }
    }
}
