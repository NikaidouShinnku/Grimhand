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
        [SerializeField] BattleScreenView screenView;
        [SerializeField] bool disableLegacyImGui = true;

        readonly BattleSession _session = new();
        Dictionary<string, CardDefinitionSO> _definitions;

        BattleDemoController _legacyDemo;

        void Awake()
        {
            _legacyDemo = GetComponent<BattleDemoController>();
            if (_legacyDemo != null && disableLegacyImGui)
                _legacyDemo.enabled = false;
        }

        void Start()
        {
            _definitions = CardVisualResolver.BuildDefinitionLookup(battleSetup);
            _session.Configure(battleSetup, expeditionSetup);
            _session.Changed += OnSessionChanged;
            screenView.Initialize(_session, cardVisualCatalog, characterVisualCatalog, _definitions);
            _session.Start();
            screenView.Refresh();
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
