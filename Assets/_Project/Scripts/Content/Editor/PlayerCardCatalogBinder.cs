#if UNITY_EDITOR
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class PlayerCardCatalogBinder
    {
        const string CardsRoot = "Assets/_Project/Data/Cards";
        const string ExpeditionSetupPath = "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset";

        [MenuItem("Grimhand/Content/Bind Player Card Catalog")]
        public static void BindPlayerCardCatalogMenu()
        {
            if (BindPlayerCardCatalogSilent())
                Debug.Log("已将全部玩家卡牌写入 ExpeditionSetup_Demo.PlayerCardCatalog。");
        }

        public static bool BindPlayerCardCatalogSilent()
        {
            var setup = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(ExpeditionSetupPath);
            if (setup == null)
            {
                Debug.LogWarning($"未找到 {ExpeditionSetupPath}。");
                return false;
            }

            setup.PlayerCardCatalog.Clear();
            var guids = AssetDatabase.FindAssets("t:CardDefinitionSO", new[] { CardsRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
                if (card == null || !PlayerCardCatalogRules.IsAllowedPlayerCard(card.CardId, card.OwnerCharacterId))
                    continue;

                setup.PlayerCardCatalog.Add(card);
            }

            EditorUtility.SetDirty(setup);
            return setup.PlayerCardCatalog.Count > 0;
        }
    }
}
#endif
