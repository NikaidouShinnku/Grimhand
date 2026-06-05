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

        RectTransform _panel;
        Text _bodyText;
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
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(10f, 8f);
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

                go.SetActive(false);
            }
            else if (_panel != null && _panel.parent != _homeParent && !_panel.gameObject.activeSelf)
            {
                _panel.SetParent(_homeParent, false);
            }

            ApplySidePlacement();
        }

        void ApplySidePlacement()
        {
            if (_panel == null || _homeParent == null)
                return;

            if (_panel.parent != _homeParent)
                return;

            _panel.sizeDelta = new Vector2(220f, 96f);

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

        public void Refresh(CombatantState unit, BattleUiIconCatalogSO icons)
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
            var lines = CharacterProgression.FormatLevelLabel(unit.Level);
            lines += $"\n攻击 {unit.Attack}    防御 {unit.Defense}    速度 {speed}";
            if (!string.IsNullOrEmpty(status))
                lines += $"\n状态 {status}";

            _bodyText.text = lines;

            var lineCount = 2 + (string.IsNullOrEmpty(status) ? 0 : 1);
            _panel.sizeDelta = new Vector2(220f, 28f + lineCount * 22f);
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
