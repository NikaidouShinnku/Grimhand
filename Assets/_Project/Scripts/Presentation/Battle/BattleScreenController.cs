using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleScreenController : MonoBehaviour
    {
        [SerializeField] BattleSetupSO battleSetup;
        [SerializeField] ExpeditionSetupSO expeditionSetup;
        [SerializeField] CardVisualCatalogSO cardVisualCatalog;
        [SerializeField] CharacterVisualCatalogSO characterVisualCatalog;
        [SerializeField] BattleActionEffectCatalogSO actionEffectCatalog;
        [SerializeField] BattleUiIconCatalogSO uiIconCatalog;
        [SerializeField] RelicVisualCatalogSO relicVisualCatalog;
        [SerializeField] ConsumableVisualCatalogSO consumableVisualCatalog;
        [SerializeField] BattleScreenView screenView;
        [SerializeField] bool disableLegacyImGui = true;

        readonly BattleSession _session = new();
        Dictionary<string, CardDefinitionSO> _definitions;

        BattleDemoController _legacyDemo;
        BattlePortraitDirector _portraitDirector;

        public BattleSession Session => _session;
        public CardView HandCardPrefab => screenView != null ? screenView.HandCardPrefab : null;
        public CardVisualCatalogSO CardVisualCatalog => cardVisualCatalog;
        public CharacterVisualCatalogSO CharacterVisualCatalog => characterVisualCatalog;
        public BattleUiIconCatalogSO UiIconCatalog => uiIconCatalog;
        public BattleSetupSO BattleSetup => battleSetup;
        public ExpeditionSetupSO ExpeditionSetup => expeditionSetup;

        void Awake()
        {
            _legacyDemo = GetComponent<BattleDemoController>();
            if (_legacyDemo != null && disableLegacyImGui)
                _legacyDemo.enabled = false;

            _portraitDirector = GetComponent<BattlePortraitDirector>();
            if (_portraitDirector == null)
                _portraitDirector = gameObject.AddComponent<BattlePortraitDirector>();

            EnsureCatalogReferences();
        }

        void EnsureCatalogReferences()
        {
            if (screenView == null)
                screenView = FindAnyObjectByType<BattleScreenView>(FindObjectsInactive.Include);

#if UNITY_EDITOR
            const string dataRoot = "Assets/_Project/Data";
            if (battleSetup == null)
            {
                battleSetup = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleSetupSO>(
                    dataRoot + "/Setups/BattleSetup_Demo.asset");
            }

            if (expeditionSetup == null)
            {
                expeditionSetup = UnityEditor.AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                    dataRoot + "/Setups/ExpeditionSetup_Demo.asset");
            }

            if (cardVisualCatalog == null)
            {
                cardVisualCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(
                    dataRoot + "/CardVisualCatalog_Demo.asset");
            }

            if (characterVisualCatalog == null)
            {
                characterVisualCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(
                    dataRoot + "/CharacterVisualCatalog_Demo.asset");
            }

            if (consumableVisualCatalog == null)
            {
                consumableVisualCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(
                    dataRoot + "/ConsumableVisualCatalog_Demo.asset");
            }

            if (actionEffectCatalog == null)
            {
                actionEffectCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleActionEffectCatalogSO>(
                    dataRoot + "/BattleActionEffectCatalog_Demo.asset");
            }

            if (uiIconCatalog == null)
            {
                uiIconCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(
                    dataRoot + "/BattleUiIconCatalog_Demo.asset");
            }

            if (relicVisualCatalog == null)
            {
                relicVisualCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(
                    dataRoot + "/RelicVisualCatalog_Demo.asset");
            }
#endif
        }

        void Start()
        {
            if (GetComponent<Camp.GameFlowController>() != null)
                return;

            PrepareSession(startExpedition: true);
        }

        /// <summary>初始化战斗 UI；营地模式下由 GameFlowController 调用且不自动开远征。</summary>
        public void PrepareSession(bool startExpedition)
        {
            EnsureCatalogReferences();

            if (screenView == null)
            {
                Debug.LogError("[BattleScreen] 未找到 BattleScreenView。请执行 Grimhand → Open Battle Test Scene。");
                return;
            }

            _definitions = CardVisualResolver.BuildDefinitionLookup(battleSetup, expeditionSetup);
            _session.Configure(battleSetup, expeditionSetup);
            _session.Changed += OnSessionChanged;
            screenView.Initialize(
                _session,
                cardVisualCatalog,
                characterVisualCatalog,
                uiIconCatalog,
                _definitions,
                relicVisualCatalog,
                consumableVisualCatalog);
            _portraitDirector.Initialize(_session, screenView, characterVisualCatalog, actionEffectCatalog, uiIconCatalog);
            screenView.SetPresentationBusyCheck(() => _portraitDirector.IsPlaying);

            if (startExpedition)
            {
                _session.Start();
                screenView.Refresh();
                screenView.BeginPlanningIdleLoops();
            }
            else
            {
                screenView.Refresh();
            }
        }

        public void SetCampRoster(CampRosterState roster) => _session.SetCampRoster(roster);
        public void SetCampMeta(CampMetaState meta) => _session.SetCampMeta(meta);

        public void SetBattleScreenVisible(bool visible)
        {
            if (screenView != null)
                screenView.gameObject.SetActive(visible);
        }

        public void BeginExpeditionFromCamp(CampRosterState roster, int mapStartLayer = 1)
        {
            _session.SetCampRoster(roster);
            _session.BeginExpedition(roster, mapStartLayer);
            screenView.Refresh();
            screenView.BeginPlanningIdleLoops();
        }

        public bool ResumeExpeditionFromCamp(ActiveRunSnapshot snapshot)
        {
            if (!_session.ResumeExpedition(snapshot))
                return false;

            screenView.Refresh();
            screenView.BeginPlanningIdleLoops();
            return true;
        }

        void Update()
        {
            _session.Tick();
        }

        void OnDestroy()
        {
            _session.Changed -= OnSessionChanged;
        }

        void OnSessionChanged() => screenView?.Refresh();
    }
}
