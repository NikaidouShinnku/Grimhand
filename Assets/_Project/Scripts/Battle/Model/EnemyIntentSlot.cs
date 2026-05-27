namespace Grimhand.Battle.Model
{
    public sealed class EnemyIntentSlot
    {
        public int CardInstanceId { get; set; }
        public string OwnerCombatantId { get; set; } = "";
        public bool IsHidden { get; set; }
        public int OrderIndex { get; set; }
    }
}
