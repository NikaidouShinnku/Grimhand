using Grimhand.Expedition;

namespace Grimhand.Persistence
{
    public static class MetaEconomySync
    {
        /// <summary>远征内获得金币时，同步等量局外黄金（Excel 局外内容）。</summary>
        public static void SyncMetaGoldFromRun(PlayerProfileState profile, ExpeditionRunState run)
        {
            if (profile?.ActiveRun == null || run == null)
                return;

            var delta = run.Gold - profile.ActiveRun.MetaGoldSyncedRunGold;
            if (delta <= 0)
                return;

            profile.AccountGold += delta;
            profile.ActiveRun.MetaGoldSyncedRunGold = run.Gold;
        }
    }
}
