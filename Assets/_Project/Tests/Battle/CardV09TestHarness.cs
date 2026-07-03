using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Battle.Tests
{
    /// <summary>从 Card_*.asset 加载定义，供 v0.9 逐卡行为测试使用。</summary>
    public static class CardV09TestHarness
    {
        public const string CardsRoot = "Assets/_Project/Data/Cards";

        public static CardDefinitionSO LoadDefinition(string cardId)
        {
#if UNITY_EDITOR
            var path = $"{CardsRoot}/Card_{cardId}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
            Assert.IsNotNull(def, $"缺少 CardDefinitionSO: {path}");
            return def;
#else
            Assert.Ignore("CardV09TestHarness 需要 Unity Editor AssetDatabase");
            return null;
#endif
        }

        public static CardInstanceState Instantiate(CardDefinitionSO def, int instanceId = 9001)
        {
            var template = def.ToTemplate();
            var card = new CardInstanceState
            {
                InstanceId = instanceId,
                DefinitionId = template.DefinitionId,
                DisplayName = template.DisplayName,
                OwnerCharacterId = template.OwnerCharacterId,
                Cost = template.Cost,
                CardType = template.CardType
            };
            foreach (var kw in template.Keywords)
                card.Keywords.Add(kw);
            foreach (var action in template.Actions)
                card.Actions.Add(EffectActionSpec.Clone(action));
            return card;
        }

        public static BattleState EmptyState()
        {
            return new BattleState
            {
                Config = new BattleConfig { HandLimit = 10 }
            };
        }

        public static CombatantState AddCombatant(
            BattleState state,
            string id,
            TeamSide team,
            FormationSlot slot,
            int hp = 40,
            int atk = 5,
            int def = 0,
            string charId = "char_knight")
        {
            var c = new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = charId,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                BaseAttack = atk,
                BaseDefense = def,
                Attack = atk,
                Defense = def,
                Speed = 5
            };
            state.Combatants.Add(c);
            return c;
        }
    }
}
