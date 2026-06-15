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
        public struct AbyssMonsterSet
        {
            public CharacterDefinitionSO SeahorseGuard;
            public CharacterDefinitionSO JellyfishCaster;
            public CharacterDefinitionSO MermaidWarrior;
            public CharacterDefinitionSO AbyssCreature;
            public CharacterDefinitionSO CorruptedCrab;
            public CharacterDefinitionSO PhantomCaptain;
        }

        public static AbyssMonsterSet GenerateAbyssMonsters()
        {
            var cards = CreateAbyssCards();

            return new AbyssMonsterSet
            {
                SeahorseGuard = SaveMonster("Character_Seahorse_Guard", "char_seahorse_guard", "踏潮守卫",
                    FormationSlot.Middle, 100, 13, 7, 8,
                    new[] { MinionTraitCatalog.SeahorseGuardSpeedAttack },
                    Pool(cards.TideLance, 2, cards.TideCharge, 2, cards.WaterShield, 1,
                        cards.GuardStance, 2, cards.TailSplash, 1, cards.FinalGuard, 1)),
                JellyfishCaster = SaveMonster("Character_Jellyfish_Caster", "char_jellyfish_caster", "水母海巫",
                    FormationSlot.Back, 80, 11, 4, 6,
                    new[] { MinionTraitCatalog.JellyfishCasterSwapMaxHp },
                    Pool(cards.JellySting, 2, cards.PhaseCurrent, 2, cards.GelWall, 1,
                        cards.BounceSting, 1, cards.MagicLightning, 1, cards.FinalSummon, 1, cards.ParalyzeSting, 1)),
                MermaidWarrior = SaveMonster("Character_Mermaid_Warrior", "char_mermaid_warrior", "人鱼战士",
                    FormationSlot.Front, 100, 14, 6, 7,
                    new[] { MinionTraitCatalog.MermaidZeroCostAttack },
                    Pool(cards.MermaidSlash, 4, cards.TidalPower, 2, cards.MermaidShield, 1, cards.WaveCleave, 1)),
                AbyssCreature = SaveMonster("Character_Abyss_Creature", MinionTraitCatalog.AbyssCreatureCharacterId, "深渊怪物",
                    FormationSlot.Middle, 95, 12, 7, 4,
                    new[] { MinionTraitCatalog.AbyssCreaturePoisonOnDamage },
                    Pool(cards.AbyssLash, 2, cards.AbyssGaze, 1, cards.ShellCraft, 1,
                        cards.CorrosionVolley, 2, cards.PiercingTentacle, 2)),
                CorruptedCrab = SaveMonster("Character_Corrupted_Crab", "char_corrupted_crab", "腐蚀蟹",
                    FormationSlot.Front, 100, 8, 10, 4,
                    new[] { MinionTraitCatalog.CorruptedCrabPoisonOnHit },
                    Pool(cards.GiantClaw, 2, cards.AbyssGaze, 1, cards.ReforgeShell, 1,
                        cards.PinchArmor, 2, cards.FesterClaw, 1)),
                PhantomCaptain = SaveMonster("Character_Phantom_Captain", "char_phantom_captain", "鬼灵海盗船长",
                    FormationSlot.Middle, 130, 16, 7, 7,
                    new[] { MinionTraitCatalog.PhantomCaptainFrenzy },
                    Pool(cards.PhantomSlash, 2, cards.MusketShot, 2, cards.PhantomArmor, 1,
                        cards.PlunderStrike, 2, cards.PlunderCannon, 1, cards.Plunder, 2, cards.GhostShip, 1))
            };
        }

        public static void UpdateAbyssVisualCatalog(CharacterVisualCatalogSO catalog)
        {
            if (catalog == null)
                return;

            UpsertVisualLargest(catalog, "char_seahorse_guard", $"{ArtRoot}/seahorse_guard.png");
            UpsertVisualLargest(catalog, "char_jellyfish_caster", $"{ArtRoot}/jellyfish_caster.png");
            UpsertVisualLargest(catalog, "char_mermaid_warrior", $"{ArtRoot}/mermaid_warrior.png");
            UpsertVisualLargest(catalog, MinionTraitCatalog.AbyssCreatureCharacterId, $"{ArtRoot}/abyss_creature.png");
            UpsertVisualLargest(catalog, "char_corrupted_crab", $"{ArtRoot}/corrupted_crab.png");
            UpsertVisualLargest(catalog, "char_phantom_captain", $"{ArtRoot}/phantom_captain.png");
            EditorUtility.SetDirty(catalog);
        }

        struct AbyssCards
        {
            public CardDefinitionSO TideLance;
            public CardDefinitionSO TideCharge;
            public CardDefinitionSO WaterShield;
            public CardDefinitionSO GuardStance;
            public CardDefinitionSO TailSplash;
            public CardDefinitionSO FinalGuard;
            public CardDefinitionSO JellySting;
            public CardDefinitionSO PhaseCurrent;
            public CardDefinitionSO GelWall;
            public CardDefinitionSO BounceSting;
            public CardDefinitionSO MagicLightning;
            public CardDefinitionSO FinalSummon;
            public CardDefinitionSO ParalyzeSting;
            public CardDefinitionSO MermaidSlash;
            public CardDefinitionSO TidalPower;
            public CardDefinitionSO MermaidShield;
            public CardDefinitionSO WaveCleave;
            public CardDefinitionSO AbyssLash;
            public CardDefinitionSO AbyssGaze;
            public CardDefinitionSO ShellCraft;
            public CardDefinitionSO CorrosionVolley;
            public CardDefinitionSO PiercingTentacle;
            public CardDefinitionSO GiantClaw;
            public CardDefinitionSO ReforgeShell;
            public CardDefinitionSO PinchArmor;
            public CardDefinitionSO FesterClaw;
            public CardDefinitionSO PhantomSlash;
            public CardDefinitionSO MusketShot;
            public CardDefinitionSO PhantomArmor;
            public CardDefinitionSO PlunderStrike;
            public CardDefinitionSO PlunderCannon;
            public CardDefinitionSO Plunder;
            public CardDefinitionSO GhostShip;
        }

        static AbyssCards CreateAbyssCards() =>
            new()
            {
                TideLance = SaveCard("m_tide_lance", "枪刺", "char_seahorse_guard", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true)),
                TideCharge = SaveCard("m_tide_charge", "浪潮冲锋", "char_seahorse_guard", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        attackScalePercent: 150, reach: TargetReach.Any)),
                WaterShield = SaveCard("m_water_shield", "以水为盾", "char_seahorse_guard", 2, CardType.Defense, Kw("parry"),
                    CardRarity.Rare,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 60,
                        condition: ReactionConditionType.LastActionAttackOnSelf),
                    Action(EffectActionType.ApplyStatus, EffectTarget.LastActionActor, 0,
                        statusId: StatusCatalog.Slow, stacks: 3, duration: -1,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                GuardStance = SaveCard("m_guard_stance", "守卫姿态", "char_seahorse_guard", 1, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true)),
                TailSplash = SaveCard("m_tail_splash", "扫尾泼水", "char_seahorse_guard", 2, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 3)),
                FinalGuard = SaveCard("m_final_guard", "终焉守护", "char_seahorse_guard", 4, CardType.Defense, Kw("parry"),
                    CardRarity.Epic,
                    Action(EffectActionType.GainBlockFromLastDamagePercent, EffectTarget.Self, 50,
                        condition: ReactionConditionType.LastActionAttackOnSelf),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true, defenseScalePercent: 50),
                    Action(EffectActionType.ReducePlayerEnergyRegenNextTurn, EffectTarget.Self, 99)),
                JellySting = SaveCard("m_jelly_sting", "电刺击", "char_jellyfish_caster", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        attackScalePercent: 120)),
                PhaseCurrent = SaveCard("m_phase_current", "相位电流", "char_jellyfish_caster", 2, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemies, 2,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                GelWall = SaveCard("m_gel_wall", "凝胶护壁", "char_jellyfish_caster", 2, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true)),
                BounceSting = SaveCard("m_bounce_sting", "弹射蛰刺", "char_jellyfish_caster", 3, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 10, duration: -1)),
                MagicLightning = SaveCard("m_magic_lightning", "魔力之电", "char_jellyfish_caster", 2, CardType.Attack,
                    Kw("far_shot"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true,
                        reach: TargetReach.Any),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Burn, stacks: 5, duration: 3, reach: TargetReach.Any)),
                FinalSummon = SaveCard("m_final_summon", "终焉召唤", "char_jellyfish_caster", 4, CardType.Status,
                    Kw("exhaust", "summon"), CardRarity.Epic,
                    Action(EffectActionType.SummonOrGainBlock, EffectTarget.Self, 0,
                        summonCharacterId: MinionTraitCatalog.AbyssCreatureCharacterId,
                        fallbackBlockDefenseScalePercent: 100)),
                ParalyzeSting = SaveCard("m_paralyze_sting", "麻痹之电", "char_jellyfish_caster", 3, CardType.Attack,
                    Kw("melee"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true,
                        attackScalePercent: 180)),
                MermaidSlash = SaveCard("m_mermaid_slash", "劈砍", "char_mermaid_warrior", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        attackScalePercent: 120)),
                TidalPower = SaveCard("m_tidal_power", "潮汐之力", "char_mermaid_warrior", 3, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUpPercent, stacks: 30, duration: 2)),
                MermaidShield = SaveCard("m_mermaid_shield", "举盾", "char_mermaid_warrior", 2, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true)),
                WaveCleave = SaveCard("m_wave_cleave", "破浪斩", "char_mermaid_warrior", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        attackScalePercent: 150, reach: TargetReach.Any)),
                AbyssLash = SaveCard("m_abyss_lash", "深渊鞭笞", MinionTraitCatalog.AbyssCreatureCharacterId, 1,
                    CardType.Attack, Kw("melee"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 4, scaleAttack: true,
                        attackScalePercent: 30, reach: TargetReach.Any, hitCount: 3)),
                AbyssGaze = SaveCard("m_abyss_gaze", "深渊凝视", MinionTraitCatalog.AbyssCreatureCharacterId, 3,
                    CardType.Status, Kw("aoe"), CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.AllEnemies, 0,
                        statusId: StatusCatalog.DefenseDownPercent, stacks: 50, duration: 2, reach: TargetReach.Any)),
                ShellCraft = SaveCard("m_shell_craft", "制造外壳", MinionTraitCatalog.AbyssCreatureCharacterId, 2,
                    CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true, defenseScalePercent: 120)),
                CorrosionVolley = SaveCard("m_corrosion_volley", "腐蚀乱射", MinionTraitCatalog.AbyssCreatureCharacterId, 2,
                    CardType.Attack, Kw("far_shot"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.RandomEnemy, 5, scaleAttack: true,
                        attackScalePercent: 20, hitCount: 5)),
                PiercingTentacle = SaveCard("m_piercing_tentacle", "贯穿之触手", MinionTraitCatalog.AbyssCreatureCharacterId, 2,
                    CardType.Attack, Kw("melee"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        attackScalePercent: 150, reach: TargetReach.Any)),
                GiantClaw = SaveCard("m_giant_claw", "巨钳击", "char_corrupted_crab", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 20, scaleAttack: true,
                        attackScalePercent: 150)),
                ReforgeShell = SaveCard("m_reforge_shell", "重塑外壳", "char_corrupted_crab", 2, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true, defenseScalePercent: 120)),
                PinchArmor = SaveCard("m_pinch_armor", "夹断护甲", "char_corrupted_crab", 2, CardType.Status, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.DefenseDownPercent, stacks: 100, duration: 1, reach: TargetReach.Any)),
                FesterClaw = SaveCard("m_fester_claw", "溃烂钳击", "char_corrupted_crab", 3, CardType.Attack, Kw("parry"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true,
                        attackScalePercent: 120),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 8, duration: -1)),
                PhantomSlash = SaveCard("m_phantom_slash", "鬼魅斩击", "char_phantom_captain", 1, CardType.Attack, Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        attackScalePercent: 120)),
                MusketShot = SaveCard("m_musket_shot", "火枪射击", "char_phantom_captain", 1, CardType.Attack, Kw("far_shot"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        attackScalePercent: 120, reach: TargetReach.Any, backRowPowerPercent: 130)),
                PhantomArmor = SaveCard("m_phantom_armor", "鬼灵盾甲", "char_phantom_captain", 2, CardType.Defense, Kw("guard"),
                    Action(EffectActionType.GainBlock, EffectTarget.Self, 10, scaleDefense: true, defenseScalePercent: 120)),
                PlunderStrike = SaveCard("m_plunder_strike", "掠夺鬼击", "char_phantom_captain", 2, CardType.Attack, Kw("melee"),
                    CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        attackScalePercent: 120, reach: TargetReach.Any)),
                PlunderCannon = SaveCard("m_plunder_cannon", "掠夺火炮", "char_phantom_captain", 3, CardType.Attack,
                    Kw("exhaust", "aoe"), CardRarity.Rare,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 8, scaleAttack: true,
                        attackScalePercent: 120, reach: TargetReach.Any)),
                Plunder = SaveCard("m_plunder", "劫掠", "char_phantom_captain", 1, CardType.Status, Kw("slow"),
                    CardRarity.Rare,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemy, 0,
                        statusId: StatusCatalog.DefenseDownPercent, stacks: 100, duration: 1)),
                GhostShip = SaveCard("m_ghost_ship", "驾驶幽灵船", "char_phantom_captain", 4, CardType.Status,
                    Kw("exhaust", "slow"), CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.AttackUpPercent, stacks: 50, duration: -1),
                    Action(EffectActionType.RemoveStatus, EffectTarget.Self, 0))
            };
    }
}
#endif
