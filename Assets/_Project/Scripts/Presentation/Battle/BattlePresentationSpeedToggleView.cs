using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>右上角切换战斗结算动画倍速（idle 不受影响）。</summary>
    [DisallowMultipleComponent]
    public sealed class BattlePresentationSpeedToggleView : MonoBehaviour
    {
        Button _button;
        Text _label;
        Image _background;

        public void EnsureCreated(Transform hudRoot)
        {
            if (_button != null || hudRoot == null)
                return;

            var go = new GameObject("PresentationSpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(hudRoot, false);

            _background = go.GetComponent<Image>();
            _background.color = NormalColor;
            _background.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 22;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _label.raycastTarget = false;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _button = go.GetComponent<Button>();
            _button.targetGraphic = _background;
            _button.onClick.AddListener(BattlePresentationSpeed.Toggle);

            BattleUiLayoutRuntimeFix.LayoutPresentationSpeedButton(go.GetComponent<RectTransform>());
            Refresh();
            BattlePresentationSpeed.Changed += Refresh;
        }

        void OnDestroy()
        {
            BattlePresentationSpeed.Changed -= Refresh;
        }

        void Refresh()
        {
            if (_label == null || _background == null)
                return;

            _label.text = BattlePresentationSpeed.IsFast ? "▶▶" : "▶";
            _background.color = BattlePresentationSpeed.IsFast ? FastColor : NormalColor;
        }

        static readonly Color NormalColor = new(0.14f, 0.15f, 0.2f, 0.96f);
        static readonly Color FastColor = new(0.28f, 0.22f, 0.1f, 0.96f);
    }
}
