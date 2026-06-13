using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class ExpeditionPathArt
    {
        public static Sprite ResolveBackground(BattleUiIconCatalogSO icons, int layerNumber)
        {
            if (icons == null)
                return null;

            return ExpeditionRegionRules.IsDungeonLayer(layerNumber)
                ? icons.DungeonBackground != null ? icons.DungeonBackground : icons.CaveBackground
                : icons.CaveBackground;
        }

        public static Sprite[] ResolvePathVariants(BattleUiIconCatalogSO icons, int layerNumber)
        {
            if (icons == null)
                return System.Array.Empty<Sprite>();

            var dungeon = ExpeditionRegionRules.IsDungeonLayer(layerNumber);
            var paths = dungeon ? icons.DungeonPathVariants : icons.CavePathVariants;
            if (paths != null && paths.Length > 0)
                return paths;

            return icons.CavePathVariants ?? System.Array.Empty<Sprite>();
        }

        public static Sprite PickPathSprite(BattleUiIconCatalogSO icons, int layerNumber, int index)
        {
            var paths = ResolvePathVariants(icons, layerNumber);
            if (paths.Length == 0)
                return icons?.UnknownPathIcon;

            if (index < 0)
                index = 0;

            return paths[index % paths.Length];
        }
    }
}
