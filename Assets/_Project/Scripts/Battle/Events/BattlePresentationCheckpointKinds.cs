namespace Grimhand.Battle.Events
{
    public static class BattlePresentationCheckpointKinds
    {
        public static bool ShouldRecord(BattleEventKind kind) =>
            kind switch
            {
                BattleEventKind.StatusApplied => true,
                BattleEventKind.StatusRemoved => true,
                BattleEventKind.DamageApplied => true,
                BattleEventKind.BlockGained => true,
                BattleEventKind.HealApplied => true,
                BattleEventKind.CharacterRevived => true,
                BattleEventKind.CharacterDied => true,
                BattleEventKind.CombatantSpawned => true,
                BattleEventKind.StatusTickDamage => true,
                BattleEventKind.PortraitIdleRestored => true,
                _ => false
            };
    }
}
