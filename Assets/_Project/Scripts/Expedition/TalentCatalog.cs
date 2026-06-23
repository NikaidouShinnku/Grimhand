using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>天赋池静态配置：战士 / 法老 / 恶魔，各两槽位（对照 Grimhand实际内容总览表.xlsx）。</summary>
    public static class TalentCatalog
    {
        public const string KnightId = "char_knight";
        public const string MageId = "char_mage";
        public const string RangerId = "char_ranger";

        public static readonly IReadOnlyList<string> PlayableCharacterIds = new[]
        {
            KnightId,
            MageId,
            RangerId
        };

        static readonly List<TalentDefinition> All = BuildAll();
        static readonly Dictionary<string, TalentDefinition> ById = BuildLookup(All);
        static readonly Dictionary<string, List<TalentDefinition>> ByCharacter = BuildCharacterLookup(All);

        public static IReadOnlyList<TalentDefinition> GetAll() => All;

        public static TalentDefinition Get(string talentId) =>
            !string.IsNullOrEmpty(talentId) && ById.TryGetValue(talentId, out var def) ? def : null;

        public static IReadOnlyList<TalentDefinition> GetForCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return System.Array.Empty<TalentDefinition>();

            return ByCharacter.TryGetValue(characterId, out var list)
                ? list
                : System.Array.Empty<TalentDefinition>();
        }

        public static IReadOnlyList<TalentDefinition> GetSlotTalents(string characterId, int slot)
        {
            var list = new List<TalentDefinition>();
            foreach (var talent in GetForCharacter(characterId))
            {
                if (talent.Slot == slot)
                    list.Add(talent);
            }

            list.Sort((a, b) => a.UnlockLevel.CompareTo(b.UnlockLevel));
            return list;
        }

        static List<TalentDefinition> BuildAll()
        {
            var list = new List<TalentDefinition>();
            list.AddRange(BuildKnight());
            list.AddRange(BuildMage());
            list.AddRange(BuildRanger());
            return list;
        }

        static IEnumerable<TalentDefinition> BuildKnight()
        {
            yield return Def(KnightId, 1, 1, "talent_knight_s1_lv1", "前排护甲",
                "如果处于前排，回合开始时获得+2护甲");
            yield return Def(KnightId, 1, 3, "talent_knight_s1_lv3", "突击姿态",
                "当不处于前排时，受到33%额外伤害，攻击牌增加33%伤害");
            yield return Def(KnightId, 1, 5, "talent_knight_s1_lv5", "余护甲回血",
                "回合结束时如果护甲量大于0，则回复2HP");
            yield return Def(KnightId, 1, 7, "talent_knight_s1_lv7", "背水一战",
                "当HP低于10%时，攻击牌增加50%伤害");
            yield return Def(KnightId, 1, 10, "talent_knight_s1_lv10", "绝地格挡",
                "每场战斗第一次即将受到致死攻击时，获得50点护甲");

            yield return Def(KnightId, 2, 2, "talent_knight_s2_lv2", "应对减伤",
                "成功触发应对后，下一次受到伤害减少20%");
            yield return Def(KnightId, 2, 3, "talent_knight_s2_lv3", "应对强击",
                "成功触发应对后，下一张攻击牌增加20%伤害");
            yield return Def(KnightId, 2, 6, "talent_knight_s2_lv6", "战阵鼓舞",
                "战士场上存活时，全队最大HP+10");
            yield return Def(KnightId, 2, 8, "talent_knight_s2_lv8", "连击",
                "如果一回合中连续使用三张攻击牌，当回合的所有攻击牌增加33%伤害");
            yield return Def(KnightId, 2, 10, "talent_knight_s2_lv10", "铁壁转化",
                "不再获得护甲，每当获取护甲时变为下一张攻击牌增加护甲量的伤害");
        }

        static IEnumerable<TalentDefinition> BuildMage()
        {
            yield return Def(MageId, 1, 1, "talent_mage_s1_lv1", "镜像护甲",
                "使用护甲类卡牌时，如果目标不是自己，自己也获得25%的护甲");
            yield return Def(MageId, 1, 5, "talent_mage_s1_lv5", "法老复苏",
                "每场远征中，法老获得一次复活机会并回复30%HP");
            yield return Def(MageId, 1, 8, "talent_mage_s1_lv8", "溢出护甲",
                "治疗超出目标HP上限时，溢出转化为等量护甲");
            yield return Def(MageId, 1, 10, "talent_mage_s1_lv10", "临终庇护",
                "法老死亡时，全队获得25护甲");

            yield return Def(MageId, 2, 2, "talent_mage_s2_lv2", "先声状态",
                "每场战斗第一张状态牌消耗能量-1");
            yield return Def(MageId, 2, 4, "talent_mage_s2_lv4", "剧毒",
                "施加的中毒层数+2");
            yield return Def(MageId, 2, 6, "talent_mage_s2_lv6", "初击减速",
                "每场战斗中，受到法老伤害的第一个敌人获得1层减速");
            yield return Def(MageId, 2, 10, "talent_mage_s2_lv10", "毒爆",
                "所有中毒层数的累计伤害都会在一回合内爆发");
        }

        static IEnumerable<TalentDefinition> BuildRanger()
        {
            yield return Def(RangerId, 1, 1, "talent_ranger_s1_lv1", "温和献祭",
                "献祭类卡牌减少10%的血量消耗");
            yield return Def(RangerId, 1, 5, "talent_ranger_s1_lv5", "血怒献祭",
                "献祭类卡牌增加30%伤害，但同时增加50%血量消耗");
            yield return Def(RangerId, 1, 7, "talent_ranger_s1_lv7", "低血狂怒",
                "当HP低于30%时，攻击牌增加25%伤害");
            yield return Def(RangerId, 1, 10, "talent_ranger_s1_lv10", "血债累击",
                "献祭总血量每达到50点所有攻击牌增加1点伤害，最多可增加10点（整场远征累计）");

            yield return Def(RangerId, 2, 2, "talent_ranger_s2_lv2", "嗜血护甲",
                "吸血效果超出HP上限时，溢出转化为等量护甲");
            yield return Def(RangerId, 2, 4, "talent_ranger_s2_lv4", "血祭节流",
                "每次献祭后，下一张牌能量消耗-1（最低0）");
            yield return Def(RangerId, 2, 6, "talent_ranger_s2_lv6", "无尽血刃",
                "远征开始时，将一张「无尽血刃」置入该角色牌组");
            yield return Def(RangerId, 2, 8, "talent_ranger_s2_lv8", "孤猎",
                "非Boss战中，若只有一个敌人，则攻击牌增加30%伤害");
        }

        static TalentDefinition Def(
            string characterId,
            int slot,
            int unlockLevel,
            string id,
            string shortTitle,
            string description) =>
            new()
            {
                CharacterId = characterId,
                Slot = slot,
                UnlockLevel = unlockLevel,
                Id = id,
                ShortTitle = shortTitle,
                Description = description
            };

        static Dictionary<string, TalentDefinition> BuildLookup(List<TalentDefinition> list)
        {
            var map = new Dictionary<string, TalentDefinition>();
            foreach (var talent in list)
                map[talent.Id] = talent;
            return map;
        }

        static Dictionary<string, List<TalentDefinition>> BuildCharacterLookup(List<TalentDefinition> list)
        {
            var map = new Dictionary<string, List<TalentDefinition>>();
            foreach (var talent in list)
            {
                if (!map.TryGetValue(talent.CharacterId, out var bucket))
                {
                    bucket = new List<TalentDefinition>();
                    map[talent.CharacterId] = bucket;
                }

                bucket.Add(talent);
            }

            return map;
        }
    }
}
