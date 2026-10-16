using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>灵能预知：检视牌库顶最多 3 张，多选弃置后确认，其余回库顶。</summary>
    public sealed class PsionicScryOverlayView : MonoBehaviour
    {
        const int SortOrder = 520;
        const float CardScale = 1.05f;

        BattleSession _session;
        BattleUiIconCatalogSO _uiIcons;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        GameObject _root;
        RectTransform _choiceRow;
        Text _hintText;
        Button _confirmButton;
        readonly List<GameObject> _dynamicObjects = new();
        readonly HashSet<int> _selectedIds = new();
        readonly List<int> _orderedIds = new();
        bool _built;

        public void Initialize(
            BattleSession session,
            Transform parent,
            BattleUiIconCatalogSO uiIcons,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _uiIcons = uiIcons;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session?.Engine?.State == null)
            {
                SetVisible(false);
                return;
            }

            var state = _session.Engine.State;
            if (!state.AwaitingPsionicScry || state.PendingPsionicScryCards.Count == 0)
            {
                _selectedIds.Clear();
                _orderedIds.Clear();
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.transform.SetAsLastSibling();
            RebuildCards(state);
            UpdateHint();
        }

        void RebuildCards(BattleState state)
        {
            ClearDynamic();
            if (_cardPrefab == null)
                return;

            _orderedIds.RemoveAll(id => !state.PendingPsionicScryCards.Exists(c => c.InstanceId == id));
            foreach (var pending in state.PendingPsionicScryCards)
                if (pending != null && !_orderedIds.Contains(pending.InstanceId))
                    _orderedIds.Add(pending.InstanceId);
            var keptIndex = 0;
            for (var i = 0; i < _orderedIds.Count; i++)
            {
                var card = state.PendingPsionicScryCards.Find(c => c.InstanceId == _orderedIds[i]);
                if (card == null)
                    continue;

                var selected = _selectedIds.Contains(card.InstanceId);
                var holder = new GameObject($"Scry_{card.InstanceId}", typeof(RectTransform), typeof(Image));
                holder.transform.SetParent(_choiceRow, false);
                var rt = holder.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(260f, 400f);
                holder.GetComponent<Image>().color = selected
                    ? new Color(0.35f, 0.28f, 0.12f, 0.95f)
                    : new Color(0.14f, 0.16f, 0.22f, 0.92f);

                var cardGo = Instantiate(_cardPrefab.gameObject, holder.transform);
                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = new Vector2(0f, 24f);
                cardRt.localScale = Vector3.one * CardScale;

                var visual = CardVisualResolver.Resolve(card, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(card, _definitions);
                var cardView = cardGo.GetComponent<CardView>();
                var instanceId = card.InstanceId;
                CardView.ConfigureForRewardPresentation(cardView, CardScale);
                cardView.BindWithCard(
                    card,
                    visual,
                    selected: selected,
                    polluted: false,
                    interactable: true,
                    orderBadge: selected ? "弃" : $"顶{++keptIndex}",
                    statsLine: statsLine,
                    uiIcons: _uiIcons,
                    characterVisuals: _characterVisuals,
                    onClick: _ => ToggleSelect(instanceId),
                    onHoverEnter: null,
                    onHoverExit: null);

                var left = CreateButton(holder.transform, "←", new Vector2(0.25f, 0.04f), new Vector2(80f, 34f), () => MoveCard(instanceId, -1));
                var right = CreateButton(holder.transform, "→", new Vector2(0.75f, 0.04f), new Vector2(80f, 34f), () => MoveCard(instanceId, 1));
                left.interactable = i > 0;
                right.interactable = i < _orderedIds.Count - 1;
                _dynamicObjects.Add(holder);
            }
        }

        void ToggleSelect(int instanceId)
        {
            if (!_selectedIds.Add(instanceId))
                _selectedIds.Remove(instanceId);

            if (_session?.Engine?.State != null)
                RebuildCards(_session.Engine.State);
            UpdateHint();
        }

        void UpdateHint()
        {
            if (_hintText == null)
                return;

            _hintText.text = $"点选弃置（已选{_selectedIds.Count}张，可不选）；用←→调整顺序。未弃置牌从左到右回库顶，最左先抽。";
        }

        void MoveCard(int instanceId, int direction)
        {
            var index = _orderedIds.IndexOf(instanceId);
            var next = index + direction;
            if (index < 0 || next < 0 || next >= _orderedIds.Count) return;
            (_orderedIds[index], _orderedIds[next]) = (_orderedIds[next], _orderedIds[index]);
            if (_session?.Engine?.State != null) RebuildCards(_session.Engine.State);
        }

        void OnConfirm()
        {
            if (_session == null)
                return;

            var ids = new List<int>(_selectedIds);
            var order = new List<int>(_orderedIds);
            _selectedIds.Clear();
            _orderedIds.Clear();
            _session.ConfirmPsionicScry(ids, order);
        }

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;
            _root = new GameObject("PsionicScryOverlay", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            _root.GetComponent<Image>().raycastTarget = true;
            var canvas = _root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.12f, 0.16f);
            panelRt.anchorMax = new Vector2(0.88f, 0.84f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.12f, 0.98f);

            var title = CreateText(panel.transform, "灵能预知", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(title.rectTransform, 0.88f, 0.97f);
            title.color = new Color(0.85f, 0.78f, 1f, 1f);

            _hintText = CreateText(panel.transform, "", 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_hintText.rectTransform, 0.78f, 0.87f);

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(panel.transform, false);
            _choiceRow = rowGo.GetComponent<RectTransform>();
            AnchorBand(_choiceRow, 0.16f, 0.76f);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            _confirmButton = CreateButton(panel.transform, "确认", new Vector2(0.5f, 0.07f), new Vector2(220f, 52f), OnConfirm);
            _root.SetActive(false);
        }

        static Text CreateText(Transform parent, string content, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 size, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.4f, 0.98f);
            var text = CreateText(go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        static void AnchorBand(RectTransform rt, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0.05f, minY);
            rt.anchorMax = new Vector2(0.95f, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
