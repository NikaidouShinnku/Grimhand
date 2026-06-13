using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>小怪模板注册：保证远征开战前能解析组合表里的 char_* 模板。</summary>
    public static class MonsterTemplateRegistry
    {
        public static void TryAddTemplate(ExpeditionConfig config, CombatantConfig template)
        {
            if (config == null || template == null || string.IsNullOrEmpty(template.CharacterDefinitionId))
                return;

            if (template.Team != TeamSide.Enemy)
                return;

            foreach (var existing in config.MonsterTemplates)
            {
                if (existing?.CharacterDefinitionId == template.CharacterDefinitionId)
                    return;
            }

            config.MonsterTemplates.Add(template);
        }

        public static Dictionary<string, CombatantConfig> BuildTemplateMap(ExpeditionConfig config) =>
            MonsterEncounterBuilder.BuildMonsterTemplateMap(config?.MonsterTemplates);
    }
}
