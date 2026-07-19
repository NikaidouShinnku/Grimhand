using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class CombatantState
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public TeamSide Team { get; set; }
        public FormationSlot Slot { get; set; }
        public string CharacterDefinitionId { get; set; } = "";

        public int Level { get; set; } = 1;
        public int Xp { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        /// <summary>远征战间 Hp=0，本场以 1 HP 复活进战。</summary>
        public bool EnteredFromExpeditionDeath { get; set; }
        /// <summary>v0.8 已废弃，恒为 0。</summary>
        public int BaseAttack { get; set; }
        /// <summary>v0.8 已废弃，恒为 0。</summary>
        public int BaseDefense { get; set; }
        /// <summary>v0.8 已废弃，恒为 0。</summary>
        public int Attack { get; set; }
        /// <summary>v0.8 已废弃，恒为 0。</summary>
        public int Defense { get; set; }

        /// <summary>v0.8：出站伤害固定加值（增伤等）。</summary>
        public int OutgoingDamageFlatBonus { get; set; }
        /// <summary>v0.8：出站伤害百分比加值。</summary>
        public int OutgoingDamagePercentBonus { get; set; }
        /// <summary>v0.8：出站伤害固定减值（虚弱等）。</summary>
        public int OutgoingDamageReductionFlat { get; set; }
        /// <summary>v0.8：入站伤害固定加值（易伤等）。</summary>
        public int IncomingDamageFlatBonus { get; set; }
        /// <summary>v0.8：入站伤害百分比加值（易伤等）。</summary>
        public int IncomingDamagePercentBonus { get; set; }
        /// <summary>v0.8：入站伤害百分比减伤。</summary>
        public int IncomingDamageReductionPercent { get; set; }
        /// <summary>v0.8：获得护甲固定加值。</summary>
        public int BlockGainFlatBonus { get; set; }
        /// <summary>v0.8：获得护甲百分比加值。</summary>
        public int BlockGainPercentBonus { get; set; }
        /// <summary>v0.8：获得护甲百分比降低。</summary>
        public int BlockGainReductionPercent { get; set; }

        /// <summary>本场战斗持久出站增伤（骷髅特性等）。</summary>
        public int PersistentOutgoingDamageFlatBonus { get; set; }
        /// <summary>本场战斗持久护甲获取加成。</summary>
        public int PersistentBlockGainFlatBonus { get; set; }
        public int Speed { get; set; }
        public int Block { get; set; }

        public List<StatusInstance> Statuses { get; } = new();

        /// <summary>出牌后武装，等待下一次受到攻击时消耗。</summary>

        public bool FirstAttackBonusPending { get; set; } = true;
        public bool BossFirstHitBlockPending { get; set; } = true;
        public List<string> Traits { get; } = new();
        public bool FirstDefenseBonusPending { get; set; } = true;
        public bool FirstHitReductionPending { get; set; } = true;
        public bool WarriorFirstHitBlockPending { get; set; } = true;
        public bool UsedAttackThisTurn { get; set; }
        public bool UsedDefenseThisTurn { get; set; }
        /// <summary>消耗品等：本回合 ATK 额外 +N%（回合开始时清零）。</summary>
        public int TurnAttackBonusPercent { get; set; }
        /// <summary>消耗品等：本回合 DEF 额外 +N%（回合开始时清零）。</summary>
        public int TurnDefenseBonusPercent { get; set; }
        public int PendingRevengeAttackBonus { get; set; }
        public int InvulnerableTurnsRemaining { get; set; }
        public int SacrificeAttackStacks { get; set; }

        /// <summary>本回合是否已被攻击命中（用于致命打击等条件加伤）。</summary>
        public bool HitThisTurn { get; set; }

        /// <summary>v0.91：本回合受到攻击次数（报复打击、无畏冲锋等）。</summary>
        public int HitsTakenThisTurn { get; set; }

        /// <summary>嗜血抓挠等：下次攻击额外固定伤害，出手后清零。</summary>
        public int NextAttackFlatBonus { get; set; }

        /// <summary>剩余无法出牌回合数（阿努比斯化身、祈求远古蛇神等硬锁）。</summary>
        public int CardsLockedTurnsRemaining { get; set; }

        /// <summary>缠绕施法锁：仅缠绕期间；目标全灭可提前解除。白名单牌可在此锁下使用。</summary>
        public int ConstrictLockTurnsRemaining { get; set; }

        /// <summary>剩余无法使用攻击牌回合数（蛛网包裹等）。</summary>
        public int AttackCardsLockedTurnsRemaining { get; set; }

        public bool GhostQueenEnrageTriggered { get; set; }
        public bool SkipRemainingPlaysThisTurn { get; set; }

        /// <summary>血怒层数（绿皮巨魔）。</summary>
        public int BloodRageStacks { get; set; }

        /// <summary>本回合首次受击闪避（巨翼蝙蝠）。</summary>
        public bool FirstHitDodgePending { get; set; } = true;

        /// <summary>本回合剩余时间无敌（幽灵隐身应对）。</summary>
        public bool InvulnerableRestOfTurn { get; set; }

        /// <summary>上一完整回合是否受过 HP 伤害（史莱姆再生；回合开始读取后清零）。</summary>
        public bool TookDamageLastTurn { get; set; }

        /// <summary>上一完整回合是否受过 HP 伤害（供本回合规划/结算使用，如无畏冲锋）。</summary>
        public bool TookDamagePreviousTurn { get; set; }

        /// <summary>本场已结算卡牌数（骷髅特性）。</summary>
        public int CardsResolvedCount { get; set; }

        /// <summary>幽灵精英低血虚化是否已触发。</summary>
        public bool WraithEliteEnrageTriggered { get; set; }

        /// <summary>本回合是否处于应对武装（偷袭增伤等）。</summary>
        public bool RespondArmedThisTurn { get; set; }

        /// <summary>额外闪避率（暗影闪避等）。</summary>
        public float DodgeChanceBonus { get; set; }

        /// <summary>低血速度加成已应用的数值。</summary>
        public int LowHpSpeedBonusApplied { get; set; }

        /// <summary>本回合已结算卡牌数（石像鬼等）。</summary>
        public int CardsResolvedThisTurn { get; set; }

        /// <summary>下回合继承的护甲（石傀儡）。</summary>
        public int CarryOverBlock { get; set; }

        /// <summary>天赋：本回合已打出的攻击牌数（连击）。</summary>
        public int TalentAttackCardsThisTurn { get; set; }
        /// <summary>天赋：应对成功后下次受伤减伤。</summary>
        public bool TalentRespondDamageReductionPending { get; set; }
        /// <summary>天赋：应对成功后下次攻击增伤。</summary>
        public bool TalentRespondAttackBonusPending { get; set; }
        /// <summary>天赋：铁壁转化待加到下一张伤害牌的数值。</summary>
        public int TalentIronWallPendingDamageBonus { get; set; }
        /// <summary>天赋：绝地格挡已触发。</summary>
        public bool TalentLastStandBlockUsed { get; set; }
        /// <summary>天赋：不再获得护甲，转为伤害加成。</summary>
        public bool TalentDisableBlockGain { get; set; }
        /// <summary>天赋：献祭后下一张牌减费。</summary>
        public bool TalentNextSacrificeEnergyDiscount { get; set; }

        /// <summary>v0.9：本回合是否回复过生命（鲜血撕咬等）。</summary>
        public bool HealedThisTurn { get; set; }
        /// <summary>v0.9：本场战斗累计应对成功次数（战术大师的终结技等）。</summary>
        public int RespondSuccessCount { get; set; }

        /// <summary>石像鬼本回合攻击姿态加值。</summary>
        public int GargoyleStanceAttackBonus { get; set; }

        /// <summary>石像鬼本回合防御姿态加值。</summary>
        public int GargoyleStanceDefenseBonus { get; set; }

        /// <summary>本回合已解析的第一张牌类型（沉睡之石等用）。</summary>
        public CardType? FirstCardTypeThisTurn { get; set; }

        /// <summary>人鱼战士：每打出 0 费牌累计 +5% 攻击。</summary>
        public int MermaidZeroCostAttackBonusPercent { get; set; }

        /// <summary>鼠人族群攻击加成（百分比）。</summary>
        public int RatPackAttackBonusPercent { get; set; }

        public bool IsAlive => Hp > 0;

        public bool IsHardCardsLocked => CardsLockedTurnsRemaining > 0;

        public bool IsConstrictCardsLocked => ConstrictLockTurnsRemaining > 0;

        public bool IsCardsLocked => IsHardCardsLocked || IsConstrictCardsLocked;

        public bool IsAttackCardsLocked => AttackCardsLockedTurnsRemaining > 0;
    }
}
