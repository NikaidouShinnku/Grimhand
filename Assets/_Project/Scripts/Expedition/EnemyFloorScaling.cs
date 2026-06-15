using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>敌人按远征层数缩放（v2 策划表）。</summary>
    public static class EnemyFloorScaling
    {
        public const float HpGrowthPerFloor = 0.05f;
        public const float AtkGrowthPerFloor = 0.03f;
        public const float DefGrowthPerFloor = 0.02f;

        public static void Apply(CombatantConfig combatant, int floor, BattleRng rng)
        {
            // 层数加成仅作用于敌方；玩家只受等级、遗物、事件等影响。
            if (combatant == null || combatant.Team != TeamSide.Enemy || floor <= 1)
                return;

            var tiers = floor - 1;
            var hpMult = 1f + HpGrowthPerFloor * tiers;
            var atkMult = 1f + AtkGrowthPerFloor * tiers;
            var defMult = 1f + DefGrowthPerFloor * tiers;
            var variance = rng != null ? rng.NextInt(90, 111) / 100f : 1f;

            combatant.MaxHp = Scale(combatant.MaxHp, hpMult * variance);
            combatant.BaseAttack = Scale(combatant.BaseAttack, atkMult * variance);
            combatant.BaseDefense = Scale(combatant.BaseDefense, defMult * variance);
        }

        static int Scale(int baseValue, float multiplier)
        {
            if (baseValue <= 0)
                return baseValue;

            return System.Math.Max(1, (int)System.Math.Round(baseValue * multiplier, System.MidpointRounding.AwayFromZero));
        }
    }
}
