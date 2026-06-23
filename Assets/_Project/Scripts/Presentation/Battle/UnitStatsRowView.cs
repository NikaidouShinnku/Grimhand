using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class UnitStatsRowView : MonoBehaviour
    {
        const int IconSize = 28;
        const int FontSize = 18;
        const int HpChipWidth = 120;
        const int ArmChipWidth = 64;
        const int StatChipWidth = 72;
        const int ChipHeight = 32;

        struct StatChip
        {
            public GameObject Root;
            public Image Icon;
            public Text Value;
        }

        StatChip _hp;
        StatChip _arm;
        StatChip _atk;
        StatChip _spd;
        bool _built;

        public void Refresh(
            CombatantState unit,
            BattleUiIconCatalogSO icons,
            bool hpOnly = true,
            int? hpOverride = null,
            int? maxHpOverride = null,
            int? blockOverride = null,
            int pendingIronWallAttackBonus = 0)
        {
            EnsureBuilt();

            if (unit == null)
            {
                SetChipVisible(_hp, false);
                SetChipVisible(_arm, false);
                SetChipVisible(_atk, false);
                SetChipVisible(_spd, false);
                gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            var hp = hpOverride ?? unit.Hp;
            var maxHp = maxHpOverride ?? unit.MaxHp;
            SetChip(_hp, icons?.HpIcon, $"{hp}/{maxHp}");

            var block = blockOverride ?? unit.Block;
            var ironWallPending = pendingIronWallAttackBonus > 0
                ? pendingIronWallAttackBonus
                : unit.TalentIronWallPendingDamageBonus;
            var showIronWallPending = ironWallPending > 0;
            var showArmor = !showIronWallPending && block >= 1;
            SetChipVisible(_arm, showArmor);
            if (showArmor)
                SetChip(_arm, icons?.ArmorIcon, block.ToString());

            var showAttackChip = !hpOnly || showIronWallPending;
            SetChipVisible(_atk, showAttackChip);
            SetChipVisible(_spd, !hpOnly);

            if (showAttackChip)
            {
                var attackText = showIronWallPending
                    ? $"+{ironWallPending}"
                    : unit.Attack.ToString();
                SetChip(_atk, icons?.AttackIcon, attackText);
            }

            if (!hpOnly)
                SetChip(_spd, icons?.SpeedIcon, Grimhand.Battle.Rules.StatusRules.GetEffectiveSpeed(unit).ToString());
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            ClearStaleLayout();

            _built = true;

            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _hp = CreateChip("HP", HpChipWidth);
            _arm = CreateChip("ARM", ArmChipWidth);
            _atk = CreateChip("ATK", StatChipWidth);
            _spd = CreateChip("SPD", StatChipWidth);
        }

        void ClearStaleLayout()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            foreach (var fitter in GetComponents<ContentSizeFitter>())
                Destroy(fitter);

            foreach (var group in GetComponents<LayoutGroup>())
                Destroy(group);
        }

        StatChip CreateChip(string label, int width)
        {
            var root = new GameObject(label, typeof(RectTransform));
            root.transform.SetParent(transform, false);

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(width, ChipHeight);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(2, 4, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = IconSize;
            iconLe.preferredHeight = IconSize;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = FontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            var textLe = textGo.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;
            textLe.minWidth = 20;

            return new StatChip { Root = root, Icon = icon, Value = text };
        }

        static void SetChip(StatChip chip, Sprite icon, string value)
        {
            if (chip.Value != null)
                chip.Value.text = value;

            if (chip.Icon == null)
                return;

            chip.Icon.sprite = icon;
            chip.Icon.enabled = icon != null;
            chip.Icon.color = Color.white;
        }

        static void SetChipVisible(StatChip chip, bool visible)
        {
            if (chip.Root != null)
                chip.Root.SetActive(visible);
        }
    }
}
