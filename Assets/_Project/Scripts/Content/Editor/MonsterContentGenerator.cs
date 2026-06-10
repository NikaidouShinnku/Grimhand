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
    public static class MonsterContentGenerator
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
                    FormationSlot.Front, 20, 4, 1, 5,
                    cards.GoblinPool),
                Slime = SaveMonster("Character_Slime", "char_slime", "史莱姆",
                    FormationSlot.Front, 30, 3, 4, 2,
                    cards.SlimePool),
                Skeleton = SaveMonster("Character_Skeleton", "char_skeleton", "骷髅兵",
                    FormationSlot.Middle, 25, 6, 3, 4,
                    cards.SkeletonPool),
                SkeletonElite = SaveMonster("Character_Skeleton_Elite", "char_skeleton_elite", "骷髅精英",
                    FormationSlot.Middle, 45, 9, 5, 5,
                    cards.SkeletonElitePool),
                Wraith = SaveMonster("Character_Wraith", "char_wraith", "幽灵",
                    FormationSlot.Back, 18, 7, 1, 7,
                    cards.WraithPool),
                WraithElite = SaveMonster("Character_Wraith_Elite", "char_wraith_elite", "幽灵精英",
                    FormationSlot.Back, 35, 11, 2, 9,
                    cards.WraithElitePool),
                SkeletonKing = SaveBoss("Character_Skeleton_King", "char_skeleton_king", "骷髅王",
                    FormationSlot.Front, 400, 30, 10, 6,
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
                    FormationSlot.Middle, 50, 0, 5, 2,
                    CharacterTraitCatalog.SkullSelfDestructHand,
                    BuildFixedDeck((cards.SkullExplode, 1))),
                GhostQueen = SaveBoss("Character_Ghost_Queen", GhostQueenBossEncounterBuilder.CharacterId, "幽灵女王",
                    FormationSlot.Front, 360, 25, 8, 7,
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

            // Demo 旧 ID 兼容
            UpsertVisual(catalog, "char_goblin_brute", $"{ArtRoot}/goblin_idle_1024.png");
            UpsertVisual(catalog, "char_goblin_shaman", $"{ArtRoot}/skeleton_idle_1024.png");
            UpsertVisual(catalog, "char_goblin_archer", $"{ArtRoot}/wraith_idle_1024.png");

            var kingArt = ArtRoot + "/skeleton king";
            UpsertVisualFull(catalog, "char_skeleton_king",
                idle: kingArt + "/skeletonking_idle_1024.png",
                attack: kingArt + "/skeletonking_attack_1024.png",
                hit: kingArt + "/skeletonking_hit_1024.png",
                death: kingArt + "/skeletonking_defeat_1024.png",
                profile: kingArt + "/skeletonking_profile.png",
                gifPath: "The Grimhands Asset/monsters/skeleton king/skeletonking_idle_anime.gif",
                defendUsesHit: true);
            UpsertVisual(catalog, "char_explosive_skull", ArtRoot + "/skeletonhead_idle_1024.png");

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

            EditorUtility.SetDirty(catalog);
        }

        static Sprite LoadMonsterCardProfilePortrait()
        {
            const string path = "Assets/The Grimhands Asset/card/card_profile_monsters.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        struct MonsterCards
        {
            public CardDefinitionSO[] GoblinPool;
            public CardDefinitionSO[] SlimePool;
            public CardDefinitionSO[] SkeletonPool;
            public CardDefinitionSO[] SkeletonElitePool;
            public CardDefinitionSO[] WraithPool;
            public CardDefinitionSO[] WraithElitePool;
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
                GoblinPool = new[]
                {
                    SaveCard("g_scratch", "抓挠", "char_goblin", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true)),
                    SaveCard("g_bite", "撕咬", "char_goblin", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true)),
                    SaveCard("g_lunge", "猛扑", "char_goblin", 2, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true)),
                    SaveCard("g_throw", "投石", "char_goblin", 1, CardType.Attack, Kw("far_shot"),
                        Action(EffectActionType.DealDamage, EffectTarget.EnemyBackSlot, 4, scaleAttack: true,
                            reach: TargetReach.Any, backRowPowerPercent: 100))
                },
                SlimePool = new[]
                {
                    SaveCard("m_slime_slam", "黏糊撞击", "char_slime", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 4, scaleAttack: true)),
                    SaveCard("m_slime_shield", "凝胶护盾", "char_slime", 1, CardType.Defense, Kw("guard"),
                        Action(EffectActionType.GainBlock, EffectTarget.Self, 6, scaleDefense: true)),
                    SaveCard("m_slime_split", "分裂", "char_slime", 2, CardType.Status, Kw("slow"),
                        Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                            statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                    SaveCard("m_slime_absorb", "吸收", "char_slime", 2, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true),
                        Action(EffectActionType.Heal, EffectTarget.Self, 4))
                },
                SkeletonPool = new[]
                {
                    SaveCard("m_bone_slash", "骨剑斩", "char_skeleton", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true)),
                    SaveCard("m_bone_shield", "举盾", "char_skeleton", 1, CardType.Defense, Kw("guard"),
                        Action(EffectActionType.GainBlock, EffectTarget.Self, 8)),
                    SaveCard("m_bone_toss", "投骨", "char_skeleton", 2, CardType.Attack, Kw("far_shot"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                            reach: TargetReach.Any)),
                    SaveCard("g_wither", "虚弱", "char_skeleton", 1, CardType.Status, Kw("slow"),
                        Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                            statusId: StatusCatalog.Slow, stacks: 1, duration: 2))
                },
                SkeletonElitePool = new[]
                {
                    SaveCard("m_bone_crush", "骨碎斩", "char_skeleton_elite", 2, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 12, scaleAttack: true)),
                    SaveCard("m_bone_wall", "骨墙", "char_skeleton_elite", 2, CardType.Defense, Kw("guard"),
                        Action(EffectActionType.GainBlock, EffectTarget.Self, 15)),
                    SaveCard("m_raise_bones", "唤骨", "char_skeleton_elite", 3, CardType.Status, Kw("summon"),
                        Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                            statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                    SaveCard("g_bite", "撕咬", "char_skeleton_elite", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7, scaleAttack: true))
                },
                WraithPool = new[]
                {
                    SaveCard("m_soul_strike", "灵魂打击", "char_wraith", 1, CardType.Attack, Kw("melee"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7, scaleAttack: true)),
                    SaveCard("g_hex", "邪咒", "char_wraith", 2, CardType.Status, Kw("poison"),
                        Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                            statusId: StatusCatalog.Poison, stacks: 5)),
                    SaveCard("m_phase", "隐身", "char_wraith", 1, CardType.Defense, Kw("guard"),
                        Action(EffectActionType.GainBlock, EffectTarget.Self, 5)),
                    SaveCard("g_arrow", "箭矢", "char_wraith", 1, CardType.Attack, Kw("far_shot"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                            reach: TargetReach.Any, backRowPowerPercent: 80))
                },
                WraithElitePool = new[]
                {
                    SaveCard("m_soul_storm", "灵魂风暴", "char_wraith_elite", 3, CardType.Attack, Kw("aoe"),
                        Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 10, scaleAttack: true)),
                    SaveCard("m_curse", "诅咒", "char_wraith_elite", 2, CardType.Status, Kw("slow"),
                        Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                            statusId: StatusCatalog.Slow, stacks: 2, duration: 2)),
                    SaveCard("m_void", "虚无", "char_wraith_elite", 2, CardType.Defense, Kw("guard"),
                        Action(EffectActionType.GainBlock, EffectTarget.Self, 12)),
                    SaveCard("g_aim", "瞄准", "char_wraith_elite", 2, CardType.Attack, Kw("snipe"),
                        Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14, scaleAttack: true,
                            reach: TargetReach.Any))
                },
                KingBoneSlash = SaveCard("m_king_bone_slash", "骨王斩击", "char_skeleton_king", 1,
                    CardType.Attack, Kw("melee"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 15, scaleAttack: true)),
                KingBoneRoar = SaveCard("m_king_bone_roar", "骨王怒吼", "char_skeleton_king", 1,
                    CardType.Status, Kw("slow"), CardRarity.Common,
                    Action(EffectActionType.ApplyStatus, EffectTarget.RandomEnemies, 2,
                        statusId: StatusCatalog.Slow, stacks: 2, duration: 2)),
                KingBoneSpear = SaveCard("m_king_bone_spear", "投掷骨矛", "char_skeleton_king", 1,
                    CardType.Attack, Kw("far_shot"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 15, scaleAttack: true,
                        reach: TargetReach.MiddleAndBack)),
                KingSummonWorkshop = SaveCard("m_king_summon_workshop", "召唤骨之王座", "char_skeleton_king", 3,
                    CardType.Status, Kw("exhaust", "summon"), CardRarity.Epic,
                    Action(EffectActionType.ApplyStatus, EffectTarget.Self, 0,
                        statusId: StatusCatalog.BoneWorkshop, stacks: 1, duration: -1)),
                KingBoneBlock = SaveCard("m_king_bone_block", "骨甲格挡", "char_skeleton_king", 1,
                    CardType.Defense, Kw("guard"), CardRarity.Common,
                    RespondBlock(80)),
                KingBoneShield = SaveCard("m_king_bone_shield", "召唤骨盾", "char_skeleton_king", 2,
                    CardType.Defense, Kw("guard"), CardRarity.Common,
                    DefBlockScaled(200)),
                KingWhiteStorm = SaveCard("m_king_white_storm", "白骨风暴", "char_skeleton_king", 3,
                    CardType.Attack, Kw("aoe"), CardRarity.Epic,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 12, scaleAttack: true,
                        reach: TargetReach.Any)),
                SkullExplode = SaveCard("m_skull_explode", "骷髅自爆", "char_explosive_skull", 0,
                    CardType.Attack, Kw("self_destruct", "bonus_hand"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.RandomEnemy, 40)),
                QueenClaw = SaveCard("m_queen_claw", "幽灵爪击", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Attack, Kw("snipe"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 20, scaleAttack: true,
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
                        statusId: StatusCatalog.Poison, stacks: 3, duration: -1, reach: TargetReach.Any)),
                QueenCommand = SaveCard("m_queen_command", "女王的命令", GhostQueenBossEncounterBuilder.CharacterId, 2,
                    CardType.Defense, Kw("parry"), CardRarity.Epic,
                    Action(EffectActionType.ArmRespondDamageRedirect, EffectTarget.Self, 0,
                        condition: ReactionConditionType.LastActionAttackOnSelf)),
                QueenSpiritGuard = SaveCard("m_queen_spirit_guard", "灵气护体", GhostQueenBossEncounterBuilder.CharacterId, 1,
                    CardType.Defense, Kw("guard"), CardRarity.Common,
                    DefBlockScaled(200)),
                QueenBurst = SaveCard("m_queen_burst", "幽灵爆发", GhostQueenBossEncounterBuilder.CharacterId, 4,
                    CardType.Attack, Kw("aoe"), CardRarity.Common,
                    Action(EffectActionType.DealDamage, EffectTarget.AllEnemies, 20, scaleAttack: true,
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
            character.EnemyRandomDeckSize = 8;
            character.EnemySkillPickMin = 2;
            character.EnemySkillPickMax = System.Math.Min(4, skillPool.Length);
            EditorUtility.SetDirty(character);
            return character;
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
            character.EnemyRandomDeckSize = deck.Length;
            character.EnemySkillPickMin = 0;
            character.EnemySkillPickMax = 0;
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
            bool defendUsesHit = false,
            bool preserveOriginalFacing = false)
        {
            var idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idle);
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
            entry.AttackPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(attack) ?? idleSprite;
            entry.HitPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(hit) ?? idleSprite;
            entry.DefensePortrait = defendUsesHit
                ? entry.HitPortrait
                : AssetDatabase.LoadAssetAtPath<Sprite>(hit) ?? idleSprite;
            entry.DeathPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(death) ?? idleSprite;
            entry.CardProfilePortrait = string.IsNullOrEmpty(profile)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(profile);
            entry.IdleAnimationGifPath = gifPath ?? "";
            entry.PreserveOriginalFacing = preserveOriginalFacing;
        }

        static void UpsertVisual(CharacterVisualCatalogSO catalog, string characterId, string spritePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
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

            entry.IdlePortrait = sprite;
            entry.AttackPortrait = sprite;
            entry.DefensePortrait = sprite;
            entry.HitPortrait = sprite;
            entry.DeathPortrait = sprite;
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
            ReactionConditionType condition = ReactionConditionType.None) =>
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
                Condition = condition
            };

        static string[] Kw(params string[] ids) => ids;
    }
}
#endif
