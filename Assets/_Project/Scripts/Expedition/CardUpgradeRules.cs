using System;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>卡牌升级：按 Excel「可升级次数 / 每次升级效果」逐级调整数值；每张牌实例独立计费。</summary>
    public static class CardUpgradeRules
    {
        public static int GetLevel(PartyMemberSnapshot member, string deckInstanceId)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId))
                return 0;

            return member.CardUpgradeLevels.TryGetValue(deckInstanceId, out var level) ? level : 0;
        }

        public static int GetMaxLevel(string displayName) =>
            CardUpgradeCatalog.TryGetByDisplayName(displayName, out var spec) ? spec.MaxUpgrades : 0;

        public static int GetUpgradeXpCost(string displayName) =>
            CardUpgradeCatalog.GetXpCostPerLevel(displayName);

        public static bool CanUpgrade(PartyMemberSnapshot member, string deckInstanceId, string displayName)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId))
                return false;

            return CardUpgradeCatalog.CanUpgrade(displayName, GetLevel(member, deckInstanceId));
        }

        /// <summary>
        /// 事件/祭坛强化列表：满级或目录中不可升级的卡不出现。
        /// 同时参考模板上已烘焙的 UpgradeLevel，避免字典键漂移时满级卡仍可选。
        /// </summary>
        public static bool CanUpgrade(PartyMemberSnapshot member, CardTemplate template)
        {
            if (member == null || template == null || string.IsNullOrEmpty(template.DisplayName))
                return false;

            if (!CardUpgradeCatalog.TryGetByDisplayName(template.DisplayName, out var spec)
                || spec.MaxUpgrades <= 0)
                return false;

            var level = GetLevel(member, template.DeckInstanceId);
            if (template.UpgradeLevel > level)
                level = template.UpgradeLevel;

            return level < spec.MaxUpgrades;
        }

        public static bool TryUpgradeLevel(
            PartyMemberSnapshot member,
            string deckInstanceId,
            string displayName,
            int levels = 1)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId) || levels <= 0)
                return false;

            if (!CardUpgradeCatalog.TryGetByDisplayName(displayName, out var spec))
                return false;

            var current = GetLevel(member, deckInstanceId);
            var next = Math.Min(spec.MaxUpgrades, current + levels);
            if (next <= current)
                return false;

            member.CardUpgradeLevels[deckInstanceId] = next;
            return true;
        }

        public static void ApplyToTemplate(CardTemplate template, int upgradeLevel) =>
            Grimhand.Battle.Rules.CardInstanceUpgradeApplier.ApplyToTemplate(template, upgradeLevel);

        public static void ApplyToTemplate(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null)
                return;

            var level = GetLevel(member, template.DeckInstanceId);
            template.UpgradeLevel = level;
            ApplyToTemplate(template, level);
            ApplyFlatDamageBonus(template, member);
        }

        public static string FormatDisplayName(string displayName, int upgradeLevel) =>
            upgradeLevel > 0 && !string.IsNullOrEmpty(displayName)
                ? $"{displayName}+{upgradeLevel}"
                : displayName ?? "";

        public static void ApplyFlatDamageBonus(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null || string.IsNullOrEmpty(template.DeckInstanceId))
                return;

            if (!member.CardFlatDamageBonuses.TryGetValue(template.DeckInstanceId, out var bonus) || bonus <= 0)
                return;

            foreach (var action in template.Actions)
            {
                if (action.Type == EffectActionType.DealDamage
                    || action.Type == EffectActionType.ApplyConstrict)
                    action.Value += bonus;
            }
        }

        public static bool AddFlatDamageToAttackCard(PartyMemberSnapshot member, string deckInstanceId, int amount = 1)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId) || amount <= 0)
                return false;

            member.CardFlatDamageBonuses.TryGetValue(deckInstanceId, out var current);
            member.CardFlatDamageBonuses[deckInstanceId] = current + amount;
            return true;
        }

        public static string FormatUpgradeSlots(string displayName, int currentLevel)
        {
            var max = GetMaxLevel(displayName);
            if (max <= 0)
                return "";

            var filled = Math.Clamp(currentLevel, 0, max);
            return new string('●', filled) + new string('○', max - filled);
        }
    }
}
