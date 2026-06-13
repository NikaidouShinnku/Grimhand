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
        Text _statusText;
        Button _confirmButton;
        Button _caveStartButton;
        Button _dungeonStartButton;
        Button _abyssStartButton;
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
            difficulty.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            difficulty.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            difficulty.rectTransform.offsetMin = new Vector2(32f, 0f);
            difficulty.rectTransform.offsetMax = new Vector2(-32f, 0f);

            var regionLabel = CampUiRuntime.CreateText(_body, "起始区域", 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            regionLabel.rectTransform.anchorMin = new Vector2(0f, 0.20f);
            regionLabel.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            regionLabel.rectTransform.offsetMin = new Vector2(32f, 0f);
            regionLabel.rectTransform.offsetMax = new Vector2(-32f, 0f);

            _caveStartButton = CampUiRuntime.CreateButton(_body, "洞穴（1层）", new Color(0.22f, 0.34f, 0.28f, 1f),
                new Vector2(132f, 40f));
            var caveRt = _caveStartButton.GetComponent<RectTransform>();
            caveRt.anchorMin = new Vector2(0.5f, 0f);
            caveRt.anchorMax = new Vector2(0.5f, 0f);
            caveRt.pivot = new Vector2(0.5f, 0f);
            caveRt.anchoredPosition = new Vector2(-150f, 72f);

            _dungeonStartButton = CampUiRuntime.CreateButton(_body, "地牢（21层）", new Color(0.28f, 0.24f, 0.38f, 1f),
                new Vector2(132f, 40f));
            var dungeonRt = _dungeonStartButton.GetComponent<RectTransform>();
            dungeonRt.anchorMin = new Vector2(0.5f, 0f);
            dungeonRt.anchorMax = new Vector2(0.5f, 0f);
            dungeonRt.pivot = new Vector2(0.5f, 0f);
            dungeonRt.anchoredPosition = new Vector2(0f, 72f);

            _abyssStartButton = CampUiRuntime.CreateButton(_body, "海渊（41层）", new Color(0.16f, 0.30f, 0.42f, 1f),
                new Vector2(132f, 40f));
            var abyssRt = _abyssStartButton.GetComponent<RectTransform>();
            abyssRt.anchorMin = new Vector2(0.5f, 0f);
            abyssRt.anchorMax = new Vector2(0.5f, 0f);
            abyssRt.pivot = new Vector2(0.5f, 0f);
            abyssRt.anchoredPosition = new Vector2(150f, 72f);

            _caveStartButton.onClick.AddListener(() => SelectStartLayer(1));
            _dungeonStartButton.onClick.AddListener(() => SelectStartLayer(21));
            _abyssStartButton.onClick.AddListener(() => SelectStartLayer(41));

            _statusText = CampUiRuntime.CreateText(_body, "", 16, FontStyle.Italic, TextAnchor.MiddleLeft);
            _statusText.rectTransform.anchorMin = new Vector2(0f, 0.04f);
            _statusText.rectTransform.anchorMax = new Vector2(1f, 0.12f);
            _statusText.rectTransform.offsetMin = new Vector2(32f, 0f);
            _statusText.rectTransform.offsetMax = new Vector2(-32f, 0f);
            _statusText.color = new Color(0.95f, 0.72f, 0.55f, 1f);

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

            for (var i = 0; i < _roster.Members.Count; i++)
            {
                var member = _roster.Members[i];
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

                var deckCount = 0;
                foreach (var id in member.DeckCardIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        deckCount++;
                }

                var deck = CampUiRuntime.CreateText(card.transform, $"卡组 {deckCount}/10", 15, FontStyle.Normal);
                deck.rectTransform.anchorMin = new Vector2(0f, 0f);
                deck.rectTransform.anchorMax = new Vector2(1f, 0f);
                deck.rectTransform.offsetMin = new Vector2(8f, 20f);
                deck.rectTransform.offsetMax = new Vector2(-8f, 48f);
                deck.color = deckCount == CampRosterState.DeckSize
                    ? new Color(0.7f, 0.95f, 0.72f, 1f)
                    : new Color(0.95f, 0.75f, 0.55f, 1f);
            }

            var ready = _roster.IsReadyForExpedition;
            _statusText.text = ready
                ? "队伍就绪，可以出发。"
                : "请先在军营补全 3 名角色与每人 10 张卡牌。";
            _statusText.color = ready
                ? new Color(0.7f, 0.95f, 0.72f, 1f)
                : new Color(0.95f, 0.72f, 0.55f, 1f);
            _confirmButton.interactable = ready;
            RefreshRegionButtons();
        }

        void SelectStartLayer(int layer)
        {
            _selectedStartLayer = layer;
            RefreshRegionButtons();
        }

        void RefreshRegionButtons()
        {
            if (_caveStartButton == null || _dungeonStartButton == null || _abyssStartButton == null)
                return;

            _caveStartButton.interactable = _selectedStartLayer != 1;
            _dungeonStartButton.interactable = _selectedStartLayer != ExpeditionRegionRules.DungeonStartLayer;
            _abyssStartButton.interactable = _selectedStartLayer != ExpeditionRegionRules.AbyssStartLayer;
        }
    }
}
