using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Persistence;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>营地图书馆图鉴：玩家角色 / 卡牌 / 敌方角色 / 敌方卡牌 / 遗物。</summary>
    [DisallowMultipleComponent]
    public sealed class LibraryCodexOverlayView : MonoBehaviour
    {
        enum CodexTab
        {
            PlayerCharacters,
            PlayerCards,
            EnemyCharacters,
            EnemyCards,
            Relics
        }

        const float PanelWidth = 1180f;
        const float PanelHeight = 820f;
        const float CardScale = 0.68f;
        const int CardsPerRow = 5;
        const int PortraitColumns = 4;
        const int RelicColumns = 5;
        const float CardGridHorizontalPadding = 16f;

        static readonly Color SilhouetteColor = new(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color TabActive = new(0.32f, 0.26f, 0.16f, 0.98f);
        static readonly Color TabIdle = new(0.14f, 0.15f, 0.2f, 0.96f);

        PlayerProfileState _profile;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action _onClose;

        RectTransform _overlayRoot;
        RectTransform _panel;
        RectTransform _content;
        ScrollRect _scroll;
        InventoryTooltipView _tooltip;
        Text _titleText;
        readonly List<Button> _tabButtons = new();
        readonly List<GameObject> _dynamicObjects = new();
        CodexTab _activeTab = CodexTab.PlayerCharacters;
        bool _built;

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            RelicVisualCatalogSO relicCatalog,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action onClose)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _relicCatalog = relicCatalog;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onClose = onClose;
            EnsureBuilt();
        }

        public void Show(PlayerProfileState profile)
        {
            _profile = profile;
            EnsureBuilt();
            _overlayRoot.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _overlayRoot.SetAsLastSibling();
            SelectTab(_activeTab, forceRebuild: true);
        }

        public void Hide()
        {
            _tooltip?.Hide();
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            var root = CampUiRuntime.CreateRect("LibraryCodexRoot", transform);
            _overlayRoot = root.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);

            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var panelGo = CampUiRuntime.CreateRect("Panel", root.transform);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.07f, 0.1f, 0.97f);

            BuildHeader(panelGo.transform);
            BuildTabs(panelGo.transform);
            BuildScroll(panelGo.transform);

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_panel);
            _overlayRoot.gameObject.SetActive(false);
        }

        void BuildHeader(Transform parent)
        {
            var header = CampUiRuntime.CreateRect("Header", parent);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 56f);

            _titleText = CampUiRuntime.CreateText(header.transform, "图书馆图鉴", 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            CampUiRuntime.SetAnchored(_titleText.rectTransform, 0.02f, 0.1f, 0.8f, 0.9f);
            _titleText.color = new Color(0.92f, 0.86f, 0.68f, 1f);

            var close = CampUiRuntime.CreateButton(header.transform, "关闭", new Color(0.28f, 0.16f, 0.16f, 0.98f),
                new Vector2(96f, 36f));
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-16f, 0f);
            close.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });
        }

        void BuildTabs(Transform parent)
        {
            var row = CampUiRuntime.CreateRect("Tabs", parent);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -56f);
            rowRt.sizeDelta = new Vector2(0f, 48f);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            AddTab(row.transform, "玩家角色", CodexTab.PlayerCharacters);
            AddTab(row.transform, "卡牌", CodexTab.PlayerCards);
            AddTab(row.transform, "敌方角色", CodexTab.EnemyCharacters);
            AddTab(row.transform, "敌方卡牌", CodexTab.EnemyCards);
            AddTab(row.transform, "遗物", CodexTab.Relics);
        }

        void AddTab(Transform parent, string label, CodexTab tab)
        {
            var btn = CampUiRuntime.CreateButton(parent, label, TabIdle, new Vector2(150f, 36f));
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 150f;
            le.preferredHeight = 36f;
            btn.onClick.AddListener(() => SelectTab(tab, forceRebuild: true));
            _tabButtons.Add(btn);
        }

        void BuildScroll(Transform parent)
        {
            var scrollGo = CampUiRuntime.CreateRect("Scroll", parent);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-16f, -112f);
            scrollGo.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.55f);

            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            var viewportGo = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewport = viewportGo.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewport);
            viewportGo.AddComponent<RectMask2D>();
            viewportGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            var contentGo = CampUiRuntime.CreateRect("Content", viewportGo.transform);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 16);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;
        }

        void SelectTab(CodexTab tab, bool forceRebuild)
        {
            _activeTab = tab;
            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var img = _tabButtons[i].targetGraphic as Image;
                if (img != null)
                    img.color = (CodexTab)i == tab ? TabActive : TabIdle;
            }

            if (forceRebuild)
                Rebuild();
        }

        void Rebuild()
        {
            ClearDynamic();
            _tooltip?.Hide();
            if (_profile == null)
            {
                AddHint("档案未就绪。");
                ForceLayout();
                return;
            }

            switch (_activeTab)
            {
                case CodexTab.PlayerCharacters:
                    _titleText.text = "图书馆图鉴 — 玩家角色";
                    BuildPlayerCharacters();
                    break;
                case CodexTab.PlayerCards:
                    _titleText.text = "图书馆图鉴 — 卡牌";
                    BuildPlayerCards();
                    break;
                case CodexTab.EnemyCharacters:
                    _titleText.text = "图书馆图鉴 — 敌方角色";
                    BuildEnemyCharacters();
                    break;
                case CodexTab.EnemyCards:
                    _titleText.text = "图书馆图鉴 — 敌方卡牌";
                    BuildEnemyCards();
                    break;
                case CodexTab.Relics:
                    _titleText.text = "图书馆图鉴 — 遗物";
                    BuildRelics();
                    break;
            }

            ForceLayout(resetScroll: true);
        }

        void BuildPlayerCharacters()
        {
            var grid = CreatePortraitGrid(_content, PortraitColumns, 168f, 210f);
            foreach (var characterId in TalentCatalog.PlayableCharacterIds)
            {
                var owned = CodexProgressRules.HasOwnedCharacter(_profile, characterId);
                var name = CharacterDisplayNames.GetOrFallback(characterId, characterId);
                CreatePortraitCell(
                    grid,
                    characterId,
                    name,
                    owned,
                    owned ? name : "？？？",
                    owned ? "已解锁角色。故事与说明稍后补充。" : "尚未拥有该角色。");
            }
        }

        void BuildEnemyCharacters()
        {
            var entries = TrainingMonsterCatalog.BuildEntries();
            if (entries.Count == 0)
            {
                AddHint("暂无敌人图鉴数据。");
                return;
            }

            var grid = CreatePortraitGrid(_content, CardsPerRow, 168f, 188f);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CharacterId))
                    continue;

                var seen = CodexProgressRules.HasSeenEnemy(_profile.Codex, entry.CharacterId);
                CreateEnemyPortraitCell(grid, entry, seen);
            }
        }

        void CreateEnemyPortraitCell(Transform parent, TrainingMonsterCatalog.Entry entry, bool seen)
        {
            var go = CampUiRuntime.CreateRect($"Enemy_{entry.CharacterId}", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 168f;
            le.preferredHeight = 188f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Portrait", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.08f, 0.06f, 0.92f, 0.94f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _characterVisuals?.GetPortrait(entry.CharacterId);
            icon.color = seen ? Color.white : SilhouetteColor;

            if (seen)
            {
                var body = FormatEnemyStats(entry);
                _tooltip?.BindHover(go, entry.DisplayName, body);
            }
            else
            {
                _tooltip?.BindHover(go, "未遇见", "尚未遇见该敌人。");
            }
        }

        static string FormatEnemyStats(TrainingMonsterCatalog.Entry entry)
        {
            var t = entry?.Template;
            if (t == null)
                return "已遇见该敌人。";

            return $"HP {t.MaxHp}\n攻击 {t.BaseAttack}\n防御 {t.BaseDefense}\n速度 {t.Speed}";
        }

        void BuildPlayerCards()
        {
            if (_cardPrefab == null)
            {
                AddHint("卡牌预制体未就绪。");
                return;
            }

            var cards = CollectPlayerCards();
            if (cards.Count == 0)
            {
                AddHint("暂无可展示的玩家卡牌。");
                return;
            }

            AddCategoryHeader($"玩家卡牌（{cards.Count}）");
            var grid = CreateCardGrid(_content);
            foreach (var def in cards)
                CreateCardCell(grid, def, unlocked: CodexProgressRules.HasOwnedCard(_profile, def.CardId));
        }

        void BuildEnemyCards()
        {
            if (_cardPrefab == null)
            {
                AddHint("卡牌预制体未就绪。");
                return;
            }

            var cards = CollectEnemyCards();
            if (cards.Count == 0)
            {
                AddHint("暂无可展示的敌方卡牌。");
                return;
            }

            AddCategoryHeader($"敌方卡牌（{cards.Count}）");
            var grid = CreateCardGrid(_content);
            foreach (var def in cards)
                CreateCardCell(grid, def, unlocked: CodexProgressRules.HasSeenEnemyCard(_profile.Codex, def.CardId));
        }

        void BuildRelics()
        {
            var relics = new List<RelicDefinition>(RelicDatabase.All);
            relics.Sort((a, b) =>
            {
                var rarity = a.Rarity.CompareTo(b.Rarity);
                if (rarity != 0)
                    return rarity;
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });

            if (relics.Count == 0)
            {
                AddHint("暂无遗物数据。");
                return;
            }

            AddCategoryHeader($"遗物（{relics.Count}）");
            var grid = CreatePortraitGrid(_content, RelicColumns, 132f, 156f);
            foreach (var relic in relics)
            {
                if (relic == null || string.IsNullOrEmpty(relic.Id))
                    continue;

                var owned = CodexProgressRules.HasOwnedRelic(_profile.Codex, relic.Id);
                CreateRelicCell(grid, relic, owned);
            }
        }

        List<CardDefinitionSO> CollectPlayerCards()
        {
            var list = new List<CardDefinitionSO>();
            foreach (var pair in _definitions)
            {
                var def = pair.Value;
                if (def == null || string.IsNullOrEmpty(def.CardId))
                    continue;
                if (!PlayerCardCatalogRules.IsAllowedPlayerCard(def.CardId, def.OwnerCharacterId))
                    continue;
                if (PlayerCardCatalogRules.IsTokenCardId(def.CardId))
                    continue;
                list.Add(def);
            }

            list.Sort(CompareCards);
            return list;
        }

        List<CardDefinitionSO> CollectEnemyCards()
        {
            var list = new List<CardDefinitionSO>();
            foreach (var pair in _definitions)
            {
                var def = pair.Value;
                if (def == null || string.IsNullOrEmpty(def.CardId))
                    continue;
                if (PlayerCardCatalogRules.IsAllowedPlayerCardId(def.CardId))
                    continue;
                if (ExpeditionCardPool.IsPlayerCharacterId(def.OwnerCharacterId))
                    continue;
                if (def.CardId.StartsWith("curse_", StringComparison.Ordinal))
                    continue;
                list.Add(def);
            }

            list.Sort(CompareCards);
            return list;
        }

        static int CompareCards(CardDefinitionSO a, CardDefinitionSO b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            var owner = string.CompareOrdinal(a.OwnerCharacterId, b.OwnerCharacterId);
            if (owner != 0)
                return owner;

            var rarity = a.Rarity.CompareTo(b.Rarity);
            if (rarity != 0)
                return rarity;

            return string.CompareOrdinal(a.DisplayName, b.DisplayName);
        }

        void CreatePortraitCell(
            Transform parent,
            string characterId,
            string displayName,
            bool unlocked,
            string tooltipTitle,
            string tooltipBody)
        {
            var go = CampUiRuntime.CreateRect($"Char_{characterId}", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 156f;
            le.preferredHeight = 196f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Portrait", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.08f, 0.22f, 0.92f, 0.94f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _characterVisuals?.GetPortrait(characterId);
            icon.color = unlocked ? Color.white : SilhouetteColor;

            var label = CampUiRuntime.CreateText(go.transform, unlocked ? displayName : "？？？", 16, FontStyle.Bold);
            CampUiRuntime.SetAnchored(label.rectTransform, 0.04f, 0.02f, 0.96f, 0.2f);
            label.color = unlocked
                ? new Color(0.9f, 0.88f, 0.8f, 1f)
                : new Color(0.45f, 0.45f, 0.5f, 1f);

            if (unlocked)
                _tooltip?.BindHover(go, tooltipTitle, tooltipBody);
            else
                _tooltip?.BindHover(go, "未解锁", tooltipBody);
        }

        void CreateRelicCell(Transform parent, RelicDefinition relic, bool owned)
        {
            var go = CampUiRuntime.CreateRect($"Relic_{relic.Id}", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 120f;
            le.preferredHeight = 148f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.11f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Icon", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.12f, 0.28f, 0.88f, 0.92f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _relicCatalog?.GetIcon(relic.Id);
            icon.color = owned
                ? (icon.sprite != null ? Color.white : new Color(0.55f, 0.48f, 0.35f, 1f))
                : SilhouetteColor;

            var label = CampUiRuntime.CreateText(go.transform, owned ? relic.DisplayName : "？？？", 14, FontStyle.Bold);
            CampUiRuntime.SetAnchored(label.rectTransform, 0.04f, 0.02f, 0.96f, 0.26f);
            label.color = owned
                ? new Color(0.9f, 0.88f, 0.8f, 1f)
                : new Color(0.45f, 0.45f, 0.5f, 1f);

            if (owned)
                _tooltip?.BindHover(go, relic.DisplayName, relic.Description ?? "");
            else
                _tooltip?.BindHover(go, "未拥有", "尚未拥有该遗物。");
        }

        void CreateCardCell(Transform parent, CardDefinitionSO def, bool unlocked)
        {
            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;
            var holder = CampUiRuntime.CreateRect($"Card_{def.CardId}", parent);
            var holderLe = holder.AddComponent<LayoutElement>();
            holderLe.preferredWidth = cardWidth + 8f;
            holderLe.preferredHeight = cardHeight + 8f;
            _dynamicObjects.Add(holder);

            var preview = CardVisualResolver.CreatePreviewInstance(
                def.CardId,
                def.OwnerCharacterId,
                unlocked ? def.DisplayName : "？？？",
                def);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);

            var view = UnityEngine.Object.Instantiate(_cardPrefab, holder.transform);
            CardView.ApplyHandPresentationScaleCentered(view, CardScale);
            view.BindWithCard(
                preview,
                visual,
                selected: false,
                polluted: false,
                interactable: false,
                orderBadge: "",
                statsLine: unlocked
                    ? BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions)
                    : "",
                uiIcons: _uiIcons,
                characterVisuals: _characterVisuals,
                onClick: null,
                onHoverEnter: null,
                onHoverExit: null);

            if (!unlocked)
                ApplyCardSilhouette(view);

            var canvasGroup = view.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            if (unlocked)
            {
                var body = ResolveCardDescription(def);
                _tooltip?.BindHover(view.gameObject, def.DisplayName, body, showTitle: false);
            }
            else
            {
                _tooltip?.BindHover(view.gameObject, "未解锁", "尚未拥有 / 遇见该卡牌。");
            }
        }

        static void ApplyCardSilhouette(CardView view)
        {
            if (view == null)
                return;

            foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                    continue;

                if (graphic is Text text)
                {
                    text.color = new Color(0.15f, 0.15f, 0.18f, 1f);
                    if (text != null && text.gameObject.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0)
                        text.text = "？？？";
                    continue;
                }

                graphic.color = SilhouetteColor;
            }
        }

        static string ResolveCardDescription(CardDefinitionSO def)
        {
            if (def == null)
                return "";

            if (CardDescriptionCatalog.TryGetByCardId(def.CardId, out var byId) && !string.IsNullOrWhiteSpace(byId))
                return byId;

            if (CardDescriptionCatalog.TryGetByDisplayName(def.DisplayName, out var byName)
                && !string.IsNullOrWhiteSpace(byName))
                return byName;

            return string.IsNullOrEmpty(def.DisplayName) ? def.CardId : def.DisplayName;
        }

        Transform CreatePortraitGrid(Transform parent, int columns, float cellW, float cellH)
        {
            var go = CampUiRuntime.CreateRect("PortraitGrid", parent);
            _dynamicObjects.Add(go);
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(8, 8, 4, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;
            return go.transform;
        }

        Transform CreateCardGrid(Transform parent)
        {
            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;
            var go = CampUiRuntime.CreateRect("CardGrid", parent);
            _dynamicObjects.Add(go);
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cardWidth + 8f, cardHeight + 8f);
            grid.spacing = new Vector2(10f, 12f);
            grid.padding = new RectOffset(
                (int)CardGridHorizontalPadding,
                (int)CardGridHorizontalPadding,
                4,
                8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardsPerRow;
            grid.childAlignment = TextAnchor.UpperLeft;
            return go.transform;
        }

        void AddCategoryHeader(string label)
        {
            var go = CampUiRuntime.CreateRect("Category", _content);
            _dynamicObjects.Add(go);
            go.AddComponent<LayoutElement>().preferredHeight = 32f;
            var text = go.AddComponent<Text>();
            text.font = CampUiRuntime.DefaultFont;
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.85f, 0.78f, 0.55f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.text = label;
        }

        void AddHint(string message)
        {
            var go = CampUiRuntime.CreateRect("Hint", _content);
            _dynamicObjects.Add(go);
            go.AddComponent<LayoutElement>().preferredHeight = 48f;
            var text = go.AddComponent<Text>();
            text.font = CampUiRuntime.DefaultFont;
            text.fontSize = 18;
            text.color = new Color(1f, 0.75f, 0.55f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.text = message;
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

        void ForceLayout(bool resetScroll = false)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null && resetScroll)
                _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
