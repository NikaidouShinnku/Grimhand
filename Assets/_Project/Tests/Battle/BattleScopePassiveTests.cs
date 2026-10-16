using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class BattleScopePassiveTests
    {
        [Test]
        public void RespondStance_GrantsBlockOnRespondSuccess()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            // The real card stores the block amount in Stacks (8), not a boolean flag.
            StatusRules.ApplyStatus(state, warrior, StatusCatalog.RespondStance, 8, -1, new List<BattleEvent>());

            var events = new List<BattleEvent>();
            PassiveCardMechanicsRules.TryTriggerRespondStanceOnRespondSuccess(
                state, warrior, events, new BattleRng(1));

            Assert.AreEqual(8, warrior.Block);
        }

        [Test]
        public void BattleWill_GrantsAttackUpOnDamageTaken()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            var goblin = AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front, atk: 5, def: 0);

            StatusRules.ApplyStatus(state, warrior, StatusCatalog.BattleWill, 1, -1, new List<BattleEvent>());
            var events = new List<BattleEvent>();

            DamageRules.ApplyDamage(state, goblin, warrior, 5, CardType.Attack, events);

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void PsionicBody_BonusInNonCombatPhase()
        {
            var state = BuildState();
            var lich = AddUnit(state, "lich", TeamSide.Player, FormationSlot.Back);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.PsionicBody, 1, -1, new List<BattleEvent>());

            state.Phase = TurnPhase.Planning;
            var bonus = V09NewMechanicsRules.ApplyPsionicBodyBonus(state, TeamSide.Player, 10);

            Assert.Greater(bonus, 10);
        }

        [Test]
        public void LichSpiritFocus_TalentBonusInNonCombatPhase()
        {
            var state = BuildState();
            state.Config.Talents = new TalentBattleContext();
            state.Config.Talents.ActiveTalentIds.Add("talent_lich_s2_lv2");
            AddUnit(state, "lich", TeamSide.Player, FormationSlot.Back);

            state.Phase = TurnPhase.Planning;
            var bonus = V09NewMechanicsRules.ApplyPsionicBodyBonus(state, TeamSide.Player, 10);

            Assert.AreEqual(11, bonus);

            state.Phase = TurnPhase.SpeedResolve;
            var combat = V09NewMechanicsRules.ApplyPsionicBodyBonus(state, TeamSide.Player, 10);
            Assert.AreEqual(10, combat);
        }

        [Test]
        public void LichSpiritFocus_StacksWithPsionicBody()
        {
            var state = BuildState();
            state.Config.Talents = new TalentBattleContext();
            state.Config.Talents.ActiveTalentIds.Add("talent_lich_s2_lv2");
            var lich = AddUnit(state, "lich", TeamSide.Player, FormationSlot.Back);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.PsionicBody, 1, -1, new List<BattleEvent>());

            state.Phase = TurnPhase.Planning;
            var bonus = V09NewMechanicsRules.ApplyPsionicBodyBonus(state, TeamSide.Player, 10);

            // 1.2 * 1.1 = 1.32 → 13
            Assert.AreEqual(13, bonus);
        }

        static BattleState BuildState() => new BattleState { Config = new BattleConfig() };

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void PlagueSpread_PoisonTicksUseTheirOwnStacksAndDuration(bool timedFirst, bool enabled)
        {
            var state = BuildState();
            var caster = AddUnit(state, "caster", TeamSide.Player, FormationSlot.Front);
            // Process the recipient first so newly spread poison cannot tick in this test.
            var adjacent = AddUnit(state, "adjacent", TeamSide.Enemy, FormationSlot.Front);
            var victim = AddUnit(state, "victim", TeamSide.Enemy, FormationSlot.Middle);
            adjacent.Hp = adjacent.MaxHp = victim.Hp = victim.MaxHp = 500;
            var events = new List<BattleEvent>();
            if (enabled)
                StatusRules.ApplyStatus(state, caster, StatusCatalog.PlagueSpread, 100, -1, events);
            StatusRules.ApplyStatus(state, victim, StatusCatalog.Poison,
                timedFirst ? 2 : 5, timedFirst ? 3 : -1, events);
            StatusRules.ApplyStatus(state, victim, StatusCatalog.Poison,
                timedFirst ? 5 : 2, timedFirst ? -1 : 3, events);

            StatusRules.ProcessTurnStartStatuses(state, events, new BattleRng(42));

            Assert.AreEqual(493, victim.Hp);
            Assert.AreEqual(500, adjacent.Hp);
            Assert.AreEqual(enabled ? 3 : 0, StatusRules.GetStatusStacks(adjacent, StatusCatalog.Poison));
            var permanent = 0;
            var timed = 0;
            foreach (var status in adjacent.Statuses)
            {
                if (status.StatusId != StatusCatalog.Poison)
                    continue;
                if (status.RemainingTurns == -1)
                    permanent += status.Stacks;
                else
                {
                    Assert.AreEqual(3, status.RemainingTurns);
                    timed += status.Stacks;
                }
            }
            Assert.AreEqual(enabled ? 2 : 0, permanent);
            Assert.AreEqual(enabled ? 1 : 0, timed);
        }

        static CombatantState AddUnit(
            BattleState state,
            string id,
            TeamSide team,
            FormationSlot slot,
            int atk = 5,
            int def = 2)
        {
            var unit = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                Hp = 30,
                MaxHp = 30,
                Attack = atk,
                Defense = def,
                BaseAttack = atk,
                BaseDefense = def
            };
            state.Combatants.Add(unit);
            return unit;
        }
    }
}
