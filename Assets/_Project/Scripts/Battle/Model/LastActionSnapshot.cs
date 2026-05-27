namespace Grimhand.Battle.Model
{
    public readonly struct LastActionSnapshot
    {
        public LastActionSnapshot(
            string actorId,
            ActionKind actionKind,
            string targetId,
            bool wasKill,
            int damageAmount)
        {
            ActorId = actorId;
            ActionKind = actionKind;
            TargetId = targetId;
            WasKill = wasKill;
            DamageAmount = damageAmount;
        }

        public string ActorId { get; }
        public ActionKind ActionKind { get; }
        public string TargetId { get; }
        public bool WasKill { get; }
        public int DamageAmount { get; }

        public static LastActionSnapshot None => new("", ActionKind.None, "", false, 0);
    }
}
