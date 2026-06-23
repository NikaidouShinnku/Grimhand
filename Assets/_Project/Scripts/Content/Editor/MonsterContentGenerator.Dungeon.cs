#if UNITY_EDITOR
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static partial class MonsterContentGenerator
    {
        public struct DungeonMonsterSet
        {
            public CharacterDefinitionSO Rat;
            public CharacterDefinitionSO ChainWraith;
            public CharacterDefinitionSO Gargoyle;
            public CharacterDefinitionSO SpiderLady;
            public CharacterDefinitionSO StoneGolem;
        }

        public static DungeonMonsterSet GenerateDungeonMonsters()
        {
            var cards = CreateDungeonCards();

            return new DungeonMonsterSet
            {
                Rat = SaveMonster("Character_Rat", "char_rat", "鼠人",
                    FormationSlot.Middle, 55, 9, 3, 6,
                    new[] { MinionTraitCatalog.RatPackAttackOnAllyDeath },
                    Pool(cards.RatPunch, 2, cards.RatTrash, 2, cards.RatAmbush, 1,
                        cards.RatMorale, 1, cards.RatBurrow, 1, cards.RatSwarmCall, 1)),
                ChainWraith = SaveMonster("Character_Chain_Wraith", "char_chain_wraith", "锁链怨灵",
                    FormationSlot.Middle, 65, 11, 5, 5,
                    new[] { MinionTraitCatalog.ChainWraithDebuffShare },
                    Pool(cards.ChainWhip, 2, cards.ChainGrudge, 2, cards.ChainThrow, 1,
                        cards.ChainGuard, 2, cards.GrudgeGuard, 1, cards.FinalBind, 1, cards.ChainRecharge, 1)),
                Gargoyle = SaveMonster("Character_Gargoyle", "char_gargoyle", "石像鬼",
                    FormationSlot.Front, 70, 8, 7, 4,
                    new[] { MinionTraitCatalog.GargoyleFirstCardStance },
                    Pool(cards.GargoyleClaw, 2, cards.GargoylePetrify, 2, cards.GargoyleSunder, 1,
                        cards.GargoyleEmpower, 1, cards.GargoyleCounter, 1, cards.GargoyleSleepStone, 1)),
                SpiderLady = SaveMonster("Character_Spider_Lady", "char_spider_lady", "蜘蛛贵妇",
                    FormationSlot.Back, 60, 9, 4, 7,
                    new[] { MinionTraitCatalog.SpiderLadyPoisonVulnerability },
                    Pool(cards.SpiderFang, 2, cards.SpiderSilk, 2, cards.SpiderTrap, 1,
                        cards.SpiderSpray, 1, cards.SpiderWrap, 1, cards.SpiderFatalBind, 1)),
                StoneGolem = SaveMonster("Character_Stone_Golem", "char_stone_golem", "石傀儡",
                    FormationSlot.Front, 80, 10, 9, 2,
                    new[] { MinionTraitCatalog.StoneGolemArmorRetain },
                    Pool(cards.GolemFist, 2, cards.GolemWall, 2, cards.GolemQuake, 2,
                        cards.GolemUnmovable, 1, cards.GolemQuakeSlam, 1, cards.GolemCrackFist, 1))
            };
        }

        public static void UpdateDungeonVisualCatalog(CharacterVisualCatalogSO catalog)
        {
            if (catalog == null)
                return;

            UpsertVisual(catalog, "char_rat", $"{ArtRoot}/boxer_rat.png");
            UpsertVisual(catalog, "char_chain_wraith", $"{ArtRoot}/chained_wraith.png");
            UpsertVisual(catalog, "char_gargoyle", $"{ArtRoot}/gargoyle.png");
            UpsertVisual(catalog, "char_spider_lady", $"{ArtRoot}/spider_girl.png");
            UpsertVisual(catalog, "char_stone_golem", $"{ArtRoot}/stone_golem.png");
            EditorUtility.SetDirty(catalog);
        }

        struct DungeonCards
        {
            public CardDefinitionSO RatPunch;
            public CardDefinitionSO RatTrash;
            public CardDefinitionSO RatAmbush;
            public CardDefinitionSO RatMorale;
            public CardDefinitionSO RatBurrow;
            public CardDefinitionSO RatSwarmCall;
            public CardDefinitionSO ChainWhip;
            public CardDefinitionSO ChainGrudge;
            public CardDefinitionSO ChainThrow;
            public CardDefinitionSO ChainGuard;
            public CardDefinitionSO GrudgeGuard;
            public CardDefinitionSO FinalBind;
            public CardDefinitionSO ChainRecharge;
            public CardDefinitionSO GargoyleClaw;
            public CardDefinitionSO GargoylePetrify;
            public CardDefinitionSO GargoyleSunder;
            public CardDefinitionSO GargoyleEmpower;
            public CardDefinitionSO GargoyleCounter;
            public CardDefinitionSO GargoyleSleepStone;
            public CardDefinitionSO SpiderFang;
            public CardDefinitionSO SpiderSilk;
            public CardDefinitionSO SpiderTrap;
            public CardDefinitionSO SpiderSpray;
            public CardDefinitionSO SpiderWrap;
            public CardDefinitionSO SpiderFatalBind;
            public CardDefinitionSO GolemFist;
            public CardDefinitionSO GolemWall;
            public CardDefinitionSO GolemQuake;
            public CardDefinitionSO GolemUnmovable;
            public CardDefinitionSO GolemQuakeSlam;
            public CardDefinitionSO GolemCrackFist;
        }

        static DungeonCards CreateDungeonCards() =>
            new()
            {
                RatPunch = SaveCard("m_rat_punch", "鼠人拳击", "char_rat", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true)),
                RatTrash = SaveCard("m_rat_trash", "投掷垃圾", "char_rat", 1, CardType.Attack, Kw("far_shot"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true,
                        reach: TargetReach.MiddleAndBack)),
                RatAmbush = SaveCard("m_rat_ambush", "偷袭", "char_rat", 2, CardType.Attack, Kw("parry"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        damageMultiplierPercentIfRespondArmed: 300)),
                RatMorale = SaveCard("m_rat_morale", "提振士气", "char_rat", 2, CardType.Status, Kw("slow"), CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUpPercent, stacks: 20, duration: 2)),
                RatBurrow = SaveCard("m_rat_burrow", "钻地逃遁", "char_rat", 2, CardType.Defense, Kw("guard"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 70,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                RatSwarmCall = SaveCard("m_rat_swarm_call", "呼唤鼠群", "char_rat", 3, CardType.Status, Kw("exhaust"),
                    CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.RatSwarmCall, stacks: 1, duration: -1)),
                ChainWhip = SaveCard("m_chain_whip", "锁链鞭打", "char_chain_wraith", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7, scaleAttack: true)),
                ChainGrudge = SaveCard("m_chain_grudge", "怨气缠绕", "char_chain_wraith", 1, CardType.Status, Kw("slow"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2),
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                ChainThrow = SaveCard("m_chain_throw", "怨链投掷", "char_chain_wraith", 2, CardType.Attack, Kw("far_shot"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 9, scaleAttack: true,
                        reach: TargetReach.Any, useAlternateIfTargetHasDebuff: true,
                        alternateAttackScalePercent: 150, alternateValue: 1)),
                ChainGuard = SaveCard("m_chain_guard", "锁链护体", "char_chain_wraith", 1, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 0, scaleDefense: true)),
                GrudgeGuard = SaveCard("m_grudge_guard", "怨气护体", "char_chain_wraith", 1, CardType.Defense, Kw("parry"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 90,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                FinalBind = SaveCard("m_final_bind", "终焉魂缚", "char_chain_wraith", 3, CardType.Status, Kw("poison"),
                    CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 15, duration: -1,
                        reach: TargetReach.FrontAndMiddle)),
                ChainRecharge = SaveCard("m_chain_recharge", "回气", "char_chain_wraith", 3, CardType.Status, Kw("exhaust"),
                    CardRarity.SuperRare,
                    new EffectActionDefinition
                    {
                        Type = EffectActionType.Heal,
                        Target = EffectTarget.Self,
                        HealMaxHpPercent = 40
                    },
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.Poison, stacks: 5, duration: 2)),
                GargoyleClaw = SaveCard("m_gargoyle_claw", "利爪斩击", "char_gargoyle", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true)),
                GargoylePetrify = SaveCard("m_gargoyle_petrify", "石化形态", "char_gargoyle", 2, CardType.Defense, Kw("guard"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 8, scaleDefense: true, defenseScalePercent: 150),
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 1)),
                GargoyleSunder = SaveCard("m_gargoyle_sunder", "破甲冲锋", "char_gargoyle", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        attackScalePercent: 80, reach: TargetReach.Any)),
                GargoyleEmpower = SaveCard("m_gargoyle_empower", "活体强化", "char_gargoyle", 3, CardType.Status, Kw("slow"),
                    CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUpPercent, stacks: 100, duration: 1)),
                GargoyleCounter = SaveCard("m_gargoyle_counter", "崩石反击", "char_gargoyle", 1, CardType.Attack, Kw("parry"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 50,
                        condition: ReactionConditionType.LastActionAttackOnSelf),
                    Action(EffectActionType.ReflectLastDamageToAttacker, EffectTarget.LastActionActor, 100,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                GargoyleSleepStone = SaveCard("m_gargoyle_sleep_stone", "沉睡之石", "char_gargoyle", 2, CardType.Status,
                    Kw("slow"), CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.DefenseUpPercent, stacks: 50, duration: 2)),
                SpiderFang = SaveCard("m_spider_fang", "毒牙刺击", "char_spider_lady", 1, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 5, duration: -1)),
                SpiderSilk = SaveCard("m_spider_silk", "蛛丝缠绕", "char_spider_lady", 2, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 3)),
                SpiderTrap = SaveCard("m_spider_trap", "蛛网陷阱", "char_spider_lady", 2, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 2)),
                SpiderSpray = SaveCard("m_spider_spray", "剧毒喷射", "char_spider_lady", 3, CardType.Attack, Kw("aoe"),
                    CardRarity.Epic,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 3, scaleAttack: true,
                        reach: TargetReach.Any),
                    Action(EffectActionType.ApplyStatus, EffectTarget.AllEnemies, 0,
                        statusId: StatusCatalog.Poison, stacks: 10, duration: -1, reach: TargetReach.Any)),
                SpiderWrap = SaveCard("m_spider_wrap", "蛛网包裹", "char_spider_lady", 2, CardType.Defense, Kw("parry"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 50,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                SpiderFatalBind = SaveCard("m_spider_fatal_bind", "致命缠杀", "char_spider_lady", 4, CardType.Attack,
                    Kw("exhaust"), CardRarity.Epic,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 30, scaleAttack: true,
                        reach: TargetReach.MiddleAndBack)),
                GolemFist = SaveCard("m_golem_fist", "石拳", "char_stone_golem", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true)),
                GolemWall = SaveCard("m_golem_wall", "石之壁垒", "char_stone_golem", 2, CardType.Defense, Kw("guard"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 6, scaleDefense: true, defenseScalePercent: 150)),
                GolemQuake = SaveCard("m_golem_quake", "地震波", "char_stone_golem", 2, CardType.Attack, Kw("aoe"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 6, scaleAttack: true,
                        attackScalePercent: 70, reach: TargetReach.Any)),
                GolemUnmovable = SaveCard("m_golem_unmovable", "不动如山", "char_stone_golem", 3, CardType.Defense, Kw("parry"),
                    CardRarity.Epic,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 90,
                        condition: ReactionConditionType.LastActionAttackOnSelf),
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.DefenseUp, stacks: 5, duration: 2)),
                GolemQuakeSlam = SaveCard("m_golem_quake_slam", "山崩地裂", "char_stone_golem", 3, CardType.Attack, Kw("melee"),
                    CardRarity.Epic,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14, scaleAttack: true,
                        attackScalePercent: 150, reach: TargetReach.FrontAndMiddle)),
                GolemCrackFist = SaveCard("m_golem_crack_fist", "崩裂拳", "char_stone_golem", 3, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 6, scaleDefense: true, defenseScalePercent: 80))
            };
    }
}
#endif
