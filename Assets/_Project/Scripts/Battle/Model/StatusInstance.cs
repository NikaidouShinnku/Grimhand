namespace Grimhand.Battle.Model
{
    public sealed class StatusInstance
    {
        public string StatusId { get; set; } = "";
        /// <summary>层数：每次生效时的强度（如中毒每层 1 伤害、易伤每层 1%）。</summary>
        public int Stacks { get; set; } = 1;
        /// <summary>剩余持续回合。&lt;0 表示永久；&gt;0 为剩余回合数。</summary>
        public int RemainingTurns { get; set; } = -1;
        /// <summary>施加来源角色 Id（缠绕等：施法者死亡后清除）。</summary>
        public string SourceCombatantId { get; set; } = "";
    }
}
