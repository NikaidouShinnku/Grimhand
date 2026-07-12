using System;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>传送门：确认编队后进入 Demo 远征。</summary>
    [DisallowMultipleComponent]
    public sealed class PortalOverlayView : MonoBehaviour
    {
        CharacterVisualCatalogSO _characterVisuals;
        [SerializeField] Sprite portalBackground;

        CampRosterState _roster;
        Action _onConfirm;
        Action _onClose;

        RectTransform _overlayRoot;
        RectTransform _body;
        RectTransform _partyRow;
        Button _confirmButton;
        Button _caveStartButton;
        Button _dungeonStartButton;
        Button _abyssStartButton;
        Button _caveBossButton;
        Button _dungeonBossButton;
        Button _abyssBossButton;
        int _selectedStartLayer = 1;
        bool _built;

        public int SelectedStartLayer => _selectedStartLayer;

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            BattleSetupSO battleSetup,
            CharacterVisualCatalogSO characterVisuals,
            Action onConfirm,
            Action onClose)
        {
            _characterVisuals = characterVisuals;
            _onConfirm = onConfirm;
            _onClose = onClose;
            EnsureBuilt();
        }

        public void Show(CampRosterState roster)
        {
            _roster = roster;
            EnsureBuilt();
            SelectStartLayer(1);
            _overlayRoot.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RebuildPartySummary();
        }

        public void Hide()
        {
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _overlayRoot = CampUiRuntime.CreateRect("PortalOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _overlayRoot, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            _body = CampUiRuntime.CreateImage("Body", _overlayRoot, new Color(0.06f, 0.08f, 0.14f, 0.98f))
                .rectTransform;
            _body.anchorMin = new Vector2(0.28f, 0.18f);
            _body.anchorMax = new Vector2(0.72f, 0.82f);
            _body.offsetMin = Vector2.zero;
            _body.offsetMax = Vector2.zero;

            var title = CampUiRuntime.CreateText(_body, "开启远征", 28, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(24f, -56f);
            title.rectTransform.offsetMax = new Vector2(-24f, -12f);

            if (portalBackground != null)
            {
                var art = CampUiRuntime.CreateImage("PortalArt", _body, Color.white);
                art.sprite = portalBackground;
                art.preserveAspect = true;
                var artRt = art.rectTransform;
                artRt.anchorMin = new Vector2(0.5f, 0.72f);
                artRt.anchorMax = new Vector2(0.5f, 0.72f);
                artRt.sizeDelta = new Vector2(280f, 280f);
            }

            var subtitle = CampUiRuntime.CreateText(_body, "确认出征队伍后将进入 Demo 远征地图", 17, FontStyle.Normal);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.offsetMin = new Vector2(24f, -92f);
            subtitle.rectTransform.offsetMax = new Vector2(-24f, -60f);
            subtitle.color = new Color(0.78f, 0.82f, 0.92f, 1f);

            _partyRow = CampUiRuntime.CreateRect("PartyRow", _body).GetComponent<RectTransform>();
            _partyRow.anchorMin = new Vector2(0f, 0.35f);
            _partyRow.anchorMax = new Vector2(1f, 0.78f);
            _partyRow.offsetMin = new Vector2(32f, 0f);
            _partyRow.offsetMax = new Vector2(-32f, 0f);

            var difficulty = CampUiRuntime.CreateText(_body, "难度：Demo（标准）", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            difficulty.raycastTarget = false;
            difficulty.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            difficulty.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            difficulty.rectTransform.offsetMin = new Vector2(32f, 0f);
            difficulty.rectTransform.offsetMax = new Vector2(-32f, 0f);

            var regionLabel = CampUiRuntime.CreateText(_body, "起始区域", 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            regionLabel.raycastTarget = false;
            regionLabel.rectTransform.anchorMin = new Vector2(0f, 0.24f);
            regionLabel.rectTransform.anchorMax = new Vector2(1f, 0.32f);
            regionLabel.rectTransform.offsetMin = new Vector2(32f, 0f);
            regionLabel.rectTransform.offsetMax = new Vector2(-32f, 0f);

            var bossLabel = CampUiRuntime.CreateText(_body, "Boss 直通", 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            bossLabel.raycastTarget = false;
            bossLabel.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            bossLabel.rectTransform.anchorMax = new Vector2(1f, 0.14f);
            bossLabel.rectTransform.offsetMin = new Vector2(32f, 0f);
            bossLabel.rectTransform.offsetMax = new Vector2(-32f, 0f);

            _caveStartButton = CampUiRuntime.CreateButton(_body, "洞穴（1层）", new Color(0.22f, 0.34f, 0.28f, 1f),
                new Vector2(132f, 40f));
            var caveRt = _caveStartButton.GetComponent<RectTransform>();
            caveRt.anchorMin = new Vector2(0.5f, 0f);
            caveRt.anchorMax = new Vector2(0.5f, 0f);
            caveRt.pivot = new Vector2(0.5f, 0f);
            caveRt.anchoredPosition = new Vector2(-150f, 116f);

            _dungeonStartButton = CampUiRuntime.CreateButton(_body, "地牢（21层）", new Color(0.28f, 0.24f, 0.38f, 1f),
                new Vector2(132f, 40f));
            var dungeonRt = _dungeonStartButton.GetComponent<RectTransform>();
            dungeonRt.anchorMin = new Vector2(0.5f, 0f);
            dungeonRt.anchorMax = new Vector2(0.5f, 0f);
            dungeonRt.pivot = new Vector2(0.5f, 0f);
            dungeonRt.anchoredPosition = new Vector2(0f, 116f);

            _abyssStartButton = CampUiRuntime.CreateButton(_body, "海渊（41层）", new Color(0.16f, 0.30f, 0.42f, 1f),
                new Vector2(132f, 40f));
            var abyssRt = _abyssStartButton.GetComponent<RectTransform>();
            abyssRt.anchorMin = new Vector2(0.5f, 0f);
            abyssRt.anchorMax = new Vector2(0.5f, 0f);
            abyssRt.pivot = new Vector2(0.5f, 0f);
            abyssRt.anchoredPosition = new Vector2(150f, 116f);

            _caveBossButton = CampUiRuntime.CreateButton(_body, "Boss·20层", new Color(0.42f, 0.24f, 0.20f, 1f),
                new Vector2(132f, 40f));
            var caveBossRt = _caveBossButton.GetComponent<RectTransform>();
            caveBossRt.anchorMin = new Vector2(0.5f, 0f);
            caveBossRt.anchorMax = new Vector2(0.5f, 0f);
            caveBossRt.pivot = new Vector2(0.5f, 0f);
            caveBossRt.anchoredPosition = new Vector2(-150f, 68f);

            _dungeonBossButton = CampUiRuntime.CreateButton(_body, "Boss·40层", new Color(0.38f, 0.22f, 0.28f, 1f),
                new Vector2(132f, 40f));
            var dungeonBossRt = _dungeonBossButton.GetComponent<RectTransform>();
            dungeonBossRt.anchorMin = new Vector2(0.5f, 0f);
            dungeonBossRt.anchorMax = new Vector2(0.5f, 0f);
            dungeonBossRt.pivot = new Vector2(0.5f, 0f);
            dungeonBossRt.anchoredPosition = new Vector2(0f, 68f);

            _abyssBossButton = CampUiRuntime.CreateButton(_body, "Boss·60层", new Color(0.18f, 0.28f, 0.44f, 1f),
                new Vector2(132f, 40f));
            var abyssBossRt = _abyssBossButton.GetComponent<RectTransform>();
            abyssBossRt.anchorMin = new Vector2(0.5f, 0f);
            abyssBossRt.anchorMax = new Vector2(0.5f, 0f);
            abyssBossRt.pivot = new Vector2(0.5f, 0f);
            abyssBossRt.anchoredPosition = new Vector2(150f, 68f);

            _caveStartButton.onClick.AddListener(() => SelectStartLayer(1));
            _dungeonStartButton.onClick.AddListener(() => SelectStartLayer(ExpeditionRegionRules.DungeonStartLayer));
            _abyssStartButton.onClick.AddListener(() => SelectStartLayer(ExpeditionRegionRules.AbyssStartLayer));
            _caveBossButton.onClick.AddListener(() => SelectStartLayer(ExpeditionRegionRules.CaveBossLayer));
            _dungeonBossButton.onClick.AddListener(() => SelectStartLayer(ExpeditionRegionRules.DungeonBossLayer));
            _abyssBossButton.onClick.AddListener(() => SelectStartLayer(ExpeditionRegionRules.AbyssBossLayer));

            var closeBtn = CampUiRuntime.CreateButton(_body, "返回", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(120f, 44f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0f, 0f);
            closeRt.anchorMax = new Vector2(0f, 0f);
            closeRt.pivot = new Vector2(0f, 0f);
            closeRt.anchoredPosition = new Vector2(32f, 24f);
            closeBtn.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });

            _confirmButton = CampUiRuntime.CreateButton(_body, "开始远征", new Color(0.55f, 0.38f, 0.12f, 1f),
                new Vector2(180f, 48f));
            var confirmRt = _confirmButton.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(1f, 0f);
            confirmRt.anchorMax = new Vector2(1f, 0f);
            confirmRt.pivot = new Vector2(1f, 0f);
            confirmRt.anchoredPosition = new Vector2(-32f, 24f);
            _confirmButton.onClick.AddListener(() =>
            {
                if (_roster == null || !_roster.IsReadyForExpedition)
                    return;

                Hide();
                _onConfirm?.Invoke();
            });
        }

        void RebuildPartySummary()
        {
            foreach (Transform child in _partyRow)
                Destroy(child.gameObject);

            if (_roster == null)
                return;

            var layoutGo = CampUiRuntime.CreateRect("Layout", _partyRow);
            CampUiRuntime.StretchFull(layoutGo.GetComponent<RectTransform>());
            var h = layoutGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 20f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;

            for (var vi = 0; vi < _roster.Members.Count && vi < CampFormationDisplay.VisualOrderMemberIndices.Length; vi++)
            {
                var index = CampFormationDisplay.VisualOrderMemberIndices[vi];
                var member = _roster.Members[index];
                var card = CampUiRuntime.CreateImage("MemberSummary", layoutGo.transform,
                    new Color(0.12f, 0.16f, 0.24f, 0.95f)).gameObject;
                var rt = card.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(240f, 220f);
                var le = card.AddComponent<LayoutElement>();
                le.preferredWidth = 240f;
                le.preferredHeight = 220f;

                var portrait = CampUiRuntime.CreateImage("Portrait", card.transform, Color.white);
                portrait.sprite = _characterVisuals?.GetPortrait(member.CharacterDefinitionId);
                portrait.preserveAspect = true;
                var pRt = portrait.rectTransform;
                pRt.anchorMin = new Vector2(0.5f, 0.62f);
                pRt.anchorMax = new Vector2(0.5f, 0.62f);
                pRt.sizeDelta = new Vector2(120f, 120f);

                var name = CampUiRuntime.CreateText(card.transform, member.DisplayName, 18, FontStyle.Bold);
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(1f, 0f);
                name.rectTransform.offsetMin = new Vector2(8f, 52f);
                name.rectTransform.offsetMax = new Vector2(-8f, 84f);

                var slot = CampUiRuntime.CreateText(card.transform, CampFormationDisplay.SlotLabel(index),
                    14, FontStyle.Normal);
                slot.rectTransform.anchorMin = new Vector2(0f, 0f);
                slot.rectTransform.anchorMax = new Vector2(1f, 0f);
                slot.rectTransform.offsetMin = new Vector2(8f, 84f);
                slot.rectTransform.offsetMax = new Vector2(-8f, 108f);
                slot.color = new Color(0.72f, 0.78f, 0.9f, 1f);

                var deckCount = 0;
                foreach (var id in member.DeckCardIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        deckCount++;
                }

                var deck = CampUiRuntime.CreateText(card.transform, $"祭坛携带 {deckCount}/10", 15, FontStyle.Normal);
                deck.rectTransform.anchorMin = new Vector2(0f, 0f);
                deck.rectTransform.anchorMax = new Vector2(1f, 0f);
                deck.rectTransform.offsetMin = new Vector2(8f, 20f);
                deck.rectTransform.offsetMax = new Vector2(-8f, 48f);
                deck.color = deckCount == CampRosterState.DeckSize
                    ? new Color(0.7f, 0.95f, 0.72f, 1f)
                    : new Color(0.95f, 0.75f, 0.55f, 1f);
            }

            var ready = _roster.IsReadyForExpedition;
            _confirmButton.interactable = ready;
            RefreshStartLayerButtons();
        }

        void SelectStartLayer(int layer)
        {
            _selectedStartLayer = layer;
            RefreshStartLayerButtons();
        }

        void RefreshStartLayerButtons()
        {
            if (_caveStartButton == null)
                return;

            SetLayerButtonSelected(_caveStartButton, new Color(0.22f, 0.34f, 0.28f, 1f),
                _selectedStartLayer == 1);
            SetLayerButtonSelected(_dungeonStartButton, new Color(0.28f, 0.24f, 0.38f, 1f),
                _selectedStartLayer == ExpeditionRegionRules.DungeonStartLayer);
            SetLayerButtonSelected(_abyssStartButton, new Color(0.16f, 0.30f, 0.42f, 1f),
                _selectedStartLayer == ExpeditionRegionRules.AbyssStartLayer);
            SetLayerButtonSelected(_caveBossButton, new Color(0.42f, 0.24f, 0.20f, 1f),
                _selectedStartLayer == ExpeditionRegionRules.CaveBossLayer);
            SetLayerButtonSelected(_dungeonBossButton, new Color(0.38f, 0.22f, 0.28f, 1f),
                _selectedStartLayer == ExpeditionRegionRules.DungeonBossLayer);
            SetLayerButtonSelected(_abyssBossButton, new Color(0.18f, 0.28f, 0.44f, 1f),
                _selectedStartLayer == ExpeditionRegionRules.AbyssBossLayer);
        }

        static void SetLayerButtonSelected(Button button, Color baseColor, bool selected)
        {
            if (button == null)
                return;

            button.interactable = true;
            if (button.targetGraphic is Image img)
                img.color = selected ? Color.Lerp(baseColor, Color.white, 0.22f) : baseColor;
        }
    }
}
