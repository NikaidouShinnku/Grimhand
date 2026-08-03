using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition.Tutorial
{
    /// <summary>精英教学战开场：固定首击打战士，并确保手牌有防御架势。</summary>
    public static class ExpeditionTutorialBattleSetup
    {
        public const string DefensiveStanceId = "w_defensive_stance";
        public const string BoneCrushId = "m_bone_crush";
        public const string KnightId = "char_knight";
        public const string SkeletonEliteId = "char_skeleton_elite";

        public static void ApplyEliteFirstTurn(BattleEngine engine)
        {
            if (engine?.State == null || engine.State.Phase != TurnPhase.Planning)
                return;

            var state = engine.State;
            EnsureDefensiveStanceInHand(state, engine);
            ForceEliteBoneCrushOnWarrior(state, engine);
        }

        static void EnsureDefensiveStanceInHand(BattleState state, BattleEngine engine)
        {
            if (FindCard(state.PlayerHand, DefensiveStanceId) != null)
                return;

            if (TryMoveToHand(state.PlayerDrawPile, state.PlayerHand, DefensiveStanceId))
                return;

            if (TryMoveToHand(state.PlayerDiscardPile, state.PlayerHand, DefensiveStanceId))
                return;

            var template = FindTemplate(state.Config, DefensiveStanceId);
            if (template != null)
                engine.AddCardTemplateToHand(template);
        }

        static void ForceEliteBoneCrushOnWarrior(BattleState state, BattleEngine engine)
        {
            var warrior = FindCombatant(state, TeamSide.Player, KnightId);
            if (warrior == null || !warrior.IsAlive)
                return;

            // 清空本回合敌方意图，改为固定骨碎斩打战士
            state.EnemyPlan.PlayQueue.Clear();
            state.EnemyPlan.EnergySpent = 0;
            state.EnemyIntents.Clear();

            var existing = FindCard(state.EnemyHand, BoneCrushId);
            if (existing != null)
            {
                state.EnemyPlan.PlayQueue.Add(existing.InstanceId);
                state.EnemyPlan.EnergySpent = System.Math.Max(0, existing.Cost);
                state.EnemyIntents.Add(new EnemyIntentSlot
                {
                    CardInstanceId = existing.InstanceId,
                    OwnerCombatantId = existing.OwnerCombatantId,
                    IsHidden = false,
                    OrderIndex = 0
                });
            }
            else
            {
                var template = FindTemplate(state.Config, BoneCrushId);
                if (template == null)
                    return;

                var spawned = engine.EnqueueEnemyIntentCard(template);
                if (spawned == null)
                    return;
            }

            if (state.EnemyPlan.PlayQueue.Count == 0)
                return;

            var firstId = state.EnemyPlan.PlayQueue[0];
            state.ResolutionTargets[firstId] = warrior.Id;

            // 保证意图列表首张可见
            if (state.EnemyIntents.Count > 0)
            {
                state.EnemyIntents[0].IsHidden = false;
                state.EnemyIntents[0].OrderIndex = 0;
            }
        }

        static bool TryMoveToHand(
            List<CardInstanceState> from,
            List<CardInstanceState> hand,
            string definitionId)
        {
            if (from == null || hand == null)
                return false;

            for (var i = 0; i < from.Count; i++)
            {
                var card = from[i];
                if (card == null || card.DefinitionId != definitionId)
                    continue;

                from.RemoveAt(i);
                hand.Add(card);
                return true;
            }

            return false;
        }

        static CardInstanceState FindCard(List<CardInstanceState> list, string definitionId)
        {
            if (list == null)
                return null;

            foreach (var card in list)
            {
                if (card != null && card.DefinitionId == definitionId)
                    return card;
            }

            return null;
        }

        static CombatantState FindCombatant(BattleState state, TeamSide team, string characterId)
        {
            foreach (var c in state.Combatants)
            {
                if (c != null && c.IsAlive && c.Team == team && c.CharacterDefinitionId == characterId)
                    return c;
            }

            return null;
        }

        static CardTemplate FindTemplate(BattleConfig config, string definitionId)
        {
            if (config?.Combatants == null)
                return null;

            foreach (var combatant in config.Combatants)
            {
                if (combatant == null)
                    continue;

                var found = FindInList(combatant.DeckTemplates, definitionId)
                            ?? FindInList(combatant.SkillPoolCandidates, definitionId);
                if (found != null)
                    return found;
            }

            return null;
        }

        static CardTemplate FindInList(IReadOnlyList<CardTemplate> list, string definitionId)
        {
            if (list == null)
                return null;

            foreach (var card in list)
            {
                if (card != null && card.DefinitionId == definitionId)
                    return card;
            }

            return null;
        }
    }
}
