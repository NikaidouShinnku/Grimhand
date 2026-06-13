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
            _run.LastEventMessage = "";
            _run.Party.Clear();
            _run.Relics.Clear();
            _run.UsedEventIds.Clear();
            _run.EventFlags.Clear();
            _run.ConsumableSlots.Clear();
            ConsumableInventory.EnsureInitialized(_run.ConsumableSlots);
            _run.PendingConsumableOfferId = "";
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
            _run.PendingShrine = null;
            _run.EventInteraction = null;
            _run.PendingEventBattleKey = "";
            _run.PendingEventBattleVictoryReward = null;
            _run.PendingDeferredReward = null;
            _run.Shop.Clear();
            _run.CurrentBattleConfig = null;

            _run.Map = ExpeditionMapGenerator.Generate(_config, _run, _rng);
            if (_config.MapStartLayer > 1 && _run.Map != null)
                _run.Map.NodesCompleted = _config.MapStartLayer - 1;

            if (campRoster != null && campRoster.Members.Count > 0)
                CampRunPartyApplier.Apply(campRoster, _run, campMeta);
            else
                InitPartyFromTemplate();

            _run.TalentRun.Reset();
            TalentDatabase.ApplyRunStartEffects(_run, _config);
            LoadRoutesForNextLayer();
        }

        /// <summary>Boss 测试场景：Lv.7 队伍 + 3 遗物 + 每人 3 张奖励牌，直进幽灵女王战。</summary>
        public void StartGhostQueenBossTest(BattleConfig ghostQueenTemplate)
        {
            const int partyLevel = 7;
            const int relicCount = 3;
            const int bonusCardsPerMember = 3;

            ResetRunState(skipMap: true);
            InitPartyAtLevel(partyLevel);
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
                isBossBattle: true);

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
            _run.LastEventMessage = "";
            _run.Party.Clear();
            _run.Relics.Clear();
            _run.UsedEventIds.Clear();
            _run.EventFlags.Clear();
            _run.ConsumableSlots.Clear();
            ConsumableInventory.EnsureInitialized(_run.ConsumableSlots);
            _run.PendingConsumableOfferId = "";
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
            _run.PendingShrine = null;
            _run.EventInteraction = null;
            _run.PendingEventBattleKey = "";
            _run.PendingEventBattleVictoryReward = null;
            _run.PendingDeferredReward = null;
            _run.Shop.Clear();
            _run.CurrentBattleConfig = null;
            _run.TalentRun.Reset();
            _run.RunAcquisitionLog.Clear();
            _run.Map = skipMap ? null : ExpeditionMapGenerator.Generate(_config, _run, _rng);
        }

        void InitPartyAtLevel(int level)
        {
            if (_config.CombatEncounters.Count == 0)
                return;

            foreach (var cc in _config.CombatEncounters[0].Combatants)
            {
                if (cc.Team != TeamSide.Player)
                    continue;

                var clamped = CharacterProgression.ClampLevel(level);
                var stats = CharacterProgression.GetStatsForCharacter(cc.CharacterDefinitionId, clamped);
                _run.Party.Add(new PartyMemberSnapshot
                {
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    DisplayName = cc.DisplayName,
                    Level = clamped,
                    Xp = 0,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                });
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

            var previousParty = _run.Party;
            _run.Party.Clear();
            _run.Party.AddRange(ExpeditionBattleConfigBuilder.CaptureParty(state, previousParty));
            TalentDatabase.SyncRunStateFromBattle(state, _run.TalentRun);

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
            ExpeditionBattleConfigBuilder.GrantXpToParty(_run.Party, _run.LastXpReward);

            if (!string.IsNullOrEmpty(_run.PendingEventBattleKey))
            {
                var eventReward = _run.PendingEventBattleVictoryReward;
                _run.PendingEventBattleKey = "";
                _run.PendingEventBattleVictoryReward = null;

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

            if (!TryGrantCardReward(rewards.CardOwnerCharacterId, rewards.CardDefinitionId, rewards.CardDisplayName))
            {
                rewards.CardSkipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            rewards.CardClaimed = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TrySkipRewardCard()
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasCard ||
                rewards.CardClaimed || rewards.CardSkipped)
                return false;

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
                        EventId = route.EventId,
                        SourceLayer = route.LayerNumber
                    };
                    _run.Phase = ExpeditionPhase.EventChoice;
                    return true;
                case ExpeditionNodeType.Shrine:
                    _run.PendingShrine = new ExpeditionPendingShrine
                    {
                        ShrineId = route.ShrineId,
                        SourceLayer = route.LayerNumber
                    };
                    _run.Phase = ExpeditionPhase.ShrineChoice;
                    return true;
                case ExpeditionNodeType.Shop:
                    _run.Phase = ExpeditionPhase.ShopVisit;
                    ExpeditionShopRoller.OpenShop(_run.Shop, _config, _run, _rng);
                    return true;
                case ExpeditionNodeType.Boss:
                    _run.Phase = ExpeditionPhase.InBattle;
                    _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
                    return true;
                default:
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

            var eventId = _run.PendingEvent.EventId;
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
                    DeferredOutcome = outcome.DeferredOutcome
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
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.PendingShrine == null)
                return false;

            var outcome = ExpeditionEventResolver.ResolveShrineChoice(
                _run, _run.PendingShrine.ShrineId, choiceIndex, _rng);
            _run.LastEventMessage = outcome.Message;
            _run.PendingShrine = null;

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
                    if (!TryGrantCardReward(offer.CardOwnerCharacterId, offer.CardDefinitionId, offer.CardDisplayName))
                    {
                        message = "无法加入该卡牌。";
                        return false;
                    }

                    message = $"购买卡牌：{offer.CardDisplayName}（-{offer.Price} 金币）";
                    return true;

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

            if (RelicDatabase.TryGet(relicId, out var relic))
                RecordRunAcquisition($"获得遗物：{relic.DisplayName}");

            if (relicId == RelicIds.LeafOfMiracle && _run.MiracleLeafUsesRemaining < 0)
                _run.MiracleLeafUsesRemaining = 2;

            return true;
        }

        public int CurrentBattleNumber => _run.Map?.NodesCompleted + 1 ?? _run.BattlesWon + 1;

        void InitPartyFromTemplate()
        {
            if (_config.CombatEncounters.Count == 0)
                return;

            foreach (var cc in _config.CombatEncounters[0].Combatants)
            {
                if (cc.Team != TeamSide.Player)
                    continue;

                var stats = CharacterProgression.GetStatsForCharacter(cc.CharacterDefinitionId, cc.Level);
                _run.Party.Add(new PartyMemberSnapshot
                {
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    DisplayName = cc.DisplayName,
                    Level = CharacterProgression.ClampLevel(cc.Level),
                    Xp = cc.Xp,
                    Hp = stats.MaxHp,
                    MaxHp = stats.MaxHp
                });
            }
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

        bool TryGrantCardReward(string ownerCharacterId, string definitionId, string displayName)
        {
            if (string.IsNullOrEmpty(definitionId))
                return false;

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

            PartyMemberSnapshot targetMember = null;
            foreach (var member in _run.Party)
            {
                if (member.CharacterDefinitionId == template.OwnerCharacterId)
                {
                    targetMember = member;
                    break;
                }
            }

            targetMember ??= _run.Party.Count > 0 ? _run.Party[0] : null;
            if (targetMember == null)
                return false;

            targetMember.BonusCards.Add(template);
            RecordRunAcquisition($"获得卡牌：{template.DisplayName}（{targetMember.DisplayName}）");
            return true;
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
                isBossBattle: false);

            if (_run.Modifiers.DivinePunishmentActive)
            {
                foreach (var cc in config.Combatants)
                {
                    if (cc.Team != TeamSide.Enemy)
                        continue;

                    cc.BaseAttack = System.Math.Max(1,
                        (int)System.Math.Round(cc.BaseAttack * 1.2f, System.MidpointRounding.AwayFromZero));
                }

                _run.Modifiers.DivinePunishmentActive = false;
            }

            config.EnergyCap += _run.Modifiers.EnergyCapBonus;
            config.TurnStartEnergyRegen = System.Math.Max(config.TurnStartEnergyRegen, 4);
            return config;
        }

        BattleConfig BuildBossBattle(bool applyPartyHp)
        {
            BattleConfig template;
            var isDungeonBoss = _run.Map?.ChapterLayerCount >= ExpeditionRegionRules.FullLayerCount;

            if (isDungeonBoss)
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
                isBossBattle: true);

            config.EnergyCap += _run.Modifiers.EnergyCapBonus;
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

        int RollCombatXp() => _config.XpPerVictory > 0 ? _config.XpPerVictory : 16;

        public bool CompleteEventInteractionStep(string selectedCharacterId = null, string selectedCardKey = null)
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
                    break;
                case ExpeditionEventStepKind.PickMemberHpLoss:
                    if (!TryFindPartyMember(selectedCharacterId, out var lossMember))
                        return false;
                    ApplyEventStepMemberHpLoss(lossMember, step);
                    interaction.SelectedCharacterId = selectedCharacterId;
                    break;
                case ExpeditionEventStepKind.PickMemberForBuff:
                    if (interaction.StepIndex > 0 &&
                        interaction.Steps[interaction.StepIndex - 1].Kind == ExpeditionEventStepKind.PickMemberHpLoss &&
                        !string.IsNullOrEmpty(interaction.SelectedCharacterId))
                    {
                        selectedCharacterId = interaction.SelectedCharacterId;
                    }

                    if (!TryFindPartyMember(selectedCharacterId, out var buffMember))
                        return false;
                    buffMember.PersonalAttackBonus += step.PersonalAttackBonus > 0 ? step.PersonalAttackBonus : 2;
                    break;
                case ExpeditionEventStepKind.PickCardRemove:
                    if (!TryResolveDeckEntry(selectedCardKey, out var removeEntry))
                        return false;
                    if (!ExpeditionRunDeckMutations.TryRemoveCard(_run, _config, removeEntry))
                        return false;
                    break;
                case ExpeditionEventStepKind.PickCardUpgrade:
                    if (!TryResolveDeckEntry(selectedCardKey, out var upgradeEntry))
                        return false;
                    if (!TryFindPartyMember(upgradeEntry.MemberId, out var upgradeMember))
                        return false;
                    if (!ExpeditionRunDeckMutations.TryUpgradeCard(
                            upgradeMember,
                            upgradeEntry.Template.DefinitionId,
                            step.PersonalAttackBonus > 0 ? step.PersonalAttackBonus : 20))
                        return false;
                    break;
                case ExpeditionEventStepKind.PickCardFusionFirst:
                    if (!TryResolveDeckEntry(selectedCardKey, out var firstEntry))
                        return false;
                    interaction.FusionFirstCardKey = selectedCardKey;
                    interaction.FusionCardType = firstEntry.Template.CardType;
                    interaction.SelectedCardKey = selectedCardKey;
                    break;
                case ExpeditionEventStepKind.PickCardFusionSecond:
                    if (!TryResolveDeckEntry(selectedCardKey, out var secondEntry))
                        return false;
                    if (!TryResolveDeckEntry(interaction.FusionFirstCardKey, out var firstFusionEntry))
                        return false;
                    if (secondEntry.Template.CardType != firstFusionEntry.Template.CardType)
                        return false;
                    if (!ExpeditionRunDeckMutations.TryFuseCards(
                            _config, _run, firstFusionEntry, secondEntry, _rng, out _, out _))
                        return false;
                    break;
                case ExpeditionEventStepKind.ShowMessage:
                    break;
                default:
                    return false;
            }

            interaction.StepIndex++;
            if (interaction.StepIndex >= interaction.Steps.Count)
                FinishEventInteractionSequence();

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

        static void ApplyEventStepMemberHpLoss(PartyMemberSnapshot member, ExpeditionEventInteractionStep step) =>
            ApplyEventStepMemberHpChange(member, step);

        static void ApplyEventStepMemberHpChange(PartyMemberSnapshot member, ExpeditionEventInteractionStep step)
        {
            if (member == null)
                return;

            if (step.FlatHpDelta != 0)
            {
                if (step.FlatHpDelta > 0)
                    member.Hp = System.Math.Min(member.MaxHp, member.Hp + step.FlatHpDelta);
                else
                    member.Hp = System.Math.Max(1, member.Hp - System.Math.Abs(step.FlatHpDelta));
                return;
            }

            if (step.PercentHpDelta == 0)
                return;

            if (step.PercentHpDelta > 0)
            {
                var heal = System.Math.Max(1, member.MaxHp * step.PercentHpDelta / 100);
                member.Hp = System.Math.Min(member.MaxHp, member.Hp + heal);
                return;
            }

            var loss = System.Math.Max(1, member.Hp * System.Math.Abs(step.PercentHpDelta) / 100);
            member.Hp = System.Math.Max(1, member.Hp - loss);
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

        void FinishEventInteractionSequence()
        {
            var interaction = _run.EventInteraction;
            _run.EventInteraction = null;

            if (_run.Phase != ExpeditionPhase.EventInteraction)
                return;

            if (interaction?.DeferredOutcome != null)
            {
                ApplyEventOutcome(interaction.DeferredOutcome);
                return;
            }

            ApplyEventOutcome(new ExpeditionEventOutcome { Message = _run.LastEventMessage });
        }
    }
}
