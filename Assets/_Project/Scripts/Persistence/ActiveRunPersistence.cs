using Grimhand.Expedition;

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
