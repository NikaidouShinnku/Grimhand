using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>天赋祭坛：角色列表 → 双槽位符文天赋配置。</summary>
    [DisallowMultipleComponent]
    public sealed class TalentCampOverlayView : MonoBehaviour
    {
        BattleSetupSO _battleSetup;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        CampMetaState _meta;
        Action<CampMetaState> _onMetaChanged;
        Action _onClose;

        List<CharacterDefinitionSO> _ownedCharacters = new();

        RectTransform _overlayRoot;
        RectTransform _listPanel;
        RectTransform _detailPanel;
        RectTransform _detailSlot1Row;
        RectTransform _detailSlot2Row;
        Text _detailTitle;
        Text _detailLevel;
        Text _detailSummary;
        Text _detailSlot1Label;
        Text _detailSlot2Label;
        Image _detailPortrait;
        GameObject _tooltipPanel;
        Text _tooltipTitle;
        Text _tooltipBody;

        string _detailCharacterId = "";
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            BattleSetupSO battleSetup,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Action<CampMetaState> onMetaChanged,
            Action onClose)
        {
            _battleSetup = battleSetup;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _onMetaChanged = onMetaChanged;
            _onClose = onClose;
            _ownedCharacters = CollectOwnedCharacters();
            EnsureBuilt();
        }

        public void Show(CampMetaState meta)
        {
            _meta = meta ?? CampMetaState.CreateDefaultDemo();
            _ownedCharacters = CollectOwnedCharacters();
            EnsureBuilt();
            HideTooltip();
            _overlayRoot.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowListPanel();
            RebuildList();
        }

        public void Hide()
        {
            HideTooltip();
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        List<CharacterDefinitionSO> CollectOwnedCharacters()
        {
            var list = new List<CharacterDefinitionSO>();
            var seen = new HashSet<string>();

            if (_battleSetup?.Combatants != null)
            {
                foreach (var character in _battleSetup.Combatants)
                {
                    if (character == null || character.Team != TeamSide.Player)
                        continue;

                    if (!IsPlayableCharacter(character.CharacterId) || !seen.Add(character.CharacterId))
                        continue;

                    list.Add(character);
                }
            }

            foreach (var id in TalentCatalog.PlayableCharacterIds)
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

        static bool IsPlayableCharacter(string characterId)
        {
            foreach (var id in TalentCatalog.PlayableCharacterIds)
            {
                if (id == characterId)
                    return true;
            }

            return false;
        }

        static int IndexOfPlayable(string characterId)
        {
            for (var i = 0; i < TalentCatalog.PlayableCharacterIds.Count; i++)
            {
                if (TalentCatalog.PlayableCharacterIds[i] == characterId)
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

            _overlayRoot = CampUiRuntime.CreateRect("TalentCampOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _overlayRoot, new Color(0.03f, 0.05f, 0.09f, 0.96f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            BuildTooltip();
            BuildListPanel();
            BuildDetailPanel();
        }

        void BuildTooltip()
        {
            _tooltipPanel = CampUiRuntime.CreateImage("TalentTooltip", _overlayRoot,
                new Color(0.08f, 0.1f, 0.14f, 0.98f)).gameObject;
            var rt = _tooltipPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 140f);
            rt.sizeDelta = new Vector2(640f, 120f);

            var outline = _tooltipPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.65f, 0.28f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _tooltipTitle = CampUiRuntime.CreateText(_tooltipPanel.transform, "", 17, FontStyle.Bold,
                TextAnchor.UpperLeft);
            _tooltipTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tooltipTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipTitle.rectTransform.offsetMin = new Vector2(16f, -34f);
            _tooltipTitle.rectTransform.offsetMax = new Vector2(-16f, -8f);
            _tooltipTitle.color = new Color(0.95f, 0.88f, 0.55f, 1f);

            _tooltipBody = CampUiRuntime.CreateText(_tooltipPanel.transform, "", 15, FontStyle.Normal,
                TextAnchor.UpperLeft);
            _tooltipBody.rectTransform.anchorMin = Vector2.zero;
            _tooltipBody.rectTransform.anchorMax = Vector2.one;
            _tooltipBody.rectTransform.offsetMin = new Vector2(16f, 12f);
            _tooltipBody.rectTransform.offsetMax = new Vector2(-16f, -38f);
            _tooltipBody.color = new Color(0.88f, 0.9f, 0.96f, 1f);
            _tooltipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltipBody.verticalOverflow = VerticalWrapMode.Overflow;

            _tooltipPanel.SetActive(false);
        }

        void BuildListPanel()
        {
            _listPanel = CampUiRuntime.CreateImage("ListPanel", _overlayRoot, new Color(0.09f, 0.1f, 0.14f, 0.98f))
                .rectTransform;
            CampUiRuntime.Stretch(_listPanel, 48f, 48f, -48f, -48f);

            var title = CampUiRuntime.CreateText(_listPanel, "天赋祭坛", 28, FontStyle.Bold, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, -52f);
            title.rectTransform.offsetMax = new Vector2(0f, -8f);

            var closeBtn = CampUiRuntime.CreateButton(_listPanel, "返回营地", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 42f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeBtn.onClick.AddListener(CloseOverlay);

            var hint = CampUiRuntime.CreateText(_listPanel,
                "选择角色进入天赋配置。局外等级解锁候选符文，每槽位装备一枚。鼠标悬停符文可查看完整描述。",
                16, FontStyle.Italic, TextAnchor.UpperCenter);
            hint.rectTransform.anchorMin = new Vector2(0f, 1f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.offsetMin = new Vector2(24f, -88f);
            hint.rectTransform.offsetMax = new Vector2(-24f, -56f);
            hint.color = new Color(0.72f, 0.76f, 0.84f, 1f);

            CampUiRuntime.CreateRect("CharacterRowHost", _listPanel);
            var rowHostRt = _listPanel.Find("CharacterRowHost").GetComponent<RectTransform>();
            rowHostRt.anchorMin = new Vector2(0f, 0.08f);
            rowHostRt.anchorMax = new Vector2(1f, 0.82f);
            rowHostRt.offsetMin = new Vector2(32f, 0f);
            rowHostRt.offsetMax = new Vector2(-32f, 0f);
        }

        void BuildDetailPanel()
        {
            _detailPanel = CampUiRuntime.CreateImage("DetailPanel", _overlayRoot, new Color(0.09f, 0.1f, 0.14f, 0.98f))
                .rectTransform;
            CampUiRuntime.Stretch(_detailPanel, 32f, 32f, -32f, -32f);
            _detailPanel.gameObject.SetActive(false);

            var backBtn = CampUiRuntime.CreateButton(_detailPanel, "返回角色列表", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 42f));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 1f);
            backRt.anchorMax = new Vector2(1f, 1f);
            backRt.pivot = new Vector2(1f, 1f);
            backRt.anchoredPosition = new Vector2(-8f, -8f);
            backBtn.onClick.AddListener(ShowListPanel);

            var top = CampUiRuntime.CreateRect("DetailTop", _detailPanel);
            var topRt = top.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.offsetMin = new Vector2(20f, -148f);
            topRt.offsetMax = new Vector2(-20f, -52f);

            _detailPortrait = CampUiRuntime.CreateImage("Portrait", top.transform, Color.white);
            _detailPortrait.preserveAspect = true;
            var portraitRt = _detailPortrait.rectTransform;
            portraitRt.anchorMin = new Vector2(0f, 0f);
            portraitRt.anchorMax = new Vector2(0f, 1f);
            portraitRt.pivot = new Vector2(0f, 0.5f);
            portraitRt.sizeDelta = new Vector2(96f, 96f);

            _detailTitle = CampUiRuntime.CreateText(top.transform, "", 26, FontStyle.Bold, TextAnchor.UpperLeft);
            _detailTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailTitle.rectTransform.offsetMin = new Vector2(112f, -40f);
            _detailTitle.rectTransform.offsetMax = new Vector2(-8f, -8f);

            _detailLevel = CampUiRuntime.CreateText(top.transform, "", 18, FontStyle.Normal, TextAnchor.UpperLeft);
            _detailLevel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailLevel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailLevel.rectTransform.offsetMin = new Vector2(112f, -72f);
            _detailLevel.rectTransform.offsetMax = new Vector2(-8f, -44f);
            _detailLevel.color = new Color(0.82f, 0.86f, 0.95f, 1f);

            var summaryBox = CampUiRuntime.CreateImage("SummaryBox", _detailPanel, new Color(0.12f, 0.14f, 0.19f, 0.95f));
            var summaryRt = summaryBox.rectTransform;
            summaryRt.anchorMin = new Vector2(0f, 1f);
            summaryRt.anchorMax = new Vector2(1f, 1f);
            summaryRt.offsetMin = new Vector2(20f, -212f);
            summaryRt.offsetMax = new Vector2(-20f, -156f);

            var summaryLabel = CampUiRuntime.CreateText(summaryBox.transform, "当前生效效果", 15, FontStyle.Bold,
                TextAnchor.UpperLeft);
            summaryLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            summaryLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            summaryLabel.rectTransform.offsetMin = new Vector2(12f, -26f);
            summaryLabel.rectTransform.offsetMax = new Vector2(-12f, -4f);

            _detailSummary = CampUiRuntime.CreateText(summaryBox.transform, "", 14, FontStyle.Normal,
                TextAnchor.UpperLeft);
            _detailSummary.rectTransform.anchorMin = Vector2.zero;
            _detailSummary.rectTransform.anchorMax = Vector2.one;
            _detailSummary.rectTransform.offsetMin = new Vector2(12f, 8f);
            _detailSummary.rectTransform.offsetMax = new Vector2(-12f, -28f);
            _detailSummary.color = new Color(0.82f, 0.88f, 0.98f, 1f);

            BuildSlotSection(_detailPanel, "Slot1Section", 0.52f, 0.74f, out _detailSlot1Label, out _detailSlot1Row);
            BuildSlotSection(_detailPanel, "Slot2Section", 0.24f, 0.46f, out _detailSlot2Label, out _detailSlot2Row);
        }

        void BuildSlotSection(
            RectTransform parent,
            string name,
            float yMin,
            float yMax,
            out Text slotLabel,
            out RectTransform cardRow)
        {
            var section = CampUiRuntime.CreateRect(name, parent);
            var sectionRt = section.GetComponent<RectTransform>();
            sectionRt.anchorMin = new Vector2(0f, yMin);
            sectionRt.anchorMax = new Vector2(1f, yMax);
            sectionRt.offsetMin = new Vector2(20f, 0f);
            sectionRt.offsetMax = new Vector2(-20f, 0f);

            slotLabel = CampUiRuntime.CreateText(section.transform, "", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            slotLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            slotLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            slotLabel.rectTransform.offsetMin = new Vector2(0f, -26f);
            slotLabel.rectTransform.offsetMax = Vector2.zero;

            var scrollGo = CampUiRuntime.CreateRect("Scroll", section.transform);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -28f);
            scrollGo.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.65f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewportRt;

            cardRow = CampUiRuntime.CreateRect("Cards", viewport.transform).GetComponent<RectTransform>();
            cardRow.anchorMin = new Vector2(0f, 0f);
            cardRow.anchorMax = new Vector2(0f, 1f);
            cardRow.pivot = new Vector2(0f, 0.5f);
            cardRow.sizeDelta = new Vector2(900f, 0f);

            var layout = cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = cardRow.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = cardRow;
        }

        void CloseOverlay()
        {
            _onMetaChanged?.Invoke(_meta);
            Hide();
            _onClose?.Invoke();
        }

        void ShowListPanel()
        {
            _detailCharacterId = "";
            HideTooltip();
            _listPanel.gameObject.SetActive(true);
            _detailPanel.gameObject.SetActive(false);
        }

        void OpenDetail(string characterId, string displayName)
        {
            _detailCharacterId = characterId;
            HideTooltip();
            _listPanel.gameObject.SetActive(false);
            _detailPanel.gameObject.SetActive(true);

            var progress = _meta.GetOrCreate(characterId);
            _detailTitle.text = $"角色：{displayName}";
            _detailLevel.text = $"局外等级 Lv.{progress.OutOfRunLevel}    经验 {progress.OutOfRunXp}";
            _detailPortrait.sprite = _characterVisuals != null
                ? _characterVisuals.GetPortrait(characterId)
                : null;
            RefreshDetailSummary(progress);

            RefreshSlotLabel(_detailSlot1Label, progress, 1);
            RefreshSlotLabel(_detailSlot2Label, progress, 2);
            RebuildSlotRow(_detailSlot1Row, characterId, 1, progress);
            RebuildSlotRow(_detailSlot2Row, characterId, 2, progress);
        }

        void RefreshSlotLabel(Text label, CharacterMetaProgress progress, int slot)
        {
            if (label == null)
                return;

            var selectedId = progress.GetSelectedTalentId(slot);
            if (string.IsNullOrEmpty(selectedId))
            {
                label.text = $"槽位 {slot}（已选：无）";
                return;
            }

            var talent = TalentCatalog.Get(selectedId);
            label.text = talent != null
                ? $"槽位 {slot}（已选：{talent.ShortTitle}）"
                : $"槽位 {slot}（已选：无）";
        }

        void RefreshDetailSummary(CharacterMetaProgress progress)
        {
            _detailSummary.text = TalentRules.BuildActiveEffectsSummary(progress);
        }

        void RebuildList()
        {
            ClearDynamic();
            var host = _listPanel.Find("CharacterRowHost");
            if (host == null)
                return;

            if (_ownedCharacters.Count == 0)
            {
                var empty = CampUiRuntime.CreateText(host, "未找到可配置的角色（战士 / 法老 / 恶魔）。",
                    18, FontStyle.Bold, TextAnchor.MiddleCenter);
                CampUiRuntime.StretchFull(empty.rectTransform);
                empty.color = new Color(0.95f, 0.7f, 0.55f, 1f);
                _dynamicObjects.Add(empty.gameObject);
                return;
            }

            var row = CampUiRuntime.CreateRect("CharacterRow", host);
            CampUiRuntime.StretchFull(row.GetComponent<RectTransform>());
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;
            _dynamicObjects.Add(row);

            foreach (var character in _ownedCharacters)
            {
                var progress = _meta.GetOrCreate(character.CharacterId);
                var card = CreateCharacterListCard(row.transform, character, progress);
                var capturedId = character.CharacterId;
                var capturedName = character.DisplayName;
                card.GetComponent<Button>().onClick.AddListener(() => OpenDetail(capturedId, capturedName));
            }
        }

        GameObject CreateCharacterListCard(
            Transform parent,
            CharacterDefinitionSO character,
            CharacterMetaProgress progress)
        {
            var go = CampUiRuntime.CreateImage("CharacterCard", parent, new Color(0.14f, 0.17f, 0.24f, 0.96f))
                .gameObject;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300f, 340f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 300f;
            le.preferredHeight = 340f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var portrait = CampUiRuntime.CreateImage("Portrait", go.transform, Color.white);
            portrait.preserveAspect = true;
            portrait.sprite = _characterVisuals?.GetPortrait(character.CharacterId);
            var portraitRt = portrait.rectTransform;
            portraitRt.anchorMin = new Vector2(0.5f, 1f);
            portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.anchoredPosition = new Vector2(0f, -20f);
            portraitRt.sizeDelta = new Vector2(128f, 128f);

            var name = CampUiRuntime.CreateText(go.transform, character.DisplayName, 22, FontStyle.Bold,
                TextAnchor.UpperCenter);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(8f, -168f);
            name.rectTransform.offsetMax = new Vector2(-8f, -136f);

            var level = CampUiRuntime.CreateText(go.transform, $"Lv.{progress.OutOfRunLevel}  经验 {progress.OutOfRunXp}",
                16, FontStyle.Normal, TextAnchor.UpperCenter);
            level.rectTransform.anchorMin = new Vector2(0f, 1f);
            level.rectTransform.anchorMax = new Vector2(1f, 1f);
            level.rectTransform.offsetMin = new Vector2(8f, -200f);
            level.rectTransform.offsetMax = new Vector2(-8f, -172f);
            level.color = new Color(0.8f, 0.84f, 0.92f, 1f);

            var summary = CampUiRuntime.CreateText(go.transform, TalentRules.BuildActiveEffectsSummary(progress),
                14, FontStyle.Italic, TextAnchor.UpperLeft);
            summary.rectTransform.anchorMin = new Vector2(0f, 0f);
            summary.rectTransform.anchorMax = new Vector2(1f, 0f);
            summary.rectTransform.offsetMin = new Vector2(12f, 48f);
            summary.rectTransform.offsetMax = new Vector2(-12f, 128f);
            summary.color = new Color(0.68f, 0.74f, 0.86f, 1f);

            var enter = CampUiRuntime.CreateText(go.transform, "点击进入天赋配置 →", 14, FontStyle.Bold,
                TextAnchor.LowerCenter);
            enter.rectTransform.anchorMin = new Vector2(0f, 0f);
            enter.rectTransform.anchorMax = new Vector2(1f, 0f);
            enter.rectTransform.offsetMin = new Vector2(8f, 12f);
            enter.rectTransform.offsetMax = new Vector2(-8f, 40f);
            enter.color = new Color(0.95f, 0.84f, 0.48f, 1f);

            _dynamicObjects.Add(go);
            return go;
        }

        void RebuildSlotRow(RectTransform row, string characterId, int slot, CharacterMetaProgress progress)
        {
            if (row == null)
                return;

            for (var i = row.childCount - 1; i >= 0; i--)
                Destroy(row.GetChild(i).gameObject);

            var runeSprite = _uiIcons != null ? _uiIcons.TalentRunePlate : null;
            foreach (var talent in TalentCatalog.GetSlotTalents(characterId, slot))
            {
                var state = TalentRules.GetCardState(talent, progress);
                var stoneGo = CampUiRuntime.CreateRect($"Talent_{talent.Id}", row);
                var stone = stoneGo.AddComponent<TalentRuneStoneView>();
                stone.Bind(
                    talent,
                    state,
                    runeSprite,
                    OnTalentCardClicked,
                    ShowTooltip,
                    HideTooltip);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }

        void OnTalentCardClicked(TalentDefinition talent)
        {
            if (talent == null || string.IsNullOrEmpty(_detailCharacterId))
                return;

            var progress = _meta.GetOrCreate(_detailCharacterId);
            if (!TalentRules.IsUnlocked(talent, progress))
                return;

            TalentRules.TryToggleSelection(talent, progress);
            _onMetaChanged?.Invoke(_meta);

            var character = FindCharacter(_detailCharacterId);
            OpenDetail(_detailCharacterId, character?.DisplayName ?? _detailCharacterId);
        }

        void ShowTooltip(TalentDefinition talent, TalentCardState state)
        {
            if (talent == null || _tooltipPanel == null)
                return;

            var locked = state == TalentCardState.Locked;
            _tooltipTitle.text = locked
                ? $"{talent.ShortTitle}（Lv.{talent.UnlockLevel} 解锁）"
                : $"{talent.ShortTitle}（槽位 {talent.Slot} · Lv.{talent.UnlockLevel}）";

            _tooltipBody.text = locked
                ? $"需要局外等级 Lv.{talent.UnlockLevel} 才能解锁此天赋。\n\n{talent.Description}"
                : talent.Description;

            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling();
        }

        void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.SetActive(false);
        }

        CharacterDefinitionSO FindCharacter(string characterId)
        {
            foreach (var character in _ownedCharacters)
            {
                if (character.CharacterId == characterId)
                    return character;
            }

            return FindCharacterInSetup(characterId);
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
