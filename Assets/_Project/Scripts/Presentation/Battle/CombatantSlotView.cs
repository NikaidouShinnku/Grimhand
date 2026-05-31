using Grimhand.Content;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CombatantSlotView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image portraitImage;
        [SerializeField] Text slotLabel;
        [SerializeField] Text bodyText;
        [SerializeField] Button selectButton;
        [SerializeField] TeamSide team;
        [SerializeField] FormationSlot formationSlot;

        string _combatantId;

        public void Configure(FormationSlot slot, TeamSide teamSide, string rowLabel)
        {
            formationSlot = slot;
            team = teamSide;
            if (slotLabel != null)
                slotLabel.text = rowLabel + " · " + BattleUiFormatters.SlotLabel(slot);
        }

        void Awake()
        {
            if (formationSlot == 0)
                TryInferSlotFromName();
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
            CharacterVisualCatalogSO visuals)
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
                    portraitImage.color = unit.IsAlive ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
                    portraitImage.enabled = sprite != null;
                }
            }

            if (background != null)
            {
                if (unit == null)
                    background.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);
                else if (team == TeamSide.Player)
                    background.color = new Color(0.22f, 0.38f, 0.58f, 0.95f);
                else
                    background.color = new Color(0.55f, 0.22f, 0.22f, 0.95f);

                if (targetMode && isValid)
                    background.color = new Color(0.85f, 0.65f, 0.15f, 1f);
            }

            if (bodyText != null)
            {
                bodyText.text = unit == null
                    ? "—"
                    : BattleUiFormatters.FormatUnitLine(unit);
            }

            if (selectButton != null)
                selectButton.interactable = targetMode && isValid;
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
