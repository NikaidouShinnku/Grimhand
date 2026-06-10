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
        public Sprite TreasureChestClosed;
        public Sprite TreasureChestOpen;
        public Sprite[] CavePathVariants = System.Array.Empty<Sprite>();
    }
}
