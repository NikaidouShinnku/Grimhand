namespace Grimhand.Battle.Model
{
    public enum TeamSide
    {
        Player = 0,
        Enemy = 1
    }

    public enum TurnPhase
    {
        Draw,
        Planning,
        SpeedResolve,
        EndOfTurn,
        BattleEnd
    }

    public enum FormationSlot
    {
        Front = 1,
        Middle = 2,
        Back = 3
    }

    public enum CardType
    {
        Attack,
        Defense,
        Status
    }

    public enum CardEffectKind
    {
        DealDamage,
        GainBlock,
        Heal,
        DrawCards
    }

    public enum BattleOutcome
    {
        Ongoing,
        PlayerVictory,
        PlayerDefeat
    }

    public enum ActionKind
    {
        None,
        Attack,
        Defense,
        Status,
        Other
    }
}
