using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class CampRunPartyApplier
    {
        public static void Apply(CampRosterState roster, ExpeditionRunState run, CampMetaState meta = null)
        {
            if (roster == null || run == null)
                return;

            run.Party.Clear();
            foreach (var loadout in roster.Members)
            {
                if (loadout == null || string.IsNullOrEmpty(loadout.CharacterDefinitionId))
                    continue;

                var stats = CharacterProgression.GetStatsForCharacter(loadout.CharacterDefinitionId, 1);
                var member = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = loadout.CharacterDefinitionId,
                    DisplayName = loadout.DisplayName,
                    Level = 1,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                };

                if (meta != null)
                {
                    var progress = meta.GetOrCreate(loadout.CharacterDefinitionId);
                    member.SelectedTalentSlot1Id = progress.SelectedSlot1TalentId ?? "";
                    member.SelectedTalentSlot2Id = progress.SelectedSlot2TalentId ?? "";
                }

                member.CampDeckCardIds.Clear();
                foreach (var cardId in loadout.DeckCardIds)
                    member.CampDeckCardIds.Add(cardId ?? "");

                run.RunStartCampDecks[member.CharacterDefinitionId] = new List<string>(member.CampDeckCardIds);
                run.Party.Add(member);
            }

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
        }
    }
}
