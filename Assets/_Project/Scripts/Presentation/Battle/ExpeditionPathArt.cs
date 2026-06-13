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

            if (ExpeditionRegionRules.IsAbyssLayer(layerNumber))
                return icons.AbyssBackground != null ? icons.AbyssBackground : icons.DungeonBackground;

            if (ExpeditionRegionRules.IsDungeonLayer(layerNumber))
                return icons.DungeonBackground != null ? icons.DungeonBackground : icons.CaveBackground;

            return icons.CaveBackground;
        }

        public static Sprite[] ResolvePathVariants(BattleUiIconCatalogSO icons, int layerNumber)
        {
            if (icons == null)
                return System.Array.Empty<Sprite>();

            Sprite[] paths;
            if (ExpeditionRegionRules.IsAbyssLayer(layerNumber))
                paths = icons.AbyssPathVariants;
            else if (ExpeditionRegionRules.IsDungeonLayer(layerNumber))
                paths = icons.DungeonPathVariants;
            else
                paths = icons.CavePathVariants;

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
