using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "BattleUiIconCatalog", menuName = "Grimhand/Battle UI Icon Catalog")]
    public class BattleUiIconCatalogSO : ScriptableObject
    {
        public Sprite HpIcon;
        public Sprite ArmorIcon;
        public Sprite AttackIcon;
        public Sprite DefenseIcon;
        public Sprite SpeedIcon;
        public Sprite EnergyIcon;
        public Sprite GoldIcon;
        public Sprite XpIcon;
        public Sprite InventoryIcon;
        public Sprite ConfirmPlayIcon;
        public Sprite SkipIcon;
        public Sprite NoteIcon;
        public Sprite MapIcon;
        public Sprite ShopRefreshIcon;
        public Sprite UnknownPathIcon;
        public Sprite CaveBackground;
        public Sprite ShopBackground;
        public Sprite CampSiteBackground;
        public Sprite ChampionCampBuilding;
        public Sprite MerchantCampBuilding;
        public Sprite PortalBuilding;
        /// <summary>Assets/The Grimhands Asset/path and background/talent_alter.png</summary>
        public Sprite TalentAltarBuilding;
        /// <summary>Assets/The Grimhands Asset/icon/talent_rune_plate.png</summary>
        public Sprite TalentRunePlate;
        public Sprite TreasureChestClosed;
        public Sprite TreasureChestOpen;
        public Sprite[] CavePathVariants = System.Array.Empty<Sprite>();
        public Sprite DungeonBackground;
        public Sprite[] DungeonPathVariants = System.Array.Empty<Sprite>();
        public Sprite AbyssBackground;
        public Sprite[] AbyssPathVariants = System.Array.Empty<Sprite>();

        public Sprite StatusDamageUp;
        public Sprite StatusDamageDown;
        public Sprite StatusDefenseUp;
        public Sprite StatusDefenseDown;
        public Sprite StatusArmorAcqUp;
        public Sprite StatusArmorAcqDown;
        public Sprite StatusSpdDown;
        public Sprite StatusSpdUp;
        /// <summary>中毒状态图标（复用 effects/poisoning_effect.png，缩小尺寸）。</summary>
        public Sprite StatusPoisoning;
        /// <summary>灼烧状态图标（复用 effects/burning_effect.png，缩小尺寸）。</summary>
        public Sprite StatusBurning;
    }
}
