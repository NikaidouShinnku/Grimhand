using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Events;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗/远征会话：无渲染，供 IMGUI 与 uGUI 共用。</summary>
    public sealed class BattleSession
    {
        readonly List<string> _log = new();
        readonly BattleTurnLogRecorder _turnLog = new();
        readonly List<string> _consumablesUsedThisBattle = new();

        public BattleEngine Engine { get; private set; }
        public ExpeditionEngine Expedition { get; private set; }
        public bool IsExpeditionMode => Expedition != null;
        public IReadOnlyList<string> Log => _log;
        public IReadOnlyList<string> ConsumablesUsedThisBattle => _consumablesUsedThisBattle;
        public BattleTurnLogRecorder TurnLog => _turnLog;

        bool _battleEndHandled;
        bool _presentationLocked;
        PresentationSnapshot _presentationSnapshot;

        public bool PresentationLocked
        {
            get => _presentationLocked;
            set => _presentationLocked = value;
        }

        public PresentationSnapshot PresentationSnapshot => _presentationSnapshot;

        public void BeginPresentation(PresentationSnapshot snapshot)
        {
            _presentationSnapshot = snapshot;
            _presentationLocked = true;
        }

        public void EndPresentation()
        {
            _presentationSnapshot = null;
            _presentationLocked = false;
        }

        /// <summary>立绘演出全部播完后调用，再处理远征结算 / 路线选择。</summary>
        public void OnPresentationComplete()
        {
            if (TryFlushPendingEndOfTurn())
                return;

            EndPresentation();
            CheckExpeditionBattleEnd();
            NotifyChanged();
        }

        public event Action Changed;
        public event Action<IReadOnlyList<BattleEvent>> EventsProduced;

        public void Configure(BattleSetupSO battleSetup, ExpeditionSetupSO expeditionSetup)
        {
            BattleSetup = battleSetup;
            ExpeditionSetup = expeditionSetup;
        }

        public BattleSetupSO BattleSetup { get; private set; }
        public ExpeditionSetupSO ExpeditionSetup { get; private set; }

        CampRosterState _campRoster;
        CampMetaState _campMeta;

        public void SetCampRoster(CampRosterState roster) => _campRoster = roster;
        public void SetCampMeta(CampMetaState meta) => _campMeta = meta ?? CampMetaState.CreateDefaultDemo();

        public void Start()
        {
            if (ExpeditionSetup != null)
                BeginExpedition();
            else
                RestartBattle();
        }

        public void BeginExpedition(CampRosterState campRoster = null, int mapStartLayer = 1)
        {
            if (campRoster != null)
                _campRoster = campRoster;

            var config = BuildExpeditionConfig(mapStartLayer);
            if (config.CombatEncounters.Count == 0)
            {
                Debug.LogError("远征配置无战斗遭遇，回退单场战斗。");
                Expedition = null;
                RestartBattle();
                return;
            }

            Expedition = new ExpeditionEngine(config);
            Expedition.StartRun(_campRoster, _campMeta ?? CampMetaState.CreateDefaultDemo());
            _log.Clear();
            _turnLog.Reset();
            _battleEndHandled = false;
            AddLog($"远征开始 — 共 {Expedition.Run.Map?.ChapterLayerCount ?? Expedition.Run.TargetBattleCount} 层 · 请先选择路线");
            NotifyChanged();
        }

        public void BeginGhostQueenBossTest(BattleSetupSO ghostQueenBossSetup)
        {
            if (ExpeditionSetup == null)
            {
                Debug.LogError("Boss 测试需要 Expedition Setup（卡牌池 / 遗物）。");
                RestartBattle();
                return;
            }

            if (ghostQueenBossSetup == null)
            {
                Debug.LogError("Boss 测试需要 Ghost Queen Battle Setup。");
                RestartBattle();
                return;
            }

            var config = BuildExpeditionConfig();
            Expedition = new ExpeditionEngine(config);
            Expedition.StartGhostQueenBossTest(ghostQueenBossSetup.ToBattleConfig());
            _log.Clear();
            _turnLog.Reset();
            _battleEndHandled = false;
            AddLog("幽灵女王 Boss 测试 — 全队 Lv.7 · 3 遗物 · 每人 +3 卡牌");
            foreach (var line in Expedition.Run.RunAcquisitionLog)
                AddLog(line);
            StartExpeditionBattle();
        }

        public void RestartBattle()
        {
            Expedition = null;
            _battleEndHandled = false;
            var config = BattleSetup != null
                ? BattleSetup.ToBattleConfig()
                : DemoBattleFactory.CreateDefault3v3();
            config.Seed = UnityEngine.Random.Range(1, int.MaxValue);
            Engine = new BattleEngine(config);
            _log.Clear();
            _turnLog.Reset();
            _consumablesUsedThisBattle.Clear();
            Engine.StartBattle();
            DrainEvents();
            AddLog($"战斗开始 — 种子 {config.Seed}");
            NotifyChanged();
        }

        public void StartExpeditionBattle()
        {
            var config = Expedition.Run.CurrentBattleConfig;
            Engine = new BattleEngine(config);
            Engine.StartBattle();
            DrainEvents();
            _battleEndHandled = false;
            _turnLog.Reset();
            _consumablesUsedThisBattle.Clear();
            AddLog($"第 {Expedition.CurrentBattleNumber}/{Expedition.Run.TargetBattleCount} 场 — 种子 {config.Seed}");
            AddLog("队伍 HP: " + BattleUiFormatters.FormatPartyHpLine(Expedition.Run.Party));
            NotifyChanged();
        }

        public bool ToggleCard(int instanceId)
        {
            if (Engine == null || !CanInteractWithBattle())
                return false;

            if (Engine.Draft.IsAwaitingConsumableTarget)
                Engine.CancelConsumableTargeting();

            var ok = Engine.ToggleCardSelection(instanceId);
            if (ok)
                DrainEvents();
            else
                NotifyChanged();

            return ok;
        }

        public bool CommitPlan()
        {
            if (Engine == null || !CanInteractWithBattle())
                return false;

            BeginPresentation(PresentationSnapshot.CaptureForTurnPresentation(
                Engine.State, Engine.Draft, Engine));
            Engine.PresentationCheckpointRecorder = (eventIndex, kind, state) =>
                _presentationSnapshot?.RecordEventCheckpoint(eventIndex, kind, state);
            var ok = Engine.CommitPlayerPlan();
            Engine.PresentationCheckpointRecorder = null;
            if (ok)
                DrainEvents();
            else
                EndPresentation();

            return ok;
        }

        public bool SkipTurn()
        {
            if (Engine == null || !CanInteractWithBattle())
                return false;

            BeginPresentation(PresentationSnapshot.CaptureForTurnPresentation(
                Engine.State, Engine.Draft, Engine));
            Engine.PresentationCheckpointRecorder = (eventIndex, kind, state) =>
                _presentationSnapshot?.RecordEventCheckpoint(eventIndex, kind, state);
            var ok = Engine.SkipPlayerTurn();
            Engine.PresentationCheckpointRecorder = null;
            if (ok)
                DrainEvents();
            else
                EndPresentation();

            return ok;
        }

        public bool AssignTarget(string combatantId)
        {
            if (Engine == null || !CanInteractWithBattle())
                return false;

            if (Engine.Draft.IsAwaitingConsumableTarget)
            {
                var slot = Engine.Draft.PendingConsumableSlotIndex;
                if (!Engine.TryAssignConsumableTarget(combatantId))
                    return false;

                if (Expedition != null && slot >= 0)
                    ConsumableInventory.RemoveAt(Expedition.Run.ConsumableSlots, slot);

                DrainEvents();
                NotifyChanged();
                return true;
            }

            var ok = Engine.Draft.TryAssignTargetAndSelect(combatantId);
            if (ok)
                DrainEvents();
            return ok;
        }

        public bool TryUseConsumableFromSlot(int slotIndex)
        {
            if (Engine == null || Expedition == null || !CanInteractWithBattle())
                return false;

            ConsumableInventory.EnsureInitialized(Expedition.Run.ConsumableSlots);
            if (slotIndex < 0 || slotIndex >= ConsumableInventory.MaxSlots)
                return false;

            var consumableId = Expedition.Run.ConsumableSlots[slotIndex];
            if (string.IsNullOrEmpty(consumableId))
                return false;

            if (!ConsumableDatabase.TryGet(consumableId, out var definition))
                return false;

            if (definition.EffectKind == ConsumableEffectKind.MirrorLastAttack
                && !ConsumableRules.CanUseMirrorShard(Engine.State, out var mirrorError))
            {
                AddLog(mirrorError);
                NotifyChanged();
                return false;
            }

            if (ConsumableRules.NeedsTarget(definition))
            {
                if (Engine.Draft.IsAwaitingConsumableTarget
                    && Engine.Draft.AwaitingConsumableSlotIndex == slotIndex)
                {
                    CancelTargetSelection();
                    NotifyChanged();
                    return true;
                }

                if (!Engine.TryBeginConsumableUse(consumableId, slotIndex))
                    return false;

                AddLog($"使用 {definition.DisplayName} — 请选择目标");
                NotifyChanged();
                return true;
            }

            if (!Engine.TryBeginConsumableUse(consumableId, slotIndex))
                return false;

            ConsumableInventory.RemoveAt(Expedition.Run.ConsumableSlots, slotIndex);
            AddLog($"使用 {definition.DisplayName}");
            DrainEvents();
            return true;
        }

        public bool ReplaceConsumableSlot(int slotIndex)
        {
            if (Expedition?.TryReplaceConsumableSlot(slotIndex) != true)
                return false;

            AddLog("已替换消耗品栏位");
            NotifyChanged();
            return true;
        }

        public bool AbandonConsumableOffer()
        {
            if (Expedition?.TryAbandonConsumableOffer() != true)
                return false;

            AddLog("已放弃新消耗品");
            NotifyChanged();
            return true;
        }

        public void SetCardAltarDraft(string memberId, int collectionIndex, string replaceDeckCardKey)
        {
            Expedition?.SetCardAltarMemberDraft(memberId, collectionIndex, replaceDeckCardKey);
            NotifyChanged();
        }

        public bool ConfirmCardAltar()
        {
            if (Expedition?.TryConfirmCardAltar() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool LeaveAltar()
        {
            if (Expedition?.TryLeaveAltar() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool UpgradeAltarMemberHp(string memberId)
        {
            if (Expedition?.TryUpgradeAltarMemberHp(memberId) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool UpgradeAltarEnergyCap()
        {
            if (Expedition?.TryUpgradeAltarEnergyCap() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool UpgradeAltarHandLimit()
        {
            if (Expedition?.TryUpgradeAltarHandLimit() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool UpgradeAltarCard(string memberId, string deckInstanceId, string displayName)
        {
            if (Expedition?.TryUpgradeAltarCard(memberId, deckInstanceId, displayName) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool ReplaceDeckCardForOffer(string deckCardKey)
        {
            if (Expedition?.TryReplaceDeckCardForPendingOffer(deckCardKey) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");

            NotifyChanged();
            return true;
        }

        public bool AbandonCardOffer()
        {
            if (Expedition?.TryAbandonPendingCardOffer() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");

            NotifyChanged();
            return true;
        }

        public void CancelTargetSelection()
        {
            if (Engine == null)
                return;

            if (Engine.Draft.IsAwaitingConsumableTarget)
                Engine.CancelConsumableTargeting();

            Engine.Draft.CancelAwaitingTarget();
            NotifyChanged();
        }

        public bool SelectRoute(int routeIndex)
        {
            if (Expedition == null)
                return false;

            var ok = Expedition.TrySelectRoute(routeIndex);
            if (!ok)
                return false;

            if (Expedition.Run.Phase == ExpeditionPhase.InBattle)
                StartExpeditionBattle();
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");
            else if (Expedition.Run.Phase == ExpeditionPhase.EventChoice)
                AddLog("遭遇特殊事件");
            else if (Expedition.Run.Phase == ExpeditionPhase.ShrineChoice)
                AddLog("发现祭坛 — 召唤卡牌");
            else if (Expedition.Run.Phase == ExpeditionPhase.ShopVisit)
                AddLog("遇到流浪商人");

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool CompleteEventInteractionStep(
            string selectedCharacterId = null,
            string selectedCardKey = null,
            string selectedSecondCardKey = null)
        {
            if (Expedition?.CompleteEventInteractionStep(selectedCharacterId, selectedCardKey, selectedSecondCardKey) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.InBattle)
                StartExpeditionBattle();
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");
            else if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool ResolveEventChoice(int choiceIndex)
        {
            if (Expedition?.TryResolveEventChoice(choiceIndex) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.EventAftermath)
            {
                NotifyChanged();
                return true;
            }

            if (Expedition.Run.Phase == ExpeditionPhase.InBattle)
                StartExpeditionBattle();
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");
            else if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool ConfirmEventAftermath()
        {
            if (Expedition?.TryConfirmEventAftermath() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.InBattle)
                StartExpeditionBattle();
            else if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");
            else if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool ResolveShrineChoice(int choiceIndex)
        {
            if (Expedition?.TryResolveShrineChoice(choiceIndex) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (Expedition.Run.Phase == ExpeditionPhase.RewardPickup)
                AddLog("拾取奖励 — 点击领取或放弃");
            else if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool BuyShopOffer(int slotIndex)
        {
            if (Expedition?.TryBuyShopOffer(slotIndex) != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            if (!string.IsNullOrEmpty(Expedition.Run.PendingCardOffer?.Template?.DisplayName))
                AddLog("卡组已满 — 请选择要替换的卡牌");

            NotifyChanged();
            return true;
        }

        public bool RefreshShop()
        {
            if (Expedition?.TryRefreshShop() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.LastEventMessage))
                AddLog(Expedition.Run.LastEventMessage);

            NotifyChanged();
            return true;
        }

        public bool LeaveShop()
        {
            if (Expedition?.TryLeaveShop() != true)
                return false;

            AddLog(Expedition.Run.LastEventMessage);
            AddLog("请选择前进路线");
            NotifyChanged();
            return true;
        }

        public bool ClaimRewardGold()
        {
            var amount = Expedition?.Run.PendingRewardPickup?.Gold ?? 0;
            if (Expedition?.TryClaimRewardGold() != true)
                return false;

            AddLog($"领取金币 +{amount}（合计 {Expedition.Run.Gold}）");
            NotifyChanged();
            return true;
        }

        public bool SkipRewardGold()
        {
            if (Expedition?.TrySkipRewardGold() != true)
                return false;

            AddLog("放弃金币");
            NotifyChanged();
            return true;
        }

        public bool ClaimRewardRelic()
        {
            var relicId = Expedition?.Run.PendingRewardPickup?.RelicId;
            if (Expedition?.TryClaimRewardRelic() != true)
                return false;

            if (RelicDatabase.TryGet(relicId, out var relic))
                AddLog($"获得遗物：{relic.DisplayName}");
            NotifyChanged();
            return true;
        }

        public bool SkipRewardRelic()
        {
            if (Expedition?.TrySkipRewardRelic() != true)
                return false;

            AddLog("放弃遗物");
            NotifyChanged();
            return true;
        }

        public bool ClaimRewardCard()
        {
            var cardName = Expedition?.Run.PendingRewardPickup?.CardDisplayName;
            if (Expedition?.TryClaimRewardCard() != true)
                return false;

            if (!string.IsNullOrEmpty(Expedition.Run.PendingCardOffer?.Template?.DisplayName))
                AddLog($"卡牌 {cardName} — 卡组已满，请选择要替换的卡牌");
            else
                AddLog($"卡牌加入卡组：{cardName}");

            NotifyChanged();
            return true;
        }

        public bool SkipRewardCard()
        {
            if (Expedition?.TrySkipRewardCard() != true)
                return false;

            AddLog("放弃卡牌");
            NotifyChanged();
            return true;
        }

        public bool ClaimRewardConsumable()
        {
            var consumableId = Expedition?.Run.PendingRewardPickup?.ConsumableId;
            if (Expedition?.TryClaimRewardConsumable() != true)
                return false;

            if (ConsumableDatabase.TryGet(consumableId, out var def))
                AddLog(string.IsNullOrEmpty(Expedition.Run.PendingConsumableOfferId)
                    ? $"获得消耗品：{def.DisplayName}"
                    : $"获得消耗品：{def.DisplayName} — 请选择替换栏位");

            NotifyChanged();
            return true;
        }

        public bool SkipRewardConsumable()
        {
            if (Expedition?.TrySkipRewardConsumable() != true)
                return false;

            AddLog("放弃消耗品");
            NotifyChanged();
            return true;
        }

        public bool ClaimRewardStat()
        {
            var rewards = Expedition?.Run.PendingRewardPickup;
            if (Expedition?.TryClaimRewardStat() != true)
                return false;

            AddLog(BuildStatRewardLog(rewards));
            NotifyChanged();
            return true;
        }

        public bool SkipRewardStat()
        {
            if (Expedition?.TrySkipRewardStat() != true)
                return false;

            AddLog("放弃属性奖励");
            NotifyChanged();
            return true;
        }

        static string BuildStatRewardLog(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return "获得属性奖励";

            if (rewards.PersonalAttackBonus != 0 && !string.IsNullOrEmpty(rewards.StatCharacterName))
                return $"{rewards.StatCharacterName} 增伤 +{rewards.PersonalAttackBonus}";

            if (rewards.TeamAttackBonus != 0)
                return $"全队增伤 +{rewards.TeamAttackBonus}";

            if (rewards.TeamDefenseBonus != 0)
                return $"全队防御 +{rewards.TeamDefenseBonus}";

            if (rewards.EnergyCapBonus != 0)
                return $"能量上限 +{rewards.EnergyCapBonus}";

            if (rewards.GrantXp > 0)
                return $"全队经验 +{rewards.GrantXp}";

            return "获得属性奖励";
        }

        public bool ClaimVictoryGold() => ClaimRewardGold();

        public bool ClaimVictoryRelic() => ClaimRewardRelic();

        public bool ClaimVictoryCard() => ClaimRewardCard();

        public bool SkipAllRemainingRewards()
        {
            if (Expedition?.TrySkipAllRemainingRewards() != true)
                return false;

            AddLog("已放弃剩余奖励");
            if (Expedition.Run.Phase == ExpeditionPhase.RouteSelect)
                AddLog("请选择前进路线");

            NotifyChanged();
            return true;
        }

        public bool SkipVictoryOptionalRewards() => SkipAllRemainingRewards();

        public bool ClaimChestGold() => ClaimRewardGold();

        public bool ClaimChestRelic() => ClaimRewardRelic();

        public bool TryGrantRelic(string relicId) =>
            Expedition?.TryAddRelic(relicId) ?? false;

        public void RestartRunOrBattle()
        {
            if (IsExpeditionMode)
                BeginExpedition();
            else
                RestartBattle();
        }

        public bool CanInteractWithBattle() =>
            Engine != null &&
            !Engine.State.AwaitingFelskullChoice &&
            Engine.State.Phase == TurnPhase.Planning &&
            !PresentationLocked &&
            (Expedition == null || Expedition.Run.Phase == ExpeditionPhase.InBattle);

        public void ApplyFelskullChoice(int choiceIndex)
        {
            if (Engine == null || !Engine.State.AwaitingFelskullChoice)
                return;

            var events = new List<BattleEvent>();
            RelicEffectRules.ApplyFelskullChoice(Engine.State, choiceIndex, events);
            foreach (var evt in events)
                _log.Add(evt.Message);

            Engine.ResumeAfterFelskullChoice();
            DrainEvents();
            NotifyChanged();
        }

        public bool ExpeditionBlocksInput =>
            Expedition != null && Expedition.Run.Phase is not ExpeditionPhase.InBattle;

        public void Tick()
        {
            if (Engine == null || PresentationLocked)
                return;

            CheckExpeditionBattleEnd();
        }

        ExpeditionConfig BuildExpeditionConfig(int mapStartLayer = 1)
        {
            ExpeditionConfig config;
            if (ExpeditionSetup != null)
                config = ExpeditionSetup.ToExpeditionConfig();
            else
            {
                config = new ExpeditionConfig
                {
                    RunSeed = UnityEngine.Random.Range(1, int.MaxValue),
                    ChapterLayerCount = ExpeditionRegionRules.FullLayerCount,
                    TargetBattleCount = ExpeditionRegionRules.FullLayerCount - 1,
                    RoutesPerVictory = 3,
                    GoldMinPerVictory = 15,
                    GoldMaxPerVictory = 25
                };

                var encounter = BattleSetup != null
                    ? BattleSetup.ToBattleConfig()
                    : DemoBattleFactory.CreateDefault3v3();
                config.CombatEncounters.Add(encounter);
                MonsterTemplateBootstrap.EnsureMonsterTemplates(config);
            }

            ExpeditionRegionRules.ApplyMapStartLayer(config, mapStartLayer);
            return config;
        }

        void CheckExpeditionBattleEnd()
        {
            if (Expedition == null || Engine == null || _battleEndHandled)
                return;

            var state = Engine.State;
            if (state.Outcome == BattleOutcome.Ongoing)
                return;

            _battleEndHandled = true;
            Expedition.OnBattleFinished(state);

            switch (Expedition.Run.Phase)
            {
                case ExpeditionPhase.RewardPickup:
                    if (Expedition.Run.PendingRewardPickup?.Kind == RewardPickupKind.BattleVictory)
                    {
                        AddLog($"第 {Expedition.Run.BattlesWon} 场胜利 — 待领取奖励");
                        AddLog($"经验池 +{Expedition.Run.LastXpReward}（当前 {Expedition.Run.SharedXpPool}）");
                    }

                    AddLog("点击领取或放弃每项奖励");
                    break;
                case ExpeditionPhase.RouteSelect:
                    AddLog("请选择前进路线");
                    AddLog(BattleUiFormatters.FormatPartySummary(Expedition.Run.Party, Expedition.Run.Gold));
                    break;
                case ExpeditionPhase.RunComplete:
                    AddLog("远征完成！");
                    break;
                case ExpeditionPhase.RunFailed:
                    AddLog("远征失败。");
                    break;
            }

            NotifyChanged();
        }

        void DrainEvents()
        {
            if (Engine == null)
                return;

            if (Engine.Events.Count == 0)
            {
                if (!PresentationLocked)
                    CheckExpeditionBattleEnd();
                return;
            }

            var batch = new List<BattleEvent>(Engine.Events);
            for (var i = 0; i < batch.Count; i++)
                batch[i].EventIndex = i;

            foreach (var e in batch)
                AppendEventLog(e);

            Engine.ClearEvents();

            var hasPresentation = BattleEventPlayback.ContainsPresentationEvents(batch);
            if (hasPresentation)
            {
                var segments = BattleEventPlayback.SplitIntoSegments(batch);
                if (segments.Count > 0)
                    EventsProduced?.Invoke(batch);
                else if (TryFlushPendingEndOfTurn())
                    return;
                else if (PresentationLocked)
                    CompletePresentationAndNotify();
                else
                    CheckExpeditionBattleEnd();
            }
            else if (TryFlushPendingEndOfTurn())
            {
                return;
            }
            else if (PresentationLocked)
            {
                CompletePresentationAndNotify();
            }
            else
            {
                CheckExpeditionBattleEnd();
            }

            NotifyChanged();
        }

        bool TryFlushPendingEndOfTurn()
        {
            if (Engine == null || !Engine.EndOfTurnPending)
                return false;

            Engine.FlushPendingEndOfTurn();
            DrainEvents();
            return true;
        }

        void CompletePresentationAndNotify()
        {
            EndPresentation();
            CheckExpeditionBattleEnd();
            NotifyChanged();
        }

        void AppendEventLog(BattleEvent e)
        {
            if (Engine != null)
                _turnLog.Feed(e, Engine.State);

            if (e.Kind == BattleEventKind.ConsumableUsed)
            {
                var label = string.IsNullOrEmpty(e.Message) ? "消耗品" : e.Message;
                if (!_consumablesUsedThisBattle.Contains(label))
                    _consumablesUsedThisBattle.Add(label);
            }

            switch (e.Kind)
            {
                case BattleEventKind.BattleEnded:
                    AddLog($"战斗结束: {e.Outcome}");
                    break;
                case BattleEventKind.CharacterDied:
                    AddLog($"阵亡: {e.CombatantId}");
                    break;
                case BattleEventKind.DeckPolluted:
                    AddLog($"牌堆污染: {e.CombatantId} ({e.Amount} 张)");
                    break;
                case BattleEventKind.DamageApplied:
                    AddLog(string.IsNullOrEmpty(e.Message)
                        ? $"伤害 {e.Amount}"
                        : $"伤害 {e.Amount}: {e.Message}");
                    break;
                case BattleEventKind.PlanCommitted:
                case BattleEventKind.TurnSkipped:
                case BattleEventKind.StatusTickDamage:
                case BattleEventKind.StatusApplied:
                case BattleEventKind.CardResolvedStarted:
                    AddLog(BattleEventLogFormatter.Format(e, Engine.State));
                    break;
            }
        }

        void AddLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 200)
                _log.RemoveAt(0);
        }

        void NotifyChanged() => Changed?.Invoke();
    }
}
