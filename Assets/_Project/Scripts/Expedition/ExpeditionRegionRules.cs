namespace Grimhand.Expedition
{
    public static class ExpeditionRegionRules
    {
        public const int CaveLayerCount = 20;
        public const int FullLayerCount = 40;
        public const int DungeonStartLayer = 21;

        public static bool IsDungeonLayer(int layerNumber) => layerNumber >= DungeonStartLayer;

        public static void ApplyMapStartLayer(Grimhand.Expedition.Model.ExpeditionConfig config, int mapStartLayer)
        {
            if (config == null)
                return;

            config.MapStartLayer = mapStartLayer <= 1 ? 1 : DungeonStartLayer;

            if (config.MapStartLayer >= DungeonStartLayer)
            {
                config.ChapterLayerCount = FullLayerCount;
                config.TargetBattleCount = FullLayerCount - 1;
            }
            else
            {
                config.ChapterLayerCount = CaveLayerCount;
                config.TargetBattleCount = CaveLayerCount - 1;
            }
        }
    }
}
