using System.Collections.Generic;

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

        public int MiracleLeafRevivesRemaining { get; set; }
        public bool JadeDaggerFirstKillConsumed { get; set; }

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
