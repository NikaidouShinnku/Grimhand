namespace Grimhand.Expedition
{
    /// <summary>局内等级 / 经验（v2 策划表）。今日仅展示等级，升级逻辑后续接入。</summary>
    public static class CharacterProgression
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 20;

        public static int ClampLevel(int level) =>
            level < MinLevel ? MinLevel : level > MaxLevel ? MaxLevel : level;

        /// <summary>升到 Lv.N 所需本级 XP（N≥2）：(N-2)×3+8</summary>
        public static int XpRequiredForLevel(int level)
        {
            if (level <= MinLevel)
                return 0;

            return (level - 2) * 3 + 8;
        }

        public static string FormatLevelLabel(int level) =>
            $"等级 Lv.{ClampLevel(level)}";
    }
}
