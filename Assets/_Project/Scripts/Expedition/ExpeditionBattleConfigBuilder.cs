using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
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
                VictoryOnCharacterDeathId = source.VictoryOnCharacterDeathId,
                ManualEnemyIntentsOnly = source.ManualEnemyIntentsOnly,
                RunModifiers = CloneModifiers(source.RunModifiers)
            };
            if (source.Talents != null)
            {
                clone.Talents = new TalentBattleContext
                {
                    MageReviveAvailable = source.Talents.MageReviveAvailable,
                    RangerBloodDebtAttackBonus = source.Talents.RangerBloodDebtAttackBonus,
                    NonBossSoloEnemyBattle = source.Talents.NonBossSoloEnemyBattle,
                    IsBossBattle = source.Talents.IsBossBattle
                };
                foreach (var id in source.Talents.ActiveTalentIds)
                    clone.Talents.ActiveTalentIds.Add(id);
            }

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
                    EnteredFromExpeditionDeath = cc.EnteredFromExpeditionDeath,
                    UseSkillPool = cc.UseSkillPool,
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
                EnteredFromExpeditionDeath = cc.EnteredFromExpeditionDeath,
                UseSkillPool = cc.UseSkillPool,
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
            ExpeditionConfig expeditionConfig = null,
            ExpeditionTalentRunState talentRunState = null,
            bool isBossBattle = false,
            IReadOnlyDictionary<string, int> relicGrowthTiers = null,
            IReadOnlyList<CardTemplate> runWideBonusCards = null)
        {
            var config = CloneTemplate(encounterTemplate);
            config.Seed = battleSeed;
            config.RunModifiers = RelicDatabase.BuildModifiers(relicIds, relicGrowthTiers);
            TalentDatabase.MergeIntoBattleConfig(config, party, talentRunState, isBossBattle);
            if (expeditionModifiers != null)
            {
                // 事件等远征加成必须并入本场战斗，否则领取后不会生效。
                config.RunModifiers.TeamAttackBonus += expeditionModifiers.TeamAttackBonus;
                config.RunModifiers.TeamDefenseBonus += expeditionModifiers.TeamDefenseBonus;
                config.RunModifiers.TeamBlockGainBonusPercent +=
                    expeditionModifiers.TeamBlockGainBonusPercent;
                config.RunModifiers.SoulRiftBattleStartRandomHpLoss =
                    expeditionModifiers.SoulRiftBattleStartRandomHpLoss;
            }
            config.MiracleLeafRevivesRemaining = ResolveMiracleLeafUsesRemaining(
                relicIds, miracleLeafUsesRemaining);

            var playerCap = party != null && party.Count > 0
                ? System.Math.Min(party.Count, CampRosterState.PartySize)
                : CampRosterState.PartySize;
            TrimPlayerCombatants(config, playerCap);

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);

            ApplyPlayerPartyProgress(
                config,
                party,
                applyPartyHp,
                expeditionModifiers,
                expeditionConfig,
                playerCardCatalog,
                runWideBonusCards);

            if (!encounterTemplate.SkipFloorScaling)
                ApplyEnemyFloorScaling(config, floor, battleSeed);

            // 层数缩放只应作用于敌人；再次刷新玩家数值以防模板被误改。
            ApplyPlayerPartyProgress(
                config,
                party,
                applyPartyHp,
                expeditionModifiers,
                expeditionConfig,
                playerCardCatalog,
                runWideBonusCards);

            return config;
        }

        static int ResolveMiracleLeafUsesRemaining(
            IReadOnlyList<string> relicIds,
            int miracleLeafUsesRemaining)
        {
            if (miracleLeafUsesRemaining >= 0)
                return miracleLeafUsesRemaining;

            if (relicIds == null)
                return -1;

            foreach (var id in relicIds)
            {
                if (id == RelicIds.LeafOfMiracle)
                    return 2;
            }

            return -1;
        }

        static void ApplyPlayerPartyProgress(
            BattleConfig config,
            IReadOnlyList<PartyMemberSnapshot> party,
            bool applyPartyHp,
            ExpeditionRunModifiers expeditionModifiers,
            ExpeditionConfig expeditionConfig,
            IReadOnlyList<CardTemplate> playerCardCatalog,
            IReadOnlyList<CardTemplate> runWideBonusCards = null)
        {
            if (!applyPartyHp || party == null || party.Count == 0)
                return;

            TrimPlayerCombatants(
                config,
                System.Math.Min(party.Count, CampRosterState.PartySize));

            // 按阵型槽位顺序将编队成员映射到玩家 combatant（支持军营换人后角色 ID 与模板不一致）。
            var players = new List<CombatantConfig>();
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == TeamSide.Player)
                    players.Add(cc);
            }

            players.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            for (var i = 0; i < players.Count && i < party.Count; i++)
            {
                var cc = players[i];
                var member = party[i];
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                cc.CharacterDefinitionId = member.CharacterDefinitionId;
                cc.DisplayName = CharacterDisplayNames.GetOrFallback(
                    member.CharacterDefinitionId,
                    member.DisplayName);

                ApplyPartyProgress(cc, member, expeditionModifiers);
                ApplyMemberDeckFromSnapshot(
                    cc,
                    member,
                    expeditionConfig,
                    playerCardCatalog,
                    runWideBonusCards,
                    addOwnerlessRunWideCards: i == 0);
            }
        }

        public static void TrimPlayerCombatantsPublic(BattleConfig config, int maxPlayers) =>
            TrimPlayerCombatants(config, maxPlayers);

        static void TrimPlayerCombatants(BattleConfig config, int maxPlayers)
        {
            if (config?.Combatants == null || maxPlayers <= 0)
            {
                if (config?.Combatants == null)
                    return;

                for (var i = config.Combatants.Count - 1; i >= 0; i--)
                {
                    if (config.Combatants[i].Team == TeamSide.Player)
                        config.Combatants.RemoveAt(i);
                }

                return;
            }

            var players = new List<(CombatantConfig cc, int listIndex)>();
            for (var i = 0; i < config.Combatants.Count; i++)
            {
                var cc = config.Combatants[i];
                if (cc != null && cc.Team == TeamSide.Player)
                    players.Add((cc, i));
            }

            players.Sort((a, b) => a.cc.Slot.CompareTo(b.cc.Slot));

            if (players.Count <= maxPlayers)
                return;

            var remove = new HashSet<CombatantConfig>();
            for (var i = maxPlayers; i < players.Count; i++)
                remove.Add(players[i].cc);

            for (var i = config.Combatants.Count - 1; i >= 0; i--)
            {
                if (remove.Contains(config.Combatants[i]))
                    config.Combatants.RemoveAt(i);
            }
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
            IReadOnlyList<CardTemplate> cardCatalog,
            IReadOnlyList<CardTemplate> runWideBonusCards = null,
            bool addOwnerlessRunWideCards = true)
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

            AppendRunWideBonusCards(cc, member, runWideBonusCards, cardCatalog, addOwnerlessRunWideCards);
        }

        static void AppendRunWideBonusCards(
            CombatantConfig cc,
            PartyMemberSnapshot member,
            IReadOnlyList<CardTemplate> runWideBonusCards,
            IReadOnlyList<CardTemplate> cardCatalog,
            bool addOwnerlessRunWideCards = true)
        {
            if (runWideBonusCards == null || member == null)
                return;

            foreach (var bonus in runWideBonusCards)
            {
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                if (!string.IsNullOrEmpty(bonus.OwnerCharacterId))
                {
                    if (bonus.OwnerCharacterId != member.CharacterDefinitionId)
                        continue;
                }
                else if (!addOwnerlessRunWideCards)
                {
                    // 无归属角色的额外牌（如诅咒牌）只在首个队员处入池一次，避免三倍污染。
                    continue;
                }

                var template = CloneTemplate(bonus);
                HydrateTemplateFromCatalog(template, cardCatalog);
                ExpeditionRunDeckCatalog.ApplyCardUpgrades(template, member);
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
                ExpeditionRunDeckCatalog.ApplyCardUpgrades(template, member);
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
                        BonusIfTargetHasStatusId = action.BonusIfTargetHasStatusId,
                        BonusIfTargetHasStatusFlat = action.BonusIfTargetHasStatusFlat,
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
            cc.EnteredFromExpeditionDeath = member.Hp <= 0;
            cc.StartHp = member.Hp <= 0 ? 1 : member.Hp;

            var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, cc.Level);
            cc.MaxHp = System.Math.Max(1, member.MaxHp > 0 ? member.MaxHp : stats.MaxHp);
            cc.BaseAttack = member.PersonalAttackBonus;
            cc.BaseDefense = 0;
            cc.Speed = stats.Speed + member.PersonalSpeedBonus;
        }

        public static List<PartyMemberSnapshot> CaptureParty(
            BattleState state,
            IReadOnlyList<PartyMemberSnapshot> existingParty = null)
        {
            var party = new List<PartyMemberSnapshot>();
            if (state?.Combatants == null)
                return party;

            var playerCombatants = new List<CombatantState>();
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player)
                    playerCombatants.Add(c);
            }

            playerCombatants.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));

            if (playerCombatants.Count == 0)
            {
                if (existingParty != null)
                {
                    for (var i = 0; i < existingParty.Count && party.Count < CampRosterState.PartySize; i++)
                    {
                        if (existingParty[i] != null)
                            party.Add(CloneExpeditionMember(existingParty[i]));
                    }
                }

                return party;
            }

            for (var i = 0; i < playerCombatants.Count && party.Count < CampRosterState.PartySize; i++)
            {
                var c = playerCombatants[i];
                var existing = ResolveExistingMember(existingParty, i, c.CharacterDefinitionId);
                var snap = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = c.CharacterDefinitionId,
                    DisplayName = CharacterDisplayNames.GetOrFallback(c.CharacterDefinitionId, c.DisplayName),
                    Level = CharacterProgression.ClampLevel(c.Level),
                    Xp = c.Xp,
                    Hp = c.Hp,
                    MaxHp = c.MaxHp,
                    PersonalAttackBonus = existing?.PersonalAttackBonus ?? 0,
                    SelectedTalentSlot1Id = existing?.SelectedTalentSlot1Id ?? "",
                    SelectedTalentSlot2Id = existing?.SelectedTalentSlot2Id ?? ""
                };

                CopyExpeditionDeckProgress(snap, existing);
                party.Add(snap);
            }

            // 战斗里玩家位少于远征编队时，补回未入场/未捕获的队员，避免祭坛等 UI 只剩一人的牌组。
            if (existingParty != null)
            {
                foreach (var existing in existingParty)
                {
                    if (existing == null || string.IsNullOrEmpty(existing.CharacterDefinitionId))
                        continue;
                    if (party.Count >= CampRosterState.PartySize)
                        break;
                    if (FindExistingMember(party, existing.CharacterDefinitionId) != null)
                        continue;

                    party.Add(CloneExpeditionMember(existing));
                }
            }

            return party;
        }

        static void CopyExpeditionDeckProgress(PartyMemberSnapshot snap, PartyMemberSnapshot existing)
        {
            if (snap == null || existing == null)
                return;

            foreach (var kv in existing.RemovedCardCounts)
                snap.RemovedCardCounts[kv.Key] = kv.Value;

            foreach (var kv in existing.CardUpgradeLevels)
                snap.CardUpgradeLevels[kv.Key] = kv.Value;

            snap.BaseDeckInstanceIds.AddRange(existing.BaseDeckInstanceIds);

            foreach (var kv in existing.CardPowerBonusPercent)
                snap.CardPowerBonusPercent[kv.Key] = kv.Value;

            foreach (var kv in existing.CardFlatDamageBonuses)
                snap.CardFlatDamageBonuses[kv.Key] = kv.Value;

            foreach (var bonus in existing.BonusCards)
            {
                if (bonus == null)
                    continue;

                snap.BonusCards.Add(CloneTemplate(bonus));
            }

            snap.CampDeckCardIds.AddRange(existing.CampDeckCardIds);
            snap.UsesCampDeckAsBattleBase = existing.UsesCampDeckAsBattleBase;

            foreach (var index in existing.ExtractedCampCardIndices)
                snap.ExtractedCampCardIndices.Add(index);

            snap.AltarMaxHpBonus = existing.AltarMaxHpBonus;
            snap.MaxHpPenalty = existing.MaxHpPenalty;
            snap.AltarSpeedUpgrades = existing.AltarSpeedUpgrades;
            snap.PersonalSpeedBonus = existing.PersonalSpeedBonus;
        }

        static PartyMemberSnapshot CloneExpeditionMember(PartyMemberSnapshot existing)
        {
            var snap = new PartyMemberSnapshot
            {
                CharacterDefinitionId = existing.CharacterDefinitionId,
                DisplayName = existing.DisplayName,
                Level = existing.Level,
                Xp = existing.Xp,
                Hp = existing.Hp,
                MaxHp = existing.MaxHp,
                MaxHpPenalty = existing.MaxHpPenalty,
                AltarMaxHpBonus = existing.AltarMaxHpBonus,
                AltarSpeedUpgrades = existing.AltarSpeedUpgrades,
                PersonalSpeedBonus = existing.PersonalSpeedBonus,
                PersonalAttackBonus = existing.PersonalAttackBonus,
                SelectedTalentSlot1Id = existing.SelectedTalentSlot1Id,
                SelectedTalentSlot2Id = existing.SelectedTalentSlot2Id
            };

            CopyExpeditionDeckProgress(snap, existing);
            return snap;
        }

        static PartyMemberSnapshot ResolveExistingMember(
            IReadOnlyList<PartyMemberSnapshot> party,
            int slotIndex,
            string characterDefinitionId)
        {
            if (party == null || string.IsNullOrEmpty(characterDefinitionId))
                return null;

            if (slotIndex >= 0 && slotIndex < party.Count)
            {
                var atSlot = party[slotIndex];
                if (atSlot?.CharacterDefinitionId == characterDefinitionId)
                    return atSlot;
            }

            return FindExistingMember(party, characterDefinitionId);
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

        public static void GrantXpToParty(
            List<PartyMemberSnapshot> party,
            int amount,
            IReadOnlyList<string> relicIds = null,
            Dictionary<string, int> relicGrowthTiers = null)
        {
            // v0.8：自动升级已移除，由调用方写入 SharedXpPool。
            GrantXpToPool(null, amount);
        }

        public static void GrantXpToPool(ExpeditionRunState run, int amount)
        {
            if (amount <= 0)
                return;

            if (run != null)
                run.SharedXpPool += amount;
        }

        public static void GrantXpToMember(
            PartyMemberSnapshot member,
            int amount,
            IReadOnlyList<string> relicIds = null,
            Dictionary<string, int> relicGrowthTiers = null)
        {
            if (member == null || amount <= 0)
                return;

            var hpBonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(
                new List<PartyMemberSnapshot> { member }, relicIds, relicGrowthTiers);
            var beforeEffective = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, hpBonus);
            var result = CharacterProgression.AddXp(member.Level, member.Xp, amount);
            member.Level = result.Level;
            member.Xp = result.Xp;

            if (result.LevelsGained <= 0)
                return;

            var afterEffective = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, hpBonus);
            var hpGain = afterEffective - beforeEffective;
            member.MaxHp = afterEffective;
            if (hpGain > 0)
                member.Hp = System.Math.Min(member.MaxHp, member.Hp + hpGain);
        }

        static RunModifierSnapshot CloneModifiers(RunModifierSnapshot source)
        {
            if (source == null)
                return RunModifierSnapshot.Empty;

            return new RunModifierSnapshot
            {
                TeamAttackBonus = source.TeamAttackBonus,
                TeamDefenseBonus = source.TeamDefenseBonus,
                TeamHpBonus = source.TeamHpBonus,
                FrontDefenseBonus = source.FrontDefenseBonus,
                BackRowExtraDrawPerTurn = source.BackRowExtraDrawPerTurn,
                BattleStartTeamHeal = source.BattleStartTeamHeal,
                BattleStartFrontBlock = source.BattleStartFrontBlock,
                ExtraDrawOnBattleStart = source.ExtraDrawOnBattleStart,
                SkipPollutedCardsOnDraw = source.SkipPollutedCardsOnDraw,
                GoldBonusPercent = source.GoldBonusPercent,
                SacrificeDamageBonusPercent = source.SacrificeDamageBonusPercent,
                SacrificeHpCostReduction = source.SacrificeHpCostReduction,
                SacrificeStackAttackBonus = source.SacrificeStackAttackBonus,
                TeamAttackBonusPercent = source.TeamAttackBonusPercent,
                TeamBlockGainBonusPercent = source.TeamBlockGainBonusPercent,
                TurnStartEnemyBurnStacks = source.TurnStartEnemyBurnStacks,
                HealBonusPercent = source.HealBonusPercent,
                PharaohBlockGivenBonusPercent = source.PharaohBlockGivenBonusPercent,
                SacrificeHpCostReductionPercent = source.SacrificeHpCostReductionPercent,
                SacrificeHpCostIncreasePercent = source.SacrificeHpCostIncreasePercent,
                HealGrantsBlock = source.HealGrantsBlock,
                StatusCardTeamBlock = source.StatusCardTeamBlock,
                WarriorBlockChanceOnHit = source.WarriorBlockChanceOnHit,
                WarriorBlockAmountOnHit = source.WarriorBlockAmountOnHit,
                WarriorFirstHitBlockAmount = source.WarriorFirstHitBlockAmount,
                WarriorTauntDamageReductionPercent = source.WarriorTauntDamageReductionPercent,
                WarriorBlockDamageReductionPercent = source.WarriorBlockDamageReductionPercent,
                FirstAttackDamageBonusPercent = source.FirstAttackDamageBonusPercent,
                FirstAttackFlatBonus = source.FirstAttackFlatBonus,
                FirstDefenseFlatBonus = source.FirstDefenseFlatBonus,
                AttackAndDefenseSameTurnHeal = source.AttackAndDefenseSameTurnHeal,
                HighCostCardDamageBonusPercent = source.HighCostCardDamageBonusPercent,
                FirstHitDamageReductionPercent = source.FirstHitDamageReductionPercent,
                EndTurnTeamHeal = source.EndTurnTeamHeal,
                StatusDurationBonusTurns = source.StatusDurationBonusTurns,
                AttackBurnProcChance = source.AttackBurnProcChance,
                AttackBurnStacks = source.AttackBurnStacks,
                AttackBurnDurationTurns = source.AttackBurnDurationTurns,
                ExtraEnergyCap = source.ExtraEnergyCap,
                RandomDiscardEachTurn = source.RandomDiscardEachTurn,
                DeathCardsSkipPolluteTurns = source.DeathCardsSkipPolluteTurns,
                DeathCardsSkipPolluteDuration = source.DeathCardsSkipPolluteDuration,
                ScryDrawPileCount = source.ScryDrawPileCount,
                TurnStartRandomAllyBlock = source.TurnStartRandomAllyBlock,
                TurnStartTeamBlock = source.TurnStartTeamBlock,
                DodgeChanceOnHit = source.DodgeChanceOnHit,
                BattleStartSpeedBonusTurns = source.BattleStartSpeedBonusTurns,
                BattleStartSpeedBonus = source.BattleStartSpeedBonus,
                EndTurnEnemyFireDamage = source.EndTurnEnemyFireDamage,
                TurnStartEnemyDamage = source.TurnStartEnemyDamage,
                RevengeAttackFlatBonus = source.RevengeAttackFlatBonus,
                BackRowAttackAnyTarget = source.BackRowAttackAnyTarget,
                JadeDaggerFirstKillBonus = source.JadeDaggerFirstKillBonus,
                MiracleLeafReviveHpPercent = source.MiracleLeafReviveHpPercent,
                SoulRiftBattleStartRandomHpLoss = source.SoulRiftBattleStartRandomHpLoss,
                PostBattleTeamHealPercent = source.PostBattleTeamHealPercent,
                FrontRowBurnTargetDamageMultiplier = source.FrontRowBurnTargetDamageMultiplier,
                FrontRowIgnoreArmorDamagePercent = source.FrontRowIgnoreArmorDamagePercent,
                RequiresFelskullChoice = source.RequiresFelskullChoice,
                FelskullOutgoingDamagePercentBonus = source.FelskullOutgoingDamagePercentBonus,
                FirstPlayerAttackPending = source.FirstPlayerAttackPending,
                HolysunSpellbookBonusUpgradeLevels = source.HolysunSpellbookBonusUpgradeLevels,
                EtherealEntryCount = source.EtherealEntryCount,
                ExpeditionRespondSuccessCount = source.ExpeditionRespondSuccessCount,
                SandSpearExhaustCardsPlayed = source.SandSpearExhaustCardsPlayed
            };
        }

        public static CardTemplate CloneTemplate(CardTemplate source)
        {
            var copy = new CardTemplate
            {
                DefinitionId = source.DefinitionId,
                DisplayName = source.DisplayName,
                OwnerCharacterId = source.OwnerCharacterId,
                DeckInstanceId = source.DeckInstanceId,
                UpgradeLevel = source.UpgradeLevel,
                Cost = source.Cost,
                CardType = source.CardType
            };

            copy.Keywords.AddRange(source.Keywords);
            foreach (var action in source.Actions)
                copy.Actions.Add(EffectActionSpec.Clone(action));

            return copy;
        }
    }
}
