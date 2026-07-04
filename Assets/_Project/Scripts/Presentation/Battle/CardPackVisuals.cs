using Grimhand.Content;
using Grimhand.Expedition;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class CardPackVisuals
    {
        public static Sprite GetPackIcon(string packId, BattleUiIconCatalogSO catalog)
        {
            if (catalog == null || string.IsNullOrEmpty(packId))
                return null;

            return packId switch
            {
                CardPackIds.Common => catalog.CardPackCommon,
                CardPackIds.Advanced => catalog.CardPackAdvanced,
                CardPackIds.Master => catalog.CardPackMaster,
                _ => null
            };
        }
    }
}
