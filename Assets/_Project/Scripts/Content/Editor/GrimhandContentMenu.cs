#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Presentation;
using Grimhand.Presentation.Battle;
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
            GenerateDemoAssetsSilent(showDialog: true);
        }

        public static void GenerateDemoAssetsSilent(bool showDialog = false)
        {
            EnsureFolder(Root + "/Cards");
            EnsureFolder(Root + "/Characters");
            EnsureFolder(Root + "/Setups");

            var players = BalanceV2ContentGenerator.GeneratePlayerContent();
            var monsters = MonsterContentGenerator.Generate();
            RelicArtBinder.BindRelicArtSilent();

            var setupClassic = SaveBattleSetup(
                "BattleSetup_Demo",
                players.Warrior, players.Pharaoh, players.Demon,
                monsters.Goblin, monsters.Skeleton, monsters.Wraith);

            var setupSlimeMix = SaveBattleSetup(
                "BattleSetup_Encounter_SlimeMix",
                players.Warrior, players.Pharaoh, players.Demon,
                monsters.Goblin, monsters.Slime, monsters.Skeleton);

            var setupWraithPack = SaveBattleSetup(
                "BattleSetup_Encounter_WraithPack",
                players.Warrior, players.Pharaoh, players.Demon,
                monsters.Slime, monsters.Wraith, monsters.WraithElite);

            var visualCatalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(
                Root + "/CharacterVisualCatalog_Demo.asset");
            MonsterContentGenerator.UpdateVisualCatalog(visualCatalog);

            const string expeditionPath = Root + "/Setups/ExpeditionSetup_Demo.asset";
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(expeditionPath);
            if (expedition == null)
            {
                expedition = ScriptableObject.CreateInstance<ExpeditionSetupSO>();
                AssetDatabase.CreateAsset(expedition, expeditionPath);
            }

            expedition.RunSeed = 0;
            expedition.TargetBattleCount = 3;
            expedition.RoutesPerVictory = 3;
            expedition.CombatEncounters.Clear();
            expedition.CombatEncounters.Add(setupClassic);
            expedition.CombatEncounters.Add(setupSlimeMix);
            expedition.CombatEncounters.Add(setupWraithPack);
            EditorUtility.SetDirty(expedition);

            ExpeditionArtBinder.BindExpeditionArtSilent();
            RelicArtBinder.BindRelicArtSilent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!showDialog)
                return;

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = setupClassic;
            EditorGUIUtility.PingObject(setupClassic);

            var assigned = TryAssignSetupToScene(setupClassic, expedition, showDialog: false);
            var assignHint = assigned
                ? "已自动绑定 Battle Setup 与 Expedition Setup 到场景中的 BattleDemo。"
                : "请在 BattleSandbox 场景选中 BattleDemo，拖入 Battle Setup 与 Expedition Setup。";

            EditorUtility.DisplayDialog(
                "Demo 数据已生成",
                "已在 Project 窗口创建/更新：\n\n" +
                "• Assets/_Project/Data/Cards/\n" +
                "• Assets/_Project/Data/Characters/\n" +
                "• Assets/_Project/Data/Setups/BattleSetup_Demo.asset\n" +
                "• Assets/_Project/Data/Setups/BattleSetup_Encounter_*.asset\n" +
                "• Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset\n" +
                "• Assets/_Project/Data/RelicVisualCatalog_Demo.asset\n\n" +
                "本场为 3 我方 vs 3 敌方（含哥布林/史莱姆/骷髅/幽灵及技能池）。\n" +
                "绑定 Expedition Setup 后 Play 即为三场连战 Demo。\n\n" +
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

            if (TryAssignSetupToScene(setup, null, showDialog: true))
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Grimhand/Content/Assign Demo Expedition Setup to Scene")]
        public static void AssignDemoExpeditionMenu()
        {
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(SetupPath);
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(Root + "/Setups/ExpeditionSetup_Demo.asset");
            if (expedition == null)
            {
                EditorUtility.DisplayDialog(
                    "未找到远征配置",
                    "请先执行：\nGrimhand → Content → Generate Demo ScriptableObjects",
                    "好的");
                return;
            }

            if (TryAssignSetupToScene(setup, expedition, showDialog: true))
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static bool TryAssignSetupToScene(BattleSetupSO setup, ExpeditionSetupSO expedition, bool showDialog)
        {
            var screenController = Object.FindAnyObjectByType<BattleScreenController>();
            var demoController = Object.FindAnyObjectByType<BattleDemoController>();
            if (screenController == null && demoController == null)
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

            var relicCatalog = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(
                Root + "/RelicVisualCatalog_Demo.asset");

            if (screenController != null)
            {
                var screenSo = new SerializedObject(screenController);
                AssignObjectReference(screenSo, "battleSetup", setup);
                AssignObjectReference(screenSo, "expeditionSetup", expedition);
                AssignObjectReference(screenSo, "relicVisualCatalog", relicCatalog);
                screenSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screenController);
            }

            if (demoController != null)
            {
                var demoSo = new SerializedObject(demoController);
                AssignObjectReference(demoSo, "battleSetup", setup);
                AssignObjectReference(demoSo, "expeditionSetup", expedition);
                demoSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(demoController);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "绑定成功",
                    "已将 Demo 配置绑定到 Battle Screen Controller。\n" +
                    "绑定 Expedition Setup 后 Play 即为三场连战。",
                    "好的");
            }

            return true;
        }

        static void AssignObjectReference(SerializedObject target, string propertyName, Object value)
        {
            if (target == null || value == null || string.IsNullOrEmpty(propertyName))
                return;

            var property = target.FindProperty(propertyName);
            if (property == null)
                return;

            property.objectReferenceValue = value;
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

        static EnemyCharacters CreateEnemyCharacters(EnemyCardSet cards)
        {
            return new EnemyCharacters
            {
                Brute = SaveCharacter("Character_Goblin_Brute", "char_goblin_brute", "哥布林", TeamSide.Enemy,
                    FormationSlot.Front, 1, 20, 4, 1, 5,
                    BuildDeck(Repeat(cards.Bite, 4), Repeat(cards.Scratch, 2), Repeat(cards.Lunge, 2))),
                Shaman = SaveCharacter("Character_Goblin_Shaman", "char_goblin_shaman", "骷髅兵", TeamSide.Enemy,
                    FormationSlot.Middle, 1, 25, 6, 3, 4,
                    BuildDeck(Repeat(cards.Hex, 4), Repeat(cards.Wither, 4), Repeat(cards.Bite, 2))),
                Archer = SaveCharacter("Character_Goblin_Archer", "char_goblin_archer", "幽灵", TeamSide.Enemy,
                    FormationSlot.Back, 1, 18, 7, 1, 7,
                    BuildDeck(Repeat(cards.Arrow, 6), Repeat(cards.Aim, 4)))
            };
        }

        struct EnemyCardSet
        {
            public CardDefinitionSO Bite, Scratch, Lunge, Hex, Wither, Arrow, Aim;
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

        static BattleSetupSO SaveBattleSetup(
            string assetName,
            CharacterDefinitionSO playerA,
            CharacterDefinitionSO playerB,
            CharacterDefinitionSO playerC,
            CharacterDefinitionSO enemyA,
            CharacterDefinitionSO enemyB,
            CharacterDefinitionSO enemyC)
        {
            var path = $"{Root}/Setups/{assetName}.asset";
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(path);
            if (setup == null)
            {
                setup = ScriptableObject.CreateInstance<BattleSetupSO>();
                AssetDatabase.CreateAsset(setup, path);
            }

            setup.Seed = 0;
            setup.Combatants.Clear();
            setup.Combatants.AddRange(new[]
            {
                playerA, playerB, playerC,
                enemyA, enemyB, enemyC
            });
            EditorUtility.SetDirty(setup);
            return setup;
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
