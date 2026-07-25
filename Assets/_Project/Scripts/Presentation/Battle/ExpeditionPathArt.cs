using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
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

        /// <summary>道路选择全屏背景（new_*_background2），按层数切洞窟/地牢/海渊。</summary>
        public static Sprite ResolveRouteSelectBackground(BattleUiIconCatalogSO icons, int layerNumber)
        {
            if (icons == null)
                return null;

            if (ExpeditionRegionRules.IsAbyssLayer(layerNumber))
            {
                return icons.AbyssRouteSelectBackground != null
                    ? icons.AbyssRouteSelectBackground
                    : ResolveBackground(icons, layerNumber);
            }

            if (ExpeditionRegionRules.IsDungeonLayer(layerNumber))
            {
                return icons.DungeonRouteSelectBackground != null
                    ? icons.DungeonRouteSelectBackground
                    : ResolveBackground(icons, layerNumber);
            }

            return icons.CaveRouteSelectBackground != null
                ? icons.CaveRouteSelectBackground
                : ResolveBackground(icons, layerNumber);
        }

        public static string ResolveRegionDisplayName(int layerNumber)
        {
            if (ExpeditionRegionRules.IsAbyssLayer(layerNumber))
                return "海渊";
            if (ExpeditionRegionRules.IsDungeonLayer(layerNumber))
                return "地牢";
            return "洞窟";
        }

        public static Sprite ResolvePathFrame(BattleUiIconCatalogSO icons, ExpeditionNodeType nodeType)
        {
            if (icons == null)
                return null;

            return nodeType switch
            {
                ExpeditionNodeType.Elite => icons.PathFrameElite ?? icons.PathFrameCombat,
                ExpeditionNodeType.Treasure => icons.PathFrameTreasure,
                ExpeditionNodeType.Event => icons.PathFrameEvent,
                ExpeditionNodeType.Shop => icons.PathFrameShop,
                ExpeditionNodeType.Shrine => icons.PathFrameAltar,
                ExpeditionNodeType.Boss => icons.PathFrameBoss,
                _ => icons.PathFrameCombat
            };
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
