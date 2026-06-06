using System;
using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleInventoryPanelView : MonoBehaviour
    {
        const float PanelWidth = 1080f;
        const float PanelHeight = 780f;
        const float ConsumableStripWidth = 108f;
        const float CardScale = 0.98f;
        const int CardsPerRow = 5;
        const float CardGridHorizontalPadding = 20f;
        const float CharacterCardWidth = 228f;
        const float CharacterCardHeight = 318f;
        const float CharacterPortraitSize = 168f;

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
        RectTransform _goldRow;
        Text _goldText;
        Image _goldIcon;
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
            if (_panel == null)
                return;

            if (IsOpen)
                Hide();
            else
                Show();
        }

        public void Hide()
        {
            _tooltip?.Hide();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (_panel == null || !IsOpen)
                return;

            Rebuild();
        }

        void Rebuild()
        {
            ClearDynamic();
            RefreshGold();
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

        void RefreshGold()
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

            foreach (var member in _session.Expedition.Run.Party)
                BuildCharacterCardFromPartyMember(member);
        }

        void BuildCharacterCardsFromCombatants(IReadOnlyList<CombatantState> combatants)
        {
            foreach (var unit in combatants)
            {
                if (unit.Team != TeamSide.Player)
                    continue;

                BuildCharacterCard(unit.DisplayName, unit.CharacterDefinitionId, unit.Level, unit.Xp, unit.Hp, unit.MaxHp,
                    unit.Attack, unit.Defense, StatusRules.GetEffectiveSpeed(unit));
            }
        }

        void BuildCharacterCardFromPartyMember(PartyMemberSnapshot member)
        {
            var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, member.Level);
            var attack = stats.BaseAttack + (_session.Expedition.Run.Modifiers?.TeamAttackBonus ?? 0);
            var defense = stats.BaseDefense + (_session.Expedition.Run.Modifiers?.TeamDefenseBonus ?? 0);
            BuildCharacterCard(member.DisplayName, member.CharacterDefinitionId, member.Level, member.Xp, member.Hp,
                member.MaxHp, attack, defense, stats.Speed);
        }

        void BuildCharacterCard(
            string displayName,
            string characterDefinitionId,
            int level,
            int xp,
            int hp,
            int maxHp,
            int attack,
            int defense,
            int speed)
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

            CreateCharacterStatGrid(card, hp, maxHp, attack, defense, speed);

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(card, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.06f, 0f);
            nameRt.anchorMax = new Vector2(0.94f, 0.12f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 16, TextAnchor.MiddleCenter);
            nameText.text = displayName;

            var xpLine = _session.IsExpeditionMode
                ? CharacterProgression.FormatXpLine(level, xp)
                : "";
            var tooltipBody =
                (string.IsNullOrEmpty(xpLine) ? "" : $"{xpLine}\n") +
                $"生命 {hp}/{maxHp}\n攻击 {attack}  防御 {defense}  速度 {speed}";

            _tooltip?.BindHover(card.gameObject, displayName, tooltipBody, showTitle: false);
        }

        void CreateCharacterStatGrid(RectTransform card, int hp, int maxHp, int attack, int defense, int speed)
        {
            var gridGo = new GameObject("Stats", typeof(RectTransform));
            gridGo.transform.SetParent(card, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.08f, 0.14f);
            gridRt.anchorMax = new Vector2(0.92f, 0.38f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;

            CreateStatPair(gridRt, 0f, 0.52f, 0.48f, 1f, _icons?.HpIcon, $"{hp}/{maxHp}");
            CreateStatPair(gridRt, 0.52f, 0.52f, 1f, 1f, _icons?.AttackIcon, attack.ToString());
            CreateStatPair(gridRt, 0f, 0f, 0.48f, 0.44f, _icons?.DefenseIcon, defense.ToString());
            CreateStatPair(gridRt, 0.52f, 0f, 1f, 0.44f, _icons?.SpeedIcon, speed.ToString());
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
            const float width = 108f;
            const float height = 132f;
            var go = new GameObject("RelicSlot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = width;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.12f);
            iconRt.anchorMax = new Vector2(0.9f, 0.92f);
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
                AddCardGroup("手牌", state.PlayerHand, useFormulas: false);
                AddCardGroup("抽牌堆", state.PlayerDrawPile, useFormulas: false);
                AddCardGroup("弃牌堆", state.PlayerDiscardPile, useFormulas: false);
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
                var sectionLabel = string.IsNullOrEmpty(member.DisplayName) ? "牌组" : member.DisplayName;
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
                var preview = CardVisualResolver.CreatePreviewInstance(
                    template.DefinitionId,
                    template.OwnerCharacterId,
                    template.DisplayName,
                    definition);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                view.BindWithCard(
                    preview,
                    visual,
                    selected: false,
                    polluted: false,
                    interactable: false,
                    orderBadge: "",
                    statsLine: "",
                    uiIcons: _icons,
                    characterVisuals: _characterVisuals,
                    onClick: null,
                    onHoverEnter: null,
                    onHoverExit: null);

                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                BindCardTooltip(view.gameObject, preview, null, preferFormulas: true);
                _dynamicObjects.Add(holder);
            }
        }

        void BindCardTooltip(GameObject target, CardInstanceState card, BattleState state, bool preferFormulas)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var stats = preferFormulas || state == null
                ? BattleUiFormatters.BuildCardStatsLinePreview(card)
                : BattleUiFormatters.BuildCardStatsLine(state, null, card);
            var keywords = StripRichText(BattleUiFormatters.BuildCardKeywordTooltip(state, card));
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("<b>", "").Replace("</b>", "");
        }

        void AddCardGroup(string label, IReadOnlyList<CardInstanceState> cards, bool useFormulas)
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
                var preview = CardVisualResolver.CreatePreviewInstance(
                    card.DefinitionId,
                    card.OwnerCharacterId,
                    card.DisplayName,
                    definition);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = useFormulas ? "" : BattleUiFormatters.BuildCardStatsLine(state, null, card);
                view.BindWithCard(
                    preview,
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

                BindCardTooltip(view.gameObject, preview, state, preferFormulas: useFormulas);
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
                    if (icon != null)
                    {
                        icon.sprite = _consumableCatalog?.GetIcon(id);
                        icon.type = Image.Type.Simple;
                        icon.color = icon.sprite != null ? Color.white : new Color(0.35f, 0.4f, 0.5f, 1f);
                    }

                    _tooltip?.BindHover(slotGo, def.DisplayName, def.Description);

                    if (inBattle)
                    {
                        var btn = slotGo.GetComponent<Button>() ?? slotGo.AddComponent<Button>();
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            if (_session.TryUseConsumableFromSlot(slotIndex))
                                OnConsumableUseStarted?.Invoke();
                        });
                    }
                }
                else if (icon != null)
                {
                    icon.sprite = null;
                    icon.color = new Color(0.18f, 0.2f, 0.26f, 0.8f);
                }
            }
        }

        void EnsureBuilt(Transform root)
        {
            if (_panel != null)
                return;

            var panelGo = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(root, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.995f);

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
            bodyRt.offsetMin = new Vector2(12f, 12f);
            bodyRt.offsetMax = new Vector2(-12f, -96f);

            var scrollGo = new GameObject("MainScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(bodyGo.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(-ConsumableStripWidth - 8f, 0f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;

            var viewport = CreateViewport(scrollGo.transform);
            _mainContent = CreateVerticalContent(viewport, "MainContent");
            _scroll.viewport = viewport;
            _scroll.content = _mainContent;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            CreateSectionHeader(_mainContent, "角色");
            _characterRow = CreateHorizontalRow(_mainContent, CharacterCardHeight + 12f);
            CreateSectionHeader(_mainContent, "遗物");
            _relicRow = CreateHorizontalRow(_mainContent, 120f);
            CreateSectionHeader(_mainContent, "卡牌");
            _cardArea = CreateVerticalContent(_mainContent, "Cards");
            var cardAreaLayout = _cardArea.GetComponent<VerticalLayoutGroup>();
            cardAreaLayout.padding = new RectOffset(24, 16, 4, 8);

            var stripGo = new GameObject("ConsumableStrip", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stripGo.transform.SetParent(bodyGo.transform, false);
            _consumableStrip = stripGo.GetComponent<RectTransform>();
            _consumableStrip.anchorMin = new Vector2(1f, 0f);
            _consumableStrip.anchorMax = new Vector2(1f, 1f);
            _consumableStrip.pivot = new Vector2(1f, 0.5f);
            _consumableStrip.sizeDelta = new Vector2(ConsumableStripWidth, 0f);
            _consumableStrip.anchoredPosition = Vector2.zero;
            var stripLayout = stripGo.GetComponent<VerticalLayoutGroup>();
            stripLayout.spacing = 8f;
            stripLayout.childAlignment = TextAnchor.UpperCenter;
            stripLayout.childControlWidth = true;
            stripLayout.childControlHeight = false;
            stripLayout.childForceExpandWidth = true;
            stripLayout.childForceExpandHeight = false;
            CreateSectionHeader(stripGo.transform, "消耗品");

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_panel);
            panelGo.SetActive(false);
        }

        RectTransform CreateTitleBar(Transform parent)
        {
            var go = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 44f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 1f);

            var textGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 0f);
            textRt.offsetMax = new Vector2(-16f, 0f);
            var text = textGo.GetComponent<Text>();
            StyleText(text, 22, TextAnchor.MiddleLeft);
            text.text = "背包 · 拖动标题栏移动";
            return rt;
        }

        RectTransform CreateGoldRow(Transform parent)
        {
            var go = new GameObject("GoldRow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -44f);
            rt.sizeDelta = new Vector2(-24f, 44f);
            go.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.95f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(16f, 0f);
            iconRt.sizeDelta = new Vector2(32f, 32f);
            _goldIcon = iconGo.GetComponent<Image>();
            _goldIcon.preserveAspect = true;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(56f, 0f);
            textRt.offsetMax = new Vector2(-12f, 0f);
            _goldText = textGo.GetComponent<Text>();
            StyleText(_goldText, 24, TextAnchor.MiddleLeft);
            return rt;
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
            layout.padding = new RectOffset(8, 8, 8, 8);
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
            go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            _dynamicObjects.Add(go);
            return rt;
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
            le.preferredWidth = 92f;
            le.preferredHeight = 92f;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            _dynamicObjects.Add(go);
            return go;
        }

        void ClearDynamic()
        {
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
        }
    }
}
