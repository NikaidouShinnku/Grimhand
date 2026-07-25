using System;
using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleInventoryPanelView : MonoBehaviour
    {
        const float PanelWidth = 1240f;
        const float PanelHeight = 860f;
        const float ConsumableStripWidth = 112f;
        const float ConsumableSlotSize = 96f;
        const float RelicSlotWidth = 108f;
        const float RelicSlotHeight = 120f;
        const float CardScale = 0.98f;
        const int CardsPerRow = 5;
        const float CardGridHorizontalPadding = 28f;
        const float CharacterCardWidth = 228f;
        const float CharacterCardHeight = 318f;
        const float CharacterPortraitSize = 168f;

        const int InventoryLayoutVersion = 10;

        BattleSession _session;
        Transform _battleRoot;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        ConsumableVisualCatalogSO _consumableCatalog;
        BattleUiIconCatalogSO _icons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _panel;
        int _layoutVersion;
        RectTransform _goldRow;
        Text _goldText;
        RectTransform _consumableDetailRoot;
        RectTransform _consumableDetailBox;
        Text _consumableDetailTitle;
        Text _consumableDetailBody;
        Button _consumableDiscardButton;
        int _consumableDetailSlot = -1;
        Image _goldIcon;
        Text _xpText;
        Image _xpIcon;
        RectTransform _mainContent;
        RectTransform _consumableStrip;
        RectTransform _characterRow;
        RectTransform _relicRow;
        RectTransform _cardArea;
        ScrollRect _scroll;
        InventoryTooltipView _tooltip;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public Action OnConsumableUseStarted;

        public void Initialize(
            BattleSession session,
            Transform root,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            RelicVisualCatalogSO relicCatalog,
            ConsumableVisualCatalogSO consumableCatalog,
            BattleUiIconCatalogSO icons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _battleRoot = root;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _relicCatalog = relicCatalog;
            _consumableCatalog = consumableCatalog;
            _icons = icons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(root);
        }

        public void Toggle()
        {
            if (_battleRoot != null)
                EnsureBuilt(_battleRoot);

            if (_panel == null)
                return;

            if (IsOpen)
                Hide();
            else
                Show();
        }

        public void Hide()
        {
            HideConsumableDetail();
            _tooltip?.Hide();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (_battleRoot != null)
                EnsureBuilt(_battleRoot);

            if (_panel == null || !IsOpen)
                return;

            Rebuild();
        }

        void Rebuild()
        {
            ClearDynamic();
            RefreshCurrency();
            RefreshCharacters();
            RefreshRelics();
            RefreshCards();
            RefreshConsumables();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_mainContent);
        }

        void Show()
        {
            _panel.gameObject.SetActive(true);
            CombatantTooltipLayer.MountToFront(_panel, _battleRoot);
            Rebuild();
        }

        void RefreshCurrency()
        {
            if (_goldText == null)
                return;

            var gold = _session?.IsExpeditionMode == true ? _session.Expedition.Run.Gold : 0;
            _goldText.text = gold.ToString();
            if (_goldIcon != null && _icons?.GoldIcon != null)
            {
                _goldIcon.sprite = _icons.GoldIcon;
                _goldIcon.color = Color.white;
            }

            if (_xpText == null || _xpIcon == null)
                return;

            var showXp = _session?.IsExpeditionMode == true;
            _xpIcon.transform.parent.gameObject.SetActive(showXp);
            if (!showXp)
                return;

            var xp = _session.Expedition.Run.SharedXpPool;
            _xpText.text = xp.ToString();
            if (_icons?.XpIcon != null)
            {
                _xpIcon.sprite = _icons.XpIcon;
                _xpIcon.color = Color.white;
            }
        }

        void RefreshCharacters()
        {
            if (_characterRow == null)
                return;

            if (ShouldShowBattleCardPiles())
            {
                if (_session?.Engine == null)
                    return;

                BuildCharacterCardsFromCombatants(_session.Engine.State.Combatants);
                return;
            }

            if (!_session.IsExpeditionMode)
                return;

            var party = _session.Expedition.Run.Party;
            var limit = System.Math.Min(party.Count, CampRosterState.PartySize);
            for (var i = 0; i < limit; i++)
                BuildCharacterCardFromPartyMember(party[i]);
        }

        void BuildCharacterCardsFromCombatants(IReadOnlyList<CombatantState> combatants)
        {
            var shown = 0;
            foreach (var unit in combatants)
            {
                if (unit.Team != TeamSide.Player)
                    continue;

                if (shown >= CampRosterState.PartySize)
                    break;

                BuildCharacterCard(unit.DisplayName, unit.CharacterDefinitionId, unit.Level, unit.Xp, unit.Hp, unit.MaxHp,
                    StatusRules.GetEffectiveSpeed(unit));
                shown++;
            }
        }

        void BuildCharacterCardFromPartyMember(PartyMemberSnapshot member)
        {
            var run = _session.Expedition.Run;
            var hpBonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(
                run.Party,
                run.Relics,
                run.RelicGrowthTiers);
            var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, member.Level);
            var maxHp = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, hpBonus);
            var hp = System.Math.Min(member.Hp, maxHp);
            BuildCharacterCard(member.DisplayName, member.CharacterDefinitionId, member.Level, member.Xp, hp,
                maxHp, stats.Speed, member.PersonalAttackBonus);
        }

        void BuildCharacterCard(
            string displayName,
            string characterDefinitionId,
            int level,
            int xp,
            int hp,
            int maxHp,
            int speed,
            int personalDamageBonus = 0)
        {
            var card = CreateSectionCard(_characterRow, CharacterCardWidth, CharacterCardHeight);
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(card, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0.5f, 1f);
            portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.anchoredPosition = new Vector2(0f, -10f);
            portraitRt.sizeDelta = new Vector2(CharacterPortraitSize, CharacterPortraitSize);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = _characterVisuals?.GetPortraitReference(characterDefinitionId)
                ?? _characterVisuals?.GetPortrait(characterDefinitionId);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.35f, 0.38f, 0.45f, 1f);

            CreateCharacterStatGrid(card, hp, maxHp, speed, personalDamageBonus);

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(card, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.06f, 0f);
            nameRt.anchorMax = new Vector2(0.94f, 0.12f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 16, TextAnchor.MiddleCenter);
            nameText.text = FormatCharacterNameWithLevel(displayName, level);

            var xpLine = _session.IsExpeditionMode
                ? CharacterProgression.FormatXpLine(level, xp)
                : "";
            var tooltipTitle = FormatCharacterNameWithLevel(displayName, level);
            var tooltipBody =
                (string.IsNullOrEmpty(xpLine) ? "" : $"{xpLine}\n") +
                $"生命 {hp}/{maxHp}\n速度 {speed}" +
                (personalDamageBonus > 0 ? $"\n增伤 +{personalDamageBonus}" : "");

            _tooltip?.BindHover(card.gameObject, tooltipTitle, tooltipBody, showTitle: false);
        }

        void CreateCharacterStatGrid(RectTransform card, int hp, int maxHp, int speed, int personalDamageBonus)
        {
            var gridGo = new GameObject("Stats", typeof(RectTransform));
            gridGo.transform.SetParent(card, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.08f, 0.14f);
            gridRt.anchorMax = new Vector2(0.92f, 0.38f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;

            CreateStatPair(gridRt, 0f, 0.52f, 0.48f, 1f, _icons?.HpIcon, $"{hp}/{maxHp}");
            CreateStatPair(gridRt, 0.52f, 0.52f, 1f, 1f, _icons?.SpeedIcon, speed.ToString());
            if (personalDamageBonus > 0)
                CreateStatPair(gridRt, 0f, 0f, 1f, 0.44f, _icons?.AttackIcon, $"+{personalDamageBonus}");
        }

        void CreateStatPair(RectTransform parent, float xMin, float yMin, float xMax, float yMax, Sprite icon, string value)
        {
            var row = new GameObject("Stat", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(24f, 24f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = icon != null ? Color.white : new Color(0.7f, 0.75f, 0.85f, 1f);

            var textGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(row.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(28f, 0f);
            textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            StyleText(text, 15, TextAnchor.MiddleLeft);
            text.text = value;
        }

        void RefreshRelics()
        {
            if (!_session.IsExpeditionMode)
                return;

            foreach (var relicId in _session.Expedition.Run.Relics)
            {
                if (!RelicDatabase.TryGet(relicId, out var relic))
                    continue;

                CreateRelicSlot(_relicRow, relic);
            }
        }

        GameObject CreateRelicSlot(Transform parent, RelicDefinition relic)
        {
            var width = RelicSlotWidth;
            var height = RelicSlotHeight;
            var go = new GameObject("RelicSlot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = width;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var bg = go.GetComponent<Image>();
            ApplyEventPlate(bg);
            bg.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.18f);
            iconRt.anchorMax = new Vector2(0.86f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = _relicCatalog?.GetIcon(relic.Id);
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            icon.raycastTarget = false;
            if (icon.sprite != null)
                icon.color = Color.white;
            else
            {
                icon.color = new Color(0.45f, 0.38f, 0.28f, 1f);
                var fallbackGo = new GameObject("Fallback", typeof(RectTransform), typeof(Text));
                fallbackGo.transform.SetParent(iconGo.transform, false);
                var fallbackRt = fallbackGo.GetComponent<RectTransform>();
                fallbackRt.anchorMin = Vector2.zero;
                fallbackRt.anchorMax = Vector2.one;
                fallbackRt.offsetMin = Vector2.zero;
                fallbackRt.offsetMax = Vector2.zero;
                var fallbackText = fallbackGo.GetComponent<Text>();
                StyleText(fallbackText, 24, TextAnchor.MiddleCenter);
                fallbackText.text = string.IsNullOrEmpty(relic.DisplayName) ? "?" : relic.DisplayName.Substring(0, 1);
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.SetActive(false);

            _tooltip?.BindHover(go, relic.DisplayName, relic.Description);
            _dynamicObjects.Add(go);
            return go;
        }

        void RefreshCards()
        {
            if (_cardPrefab == null)
                return;

            if (ShouldShowBattleCardPiles())
            {
                if (_session?.Engine == null)
                    return;

                var state = _session.Engine.State;
                AddCardGroup("手牌", state.PlayerHand);
                AddCardGroup("抽牌堆", state.PlayerDrawPile);
                AddCardGroup("弃牌堆", state.PlayerDiscardPile);
                return;
            }

            if (!_session.IsExpeditionMode || _session.Expedition == null)
                return;

            var party = _session.Expedition.Run.Party;
            var config = _session.Expedition.Config;
            var totalCount = 0;

            foreach (var member in party)
            {
                var templates = ExpeditionRunDeckCatalog.CollectMemberDeck(config, member);
                if (templates.Count == 0)
                    continue;

                totalCount += templates.Count;
                var sectionLabel = string.IsNullOrEmpty(member.DisplayName)
                    ? "牌组"
                    : FormatCharacterNameWithLevel(member.DisplayName, member.Level);
                AddTemplateGroup(sectionLabel, templates);
            }

            if (totalCount == 0)
                return;
        }

        bool ShouldShowBattleCardPiles()
        {
            if (!_session.IsExpeditionMode)
                return _session?.Engine != null;

            return _session.Expedition.Run.Phase == ExpeditionPhase.InBattle && _session.Engine != null;
        }

        void AddTemplateGroup(string label, IReadOnlyList<CardTemplate> templates)
        {
            var header = CreateTextRow(_cardArea, label + $" ({templates.Count})");
            StyleText(header, 17, TextAnchor.MiddleLeft);
            _dynamicObjects.Add(header.gameObject);

            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;
            var grid = CreateCardGrid(_cardArea, cardWidth, cardHeight);

            foreach (var template in templates)
            {
                _definitions.TryGetValue(template.DefinitionId, out var definition);
                var holder = new GameObject("CardHolder", typeof(RectTransform), typeof(LayoutElement));
                holder.transform.SetParent(grid, false);
                var holderLe = holder.GetComponent<LayoutElement>();
                holderLe.preferredWidth = cardWidth + 8f;
                holderLe.preferredHeight = cardHeight + 8f;

                var view = Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                var preview = CardVisualResolver.CreatePreviewInstanceFromTemplate(template, definition);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                view.BindWithCard(
                    preview,
                    visual,
                    selected: false,
                    polluted: false,
                    interactable: false,
                    orderBadge: "",
                    statsLine,
                    uiIcons: _icons,
                    characterVisuals: _characterVisuals,
                    onClick: null,
                    onHoverEnter: null,
                    onHoverExit: null);

                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                BindCardTooltip(view.gameObject, preview, null);
                ScrollRectNavigation.WireForwarding(holder);
                _dynamicObjects.Add(holder);
            }
        }

        void BindCardTooltip(GameObject target, CardInstanceState card, BattleState state)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = StripRichText(BattleUiFormatters.BuildCardKeywordTooltip(state, descCard, _definitions));
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("<b>", "").Replace("</b>", "");
        }

        void AddCardGroup(string label, IReadOnlyList<CardInstanceState> cards)
        {
            if (cards.Count == 0)
                return;

            var header = CreateTextRow(_cardArea, label + $" ({cards.Count})");
            StyleText(header, 17, TextAnchor.MiddleLeft);
            _dynamicObjects.Add(header.gameObject);

            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;
            var grid = CreateCardGrid(_cardArea, cardWidth, cardHeight);
            var state = _session?.Engine?.State;

            foreach (var card in cards)
            {
                _definitions.TryGetValue(card.DefinitionId, out var definition);
                var holder = new GameObject("CardHolder", typeof(RectTransform), typeof(LayoutElement));
                holder.transform.SetParent(grid, false);
                var holderLe = holder.GetComponent<LayoutElement>();
                holderLe.preferredWidth = cardWidth + 8f;
                holderLe.preferredHeight = cardHeight + 8f;

                var view = Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                var visual = CardVisualResolver.Resolve(card, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(card, _definitions);
                view.BindWithCard(
                    card,
                    visual,
                    selected: false,
                    polluted: false,
                    interactable: false,
                    orderBadge: "",
                    statsLine: statsLine,
                    uiIcons: _icons,
                    characterVisuals: _characterVisuals,
                    onClick: null,
                    onHoverEnter: null,
                    onHoverExit: null);

                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                BindCardTooltip(view.gameObject, card, state);
                ScrollRectNavigation.WireForwarding(holder);
                _dynamicObjects.Add(holder);
            }
        }

        RectTransform CreateCardGrid(Transform parent, float cellWidth, float cellHeight)
        {
            var go = new GameObject("CardGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellWidth + 8f, cellHeight + 8f);
            grid.spacing = new Vector2(10f, 12f);
            grid.padding = new RectOffset(
                (int)CardGridHorizontalPadding,
                (int)CardGridHorizontalPadding,
                4,
                8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardsPerRow;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            _dynamicObjects.Add(go);
            return go.GetComponent<RectTransform>();
        }

        void RefreshConsumables()
        {
            if (!_session.IsExpeditionMode)
                return;

            HideConsumableDetail();
            ConsumableInventory.EnsureInitialized(_session.Expedition.Run.ConsumableSlots);
            var slots = _session.Expedition.Run.ConsumableSlots;
            var inBattle = _session.Engine != null &&
                           _session.Engine.State.Phase == TurnPhase.Planning &&
                           _session.CanInteractWithBattle();

            for (var i = 0; i < ConsumableInventory.MaxSlots; i++)
            {
                var slotIndex = i;
                var slotGo = CreateConsumableSlot(_consumableStrip, i);
                var id = i < slots.Count ? slots[i] : "";
                var icon = slotGo.transform.Find("Icon")?.GetComponent<Image>();
                var empty = string.IsNullOrEmpty(id);

                if (!empty && ConsumableDatabase.TryGet(id, out var def))
                {
                    var slotBg = slotGo.GetComponent<Image>();
                    if (slotBg != null)
                        ApplyEventPlate(slotBg);

                    if (icon != null)
                    {
                        icon.sprite = _consumableCatalog?.GetIcon(id);
                        icon.type = Image.Type.Simple;
                        icon.color = icon.sprite != null ? Color.white : new Color(0.35f, 0.4f, 0.5f, 1f);
                        icon.enabled = true;
                    }

                    _tooltip?.BindHover(slotGo, def.DisplayName, def.Description);

                    var btn = slotGo.GetComponent<Button>() ?? slotGo.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    if (inBattle)
                    {
                        btn.onClick.AddListener(() =>
                        {
                            if (_session.TryUseConsumableFromSlot(slotIndex))
                                OnConsumableUseStarted?.Invoke();
                        });
                    }
                    else
                    {
                        btn.onClick.AddListener(() => ShowConsumableDetail(slotIndex, def));
                    }

                    ScrollRectNavigation.WireForwarding(slotGo);
                }
                else
                {
                    // 空槽：保留 event plate 边框，只清图标（不要蓝虚影填充）
                    var slotBg = slotGo.GetComponent<Image>();
                    if (slotBg != null)
                        ApplyEventPlate(slotBg);

                    if (icon != null)
                    {
                        icon.sprite = null;
                        icon.color = Color.clear;
                        icon.enabled = false;
                    }

                    var btn = slotGo.GetComponent<Button>();
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                }
            }
        }

        void ShowConsumableDetail(int slotIndex, ConsumableDefinition def)
        {
            if (_consumableDetailRoot == null || def == null)
                return;

            _consumableDetailSlot = slotIndex;
            _consumableDetailTitle.text = def.DisplayName;
            _consumableDetailBody.text = def.Description;
            ResizeConsumableDetailBox();
            _consumableDetailRoot.gameObject.SetActive(true);
            _consumableDetailRoot.SetAsLastSibling();
            _tooltip?.Hide();
        }

        void ResizeConsumableDetailBox()
        {
            if (_consumableDetailBox == null || _consumableDetailTitle == null || _consumableDetailBody == null)
                return;

            var panelW = UiInfoPlateMetrics.MaxWidth;
            var innerW = UiInfoPlateMetrics.InnerWidth(panelW);
            var titleH = UiInfoPlateMetrics.MeasureHeight(_consumableDetailTitle, _consumableDetailTitle.text, innerW);
            var bodyH = UiInfoPlateMetrics.MeasureHeight(_consumableDetailBody, _consumableDetailBody.text, innerW);
            const float discardH = 96f;
            const float gap = 12f;
            var panelH = UiInfoPlateMetrics.PadY + titleH + gap + bodyH + gap + discardH + UiInfoPlateMetrics.PadY;
            _consumableDetailBox.sizeDelta = new Vector2(panelW, Mathf.Max(220f, panelH));

            var titleRt = _consumableDetailTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -UiInfoPlateMetrics.PadY);
            titleRt.sizeDelta = new Vector2(-UiInfoPlateMetrics.PadX * 2f, titleH);

            var bodyRt = _consumableDetailBody.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -(UiInfoPlateMetrics.PadY + titleH + gap));
            bodyRt.sizeDelta = new Vector2(-UiInfoPlateMetrics.PadX * 2f, bodyH);
        }

        void HideConsumableDetail()
        {
            _consumableDetailSlot = -1;
            if (_consumableDetailRoot != null)
                _consumableDetailRoot.gameObject.SetActive(false);
        }

        void OnDiscardConsumableClicked()
        {
            if (_consumableDetailSlot < 0)
                return;

            if (_session.DiscardConsumableSlot(_consumableDetailSlot))
            {
                HideConsumableDetail();
                Refresh();
            }
        }

        void EnsureBuilt(Transform root)
        {
            if (_panel != null && _layoutVersion == InventoryLayoutVersion)
            {
                if (_consumableDetailRoot == null)
                    BuildConsumableDetailPopup(_panel);
                return;
            }

            var wasOpen = _panel != null && _panel.gameObject.activeSelf;
            if (_panel != null)
            {
                Destroy(_panel.gameObject);
                _panel = null;
                _consumableDetailRoot = null;
                _consumableDetailBox = null;
                _tooltip = null;
                _mainContent = null;
                _consumableStrip = null;
                _characterRow = null;
                _relicRow = null;
                _cardArea = null;
                _scroll = null;
                _goldRow = null;
                _goldText = null;
                _xpText = null;
                _goldIcon = null;
                _xpIcon = null;
                _dynamicObjects.Clear();
            }

            _layoutVersion = InventoryLayoutVersion;

            var panelGo = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(root, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            ApplyEventPlate(panelGo.GetComponent<Image>());

            var titleBar = CreateTitleBar(panelGo.transform);
            var dragHandle = titleBar.gameObject.AddComponent<UiPanelDragHandle>();
            dragHandle.SetDragTarget(_panel);
            dragHandle.HideTooltipOnDrag = () => _tooltip?.Hide();

            _goldRow = CreateGoldRow(panelGo.transform);

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(48f, 16f);
            bodyRt.offsetMax = new Vector2(-16f, -140f);

            var scrollGo = new GameObject("MainScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(bodyGo.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(-ConsumableStripWidth - 28f, 0f);
            scrollGo.GetComponent<Image>().color = Color.clear;
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.scrollSensitivity = 64f;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateViewport(scrollGo.transform);
            _mainContent = CreateVerticalContent(viewport, "MainContent");
            _scroll.viewport = viewport;
            _scroll.content = _mainContent;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            CreateSectionHeader(_mainContent, "角色");
            _characterRow = CreateHorizontalRow(_mainContent, CharacterCardHeight + 12f);
            CreateSectionHeader(_mainContent, "遗物");
            _relicRow = CreateHorizontalRow(_mainContent, RelicSlotHeight + 12f);
            CreateSectionHeader(_mainContent, "卡牌");
            _cardArea = CreateVerticalContent(_mainContent, "Cards");
            var cardAreaLayout = _cardArea.GetComponent<VerticalLayoutGroup>();
            cardAreaLayout.padding = new RectOffset(32, 16, 4, 8);

            var stripGo = new GameObject("ConsumableStrip", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stripGo.transform.SetParent(bodyGo.transform, false);
            _consumableStrip = stripGo.GetComponent<RectTransform>();
            _consumableStrip.anchorMin = new Vector2(1f, 0f);
            _consumableStrip.anchorMax = new Vector2(1f, 1f);
            _consumableStrip.pivot = new Vector2(1f, 0.5f);
            _consumableStrip.sizeDelta = new Vector2(ConsumableStripWidth, 0f);
            // 整列消耗品框略向左
            _consumableStrip.anchoredPosition = new Vector2(-22f, 0f);
            var stripLayout = stripGo.GetComponent<VerticalLayoutGroup>();
            stripLayout.spacing = 8f;
            // 标题「消耗品」略靠右、略靠下
            stripLayout.padding = new RectOffset(10, 4, 22, 8);
            stripLayout.childAlignment = TextAnchor.UpperCenter;
            stripLayout.childControlWidth = true;
            stripLayout.childControlHeight = false;
            stripLayout.childForceExpandWidth = true;
            stripLayout.childForceExpandHeight = false;
            CreateSectionHeader(stripGo.transform, "消耗品");

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_panel, _icons);
            BuildConsumableDetailPopup(panelGo.transform);
            panelGo.SetActive(false);
            if (wasOpen)
                Show();
        }

        void BuildConsumableDetailPopup(Transform parent)
        {
            // 全面板透明点击层：点详情框外任意处关闭；详情框本体拦截点击以防穿透。
            var rootGo = new GameObject("ConsumableDetailHost", typeof(RectTransform), typeof(Image), typeof(Button));
            rootGo.transform.SetParent(parent, false);
            _consumableDetailRoot = rootGo.GetComponent<RectTransform>();
            _consumableDetailRoot.anchorMin = Vector2.zero;
            _consumableDetailRoot.anchorMax = Vector2.one;
            // 右侧消耗品栏留空，便于切换查看其它消耗品而不先关掉弹窗。
            _consumableDetailRoot.offsetMin = Vector2.zero;
            _consumableDetailRoot.offsetMax = new Vector2(-(ConsumableStripWidth + 28f), 0f);
            var hostImage = rootGo.GetComponent<Image>();
            hostImage.color = new Color(0f, 0f, 0f, 0.01f);
            hostImage.raycastTarget = true;
            var hostButton = rootGo.GetComponent<Button>();
            hostButton.transition = Selectable.Transition.None;
            hostButton.onClick.AddListener(HideConsumableDetail);

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(rootGo.transform, false);
            var boxRt = boxGo.GetComponent<RectTransform>();
            _consumableDetailBox = boxRt;
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(UiInfoPlateMetrics.MaxWidth, 260f);
            boxRt.anchoredPosition = new Vector2(180f, 40f);
            var boxImg = boxGo.GetComponent<Image>();
            if (_icons != null && _icons.UiInformationPlate != null)
            {
                boxImg.sprite = _icons.UiInformationPlate;
                boxImg.type = Image.Type.Simple;
                boxImg.preserveAspect = false;
                boxImg.color = Color.white;
            }
            else
            {
                boxImg.color = new Color(0.09f, 0.1f, 0.14f, 0.98f);
            }

            // 阻断点击落到宿主 Button，避免点框内文字区域也关闭。
            boxGo.AddComponent<Button>().transition = Selectable.Transition.None;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(boxGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.12f, 0.72f);
            titleRt.anchorMax = new Vector2(0.88f, 0.90f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _consumableDetailTitle = titleGo.GetComponent<Text>();
            _consumableDetailTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _consumableDetailTitle.fontSize = 22;
            _consumableDetailTitle.fontStyle = FontStyle.Bold;
            _consumableDetailTitle.alignment = TextAnchor.MiddleCenter;
            _consumableDetailTitle.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            _consumableDetailTitle.raycastTarget = false;

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(boxGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.14f, 0.36f);
            bodyRt.anchorMax = new Vector2(0.86f, 0.70f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            _consumableDetailBody = bodyGo.GetComponent<Text>();
            _consumableDetailBody.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _consumableDetailBody.fontSize = 16;
            _consumableDetailBody.alignment = TextAnchor.UpperCenter;
            _consumableDetailBody.color = Color.white;
            _consumableDetailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _consumableDetailBody.verticalOverflow = VerticalWrapMode.Overflow;
            _consumableDetailBody.raycastTarget = false;

            var discardGo = new GameObject("Discard", typeof(RectTransform), typeof(Image), typeof(Button));
            discardGo.transform.SetParent(boxGo.transform, false);
            var discardRt = discardGo.GetComponent<RectTransform>();
            discardRt.anchorMin = new Vector2(0.5f, 0f);
            discardRt.anchorMax = new Vector2(0.5f, 0f);
            discardRt.pivot = new Vector2(0.5f, 0f);
            discardRt.anchoredPosition = new Vector2(0f, 22f);
            // button3 素材约 512×292，按比例做丢弃钮
            const float discardW = 168f;
            discardRt.sizeDelta = new Vector2(discardW, discardW * (292f / 512f));
            var discardImg = discardGo.GetComponent<Image>();
            if (_icons != null && _icons.UiButton3 != null)
            {
                discardImg.sprite = _icons.UiButton3;
                discardImg.type = Image.Type.Simple;
                discardImg.preserveAspect = true;
                discardImg.color = Color.white;
            }
            else
            {
                discardImg.color = new Color(0.55f, 0.14f, 0.14f, 1f);
            }

            _consumableDiscardButton = discardGo.GetComponent<Button>();
            _consumableDiscardButton.transition = Selectable.Transition.None;
            _consumableDiscardButton.onClick.AddListener(OnDiscardConsumableClicked);

            var discardLabelGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            discardLabelGo.transform.SetParent(discardGo.transform, false);
            var discardLabelRt = discardLabelGo.GetComponent<RectTransform>();
            discardLabelRt.anchorMin = Vector2.zero;
            discardLabelRt.anchorMax = Vector2.one;
            discardLabelRt.offsetMin = new Vector2(4f, 4f);
            discardLabelRt.offsetMax = new Vector2(-4f, -6f);
            var discardLabel = discardLabelGo.GetComponent<Text>();
            discardLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            discardLabel.fontSize = 18;
            discardLabel.fontStyle = FontStyle.Bold;
            discardLabel.alignment = TextAnchor.MiddleCenter;
            discardLabel.color = Color.white;
            discardLabel.text = "丢弃";
            discardLabel.raycastTarget = false;

            rootGo.SetActive(false);
        }

        RectTransform CreateTitleBar(Transform parent)
        {
            var go = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // 标题落入大框内，略下移
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(-40f, 40f);
            var bg = go.GetComponent<Image>();
            bg.color = Color.clear;
            bg.raycastTarget = true;

            var textGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(56f, 0f);
            textRt.offsetMax = new Vector2(-16f, 0f);
            var text = textGo.GetComponent<Text>();
            StyleText(text, 22, TextAnchor.MiddleLeft);
            text.text = "背包";
            return rt;
        }

        RectTransform CreateGoldRow(Transform parent)
        {
            var go = new GameObject("CurrencyRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // 图标×2 后整体略下移
            rt.anchoredPosition = new Vector2(0f, -58f);
            rt.sizeDelta = new Vector2(-24f, 72f);
            var bg = go.GetComponent<Image>();
            bg.color = Color.clear;
            bg.raycastTarget = false;

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.padding = new RectOffset(16, 16, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            CreateCurrencyBadge(go.transform, out _goldIcon, out _goldText);
            CreateCurrencyBadge(go.transform, out _xpIcon, out _xpText);
            return rt;
        }

        static void CreateCurrencyBadge(Transform parent, out Image icon, out Text amount)
        {
            var slotGo = new GameObject("Currency", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            slotGo.transform.SetParent(parent, false);
            var slotLe = slotGo.GetComponent<LayoutElement>();
            slotLe.preferredWidth = 260f;
            slotLe.minHeight = 64f;

            var slotLayout = slotGo.GetComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = 12f;
            slotLayout.childAlignment = TextAnchor.MiddleLeft;
            slotLayout.childControlWidth = false;
            slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = false;
            slotLayout.childForceExpandHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(slotGo.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 64f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 64f;
            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(slotGo.transform, false);
            textGo.GetComponent<LayoutElement>().preferredWidth = 160f;
            amount = textGo.GetComponent<Text>();
            StyleText(amount, 24, TextAnchor.MiddleLeft);
            amount.raycastTarget = false;
        }

        static RectTransform CreateViewport(Transform parent)
        {
            var go = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.clear;
            return rt;
        }

        static RectTransform CreateVerticalContent(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 100f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(36, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        RectTransform CreateHorizontalRow(Transform parent, float minHeight)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go.GetComponent<RectTransform>();
        }

        Text CreateSectionHeader(Transform parent, string label)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28f;
            var text = go.GetComponent<Text>();
            StyleText(text, 18, TextAnchor.MiddleLeft);
            text.text = label;
            text.color = new Color(0.92f, 0.82f, 0.55f, 1f);
            return text;
        }

        Text CreateTextRow(Transform parent, string label)
        {
            var go = new GameObject("TextRow", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 24f;
            var text = go.GetComponent<Text>();
            StyleText(text, 15, TextAnchor.MiddleLeft);
            text.text = label;
            return text;
        }

        RectTransform CreateSectionCard(Transform parent, float width, float height)
        {
            var go = new GameObject("CharacterCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            go.GetComponent<LayoutElement>().preferredWidth = width;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            ApplyCharacterPlate(go.GetComponent<Image>());
            _dynamicObjects.Add(go);
            return rt;
        }

        void ApplyEventPlate(Image image)
        {
            if (image == null)
                return;

            var plate = _icons != null ? _icons.UiEventPlate : null;
            if (plate != null)
            {
                image.enabled = true;
                image.sprite = plate;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.enabled = true;
                image.sprite = null;
                image.color = new Color(0.07f, 0.08f, 0.12f, 0.995f);
            }
        }

        void ApplyCharacterPlate(Image image)
        {
            if (image == null)
                return;

            var plate = _icons != null ? _icons.UiCharacterPlate : null;
            if (plate != null)
            {
                image.sprite = plate;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            }
        }

        GameObject CreateIconSlot(Transform parent, float size)
        {
            var go = new GameObject("RelicSlot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = size;
            go.GetComponent<LayoutElement>().preferredHeight = size + 24f;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.12f, 0.28f);
            iconRt.anchorMax = new Vector2(0.88f, 0.96f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            iconGo.GetComponent<Image>().preserveAspect = true;
            _dynamicObjects.Add(go);
            return go;
        }

        GameObject CreateConsumableSlot(Transform parent, int index)
        {
            var go = new GameObject($"ConsumableSlot_{index}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = ConsumableSlotSize;
            le.minWidth = ConsumableSlotSize;
            le.preferredHeight = ConsumableSlotSize;
            le.minHeight = ConsumableSlotSize;
            // 边框：与遗物同款 event plate（空槽也保留）；不要蓝色 Outline/Button 高亮虚影
            var bg = go.GetComponent<Image>();
            ApplyEventPlate(bg);
            bg.raycastTarget = true;
            foreach (var fx in go.GetComponents<Shadow>())
                Destroy(fx);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            btn.colors = colors;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.14f);
            iconRt.anchorMax = new Vector2(0.86f, 0.86f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = null;
            icon.color = Color.clear;
            icon.enabled = false;
            _dynamicObjects.Add(go);
            return go;
        }

        void ClearDynamic()
        {
            HideConsumableDetail();
            _tooltip?.Hide();

            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();

            ClearChildren(_characterRow);
            ClearChildren(_relicRow);
            ClearChildren(_cardArea);
            ClearChildren(_consumableStrip, keepFirst: 1);
        }

        static void ClearChildren(RectTransform parent, int keepFirst = 0)
        {
            if (parent == null)
                return;

            for (var i = parent.childCount - 1; i >= keepFirst; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        static string FormatCharacterNameWithLevel(string displayName, int level)
        {
            var lv = CharacterProgression.ClampLevel(level);
            return string.IsNullOrEmpty(displayName) ? $"Lv{lv}" : $"{displayName}Lv{lv}";
        }

        static void StyleText(Text text, int size, TextAnchor anchor)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            foreach (var fx in text.GetComponents<Shadow>())
                UnityEngine.Object.Destroy(fx);
        }
    }
}
