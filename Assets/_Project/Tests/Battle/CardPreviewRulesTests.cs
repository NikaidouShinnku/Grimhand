using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CardPreviewRulesTests
    {
        [Test]
        public void ExpectedDamage_IncludesAttackerPositionOutgoing()
        {
            var state = BuildState();
            var owner = AddPlayer(state, FormationSlot.Back, attack: 0);
            var card = AttackCard(value: 10);

            var action = card.Actions[0];
            var preview = CardPreviewRules.ComputeExpectedDamage(state, owner, card, action);

            Assert.AreEqual(13, preview);
        }

        [Test]
        public void ExpectedDamage_IncludesRelicFlatAndPercentBonuses()
        {
            var state = BuildState();
            state.Config.RunModifiers.FirstAttackFlatBonus = 5;
            state.Config.RunModifiers.HighCostCardDamageBonusPercent = 15f;
            state.Config.RunModifiers.FirstPlayerAttackPending = true;

            var owner = AddPlayer(state, FormationSlot.Middle, attack: 4);
            var card = AttackCard(value: 6, cost: 3);

            var preview = CardPreviewRules.ComputeExpectedDamage(
                state, owner, card, card.Actions[0]);

            // (6+4)*1.15 high-cost → 12, +5 flat → 17, *1.15 middle → 20
            Assert.AreEqual(20, preview);
        }

        [Test]
        public void ExpectedDamage_DoesNotDependOnEnemySlot()
        {
            var state = BuildState();
            var owner = AddPlayer(state, FormationSlot.Middle, attack: 2);
            var card = AttackCard(value: 8);

            AddEnemy(state, FormationSlot.Front);
            var previewFront = CardPreviewRules.ComputeExpectedDamage(
                state, owner, card, card.Actions[0]);

            state.Combatants.RemoveAt(state.Combatants.Count - 1);
            AddEnemy(state, FormationSlot.Back);
            var previewBack = CardPreviewRules.ComputeExpectedDamage(
                state, owner, card, card.Actions[0]);

            Assert.AreEqual(previewFront, previewBack);
            Assert.AreEqual(12, previewFront);
        }

        static BattleState BuildState()
        {
            return new BattleState
            {
                Config = new BattleConfig
                {
                    RunModifiers = RunModifierSnapshot.Empty
                }
            };
        }

        static CombatantState AddPlayer(BattleState state, FormationSlot slot, int attack)
        {
            var c = new CombatantState
            {
                Id = $"player_{slot}",
                Team = TeamSide.Player,
                Slot = slot,
                Attack = attack,
                BaseAttack = attack,
                Hp = 20,
                MaxHp = 20
            };
            state.Combatants.Add(c);
            return c;
        }

        [Test]
        public void PreviewHpDamage_SubtractsBlockFromFlatDamage()
        {
            var state = BuildState();
            var owner = AddPlayer(state, FormationSlot.Middle, attack: 0);
            var enemy = AddEnemy(state, FormationSlot.Back, block: 2);
            var card = AttackCard(value: 10, scaleWithAttack: false);

            var preview = CardPreviewRules.PreviewHpDamageAgainstTarget(
                state, owner, card, card.Actions[0], enemy);

            Assert.AreEqual(8, preview);
        }

        [Test]
        public void PreviewHpDamage_BaseExpectedDamageIgnoresTargetDefense()
        {
            var state = BuildState();
            var owner = AddPlayer(state, FormationSlot.Middle, attack: 0);
            AddEnemy(state, FormationSlot.Back, block: 2);
            var card = AttackCard(value: 10, scaleWithAttack: false);

            var basePreview = CardPreviewRules.ComputeExpectedDamage(
                state, owner, card, card.Actions[0]);
            Assert.AreEqual(10, basePreview);
        }

        [Test]
        public void PreviewHpDamage_EnemyAttackingPlayer_IsNonZero()
        {
            var state = BuildState();
            var goblin = AddEnemy(state, FormationSlot.Front, attack: 4);
            var player = AddPlayer(state, FormationSlot.Middle, attack: 0);
            var card = AttackCard(value: 7);

            var preview = CardPreviewRules.PreviewHpDamageAgainstTarget(
                state, goblin, card, card.Actions[0], player);

            Assert.Greater(preview, 0);
        }

        static CombatantState AddEnemy(BattleState state, FormationSlot slot, int block = 0, int attack = 0)
        {
            var enemy = new CombatantState
            {
                Id = $"enemy_{slot}",
                Team = TeamSide.Enemy,
                Slot = slot,
                Attack = attack,
                BaseAttack = attack,
                Hp = 20,
                MaxHp = 20,
                Block = block
            };
            state.Combatants.Add(enemy);
            return enemy;
        }

        static CardInstanceState AttackCard(int value, int cost = 1, bool scaleWithAttack = true)
        {
            return new CardInstanceState
            {
                CardType = CardType.Attack,
                Cost = cost,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Value = value,
                        ScaleWithAttack = scaleWithAttack
                    }
                }
            };
        }
    }
}
