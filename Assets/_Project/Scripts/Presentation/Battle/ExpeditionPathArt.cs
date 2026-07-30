using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class ExpeditionPathArt
    {
        /// <summary>
        /// 表现用层数：战后领奖留在刚打完的层（含 20/40 Boss），道路选择才进下一层。
        /// </summary>
        public static int ResolvePresentationLayer(ExpeditionRunState run)
        {
            if (run?.Map == null)
                return System.Math.Max(1, run?.LastBattleFloor ?? 1);

            var nodesCompleted = run.Map.NodesCompleted;
            switch (run.Phase)
            {
                case ExpeditionPhase.RewardPickup:
                    // 宝箱：节点尚未结算，当前层 = NodesCompleted + 1
                    if (run.PendingRewardPickup?.Kind == RewardPickupKind.Chest)
                        return System.Math.Max(1, nodesCompleted + 1);
                    // 战斗/事件奖励：节点已结算，留在刚完成的层（避免 20→21 提前切背景）
                    if (run.LastBattleFloor > 0)
                        return run.LastBattleFloor;
                    return System.Math.Max(1, nodesCompleted);

                case ExpeditionPhase.RunComplete:
                case ExpeditionPhase.RunFailed:
                    if (run.LastBattleFloor > 0)
                        return run.LastBattleFloor;
                    return System.Math.Max(1, nodesCompleted);

                case ExpeditionPhase.RouteSelect:
                    return System.Math.Max(1, nodesCompleted + 1);

                default:
                    // InBattle / Shop / Event / Altar：当前进行中的层
                    return System.Math.Max(1, nodesCompleted + 1);
            }
        }

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
