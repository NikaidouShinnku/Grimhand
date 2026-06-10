using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
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
                EnemyCardsDrawnPerTurn = source.EnemyCardsDrawnPerTurn,
                EnemyTurnEnergyBudget = source.EnemyTurnEnergyBudget,
                SkipFloorScaling = source.SkipFloorScaling,
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

                copy.Traits.AddRange(cc.Traits);

                foreach (var template in cc.DeckTemplates)
                    copy.DeckTemplates.Add(CloneTemplate(template));

                foreach (var template in cc.SkillPoolCandidates)
                    copy.SkillPoolCandidates.Add(CloneTemplate(template));

                clone.Combatants.Add(copy);
            }

            foreach (var pair in source.SummonTemplates)
                clone.SummonTemplates[pair.Key] = CloneCombatantConfig(pair.Value);

            return clone;
        }

        static CombatantConfig CloneCombatantConfig(CombatantConfig cc) =>
            CloneCombatantConfigPublic(cc);

        public static CombatantConfig CloneCombatantConfigPublic(CombatantConfig cc)
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

            copy.Traits.AddRange(cc.Traits);
            foreach (var template in cc.DeckTemplates)
                copy.DeckTemplates.Add(CloneTemplate(template));
            foreach (var template in cc.SkillPoolCandidates)
                copy.SkillPoolCandidates.Add(CloneTemplate(template));

            return copy;
        }

        public static BattleConfig BuildEncounter(
            BattleConfig encounterTemplate,
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            int battleSeed,
            bool applyPartyHp,
            int miracleLeafUsesRemaining = -1,
            int floor = 1,
            ExpeditionRunModifiers expeditionModifiers = null,
            IReadOnlyList<CardTemplate> playerCardCatalog = null,
            ExpeditionConfig expeditionConfig = null)
        {
            var config = CloneTemplate(encounterTemplate);
            config.Seed = battleSeed;
            config.RunModifiers = RelicDatabase.BuildModifiers(relicIds);
            if (expeditionModifiers != null)
                config.RunModifiers.SoulRiftBattleStartRandomHpLoss =
                    expeditionModifiers.SoulRiftBattleStartRandomHpLoss;
            config.MiracleLeafRevivesRemaining = miracleLeafUsesRemaining;

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);

            if (!encounterTemplate.SkipFloorScaling)
                ApplyEnemyFloorScaling(config, floor, battleSeed);

            if (applyPartyHp && party != null && party.Count > 0)
            {
                foreach (var cc in config.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    foreach (var member in party)
                    {
                        if (member.CharacterDefinitionId != cc.CharacterDefinitionId)
                            continue;

                        ApplyPartyProgress(cc, member, expeditionModifiers);
                        ApplyMemberDeckFromSnapshot(cc, member, expeditionConfig, playerCardCatalog);
                        break;
                    }
                }
            }

            return config;
        }

        static void ApplyEnemyFloorScaling(BattleConfig config, int floor, int battleSeed)
        {
            if (floor <= 1)
                return;

            var rng = new Core.BattleRng(battleSeed ^ unchecked((int)0xE11E0001));
            foreach (var cc in config.Combatants)
            {
                if (cc.Team != TeamSide.Enemy)
                    continue;

                EnemyFloorScaling.Apply(cc, floor, rng);
            }
        }

        static void ApplyMemberDeckFromSnapshot(
            CombatantConfig cc,
            PartyMemberSnapshot member,
            ExpeditionConfig expeditionConfig,
            IReadOnlyList<CardTemplate> cardCatalog)
        {
            if (member == null || expeditionConfig == null)
            {
                ApplyBonusCards(cc, member, cardCatalog);
                return;
            }

            cc.DeckTemplates.Clear();
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(expeditionConfig, member))
            {
                if (entry?.Template == null)
                    continue;

                var template = CloneTemplate(entry.Template);
                HydrateTemplateFromCatalog(template, cardCatalog);
                cc.DeckTemplates.Add(template);
            }
        }

        static void ApplyBonusCards(
            CombatantConfig cc,
            PartyMemberSnapshot member,
            IReadOnlyList<CardTemplate> cardCatalog)
        {
            if (member?.BonusCards == null || member.BonusCards.Count == 0)
                return;

            foreach (var bonus in member.BonusCards)
            {
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                var template = CloneTemplate(bonus);
                HydrateTemplateFromCatalog(template, cardCatalog);
                cc.DeckTemplates.Add(template);
            }
        }

        public static void HydrateTemplateFromCatalog(CardTemplate template, IReadOnlyList<CardTemplate> cardCatalog)
        {
            if (template == null || template.Actions.Count > 0 || cardCatalog == null)
                return;

            foreach (var source in cardCatalog)
            {
                if (source == null || source.DefinitionId != template.DefinitionId)
                    continue;

                if (source.Actions.Count == 0)
                    return;

                template.Cost = source.Cost;
                template.CardType = source.CardType;
                if (string.IsNullOrEmpty(template.DisplayName))
                    template.DisplayName = source.DisplayName;
                if (string.IsNullOrEmpty(template.OwnerCharacterId))
                    template.OwnerCharacterId = source.OwnerCharacterId;

                template.Keywords.Clear();
                template.Keywords.AddRange(source.Keywords);
                foreach (var action in source.Actions)
                {
                    template.Actions.Add(new EffectActionSpec
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
                        BackRowPowerPercent = action.BackRowPowerPercent,
                        IgnoreDefPercent = action.IgnoreDefPercent,
                        BonusIfTargetHpBelowPercent = action.BonusIfTargetHpBelowPercent,
                        BonusIfTargetHpBelowFlat = action.BonusIfTargetHpBelowFlat,
                        BonusIfTargetHitThisTurnPercent = action.BonusIfTargetHitThisTurnPercent,
                        LifestealPercent = action.LifestealPercent,
                        HealMaxHpPercent = action.HealMaxHpPercent,
                        OnKillHealAmount = action.OnKillHealAmount
                    });
                }

                return;
            }
        }

        public static void ApplyPartyProgress(
            CombatantConfig cc,
            PartyMemberSnapshot member,
            ExpeditionRunModifiers expeditionModifiers = null)
        {
            cc.Level = CharacterProgression.ClampLevel(member.Level);
            cc.Xp = member.Xp;
            cc.StartHp = member.Hp <= 0 ? 1 : member.Hp;

            var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, cc.Level);
            cc.MaxHp = stats.MaxHp;
            cc.BaseAttack = stats.BaseAttack + (expeditionModifiers?.TeamAttackBonus ?? 0) + member.PersonalAttackBonus;
            cc.BaseDefense = stats.BaseDefense + (expeditionModifiers?.TeamDefenseBonus ?? 0);
            cc.Speed = stats.Speed;
            member.MaxHp = stats.MaxHp;
        }

        public static List<PartyMemberSnapshot> CaptureParty(
            BattleState state,
            IReadOnlyList<PartyMemberSnapshot> existingParty = null)
        {
            var party = new List<PartyMemberSnapshot>();
            foreach (var c in state.Combatants)
            {
                if (c.Team != TeamSide.Player)
                    continue;

                var existing = FindExistingMember(existingParty, c.CharacterDefinitionId);
                var snap = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = c.CharacterDefinitionId,
                    DisplayName = c.DisplayName,
                    Level = CharacterProgression.ClampLevel(c.Level),
                    Xp = c.Xp,
                    Hp = c.Hp,
                    MaxHp = c.MaxHp,
                    PersonalAttackBonus = existing?.PersonalAttackBonus ?? 0
                };

                if (existing != null)
                {
                    foreach (var kv in existing.RemovedCardCounts)
                        snap.RemovedCardCounts[kv.Key] = kv.Value;

                    foreach (var kv in existing.CardPowerBonusPercent)
                        snap.CardPowerBonusPercent[kv.Key] = kv.Value;

                    foreach (var bonus in existing.BonusCards)
                    {
                        if (bonus == null)
                            continue;

                        snap.BonusCards.Add(CloneTemplate(bonus));
                    }

                    snap.CampDeckCardIds.AddRange(existing.CampDeckCardIds);
                }

                party.Add(snap);
            }

            return party;
        }

        static PartyMemberSnapshot FindExistingMember(
            IReadOnlyList<PartyMemberSnapshot> party,
            string characterDefinitionId)
        {
            if (party == null || string.IsNullOrEmpty(characterDefinitionId))
                return null;

            foreach (var member in party)
            {
                if (member?.CharacterDefinitionId == characterDefinitionId)
                    return member;
            }

            return null;
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
