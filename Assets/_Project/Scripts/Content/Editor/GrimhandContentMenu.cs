#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grimhand.Content.Editor
{
    public static class GrimhandContentMenu
    {
        const string Root = "Assets/_Project/Data";
        const string SetupPath = Root + "/Setups/BattleSetup_Demo.asset";

        [MenuItem("Grimhand/Content/Generate Demo ScriptableObjects")]
        public static void GenerateDemoAssets()
        {
            EnsureFolder(Root + "/Cards");
            EnsureFolder(Root + "/Characters");
            EnsureFolder(Root + "/Setups");

            var playerCards = CreatePlayerCards();
            var enemyCards = CreateEnemyCards();
            var players = CreatePlayerCharacters(playerCards);
            var enemies = CreateEnemyCharacters(enemyCards);

            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(SetupPath);
            if (setup == null)
            {
                setup = ScriptableObject.CreateInstance<BattleSetupSO>();
                AssetDatabase.CreateAsset(setup, SetupPath);
            }

            setup.Seed = 42;
            setup.Combatants.Clear();
            setup.Combatants.AddRange(new[]
            {
                players.Knight, players.Mage, players.Ranger,
                enemies.Brute, enemies.Shaman, enemies.Archer
            });
            EditorUtility.SetDirty(setup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = setup;
            EditorGUIUtility.PingObject(setup);

            var assigned = TryAssignSetupToScene(setup, showDialog: false);
            var assignHint = assigned
                ? "已自动绑定到场景中的 BattleDemo。"
                : "请在 BattleSandbox 场景选中 BattleDemo，将 Battle Setup 拖入组件。";

            EditorUtility.DisplayDialog(
                "Demo 数据已生成",
                "已在 Project 窗口创建/更新：\n\n" +
                "• Assets/_Project/Data/Cards/\n" +
                "• Assets/_Project/Data/Characters/\n" +
                "• Assets/_Project/Data/Setups/BattleSetup_Demo.asset\n\n" +
                "本场为 3 我方 vs 3 敌方（前排蛮兵 / 中排萨满 / 后排弓手）。\n\n" +
                assignHint,
                "好的");
        }

        [MenuItem("Grimhand/Content/Assign Demo Battle Setup to Scene")]
        public static void AssignDemoSetupMenu()
        {
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(SetupPath);
            if (setup == null)
            {
                EditorUtility.DisplayDialog(
                    "未找到战斗配置",
                    "请先在菜单执行：\nGrimhand → Content → Generate Demo ScriptableObjects",
                    "好的");
                return;
            }

            if (TryAssignSetupToScene(setup, showDialog: true))
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static bool TryAssignSetupToScene(BattleSetupSO setup, bool showDialog)
        {
            var controller = Object.FindAnyObjectByType<BattleDemoController>();
            if (controller == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "未找到 BattleDemo",
                        "请打开 BattleSandbox 场景，或执行 Grimhand → Setup Battle Sandbox Scene。",
                        "好的");
                }

                return false;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("battleSetup").objectReferenceValue = setup;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "绑定成功",
                    "已将 BattleSetup_Demo 绑定到 Battle Demo Controller。\n按 Play 即可使用 SO 数据开战。",
                    "好的");
            }

            return true;
        }

        static PlayerCardSet CreatePlayerCards()
        {
            return new PlayerCardSet
            {
                Strike = SaveCard("k_strike", "重击", "char_knight", 1, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true)),
                Slash = SaveCard("k_slash", "斩击", "char_knight", 2, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14, scaleAttack: true)),
                Parry = SaveCard("k_parry", "弹反", "char_knight", 2, CardType.Defense,
                    Kw("parry"),
                    new EffectActionDefinition
                    {
                        Type = EffectActionType.GainBlockFromLastDamagePercent,
                        Target = EffectTarget.Self,
                        Value = 50,
                        Condition = ReactionConditionType.LastActionAttackOnSelf
                    },
                    new EffectActionDefinition
                    {
                        Type = EffectActionType.ReflectLastDamageToAttacker,
                        Target = EffectTarget.LastActionActor,
                        Value = 200,
                        Condition = ReactionConditionType.LastActionAttackOnSelf
                    }),
                Bolt = SaveCard("m_bolt", "魔弹", "char_mage", 1, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 7, scaleAttack: true)),
                Poison = SaveCard("m_poison", "毒云", "char_mage", 2, CardType.Status,
                    Kw("poison"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 10)),
                Snipe = SaveCard("r_snipe", "狙击", "char_ranger", 2, CardType.Attack,
                    Kw("snipe"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 15, scaleAttack: true,
                        reach: TargetReach.Any)),
                Pierce = SaveCard("r_pierce", "贯射", "char_ranger", 2, CardType.Attack,
                    Kw("pierce", "melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 11, scaleAttack: true,
                        reach: TargetReach.FrontAndMiddle, splashBehind: true, splashPowerPercent: 80)),
                FarShot = SaveCard("r_far_shot", "远射", "char_ranger", 2, CardType.Attack,
                    Kw("far_shot"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true,
                        reach: TargetReach.Any, backRowPowerPercent: 70)),
                Slow = SaveCard("r_slow", "缚足", "char_ranger", 1, CardType.Status,
                    Kw("slow", "slot"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.EnemyBackSlot, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2))
            };
        }

        static EnemyCardSet CreateEnemyCards()
        {
            return new EnemyCardSet
            {
                Bite = SaveCard("g_bite", "撕咬", "char_goblin_brute", 1, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true)),
                Scratch = SaveCard("g_scratch", "抓挠", "char_goblin_brute", 1, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 5, scaleAttack: true)),
                Lunge = SaveCard("g_lunge", "猛扑", "char_goblin_brute", 2, CardType.Attack,
                    Kw("melee"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 10, scaleAttack: true)),
                Hex = SaveCard("g_hex", "邪咒", "char_goblin_shaman", 2, CardType.Status,
                    Kw("poison"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Poison, stacks: 5)),
                Wither = SaveCard("g_wither", "虚弱", "char_goblin_shaman", 1, CardType.Status,
                    Kw("slow"),
                    Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0,
                        statusId: StatusCatalog.Slow, stacks: 1, duration: 2)),
                Arrow = SaveCard("g_arrow", "箭矢", "char_goblin_archer", 1, CardType.Attack,
                    Kw("far_shot"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true,
                        reach: TargetReach.Any, backRowPowerPercent: 80)),
                Aim = SaveCard("g_aim", "瞄准", "char_goblin_archer", 2, CardType.Attack,
                    Kw("snipe"),
                    Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14, scaleAttack: true,
                        reach: TargetReach.Any))
            };
        }

        static PlayerCharacters CreatePlayerCharacters(PlayerCardSet cards)
        {
            return new PlayerCharacters
            {
                Knight = SaveCharacter("Character_Knight", "char_knight", "骑士", TeamSide.Player,
                    FormationSlot.Front, 2, 40, 6, 4, 10,
                    BuildDeck(Repeat(cards.Strike, 4), Repeat(cards.Slash, 2), Of(cards.Parry, cards.Parry))),
                Mage = SaveCharacter("Character_Mage", "char_mage", "法师", TeamSide.Player,
                    FormationSlot.Middle, 1, 28, 5, 2, 5,
                    BuildDeck(Repeat(cards.Bolt, 6), Of(cards.Poison, cards.Poison))),
                Ranger = SaveCharacter("Character_Ranger", "char_ranger", "游侠", TeamSide.Player,
                    FormationSlot.Back, 1, 30, 7, 2, 7,
                    BuildDeck(Repeat(cards.Snipe, 2), Repeat(cards.Pierce, 3),
                        Repeat(cards.FarShot, 2), Repeat(cards.Slow, 3)))
            };
        }

        static EnemyCharacters CreateEnemyCharacters(EnemyCardSet cards)
        {
            return new EnemyCharacters
            {
                Brute = SaveCharacter("Character_Goblin_Brute", "char_goblin_brute", "哥布林蛮兵", TeamSide.Enemy,
                    FormationSlot.Front, 2, 45, 7, 2, 8,
                    BuildDeck(Repeat(cards.Bite, 4), Repeat(cards.Scratch, 2), Repeat(cards.Lunge, 2))),
                Shaman = SaveCharacter("Character_Goblin_Shaman", "char_goblin_shaman", "哥布林萨满", TeamSide.Enemy,
                    FormationSlot.Middle, 1, 32, 4, 1, 6,
                    BuildDeck(Repeat(cards.Hex, 4), Repeat(cards.Wither, 4), Repeat(cards.Bite, 2))),
                Archer = SaveCharacter("Character_Goblin_Archer", "char_goblin_archer", "哥布林弓手", TeamSide.Enemy,
                    FormationSlot.Back, 1, 28, 8, 1, 9,
                    BuildDeck(Repeat(cards.Arrow, 6), Repeat(cards.Aim, 4)))
            };
        }

        struct PlayerCardSet
        {
            public CardDefinitionSO Strike, Slash, Parry, Bolt, Poison, Snipe, Pierce, FarShot, Slow;
        }

        struct EnemyCardSet
        {
            public CardDefinitionSO Bite, Scratch, Lunge, Hex, Wither, Arrow, Aim;
        }

        struct PlayerCharacters
        {
            public CharacterDefinitionSO Knight, Mage, Ranger;
        }

        struct EnemyCharacters
        {
            public CharacterDefinitionSO Brute, Shaman, Archer;
        }

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
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
            card.Keywords.Clear();
            if (keywords != null)
                card.Keywords.AddRange(keywords);
            card.Actions.Clear();
            card.Actions.AddRange(actions);
            EditorUtility.SetDirty(card);
            return card;
        }

        static string[] Kw(params string[] ids) => ids;

        static CharacterDefinitionSO SaveCharacter(
            string assetName,
            string charId,
            string displayName,
            TeamSide team,
            FormationSlot slot,
            int level,
            int hp,
            int atk,
            int def,
            int spd,
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
            character.Team = team;
            character.Slot = slot;
            character.Level = level;
            character.MaxHp = hp;
            character.BaseAttack = atk;
            character.BaseDefense = def;
            character.Speed = spd;
            character.Deck.Clear();
            character.Deck.AddRange(deck);
            EditorUtility.SetDirty(character);
            return character;
        }

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
            bool splashBehind = false,
            int splashPowerPercent = 100,
            int backRowPowerPercent = 100)
        {
            return new EffectActionDefinition
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
                SplashBehindTarget = splashBehind,
                SplashPowerPercent = splashPowerPercent,
                BackRowPowerPercent = backRowPowerPercent
            };
        }

        static CardDefinitionSO[] Repeat(CardDefinitionSO card, int count)
        {
            var arr = new CardDefinitionSO[count];
            for (var i = 0; i < count; i++)
                arr[i] = card;
            return arr;
        }

        static CardDefinitionSO[] Of(params CardDefinitionSO[] cards) => cards;

        static CardDefinitionSO[] BuildDeck(params CardDefinitionSO[][] parts)
        {
            var list = new List<CardDefinitionSO>();
            foreach (var part in parts)
                list.AddRange(part);
            return list.ToArray();
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
