using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TalentV092AlignmentTests
    {
        [Test]
        public void Knight_S2Lv3_OnlyOnRespondAttack()
        {
            var state = MakeState("talent_knight_s2_lv3", TalentBattleRules.KnightId);
            var knight = state.Combatants[0];
            var attack = new CardInstanceState
            {
                InstanceId = 9,
                CardType = CardType.Attack
            };
            var status = new CardInstanceState
            {
                InstanceId = 10,
                CardType = CardType.Status
            };
            state.CardsById[attack.InstanceId] = attack;
            state.CardsById[status.InstanceId] = status;

            TalentBattleRules.OnRespondSuccess(
                state, knight, new RespondTriggerContext("e1", status.InstanceId), new List<BattleEvent>());
            Assert.AreEqual(0, StatusRules.GetStatusStacks(knight, StatusCatalog.AttackUpPercent));

            TalentBattleRules.OnRespondSuccess(
                state, knight, new RespondTriggerContext("e1", attack.InstanceId), new List<BattleEvent>());
            Assert.AreEqual(20, StatusRules.GetStatusStacks(knight, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void Mage_S2Lv2_DiscountsAnyCharactersFirstStatus()
        {
            var state = MakeState("talent_mage_s2_lv2", TalentBattleRules.KnightId);
            TalentBattleRules.OnBattleInitialized(state);
            var knight = state.Combatants[0];
            var status = new CardInstanceState
            {
                InstanceId = 1,
                CardType = CardType.Status,
                Cost = 2,
                OwnerCharacterId = TalentBattleRules.KnightId
            };

            Assert.AreEqual(1, TalentBattleRules.GetEffectivePlayCost(state, knight, status));
        }

        [Test]
        public void Mage_S2Lv6_AppliesPermanentSlow()
        {
            var state = MakeState("talent_mage_s2_lv6", TalentBattleRules.MageId);
            TalentBattleRules.OnBattleInitialized(state);
            var mage = state.Combatants[0];
            var enemy = new CombatantState
            {
                Id = "e1",
                Team = TeamSide.Enemy,
                MaxHp = 30,
                Hp = 30
            };
            state.Combatants.Add(enemy);

            TalentBattleRules.OnMageDamageDealt(state, mage, enemy, 5, new List<BattleEvent>());
            var slow = enemy.Statuses.Find(s => s.StatusId == StatusCatalog.Slow);
            Assert.IsNotNull(slow);
            Assert.AreEqual(-1, slow.RemainingTurns);
        }

        [Test]
        public void Ranger_MicroSacrifice_IsSlot1Lv3()
        {
            var def = TalentCatalog.Get("talent_ranger_s1_lv3");
            Assert.IsNotNull(def);
            Assert.AreEqual(1, def.Slot);
            Assert.AreEqual(3, def.UnlockLevel);
            // 旧 id 仅作存档别名，解析到新定义
            Assert.AreSame(def, TalentCatalog.Get("talent_ranger_s2_lv3"));
        }

        [Test]
        public void Ranger_S2Lv8_UsesLiveEnemyCount()
        {
            var state = MakeState("talent_ranger_s2_lv8", TalentBattleRules.RangerId);
            state.Config.Talents.IsBossBattle = false;
            var ranger = state.Combatants[0];
            state.Combatants.Add(Enemy("e1", 20));
            state.Combatants.Add(Enemy("e2", 20));

            RelicBattleRules.RefreshDerivedStats(state, ranger, state.Config.RunModifiers);
            Assert.Less(ranger.OutgoingDamagePercentBonus, 30);

            state.Combatants[2].Hp = 0;
            RelicBattleRules.RefreshDerivedStats(state, ranger, state.Config.RunModifiers);
            Assert.GreaterOrEqual(ranger.OutgoingDamagePercentBonus, 30);
        }

        [Test]
        public void Lich_S1Lv4_HealsTwoOnEtherealHit()
        {
            var state = MakeState("talent_lich_s1_lv4", TalentBattleRules.LichQueenId);
            var lich = state.Combatants[0];
            lich.Hp = 20;
            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            var hpDamage = 8;
            Assert.IsTrue(TalentBattleRules.TryHandleEtherealDamage(state, lich, ref hpDamage, new List<BattleEvent>()));
            Assert.AreEqual(0, hpDamage);
            Assert.AreEqual(22, lich.Hp);
        }

        [Test]
        public void Lich_S2Lv8_GrantsEtherealEveryFourthTurn()
        {
            var state = MakeState("talent_lich_s2_lv8", TalentBattleRules.LichQueenId);
            state.TurnNumber = 4;
            TalentBattleRules.ProcessTurnStartV09Talents(state, new List<BattleEvent>());
            Assert.IsTrue(StatusRules.HasStatus(state.Combatants[0], StatusCatalog.Ethereal));
        }

        [Test]
        public void Lich_S1Lv9_QueuesAoeWhenAllCardsAreLich()
        {
            var state = MakeState("talent_lich_s1_lv9", TalentBattleRules.LichQueenId);
            var lich = state.Combatants[0];
            var card = new CardInstanceState
            {
                InstanceId = 1,
                OwnerCharacterId = TalentBattleRules.LichQueenId,
                CardType = CardType.Attack
            };

            TalentBattleRules.OnCardResolved(state, lich, card, new List<BattleEvent>());
            TalentBattleRules.ProcessEndOfTurn(state, new List<BattleEvent>());
            Assert.AreEqual(10, state.TalentLichPendingEnemyAoeNextTurn);

            var enemy = Enemy("e1", 40);
            state.Combatants.Add(enemy);
            TalentBattleRules.ProcessTurnStartV09Talents(state, new List<BattleEvent>());
            Assert.AreEqual(0, state.TalentLichPendingEnemyAoeNextTurn);
            Assert.AreEqual(30, enemy.Hp);
        }

        [Test]
        public void Lich_S2Lv10_SealedCardGoesToHandTemporaryCostPlusOneFullEffects()
        {
            var state = MakeState("talent_lich_s2_lv10", TalentBattleRules.LichQueenId);
            state.TurnNumber = 2;
            var sealedCard = new CardInstanceState
            {
                InstanceId = 5,
                DefinitionId = "g_blood_scratch",
                OwnerCharacterId = "char_goblin",
                DisplayName = "嗜血抓挠",
                CardType = CardType.Attack,
                Cost = 1,
                BaseCost = 1
            };
            sealedCard.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 4
            });
            sealedCard.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUp,
                Stacks = 3,
                Duration = 1
            });
            state.CardsById[sealedCard.InstanceId] = sealedCard;

            TalentBattleRules.OnEnemyCardSealed(state, sealedCard, new List<BattleEvent>());
            Assert.AreEqual(1, state.PlayerHand.Count);
            var taken = state.PlayerHand[0];
            Assert.AreEqual(2, taken.Cost);
            Assert.IsFalse(taken.Keywords.Contains("exhaust"));
            Assert.IsTrue(taken.IsBonusHandCard);
            Assert.AreEqual(2, taken.BonusHandGrantedTurn);
            Assert.AreEqual("char_goblin", taken.OwnerCharacterId);
            Assert.AreEqual("p1", taken.OwnerCombatantId);
            Assert.AreEqual("g_blood_scratch", taken.DefinitionId);
            Assert.AreEqual(2, taken.Actions.Count);
            Assert.AreEqual(EffectActionType.DealDamage, taken.Actions[0].Type);
            Assert.AreEqual(4, taken.Actions[0].Value);
            Assert.AreEqual(EffectActionType.ApplyStatus, taken.Actions[1].Type);
            Assert.AreEqual(StatusCatalog.AttackUp, taken.Actions[1].StatusId);
            Assert.AreEqual(3, taken.Actions[1].Stacks);
        }

        [Test]
        public void Lich_S1Lv10_RemovedFromCatalog()
        {
            Assert.IsNull(TalentCatalog.Get("talent_lich_s1_lv10"));
            Assert.IsNotNull(TalentCatalog.Get("talent_lich_s1_lv9"));
        }

        static BattleState MakeState(string talentId, string characterId)
        {
            var config = new BattleConfig
            {
                RunModifiers = new RunModifierSnapshot(),
                Talents = new TalentBattleContext()
            };
            config.Talents.ActiveTalentIds.Add(talentId);

            var state = new BattleState { Config = config };
            state.Combatants.Add(new CombatantState
            {
                Id = "p1",
                DisplayName = "unit",
                Team = TeamSide.Player,
                CharacterDefinitionId = characterId,
                MaxHp = 50,
                Hp = 50,
                Slot = FormationSlot.Front
            });
            return state;
        }

        static CombatantState Enemy(string id, int hp) => new()
        {
            Id = id,
            Team = TeamSide.Enemy,
            MaxHp = hp,
            Hp = hp,
            Slot = FormationSlot.Front
        };
    }
}
