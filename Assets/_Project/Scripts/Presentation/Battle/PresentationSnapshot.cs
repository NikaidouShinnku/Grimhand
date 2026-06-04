using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗演出期间的 HP / 存活展示快照，与已结算完毕的逻辑状态解耦。</summary>
    public sealed class PresentationSnapshot
    {
        readonly Dictionary<string, int> _hp = new();
        readonly Dictionary<string, int> _maxHp = new();
        readonly HashSet<string> _dead = new();

        public static PresentationSnapshot Capture(BattleState state)
        {
            var snap = new PresentationSnapshot();
            if (state == null)
                return snap;

            foreach (var c in state.Combatants)
            {
                snap._hp[c.Id] = c.Hp;
                snap._maxHp[c.Id] = c.MaxHp;
                if (!c.IsAlive)
                    snap._dead.Add(c.Id);
            }

            return snap;
        }

        public bool IsAlive(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return false;

            return !_dead.Contains(combatantId) && _hp.TryGetValue(combatantId, out var hp) && hp > 0;
        }

        public int GetHp(string combatantId) =>
            _hp.TryGetValue(combatantId, out var hp) ? hp : 0;

        public int GetMaxHp(string combatantId) =>
            _maxHp.TryGetValue(combatantId, out var maxHp) ? maxHp : 0;

        public void ApplyDamage(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            if (!_hp.TryGetValue(combatantId, out var hp))
                return;

            hp = System.Math.Max(0, hp - amount);
            _hp[combatantId] = hp;
            if (hp <= 0)
                _dead.Add(combatantId);
        }

        public void MarkDead(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return;

            _dead.Add(combatantId);
            if (_hp.ContainsKey(combatantId))
                _hp[combatantId] = 0;
        }
    }
}
