using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Map;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;

namespace Grimhand.Expedition
{
    public sealed class ExpeditionEngine
    {
        readonly ExpeditionConfig _config;
        readonly BattleRng _rng;
        readonly ExpeditionRunState _run = new();

        public ExpeditionEngine(ExpeditionConfig config)
        {
            _config = config ?? new ExpeditionConfig();
            _rng = new BattleRng(_config.RunSeed);
            _run.TargetBattleCount = _config.TargetBattleCount > 0
                ? _config.TargetBattleCount
                : _config.ChapterLayerCount - 1;
        }

        public ExpeditionRunState Run => _run;
        public ExpeditionConfig Config => _config;

        public void StartRun(CampRosterState campRoster = null, CampMetaState campMeta = null)
        {
            _run.Phase = ExpeditionPhase.RouteSelect;
            _run.BattlesWon = 0;
            _run.Gold = 0;
            _run.LastGoldReward = 0;
            _run.LastXpReward = 0;
            _run.SharedXpPool = 0;
            _run.LastEventMessage = "";
            _run.Party.Clear();
            _run.Relics.Clear();
            _run.RelicGrowthTiers.Clear();
            _run.UsedEventIds.Clear();
            _run.EventFlags.Clear();
            _run.ConsumableSlots.Clear();
            ConsumableInventory.EnsureInitialized(_run.ConsumableSlots);
            _run.PendingConsumableOfferId = "";
            _run.PendingCardOffer = null;
            _run.CardAltar = null;
            _run.RunStartCampDecks.Clear();
            _run.ExtractedCampCollectionIndices.Clear();
            _run.Modifiers.TeamAttackBonus = 0;
            _run.Modifiers.TeamDefenseBonus = 0;
            _run.Modifiers.EnergyCapBonus = 0;
            _run.Modifiers.NextCombatEnemyAttackBonus = false;
            _run.Modifiers.ForeseenLayerCount = 0;
            _run.Modifiers.SkipNextRouteSelect = false;
            _run.Modifiers.LootedInjuredAdventurer = false;
            _run.Modifiers.DivinePunishmentActive = false;
            _run.Modifiers.SoulRiftBattleStartRandomHpLoss = 0;
            _run.PendingRoutes.Clear();
            _run.PendingRewardPickup = null;
            _run.PendingEvent = null;
            _run.PendingEventAftermath = null;
            _run.EventResolutionFixedRoll100 = null;
            _run.PendingShrine = null;
            _run.EventInteraction = null;
            _run.PendingEventBattleKey = "";
            _run.PendingEventBattleBonusXp = 0;
            _run.PendingEventBattleVictoryReward = null;
            _run.PendingDeferredReward = null;
            _run.Shop.Clear();
            _run.RunWideBonusCards.Clear();
            _run.PendingTravelerGiftRelicId = "";
            _run.PendingTravelerGiftCurseOwnerId = "";
            _run.CurrentBattleConfig = null;

            _run.Map = ExpeditionMapGenerator.Generate(_config, _run, _rng);
            if (_config.MapStartLayer > 1 && _run.Map != null)
                _run.Map.NodesCompleted = _config.MapStartLayer - 1;

            if (campRoster != null && ExpeditionPartyRules.HasUsableCampRoster(campRoster))
                CampRunPartyApplier.Apply(campRoster, _run, campMeta);
            else
                InitPartyFromTemplate(campMeta);

            ExpeditionPartyRules.EnforceMaxSize(_run.Party);
            CampDeckOwnershipRules.SanitizeParty(_config, _run.Party);
            CampDeckOwnershipRules.SyncRunStartCampDecks(_run);

            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(_config, _run.Party);

            _run.TalentRun.Reset();
            TalentDatabase.ApplyRunStartEffects(_run, _config);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
            LoadRoutesForNextLayer();
            TryBeginBossTestJump();
        }

        void TryBeginBossTestJump()
        {
            if (!ExpeditionRegionRules.IsBossTestStartLayer(_config.MapStartLayer))
                return;

            ExpeditionMapGenerator.ForceBossLayer(_run.Map, _config.MapStartLayer);
            RecordLastBattleContext(_config.MapStartLayer, isElite: false, isBoss: true);
            _run.Phase = ExpeditionPhase.InBattle;
            _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
        }

        /// <summary>Boss 测试场景：Lv.7 队伍 + 3 遗物 + 每人 3 张奖励牌，直进幽灵女王战。</summary>
        public void StartGhostQueenBossTest(BattleConfig ghostQueenTemplate)
        {
            const int partyLevel = 7;
            const int relicCount = 3;
            const int bonusCardsPerMember = 3;

            ResetRunState(skipMap: true);
            InitPartyAtLevel(partyLevel, CampMetaState.CreateDefaultDemo());
            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(_config, _run.Party);
            RollStartingRelics(relicCount);
            RollStartingBonusCards(bonusCardsPerMember);

            _run.Phase = ExpeditionPhase.InBattle;
            _run.CurrentBossDisplayName = Floor10BossEncounterBuilder.GhostQueenDisplayName;

            if (ghostQueenTemplate == null)
            {
                var standard = _config.CombatEncounters.Count > 0 ? _config.CombatEncounters[0] : null;
                ghostQueenTemplate = GhostQueenBossEncounterBuilder.BuildTemplate(standard);
            }

            var seed = _rng.NextInt(1, int.MaxValue);
            _run.CurrentBattleConfig = ExpeditionBattleConfigBuilder.BuildEncounter(
                ghostQueenTemplate,
                _run.Party,
                _run.Relics,
                seed,
                applyPartyHp: true,
                _run.MiracleLeafUsesRemaining,
                floor: 10,
                _run.Modifiers,
                _config.PlayerCardCatalog,
                _config,
                _run.TalentRun,
                isBossBattle: true,
                _run.RelicGrowthTiers,
                _run.RunWideBonusCards);

            _run.CurrentBattleConfig.EnergyCap += _run.Modifiers.EnergyCapBonus;
            _run.CurrentBattleConfig.TurnStartEnergyRegen = System.Math.Max(
                _run.CurrentBattleConfig.TurnStartEnergyRegen, 4);
        }

        void ResetRunState(bool skipMap)
        {
            _run.BattlesWon = 0;
            _run.Gold = 0;
            _run.LastGoldReward = 0;
            _run.LastXpReward = 0;
            _run.SharedXpPool = 0;
            _run.LastEventMessage = "";
            _run.Party.Clear();
            _run.Relics.Clear();
            _run.RelicGrowthTiers.Clear();
            _run.UsedEventIds.Clear();
            _run.EventFlags.Clear();
            _run.ConsumableSlots.Clear();
            ConsumableInventory.EnsureInitialized(_run.ConsumableSlots);
            _run.PendingConsumableOfferId = "";
            _run.PendingCardOffer = null;
            _run.CardAltar = null;
            _run.RunStartCampDecks.Clear();
            _run.ExtractedCampCollectionIndices.Clear();
            _run.Modifiers.TeamAttackBonus = 0;
            _run.Modifiers.TeamDefenseBonus = 0;
            _run.Modifiers.EnergyCapBonus = 0;
            _run.Modifiers.NextCombatEnemyAttackBonus = false;
            _run.Modifiers.ForeseenLayerCount = 0;
            _run.Modifiers.SkipNextRouteSelect = false;
            _run.Modifiers.LootedInjuredAdventurer = false;
            _run.Modifiers.DivinePunishmentActive = false;
            _run.Modifiers.SoulRiftBattleStartRandomHpLoss = 0;
            _run.PendingRoutes.Clear();
            _run.PendingRewardPickup = null;
            _run.PendingEvent = null;
            _run.PendingEventAftermath = null;
            _run.EventResolutionFixedRoll100 = null;
            _run.PendingShrine = null;
            _run.EventInteraction = null;
            _run.PendingEventBattleKey = "";
            _run.PendingEventBattleBonusXp = 0;
            _run.PendingEventBattleVictoryReward = null;
            _run.PendingDeferredReward = null;
            _run.Shop.Clear();
            _run.RunWideBonusCards.Clear();
            _run.PendingTravelerGiftRelicId = "";
            _run.PendingTravelerGiftCurseOwnerId = "";
            _run.CurrentBattleConfig = null;
            _run.TalentRun.Reset();
            _run.RunAcquisitionLog.Clear();
            _run.Map = skipMap ? null : ExpeditionMapGenerator.Generate(_config, _run, _rng);
        }

