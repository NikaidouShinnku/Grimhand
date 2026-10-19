using System.Collections.Generic;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Model
{
    // Instance-owned battle facts. Persist the public progress fields as well as earned flags.
    public sealed class AchievementBattleTelemetry
    {
        public bool ThreeResponsesInTurn { get; set; }
        public bool ThreePoisonKills { get; set; }
        public bool PoisonBurnKill { get; set; }
        public bool ThreeKillsOneAttack { get; set; }
        public bool FrontBackKillsInTurn { get; set; }
        public bool RetainedThenUsed { get; set; }
        public HashSet<string> RespondedPlayerIds { get; } = new();
        public int ConsumablesUsed { get; set; }
        public int PoisonKills { get; set; }
        public int ProgressTurn { get; set; }
        public HashSet<int> RespondedEnemyAttackIds { get; } = new();
        public HashSet<FormationSlot> KilledPositions { get; } = new();
        public HashSet<string> DeadEnemyIds { get; } = new();
        public Dictionary<int, int> RetainedTurnEnds { get; } = new();
        public Dictionary<int, int> LastRetainedTurn { get; } = new();
        int attackId;
        readonly HashSet<string> attackKills = new();

        static bool RealPlayer(CombatantState actor) => actor?.Team == TeamSide.Player
            && !string.IsNullOrEmpty(actor.Id) && !actor.Id.StartsWith("summon_") && actor.CharacterDefinitionId is
                "char_berserker" or "char_knight" or "char_warrior" or "char_mage" or "char_pharaoh"
                or "char_ranger" or "char_demon" or "char_snake_queen" or "char_viper_queen"
                or "char_lich_queen" or "char_lich";

        void Turn(int turn)
        {
            if (ProgressTurn == turn) return;
            ProgressTurn = turn;
            RespondedEnemyAttackIds.Clear();
            KilledPositions.Clear();
        }

        public void Responded(BattleState state, CombatantState actor, CardInstanceState enemyCard)
        {
            if (!RealPlayer(actor) || enemyCard == null || enemyCard.CardType != CardType.Attack) return;
            // A borrowed enemy or a later temporary ally is not an original party member.
            if (state.Config.Combatants.Count > 0 && !state.Config.Combatants.Exists(c =>
                    c.Id == actor.Id && c.Team == TeamSide.Player && c.CharacterDefinitionId == actor.CharacterDefinitionId)) return;
            RespondedPlayerIds.Add(actor.Id);
            Turn(state.TurnNumber);
            RespondedEnemyAttackIds.Add(enemyCard.InstanceId);
            ThreeResponsesInTurn |= RespondedEnemyAttackIds.Count >= 3;
        }

        public void BeginCard(CombatantState actor, CardInstanceState card)
        {
            attackId = RealPlayer(actor) && card.CardType == CardType.Attack ? card.InstanceId : 0;
            attackKills.Clear();
        }

        public void UsedCard(CombatantState actor, CardInstanceState card)
        {
            if (RealPlayer(actor) && card.SpectralRepeatSourceId == 0
                && RetainedTurnEnds.TryGetValue(card.InstanceId, out var count))
                RetainedThenUsed |= count >= 3;
            LeftHand(card);
            attackId = 0;
            attackKills.Clear();
        }

        public void LeftHand(CardInstanceState card)
        {
            RetainedTurnEnds.Remove(card.InstanceId);
            LastRetainedTurn.Remove(card.InstanceId);
        }

        public void Retained(BattleState state, CardInstanceState card)
        {
            if (LastRetainedTurn.TryGetValue(card.InstanceId, out var last) && last == state.TurnNumber) return;
            RetainedTurnEnds.TryGetValue(card.InstanceId, out var count);
            RetainedTurnEnds[card.InstanceId] = last == state.TurnNumber - 1 ? count + 1 : 1;
            LastRetainedTurn[card.InstanceId] = state.TurnNumber;
        }

        public void DirectKill(CombatantState actor, CombatantState target, int sourceCardId)
        {
            if (actor?.Team != TeamSide.Player || target.Team != TeamSide.Enemy
                || attackId == 0 || sourceCardId != attackId) return;
            attackKills.Add(target.Id);
            ThreeKillsOneAttack |= attackKills.Count >= 3;
        }

        public void Died(BattleState state, CombatantState target)
        {
            if (target.Team != TeamSide.Enemy || !DeadEnemyIds.Add(target.Id)) return;
            Turn(state.TurnNumber);
            KilledPositions.Add(target.Slot);
            FrontBackKillsInTurn |= KilledPositions.Contains(FormationSlot.Front) && KilledPositions.Contains(FormationSlot.Back);
            PoisonBurnKill |= StatusRules.HasStatus(target, StatusCatalog.Poison) && StatusRules.HasStatus(target, StatusCatalog.Burn);
        }

        public void PoisonKilled()
        {
            PoisonKills++;
            ThreePoisonKills |= PoisonKills >= 3;
        }

        public AchievementBattleTelemetry Copy()
        {
            var copy = new AchievementBattleTelemetry
            {
                ThreeResponsesInTurn = ThreeResponsesInTurn, ThreePoisonKills = ThreePoisonKills,
                PoisonBurnKill = PoisonBurnKill, ThreeKillsOneAttack = ThreeKillsOneAttack,
                FrontBackKillsInTurn = FrontBackKillsInTurn, RetainedThenUsed = RetainedThenUsed,
                ConsumablesUsed = ConsumablesUsed, PoisonKills = PoisonKills, ProgressTurn = ProgressTurn,
                attackId = attackId
            };
            copy.RespondedPlayerIds.UnionWith(RespondedPlayerIds);
            copy.RespondedEnemyAttackIds.UnionWith(RespondedEnemyAttackIds);
            copy.KilledPositions.UnionWith(KilledPositions);
            copy.DeadEnemyIds.UnionWith(DeadEnemyIds);
            copy.attackKills.UnionWith(attackKills);
            foreach (var pair in RetainedTurnEnds) copy.RetainedTurnEnds.Add(pair.Key, pair.Value);
            foreach (var pair in LastRetainedTurn) copy.LastRetainedTurn.Add(pair.Key, pair.Value);
            return copy;
        }
    }
}
