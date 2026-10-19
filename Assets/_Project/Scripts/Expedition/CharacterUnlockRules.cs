using Grimhand.Expedition.Model;
namespace Grimhand.Expedition
{
    public interface ICharacterUnlockProfile { CampMetaState Meta { get; } }
    public static class CharacterUnlockRules
    {
        public const string LockedDescription = "击败第20层骷髅王后解锁。";
        public static string GetLockedDescription(string id) => id==TalentCatalog.LichQueenId
            ? "击败第20层幽灵女王后解锁。" : LockedDescription;
        public static bool IsUnlocked(CampMetaState meta, string id) => id switch
        {
            TalentCatalog.BerserkerId => meta?.BerserkerUnlocked==true,
            TalentCatalog.LichQueenId => meta?.LichUnlocked==true,
            _=>true
        };
        public static System.Collections.Generic.List<string> CollectUnlocked(CampMetaState meta,System.Collections.Generic.IEnumerable<string> ids)
        {
            var result=new System.Collections.Generic.List<string>();
            if(ids!=null)foreach(var id in ids)if(IsUnlocked(meta,id))result.Add(id);
            return result;
        }
        public static bool RecordVictory(CampMetaState meta, int floor, bool boss, bool training, bool tutorial,string bossId=null)
        {
            if(meta==null||floor!=20||!boss||training||tutorial)return false;
            if(bossId=="char_skeleton_king"&&!meta.BerserkerUnlocked)
            {meta.BerserkerUnlocked=true;meta.GetOrCreate(TalentCatalog.BerserkerId);return true;}
            if(bossId=="char_ghost_queen"&&!meta.LichUnlocked)
            {meta.LichUnlocked=true;meta.GetOrCreate(TalentCatalog.LichQueenId);return true;}
            return false;
        }
    }
}
