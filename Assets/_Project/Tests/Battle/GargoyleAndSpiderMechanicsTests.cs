using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class GargoyleAndSpiderMechanicsTests
    {
        [Test]
        public void SpiderFang_PoisonChanceCanHitOrMiss()
        {
            var state = BuildState(out var spider, out var warrior);
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "m_spider_fang",
                DisplayName = "毒牙刺击",
                Cost = 1,
                CardType = CardType.Attack
            };
            var poison = new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1,
                ChancePercent = 30
            };

            EffectActionExecutor.ExecuteOne(
                state, spider, card, poison, new List<BattleEvent>(), new BattleRng(1), 1,
                targetOverride: warrior);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison));

            EffectActionExecutor.ExecuteOne(
                state, spider, card, poison, new List<BattleEvent>(), new BattleRng(2), 1,
                targetOverride: warrior);
            Assert.AreEqual(5, StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison));
        }

        [Test]
        public void SpiderWrap_ArmLocksAttackerOnConsume()
        {
            var state = BuildState(out var spider, out var warrior);
            var wrap = new CardInstanceState
            {
                InstanceId = 2,
                DefinitionId = "m_spider_wrap",
                DisplayName = "蛛网包裹",
                CardType = CardType.Defense
            };
            wrap.Keywords.Add("parry");
            wrap.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            wrap.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockAttackCards,
                Target = EffectTarget.LastActionActor,
                Value = 2,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });

            DefenderRespondArmRules.TryArmFromEnemyCardResolve(state, spider, wrap);
            Assert.AreEqual(1, state.DefenderRespondArms.Count);
            Assert.AreEqual(2, state.DefenderRespondArms[0].LockAttackerTurns);

            var hpDamage = 20;
            CombatantState recipient = spider;
            var ok = DefenderRespondArmRules.TryConsumeForIncomingPlayerAttack(
                state, warrior, ref recipient, ref hpDamage, new List<BattleEvent>(), out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(10, hpDamage);
            Assert.AreEqual(2, warrior.AttackCardsLockedTurnsRemaining);
            Assert.IsTrue(CardLockRules.ShouldBlockPlayerCardPlanning(
                warrior, new CardInstanceState { CardType = CardType.Attack }));
        }

        [Test]
        public void SpiderFatalBind_DamagesTargetThenSelfHalfHp()
        {
            var state = BuildState(out var spider, out var warrior);
            warrior.Slot = FormationSlot.Middle;
            spider.Hp = 40;

            var card = new CardInstanceState
            {
                InstanceId = 3,
                DefinitionId = PassiveCardMechanicsRules.SpiderFatalBindCardId,
                DisplayName = "致命缠杀",
                CardType = CardType.Attack
            };
            card.Keywords.Add("exhaust");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 18,
                Reach = TargetReach.MiddleAndBack
            });

            var events = new List<BattleEvent>();
            var warriorHpBefore = warrior.Hp;
            EffectActionExecutor.ExecuteAll(state, spider, card, events, new BattleRng(1));
            Assert.Less(warrior.Hp, warriorHpBefore);

            PassiveCardMechanicsRules.OnSpiderFatalBindResolved(
                state, spider, card, events, new BattleRng(1));
            Assert.AreEqual(20, spider.Hp);
        }

        [Test]
        public void SpiderLadyTrait_IncreasesDamageOnPoisonedPlayers()
        {
            var state = BuildState(out var spider, out var warrior);
            spider.Traits.Add(MinionTraitCatalog.SpiderLadyPoisonVulnerability);
            var events = new List<BattleEvent>();

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Poison, 5, -1, events);
            Assert.AreEqual(10, StatusRules.GetStatusStacks(warrior, StatusCatalog.SpiderPoisonVulnerable),
                "5层中毒应显示10层易伤icon");
            Assert.AreEqual(110, CombatModifierRules.ApplyIncomingDamageModifiers(warrior, 100, 0));

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Poison, 5, -1, events);
            Assert.AreEqual(20, StatusRules.GetStatusStacks(warrior, StatusCatalog.SpiderPoisonVulnerable));
            Assert.AreEqual(120, CombatModifierRules.ApplyIncomingDamageModifiers(warrior, 100, 0));

            StatusRules.RemoveAllStatus(warrior, StatusCatalog.Poison, events, state);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(warrior, StatusCatalog.SpiderPoisonVulnerable),
                "中毒消失后易伤icon应立即消失");
            Assert.AreEqual(100, CombatModifierRules.ApplyIncomingDamageModifiers(warrior, 100, 0));
            Assert.AreEqual(100, MinionTraitRules.ApplySpiderPoisonVulnerability(state, warrior, 100),
                "勿与状态易伤双重乘伤");
        }

        [Test]
        public void ChainWraith_MirrorsDebuffToPlayersWithTwoTurnDuration()
        {
            var state = BuildState(out var wraith, out var warrior);
            wraith.Id = "wraith";
            wraith.DisplayName = "锁链怨灵";
            wraith.CharacterDefinitionId = "char_chain_wraith";
            wraith.Traits.Clear();
            wraith.Traits.Add(MinionTraitCatalog.ChainWraithDebuffShare);

            StatusRules.ApplyStatus(state, wraith, StatusCatalog.Slow, 3, -1, new List<BattleEvent>());
            Assert.AreEqual(3, StatusRules.GetStatusStacks(warrior, StatusCatalog.Slow));
            Assert.AreEqual(
                MinionTraitCatalog.ChainWraithMirrorDebuffDurationTurns,
                StatusRules.FindStatus(warrior, StatusCatalog.Slow)?.RemainingTurns);
            Assert.AreEqual(-1, StatusRules.FindStatus(wraith, StatusCatalog.Slow)?.RemainingTurns);
        }

        [Test]
        public void RatPack_BonusUsesBattleDeathCountNotAliveWindow()
        {
            var state = new BattleState { Config = new BattleConfig() };
            CombatantState MakeRat(string id)
            {
                var rat = new CombatantState
                {
                    Id = id,
                    DisplayName = id,
                    Team = TeamSide.Enemy,
                    CharacterDefinitionId = MinionTraitCatalog.RatCharacterId,
                    Hp = 10,
                    MaxHp = 10
                };
                rat.Traits.Add(MinionTraitCatalog.RatPackAttackOnAllyDeath);
                StatusRules.ApplyStatus(state, rat, StatusCatalog.RatSwarmCall, 1, -1, new List<BattleEvent>());
                state.Combatants.Add(rat);
                return rat;
            }

            var a = MakeRat("rat_a");
            var b = MakeRat("rat_b");
            var c = MakeRat("rat_c");

            a.Hp = 0;
            MinionTraitRules.OnCharacterDied(state, a, new List<BattleEvent>());
            b.Hp = 0;
            MinionTraitRules.OnCharacterDied(state, b, new List<BattleEvent>());
            c.Hp = 0;
            MinionTraitRules.OnCharacterDied(state, c, new List<BattleEvent>());

            Assert.AreEqual(3, state.RatDeathsThisBattle);
            CombatantState survivor = null;
            var cloneCount = 0;
            foreach (var unit in state.Combatants)
            {
                if (!unit.IsAlive || unit.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId)
                    continue;
                cloneCount++;
                Assert.AreEqual(60, unit.RatPackAttackBonusPercent);
                survivor = unit;
            }

            Assert.AreEqual(3, cloneCount);

            var toKill = new List<CombatantState>();
            foreach (var unit in state.Combatants)
            {
                if (unit.IsAlive
                    && unit.CharacterDefinitionId == MinionTraitCatalog.RatCharacterId
                    && unit.Id != survivor.Id)
                    toKill.Add(unit);
            }

            Assert.AreEqual(2, toKill.Count);
            foreach (var unit in toKill)
            {
                unit.Hp = 0;
                MinionTraitRules.OnCharacterDied(state, unit, new List<BattleEvent>());
            }

            Assert.AreEqual(5, state.RatDeathsThisBattle);
            Assert.IsTrue(survivor.IsAlive);
            Assert.AreEqual(100, survivor.RatPackAttackBonusPercent);
        }

        [Test]
        public void GargoyleTrait_AppliesBuffAtNextTurnStartFromPriorFirstCard()
        {
            var state = BuildState(out var gargoyle, out _);
            gargoyle.Id = "gargoyle";
            gargoyle.DisplayName = "石像鬼";
            gargoyle.CharacterDefinitionId = "char_gargoyle";
            gargoyle.Traits.Clear();
            gargoyle.Traits.Add(MinionTraitCatalog.GargoyleFirstCardStance);

            var attack = new CardInstanceState
            {
                InstanceId = 4,
                DefinitionId = "m_gargoyle_claw",
                CardType = CardType.Attack
            };
            MinionTraitRules.OnCardResolved(state, gargoyle, attack, new List<BattleEvent>());
            Assert.AreEqual(CardType.Attack, gargoyle.FirstCardTypeThisTurn);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(gargoyle, StatusCatalog.AttackUpPercent));

            MinionTraitRules.ProcessTurnStart(state, new List<BattleEvent>());
            Assert.AreEqual(
                MinionTraitCatalog.GargoyleTraitPercentBonus,
                StatusRules.GetStatusStacks(gargoyle, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(1, StatusRules.FindStatus(gargoyle, StatusCatalog.AttackUpPercent)?.RemainingTurns);
            Assert.IsNull(gargoyle.FirstCardTypeThisTurn);

            StatusRules.ProcessEndOfTurnDurations(state, new List<BattleEvent>());
            Assert.AreEqual(0, StatusRules.GetStatusStacks(gargoyle, StatusCatalog.AttackUpPercent),
                "石像鬼特性应仅持续 1 回合，回合末到期");
        }

        [Test]
        public void SkeletonTrait_GainsArmorEveryThreeCards()
        {
            var state = BuildState(out var skeleton, out _);
            skeleton.Traits.Clear();
            skeleton.Traits.Add(MinionTraitCatalog.SkeletonCardDef);
            skeleton.Block = 0;

            for (var i = 0; i < 2; i++)
            {
                MinionTraitRules.OnCardResolved(state, skeleton, new CardInstanceState
                {
                    InstanceId = i + 1,
                    CardType = CardType.Attack
                }, new List<BattleEvent>());
            }

            Assert.AreEqual(0, skeleton.Block);
            MinionTraitRules.OnCardResolved(state, skeleton, new CardInstanceState
            {
                InstanceId = 3,
                CardType = CardType.Defense
            }, new List<BattleEvent>());
            Assert.AreEqual(MinionTraitCatalog.SkeletonArmorPerThreshold, skeleton.Block);
            Assert.AreEqual(3, skeleton.CardsResolvedCount);
        }

        [Test]
        public void SkeletonEliteTrait_GainsArmorAndPermanentAttackPercent()
        {
            var state = BuildState(out var elite, out _);
            elite.Traits.Clear();
            elite.Traits.Add(MinionTraitCatalog.SkeletonEliteCardStats);
            elite.Block = 0;

            for (var i = 0; i < 3; i++)
            {
                MinionTraitRules.OnCardResolved(state, elite, new CardInstanceState
                {
                    InstanceId = i + 1,
                    CardType = CardType.Attack
                }, new List<BattleEvent>());
            }

            Assert.AreEqual(MinionTraitCatalog.SkeletonEliteArmorPerThreshold, elite.Block);
            Assert.AreEqual(
                MinionTraitCatalog.SkeletonEliteAttackPercentPerThreshold,
                StatusRules.GetStatusStacks(elite, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(-1, StatusRules.FindStatus(elite, StatusCatalog.AttackUpPercent)?.RemainingTurns);
        }

        [Test]
        public void BatFirstHitDodge_IsFiftyPercentNotGuaranteed()
        {
            var state = BuildState(out var bat, out var warrior);
            bat.Traits.Clear();
            bat.Traits.Add(MinionTraitCatalog.BatFirstHitDodge);
            bat.Team = TeamSide.Enemy;
            bat.FirstHitDodgePending = true;
            warrior.Team = TeamSide.Player;

            var dodges = 0;
            const int trials = 200;
            for (var i = 0; i < trials; i++)
            {
                bat.FirstHitDodgePending = true;
                if (MinionTraitRules.TryFirstHitDodge(state, bat, new BattleRng(i * 97 + 13), new List<BattleEvent>()))
                    dodges++;
            }

            var rate = dodges / (float)trials;
            Assert.Greater(rate, 0.35f, $"闪避率过低: {rate}");
            Assert.Less(rate, 0.65f, $"闪避率过高(疑似100%): {rate}");
        }

        [Test]
        public void GargoyleEmpower_EnemyCasterPenalizesEnemyEnergyNotPlayer()
        {
            var state = BuildState(out var gargoyle, out _);
            gargoyle.Id = "gargoyle";
            gargoyle.Team = TeamSide.Enemy;
            var card = new CardInstanceState
            {
                InstanceId = 5,
                DefinitionId = "m_gargoyle_empower",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.Self,
                Value = 2
            });

            EffectActionExecutor.ExecuteAll(state, gargoyle, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(2, state.PendingEnemyEnergyRegenPenaltyNextTurn);
            Assert.AreEqual(0, state.PendingPlayerEnergyRegenPenaltyNextTurn);
        }

        [Test]
        public void GolemQuakeSlam_Uses40WhenNotHit()
        {
            var state = BuildState(out var golem, out var warrior);
            golem.Id = "golem";
            golem.DisplayName = "石傀儡";
            golem.HitThisTurn = false;

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 17,
                AlternateValue = 40,
                UseAlternateIfActorNotHitThisTurn = true,
                Reach = TargetReach.FrontAndMiddle
            };

            var power = CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior);
            Assert.AreEqual(40, power);

            golem.HitThisTurn = true;
            power = CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior);
            Assert.AreEqual(17, power);
        }

        [Test]
        public void GolemFist_DoublesWhenSelfBlockAbove20()
        {
            var state = BuildState(out var golem, out var warrior);
            golem.Id = "golem";
            golem.Block = 21;

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10,
                SelfBlockAboveThreshold = 20,
                AlternateValueIfSelfBlockAbove = 20,
                Reach = TargetReach.FrontAndMiddle
            };

            Assert.AreEqual(20, CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior));
            golem.Block = 20;
            Assert.AreEqual(10, CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior));
        }

        [Test]
        public void GolemUnmovable_AppliesDamageReductionUnconditionally()
        {
            var state = BuildState(out var golem, out _);
            golem.Id = "golem";
            var card = new CardInstanceState
            {
                InstanceId = 6,
                DefinitionId = "m_golem_unmovable",
                CardType = CardType.Defense
            };
            card.Keywords.Add("parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 90,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.DamageReduction,
                Stacks = 20,
                Duration = 3
            });

            EffectActionExecutor.ExecuteUnconditionalActions(state, golem, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(20, StatusRules.GetStatusStacks(golem, StatusCatalog.DamageReduction));
        }

        [Test]
        public void StoneGolemTrait_RetainsHalfBlockNextTurn()
        {
            var state = BuildState(out var golem, out _);
            golem.Id = "golem";
            golem.Traits.Add(MinionTraitCatalog.StoneGolemArmorRetain);
            golem.Block = 20;

            MinionTraitRules.PrepareTurnEndArmorRetain(state);
            Assert.AreEqual(10, golem.CarryOverBlock);

            golem.Block = 0;
            MinionTraitRules.ProcessTurnStart(state, new List<BattleEvent>());
            Assert.AreEqual(10, golem.Block);
            Assert.AreEqual(0, golem.CarryOverBlock);
        }

        static BattleState BuildState(out CombatantState spider, out CombatantState warrior)
        {
            var state = new BattleState { Config = new BattleConfig() };
            spider = new CombatantState
            {
                Id = "spider",
                DisplayName = "蜘蛛贵妇",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_spider_lady",
                Hp = 60,
                MaxHp = 60,
                BaseAttack = 9,
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
            state.Combatants.Add(spider);
            state.Combatants.Add(warrior);
            return state;
        }
    }
}
