using System.Collections.Generic;
using Grimhand.Expedition;

namespace Grimhand.Expedition.Model
{
    /// <summary>营地局外存档：各角色的等级与天赋配置（与出征编队独立）。</summary>
    public sealed class CampMetaState
    {
        public Dictionary<string, CharacterMetaProgress> Characters { get; } = new();

        public CharacterMetaProgress GetOrCreate(string characterId)
        {
            if (!Characters.TryGetValue(characterId, out var progress) || progress == null)
            {
                progress = new CharacterMetaProgress { CharacterDefinitionId = characterId };
                Characters[characterId] = progress;
            }

            return progress;
        }

        public static CampMetaState CreateDefaultDemo()
        {
            var state = new CampMetaState();
            foreach (var characterId in TalentCatalog.PlayableCharacterIds)
            {
                state.Characters[characterId] = new CharacterMetaProgress
                {
                    CharacterDefinitionId = characterId,
                    OutOfRunLevel = 10,
                    OutOfRunXp = 0
                };
            }

            return state;
        }
    }
}
