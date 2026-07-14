using Grimhand.Battle.Consumables;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class ConsumableReplaceOverlayView : MonoBehaviour
    {
        const float PanelWidth = 980f;
        const float PanelHeight = 420f;
        const float IconSize = 128f;
        const float IconGap = 24f;

        BattleSession _session;
        ConsumableVisualCatalogSO _consumableCatalog;
        RectTransform _root;
        Text _bodyText;
        RectTransform _iconRow;
        RectTransform _footerRow;
        bool _built;
        const int LayoutVersion = 2;
        int _layoutVersion;

        public void Initialize(BattleSession session, Transform parent, ConsumableVisualCatalogSO consumableCatalog)
        {
            _session = session;
            _consumableCatalog = consumableCatalog;
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var offerId = _session.Expedition.Run.PendingConsumableOfferId;
            if (string.IsNullOrEmpty(offerId))
            {
                SetVisible(false);
                return;
            }

            ConsumableDatabase.TryGet(offerId, out var def);
            SetVisible(true);
            _root.SetAsLastSibling();
            ClearChildren(_iconRow);
            ClearChildren(_footerRow);

            _bodyText.text =
                $"消耗品栏已满（{ConsumableInventory.MaxSlots}/{ConsumableInventory.MaxSlots}）\n" +
                $"获得：{def?.DisplayName ?? offerId}　　点击下方图标替换对应道具，或放弃新物品。";

            ConsumableInventory.EnsureInitialized(_session.Expedition.Run.ConsumableSlots);
            var slots = _session.Expedition.Run.ConsumableSlots;
            for (var i = 0; i < ConsumableInventory.MaxSlots; i++)
            {
                var index = i;
                var slotId = i < slots.Count ? slots[i] : "";
                ConsumableDatabase.TryGet(slotId, out var occupied);
                AddIconChoice(
                    slotId,
                    occupied?.DisplayName ?? (string.IsNullOrEmpty(slotId) ? "空栏" : slotId),
                    () => _session.ReplaceConsumableSlot(index));
            }

            FitIconRow();
            AddAbandonButton();
        }

        void AddIconChoice(string consumableId, string displayName, System.Action onClick)
        {
            var go = new GameObject("SlotIcon", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_iconRow, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(IconSize, IconSize + 28f);
            go.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 0.98f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -8f);
            iconRt.sizeDelta = new Vector2(IconSize - 20f, IconSize - 20f);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (!string.IsNullOrEmpty(consumableId))
            {
                icon.sprite = _consumableCatalog?.GetIcon(consumableId);
                icon.color = icon.sprite != null ? Color.white : new Color(0.4f, 0.45f, 0.55f, 1f);
            }
            else
            {
                icon.sprite = null;
                icon.color = new Color(0.2f, 0.22f, 0.28f, 0.9f);
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 4f);
            labelRt.sizeDelta = new Vector2(0f, 26f);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.85f, 0.88f, 0.94f, 1f);
            label.text = displayName;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        void FitIconRow()
        {
            if (_iconRow == null || _iconRow.childCount == 0)
                return;

            var n = _iconRow.childCount;
            var totalW = n * IconSize + Mathf.Max(0, n - 1) * IconGap;
            var startX = -totalW * 0.5f + IconSize * 0.5f;
            for (var i = 0; i < n; i++)
            {
                var child = _iconRow.GetChild(i) as RectTransform;
                if (child == null)
                    continue;
                child.anchoredPosition = new Vector2(startX + i * (IconSize + IconGap), 0f);
            }
        }

        void AddAbandonButton()
        {
            var go = new GameObject("Abandon", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_footerRow, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280f, 52f);
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.28f, 0.16f, 0.16f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "放弃新物品";

            go.GetComponent<Button>().onClick.AddListener(() => _session.AbandonConsumableOffer());
        }

        static void ClearChildren(RectTransform row)
        {
            if (row == null)
                return;

            for (var i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i);
                if (child != null)
                    DestroyImmediate(child.gameObject);
            }
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && _layoutVersion == LayoutVersion && _root != null)
                return;

            if (_root != null)
                DestroyImmediate(_root.gameObject);

            _built = true;
            _layoutVersion = LayoutVersion;
            var go = new GameObject("ConsumableReplaceOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.06f, 0.72f);
            bodyRt.anchorMax = new Vector2(0.94f, 0.94f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 20;
            _bodyText.alignment = TextAnchor.MiddleCenter;
            _bodyText.color = Color.white;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var rowGo = new GameObject("Icons", typeof(RectTransform));
            rowGo.transform.SetParent(panelGo.transform, false);
            _iconRow = rowGo.GetComponent<RectTransform>();
            _iconRow.anchorMin = new Vector2(0.05f, 0.28f);
            _iconRow.anchorMax = new Vector2(0.95f, 0.70f);
            _iconRow.offsetMin = Vector2.zero;
            _iconRow.offsetMax = Vector2.zero;

            var footerGo = new GameObject("Footer", typeof(RectTransform));
            footerGo.transform.SetParent(panelGo.transform, false);
            _footerRow = footerGo.GetComponent<RectTransform>();
            _footerRow.anchorMin = new Vector2(0.05f, 0.06f);
            _footerRow.anchorMax = new Vector2(0.95f, 0.22f);
            _footerRow.offsetMin = Vector2.zero;
            _footerRow.offsetMax = Vector2.zero;

            go.SetActive(false);
        }
    }
}
