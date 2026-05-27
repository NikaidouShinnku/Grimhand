using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class BattlePlan
    {
        public List<int> PlayQueue { get; } = new();
        public Dictionary<int, string> TargetByCardInstanceId { get; } = new();
        public int EnergySpent { get; set; }
    }
}
