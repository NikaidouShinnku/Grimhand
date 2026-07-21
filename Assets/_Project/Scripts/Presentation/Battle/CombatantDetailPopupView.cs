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
        const float StatusPanelWidth = 240f;

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

                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.75f, 0.82f, 0.95f, 0.55f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                var textGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(0f, 1f);
                textRt.anchorMax = new Vector2(1f, 1f);
                textRt.pivot = new Vector2(0f, 1f);
                textRt.offsetMin = new Vector2(10f, -999f);
                textRt.offsetMax = new Vector2(-10f, -8f);

                _bodyText = textGo.GetComponent<Text>();
                _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bodyText.fontSize = FontSize;
                _bodyText.fontStyle = FontStyle.Bold;
                _bodyText.color = Color.white;
                _bodyText.alignment = TextAnchor.UpperLeft;
                _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
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

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.72f, 0.95f, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var textGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 8f);
            textRt.offsetMax = new Vector2(-10f, -8f);

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
            ExpeditionConfig expeditionConfig = null)
        {
            if (_bodyText == null)
                return;

            if (unit == null)
            {
                SetVisible(false);
                return;
            }

            var statusTooltip = BattleUiFormatters.FormatStatusTooltipDescriptions(unit);
            var speed = CombatantDisplayHelper.GetSpeed(unit, presentation);
            var showExp = showExpBar && unit.Team == TeamSide.Player;
            var traitFootnote = CombatantDisplayHelper.GetTraitFootnote(unit, presentation);

            var lines = CharacterProgression.FormatLevelLabel(unit.Level);
            if (showExp)
                lines += "\n";
            lines += $"\n生命 {unit.Hp}/{unit.MaxHp}    速度 {speed}";
            if (expeditionMember != null
                && expeditionMember.PersonalAttackBonus > 0)
            {
                lines += $"\n增伤 +{expeditionMember.PersonalAttackBonus}";
            }
            if (!string.IsNullOrEmpty(traitFootnote))
                lines += $"\n{traitFootnote}";

            if (runRelics != null && runRelics.Count > 0 && unit.Team == TeamSide.Player)
            {
                lines += "\n遗物";
                foreach (var relicId in runRelics)
                {
                    if (RelicDatabase.TryGet(relicId, out var relic))
                        lines += $"\n· {relic.DisplayName}";
                }
            }

            _bodyText.text = lines;

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

            // 属性框：沿用原逻辑，勿用 preferredHeight 改布局
            var lineCount = 2;
            if (!string.IsNullOrEmpty(traitFootnote))
                lineCount += traitFootnote.Split('\n').Length;
            if (showExp)
                lineCount++;
            if (runRelics != null && runRelics.Count > 0 && unit.Team == TeamSide.Player)
                lineCount += 1 + runRelics.Count;
            if (expeditionMember != null && expeditionMember.PersonalAttackBonus > 0)
                lineCount++;
            _panel.sizeDelta = new Vector2(280f, 28f + lineCount * 20f);

            if (_bodyText.rectTransform != null)
            {
                var top = showExp ? -52f : -30f;
                _bodyText.rectTransform.offsetMax = new Vector2(-10f, -8f);
                _bodyText.rectTransform.offsetMin = new Vector2(10f, top);
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

            // 顶对齐 + 固定内容宽度，再用 preferredHeight 决定面板高度
            var textRt = _statusText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = new Vector2(0f, -8f);
            var contentWidth = StatusPanelWidth - 20f;
            textRt.sizeDelta = new Vector2(-20f, 0f);

            Canvas.ForceUpdateCanvases();
            var preferred = _statusText.preferredHeight;
            if (preferred < 8f)
                preferred = EstimateWrappedTextHeight(statusTooltip, contentWidth);

            var height = Mathf.Max(48f, preferred + 16f);
            _statusPanel.sizeDelta = new Vector2(StatusPanelWidth, height);
            textRt.sizeDelta = new Vector2(-20f, preferred);
        }

        float EstimateWrappedTextHeight(string richText, float width)
        {
            var plain = StripRichText(richText ?? "");
            if (string.IsNullOrEmpty(plain) || width <= 0f)
                return FontSize + 4f;

            var avgCharWidth = FontSize * 0.95f; // 中文为主，按近似全角宽估算换行
            var charsPerLine = Mathf.Max(1, Mathf.FloorToInt(width / avgCharWidth));
            var visualLines = 0;
            foreach (var line in plain.Split('\n'))
            {
                var len = Mathf.Max(1, line.Length);
                visualLines += Mathf.CeilToInt(len / (float)charsPerLine);
            }

            return visualLines * (FontSize + 6f);
        }

        static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var sb = new System.Text.StringBuilder(text.Length);
            var inTag = false;
            foreach (var ch in text)
            {
                if (ch == '<')
                {
                    inTag = true;
                    continue;
                }

                if (ch == '>')
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                    sb.Append(ch);
            }

            return sb.ToString();
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
    }
}
