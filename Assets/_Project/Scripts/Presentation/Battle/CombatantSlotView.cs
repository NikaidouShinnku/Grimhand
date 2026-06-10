using System;
using System.Collections.Generic;
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
    public sealed class CombatantSlotView : MonoBehaviour
    {
        static readonly Color ValidTargetTintEnemy = new(1.18f, 1.02f, 0.48f, 1f);
        static readonly Color ValidTargetTintAlly = new(0.62f, 0.98f, 1.18f, 1f);
        static readonly Color ValidTargetHoverMul = new(1.1f, 1.1f, 1.1f, 1f);
        static readonly Color HoverTint = new(1.08f, 1.08f, 1.08f, 1f);
        static readonly Color DeadTint = new(0.35f, 0.35f, 0.35f, 1f);

        const float PlayerPortraitScale = 2.28f;
        const float EnemyPortraitScale = 1.28f;
        const float BossEnemyPortraitScale = 2.35f;
        /// <summary>玩家立绘脚线（槽内比例）。</summary>
        const float PlayerFeetLine = 0.02f;
        const float EnemyFeetLine = 0.13f;
        /// <summary>Boss 敌人与玩家共用地面线，保证血条水平对齐。</summary>
        const float BossEnemyFeetLine = PlayerFeetLine;
        /// <summary>在脚线锚点基础上，玩家立绘额外下移（Canvas 本地像素）。</summary>
        const float PlayerPortraitExtraDownPx = -52f;
        const float BossEnemyPortraitExtraDownPx = PlayerPortraitExtraDownPx;
        const float PortraitTop = 0.82f;
        const float PlayerStatusDropPx = 10f;
        const float EnemyStatusDropPx = 10f;
        const float HoverScaleMul = 1.07f;
        const float TargetScaleMul = 1.05f;
        const float HitboxPadding = 2f;

        [SerializeField] Image background;
        [SerializeField] Image targetHighlight;
        [SerializeField] RectTransform portraitRoot;
        [SerializeField] Image portraitImage;
        [SerializeField] Text slotLabel;
        [SerializeField] Text bodyText;
        [SerializeField] Text nameText;
        [SerializeField] UnitStatsRowView statsRow;
        [SerializeField] Button selectButton;
        [SerializeField] TeamSide team;
        [SerializeField] FormationSlot formationSlot;
        [SerializeField] bool mirrorPortrait;

        RectTransform _portraitHit;
        Outline _targetOutline;
        CombatantDetailPopupView _detailPopup;
        Action<CombatantState> _hoverPreviewEnter;
        Action _hoverPreviewExit;
        CombatantPortraitView _portraitView;
        CombatantState _currentUnit;
        BattleUiIconCatalogSO _currentIcons;
        CharacterVisualCatalogSO _currentVisuals;
        BattleSession _session;

        string _combatantId;
        bool _hovered;
        bool _targetMode;
        bool _isValidTarget;
        bool _displayAlive = true;
        bool _showExpBar;
        Vector3 _basePortraitScale = Vector3.one;

        public void Configure(FormationSlot slot, TeamSide teamSide, string rowLabel, bool mirror = false)
        {
            formationSlot = slot;
            team = teamSide;
            mirrorPortrait = mirror;
            ApplyPortraitMirror();
            ApplyDrawOrder();
            ApplyStatusAnchorLayout();
            if (slotLabel != null)
                slotLabel.gameObject.SetActive(false);
        }

        void Awake()
        {
            if (formationSlot == 0)
                TryInferSlotFromName();
            ApplyStatusAnchorLayout();
            ApplyPortraitMirror();
            EnsurePortraitInteraction();
            EnsurePortraitView();
            EnsureDetailPopup();
            EnsureStatusTextStyle();
            ApplyDrawOrder();
        }

        void LateUpdate()
        {
            if (_portraitView == null || _currentUnit == null)
                return;

            if (_portraitView.IsAnimating || _portraitView.IsAwayFromHome)
                AlignStatusBelowPortrait();
        }

        void TryInferSlotFromName()
        {
            var n = gameObject.name;
            if (n.Contains("Front")) formationSlot = FormationSlot.Front;
            else if (n.Contains("Middle")) formationSlot = FormationSlot.Middle;
            else if (n.Contains("Back")) formationSlot = FormationSlot.Back;

            var parent = transform.parent;
            if (parent != null)
            {
                if (parent.name.Contains("Enemy")) team = TeamSide.Enemy;
                else if (parent.name.Contains("Player")) team = TeamSide.Player;
            }

            mirrorPortrait = team == TeamSide.Enemy;
        }

        void EnsurePortraitInteraction()
        {
            if (portraitRoot == null)
                portraitRoot = transform.Find("PortraitRoot") as RectTransform;
            if (portraitImage == null && portraitRoot != null)
                portraitImage = portraitRoot.Find("Portrait")?.GetComponent<Image>();

            if (targetHighlight == null && portraitRoot != null)
                targetHighlight = portraitRoot.Find("TargetHighlight")?.GetComponent<Image>();

            if (portraitRoot == null || portraitImage == null)
                return;

            portraitImage.raycastTarget = false;

            _targetOutline = portraitImage.GetComponent<Outline>();
            if (_targetOutline == null)
                _targetOutline = portraitImage.gameObject.AddComponent<Outline>();
            _targetOutline.effectColor = new Color(1f, 0.75f, 0.1f, 1f);
            _targetOutline.effectDistance = new Vector2(4f, -4f);
            _targetOutline.useGraphicAlpha = true;
            _targetOutline.enabled = false;

            if (targetHighlight != null)
            {
                targetHighlight.gameObject.SetActive(false);
                targetHighlight.raycastTarget = false;
            }

            _portraitHit = portraitRoot.Find("PortraitHit") as RectTransform;
            if (_portraitHit == null)
            {
                var hitGo = new GameObject("PortraitHit", typeof(RectTransform), typeof(Image));
                hitGo.transform.SetParent(portraitRoot, false);
                _portraitHit = hitGo.GetComponent<RectTransform>();
            }

            var hit = _portraitHit.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var slotButton = GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.interactable = false;
                slotButton.enabled = false;
                Destroy(slotButton);
            }

            if (background != null)
                background.raycastTarget = false;

            selectButton = hit.GetComponent<Button>();
            if (selectButton == null)
                selectButton = hit.gameObject.AddComponent<Button>();

            selectButton.targetGraphic = hit;
            selectButton.transition = Selectable.Transition.None;

            EnsureHoverEvents(hit.gameObject);
        }

        void EnsureHoverEvents(GameObject hitObject)
        {
            var trigger = hitObject.GetComponent<EventTrigger>() ?? hitObject.AddComponent<EventTrigger>();

            trigger.triggers.RemoveAll(entry =>
                entry.eventID == EventTriggerType.PointerEnter ||
                entry.eventID == EventTriggerType.PointerExit);

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnPortraitPointerEnter());
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnPortraitPointerExit());
            trigger.triggers.Add(exit);
        }

        void EnsurePortraitView()
        {
            if (_portraitView == null)
                _portraitView = GetComponent<CombatantPortraitView>() ?? gameObject.AddComponent<CombatantPortraitView>();

            if (portraitRoot == null)
                portraitRoot = transform.Find("PortraitRoot") as RectTransform;
            if (portraitImage == null && portraitRoot != null)
                portraitImage = portraitRoot.Find("Portrait")?.GetComponent<Image>();

            _portraitView.Bind(null, portraitImage, portraitRoot, transform as RectTransform, team);
        }

        public CombatantPortraitView PortraitView => _portraitView;

        void EnsureDetailPopup()
        {
            if (_detailPopup != null)
                return;

            _detailPopup = GetComponent<CombatantDetailPopupView>();
            if (_detailPopup == null)
                _detailPopup = gameObject.AddComponent<CombatantDetailPopupView>();

            _detailPopup.EnsureBuilt(transform, team);
        }

        void EnsureStatusTextStyle()
        {
            if (bodyText != null)
                bodyText.gameObject.SetActive(false);

            if (nameText != null)
            {
                nameText.fontSize = Mathf.Max(nameText.fontSize, 18);
                EnsureOutline(nameText, new Color(0f, 0f, 0f, 0.9f));
            }
        }

        static void EnsureOutline(Text text, Color effectColor)
        {
            if (text.GetComponent<Outline>() != null)
                return;

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = effectColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        bool IsBossEnemyPortrait()
        {
            if (_currentUnit == null)
                return false;

            var id = _currentUnit.CharacterDefinitionId;
            return id == "char_skeleton_king" || id == GhostQueenBossEncounterBuilder.CharacterId;
        }

        float ResolveFeetLine()
        {
            if (team == TeamSide.Player)
                return PlayerFeetLine;

            return IsBossEnemyPortrait() ? BossEnemyFeetLine : EnemyFeetLine;
        }

        float ResolvePortraitExtraDownPx()
        {
            if (team == TeamSide.Player)
                return PlayerPortraitExtraDownPx;

            return IsBossEnemyPortrait() ? BossEnemyPortraitExtraDownPx : 0f;
        }

        float ResolvePortraitScale()
        {
            if (team == TeamSide.Player)
                return PlayerPortraitScale;

            if (IsBossEnemyPortrait())
                return BossEnemyPortraitScale;

            return EnemyPortraitScale;
        }

        bool ResolveMirrorPortrait()
        {
            if (_currentUnit != null
                && _currentVisuals != null
                && _currentVisuals.GetPreserveOriginalFacing(_currentUnit.CharacterDefinitionId))
                return false;

            return mirrorPortrait;
        }

        void ApplyPortraitMirror()
        {
            if (portraitRoot == null)
                return;

            var scale = ResolvePortraitScale();
            var mirror = ResolveMirrorPortrait();
            _basePortraitScale = mirror
                ? new Vector3(-scale, scale, 1f)
                : new Vector3(scale, scale, 1f);

            ApplyPortraitScale();
        }

        void ApplyPortraitScale()
        {
            if (portraitRoot == null)
                return;

            var mul = 1f;
            if (_targetMode && _isValidTarget)
                mul = TargetScaleMul;
            if (_hovered)
                mul *= HoverScaleMul;

            portraitRoot.localScale = new Vector3(
                _basePortraitScale.x * mul,
                _basePortraitScale.y * mul,
                1f);
        }

        void ApplyDrawOrder()
        {
            var order = formationSlot switch
            {
                FormationSlot.Front => 2,
                FormationSlot.Middle => 1,
                _ => 0
            };
            transform.SetSiblingIndex(order);
        }

        public void ApplyPortraitScaleFromRuntime()
        {
            ApplyStatusAnchorLayout();
            ApplyPortraitMirror();
            _portraitView?.RecaptureHomeIfIdle();
        }

        public void ApplyStatusAnchorLayout()
        {
            if (portraitRoot == null)
                portraitRoot = transform.Find("PortraitRoot") as RectTransform;

            if (portraitRoot != null)
            {
                var feetLine = ResolveFeetLine();
                portraitRoot.localScale = Vector3.one;
                portraitRoot.anchorMin = new Vector2(0.04f, feetLine);
                portraitRoot.anchorMax = new Vector2(0.96f, PortraitTop);
                portraitRoot.pivot = new Vector2(0.5f, 0f);
                portraitRoot.offsetMin = Vector2.zero;
                portraitRoot.offsetMax = Vector2.zero;
                portraitRoot.anchoredPosition = new Vector2(0f, ResolvePortraitExtraDownPx());
            }

            var footRoot = transform.Find("FootStatusRoot") as RectTransform;
            if (footRoot == null)
            {
                var footGo = new GameObject("FootStatusRoot", typeof(RectTransform));
                footGo.transform.SetParent(transform, false);
                footRoot = footGo.GetComponent<RectTransform>();
            }

            footRoot.pivot = new Vector2(0.5f, 0f);
            footRoot.sizeDelta = new Vector2(160f, 56f);

            if (nameText != null)
            {
                var rt = nameText.rectTransform;
                rt.SetParent(footRoot, false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 28f);
                rt.sizeDelta = new Vector2(0f, 18f);
            }

            if (statsRow != null)
            {
                var rt = statsRow.transform as RectTransform;
                if (rt != null)
                {
                    rt.SetParent(footRoot, false);
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(148f, 32f);
                }
            }
        }

        public Vector3 GetFeetWorldPosition()
        {
            if (_portraitHit != null)
            {
                var corners = new Vector3[4];
                _portraitHit.GetWorldCorners(corners);
                return new Vector3(
                    (corners[0].x + corners[2].x) * 0.5f,
                    corners[0].y,
                    corners[0].z);
            }

            if (portraitRoot != null)
            {
                var corners = new Vector3[4];
                portraitRoot.GetWorldCorners(corners);
                return new Vector3(
                    (corners[0].x + corners[2].x) * 0.5f,
                    corners[0].y,
                    corners[0].z);
            }

            return transform.position;
        }

        public Vector3 GetDuelReferenceWorldPosition() => GetFeetWorldPosition();

        public void SetHoverPreviewCallbacks(Action<CombatantState> onEnter, Action onExit)
        {
            _hoverPreviewEnter = onEnter;
            _hoverPreviewExit = onExit;
        }

        public void SetSelectHandler(System.Action<string> onSelect)
        {
            if (selectButton == null)
                return;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(_combatantId) || !_targetMode || !_isValidTarget)
                    return;

                onSelect?.Invoke(_combatantId);
            });
        }

        public void DismissHoverDetail()
        {
            if (!_hovered)
            {
                _detailPopup?.SetVisible(false);
                return;
            }

            OnPortraitPointerExit();
        }

        public void Refresh(
            BattleState state,
            bool targetMode,
            System.Collections.Generic.IReadOnlyList<CombatantState> validTargets,
            CharacterVisualCatalogSO visuals,
            BattleUiIconCatalogSO uiIcons,
            PresentationSnapshot presentation = null,
            bool showExpBar = false,
            BattleSession session = null)
        {
            var unit = FindCombatant(state);
            _currentUnit = unit;
            _currentIcons = uiIcons;
            _currentVisuals = visuals;
            _session = session;
            _combatantId = unit?.Id;
            _targetMode = targetMode;

            var allowHoverDetail = session == null
                                   || !session.IsExpeditionMode
                                   || session.Expedition.Run.Phase == ExpeditionPhase.InBattle;
            if (!allowHoverDetail)
                DismissHoverDetail();

            ApplyStatusAnchorLayout();
            ApplyPortraitMirror();

            var displayAlive = unit != null
                && (presentation != null ? presentation.IsAlive(unit.Id) : unit.IsAlive);
            _displayAlive = displayAlive;
            int? hpOverride = null;
            int? maxHpOverride = null;
            int? blockOverride = null;
            if (presentation != null && unit != null)
            {
                hpOverride = presentation.GetHp(unit.Id);
                maxHpOverride = presentation.GetMaxHp(unit.Id);
                blockOverride = presentation.GetBlock(unit.Id);
            }

            _isValidTarget = false;
            if (targetMode && unit != null && validTargets != null)
            {
                foreach (var t in validTargets)
                {
                    if (t.Id == unit.Id)
                    {
                        _isValidTarget = true;
                        break;
                    }
                }
            }

            if (background != null)
                background.color = new Color(1f, 1f, 1f, 0f);

            Sprite sprite = null;
            if (portraitImage != null)
            {
                if (unit == null)
                {
                    portraitImage.enabled = false;
                    portraitImage.sprite = null;
                    _portraitView?.StopIdleLoop();
                }
                else
                {
                    _portraitView?.Bind(visuals, portraitImage, portraitRoot, transform as RectTransform, team);
                    _portraitView?.SetIdentity(unit.Id, unit.CharacterDefinitionId, displayAlive, team);
                    if (_portraitView == null || (!_portraitView.IsIdleLoopActive && !_portraitView.IsAwayFromHome))
                        _portraitView?.RecaptureHomeIfIdle();

                    var preservePortraitSprite = _portraitView != null
                        && (_portraitView.IsAnimating || _portraitView.IsAwayFromHome || _portraitView.IsIdleLoopActive || _portraitView.IsDeadDisplay);

                    if (!preservePortraitSprite)
                    {
                        sprite = visuals != null
                            ? visuals.GetPortrait(unit.CharacterDefinitionId)
                            : null;
                        portraitImage.sprite = sprite;
                        portraitImage.preserveAspect = true;
                        portraitImage.enabled = sprite != null;
                    }

                    ApplyPortraitColor(unit);
                }
            }

            ApplyTargetVisuals();

            ApplyInteractionBounds(
                _portraitView != null && _portraitView.IsIdleLoopActive && unit != null
                    ? visuals?.GetPortrait(unit.CharacterDefinitionId)
                    : sprite ?? portraitImage?.sprite);

            AlignStatusBelowPortrait();

            if (bodyText != null)
            {
                bodyText.text = "";
                bodyText.gameObject.SetActive(false);
            }

            if (nameText != null)
                nameText.text = unit == null ? "" : unit.DisplayName;

            if (statsRow == null)
                statsRow = GetComponentInChildren<UnitStatsRowView>(true);
            if (statsRow != null)
            {
                statsRow.gameObject.SetActive(unit != null);
                if (unit != null)
                    statsRow.Refresh(unit, uiIcons, hpOnly: true, hpOverride, maxHpOverride, blockOverride);
            }
            _portraitView?.SetDamageFloaterBelow(statsRow != null ? statsRow.transform as RectTransform : null);

            _showExpBar = showExpBar;

            if (selectButton != null)
            {
                var hasUnit = unit != null;
                selectButton.gameObject.SetActive(hasUnit);
                selectButton.interactable = hasUnit && (!_targetMode || _isValidTarget);
            }

            var xp = unit?.Xp ?? 0;
            ResolveExpeditionDetailContextFromSession(session, unit, out var expeditionMember, out var runRelics);

            if (!allowHoverDetail)
            {
                _detailPopup?.Refresh(unit, uiIcons, showExpBar, xp, expeditionMember, runRelics);
                _detailPopup?.SetVisible(false);
            }
            else if (!_hovered)
            {
                _detailPopup?.Refresh(unit, uiIcons, showExpBar, xp, expeditionMember, runRelics);
                _detailPopup?.SetVisible(false);
            }
            else if (!_targetMode || !_isValidTarget)
            {
                _detailPopup?.Refresh(unit, uiIcons, showExpBar, xp, expeditionMember, runRelics);
                _detailPopup?.SetVisible(unit != null);
            }
            else
            {
                _detailPopup?.SetVisible(false);
            }

            SyncHoverWithPointer();
        }

        void SyncHoverWithPointer()
        {
            if (!_hovered || _portraitHit == null)
                return;

            if (UiPointerUtility.IsOverRectTransform(_portraitHit, UiPointerUtility.GetEventCamera(_portraitHit)))
                return;

            OnPortraitPointerExit();
        }

        void AlignStatusBelowPortrait()
        {
            var footRoot = transform.Find("FootStatusRoot") as RectTransform;
            if (footRoot == null || _portraitHit == null)
                return;

            var corners = new Vector3[4];
            _portraitHit.GetWorldCorners(corners);
            var footWorld = new Vector3(
                (corners[0].x + corners[2].x) * 0.5f,
                corners[0].y,
                corners[0].z);

            var slotRt = transform as RectTransform;
            if (slotRt == null)
                return;

            var canvas = slotRt.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    slotRt, footWorld, cam, out var localFoot))
                return;

            var statusDrop = team == TeamSide.Player ? PlayerStatusDropPx : EnemyStatusDropPx;
            localFoot.y -= statusDrop;

            footRoot.anchorMin = new Vector2(0.5f, 0.5f);
            footRoot.anchorMax = new Vector2(0.5f, 0.5f);
            footRoot.pivot = new Vector2(0.5f, 0f);
            footRoot.anchoredPosition = localFoot;
        }

        void ApplyInteractionBounds(Sprite sprite)
        {
            if (portraitRoot == null || _portraitHit == null)
                return;

            UiSpriteBounds.FitCentered(portraitRoot, _portraitHit, sprite, HitboxPadding);
        }

        void ApplyTargetVisuals()
        {
            var showTarget = _targetMode && _isValidTarget && _displayAlive;

            if (targetHighlight != null)
                targetHighlight.gameObject.SetActive(false);

            if (_targetOutline != null)
            {
                _targetOutline.enabled = showTarget;
                if (showTarget)
                {
                    _targetOutline.effectColor = team == TeamSide.Player
                        ? new Color(0.2f, 0.95f, 1f, 1f)
                        : new Color(1f, 0.78f, 0.08f, 1f);
                    _targetOutline.effectDistance = new Vector2(5f, -5f);
                }
            }

            ApplyPortraitScale();
            ApplyPortraitColor(_currentUnit);
        }

        void ApplyPortraitColor(CombatantState unit)
        {
            if (portraitImage == null)
                return;

            if (unit == null)
                return;

            if (!_displayAlive)
                portraitImage.color = DeadTint;
            else if (_targetMode && _isValidTarget)
            {
                var tint = team == TeamSide.Player ? ValidTargetTintAlly : ValidTargetTintEnemy;
                portraitImage.color = _hovered ? tint * ValidTargetHoverMul : tint;
            }
            else if (_hovered)
                portraitImage.color = HoverTint;
            else
                portraitImage.color = Color.white;
        }

        void OnPortraitPointerEnter()
        {
            if (_currentUnit == null || !_displayAlive)
                return;

            _hovered = true;
            ApplyTargetVisuals();
            if (_targetMode && _isValidTarget && _currentUnit != null)
                _hoverPreviewEnter?.Invoke(_currentUnit);
            else if (!_targetMode || !_isValidTarget)
            {
                ResolveExpeditionDetailContext(out var member, out var relics);
                _detailPopup?.Refresh(_currentUnit, _currentIcons, _showExpBar, _currentUnit.Xp, member, relics);
                _detailPopup?.SetVisible(true);
            }
        }

        void ResolveExpeditionDetailContext(out PartyMemberSnapshot member, out IReadOnlyList<string> relics) =>
            ResolveExpeditionDetailContextFromSession(_session, _currentUnit, out member, out relics);

        static void ResolveExpeditionDetailContextFromSession(
            BattleSession session,
            CombatantState unit,
            out PartyMemberSnapshot member,
            out IReadOnlyList<string> relics)
        {
            member = null;
            relics = null;
            if (session?.IsExpeditionMode != true || unit == null || unit.Team != TeamSide.Player)
                return;

            relics = session.Expedition.Run.Relics;
            foreach (var partyMember in session.Expedition.Run.Party)
            {
                if (partyMember.CharacterDefinitionId == unit.CharacterDefinitionId)
                {
                    member = partyMember;
                    break;
                }
            }
        }

        void OnPortraitPointerExit()
        {
            if (!_hovered)
                return;

            _hovered = false;
            ApplyTargetVisuals();
            if (_targetMode && _isValidTarget)
                _hoverPreviewExit?.Invoke();
            _detailPopup?.SetVisible(false);
        }

        CombatantState FindCombatant(BattleState state)
        {
            CombatantState dead = null;
            foreach (var c in state.Combatants)
            {
                if (c.Team != team || c.Slot != formationSlot)
                    continue;

                if (c.IsAlive)
                    return c;

                dead = c;
            }

            return dead;
        }
    }
}
