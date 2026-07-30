using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>卡牌详情：模板底板 + 左卡面 + 右属性/描述/关键词；可选出售。</summary>
    [DisallowMultipleComponent]
    public sealed class CampCardDetailView : MonoBehaviour
    {
        const float ButtonHoverScale = 1.08f;
        // 改布局常量时递增，确保重新进 Play 会拆掉旧树重建
        const int LayoutVersion = 4;
        // 底板 sprite 裁切 1364×1071
        const float DialogAspect = 1071f / 1364f;
        const float DialogW = 1100f;
        const float DialogH = DialogW * DialogAspect;
        // 出售/返回：加宽压矮（高于原生 1.75）
        const float SpriteButtonAspect = 2.9f;
        const float DetailCardFitFactor = 0.52f;
        const float SellGoldIconSize = 18f;

        static readonly Color LabelGold = new(0.92f, 0.82f, 0.52f, 1f);
        static readonly Color BodyText = new(0.88f, 0.90f, 0.94f, 1f);
        static readonly Color ValueText = new(0.96f, 0.92f, 0.78f, 1f);

        // 相对 Dialog（模板 sprite 归一化，原点左下）
        static readonly Vector4 ZoneBack = new(0.788f, 0.910f, 0.982f, 0.948f);
        static readonly Vector4 ZoneCard = new(0.012f, 0.220f, 0.352f, 0.820f);
        static readonly Vector4 ZoneSell = new(0.038f, 0.070f, 0.255f, 0.108f);
        // 金锭+金额：紧贴出售钮
        static readonly Vector4 ZoneSellGold = new(0.262f, 0.070f, 0.380f, 0.108f);
        // 属性值区上移，落在分隔线下方格心
        static readonly Vector4[] ZoneAttrValueCells =
        {
            new(0.368f, 0.708f, 0.454f, 0.818f),
            new(0.463f, 0.708f, 0.573f, 0.818f),
            new(0.581f, 0.708f, 0.691f, 0.818f),
            new(0.699f, 0.708f, 0.824f, 0.818f),
            new(0.831f, 0.708f, 0.957f, 0.818f)
        };
        static readonly Vector4 ZoneDescBody = new(0.358f, 0.365f, 0.915f, 0.575f);
        static readonly Vector4 ZoneDetailBody = new(0.358f, 0.175f, 0.915f, 0.345f);

        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        List<CharacterDefinitionSO> _playerCharacters = new();

        RectTransform _root;
        RectTransform _dialog;
        Image _dialogImage;
        RectTransform _cardAnchor;
        RectTransform _sellRow;
        Text[] _attrValues = Array.Empty<Text>();
        Text _descText;
        Text _keywordText;
        Text _sellGoldText;
        Image _sellGoldIcon;
        Button _sellButton;
        Action _onBack;
        Action _onSell;
        bool _built;
        int _builtVersion;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Initialize(
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            List<CharacterDefinitionSO> playerCharacters)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _playerCharacters = playerCharacters ?? new List<CharacterDefinitionSO>();
            EnsureBuilt();
        }

        public void Show(
            CardDefinitionSO definition,
            string cardId,
            bool showSell,
            Action onBack,
            Action onSell = null,
            string factionOverride = null,
            bool isEngraved = false)
        {
            EnsureBuilt();
            ApplyTemplateBackground();
            _onBack = onBack;
            _onSell = onSell;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            if (_sellRow != null)
                _sellRow.gameObject.SetActive(showSell);
            if (_sellButton != null)
                _sellButton.gameObject.SetActive(showSell);

            // isEngraved 仅保留兼容调用方；不再在稀有度上展示「已刻印」
            _ = isEngraved;
            Bind(definition, cardId, showSell, factionOverride);
        }

        public void Hide()
        {
            ClearCard();
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        void Bind(CardDefinitionSO definition, string cardId, bool showSell, string factionOverride)
        {
            ClearCard();
            if (string.IsNullOrEmpty(cardId) && definition != null)
                cardId = definition.CardId;

            var ownerId = definition?.OwnerCharacterId ?? "";
            var displayName = definition?.DisplayName ?? cardId;
            var preview = CardVisualResolver.CreatePreviewInstance(cardId, ownerId, displayName, definition);
            var descCard = CardVisualResolver.ResolveForDescription(preview, _definitions);

            var rarity = definition != null
                ? CampCardUiLabels.FormatRarity(definition.Rarity)
                : CampCardUiLabels.FormatRarity(cardId);
            var cost = descCard != null ? descCard.Cost.ToString() : "-";
            var type = descCard != null ? CampCardUiLabels.FormatType(descCard.CardType) : "-";
            var faction = !string.IsNullOrEmpty(factionOverride)
                ? factionOverride
                : FormatFaction(ownerId, _playerCharacters);
            var owner = FormatOwnerName(ownerId);

            if (_attrValues.Length >= 5)
            {
                _attrValues[0].text = rarity;
                _attrValues[1].text = cost;
                _attrValues[2].text = type;
                _attrValues[3].text = faction;
                _attrValues[4].text = owner;
            }

            _descText.text = ResolveCardDescription(definition, displayName);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, descCard, _definitions)
                .Replace("<b>", "")
                .Replace("</b>", "");
            _keywordText.text = string.IsNullOrWhiteSpace(keywords)
                ? "（无关键词说明）"
                : keywords;

            if (_cardPrefab != null && _cardAnchor != null)
            {
                Canvas.ForceUpdateCanvases();
                var view = Instantiate(_cardPrefab, _cardAnchor);
                var aw = Mathf.Max(1f, _cardAnchor.rect.width);
                var ah = Mathf.Max(1f, _cardAnchor.rect.height);
                var scale = Mathf.Min(aw / CardPortraitLayout.CardWidth, ah / CardPortraitLayout.CardHeight)
                            * DetailCardFitFactor;
                scale = Mathf.Clamp(scale, 0.75f, 1.35f);
                CardView.ApplyHandPresentationScaleCentered(view, scale);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                view.BindWithCard(
                    preview,
                    visual,
                    false,
                    false,
                    false,
                    "",
                    statsLine,
                    _uiIcons,
                    _characterVisuals,
                    null,
                    null,
                    null);
            }

            if (showSell)
                RefreshSellPrice(cardId, definition);
        }

        void RefreshSellPrice(string cardId, CardDefinitionSO definition)
        {
            var rarity = definition != null ? definition.Rarity : CardRarityTable.GetOrDefault(cardId);
            var gold = CampCollectionRules.GetSellGold(rarity);
            if (_sellGoldText != null)
                _sellGoldText.text = gold.ToString();
            if (_sellGoldIcon != null && _uiIcons?.CampGoldIcon != null)
                _sellGoldIcon.sprite = _uiIcons.CampGoldIcon;
        }

        void ClearCard()
        {
            if (_cardAnchor == null)
                return;

            foreach (Transform child in _cardAnchor)
                Destroy(child.gameObject);
        }

        void EnsureBuilt()
        {
            if (_built && _builtVersion == LayoutVersion && _root != null)
                return;

            if (_root != null)
                Destroy(_root.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;
            var rootGo = CampUiRuntime.CreateRect("CardDetailRoot", transform);
            _root = rootGo.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_root);

            // 半透明遮罩：透出上一层（收藏 / 图书馆），不用纯色底板
            var dim = rootGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.32f);
            dim.raycastTarget = true;

            _dialogImage = CampUiRuntime.CreateImage("Dialog", _root, Color.white);
            _dialog = _dialogImage.rectTransform;
            _dialog.anchorMin = new Vector2(0.5f, 0.5f);
            _dialog.anchorMax = new Vector2(0.5f, 0.5f);
            _dialog.pivot = new Vector2(0.5f, 0.5f);
            _dialog.sizeDelta = new Vector2(DialogW, DialogW * DialogAspect);
            _dialogImage.preserveAspect = false;
            _dialogImage.raycastTarget = true;
            ApplyTemplateBackground();

            CreateSpriteButton(
                "Back",
                ZoneBack,
                _uiIcons != null ? _uiIcons.UiButton2 : null,
                "返回",
                20,
                true,
                () =>
                {
                    Hide();
                    _onBack?.Invoke();
                });

            _cardAnchor = CampUiRuntime.CreateRect("CardAnchor", _dialog).GetComponent<RectTransform>();
            SetZone(_cardAnchor, ZoneCard);
            var cardBlocker = _cardAnchor.gameObject.AddComponent<Image>();
            cardBlocker.color = new Color(0f, 0f, 0f, 0f);
            cardBlocker.raycastTarget = false;

            // 属性值：格内正中；稀有度略偏左
            _attrValues = new Text[ZoneAttrValueCells.Length];
            for (var i = 0; i < ZoneAttrValueCells.Length; i++)
            {
                var cell = CampUiRuntime.CreateRect($"Attr{i}", _dialog);
                SetZone(cell.GetComponent<RectTransform>(), ZoneAttrValueCells[i]);

                _attrValues[i] = CampUiRuntime.CreateText(cell.transform, "-", 18, FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                CampUiRuntime.StretchFull(_attrValues[i].rectTransform);
                // 抬高到底中；稀有度格心略左
                var leftPad = i == 0 ? -6f : 2f;
                var rightPad = i == 0 ? -14f : -2f;
                _attrValues[i].rectTransform.offsetMin = new Vector2(leftPad, 16f);
                _attrValues[i].rectTransform.offsetMax = new Vector2(rightPad, -2f);
                _attrValues[i].color = ValueText;
                _attrValues[i].raycastTarget = false;
                _attrValues[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _attrValues[i].verticalOverflow = VerticalWrapMode.Truncate;
            }

            // 描述 / 详细说明：靠左直接写在模板凹槽内，不加新框
            _descText = CampUiRuntime.CreateText(_dialog, "", 17, FontStyle.Normal, TextAnchor.UpperLeft);
            SetZone(_descText.rectTransform, ZoneDescBody);
            _descText.rectTransform.offsetMin = new Vector2(4f, 6f);
            _descText.rectTransform.offsetMax = new Vector2(-10f, -6f);
            _descText.color = BodyText;
            _descText.raycastTarget = false;
            _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descText.verticalOverflow = VerticalWrapMode.Truncate;
            _descText.alignment = TextAnchor.UpperLeft;

            _keywordText = CampUiRuntime.CreateText(_dialog, "", 17, FontStyle.Normal, TextAnchor.UpperLeft);
            SetZone(_keywordText.rectTransform, ZoneDetailBody);
            _keywordText.rectTransform.offsetMin = new Vector2(4f, 6f);
            _keywordText.rectTransform.offsetMax = new Vector2(-10f, -6f);
            _keywordText.color = BodyText;
            _keywordText.raycastTarget = false;
            _keywordText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _keywordText.verticalOverflow = VerticalWrapMode.Truncate;
            _keywordText.alignment = TextAnchor.UpperLeft;

            _sellButton = CreateSpriteButton(
                "Sell",
                ZoneSell,
                _uiIcons != null ? _uiIcons.UiButton3 : null,
                "出售卡牌",
                18,
                true,
                () => _onSell?.Invoke());

            // 金锭必须显式 sizeDelta：LayoutElement 在 childControl=false 时不生效（之前一直是默认 100×100）
            _sellRow = CampUiRuntime.CreateRect("SellGold", _dialog).GetComponent<RectTransform>();
            SetZone(_sellRow, ZoneSellGold);

            _sellGoldIcon = CampUiRuntime.CreateImage("Icon", _sellRow, Color.white);
            _sellGoldIcon.preserveAspect = true;
            _sellGoldIcon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            _sellGoldIcon.raycastTarget = false;
            var iconRt = _sellGoldIcon.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(SellGoldIconSize, SellGoldIconSize);

            _sellGoldText = CampUiRuntime.CreateText(_sellRow, "0", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            _sellGoldText.color = LabelGold;
            _sellGoldText.raycastTarget = false;
            var priceRt = _sellGoldText.rectTransform;
            priceRt.anchorMin = Vector2.zero;
            priceRt.anchorMax = Vector2.one;
            priceRt.offsetMin = new Vector2(SellGoldIconSize + 4f, 0f);
            priceRt.offsetMax = Vector2.zero;

            _root.gameObject.SetActive(false);
        }

        void ApplyTemplateBackground()
        {
            if (_dialogImage == null)
                return;

            var bg = _uiIcons != null ? _uiIcons.UiCardDetailBackground : null;
            if (bg != null)
            {
                _dialogImage.sprite = bg;
                _dialogImage.color = Color.white;
                _dialogImage.type = Image.Type.Simple;
                return;
            }

            _dialogImage.sprite = null;
            _dialogImage.color = new Color(0.07f, 0.08f, 0.1f, 0.98f);
            Debug.LogWarning("[CardDetail] 缺少 UiCardDetailBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
        }

        Button CreateSpriteButton(
            string id,
            Vector4 zone,
            Sprite sprite,
            string label,
            int fontSize,
            bool coverWithSpriteAspect,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, _dialog);
            var rt = go.GetComponent<RectTransform>();
            if (coverWithSpriteAspect)
                SetZoneCoverAspect(rt, zone, SpriteButtonAspect);
            else
                SetZone(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.35f, 0.28f, 0.18f, 0.95f);

            var text = CampUiRuntime.CreateText(go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(text.rectTransform);
            // Legacy 字体视觉中心偏上，略下移以落在按钮正中
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -6f);
            text.color = ValueText;
            text.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        static void SetZone(RectTransform rt, Vector4 zone)
        {
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);
        }

        /// <summary>以 zone 为热区，按素材宽高比放大到完全盖住（避免横向拉扁）。</summary>
        static void SetZoneCoverAspect(RectTransform rt, Vector4 zone, float aspect)
        {
            var cx = (zone.x + zone.z) * 0.5f;
            var cy = (zone.y + zone.w) * 0.5f;
            var rw = (zone.z - zone.x) * DialogW;
            var rh = (zone.w - zone.y) * DialogH;
            var bw = rw;
            var bh = bw / aspect;
            if (bh < rh)
            {
                bh = rh;
                bw = bh * aspect;
            }

            var nw = bw / DialogW;
            var nh = bh / DialogH;
            CampUiRuntime.SetAnchored(rt, cx - nw * 0.5f, cy - nh * 0.5f, cx + nw * 0.5f, cy + nh * 0.5f);
        }

        public static string FormatFaction(string ownerId, IReadOnlyList<CharacterDefinitionSO> playerCharacters)
        {
            if (string.IsNullOrEmpty(ownerId) || playerCharacters == null)
                return "怪物";

            foreach (var character in playerCharacters)
            {
                if (character != null && character.CharacterId == ownerId)
                    return "远征军";
            }

            return "怪物";
        }

        string FormatOwnerName(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return "-";

            foreach (var character in _playerCharacters)
            {
                if (character != null && character.CharacterId == ownerId)
                    return CharacterDisplayNames.GetOrFallback(ownerId, character.DisplayName);
            }

            return CharacterDisplayNames.GetOrFallback(ownerId, ownerId);
        }

        static string ResolveCardDescription(CardDefinitionSO def, string fallback)
        {
            if (def == null)
                return fallback ?? "";

            if (CardDescriptionCatalog.TryGetByCardId(def.CardId, out var byId) && !string.IsNullOrWhiteSpace(byId))
                return byId;

            if (CardDescriptionCatalog.TryGetByDisplayName(def.DisplayName, out var byName)
                && !string.IsNullOrWhiteSpace(byName))
                return byName;

            return string.IsNullOrEmpty(def.DisplayName) ? def.CardId : def.DisplayName;
        }
    }
}
