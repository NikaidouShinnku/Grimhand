using System;
using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public enum TutorialPlateAnchor
    {
        Center,
        BelowHighlight,
        AboveHighlight
    }

    /// <summary>教程高亮提示：半透明遮罩 + information_plate + button1。</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialCoachOverlayView : MonoBehaviour
    {
        const float HighlightPad = 8f;
        const float PlateWidth = 560f;
        const float ButtonWidth = 200f;
        const float TextPadLeft = 48f;
        const float TextPadRight = 40f;
        const float TextPadTop = 46f;
        const float TextPadBottom = 34f;
        const int MaxFrames = 4;

        static readonly Color DimColor = new(0.02f, 0.03f, 0.06f, 0.72f);
        static readonly Color FrameColor = new(0.95f, 0.82f, 0.42f, 0.95f);
        static readonly Color TitleColor = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyColor = new(0.88f, 0.90f, 0.95f, 1f);

        BattleUiIconCatalogSO _icons;
        RectTransform _root;
        Image _dimTop;
        Image _dimBottom;
        Image _dimLeft;
        Image _dimRight;
        readonly List<RectTransform> _frames = new();
        RectTransform _plate;
        Image _plateBg;
        Text _titleText;
        Text _bodyText;
        Button _continueButton;
        Action _onContinue;
        bool _built;
        bool _showing;
        bool _awaitingClick;

        public bool IsShowing => _showing && _root != null && _root.gameObject.activeSelf;
        public bool IsAwaitingClick => _awaitingClick && IsShowing;

        public void Initialize(Transform parent, BattleUiIconCatalogSO icons)
        {
            _icons = icons;
            EnsureBuilt(parent);
            Hide();
        }

        public void Show(
            string title,
            string body,
            RectTransform highlightTarget,
            string buttonLabel,
            Action onContinue,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            var targets = highlightTarget != null
                ? new[] { highlightTarget }
                : Array.Empty<RectTransform>();
            Show(title, body, targets, buttonLabel, onContinue, anchor);
        }

        public void Show(
            string title,
            string body,
            IReadOnlyList<RectTransform> highlightTargets,
            string buttonLabel,
            Action onContinue,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            EnsureBuilt(transform);
            _onContinue = onContinue;
            _awaitingClick = false;
            if (_titleText != null)
                _titleText.text = title ?? "";
            if (_bodyText != null)
                _bodyText.text = body ?? "";

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(true);
                PlanningActionButtonStyle.Apply(
                    _continueButton,
                    _icons != null ? _icons.UiButton1 : null,
                    string.IsNullOrEmpty(buttonLabel) ? "继续" : buttonLabel,
                    ButtonWidth);
            }

            LayoutHighlights(highlightTargets);
            LayoutPlate(highlightTargets, showButton: true, anchor);
            BringToFront();
            _showing = true;
        }

        /// <summary>强制点击高亮目标（无按钮），条件满足后由外部 Hide。</summary>
        public void ShowAwaitingTargetClick(
            string title,
            string body,
            RectTransform highlightTarget,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            var targets = highlightTarget != null
                ? new[] { highlightTarget }
                : Array.Empty<RectTransform>();
            ShowAwaitingTargetClick(title, body, targets, anchor);
        }

        public void ShowAwaitingTargetClick(
            string title,
            string body,
            IReadOnlyList<RectTransform> highlightTargets,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            EnsureBuilt(transform);
            _onContinue = null;
            _awaitingClick = true;
            if (_titleText != null)
                _titleText.text = title ?? "";
            if (_bodyText != null)
                _bodyText.text = body ?? "";
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(false);

            LayoutHighlights(highlightTargets);
            LayoutPlate(highlightTargets, showButton: false, anchor);
            BringToFront();
            _showing = true;
        }

        public void BringToFront()
        {
            if (_root == null)
                return;

            // 挂到角色浮层 Canvas，避免被背包/祭坛等覆盖
            var battleRoot = transform;
            CombatantTooltipLayer.MountToFront(_root, battleRoot);
            StretchFull(_root);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Hide()
        {
            _showing = false;
            _awaitingClick = false;
            _onContinue = null;
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(true);
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        public bool TryDismissViaEscape()
        {
            if (!IsShowing)
                return false;

            if (_awaitingClick || _continueButton == null || !_continueButton.gameObject.activeSelf)
                return true;

            OnContinueClicked();
            return true;
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;
            var host = parent != null ? parent : transform;
            var go = new GameObject("TutorialCoachOverlay", typeof(RectTransform));
            go.transform.SetParent(host, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);

            _dimTop = CreateDim("DimTop");
            _dimBottom = CreateDim("DimBottom");
            _dimLeft = CreateDim("DimLeft");
            _dimRight = CreateDim("DimRight");

            for (var i = 0; i < MaxFrames; i++)
            {
                var frameGo = new GameObject($"HighlightFrame_{i}", typeof(RectTransform), typeof(Image));
                frameGo.transform.SetParent(_root, false);
                var frame = frameGo.GetComponent<RectTransform>();
                var frameImg = frameGo.GetComponent<Image>();
                frameImg.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, 0.35f);
                frameImg.raycastTarget = false;
                frameGo.SetActive(false);
                _frames.Add(frame);
            }

            var plateGo = new GameObject("TipPlate", typeof(RectTransform), typeof(Image));
            plateGo.transform.SetParent(_root, false);
            _plate = plateGo.GetComponent<RectTransform>();
            _plateBg = plateGo.GetComponent<Image>();
            _plateBg.raycastTarget = true;
            ApplyPlateSprite();

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(_plate, false);
            _titleText = titleGo.GetComponent<Text>();
            ConfigureText(_titleText, 24, FontStyle.Bold, TitleColor);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(_plate, false);
            _bodyText = bodyGo.GetComponent<Text>();
            ConfigureText(_bodyText, 20, FontStyle.Normal, BodyColor);

            var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_plate, false);
            _continueButton = btnGo.GetComponent<Button>();
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, TextPadBottom);
            btnRt.sizeDelta = new Vector2(
                ButtonWidth,
                PlanningActionButtonStyle.HeightForWidth(ButtonWidth));
            _continueButton.onClick.AddListener(OnContinueClicked);
            UiAudioHooks.WireButton(_continueButton);
            PlanningActionButtonStyle.Apply(
                _continueButton,
                _icons != null ? _icons.UiButton1 : null,
                "继续",
                ButtonWidth);
        }

        Image CreateDim(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var img = go.GetComponent<Image>();
            img.color = DimColor;
            img.raycastTarget = true;
            StretchFull(go.GetComponent<RectTransform>());
            return img;
        }

        void ApplyPlateSprite()
        {
            if (_plateBg == null)
                return;

            var plate = _icons != null ? _icons.UiInformationPlate : null;
            if (plate != null)
            {
                _plateBg.sprite = plate;
                _plateBg.type = Image.Type.Sliced;
                _plateBg.color = Color.white;
            }
            else
            {
                _plateBg.sprite = null;
                _plateBg.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            }
        }

        static void ConfigureText(Text text, int size, FontStyle style, Color color)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;
            text.raycastTarget = false;
            text.supportRichText = true;
        }

        void LayoutHighlights(IReadOnlyList<RectTransform> targets)
        {
            if (_root == null)
                return;

            for (var i = 0; i < _frames.Count; i++)
                _frames[i].gameObject.SetActive(false);

            if (targets == null || targets.Count == 0)
            {
                SetDimEdge(_dimTop, 0f, 1f, 0f, 1f);
                SetDimEdge(_dimBottom, 0f, 0f, 0f, 0f);
                SetDimEdge(_dimLeft, 0f, 0f, 0f, 0f);
                SetDimEdge(_dimRight, 0f, 0f, 0f, 0f);
                return;
            }

            var canvas = _root.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var rootCorners = new Vector3[4];
            _root.GetWorldCorners(rootCorners);
            var rootMin = RectTransformUtility.WorldToScreenPoint(cam, rootCorners[0]);
            var rootMax = RectTransformUtility.WorldToScreenPoint(cam, rootCorners[2]);
            var rootH = Mathf.Max(1f, rootMax.y - rootMin.y);
            var rootW = Mathf.Max(1f, rootMax.x - rootMin.x);

            var unionX0 = 1f;
            var unionX1 = 0f;
            var unionY0 = 1f;
            var unionY1 = 0f;
            var any = false;
            var frameIndex = 0;

            for (var i = 0; i < targets.Count && frameIndex < _frames.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                if (!TryNormRect(target, cam, rootMin, rootW, rootH, out var x0, out var x1, out var y0, out var y1))
                    continue;

                any = true;
                unionX0 = Mathf.Min(unionX0, x0);
                unionX1 = Mathf.Max(unionX1, x1);
                unionY0 = Mathf.Min(unionY0, y0);
                unionY1 = Mathf.Max(unionY1, y1);

                var frame = _frames[frameIndex++];
                frame.gameObject.SetActive(true);
                frame.anchorMin = new Vector2(x0, y0);
                frame.anchorMax = new Vector2(x1, y1);
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
            }

            if (!any)
            {
                SetDimEdge(_dimTop, 0f, 1f, 0f, 1f);
                SetDimEdge(_dimBottom, 0f, 0f, 0f, 0f);
                SetDimEdge(_dimLeft, 0f, 0f, 0f, 0f);
                SetDimEdge(_dimRight, 0f, 0f, 0f, 0f);
                return;
            }

            SetDimEdge(_dimTop, 0f, 1f, unionY1, 1f);
            SetDimEdge(_dimBottom, 0f, 1f, 0f, unionY0);
            SetDimEdge(_dimLeft, 0f, unionX0, unionY0, unionY1);
            SetDimEdge(_dimRight, unionX1, 1f, unionY0, unionY1);
        }

        bool TryNormRect(
            RectTransform target,
            Camera cam,
            Vector2 rootMin,
            float rootW,
            float rootH,
            out float x0,
            out float x1,
            out float y0,
            out float y1)
        {
            x0 = x1 = y0 = y1 = 0f;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var tMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            var tMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            var pad = HighlightPad;
            x0 = Mathf.Clamp01((Mathf.Min(tMin.x, tMax.x) - rootMin.x - pad) / rootW);
            x1 = Mathf.Clamp01((Mathf.Max(tMin.x, tMax.x) - rootMin.x + pad) / rootW);
            y0 = Mathf.Clamp01((Mathf.Min(tMin.y, tMax.y) - rootMin.y - pad) / rootH);
            y1 = Mathf.Clamp01((Mathf.Max(tMin.y, tMax.y) - rootMin.y + pad) / rootH);
            if (x1 < x0)
                (x0, x1) = (x1, x0);
            if (y1 < y0)
                (y0, y1) = (y1, y0);

            const float minNorm = 0.018f;
            if (x1 - x0 < minNorm)
            {
                var mid = (x0 + x1) * 0.5f;
                x0 = Mathf.Clamp01(mid - minNorm * 0.5f);
                x1 = Mathf.Clamp01(mid + minNorm * 0.5f);
            }

            if (y1 - y0 < minNorm)
            {
                var mid = (y0 + y1) * 0.5f;
                y0 = Mathf.Clamp01(mid - minNorm * 0.5f);
                y1 = Mathf.Clamp01(mid + minNorm * 0.5f);
            }

            return true;
        }

        void LayoutPlate(
            IReadOnlyList<RectTransform> highlightTargets,
            bool showButton,
            TutorialPlateAnchor anchor)
        {
            if (_plate == null)
                return;

            ApplyPlateSprite();
            var innerW = Mathf.Max(40f, PlateWidth - TextPadLeft - TextPadRight);
            var titleH = UiInfoPlateMetrics.MeasureHeight(_titleText, _titleText.text, innerW);
            var bodyH = UiInfoPlateMetrics.MeasureHeight(_bodyText, _bodyText.text, innerW);
            var btnH = showButton ? PlanningActionButtonStyle.HeightForWidth(ButtonWidth) : 0f;
            var contentH = titleH + 10f + bodyH + (showButton ? 20f + btnH : 8f);
            var panelH = Mathf.Max(170f, contentH + TextPadTop + TextPadBottom);
            _plate.sizeDelta = new Vector2(PlateWidth, panelH);

            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(TextPadLeft, -TextPadTop);
            titleRt.sizeDelta = new Vector2(-(TextPadLeft + TextPadRight), titleH);

            var bodyRt = _bodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0f, 1f);
            bodyRt.anchoredPosition = new Vector2(TextPadLeft, -(TextPadTop + titleH + 10f));
            bodyRt.sizeDelta = new Vector2(-(TextPadLeft + TextPadRight), bodyH);

            if (_continueButton != null)
            {
                var btnRt = _continueButton.transform as RectTransform;
                if (btnRt != null)
                    btnRt.anchoredPosition = new Vector2(0f, TextPadBottom);
            }

            _plate.anchorMin = new Vector2(0.5f, 0.5f);
            _plate.anchorMax = new Vector2(0.5f, 0.5f);
            _plate.pivot = new Vector2(0.5f, 0.5f);

            var y = 30f;
            switch (anchor)
            {
                case TutorialPlateAnchor.BelowHighlight:
                    y = -260f;
                    break;
                case TutorialPlateAnchor.AboveHighlight:
                    y = 220f;
                    break;
                default:
                    y = highlightTargets != null && highlightTargets.Count > 0 ? -20f : 30f;
                    break;
            }

            _plate.anchoredPosition = new Vector2(0f, y);
        }

        static void SetDimEdge(Image img, float xMin, float xMax, float yMin, float yMax)
        {
            if (img == null)
                return;

            var rt = img.rectTransform;
            var empty = xMax <= xMin || yMax <= yMin;
            img.gameObject.SetActive(!empty);
            if (empty)
                return;

            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        void OnContinueClicked()
        {
            var cb = _onContinue;
            Hide();
            cb?.Invoke();
        }
    }
}
