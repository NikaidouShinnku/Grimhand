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
        /// <summary>开局主菜单全屏背景（new_UI_and_Layout/mainmenu_background.png）</summary>
        public Sprite MainMenuBackground;
        public Sprite ChampionCampBuilding;
        /// <summary>军营一级：templates/远征军军营一级界面概念图-模板.png</summary>
        public Sprite ChampionCampHubBackground;
        /// <summary>军营一级悬停：interactables_and_ui/champion_camp_button1.png（配置队伍）</summary>
        public Sprite ChampionCampButton1;
        /// <summary>军营一级悬停：interactables_and_ui/champion_camp_button2.png（管理卡牌）</summary>
        public Sprite ChampionCampButton2;
        /// <summary>通用 UI 按钮框：interactables_and_ui/button3.png</summary>
        public Sprite UiButton3;
        /// <summary>通用 UI 按钮框：interactables_and_ui/button1.png</summary>
        public Sprite UiButton1;
        /// <summary>通用 UI 按钮框：interactables_and_ui/button2.png</summary>
        public Sprite UiButton2;
        /// <summary>通用 UI 按钮框：interactables_and_ui/button4.png</summary>
        public Sprite UiButton4;
        /// <summary>通用 UI 按钮框：interactables_and_ui/button6.png（离开商店等宽钮）</summary>
        public Sprite UiButton6;
        /// <summary>配置队伍二级：templates/配置队伍概念图-模板.png</summary>
        public Sprite ChampionCampTeamBackground;
        /// <summary>管理卡牌二级：templates/军营收藏概念图-模板.png</summary>
        public Sprite ChampionCampCollectionBackground;
        /// <summary>换人选择：templates/军营角色选择概念图-模板.png</summary>
        public Sprite ChampionCampCharacterSelectBackground;
        /// <summary>角色选择板：interactables_and_ui/character_plate.png</summary>
        public Sprite UiCharacterPlate;
        /// <summary>通用竖直滑动条轨道：interactables_and_ui/sliderbar.png</summary>
        public Sprite UiSliderBar;
        /// <summary>通用竖直滑动条手柄：interactables_and_ui/slider.png</summary>
        public Sprite UiSlider;
        /// <summary>卡牌/遗物/角色详情：templates/卡牌遗物角色详情概念图-模板.png</summary>
        public Sprite UiCardDetailBackground;
        /// <summary>局外营地商店：templates/营地商店概念图-模板.png</summary>
        public Sprite UiCampShopBackground;
        /// <summary>商店商品行底板：interactables_and_ui/merchant_camp_goodsplate.png</summary>
        public Sprite UiMerchantGoodsPlate;
        /// <summary>商店商品图标框：interactables_and_ui/merchant_camp_goodsimageplate.png</summary>
        public Sprite UiMerchantGoodsImagePlate;
        /// <summary>通用是/否确认底板：interactables_and_ui/prompt_plate.png</summary>
        public Sprite UiPromptPlate;
        /// <summary>信息/设置底板：interactables_and_ui/information_plate.png</summary>
        public Sprite UiInformationPlate;
        public Sprite MerchantCampBuilding;
        public Sprite PortalBuilding;
        /// <summary>Assets/The Grimhands Asset/path and background/training_ground.png</summary>
        public Sprite TrainingGroundBuilding;
        /// <summary>Assets/The Grimhands Asset/path and background/library.png</summary>
        public Sprite LibraryBuilding;
        /// <summary>Assets/The Grimhands Asset/path and background/training_ground_background.png</summary>
        public Sprite TrainingGroundBackground;
        /// <summary>gamemenu.png 切片：0=START, 1=CONTINUE, 2=SETTINGS, 3=QUIT GAME</summary>
        public Sprite[] GameMenuButtons = System.Array.Empty<Sprite>();
        /// <summary>escmenu.png 切片：0=RETURN TO GAME, 1=SETTINGS, 2=FORFEIT, 3=QUIT GAME</summary>
        public Sprite[] EscMenuButtons = System.Array.Empty<Sprite>();
        /// <summary>Assets/The Grimhands Asset/path and background/talent_alter.png</summary>
        public Sprite TalentAltarBuilding;
        /// <summary>局外天赋祭坛：templates/天赋祭坛概念图-模板.png</summary>
        public Sprite UiTalentAltarBackground;
        /// <summary>局外图书馆图鉴：templates/图鉴概念图-模板.png</summary>
        public Sprite UiLibraryCodexBackground;
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
        /// <summary>烙印（Assets/The Grimhands Asset/icon/warden_brand.png）</summary>
        public Sprite StatusBrandMark;
    }
}
