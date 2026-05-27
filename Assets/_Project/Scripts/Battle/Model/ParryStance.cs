namespace Grimhand.Battle.Model
{
    /// <summary>
    /// 本回合速度结算中，等待下一次受到攻击时触发的弹反姿态。
    /// </summary>
    public sealed class ParryStance
    {
        public int DamageReductionPercent { get; set; }
        public int ReflectPercent { get; set; }
    }
}
