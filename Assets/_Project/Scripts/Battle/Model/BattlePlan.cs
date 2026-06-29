using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class BattlePlan
    {
        public List<int> PlayQueue { get; } = new();
        public Dictionary<int, string> TargetByCardInstanceId { get; } = new();
        /// <summary>出牌时实际消耗的能量（含 X 费/剩余能量牌）。</summary>
        public Dictionary<int, int> EnergySpentPerCard { get; } = new();
        public int EnergySpent { get; set; }
    }
}
