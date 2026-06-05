using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Map;
using Grimhand.Expedition.Model;

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
            _run.Consumables.Clear();
            _run.Modifiers.TeamAttackBonus = 0;
            _run.Modifiers.TeamDefenseBonus = 0;
            _run.Modifiers.EnergyCapBonus = 0;
            _run.Modifiers.NextCombatEnemyAttackBonus = false;
            _run.Modifiers.ForeseenLayerCount = 0;
            _run.Modifiers.SkipNextRouteSelect = false;
            _run.Modifiers.LootedInjuredAdventurer = false;
            _run.Modifiers.DivinePunishmentActive = false;
            _run.PendingRoutes.Clear();
            _run.PendingVictoryRewards = null;
            _run.PendingChestReward = null;
            _run.PendingEvent = null;
            _run.PendingShrine = null;
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
                _run.PendingVictoryRewards = null;
                _run.PendingChestReward = null;
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
            _run.PendingVictoryRewards = ExpeditionRewardRoller.RollVictoryRewards(_config, _run, _rng);
            _run.LastGoldReward = _run.PendingVictoryRewards.Gold;
            _run.Phase = ExpeditionPhase.VictoryRewards;
        }

        public bool TryClaimVictoryGold()
        {
            var rewards = _run.PendingVictoryRewards;
            if (_run.Phase != ExpeditionPhase.VictoryRewards || rewards == null || rewards.GoldClaimed)
                return false;

            rewards.GoldClaimed = true;
            _run.Gold += rewards.Gold;
            TryAdvanceFromVictoryRewards();
            return true;
        }

        public bool TryClaimVictoryRelic()
        {
            var rewards = _run.PendingVictoryRewards;
            if (_run.Phase != ExpeditionPhase.VictoryRewards || rewards == null || !rewards.HasRelic || rewards.RelicClaimed)
                return false;

            if (!TryAddRelic(rewards.RelicId))
            {
                rewards.RelicClaimed = true;
                TryAdvanceFromVictoryRewards();
                return false;
            }

            rewards.RelicClaimed = true;
            TryAdvanceFromVictoryRewards();
            return true;
        }

        public bool TryClaimVictoryCard()
        {
            var rewards = _run.PendingVictoryRewards;
            if (_run.Phase != ExpeditionPhase.VictoryRewards || rewards == null || !rewards.HasCard || rewards.CardClaimed)
                return false;

            if (!TryGrantCardReward(rewards.CardOwnerCharacterId, rewards.CardDefinitionId, rewards.CardDisplayName))
            {
                rewards.CardClaimed = true;
                TryAdvanceFromVictoryRewards();
                return false;
            }

            rewards.CardClaimed = true;
            TryAdvanceFromVictoryRewards();
            return true;
        }

        public bool TrySkipVictoryOptionalRewards()
        {
            var rewards = _run.PendingVictoryRewards;
            if (_run.Phase != ExpeditionPhase.VictoryRewards || rewards == null)
                return false;

            if (!rewards.GoldClaimed)
            {
                rewards.GoldClaimed = true;
                _run.Gold += rewards.Gold;
            }

            if (rewards.HasRelic && !rewards.RelicClaimed)
                rewards.RelicClaimed = true;

            if (rewards.HasCard && !rewards.CardClaimed)
                rewards.CardClaimed = true;

            TryAdvanceFromVictoryRewards();
            return _run.Phase == ExpeditionPhase.RouteSelect || _run.Phase == ExpeditionPhase.RunComplete;
        }

        public bool TryClaimChestGold()
        {
            var reward = _run.PendingChestReward;
            if (_run.Phase != ExpeditionPhase.TreasureLoot || reward == null || reward.GoldClaimed)
                return false;

            reward.GoldClaimed = true;
            _run.Gold += reward.Gold;
            TryAdvanceFromTreasureLoot();
            return true;
        }

        public bool TryClaimChestRelic()
        {
            var reward = _run.PendingChestReward;
            if (_run.Phase != ExpeditionPhase.TreasureLoot || reward == null || !reward.HasRelic || reward.RelicClaimed)
                return false;

            if (!TryAddRelic(reward.RelicId))
            {
                reward.RelicClaimed = true;
                TryAdvanceFromTreasureLoot();
                return false;
            }

            reward.RelicClaimed = true;
            TryAdvanceFromTreasureLoot();
            return true;
        }

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
                    _run.PendingChestReward = ExpeditionRewardRoller.RollChestReward(_config, _run, _rng);
                    _run.Phase = ExpeditionPhase.TreasureLoot;
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
            CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return true;

            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        public bool TryResolveShopChoice(int choiceIndex)
        {
            if (_run.Phase != ExpeditionPhase.ShopVisit)
                return false;

            var outcome = ExpeditionEventResolver.ResolveShopChoice(_run, choiceIndex);
            _run.LastEventMessage = outcome.Message;
            CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return true;

            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        public bool TryLeaveShop()
        {
            if (_run.Phase != ExpeditionPhase.ShopVisit)
                return false;

            _run.LastEventMessage = "你离开了商店。";
            CompleteCurrentNode();
            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
            return true;
        }

        public bool TryAddRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId) || !RelicDatabase.TryGet(relicId, out _))
                return false;

            if (_run.Relics.Contains(relicId))
                return false;

            _run.Relics.Add(relicId);

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

        void TryAdvanceFromVictoryRewards()
        {
            if (_run.Phase != ExpeditionPhase.VictoryRewards)
                return;

            var rewards = _run.PendingVictoryRewards;
            if (rewards != null && !rewards.IsFullyResolved)
                return;

            _run.PendingVictoryRewards = null;
            LoadRoutesForNextLayer();
            _run.Phase = ExpeditionPhase.RouteSelect;
        }

        void TryAdvanceFromTreasureLoot()
        {
            if (_run.Phase != ExpeditionPhase.TreasureLoot)
                return;

            var reward = _run.PendingChestReward;
            if (reward != null && !reward.IsFullyResolved)
                return;

            _run.PendingChestReward = null;
            CompleteCurrentNode();

            if (_run.Phase == ExpeditionPhase.RunComplete)
                return;

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
            return true;
        }

        CardTemplate FindCardTemplate(string definitionId)
        {
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
