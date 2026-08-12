using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Presentation.Battle
{
    /// <summary>训练场「测试怪物」列表：全部小怪与 Boss 的可生成模板。</summary>
    public static class TrainingMonsterCatalog
    {
        public sealed class Entry
        {
            public string CharacterId;
            public string DisplayName;
            public string Category;
            public CombatantConfig Template;
        }

        public static IReadOnlyList<Entry> BuildEntries()
        {
            var list = new List<Entry>();
            var seen = new HashSet<string>();

            foreach (var def in LoadEnemyDefinitions())
            {
                if (def == null || string.IsNullOrEmpty(def.CharacterId))
                    continue;
                if (def.Team != TeamSide.Enemy && def.CharacterId != "char_dummy")
                    continue;
                if (def.CharacterId == "char_dummy")
                    continue;
                if (!seen.Add(def.CharacterId))
                    continue;

                list.Add(new Entry
                {
                    CharacterId = def.CharacterId,
                    DisplayName = string.IsNullOrEmpty(def.DisplayName) ? def.CharacterId : def.DisplayName,
                    Category = ResolveCategory(def.CharacterId),
                    Template = FromDefinition(def)
                });
            }

            // 确保三 Boss / 囚笼即使 SO 未生成也能测
            EnsureBoss(list, WardenBossEncounterBuilder.CharacterId, "典狱长", "Boss",
                250, 22, 8, 5, FormationSlot.Back, CharacterTraitCatalog.WardenCageMaster);
            EnsureBoss(list, DarkKnightBossEncounterBuilder.CharacterId, "黑暗骑士", "Boss",
                350, 25, 10, 8, FormationSlot.Front, CharacterTraitCatalog.DarkKnightPoisonAura);
            EnsureBoss(list, CorruptedOceanGoddessBossEncounterBuilder.CharacterId, "腐化海洋女神", "Boss",
                400, 20, 10, 6, FormationSlot.Front, CharacterTraitCatalog.OceanGoddessTide);
            EnsureBoss(list, CharacterTraitCatalog.PrisonCageCharacterId, "囚笼", "Boss",
                150, 0, 5, 5, FormationSlot.Middle, CharacterTraitCatalog.PrisonCage);
            EnsureBoss(list, "char_ghost_queen", "幽灵女王", "Boss·幽灵女王",
                320, 25, 8, 7, FormationSlot.Middle, CharacterTraitCatalog.GhostQueenEnrage);
            EnsureBoss(list, "char_skeleton_king", "骷髅王", "Boss·骷髅王",
                280, 20, 10, 4, FormationSlot.Front, CharacterTraitCatalog.BossFirstHitBlock);

            list.Sort((a, b) =>
            {
                var cat = string.CompareOrdinal(a.Category, b.Category);
                return cat != 0 ? cat : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            return list;
        }

        static IEnumerable<CharacterDefinitionSO> LoadEnemyDefinitions()
        {
            var runtime = Resources.Load<CharacterDefinitionCatalogSO>("CharacterDefinitionCatalog_Demo");
            if (runtime?.Characters != null)
            {
                foreach (var character in runtime.Characters)
                    yield return character;
            }

#if UNITY_EDITOR
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinitionSO",
                         new[] { "Assets/_Project/Data/Characters" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            }
#endif
        }

        static void EnsureBoss(
            List<Entry> list,
            string id,
            string name,
            string category,
            int hp,
            int atk,
            int def,
            int spd,
            FormationSlot slot,
            string trait)
        {
            foreach (var e in list)
            {
                if (e.CharacterId == id)
                    return;
            }

            var template = new CombatantConfig
            {
                DisplayName = name,
                Team = TeamSide.Enemy,
                Slot = slot,
                CharacterDefinitionId = id,
                MaxHp = hp,
                BaseAttack = atk,
                BaseDefense = def,
                Speed = spd
            };
            if (!string.IsNullOrEmpty(trait))
                template.Traits.Add(trait);

            list.Add(new Entry
            {
                CharacterId = id,
                DisplayName = name,
                Category = category,
                Template = template
            });
        }

        static string ResolveCategory(string characterId) =>
            CardCodexCatalog.ResolveOwnerCategory(characterId);

        static CombatantConfig FromDefinition(CharacterDefinitionSO def)
        {
            var template = new CombatantConfig
            {
                DisplayName = def.DisplayName,
                Team = TeamSide.Enemy,
                Slot = def.Slot,
                CharacterDefinitionId = def.CharacterId,
                Level = def.Level,
                MaxHp = def.MaxHp,
                BaseAttack = def.BaseAttack,
                BaseDefense = def.BaseDefense,
                Speed = def.Speed,
                UseSkillPool = def.SkillPool != null && def.SkillPool.Count > 0
            };
            if (def.Traits != null)
                template.Traits.AddRange(def.Traits);
            return template;
        }
    }
}
