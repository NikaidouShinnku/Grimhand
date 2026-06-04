using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗/远征会话：无渲染，供 IMGUI 与 uGUI 共用。</summary>
    public sealed class BattleSession
    {
        readonly List<string> _log = new();

        public BattleEngine Engine { get; private set; }
        public ExpeditionEngine Expedition { get; private set; }
        public bool IsExpeditionMode => Expedition != null;
        public IReadOnlyList<string> Log => _log;

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

        public void Start()
        {
            if (ExpeditionSetup != null)
                BeginExpedition();
            else
                RestartBattle();
        }

        public void BeginExpedition()
        {
            var config = BuildExpeditionConfig();
            if (config.CombatEncounters.Count == 0)
            {
                Debug.LogError("远征配置无战斗遭遇，回退单场战斗。");
                Expedition = null;
                RestartBattle();
                return;
            }

            Expedition = new ExpeditionEngine(config);
            Expedition.StartRun();
            _log.Clear();
            _battleEndHandled = false;
            AddLog($"远征开始 — 共 {Expedition.Run.TargetBattleCount} 场 · 血量跨场不恢复");
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
            AddLog($"第 {Expedition.CurrentBattleNumber}/{Expedition.Run.TargetBattleCount} 场 — 种子 {config.Seed}");
            AddLog("队伍 HP: " + BattleUiFormatters.FormatPartyHpLine(Expedition.Run.Party));
            NotifyChanged();
        }

        public bool ToggleCard(int instanceId)
        {
            if (Engine == null || !CanInteractWithBattle())
                return false;

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

            BeginPresentation(PresentationSnapshot.Capture(Engine.State));
            var ok = Engine.CommitPlayerPlan();
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

            BeginPresentation(PresentationSnapshot.Capture(Engine.State));
            var ok = Engine.SkipPlayerTurn();
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

            var ok = Engine.Draft.TryAssignTargetAndSelect(combatantId);
            if (ok)
                DrainEvents();
            return ok;
        }

        public void CancelTargetSelection()
        {
            if (Engine == null)
                return;

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

            StartExpeditionBattle();
            return true;
        }

        public void RestartRunOrBattle()
        {
            if (IsExpeditionMode)
                BeginExpedition();
            else
                RestartBattle();
        }

        public bool CanInteractWithBattle() =>
            Engine != null &&
            Engine.State.Phase == TurnPhase.Planning &&
            !PresentationLocked &&
            (Expedition == null || Expedition.Run.Phase == ExpeditionPhase.InBattle);

        public bool ExpeditionBlocksInput =>
            Expedition != null && Expedition.Run.Phase != ExpeditionPhase.InBattle;

        public void Tick()
        {
            if (Engine == null || PresentationLocked)
                return;

            CheckExpeditionBattleEnd();
        }

        ExpeditionConfig BuildExpeditionConfig()
        {
            if (ExpeditionSetup != null)
                return ExpeditionSetup.ToExpeditionConfig();

            var config = new ExpeditionConfig
            {
                RunSeed = UnityEngine.Random.Range(1, int.MaxValue),
                TargetBattleCount = 3,
                RoutesPerVictory = 3
            };

            var encounter = BattleSetup != null
                ? BattleSetup.ToBattleConfig()
                : DemoBattleFactory.CreateDefault3v3();
            config.CombatEncounters.Add(encounter);
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
                case ExpeditionPhase.RouteSelect:
                    AddLog($"第 {Expedition.Run.BattlesWon} 场胜利 — 请选择前进路线");
                    AddLog("队伍 HP: " + BattleUiFormatters.FormatPartyHpLine(Expedition.Run.Party));
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
            foreach (var e in batch)
                AppendEventLog(e);

            Engine.ClearEvents();

            var hasPresentation = BattleEventPlayback.ContainsPresentationEvents(batch);
            if (hasPresentation)
            {
                var segments = BattleEventPlayback.SplitIntoSegments(batch);
                if (segments.Count > 0)
                    EventsProduced?.Invoke(batch);
                else
                    OnPresentationComplete();
            }
            else
            {
                CheckExpeditionBattleEnd();
            }

            NotifyChanged();
        }

        void AppendEventLog(BattleEvent e)
        {
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
