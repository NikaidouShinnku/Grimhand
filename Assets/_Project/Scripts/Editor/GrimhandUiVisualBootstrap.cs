#if UNITY_EDITOR
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class GrimhandUiVisualBootstrap
    {
        public const string IconCatalogPath = "Assets/_Project/Data/BattleUiIconCatalog_Demo.asset";
        public const string CardCatalogPath = "Assets/_Project/Data/CardVisualCatalog_Demo.asset";
        const string IconRoot = "Assets/The Grimhands Asset/icon/";
        const string CardRoot = "Assets/The Grimhands Asset/card/";

        [MenuItem("Grimhand/Content/Refresh UI Visual Catalogs")]
        public static void RefreshUiVisualCatalogsMenu()
        {
            EnsureUiIconCatalog();
            EnsureCardVisualCatalog();
            AssignDemoCardRarities();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "UI 美术目录已刷新",
                "已更新：\n" +
                "• BattleUiIconCatalog_Demo.asset\n" +
                "• CardVisualCatalog_Demo.asset（卡框按稀有度 + 类型）",
                "好的");
        }

        public static void EnsureUiIconCatalog()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(IconCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleUiIconCatalogSO>();
                AssetDatabase.CreateAsset(catalog, IconCatalogPath);
            }

            catalog.HpIcon = LoadFirstSprite(IconRoot + "HP.png");
            catalog.ArmorIcon = LoadFirstSprite(IconRoot + "ARM.png");
            catalog.AttackIcon = LoadFirstSprite(IconRoot + "ATK.png");
            catalog.DefenseIcon = LoadFirstSprite(IconRoot + "DEF.png");
            catalog.SpeedIcon = LoadFirstSprite(IconRoot + "SPD.png");
            catalog.EnergyIcon = LoadFirstSprite(IconRoot + "ERG.png");
            catalog.ConfirmPlayIcon = LoadFirstSprite(IconRoot + "check.png");
            catalog.SkipIcon = LoadFirstSprite(IconRoot + "pass.png");

            EditorUtility.SetDirty(catalog);
        }

        public static void EnsureCardVisualCatalog()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(CardCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CardCatalogPath);
            }

            catalog.FrameSets.Clear();
            AddFrameSet(catalog, CardRarity.Common, "common");
            AddFrameSet(catalog, CardRarity.Rare, "rare");
            AddFrameSet(catalog, CardRarity.Epic, "epic");
            AddFrameSet(catalog, CardRarity.SuperRare, "superrare");
            AddFrameSet(catalog, CardRarity.Legendary, "legendary");

            var common = catalog.FrameSets.Count > 0 ? catalog.FrameSets[0] : null;
            if (common != null)
            {
                catalog.DefaultFrameAttack = common.AttackFrame;
                catalog.DefaultFrameDefense = common.DefenseFrame;
                catalog.DefaultFrameStatus = common.StatusFrame;
            }

            EditorUtility.SetDirty(catalog);
        }

        public static void AssignDemoCardRarities()
        {
            SetCardRarity("Card_k_strike", CardRarity.Common);
            SetCardRarity("Card_k_slash", CardRarity.Rare);
            SetCardRarity("Card_k_parry", CardRarity.Epic);
            SetCardRarity("Card_m_bolt", CardRarity.Common);
            SetCardRarity("Card_m_poison", CardRarity.Rare);
            SetCardRarity("Card_r_snipe", CardRarity.Epic);
            SetCardRarity("Card_r_pierce", CardRarity.Rare);
            SetCardRarity("Card_r_far_shot", CardRarity.Common);
            SetCardRarity("Card_r_slow", CardRarity.SuperRare);
            SetCardRarity("Card_g_bite", CardRarity.Common);
            SetCardRarity("Card_g_scratch", CardRarity.Common);
            SetCardRarity("Card_g_lunge", CardRarity.Rare);
            SetCardRarity("Card_g_hex", CardRarity.Rare);
            SetCardRarity("Card_g_wither", CardRarity.Common);
            SetCardRarity("Card_g_aim", CardRarity.Common);
            SetCardRarity("Card_g_arrow", CardRarity.Rare);
        }

        static void SetCardRarity(string assetName, CardRarity rarity)
        {
            var path = $"Assets/_Project/Data/Cards/{assetName}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
            if (card == null)
                return;

            card.Rarity = rarity;
            EditorUtility.SetDirty(card);
        }

        static void AddFrameSet(CardVisualCatalogSO catalog, CardRarity rarity, string prefix)
        {
            catalog.FrameSets.Add(new CardFrameRaritySet
            {
                Rarity = rarity,
                AttackFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_atk.png"),
                DefenseFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_def.png"),
                StatusFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_spc.png")
            });
        }

        static Sprite LoadFirstSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
