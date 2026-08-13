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

        const int LayoutVersion = 7;
        const float ButtonHoverScale = 1.06f;
        const float CardBaseW = 168f;
        const float CardBaseH = 236f;
        const int CardsPerRow = 6;
        const int PortraitColumns = 7;
        const int RelicColumns = 6;
        // character_plate：一行 7 个（横向间距按面板宽度拉开）
        const float CharPlateW = 128f;
        const float CharPlateH = 228f;
        // event_plate：一行 6 个（同上）
        const float RelicPlateW = 140f;
        const float RelicPlateH = 152f;
        // 滑动条两端菱形装饰内缩，避免手柄压住菱形
        const float ScrollbarEndInset = 22f;
        // 滑块相对轨道略右移，视觉居中
        const float ScrollbarHandleNudgeX = 3f;

        // 模板归一化（原点左下）：红框基础上略放大并上移
        static readonly Vector4 ZoneClose = new(0.798f, 0.842f, 0.888f, 0.922f);
        static readonly Vector4[] ZoneTabs =
        {
            new(0.168f, 0.770f, 0.298f, 0.842f), // 玩家角色
            new(0.296f, 0.770f, 0.422f, 0.843f), // 卡牌
            new(0.420f, 0.768f, 0.560f, 0.845f), // 敌方角色
            new(0.556f, 0.768f, 0.708f, 0.843f), // 敌方卡牌
            new(0.704f, 0.766f, 0.838f, 0.843f)  // 遗物
        };
        // 内容整体右移约一个滑动条宽；滑动条 Zone 不动
        static readonly Vector4 ZoneContent = new(0.171f, 0.145f, 0.822f, 0.715f);
        static readonly Vector4 ZoneScrollbar = new(0.832f, 0.155f, 0.858f, 0.700f);

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
        Scrollbar _scrollbar;
        InventoryTooltipView _tooltip;
        CampCardDetailView _cardDetail;
        float _cardScale = 0.88f;
        readonly List<Image> _tabImages = new();
        readonly List<Text> _tabLabels = new();
        readonly List<GameObject> _dynamicObjects = new();
        CodexTab _activeTab = CodexTab.PlayerCharacters;
        bool _built;
        int _builtVersion = -1;

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        /// <summary>ESC：卡牌详情 → 图鉴列表 → 关闭。</summary>
        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            if (_cardDetail != null && _cardDetail.IsOpen)
            {
                _cardDetail.Hide();
                return true;
            }

            Hide();
            _onClose?.Invoke();
            return true;
        }

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
            _tooltip.Initialize(_overlayRoot, _uiIcons);

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
            if (_uiIcons != null && _uiIcons.UiButton3 != null)
                img.sprite = _uiIcons.UiButton3;
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
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var tab = (CodexTab)i;
                var go = CampUiRuntime.CreateRect($"Tab_{tab}", _overlayRoot);
                var rt = go.GetComponent<RectTransform>();
                SetZone(rt, ZoneTabs[i]);

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
                go.AddComponent<CampBuildingHoverView>().Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

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
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.001f);
            scrollBg.raycastTarget = true;

            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 44f;
            _scroll.inertia = true;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            var viewportGo = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewport = viewportGo.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewport);
            viewport.offsetMin = new Vector2(6f, 6f);
            viewport.offsetMax = new Vector2(-6f, -6f);
            var vpImg = viewportGo.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.02f);
            vpImg.raycastTarget = true;
            var mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = CampUiRuntime.CreateRect("Content", viewportGo.transform);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 18);
            vlg.spacing = 12f;
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
            BuildScrollbar();
        }

        void BuildScrollbar()
        {
            var barGo = CampUiRuntime.CreateRect("Scrollbar", _overlayRoot);
            var barRt = barGo.GetComponent<RectTransform>();
            SetZone(barRt, ZoneScrollbar);

            var barImg = barGo.AddComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSliderBar != null)
                barImg.sprite = _uiIcons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var slidingArea = CampUiRuntime.CreateRect("Sliding Area", barGo.transform);
            var slidingRt = slidingArea.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(slidingRt);
            // 上下内缩避开菱形；左右略不对称，让滑块视觉居中偏右一丝
            slidingRt.offsetMin = new Vector2(1f + ScrollbarHandleNudgeX, ScrollbarEndInset);
            slidingRt.offsetMax = new Vector2(-1f + ScrollbarHandleNudgeX, -ScrollbarEndInset);

            var handleGo = CampUiRuntime.CreateRect("Handle", slidingArea.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            handleImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSlider != null)
                handleImg.sprite = _uiIcons.UiSlider;
            else
                handleImg.color = new Color(0.42f, 0.34f, 0.28f, 1f);

            _scrollbar = barGo.AddComponent<Scrollbar>();
            _scrollbar.handleRect = handleRt;
            _scrollbar.targetGraphic = handleImg;
            _scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scrollbar.numberOfSteps = 0;
            _scrollbar.size = 1f;
            _scrollbar.value = 1f;

            _scroll.verticalScrollbar = _scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _scroll.verticalScrollbarSpacing = 0f;
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
            var grid = CreatePortraitGrid(_content, PortraitColumns, CharPlateW, CharPlateH);
            foreach (var characterId in ids)
            {
                var owned = CodexProgressRules.HasOwnedCharacter(_profile, characterId);
                var name = CharacterDisplayNames.GetOrFallback(characterId, characterId);
                CreatePortraitCell(
                    grid,
                    characterId,
                    name,
                    owned,
                    owned ? CharacterLoreCatalog.GetTooltipTitle(characterId, name) : "？？？",
                    owned
                        ? CharacterLoreCatalog.GetTooltipBody(characterId)
                        : "尚未拥有该角色。");
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
            var grid = CreatePortraitGrid(_content, PortraitColumns, CharPlateW, CharPlateH);
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
            le.preferredWidth = CharPlateW;
            le.preferredHeight = CharPlateH;
            var bg = go.AddComponent<Image>();
            bg.color = Color.white;
            bg.raycastTarget = true;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiCharacterPlate != null)
                bg.sprite = _uiIcons.UiCharacterPlate;
            else
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Portrait", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.10f, 0.28f, 0.90f, 0.90f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _characterVisuals?.GetPortrait(entry.CharacterId);
            icon.color = seen ? Color.white : SilhouetteColor;

            var name = CampUiRuntime.CreateText(
                go.transform,
                seen ? entry.DisplayName : "？？？",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(name.rectTransform, 0.08f, 0.06f, 0.92f, 0.24f);
            name.color = seen
                ? new Color(0.96f, 0.92f, 0.78f, 1f)
                : new Color(0.45f, 0.45f, 0.5f, 1f);
            name.raycastTarget = false;

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
            RefreshCardScale();
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
            RefreshCardScale();
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
            var grid = CreatePortraitGrid(_content, RelicColumns, RelicPlateW, RelicPlateH);
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
            foreach (var def in EnumerateKnownDefinitions())
            {
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
            foreach (var def in EnumerateKnownDefinitions())
            {
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

        IEnumerable<CardDefinitionSO> EnumerateKnownDefinitions()
        {
            var seen = new HashSet<string>();
            if (_definitions != null)
            {
                foreach (var pair in _definitions)
                {
                    var def = pair.Value;
                    if (def == null || string.IsNullOrEmpty(def.CardId))
                        continue;
                    if (!seen.Add(def.CardId))
                        continue;
                    yield return def;
                }
            }

            foreach (var def in CardCodexCatalog.LoadAllCardDefinitions())
            {
                if (def == null || string.IsNullOrEmpty(def.CardId))
                    continue;
                if (!seen.Add(def.CardId))
                    continue;
                yield return def;
            }
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
            le.preferredWidth = CharPlateW;
            le.preferredHeight = CharPlateH;
            var bg = go.AddComponent<Image>();
            bg.color = Color.white;
            bg.raycastTarget = true;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiCharacterPlate != null)
                bg.sprite = _uiIcons.UiCharacterPlate;
            else
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Portrait", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.10f, 0.28f, 0.90f, 0.90f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _characterVisuals?.GetPortrait(characterId);
            icon.color = unlocked ? Color.white : SilhouetteColor;

            var label = CampUiRuntime.CreateText(
                go.transform,
                unlocked ? displayName : "？？？",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(label.rectTransform, 0.08f, 0.06f, 0.92f, 0.24f);
            label.color = unlocked
                ? new Color(0.96f, 0.92f, 0.78f, 1f)
                : new Color(0.45f, 0.45f, 0.5f, 1f);
            label.raycastTarget = false;

            if (unlocked)
                _tooltip?.BindHover(go, tooltipTitle, tooltipBody);
            else
                _tooltip?.BindHover(go, "未解锁", tooltipBody);
        }

        void CreateRelicCell(Transform parent, RelicDefinition relic, bool owned)
        {
            var go = CampUiRuntime.CreateRect($"Relic_{relic.Id}", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = RelicPlateW;
            le.preferredHeight = RelicPlateH;
            var bg = go.AddComponent<Image>();
            bg.color = Color.white;
            bg.raycastTarget = true;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiEventPlate != null)
                bg.sprite = _uiIcons.UiEventPlate;
            else
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
            _dynamicObjects.Add(go);

            var iconGo = CampUiRuntime.CreateRect("Icon", go.transform);
            CampUiRuntime.SetAnchored(iconGo.GetComponent<RectTransform>(), 0.18f, 0.32f, 0.82f, 0.88f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _relicCatalog?.GetIcon(relic.Id);
            icon.color = owned
                ? (icon.sprite != null ? Color.white : new Color(0.55f, 0.48f, 0.35f, 1f))
                : SilhouetteColor;

            var label = CampUiRuntime.CreateText(
                go.transform,
                owned ? relic.DisplayName : "？？？",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(label.rectTransform, 0.08f, 0.06f, 0.92f, 0.28f);
            label.color = owned
                ? new Color(0.96f, 0.92f, 0.78f, 1f)
                : new Color(0.45f, 0.45f, 0.5f, 1f);
            label.raycastTarget = false;

            if (owned)
                _tooltip?.BindHover(go, relic.DisplayName, relic.Description ?? "");
            else
                _tooltip?.BindHover(go, "未拥有", "尚未拥有该遗物。");
        }

        void RefreshCardScale()
        {
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.GetComponent<RectTransform>());

            // 预留右边距，避免最右一列贴边/被裁切
            var avail = GetContentInnerWidth() - 20f;
            const float gap = 10f;
            const float cellPad = 4f;
            // n*(baseW*scale + cellPad) + (n-1)*gap = avail
            var scale = (avail - gap * (CardsPerRow - 1) - cellPad * CardsPerRow)
                        / (CardsPerRow * CardBaseW);
            _cardScale = Mathf.Clamp(scale * 0.97f, 0.64f, 1.05f);
        }

        float GetContentInnerWidth()
        {
            var avail = _content != null ? _content.rect.width : 0f;
            if (avail < 80f && _overlayRoot != null)
                avail = _overlayRoot.rect.width * (ZoneContent.z - ZoneContent.x) - 24f;
            return Mathf.Max(200f, avail);
        }

        void CreateCardCell(Transform parent, CardDefinitionSO def, bool unlocked)
        {
            var cardWidth = CardBaseW * _cardScale;
            var cardHeight = CardBaseH * _cardScale;
            var holder = CampUiRuntime.CreateRect($"Card_{def.CardId}", parent);
            var holderLe = holder.AddComponent<LayoutElement>();
            holderLe.preferredWidth = cardWidth + 4f;
            holderLe.preferredHeight = cardHeight + 4f;
            _dynamicObjects.Add(holder);

            var preview = CardVisualResolver.CreatePreviewInstance(
                def.CardId,
                def.OwnerCharacterId,
                unlocked ? def.DisplayName : "？？？",
                def);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);

            var view = UnityEngine.Object.Instantiate(_cardPrefab, holder.transform);
            CardView.ApplyHandPresentationScaleCentered(view, _cardScale);
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

            const int padL = 4;
            const int padR = 10; // 右侧多留一点，避免贴滚动条被裁切
            // 可用宽略收紧，拉开间距时不会顶出面板
            var avail = Mathf.Max(columns * cellW, GetContentInnerWidth() - padL - padR - 18f);
            if (columns > 1)
            {
                var minGap = 10f;
                var maxCellW = (avail - minGap * (columns - 1)) / columns;
                if (maxCellW < cellW && maxCellW > 40f)
                {
                    var s = maxCellW / cellW;
                    cellW = maxCellW;
                    cellH *= s;
                }
            }

            var spacingX = columns > 1
                ? Mathf.Max(10f, (avail - columns * cellW) / (columns - 1))
                : 0f;

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(spacingX, 18f);
            grid.padding = new RectOffset(padL, padR, 4, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            // 与敌方角色一致：左起排布，不满一行也不居中
            grid.childAlignment = TextAnchor.UpperLeft;
            return go.transform;
        }

        Transform CreateCardGrid(Transform parent)
        {
            var cardWidth = CardBaseW * _cardScale;
            var cardHeight = CardBaseH * _cardScale;
            var go = CampUiRuntime.CreateRect("CardGrid", parent);
            _dynamicObjects.Add(go);
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cellW = cardWidth + 4f;
            var avail = Mathf.Max(CardsPerRow * cellW, GetContentInnerWidth() - 12f);
            var spacingX = CardsPerRow > 1
                ? Mathf.Max(8f, (avail - CardsPerRow * cellW) / (CardsPerRow - 1))
                : 0f;

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cardHeight + 4f);
            grid.spacing = new Vector2(spacingX, 14f);
            grid.padding = new RectOffset(4, 8, 4, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardsPerRow;
            grid.childAlignment = TextAnchor.UpperCenter;
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
