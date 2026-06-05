using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleInventoryPanelView : MonoBehaviour
    {
        GameObject _panel;
        Text _bodyText;
        ScrollRect _scroll;
        RectTransform _contentRt;

        BattleSession _session;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Initialize(BattleSession session, Transform root)
        {
            _session = session;
            EnsureBuilt(root);
            Hide();
        }

        public void Toggle()
        {
            if (_panel == null)
                return;

            if (_panel.activeSelf)
                Hide();
            else
                Show();
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        public void Refresh()
        {
            if (_panel == null || !_panel.activeSelf || _bodyText == null)
                return;

            if (_session?.Engine == null)
            {
                _bodyText.text = "战斗数据尚未就绪…";
                ResizeBody();
                return;
            }

            var body = BuildBody();
            _bodyText.text = string.IsNullOrWhiteSpace(body) ? "（暂无数据）" : body;
            ResizeBody();
        }

        void Show()
        {
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            Refresh();
        }

        void ResizeBody()
        {
            if (_bodyText == null || _contentRt == null)
                return;

            Canvas.ForceUpdateCanvases();
            var width = _contentRt.rect.width > 1f ? _contentRt.rect.width - 24f : 320f;
            var height = _bodyText.preferredHeight + 24f;
            var bodyRt = _bodyText.rectTransform;
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 120f));
            _contentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyRt.rect.height + 16f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRt);
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        string BuildBody()
        {
            var sb = new StringBuilder();
            var state = _session.Engine.State;

            if (_session.IsExpeditionMode)
                sb.AppendLine($"【金币】 {_session.Expedition.Run.Gold}");
            else
                sb.AppendLine("【金币】 —");

            sb.AppendLine("【遗物】");
            if (_session.IsExpeditionMode && _session.Expedition.Run.Relics.Count > 0)
            {
                foreach (var relicId in _session.Expedition.Run.Relics)
                {
                    if (RelicDatabase.TryGet(relicId, out var relic))
                        sb.AppendLine($"  · {relic.DisplayName} — {relic.Description}");
                    else
                        sb.AppendLine($"  · {relicId}");
                }
            }
            else
                sb.AppendLine("  暂无");

            sb.AppendLine();

            sb.AppendLine("【角色】");
            var wrotePlayer = false;
            foreach (var unit in state.Combatants)
            {
                if (unit.Team != TeamSide.Player)
                    continue;

                wrotePlayer = true;
                var xpLine = _session.IsExpeditionMode
                    ? $"  {CharacterProgression.FormatXpLine(unit.Level, unit.Xp)}"
                    : "";
                sb.AppendLine(
                    $"{unit.DisplayName}  Lv.{unit.Level}{xpLine}  " +
                    $"HP {unit.Hp}/{unit.MaxHp}  攻{unit.Attack}  防{unit.Defense}  " +
                    $"速{StatusRules.GetEffectiveSpeed(unit)}");
            }

            if (!wrotePlayer)
                sb.AppendLine("  —");

            sb.AppendLine();
            sb.AppendLine("【卡牌】");
            AppendCardPool(sb, "手牌", state.PlayerHand);
            AppendCardPool(sb, "抽牌堆", state.PlayerDrawPile);
            AppendCardPool(sb, "弃牌堆", state.PlayerDiscardPile);

            if (_session.IsExpeditionMode && _session.Expedition.Run.Party.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【远征队伍】");
                foreach (var m in _session.Expedition.Run.Party)
                    sb.AppendLine(
                        $"{m.DisplayName}  Lv.{m.Level}  {CharacterProgression.FormatXpLine(m.Level, m.Xp)}  " +
                        $"HP {m.Hp}/{m.MaxHp}");
            }

            return sb.ToString().TrimEnd();
        }

        static void AppendCardPool(StringBuilder sb, string label, IReadOnlyList<CardInstanceState> cards)
        {
            sb.Append(label).Append(" (").Append(cards.Count).Append("): ");
            if (cards.Count == 0)
            {
                sb.AppendLine("—");
                return;
            }

            sb.AppendLine();
            for (var i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                sb.Append("  · ").Append(c.DisplayName).Append(" 费").Append(c.Cost);
                if (i < cards.Count - 1)
                    sb.AppendLine();
            }

            sb.AppendLine();
        }

        void EnsureBuilt(Transform root)
        {
            if (_panel != null)
                return;

            _panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(root, false);
            var panelRt = _panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            panelRt.anchoredPosition = new Vector2(8f, 68f);
            panelRt.sizeDelta = new Vector2(380f, 460f);

            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.13f, 0.96f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(_panel.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "背包";
            title.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(12f, -40f);
            titleRt.offsetMax = new Vector2(-12f, -8f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(_panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(8f, 8f);
            scrollRt.offsetMax = new Vector2(-8f, -44f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _contentRt = contentGo.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0f, 1f);
            _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.anchoredPosition = Vector2.zero;
            _contentRt.sizeDelta = new Vector2(0f, 400f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(_contentRt, false);
            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 15;
            _bodyText.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _bodyText.supportRichText = false;
            _bodyText.raycastTarget = false;

            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = new Vector2(-24f, 400f);

            _scroll.viewport = viewportRt;
            _scroll.content = _contentRt;
        }
    }
}
