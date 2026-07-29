using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    /// <summary>单场战斗的天赋上下文：激活 ID + 远征继承的运行时数值。</summary>
    public sealed class TalentBattleContext
    {
        public HashSet<string> ActiveTalentIds { get; } = new();
        public bool MageReviveAvailable { get; set; }
        public int RangerBloodDebtAttackBonus { get; set; }
        /// <summary>开战时快照：非 Boss 且开局仅 1 敌。孤猎运行时改为动态数敌，此字段仅兼容旧逻辑。</summary>
        public bool NonBossSoloEnemyBattle { get; set; }
        public bool IsBossBattle { get; set; }

        public bool Has(string talentId) =>
            !string.IsNullOrEmpty(talentId) && ActiveTalentIds.Contains(talentId);
    }
}
