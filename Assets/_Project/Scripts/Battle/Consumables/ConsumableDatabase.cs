using System.Collections.Generic;

namespace Grimhand.Battle.Consumables
{
    public static class ConsumableDatabase
    {
        static readonly Dictionary<string, ConsumableDefinition> ById = Build();

        public static IReadOnlyCollection<ConsumableDefinition> All => ById.Values;

        public static bool TryGet(string consumableId, out ConsumableDefinition definition) =>
            ById.TryGetValue(consumableId, out definition);

        public static bool CanAppearInRewardPool(string consumableId) =>
            TryGet(consumableId, out var definition) && !definition.EventOnly;

        public static void CollectRewardPoolIds(ICollection<string> pool)
        {
            if (pool == null)
                return;

            foreach (var consumable in All)
            {
                if (!consumable.EventOnly)
                    pool.Add(consumable.Id);
            }
        }

        static Dictionary<string, ConsumableDefinition> Build()
        {
            return new Dictionary<string, ConsumableDefinition>
            {
                [ConsumableIds.SmallHealingPotion] = Def(
                    ConsumableIds.SmallHealingPotion,
                    "小治疗药水",
                    "回复该角色15 HP（含遗物治疗加成）",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.HealSingle,
                    15),
                [ConsumableIds.LargeHealingPotion] = Def(
                    ConsumableIds.LargeHealingPotion,
                    "大治疗药水",
                    "全队存活成员各回复10 HP",
                    ConsumableTargetKind.None,
                    ConsumableEffectKind.HealTeam,
                    10),
                [ConsumableIds.StrengthPotion] = Def(
                    ConsumableIds.StrengthPotion,
                    "力量药剂",
                    "本回合该角色ATK+30%",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.TurnAttackBonusPercent,
                    30),
                [ConsumableIds.IronskinPotion] = Def(
                    ConsumableIds.IronskinPotion,
                    "铁壁药剂",
                    "本回合该角色DEF+30%",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.TurnDefenseBonusPercent,
                    30),
                [ConsumableIds.SpringBottle] = Def(
                    ConsumableIds.SpringBottle,
                    "泉水瓶",
                    "回复该角色15 HP",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.HealSingle,
                    15,
                    eventOnly: true),
                [ConsumableIds.MirrorShard] = Def(
                    ConsumableIds.MirrorShard,
                    "镜之碎片",
                    "复制上一回合最后打出的己方攻击牌，由原出牌者再执行一次完整效果",
                    ConsumableTargetKind.MirrorAttack,
                    ConsumableEffectKind.MirrorLastAttack,
                    0,
                    eventOnly: true),
                [ConsumableIds.ScrollPage] = Def(
                    ConsumableIds.ScrollPage,
                    "古卷残页",
                    "本回合能量+2（不超过能量上限）",
                    ConsumableTargetKind.None,
                    ConsumableEffectKind.EnergyThisTurn,
                    2,
                    eventOnly: true),
                [ConsumableIds.SmokeBomb] = Def(
                    ConsumableIds.SmokeBomb,
                    "烟雾弹",
                    "本回合所有角色（含敌我）闪避率+50%",
                    ConsumableTargetKind.None,
                    ConsumableEffectKind.DodgeAllThisTurn,
                    50)
            };
        }

        static ConsumableDefinition Def(
            string id,
            string name,
            string description,
            ConsumableTargetKind target,
            ConsumableEffectKind effect,
            int value,
            bool eventOnly = false) =>
            new()
            {
                Id = id,
                DisplayName = name,
                Description = description,
                TargetKind = target,
                EffectKind = effect,
                Value = value,
                EventOnly = eventOnly
            };
    }
}
