using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>
    /// 全量卡牌定义目录（正式包可读）。由 CardDefinitionCatalogBinder 从 Data/Cards 同步。
    /// </summary>
    [CreateAssetMenu(fileName = "CardDefinitionCatalog", menuName = "Grimhand/Card Definition Catalog")]
    public sealed class CardDefinitionCatalogSO : ScriptableObject
    {
        public List<CardDefinitionSO> Cards = new();
    }
}
