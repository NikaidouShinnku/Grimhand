using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class BossMechanicsTests
    {
        [Test]
        public void FindSlotBehindSummoner_PrefersBehindKing()
        {
            var state = new BattleState();
            var king = new CombatantState
            {
                Id = "king",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 100,
                MaxHp = 100
            };
            state.Combatants.Add(king);

            Assert.AreEqual(FormationSlot.Middle, SummonRules.FindSlotBehindSummoner(state, king));

            state.Combatants.Add(new CombatantState
            {
                Id = "skull1",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                Hp = 50,
                MaxHp = 50
            });

            Assert.AreEqual(FormationSlot.Back, SummonRules.FindSlotBehindSummoner(state, king));
        }

        [Test]
        public void FindNextSummonSlot_PrefersFrontThenMiddleThenBack()
        {
            var state = new BattleState();
            state.Combatants.Add(new CombatantState
            {
                Id = "king",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 100,
                MaxHp = 100
            });

            Assert.AreEqual(FormationSlot.Middle, SummonRules.FindNextEnemySummonSlot(state, TeamSide.Enemy));

            state.Combatants.Add(new CombatantState
            {
                Id = "skull1",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                Hp = 50,
                MaxHp = 50
            });

            Assert.AreEqual(FormationSlot.Back, SummonRules.FindNextEnemySummonSlot(state, TeamSide.Enemy));

            state.Combatants.Add(new CombatantState
            {
                Id = "skull2",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                Hp = 50,
                MaxHp = 50
            });

            Assert.IsNull(SummonRules.FindNextEnemySummonSlot(state, TeamSide.Enemy));
        }

        [Test]
        public void BossFirstHitBlock_AppliesOncePerTurn()
        {
            var state = new BattleState();
            var boss = new CombatantState
            {
                Id = "king",
                DisplayName = "骷髅王",
                Team = TeamSide.Enemy,
                Hp = 100,
                MaxHp = 100,
                BossFirstHitBlockPending = true
            };
            boss.Traits.Add(CharacterTraitCatalog.BossFirstHitBlock);
            state.Combatants.Add(boss);

            var events = new List<BattleEvent>();
            BossTraitRules.TryApplyFirstHitBlock(state, boss, events);
            Assert.AreEqual(10, boss.Block);
            Assert.IsFalse(boss.BossFirstHitBlockPending);

            BossTraitRules.TryApplyFirstHitBlock(state, boss, events);
            Assert.AreEqual(10, boss.Block);
        }

        [Test]
        public void BoneWorkshop_SpawnsExplosiveSkullWhenSlotAvailable()
        {
            var state = BuildBossBattleState();
            var king = state.GetCombatant("king");
            var events = new List<BattleEvent>();
            StatusRules.ApplyStatus(state, king, StatusCatalog.BoneWorkshop, 1, -1, events);

            var skullCount = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == SummonRules.ExplosiveSkullCharacterId && unit.IsAlive)
                    skullCount++;
            }

            Assert.AreEqual(1, skullCount, "获得骨之王座时应立即召唤");
            Assert.AreEqual(FormationSlot.Middle, state.Combatants[1].Slot);

            Assert.IsTrue(SummonRules.TrySummonExplosiveSkull(state, king, events));
            skullCount = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == SummonRules.ExplosiveSkullCharacterId && unit.IsAlive)
                    skullCount++;
            }

            Assert.AreEqual(2, skullCount);
            Assert.AreEqual(FormationSlot.Back, state.Combatants[2].Slot);
        }

        [Test]
        public void SkullSelfDestructHand_GrantsBonusCardWithoutDuplicate()
        {
            var state = BuildBossBattleState();
            var events = new List<BattleEvent>();
            var king = state.GetCombatant("king");
            StatusRules.ApplyStatus(state, king, StatusCatalog.BoneWorkshop, 1, -1, events);

            SummonRules.TrySummonExplosiveSkull(state, king, events);
            SummonRules.GrantSkullSelfDestructHands(state, events);
            var firstCount = state.EnemyHand.Count;

            SummonRules.GrantSkullSelfDestructHands(state, events);
            Assert.AreEqual(firstCount, state.EnemyHand.Count);
            Assert.IsTrue(state.EnemyHand[0].IsBonusHandCard);
        }

        [Test]
        public void CombatantDeath_PollutesOnlyBoundSkullCards()
        {
            var state = BuildBossBattleState();
            var events = new List<BattleEvent>();
            var template = state.Config.SummonTemplates[SummonRules.ExplosiveSkullCharacterId];

            SummonRules.SpawnFromTemplate(state, template, FormationSlot.Middle, events);
            SummonRules.SpawnFromTemplate(state, template, FormationSlot.Back, events);

            var skullA = state.Combatants[1];
            var skullB = state.Combatants[2];
            var cardA = SummonRules.CreateBoundCard(state, BuildExplodeTemplate(), skullA.Id);
            var cardB = SummonRules.CreateBoundCard(state, BuildExplodeTemplate(), skullB.Id);

            CombatantDeathRules.OnCharacterDied(state, skullA, events);

            Assert.IsFalse(cardA.IsUsable);
            Assert.IsTrue(cardB.IsUsable);
        }

        static BattleState BuildBossBattleState()
        {
            var config = new BattleConfig();
            var template = new CombatantConfig
            {
                CharacterDefinitionId = SummonRules.ExplosiveSkullCharacterId,
                DisplayName = "易爆骷髅头",
                Team = TeamSide.Enemy,
                MaxHp = 20,
                BaseAttack = 0,
                BaseDefense = 5,
                Speed = 2
            };
            template.Traits.Add(CharacterTraitCatalog.SkullSelfDestructHand);
            template.DeckTemplates.Add(BuildExplodeTemplate());
            config.SummonTemplates[SummonRules.ExplosiveSkullCharacterId] = template;

            var state = new BattleState { Config = config };
            state.Combatants.Add(new CombatantState
            {
                Id = "king",
                DisplayName = "骷髅王",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 800,
                MaxHp = 800,
                BaseDefense = 10,
                Defense = 10
            });
            return state;
        }

        static CardTemplate BuildExplodeTemplate() =>
            CardTemplate.Create(
                CharacterTraitCatalog.SkullExplodeCardId,
                "骷髅自爆",
                SummonRules.ExplosiveSkullCharacterId,
                0,
                CardType.Attack,
                new EffectActionSpec
                {
                    Type = EffectActionType.DealDamage,
                    Target = EffectTarget.RandomEnemy,
                    Value = 24
                });
    }
}
