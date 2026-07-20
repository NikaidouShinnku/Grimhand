#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static partial class MonsterContentGenerator
    {
        const string Root = "Assets/_Project/Data";
        const string ArtRoot = "Assets/The Grimhands Asset/monsters";

        public struct MonsterSet
        {
            public CharacterDefinitionSO Goblin;
            public CharacterDefinitionSO Slime;
            public CharacterDefinitionSO Skeleton;
            public CharacterDefinitionSO SkeletonElite;
            public CharacterDefinitionSO Wraith;
            public CharacterDefinitionSO WraithElite;
            public CharacterDefinitionSO Ogre;
            public CharacterDefinitionSO Bat;
            public CharacterDefinitionSO SkeletonKing;
            public CharacterDefinitionSO ExplosiveSkull;
            public CharacterDefinitionSO GhostQueen;
        }

        public static MonsterSet Generate()
        {
            var cards = CreateMonsterCards();

            return new MonsterSet
            {
                Goblin = SaveMonster("Character_Goblin", "char_goblin", "哥布林",
                    FormationSlot.Front, 20, 4, 1, 5, null,
                    Pool(cards.GoblinBite, 2, cards.GoblinBloodScratch, 2, cards.GoblinThrow, 2)),
                Slime = SaveMonster("Character_Slime", "char_slime", "史莱姆",
                    FormationSlot.Front, 30, 3, 4, 2,
                    new[] { MinionTraitCatalog.SlimeRegen },
                    Pool(cards.SlimeShield, 2, cards.SlimeSlam, 2, cards.SlimeAbsorb, 1, cards.SlimeWrap, 1)),
                Skeleton = SaveMonster("Character_Skeleton", "char_skeleton", "骷髅兵",
                    FormationSlot.Middle, 25, 6, 3, 4,
                    new[] { MinionTraitCatalog.SkeletonCardDef },
                    Pool(cards.SkeletonShield, 1, cards.SkeletonSlash, 2, cards.SkeletonToss, 2, cards.SkeletonMaim, 1)),
                SkeletonElite = SaveMonster("Character_Skeleton_Elite", "char_skeleton_elite", "骷髅精英",
                    FormationSlot.Middle, 45, 9, 5, 5,
                    new[] { MinionTraitCatalog.SkeletonEliteCardStats },
                    Pool(cards.EliteBoneWall, 2, cards.EliteBoneCrush, 2, cards.EliteBoneSpear, 2,
                        cards.EliteShatterRush, 1, cards.EliteRaiseBones, 2)),
                Wraith = SaveMonster("Character_Wraith", "char_wraith", "幽灵",
                    FormationSlot.Back, 20, 7, 1, 7,
                    new[] { MinionTraitCatalog.WraithLowHpSpeed },
                    Pool(cards.WraithArrow, 2, cards.WraithPhase, 1, cards.WraithSoulStrike, 2, cards.WraithHex, 1)),
                WraithElite = SaveMonster("Character_Wraith_Elite", "char_wraith_elite", "幽灵精英",
                    FormationSlot.Back, 35, 10, 2, 8,
                    new[] { MinionTraitCatalog.WraithEliteLowHpEthereal },
                    Pool(cards.EliteWraithSoulStrike, 2, cards.EliteWraithPhase, 1, cards.WraithAdvancedHex, 2,
                        cards.WraithSoulStorm, 1, cards.WraithSoulBind, 1)),
                Ogre = SaveMonster("Character_Ogre", "char_ogre", "绿皮巨魔",
                    FormationSlot.Front, 75, 12, 8, 3,
                    new[] { MinionTraitCatalog.OgreBloodRage },
                    Pool(cards.OgreHeavyPunch, 2, cards.OgreStomp, 2, cards.OgreWarCry, 2,
                        cards.OgreComboSmash, 1, cards.OgreThickHide, 1)),
                Bat = SaveMonster("Character_Bat", "char_bat", "巨翼蝙蝠",
                    FormationSlot.Middle, 55, 10, 3, 9,
                    new[] { MinionTraitCatalog.BatFirstHitDodge },
                    Pool(cards.BatClaw, 2, cards.BatDive, 1, cards.BatAmbush, 2, cards.BatShadowDodge, 1,
                        cards.BatPoisonWing, 2, cards.BatNightSlash, 1)),
                SkeletonKing = SaveBoss("Character_Skeleton_King", "char_skeleton_king", "骷髅王",
                    FormationSlot.Front, 350, 30, 10, 6,
                    new[]
                    {
                        CharacterTraitCatalog.BossFirstHitBlock
                    },
                    BuildFixedDeck(
                        (cards.KingBoneSlash, 4),
                        (cards.KingBoneRoar, 1),
                        (cards.KingBoneSpear, 2),
                        (cards.KingSummonWorkshop, 1),
                        (cards.KingBoneBlock, 1),
                        (cards.KingBoneShield, 2),
                        (cards.KingWhiteStorm, 1))),
                ExplosiveSkull = SaveBoss("Character_Explosive_Skull", "char_explosive_skull", "易爆骷髅头",
                    FormationSlot.Middle, 20, 0, 5, 2,
                    CharacterTraitCatalog.SkullSelfDestructHand,
                    BuildFixedDeck((cards.SkullExplode, 1))),
                GhostQueen = SaveBoss("Character_Ghost_Queen", GhostQueenBossEncounterBuilder.CharacterId, "幽灵女王",
                    FormationSlot.Front, 320, 25, 8, 7,
                    new[] { CharacterTraitCatalog.GhostQueenEnrage },
                    BuildFixedDeck(
                        (cards.QueenClaw, 4),
                        (cards.QueenDeterrence, 1),
                        (cards.QueenSoulDrain, 2),
                        (cards.QueenCurse, 2),
                        (cards.QueenCommand, 1),
                        (cards.QueenSpiritGuard, 2),
                        (cards.QueenBurst, 1)))
            };
        }

        public static void UpdateVisualCatalog(CharacterVisualCatalogSO catalog)
        {
            if (catalog == null)
                return;

            catalog.MonsterCardProfilePortrait = LoadMonsterCardProfilePortrait();

            UpsertVisual(catalog, "char_goblin", $"{ArtRoot}/goblin_idle_1024.png");
            UpsertVisual(catalog, "char_slime", $"{ArtRoot}/slime_idle_1024.png");
            UpsertVisual(catalog, "char_skeleton", $"{ArtRoot}/skeleton_idle_1024.png");
            UpsertVisual(catalog, "char_skeleton_elite", $"{ArtRoot}/skeleton2_idle_1024.png");
            UpsertVisual(catalog, "char_wraith", $"{ArtRoot}/wraith_idle_1024.png");
            UpsertVisual(catalog, "char_wraith_elite", $"{ArtRoot}/wraith2_idle_1024.png");
            UpsertVisual(catalog, "char_ogre", $"{ArtRoot}/green_ogre.png");
            // 略放大；朝向由原画保证，一律不翻转
            foreach (var e in catalog.Entries)
            {
                if (e == null || e.CharacterId != "char_ogre")
                    continue;
                e.PreserveOriginalFacing = true;
                e.PortraitScaleMultiplier = 1.55f;
                break;
            }
            UpsertVisual(catalog, "char_bat", $"{ArtRoot}/bat_girl.png");
            UpsertVisualFull(catalog, TrainingGroundEncounterBuilder.DummyCharacterId,
                idle: $"{ArtRoot}/dummy/dummy_idle.png",
                attack: $"{ArtRoot}/dummy/dummy_idle.png",
                hit: $"{ArtRoot}/dummy/dummy_hit.png",
                death: $"{ArtRoot}/dummy/dummy_idle.png",
                preserveOriginalFacing: true,
                portraitScaleMultiplier: 1.8f);

            var kingArt = ArtRoot + "/skeleton king";
            UpsertVisualFull(catalog, "char_skeleton_king",
                idle: kingArt + "/skeletonking_idle_1024.png",
                attack: kingArt + "/skeletonking_attack_1024.png",
                hit: kingArt + "/skeletonking_hit_1024.png",
                death: kingArt + "/skeletonking_defeat_1024.png",
                profile: kingArt + "/skeletonking_profile.png",
                gifPath: "The Grimhands Asset/monsters/skeleton king/skeletonking_idle_anime.gif",
                defendUsesHit: true);
            UpsertVisualFull(catalog, "char_explosive_skull",
                idle: ArtRoot + "/skeletonhead_idle_1024.png",
                attack: ArtRoot + "/skeletonhead_idle_1024.png",
                hit: ArtRoot + "/skeletonhead_idle_1024.png",
                death: ArtRoot + "/skeletonhead_idle_1024.png",
                portraitScaleMultiplier: 1f);

            var queenArt = ArtRoot + "/ghost queen";
            UpsertVisualFull(catalog, GhostQueenBossEncounterBuilder.CharacterId,
                idle: queenArt + "/ghostqueen_idle_1024.png",
                attack: queenArt + "/ghostqueen_attack_1024.png",
                hit: queenArt + "/ghostqueen_hit_1024.png",
                death: queenArt + "/ghostqueen_defeat_1024.png",
                profile: queenArt + "/ghostqueen_profile.png",
                gifPath: "The Grimhands Asset/monsters/ghost queen/ghostqueen_idle_anime.gif",
                defendUsesHit: true,
                preserveOriginalFacing: true);

            UpdateDungeonVisualCatalog(catalog);
            UpdateAbyssVisualCatalog(catalog);
            UpdateBossVisualCatalog(catalog);
            CardProfileArt.BindAllProfiles(catalog);

            EditorUtility.SetDirty(catalog);
        }

        static Sprite LoadMonsterCardProfilePortrait()
        {
            // 已改为按角色单独绑定 card/card_profile/*；保留方法避免旧菜单调用报错。
            return null;
        }

        struct MonsterCards
        {
            public CardDefinitionSO GoblinBite;
            public CardDefinitionSO GoblinBloodScratch;
            public CardDefinitionSO GoblinThrow;
            public CardDefinitionSO SlimeShield;
            public CardDefinitionSO SlimeSlam;
            public CardDefinitionSO SlimeAbsorb;
            public CardDefinitionSO SlimeWrap;
            public CardDefinitionSO SkeletonShield;
            public CardDefinitionSO SkeletonSlash;
            public CardDefinitionSO SkeletonToss;
            public CardDefinitionSO SkeletonMaim;
            public CardDefinitionSO EliteBoneWall;
            public CardDefinitionSO EliteBoneCrush;
            public CardDefinitionSO EliteBoneSpear;
            public CardDefinitionSO EliteShatterRush;
            public CardDefinitionSO EliteRaiseBones;
            public CardDefinitionSO WraithArrow;
            public CardDefinitionSO WraithPhase;
            public CardDefinitionSO WraithSoulStrike;
            public CardDefinitionSO WraithHex;
            public CardDefinitionSO EliteWraithPhase;
            public CardDefinitionSO EliteWraithSoulStrike;
            public CardDefinitionSO WraithAdvancedHex;
            public CardDefinitionSO WraithSoulStorm;
            public CardDefinitionSO WraithSoulBind;
            public CardDefinitionSO OgreHeavyPunch;
            public CardDefinitionSO OgreStomp;
            public CardDefinitionSO OgreWarCry;
            public CardDefinitionSO OgreComboSmash;
            public CardDefinitionSO OgreThickHide;
            public CardDefinitionSO BatClaw;
            public CardDefinitionSO BatDive;
            public CardDefinitionSO BatAmbush;
            public CardDefinitionSO BatShadowDodge;
            public CardDefinitionSO BatPoisonWing;
            public CardDefinitionSO BatNightSlash;
            public CardDefinitionSO KingBoneSlash;
            public CardDefinitionSO KingBoneRoar;
            public CardDefinitionSO KingBoneSpear;
            public CardDefinitionSO KingSummonWorkshop;
            public CardDefinitionSO KingBoneBlock;
            public CardDefinitionSO KingBoneShield;
            public CardDefinitionSO KingWhiteStorm;
            public CardDefinitionSO SkullExplode;
            public CardDefinitionSO QueenClaw;
            public CardDefinitionSO QueenDeterrence;
            public CardDefinitionSO QueenSoulDrain;
            public CardDefinitionSO QueenCurse;
            public CardDefinitionSO QueenCommand;
            public CardDefinitionSO QueenSpiritGuard;
            public CardDefinitionSO QueenBurst;
            public CardDefinitionSO QueenWrath;
        }

        static MonsterCards CreateMonsterCards()
        {
            return new MonsterCards
            {
                GoblinBite = SaveCard("g_bite", "撕咬", "char_goblin", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7)),
                GoblinBloodScratch = SaveCard("g_blood_scratch", "嗜血抓挠", "char_goblin", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 4),
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUp, stacks: 3, duration: 1)),
                GoblinThrow = SaveCard("g_throw", "投石", "char_goblin", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5,
                        reach: TargetReach.MiddleAndBack)),
                SlimeShield = SaveCard("m_slime_shield", "凝胶护盾", "char_slime", 1, CardType.Defense, null,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 2)),
                SlimeSlam = SaveCard("m_slime_slam", "黏糊撞击", "char_slime", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 4)),
                SlimeAbsorb = SaveCard("m_slime_absorb", "吸收", "char_slime", 2, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5),
                    Action(EffectActionType.Heal, EffectTarget.Self, 4)),
                SlimeWrap = SaveCard("m_slime_wrap", "粘液缠绕", "char_slime", 2, CardType.Status, Kw("slow"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                SkeletonShield = SaveCard("m_bone_shield", "举盾", "char_skeleton", 1, CardType.Defense, null,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 2)),
                SkeletonSlash = SaveCard("m_bone_slash", "骨剑斩", "char_skeleton", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7)),
                SkeletonToss = SaveCard("m_bone_toss", "投骨", "char_skeleton", 2, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8,
                        reach: TargetReach.MiddleAndBack)),
                SkeletonMaim = SaveCard("m_maim", "致残", "char_skeleton", 2, CardType.Status, Kw("slow"), CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                EliteBoneWall = SaveCard("m_bone_wall", "骨墙", "char_skeleton_elite", 1, CardType.Defense, null,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 3)),
                EliteBoneCrush = SaveCard("m_bone_crush", "骨碎斩", "char_skeleton_elite", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 11)),
                EliteBoneSpear = SaveCard("m_bone_spear", "投掷骨矛", "char_skeleton_elite", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 11,
                        reach: TargetReach.MiddleAndBack, selfDamageFlat: 1)),
                EliteShatterRush = SaveCard("m_shatter_rush", "碎骨突袭", "char_skeleton_elite", 2, CardType.Attack,
                    null, CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10,
                        useAlternateIfTargetHasDebuff: true, alternateValue: 16)),
                EliteRaiseBones = SaveCard("m_raise_bones", "唤骨", "char_skeleton_elite", 3, CardType.Status,
                    Kw("exhaust", "summon"), CardRarity.Rare,
                    Action(EffectActionType.SummonOrGainBlock, EffectTarget.Self, 0,
                        summonCharacterId: MinionTraitCatalog.SkeletonCharacterId,
                        fallbackBlockValue: 6)),
                WraithArrow = SaveCard("g_arrow", "箭矢", "char_wraith", 2, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 9,
                        reach: TargetReach.Any, backRowPowerPercent: 130)),
                WraithPhase = SaveCard("m_wraith_phase", "隐身", "char_wraith", 1, CardType.Defense, Kw("parry"),
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 20,
                        condition: ReactionConditionType.LastActionAttackOnSelf, grantInvulnerableOnRespondArm: true)),
                WraithSoulStrike = SaveCard("m_wraith_soul_strike", "灵魂打击", "char_wraith", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8)),
                WraithHex = SaveCard("g_hex", "邪咒", "char_wraith", 2, CardType.Status, Kw("poison"), CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 5)),
                EliteWraithPhase = SaveCard("m_phase", "隐身", "char_wraith_elite", 1, CardType.Defense, Kw("parry"),
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 20,
                        condition: ReactionConditionType.LastActionAttackOnSelf, grantInvulnerableOnRespondArm: true)),
                EliteWraithSoulStrike = SaveCard("m_soul_strike", "灵魂打击", "char_wraith_elite", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10)),
                WraithAdvancedHex = SaveCard("m_advanced_hex", "高级邪咒", "char_wraith_elite", 2, CardType.Status,
                    Kw("poison"), CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 10)),
                WraithSoulStorm = SaveCard("m_soul_storm", "灵魂风暴", "char_wraith_elite", 3, CardType.Attack, Kw("aoe"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 11,
                        reach: TargetReach.Any)),
                WraithSoulBind = SaveCard("m_soul_bind", "灵魂束缚", "char_wraith_elite", 2, CardType.Status, Kw("aoe"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.AllEnemies, 0,
                        statusId: StatusCatalog.DefenseDownPercent, stacks: 25, duration: 2, reach: TargetReach.Any)),
                OgreHeavyPunch = SaveCard("m_ogre_heavy_punch", "重拳", "char_ogre", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, hitCount: 2)),
                OgreStomp = SaveCard("m_ogre_stomp", "践踏", "char_ogre", 2, CardType.Attack, Kw("aoe"),
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 11,
                        reach: TargetReach.Any)),
                OgreWarCry = SaveCard("m_ogre_war_cry", "战争怒吼", "char_ogre", 2, CardType.Status, null, CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUpPercent, stacks: 30, duration: 3)),
                OgreComboSmash = SaveCard("m_ogre_combo_smash", "连环猛击", "char_ogre", 2, CardType.Attack, null,
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14,
                        alternateAttackScaleIfActorUsedAttack: 100,
                        alternateValueIfActorUsedAttack: 23)),
                OgreThickHide = SaveCard("m_ogre_thick_hide", "厚皮护甲", "char_ogre", 1, CardType.Defense, null,
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.DefenseUpPercent, stacks: 25, duration: 2),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 9)),
                BatClaw = SaveCard("m_bat_claw", "蝙蝠爪击", "char_bat", 1, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10)),
                BatDive = SaveCard("m_bat_dive", "俯冲撕咬", "char_bat", 2, CardType.Attack, null,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 12,
                        reach: TargetReach.Any, lifestealUnblockedOnly: true)),
                BatAmbush = SaveCard("m_bat_ambush", "偷袭", "char_bat", 2, CardType.Attack, null, CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 12,
                        useAlternateIfTargetHasAnyStatus: true, alternateValue: 24)),
                BatShadowDodge = SaveCard("m_bat_shadow_dodge", "暗影闪避", "char_bat", 2, CardType.Defense, null,
                    CardRarity.Rare,
                    Action(EffectActionType.GrantDodgeChance, EffectTarget.Self, 60)),
                BatPoisonWing = SaveCard("m_bat_poison_wing", "淬毒翼击", "char_bat", 2, CardType.Attack, null,
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 8)),
                BatNightSlash = SaveCard("m_bat_night_slash", "夜袭连斩", "char_bat", 3, CardType.Attack, null,
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.RandomEnemy, 14,
                        repeatPerEnemyAttackCardThisTurn: 1)),
                KingBoneSlash = SaveCard("m_king_bone_slash", "骨王斩击", "char_skeleton_king", 1,
                    CardType.Attack, null, CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 27)),
                KingBoneRoar = SaveCard("m_king_bone_roar", "骨王怒吼", "char_skeleton_king", 1,
                    CardType.Status, Kw("slow"), CardRarity.Common,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemies, 2,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 4)),
                KingBoneSpear = SaveCard("m_king_bone_spear", "投掷骨矛", "char_skeleton_king", 1,
                    CardType.Attack, null, CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 27,
                        reach: TargetReach.MiddleAndBack)),
                KingSummonWorkshop = SaveCard("m_king_summon_workshop", "召唤骨之王座", "char_skeleton_king", 3,
                    CardType.Status, Kw("exhaust", "summon"), CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.BoneWorkshop, stacks: 1, duration: -1)),
                KingBoneBlock = SaveCard("m_king_bone_block", "骨甲格挡", "char_skeleton_king", 1,
                    CardType.Defense, null, CardRarity.Common,
                    RespondBlock(80)),
                KingBoneShield = SaveCard("m_king_bone_shield", "召唤骨盾", "char_skeleton_king", 2,
                    CardType.Defense, null, CardRarity.Common,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 12)),
                KingWhiteStorm = SaveCard("m_king_white_storm", "白骨风暴", "char_skeleton_king", 3,
                    CardType.Attack, Kw("aoe"), CardRarity.Epic,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 25,
                        reach: TargetReach.Any)),
                SkullExplode = SaveCard("m_skull_explode", "骷髅自爆", "char_explosive_skull", 0,
                    CardType.Attack, Kw("self_destruct", "bonus_hand"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.RandomEnemy, 24)),
                QueenClaw = SaveCard("m_queen_claw", "幽灵爪击", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Attack, null, CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 23,
                        reach: TargetReach.Any)),
                QueenDeterrence = SaveCard("m_queen_deterrence", "女王的威慑", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Status, Kw("slow"), CardRarity.Common,
                    Action(EffectActionType.LockRandomPlayerPlaysThisTurn, EffectTarget.DefaultEnemy, 0)),
                QueenSoulDrain = SaveCard("m_queen_soul_drain", "摄魂", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Status, Kw("slow"), CardRarity.Common,
                    Action(EffectActionType.ReducePlayerEnergyRegenNextTurn, EffectTarget.AllEnemies, 2)),
                QueenCurse = SaveCard("m_queen_curse", "女王的诅咒", GhostQueenBossEncounterBuilder.CharacterId, 2,
                    CardType.Status, Kw("poison", "aoe"), CardRarity.Common,
                    Action(EffectActionType.ApplyStatus, EffectTarget.AllEnemies, 0,
                        statusId: StatusCatalog.Poison, stacks: 6, duration: -1, reach: TargetReach.Any)),
                QueenCommand = SaveCard("m_queen_command", "女王的命令", GhostQueenBossEncounterBuilder.CharacterId, 2,
                    CardType.Defense, Kw("parry"), CardRarity.Epic,
                    Action(EffectActionType.ArmRespondDamageRedirect, EffectTarget.Self, 0,
                        condition: ReactionConditionType.None)),
                QueenSpiritGuard = SaveCard("m_queen_spirit_guard", "灵气护体", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Defense, null, CardRarity.Common,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10)),
                QueenBurst = SaveCard("m_queen_burst", "幽灵爆发", GhostQueenBossEncounterBuilder.CharacterId, 4,
                    CardType.Attack, Kw("aoe"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 27,
                        reach: TargetReach.Any)),
                QueenWrath = SaveCard("m_queen_wrath", "幽灵女王之怒", GhostQueenBossEncounterBuilder.CharacterId, 0,
                    CardType.Status, Kw("bonus_hand"), CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.GhostQueenWrath, stacks: 1, duration: -1))
            };
        }

        static CardDefinitionSO[] BuildFixedDeck(params (CardDefinitionSO card, int count)[] entries)
        {
            var deck = new List<CardDefinitionSO>();
            foreach (var (card, count) in entries)
            {
                if (card == null || count <= 0)
                    continue;

                for (var i = 0; i < count; i++)
                    deck.Add(card);
            }

            return deck.ToArray();
        }

        static CharacterDefinitionSO SaveBoss(
            string assetName,
            string charId,
            string displayName,
            FormationSlot slot,
            int hp,
            int atk,
            int def,
            int spd,
            string[] traits,
            CardDefinitionSO[] deck) =>
            SaveBossMonster(assetName, charId, displayName, slot, hp, atk, def, spd, traits, deck);

        static CharacterDefinitionSO SaveBoss(
            string assetName,
            string charId,
            string displayName,
            FormationSlot slot,
            int hp,
            int atk,
            int def,
            int spd,
            string trait,
            CardDefinitionSO[] deck) =>
            SaveBossMonster(assetName, charId, displayName, slot, hp, atk, def, spd,
                string.IsNullOrEmpty(trait) ? null : new[] { trait }, deck);

        static CharacterDefinitionSO SaveMonster(
            string assetName,
            string charId,
            string displayName,
            FormationSlot slot,
            int hp,
            int atk,
            int def,
            int spd,
            string[] traits,
            CardDefinitionSO[] skillPool)
        {
            var path = $"{Root}/Characters/{assetName}.asset";
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
                AssetDatabase.CreateAsset(character, path);
            }

            character.CharacterId = charId;
            character.DisplayName = displayName;
            character.Team = TeamSide.Enemy;
            character.Slot = slot;
            character.Level = 1;
            character.MaxHp = hp;
            character.BaseAttack = atk;
            character.BaseDefense = def;
            character.Speed = spd;
            character.Deck.Clear();
            character.SkillPool.Clear();
            character.SkillPool.AddRange(skillPool);
            character.Traits.Clear();
            if (traits != null)
                character.Traits.AddRange(traits);
            EditorUtility.SetDirty(character);
            return character;
        }

        static CardDefinitionSO[] Pool(params object[] entries)
        {
            var list = new List<CardDefinitionSO>();
            for (var i = 0; i < entries.Length; i += 2)
            {
                var card = entries[i] as CardDefinitionSO;
                var count = entries[i + 1] is int c ? c : 1;
                if (card == null || count <= 0)
                    continue;

                for (var n = 0; n < count; n++)
                    list.Add(card);
            }

            return list.ToArray();
        }

        static CharacterDefinitionSO SaveBossMonster(
            string assetName,
            string charId,
            string displayName,
            FormationSlot slot,
            int hp,
            int atk,
            int def,
            int spd,
            string[] traits,
            CardDefinitionSO[] deck)
        {
            var path = $"{Root}/Characters/{assetName}.asset";
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
                AssetDatabase.CreateAsset(character, path);
            }

            character.CharacterId = charId;
            character.DisplayName = displayName;
            character.Team = TeamSide.Enemy;
            character.Slot = slot;
            character.Level = 1;
            character.MaxHp = hp;
            character.BaseAttack = atk;
            character.BaseDefense = def;
            character.Speed = spd;
            character.SkillPool.Clear();
            character.Deck.Clear();
            character.Deck.AddRange(deck);
            character.Traits.Clear();
            if (traits != null)
                character.Traits.AddRange(traits);
            EditorUtility.SetDirty(character);
            return character;
        }

        static void UpsertVisualFull(
            CharacterVisualCatalogSO catalog,
            string characterId,
            string idle,
            string attack,
            string hit,
            string death,
            string profile = null,
            string gifPath = null,
            string defend = null,
            bool defendUsesHit = false,
            bool preserveOriginalFacing = true,
            float portraitScaleMultiplier = 1f)
        {
            var idleSprite = LoadPoseSprite(idle);
            if (idleSprite == null)
                return;

            CharacterVisualEntry entry = null;
            foreach (var e in catalog.Entries)
            {
                if (e != null && e.CharacterId == characterId)
                {
                    entry = e;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new CharacterVisualEntry { CharacterId = characterId };
                catalog.Entries.Add(entry);
            }

            entry.IdlePortrait = idleSprite;
            entry.AttackPortrait = LoadPoseSprite(attack) ?? idleSprite;
            entry.HitPortrait = LoadPoseSprite(hit) ?? idleSprite;
            entry.DefensePortrait = defendUsesHit
                ? entry.HitPortrait
                : LoadPoseSprite(defend) ?? entry.HitPortrait ?? idleSprite;
            entry.DeathPortrait = LoadPoseSprite(death) ?? idleSprite;
            // 优先统一卡面目录；无则回退到调用方传入的 profile 路径
            entry.CardProfilePortrait = CardProfileArt.LoadSprite(characterId)
                ?? (string.IsNullOrEmpty(profile) ? null : LoadPoseSprite(profile));
            entry.IdleAnimationGifPath = gifPath ?? "";
            entry.PreserveOriginalFacing = true;
            entry.PortraitScaleMultiplier = portraitScaleMultiplier <= 0f ? 1f : portraitScaleMultiplier;
        }

        static Sprite LoadPoseSprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
                return null;

            return LoadLargestSprite(spritePath);
        }

        static void UpsertVisual(CharacterVisualCatalogSO catalog, string characterId, string spritePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                return;

            ApplyVisualSprite(catalog, characterId, sprite);
        }

        static void UpsertVisualLargest(CharacterVisualCatalogSO catalog, string characterId, string spritePath)
        {
            var sprite = LoadLargestSprite(spritePath);
            if (sprite == null)
                return;

            ApplyVisualSprite(catalog, characterId, sprite);
        }

        static void ApplyVisualSprite(CharacterVisualCatalogSO catalog, string characterId, Sprite sprite)
        {
            CharacterVisualEntry entry = null;
            foreach (var e in catalog.Entries)
            {
                if (e != null && e.CharacterId == characterId)
                {
                    entry = e;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new CharacterVisualEntry { CharacterId = characterId };
                catalog.Entries.Add(entry);
            }

            entry.IdlePortrait = sprite;
            entry.AttackPortrait = sprite;
            entry.DefensePortrait = sprite;
            entry.HitPortrait = sprite;
            entry.DeathPortrait = sprite;
            entry.PreserveOriginalFacing = true;
            var profile = CardProfileArt.LoadSprite(characterId);
            if (profile != null)
                entry.CardProfilePortrait = profile;
        }

        /// <summary>多 Sprite 图集时取面积最大的子图（仅海渊怪绑定使用）。</summary>
        static Sprite LoadLargestSprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
                return null;

            Sprite best = null;
            var bestArea = 0f;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spritePath))
            {
                if (asset is not Sprite sprite)
                    continue;

                var area = sprite.rect.width * sprite.rect.height;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                best = sprite;
            }

            return best ?? AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
            params EffectActionDefinition[] actions) =>
            SaveCard(id, displayName, owner, cost, cardType, keywords, CardRarity.Common, actions);

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
            CardRarity rarity,
            params EffectActionDefinition[] actions)
        {
            var path = $"{Root}/Cards/Card_{id}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinitionSO>();
                AssetDatabase.CreateAsset(card, path);
            }

            card.CardId = id;
            card.DisplayName = displayName;
            card.OwnerCharacterId = owner;
            card.Cost = cost;
            card.CardType = cardType;
            card.Rarity = rarity;
            card.Keywords.Clear();
            if (keywords != null)
                card.Keywords.AddRange(keywords);
            card.Actions.Clear();
            card.Actions.AddRange(actions);
            EditorUtility.SetDirty(card);
            return card;
        }

        static EffectActionDefinition DefBlockScaled(int defenseScalePercent) =>
            new()
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                ScaleWithDefense = true,
                DefenseScalePercent = defenseScalePercent
            };

        static EffectActionDefinition RespondBlock(int reductionPercent) =>
            new()
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = reductionPercent,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            };

        static EffectActionDefinition Action(
            EffectActionType type,
            EffectTarget target,
            int value,
            bool scaleAttack = false,
            bool scaleDefense = false,
            string statusId = "",
            int stacks = 1,
            int duration = -1,
            TargetReach reach = TargetReach.FrontAndMiddle,
            int backRowPowerPercent = 100,
            ReactionConditionType condition = ReactionConditionType.None,
            int attackScalePercent = 100,
            int defenseScalePercent = 100,
            bool useAlternateIfTargetHasDebuff = false,
            bool useAlternateIfTargetHasAnyStatus = false,
            int alternateAttackScalePercent = 0,
            int alternateValue = 0,
            int alternateAttackScaleIfActorUsedAttack = 0,
            int alternateValueIfActorUsedAttack = 0,
            int hitCount = 1,
            int damageMultiplierPercentIfRespondArmed = 100,
            int selfDamageFlat = 0,
            int repeatPerEnemyAttackCardThisTurn = 0,
            string summonCharacterId = "",
            int fallbackBlockDefenseScalePercent = 100,
            int fallbackBlockValue = 0,
            bool grantInvulnerableOnRespondArm = false,
            bool lifestealUnblockedOnly = false,
            int chancePercent = 0,
            bool useAlternateIfActorNotHitThisTurn = false,
            int selfBlockAboveThreshold = 0,
            int alternateValueIfSelfBlockAbove = 0) =>
            new()
            {
                Type = type,
                Target = target,
                Value = value,
                ScaleWithAttack = scaleAttack,
                ScaleWithDefense = scaleDefense,
                StatusId = statusId,
                Stacks = stacks,
                Duration = duration,
                Reach = reach,
                BackRowPowerPercent = backRowPowerPercent,
                Condition = condition,
                AttackScalePercent = attackScalePercent,
                DefenseScalePercent = defenseScalePercent,
                UseAlternateIfTargetHasDebuff = useAlternateIfTargetHasDebuff,
                UseAlternateIfTargetHasAnyStatus = useAlternateIfTargetHasAnyStatus,
                AlternateAttackScalePercent = alternateAttackScalePercent,
                AlternateValue = alternateValue,
                AlternateAttackScaleIfActorUsedAttack = alternateAttackScaleIfActorUsedAttack,
                AlternateValueIfActorUsedAttack = alternateValueIfActorUsedAttack,
                HitCount = hitCount,
                DamageMultiplierPercentIfRespondArmed = damageMultiplierPercentIfRespondArmed,
                SelfDamageFlat = selfDamageFlat,
                RepeatPerEnemyAttackCardThisTurn = repeatPerEnemyAttackCardThisTurn,
                SummonCharacterId = summonCharacterId ?? "",
                FallbackBlockDefenseScalePercent = fallbackBlockDefenseScalePercent,
                FallbackBlockValue = fallbackBlockValue,
                GrantInvulnerableOnRespondArm = grantInvulnerableOnRespondArm,
                LifestealUnblockedOnly = lifestealUnblockedOnly,
                ChancePercent = chancePercent,
                UseAlternateIfActorNotHitThisTurn = useAlternateIfActorNotHitThisTurn,
                SelfBlockAboveThreshold = selfBlockAboveThreshold,
                AlternateValueIfSelfBlockAbove = alternateValueIfSelfBlockAbove
            };

        static string[] Kw(params string[] ids) => ids;
    }
}
#endif
