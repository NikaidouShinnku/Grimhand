using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class ExpeditionCardAltarOverlayView : MonoBehaviour
    {
        const float CardScale = 0.74f;
        const float CardHolderWidth = 188f;
        const float CardHolderHeight = 272f;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Text _headerText;
        Text _previewText;
        Text _statusText;
        RectTransform _memberRow;
        RectTransform _deckRow;
        RectTransform _collectionRow;
        Button _skipButton;
        Button _confirmButton;
        InventoryTooltipView _tooltip;

        int _activeMemberIndex;
        bool _built;

        public void Initialize(
            BattleSession session,
            Transform parent,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var run = _session.Expedition.Run;
            if (run.Phase != ExpeditionPhase.ShrineChoice || run.CardAltar == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.SetAsLastSibling();
            _headerText.text = $"祭坛 · 第 {run.CardAltar.SourceLayer} 层";
            Rebuild();
        }

        void Rebuild()
        {
            _tooltip?.Hide();
            ClearRow(_memberRow);
            ClearRow(_deckRow);
            ClearRow(_collectionRow);
            var run = _session.Expedition.Run;
            if (run.Party.Count == 0)
                return;

            if (_activeMemberIndex >= run.Party.Count)
                _activeMemberIndex = 0;

            RebuildMemberTabs(run);
            var member = run.Party[_activeMemberIndex];
            RebuildDeckRow(member);
            RebuildCollectionRow(member);
            RefreshPreviewAndStatus(member);
        }

        void RebuildMemberTabs(ExpeditionRunState run)
        {
            for (var i = 0; i < run.Party.Count; i++)
            {
                var index = i;
                var member = run.Party[i];
                var active = index == _activeMemberIndex;
                var go = CreatePanelImage("MemberTab", _memberRow,
                    active ? new Color(0.35f, 0.48f, 0.72f, 0.95f) : new Color(0.16f, 0.18f, 0.24f, 0.95f));
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 300f;
                le.preferredHeight = 112f;

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = go.GetComponent<Image>();
                btn.onClick.AddListener(() =>
                {
                    _activeMemberIndex = index;
                    Rebuild();
                });

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(go.transform, false);
                var portraitRt = portraitGo.GetComponent<RectTransform>();
                portraitRt.anchorMin = new Vector2(0f, 0.5f);
                portraitRt.anchorMax = new Vector2(0f, 0.5f);
                portraitRt.pivot = new Vector2(0f, 0.5f);
                portraitRt.sizeDelta = new Vector2(84f, 84f);
                portraitRt.anchoredPosition = new Vector2(12f, 0f);
                var portrait = portraitGo.GetComponent<Image>();
                portrait.sprite = _characterVisuals?.GetPortrait(member.CharacterDefinitionId);
                portrait.preserveAspect = true;

                var name = CreateRowText(go.transform, member.DisplayName, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
                var nameRt = name.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(1f, 1f);
                nameRt.offsetMin = new Vector2(108f, 10f);
                nameRt.offsetMax = new Vector2(-12f, -10f);
            }
        }

        void RebuildDeckRow(PartyMemberSnapshot member)
        {
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
            {
                if (!needsReplace)
                {
                    SpawnCardButton(
                        _deckRow,
                        entry.Template,
                        member.CharacterDefinitionId,
                        selected: false,
                        onClick: null);
                    continue;
                }

                var capturedKey = entry.Key;
                var selected = draft.ReplaceDeckCardKey == capturedKey;
                SpawnCardButton(
                    _deckRow,
                    entry.Template,
                    member.CharacterDefinitionId,
                    selected,
                    () =>
                    {
                        var currentDraft = GetDraft(member);
                        var replaceKey = currentDraft.ReplaceDeckCardKey == capturedKey ? "" : capturedKey;
                        _session.SetCardAltarDraft(
                            member.CharacterDefinitionId,
                            currentDraft.CollectionCardIndex,
                            replaceKey);
                        Rebuild();
                    });
            }
        }

        void RebuildCollectionRow(PartyMemberSnapshot member)
        {
            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);
            foreach (var index in ExpeditionRunDeckRules.GetAvailableCollectionIndices(run, member))
            {
                var cardId = ExpeditionRunDeckCatalog.GetCampCollectionCardId(run, member, index);
                if (string.IsNullOrEmpty(cardId))
                    continue;

                var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(config, run, member, index);
                var capturedIndex = index;
                var selected = draft.CollectionCardIndex == capturedIndex;
                SpawnCollectionCardButton(
                    _collectionRow,
                    cardId,
                    member.CharacterDefinitionId,
                    template,
                    selected,
                    () =>
                    {
                        var currentDraft = GetDraft(member);
                        var collectionIndex = currentDraft.CollectionCardIndex == capturedIndex ? -1 : capturedIndex;
                        var replaceKey = needsReplace ? currentDraft.ReplaceDeckCardKey : "";
                        _session.SetCardAltarDraft(
                            member.CharacterDefinitionId,
                            collectionIndex,
                            replaceKey);
                        Rebuild();
                    });
            }
        }

        void SpawnCollectionCardButton(
            RectTransform parent,
            string cardId,
            string ownerCharacterId,
            CardTemplate template,
            bool selected,
            System.Action onClick)
        {
            _definitions.TryGetValue(cardId, out var definition);
            if (template == null && definition != null)
            {
                template = definition.ToTemplate();
                template.OwnerCharacterId = ownerCharacterId;
            }

            if (template != null && !string.IsNullOrEmpty(ownerCharacterId))
                template.OwnerCharacterId = ownerCharacterId;

            SpawnCardButton(parent, template, ownerCharacterId, selected, onClick);
        }

        ExpeditionCardAltarMemberDraft GetDraft(PartyMemberSnapshot member)
        {
            var altar = _session.Expedition.Run.CardAltar;
            if (altar == null)
                return new ExpeditionCardAltarMemberDraft();

            return altar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft)
                ? draft
                : new ExpeditionCardAltarMemberDraft();
        }

        void RefreshPreviewAndStatus(PartyMemberSnapshot member)
        {
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var deckCount = ExpeditionRunDeckRules.CountMemberDeck(config, member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);

            if (!draft.HasSelection)
            {
                _previewText.text = needsReplace
                    ? "从下方收藏中选择一张卡牌，并点选上方卡组中要替换的牌。"
                    : "从下方收藏中选择一张卡牌（将直接加入卡组）。";
                _statusText.text = $"当前卡组 {deckCount}/{ExpeditionRunDeckRules.DeckSize}";
                _confirmButton.interactable = HasAnyValidDraft();
                return;
            }

            var newTemplate = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(
                config,
                _session.Expedition.Run,
                member,
                draft.CollectionCardIndex);
            var newName = newTemplate?.DisplayName ?? "未知卡牌";

            if (needsReplace)
            {
                CardTemplate oldTemplate = null;
                if (!string.IsNullOrEmpty(draft.ReplaceDeckCardKey)
                    && ExpeditionRunDeckRules.TryFindMemberDeckEntryByKey(
                        config,
                        member,
                        draft.ReplaceDeckCardKey,
                        out var oldEntry))
                {
                    oldTemplate = oldEntry.Template;
                }

                var oldName = oldTemplate?.DisplayName ?? "（请选择要替换的卡牌）";
                _previewText.text = string.IsNullOrEmpty(draft.ReplaceDeckCardKey)
                    ? $"将加入：{newName}\n请先选择卡组中要替换的卡牌。"
                    : $"{oldName}  →  {newName}";
            }
            else
            {
                _previewText.text = $"将加入卡组：{newName}";
            }

            _statusText.text = $"当前卡组 {deckCount}/{ExpeditionRunDeckRules.DeckSize}";
            _confirmButton.interactable = HasAnyValidDraft();
        }

        bool HasAnyValidDraft()
        {
            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            if (run.CardAltar == null)
                return false;

            var anySelection = false;
            foreach (var member in run.Party)
            {
                if (!run.CardAltar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft) || !draft.HasSelection)
                    continue;

                anySelection = true;
                if (ExpeditionRunDeckRules.NeedsReplace(config, member) && string.IsNullOrEmpty(draft.ReplaceDeckCardKey))
                    return false;
            }

            return anySelection;
        }

        void SpawnCardButton(
            RectTransform parent,
            CardTemplate template,
            string ownerCharacterId,
            bool selected,
            System.Action onClick)
        {
            var holder = CreatePanelImage("CardHolder", parent,
                selected ? new Color(0.45f, 0.55f, 0.85f, 0.35f) : new Color(0.12f, 0.13f, 0.17f, 0.9f));
            var le = holder.AddComponent<LayoutElement>();
            le.preferredWidth = CardHolderWidth;
            le.preferredHeight = CardHolderHeight;

            if (onClick != null)
            {
                var btn = holder.AddComponent<Button>();
                btn.targetGraphic = holder.GetComponent<Image>();
                btn.onClick.AddListener(() => onClick.Invoke());
            }

            if (_cardPrefab == null || template == null)
                return;

            _definitions.TryGetValue(template.DefinitionId, out var definition);
            var ownerId = string.IsNullOrEmpty(ownerCharacterId) ? template.OwnerCharacterId : ownerCharacterId;
            var cardView = Instantiate(_cardPrefab, holder.transform);
            CardView.ApplyHandPresentationScaleCentered(cardView, CardScale);
            var preview = CardVisualResolver.CreatePreviewInstance(
                template.DefinitionId,
                ownerId,
                template.DisplayName,
                definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            cardView.BindWithCard(
                preview,
                visual,
                selected,
                false,
                false,
                "",
                statsLine,
                _uiIcons,
                _characterVisuals,
                null,
                null,
                null);

            var cg = cardView.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.blocksRaycasts = false;

            BindCardTooltip(holder, preview);
        }

        void BindCardTooltip(GameObject target, CardInstanceState card)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = StripRichText(BattleUiFormatters.BuildCardKeywordTooltip(
                _session?.Engine?.State,
                descCard,
                _definitions));
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("<b>", "").Replace("</b>", "");
        }

        GameObject CreatePanelImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        Text CreateRowText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = Color.white;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        Text CreateStaticText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
        {
            return CreateRowText(parent, text, size, style, anchor);
        }

        void ClearRow(RectTransform row)
        {
            if (row == null)
                return;

            foreach (Transform child in row)
                Destroy(child.gameObject);
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);

            if (!visible)
                _tooltip?.Hide();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;
            var go = new GameObject("ExpeditionCardAltarOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.02f, 0.03f);
            panelRt.anchorMax = new Vector2(0.98f, 0.97f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt);

            _headerText = CreateStaticText(panelGo.transform, "祭坛", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_headerText.rectTransform, 0.90f, 0.98f);
            _headerText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            var title = CreateStaticText(panelGo.transform, "召唤卡牌", 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(title.rectTransform, 0.84f, 0.90f);

            var memberRowGo = new GameObject("Members", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            memberRowGo.transform.SetParent(panelGo.transform, false);
            _memberRow = memberRowGo.GetComponent<RectTransform>();
            AnchorBand(_memberRow, 0.72f, 0.83f);
            var memberLayout = memberRowGo.GetComponent<HorizontalLayoutGroup>();
            memberLayout.spacing = 16f;
            memberLayout.childControlWidth = false;
            memberLayout.childControlHeight = false;
            memberLayout.childAlignment = TextAnchor.MiddleCenter;

            var hint = CreateStaticText(panelGo.transform,
                "从军营收藏中取出卡牌加入远征卡组（每位角色最多 1 张）",
                18, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(hint.rectTransform, 0.68f, 0.72f);
            hint.color = new Color(0.78f, 0.82f, 0.9f, 1f);

            var deckLabel = CreateStaticText(panelGo.transform, "当前卡组", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            AnchorBand(deckLabel.rectTransform, 0.64f, 0.68f);
            deckLabel.rectTransform.offsetMin = new Vector2(24f, 0f);

            _deckRow = BuildScrollRow(panelGo.transform, 0.46f, 0.63f);

            _previewText = CreateStaticText(panelGo.transform, "", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_previewText.rectTransform, 0.40f, 0.45f);

            var collectionLabel = CreateStaticText(panelGo.transform, "收藏（军营配置）", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            AnchorBand(collectionLabel.rectTransform, 0.36f, 0.40f);
            collectionLabel.rectTransform.offsetMin = new Vector2(24f, 0f);

            _collectionRow = BuildScrollRow(panelGo.transform, 0.12f, 0.35f);

            _statusText = CreateStaticText(panelGo.transform, "", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            AnchorBand(_statusText.rectTransform, 0.08f, 0.11f);
            _statusText.rectTransform.offsetMin = new Vector2(24f, 0f);
            _statusText.color = new Color(0.75f, 0.8f, 0.88f, 1f);

            _skipButton = CreateFooterButton(panelGo.transform, "跳过祭坛", new Vector2(-160f, 28f),
                new Vector2(220f, 52f), new Color(0.24f, 0.26f, 0.32f, 1f), () => _session.SkipCardAltar());
            _confirmButton = CreateFooterButton(panelGo.transform, "确认取出", new Vector2(160f, 28f),
                new Vector2(220f, 52f), new Color(0.28f, 0.48f, 0.34f, 1f), () => _session.ConfirmCardAltar());

            go.SetActive(false);
        }

        static void AnchorBand(RectTransform rt, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0f, minY);
            rt.anchorMax = new Vector2(1f, maxY);
            rt.offsetMin = new Vector2(20f, 0f);
            rt.offsetMax = new Vector2(-20f, 0f);
        }

        Button CreateFooterButton(
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = color;

            var text = CreateStaticText(go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        RectTransform BuildScrollRow(Transform parent, float minY, float maxY)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, minY);
            scrollRt.anchorMax = new Vector2(0.98f, maxY);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.45f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(viewportGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(0f, 0.5f);
            rowRt.pivot = new Vector2(0f, 0.5f);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            rowGo.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.viewport = viewportRt;
            scroll.content = rowRt;
            return rowRt;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
