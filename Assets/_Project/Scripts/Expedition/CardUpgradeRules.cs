using System;
using Grimhand.Battle.Model;
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

        public static void ApplyToTemplate(CardTemplate template, int upgradeLevel)
        {
            if (template == null || upgradeLevel <= 0)
                return;

            if (!CardUpgradeCatalog.TryGetByDisplayName(template.DisplayName, out var spec))
                return;

            var dmg = spec.DamagePerLevel * upgradeLevel;
            var block = spec.BlockPerLevel * upgradeLevel;
            var heal = spec.HealPerLevel * upgradeLevel;
            var poison = spec.PoisonStacksPerLevel * upgradeLevel;
            var slow = spec.SlowStacksPerLevel * upgradeLevel;
            var costReduce = spec.CostReductionPerLevel * upgradeLevel;

            if (costReduce > 0)
                template.Cost = Math.Max(0, template.Cost - costReduce);

            foreach (var action in template.Actions)
            {
                switch (action.Type)
                {
                    case EffectActionType.DealDamage when dmg > 0:
                        action.Value += dmg;
                        break;
                    case EffectActionType.GainBlock when block > 0:
                        action.Value += block;
                        break;
                    case EffectActionType.Heal when heal > 0:
                        action.Value += heal;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "poison" && poison > 0:
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "slow" && slow > 0:
                        action.Stacks += slow;
                        break;
                }
            }
        }

        public static void ApplyToTemplate(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null)
                return;

            var level = GetLevel(member, template.DeckInstanceId);
            ApplyToTemplate(template, level);
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
