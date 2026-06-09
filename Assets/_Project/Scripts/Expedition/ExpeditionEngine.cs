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

        public void StartRun()
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
            _run.PendingRoutes.Clear();
            _run.PendingRewardPickup = null;
            _run.PendingEvent = null;
            _run.PendingShrine = null;
            _run.Shop.Clear();
            _run.CurrentBattleConfig = null;

            _run.Map = ExpeditionMapGenerator.Generate(_config, _run, _rng);
            InitPartyFromTemplate();
            LoadRoutesForNextLayer();
        }

        public void OnBattleFinished(BattleState state)
        {
            if (state == null)
                return;

            _run.Party.Clear();
            _run.Party.AddRange(ExpeditionBattleConfigBuilder.CaptureParty(state));

            if (_run.MiracleLeafUsesRemaining >= 0)
                _run.MiracleLeafUsesRemaining = state.MiracleLeafRevivesRemaining;

            if (state.Outcome == BattleOutcome.PlayerDefeat)
            {
                _run.Phase = ExpeditionPhase.RunFailed;
                _run.PendingRoutes.Clear();
                _run.PendingRewardPickup = null;
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
                default:
                    _run.Phase = ExpeditionPhase.InBattle;
                    _run.CurrentBattleConfig = BuildBattleFromEncounter(route.EncounterIndex, applyPartyHp: true);
                    return true;
            }
        }

        public bool TryResolveEventChoice(int choiceIndex)
        {
            if (_run.Phase != ExpeditionPhase.EventChoice || _run.PendingEvent == null)
                return false;

            var outcome = ExpeditionEventResolver.ResolveChoice(
                _run, _config, _run.PendingEvent.EventId, choiceIndex, _rng);
            _run.LastEventMessage = outcome.Message;
            _run.PendingEvent = null;

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

        BattleConfig BuildBattleFromEncounter(int encounterIndex, bool applyPartyHp)
        {
            if (_config.CombatEncounters.Count == 0)
                throw new System.InvalidOperationException("ExpeditionConfig.CombatEncounters is empty.");

            var index = encounterIndex % _config.CombatEncounters.Count;
            var template = _config.CombatEncounters[index];
            var seed = _rng.NextInt(1, int.MaxValue);
            var config = ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                _run.Party,
                _run.Relics,
                seed,
                applyPartyHp,
                _run.MiracleLeafUsesRemaining,
                CurrentBattleNumber,
                _run.Modifiers);

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

        int RollCombatXp() => _config.XpPerVictory > 0 ? _config.XpPerVictory : 16;
    }
}
