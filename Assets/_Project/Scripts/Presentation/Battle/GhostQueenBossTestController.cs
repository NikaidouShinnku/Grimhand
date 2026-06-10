using System.Collections.Generic;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>幽灵女王 Boss 专用测试：Lv.7 + 随机遗物/卡牌，直接开战。</summary>
    public sealed class GhostQueenBossTestController : MonoBehaviour
    {
        [SerializeField] ExpeditionSetupSO expeditionSetup;
        [SerializeField] BattleSetupSO ghostQueenBossSetup;
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

        void Awake()
        {
            _legacyDemo = GetComponent<BattleDemoController>();
            if (_legacyDemo != null && disableLegacyImGui)
                _legacyDemo.enabled = false;

            _portraitDirector = GetComponent<BattlePortraitDirector>();
            if (_portraitDirector == null)
                _portraitDirector = gameObject.AddComponent<BattlePortraitDirector>();

#if UNITY_EDITOR
            if (consumableVisualCatalog == null)
            {
                consumableVisualCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(
                    "Assets/_Project/Data/ConsumableVisualCatalog_Demo.asset");
            }

            if (actionEffectCatalog == null)
            {
                actionEffectCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleActionEffectCatalogSO>(
                    "Assets/_Project/Data/BattleActionEffectCatalog_Demo.asset");
            }

            if (ghostQueenBossSetup == null)
            {
                ghostQueenBossSetup = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleSetupSO>(
                    "Assets/_Project/Data/Setups/BattleSetup_Encounter_GhostQueenBoss.asset");
            }

            if (expeditionSetup == null)
            {
                expeditionSetup = UnityEditor.AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                    "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            }
#endif
        }

        void Start()
        {
            _definitions = CardVisualResolver.BuildDefinitionLookup(null, expeditionSetup);
            _session.Configure(null, expeditionSetup);
            _session.Changed += OnSessionChanged;
            screenView.Initialize(
                _session,
                cardVisualCatalog,
                characterVisualCatalog,
                uiIconCatalog,
                _definitions,
                relicVisualCatalog,
                consumableVisualCatalog);
            _portraitDirector.Initialize(_session, screenView, characterVisualCatalog, actionEffectCatalog);
            screenView.SetPresentationBusyCheck(() => _portraitDirector.IsPlaying);
            _session.BeginGhostQueenBossTest(ghostQueenBossSetup);
            screenView.Refresh();
            screenView.BeginPlanningIdleLoops();
        }

        void Update() => _session.Tick();

        void OnDestroy() => _session.Changed -= OnSessionChanged;

        void OnSessionChanged() => screenView?.Refresh();
    }
}
