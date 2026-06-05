using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CombatantDetailPopupView : MonoBehaviour
    {
        const int FontSize = 15;
        const float ExpBarWidth = 72f;
        const float ExpBarHeight = 6f;

        RectTransform _panel;
        Text _bodyText;
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
        }

        public void Refresh(CombatantState unit, BattleUiIconCatalogSO icons, bool showExpBar, int xp = 0)
        {
            if (_bodyText == null)
                return;

            if (unit == null)
            {
                SetVisible(false);
                return;
            }

            var status = BattleUiFormatters.FormatStatusListDisplay(unit);
            var speed = StatusRules.GetEffectiveSpeed(unit);
            var showExp = showExpBar && unit.Team == TeamSide.Player;

            var lines = CharacterProgression.FormatLevelLabel(unit.Level);
            if (showExp)
                lines += "\n";
            lines += $"\n攻击 {unit.Attack}    防御 {unit.Defense}    速度 {speed}";
            if (!string.IsNullOrEmpty(status))
                lines += $"\n状态 {status}";

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

            var lineCount = 2 + (string.IsNullOrEmpty(status) ? 0 : 1);
            if (showExp)
                lineCount++;
            _panel.sizeDelta = new Vector2(260f, 28f + lineCount * 22f);

            if (_bodyText.rectTransform != null)
            {
                var top = showExp ? -52f : -30f;
                _bodyText.rectTransform.offsetMax = new Vector2(-10f, -8f);
                _bodyText.rectTransform.offsetMin = new Vector2(10f, top);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_panel == null)
                return;

            if (!visible)
            {
                _panel.gameObject.SetActive(false);
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

            var battleRoot = GetComponentInParent<BattleScreenView>()?.transform ?? transform.root;
            CombatantTooltipLayer.MountToFront(_panel, battleRoot);
        }
    }
}
