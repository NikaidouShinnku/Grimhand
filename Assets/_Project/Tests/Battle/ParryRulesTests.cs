using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ParryRulesTests
    {
        [Test]
        public void Parry_ArmsOnCardResolve_AndTriggersOnNextHit()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 50);
            state.Combatants.Add(knight);
            state.Combatants.Add(goblin);

            var parryCard = ParryCard();
            EffectActionExecutor.ExecuteAll(state, knight, parryCard, new List<BattleEvent>());
            Assert.NotNull(knight.ActiveParry);

            var events = new List<BattleEvent>();
            // 威力 22（面伤+攻）：受击 raw≈11，减伤 50%→6；反射威力 44，经站位后 raw≈22
            DamageRules.ApplyDamage(state, goblin, knight, 22, CardType.Attack, events);

            Assert.IsNull(knight.ActiveParry, "弹反触发后应消耗");
            Assert.AreEqual(34, knight.Hp, "11 伤害减 50% ≈ 6");
            Assert.AreEqual(28, goblin.Hp, "反射 22*200%=44 威力，前排对前排约 22 HP");
        }

        [Test]
        public void Parry_ClearAll_RemovesArmedStance()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            state.Combatants.Add(knight);
            ParryRules.Arm(knight, 50, 200, new List<BattleEvent>());
            ParryRules.ClearAll(state);
            Assert.IsNull(knight.ActiveParry);
        }

        static CombatantState Unit(string id, TeamSide team, FormationSlot slot, int hp)
        {
            return new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                Attack = 7
            };
        }

        static CardInstanceState ParryCard()
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "弹反",
                CardType = CardType.Defense
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReflectLastDamageToAttacker,
                Target = EffectTarget.LastActionActor,
                Value = 200,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            return card;
        }
    }
}
