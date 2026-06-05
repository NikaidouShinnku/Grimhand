namespace Grimhand.Expedition
{
    public readonly struct CharacterStats
    {
        public CharacterStats(int maxHp, int baseAttack, int baseDefense, int speed)
        {
            MaxHp = maxHp;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
            Speed = speed;
        }

        public int MaxHp { get; }
        public int BaseAttack { get; }
        public int BaseDefense { get; }
        public int Speed { get; }
    }

    public readonly struct XpGainResult
    {
        public XpGainResult(int level, int xp, int levelsGained)
        {
            Level = level;
            Xp = xp;
            LevelsGained = levelsGained;
        }

        public int Level { get; }
        public int Xp { get; }
        public int LevelsGained { get; }
    }
}
