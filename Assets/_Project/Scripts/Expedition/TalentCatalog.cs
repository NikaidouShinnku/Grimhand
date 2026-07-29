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
        public const string SnakeQueenId = "char_snake_queen";
        public const string LichQueenId = "char_lich_queen";

        public static readonly IReadOnlyList<string> PlayableCharacterIds = new[]
        {
            KnightId,
            MageId,
            RangerId,
            SnakeQueenId,
            LichQueenId
        };

        static readonly List<TalentDefinition> All = BuildAll();
        static readonly Dictionary<string, TalentDefinition> ById = BuildLookup(All);
        static readonly Dictionary<string, List<TalentDefinition>> ByCharacter = BuildCharacterLookup(All);

        public static IReadOnlyList<TalentDefinition> GetAll() => All;

        public static TalentDefinition Get(string talentId)
        {
            if (talentId == "talent_ranger_s2_lv3")
                talentId = "talent_ranger_s1_lv3";

            return !string.IsNullOrEmpty(talentId) && ById.TryGetValue(talentId, out var def) ? def : null;
        }

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
            list.AddRange(BuildSnakeQueen());
            list.AddRange(BuildLichQueen());
            return list;
        }

        static IEnumerable<TalentDefinition> BuildKnight()
        {
            yield return Def(KnightId, 1, 1, "talent_knight_s1_lv1", "前排护甲",
                "如果处于前排，回合开始时获得2护甲");
            yield return Def(KnightId, 1, 3, "talent_knight_s1_lv3", "突击姿态",
                "当不处于前排时，获得33%易伤和33%增伤");
            yield return Def(KnightId, 1, 5, "talent_knight_s1_lv5", "余护甲回血",
                "回合结束时如果护甲量大于0，则回复2HP");
            yield return Def(KnightId, 1, 7, "talent_knight_s1_lv7", "背水一战",
                "当HP低于护甲时，获得20%增伤");
            yield return Def(KnightId, 1, 10, "talent_knight_s1_lv10", "绝地格挡",
                "每场战斗第一次即将受到致死攻击时，获得50点护甲");

            yield return Def(KnightId, 2, 2, "talent_knight_s2_lv2", "应对护甲",
                "成功应对攻击后，下回合开始时获得5护甲");
            yield return Def(KnightId, 2, 3, "talent_knight_s2_lv3", "应对增伤",
                "成功应对攻击后，获得20%增伤（2回合）");
            yield return Def(KnightId, 2, 6, "talent_knight_s2_lv6", "战阵鼓舞",
                "战士场上存活时，全队最大HP+10");
            yield return Def(KnightId, 2, 8, "talent_knight_s2_lv8", "连击",
                "如果一回合中连续使用三张战士的攻击牌，当回合获得33%增伤（点击出牌后立刻获得）");
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
                "每场战斗第一张状态牌能量消耗-1（任意角色的第一张状态牌皆可）");
            yield return Def(MageId, 2, 4, "talent_mage_s2_lv4", "剧毒",
                "施加的中毒层数+2");
            yield return Def(MageId, 2, 6, "talent_mage_s2_lv6", "初击减速",
                "每场战斗中，受到法老伤害的第一个敌人获得1层减速（永久）");
            yield return Def(MageId, 2, 10, "talent_mage_s2_lv10", "永驻毒蚀",
                "所有施加的中毒层数-1，但持续时间全部变为永久");
        }

        static IEnumerable<TalentDefinition> BuildRanger()
        {
            yield return Def(RangerId, 1, 1, "talent_ranger_s1_lv1", "温和献祭",
                "献祭类卡牌减少25%的血量消耗");
            yield return Def(RangerId, 1, 3, "talent_ranger_s1_lv3", "微献保护",
                "献祭生命值小于5时不会扣血");
            yield return Def(RangerId, 1, 5, "talent_ranger_s1_lv5", "血怒献祭",
                "献祭类卡牌增加30%伤害，但同时增加40%血量消耗");
            yield return Def(RangerId, 1, 7, "talent_ranger_s1_lv7", "低血狂怒",
                "当HP低于30%时，获得25%增伤");
            yield return Def(RangerId, 1, 10, "talent_ranger_s1_lv10", "血债累击",
                "献祭总血量每达到50点所有攻击牌增加1点伤害，最多可增加10点（整场远征累计）");

            yield return Def(RangerId, 2, 2, "talent_ranger_s2_lv2", "嗜血护甲",
                "吸血效果超出HP上限时，溢出转化为等量护甲");
            yield return Def(RangerId, 2, 4, "talent_ranger_s2_lv4", "血祭节流",
                "每次献祭后，下一张牌能量消耗-1（最低0）");
            yield return Def(RangerId, 2, 6, "talent_ranger_s2_lv6", "无尽血刃",
                "远征开始时，将一张「无尽血刃」置入该角色牌组");
            yield return Def(RangerId, 2, 8, "talent_ranger_s2_lv8", "孤猎",
                "非Boss战中，若敌方队伍只剩一人，则获得30%增伤");
        }

        static IEnumerable<TalentDefinition> BuildSnakeQueen()
        {
            yield return Def(SnakeQueenId, 1, 1, "talent_snake_s1_lv1", "毒血免疫",
                "使自身免疫中毒伤害");
            yield return Def(SnakeQueenId, 1, 4, "talent_snake_s1_lv4", "毒甲共生",
                "自身每拥有1层中毒，便视为拥有1%强固");
            yield return Def(SnakeQueenId, 1, 6, "talent_snake_s1_lv6", "毒息汲取",
                "敌人受到中毒伤害时，自身回复1HP");
            yield return Def(SnakeQueenId, 1, 10, "talent_snake_s1_lv10", "以毒养命",
                "自身受到的中毒伤害变为治疗");

            yield return Def(SnakeQueenId, 2, 2, "talent_snake_s2_lv2", "毒蜕净化",
                "受到一次性大于自身25%最大HP的伤害后，清除自身所有负面状态");
            yield return Def(SnakeQueenId, 2, 4, "talent_snake_s2_lv4", "蛇之疾速",
                "若任何敌人拥有中毒状态，获得+1SPD（不可叠加）");
            yield return Def(SnakeQueenId, 2, 7, "talent_snake_s2_lv7", "毒囊武装",
                "远征开始时，将一张「引爆毒囊」置入该角色牌组");
            yield return Def(SnakeQueenId, 2, 10, "talent_snake_s2_lv10", "慢性毒素",
                "中毒的持续时间结束时，将中毒层数减半而非完全消除（仅正常持续时间结束时生效，卡牌消除无法联动）");
        }

        static IEnumerable<TalentDefinition> BuildLichQueen()
        {
            yield return Def(LichQueenId, 1, 1, "talent_lich_s1_lv1", "虚界恩赐",
                "获得虚化状态时，回复3HP");
            yield return Def(LichQueenId, 1, 4, "talent_lich_s1_lv4", "灵体无伤",
                "在虚化状态下受到伤害不会掉血且会回复2HP");
            yield return Def(LichQueenId, 1, 7, "talent_lich_s1_lv7", "零点共鸣",
                "回合开始时，若剩余能量为0，则额外回复1能量");
            yield return Def(LichQueenId, 1, 9, "talent_lich_s1_lv9", "灵火齐奏",
                "若一回合中打出的所有卡牌都属于巫妖女王，则下回合开始时，对所有敌人造成10伤害");

            yield return Def(LichQueenId, 2, 2, "talent_lich_s2_lv2", "灵界专注",
                "自身在非战斗回合造成的伤害拥有10%增伤");
            yield return Def(LichQueenId, 2, 5, "talent_lich_s2_lv5", "魂火节流",
                "巫妖女王每场战斗使用的第一张消耗牌能量消耗-1");
            yield return Def(LichQueenId, 2, 8, "talent_lich_s2_lv8", "周期虚化",
                "每4回合巫妖女王会获得虚化（1回合）");
            yield return Def(LichQueenId, 2, 10, "talent_lich_s2_lv10", "封印武装",
                "远征开始时，将一张「灵界封印」置入该角色牌组。当成功封印时，将被封印的卡加入玩家手牌，但是其获得消耗关键词并且费用+1");
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
