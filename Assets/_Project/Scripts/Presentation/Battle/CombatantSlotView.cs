using Grimhand.Content;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CombatantSlotView : MonoBehaviour
    {
        static readonly Color ValidTargetTint = new(1f, 0.92f, 0.45f, 1f);
        static readonly Color DeadTint = new(0.35f, 0.35f, 0.35f, 1f);

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

        string _combatantId;

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

            if (targetHighlight != null)
            {
                if (targetHighlight.transform.parent != portraitRoot)
                {
                    targetHighlight.transform.SetParent(portraitRoot, false);
                    StretchLocal(targetHighlight.rectTransform);
                    targetHighlight.transform.SetSiblingIndex(portraitImage.transform.GetSiblingIndex() + 1);
                }

                targetHighlight.raycastTarget = false;
                targetHighlight.preserveAspect = true;
                targetHighlight.gameObject.SetActive(false);
            }

            var hit = portraitRoot.Find("PortraitHit")?.GetComponent<Image>();
            if (hit == null)
            {
                var hitGo = new GameObject("PortraitHit", typeof(RectTransform), typeof(Image));
                hitGo.transform.SetParent(portraitRoot, false);
                hit = hitGo.GetComponent<Image>();
                StretchLocal(hit.rectTransform);
            }

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
        }

        void EnsureStatusTextStyle()
        {
            if (bodyText != null)
            {
                bodyText.fontSize = Mathf.Max(bodyText.fontSize, 14);
                bodyText.fontStyle = FontStyle.Bold;
                EnsureOutline(bodyText, new Color(0f, 0f, 0f, 0.85f));
            }

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

        static void StretchLocal(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        void ApplyPortraitMirror()
        {
            if (portraitRoot != null)
                portraitRoot.localScale = mirrorPortrait ? new Vector3(-1f, 1f, 1f) : Vector3.one;
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

        public void SetSelectHandler(System.Action<string> onSelect)
        {
            if (selectButton == null)
                return;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_combatantId))
                    onSelect?.Invoke(_combatantId);
            });
        }

        public void Refresh(
            BattleState state,
            bool targetMode,
            System.Collections.Generic.IReadOnlyList<CombatantState> validTargets,
            CharacterVisualCatalogSO visuals,
            BattleUiIconCatalogSO uiIcons)
        {
            var unit = FindCombatant(state);
            _combatantId = unit?.Id;

            var isValid = false;
            if (targetMode && unit != null && validTargets != null)
            {
                foreach (var t in validTargets)
                {
                    if (t.Id == unit.Id)
                    {
                        isValid = true;
                        break;
                    }
                }
            }

            if (background != null)
                background.color = new Color(1f, 1f, 1f, 0f);

            if (portraitImage != null)
            {
                if (unit == null)
                {
                    portraitImage.enabled = false;
                    portraitImage.sprite = null;
                }
                else
                {
                    var sprite = visuals != null
                        ? visuals.GetPortrait(unit.CharacterDefinitionId)
                        : null;
                    portraitImage.sprite = sprite;
                    portraitImage.preserveAspect = true;
                    portraitImage.enabled = sprite != null;

                    if (!unit.IsAlive)
                        portraitImage.color = DeadTint;
                    else if (targetMode && isValid)
                        portraitImage.color = ValidTargetTint;
                    else
                        portraitImage.color = Color.white;
                }
            }

            if (targetHighlight != null)
                targetHighlight.gameObject.SetActive(false);

            if (bodyText != null)
                bodyText.text = unit == null ? "" : BattleUiFormatters.FormatStatusList(unit);

            if (nameText != null)
                nameText.text = unit == null ? "" : unit.DisplayName;

            if (statsRow == null)
                statsRow = GetComponentInChildren<UnitStatsRowView>(true);
            statsRow?.Refresh(unit, uiIcons);

            if (selectButton != null)
            {
                selectButton.gameObject.SetActive(targetMode && isValid);
                selectButton.interactable = targetMode && isValid;
            }
        }

        CombatantState FindCombatant(BattleState state)
        {
            foreach (var c in state.Combatants)
            {
                if (c.Team == team && c.Slot == formationSlot && c.IsAlive)
                    return c;
            }

            return null;
        }
    }
}
