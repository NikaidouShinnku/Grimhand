using System.Collections.Generic;

namespace Grimhand.Battle.Consumables
{
    public static class ConsumableDatabase
    {
        static readonly Dictionary<string, ConsumableDefinition> ById = Build();

        public static IReadOnlyCollection<ConsumableDefinition> All => ById.Values;

        public static bool TryGet(string consumableId, out ConsumableDefinition definition) =>
            ById.TryGetValue(consumableId, out definition);

        static Dictionary<string, ConsumableDefinition> Build()
        {
            return new Dictionary<string, ConsumableDefinition>
            {
                [ConsumableIds.SmallHealingPotion] = Def(
                    ConsumableIds.SmallHealingPotion,
                    "小治疗药水",
                    "回复指定角色 15 HP",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.HealSingle,
                    15),
                [ConsumableIds.LargeHealingPotion] = Def(
                    ConsumableIds.LargeHealingPotion,
                    "大治疗药水",
                    "回复全队 10 HP",
                    ConsumableTargetKind.None,
                    ConsumableEffectKind.HealTeam,
                    10),
                [ConsumableIds.StrengthPotion] = Def(
                    ConsumableIds.StrengthPotion,
                    "力量药剂",
                    "指定角色本场战斗 ATK+3",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.BattleAttackBonus,
                    3),
                [ConsumableIds.IronskinPotion] = Def(
                    ConsumableIds.IronskinPotion,
                    "铁壁药剂",
                    "指定角色本场战斗 DEF+3",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.BattleDefenseBonus,
                    3),
                [ConsumableIds.SpringBottle] = Def(
                    ConsumableIds.SpringBottle,
                    "泉水瓶",
                    "回复指定角色 15 HP",
                    ConsumableTargetKind.SingleAlly,
                    ConsumableEffectKind.HealSingle,
                    15),
                [ConsumableIds.MirrorShard] = Def(
                    ConsumableIds.MirrorShard,
                    "镜之碎片",
                    "复制上一张打出的攻击牌效果（一次性）",
                    ConsumableTargetKind.MirrorAttack,
                    ConsumableEffectKind.MirrorLastAttack,
                    0),
                [ConsumableIds.ScrollPage] = Def(
                    ConsumableIds.ScrollPage,
                    "古卷残页",
                    "本回合能量 +2",
                    ConsumableTargetKind.None,
                    ConsumableEffectKind.EnergyThisTurn,
                    2),
                [ConsumableIds.SmokeBomb] = Def(
                    ConsumableIds.SmokeBomb,
                    "烟雾弹",
                    "本回合所有角色闪避率 +50%",
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
            int value) =>
            new()
            {
                Id = id,
                DisplayName = name,
                Description = description,
                TargetKind = target,
                EffectKind = effect,
                Value = value
            };
    }
}
