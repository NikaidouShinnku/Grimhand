using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class KnightTalentReworkTests
    {
        [Test]
        public void Catalog_MatchesReworkedDescriptions()
        {
            Assert.AreEqual(
                "成功应对攻击后，下回合开始时获得5护甲",
                TalentCatalog.Get("talent_knight_s2_lv2").Description);
            Assert.AreEqual(
                "成功应对攻击后，获得20%增伤（2回合）",
                TalentCatalog.Get("talent_knight_s2_lv3").Description);
            Assert.AreEqual(
                "如果一回合中连续使用三张战士的攻击牌，当回合获得33%增伤（不包括快速启动牌）",
                TalentCatalog.Get("talent_knight_s2_lv8").Description);
        }

        [Test]
        public void RespondAttack_QueuesNextTurnBlock()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv2");
            var knight = FindKnight(state);
            var enemyCard = new CardInstanceState
            {
                InstanceId = 9,
                DefinitionId = "m_bite",
                CardType = CardType.Attack
            };
            state.CardsById[enemyCard.InstanceId] = enemyCard;

            TalentBattleRules.OnRespondSuccess(
                state,
                knight,
                new RespondTriggerContext("e1", enemyCard.InstanceId),
                new List<BattleEvent>());

            Assert.AreEqual(5, knight.TalentRespondNextTurnBlock);

            TalentBattleRules.ProcessTurnStart(state, new List<BattleEvent>());
            Assert.AreEqual(0, knight.TalentRespondNextTurnBlock);
            Assert.AreEqual(5, knight.Block);
        }

        [Test]
        public void RespondStatus_DoesNotQueueNextTurnBlock()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv2");
            var knight = FindKnight(state);
            var enemyCard = new CardInstanceState
            {
                InstanceId = 9,
                DefinitionId = "m_curse",
                CardType = CardType.Status
            };
            state.CardsById[enemyCard.InstanceId] = enemyCard;

            TalentBattleRules.OnRespondSuccess(
                state,
                knight,
                new RespondTriggerContext("e1", enemyCard.InstanceId),
                new List<BattleEvent>());

            Assert.AreEqual(0, knight.TalentRespondNextTurnBlock);
        }

        [Test]
        public void RespondAttack_AppliesAttackUpForTwoTurns()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv3");
            var knight = FindKnight(state);
            var events = new List<BattleEvent>();
            var enemyCard = new CardInstanceState
            {
                InstanceId = 9,
                CardType = CardType.Attack
            };
            state.CardsById[enemyCard.InstanceId] = enemyCard;

            TalentBattleRules.OnRespondSuccess(
                state,
                knight,
                new RespondTriggerContext("e1", enemyCard.InstanceId),
                events);

            Assert.AreEqual(20, StatusRules.GetStatusStacks(knight, StatusCatalog.AttackUpPercent));
            RelicBattleRules.RefreshDerivedStats(state, knight, state.Config.RunModifiers);
            Assert.GreaterOrEqual(knight.OutgoingDamagePercentBonus, 20);
        }

        [Test]
        public void RespondStatus_DoesNotApplyAttackUp()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv3");
            var knight = FindKnight(state);
            var enemyCard = new CardInstanceState
            {
                InstanceId = 9,
                CardType = CardType.Status
            };
            state.CardsById[enemyCard.InstanceId] = enemyCard;

            TalentBattleRules.OnRespondSuccess(
                state,
                knight,
                new RespondTriggerContext("e1", enemyCard.InstanceId),
                new List<BattleEvent>());

            Assert.AreEqual(0, StatusRules.GetStatusStacks(knight, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void Combo_ThirdWarriorAttackGetsBonusImmediately()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv8");
            var knight = FindKnight(state);
            var attack = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "w_strike",
                CardType = CardType.Attack,
                OwnerCharacterId = TalentBattleRules.KnightId
            };

            TalentBattleRules.OnCardAboutToResolve(state, knight, attack);
            TalentBattleRules.OnCardAboutToResolve(state, knight, attack);
            Assert.AreEqual(2, knight.TalentAttackCardsThisTurn);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(knight, StatusCatalog.KnightComboAtk));

            TalentBattleRules.OnCardAboutToResolve(state, knight, attack);
            Assert.AreEqual(3, knight.TalentAttackCardsThisTurn);
            Assert.AreEqual(33, StatusRules.GetStatusStacks(knight, StatusCatalog.KnightComboAtk));
            RelicBattleRules.RefreshDerivedStats(state, knight, state.Config?.RunModifiers);
            Assert.GreaterOrEqual(knight.OutgoingDamagePercentBonus, 33);
        }

        [Test]
        public void Combo_NonAttackBreaksStreak()
        {
            var state = MakeStateWithTalent("talent_knight_s2_lv8");
            var knight = FindKnight(state);
            var attack = new CardInstanceState
            {
                InstanceId = 1,
                CardType = CardType.Attack,
                OwnerCharacterId = TalentBattleRules.KnightId
            };
            var defense = new CardInstanceState
            {
                InstanceId = 2,
                CardType = CardType.Defense,
                OwnerCharacterId = TalentBattleRules.KnightId
            };

            TalentBattleRules.OnCardAboutToResolve(state, knight, attack);
            TalentBattleRules.OnCardAboutToResolve(state, knight, attack);
            TalentBattleRules.OnCardAboutToResolve(state, knight, defense);
            Assert.AreEqual(0, knight.TalentAttackCardsThisTurn);
        }

        static BattleState MakeStateWithTalent(string talentId)
        {
            var config = new BattleConfig
            {
                RunModifiers = new RunModifierSnapshot(),
                Talents = new TalentBattleContext()
            };
            config.Talents.ActiveTalentIds.Add(talentId);

            var state = new BattleState { Config = config };
            var knight = new CombatantState
            {
                Id = "p_knight",
                DisplayName = "战士",
                Team = TeamSide.Player,
                CharacterDefinitionId = TalentBattleRules.KnightId,
                MaxHp = 50,
                Hp = 50,
                Slot = FormationSlot.Front
            };
            state.Combatants.Add(knight);
            return state;
        }

        static CombatantState FindKnight(BattleState state) => state.Combatants[0];
    }
}
