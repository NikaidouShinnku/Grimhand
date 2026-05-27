namespace Grimhand.Battle.Model
{
    public sealed class StatusInstance
    {
        public string StatusId { get; set; } = "";
        public int Stacks { get; set; } = 1;
        public int RemainingTurns { get; set; } = -1;
    }
}
