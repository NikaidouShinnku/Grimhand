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

        public ulong RngState
        {
            get => _rng.State;
            set => _rng.RestoreState(value);
        }

        public static ExpeditionRunState CloneRunState(ExpeditionRunState source) =>
            ExpeditionRunStateCopy.Clone(source);

        public ExpeditionRunState ExportRunState() => ExpeditionRunStateCopy.Clone(_run, _config);

        public void ResumeRun(ExpeditionRunState run, ulong rngState)
        {
            if (run == null)
                throw new System.ArgumentNullException(nameof(run));

            ExpeditionRunStateCopy.CopyInto(ExpeditionRunStateCopy.Clone(run, _config), _run, _config);
            _rng.RestoreState(rngState);

            if (_run.Phase == ExpeditionPhase.InBattle)
                RebuildCurrentBattleForResume();
            else
                ReconcileAfterResume();
        }

        /// <summary>Continue 后修复半开奖励、卡包三选一、事件交互等中间态。</summary>
        public void ReconcileAfterResume()
        {
            ReconcilePendingCardOffer();
            ReconcilePendingCardPackOffer();
            ReconcileChestRewardState();
            ReconcileEventInteraction();
            ReconcileRouteSelect();
            ReconcileRewardPickupProgress();
        }

        void ReconcilePendingCardOffer()
        {
            var offer = _run.PendingCardOffer;
            if (offer == null)
                return;

            if (offer.Template == null || string.IsNullOrEmpty(offer.Template.DefinitionId))
            {
                _run.PendingCardOffer = null;
                return;
            }

            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(offer.Template, _config.PlayerCardCatalog);
        }

        void ReconcilePendingCardPackOffer()
        {
            var offer = _run.PendingCardPackOffer;
            if (offer == null)
                return;

            var hasValidChoice = false;
            foreach (var choice in offer.Choices)
            {
                if (choice?.Template == null || string.IsNullOrEmpty(choice.Template.DefinitionId))
                    continue;

                ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(choice.Template, _config.PlayerCardCatalog);
                hasValidChoice = true;
            }

            if (hasValidChoice)
                return;

            offer.Choices.Clear();
            if (CardPackIds.IsValid(offer.PackId))
            {
                var choices = CardPackRoller.RollChoices(offer.PackId, _config, _run, _rng);
                if (choices.Count > 0)
                {
                    offer.Choices.AddRange(choices);
                    return;
                }
            }

            _run.PendingCardPackOffer = null;
        }

        void ReconcileChestRewardState()
        {
            if (_run.Phase != ExpeditionPhase.RewardPickup
                || _run.PendingRewardPickup?.Kind != RewardPickupKind.Chest)
            {
                return;
            }

            if (_run.ChestRewardRevealed)
                return;

            // 仅在断线恢复且已有领取进度时跳过关闭态；
            // 否则会把新宝箱直接标成已开启，导致点击 RevealChest / 开箱音效永不触发。
            if (HasResolvedRewardProgress(_run.PendingRewardPickup))
                _run.ChestRewardRevealed = true;
        }

        static bool HasResolvedRewardProgress(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return false;

            if (rewards.HasGold && (rewards.GoldClaimed || rewards.GoldSkipped))
                return true;
            if (rewards.HasRelic && (rewards.RelicClaimed || rewards.RelicSkipped))
                return true;
            if (rewards.HasCard && (rewards.CardClaimed || rewards.CardSkipped))
                return true;
            if (rewards.HasConsumable && (rewards.ConsumableClaimed || rewards.ConsumableSkipped))
                return true;
            if (rewards.HasStatBonus && (rewards.StatClaimed || rewards.StatSkipped))
                return true;

            foreach (var pack in rewards.CardPacks)
            {
                if (pack != null && pack.IsResolved)
                    return true;
            }

            return false;
        }

        void ReconcileEventInteraction()
        {
            if (_run.Phase != ExpeditionPhase.EventInteraction || _run.EventInteraction == null)
                return;

            if (_run.EventInteraction.StepIndex >= _run.EventInteraction.Steps.Count)
                FinishEventInteractionSequence();
        }

        void ReconcileRouteSelect()
        {
            if (_run.Phase != ExpeditionPhase.RouteSelect || _run.PendingRoutes.Count > 0)
                return;

            LoadRoutesForNextLayer();
        }

        void ReconcileRewardPickupProgress()
        {
            if (_run.Phase != ExpeditionPhase.RewardPickup)
                return;

            var rewards = _run.PendingRewardPickup;
            if (rewards == null || !rewards.HasAnyReward)
            {
                _run.PendingRewardPickup = null;
                TryAdvanceFromRewardPickup();
                return;
            }

            if (rewards.IsFullyResolved)
                TryAdvanceFromRewardPickup();
        }

        void FailRun(string message)
        {
            _run.Phase = ExpeditionPhase.RunFailed;
            _run.PendingRoutes.Clear();
            _run.PendingRewardPickup = null;
            _run.PendingEventBattleKey = "";
            _run.PendingEventBattleVictoryReward = null;
            _run.PendingEvent = null;
            _run.PendingEventAftermath = null;
            _run.EventInteraction = null;
            _run.PendingCardOffer = null;
            _run.PendingCardPackOffer = null;
            _run.PendingShrine = null;
            _run.CardAltar = null;
            if (!string.IsNullOrEmpty(message))
                _run.LastEventMessage = message;
        }

        bool TryFailRunIfPartyWiped(string message = "队伍全员倒下，远征失败。")
        {
            if (_run.Phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed)
                return false;

            if (!ExpeditionPartyRules.IsPartyWiped(_run.Party))
                return false;

            FailRun(message);
            return true;
        }

        public void RebuildCurrentBattleForResume()
        {
            if (_run.Phase != ExpeditionPhase.InBattle)
                return;

            _run.CurrentBattleConfig = null;

            if (!string.IsNullOrEmpty(_run.PendingEventBattleKey))
            {
                RecordLastBattleContext(CurrentBattleNumber, isElite: false, isBoss: false);
                _run.CurrentBattleConfig = BuildBattleFromEncounter(0, applyPartyHp: true);
                return;
            }

            var layerNumber = (_run.Map?.NodesCompleted ?? 0) + 1;
            var layer = _run.Map?.GetLayer(layerNumber);

            if (TryRebuildBossBattleForResume(layer, layerNumber))
                return;

            if (layer == null || !layer.ChosenOptionIndex.HasValue)
                return;

            var optionIndex = layer.ChosenOptionIndex.Value;
            if (optionIndex < 0 || optionIndex >= layer.Options.Count)
                return;

            var option = layer.Options[optionIndex];
            if (option.NodeType == ExpeditionNodeType.Boss)
            {
                RecordLastBattleContext(layer.LayerNumber, isElite: false, isBoss: true);
                _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
                return;
            }

            RecordLastBattleContext(layer.LayerNumber, option.IsElite, isBoss: false);
            _run.CurrentBattleConfig = BuildBattleFromEncounter(
                option.EncounterIndex,
                applyPartyHp: true,
                option.MonsterEncounterId,
                option.IsElite,
                layer.LayerNumber);
        }

        bool TryRebuildBossBattleForResume(ExpeditionMapLayer layer, int layerNumber)
        {
            if (layer != null && IsBossLayer(layer))
            {
                RecordLastBattleContext(layer.LayerNumber, isElite: false, isBoss: true);
                _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
                return _run.CurrentBattleConfig != null;
            }

            if (ExpeditionRegionRules.IsBossTestStartLayer(_config.MapStartLayer)
                && layerNumber == _config.MapStartLayer)
            {
                RecordLastBattleContext(layerNumber, isElite: false, isBoss: true);
                _run.CurrentBattleConfig = BuildBossBattle(applyPartyHp: true);
                return _run.CurrentBattleConfig != null;
            }

            return false;
        }

        static bool IsBossLayer(ExpeditionMapLayer layer)
        {
            if (layer == null)
                return false;

            if (layer.IsBoss)
                return true;

            foreach (var option in layer.Options)
            {
                if (option?.NodeType == ExpeditionNodeType.Boss)
                    return true;
            }

            return false;
        }

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
            _run.PendingCardPackOffer = null;
            _run.CardAltar = null;
            _run.RunStartCampDecks.Clear();
            _run.ExtractedCampCollectionIndices.Clear();
            _run.Modifiers.TeamAttackBonus = 0;
            _run.Modifiers.TeamDefenseBonus = 0;
            _run.Modifiers.TeamBlockGainBonusPercent = 0f;
            _run.Modifiers.EnergyCapBonus = 0;
            _run.Modifiers.HandLimitBonus = 0;
            _run.Modifiers.DrawPerTurnBonus = 0;
            _run.Modifiers.AltarHpPlus5Purchases = 0;
            _run.Modifiers.AltarHpPlus10Purchases = 0;
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
            _run.IsTrainingGround = false;

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
            var layer = _run.Map?.GetLayer(_config.MapStartLayer);
            if (layer != null && layer.Options.Count > 0)
                layer.ChosenOptionIndex = 0;
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

        /// <summary>营地训练场：军营携带牌为战斗基组，对战不出牌的假人。</summary>
        public void StartTrainingGround(CampRosterState roster, CampMetaState meta, BattleConfig playerBaseline)
        {
            ResetRunState(skipMap: true);
            _run.IsTrainingGround = true;
            _run.Phase = ExpeditionPhase.InBattle;
            _run.CurrentBossDisplayName = TrainingGroundEncounterBuilder.DummyDisplayName;
            _run.TargetBattleCount = 1;

            CampRunPartyApplier.Apply(roster, _run, meta);
            foreach (var member in _run.Party)
                member.UsesCampDeckAsBattleBase = true;

            ExpeditionDeckInstanceRules.EnsurePartyBaseDeckInstances(_config, _run.Party);
            CampDeckOwnershipRules.SyncRunStartCampDecks(_run);

            var template = TrainingGroundEncounterBuilder.BuildTemplate(
                playerBaseline
                ?? (_config.CombatEncounters.Count > 0 ? _config.CombatEncounters[0] : null));

            var seed = _rng.NextInt(1, int.MaxValue);
            _run.CurrentBattleConfig = ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                _run.Party,
                _run.Relics,
                seed,
                applyPartyHp: true,
                _run.MiracleLeafUsesRemaining,
                floor: 1,
                _run.Modifiers,
                _config.PlayerCardCatalog,
                _config,
                _run.TalentRun,
                isBossBattle: false,
                _run.RelicGrowthTiers,
                _run.RunWideBonusCards);

            _run.CurrentBattleConfig.EnemyCardsDrawnPerTurn = 0;
            _run.CurrentBattleConfig.EnemyTurnEnergyBudget = 0;
            _run.CurrentBattleConfig.SkipFloorScaling = true;
            _run.CurrentBattleConfig.ManualEnemyIntentsOnly = true;
            _run.CurrentBattleConfig.VictoryOnCharacterDeathId =
                TrainingGroundEncounterBuilder.DummyCharacterId;
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
            _run.PendingCardPackOffer = null;
            _run.CardAltar = null;
            _run.RunStartCampDecks.Clear();
            _run.ExtractedCampCollectionIndices.Clear();
            _run.Modifiers.TeamAttackBonus = 0;
            _run.Modifiers.TeamDefenseBonus = 0;
            _run.Modifiers.TeamBlockGainBonusPercent = 0f;
            _run.Modifiers.EnergyCapBonus = 0;
            _run.Modifiers.HandLimitBonus = 0;
            _run.Modifiers.DrawPerTurnBonus = 0;
            _run.Modifiers.AltarHpPlus5Purchases = 0;
            _run.Modifiers.AltarHpPlus10Purchases = 0;
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
            _run.IsTrainingGround = false;
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

        /// <summary>
        /// 将本场战斗中的远征计数（虚化进入次数、消耗牌次数、应对成功等）写回 Run。
        /// 战后与局中存档前都应调用，避免断线重连丢失本场增量。
        /// </summary>
        public void SyncV09BattleCountersFromBattleState(BattleState state)
        {
            if (state?.Config?.RunModifiers == null)
                return;

            _run.V09EtherealEntryCount = state.Config.RunModifiers.EtherealEntryCount;
            _run.V09ExpeditionRespondSuccessCount = state.Config.RunModifiers.ExpeditionRespondSuccessCount;
            _run.V09SandSpearExhaustCardsPlayed = state.Config.RunModifiers.SandSpearExhaustCardsPlayed;
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
            SyncV09BattleCountersFromBattleState(state);
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);

            if (state.Outcome == BattleOutcome.PlayerVictory)
                ApplyPostBattleRelicHeals(_run);

            if (_run.MiracleLeafUsesRemaining >= 0)
                _run.MiracleLeafUsesRemaining = state.MiracleLeafRevivesRemaining;

            if (state.Outcome == BattleOutcome.PlayerDefeat)
            {
                FailRun("战斗失败，队伍无法继续。");
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

            _run.PendingRewardPickup = ExpeditionRewardRoller.RollVictoryRewards(
                _config,
                _run,
                _rng,
                _run.LastBattleFloor,
                _run.LastBattleWasElite,
                _run.LastBattleWasBoss);
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
                if (!RelicGrowthRules.TryEvolveRelic(
                        _run,
                        rewards.RelicEvolveFromId,
                        rewards.RelicEvolveToId))
                {
                    rewards.RelicSkipped = true;
                    TryAdvanceFromRewardPickup();
                    return false;
                }

                if (RelicDatabase.TryGet(rewards.RelicEvolveToId, out var evolved))
                    RecordRunAcquisition($"遗物进化：{evolved.DisplayName}");

                rewards.RelicClaimed = true;
                TryAdvanceFromRewardPickup();
                return true;
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
            else if (result == CardGrantResult.PendingReplace)
            {
                // PendingCardOffer 已由 TryOfferCard 写入，等待替换 UI。
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

        public bool TrySkipRewardCardPack(int packIndex)
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasCardPacks)
                return false;

            if (packIndex < 0 || packIndex >= rewards.CardPacks.Count)
                return false;

            var entry = rewards.CardPacks[packIndex];
            if (entry.IsResolved)
                return false;

            if (_run.PendingCardPackOffer?.RewardPackIndex == packIndex)
                _run.PendingCardPackOffer = null;

            entry.Skipped = true;
            TryAdvanceFromRewardPickup();
            return true;
        }

        public bool TryOpenRewardCardPack(int packIndex)
        {
            var rewards = _run.PendingRewardPickup;
            if (_run.Phase != ExpeditionPhase.RewardPickup || rewards == null || !rewards.HasCardPacks)
                return false;

            if (packIndex < 0 || packIndex >= rewards.CardPacks.Count)
                return false;

            var entry = rewards.CardPacks[packIndex];
            if (entry.IsResolved || !CardPackIds.IsValid(entry.PackId))
                return false;

            if (_run.PendingCardPackOffer != null || _run.PendingCardOffer != null)
                return false;

            var choices = CardPackRoller.RollChoices(entry.PackId, _config, _run, _rng);
            if (choices.Count == 0)
            {
                entry.Skipped = true;
                TryAdvanceFromRewardPickup();
                return false;
            }

            _run.PendingCardPackOffer = new ExpeditionPendingCardPackOffer
            {
                PackId = entry.PackId,
                Context = ExpeditionCardOfferContext.RewardPickup,
                RewardPackIndex = packIndex
            };
            _run.PendingCardPackOffer.Choices.AddRange(choices);
            return true;
        }

        public bool TryPickCardFromPack(int choiceIndex)
        {
            var packOffer = _run.PendingCardPackOffer;
            if (packOffer == null || choiceIndex < 0 || choiceIndex >= packOffer.Choices.Count)
                return false;

            if (_run.PendingCardOffer != null)
                return false;

            var choice = packOffer.Choices[choiceIndex];
            if (choice?.Template == null)
                return false;

            if (!TryFindPartyMember(choice.OwnerCharacterId, out var member) && _run.Party.Count > 0)
                member = _run.Party[0];

            if (member == null)
                return false;

            var offerContext = packOffer.Context == ExpeditionCardOfferContext.Shop
                ? ExpeditionCardOfferContext.Shop
                : ExpeditionCardOfferContext.CardPack;

            var result = ExpeditionRunDeckRules.TryOfferCard(
                _config,
                _run,
                member,
                choice.Template,
                offerContext,
                RecordRunAcquisition);

            if (result == CardGrantResult.Failed)
            {
                TrySkipCardPack();
                return false;
            }

            if (result == CardGrantResult.PendingReplace)
            {
                _run.PendingCardOffer = new ExpeditionPendingCardOffer
                {
                    OwnerCharacterId = member.CharacterDefinitionId,
                    Template = ExpeditionBattleConfigBuilder.CloneTemplate(choice.Template),
                    Context = ExpeditionCardOfferContext.CardPack,
                    SourceRewardPackIndex = packOffer.RewardPackIndex,
                    SourceShopSlotIndex = packOffer.ShopSlotIndex,
                    SourcePackId = packOffer.PackId
                };
                ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(
                    _run.PendingCardOffer.Template,
                    _config.PlayerCardCatalog);
                return true;
            }

            CompleteCardPackOffer(claimed: true);
            return true;
        }

        public bool TrySkipCardPack()
        {
            if (_run.PendingCardPackOffer == null)
                return false;

            CompleteCardPackOffer(claimed: false);
            return true;
        }

        void CompleteCardPackOffer(bool claimed)
        {
            var packOffer = _run.PendingCardPackOffer;
            if (packOffer == null)
                return;

            if (packOffer.RewardPackIndex >= 0
                && _run.PendingRewardPickup != null
                && packOffer.RewardPackIndex < _run.PendingRewardPickup.CardPacks.Count)
            {
                var entry = _run.PendingRewardPickup.CardPacks[packOffer.RewardPackIndex];
                if (claimed)
                    entry.Claimed = true;
                else
                    entry.Skipped = true;
            }

            _run.PendingCardPackOffer = null;
            _run.LastEventMessage = claimed
                ? $"已从{CardPackIds.GetDisplayName(packOffer.PackId)}加入卡牌。"
                : $"已放弃{CardPackIds.GetDisplayName(packOffer.PackId)}。";

            if (packOffer.Context == ExpeditionCardOfferContext.RewardPickup)
                TryAdvanceFromRewardPickup();
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
            if (rewards.TeamBlockGainBonusPercent != 0f)
                _run.Modifiers.TeamBlockGainBonusPercent += rewards.TeamBlockGainBonusPercent;
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
            // 遗物进化不可跳过：跳过奖励时仍自动完成替换，避免叙事已写「进化完成」却保留原遗物。
            if (rewards.HasRelic && !rewards.RelicClaimed && !rewards.RelicSkipped)
            {
                if (rewards.HasRelicEvolution)
                    TryClaimRewardRelic();
                else
                    TrySkipRewardRelic();
            }
            if (rewards.HasCard && !rewards.CardClaimed && !rewards.CardSkipped)
                TrySkipRewardCard();
            if (rewards.HasCardPacks)
            {
                for (var i = 0; i < rewards.CardPacks.Count; i++)
                {
                    if (!rewards.CardPacks[i].IsResolved)
                        TrySkipRewardCardPack(i);
                }
            }
            if (rewards.HasConsumable && !rewards.ConsumableClaimed && !rewards.ConsumableSkipped)
                TrySkipRewardConsumable();
            // 属性/强固等强制增益：放弃剩余时仍自动领取，不可丢弃
            if (rewards.HasStatBonus && !rewards.StatClaimed && !rewards.StatSkipped)
                TryClaimRewardStat();

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
                    _run.ChestRewardRevealed = false;
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
            if (choice.RequiredGold > 0 && _run.Gold < choice.RequiredGold)
                return false;

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
                // 纯「继续确认」文案步：已有事件结果页时多余，直接结算延迟结果
                if (AreOnlyShowMessageSteps(outcome.InteractionSteps))
                {
                    foreach (var step in outcome.InteractionSteps)
                    {
                        if (!string.IsNullOrEmpty(step.Message))
                            _run.LastEventMessage = step.Message;
                    }

                    outcome.DeferredRunAction?.Invoke(_run);
                    if (TryFailRunIfPartyWiped())
                        return true;

                    ApplyPendingTravelerGift();
                    if (TryFailRunIfPartyWiped())
                        return true;

                    if (outcome.DeferredOutcome != null)
                    {
                        var deferred = outcome.DeferredOutcome;
                        deferred.DeferredRunAction?.Invoke(_run);
                        ApplyEventOutcome(deferred);
                        return true;
                    }

                    ApplyEventOutcome(new ExpeditionEventOutcome { Message = _run.LastEventMessage });
                    return true;
                }

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

            if (TryFailRunIfPartyWiped())
                return true;

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
            if (draft.Confirmed)
                return;

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

            _run.LastEventMessage = "抽牌数量 +1";
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

        public bool TryAltarRestHealWithGold()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryRestHealWithGold(_run))
                return false;

            _run.LastEventMessage = $"花费 {ExpeditionAltarUpgradeRules.RestHealGoldCost} 金币，全队回复 {ExpeditionAltarUpgradeRules.RestHealPercent}% 生命";
            return true;
        }

        public bool TryAltarRestHealWithXp()
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            if (!ExpeditionAltarUpgradeRules.TryRestHealWithXp(_run))
                return false;

            _run.LastEventMessage = $"花费 {ExpeditionAltarUpgradeRules.RestHealXpCost} 经验，全队回复 {ExpeditionAltarUpgradeRules.RestHealPercent}% 生命";
            return true;
        }

        public int GetAltarBaseEnergyCap()
        {
            if (_config.CombatEncounters.Count > 0 && _config.CombatEncounters[0] != null)
                return _config.CombatEncounters[0].EnergyCap;

            return 8;
        }

        public int GetAltarBaseDrawCount()
        {
            if (_config.CombatEncounters.Count > 0 && _config.CombatEncounters[0] != null)
            {
                var drawn = _config.CombatEncounters[0].CardsDrawnPerTurn;
                if (drawn > 0)
                    return drawn;
            }

            return 5;
        }

        /// <summary>兼容旧调用名；实际返回抽牌基数。</summary>
        public int GetAltarBaseHandLimit() => GetAltarBaseDrawCount();

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

        public bool TryConfirmCardAltar(string memberId = null)
        {
            if (_run.Phase != ExpeditionPhase.ShrineChoice || _run.CardAltar == null)
                return false;

            PartyMemberSnapshot member;
            if (!string.IsNullOrEmpty(memberId))
            {
                member = FindPartyMember(memberId);
            }
            else
            {
                member = null;
                foreach (var candidate in _run.Party)
                {
                    if (candidate == null || string.IsNullOrEmpty(candidate.CharacterDefinitionId))
                        continue;

                    if (!_run.CardAltar.Drafts.TryGetValue(candidate.CharacterDefinitionId, out var d)
                        || !d.HasSelection
                        || d.Confirmed)
                        continue;

                    member = candidate;
                    break;
                }
            }

            if (member == null)
            {
                _run.LastEventMessage = "请先为当前角色选择要取出的卡牌。";
                return false;
            }

            if (!_run.CardAltar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft)
                || !draft.HasSelection)
            {
                _run.LastEventMessage = $"{member.DisplayName} 尚未选择要取出的卡牌。";
                return false;
            }

            if (draft.Confirmed)
            {
                _run.LastEventMessage = $"{member.DisplayName} 本趟祭坛已取出过卡牌。";
                return false;
            }

            if (CampCollectionProgress.IsExtracted(_run, member.CharacterDefinitionId, draft.CollectionCardIndex))
            {
                draft.CollectionCardIndex = -1;
                draft.ReplaceDeckCardKey = "";
                _run.LastEventMessage = $"{member.DisplayName} 的该收藏牌已被取出。";
                return false;
            }

            if (!TryValidateCardAltarExtraction(member, draft, out var error))
            {
                _run.LastEventMessage = error;
                return false;
            }

            var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(
                _config, _run, member, draft.CollectionCardIndex);
            ApplyCardAltarExtraction(member, draft);
            draft.Confirmed = true;
            draft.CollectionCardIndex = -1;
            draft.ReplaceDeckCardKey = "";
            _run.LastEventMessage = template != null
                ? $"已为 {member.DisplayName} 取出：{template.DisplayName}"
                : $"已为 {member.DisplayName} 取出卡牌。";
            return true;
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
            var sourceRewardPackIndex = offer.SourceRewardPackIndex;
            var sourceShopSlotIndex = offer.SourceShopSlotIndex;
            var sourcePackId = offer.SourcePackId;
            _run.PendingCardOffer = null;
            RecordRunAcquisition($"获得卡牌：{cardName}（{member.DisplayName}）");

            if (context == ExpeditionCardOfferContext.CardPack)
            {
                if (sourceRewardPackIndex >= 0
                    && _run.PendingRewardPickup != null
                    && sourceRewardPackIndex < _run.PendingRewardPickup.CardPacks.Count)
                    _run.PendingRewardPickup.CardPacks[sourceRewardPackIndex].Claimed = true;

                _run.PendingCardPackOffer = null;
                TryAdvanceFromRewardPickup();
            }
            else if (context == ExpeditionCardOfferContext.RewardPickup && _run.PendingRewardPickup != null)
            {
                _run.PendingRewardPickup.CardClaimed = true;
                TryAdvanceFromRewardPickup();
            }
            else if (context == ExpeditionCardOfferContext.Shop)
            {
                _run.PendingCardPackOffer = null;
            }

            _run.LastEventMessage = $"已将 {cardName} 加入 {member.DisplayName} 的卡组。";
            return true;
        }

        public bool TryAbandonPendingCardOffer()
        {
            if (_run.PendingCardOffer == null)
                return false;

            var context = _run.PendingCardOffer.Context;
            var sourceRewardPackIndex = _run.PendingCardOffer.SourceRewardPackIndex;
            _run.PendingCardOffer = null;

            if (context == ExpeditionCardOfferContext.CardPack && sourceRewardPackIndex >= 0
                && _run.PendingRewardPickup != null
                && sourceRewardPackIndex < _run.PendingRewardPickup.CardPacks.Count)
            {
                _run.PendingRewardPickup.CardPacks[sourceRewardPackIndex].Skipped = true;
                _run.PendingCardPackOffer = null;
                TryAdvanceFromRewardPickup();
            }
            else if (context == ExpeditionCardOfferContext.RewardPickup && _run.PendingRewardPickup != null)
            {
                _run.PendingRewardPickup.CardSkipped = true;
                TryAdvanceFromRewardPickup();
            }

            _run.LastEventMessage = context switch
            {
                ExpeditionCardOfferContext.Shop => "已放弃购买的卡牌。",
                ExpeditionCardOfferContext.Event => "已放弃获得的卡牌。",
                ExpeditionCardOfferContext.CardPack => "已放弃卡包中的卡牌。",
                _ => "已放弃新卡牌。"
            };
            return true;
        }

        void OpenShopCardPack(int slotIndex, string packId)
        {
            if (!CardPackIds.IsValid(packId))
                return;

            var choices = CardPackRoller.RollChoices(packId, _config, _run, _rng);
            if (choices.Count == 0)
            {
                _run.LastEventMessage = "卡包是空的。";
                return;
            }

            _run.PendingCardPackOffer = new ExpeditionPendingCardPackOffer
            {
                PackId = packId,
                Context = ExpeditionCardOfferContext.Shop,
                ShopSlotIndex = slotIndex
            };
            _run.PendingCardPackOffer.Choices.AddRange(choices);
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

            if (!TryFulfillShopOffer(slotIndex, offer, out var message))
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

            if (_run.PendingCardPackOffer != null || _run.PendingCardOffer != null)
            {
                _run.LastEventMessage = "请先处理当前卡包。";
                return false;
            }

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

        bool TryFulfillShopOffer(int slotIndex, ShopOffer offer, out string message)
        {
            message = "";
            switch (offer.Kind)
            {
                case ShopOfferKind.CardPack:
                {
                    if (!CardPackIds.IsValid(offer.CardPackId))
                    {
                        message = "无法购买该卡包。";
                        return false;
                    }

                    if (_run.PendingCardPackOffer != null || _run.PendingCardOffer != null)
                    {
                        message = "请先处理当前卡包。";
                        return false;
                    }

                    OpenShopCardPack(slotIndex, offer.CardPackId);
                    message = $"购买{CardPackIds.GetDisplayName(offer.CardPackId)}（-{offer.Price} 金币）";
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
            _run.PendingCardPackOffer = null;

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
            _run.ChestRewardRevealed = false;
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

            if (_run.PendingCardPackOffer != null)
                return;

            var kind = rewards?.Kind ?? RewardPickupKind.EventOrShrine;
            _run.PendingRewardPickup = null;
            // 离开奖励阶段后必须清掉，否则下一只宝箱会沿用“已开启”状态。
            _run.ChestRewardRevealed = false;

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

            if (TryFailRunIfPartyWiped())
                return true;

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
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
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
            // 手牌上限固定 10；旧 HandLimitBonus 与 DrawPerTurnBonus 均计入每回合抽牌。
            config.HandLimit = 10;
            config.CardsDrawnPerTurn += _run.Modifiers.DrawPerTurnBonus + _run.Modifiers.HandLimitBonus;
            config.TurnStartEnergyRegen = System.Math.Max(config.TurnStartEnergyRegen, 4);
            config.RunModifiers.EtherealEntryCount = _run.V09EtherealEntryCount;
            config.RunModifiers.ExpeditionRespondSuccessCount = _run.V09ExpeditionRespondSuccessCount;
            config.RunModifiers.SandSpearExhaustCardsPlayed = _run.V09SandSpearExhaustCardsPlayed;
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
                template = CorruptedOceanGoddessBossEncounterBuilder.BuildTemplate(standard);
                _run.CurrentBossDisplayName = CorruptedOceanGoddessBossEncounterBuilder.DisplayName;
            }
            else if (isDungeonBoss)
            {
                var standard = _config.CombatEncounters.Count > 0
                    ? _config.CombatEncounters[0]
                    : null;
                var monsterTemplates = MonsterTemplateRegistry.BuildTemplateMap(_config);
                template = Floor40BossEncounterBuilder.BuildRandomTemplate(standard, _rng, monsterTemplates);
                _run.CurrentBossDisplayName = ResolveBossDisplayName(template);
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
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(_run.Party, _run.Relics, _run.RelicGrowthTiers);
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
            // 手牌上限固定 10；旧 HandLimitBonus 与 DrawPerTurnBonus 均计入每回合抽牌。
            config.HandLimit = 10;
            config.CardsDrawnPerTurn += _run.Modifiers.DrawPerTurnBonus + _run.Modifiers.HandLimitBonus;
            config.TurnStartEnergyRegen = System.Math.Max(config.TurnStartEnergyRegen, 4);
            config.RunModifiers.EtherealEntryCount = _run.V09EtherealEntryCount;
            config.RunModifiers.ExpeditionRespondSuccessCount = _run.V09ExpeditionRespondSuccessCount;
            config.RunModifiers.SandSpearExhaustCardsPlayed = _run.V09SandSpearExhaustCardsPlayed;
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

                if (cc.CharacterDefinitionId == WardenBossEncounterBuilder.CharacterId)
                    return WardenBossEncounterBuilder.DisplayName;

                if (cc.CharacterDefinitionId == DarkKnightBossEncounterBuilder.CharacterId)
                    return DarkKnightBossEncounterBuilder.DisplayName;

                if (cc.CharacterDefinitionId == CorruptedOceanGoddessBossEncounterBuilder.CharacterId)
                    return CorruptedOceanGoddessBossEncounterBuilder.DisplayName;

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

            if (step.Kind is ExpeditionEventStepKind.ShowTeamHpLoss or ExpeditionEventStepKind.PickMemberHpLoss)
            {
                if (TryFailRunIfPartyWiped())
                {
                    _run.EventInteraction = null;
                    return true;
                }
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
                    member.Hp = System.Math.Max(0, member.Hp - System.Math.Abs(step.FlatHpDelta));
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
                member.Hp = System.Math.Max(0, member.Hp - maxHpLoss);
                return;
            }

            var currentHpLoss = System.Math.Max(1, member.Hp * System.Math.Abs(step.PercentHpDelta) / 100);
            member.Hp = System.Math.Max(0, member.Hp - currentHpLoss);
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
            if (TryFailRunIfPartyWiped())
                return;

            ApplyPendingTravelerGift();
            if (TryFailRunIfPartyWiped())
                return;

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

        static bool AreOnlyShowMessageSteps(IReadOnlyList<ExpeditionEventInteractionStep> steps)
        {
            if (steps == null || steps.Count == 0)
                return false;

            foreach (var step in steps)
            {
                if (step == null || step.Kind != ExpeditionEventStepKind.ShowMessage)
                    return false;
            }

            return true;
        }
    }
}
