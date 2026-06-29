using System;

namespace Grimhand.Expedition
{
    /// <summary>局内等级 / 经验（v0.8 Excel 角色表）。</summary>
    public static class CharacterProgression
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 20;

        static readonly int[] WarriorHp =
        {
            80, 86, 92, 98, 104, 110, 116, 122, 128, 134,
            140, 146, 152, 158, 164, 170, 176, 182, 188, 194
        };

        static readonly int[] PharaohHp =
        {
            60, 65, 70, 75, 80, 85, 90, 95, 100, 105,
            110, 115, 120, 125, 130, 135, 140, 145, 150, 155
        };

        static readonly int[] DemonHp =
        {
            45, 49, 53, 57, 61, 65, 69, 73, 77, 81,
            85, 89, 93, 97, 101, 105, 109, 113, 117, 121
        };

        static readonly int[] XpToLevel =
        {
            0, 0, 8, 11, 14, 17, 20, 23, 26, 29,
            32, 35, 38, 41, 44, 47, 50, 53, 56, 59, 62
        };

        public const int WarriorSpeed = 7;
        public const int PharaohSpeed = 5;
        public const int DemonSpeed = 6;

        public static int ClampLevel(int level) =>
            level < MinLevel ? MinLevel : level > MaxLevel ? MaxLevel : level;

        public static int XpRequiredForLevel(int level)
        {
            level = ClampLevel(level);
            if (level <= MinLevel)
                return 0;

            return XpToLevel[level];
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
            var index = level - 1;
            return characterDefinitionId switch
            {
                "char_knight" or "char_warrior" => new CharacterStats(WarriorHp[index], WarriorSpeed),
                "char_mage" or "char_pharaoh" => new CharacterStats(PharaohHp[index], PharaohSpeed),
                "char_ranger" or "char_demon" => new CharacterStats(DemonHp[index], DemonSpeed),
                _ => new CharacterStats(WarriorHp[index], WarriorSpeed)
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
    }
}
