using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    public static class ActiveRunPersistence
    {
        public static void BeginNewRun(PlayerProfileState profile, ExpeditionEngine engine, int mapStartLayer)
        {
            if (profile == null || engine == null)
                return;

            profile.ActiveRun = CreateSnapshot(engine, mapStartLayer);
        }

        public static void UpdateCheckpoint(PlayerProfileState profile, ExpeditionEngine engine)
        {
            if (profile?.ActiveRun == null || engine == null)
                return;

            MetaEconomySync.SyncMetaGoldFromRun(profile, engine.Run);
            profile.ActiveRun.RngState = engine.RngState;
            profile.ActiveRun.RunJson = ExpeditionRunSaveCodec.Serialize(engine.Run, engine.RngState);
        }

        public static void Clear(PlayerProfileState profile) => profile.ActiveRun = null;

        /// <summary>
        /// 主菜单放弃未完成远征：结算局外经验（层数×5）并同步尚未入账的局外黄金，然后清除远征存档。
        /// </summary>
        public static bool TryAbandonAndSettle(
            PlayerProfileState profile,
            CampMetaState meta,
            ExpeditionConfig config)
        {
            if (profile == null || !profile.HasActiveRun)
                return false;

            ExpeditionRunState run = null;
            if (config != null
                && ExpeditionRunSaveCodec.TryDeserialize(
                    profile.ActiveRun.RunJson,
                    config,
                    out run,
                    out _))
            {
                MetaEconomySync.SyncMetaGoldFromRun(profile, run);
                RunSettlementRules.ApplyRunEndMetaRewards(run, meta ?? profile.Meta);
            }

            Clear(profile);
            return true;
        }

        static ActiveRunSnapshot CreateSnapshot(ExpeditionEngine engine, int mapStartLayer) =>
            new()
            {
                Version = ActiveRunSnapshot.CurrentVersion,
                MapStartLayer = mapStartLayer,
                RunSeed = engine.Config.RunSeed,
                RngState = engine.RngState,
                MetaGoldSyncedRunGold = engine.Run.Gold,
                RunJson = ExpeditionRunSaveCodec.Serialize(engine.Run, engine.RngState)
            };
    }
}
