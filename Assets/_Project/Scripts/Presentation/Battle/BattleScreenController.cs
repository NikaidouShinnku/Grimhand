using System.Collections.Generic;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleScreenController : MonoBehaviour
    {
        [SerializeField] BattleSetupSO battleSetup;
        [SerializeField] ExpeditionSetupSO expeditionSetup;
        [SerializeField] CardVisualCatalogSO cardVisualCatalog;
        [SerializeField] CharacterVisualCatalogSO characterVisualCatalog;
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
        }

        void Start()
        {
            _definitions = CardVisualResolver.BuildDefinitionLookup(battleSetup);
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
            _portraitDirector.Initialize(_session, screenView, characterVisualCatalog);
            screenView.SetPresentationBusyCheck(() => _portraitDirector.IsPlaying);
            _session.Start();
            screenView.Refresh();
            screenView.BeginPlanningIdleLoops();
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
