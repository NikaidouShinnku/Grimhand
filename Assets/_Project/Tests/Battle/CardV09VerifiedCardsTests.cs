using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    /// <summary>v0.9 逐卡行为测试：每张卡一个 Test，跑绿后写入 _card_behavior_verified.json。</summary>
    public class CardV09VerifiedCardsTests
    {
        [Test]
        public void w_basic_slash_Deals8RequiresFrontMidPick()
        {
            var def = CardV09TestHarness.LoadDefinition("w_basic_slash");
            var state = CardV09TestHarness.EmptyState();
            var warrior = CardV09TestHarness.AddCombatant(state, "warrior", TeamSide.Player, FormationSlot.Front);
            var goblin = CardV09TestHarness.AddCombatant(state, "goblin", TeamSide.Enemy, FormationSlot.Front, hp: 30, def: 0);
            CardV09TestHarness.AddCombatant(state, "skel", TeamSide.Enemy, FormationSlot.Back, hp: 30, def: 0);

            var card = CardV09TestHarness.Instantiate(def);
            Assert.IsTrue(CardRules.ShouldPromptForTarget(state, card, warrior));

            state.ResolutionTargets[card.InstanceId] = "goblin";
            var hpBefore = goblin.Hp;
            EffectActionExecutor.ExecuteAll(state, warrior, card, new List<BattleEvent>());
            Assert.AreEqual(hpBefore - 8, goblin.Hp);
        }

        [Test]
        public void w_shield_block_Grants6Block()
        {
            var def = CardV09TestHarness.LoadDefinition("w_shield_block");
            var state = CardV09TestHarness.EmptyState();
            var warrior = CardV09TestHarness.AddCombatant(state, "warrior", TeamSide.Player, FormationSlot.Front, def: 2);
            var card = CardV09TestHarness.Instantiate(def);

            EffectActionExecutor.ExecuteAll(state, warrior, card, new List<BattleEvent>());
            Assert.GreaterOrEqual(warrior.Block, 10);
        }

        [Test]
        public void w_first_strike_Deals3WithReach()
        {
            var def = CardV09TestHarness.LoadDefinition("w_first_strike");
            var state = CardV09TestHarness.EmptyState();
            var warrior = CardV09TestHarness.AddCombatant(state, "warrior", TeamSide.Player, FormationSlot.Front);
            var goblin = CardV09TestHarness.AddCombatant(state, "goblin", TeamSide.Enemy, FormationSlot.Front, hp: 40, def: 0);
            var card = CardV09TestHarness.Instantiate(def);

            Assert.IsTrue(CardRules.ShouldPromptForTarget(state, card, warrior));
            state.ResolutionTargets[card.InstanceId] = "goblin";
            EffectActionExecutor.ExecuteAll(state, warrior, card, new List<BattleEvent>());
            Assert.AreEqual(37, goblin.Hp);
        }
    }
}
