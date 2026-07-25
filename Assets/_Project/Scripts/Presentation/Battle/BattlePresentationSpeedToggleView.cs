using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>右上角切换战斗结算动画倍速（idle 不受影响）。</summary>
    [DisallowMultipleComponent]
    public sealed class BattlePresentationSpeedToggleView : MonoBehaviour
    {
        static readonly Color NormalTint = Color.white;
        static readonly Color FastTint = new Color(1f, 0.82f, 0.28f, 1f);

        Button _button;
        Image _background;
        Outline _outline;
        Text _fastLabel;
        BattleUiIconCatalogSO _icons;

        public void EnsureCreated(Transform hudRoot, BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                _icons = icons;

            if (_button != null || hudRoot == null)
            {
                if (_button != null)
                {
                    BattleButtonPressFeedback.Apply(_button);
                    BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(_button.transform as RectTransform);
                }

                ApplyVisual();
                return;
            }

            var go = new GameObject("PresentationSpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(hudRoot, false);

            _background = go.GetComponent<Image>();
            _background.raycastTarget = true;
            _background.preserveAspect = true;
            _background.type = Image.Type.Simple;

            _button = go.GetComponent<Button>();
            _button.targetGraphic = _background;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClicked);
            BattleButtonPressFeedback.Apply(_button);

            BattleUiLayoutRuntimeFix.LayoutPresentationSpeedButton(go.GetComponent<RectTransform>());
            BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(go.GetComponent<RectTransform>());
            EnsureFastLabel(go.transform);
            ApplyVisual();
            BattlePresentationSpeed.Changed += ApplyVisual;
        }

        void OnClicked()
        {
            BattlePresentationSpeed.Toggle();
        }

        void OnDestroy()
        {
            BattlePresentationSpeed.Changed -= ApplyVisual;
        }

        void EnsureFastLabel(Transform host)
        {
            if (_fastLabel != null || host == null)
                return;

            var labelGo = new GameObject("FastLabel", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(host, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.55f, 0.02f);
            rt.anchorMax = new Vector2(0.98f, 0.42f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _fastLabel = labelGo.GetComponent<Text>();
            _fastLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fastLabel.fontSize = 22;
            _fastLabel.fontStyle = FontStyle.Bold;
            _fastLabel.alignment = TextAnchor.LowerRight;
            _fastLabel.color = new Color(0.15f, 0.1f, 0.02f, 1f);
            _fastLabel.text = "×2";
            _fastLabel.raycastTarget = false;
        }

        void ApplyVisual()
        {
            if (_background == null)
                return;

            if (_fastLabel == null && _button != null)
                EnsureFastLabel(_button.transform);

            var fast = BattlePresentationSpeed.IsFast;
            var sprite = _icons != null ? _icons.UiChangeGamespeedButton : null;
            if (sprite != null)
            {
                _background.sprite = sprite;
                _background.color = fast ? FastTint : NormalTint;
            }
            else
            {
                _background.sprite = null;
                _background.color = fast
                    ? new Color(0.72f, 0.52f, 0.12f, 0.98f)
                    : new Color(0.14f, 0.15f, 0.2f, 0.96f);
            }

            if (_outline == null)
                _outline = _background.GetComponent<Outline>() ?? _background.gameObject.AddComponent<Outline>();

            _outline.enabled = fast;
            _outline.effectColor = new Color(1f, 0.9f, 0.35f, 0.95f);
            _outline.effectDistance = new Vector2(3f, -3f);

            if (_fastLabel != null)
                _fastLabel.gameObject.SetActive(fast);

            if (_button != null)
                _button.interactable = true;
        }
    }
}
