namespace Grimhand.Expedition
{
    public static class ExpeditionRegionRules
    {
        public const int CaveLayerCount = 20;
        public const int DungeonLayerCount = 40;
        public const int FullLayerCount = 60;
        public const int DungeonStartLayer = 21;
        public const int AbyssStartLayer = 41;
        public const int CaveBossLayer = 20;
        public const int DungeonBossLayer = 40;
        public const int AbyssBossLayer = 60;

        /// <summary>洞窟/地牢/海渊关底：这些层必须是唯一可选的 Boss 房。</summary>
        public static bool IsMandatoryBossLayer(int layerNumber) =>
            layerNumber is CaveBossLayer or DungeonBossLayer or AbyssBossLayer;

        public static bool IsAbyssLayer(int layerNumber) => layerNumber >= AbyssStartLayer;

        public static bool IsDungeonLayer(int layerNumber) =>
            layerNumber >= DungeonStartLayer && layerNumber < AbyssStartLayer;

        public static bool IsBossTestStartLayer(int mapStartLayer) =>
            mapStartLayer is CaveBossLayer or DungeonBossLayer or AbyssBossLayer;

        public static void ApplyMapStartLayer(Grimhand.Expedition.Model.ExpeditionConfig config, int mapStartLayer)
        {
            if (config == null)
                return;

            config.ChapterLayerCount = FullLayerCount;
            config.TargetBattleCount = FullLayerCount - 1;

            config.MapStartLayer = mapStartLayer switch
            {
                CaveBossLayer => CaveBossLayer,
                DungeonBossLayer => DungeonBossLayer,
                AbyssBossLayer => AbyssBossLayer,
                >= AbyssStartLayer => AbyssStartLayer,
                >= DungeonStartLayer => DungeonStartLayer,
                _ => 1
            };
        }
    }
}
