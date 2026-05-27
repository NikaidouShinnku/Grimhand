using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Status
{
    public static class StatusCatalog
    {
        public const string Poison = "poison";
        public const string Slow = "slow";

        static readonly Dictionary<string, StatusDefinition> Definitions = Build();

        public static StatusDefinition Get(string id)
        {
            Definitions.TryGetValue(id, out var def);
            return def;
        }

        static Dictionary<string, StatusDefinition> Build()
        {
            var map = new Dictionary<string, StatusDefinition>();
            map[Poison] = new StatusDefinition
            {
                Id = Poison,
                DisplayName = "中毒",
                DurationKind = StatusDurationKind.Permanent,
                TurnStartDamagePerStack = 1
            };
            map[Slow] = new StatusDefinition
            {
                Id = Slow,
                DisplayName = "减速",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                SpeedModifierPerStack = -2
            };
            return map;
        }
    }
}
