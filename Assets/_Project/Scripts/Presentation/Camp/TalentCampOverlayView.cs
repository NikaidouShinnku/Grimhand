using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 天赋祭坛：单层 UI。模板底图 + 左角色/切换 + 双槽垂直天赋 + 底栏生效效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TalentCampOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 23;
        const float TemplateW = 1672f;
        const float TemplateH = 941f;
        const float ButtonAspect = 512f / 292f;
        const float ButtonHoverScale = 1.06f;
        const int TalentRowsPerSlot = 5;
        // 符文边长略放大，仍贴近模板圆槽
        const float RuneSizePx = 85f;
        // 切换角色缩略图：从第 2 个起逐个右移；恶魔(char_ranger)/巫妖再略右
        const float ThumbProgressiveShift = 0.014f;
        const float ThumbExtraShiftDemonLich = 0.028f;
        // 仅恶魔大立绘：相对默认肖像区略右移（框/名/经验不动）
        const float PortraitExtraShiftDemon = 0.016f;

        // 模板归一化（原点左下）
        // 返回：拉长盖住模板钮，略加高（直接铺满热区，勿用 Cover/Fit 乱缩放）
        static readonly Vector4 ZoneBack = new(0.842f, 0.908f, 0.978f, 0.972f);
        static readonly Vector4 ZonePortrait = new(0.090f, 0.470f, 0.255f, 0.825f);
        // 角色名略右，落在平台铭牌视觉中心
        static readonly Vector4 ZoneName = new(0.108f, 0.408f, 0.263f, 0.450f);
        // 等级/经验：贴齐左侧竖线后再略右约 1mm；满级绿条需顶到模板细槽右缘
        static readonly Vector4 ZoneLevel = new(0.106f, 0.372f, 0.261f, 0.405f);
        static readonly Vector4 ZoneXpBar = new(0.106f, 0.348f, 0.278f, 0.368f);
        // 隐形热区：左恢复原对齐；右再略右移
        static readonly Vector4 ZoneCharPrev = new(0.038f, 0.095f, 0.070f, 0.200f);
        static readonly Vector4 ZoneCharNext = new(0.316f, 0.095f, 0.348f, 0.200f);
        // 缩略图落入模板五框（略上移）
        static readonly Vector4 ZoneCharThumbs = new(0.078f, 0.078f, 0.288f, 0.235f);
        // 槽位标题：右下微调，落入石柱顶帽中心
        static readonly Vector4 ZoneSlot1Header = new(0.418f, 0.778f, 0.578f, 0.826f);
        static readonly Vector4 ZoneSlot2Header = new(0.706f, 0.778f, 0.866f, 0.826f);
        // 天赋列：符文位不动；等级略左，拉开与符文间距
        static readonly Vector4 ZoneSlot1Lv = new(0.400f, 0.332f, 0.440f, 0.772f);
        static readonly Vector4 ZoneSlot1Icon = new(0.455f, 0.332f, 0.510f, 0.772f);
        static readonly Vector4 ZoneSlot1Name = new(0.515f, 0.332f, 0.695f, 0.772f);
        static readonly Vector4 ZoneSlot2Lv = new(0.697f, 0.332f, 0.737f, 0.772f);
        static readonly Vector4 ZoneSlot2Icon = new(0.752f, 0.332f, 0.807f, 0.772f);
        static readonly Vector4 ZoneSlot2Name = new(0.812f, 0.332f, 0.980f, 0.772f);
        // 底栏生效效果右移
        static readonly Vector4 ZoneEffect1 = new(0.400f, 0.048f, 0.640f, 0.158f);
        static readonly Vector4 ZoneEffect2 = new(0.690f, 0.048f, 0.930f, 0.158f);

        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyText = new(0.86f, 0.88f, 0.94f, 1f);
        static readonly Color MuteText = new(0.70f, 0.74f, 0.82f, 1f);
        static readonly Color EquippedGold = new(1f, 0.88f, 0.38f, 1f);
        static readonly Color ButtonLabel = new(0.96f, 0.92f, 0.78f, 1f);
        static readonly Color XpBarFill = new(0.28f, 0.78f, 0.38f, 1f);

        BattleSetupSO _battleSetup;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        CampMetaState _meta;
        Action<CampMetaState> _onMetaChanged;
        Action _onClose;

        List<CharacterDefinitionSO> _ownedCharacters = new();
        int _selectedIndex;

        RectTransform _overlayRoot;
        Image _bgImage;
        Image _portraitImage;
        CampIdlePortraitAnimator _portraitAnimator;
        Text _nameText;
        Text _levelText;
        Image _xpFill;
        Text _xpText;
        Text _slot1Header;
        Text _slot2Header;
        Text _effect1Text;
        Text _effect2Text;
        RectTransform _thumbHost;
        RectTransform _slot1Host;
        RectTransform _slot2Host;
        GameObject _tooltipPanel;
        Text _tooltipTitle;
        Text _tooltipBody;

        bool _built;
        int _builtVersion = -1;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            CloseOverlay();
            return true;
        }

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
            foreach (var characterId in TalentCatalog.PlayableCharacterIds)
            {
                var progress = _meta.GetOrCreate(characterId);
                MetaProgressionRules.NormalizeProgress(progress);
                TalentRules.PruneInvalidSelections(progress);
            }

            _ownedCharacters = CollectOwnedCharacters();
            if (_selectedIndex < 0 || _selectedIndex >= _ownedCharacters.Count)
                _selectedIndex = 0;

            EnsureBuilt();
            HideTooltip();
            _overlayRoot.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshAll();
        }

        public void Hide()
        {
            HideTooltip();
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

            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _overlayRoot = CampUiRuntime.CreateRect("TalentCampOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            _bgImage = CampUiRuntime.CreateImage("Background", _overlayRoot, Color.white);
            CampUiRuntime.StretchFull(_bgImage.rectTransform);
            _bgImage.preserveAspect = false;
            _bgImage.raycastTarget = true;
            var bgSprite = _uiIcons != null ? _uiIcons.UiTalentAltarBackground : null;
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
                Debug.LogWarning("[TalentCamp] 缺少 UiTalentAltarBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            CreateBackButton();
            BuildPortraitBlock();
            BuildCharacterSwitcher();
            BuildSlotHeaders();
            _slot1Host = CampUiRuntime.CreateRect("Slot1Rows", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_slot1Host);
            _slot2Host = CampUiRuntime.CreateRect("Slot2Rows", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_slot2Host);
            BuildEffectsBlock();
            BuildTooltip();
        }

        void CreateBackButton()
        {
            var go = CampUiRuntime.CreateRect("Back", _overlayRoot);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneBack);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton2 != null)
                img.sprite = _uiIcons.UiButton2;
            else
                img.color = new Color(0.28f, 0.3f, 0.36f, 1f);

            var label = CampUiRuntime.CreateText(go.transform, "返回", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
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
            btn.onClick.AddListener(CloseOverlay);
            UiAudioHooks.WireButton(btn);
        }

        void BuildPortraitBlock()
        {
            _portraitImage = CampUiRuntime.CreateImage("Portrait", _overlayRoot, Color.white);
            _portraitImage.preserveAspect = true;
            _portraitImage.raycastTarget = false;
            SetZone(_portraitImage.rectTransform, ZonePortrait);
            _portraitAnimator = _portraitImage.gameObject.AddComponent<CampIdlePortraitAnimator>();

            _nameText = CampUiRuntime.CreateText(_overlayRoot, "", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetZone(_nameText.rectTransform, ZoneName);
            _nameText.color = Color.white;
            _nameText.raycastTarget = false;

            _levelText = CampUiRuntime.CreateText(_overlayRoot, "", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetZone(_levelText.rectTransform, ZoneLevel);
            _levelText.color = BodyText;
            _levelText.raycastTarget = false;

            // 经验槽：透明底，仅绿填充叠在模板细槽上
            var xpBg = CampUiRuntime.CreateImage("XpBarBg", _overlayRoot, new Color(0f, 0f, 0f, 0.01f));
            SetZone(xpBg.rectTransform, ZoneXpBar);
            xpBg.raycastTarget = false;

            _xpFill = CampUiRuntime.CreateImage("XpFill", xpBg.transform, XpBarFill);
            var fillRt = _xpFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);
            _xpFill.raycastTarget = false;

            _xpText = CampUiRuntime.CreateText(xpBg.transform, "", 12, FontStyle.Normal, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(_xpText.rectTransform);
            _xpText.color = BodyText;
            _xpText.raycastTarget = false;
        }

        void BuildCharacterSwitcher()
        {
            // 仅隐形热区盖住模板箭头，不叠黄色字
            CreateInvisibleArrowHit("CharPrev", ZoneCharPrev, () => CycleCharacter(-1));
            CreateInvisibleArrowHit("CharNext", ZoneCharNext, () => CycleCharacter(1));

            _thumbHost = CampUiRuntime.CreateRect("CharThumbs", _overlayRoot).GetComponent<RectTransform>();
            SetZone(_thumbHost, ZoneCharThumbs);
        }

        void CreateInvisibleArrowHit(string id, Vector4 zone, Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, _overlayRoot);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void BuildSlotHeaders()
        {
            _slot1Header = CampUiRuntime.CreateText(_overlayRoot, "槽位 1", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetZone(_slot1Header.rectTransform, ZoneSlot1Header);
            _slot1Header.color = TitleGold;
            _slot1Header.raycastTarget = false;

            _slot2Header = CampUiRuntime.CreateText(_overlayRoot, "槽位 2", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetZone(_slot2Header.rectTransform, ZoneSlot2Header);
            _slot2Header.color = TitleGold;
            _slot2Header.raycastTarget = false;
        }

        void BuildEffectsBlock()
        {
            _effect1Text = CampUiRuntime.CreateText(_overlayRoot, "", 14, FontStyle.Normal, TextAnchor.UpperLeft);
            SetZone(_effect1Text.rectTransform, ZoneEffect1);
            _effect1Text.color = BodyText;
            _effect1Text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _effect1Text.verticalOverflow = VerticalWrapMode.Truncate;
            _effect1Text.raycastTarget = false;

            _effect2Text = CampUiRuntime.CreateText(_overlayRoot, "", 14, FontStyle.Normal, TextAnchor.UpperLeft);
            SetZone(_effect2Text.rectTransform, ZoneEffect2);
            _effect2Text.color = BodyText;
            _effect2Text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _effect2Text.verticalOverflow = VerticalWrapMode.Truncate;
            _effect2Text.raycastTarget = false;
        }

        void BuildTooltip()
        {
            _tooltipPanel = CampUiRuntime.CreateImage("TalentTooltip", _overlayRoot, Color.white).gameObject;
            var rt = _tooltipPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.18f);
            rt.anchorMax = new Vector2(0.5f, 0.18f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(520f, 110f);

            var tipImg = _tooltipPanel.GetComponent<Image>();
            if (_uiIcons != null && _uiIcons.UiInformationPlate != null)
            {
                tipImg.sprite = _uiIcons.UiInformationPlate;
                tipImg.type = Image.Type.Simple;
                tipImg.preserveAspect = false;
                tipImg.color = Color.white;
            }
            else
            {
                tipImg.color = new Color(0.08f, 0.1f, 0.14f, 0.98f);
            }

            _tooltipTitle = CampUiRuntime.CreateText(_tooltipPanel.transform, "", 16, FontStyle.Bold,
                TextAnchor.UpperLeft);
            _tooltipTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tooltipTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tooltipTitle.rectTransform.offsetMin = new Vector2(32f, -40f);
            _tooltipTitle.rectTransform.offsetMax = new Vector2(-32f, -16f);
            _tooltipTitle.color = TitleGold;

            _tooltipBody = CampUiRuntime.CreateText(_tooltipPanel.transform, "", 14, FontStyle.Normal,
                TextAnchor.UpperLeft);
            _tooltipBody.rectTransform.anchorMin = Vector2.zero;
            _tooltipBody.rectTransform.anchorMax = Vector2.one;
            _tooltipBody.rectTransform.offsetMin = new Vector2(32f, 18f);
            _tooltipBody.rectTransform.offsetMax = new Vector2(-32f, -44f);
            _tooltipBody.color = BodyText;
            _tooltipBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            _tooltipPanel.SetActive(false);
        }

        void CloseOverlay()
        {
            _onMetaChanged?.Invoke(_meta);
            Hide();
            _onClose?.Invoke();
        }

        void CycleCharacter(int delta)
        {
            if (_ownedCharacters.Count == 0)
                return;

            _selectedIndex = (_selectedIndex + delta + _ownedCharacters.Count) % _ownedCharacters.Count;
            HideTooltip();
            RefreshAll();
        }

        void SelectCharacter(int index)
        {
            if (index < 0 || index >= _ownedCharacters.Count)
                return;

            _selectedIndex = index;
            HideTooltip();
            RefreshAll();
        }

        void RefreshAll()
        {
            ClearDynamic();
            if (_ownedCharacters.Count == 0)
            {
                ApplyPortraitZone("");
                _portraitAnimator?.Bind(_portraitImage, _characterVisuals, "");
                _nameText.text = "无可用角色";
                _levelText.text = "";
                _xpText.text = "";
                _effect1Text.text = "";
                _effect2Text.text = "";
                return;
            }

            var character = _ownedCharacters[_selectedIndex];
            var progress = _meta.GetOrCreate(character.CharacterId);

            ApplyPortraitZone(character.CharacterId);
            _portraitAnimator?.Bind(_portraitImage, _characterVisuals, character.CharacterId);
            _nameText.text = character.DisplayName;
            _levelText.text = $"Lv.{progress.OutOfRunLevel}";
            RefreshXpBar(progress);
            RebuildThumbs();
            RebuildSlotRows(_slot1Host, character.CharacterId, 1, progress,
                ZoneSlot1Lv, ZoneSlot1Icon, ZoneSlot1Name);
            RebuildSlotRows(_slot2Host, character.CharacterId, 2, progress,
                ZoneSlot2Lv, ZoneSlot2Icon, ZoneSlot2Name);
            RefreshEffects(progress);
        }

        void RefreshXpBar(CharacterMetaProgress progress)
        {
            if (MetaProgressionRules.IsMaxLevel(progress))
            {
                _xpFill.rectTransform.anchorMax = new Vector2(1f, 1f);
                _xpText.text = "满级";
                return;
            }

            var need = MetaProgressionRules.XpRequiredForNextLevel(progress);
            var t = need > 0 ? Mathf.Clamp01(progress.OutOfRunXp / (float)need) : 0f;
            _xpFill.rectTransform.anchorMax = new Vector2(t, 1f);
            _xpText.text = MetaProgressionRules.FormatXpProgress(progress);
        }

        void RebuildThumbs()
        {
            if (_thumbHost == null)
                return;

            var count = _ownedCharacters.Count;
            for (var i = 0; i < count; i++)
            {
                var character = _ownedCharacters[i];
                var go = CampUiRuntime.CreateRect($"Thumb_{i}", _thumbHost);
                var rt = go.GetComponent<RectTransform>();
                var x0 = i / (float)count;
                var x1 = (i + 1) / (float)count;
                var shift = i * ThumbProgressiveShift;
                // 恶魔(char_ranger) / 巫妖女王：再略右
                if (character.CharacterId == TalentCatalog.RangerId
                    || character.CharacterId == TalentCatalog.LichQueenId)
                    shift += ThumbExtraShiftDemonLich;
                CampUiRuntime.SetAnchored(rt, x0 + 0.015f + shift, 0.06f, x1 - 0.015f + shift, 0.98f);

                var img = go.AddComponent<Image>();
                img.color = Color.white;
                img.preserveAspect = true;
                img.raycastTarget = true;
                img.sprite = _characterVisuals != null
                    ? _characterVisuals.GetPortrait(character.CharacterId)
                    : null;

                var selected = i == _selectedIndex;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = selected
                    ? new Color(1f, 0.84f, 0.28f, 1f)
                    : new Color(0.2f, 0.22f, 0.28f, 0.6f);
                outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                var captured = i;
                btn.onClick.AddListener(() => SelectCharacter(captured));
                UiAudioHooks.WireButton(btn);

                _dynamicObjects.Add(go);
            }
        }

        void RebuildSlotRows(
            RectTransform host,
            string characterId,
            int slot,
            CharacterMetaProgress progress,
            Vector4 zoneLv,
            Vector4 zoneIcon,
            Vector4 zoneName)
        {
            if (host == null)
                return;

            var talents = TalentCatalog.GetSlotTalents(characterId, slot);
            var runeSprite = _uiIcons != null ? _uiIcons.TalentRunePlate : null;
            var rows = Mathf.Min(TalentRowsPerSlot, talents.Count);

            for (var i = 0; i < rows; i++)
            {
                var talent = talents[i];
                var state = TalentRules.GetCardState(talent, progress);
                var t0 = 1f - (i + 1) / (float)TalentRowsPerSlot;
                var t1 = 1f - i / (float)TalentRowsPerSlot;

                var row = CampUiRuntime.CreateRect($"Talent_{talent.Id}", host);
                CampUiRuntime.StretchFull(row.GetComponent<RectTransform>());
                _dynamicObjects.Add(row);

                var lv = CampUiRuntime.CreateText(row.transform, $"Lv{talent.UnlockLevel}", 16, FontStyle.Bold,
                    TextAnchor.MiddleRight);
                SetRowBand(lv.rectTransform, zoneLv, t0, t1);
                lv.color = state == TalentCardState.Locked ? MuteText : BodyText;
                lv.raycastTarget = false;

                var iconGo = CampUiRuntime.CreateRect("Icon", row.transform);
                var iconRt = iconGo.GetComponent<RectTransform>();
                SetSquareInColumn(iconRt, zoneIcon, t0, t1);

                var icon = iconGo.AddComponent<Image>();
                icon.sprite = runeSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = state == TalentCardState.Locked
                    ? new Color(0.42f, 0.42f, 0.45f, 0.85f)
                    : state == TalentCardState.Selected
                        ? new Color(1f, 0.96f, 0.78f, 1f)
                        : Color.white;

                if (state == TalentCardState.Selected)
                {
                    var outline = iconGo.AddComponent<Outline>();
                    outline.effectColor = EquippedGold;
                    outline.effectDistance = new Vector2(2.5f, -2.5f);
                }

                if (state == TalentCardState.Locked)
                {
                    var lockImg = CampUiRuntime.CreateImage("Lock", iconGo.transform, new Color(0f, 0f, 0f, 0.55f));
                    CampUiRuntime.StretchFull(lockImg.rectTransform);
                    lockImg.raycastTarget = false;
                    var lockText = CampUiRuntime.CreateText(lockImg.transform, "锁", 14, FontStyle.Bold,
                        TextAnchor.MiddleCenter);
                    CampUiRuntime.StretchFull(lockText.rectTransform);
                    lockText.raycastTarget = false;
                }

                var name = CampUiRuntime.CreateText(row.transform, talent.ShortTitle, 19, FontStyle.Bold,
                    TextAnchor.MiddleLeft);
                SetRowBand(name.rectTransform, zoneName, t0, t1);
                name.color = state == TalentCardState.Locked ? MuteText : BodyText;
                name.raycastTarget = false;

                var hitGo = CampUiRuntime.CreateRect("Hit", row.transform);
                var hitRt = hitGo.GetComponent<RectTransform>();
                var y0 = Lerp(zoneLv.y, zoneLv.w, t0);
                var y1 = Lerp(zoneLv.y, zoneLv.w, t1);
                CampUiRuntime.SetAnchored(hitRt, zoneLv.x, y0, zoneName.z, y1);
                var hit = hitGo.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0.01f);
                hit.raycastTarget = true;

                var btn = hitGo.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                var captured = talent;
                btn.onClick.AddListener(() => OnTalentClicked(captured));
                UiAudioHooks.WireButton(btn);

                var hover = hitGo.AddComponent<TalentRowHoverRelay>();
                hover.Bind(
                    () => ShowTooltip(captured, state),
                    HideTooltip);
            }
        }

        void RefreshEffects(CharacterMetaProgress progress)
        {
            _effect1Text.text = FormatEffectColumn(progress, 1);
            _effect2Text.text = FormatEffectColumn(progress, 2);
        }

        static string FormatEffectColumn(CharacterMetaProgress progress, int slot)
        {
            var talentId = progress.GetSelectedTalentId(slot);
            if (string.IsNullOrEmpty(talentId))
                return $"槽位 {slot}：未装备";

            var talent = TalentCatalog.Get(talentId);
            if (talent == null)
                return $"槽位 {slot}：未装备";

            return $"槽位 {slot}：{talent.ShortTitle}  Lv.{talent.UnlockLevel}\n{talent.Description}";
        }

        void OnTalentClicked(TalentDefinition talent)
        {
            if (talent == null || _ownedCharacters.Count == 0)
                return;

            var character = _ownedCharacters[_selectedIndex];
            var progress = _meta.GetOrCreate(character.CharacterId);
            if (!TalentRules.IsUnlocked(talent, progress))
                return;

            TalentRules.TryToggleSelection(talent, progress);
            _onMetaChanged?.Invoke(_meta);
            HideTooltip();
            RefreshAll();
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
                ? $"需要局外等级 Lv.{talent.UnlockLevel} 才能解锁此天赋。\n{talent.Description}"
                : talent.Description;

            var panelRt = _tooltipPanel.GetComponent<RectTransform>();
            var panelW = UiInfoPlateMetrics.MaxWidth;
            var innerW = UiInfoPlateMetrics.InnerWidth(panelW);
            var titleH = UiInfoPlateMetrics.MeasureHeight(_tooltipTitle, _tooltipTitle.text, innerW);
            var bodyH = UiInfoPlateMetrics.MeasureHeight(_tooltipBody, _tooltipBody.text, innerW);
            var gap = 8f;
            panelRt.sizeDelta = new Vector2(panelW, titleH + gap + bodyH + UiInfoPlateMetrics.PadY * 2f);

            var titleRt = _tooltipTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -UiInfoPlateMetrics.PadY);
            titleRt.sizeDelta = new Vector2(-UiInfoPlateMetrics.PadX * 2f, titleH);

            var bodyRt = _tooltipBody.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -(UiInfoPlateMetrics.PadY + titleH + gap));
            bodyRt.sizeDelta = new Vector2(-UiInfoPlateMetrics.PadX * 2f, bodyH);

            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling();
        }

        void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.SetActive(false);
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

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }

        void ApplyPortraitZone(string characterId)
        {
            if (_portraitImage == null)
                return;

            var zone = ZonePortrait;
            if (characterId == TalentCatalog.RangerId)
            {
                zone = new Vector4(
                    zone.x + PortraitExtraShiftDemon,
                    zone.y,
                    zone.z + PortraitExtraShiftDemon,
                    zone.w);
            }

            SetZone(_portraitImage.rectTransform, zone);
        }

        static void SetZone(RectTransform rt, Vector4 zone) =>
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);

        static void SetZoneCoverAspect(RectTransform rt, Vector4 zone, float aspect)
        {
            var cx = (zone.x + zone.z) * 0.5f;
            var cy = (zone.y + zone.w) * 0.5f;
            var rw = (zone.z - zone.x) * TemplateW;
            var rh = (zone.w - zone.y) * TemplateH;
            var bw = rw;
            var bh = bw / aspect;
            if (bh < rh)
            {
                bh = rh;
                bw = bh * aspect;
            }

            var nw = bw / TemplateW;
            var nh = bh / TemplateH;
            CampUiRuntime.SetAnchored(rt, cx - nw * 0.5f, cy - nh * 0.5f, cx + nw * 0.5f, cy + nh * 0.5f);
        }

        /// <summary>保持宽高比缩进热区内（不撑破），用于返回钮刚好盖住模板按钮。</summary>
        static void SetZoneFitAspect(RectTransform rt, Vector4 zone, float aspect)
        {
            var cx = (zone.x + zone.z) * 0.5f;
            var cy = (zone.y + zone.w) * 0.5f;
            var rw = (zone.z - zone.x) * TemplateW;
            var rh = (zone.w - zone.y) * TemplateH;
            var bw = rw;
            var bh = bw / aspect;
            if (bh > rh)
            {
                bh = rh;
                bw = bh * aspect;
            }

            var nw = bw / TemplateW;
            var nh = bh / TemplateH;
            CampUiRuntime.SetAnchored(rt, cx - nw * 0.5f, cy - nh * 0.5f, cx + nw * 0.5f, cy + nh * 0.5f);
        }

        static void SetRowBand(RectTransform rt, Vector4 colZone, float t0, float t1)
        {
            var y0 = Lerp(colZone.y, colZone.w, t0);
            var y1 = Lerp(colZone.y, colZone.w, t1);
            CampUiRuntime.SetAnchored(rt, colZone.x, y0, colZone.z, y1);
        }

        /// <summary>按模板圆槽虚影固定边长，居中到该行。</summary>
        static void SetSquareInColumn(RectTransform rt, Vector4 colZone, float t0, float t1)
        {
            var y0 = Lerp(colZone.y, colZone.w, t0);
            var y1 = Lerp(colZone.y, colZone.w, t1);
            var cy = (y0 + y1) * 0.5f;
            var cx = (colZone.x + colZone.z) * 0.5f;
            var nw = RuneSizePx / TemplateW;
            var nh = RuneSizePx / TemplateH;
            CampUiRuntime.SetAnchored(rt, cx - nw * 0.5f, cy - nh * 0.5f, cx + nw * 0.5f, cy + nh * 0.5f);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>天赋行悬停转发（避免与 Button 抢事件）。</summary>
    sealed class TalentRowHoverRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        Action _enter;
        Action _exit;

        public void Bind(Action enter, Action exit)
        {
            _enter = enter;
            _exit = exit;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => _enter?.Invoke();

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => _exit?.Invoke();
    }
}
