namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionRouteOption
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public ExpeditionNodeType NodeType { get; set; } = ExpeditionNodeType.Combat;
        public int EncounterIndex { get; set; }
    }
}
