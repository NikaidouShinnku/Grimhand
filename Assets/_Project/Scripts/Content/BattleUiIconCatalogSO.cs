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
        /// <summary>局外金币图标（Assets/The Grimhands Asset/icon/camp_gold.png）</summary>
        public Sprite CampGoldIcon;
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
        /// <summary>Assets/The Grimhands Asset/path and background/training_ground.png</summary>
        public Sprite TrainingGroundBuilding;
        /// <summary>Assets/The Grimhands Asset/path and background/training_ground_background.png</summary>
        public Sprite TrainingGroundBackground;
        /// <summary>gamemenu.png 切片：0=START, 1=CONTINUE, 2=SETTINGS, 3=QUIT GAME</summary>
        public Sprite[] GameMenuButtons = System.Array.Empty<Sprite>();
        /// <summary>escmenu.png 切片：0=RETURN TO GAME, 1=SETTINGS, 2=FORFEIT, 3=QUIT GAME</summary>
        public Sprite[] EscMenuButtons = System.Array.Empty<Sprite>();
        /// <summary>Assets/The Grimhands Asset/path and background/talent_alter.png</summary>
        public Sprite TalentAltarBuilding;
        /// <summary>Assets/The Grimhands Asset/icon/talent_rune_plate.png</summary>
        public Sprite TalentRunePlate;
        public Sprite TreasureChestClosed;
        public Sprite TreasureChestOpen;
        /// <summary>Assets/The Grimhands Asset/card/cardpack_common.png</summary>
        public Sprite CardPackCommon;
        /// <summary>Assets/The Grimhands Asset/card/cardpack_advanced.png</summary>
        public Sprite CardPackAdvanced;
        /// <summary>Assets/The Grimhands Asset/card/cardpack_master.png</summary>
        public Sprite CardPackMaster;
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
