using Grimhand.Expedition.Model;
using Grimhand.Expedition.Map;

namespace Grimhand.Presentation.Battle
{
    public static class ExpeditionRoutePresentation
    {
        public static string BuildDoorLabel(ExpeditionRouteOption route)
        {
            if (route == null)
                return "";

            return $"{route.DisplayName}\n[{BattleUiFormatters.DescribeNodeType(route.NodeType)}]\n{route.Description}";
        }
    }
}
