using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class UnitStatsRowView : MonoBehaviour
    {
        const int IconSize = 30;
        const int FontSize = 17;
        const int ChipWidth = 108;
        const int ChipHeight = 34;

        struct StatChip
        {
            public Image Icon;
            public Text Value;
        }

        StatChip _hp;
        StatChip _def;
        StatChip _atk;
        StatChip _spd;
        bool _built;

        public void Refresh(CombatantState unit, BattleUiIconCatalogSO icons)
        {
            EnsureBuilt();

            if (unit == null)
            {
                SetChip(_hp, icons?.HpIcon, "—");
                SetChip(_def, icons?.DefenseIcon, "—");
                SetChip(_atk, icons?.AttackIcon, "—");
                SetChip(_spd, icons?.SpeedIcon, "—");
                return;
            }

            SetChip(_hp, icons?.HpIcon, $"{unit.Hp}/{unit.MaxHp}");
            SetChip(_def, icons?.DefenseIcon, unit.Block.ToString());
            SetChip(_atk, icons?.AttackIcon, unit.Attack.ToString());
            SetChip(_spd, icons?.SpeedIcon, StatusRules.GetEffectiveSpeed(unit).ToString());
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;

            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _hp = CreateChip("HP");
            _def = CreateChip("DEF");
            _atk = CreateChip("ATK");
            _spd = CreateChip("SPD");
        }

        StatChip CreateChip(string label)
        {
            var root = new GameObject(label, typeof(RectTransform));
            root.transform.SetParent(transform, false);

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(ChipWidth, ChipHeight);

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
            textLe.minWidth = 36;

            return new StatChip { Icon = icon, Value = text };
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
    }
}
