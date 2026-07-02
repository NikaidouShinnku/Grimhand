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
            var slotLimit = System.Math.Min(roster.Members.Count, CampRosterState.PartySize);
            for (var slot = 0; slot < slotLimit; slot++)
            {
                var loadout = roster.Members[slot];
                if (loadout == null || string.IsNullOrEmpty(loadout.CharacterDefinitionId))
                    continue;

                if (run.Party.Count >= CampRosterState.PartySize)
                    break;

                var stats = CharacterProgression.GetStatsForCharacter(loadout.CharacterDefinitionId, 1);
                var member = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = loadout.CharacterDefinitionId,
                    DisplayName = CharacterDisplayNames.GetOrFallback(
                        loadout.CharacterDefinitionId,
                        loadout.DisplayName),
                    Level = 1,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                };

                if (meta != null)
                {
                    var progress = meta.GetOrCreate(loadout.CharacterDefinitionId);
                    member.SelectedTalentSlot1Id = progress.SelectedSlot1TalentId ?? "";
                    member.SelectedTalentSlot2Id = progress.SelectedSlot2TalentId ?? "";
                    TalentRules.PruneInvalidSelections(progress);
                    member.SelectedTalentSlot1Id = progress.SelectedSlot1TalentId ?? "";
                    member.SelectedTalentSlot2Id = progress.SelectedSlot2TalentId ?? "";
                }

                member.CampDeckCardIds.Clear();
                foreach (var cardId in loadout.DeckCardIds)
                    member.CampDeckCardIds.Add(cardId ?? "");

                run.RunStartCampDecks[member.CharacterDefinitionId] = new List<string>(member.CampDeckCardIds);
                if (HasFilledCampDeck(member.CampDeckCardIds))
                {
                    member.UsesCampDeckAsBattleBase = true;
                    for (var i = 0; i < member.CampDeckCardIds.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(member.CampDeckCardIds[i]))
                            CampCollectionProgress.MarkExtracted(run, member.CharacterDefinitionId, i);
                    }
                }

                run.Party.Add(member);
            }

            ExpeditionPartyRules.EnforceMaxSize(run.Party);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
        }

        static bool HasFilledCampDeck(IReadOnlyList<string> cardIds)
        {
            if (cardIds == null)
                return false;

            foreach (var id in cardIds)
            {
                if (!string.IsNullOrEmpty(id))
                    return true;
            }

            return false;
        }

        public static void ApplyTalentsFromMeta(PartyMemberSnapshot member, CampMetaState meta)
        {
            if (member == null)
                return;

            member.SelectedTalentSlot1Id = "";
            member.SelectedTalentSlot2Id = "";
            if (meta == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            var progress = meta.GetOrCreate(member.CharacterDefinitionId);
            member.SelectedTalentSlot1Id = progress.SelectedSlot1TalentId ?? "";
            member.SelectedTalentSlot2Id = progress.SelectedSlot2TalentId ?? "";
            TalentRules.PruneInvalidSelections(progress);
            member.SelectedTalentSlot1Id = progress.SelectedSlot1TalentId ?? "";
            member.SelectedTalentSlot2Id = progress.SelectedSlot2TalentId ?? "";
        }
    }
}
