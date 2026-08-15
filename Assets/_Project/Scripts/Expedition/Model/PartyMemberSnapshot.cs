using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Model
{
    public sealed class PartyMemberSnapshot
    {
        public string CharacterDefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Xp { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        /// <summary>远征期间永久扣除的最大生命（事件等），从有效 MaxHp 中减去。</summary>
        public int MaxHpPenalty { get; set; }
        /// <summary>祭坛 HP+5 等永久加血（Sync 时计入有效 MaxHp，避免被基础值覆盖）。</summary>
        public int AltarMaxHpBonus { get; set; }
        /// <summary>该角色已购买祭坛 HP+5 的次数（费用按角色独立递增）。</summary>
        public int AltarHpPlus5Purchases { get; set; }
        public int PersonalAttackBonus { get; set; }
        /// <summary>从基础牌组移除的卡牌计数（definitionId → 张数）。</summary>
        public Dictionary<string, int> RemovedCardCounts { get; } = new();
        /// <summary>卡牌升级等级（deckInstanceId → 已升级次数）。</summary>
        public Dictionary<string, int> CardUpgradeLevels { get; } = new();
        /// <summary>与初始套牌 config 槽位一一对应的实例 id（删牌时不回收，保证升级绑定稳定）。</summary>
        public List<string> BaseDeckInstanceIds { get; } = new();
        /// <summary>旧版百分比强化（迁移用，不再写入）。</summary>
        public Dictionary<string, int> CardPowerBonusPercent { get; } = new();
        /// <summary>单张攻击牌永久平铺增伤（deckInstanceId → 额外伤害）。</summary>
        public Dictionary<string, int> CardFlatDamageBonuses { get; } = new();
        public List<CardTemplate> BonusCards { get; } = new();
        /// <summary>军营收藏牌组，仅在祭坛取出后加入远征卡组；战斗初始套牌来自 Content 默认配置。</summary>
        public List<string> CampDeckCardIds { get; } = new();
        /// <summary>遗留标记：为 true 时以军营牌组作为战斗初始套牌（新局不再自动开启）。</summary>
        public bool UsesCampDeckAsBattleBase { get; set; }
        /// <summary>本局已从收藏取走的收藏槽位索引。</summary>
        public HashSet<int> ExtractedCampCardIndices { get; } = new();
        public string SelectedTalentSlot1Id { get; set; } = "";
        public string SelectedTalentSlot2Id { get; set; } = "";
        /// <summary>祭坛 SPD+1 已购买次数（每角色最多 2 次）。</summary>
        public int AltarSpeedUpgrades { get; set; }
        public int PersonalSpeedBonus { get; set; }
    }
}
