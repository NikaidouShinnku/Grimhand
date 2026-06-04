using Grimhand.Content;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CombatantSlotView : MonoBehaviour
    {
        static readonly Color ValidTargetTint = new(1f, 0.92f, 0.45f, 1f);
        static readonly Color HoverTint = new(1.08f, 1.08f, 1.08f, 1f);
        static readonly Color DeadTint = new(0.35f, 0.35f, 0.35f, 1f);

        const float PlayerPortraitScale = 1.06f;
        const float EnemyPortraitScale = 0.88f;
        const float HoverScaleMul = 1.07f;
        const float HitboxPadding = 6f;

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
        CombatantPortraitView _portraitView;
        CombatantState _currentUnit;
        BattleUiIconCatalogSO _currentIcons;

        string _combatantId;
        bool _hovered;
        bool _targetMode;
        bool _isValidTarget;
        bool _displayAlive = true;
        Vector3 _basePortraitScale = Vector3.one;

        public void Configure(FormationSlot slot, TeamSide teamSide, string rowLabel, bool mirror = false)
        {
            formationSlot = slot;
            team = teamSide;
            mirrorPortrait = mirror;
            ApplyPortraitMirror();
            ApplyDrawOrder();
            if (slotLabel != null)
                slotLabel.gameObject.SetActive(false);
        }

        void Awake()
        {
            if (formationSlot == 0)
                TryInferSlotFromName();
            ApplyPortraitMirror();
            EnsurePortraitInteraction();
            EnsurePortraitView();
            EnsureDetailPopup();
            EnsureStatusTextStyle();
            ApplyDrawOrder();
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

            if (portraitRoot == null || portraitImage == null)
                return;

            portraitImage.raycastTarget = false;

            _targetOutline = portraitImage.GetComponent<Outline>();
            if (_targetOutline == null)
                _targetOutline = portraitImage.gameObject.AddComponent<Outline>();
            _targetOutline.effectColor = new Color(1f, 0.88f, 0.2f, 0.95f);
            _targetOutline.effectDistance = new Vector2(2.5f, -2.5f);
            _targetOutline.useGraphicAlpha = true;
            _targetOutline.enabled = false;

            if (targetHighlight != null)
                targetHighlight.gameObject.SetActive(false);

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

        void ApplyPortraitMirror()
        {
            if (portraitRoot == null)
                return;

            var scale = team == TeamSide.Player ? PlayerPortraitScale : EnemyPortraitScale;
            _basePortraitScale = mirrorPortrait
                ? new Vector3(-scale, scale, 1f)
                : new Vector3(scale, scale, 1f);

            ApplyPortraitScale();
        }

        void ApplyPortraitScale()
        {
            if (portraitRoot == null)
                return;

            var mul = _hovered ? HoverScaleMul : 1f;
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
            ApplyPortraitMirror();
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

        public void Refresh(
            BattleState state,
            bool targetMode,
            System.Collections.Generic.IReadOnlyList<CombatantState> validTargets,
            CharacterVisualCatalogSO visuals,
            BattleUiIconCatalogSO uiIcons,
            PresentationSnapshot presentation = null)
        {
            var unit = FindCombatant(state);
            _currentUnit = unit;
            _currentIcons = uiIcons;
            _combatantId = unit?.Id;
            _targetMode = targetMode;

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

            ApplyInteractionBounds(
                _portraitView != null && _portraitView.IsIdleLoopActive && unit != null
                    ? visuals?.GetPortrait(unit.CharacterDefinitionId)
                    : sprite ?? portraitImage?.sprite);

            if (bodyText != null)
            {
                bodyText.text = "";
                bodyText.gameObject.SetActive(false);
            }

            if (nameText != null)
                nameText.text = unit == null ? "" : unit.DisplayName;

            if (statsRow == null)
                statsRow = GetComponentInChildren<UnitStatsRowView>(true);
            statsRow?.Refresh(unit, uiIcons, hpOnly: true, hpOverride, maxHpOverride, blockOverride);
            _portraitView?.SetDamageFloaterBelow(statsRow != null ? statsRow.transform as RectTransform : null);

            if (selectButton != null)
            {
                var hasUnit = unit != null;
                selectButton.gameObject.SetActive(hasUnit);
                selectButton.interactable = hasUnit;
            }

            if (!_hovered)
            {
                _detailPopup?.Refresh(unit, uiIcons);
                _detailPopup?.SetVisible(false);
            }
            else
            {
                _detailPopup?.Refresh(unit, uiIcons);
                _detailPopup?.SetVisible(unit != null);
            }
        }

        void ApplyInteractionBounds(Sprite sprite)
        {
            if (portraitRoot == null || _portraitHit == null)
                return;

            UiSpriteBounds.FitCentered(portraitRoot, _portraitHit, sprite, HitboxPadding);

            if (_targetOutline != null)
                _targetOutline.enabled = _targetMode && _isValidTarget;
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
                portraitImage.color = _hovered ? ValidTargetTint * HoverTint : ValidTargetTint;
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
            ApplyPortraitScale();
            ApplyPortraitColor(_currentUnit);
            _detailPopup?.Refresh(_currentUnit, _currentIcons);
            _detailPopup?.SetVisible(true);
        }

        void OnPortraitPointerExit()
        {
            if (!_hovered)
                return;

            _hovered = false;
            ApplyPortraitScale();
            ApplyPortraitColor(_currentUnit);
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
