using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    /// <summary>单场战斗的天赋上下文：激活 ID + 远征继承的运行时数值。</summary>
    public sealed class TalentBattleContext
    {
        public HashSet<string> ActiveTalentIds { get; } = new();
        public bool MageReviveAvailable { get; set; }
        public int RangerBloodDebtAttackBonus { get; set; }
        public bool NonBossSoloEnemyBattle { get; set; }

        public bool Has(string talentId) =>
            !string.IsNullOrEmpty(talentId) && ActiveTalentIds.Contains(talentId);
    }
}
