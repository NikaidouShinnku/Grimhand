using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>
    /// 远征编队站位：Party[0]=前排、[1]=中排、[2]=后排。
    /// 开战按此顺序赋槽；局内换位后同步回 Party。
    /// </summary>
    public static class ExpeditionPartyFormationRules
    {
        /// <summary>按当前战斗玩家 Slot 重排 Party，保留各角色牌组/天赋进度。</summary>
        public static void SyncPartyOrderFromBattle(
            BattleState state,
            List<PartyMemberSnapshot> party)
        {
            if (state?.Combatants == null || party == null || party.Count == 0)
                return;

            var bySlot = new List<CombatantState>();
            foreach (var unit in state.Combatants)
            {
                if (unit != null && unit.Team == TeamSide.Player)
                    bySlot.Add(unit);
            }

            if (bySlot.Count == 0)
                return;

            bySlot.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));

            var reordered = new List<PartyMemberSnapshot>(CampRosterState.PartySize);
            var used = new HashSet<string>();

            foreach (var unit in bySlot)
            {
                if (reordered.Count >= CampRosterState.PartySize)
                    break;

                var member = FindByCharacterId(party, unit.CharacterDefinitionId);
                if (member == null || !used.Add(unit.CharacterDefinitionId))
                    continue;

                reordered.Add(member);
            }

            foreach (var member in party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;
                if (reordered.Count >= CampRosterState.PartySize)
                    break;
                if (!used.Add(member.CharacterDefinitionId))
                    continue;

                reordered.Add(member);
            }

            if (reordered.Count == 0)
                return;

            party.Clear();
            party.AddRange(reordered);
        }

        public static void SwapPartyMembers(List<PartyMemberSnapshot> party, int indexA, int indexB)
        {
            if (party == null)
                return;
            if (indexA < 0 || indexB < 0 || indexA >= party.Count || indexB >= party.Count)
                return;
            if (indexA == indexB)
                return;

            (party[indexA], party[indexB]) = (party[indexB], party[indexA]);
        }

        /// <summary>军营编队顺序跟随远征 Party（按角色 Id 对齐）。</summary>
        public static void SyncCampRosterOrderFromParty(
            CampRosterState roster,
            IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (roster?.Members == null || party == null || party.Count == 0)
                return;

            CampRosterLoadoutRules.EnsureRosterStructure(roster);
            if (roster.Members.Count == 0)
                return;

            var byId = new Dictionary<string, CampMemberLoadout>();
            foreach (var member in roster.Members)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;
                byId[member.CharacterDefinitionId] = member;
            }

            var reordered = new List<CampMemberLoadout>(roster.Members.Count);
            var used = new HashSet<string>();

            foreach (var partyMember in party)
            {
                if (partyMember == null || string.IsNullOrEmpty(partyMember.CharacterDefinitionId))
                    continue;
                if (!byId.TryGetValue(partyMember.CharacterDefinitionId, out var loadout))
                    continue;
                if (!used.Add(partyMember.CharacterDefinitionId))
                    continue;
                reordered.Add(loadout);
            }

            foreach (var member in roster.Members)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;
                if (!used.Add(member.CharacterDefinitionId))
                    continue;
                reordered.Add(member);
            }

            while (reordered.Count < roster.Members.Count)
                reordered.Add(new CampMemberLoadout());

            for (var i = 0; i < roster.Members.Count; i++)
            {
                var source = i < reordered.Count ? reordered[i] : null;
                CopyLoadoutInto(source, roster.Members[i]);
            }
        }

        static PartyMemberSnapshot FindByCharacterId(
            IReadOnlyList<PartyMemberSnapshot> party,
            string characterDefinitionId)
        {
            if (party == null || string.IsNullOrEmpty(characterDefinitionId))
                return null;

            foreach (var member in party)
            {
                if (member != null && member.CharacterDefinitionId == characterDefinitionId)
                    return member;
            }

            return null;
        }

        static void CopyLoadoutInto(CampMemberLoadout source, CampMemberLoadout target)
        {
            if (target == null)
                return;

            target.CharacterDefinitionId = source?.CharacterDefinitionId ?? "";
            target.DisplayName = source?.DisplayName ?? "";
            target.DeckCardIds.Clear();
            target.DeckCollectionEntryIndices.Clear();

            if (source?.DeckCardIds != null)
            {
                foreach (var id in source.DeckCardIds)
                    target.DeckCardIds.Add(id ?? "");
            }

            if (source?.DeckCollectionEntryIndices != null)
            {
                foreach (var index in source.DeckCollectionEntryIndices)
                    target.DeckCollectionEntryIndices.Add(index);
            }

            CampRosterLoadoutRules.EnsureDeckStructure(target);
        }
    }
}
