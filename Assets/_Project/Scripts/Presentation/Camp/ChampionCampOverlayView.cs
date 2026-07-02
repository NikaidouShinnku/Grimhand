using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>军营：3 角色槽 + 每人 10 张牌，Demo 阶段全卡池可选。</summary>
    [DisallowMultipleComponent]
    public sealed class ChampionCampOverlayView : MonoBehaviour
    {
        const float CardScale = 0.68f;
        const int CardsPerRow = 5;
        const float DeckSlotWidth = 132f;
        const float DeckSlotHeight = 186f;

        BattleSetupSO _battleSetup;
        ExpeditionSetupSO _expeditionSetup;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        List<CardDefinitionSO> _cardPool = new();
        List<CharacterDefinitionSO> _playableCharacters = new();

        CampRosterState _roster;
        Action<CampRosterState> _onRosterChanged;
        Action _onClose;

        RectTransform _overlayRoot;
        RectTransform _body;
        RectTransform _memberRow;
        RectTransform _deckGrid;
        RectTransform _poolGrid;
        ScrollRect _poolScroll;
        Text _hintText;
        Button _closeButton;
        Button _confirmButton;
        InventoryTooltipView _tooltip;

        int _activeMemberIndex;
        int _selectedDeckSlot = -1;
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            BattleSetupSO battleSetup,
            ExpeditionSetupSO expeditionSetup,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action<CampRosterState> onRosterChanged,
            Action onClose)
        {
            _battleSetup = battleSetup;
            _expeditionSetup = expeditionSetup;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onRosterChanged = onRosterChanged;
            _onClose = onClose;

            _cardPool = CampRosterBuilder.BuildCardCatalog(expeditionSetup);
            EnsureCardPoolFallback();
            _playableCharacters = CollectPlayableCharacters();
            EnsureBuilt();
        }

        void EnsureCardPoolFallback()
        {
            if (_cardPool.Count > 0 || _battleSetup == null)
                return;

            var seen = new HashSet<string>();
            foreach (var character in _battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                foreach (var card in character.Deck)
                {
                    if (card == null || string.IsNullOrEmpty(card.CardId) || !seen.Add(card.CardId))
                        continue;

                    _cardPool.Add(card);
                }
            }

            foreach (var pair in _definitions)
            {
                var card = pair.Value;
                if (card == null || string.IsNullOrEmpty(card.CardId) || !seen.Add(card.CardId))
                    continue;

                _cardPool.Add(card);
            }
        }

        public void BindRoster(CampRosterState roster)
        {
            _roster = roster;
            if (IsOpen)
                Rebuild();
        }

        public void Show(CampRosterState roster)
        {
            _roster = roster;
            EnsureBuilt();
            SanitizeDuplicateCharacters();
            _overlayRoot.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _activeMemberIndex = 0;
            _selectedDeckSlot = 0;
            Rebuild();
        }

        public void Hide()
        {
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        List<CharacterDefinitionSO> CollectPlayableCharacters()
        {
            var list = new List<CharacterDefinitionSO>();
            if (_battleSetup?.Combatants == null)
                return list;

            var seen = new HashSet<string>();
            foreach (var character in _battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                if (!seen.Add(character.CharacterId))
                    continue;

                list.Add(character);
            }

            foreach (var id in CampRosterBuilder.PlayableCharacterIds)
            {
                if (seen.Contains(id))
                    continue;

                var fromSetup = FindCharacterInSetup(id);
                if (fromSetup != null && seen.Add(id))
                    list.Add(fromSetup);
            }

            list.Sort((a, b) => IndexOfPlayable(a.CharacterId).CompareTo(IndexOfPlayable(b.CharacterId)));
            return list;
        }

        CharacterDefinitionSO FindCharacterInSetup(string characterId)
        {
            if (_battleSetup?.Combatants == null)
                return null;

            foreach (var c in _battleSetup.Combatants)
            {
                if (c != null && c.CharacterId == characterId)
                    return c;
            }

            return null;
        }

        static int IndexOfPlayable(string characterId)
        {
            for (var i = 0; i < CampRosterBuilder.PlayableCharacterIds.Count; i++)
            {
                if (CampRosterBuilder.PlayableCharacterIds[i] == characterId)
                    return i;
            }

            return 99;
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

            _overlayRoot = CampUiRuntime.CreateRect("ChampionCampOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _overlayRoot, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            _body = CampUiRuntime.CreateImage("Body", _overlayRoot, new Color(0.07f, 0.08f, 0.11f, 0.98f))
                .rectTransform;
            _body.anchorMin = new Vector2(0.04f, 0.05f);
            _body.anchorMax = new Vector2(0.96f, 0.93f);
            _body.offsetMin = Vector2.zero;
            _body.offsetMax = Vector2.zero;

            var header = CampUiRuntime.CreateRect("Header", _body);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = new Vector2(20f, -58f);
            headerRt.offsetMax = new Vector2(-20f, -8f);

            var title = CampUiRuntime.CreateText(header.transform, "出征编队", 26, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            CampUiRuntime.StretchFull(title.rectTransform);

            _closeButton = CampUiRuntime.CreateButton(header.transform, "返回", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(120f, 40f));
            var closeRt = _closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(0f, 0f);
            _closeButton.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });

            _confirmButton = CampUiRuntime.CreateButton(header.transform, "保存编队", new Color(0.55f, 0.42f, 0.15f, 1f),
                new Vector2(140f, 40f));
            var confirmRt = _confirmButton.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(1f, 0.5f);
            confirmRt.anchorMax = new Vector2(1f, 0.5f);
            confirmRt.pivot = new Vector2(1f, 0.5f);
            confirmRt.anchoredPosition = new Vector2(-132f, 0f);
            _confirmButton.onClick.AddListener(SaveAndClose);

            _memberRow = CampUiRuntime.CreateRect("MemberRow", _body).GetComponent<RectTransform>();
            _memberRow.anchorMin = new Vector2(0f, 1f);
            _memberRow.anchorMax = new Vector2(1f, 1f);
            _memberRow.offsetMin = new Vector2(20f, -188f);
            _memberRow.offsetMax = new Vector2(-20f, -68f);

            var deckColumn = CampUiRuntime.CreateRect("DeckColumn", _body);
            var deckColumnRt = deckColumn.GetComponent<RectTransform>();
            deckColumnRt.anchorMin = new Vector2(0f, 0f);
            deckColumnRt.anchorMax = new Vector2(0.48f, 1f);
            deckColumnRt.offsetMin = new Vector2(20f, 52f);
            deckColumnRt.offsetMax = new Vector2(-8f, -196f);

            var deckLabel = CampUiRuntime.CreateText(deckColumn.transform, "当前卡组", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            deckLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            deckLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            deckLabel.rectTransform.offsetMin = new Vector2(0f, -32f);
            deckLabel.rectTransform.offsetMax = Vector2.zero;

            _deckGrid = CampUiRuntime.CreateRect("DeckGrid", deckColumn.transform).GetComponent<RectTransform>();
            _deckGrid.anchorMin = Vector2.zero;
            _deckGrid.anchorMax = Vector2.one;
            _deckGrid.offsetMin = Vector2.zero;
            _deckGrid.offsetMax = new Vector2(0f, -36f);
            var deckLayout = _deckGrid.gameObject.AddComponent<GridLayoutGroup>();
            deckLayout.cellSize = new Vector2(DeckSlotWidth, DeckSlotHeight);
            deckLayout.spacing = new Vector2(8f, 8f);
            deckLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            deckLayout.constraintCount = 5;
            deckLayout.childAlignment = TextAnchor.UpperLeft;

            var poolColumn = CampUiRuntime.CreateRect("PoolColumn", _body);
            var poolColumnRt = poolColumn.GetComponent<RectTransform>();
            poolColumnRt.anchorMin = new Vector2(0.52f, 0f);
            poolColumnRt.anchorMax = new Vector2(1f, 1f);
            poolColumnRt.offsetMin = new Vector2(0f, 52f);
            poolColumnRt.offsetMax = new Vector2(-20f, -196f);

            var poolLabel = CampUiRuntime.CreateText(poolColumn.transform, "卡牌库", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            poolLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            poolLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            poolLabel.rectTransform.offsetMin = new Vector2(0f, -32f);
            poolLabel.rectTransform.offsetMax = Vector2.zero;

            var scrollGo = CampUiRuntime.CreateRect("PoolScroll", poolColumn.transform);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -36f);
            scrollGo.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.65f);

            _poolScroll = scrollGo.AddComponent<ScrollRect>();
            _poolScroll.horizontal = false;
            _poolScroll.vertical = true;
            _poolScroll.movementType = ScrollRect.MovementType.Clamped;
            _poolScroll.scrollSensitivity = 36f;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0.1f, 0.11f, 0.14f, 0.01f);
            viewportImg.raycastTarget = true;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            _poolScroll.viewport = viewportRt;

            _poolGrid = CampUiRuntime.CreateRect("PoolGrid", viewport.transform).GetComponent<RectTransform>();
            _poolGrid.anchorMin = new Vector2(0f, 1f);
            _poolGrid.anchorMax = new Vector2(1f, 1f);
            _poolGrid.pivot = new Vector2(0.5f, 1f);
            _poolGrid.offsetMin = new Vector2(4f, 0f);
            _poolGrid.offsetMax = new Vector2(-4f, 0f);
            var poolLayout = _poolGrid.gameObject.AddComponent<GridLayoutGroup>();
            poolLayout.cellSize = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);
            poolLayout.spacing = new Vector2(6f, 6f);
            poolLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            poolLayout.constraintCount = CardsPerRow;
            poolLayout.childAlignment = TextAnchor.UpperLeft;
            var poolFitter = _poolGrid.gameObject.AddComponent<ContentSizeFitter>();
            poolFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            poolFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _poolScroll.content = _poolGrid;

            _hintText = CampUiRuntime.CreateText(_body, "", 15, FontStyle.Italic, TextAnchor.MiddleLeft);
            _hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hintText.rectTransform.offsetMin = new Vector2(20f, 10f);
            _hintText.rectTransform.offsetMax = new Vector2(-20f, 42f);
            _hintText.color = new Color(0.75f, 0.78f, 0.85f, 1f);

            _tooltip = _overlayRoot.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_overlayRoot);
        }

        void SaveAndClose()
        {
            EnsureRosterSize();
            SanitizeRosterCardOwnership();
            SanitizeDuplicateCharacters();
            _onRosterChanged?.Invoke(_roster);
            Hide();
            _onClose?.Invoke();
        }

        void SanitizeDuplicateCharacters()
        {
            if (_roster?.Members == null)
                return;

            var seen = new HashSet<string>();
            foreach (var member in _roster.Members)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                if (seen.Add(member.CharacterDefinitionId))
                    continue;

                member.CharacterDefinitionId = "";
                member.DisplayName = "";
                member.DeckCardIds.Clear();
                while (member.DeckCardIds.Count < CampRosterState.DeckSize)
                    member.DeckCardIds.Add("");
            }
        }

        void SanitizeRosterCardOwnership()
        {
            if (_roster == null)
                return;

            foreach (var member in _roster.Members)
            {
                if (member == null)
                    continue;

                for (var i = 0; i < member.DeckCardIds.Count; i++)
                {
                    var id = member.DeckCardIds[i];
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (!_definitions.TryGetValue(id, out var definition)
                        || !CampRosterBuilder.IsCardOwnedByCharacter(definition, member.CharacterDefinitionId))
                    {
                        member.DeckCardIds[i] = "";
                    }
                }
            }
        }

        void Rebuild()
        {
            _tooltip?.Hide();
            ClearDynamic();
            if (_roster == null)
                return;

            EnsureRosterSize();
            RebuildMemberRow();
            RebuildDeckSlots();
            RebuildCardPool();
            UpdateHint();
        }

        void EnsureRosterSize()
        {
            while (_roster.Members.Count < CampRosterState.PartySize)
                _roster.Members.Add(new CampMemberLoadout());

            while (_roster.Members.Count > CampRosterState.PartySize)
                _roster.Members.RemoveAt(_roster.Members.Count - 1);

            for (var i = 0; i < CampRosterState.PartySize; i++)
            {
                var member = _roster.Members[i];
                while (member.DeckCardIds.Count < CampRosterState.DeckSize)
                    member.DeckCardIds.Add("");
                while (member.DeckCardIds.Count > CampRosterState.DeckSize)
                    member.DeckCardIds.RemoveAt(member.DeckCardIds.Count - 1);
            }
        }

        void RebuildMemberRow()
        {
            var layoutGo = CampUiRuntime.CreateRect("MemberLayout", _memberRow);
            CampUiRuntime.StretchFull(layoutGo.GetComponent<RectTransform>());
            var h = layoutGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            _dynamicObjects.Add(layoutGo);

            for (var vi = 0; vi < CampRosterState.PartySize; vi++)
            {
                var index = CampFormationDisplay.VisualOrderMemberIndices[vi];
                var member = _roster.Members[index];
                var card = CreateMemberCard(layoutGo.transform, member, index, index == _activeMemberIndex);
                card.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _activeMemberIndex = index;
                    _selectedDeckSlot = 0;
                    Rebuild();
                });
            }
        }

        GameObject CreateMemberCard(
            Transform parent,
            CampMemberLoadout member,
            int memberIndex,
            bool active)
        {
            var go = CampUiRuntime.CreateImage("Member", parent,
                active ? new Color(0.35f, 0.48f, 0.72f, 0.95f) : new Color(0.16f, 0.18f, 0.24f, 0.95f)).gameObject;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 128f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 320f;
            le.preferredHeight = 128f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var portrait = CampUiRuntime.CreateImage("Portrait", go.transform, Color.white);
            portrait.preserveAspect = true;
            portrait.sprite = _characterVisuals != null
                ? _characterVisuals.GetPortrait(member.CharacterDefinitionId)
                : null;
            var portraitRt = portrait.rectTransform;
            portraitRt.anchorMin = new Vector2(0f, 0.5f);
            portraitRt.anchorMax = new Vector2(0f, 0.5f);
            portraitRt.pivot = new Vector2(0f, 0.5f);
            portraitRt.sizeDelta = new Vector2(88f, 88f);
            portraitRt.anchoredPosition = new Vector2(12f, 0f);

            var slotLabel = CampUiRuntime.CreateText(go.transform, CampFormationDisplay.SlotLabel(memberIndex),
                13, FontStyle.Bold, TextAnchor.UpperLeft);
            slotLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            slotLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            slotLabel.rectTransform.offsetMin = new Vector2(108f, -52f);
            slotLabel.rectTransform.offsetMax = new Vector2(-8f, -36f);
            slotLabel.color = new Color(0.75f, 0.82f, 0.95f, 1f);

            var name = CampUiRuntime.CreateText(go.transform,
                string.IsNullOrEmpty(member.DisplayName) ? "未选择" : member.DisplayName,
                18, FontStyle.Bold, TextAnchor.UpperLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(108f, -36f);
            name.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var filled = CountFilledSlots(member);
            var deckInfo = CampUiRuntime.CreateText(go.transform, $"卡组 {filled}/{CampRosterState.DeckSize}",
                15, FontStyle.Normal, TextAnchor.UpperLeft);
            deckInfo.rectTransform.anchorMin = new Vector2(0f, 0f);
            deckInfo.rectTransform.anchorMax = new Vector2(1f, 0f);
            deckInfo.rectTransform.offsetMin = new Vector2(108f, 36f);
            deckInfo.rectTransform.offsetMax = new Vector2(-120f, 64f);
            deckInfo.color = filled == CampRosterState.DeckSize
                ? new Color(0.7f, 0.95f, 0.72f, 1f)
                : new Color(0.95f, 0.75f, 0.55f, 1f);

            var swapBtn = CampUiRuntime.CreateButton(go.transform, "换人", new Color(0.22f, 0.28f, 0.38f, 1f),
                new Vector2(88f, 32f));
            var swapRt = swapBtn.GetComponent<RectTransform>();
            swapRt.anchorMin = new Vector2(1f, 0f);
            swapRt.anchorMax = new Vector2(1f, 0f);
            swapRt.pivot = new Vector2(1f, 0f);
            swapRt.anchoredPosition = new Vector2(-8f, 10f);
            swapBtn.onClick.AddListener(() => ShowCharacterPicker(memberIndex));

            _dynamicObjects.Add(go);
            return go;
        }

        void ShowCharacterPicker(int memberIndex)
        {
            _activeMemberIndex = memberIndex;
            var picker = CampUiRuntime.CreateImage("CharacterPicker", _body, new Color(0.05f, 0.06f, 0.09f, 0.98f))
                .gameObject;
            var rt = picker.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 360f);
            picker.transform.SetAsLastSibling();
            _dynamicObjects.Add(picker);

            var title = CampUiRuntime.CreateText(picker.transform, "选择角色", 22, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(16f, -48f);
            title.rectTransform.offsetMax = new Vector2(-16f, -8f);

            var row = CampUiRuntime.CreateRect("PickRow", picker.transform);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.15f);
            rowRt.anchorMax = new Vector2(1f, 0.85f);
            rowRt.offsetMin = new Vector2(16f, 0f);
            rowRt.offsetMax = new Vector2(-16f, 0f);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;

            foreach (var character in _playableCharacters)
            {
                var pickBtn = CampUiRuntime.CreateButton(row.transform, "",
                    new Color(0.2f, 0.32f, 0.48f, 1f), new Vector2(140f, 180f));
                var pickRt = pickBtn.GetComponent<RectTransform>();
                pickRt.sizeDelta = new Vector2(140f, 180f);

                var portrait = CampUiRuntime.CreateImage("Portrait", pickBtn.transform, Color.white);
                portrait.sprite = _characterVisuals?.GetPortrait(character.CharacterId);
                portrait.preserveAspect = true;
                var pRt = portrait.rectTransform;
                pRt.anchorMin = new Vector2(0.5f, 1f);
                pRt.anchorMax = new Vector2(0.5f, 1f);
                pRt.pivot = new Vector2(0.5f, 1f);
                pRt.anchoredPosition = new Vector2(0f, -12f);
                pRt.sizeDelta = new Vector2(96f, 96f);

                var nameLabel = CampUiRuntime.CreateText(pickBtn.transform,
                    CampRosterValidation.FindMemberIndexWithCharacter(_roster, character.CharacterId, memberIndex) >= 0
                        ? $"{character.DisplayName}\n(互换)"
                        : character.DisplayName,
                    16,
                    FontStyle.Bold, TextAnchor.UpperCenter);
                var nameRt = nameLabel.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(1f, 0f);
                nameRt.offsetMin = new Vector2(4f, 8f);
                nameRt.offsetMax = new Vector2(-4f, 36f);

                var captured = character;
                pickBtn.onClick.AddListener(() =>
                {
                    ApplyCharacter(memberIndex, captured);
                    Destroy(picker);
                    Rebuild();
                });
            }

            var cancel = CampUiRuntime.CreateButton(picker.transform, "取消", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(100f, 36f));
            var cancelRt = cancel.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(1f, 0f);
            cancelRt.anchorMax = new Vector2(1f, 0f);
            cancelRt.pivot = new Vector2(1f, 0f);
            cancelRt.anchoredPosition = new Vector2(-12f, 12f);
            cancel.onClick.AddListener(() => Destroy(picker));
        }

        void ApplyCharacter(int memberIndex, CharacterDefinitionSO character)
        {
            var duplicateIndex = CampRosterValidation.FindMemberIndexWithCharacter(
                _roster, character.CharacterId, memberIndex);
            if (duplicateIndex >= 0)
                CampRosterValidation.SwapMembers(_roster, memberIndex, duplicateIndex);

            var member = _roster.Members[memberIndex];
            member.CharacterDefinitionId = character.CharacterId;
            member.DisplayName = CharacterDisplayNames.GetOrFallback(character.CharacterId, character.DisplayName);

            var defaultLoadout = CampRosterBuilder.CreateDefaultMember(character, _cardPool);
            member.DeckCardIds.Clear();
            foreach (var id in defaultLoadout.DeckCardIds)
                member.DeckCardIds.Add(id);
        }

        void RebuildDeckSlots()
        {
            var member = _roster.Members[_activeMemberIndex];
            for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
            {
                var slotIndex = slot;
                var cardId = member.DeckCardIds[slot];
                var slotGo = CampUiRuntime.CreateImage($"DeckSlot{slot}", _deckGrid,
                    slotIndex == _selectedDeckSlot
                        ? new Color(0.45f, 0.55f, 0.85f, 0.35f)
                        : new Color(0.12f, 0.13f, 0.17f, 0.9f)).gameObject;

                var btn = slotGo.AddComponent<Button>();
                btn.targetGraphic = slotGo.GetComponent<Image>();
                btn.onClick.AddListener(() =>
                {
                    if (_selectedDeckSlot == slotIndex && !string.IsNullOrEmpty(member.DeckCardIds[slotIndex]))
                        member.DeckCardIds[slotIndex] = "";
                    else
                        _selectedDeckSlot = slotIndex;
                    Rebuild();
                });

                if (!string.IsNullOrEmpty(cardId) && _cardPrefab != null)
                {
                    _definitions.TryGetValue(cardId, out var definition);
                    var view = Instantiate(_cardPrefab, slotGo.transform);
                    CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                    var preview = CardVisualResolver.CreatePreviewInstance(
                        cardId,
                        member.CharacterDefinitionId,
                        definition?.DisplayName ?? cardId,
                        definition);
                    var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                    var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                    view.BindWithCard(preview, visual, false, false, false, "", statsLine,
                        _uiIcons, _characterVisuals, null, null, null);
                    var cg = view.GetComponent<CanvasGroup>();
                    if (cg != null)
                        cg.blocksRaycasts = false;
                    BindCardTooltip(slotGo, preview);
                }
                else
                {
                    var empty = CampUiRuntime.CreateText(slotGo.transform, $"槽 {slot + 1}", 14, FontStyle.Normal);
                    CampUiRuntime.StretchFull(empty.rectTransform);
                    empty.color = new Color(0.55f, 0.58f, 0.65f, 1f);
                }

                _dynamicObjects.Add(slotGo);
            }
        }

        void RebuildCardPool()
        {
            var member = _roster.Members[_activeMemberIndex];
            foreach (var card in _cardPool)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                if (!CampRosterBuilder.IsCardOwnedByCharacter(card, member.CharacterDefinitionId))
                    continue;

                var holder = CampUiRuntime.CreateRect(card.CardId, _poolGrid);
                var holderRt = holder.GetComponent<RectTransform>();
                holderRt.sizeDelta = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);

                if (_cardPrefab == null)
                    continue;

                var view = Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                var preview = CardVisualResolver.CreatePreviewInstance(
                    card.CardId,
                    member.CharacterDefinitionId,
                    card.DisplayName,
                    card);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                var capturedId = card.CardId;
                view.BindWithCard(preview, visual, false, false, true, "", statsLine,
                    _uiIcons, _characterVisuals,
                    _ => AssignCardToSelectedSlot(capturedId),
                    null,
                    null);
                BindCardTooltip(holder, preview);

                _dynamicObjects.Add(holder);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_poolGrid);
            _poolScroll.verticalNormalizedPosition = 1f;
            UpdateHint();
        }

        void BindCardTooltip(GameObject target, CardInstanceState card)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, descCard, _definitions)
                .Replace("<b>", "").Replace("</b>", "");
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        void AssignCardToSelectedSlot(string cardId)
        {
            if (_selectedDeckSlot < 0 || _selectedDeckSlot >= CampRosterState.DeckSize)
                return;

            var member = _roster.Members[_activeMemberIndex];
            _definitions.TryGetValue(cardId, out var definition);
            if (definition != null
                && !CampRosterBuilder.IsCardOwnedByCharacter(definition, member.CharacterDefinitionId))
                return;

            member.DeckCardIds[_selectedDeckSlot] = cardId;
            if (_selectedDeckSlot < CampRosterState.DeckSize - 1)
                _selectedDeckSlot++;
            Rebuild();
        }

        static int CountFilledSlots(CampMemberLoadout member)
        {
            var count = 0;
            foreach (var id in member.DeckCardIds)
            {
                if (!string.IsNullOrEmpty(id))
                    count++;
            }

            return count;
        }

        void UpdateHint()
        {
            if (_hintText == null || _roster == null)
                return;

            if (!CampRosterValidation.HasUniqueCharacters(_roster))
            {
                _hintText.text = "编队中存在重复角色，请为每个槽位选择不同角色。";
                return;
            }

            var member = _roster.Members[_activeMemberIndex];
            var ready = _roster.IsReadyForExpedition;
            _hintText.text = ready
                ? "编队已就绪。保存后可通过传送门开始远征。"
                : $"正在编辑：{member.DisplayName} — 选中槽位 {_selectedDeckSlot + 1}，点击右侧「{member.DisplayName}专属」卡牌填入；再次点击已填槽位可清空。";
        }

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }
    }
}
