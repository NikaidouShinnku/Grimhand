using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>局外等级 / 经验（对齐 Excel 局外经验表，天赋解锁上限 Lv.10）。</summary>
    public static class MetaProgressionRules
    {
        public const int MaxOutOfRunLevel = 10;
        public const int MetaXpPerCompletedLayer = 5;

        /// <summary>升至目标等级所需 XP（index = 目标等级；Lv.1 起始为 0）。</summary>
        static readonly int[] OutOfRunXpPerLevel =
        {
            0, 0, 100, 200, 300, 500, 800, 1000, 1500, 2000, 2500
        };

        /// <summary>累计 XP 达到该等级（对照总览表「总计XP」列）。</summary>
        static readonly int[] OutOfRunTotalXpToReachLevel =
        {
            0, 0, 100, 300, 600, 1100, 1900, 2900, 4400, 6400, 8900
        };

        public static int ClampLevel(int level) =>
            level < 1 ? 1 : level > MaxOutOfRunLevel ? MaxOutOfRunLevel : level;

        public static void NormalizeProgress(CharacterMetaProgress progress)
        {
            if (progress == null)
                return;

            progress.OutOfRunLevel = progress.OutOfRunLevel < 1 ? 1 : ClampLevel(progress.OutOfRunLevel);
            if (progress.OutOfRunLevel >= MaxOutOfRunLevel)
                progress.OutOfRunXp = 0;
        }

        public static int XpRequiredForLevel(int targetLevel)
        {
            targetLevel = ClampLevel(targetLevel);
            if (targetLevel <= 1)
                return 0;

            return targetLevel < OutOfRunXpPerLevel.Length
                ? OutOfRunXpPerLevel[targetLevel]
                : OutOfRunXpPerLevel[OutOfRunXpPerLevel.Length - 1];
        }

        public static int XpRequiredForNextLevel(CharacterMetaProgress progress)
        {
            NormalizeProgress(progress);
            if (progress == null || progress.OutOfRunLevel >= MaxOutOfRunLevel)
                return 0;

            return XpRequiredForLevel(progress.OutOfRunLevel + 1);
        }

        public static int TotalXpToReachLevel(int level)
        {
            level = ClampLevel(level);
            return level < OutOfRunTotalXpToReachLevel.Length
                ? OutOfRunTotalXpToReachLevel[level]
                : OutOfRunTotalXpToReachLevel[OutOfRunTotalXpToReachLevel.Length - 1];
        }

        public static bool IsMaxLevel(CharacterMetaProgress progress)
        {
            NormalizeProgress(progress);
            return progress != null && progress.OutOfRunLevel >= MaxOutOfRunLevel;
        }

        public static string FormatXpProgress(CharacterMetaProgress progress)
        {
            if (progress == null)
                return "0/100 XP";

            NormalizeProgress(progress);
            if (IsMaxLevel(progress))
                return "满级";

            var need = XpRequiredForNextLevel(progress);
            return $"{progress.OutOfRunXp}/{need} XP";
        }

        public static string FormatLevelProgress(CharacterMetaProgress progress)
        {
            if (progress == null)
                return "Lv.1  0/100 XP";

            NormalizeProgress(progress);
            return $"Lv.{progress.OutOfRunLevel}  {FormatXpProgress(progress)}";
        }

        public static string FormatNextLevelHint(CharacterMetaProgress progress)
        {
            if (progress == null || IsMaxLevel(progress))
                return "已达最高等级 Lv.10";

            var next = progress.OutOfRunLevel + 1;
            var need = XpRequiredForNextLevel(progress);
            var remaining = need - progress.OutOfRunXp;
            if (remaining <= 0)
                return $"即将升至 Lv.{next}";

            return $"距 Lv.{next} 还需 {remaining} 经验（本级需 {need}）";
        }

        public static void GrantOutOfRunXp(CharacterMetaProgress progress, int amount)
        {
            if (progress == null || amount <= 0)
                return;

            NormalizeProgress(progress);
            if (progress.OutOfRunLevel >= MaxOutOfRunLevel)
                return;

            progress.OutOfRunXp += amount;
            while (progress.OutOfRunLevel < MaxOutOfRunLevel)
            {
                var need = XpRequiredForLevel(progress.OutOfRunLevel + 1);
                if (need <= 0 || progress.OutOfRunXp < need)
                    break;

                progress.OutOfRunXp -= need;
                progress.OutOfRunLevel++;
                TalentRules.PruneInvalidSelections(progress);
            }

            if (progress.OutOfRunLevel >= MaxOutOfRunLevel)
                progress.OutOfRunXp = 0;
        }

        public static int ComputeRunEndMetaXpGrant(ExpeditionRunState run)
        {
            var layers = run?.Map?.NodesCompleted ?? 0;
            return layers * MetaXpPerCompletedLayer;
        }
    }
}
