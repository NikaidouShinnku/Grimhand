using System.Text;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    [DisallowMultipleComponent]
    public sealed class BattleTurnDetailPanelView : MonoBehaviour
    {
        const float PanelWidth = 560f;
        const float PanelHeight = 640f;
        const float PanelLeft = 12f;
        const float PanelBottom = 120f;

        BattleSession _session;
        Transform _battleRoot;
        RectTransform _panel;
        RectTransform _content;
        Text _bodyText;
        ScrollRect _scroll;
        bool _open;
        bool _built;

        public bool IsOpen => _open;

        public void Initialize(BattleSession session, Transform battleRoot)
        {
            _session = session;
            _battleRoot = battleRoot;
            EnsureBuilt(battleRoot);
        }

        public void Toggle()
        {
            _open = !_open;
            if (_panel == null)
                return;

            if (_open)
            {
                CombatantTooltipLayer.MountToFront(_panel, _battleRoot != null ? _battleRoot : _panel.root);
                _panel.gameObject.SetActive(true);
                Refresh();
            }
            else
            {
                _panel.gameObject.SetActive(false);
            }
        }

        public void Refresh()
        {
            if (!_built || _bodyText == null || _session == null)
                return;

            var sb = new StringBuilder();

            if (_session.IsExpeditionMode && _session.Expedition?.Run?.RunAcquisitionLog is { Count: > 0 } acquisitions)
            {
                sb.AppendLine("【远征获取】");
                for (var i = 0; i < acquisitions.Count; i++)
                    sb.AppendLine($"· {acquisitions[i]}");
                sb.AppendLine();
            }

            if (_session.ConsumablesUsedThisBattle is { Count: > 0 } consumables)
            {
                sb.AppendLine("【本战消耗品】");
                for (var i = 0; i < consumables.Count; i++)
                    sb.AppendLine($"· {consumables[i]}");
                sb.AppendLine();
            }

            var lines = _session.TurnLog.LastRound;
            if (lines == null || lines.Count == 0)
            {
                if (sb.Length == 0)
                    sb.AppendLine("暂无战斗明细。\n出牌结算后可在此查看（最多保留 40 条）。");
                else
                    sb.AppendLine("【战斗明细】\n（本回合暂无出牌记录）");
            }
            else
            {
                sb.AppendLine("【战斗明细】");
                for (var i = 0; i < lines.Count; i++)
                    sb.AppendLine($"{i + 1}. {lines[i]}");
            }

            _bodyText.text = sb.ToString();
            ResizeBody();
        }

        void ResizeBody()
        {
            if (_bodyText == null || _content == null)
                return;

            Canvas.ForceUpdateCanvases();
            var width = _content.rect.width > 1f ? _content.rect.width - 16f : 500f;
            var height = _bodyText.preferredHeight + 16f;
            var bodyRt = _bodyText.rectTransform;
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 80f));
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyRt.rect.height + 8f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        void EnsureBuilt(Transform battleRoot)
        {
            if (_built)
                return;

            _built = true;

            var go = new GameObject("TurnDetailPanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(battleRoot, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);
            _panel.anchoredPosition = new Vector2(PanelLeft, PanelBottom);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.97f);
            bg.raycastTarget = true;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(-20f, 32f);
            var title = titleGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.UpperLeft;
            title.color = Color.white;
            title.text = "上回合明细";

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(go.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(10f, 10f);
            scrollRt.offsetMax = new Vector2(-26f, -44f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            scrollBg.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            var viewportMask = viewportGo.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot = new Vector2(1f, 0.5f);
            scrollbarRt.sizeDelta = new Vector2(12f, 0f);
            scrollbarGo.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.26f, 0.95f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(scrollbarGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = new Vector2(2f, 2f);
            handleRt.offsetMax = new Vector2(-2f, -2f);
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0.45f, 0.5f, 0.62f, 1f);

            var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);

            var contentFitter = contentGo.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter), typeof(LayoutElement));
            bodyGo.transform.SetParent(contentGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = new Vector2(-16f, 0f);

            var bodyFitter = bodyGo.GetComponent<ContentSizeFitter>();
            bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = bodyGo.GetComponent<LayoutElement>();
            layout.minWidth = 500f;
            layout.preferredWidth = 500f;

            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 16;
            _bodyText.fontStyle = FontStyle.Normal;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _bodyText.raycastTarget = false;

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewportRt;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 30f;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            go.SetActive(false);
        }
    }
}
