using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>HP 心形行下方：状态图标 + 层数，超宽自动换行并居中。</summary>
    [DisallowMultipleComponent]
    public sealed class CombatantFootStatusIconsView : MonoBehaviour
    {
        const float IconSize = 28f;
        const float StackFontSize = 14f;
        const float ItemSpacing = 4f;
        const float RowSpacing = 3f;
        const float MaxRowWidth = 144f;

        static readonly string[] SortPriority =
        {
            StatusCatalog.Poison,
            StatusCatalog.NecroticPoison,
            StatusCatalog.Burn,
            StatusCatalog.Slow,
            StatusCatalog.ArmorUp,
            StatusCatalog.ArmorDown,
            StatusCatalog.DefenseUp,
            StatusCatalog.DefenseUpPercent,
            StatusCatalog.DefenseDownPercent,
            StatusCatalog.DamageReduction,
            StatusCatalog.Vulnerable,
            StatusCatalog.AttackUpPercent,
            StatusCatalog.AttackUp,
            StatusCatalog.DamageUp,
            StatusCatalog.Weaken,
            StatusCatalog.AttackDown,
            StatusCatalog.Taunt,
            StatusCatalog.Guard,
            StatusCatalog.Ethereal,
            StatusCatalog.EtherealOnNextHit,
            StatusCatalog.ReviveBlessing,
            StatusCatalog.RisingTide,
            StatusCatalog.EbbingTide,
            StatusCatalog.TideLocked,
            StatusCatalog.TideEmpower,
        };

        readonly List<StatusSlot> _slots = new();
        RectTransform _row;

        sealed class StatusSlot
        {
            public GameObject Root;
            public Image Icon;
            public Text Stacks;
        }

        public void EnsureBuilt(RectTransform footRoot)
        {
            if (footRoot == null)
                return;

            if (_row == null)
            {
                var go = new GameObject("StatusIconsRow", typeof(RectTransform));
                go.transform.SetParent(footRoot, false);
                _row = go.GetComponent<RectTransform>();
            }

            _row.anchorMin = new Vector2(0.5f, 0f);
            _row.anchorMax = new Vector2(0.5f, 0f);
            _row.pivot = new Vector2(0.5f, 1f);
            _row.anchoredPosition = new Vector2(0f, -2f);
            _row.sizeDelta = new Vector2(MaxRowWidth, IconSize + 4f);
        }

        public void Refresh(CombatantState unit, BattleUiIconCatalogSO icons) =>
            RefreshInternal(CollectVisible(unit), icons);

        public void Refresh(IReadOnlyList<FootStatusEntry> entries, BattleUiIconCatalogSO icons) =>
            RefreshInternal(CollectVisible(entries), icons);

        void RefreshInternal(IReadOnlyList<VisibleStatus> visible, BattleUiIconCatalogSO icons)
        {
            if (_row == null)
                return;

            EnsureSlotCount(visible.Count);

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i >= visible.Count)
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                var entry = visible[i];
                slot.Root.SetActive(true);
                slot.Icon.sprite = StatusIconSpriteResolver.Resolve(icons, entry.StatusId);
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Stacks.text = FormatStackLabel(entry.StatusId, entry.Stacks);
                var stackWidth = MeasureStackTextWidth(entry.StatusId, entry.Stacks);
                var stackRt = slot.Stacks.rectTransform;
                stackRt.sizeDelta = new Vector2(stackWidth, IconSize);
            }

            LayoutSlots(visible);
            _row.gameObject.SetActive(visible.Count > 0);
        }

        struct VisibleStatus
        {
            public string StatusId;
            public int Stacks;
        }

        static List<VisibleStatus> CollectVisible(CombatantState unit)
        {
            var list = new List<VisibleStatus>();
            if (unit?.Statuses == null)
                return list;

            foreach (var status in unit.Statuses)
            {
                if (status == null || status.Stacks <= 0 || string.IsNullOrEmpty(status.StatusId))
                    continue;

                list.Add(new VisibleStatus
                {
                    StatusId = status.StatusId,
                    Stacks = status.Stacks
                });
            }

            list.Sort(CompareVisible);
            return list;
        }

        static List<VisibleStatus> CollectVisible(IReadOnlyList<FootStatusEntry> entries)
        {
            var list = new List<VisibleStatus>();
            if (entries == null)
                return list;

            foreach (var entry in entries)
            {
                if (entry.Stacks <= 0 || string.IsNullOrEmpty(entry.StatusId))
                    continue;

                list.Add(new VisibleStatus
                {
                    StatusId = entry.StatusId,
                    Stacks = entry.Stacks
                });
            }

            list.Sort(CompareVisible);
            return list;
        }

        static int CompareVisible(VisibleStatus a, VisibleStatus b)
        {
            var rankA = SortRank(a.StatusId);
            var rankB = SortRank(b.StatusId);
            if (rankA != rankB)
                return rankA.CompareTo(rankB);

            var nameA = StatusCatalog.Get(a.StatusId)?.DisplayName ?? a.StatusId;
            var nameB = StatusCatalog.Get(b.StatusId)?.DisplayName ?? b.StatusId;
            return string.CompareOrdinal(nameA, nameB);
        }

        static int SortRank(string statusId)
        {
            for (var i = 0; i < SortPriority.Length; i++)
            {
                if (SortPriority[i] == statusId)
                    return i;
            }

            return SortPriority.Length + 1;
        }

        void EnsureSlotCount(int count)
        {
            while (_slots.Count < count)
            {
                var root = new GameObject($"StatusSlot{_slots.Count}", typeof(RectTransform));
                root.transform.SetParent(_row, false);
                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(IconSize + 28f, IconSize);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(root.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                var icon = iconGo.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                var textGo = new GameObject("Stacks", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(root.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(0f, 0.5f);
                textRt.anchorMax = new Vector2(0f, 0.5f);
                textRt.pivot = new Vector2(0f, 0.5f);
                textRt.anchoredPosition = new Vector2(IconSize + 2f, 0f);
                textRt.sizeDelta = new Vector2(24f, IconSize);
                var text = textGo.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = (int)StackFontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = Color.white;
                text.raycastTarget = false;
                var outline = textGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);

                _slots.Add(new StatusSlot { Root = root, Icon = icon, Stacks = text });
            }
        }

        void LayoutSlots(IReadOnlyList<VisibleStatus> visible)
        {
            if (_row == null || visible == null || visible.Count == 0)
                return;

            var rows = new List<List<int>>();
            var currentRow = new List<int>();
            var currentWidth = 0f;

            for (var i = 0; i < visible.Count; i++)
            {
                var itemWidth = MeasureItemWidth(visible[i].StatusId, visible[i].Stacks);
                if (currentRow.Count > 0 && currentWidth + ItemSpacing + itemWidth > MaxRowWidth)
                {
                    rows.Add(currentRow);
                    currentRow = new List<int>();
                    currentWidth = 0f;
                }

                if (currentRow.Count > 0)
                    currentWidth += ItemSpacing;

                currentRow.Add(i);
                currentWidth += itemWidth;
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            var rowHeight = IconSize + RowSpacing;
            var totalHeight = rows.Count * rowHeight - (rows.Count > 0 ? RowSpacing : 0f);
            var topY = 0f;

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var rowWidth = 0f;
                for (var j = 0; j < row.Count; j++)
                {
                    if (j > 0)
                        rowWidth += ItemSpacing;
                    rowWidth += MeasureItemWidth(visible[row[j]].StatusId, visible[row[j]].Stacks);
                }

                var x = -rowWidth * 0.5f;
                var y = topY - rowIndex * rowHeight;

                for (var j = 0; j < row.Count; j++)
                {
                    var slotIndex = row[j];
                    var slotRt = _slots[slotIndex].Root.transform as RectTransform;
                    if (slotRt == null)
                        continue;

                    var itemWidth = MeasureItemWidth(visible[slotIndex].StatusId, visible[slotIndex].Stacks);
                    slotRt.anchorMin = new Vector2(0.5f, 1f);
                    slotRt.anchorMax = new Vector2(0.5f, 1f);
                    slotRt.pivot = new Vector2(0f, 1f);
                    slotRt.sizeDelta = new Vector2(itemWidth, IconSize);
                    slotRt.anchoredPosition = new Vector2(x, y);
                    x += itemWidth + ItemSpacing;
                }
            }

            _row.sizeDelta = new Vector2(MaxRowWidth, totalHeight);
        }

        static string FormatStackLabel(string statusId, int stacks)
        {
            if (stacks <= 0)
                return "";

            if (statusId == StatusCatalog.DefenseDownPercent
                || statusId == StatusCatalog.DefenseUpPercent
                || statusId == StatusCatalog.AttackUpPercent
                || statusId == StatusCatalog.Vulnerable
                || statusId == StatusCatalog.DamageReduction
                || statusId == StatusCatalog.ArmorDown)
                return $"{stacks}%";

            return $"×{stacks}";
        }

        static float MeasureItemWidth(string statusId, int stacks)
        {
            return IconSize + 2f + MeasureStackTextWidth(statusId, stacks);
        }

        static float MeasureStackTextWidth(string statusId, int stacks)
        {
            var label = FormatStackLabel(statusId, stacks);
            return Mathf.Max(24f, 8f + label.Length * 8f);
        }
    }
}
