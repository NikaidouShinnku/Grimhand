namespace Grimhand.Battle.Model
{
    public readonly struct KeywordDefinition
    {
        public KeywordDefinition(string id, string displayName, string description)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }
}
