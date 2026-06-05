using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ReviveAndRelicRulesTests
    {
        [Test]
        public void Heal_DoesNotRestoreDeadAlly()
        {
            var state = BuildState();
            var dead = AddPlayer(state, "dead", hp: 0);
            var events = new List<BattleEvent>();

            DamageRules.ApplyHeal(state, dead, 10, events);

            Assert.AreEqual(0, dead.Hp);
            Assert.IsEmpty(events);
        }

        [Test]
        public void ReviveBlessing_RestoresTwentyFivePercentOnDeath()
        {
            var state = BuildState();
            var pharaoh = AddPlayer(state, "pharaoh", hp: 30, charId: RelicBattleRules.PharaohCharacterId);
            var ally = AddPlayer(state, "ally", hp: 10, maxHp: 40, charId: "char_knight", slot: FormationSlot.Front);
            var enemy = AddEnemy(state, "enemy", atk: 20);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                CardType = CardType.Status,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.FrontAlly,
                        StatusId = StatusCatalog.ReviveBlessing,
                        Stacks = 1,
                        Duration = -1,
                        Reach = TargetReach.Any
                    }
                }
            };
            state.ResolutionTargets[card.InstanceId] = ally.Id;

            var events = new List<BattleEvent>();
            EffectActionExecutor.ExecuteAll(state, pharaoh, card, events);
            Assert.IsTrue(StatusRules.HasStatus(ally, StatusCatalog.ReviveBlessing));

            DamageRules.ApplyDamage(state, enemy, ally, 20, CardType.Attack, events);

            Assert.IsTrue(ally.IsAlive);
            Assert.AreEqual(10, ally.Hp);
        }

        [Test]
        public void SunPyramid_GrantsTeamBlockOnPharaohStatusCard()
        {
            var state = BuildState();
            state.Config.RunModifiers = RelicDatabase.BuildModifiers(new[] { "sun_pyramid" });
            var pharaoh = AddPlayer(state, "pharaoh", hp: 30, charId: RelicBattleRules.PharaohCharacterId);
            var ally = AddPlayer(state, "knight", hp: 20, charId: "char_knight");
            var card = new CardInstanceState
            {
                InstanceId = 2,
                CardType = CardType.Status,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DrawCardsNextTurn,
                        Target = EffectTarget.Self,
                        Value = 1
                    }
                }
            };
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteAll(state, pharaoh, card, events);
            RelicBattleRules.TryApplyStatusCardTeamBlock(state, pharaoh, card, events);

            Assert.AreEqual(3, pharaoh.Block);
            Assert.AreEqual(3, ally.Block);
        }

        [Test]
        public void DeadPartyMember_StartsNextBattleWithOneHp()
        {
            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "战士",
                    Hp = 0,
                    MaxHp = 40,
                    Level = 1
                }
            };

            var template = new BattleConfig();
            template.Combatants.Add(new CombatantConfig
            {
                Id = "p1",
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                MaxHp = 40
            });

            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, System.Array.Empty<string>(), 1, applyPartyHp: true);

            Assert.AreEqual(1, config.Combatants[0].StartHp);
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

        static CombatantState AddPlayer(
            BattleState state,
            string id,
            int hp,
            string charId = "char_mage",
            FormationSlot slot = FormationSlot.Middle,
            int maxHp = 40)
        {
            var c = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = TeamSide.Player,
                Slot = slot,
                CharacterDefinitionId = charId,
                Hp = hp,
                MaxHp = maxHp,
                Attack = 3,
                Defense = 2,
                Speed = 5
            };
            state.Combatants.Add(c);
            return c;
        }

        static CombatantState AddEnemy(
            BattleState state,
            string id,
            int atk = 5)
        {
            var c = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 20,
                MaxHp = 20,
                Attack = atk,
                Defense = 0,
                Speed = 4
            };
            state.Combatants.Add(c);
            return c;
        }
    }
}
