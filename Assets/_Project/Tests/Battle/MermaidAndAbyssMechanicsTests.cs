using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class MermaidAndAbyssMechanicsTests
    {
        [Test]
        public void TidalPower_AppliesAttackUpAndCostCut()
        {
            var state = BuildState(out var mermaid, out var warrior);
            var template = new CardTemplate
            {
                DefinitionId = AbyssMonsterCardCatalog.TidalPowerCardId,
                DisplayName = "潮汐之力"
            };
            Assert.IsTrue(AbyssMonsterCardCatalog.TryApplyCanonical(template));
            Assert.AreEqual(2, template.Actions.Count);

            var card = ToInstance(template, mermaid.Id);
            var events = new List<BattleEvent>();
            EffectActionExecutor.ExecuteUnconditionalActions(state, mermaid, card, events);

            Assert.AreEqual(30, StatusRules.GetStatusStacks(mermaid, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(3, StatusRules.FindStatus(mermaid, StatusCatalog.AttackUpPercent)?.RemainingTurns);
            Assert.IsTrue(StatusRules.HasStatus(mermaid, StatusCatalog.MermaidTidalCostCut));
            Assert.AreEqual(2, StatusRules.FindStatus(mermaid, StatusCatalog.MermaidTidalCostCut)?.RemainingTurns);

            var slash = new CardInstanceState
            {
                InstanceId = 2,
                DefinitionId = AbyssMonsterCardCatalog.MermaidSlashCardId,
                Cost = 1,
                BaseCost = 1,
                OwnerCombatantId = mermaid.Id
            };
            Assert.AreEqual(0, MinionTraitRules.GetAdjustedCardCost(state, mermaid, slash));
        }

        [Test]
        public void TidalCostCut_StacksAndExpiresIndependently()
        {
            var state = BuildState(out var mermaid, out _);
            var events = new List<BattleEvent>();

            StatusRules.ApplyStatus(state, mermaid, StatusCatalog.MermaidTidalCostCut, 1, 2, events);
            Assert.AreEqual(1, StatusRules.GetStatusStacks(mermaid, StatusCatalog.MermaidTidalCostCut));

            // 模拟过了 1 回合：第一层剩余 1
            foreach (var s in mermaid.Statuses)
            {
                if (s.StatusId == StatusCatalog.MermaidTidalCostCut)
                    s.RemainingTurns = 1;
            }

            StatusRules.ApplyStatus(state, mermaid, StatusCatalog.MermaidTidalCostCut, 1, 2, events);
            Assert.AreEqual(2, StatusRules.GetStatusStacks(mermaid, StatusCatalog.MermaidTidalCostCut),
                "两次潮汐之力应叠成 2 层减耗");

            var wave = new CardInstanceState
            {
                InstanceId = 3,
                DefinitionId = AbyssMonsterCardCatalog.WaveCleaveCardId,
                Cost = 2,
                BaseCost = 2,
                OwnerCombatantId = mermaid.Id
            };
            Assert.AreEqual(0, MinionTraitRules.GetAdjustedCardCost(state, mermaid, wave),
                "2 层减耗使破浪斩 2→0，可触发人鱼被动");

            // 到期一层（剩余 1 的那桶）
            for (var i = mermaid.Statuses.Count - 1; i >= 0; i--)
            {
                var s = mermaid.Statuses[i];
                if (s.StatusId == StatusCatalog.MermaidTidalCostCut && s.RemainingTurns == 1)
                    mermaid.Statuses.RemoveAt(i);
            }

            Assert.AreEqual(1, StatusRules.GetStatusStacks(mermaid, StatusCatalog.MermaidTidalCostCut));
            Assert.AreEqual(1, MinionTraitRules.GetAdjustedCardCost(state, mermaid, wave),
                "第一层到期后破浪斩应为 1 费");
        }

        [Test]
        public void MermaidPassive_TriggersOnZeroCostAndDoesNotDoubleDip()
        {
            var state = BuildState(out var mermaid, out _);
            mermaid.Traits.Add(MinionTraitCatalog.MermaidZeroCostAttack);
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "free_card",
                Cost = 0,
                BaseCost = 0,
                CardType = CardType.Attack,
                OwnerCombatantId = mermaid.Id
            };

            MinionTraitRules.OnCardResolved(state, mermaid, card, new List<BattleEvent>());
            Assert.AreEqual(5, mermaid.MermaidZeroCostAttackBonusPercent);

            CombatModifierRules.RefreshCombatantModifiers(state, mermaid, null);
            Assert.AreEqual(5, mermaid.OutgoingDamagePercentBonus);

            // ApplyMinionOutgoingAttackBonus 不应再乘一次
            var power = MinionTraitRules.ApplyMinionOutgoingAttackBonus(
                state, mermaid, null, CardType.Attack, 100);
            Assert.AreEqual(100, power);
        }

        [Test]
        public void AbyssGaze_AppliesAoeDefenseDownPercent()
        {
            var state = BuildState(out var abyss, out var warrior);
            abyss.CharacterDefinitionId = MinionTraitCatalog.AbyssCreatureCharacterId;
            var ally = new CombatantState
            {
                Id = "mage",
                DisplayName = "法师",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                Hp = 40,
                MaxHp = 40
            };
            state.Combatants.Add(ally);

            var template = new CardTemplate { DefinitionId = AbyssMonsterCardCatalog.AbyssCreatureGazeCardId };
            Assert.IsTrue(AbyssMonsterCardCatalog.TryApplyCanonical(template));
            var card = ToInstance(template, abyss.Id);
            EffectActionExecutor.ExecuteUnconditionalActions(state, abyss, card, new List<BattleEvent>());

            Assert.AreEqual(50, StatusRules.GetStatusStacks(warrior, StatusCatalog.DefenseDownPercent));
            Assert.AreEqual(50, StatusRules.GetStatusStacks(ally, StatusCatalog.DefenseDownPercent));
            Assert.AreEqual(2, StatusRules.FindStatus(warrior, StatusCatalog.DefenseDownPercent)?.RemainingTurns);
        }

        [Test]
        public void AbyssPoisonPassive_EmitsDamageBeforePoisonEvents()
        {
            var state = BuildState(out var abyss, out var warrior);
            abyss.CharacterDefinitionId = MinionTraitCatalog.AbyssCreatureCharacterId;
            abyss.Traits.Add(MinionTraitCatalog.AbyssCreaturePoisonOnDamage);
            warrior.Block = 0;

            var events = new List<BattleEvent>();
            DamageRules.ApplyDamage(
                state, abyss, warrior, 10, CardType.Attack, events, canTriggerParry: false);

            var damageIdx = events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied);
            var poisonIdx = events.FindIndex(e =>
                e.Kind == BattleEventKind.StatusApplied && e.TargetId == StatusCatalog.Poison);
            Assert.GreaterOrEqual(damageIdx, 0);
            Assert.GreaterOrEqual(poisonIdx, 0);
            Assert.Less(damageIdx, poisonIdx, "应先 DamageApplied 再 StatusApplied(中毒)");
            Assert.AreEqual(5, StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison));
        }

        [Test]
        public void PiercingTentacle_DealsTrueDamagePerPoisonStack()
        {
            var state = BuildState(out var abyss, out var warrior);
            abyss.CharacterDefinitionId = MinionTraitCatalog.AbyssCreatureCharacterId;
            abyss.Traits.Add(MinionTraitCatalog.AbyssCreaturePoisonOnDamage);
            warrior.Hp = 100;
            warrior.MaxHp = 100;
            warrior.Block = 20;
            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Poison, 7, -1, new List<BattleEvent>());

            var template = new CardTemplate { DefinitionId = AbyssMonsterCardCatalog.PiercingTentacleCardId };
            Assert.IsTrue(AbyssMonsterCardCatalog.TryApplyCanonical(template));
            var card = ToInstance(template, abyss.Id);
            state.ResolutionTargets[card.InstanceId] = warrior.Id;

            var events = new List<BattleEvent>();
            var hpBefore = warrior.Hp;
            EffectActionExecutor.ExecuteUnconditionalActions(state, abyss, card, events);

            // 17 普通伤打在护甲上（20→3），HP 不掉；真实伤 7 直扣 HP
            // 被动再叠 5 毒（因护甲吃掉了主伤害？若 hpDamage=0 则不上毒）
            // 17 <= 20 block → hpDamage=0 → 不上毒；真实伤仍按命中前 7 层
            Assert.AreEqual(hpBefore - 7, warrior.Hp, "真实伤害应按命中前中毒层数直扣 HP");
            Assert.AreEqual(3, warrior.Block);
        }

        static CardInstanceState ToInstance(CardTemplate template, string ownerId)
        {
            var card = new CardInstanceState
            {
                InstanceId = 99,
                DefinitionId = template.DefinitionId,
                DisplayName = template.DisplayName,
                Cost = template.Cost,
                BaseCost = template.Cost,
                CardType = template.CardType,
                OwnerCombatantId = ownerId,
                OwnerCharacterId = template.OwnerCharacterId
            };
            foreach (var kw in template.Keywords)
                card.Keywords.Add(kw);
            foreach (var action in template.Actions)
                card.Actions.Add(EffectActionSpec.Clone(action));
            return card;
        }

        static BattleState BuildState(out CombatantState enemy, out CombatantState warrior)
        {
            var state = new BattleState { Config = new BattleConfig() };
            enemy = new CombatantState
            {
                Id = "enemy",
                DisplayName = "敌人",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_mermaid_warrior",
                Hp = 100,
                MaxHp = 100,
                BaseAttack = 14,
                Speed = 7
            };
            warrior = new CombatantState
            {
                Id = "warrior",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Hp = 50,
                MaxHp = 50,
                Speed = 5
            };
            state.Combatants.Add(enemy);
            state.Combatants.Add(warrior);
            return state;
        }
    }
}
