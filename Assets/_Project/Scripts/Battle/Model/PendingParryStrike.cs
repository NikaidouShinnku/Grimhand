namespace Grimhand.Battle.Model
{
    /// <summary>应对弹反：在触发该次敌方攻击结算完成后再造成伤害。</summary>
    public sealed class PendingParryStrike
    {
        public int TriggerEnemyCardInstanceId { get; set; }
        public string DefenderId { get; set; } = "";
        public string AttackerId { get; set; } = "";
        public int Damage { get; set; }
        public int RespondCardInstanceId { get; set; }
        public CardType RespondCardType { get; set; }
    }
}
