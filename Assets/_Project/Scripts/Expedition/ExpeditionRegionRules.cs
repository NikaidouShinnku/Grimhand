namespace Grimhand.Expedition
{
    public static class ExpeditionRegionRules
    {
        public const int CaveLayerCount = 20;
        public const int DungeonLayerCount = 40;
        public const int FullLayerCount = 60;
        public const int DungeonStartLayer = 21;
        public const int AbyssStartLayer = 41;

        public static bool IsAbyssLayer(int layerNumber) => layerNumber >= AbyssStartLayer;

        public static bool IsDungeonLayer(int layerNumber) =>
            layerNumber >= DungeonStartLayer && layerNumber < AbyssStartLayer;

        public static void ApplyMapStartLayer(Grimhand.Expedition.Model.ExpeditionConfig config, int mapStartLayer)
        {
            if (config == null)
                return;

            if (mapStartLayer >= AbyssStartLayer)
            {
                config.MapStartLayer = AbyssStartLayer;
                config.ChapterLayerCount = FullLayerCount;
                config.TargetBattleCount = FullLayerCount - 1;
            }
            else if (mapStartLayer >= DungeonStartLayer)
            {
                config.MapStartLayer = DungeonStartLayer;
                config.ChapterLayerCount = DungeonLayerCount;
                config.TargetBattleCount = DungeonLayerCount - 1;
            }
            else
            {
                config.MapStartLayer = 1;
                config.ChapterLayerCount = CaveLayerCount;
                config.TargetBattleCount = CaveLayerCount - 1;
            }
        }
    }
}
