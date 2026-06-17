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
        public int PersonalAttackBonus { get; set; }
        /// <summary>从基础牌组移除的卡牌计数（definitionId → 张数）。</summary>
        public Dictionary<string, int> RemovedCardCounts { get; } = new();
        /// <summary>卡牌效果强化百分比累加（definitionId → +N%）。</summary>
        public Dictionary<string, int> CardPowerBonusPercent { get; } = new();
        public List<CardTemplate> BonusCards { get; } = new();
        /// <summary>军营收藏牌（仅祭坛节点读取）；战斗牌组仍用初始套牌 + 奖励牌。</summary>
        public List<string> CampDeckCardIds { get; } = new();
        /// <summary>本局已从收藏取走的收藏槽位索引。</summary>
        public HashSet<int> ExtractedCampCardIndices { get; } = new();
        public string SelectedTalentSlot1Id { get; set; } = "";
        public string SelectedTalentSlot2Id { get; set; } = "";
    }
}
