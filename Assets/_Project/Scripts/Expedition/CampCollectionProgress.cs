using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>军营收藏 → 祭坛取牌进度（远征级，不随 CaptureParty 丢失）。</summary>
    public static class CampCollectionProgress
    {
        public static bool IsExtracted(ExpeditionRunState run, string memberId, int collectionIndex)
        {
            if (run == null || string.IsNullOrEmpty(memberId) || collectionIndex < 0)
                return false;

            return run.ExtractedCampCollectionIndices.TryGetValue(memberId, out var set)
                   && set.Contains(collectionIndex);
        }

        public static void MarkExtracted(ExpeditionRunState run, string memberId, int collectionIndex)
        {
            if (run == null || string.IsNullOrEmpty(memberId) || collectionIndex < 0)
                return;

            if (!run.ExtractedCampCollectionIndices.TryGetValue(memberId, out var set))
            {
                set = new HashSet<int>();
                run.ExtractedCampCollectionIndices[memberId] = set;
            }

            set.Add(collectionIndex);
        }

        public static void SyncMemberFromRun(ExpeditionRunState run, PartyMemberSnapshot member)
        {
            if (run == null || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            if (!run.ExtractedCampCollectionIndices.TryGetValue(member.CharacterDefinitionId, out var set))
                return;

            foreach (var index in set)
                member.ExtractedCampCardIndices.Add(index);
        }

        public static void SyncRunFromMember(ExpeditionRunState run, PartyMemberSnapshot member)
        {
            if (run == null || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            foreach (var index in member.ExtractedCampCardIndices)
                MarkExtracted(run, member.CharacterDefinitionId, index);
        }

        public static void SyncRunFromParty(ExpeditionRunState run)
        {
            if (run?.Party == null)
                return;

            foreach (var member in run.Party)
                SyncRunFromMember(run, member);
        }
    }
}
