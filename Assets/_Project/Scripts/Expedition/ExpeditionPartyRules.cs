using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征编队人数上限与去重（始终最多 3 人）。</summary>
    public static class ExpeditionPartyRules
    {
        public static void EnforceMaxSize(IList<PartyMemberSnapshot> party)
        {
            if (party == null)
                return;

            var seen = new HashSet<string>();
            for (var i = party.Count - 1; i >= 0; i--)
            {
                var member = party[i];
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                {
                    party.RemoveAt(i);
                    continue;
                }

                if (!seen.Add(member.CharacterDefinitionId))
                    party.RemoveAt(i);
            }

            while (party.Count > CampRosterState.PartySize)
                party.RemoveAt(party.Count - 1);
        }

        public static bool IsPartyWiped(IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (party == null || party.Count == 0)
                return false;

            var hasMember = false;
            foreach (var member in party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                hasMember = true;
                if (member.Hp > 0)
                    return false;
            }

            return hasMember;
        }

        public static bool HasUsableCampRoster(CampRosterState roster)
        {
            if (roster?.Members == null || roster.Members.Count == 0)
                return false;

            var limit = System.Math.Min(roster.Members.Count, CampRosterState.PartySize);
            for (var i = 0; i < limit; i++)
            {
                var member = roster.Members[i];
                if (member != null && !string.IsNullOrEmpty(member.CharacterDefinitionId))
                    return true;
            }

            return false;
        }
    }
}
