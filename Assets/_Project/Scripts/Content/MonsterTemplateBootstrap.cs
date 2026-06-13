using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Content
{
    /// <summary>从 ScriptableObject / Editor 资产填充 ExpeditionConfig.MonsterTemplates。</summary>
    public static class MonsterTemplateBootstrap
    {
        public static readonly string[] EditorAssetPaths =
        {
            "Assets/_Project/Data/Characters/Character_Goblin.asset",
            "Assets/_Project/Data/Characters/Character_Slime.asset",
            "Assets/_Project/Data/Characters/Character_Skeleton.asset",
            "Assets/_Project/Data/Characters/Character_Skeleton_Elite.asset",
            "Assets/_Project/Data/Characters/Character_Wraith.asset",
            "Assets/_Project/Data/Characters/Character_Wraith_Elite.asset",
            "Assets/_Project/Data/Characters/Character_Ogre.asset",
            "Assets/_Project/Data/Characters/Character_Bat.asset",
            "Assets/_Project/Data/Characters/Character_Rat.asset",
            "Assets/_Project/Data/Characters/Character_Chain_Wraith.asset",
            "Assets/_Project/Data/Characters/Character_Gargoyle.asset",
            "Assets/_Project/Data/Characters/Character_Spider_Lady.asset",
            "Assets/_Project/Data/Characters/Character_Stone_Golem.asset"
        };

        public static void EnsureMonsterTemplates(ExpeditionConfig config, ExpeditionSetupSO setup = null)
        {
            if (config == null || config.MonsterTemplates.Count > 0)
                return;

            if (setup != null && setup.MonsterCharacters.Count > 0)
            {
                foreach (var monster in setup.MonsterCharacters)
                {
                    if (monster == null || string.IsNullOrEmpty(monster.CharacterId))
                        continue;

                    MonsterTemplateRegistry.TryAddTemplate(
                        config,
                        BattleSetupSO.BuildCombatantConfigPublic(monster));
                }
            }

#if UNITY_EDITOR
            if (config.MonsterTemplates.Count == 0)
            {
                foreach (var path in EditorAssetPaths)
                {
                    var monster = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
                    if (monster == null || string.IsNullOrEmpty(monster.CharacterId))
                        continue;

                    MonsterTemplateRegistry.TryAddTemplate(
                        config,
                        BattleSetupSO.BuildCombatantConfigPublic(monster));
                }
            }
#endif
        }
    }
}
