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
        /// <summary>营地配置的 10 张牌；非空时取代默认牌组。</summary>
        public List<string> CampDeckCardIds { get; } = new();
    }
}
