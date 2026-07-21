using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    /// <summary>图鉴解锁判定与战斗/掉落写入。</summary>
    public static class CodexProgressRules
    {
        public static bool HasOwnedCharacter(PlayerProfileState profile, string characterId)
        {
            if (profile?.Meta?.Characters == null || string.IsNullOrEmpty(characterId))
                return false;

            return profile.Meta.Characters.ContainsKey(characterId);
        }

        public static bool HasOwnedCard(PlayerProfileState profile, string cardId)
        {
            if (profile == null || string.IsNullOrEmpty(cardId))
                return false;

            if (profile.Collection?.Entries != null)
            {
                foreach (var entry in profile.Collection.Entries)
                {
                    if (entry == cardId)
                        return true;
                }
            }

            if (profile.Roster?.Members == null)
                return false;

            foreach (var member in profile.Roster.Members)
            {
                if (member?.DeckCardIds == null)
                    continue;

                foreach (var deckCardId in member.DeckCardIds)
                {
                    if (deckCardId == cardId)
                        return true;
                }
            }

            return false;
        }

        public static bool HasOwnedRelic(CodexProgressState codex, string relicId) =>
            codex != null
            && !string.IsNullOrEmpty(relicId)
            && codex.SeenRelicIds.Contains(relicId);

        public static bool HasSeenEnemy(CodexProgressState codex, string characterId) =>
            codex != null
            && !string.IsNullOrEmpty(characterId)
            && codex.SeenEnemyIds.Contains(characterId);

        public static bool HasSeenEnemyCard(CodexProgressState codex, string cardId) =>
            codex != null
            && !string.IsNullOrEmpty(cardId)
            && codex.SeenEnemyCardIds.Contains(cardId);

        public static bool MarkEnemySeen(CodexProgressState codex, string characterId)
        {
            if (codex == null || string.IsNullOrEmpty(characterId))
                return false;
            return codex.SeenEnemyIds.Add(characterId);
        }

        public static bool MarkEnemyCardSeen(CodexProgressState codex, string cardId)
        {
            if (codex == null || string.IsNullOrEmpty(cardId))
                return false;
            return codex.SeenEnemyCardIds.Add(cardId);
        }

        public static bool MarkRelicSeen(CodexProgressState codex, string relicId)
        {
            if (codex == null || string.IsNullOrEmpty(relicId))
                return false;
            return codex.SeenRelicIds.Add(relicId);
        }

        /// <summary>从当前战斗配置写入遇见过的敌人与敌卡。</summary>
        public static bool RecordFromBattleConfig(CodexProgressState codex, BattleConfig config)
        {
            if (codex == null || config?.Combatants == null)
                return false;

            var changed = false;
            foreach (var combatant in config.Combatants)
            {
                if (combatant == null || combatant.Team != TeamSide.Enemy)
                    continue;

                if (MarkEnemySeen(codex, combatant.CharacterDefinitionId))
                    changed = true;

                changed |= MarkCardsFromTemplates(codex, combatant.SkillPoolCandidates);
                changed |= MarkCardsFromTemplates(codex, combatant.DeckTemplates);
            }

            if (config.CardCatalog != null)
            {
                foreach (var pair in config.CardCatalog)
                {
                    var template = pair.Value;
                    if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                        continue;

                    if (ExpeditionCardPool.IsPlayerCharacterId(template.OwnerCharacterId))
                        continue;

                    if (PlayerCardCatalogRules.IsAllowedPlayerCardId(template.DefinitionId))
                        continue;

                    if (MarkEnemyCardSeen(codex, template.DefinitionId))
                        changed = true;
                }
            }

            return changed;
        }

        public static bool RecordRelicsFromRun(CodexProgressState codex, IEnumerable<string> relicIds)
        {
            if (codex == null || relicIds == null)
                return false;

            var changed = false;
            foreach (var relicId in relicIds)
            {
                if (MarkRelicSeen(codex, relicId))
                    changed = true;
            }

            return changed;
        }

        static bool MarkCardsFromTemplates(CodexProgressState codex, IEnumerable<CardTemplate> templates)
        {
            if (templates == null)
                return false;

            var changed = false;
            foreach (var template in templates)
            {
                if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                    continue;

                if (MarkEnemyCardSeen(codex, template.DefinitionId))
                    changed = true;
            }

            return changed;
        }
    }
}
