using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CombatantDetailPopupView : MonoBehaviour
    {
        const int FontSize = 15;
        const float ExpBarWidth = 72f;
        const float ExpBarHeight = 6f;
        const float StatusPanelGap = 6f;
        const float StatusPanelWidth = 280f;

        RectTransform _panel;
        RectTransform _statusPanel;
        Text _bodyText;
        Text _statusText;
        RectTransform _expRow;
        Text _expLabel;
        Image _expFill;
        Transform _homeParent;
        bool _built;
        TeamSide _team;

        public void EnsureBuilt(Transform parent, TeamSide team)
        {
            _team = team;
            _homeParent = parent;

            if (!_built)
            {
                _built = true;

                var go = new GameObject("DetailPopup", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                _panel = go.GetComponent<RectTransform>();

                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
                bg.raycastTarget = false;

                var textGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(0f, 1f);
                textRt.anchorMax = new Vector2(1f, 1f);
                textRt.pivot = new Vector2(0f, 1f);
                textRt.offsetMin = new Vector2(UiInfoPlateMetrics.PadX, -999f);
                textRt.offsetMax = new Vector2(-UiInfoPlateMetrics.PadX, -UiInfoPlateMetrics.PadY);

                _bodyText = textGo.GetComponent<Text>();
                _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bodyText.fontSize = FontSize;
                _bodyText.fontStyle = FontStyle.Normal;
                _bodyText.color = Color.white;
                _bodyText.alignment = TextAnchor.UpperLeft;
                _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                _bodyText.supportRichText = true;
                _bodyText.raycastTarget = false;

                var textOutline = textGo.AddComponent<Outline>();
                textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                textOutline.effectDistance = new Vector2(1f, -1f);

                BuildExpRow(go.transform);
                BuildStatusPanel(_panel);

                go.SetActive(false);
            }
            else if (_panel != null && _panel.parent != _homeParent && !_panel.gameObject.activeSelf)
            {
                _panel.SetParent(_homeParent, false);
            }

            ApplySidePlacement();
        }

        void BuildExpRow(Transform parent)
        {
            var rowGo = new GameObject("ExpRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            _expRow = rowGo.GetComponent<RectTransform>();
            _expRow.anchorMin = new Vector2(0f, 1f);
            _expRow.anchorMax = new Vector2(1f, 1f);
            _expRow.pivot = new Vector2(0f, 1f);
            _expRow.anchoredPosition = new Vector2(10f, -30f);
            _expRow.sizeDelta = new Vector2(-20f, ExpBarHeight);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(_expRow, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = new Vector2(72f, 18f);

            _expLabel = labelGo.GetComponent<Text>();
            _expLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _expLabel.fontSize = FontSize;
            _expLabel.fontStyle = FontStyle.Bold;
            _expLabel.color = Color.white;
            _expLabel.alignment = TextAnchor.MiddleLeft;
            _expLabel.raycastTarget = false;

            var barBgGo = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(_expRow, false);
            var barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 0.5f);
            barBgRt.anchorMax = new Vector2(0f, 0.5f);
            barBgRt.pivot = new Vector2(0f, 0.5f);
            barBgRt.anchoredPosition = new Vector2(76f, 0f);
            barBgRt.sizeDelta = new Vector2(ExpBarWidth, ExpBarHeight);
            barBgGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.9f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBgGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);
            _expFill = fillGo.GetComponent<Image>();
            _expFill.color = new Color(0.35f, 0.82f, 1f, 0.95f);

            _expRow.gameObject.SetActive(false);
        }

        void BuildStatusPanel(RectTransform panelParent)
        {
            var go = new GameObject("StatusPopup", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panelParent, false);
            _statusPanel = go.GetComponent<RectTransform>();

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(UiInfoPlateMetrics.PadX, UiInfoPlateMetrics.PadY);
            textRt.offsetMax = new Vector2(-UiInfoPlateMetrics.PadX, -UiInfoPlateMetrics.PadY);

            _statusText = textGo.GetComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = FontSize;
            _statusText.fontStyle = FontStyle.Normal;
            _statusText.color = new Color(0.92f, 0.95f, 1f, 1f);
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
            _statusText.supportRichText = true;
            _statusText.raycastTarget = false;

            go.SetActive(false);
        }

        void ApplySidePlacement()
        {
            if (_panel == null || _homeParent == null)
                return;

            if (_panel.parent != _homeParent)
                return;

            if (_team == TeamSide.Player)
            {
                _panel.anchorMin = new Vector2(0.72f, 0.52f);
                _panel.anchorMax = new Vector2(0.72f, 0.52f);
                _panel.pivot = new Vector2(0f, 0.5f);
                _panel.anchoredPosition = new Vector2(8f, 0f);
            }
            else
            {
                _panel.anchorMin = new Vector2(0.28f, 0.52f);
                _panel.anchorMax = new Vector2(0.28f, 0.52f);
                _panel.pivot = new Vector2(1f, 0.5f);
                _panel.anchoredPosition = new Vector2(-8f, 0f);
            }

            LayoutStatusPanelBesideMain();
        }

        void LayoutStatusPanelBesideMain()
        {
            if (_statusPanel == null || _panel == null)
                return;

            if (_team == TeamSide.Player)
            {
                _statusPanel.anchorMin = new Vector2(1f, 0.5f);
                _statusPanel.anchorMax = new Vector2(1f, 0.5f);
                _statusPanel.pivot = new Vector2(0f, 0.5f);
                _statusPanel.anchoredPosition = new Vector2(StatusPanelGap, 0f);
            }
            else
            {
                _statusPanel.anchorMin = new Vector2(0f, 0.5f);
                _statusPanel.anchorMax = new Vector2(0f, 0.5f);
                _statusPanel.pivot = new Vector2(1f, 0.5f);
                _statusPanel.anchoredPosition = new Vector2(-StatusPanelGap, 0f);
            }
        }

        public void Refresh(
            CombatantState unit,
            BattleUiIconCatalogSO icons,
            bool showExpBar,
            int xp = 0,
            PartyMemberSnapshot expeditionMember = null,
            IReadOnlyList<string> runRelics = null,
            PresentationSnapshot presentation = null,
            ExpeditionConfig expeditionConfig = null,
            BattleState battleState = null)
        {
            if (_bodyText == null)
                return;

            if (unit == null)
            {
                SetVisible(false);
                return;
            }

            ApplyInformationPlate(_panel, icons);
            ApplyInformationPlate(_statusPanel, icons);

            var statusTooltip = BattleUiFormatters.FormatStatusTooltipDescriptions(unit, battleState);
            var speed = CombatantDisplayHelper.GetSpeed(unit, presentation);
            var showExp = showExpBar && unit.Team == TeamSide.Player;
            var traitFootnote = CombatantDisplayHelper.GetTraitFootnote(unit, presentation);
            var talentBlock = unit.Team == TeamSide.Player
                ? TalentDisplayFormatter.FormatSelectedTalents(expeditionMember)
                : "";
            var traitBlock = unit.Team == TeamSide.Enemy
                ? MinionTraitDisplayFormatter.FormatTraitDescriptions(unit)
                : "";

            var lines = CharacterProgression.FormatLevelLabel(unit.Level);
            if (showExp)
                lines += "\n";
            lines += $"\n生命 {unit.Hp}/{unit.MaxHp}    速度 {speed}";
            if (expeditionMember != null
                && expeditionMember.PersonalAttackBonus > 0)
            {
                lines += $"\n增伤 +{expeditionMember.PersonalAttackBonus}";
            }

            if (!string.IsNullOrEmpty(talentBlock))
                lines += $"\n\n<b>天赋</b>\n{talentBlock}";

            if (!string.IsNullOrEmpty(traitBlock))
                lines += $"\n\n<b>特性</b>\n{traitBlock}";

            if (!string.IsNullOrEmpty(traitFootnote))
            {
                if (!string.IsNullOrEmpty(talentBlock) || !string.IsNullOrEmpty(traitBlock))
                    lines += "\n";
                lines += $"\n{traitFootnote}";
            }

            _bodyText.text = lines;
            _bodyText.supportRichText = true;

            if (_expRow != null)
            {
                _expRow.gameObject.SetActive(showExp);
                if (showExp && _expLabel != null && _expFill != null)
                {
                    _expLabel.text = CharacterProgression.FormatXpLine(unit.Level, xp);
                    var fillRt = _expFill.rectTransform;
                    fillRt.anchorMax = new Vector2(CharacterProgression.XpFill01(unit.Level, xp), 1f);
                }
            }

            var hasExtraBlock = !string.IsNullOrEmpty(talentBlock) || !string.IsNullOrEmpty(traitBlock);
            var panelW = hasExtraBlock
                ? Mathf.Clamp(UiInfoPlateMetrics.MinWidth + 100f, UiInfoPlateMetrics.MinWidth, UiInfoPlateMetrics.MaxWidth)
                : UiInfoPlateMetrics.MinWidth + 40f;
            var innerW = UiInfoPlateMetrics.InnerWidth(panelW);
            var bodyH = UiInfoPlateMetrics.MeasureHeight(_bodyText, lines, innerW);
            var topReserve = showExp ? 56f : UiInfoPlateMetrics.PadY;
            _panel.sizeDelta = new Vector2(panelW, topReserve + bodyH + UiInfoPlateMetrics.PadY);

            if (_bodyText.rectTransform != null)
            {
                var textRt = _bodyText.rectTransform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(UiInfoPlateMetrics.PadX, UiInfoPlateMetrics.PadY);
                textRt.offsetMax = new Vector2(-UiInfoPlateMetrics.PadX, -topReserve);
            }

            if (_statusPanel != null && _statusText != null)
            {
                var hasStatusBox = !string.IsNullOrEmpty(statusTooltip);
                _statusPanel.gameObject.SetActive(hasStatusBox);
                if (hasStatusBox)
                    ResizeStatusPanelToFit(statusTooltip);
            }

            LayoutStatusPanelBesideMain();
            ApplySidePlacement();
        }

        void ResizeStatusPanelToFit(string statusTooltip)
        {
            if (_statusPanel == null || _statusText == null)
                return;

            _statusText.text = statusTooltip;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
            _statusText.supportRichText = true;

            var panelW = StatusPanelWidth;
            var innerW = UiInfoPlateMetrics.InnerWidth(panelW);
            var preferred = UiInfoPlateMetrics.MeasureHeight(_statusText, statusTooltip, innerW);
            _statusPanel.sizeDelta = new Vector2(panelW, preferred + UiInfoPlateMetrics.PadY * 2f);
            UiInfoPlateMetrics.ApplyTextInsets(_statusText.rectTransform);
        }

        public void SetVisible(bool visible)
        {
            if (_panel == null)
                return;

            if (!visible)
            {
                _panel.gameObject.SetActive(false);
                _statusPanel?.gameObject.SetActive(false);
                if (_homeParent != null && _panel.parent != _homeParent)
                {
                    _panel.SetParent(_homeParent, false);
                    ApplySidePlacement();
                }

                return;
            }

            if (_homeParent != null && _panel.parent != _homeParent)
            {
                _panel.SetParent(_homeParent, false);
                ApplySidePlacement();
            }
            else
            {
                ApplySidePlacement();
            }

            _panel.gameObject.SetActive(true);
            LayoutStatusPanelBesideMain();

            var battleRoot = GetComponentInParent<BattleScreenView>()?.transform ?? transform.root;
            CombatantTooltipLayer.MountToFront(_panel, battleRoot);
        }

        static void ApplyInformationPlate(RectTransform target, BattleUiIconCatalogSO icons)
        {
            if (target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image == null)
                return;

            var plate = icons != null ? icons.UiInformationPlate : null;
            if (plate != null)
            {
                image.sprite = plate;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }

            foreach (var fx in target.GetComponents<Outline>())
                Destroy(fx);
        }
    }
}
