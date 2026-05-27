#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class GrimhandContentMenu
    {
        const string Root = "Assets/_Project/Data";

        [MenuItem("Grimhand/Content/Generate Demo ScriptableObjects")]
        public static void GenerateDemoAssets()
        {
            EnsureFolder(Root + "/Cards");
            EnsureFolder(Root + "/Characters");
            EnsureFolder(Root + "/Setups");

            var strike = SaveCard("k_strike", "重击", "char_knight", 1, CardType.Attack,
                Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 8, scaleAttack: true));
            var slash = SaveCard("k_slash", "斩击", "char_knight", 2, CardType.Attack,
                Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 14, scaleAttack: true));
            var parry = SaveCard("k_parry", "弹反", "char_knight", 2, CardType.Defense,
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
                });
            var poison = SaveCard("m_poison", "毒云", "char_mage", 2, CardType.Status,
                Action(EffectActionType.ApplyStatus, EffectTarget.DefaultEnemy, 0, statusId: StatusCatalog.Poison, stacks: 10));
            var snipe = SaveCard("r_snipe", "狙击", "char_ranger", 2, CardType.Attack,
                Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 15, scaleAttack: true));
            var slow = SaveCard("r_slow", "缚足", "char_ranger", 1, CardType.Status,
                Action(EffectActionType.ApplyStatus, EffectTarget.EnemyBackSlot, 0, statusId: StatusCatalog.Slow, stacks: 1, duration: 2));
            var bite = SaveCard("g_bite", "撕咬", "char_goblin", 1, CardType.Attack,
                Action(EffectActionType.DealDamage, EffectTarget.DefaultEnemy, 6, scaleAttack: true));

            var knight = SaveCharacter("Character_Knight", "char_knight", "骑士", TeamSide.Player,
                FormationSlot.Front, 2, 40, 6, 4, 10, BuildDeck(Repeat(strike, 4), Repeat(slash, 2), Of(parry, parry)));
            var mage = SaveCharacter("Character_Mage", "char_mage", "法师", TeamSide.Player,
                FormationSlot.Middle, 1, 28, 5, 2, 5, BuildDeck(Repeat(strike, 6), Of(poison, poison)));
            var ranger = SaveCharacter("Character_Ranger", "char_ranger", "游侠", TeamSide.Player,
                FormationSlot.Back, 1, 30, 7, 2, 7, BuildDeck(Repeat(snipe, 4), Of(slow, slow)));
            var goblin = SaveCharacter("Character_Goblin", "char_goblin", "哥布林", TeamSide.Enemy,
                FormationSlot.Front, 2, 50, 7, 1, 8, BuildDeck(Repeat(bite, 10)));

            var setup = ScriptableObject.CreateInstance<BattleSetupSO>();
            setup.Seed = 42;
            setup.Combatants.AddRange(new[] { knight, mage, ranger, goblin });
            AssetDatabase.CreateAsset(setup, Root + "/Setups/BattleSetup_Demo.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Demo ScriptableObjects generated under Assets/_Project/Data/");
        }

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            params EffectActionDefinition[] actions)
        {
            var card = ScriptableObject.CreateInstance<CardDefinitionSO>();
            card.CardId = id;
            card.DisplayName = displayName;
            card.OwnerCharacterId = owner;
            card.Cost = cost;
            card.CardType = cardType;
            card.Actions.AddRange(actions);
            var path = $"{Root}/Cards/Card_{id}.asset";
            AssetDatabase.CreateAsset(card, path);
            return card;
        }

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
            var character = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
            character.CharacterId = charId;
            character.DisplayName = displayName;
            character.Team = team;
            character.Slot = slot;
            character.Level = level;
            character.MaxHp = hp;
            character.BaseAttack = atk;
            character.BaseDefense = def;
            character.Speed = spd;
            character.Deck.AddRange(deck);
            var path = $"{Root}/Characters/{assetName}.asset";
            AssetDatabase.CreateAsset(character, path);
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
            int duration = -1)
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
                Duration = duration
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
