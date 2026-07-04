using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征队伍在战斗外的有效属性（含遗物与天赋加成）。</summary>
    public static class ExpeditionPartyStatsRules
    {
        public static int GetPartyMaxHpBonus(
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            Dictionary<string, int> relicGrowthTiers = null)
        {
            var bonus = 0;
            if (relicIds != null && relicIds.Count > 0)
            {
                var mods = RelicDatabase.BuildModifiers(relicIds, relicGrowthTiers);
                bonus += mods.TeamHpBonus;
            }

            if (PartyHasTalent(party, "talent_knight_s2_lv6") && IsKnightAliveInParty(party))
                bonus += 10;

            return bonus;
        }

        public static int GetEffectiveMaxHp(PartyMemberSnapshot member, int partyMaxHpBonus)
        {
            if (member == null)
                return 0;

            var stats = CharacterProgression.GetStatsForCharacter(
                member.CharacterDefinitionId,
                CharacterProgression.ClampLevel(member.Level));
            return System.Math.Max(
                1,
                stats.MaxHp + partyMaxHpBonus + member.AltarMaxHpBonus - member.MaxHpPenalty);
        }

        public static void GetDisplayHp(
            PartyMemberSnapshot member,
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            Dictionary<string, int> relicGrowthTiers,
            out int hp,
            out int maxHp)
        {
            var bonus = GetPartyMaxHpBonus(party, relicIds, relicGrowthTiers);
            maxHp = GetEffectiveMaxHp(member, bonus);
            hp = member == null ? 0 : System.Math.Min(member.Hp, maxHp);
        }

        public static void SyncPartyEffectiveMaxHp(
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            Dictionary<string, int> relicGrowthTiers = null)
        {
            if (party == null || party.Count == 0)
                return;

            var hpBonus = GetPartyMaxHpBonus(party, relicIds, relicGrowthTiers);
            foreach (var member in party)
            {
                if (member == null)
                    continue;

                var effectiveMax = GetEffectiveMaxHp(member, hpBonus);
                var hpGain = effectiveMax - member.MaxHp;
                member.MaxHp = effectiveMax;
                if (hpGain > 0 && member.Hp > 0)
                    member.Hp = System.Math.Min(member.MaxHp, member.Hp + hpGain);
                else if (member.Hp > member.MaxHp)
                    member.Hp = member.MaxHp;
            }
        }

        static bool PartyHasTalent(IReadOnlyList<PartyMemberSnapshot> party, string talentId)
        {
            if (party == null || string.IsNullOrEmpty(talentId))
                return false;

            foreach (var member in party)
            {
                if (member == null)
                    continue;

                if (member.SelectedTalentSlot1Id == talentId || member.SelectedTalentSlot2Id == talentId)
                    return true;
            }

            return false;
        }

        static bool IsKnightAliveInParty(IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (party == null)
                return false;

            foreach (var member in party)
            {
                if (member?.CharacterDefinitionId == TalentCatalog.KnightId && member.Hp > 0)
                    return true;
            }

            return false;
        }
    }
}
