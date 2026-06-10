using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    public enum Floor10BossKind
    {
        SkeletonKing,
        GhostQueen
    }

    public static class Floor10BossEncounterBuilder
    {
        public const string SkeletonKingDisplayName = "骷髅王";
        public const string GhostQueenDisplayName = "幽灵女王";

        public static Floor10BossKind RollBossKind(BattleRng rng) =>
            rng != null && rng.NextInt(0, 2) == 1
                ? Floor10BossKind.GhostQueen
                : Floor10BossKind.SkeletonKing;

        public static string GetDisplayName(Floor10BossKind kind) =>
            kind switch
            {
                Floor10BossKind.GhostQueen => GhostQueenDisplayName,
                _ => SkeletonKingDisplayName
            };

        public static BattleConfig BuildTemplate(
            BattleConfig standardEncounter,
            Floor10BossKind kind) =>
            kind switch
            {
                Floor10BossKind.GhostQueen => GhostQueenBossEncounterBuilder.BuildTemplate(standardEncounter),
                _ => SkeletonKingBossEncounterBuilder.BuildTemplate(standardEncounter)
            };

        public static BattleConfig BuildRandomTemplate(BattleConfig standardEncounter, BattleRng rng) =>
            BuildTemplate(standardEncounter, RollBossKind(rng));
    }
}
