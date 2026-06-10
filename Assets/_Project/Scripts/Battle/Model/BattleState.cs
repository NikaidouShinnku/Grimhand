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

        public List<CombatantState> Combatants { get; } = new();
        public List<CardInstanceState> PlayerDrawPile { get; } = new();
        public List<CardInstanceState> PlayerHand { get; } = new();
        public List<CardInstanceState> PlayerDiscardPile { get; } = new();

        public List<CardInstanceState> EnemyDrawPile { get; } = new();
        public List<CardInstanceState> EnemyHand { get; } = new();
        public List<CardInstanceState> EnemyDiscardPile { get; } = new();

        public List<CardInstanceState> GetDrawPile(TeamSide team) =>
            team == TeamSide.Player ? PlayerDrawPile : EnemyDrawPile;

        public List<CardInstanceState> GetHand(TeamSide team) =>
            team == TeamSide.Player ? PlayerHand : EnemyHand;

        public List<CardInstanceState> GetDiscardPile(TeamSide team) =>
            team == TeamSide.Player ? PlayerDiscardPile : EnemyDiscardPile;

        public Dictionary<int, CardInstanceState> CardsById { get; } = new();
        public Dictionary<string, string> CharacterOwnerByCombatantId { get; } = new();

        public LastActionSnapshot LastAction { get; set; } = LastActionSnapshot.None;

        public BattlePlan PlayerPlan { get; } = new();
        public BattlePlan EnemyPlan { get; } = new();
        public List<EnemyIntentSlot> EnemyIntents { get; } = new();

        public Dictionary<int, string> ResolutionTargets { get; } = new();

        public int NextCardInstanceId { get; set; } = 1;
        public int NextSummonInstanceId { get; set; } = 1;

        public int MiracleLeafRevivesRemaining { get; set; }
        public bool JadeDaggerFirstKillConsumed { get; set; }
        public bool TeamFirstHitReductionPending { get; set; } = true;

        public bool ConsumableUsedThisBattle { get; set; }
        public float ConsumableDodgeBonusThisTurn { get; set; }
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

        /// <summary>无尽血刃等：单张牌实例在本场战斗中的 outgoing 伤害倍率（100=1×）。</summary>
        public Dictionary<int, int> CardInstanceDamageMultiplierPercent { get; } = new();

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
