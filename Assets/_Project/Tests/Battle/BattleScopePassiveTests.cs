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
            StatusRules.ApplyStatus(state, warrior, StatusCatalog.RespondStance, 1, -1, new List<BattleEvent>());

            var events = new List<BattleEvent>();
            PassiveCardMechanicsRules.TryTriggerRespondStanceOnRespondSuccess(
                state, warrior, events, new BattleRng(1));

            Assert.GreaterOrEqual(warrior.Block, PassiveCardMechanicsRules.RespondStanceBlock);
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
