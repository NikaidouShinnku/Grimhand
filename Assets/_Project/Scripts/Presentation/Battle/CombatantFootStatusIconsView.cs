using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>立绘脚线下方：v0.8 状态图标 + 层数。</summary>
    [DisallowMultipleComponent]
    public sealed class CombatantFootStatusIconsView : MonoBehaviour
    {
        const float IconSize = 22f;
        const float StackFontSize = 13f;
        const float SlotWidth = 26f;

        static readonly string[] DisplayOrder =
        {
            StatusCatalog.AttackUpPercent,
            StatusCatalog.AttackUp,
            StatusCatalog.DamageUp,
            StatusCatalog.Weaken,
            StatusCatalog.AttackDown,
            StatusCatalog.Vulnerable,
            StatusCatalog.DamageReduction,
            StatusCatalog.DefenseUpPercent,
            StatusCatalog.DefenseUp,
            StatusCatalog.ArmorUp,
            StatusCatalog.DefenseDownPercent,
            StatusCatalog.ArmorDown,
            StatusCatalog.Slow,
            StatusCatalog.Poison,
            StatusCatalog.Burn
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
            if (_row != null || footRoot == null)
                return;

            var go = new GameObject("StatusIconsRow", typeof(RectTransform));
            go.transform.SetParent(footRoot, false);
            _row = go.GetComponent<RectTransform>();
            _row.anchorMin = new Vector2(0.5f, 0f);
            _row.anchorMax = new Vector2(0.5f, 0f);
            _row.pivot = new Vector2(0.5f, 0f);
            _row.anchoredPosition = new Vector2(0f, 50f);
            _row.sizeDelta = new Vector2(220f, IconSize + 4f);
        }

        public void Refresh(CombatantState unit, BattleUiIconCatalogSO icons)
        {
            if (_row == null)
                return;

            var visible = CollectVisible(unit);
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
                slot.Icon.sprite = ResolveSprite(icons, entry.StatusId);
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Stacks.text = entry.Stacks > 1 ? entry.Stacks.ToString() : "";
            }

            LayoutSlots(visible.Count);
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

            var shownGroups = new HashSet<string>();

            foreach (var statusId in DisplayOrder)
            {
                var group = GroupFor(statusId);
                if (group != null && shownGroups.Contains(group))
                    continue;

                foreach (var status in unit.Statuses)
                {
                    if (status == null || status.StatusId != statusId || status.Stacks <= 0)
                        continue;

                    list.Add(new VisibleStatus { StatusId = statusId, Stacks = status.Stacks });
                    if (group != null)
                        shownGroups.Add(group);
                    break;
                }
            }

            return list;
        }

        static string GroupFor(string statusId)
        {
            return statusId switch
            {
                StatusCatalog.AttackUpPercent => "dmg_up",
                StatusCatalog.AttackUp => "dmg_up",
                StatusCatalog.DamageUp => "dmg_up",
                StatusCatalog.Weaken => "dmg_down",
                StatusCatalog.AttackDown => "dmg_down",
                StatusCatalog.Vulnerable => "def_down",
                StatusCatalog.DamageReduction => "def_up",
                StatusCatalog.DefenseUpPercent => "armor_up",
                StatusCatalog.DefenseUp => "armor_up",
                StatusCatalog.ArmorUp => "armor_up",
                StatusCatalog.DefenseDownPercent => "armor_down",
                StatusCatalog.ArmorDown => "armor_down",
                StatusCatalog.Slow => "spd_down",
                StatusCatalog.Poison => "poison",
                StatusCatalog.Burn => "burn",
                _ => statusId
            };
        }

        static Sprite ResolveSprite(BattleUiIconCatalogSO icons, string statusId)
        {
            if (icons == null)
                return null;

            return statusId switch
            {
                StatusCatalog.AttackUpPercent => icons.StatusDamageUp,
                StatusCatalog.AttackUp => icons.StatusDamageUp,
                StatusCatalog.DamageUp => icons.StatusDamageUp,
                StatusCatalog.Weaken => icons.StatusDamageDown,
                StatusCatalog.AttackDown => icons.StatusDamageDown,
                StatusCatalog.Vulnerable => icons.StatusDefenseDown,
                StatusCatalog.DamageReduction => icons.StatusDefenseUp,
                StatusCatalog.DefenseUpPercent => icons.StatusArmorAcqUp,
                StatusCatalog.DefenseUp => icons.StatusArmorAcqUp,
                StatusCatalog.ArmorUp => icons.StatusArmorAcqUp,
                StatusCatalog.DefenseDownPercent => icons.StatusArmorAcqDown,
                StatusCatalog.ArmorDown => icons.StatusArmorAcqDown,
                StatusCatalog.Slow => icons.StatusSpdDown,
                StatusCatalog.Poison => icons.StatusPoisoning,
                StatusCatalog.Burn => icons.StatusBurning,
                _ => null
            };
        }

        void EnsureSlotCount(int count)
        {
            while (_slots.Count < count)
            {
                var root = new GameObject($"StatusSlot{_slots.Count}", typeof(RectTransform));
                root.transform.SetParent(_row, false);
                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(SlotWidth, IconSize + 6f);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(root.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                var icon = iconGo.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                var textGo = new GameObject("Stacks", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(root.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(1f, 0f);
                textRt.anchorMax = new Vector2(1f, 0f);
                textRt.pivot = new Vector2(0f, 0f);
                textRt.anchoredPosition = new Vector2(-2f, 0f);
                textRt.sizeDelta = new Vector2(16f, 14f);
                var text = textGo.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = (int)StackFontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.LowerRight;
                text.color = Color.white;
                text.raycastTarget = false;
                var outline = textGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);

                _slots.Add(new StatusSlot { Root = root, Icon = icon, Stacks = text });
            }
        }

        void LayoutSlots(int count)
        {
            if (_row == null || count <= 0)
                return;

            var totalWidth = count * SlotWidth;
            var startX = -totalWidth * 0.5f + SlotWidth * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var rt = _slots[i].Root.transform as RectTransform;
                if (rt == null)
                    continue;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * SlotWidth, 0f);
            }

            _row.sizeDelta = new Vector2(totalWidth, IconSize + 6f);
        }
    }
}
