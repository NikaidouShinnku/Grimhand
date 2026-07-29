using System.Collections.Generic;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Model
{
    public sealed class BattleState
    {
        public BattleConfig Config { get; set; } = new();
        public int TurnNumber { get; set; } = 1;
        public TurnPhase Phase { get; set; } = TurnPhase.Draw;
        public BattleOutcome Outcome { get; set; } = BattleOutcome.Ongoing;

        public int EnergyCurrent { get; set; }
        public int EnergyMax { get; set; } = 8;
        public bool IsFirstPlayerTurn { get; set; } = true;

        public int PendingDrawNextTurn { get; set; }

        /// <summary>下回合额外抽到的牌费用减免（召唤卡牌之灵等，仅作用于 PendingDrawNextTurn 那几张）。</summary>
        public int PendingDrawNextTurnCostReduction { get; set; }

        /// <summary>下回合开始时额外回复的能量（翡翠短刀等）。</summary>
        public int PendingEnergyNextTurn { get; set; }

        public List<CombatantState> Combatants { get; } = new();
        public List<CardInstanceState> PlayerDrawPile { get; } = new();
        public List<CardInstanceState> PlayerHand { get; } = new();
        public List<CardInstanceState> PlayerDiscardPile { get; } = new();
        /// <summary>已打出的消耗牌堆（本场战斗不再进入抽牌，除非神圣轮回等回收）。</summary>
        public List<CardInstanceState> PlayerExhaustPile { get; } = new();

        public List<CardInstanceState> EnemyDrawPile { get; } = new();
        public List<CardInstanceState> EnemyHand { get; } = new();
        public List<CardInstanceState> EnemyDiscardPile { get; } = new();
        public List<CardInstanceState> EnemyExhaustPile { get; } = new();

        public List<CardInstanceState> GetDrawPile(TeamSide team) =>
            team == TeamSide.Player ? PlayerDrawPile : EnemyDrawPile;

        public List<CardInstanceState> GetHand(TeamSide team) =>
            team == TeamSide.Player ? PlayerHand : EnemyHand;

        public List<CardInstanceState> GetDiscardPile(TeamSide team) =>
            team == TeamSide.Player ? PlayerDiscardPile : EnemyDiscardPile;

        public List<CardInstanceState> GetExhaustPile(TeamSide team) =>
            team == TeamSide.Player ? PlayerExhaustPile : EnemyExhaustPile;

        public Dictionary<int, CardInstanceState> CardsById { get; } = new();
        public Dictionary<string, string> CharacterOwnerByCombatantId { get; } = new();

        public LastActionSnapshot LastAction { get; set; } = LastActionSnapshot.None;

        /// <summary>上一击伤害结算是否触发了成功应对（溃烂钳击等消费）。</summary>
        public bool LastDamageHadRespondDefense { get; set; }

        public BattlePlan PlayerPlan { get; } = new();
        public BattlePlan EnemyPlan { get; } = new();
        public List<EnemyIntentSlot> EnemyIntents { get; } = new();

        public Dictionary<int, string> ResolutionTargets { get; } = new();

        public int NextCardInstanceId { get; set; } = 1;
        public int NextSummonInstanceId { get; set; } = 1;

        /// <summary>本场战斗中已死亡的鼠人数量（含召唤鼠）；用于鼠群狂怒全局叠层。</summary>
        public int RatDeathsThisBattle { get; set; }

        public int MiracleLeafRevivesRemaining { get; set; }
        public bool JadeDaggerFirstKillConsumed { get; set; }
        public bool TeamFirstHitReductionPending { get; set; } = true;

        public bool ConsumableUsedThisBattle { get; set; }
        public float ConsumableDodgeBonusThisTurn { get; set; }
        /// <summary>本回合敌方已结算的攻击牌数量（夜袭连斩等）。</summary>
        public int EnemyAttackCardsPlayedThisTurn { get; set; }
        public string LastPlayerAttackActorId { get; set; } = "";
        public CardInstanceState LastPlayerAttackCard { get; set; }
        /// <summary>上一回合最后打出的己方攻击牌（镜之碎片等）。</summary>
        public string PreviousTurnLastPlayerAttackActorId { get; set; } = "";
        public CardInstanceState PreviousTurnLastPlayerAttackCard { get; set; }

        /// <summary>key = 敌方牌 instanceId；在其造成伤害时按层叠顺序减伤。</summary>
        public Dictionary<int, List<RespondMitigationLayer>> RespondMitigationByEnemyCard { get; } = new();

        /// <summary>在对应敌方攻击演出结束后结算的弹反反击。</summary>
        public List<PendingParryStrike> PendingParryStrikes { get; } = new();

        /// <summary>敌方防御【应对攻击】武装层。</summary>
        public List<DefenderRespondArm> DefenderRespondArms { get; } = new();

        /// <summary>下回合开始时加入 Boss 额外手牌（不占抽牌上限）。</summary>
        public List<PendingBossBonusHand> PendingBossBonusHandsNextTurn { get; } = new();

        /// <summary>下回合玩家能量回复惩罚（摄魂等）。</summary>
        public int PendingPlayerEnergyRegenPenaltyNextTurn { get; set; }

        /// <summary>下回合敌方出牌能量预算惩罚（活体强化等，扣己方队伍）。</summary>
        public int PendingEnemyEnergyRegenPenaltyNextTurn { get; set; }

        /// <summary>下回合开始时获得的临时能量（灵魂吞噬等，可超过 EnergyMax）。</summary>
        public int PendingPlayerEnergyGainNextTurn { get; set; }

        /// <summary>绝望之魂：战斗中获虚化时，下回合开始再从弃牌堆回收。</summary>
        public bool PendingDespairSoulRecallNextTurn { get; set; }

        /// <summary>本回合被应对状态压制的敌方牌（如剑柄猛击），结算时跳过效果。</summary>
        public HashSet<int> SuppressedEnemyCardInstanceIds { get; } = new();

        /// <summary>X 费牌打出时实际消耗的能量（太阳神之怒等）。</summary>
        public Dictionary<int, int> EnergySpentByCardInstanceId { get; } = new();

        /// <summary>key = 牌 instanceId；该牌下次造成伤害时的倍率（%），无尽血刃等。</summary>
        public Dictionary<int, int> CardInstanceDamageMultiplierPercent { get; } = new();

        /// <summary>法师天赋：本局首张状态牌 -1 费（待消耗）。</summary>
        public bool TalentMageFirstStatusDiscountPending { get; set; }

        public bool PlayerRespondStatusUsedThisTurn { get; set; }
        public bool TalentMageFirstHitSlowPending { get; set; }
        public bool TalentMageReviveAvailable { get; set; }
        public int TalentRangerBloodDebtAttackBonus { get; set; }
        public int TalentSacrificeHpAccumulatedBattle { get; set; }

        /// <summary>巫妖女王 s2_lv5：本场战斗首张消耗牌 -1 费（待消耗）。</summary>
        public bool TalentLichFirstExhaustDiscountPending { get; set; }

        /// <summary>巫妖 s1_lv9：本回合已打出的玩家牌数量。</summary>
        public int TalentLichCardsPlayedThisTurn { get; set; }

        /// <summary>巫妖 s1_lv9：本回合已打出牌是否全属巫妖（无出牌时为 true）。</summary>
        public bool TalentLichAllCardsLichOwnedThisTurn { get; set; } = true;

        /// <summary>巫妖 s1_lv9：下回合开始对全体敌人造成的伤害。</summary>
        public int TalentLichPendingEnemyAoeNextTurn { get; set; }

        /// <summary>v0.91：灵魂纽带伙伴映射（本回合有效，回合开始清空）。</summary>
        public Dictionary<string, string> SoulBondPartnerByCombatantId { get; } = new();

        /// <summary>v0.91：沙之预知剩余回合（每回合看破全部意图）。</summary>
        public int RevealAllEnemyIntentsTurnsRemaining { get; set; }

        /// <summary>v0.91：魔神回响当前减费层数（洗入牌库时重置）。</summary>
        public Dictionary<string, int> DemonEchoCostReductionByCardId { get; } = new();

        /// <summary>v0.91：灵质护盾延迟至下回合开始的护甲。</summary>
        public Dictionary<string, int> PendingDelayedBlockByCombatantId { get; } = new();

        /// <summary>
        /// 灵质护盾延迟护甲：发放当回合末跳过一次清甲，保留到再下一回合末。
        /// </summary>
        public HashSet<string> RetainBlockOnceCombatantIds { get; } = new();

        /// <summary>灵界封印：接下来失效的敌方出牌次数（无需指定目标）。</summary>
        public int PendingEnemyCardSeals { get; set; }

        /// <summary>灵能预知：等待玩家选择牌库顶检视结果。</summary>
        public bool AwaitingPsionicScry { get; set; }

        /// <summary>灵能预知：已从牌库顶取出、待玩家处理的牌（顺序=顶→下）。</summary>
        public List<CardInstanceState> PendingPsionicScryCards { get; } = new();

        /// <summary>v0.91：女王之吻待在下回合开始转化中毒为易伤。</summary>
        public bool QueenKissConversionPending { get; set; }

        /// <summary>下回合开始时施加的状态（蓄能等）。</summary>
        public List<PendingNextTurnStatus> PendingStatusesNextTurn { get; } = new();

        /// <summary>v0.9：本场战斗全队累计应对成功次数（战术大师的终结技按此计算伤害）。</summary>
        public int RespondSuccessCount { get; set; }

        /// <summary>魔焰颅骨：战斗开始前等待玩家选择。</summary>
        public bool AwaitingFelskullChoice { get; set; }

        public CombatantState GetCombatant(string id)
        {
            foreach (var c in Combatants)
            {
                if (c.Id == id)
                    return c;
            }

            return null;
        }

        public IEnumerable<CombatantState> GetTeam(TeamSide team)
        {
            foreach (var c in Combatants)
            {
                if (c.Team == team)
                    yield return c;
            }
        }

        public CardInstanceState GetCard(int instanceId)
        {
            CardsById.TryGetValue(instanceId, out var card);
            return card;
        }
    }
}
