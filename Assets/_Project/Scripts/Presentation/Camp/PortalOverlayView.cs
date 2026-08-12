using System;
using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 传送门 / 开启远征：模板底图 + 三槽出征角色 + 难度说明 + 返回 / 开始远征。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 7;
        const float ButtonHoverScale = 1.06f;

        // 模板归一化（原点左下）1672×941
        static readonly Vector4[] ZoneSlots =
        {
            new(0.226f, 0.355f, 0.416f, 0.720f), // 后排：微左
            new(0.398f, 0.355f, 0.588f, 0.720f), // 中排
            new(0.580f, 0.355f, 0.770f, 0.720f)  // 前排：微右
        };
        static readonly Vector4 ZoneDifficulty = new(0.220f, 0.175f, 0.780f, 0.305f);
        static readonly Vector4 ZoneBack = new(0.055f, 0.035f, 0.215f, 0.125f);
        // 开始远征：略左移并略放大
        static readonly Vector4 ZoneStart = new(0.748f, 0.028f, 0.938f, 0.138f);

        static readonly Color BodyText = new(0.88f, 0.90f, 0.95f, 1f);
        static readonly Color MuteText = new(0.70f, 0.74f, 0.82f, 1f);
        static readonly Color GoldText = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color ButtonLabel = new(0.96f, 0.92f, 0.78f, 1f);
        static readonly Color ReadyGreen = new(0.70f, 0.95f, 0.72f, 1f);
        static readonly Color WarnOrange = new(0.95f, 0.75f, 0.55f, 1f);

        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        CampRosterState _roster;
        CampMetaState _meta;
        Action _onConfirm;
        Action _onClose;

        RectTransform _overlayRoot;
        Image _bgImage;
        Button _startButton;
        readonly List<GameObject> _slotHosts = new();
        readonly List<GameObject> _dynamicObjects = new();
        const int FixedStartLayer = 1;
        bool _built;
        int _builtVersion = -1;

        public int SelectedStartLayer => FixedStartLayer;

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            Hide();
            _onClose?.Invoke();
            return true;
        }

        public void Initialize(
            BattleSetupSO battleSetup,
            CharacterVisualCatalogSO characterVisuals,
            Action onConfirm,
            Action onClose)
        {
            Initialize(characterVisuals, null, onConfirm, onClose);
        }

        public void Initialize(
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Action onConfirm,
            Action onClose)
        {
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _onConfirm = onConfirm;
            _onClose = onClose;
            EnsureBuilt();
        }

        public void Show(CampRosterState roster)
        {
            Show(roster, null);
        }

        public void Show(CampRosterState roster, CampMetaState meta)
        {
            _roster = roster;
            _meta = meta;
            EnsureBuilt();
            _overlayRoot.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _overlayRoot.SetAsLastSibling();
            RebuildPartySlots();
        }

        public void Hide()
        {
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
            _slotHosts.Clear();
            _dynamicObjects.Clear();

            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _overlayRoot = CampUiRuntime.CreateRect("PortalOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            _bgImage = CampUiRuntime.CreateImage("Background", _overlayRoot, Color.white);
            CampUiRuntime.StretchFull(_bgImage.rectTransform);
            _bgImage.preserveAspect = false;
            _bgImage.raycastTarget = true;
            var bgSprite = _uiIcons != null ? _uiIcons.UiExpeditionStartBackground : null;
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
                Debug.LogWarning("[Portal] 缺少 UiExpeditionStartBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            for (var i = 0; i < ZoneSlots.Length; i++)
            {
                var host = CampUiRuntime.CreateRect($"Slot_{i}", _overlayRoot);
                SetZone(host.GetComponent<RectTransform>(), ZoneSlots[i]);
                _slotHosts.Add(host);
            }

            BuildDifficultySelector();
            BuildBackButton();
            BuildStartButton();
        }

        void BuildDifficultySelector()
        {
            var go = CampUiRuntime.CreateRect("Difficulty", _overlayRoot);
            SetZone(go.GetComponent<RectTransform>(), ZoneDifficulty);

            var label = CampUiRuntime.CreateText(
                go.transform,
                "难度1：无调整",
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.color = GoldText;
            label.raycastTarget = false;
        }

        void BuildBackButton()
        {
            var go = CampUiRuntime.CreateRect("Back", _overlayRoot);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneBack);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton6 != null)
                img.sprite = _uiIcons.UiButton6;
            else
                img.color = new Color(0.28f, 0.3f, 0.36f, 1f);

            var label = CampUiRuntime.CreateText(go.transform, "返回", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
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

        void BuildStartButton()
        {
            var go = CampUiRuntime.CreateRect("Start", _overlayRoot);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneStart);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton6 != null)
                img.sprite = _uiIcons.UiButton6;
            else
                img.color = new Color(0.55f, 0.38f, 0.12f, 1f);

            var label = CampUiRuntime.CreateText(go.transform, "开始远征", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
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

            _startButton = go.AddComponent<Button>();
            _startButton.targetGraphic = img;
            _startButton.transition = Selectable.Transition.None;
            _startButton.onClick.AddListener(() =>
            {
                if (_roster == null || !_roster.IsReadyForExpedition)
                    return;

                Hide();
                _onConfirm?.Invoke();
            });
            UiAudioHooks.WireButton(_startButton);
        }

        void RebuildPartySlots()
        {
            ClearDynamic();

            for (var vi = 0; vi < _slotHosts.Count; vi++)
            {
                var host = _slotHosts[vi];
                if (host == null)
                    continue;

                if (_roster == null
                    || vi >= CampFormationDisplay.VisualOrderMemberIndices.Length
                    || CampFormationDisplay.VisualOrderMemberIndices[vi] >= _roster.Members.Count)
                {
                    BuildEmptySlot(host.transform, CampFormationDisplay.SlotLabel(
                        CampFormationDisplay.VisualOrderMemberIndices[
                            Mathf.Min(vi, CampFormationDisplay.VisualOrderMemberIndices.Length - 1)]));
                    continue;
                }

                var memberIndex = CampFormationDisplay.VisualOrderMemberIndices[vi];
                var member = _roster.Members[memberIndex];
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                {
                    BuildEmptySlot(host.transform, CampFormationDisplay.SlotLabel(memberIndex));
                    continue;
                }

                BuildMemberSlot(host.transform, member, memberIndex);
            }

            if (_startButton != null)
                _startButton.interactable = _roster != null && _roster.IsReadyForExpedition;
        }

        void BuildEmptySlot(Transform host, string slotLabel)
        {
            // 未配置：不叠 plate / 白块，只留模板空框 + 提示字
            var hint = CampUiRuntime.CreateText(host, $"{slotLabel}\n未配置", 20, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(hint.rectTransform);
            hint.color = MuteText;
            hint.raycastTarget = false;
            _dynamicObjects.Add(hint.gameObject);
        }

        void BuildMemberSlot(Transform host, CampMemberLoadout member, int memberIndex)
        {
            var portrait = CampUiRuntime.CreateImage("Portrait", host, Color.white);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            CampUiRuntime.SetAnchored(portrait.rectTransform, 0.12f, 0.38f, 0.88f, 0.92f);
            portrait.sprite = _characterVisuals != null
                ? _characterVisuals.GetPortrait(member.CharacterDefinitionId)
                : null;
            var animator = portrait.gameObject.AddComponent<CampIdlePortraitAnimator>();
            animator.Bind(portrait, _characterVisuals, member.CharacterDefinitionId);
            _dynamicObjects.Add(portrait.gameObject);

            var displayName = string.IsNullOrEmpty(member.DisplayName)
                ? CharacterDisplayNames.GetOrFallback(member.CharacterDefinitionId, member.CharacterDefinitionId)
                : member.DisplayName;

            var name = CampUiRuntime.CreateText(host, displayName, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(name.rectTransform, 0.06f, 0.26f, 0.94f, 0.36f);
            name.color = GoldText;
            name.raycastTarget = false;
            _dynamicObjects.Add(name.gameObject);

            var level = 1;
            if (_meta != null && !string.IsNullOrEmpty(member.CharacterDefinitionId))
            {
                var progress = _meta.GetOrCreate(member.CharacterDefinitionId);
                MetaProgressionRules.NormalizeProgress(progress);
                level = progress.OutOfRunLevel;
            }

            var levelText = CampUiRuntime.CreateText(host, $"Lv.{level}", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(levelText.rectTransform, 0.06f, 0.18f, 0.94f, 0.26f);
            levelText.color = BodyText;
            levelText.raycastTarget = false;
            _dynamicObjects.Add(levelText.gameObject);

            var deckCount = 0;
            foreach (var id in member.DeckCardIds)
            {
                if (!string.IsNullOrEmpty(id))
                    deckCount++;
            }

            var deck = CampUiRuntime.CreateText(
                host,
                $"卡组 {deckCount}/{CampRosterState.DeckSize}",
                15,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(deck.rectTransform, 0.06f, 0.08f, 0.94f, 0.18f);
            deck.color = deckCount == CampRosterState.DeckSize ? ReadyGreen : WarnOrange;
            deck.raycastTarget = false;
            _dynamicObjects.Add(deck.gameObject);

            var slot = CampUiRuntime.CreateText(
                host,
                CampFormationDisplay.SlotLabel(memberIndex),
                14,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(slot.rectTransform, 0.06f, 0.01f, 0.94f, 0.08f);
            slot.color = MuteText;
            slot.raycastTarget = false;
            _dynamicObjects.Add(slot.gameObject);
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

        static void SetZone(RectTransform rt, Vector4 zone) =>
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);
    }
}
