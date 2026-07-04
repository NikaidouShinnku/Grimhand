using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>战斗胜利经验（对照 v0.9 怪物组合表）。</summary>
    public static class CombatXpRules
    {
        public static int Roll(BattleRng rng, int floor, bool isElite, bool isBoss) =>
            CombatRewardRules.RollXp(rng, floor, isElite, isBoss);
    }
}
