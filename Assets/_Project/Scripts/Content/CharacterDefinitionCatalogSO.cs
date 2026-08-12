using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>
    /// 角色定义目录（正式包可读）。由 CharacterDefinitionCatalogBinder 从 Data/Characters 同步。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinitionCatalog", menuName = "Grimhand/Character Definition Catalog")]
    public sealed class CharacterDefinitionCatalogSO : ScriptableObject
    {
        public List<CharacterDefinitionSO> Characters = new();
    }
}
