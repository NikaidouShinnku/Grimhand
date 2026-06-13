using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionBattleConfigBuilderTests
    {
        [Test]
        public void BuildEncounter_HighFloorScalesEnemiesOnlyNotPlayers()
        {
            var template = new BattleConfig();
            template.Combatants.Add(new CombatantConfig
            {
                Id = "player_knight",
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                Level = 1,
                MaxHp = 999,
                BaseAttack = 999,
                BaseDefense = 999
            });
            template.Combatants.Add(new CombatantConfig
            {
                Id = "enemy_goblin",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_goblin",
                MaxHp = 20,
                BaseAttack = 4,
                BaseDefense = 1
            });

            var party = new List<PartyMemberSnapshot>
            {
                new()
                {
                    CharacterDefinitionId = "char_knight",
                    DisplayName = "骑士",
                    Level = 1,
                    Hp = 30,
                    MaxHp = 30
                }
            };

            var floor1 = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, new List<string>(), battleSeed: 11, applyPartyHp: true, floor: 1);
            var floor12 = ExpeditionBattleConfigBuilder.BuildEncounter(
                template, party, new List<string>(), battleSeed: 11, applyPartyHp: true, floor: 12);

            var player1 = FindCombatant(floor1, TeamSide.Player);
            var player12 = FindCombatant(floor12, TeamSide.Player);
            var enemy1 = FindCombatant(floor1, TeamSide.Enemy);
            var enemy12 = FindCombatant(floor12, TeamSide.Enemy);

            Assert.AreEqual(player1.MaxHp, player12.MaxHp);
            Assert.AreEqual(player1.BaseAttack, player12.BaseAttack);
            Assert.AreEqual(player1.BaseDefense, player12.BaseDefense);
            Assert.Greater(enemy12.MaxHp, enemy1.MaxHp);
            Assert.Greater(enemy12.BaseAttack, enemy1.BaseAttack);
        }

        static CombatantConfig FindCombatant(BattleConfig config, TeamSide team)
        {
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == team)
                    return cc;
            }

            Assert.Fail($"Missing combatant for team {team}");
            return null;
        }
    }
}
