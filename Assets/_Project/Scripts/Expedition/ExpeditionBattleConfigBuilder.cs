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
                CardsDrawnPerTurn = source.CardsDrawnPerTurn,
                RunModifiers = CloneModifiers(source.RunModifiers)
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
                    Xp = cc.Xp,
                    MaxHp = cc.MaxHp,
                    BaseAttack = cc.BaseAttack,
                    BaseDefense = cc.BaseDefense,
                    Speed = cc.Speed,
                    StartHp = cc.StartHp,
                    UseRandomSkillPool = cc.UseRandomSkillPool,
                    RandomDeckSize = cc.RandomDeckSize,
                    RandomSkillPickMin = cc.RandomSkillPickMin,
                    RandomSkillPickMax = cc.RandomSkillPickMax
                };

                foreach (var template in cc.DeckTemplates)
                    copy.DeckTemplates.Add(CloneTemplate(template));

                foreach (var template in cc.SkillPoolCandidates)
                    copy.SkillPoolCandidates.Add(CloneTemplate(template));

                clone.Combatants.Add(copy);
            }

            return clone;
        }

        public static BattleConfig BuildEncounter(
            BattleConfig encounterTemplate,
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            int battleSeed,
            bool applyPartyHp)
        {
            var config = CloneTemplate(encounterTemplate);
            config.Seed = battleSeed;
            config.RunModifiers = RelicDatabase.BuildModifiers(relicIds);

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

                    ApplyPartyProgress(cc, member);
                    ApplyBonusCards(cc, member);
                    break;
                }
            }

            return config;
        }

        static void ApplyBonusCards(CombatantConfig cc, PartyMemberSnapshot member)
        {
            if (member?.BonusCards == null || member.BonusCards.Count == 0)
                return;

            foreach (var bonus in member.BonusCards)
            {
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                cc.DeckTemplates.Add(CloneTemplate(bonus));
            }
        }

        public static void ApplyPartyProgress(CombatantConfig cc, PartyMemberSnapshot member)
        {
            cc.Level = CharacterProgression.ClampLevel(member.Level);
            cc.Xp = member.Xp;
            cc.StartHp = member.Hp;

            var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, cc.Level);
            cc.MaxHp = stats.MaxHp;
            cc.BaseAttack = stats.BaseAttack;
            cc.BaseDefense = stats.BaseDefense;
            cc.Speed = stats.Speed;
            member.MaxHp = stats.MaxHp;
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
                    Xp = c.Xp,
                    Hp = c.Hp,
                    MaxHp = c.MaxHp
                });
            }

            return party;
        }

        public static void GrantXpToParty(List<PartyMemberSnapshot> party, int amount)
        {
            if (party == null || amount <= 0)
                return;

            foreach (var member in party)
            {
                var before = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, member.Level);
                var result = CharacterProgression.AddXp(member.Level, member.Xp, amount);
                member.Level = result.Level;
                member.Xp = result.Xp;

                if (result.LevelsGained <= 0)
                    continue;

                var after = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, member.Level);
                var hpGain = after.MaxHp - before.MaxHp;
                member.MaxHp = after.MaxHp;
                if (hpGain > 0)
                    member.Hp = System.Math.Min(member.MaxHp, member.Hp + hpGain);
            }
        }

        static RunModifierSnapshot CloneModifiers(RunModifierSnapshot source)
        {
            if (source == null)
                return RunModifierSnapshot.Empty;

            return new RunModifierSnapshot
            {
                TeamAttackBonus = source.TeamAttackBonus,
                FrontDefenseBonus = source.FrontDefenseBonus,
                BackRowExtraDrawPerTurn = source.BackRowExtraDrawPerTurn,
                BattleStartTeamHeal = source.BattleStartTeamHeal,
                GoldBonusPercent = source.GoldBonusPercent,
                SacrificeDamageBonusPercent = source.SacrificeDamageBonusPercent,
                HealBonusPercent = source.HealBonusPercent,
                HealGrantsBlock = source.HealGrantsBlock,
                WarriorBlockChanceOnHit = source.WarriorBlockChanceOnHit,
                WarriorBlockAmountOnHit = source.WarriorBlockAmountOnHit,
                FirstAttackDamageBonusPercent = source.FirstAttackDamageBonusPercent,
                ExtraEnergyCap = source.ExtraEnergyCap,
                RandomDiscardEachTurn = source.RandomDiscardEachTurn,
                DeathCardsSkipPolluteTurns = source.DeathCardsSkipPolluteTurns,
                DeathCardsSkipPolluteDuration = source.DeathCardsSkipPolluteDuration,
                ScryDrawPileCount = source.ScryDrawPileCount,
                FirstPlayerAttackPending = true
            };
        }

        public static CardTemplate CloneTemplate(CardTemplate source)
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
