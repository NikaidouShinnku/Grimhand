using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class ExpeditionBattleConfigBuilder
    {
        public static BattleConfig CloneTemplate(BattleConfig source)
        {
            var clone = new BattleConfig
            {
                Seed = source.Seed,
                EnergyCap = source.EnergyCap,
                TurnStartEnergyRegen = source.TurnStartEnergyRegen,
                HandLimit = source.HandLimit,
                CardsDrawnPerTurn = source.CardsDrawnPerTurn
            };

            foreach (var cc in source.Combatants)
            {
                var copy = new CombatantConfig
                {
                    Id = cc.Id,
                    DisplayName = cc.DisplayName,
                    Team = cc.Team,
                    Slot = cc.Slot,
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    Level = cc.Level,
                    MaxHp = cc.MaxHp,
                    BaseAttack = cc.BaseAttack,
                    BaseDefense = cc.BaseDefense,
                    Speed = cc.Speed,
                    StartHp = cc.StartHp
                };

                foreach (var template in cc.DeckTemplates)
                    copy.DeckTemplates.Add(CloneTemplate(template));

                clone.Combatants.Add(copy);
            }

            return clone;
        }

        public static BattleConfig BuildEncounter(
            BattleConfig encounterTemplate,
            IReadOnlyList<PartyMemberSnapshot> party,
            int battleSeed,
            bool applyPartyHp)
        {
            var config = CloneTemplate(encounterTemplate);
            config.Seed = battleSeed;

            if (!applyPartyHp || party == null || party.Count == 0)
                return config;

            foreach (var cc in config.Combatants)
            {
                if (cc.Team != TeamSide.Player)
                    continue;

                foreach (var member in party)
                {
                    if (member.CharacterDefinitionId != cc.CharacterDefinitionId)
                        continue;

                    cc.StartHp = member.Hp;
                    cc.Level = CharacterProgression.ClampLevel(member.Level);
                    break;
                }
            }

            return config;
        }

        public static List<PartyMemberSnapshot> CaptureParty(BattleState state)
        {
            var party = new List<PartyMemberSnapshot>();
            foreach (var c in state.Combatants)
            {
                if (c.Team != TeamSide.Player)
                    continue;

                party.Add(new PartyMemberSnapshot
                {
                    CharacterDefinitionId = c.CharacterDefinitionId,
                    DisplayName = c.DisplayName,
                    Level = CharacterProgression.ClampLevel(c.Level),
                    Hp = c.Hp,
                    MaxHp = c.MaxHp
                });
            }

            return party;
        }

        static CardTemplate CloneTemplate(CardTemplate source)
        {
            var copy = new CardTemplate
            {
                DefinitionId = source.DefinitionId,
                DisplayName = source.DisplayName,
                OwnerCharacterId = source.OwnerCharacterId,
                Cost = source.Cost,
                CardType = source.CardType
            };

            copy.Keywords.AddRange(source.Keywords);
            foreach (var action in source.Actions)
            {
                copy.Actions.Add(new EffectActionSpec
                {
                    Type = action.Type,
                    Target = action.Target,
                    Value = action.Value,
                    StatusId = action.StatusId,
                    Stacks = action.Stacks,
                    Duration = action.Duration,
                    ScaleWithAttack = action.ScaleWithAttack,
                    ScaleWithDefense = action.ScaleWithDefense,
                    AttackScalePercent = action.AttackScalePercent,
                    DefenseScalePercent = action.DefenseScalePercent,
                    Condition = action.Condition,
                    Reach = action.Reach,
                    SplashBehindTarget = action.SplashBehindTarget,
                    SplashPowerPercent = action.SplashPowerPercent,
                    BackRowPowerPercent = action.BackRowPowerPercent
                });
            }

            return copy;
        }
    }
}
