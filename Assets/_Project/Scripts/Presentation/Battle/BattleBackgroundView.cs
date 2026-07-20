using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>远征战斗洞穴背景：直接复用场景里的 Background 节点，避免被不透明底色盖住。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundView : MonoBehaviour
    {
        static readonly Color FallbackColor = new(0.06f, 0.07f, 0.1f, 1f);
        const float ExpeditionAlpha = 0.88f;

        Image _image;
        Sprite _lastSprite;
        bool _bound;
        bool _visible = true;

        public void EnsureBuilt(Transform parent, Sprite backgroundSprite)
        {
            if (!_bound)
            {
                _bound = true;

                var legacy = parent.Find("ExpeditionBackground");
                if (legacy != null)
                    Destroy(legacy.gameObject);

                var bgTransform = parent.Find("Background");
                if (bgTransform != null)
                    _image = bgTransform.GetComponent<Image>();

                if (_image == null)
                {
                    var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(parent, false);
                    go.transform.SetAsFirstSibling();
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    _image = go.GetComponent<Image>();
                }

                _image.raycastTarget = false;
                _image.preserveAspect = false;
                _image.type = Image.Type.Simple;
            }

            _lastSprite = backgroundSprite;
            if (_visible)
                ApplyExpeditionSprite(_lastSprite);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_image == null)
                return;

            if (visible)
            {
                ApplyExpeditionSprite(_lastSprite);
                return;
            }

            _image.sprite = null;
            _image.color = FallbackColor;
        }

        void ApplyExpeditionSprite(Sprite sprite)
        {
            if (_image == null)
                return;

            if (sprite != null)
            {
                _image.sprite = sprite;
                _image.color = new Color(1f, 1f, 1f, ExpeditionAlpha);
                _image.enabled = true;
                _image.gameObject.SetActive(true);
                return;
            }

            _image.sprite = null;
            _image.color = FallbackColor;
        }
    }
}
