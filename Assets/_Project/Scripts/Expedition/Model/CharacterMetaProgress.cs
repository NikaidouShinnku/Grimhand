namespace Grimhand.Expedition.Model
{
    /// <summary>角色局外成长：等级、经验与双槽位天赋装备。</summary>
    public sealed class CharacterMetaProgress
    {
        public string CharacterDefinitionId { get; set; } = "";
        public int OutOfRunLevel { get; set; } = 1;
        public int OutOfRunXp { get; set; }
        public string SelectedSlot1TalentId { get; set; } = "";
        public string SelectedSlot2TalentId { get; set; } = "";

        public string GetSelectedTalentId(int slot) =>
            slot == 1 ? SelectedSlot1TalentId : slot == 2 ? SelectedSlot2TalentId : "";

        public void SetSelectedTalentId(int slot, string talentId)
        {
            if (slot == 1)
                SelectedSlot1TalentId = talentId ?? "";
            else if (slot == 2)
                SelectedSlot2TalentId = talentId ?? "";
        }
    }
}
