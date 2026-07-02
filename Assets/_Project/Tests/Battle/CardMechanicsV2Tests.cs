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
    public class CardMechanicsV2Tests
    {
        [Test]
        public void Taunt_ForcesDefaultTarget()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, hp: 40);
            AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, hp: 30);
            AddUnit(state, "slime", TeamSide.Enemy, FormationSlot.Front, hp: 20);

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Taunt, 1, 1, new List<BattleEvent>());

            var target = PositionRules.PickDefaultTarget(state, TeamSide.Enemy);
            Assert.AreEqual("warrior", target.Id);
        }

        [Test]
        public void Guard_RedirectsAllyDamageWithReduction()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, hp: 40, def: 0);
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, hp: 30, def: 0);
            var goblin = AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front, atk: 8, def: 0);

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Guard, 1, 1, new List<BattleEvent>());
            var events = new List<BattleEvent>();

            DamageRules.ApplyDamage(state, goblin, mage, 10, CardType.Attack, events);

            Assert.AreEqual(30, mage.Hp);
            Assert.Less(warrior.Hp, 40);
            Assert.AreEqual(35, warrior.Hp);
        }

        [Test]
        public void PowerCleave_BonusWhenTargetBelowHalfHp()
        {
            var actionLow = ActionAtk(5, 120, bonusHpBelowPercent: 50, bonusHpBelowFlat: 10);
            var actionHigh = ActionAtk(5, 120, bonusHpBelowPercent: 50, bonusHpBelowFlat: 10);
            var lowTarget = new CombatantState { Hp = 4, MaxHp = 20 };
            var highTarget = new CombatantState { Hp = 12, MaxHp = 20 };

            Assert.AreEqual(15, CombatMechanicsRules.ComputeConditionalDamageBonus(null, actionLow, lowTarget, 5));
            Assert.AreEqual(5, CombatMechanicsRules.ComputeConditionalDamageBonus(null, actionHigh, highTarget, 5));
        }

        [Test]
        public void FatalStrike_BonusWhenTargetAlreadyHit()
        {
            var action = ActionAtk(6, 180, bonusHitThisTurnPercent: 50);
            var target = new CombatantState { HitThisTurn = true };

            Assert.AreEqual(9, CombatMechanicsRules.ComputeConditionalDamageBonus(null, action, target, 6));
        }

        [Test]
        public void LifeSteal_HealsAttacker()
        {
            var state = BuildState();
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, hp: 20, atk: 6);
            var enemy = AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, hp: 30, def: 0);
            var card = CardWith(ActionAtk(4, 100, lifestealPercent: 50));
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, mage, card, events);

            Assert.Less(enemy.Hp, 30);
            Assert.Greater(mage.Hp, 20);
        }

        [Test]
        public void ReviveBlessing_PreventsDeathOnce()
        {
            var state = BuildState();
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, hp: 40, maxHp: 40);
            var ally = AddUnit(state, "ally", TeamSide.Player, FormationSlot.Front, hp: 10, maxHp: 40);
            var enemy = AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, atk: 20);

            StatusRules.ApplyStatus(state, ally, StatusCatalog.ReviveBlessing, 1, -1, new List<BattleEvent>());
            var events = new List<BattleEvent>();

            DamageRules.ApplyDamage(state, enemy, ally, 20, CardType.Attack, events);

            Assert.IsTrue(ally.IsAlive);
            Assert.AreEqual(10, ally.Hp);
            Assert.IsFalse(StatusRules.HasStatus(ally, StatusCatalog.ReviveBlessing));
        }

        [Test]
        public void Unyielding_TriggersBelowQuarterHp()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, hp: 10, maxHp: 40);
            var enemy = AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, atk: 5);

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Unyielding, 1, -1, new List<BattleEvent>());
            var events = new List<BattleEvent>();

            DamageRules.ApplyDamage(state, enemy, warrior, 5, CardType.Attack, events);

            Assert.AreEqual(25, warrior.Hp);
            Assert.IsFalse(StatusRules.HasStatus(warrior, StatusCatalog.Unyielding));
        }

        [Test]
        public void AttackDown_ReducesOutgoingDamage()
        {
            var state = BuildState();
            var demon = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Back, atk: 9);
            StatusRules.ApplyStatus(state, demon, StatusCatalog.AttackDown, 3, 2, new List<BattleEvent>());

            CombatantRules.RefreshDerivedStats(demon);
            Assert.AreEqual(3, demon.OutgoingDamageReductionFlat);
        }

        [Test]
        public void WarCry_GrantsTeamAttackUpToAllSlots()
        {
            var state = BuildState();
            var front = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, atk: 8);
            var middle = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, atk: 6);
            var back = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Back, atk: 9);
            var card = CardWith(
                StatusAction(StatusCatalog.AttackUpPercent, 10, 1, EffectTarget.AllyFrontSlot,
                    reach: TargetReach.FrontAndMiddle),
                StatusAction(StatusCatalog.AttackUpPercent, 10, 1, EffectTarget.AllyMiddleSlot,
                    reach: TargetReach.FrontAndMiddle),
                StatusAction(StatusCatalog.AttackUpPercent, 10, 1, EffectTarget.AllyBackSlot,
                    reach: TargetReach.FrontAndMiddle));
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, front, card, events);

            Assert.IsTrue(StatusRules.HasStatus(front, StatusCatalog.AttackUpPercent));
            Assert.IsTrue(StatusRules.HasStatus(middle, StatusCatalog.AttackUpPercent));
            Assert.IsTrue(StatusRules.HasStatus(back, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void WarCry_GrantsTeamAttackUp()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, atk: 8);
            var card = CardWith(
                StatusAction(StatusCatalog.AttackUp, 3, 1, EffectTarget.AllyFrontSlot),
                StatusAction(StatusCatalog.AttackUp, 3, 1, EffectTarget.AllyMiddleSlot),
                StatusAction(StatusCatalog.AttackUp, 3, 1, EffectTarget.AllyBackSlot));
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, warrior, card, events);

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.AttackUp));
            CombatModifierRules.RefreshCombatantModifiers(state, warrior, null);
            Assert.AreEqual(3, warrior.OutgoingDamageFlatBonus);
        }

        [Test]
        public void Damage_AppliesIncomingReductionPercent()
        {
            Assert.AreEqual(4, CombatMechanicsRules.ComputeHpDamageAfterDefense(10, 60));
            Assert.AreEqual(1, CombatMechanicsRules.ComputeHpDamageAfterDefense(3, 60));
        }

        [Test]
        public void Charge_IgnoresHalfDamageReduction()
        {
            var state = BuildState();
            var target = AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, def: 6);
            StatusRules.ApplyStatus(state, target, StatusCatalog.DamageReduction, 6, 1, new List<BattleEvent>());

            Assert.AreEqual(6, CombatMechanicsRules.GetEffectiveDefense(state, target, 0));
            Assert.AreEqual(3, CombatMechanicsRules.GetEffectiveDefense(state, target, 50));
        }

        [Test]
        public void Exhaust_RemovesCardFromBattle()
        {
            var state = BuildState();
            var card = new CardInstanceState { InstanceId = 1, IsUsable = true };
            state.PlayerHand.Add(card);
            state.PlayerDrawPile.Add(card);
            var events = new List<BattleEvent>();

            DeckRules.ExhaustCard(state, TeamSide.Player, card, events);

            Assert.IsFalse(card.IsUsable);
            Assert.IsFalse(state.PlayerHand.Contains(card));
            Assert.IsFalse(state.PlayerDrawPile.Contains(card));
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

        static CombatantState AddUnit(
            BattleState state,
            string id,
            TeamSide team,
            FormationSlot slot,
            int hp = 30,
            int maxHp = 30,
            int atk = 5,
            int def = 2,
            int speed = 5)
        {
            var unit = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = maxHp,
                BaseAttack = atk,
                BaseDefense = def,
                Attack = atk,
                Defense = def,
                Speed = speed
            };
            state.Combatants.Add(unit);
            return unit;
        }

        [Test]
        public void AoE_HitsEveryEnemyEvenWhenFrontDiesFirst()
        {
            var state = BuildState();
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle, atk: 6);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front, hp: 5, maxHp: 20, def: 0);
            AddUnit(state, "skeleton", TeamSide.Enemy, FormationSlot.Middle, hp: 25, maxHp: 25, def: 0);
            AddUnit(state, "ghost", TeamSide.Enemy, FormationSlot.Back, hp: 18, maxHp: 18, def: 0);

            var card = CardWith(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 20,
                ScaleWithAttack = false
            });

            EffectActionExecutor.ExecuteAll(state, mage, card, new List<BattleEvent>());

            Assert.AreEqual(0, state.GetCombatant("goblin").Hp);
            Assert.AreEqual(5, state.GetCombatant("skeleton").Hp);
            Assert.AreEqual(0, state.GetCombatant("ghost").Hp);
        }

        [Test]
        public void HellFire_SacrificeThenHitsAllEnemies()
        {
            var state = BuildState();
            var demon = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Back, atk: 9, hp: 30, maxHp: 30);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front, hp: 20, maxHp: 20, def: 0);
            AddUnit(state, "slime", TeamSide.Enemy, FormationSlot.Middle, hp: 30, maxHp: 30, def: 0);
            AddUnit(state, "skel", TeamSide.Enemy, FormationSlot.Back, hp: 25, maxHp: 25, def: 0);

            var card = new CardInstanceState
            {
                InstanceId = 99,
                CardType = CardType.Attack
            };
            card.Keywords.Add("sacrifice");
            card.Keywords.Add("aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = 8
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 5,
                ScaleWithAttack = true,
                AttackScalePercent = 100
            });

            EffectActionExecutor.ExecuteAll(state, demon, card, new List<BattleEvent>());

            Assert.AreEqual(22, demon.Hp);
            Assert.Less(state.GetCombatant("goblin").Hp, 20);
            Assert.Less(state.GetCombatant("slime").Hp, 30);
            Assert.Less(state.GetCombatant("skel").Hp, 25);
        }

        [Test]
        public void SplashBehind_HitsBehindEvenWhenPrimaryDies()
        {
            var state = BuildState();
            var demon = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Middle, atk: 0);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front, hp: 5, maxHp: 20, def: 0);
            var skeleton = AddUnit(state, "skeleton", TeamSide.Enemy, FormationSlot.Middle, hp: 25, maxHp: 25, def: 0);

            var card = CardWith(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 20,
                ScaleWithAttack = false,
                SplashBehindTarget = true,
                SplashPowerPercent = 80
            });
            state.ResolutionTargets[card.InstanceId] = "goblin";

            EffectActionExecutor.ExecuteAll(state, demon, card, new List<BattleEvent>());

            Assert.AreEqual(0, state.GetCombatant("goblin").Hp);
            Assert.AreEqual(9, skeleton.Hp);
        }

        [Test]
        public void BloodArmor_SacrificesHpBeforeGainingBlock()
        {
            var state = BuildState();
            var demon = AddUnit(state, "demon", TeamSide.Player, FormationSlot.Middle, hp: 30, def: 0);
            var card = new CardInstanceState
            {
                InstanceId = 88,
                CardType = CardType.Defense
            };
            card.Keywords.Add("sacrifice");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = 3
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 12
            });

            EffectActionExecutor.ExecuteAll(state, demon, card, new List<BattleEvent>());

            Assert.AreEqual(27, demon.Hp);
            Assert.AreEqual(12, demon.Block);
        }

        [Test]
        public void SolarBlessing_UsesEnergySpentFromPlayerPlan()
        {
            var state = BuildState();
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle);
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            var card = new CardInstanceState
            {
                InstanceId = 102,
                DefinitionId = CardPowerRules.SolarBlessingCardId,
                CardType = CardType.Defense
            };
            state.PlayerPlan.EnergySpentPerCard[card.InstanceId] = 2;

            SpecialCardRules.TryResolve(state, mage, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.AreEqual(12, mage.Block);
            Assert.AreEqual(12, warrior.Block);
        }

        static CardInstanceState CardWith(params EffectActionSpec[] actions)
        {
            var card = new CardInstanceState
            {
                InstanceId = 99,
                CardType = CardType.Attack
            };
            card.Actions.AddRange(actions);
            return card;
        }

        static EffectActionSpec ActionAtk(
            int value,
            int atkPercent,
            EffectTarget target = EffectTarget.DefaultEnemy,
            int ignoreDefPercent = 0,
            int bonusHpBelowPercent = 0,
            int bonusHpBelowFlat = 0,
            int bonusHitThisTurnPercent = 0,
            int lifestealPercent = 0,
            int onKillHeal = 0)
        {
            return new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = target,
                Value = value,
                ScaleWithAttack = true,
                AttackScalePercent = atkPercent,
                IgnoreDefPercent = ignoreDefPercent,
                BonusIfTargetHpBelowPercent = bonusHpBelowPercent,
                BonusIfTargetHpBelowFlat = bonusHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = bonusHitThisTurnPercent,
                LifestealPercent = lifestealPercent,
                OnKillHealAmount = onKillHeal
            };
        }

        [Test]
        public void SlimeSplit_SlowsPlayerNotSelf()
        {
            var state = BuildState();
            var slime = AddUnit(state, "slime", TeamSide.Enemy, FormationSlot.Front, atk: 3, speed: 2);
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front, hp: 40, speed: 7);
            var card = CardWith(StatusAction(StatusCatalog.Slow, 1, 2, EffectTarget.DefaultEnemy));
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, slime, card, events);

            Assert.IsFalse(StatusRules.HasStatus(slime, StatusCatalog.Slow));
            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Slow));
            Assert.AreEqual(5, StatusRules.GetEffectiveSpeed(warrior));
        }

        static EffectActionSpec StatusAction(
            string statusId,
            int stacks,
            int duration,
            EffectTarget target,
            TargetReach reach = TargetReach.Any)
        {
            return new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = target,
                StatusId = statusId,
                Stacks = stacks,
                Duration = duration,
                Reach = reach
            };
        }

        [Test]
        public void EtherealForm_LocksOwnerCardsForRestOfTurn()
        {
            var state = BuildState();
            var lich = AddUnit(state, "lich", TeamSide.Player, FormationSlot.Back, hp: 48, speed: 7);
            lich.CharacterDefinitionId = "char_lich_queen";

            var ethereal = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "虚化形态",
                OwnerCharacterId = "char_lich_queen",
                Actions =
                {
                    StatusAction(StatusCatalog.Ethereal, 1, 1, EffectTarget.Self),
                    new EffectActionSpec { Type = EffectActionType.LockSelfCards, Target = EffectTarget.Self, Value = 1 }
                }
            };

            var claw = new CardInstanceState
            {
                InstanceId = 2,
                DisplayName = "幽灵爪击",
                OwnerCharacterId = "char_lich_queen",
                Actions = { ActionAtk(8, 100, EffectTarget.DefaultEnemy) }
            };

            EffectActionExecutor.ExecuteAll(state, lich, ethereal, new List<BattleEvent>());

            Assert.IsTrue(StatusRules.HasStatus(lich, StatusCatalog.Ethereal));
            Assert.IsTrue(lich.IsCardsLocked);
            Assert.IsTrue(CardLockRules.ShouldSkipPlayerCard(lich, claw));
        }
    }
}
