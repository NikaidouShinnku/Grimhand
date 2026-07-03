using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Model
{
    public sealed class CardInstanceState
    {
        public int InstanceId { get; set; }
        public string DefinitionId { get; set; } = "";
        public string OwnerCharacterId { get; set; } = "";
        public int Cost { get; set; }
        public CardType CardType { get; set; }
        public bool IsUsable { get; set; } = true;
        /// <summary>绑定到具体战斗单位（同 charId 多名召唤物时区分归属）。</summary>
        public string OwnerCombatantId { get; set; } = "";
        /// <summary>回合开始注入的手牌，不占抽牌上限；回合末移除。</summary>
        public bool IsBonusHandCard { get; set; }
        /// <summary>速度结算/快速启动阶段抽入手牌，下回合开始时仍保留。</summary>
        public bool RetainInHandOverTurnEnd { get; set; }
        public string DisplayName { get; set; } = "";
        /// <summary>祭坛/事件升级次数，用于卡名显示 +N。</summary>
        public int UpgradeLevel { get; set; }
        public List<string> Keywords { get; } = new();
        public List<EffectActionSpec> Actions { get; } = new();
    }
}
