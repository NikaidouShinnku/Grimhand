using Grimhand.Battle.Consumables;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class ConsumableReplaceOverlayView : MonoBehaviour
    {
        BattleSession _session;
        ConsumableVisualCatalogSO _consumableCatalog;
        RectTransform _root;
        Text _bodyText;
        RectTransform _slotRow;
        bool _built;

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
            ClearSlots();
            _bodyText.text =
                $"消耗品栏已满（{ConsumableInventory.MaxSlots}/{ConsumableInventory.MaxSlots}）\n" +
                $"获得：{def?.DisplayName ?? offerId}\n请选择要替换的栏位，或放弃新物品。";

            ConsumableInventory.EnsureInitialized(_session.Expedition.Run.ConsumableSlots);
            var slots = _session.Expedition.Run.ConsumableSlots;
            for (var i = 0; i < ConsumableInventory.MaxSlots; i++)
            {
                var index = i;
                var slotId = slots[i];
                ConsumableDatabase.TryGet(slotId, out var occupied);
                var label = string.IsNullOrEmpty(slotId)
                    ? $"栏位 {i + 1}\n（空）"
                    : $"栏位 {i + 1}\n{occupied?.DisplayName ?? slotId}";
                AddSlotButton(label, () => _session.ReplaceConsumableSlot(index));
            }

            AddSlotButton("放弃新物品", () => _session.AbandonConsumableOffer());
        }

        void AddSlotButton(string label, System.Action onClick)
        {
            var go = new GameObject("SlotChoice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_slotRow, false);
            go.GetComponent<LayoutElement>().preferredHeight = 64f;
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.96f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        void ClearSlots()
        {
            if (_slotRow == null)
                return;

            foreach (Transform child in _slotRow)
                Destroy(child.gameObject);
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;
            var go = new GameObject("ConsumableReplaceOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520f, 460f);
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.08f, 0.52f);
            bodyRt.anchorMax = new Vector2(0.92f, 0.92f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 18;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = Color.white;

            var rowGo = new GameObject("Slots", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            _slotRow = rowGo.GetComponent<RectTransform>();
            _slotRow.anchorMin = new Vector2(0.08f, 0.06f);
            _slotRow.anchorMax = new Vector2(0.92f, 0.48f);
            _slotRow.offsetMin = Vector2.zero;
            _slotRow.offsetMax = Vector2.zero;
            var layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            go.SetActive(false);
        }
    }
}