        void InitPartyAtLevel(int level, CampMetaState campMeta = null)
        {
            if (_config.CombatEncounters.Count == 0)
                return;

            campMeta ??= CampMetaState.CreateDefaultDemo();
            foreach (var cc in _config.CombatEncounters[0].Combatants)
            {
                if (cc.Team != TeamSide.Player)
                    continue;

                if (_run.Party.Count >= CampRosterState.PartySize)
                    break;

                var clamped = CharacterProgression.ClampLevel(level);
                var stats = CharacterProgression.GetStatsForCharacter(cc.CharacterDefinitionId, clamped);
                var member = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    DisplayName = cc.DisplayName,
                    Level = clamped,
                    Xp = 0,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                };
                CampRunPartyApplier.ApplyTalentsFromMeta(member, campMeta);
                _run.Party.Add(member);
            }
        }

        void RollStartingRelics(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var relicId = PickRandomRelicId();
                if (string.IsNullOrEmpty(relicId))
                    break;

                if (!_run.Relics.Contains(relicId))
                {
                    _run.Relics.Add(relicId);
                    if (RelicDatabase.TryGet(relicId, out var relic))
                        RecordRunAcquisition($"测试遗物：{relic.DisplayName}");
                }
            }
        }

        string PickRandomRelicId()
        {
            var pool = new List<string>();
            foreach (var relic in RelicDatabase.All)
            {
                if (_run.Relics.Contains(relic.Id))
                    continue;

                if (!RelicDatabase.CanAppearInRewardPool(relic, _run.Party))
                    continue;

                pool.Add(relic.Id);
            }

            return pool.Count == 0 ? "" : pool[_rng.NextIndex(pool.Count)];
        }

        void RollStartingBonusCards(int countPerMember)
        {
            var catalog = ExpeditionCardPool.CollectPlayerCardTemplates(_config);
            foreach (var member in _run.Party)
            {
                var pool = new List<CardTemplate>();
                foreach (var template in catalog)
                {
                    if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                        continue;

                    if (template.OwnerCharacterId != member.CharacterDefinitionId)
                        continue;

                    pool.Add(ExpeditionBattleConfigBuilder.CloneTemplate(template));
                }

                var pickedIds = new HashSet<string>();
                for (var n = 0; n < countPerMember && pool.Count > 0; n++)
                {
                    CardTemplate chosen = null;
                    for (var attempt = 0; attempt < pool.Count * 2; attempt++)
                    {
                        var candidate = pool[_rng.NextIndex(pool.Count)];
                        if (!pickedIds.Add(candidate.DefinitionId))
                            continue;

                        chosen = candidate;
                        break;
                    }

                    if (chosen == null)
                        break;

                    ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(chosen, _config.PlayerCardCatalog);
                    if (!ExpeditionRunDeckRules.CanAddWithoutReplace(_config, member))
                        break;

                    ExpeditionDeckInstanceRules.PrepareNewDeckCard(member, chosen);
                    member.BonusCards.Add(chosen);
                    RecordRunAcquisition($"测试卡牌：{chosen.DisplayName}（{member.DisplayName}）");
                }
            }
        }

        public void OnBattleFinished(BattleState state)
        {
            if (state == null)
                return;

            if (_run.MiracleLeafUsesRemaining >= 0)
                _run.MiracleLeafUsesRemaining = state.MiracleLeafRevivesRemaining;

            // CaptureParty 必须在 Clear 之前调用：previousParty 若与 _run.Party 为同一列表，
            // 先 Clear 会导致 existingParty 为空，BonusCards / 收藏进度等远征卡组数据全部丢失。
            var capturedParty = ExpeditionBattleConfigBuilder.CaptureParty(state, _run.Party);
            _run.Party.Clear();
            _run.Party.AddRange(capturedParty);
            ExpeditionPartyRules.EnforceMaxSize(_run.Party);
            CampCollectionProgress.SyncRunFromParty(_run);
            foreach (var member in _run.Party)
                CampCollectionProgress.SyncMemberFromRun(_run, member);
            TalentDatabase.SyncRunStateFromBattle(state, _run.TalentRun);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);

            if (state.Outcome == BattleOutcome.PlayerVictory)
                ApplyPostBattleRelicHeals(_run);

            if (_run.MiracleLeafUsesRemaining >= 0)
                _run.MiracleLeafUsesRemaining = state.MiracleLeafRevivesRemaining;

            if (state.Outcome == BattleOutcome.PlayerDefeat)
            {
                _run.Phase = ExpeditionPhase.RunFailed;
                _run.PendingRoutes.Clear();
                _run.PendingRewardPickup = null;
                _run.PendingEventBattleKey = "";
                _run.PendingEventBattleVictoryReward = null;
                return;
            }

            if (state.Outcome != BattleOutcome.PlayerVictory)
                return;

            _run.BattlesWon++;
            CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return;

            _run.LastXpReward = RollCombatXp();
            ExpeditionBattleConfigBuilder.GrantXpToPool(_run, _run.LastXpReward);

            if (!string.IsNullOrEmpty(_run.PendingEventBattleKey))
            {
                var eventReward = _run.PendingEventBattleVictoryReward;
                var bonusXp = _run.PendingEventBattleBonusXp;
                _run.PendingEventBattleKey = "";
                _run.PendingEventBattleBonusXp = 0;
                _run.PendingEventBattleVictoryReward = null;

                if (bonusXp > 0)
                {
                    eventReward ??= new ExpeditionRewardPickup
                    {
                        HeaderText = "事件战利品",
                        Kind = RewardPickupKind.EventOrShrine
                    };
                    eventReward.GrantXp += bonusXp;
                }

                if (eventReward != null && eventReward.HasAnyReward)
                {
                    _run.PendingRewardPickup = eventReward;
                    _run.LastGoldReward = eventReward.Gold;
                    _run.Phase = ExpeditionPhase.RewardPickup;
                    return;
                }

                LoadRoutesForNextLayer();
                _run.Phase = ExpeditionPhase.RouteSelect;
                return;
            }

            _run.PendingRewardPickup = ExpeditionRewardRoller.RollVictoryRewards(_config, _run, _rng);
            _run.LastGoldReward = _run.PendingRewardPickup.Gold;
            _run.Phase = ExpeditionPhase.RewardPickup;
        }

        public bool TryClaimRewardGold()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasGold ||
                rewards.GoldClaimed || rewards.GoldSkipped)
                return false;

            rewards.GoldClaimed = true;
            _run.Gold += rewards.Gold;
            if (rewards.EnableDivinePunishment)
                _run.Modifiers.DivinePunishmentActive = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TrySkipRewardGold()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasGold ||
                rewards.GoldClaimed || rewards.GoldSkipped)
                return false;

            rewards.GoldSkipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TryClaimRewardRelic()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasRelic ||
                rewards.RelicClaimed || rewards.RelicSkipped)
                return false;

            if (rewards.HasRelicEvolution)
            {
                if (!_run.Relics.Contains(rewards.RelicEvolveFromId))
                {
                    rewards.RelicSkipped = true;
                    TryAdvanceFromRewardPickup();
                    return false;
                }

                _run.Relics.Remove(rewards.RelicEvolveFromId);
                RelicGrowthRules.TransferGrowthTiers(
                    _run.RelicGrowthTiers,
                    rewards.RelicEvolveFromId,
                    rewards.RelicEvolveToId);
            }

            if (!TryAddRelic(rewards.RelicId))
            {
                rewards.RelicSkipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            rewards.RelicClaimed = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TrySkipRewardRelic()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasRelic ||
                rewards.RelicClaimed || rewards.RelicSkipped)
                return false;

            rewards.RelicSkipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TryClaimRewardCard()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasCard ||
                rewards.CardClaimed || rewards.CardSkipped)
                return false;

            if (_run.PendingCardOffer != null)
                return false;

            var template = BuildCardTemplateForGrant(
                rewards.CardOwnerCharacterId,
                rewards.CardDefinitionId,
                rewards.CardDisplayName);
            if (template == null)
            {
                rewards.CardSkipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            if (!TryFindPartyMember(rewards.CardOwnerCharacterId, out var member) && _run.Party.Count > 0)
                member = _run.Party[0];

            if (member == null)
            {
                rewards.CardSkipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            var result = ExpeditionRunDeckRules.TryOfferCard(
                _config,
                _run,
                member,
                template,
                ExpeditionCardOfferContext.RewardPickup,
                RecordRunAcquisition);

            if (result == CardGrantResult.Failed)
            {
                rewards.CardSkipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            if (result == CardGrantResult.Added)
            {
                rewards.CardClaimed = true;
                TryAdvanceFromRewardPickup();
            }

            return true;
        }

        public bool TrySkipRewardCard()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasCard ||
                rewards.CardClaimed || rewards.CardSkipped)
                return false;

            if (_run.PendingCardOffer?.Context == ExpeditionCardOfferContext.RewardPickup)
                _run.PendingCardOffer = null;

            rewards.CardSkipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TryClaimRewardConsumable()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasConsumable ||
                rewards.ConsumableClaimed || rewards.ConsumableSkipped)
                return false;

            var count = System.Math.Max(1, rewards.ConsumableCount);
            if (count == 1)
            {
                if (ConsumableInventory.TryAdd(_run.ConsumableSlots, rewards.ConsumableId, out var inventoryFull))
                {
                    rewards.ConsumableClaimed = true;
                    TryAdvanceFromRewardPickup();
                    return true;
                }

                if (inventoryFull)
                {
                    _run.PendingConsumableOfferId = rewards.ConsumableId;
                    return true;
                }
            }
            else if (ConsumableInventory.TryAddMany(
                         _run.ConsumableSlots,
                         rewards.ConsumableId,
                         count,
                         out var pendingOfferId))
            {
                rewards.ConsumableClaimed = true;
                TryAdvanceFromRewardPickup();
                return true;
            }
            else if (!string.IsNullOrEmpty(pendingOfferId))
            {
                _run.PendingConsumableOfferId = pendingOfferId;
                return true;
            }

            rewards.ConsumableSkipped = true;
            TryAdvanceFromRewardPickup();
            return false;
        }

        public bool TrySkipRewardConsumable()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasConsumable ||
                rewards.ConsumableClaimed || rewards.ConsumableSkipped)
                return false;

            rewards.ConsumableSkipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TryClaimRewardStat()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasStatBonus ||
                rewards.StatClaimed || rewards.StatSkipped)
                return false;

            ApplyStatReward(rewards);
            rewards.StatClaimed = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TrySkipRewardStat()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasStatBonus ||
                rewards.StatClaimed || rewards.StatSkipped)
                return false;

            rewards.StatSkipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        void ApplyStatReward(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return;

            if (rewards.TeamAttackBonus != 0)
                _run.Modifiers.TeamAttackBonus += rewards.TeamAttackBonus;
            if (rewards.TeamDefenseBonus != 0)
                _run.Modifiers.TeamDefenseBonus += rewards.TeamDefenseBonus;
            if (rewards.EnergyCapBonus != 0)
                _run.Modifiers.EnergyCapBonus += rewards.EnergyCapBonus;
            if (rewards.EnableSoulRiftBattleStartRandomHpLoss)
                _run.Modifiers.SoulRiftBattleStartRandomHpLoss = System.Math.Max(
                    _run.Modifiers.SoulRiftBattleStartRandomHpLoss,
                    5);
            if (rewards.EnableDivinePunishment)
                _run.Modifiers.DivinePunishmentActive = true;

            if (rewards.PersonalAttackBonus != 0)
            {
                var characterId = rewards.StatCharacterId;
                if (string.IsNullOrEmpty(characterId) && _run.Party.Count > 0)
                    characterId = _run.Party[0].CharacterDefinitionId;

                if (TryFindPartyMember(characterId, out var member))
                    member.PersonalAttackBonus += rewards.PersonalAttackBonus;
            }

            if (rewards.GrantXp > 0)
                ExpeditionBattleConfigBuilder.GrantXpToPool(_run, rewards.GrantXp);

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
        }

        public bool TryClaimVictoryGold() => TryClaimRewardGold();

        public bool TrySkipVictoryOptionalRewards() => TrySkipAllRemainingRewards();

        public bool TrySkipAllRemainingRewards()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null)
                return false;

            if (rewards.HasGold && !rewards.GoldClaimed && !rewards.GoldSkipped)
                TrySkipRewardGold();
            if (rewards.HasRelic && !rewards.RelicClaimed && !rewards.RelicSkipped)
                TrySkipRewardRelic();
            if (rewards.HasCard && !rewards.CardClaimed && !rewards.CardSkipped)
                TrySkipRewardCard();
            if (rewards.HasConsumable && !rewards.ConsumableClaimed && !rewards.ConsumableSkipped)
                TrySkipRewardConsumable();
            if (rewards.HasStatBonus && !rewards.StatClaimed && !rewards.StatSkipped)
                TrySkipRewardStat();

            return _run.Phase == ExpeditionPhase.RouteSelect || _run.Phase == ExpeditionPhase.RunComplete;
        }

        public bool TryClaimVictoryRelic() => TryClaimRewardRelic();

        public bool TryClaimVictoryCard() => TryClaimRewardCard();

        public bool TryClaimChestGold() => TryClaimRewardGold();

        public bool TryClaimChestRelic() => TryClaimRewardRelic();

        public bool TrySelectRoute(int routeIndex)
        {
            if (_run.Phase != ExpeditionPhase.RouteSelect)
                return false;

            if (routeIndex < 0 || routeIndex >= _run.PendingRoutes.Count)
                return false;

            var route = _run.PendingRoutes[routeIndex];
            _run.PendingRoutes.Clear();
            RecordRouteChoice(route);

            switch (route.NodeType)
            {
                case ExpeditionNodeType.Treasure:
                    _run.PendingRewardPickup = ExpeditionRewardRoller.RollChestReward(_config, _run, _rng);
                    _run.Phase = ExpeditionPhase.RewardPickup;
                    return true;
                case ExpeditionNodeType.Event:
                    _run.PendingEvent = new ExpeditionPendingEvent
                    {
                        EventId = ExpeditionEventRoller.ResolveEventForVisit(_run, route.EventId, _rng),
                        SourceLayer = route.LayerNumber
                    };
                    _run.Phase = ExpeditionPhase.EventChoice;
                    return true;
                case ExpeditionNodeType.Shrine:
                    _run.CardAltar = new ExpeditionCardAltarState { SourceLayer = route.LayerNumber };
                    _run.PendingShrine = null;
                    _run.Phase = ExpeditionPhase.ShrineChoice;
                    _run.LastEventMessage = "祭坛 — 选择服务";
                    return true;
                case ExpeditionNodeType.Shop:
                    _run.Phase = ExpeditionPhase.ShopVisit;
                    ExpeditionShopRoller.OpenShop(_run.Shop, _config, _run, _rng);
                    return true;
                case ExpeditionNodeType.Boss:
                    RecordLastBattleContext(route.LayerNumber, false, true);
                    _run.Phase = ExpeditionPhase.InBattle;
                    _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
                    return true;
                default:
                    RecordLastBattleContext(route.LayerNumber, route.IsElite, false);
                    _run.Phase = ExpeditionPhase.InBattle;
                    _run.CurrentBattleConfig = BuildBattleFromEncounter(
                        route.EncounterIndex,
                        applyPartyHp: true,
                        route.MonsterEncounterId,
                        route.IsElite,
                        route.LayerNumber);
                    return true;
            }
        }

        public bool TryResolveEventChoice(int choiceIndex)
        {
            if (_run.Phase != ExpeditionPhase.EventChoice || _run.PendingEvent == null)
                return false;

            if (!ExpeditionEventCatalog.TryGet(_run.PendingEvent.EventId, out var definition))
                return false;

            if (choiceIndex < 0 || choiceIndex >= definition.Choices.Count)
                return false;

            var choice = definition.Choices[choiceIndex];
            var eventId = _run.PendingEvent.EventId;
            var aftermathText = choice.AfterChoiceText;
            int? fixedRoll = null;

            if (ExpeditionEventAftermathText.NeedsStochasticRoll(eventId, choiceIndex))
            {
                fixedRoll = _rng.NextIndex(100);
                aftermathText = ExpeditionEventAftermathText.Resolve(eventId, choiceIndex, fixedRoll.Value);
            }

            if (!string.IsNullOrEmpty(aftermathText))
            {
                _run.PendingEventAftermath = new ExpeditionPendingEventAftermath
                {
                    EventId = eventId,
                    ChoiceIndex = choiceIndex,
                    SourceLayer = _run.PendingEvent.SourceLayer,
                    AfterChoiceText = aftermathText,
                    FixedRoll100 = fixedRoll
                };
                _run.PendingEvent = null;
                _run.Phase = ExpeditionPhase.EventAftermath;
                return true;
            }

            return ApplyResolvedEventChoice(eventId, choiceIndex, _run.PendingEvent.SourceLayer);
        }

        public bool TryConfirmEventAftermath()
        {
            if (_run.Phase != ExpeditionPhase.EventAftermath || _run.PendingEventAftermath == null)
                return false;

            var pending = _run.PendingEventAftermath;
            _run.PendingEventAftermath = null;
            _run.EventResolutionFixedRoll100 = null;
            return ApplyResolvedEventChoice(
                pending.EventId,
                pending.ChoiceIndex,
                pending.SourceLayer,
                pending.FixedRoll100);
        }

        bool ApplyResolvedEventChoice(
            string eventId,
            int choiceIndex,
            int sourceLayer,
            int? fixedRoll100 = null)
        {
            _run.EventResolutionFixedRoll100 = fixedRoll100;
            try
            {
                return ApplyResolvedEventChoiceCore(eventId, choiceIndex);
            }
            finally
            {
                _run.EventResolutionFixedRoll100 = null;
            }
        }

        bool ApplyResolvedEventChoiceCore(string eventId, int choiceIndex)
        {
            var outcome = ExpeditionEventResolver.ResolveChoice(
                _run, _config, eventId, choiceIndex, _rng);
            _run.LastEventMessage = outcome.Message;
            _run.PendingEvent = null;

            if (outcome.InteractionSteps.Count > 0)
            {
                var interaction = new ExpeditionEventInteractionState
                {
                    EventId = eventId,
                    ChoiceIndex = choiceIndex,
                    DeferredOutcome = outcome.DeferredOutcome,
                    DeferredRunAction = outcome.DeferredRunAction
                };
                interaction.Steps.AddRange(outcome.InteractionSteps);
                _run.EventInteraction = interaction;
                _run.Phase = ExpeditionPhase.EventInteraction;
                return true;
            }

            return ApplyEventOutcome(outcome);
        }

        public bool TryResolveShrineChoice(int choiceIndex)
        {
            return TryLeaveAltar();
        }

        public void SetCardAltarMemberDraft(string memberId, int collectionIndex, string replaceDeckCardKey)
        {
            if (_run.CardAltar == null || string.IsNullOrEmpty(memberId))
                return;

            var draft = _run.CardAltar.GetOrCreateDraft(memberId);
            draft.CollectionCardIndex = collectionIndex;

            if (TryFindPartyMember(memberId, out var member)
                && ExpeditionRunDeckRules.NeedsReplace(_config, member))
            {
                draft.ReplaceDeckCardKey = replaceDeckCardKey ?? "";
            }
            else
            {
                draft.ReplaceDeckCardKey = "";
            }
        }

        public bool TryLeaveAltar()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            return FinishAltarVisit("你离开了祭坛。");
        }

        public bool TryUpgradeAltarMemberHp(string memberId)
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            var member = FindPartyMember(memberId);
            if (member == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryUpgradeMemberHp(_run, member))
                return false;

            _run.LastEventMessage = $"{member.DisplayName} 最大 HP +{ExpeditionAltarUpgradeRules.HpPlus5Amount}";
            return true;
        }

        public bool TryUpgradeAltarEnergyCap()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryUpgradeEnergyCap(_run))
                return false;

            _run.LastEventMessage = "能量上限 +1";
            return true;
        }

        public bool TryUpgradeAltarHandLimit()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryUpgradeHandLimit(_run))
                return false;

            _run.LastEventMessage = "手牌上限 +1";
            return true;
        }

        public bool TryUpgradeAltarCard(string memberId, string deckInstanceId, string displayName)
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            var member = FindPartyMember(memberId);
            if (member == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryUpgradeMemberCard(_run, member, deckInstanceId, displayName))
                return false;

            _run.LastEventMessage = $"{displayName} 已强化";
            return true;
        }

        public int GetAltarBaseEnergyCap()
        {
            if (_config.CombatEncounters.Count > 0 && _config.CombatEncounters[0] != null)
                return _config.CombatEncounters[0].EnergyCap;

            return 8;
        }

        public int GetAltarBaseHandLimit()
        {
            if (_config.CombatEncounters.Count > 0 && _config.CombatEncounters[0] != null)
                return _config.CombatEncounters[0].HandLimit;

            return 8;
        }

        bool FinishAltarVisit(string message)
        {
            _run.CardAltar = null;
            _run.LastEventMessage = message;
            CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return true;

            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        PartyMemberSnapshot FindPartyMember(string memberId)
        {
            if (string.IsNullOrEmpty(memberId))
                return null;

            foreach (var member in _run.Party)
            {
                if (member != null && member.CharacterDefinitionId == memberId)
                    return member;
            }

            return null;
        }

        public bool TryConfirmCardAltar()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            var pending = new List<(PartyMemberSnapshot member, ExpeditionCardAltarMemberDraft draft)>();
            foreach (var member in _run.Party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                if (!_run.CardAltar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft) || !draft.HasSelection)
                    continue;

                if (CampCollectionProgress.IsExtracted(_run, member.CharacterDefinitionId, draft.CollectionCardIndex))
                {
                    draft.CollectionCardIndex = -1;
                    draft.ReplaceDeckCardKey = "";
                    continue;
                }

                if (!TryValidateCardAltarExtraction(member, draft, out var error))
                {
                    _run.LastEventMessage = error;
                    return false;
                }

                pending.Add((member, draft));
            }

            foreach (var (member, draft) in pending)
                ApplyCardAltarExtraction(member, draft);

            return FinishAltarVisit("已完成祭坛召唤。");
        }

        bool TryValidateCardAltarExtraction(
            PartyMemberSnapshot member,
            ExpeditionCardAltarMemberDraft draft,
            out string error)
        {
            error = "";
            if (CampCollectionProgress.IsExtracted(_run, member.CharacterDefinitionId, draft.CollectionCardIndex))
            {
                error = $"{member.DisplayName} 的该收藏牌已被取出。";
                return false;
            }

            var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(
                _config,
                _run,
                member,
                draft.CollectionCardIndex);
            if (template == null)
            {
                error = "无法解析收藏卡牌。";
                return false;
            }

            if (ExpeditionRunDeckRules.NeedsReplace(_config, member))
            {
                if (string.IsNullOrEmpty(draft.ReplaceDeckCardKey))
                {
                    error = $"{member.DisplayName} 卡组已满，请先选择要替换的卡牌。";
                    return false;
                }

                if (!ExpeditionRunDeckRules.TryFindMemberDeckEntryByKey(
                        _config,
                        member,
                        draft.ReplaceDeckCardKey,
                        out _))
                {
                    error = $"{member.DisplayName} 替换目标无效，请重新选择。";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(draft.ReplaceDeckCardKey))
            {
                draft.ReplaceDeckCardKey = "";
            }

            return true;
        }

        void ApplyCardAltarExtraction(PartyMemberSnapshot member, ExpeditionCardAltarMemberDraft draft)
        {
            var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(
                _config,
                _run,
                member,
                draft.CollectionCardIndex);
            if (template == null)
                return;

            template = ExpeditionBattleConfigBuilder.CloneTemplate(template);
            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, _config.PlayerCardCatalog);
            if (string.IsNullOrEmpty(template.OwnerCharacterId))
                template.OwnerCharacterId = member.CharacterDefinitionId;

            if (ExpeditionRunDeckRules.NeedsReplace(_config, member))
            {
                ExpeditionRunDeckRules.TryFindMemberDeckEntryByKey(
                    _config,
                    member,
                    draft.ReplaceDeckCardKey,
                    out var removeEntry);
                if (!ExpeditionRunDeckRules.TryReplaceAndAdd(_run, member, removeEntry, template))
                    return;
            }
            else
            {
                ExpeditionDeckInstanceRules.PrepareNewDeckCard(member, template);
                member.BonusCards.Add(template);
            }

            CampCollectionProgress.MarkExtracted(_run, member.CharacterDefinitionId, draft.CollectionCardIndex);
            member.ExtractedCampCardIndices.Add(draft.CollectionCardIndex);
            RecordRunAcquisition($"祭坛召唤：{template.DisplayName}（{member.DisplayName}）");
        }

        bool TryApplyCardAltarExtraction(
            PartyMemberSnapshot member,
            ExpeditionCardAltarMemberDraft draft,
            out string error)
        {
            if (!TryValidateCardAltarExtraction(member, draft, out error))
                return false;

            ApplyCardAltarExtraction(member, draft);
            return true;
        }

        public bool TryReplaceDeckCardForPendingOffer(string deckCardKey)
        {
            var offer = _run.PendingCardOffer;
            if (offer?.Template == null)
                return false;

            if (!TryFindPartyMember(offer.OwnerCharacterId, out var member) && _run.Party.Count > 0)
                member = _run.Party[0];

            if (member == null)
                return false;

            if (ExpeditionRunDeckRules.CanAddWithoutReplace(_config, member))
            {
                var clone = ExpeditionBattleConfigBuilder.CloneTemplate(offer.Template);
                ExpeditionDeckInstanceRules.PrepareNewDeckCard(member, clone);
                member.BonusCards.Add(clone);
            }
            else
            {
                if (!ExpeditionRunDeckRules.TryFindMemberDeckEntryByKey(_config, member, deckCardKey, out var entry)
                    || !ExpeditionRunDeckRules.TryReplaceAndAdd(_run, member, entry, offer.Template))
                {
                    return false;
                }
            }

            var cardName = offer.Template.DisplayName;
            var context = offer.Context;
            _run.PendingCardOffer = null;
            RecordRunAcquisition($"获得卡牌：{cardName}（{member.DisplayName}）");

            if (context == ExpeditionCardOfferContext.RewardPickup && _run.PendingRewardPickup != null)
            {
                _run.PendingRewardPickup.CardClaimed = true;
                TryAdvanceFromRewardPickup();
            }

            _run.LastEventMessage = $"已将 {cardName} 加入 {member.DisplayName} 的卡组。";
            return true;
        }

        public bool TryAbandonPendingCardOffer()
        {
            if (_run.PendingCardOffer == null)
                return false;

            var context = _run.PendingCardOffer.Context;
            _run.PendingCardOffer = null;

            if (context == ExpeditionCardOfferContext.RewardPickup && _run.PendingRewardPickup != null)
            {
                _run.PendingRewardPickup.CardSkipped = true;
                TryAdvanceFromRewardPickup();
            }

            _run.LastEventMessage = context switch
            {
                ExpeditionCardOfferContext.Shop => "已放弃购买的卡牌。",
                ExpeditionCardOfferContext.Event => "已放弃获得的卡牌。",
                _ => "已放弃新卡牌。"
            };
            return true;
        }

        public bool TryBuyShopOffer(int slotIndex)
        {
            if (_run.Phase != ExpeditionPhase.ShopVisit)
                return false;

            if (slotIndex < 0 || slotIndex >= _run.Shop.Offers.Count)
                return false;

            var offer = _run.Shop.Offers[slotIndex];
            if (offer.Sold)
                return false;

            if (_run.Gold < offer.Price)
            {
                _run.LastEventMessage = "金币不足。";
                return false;
            }

            if (!TryFulfillShopOffer(offer, out var message))
            {
                _run.LastEventMessage = message;
                return false;
            }

            _run.Gold -= offer.Price;
            offer.Sold = true;
            _run.LastEventMessage = message;
            return true;
        }

        public bool TryRefreshShop()
        {
            if (_run.Phase != ExpeditionPhase.ShopVisit)
                return false;

            var cost = _run.Shop.NextRefreshCost;
            if (_run.Gold < cost)
            {
                _run.LastEventMessage = "金币不足以刷新商品。";
                return false;
            }

            _run.Gold -= cost;
            ExpeditionShopRoller.RefreshStock(_run.Shop, _config, _run, _rng);
            _run.LastEventMessage = $"已刷新商品（-{cost} 金币）。";
            return true;
        }

        bool TryFulfillShopOffer(ShopOffer offer, out string message)
        {
            message = "";
            switch (offer.Kind)
            {
                case ShopOfferKind.Card:
                {
                    var template = BuildCardTemplateForGrant(
                        offer.CardOwnerCharacterId,
                        offer.CardDefinitionId,
                        offer.CardDisplayName);
                    if (template == null)
                    {
                        message = "无法加入该卡牌。";
                        return false;
                    }

                    if (!TryFindPartyMember(offer.CardOwnerCharacterId, out var member) && _run.Party.Count > 0)
                        member = _run.Party[0];

                    if (member == null)
                    {
                        message = "无法加入该卡牌。";
                        return false;
                    }

                    var result = ExpeditionRunDeckRules.TryOfferCard(
                        _config,
                        _run,
                        member,
                        template,
                        ExpeditionCardOfferContext.Shop,
                        RecordRunAcquisition);

                    if (result == CardGrantResult.Failed)
                    {
                        message = "无法加入该卡牌。";
                        return false;
                    }

                    message = result == CardGrantResult.PendingReplace
                        ? $"购买卡牌：{offer.CardDisplayName}（卡组已满，请选择要替换的卡牌）"
                        : $"购买卡牌：{offer.CardDisplayName}（-{offer.Price} 金币）";
                    return true;
                }

                case ShopOfferKind.Consumable:
                    if (!ConsumableInventory.TryAdd(_run.ConsumableSlots, offer.ConsumableId, out var inventoryFull))
                    {
                        message = "无法获得该消耗品。";
                        return false;
                    }

                    if (inventoryFull)
                        _run.PendingConsumableOfferId = offer.ConsumableId;

                    var suffix = inventoryFull ? "（栏位已满，请选择替换或放弃）" : "";
                    message = $"购买 {offer.ConsumableDisplayName}（-{offer.Price} 金币）{suffix}";
                    return true;

                case ShopOfferKind.Relic:
                    if (!TryAddRelic(offer.RelicId))
                    {
                        message = "无法获得该遗物。";
                        return false;
                    }

                    message = $"购买遗物：{offer.RelicDisplayName}（-{offer.Price} 金币）";
                    return true;

                default:
                    message = "无效商品。";
                    return false;
            }
        }

        public bool TryLeaveShop()
        {
            if (_run.Phase != ExpeditionPhase.ShopVisit)
                return false;

            _run.LastEventMessage = "你离开了商店。";
            _run.Shop.Clear();
            CompleteCurrentNode();
            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        public bool TryReplaceConsumableSlot(int slotIndex)
        {
            if (string.IsNullOrEmpty(_run.PendingConsumableOfferId))
                return false;

            if (slotIndex < 0 || slotIndex >= ConsumableInventory.MaxSlots)
                return false;

            ConsumableInventory.ReplaceAt(_run.ConsumableSlots, slotIndex, _run.PendingConsumableOfferId);
            ResolvePendingConsumableOffer(claimed: true);
            return true;
        }

        public bool TryAbandonConsumableOffer()
        {
            if (string.IsNullOrEmpty(_run.PendingConsumableOfferId))
                return false;

            ResolvePendingConsumableOffer(claimed: false);
            return true;
        }

        void ResolvePendingConsumableOffer(bool claimed)
        {
            var offerId = _run.PendingConsumableOfferId;
            _run.PendingConsumableOfferId = "";
            _run.PendingCardOffer = null;

            var rewards = _run.PendingRewardPickup;
            if (rewards == null || !rewards.HasConsumable || rewards.ConsumableId != offerId)
                return;

            if (claimed)
                rewards.ConsumableClaimed = true;
            else
                rewards.ConsumableSkipped = true;

            TryAdvanceFromRewardPickup();
        }

        public bool TryAddRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId) || !RelicDatabase.TryGet(relicId, out _))
                return false;

            if (_run.Relics.Contains(relicId))
                return false;

            _run.Relics.Add(relicId);
            RelicGrowthRules.OnRelicAcquired(_run.RelicGrowthTiers, relicId, ResolveRelicAcquisitionFloor());

            if (RelicDatabase.TryGet(relicId, out var relic))
                RecordRunAcquisition($"获得遗物：{relic.DisplayName}");

            if (relicId == RelicIds.LeafOfMiracle && _run.MiracleLeafUsesRemaining < 0)
                _run.MiracleLeafUsesRemaining = 2;

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
            return true;
        }

        int ResolveRelicAcquisitionFloor()
        {
            if (_run.PendingEventAftermath?.SourceLayer > 0)
                return _run.PendingEventAftermath.SourceLayer;

            if (_run.Phase == ExpeditionPhase.RewardPickup && _run.Map != null)
                return System.Math.Max(1, _run.Map.NodesCompleted);

            return System.Math.Max(1, (_run.Map?.NodesCompleted ?? 0) + 1);
        }

        void RecordLastBattleContext(int floor, bool isElite, bool isBoss)
        {
            _run.LastBattleFloor = System.Math.Max(1, floor);
            _run.LastBattleWasElite = isElite;
            _run.LastBattleWasBoss = isBoss;
        }

        int RollCombatXp() =>
            CombatXpRules.Roll(_rng, _run.LastBattleFloor, _run.LastBattleWasElite, _run.LastBattleWasBoss);

        public int CurrentBattleNumber => _run.Map?.NodesCompleted + 1 ?? _run.BattlesWon + 1;

        void InitPartyFromTemplate(CampMetaState campMeta = null)
        {
            if (_config.CombatEncounters.Count == 0)
                return;

            campMeta ??= CampMetaState.CreateDefaultDemo();
            foreach (var cc in _config.CombatEncounters[0].Combatants)
            {
                if (cc.Team != TeamSide.Player)
                    continue;

                if (_run.Party.Count >= CampRosterState.PartySize)
                    break;

                var stats = CharacterProgression.GetStatsForCharacter(cc.CharacterDefinitionId, cc.Level);
                var member = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    DisplayName = cc.DisplayName,
                    Level = CharacterProgression.ClampLevel(cc.Level),
                    Xp = cc.Xp,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                };
                CampRunPartyApplier.ApplyTalentsFromMeta(member, campMeta);
                _run.Party.Add(member);
            }

            ExpeditionPartyRules.EnforceMaxSize(_run.Party);
        }

        void RecordRouteChoice(ExpeditionRouteOption route)
        {
            var layer = _run.Map?.GetLayer(route.LayerNumber);
            if (layer != null)
                layer.ChosenOptionIndex = route.MapOptionIndex;
        }

        void CompleteCurrentNode()
        {
            if (_run.Map == null)
                return;

            _run.Map.NodesCompleted++;

            RelicGrowthRules.SyncFloorGrowth(_run.RelicGrowthTiers, _run.Relics, _run.Map.NodesCompleted);

            if (_run.Map.NodesCompleted >= _run.Map.ChapterLayerCount)
            {
                _run.Phase = ExpeditionPhase.RunComplete;
                _run.PendingRoutes.Clear();
                return;
            }

            if (_run.Modifiers.SkipNextRouteSelect)
            {
                _run.Modifiers.SkipNextRouteSelect = false;
                _run.Map.NodesCompleted++;
                if (_run.Map.NodesCompleted >= _run.Map.ChapterLayerCount)
                {
                    _run.Phase = ExpeditionPhase.RunComplete;
                    _run.PendingRoutes.Clear();
                }
            }
        }

        void LoadRoutesForNextLayer()
        {
            _run.PendingRoutes.Clear();
            if (_run.Map == null || _run.Phase == ExpeditionPhase.RunComplete)
                return;

            var nextLayerNumber = _run.Map.NodesCompleted + 1;
            var layer = _run.Map.GetLayer(nextLayerNumber);
            if (layer == null)
                return;

            if (nextLayerNumber == _run.Map.ChapterLayerCount - 1 && _run.Map.NodesCompleted == _run.Map.ChapterLayerCount - 2)
                _run.LastEventMessage = "Boss 在前方。";

            for (var i = 0; i < layer.Options.Count; i++)
                _run.PendingRoutes.Add(ToRouteOption(layer, layer.Options[i], i));
        }

        static ExpeditionRouteOption ToRouteOption(ExpeditionMapLayer layer, ExpeditionMapOption option, int index) =>
            new()
            {
                Id = $"layer_{layer.LayerNumber}_{index}",
                DisplayName = option.DisplayName,
                Description = option.Description,
                NodeType = option.NodeType,
                EncounterIndex = option.EncounterIndex,
                MonsterEncounterId = option.MonsterEncounterId,
                EventId = option.EventId,
                ShrineId = option.ShrineId,
                TreasureTier = option.TreasureTier,
                IsElite = option.IsElite,
                LayerNumber = layer.LayerNumber,
                MapOptionIndex = index,
                PathSpriteIndex = option.PathSpriteIndex
            };

        bool TryEnterRewardPickupPhase(ExpeditionRewardPickup pickup)
        {
            if (pickup == null || !pickup.HasAnyReward)
                return false;

            _run.PendingRewardPickup = pickup;
            _run.Phase = ExpeditionPhase.RewardPickup;
            return true;
        }

        void TryAdvanceFromRewardPickup()
        {
            if (_run.Phase != ExpeditionPhase.RewardPickup)
                return;

            var rewards = _run.PendingRewardPickup;
            if (rewards != null && !rewards.IsFullyResolved)
                return;

            if (!string.IsNullOrEmpty(_run.PendingConsumableOfferId))
                return;

            if (_run.PendingCardOffer != null)
                return;

            var kind = rewards?.Kind ?? RewardPickupKind.EventOrShrine;
            _run.PendingRewardPickup = null;

            if (kind == RewardPickupKind.Chest)
            {
                CompleteCurrentNode();
                if (_run.Phase == ExpeditionPhase.RunComplete)
                    return;
            }

            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
        }

        CardTemplate BuildCardTemplateForGrant(string ownerCharacterId, string definitionId, string displayName)
        {
            if (string.IsNullOrEmpty(definitionId))
                return null;

            var template = FindCardTemplate(definitionId);
            if (template == null)
            {
                template = new CardTemplate
                {
                    DefinitionId = definitionId,
                    DisplayName = string.IsNullOrEmpty(displayName) ? definitionId : displayName,
                    OwnerCharacterId = ownerCharacterId ?? ""
                };
            }
            else
            {
                template = ExpeditionBattleConfigBuilder.CloneTemplate(template);
            }

            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, _config.PlayerCardCatalog);

            if (string.IsNullOrEmpty(template.OwnerCharacterId) && !string.IsNullOrEmpty(ownerCharacterId))
                template.OwnerCharacterId = ownerCharacterId;

            return template;
        }

        void RecordRunAcquisition(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            _run.RunAcquisitionLog.Add(line);
            while (_run.RunAcquisitionLog.Count > 40)
                _run.RunAcquisitionLog.RemoveAt(0);
        }

        CardTemplate FindCardTemplate(string definitionId)
        {
            foreach (var template in _config.PlayerCardCatalog)
            {
                if (template.DefinitionId == definitionId)
                    return template;
            }

            foreach (var encounter in _config.CombatEncounters)
            {
                foreach (var cc in encounter.Combatants)
                {
                    foreach (var template in cc.DeckTemplates)
                    {
                        if (template.DefinitionId == definitionId)
                            return template;
                    }
                }
            }

            return null;
        }

        bool ApplyEventOutcome(ExpeditionEventOutcome outcome)
        {
            if (outcome == null)
                return false;

            if (!string.IsNullOrEmpty(outcome.EventBattleKey))
                _run.PendingEventBattleKey = outcome.EventBattleKey;

            if (outcome.StartsCombat)
            {
                RecordLastBattleContext(CurrentBattleNumber, isElite: false, isBoss: false);
                _run.Phase = ExpeditionPhase.InBattle;
                _run.CurrentBattleConfig = BuildBattleFromEncounter(outcome.CombatEncounterIndex, applyPartyHp: true);
                return true;
            }

            if (outcome.AdvanceNode)
                CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return true;

            if (TryEnterRewardPickupPhase(outcome.PendingRewardPickup))
                return true;

            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        BattleConfig BuildBattleFromEncounter(
            int encounterIndex,
            bool applyPartyHp,
            string monsterEncounterId = "",
            bool isElite = false,
            int battleLayer = 0)
        {
            if (_config.CombatEncounters.Count == 0)
                throw new System.InvalidOperationException("ExpeditionConfig.CombatEncounters is empty.");

            if (_config.MonsterTemplates.Count == 0)
                throw new System.InvalidOperationException(
                    "ExpeditionConfig.MonsterTemplates 为空。请在 ExpeditionSetup 配置 MonsterCharacters，" +
                    "或执行 Grimhand → Content → Generate Demo ScriptableObjects。");

            var standard = _config.CombatEncounters[encounterIndex % _config.CombatEncounters.Count];
            var floor = battleLayer > 0 ? battleLayer : CurrentBattleNumber;
            BattleConfig template;
            if (_run.PendingEventBattleKey == MirrorPhantomEncounterBuilder.BattleKey)
                template = MirrorPhantomEncounterBuilder.BuildMirrorBattle(standard, _run.Party);
            else if (_run.PendingEventBattleKey == AdventurerRevengeEncounterBuilder.BattleKey)
                template = AdventurerRevengeEncounterBuilder.BuildRevengeBattle(standard);
            else if (_run.PendingEventBattleKey == AncientFurnaceEncounterBuilder.BattleKey)
            {
                var map = MonsterTemplateRegistry.BuildTemplateMap(_config);
                template = AncientFurnaceEncounterBuilder.BuildGolemBattle(standard, map);
            }
            else if (_run.PendingEventBattleKey == FelFlameAltarEncounterBuilder.BattleKey)
            {
                var map = MonsterTemplateRegistry.BuildTemplateMap(_config);
                template = FelFlameAltarEncounterBuilder.BuildEliteBattle(standard, floor, _rng, map);
            }
            else
            {
                var encounterId = string.IsNullOrEmpty(monsterEncounterId)
                    ? MonsterEncounterCatalog.Roll(floor, isElite, _rng)
                    : monsterEncounterId;
                var encounter = MonsterEncounterCatalog.GetById(encounterId)
                                ?? MonsterEncounterCatalog.GetById(
                                    MonsterEncounterCatalog.Roll(floor, isElite, _rng));
                if (encounter == null)
                    throw new System.InvalidOperationException(
                        $"无法解析怪物组合：layer={floor}, elite={isElite}, id={encounterId}");

                var map = MonsterTemplateRegistry.BuildTemplateMap(_config);
                template = MonsterEncounterBuilder.Build(standard, encounter, map);
            }

            var seed = _rng.NextInt(1, int.MaxValue);
            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                _run.Party,
                _run.Relics,
                seed,
                applyPartyHp,
                _run.MiracleLeafUsesRemaining,
                floor,
                _run.Modifiers,
                _config.PlayerCardCatalog,
                _config,
                _run.TalentRun,
                isBossBattle: false,
                _run.RelicGrowthTiers,
                _run.RunWideBonusCards);

            if (_run.Modifiers.DivinePunishmentActive)
            {
                foreach (var cc in config.Combatants)
                {
                    if (cc.Team != TeamSide.Enemy)
                        continue;

                    cc.BaseDefense = 20;
                }

                _run.Modifiers.DivinePunishmentActive = false;
            }

            config.EnergyCap += _run.Modifiers.EnergyCapBonus;
            config.HandLimit += _run.Modifiers.HandLimitBonus;
            config.TurnStartEnergyRegen = System.Math.Max(config.TurnStartEnergyRegen, 4);
            return config;
        }

        BattleConfig BuildBossBattle(bool applyPartyHp)
        {
            BattleConfig template;
            var bossFloor = CurrentBattleNumber;
            var isAbyssBoss = bossFloor >= ExpeditionRegionRules.AbyssBossLayer;
            var isDungeonBoss = !isAbyssBoss && bossFloor >= ExpeditionRegionRules.DungeonBossLayer;

            if (isAbyssBoss)
            {
                var standard = _config.CombatEncounters.Count > 0
                    ? _config.CombatEncounters[0]
                    : null;
                var templates = MonsterEncounterBuilder.BuildMonsterTemplateMap(_config.MonsterTemplates);
                template = AbyssBossEncounterBuilder.BuildTemplate(standard, templates);
                _run.CurrentBossDisplayName = AbyssBossEncounterBuilder.DisplayName;
            }
            else if (isDungeonBoss)
            {
                var standard = _config.CombatEncounters.Count > 0
                    ? _config.CombatEncounters[0]
                    : null;
                var templates = MonsterEncounterBuilder.BuildMonsterTemplateMap(_config.MonsterTemplates);
                template = StoneGolemBossEncounterBuilder.BuildTemplate(standard, templates);
                _run.CurrentBossDisplayName = StoneGolemBossEncounterBuilder.DisplayName;
            }
            else if (_config.BossEncounters.Count > 1)
            {
                template = _config.BossEncounters[_rng.NextIndex(_config.BossEncounters.Count)];
                _run.CurrentBossDisplayName = ResolveBossDisplayName(template);
            }
            else if (_config.BossEncounters.Count == 1)
            {
                template = _config.BossEncounters[0];
                _run.CurrentBossDisplayName = ResolveBossDisplayName(template);
            }
            else
            {
                var standard = _config.CombatEncounters.Count > 0
                    ? _config.CombatEncounters[0]
                    : null;
                template = Floor10BossEncounterBuilder.BuildRandomTemplate(standard, _rng);
                _run.CurrentBossDisplayName = ResolveBossDisplayName(template);
            }

            var seed = _rng.NextInt(1, int.MaxValue);
            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                _run.Party,
                _run.Relics,
                seed,
                applyPartyHp,
                _run.MiracleLeafUsesRemaining,
                CurrentBattleNumber,
                _run.Modifiers,
                _config.PlayerCardCatalog,
                _config,
                _run.TalentRun,
                isBossBattle: true,
                _run.RelicGrowthTiers,
                _run.RunWideBonusCards);

            config.EnergyCap += _run.Modifiers.EnergyCapBonus;
            config.HandLimit += _run.Modifiers.HandLimitBonus;
            config.TurnStartEnergyRegen = System.Math.Max(config.TurnStartEnergyRegen, 4);
            return config;
        }

        static string ResolveBossDisplayName(BattleConfig template)
        {
            if (template?.Combatants == null)
                return Floor10BossEncounterBuilder.SkeletonKingDisplayName;

            foreach (var cc in template.Combatants)
            {
                if (cc.Team != TeamSide.Enemy)
                    continue;

                if (cc.CharacterDefinitionId == GhostQueenBossEncounterBuilder.CharacterId)
                    return Floor10BossEncounterBuilder.GhostQueenDisplayName;

                if (cc.CharacterDefinitionId == StoneGolemBossEncounterBuilder.CharacterId)
                    return StoneGolemBossEncounterBuilder.DisplayName;

                if (cc.CharacterDefinitionId == "char_skeleton_king")
                    return Floor10BossEncounterBuilder.SkeletonKingDisplayName;

                if (!string.IsNullOrEmpty(cc.DisplayName))
                    return cc.DisplayName;
            }

            return Floor10BossEncounterBuilder.SkeletonKingDisplayName;
        }

        public bool CompleteEventInteractionStep(
            string selectedCharacterId = null,
            string selectedCardKey = null,
            string selectedSecondCardKey = null)
        {
            if (_run.Phase != ExpeditionPhase.EventInteraction || _run.EventInteraction == null)
                return false;

            var interaction = _run.EventInteraction;
            if (interaction.StepIndex < 0 || interaction.StepIndex >= interaction.Steps.Count)
                return false;

            var step = interaction.Steps[interaction.StepIndex];
            switch (step.Kind)
            {
                case ExpeditionEventStepKind.ShowTeamHpLoss:
                    ApplyEventStepTeamHpLoss(step);
                    ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
                    break;
                case ExpeditionEventStepKind.PickMemberHpLoss:
                    if (!TryFindPartyMember(selectedCharacterId, out var lossMember))
                        return false;
                    ApplyEventStepMemberHpLoss(lossMember, step);
                    ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
                    interaction.SelectedCharacterId = selectedCharacterId;
                    break;
                case ExpeditionEventStepKind.PickMemberForBuff:
                    if (interaction.StepIndex > 0 &&
                        interaction.Steps[interaction.StepIndex - 1].Kind == ExpeditionEventStepKind.PickMemberHpLoss &&
                        !string.IsNullOrEmpty(interaction.SelectedCharacterId))
                    {
                        selectedCharacterId = interaction.SelectedCharacterId;
                    }

                    if (!TryFindPartyMember(selectedCharacterId, out _))
                        return false;

                    interaction.SelectedCharacterId = selectedCharacterId;
                    break;
                case ExpeditionEventStepKind.PickCardRemove:
                    if (!TryResolveDeckEntry(selectedCardKey, out _))
                        return false;
                    QueuePendingCardAction(interaction, step.Kind, selectedCardKey, "", step.PersonalAttackBonus);
                    break;
                case ExpeditionEventStepKind.PickCardUpgrade:
                    if (!TryResolveDeckEntry(selectedCardKey, out _))
                        return false;
                    QueuePendingCardAction(interaction, step.Kind, selectedCardKey, "", step.PersonalAttackBonus);
                    break;
                case ExpeditionEventStepKind.PickTwoCardsForFusion:
                    if (!TryResolveDeckEntry(selectedCardKey, out var firstEntry)
                        || !TryResolveDeckEntry(selectedSecondCardKey, out var secondEntry))
                    {
                        return false;
                    }

                    if (firstEntry.Template.CardType != secondEntry.Template.CardType)
                        return false;

                    if (firstEntry.Key == secondEntry.Key)
                        return false;

                    QueuePendingCardAction(
                        interaction,
                        step.Kind,
                        selectedCardKey,
                        selectedSecondCardKey,
                        step.PersonalAttackBonus);
                    break;
                case ExpeditionEventStepKind.ShowMessage:
                    if (!ApplyPendingCardAction(interaction))
                        return false;

                    if (!string.IsNullOrEmpty(step.Message))
                        _run.LastEventMessage = step.Message;
                    break;
                default:
                    return false;
            }

            interaction.StepIndex++;
            if (interaction.StepIndex >= interaction.Steps.Count)
                FinishEventInteractionSequence();

            return true;
        }

        static void QueuePendingCardAction(
            ExpeditionEventInteractionState interaction,
            ExpeditionEventStepKind kind,
            string primaryKey,
            string secondaryKey,
            int upgradeBonus)
        {
            interaction.PendingApplyKind = kind;
            interaction.HasPendingCardAction = true;
            interaction.PendingPrimaryCardKey = primaryKey ?? "";
            interaction.PendingSecondaryCardKey = secondaryKey ?? "";
            interaction.PendingUpgradeBonus = upgradeBonus;
        }

        bool ApplyPendingCardAction(ExpeditionEventInteractionState interaction)
        {
            if (interaction == null || !interaction.HasPendingCardAction)
                return true;

            switch (interaction.PendingApplyKind)
            {
                case ExpeditionEventStepKind.PickCardRemove:
                    if (!TryResolveDeckEntry(interaction.PendingPrimaryCardKey, out var removeEntry))
                        return false;
                    if (!ExpeditionRunDeckMutations.TryRemoveCard(_run, _config, removeEntry))
                        return false;
                    break;
                case ExpeditionEventStepKind.PickCardUpgrade:
                    if (!TryResolveDeckEntry(interaction.PendingPrimaryCardKey, out var upgradeEntry))
                        return false;
                    if (!TryFindPartyMember(upgradeEntry.MemberId, out var upgradeMember))
                        return false;
                    if (!ExpeditionRunDeckMutations.TryUpgradeCard(
                            upgradeMember,
                            upgradeEntry,
                            interaction.PendingUpgradeBonus > 0 ? interaction.PendingUpgradeBonus : 1))
                    {
                        return false;
                    }
                    break;
                case ExpeditionEventStepKind.PickTwoCardsForFusion:
                    if (!TryResolveDeckEntry(interaction.PendingPrimaryCardKey, out var firstFusionEntry)
                        || !TryResolveDeckEntry(interaction.PendingSecondaryCardKey, out var secondFusionEntry))
                    {
                        return false;
                    }

                    if (!ExpeditionRunDeckMutations.TryFuseCards(
                            _config,
                            _run,
                            firstFusionEntry,
                            secondFusionEntry,
                            _rng,
                            out var fused,
                            out var owner))
                    {
                        return false;
                    }

                    if (fused != null && owner != null)
                    {
                        _run.PendingDeferredReward =
                            ExpeditionRewardPickupFactory.Card(fused, owner, "流浪铁匠");
                    }
                    break;
                default:
                    return false;
            }

            interaction.HasPendingCardAction = false;
            interaction.PendingPrimaryCardKey = "";
            interaction.PendingSecondaryCardKey = "";
            interaction.PendingUpgradeBonus = 0;
            return true;
        }

        void ApplyEventStepTeamHpLoss(ExpeditionEventInteractionStep step)
        {
            if (!string.IsNullOrEmpty(step.TargetCharacterId))
            {
                if (TryFindPartyMember(step.TargetCharacterId, out var target))
                    ApplyEventStepMemberHpChange(target, step);
                return;
            }

            foreach (var member in _run.Party)
                ApplyEventStepMemberHpChange(member, step);
        }

        static void ApplyPostBattleRelicHeals(ExpeditionRunState run)
        {
            if (run?.Party == null || run.Party.Count == 0)
                return;

            var mods = RelicDatabase.BuildModifiers(run.Relics, run.RelicGrowthTiers);
            if (mods.PostBattleTeamHealPercent <= 0f)
                return;

            foreach (var member in run.Party)
            {
                if (member == null || member.Hp <= 0)
                    continue;

                var heal = System.Math.Max(
                    1,
                    (int)System.Math.Round(member.MaxHp * mods.PostBattleTeamHealPercent / 100f));
                member.Hp = System.Math.Min(member.MaxHp, member.Hp + heal);
            }
        }

        static void ApplyEventStepMemberHpLoss(PartyMemberSnapshot member, ExpeditionEventInteractionStep step) =>
            ApplyEventStepMemberHpChange(member, step, null, null, null);

        void ApplyEventStepMemberHpChange(PartyMemberSnapshot member, ExpeditionEventInteractionStep step)
        {
            ApplyEventStepMemberHpChange(member, step, _run.Party, _run.Relics, _run.RelicGrowthTiers);
        }

        static void ApplyEventStepMemberHpChange(
            PartyMemberSnapshot member,
            ExpeditionEventInteractionStep step,
            IReadOnlyList<PartyMemberSnapshot> party,
            IReadOnlyList<string> relicIds,
            Dictionary<string, int> relicGrowthTiers)
        {
            if (member == null)
                return;

            var hpBonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(party, relicIds, relicGrowthTiers);
            var maxHp = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, hpBonus);

            if (step.FlatHpDelta != 0)
            {
                if (step.FlatHpDelta > 0)
                    member.Hp = System.Math.Min(maxHp, member.Hp + step.FlatHpDelta);
                else
                    member.Hp = System.Math.Max(1, member.Hp - System.Math.Abs(step.FlatHpDelta));
                return;
            }

            if (step.PercentHpDelta == 0)
                return;

            if (step.PercentHpDelta > 0)
            {
                var heal = System.Math.Max(1, maxHp * step.PercentHpDelta / 100);
                member.Hp = System.Math.Min(maxHp, member.Hp + heal);
                return;
            }

            if (step.PercentFromMaxHp)
            {
                var maxHpLoss = System.Math.Max(1, maxHp * System.Math.Abs(step.PercentHpDelta) / 100);
                member.MaxHpPenalty += maxHpLoss;
                member.Hp = System.Math.Max(1, member.Hp - maxHpLoss);
                return;
            }

            var currentHpLoss = System.Math.Max(1, member.Hp * System.Math.Abs(step.PercentHpDelta) / 100);
            member.Hp = System.Math.Max(1, member.Hp - currentHpLoss);
        }

        bool TryFindPartyMember(string characterId, out PartyMemberSnapshot member)
        {
            member = null;
            if (string.IsNullOrEmpty(characterId))
                return false;

            foreach (var candidate in _run.Party)
            {
                if (candidate.CharacterDefinitionId != characterId)
                    continue;

                member = candidate;
                return true;
            }

            return false;
        }

        bool TryResolveDeckEntry(string cardKey, out ExpeditionRunDeckMutations.DeckCardEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(cardKey))
                return false;

            foreach (var candidate in ExpeditionRunDeckMutations.ListSelectableCards(_config, _run))
            {
                if (candidate.Key != cardKey)
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        void ApplyPendingTravelerGift()
        {
            var curseOwnerId = _run.PendingTravelerGiftCurseOwnerId;
            if (string.IsNullOrEmpty(curseOwnerId))
                return;

            _run.PendingTravelerGiftCurseOwnerId = "";

            var curseTemplate = FindCardTemplate("curse_chaos_touch");
            if (curseTemplate == null)
                return;

            var clone = ExpeditionBattleConfigBuilder.CloneTemplate(curseTemplate);
            // 诅咒牌无归属角色：作为额外污染牌加入整场远征的公共牌池，不在任意角色牌组内。
            clone.OwnerCharacterId = "";
            ExpeditionRunDeckRules.TryAddRunWideBonusCard(_config, _run, clone, RecordRunAcquisition);
        }

        void FinishEventInteractionSequence()
        {
            var interaction = _run.EventInteraction;
            _run.EventInteraction = null;

            if (_run.Phase != ExpeditionPhase.EventInteraction)
                return;

            interaction?.DeferredRunAction?.Invoke(_run);

            ApplyPendingTravelerGift();

            if (interaction?.DeferredOutcome != null)
            {
                var deferred = interaction.DeferredOutcome;
                deferred.DeferredRunAction?.Invoke(_run);

                if (_run.PendingDeferredReward != null)
                {
                    if (deferred.PendingRewardPickup == null)
                        deferred.PendingRewardPickup = _run.PendingDeferredReward;
                    _run.PendingDeferredReward = null;
                }

                if (deferred.PendingRewardPickup != null
                    && deferred.PendingRewardPickup.ResolveStatCharacterFromInteraction
                    && !string.IsNullOrEmpty(interaction.SelectedCharacterId)
                    && TryFindPartyMember(interaction.SelectedCharacterId, out var statMember))
                {
                    deferred.PendingRewardPickup.StatCharacterId = statMember.CharacterDefinitionId;
                    deferred.PendingRewardPickup.StatCharacterName = statMember.DisplayName;
                }

                ApplyEventOutcome(deferred);
                return;
            }

            ApplyEventOutcome(new ExpeditionEventOutcome { Message = _run.LastEventMessage });
        }
    }
}
