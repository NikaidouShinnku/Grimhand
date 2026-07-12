using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征结束结算：局外经验（层数×5）。</summary>
    public static class RunSettlementRules
    {
        public static void ApplyRunEndMetaRewards(ExpeditionRunState run, CampMetaState meta)
        {
            if (run?.Party == null || meta == null)
                return;

            var xpGrant = MetaProgressionRules.ComputeRunEndMetaXpGrant(run);
            if (xpGrant <= 0)
                return;

            foreach (var member in run.Party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                MetaProgressionRules.GrantOutOfRunXp(
                    meta.GetOrCreate(member.CharacterDefinitionId),
                    xpGrant);
            }
        }
    }
}
