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
    /// <summary>
    /// 图书馆图鉴：模板底图 + button6 五页签 + 内容区（角色/卡牌/敌人/敌卡/遗物）。
    /// </summary>
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

        const int LayoutVersion = 1;
        const float ButtonHoverScale = 1.06f;
        const float CardScale = 0.62f;
        const int CardsPerRow = 6;
        const int PortraitColumns = 6;
        const int RelicColumns = 7;
        const float CardGridHorizontalPadding = 10f;

        // 模板归一化（原点左下）
        static readonly Vector4 ZoneClose = new(0.862f, 0.898f, 0.968f, 0.962f);
        static readonly Vector4 ZoneTabs = new(0.128f, 0.748f, 0.872f, 0.818f);
        static readonly Vector4 ZoneContent = new(0.138f, 0.138f, 0.862f, 0.720f);

        static readonly Color SilhouetteColor = new(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color TabActiveTint = new(1f, 0.92f, 0.72f, 1f);
        static readonly Color TabIdleTint = new(0.62f, 0.64f, 0.70f, 1f);
        static readonly Color TabActiveLabel = new(0.98f, 0.94f, 0.78f, 1f);
        static readonly Color TabIdleLabel = new(0.78f, 0.80f, 0.86f, 1f);
        static readonly Color SectionGold = new(0.92f, 0.82f, 0.48f, 1f);
        static readonly Color ButtonLabel = new(0.96f, 0.92f, 0.78f, 1f);

        static readonly string[] TabLabels =
        {
            "玩家角色",
            "卡牌",
            "敌方角色",
            "敌方卡牌",
            "遗物"
        };

        PlayerProfileState _profile;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action _onClose;

        RectTransform _overlayRoot;
        Image _bgImage;
        RectTransform _content;
        ScrollRect _scroll;
        InventoryTooltipView _tooltip;
        CampCardDetailView _cardDetail;
        readonly List<Image> _tabImages = new();
        readonly List<Text> _tabLabels = new();
        readonly List<GameObject> _dynamicObjects = new();
        CodexTab _activeTab = CodexTab.PlayerCharacters;
        bool _built;
        int _builtVersion = -1;

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
            _cardDetail?.Hide();
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (_built && _builtVersion == LayoutVersion)
                return;

            if (_overlayRoot != null)
                Destroy(_overlayRoot.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;
            _dynamicObjects.Clear();
            _tabImages.Clear();
            _tabLabels.Clear();

            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _overlayRoot = CampUiRuntime.CreateRect("LibraryCodexRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            _bgImage = CampUiRuntime.CreateImage("Background", _overlayRoot, Color.white);
            CampUiRuntime.StretchFull(_bgImage.rectTransform);
            _bgImage.preserveAspect = false;
            _bgImage.raycastTarget = true;
            var bgSprite = _uiIcons != null ? _uiIcons.UiLibraryCodexBackground : null;
            if (bgSprite != null)
            {
                _bgImage.sprite = bgSprite;
                _bgImage.color = Color.white;
                _bgImage.type = Image.Type.Simple;
            }
            else
            {
                _bgImage.sprite = null;
                _bgImage.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);
                Debug.LogWarning("[LibraryCodex] 缺少 UiLibraryCodexBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            CreateCloseButton();
            BuildTabs();
            BuildScroll();

            _tooltip = _overlayRoot.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_overlayRoot);

            var detailHost = CampUiRuntime.CreateRect("CardDetailHost", _overlayRoot);
            CampUiRuntime.StretchFull(detailHost.GetComponent<RectTransform>());
            _cardDetail = detailHost.AddComponent<CampCardDetailView>();
            _cardDetail.Initialize(
                _cardPrefab,
                _cardCatalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                playerCharacters: null);
        }

        void CreateCloseButton()
        {
            var go = CampUiRuntime.CreateRect("Close", _overlayRoot);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneClose);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton2 != null)
                img.sprite = _uiIcons.UiButton2;
            else
                img.color = new Color(0.28f, 0.16f, 0.16f, 0.98f);

            var label = CampUiRuntime.CreateText(go.transform, "关闭", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -6f);
            label.color = ButtonLabel;
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            go.AddComponent<CampBuildingHoverView>().Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });
            UiAudioHooks.WireButton(btn);
        }

        void BuildTabs()
        {
            var row = CampUiRuntime.CreateRect("Tabs", _overlayRoot);
            var rowRt = row.GetComponent<RectTransform>();
            SetZone(rowRt, ZoneTabs);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 2, 4);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (var i = 0; i < TabLabels.Length; i++)
            {
                var tab = (CodexTab)i;
                var go = CampUiRuntime.CreateRect($"Tab_{tab}", row.transform);
                go.AddComponent<LayoutElement>().flexibleWidth = 1f;

                var img = go.AddComponent<Image>();
                img.color = TabIdleTint;
                img.raycastTarget = true;
                img.preserveAspect = false;
                if (_uiIcons != null && _uiIcons.UiButton6 != null)
                    img.sprite = _uiIcons.UiButton6;

                var label = CampUiRuntime.CreateText(go.transform, TabLabels[i], 20, FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                CampUiRuntime.StretchFull(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(4f, 2f);
                label.rectTransform.offsetMax = new Vector2(-4f, -6f);
                label.color = TabIdleLabel;
                label.raycastTarget = false;

                var group = go.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
                go.AddComponent<CampBuildingHoverView>().Bind(
                    go.GetComponent<RectTransform>(), group, ButtonHoverScale, hideWhenIdle: false);

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                var captured = tab;
                btn.onClick.AddListener(() => SelectTab(captured, forceRebuild: true));
                UiAudioHooks.WireButton(btn);

                _tabImages.Add(img);
                _tabLabels.Add(label);
            }
        }

        void BuildScroll()
        {
            var scrollGo = CampUiRuntime.CreateRect("Scroll", _overlayRoot);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            SetZone(scrollRt, ZoneContent);
            // 透明底，露出模板内框；仅挡射线
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollBg.raycastTarget = true;

            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 44f;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            var viewportGo = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewport = viewportGo.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewport);
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            viewportGo.AddComponent<RectMask2D>();
            var vpImg = viewportGo.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = true;

            var contentGo = CampUiRuntime.CreateRect("Content", viewportGo.transform);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 20);
            vlg.spacing = 14f;
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
            for (var i = 0; i < _tabImages.Count; i++)
            {
                var active = (CodexTab)i == tab;
                _tabImages[i].color = active ? TabActiveTint : TabIdleTint;
                if (i < _tabLabels.Count)
                    _tabLabels[i].color = active ? TabActiveLabel : TabIdleLabel;
            }

            if (forceRebuild)
                Rebuild();
        }

        void Rebuild()
        {
            ClearDynamic();
            _tooltip?.Hide();
            _cardDetail?.Hide();
            if (_profile == null)
            {
                AddHint("档案未就绪。");
                ForceLayout();
                return;
            }

            switch (_activeTab)
            {
                case CodexTab.PlayerCharacters:
                    BuildPlayerCharacters();
                    break;
                case CodexTab.PlayerCards:
                    BuildPlayerCards();
                    break;
                case CodexTab.EnemyCharacters:
                    BuildEnemyCharacters();
                    break;
                case CodexTab.EnemyCards:
                    BuildEnemyCards();
                    break;
                case CodexTab.Relics:
                    BuildRelics();
                    break;
            }

            ForceLayout(resetScroll: true);
        }

        void BuildPlayerCharacters()
        {
            var ids = TalentCatalog.PlayableCharacterIds;
            var unlocked = 0;
            foreach (var id in ids)
            {
                if (CodexProgressRules.HasOwnedCharacter(_profile, id))
                    unlocked++;
            }

            AddCategoryHeader($"玩家角色 ({unlocked}/{ids.Count})");
            var grid = CreatePortraitGrid(_content, PortraitColumns, 148f, 188f);
            foreach (var characterId in ids)
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

            var seenCount = 0;
            foreach (var entry in entries)
            {
                if (entry != null
                    && !string.IsNullOrEmpty(entry.CharacterId)
                    && CodexProgressRules.HasSeenEnemy(_profile.Codex, entry.CharacterId))
                    seenCount++;
            }

            AddCategoryHeader($"敌方角色 ({seenCount}/{entries.Count})");
            var grid = CreatePortraitGrid(_content, PortraitColumns, 148f, 176f);
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
            le.preferredWidth = 148f;
            le.preferredHeight = 176f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.72f);
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
                _tooltip?.BindHover(go, entry.DisplayName, FormatEnemyStats(entry));
            else
                _tooltip?.BindHover(go, "未遇见", "尚未遇见该敌人。");
        }

        static string FormatEnemyStats(TrainingMonsterCatalog.Entry entry)
        {
            var t = entry?.Template;
            if (t == null)
                return "已遇见该敌人。";

            var habitat = EnemyHabitatCatalog.GetHabitat(entry.CharacterId);
            var lines = new List<string>
            {
                $"基础HP {t.MaxHp}",
                $"速度 {t.Speed}"
            };

            if (!string.IsNullOrEmpty(habitat))
                lines.Add($"出没：{habitat}");

            return string.Join("\n", lines);
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

            var owned = 0;
            foreach (var def in cards)
            {
                if (CodexProgressRules.HasOwnedCard(_profile, def.CardId))
                    owned++;
            }

            AddCategoryHeader($"卡牌 ({owned}/{cards.Count})");
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

            var seen = 0;
            foreach (var def in cards)
            {
                if (CodexProgressRules.HasSeenEnemyCard(_profile.Codex, def.CardId))
                    seen++;
            }

            AddCategoryHeader($"敌方卡牌 ({seen}/{cards.Count})");
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

            var owned = 0;
            foreach (var relic in relics)
            {
                if (relic != null
                    && !string.IsNullOrEmpty(relic.Id)
                    && CodexProgressRules.HasOwnedRelic(_profile.Codex, relic.Id))
                    owned++;
            }

            AddCategoryHeader($"遗物 ({owned}/{relics.Count})");
            var grid = CreatePortraitGrid(_content, RelicColumns, 118f, 142f);
            foreach (var relic in relics)
            {
                if (relic == null || string.IsNullOrEmpty(relic.Id))
                    continue;

                CreateRelicCell(grid, relic, CodexProgressRules.HasOwnedRelic(_profile.Codex, relic.Id));
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
            le.preferredWidth = 148f;
            le.preferredHeight = 188f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.72f);
            bg.raycastTarget = true;
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Portrait", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.08f, 0.22f, 0.92f, 0.94f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _characterVisuals?.GetPortrait(characterId);
            icon.color = unlocked ? Color.white : SilhouetteColor;

            var label = CampUiRuntime.CreateText(go.transform, unlocked ? displayName : "？？？", 15, FontStyle.Bold);
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
            le.preferredWidth = 110f;
            le.preferredHeight = 136f;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.72f);
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

            var label = CampUiRuntime.CreateText(go.transform, owned ? relic.DisplayName : "？？？", 13, FontStyle.Bold);
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
            var faction = _activeTab == CodexTab.PlayerCards ? "远征军" : "怪物";
            view.BindWithCard(
                preview,
                visual,
                selected: false,
                polluted: false,
                interactable: unlocked,
                orderBadge: "",
                statsLine: unlocked
                    ? BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions)
                    : "",
                uiIcons: _uiIcons,
                characterVisuals: _characterVisuals,
                onClick: unlocked
                    ? _ => ShowCardDetail(def, faction)
                    : null,
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

        void ShowCardDetail(CardDefinitionSO def, string faction)
        {
            if (def == null || _cardDetail == null)
                return;

            _tooltip?.Hide();
            _cardDetail.Show(
                def,
                def.CardId,
                showSell: false,
                onBack: null,
                onSell: null,
                factionOverride: faction);
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
                    if (text.gameObject.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0)
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
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 2, 8);
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
            grid.spacing = new Vector2(12f, 14f);
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
            go.AddComponent<LayoutElement>().preferredHeight = 36f;
            var text = go.AddComponent<Text>();
            text.font = CampUiRuntime.DefaultFont;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.color = SectionGold;
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
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null && resetScroll)
                _scroll.verticalNormalizedPosition = 1f;
        }

        static void SetZone(RectTransform rt, Vector4 zone) =>
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);
    }
}
