using System;

namespace Grimhand.Expedition
{
    /// <summary>局内等级 / 经验（v2 策划表）。</summary>
    public static class CharacterProgression
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 20;

        public static int ClampLevel(int level) =>
            level < MinLevel ? MinLevel : level > MaxLevel ? MaxLevel : level;

        /// <summary>从 Lv.(N-1) 升到 Lv.N 所需 XP（N≥2）：(N-2)×3+8</summary>
        public static int XpRequiredForLevel(int level)
        {
            if (level <= MinLevel)
                return 0;

            return (level - 2) * 3 + 8;
        }

        public static int XpToNextLevel(int level) =>
            level >= MaxLevel ? 0 : XpRequiredForLevel(level + 1);

        public static XpGainResult AddXp(int level, int currentXp, int amount)
        {
            level = ClampLevel(level);
            if (amount <= 0 || level >= MaxLevel)
                return new XpGainResult(level, currentXp, 0);

            var xp = currentXp + amount;
            var levelsGained = 0;

            while (level < MaxLevel)
            {
                var need = XpRequiredForLevel(level + 1);
                if (need <= 0 || xp < need)
                    break;

                xp -= need;
                level++;
                levelsGained++;
            }

            if (level >= MaxLevel)
                xp = 0;

            return new XpGainResult(level, xp, levelsGained);
        }

        public static CharacterStats GetStatsForCharacter(string characterDefinitionId, int level)
        {
            level = ClampLevel(level);
            return characterDefinitionId switch
            {
                "char_knight" or "char_warrior" => StatsWarrior(level),
                "char_mage" or "char_pharaoh" => StatsPharaoh(level),
                "char_ranger" or "char_demon" => StatsDemon(level),
                _ => StatsWarrior(level)
            };
        }

        public static string FormatLevelLabel(int level) =>
            $"等级 Lv.{ClampLevel(level)}";

        public static string FormatXpLine(int level, int xp)
        {
            level = ClampLevel(level);
            var toNext = XpToNextLevel(level);
            if (toNext <= 0)
                return "经验 MAX";

            return $"经验 {xp}/{toNext}";
        }

        public static float XpFill01(int level, int xp)
        {
            var toNext = XpToNextLevel(level);
            if (toNext <= 0)
                return 1f;

            return Math.Clamp(xp / (float)toNext, 0f, 1f);
        }

        static CharacterStats StatsWarrior(int level)
        {
            var i = level - 1;
            return new CharacterStats(
                50 + i * 6,
                RoundStat(8f + i * 1.5f),
                RoundStat(6f + i * 1.2f),
                7);
        }

        static CharacterStats StatsPharaoh(int level)
        {
            var i = level - 1;
            return new CharacterStats(
                40 + i * 5,
                6 + i * 2,
                RoundStat(4f + i * 0.8f),
                5);
        }

        static CharacterStats StatsDemon(int level)
        {
            var i = level - 1;
            return new CharacterStats(
                30 + i * 4,
                RoundStat(9f + i * 2.5f),
                RoundStat(2f + i * 0.3f),
                6);
        }

        static int RoundStat(float value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
