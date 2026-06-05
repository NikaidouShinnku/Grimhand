using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public sealed class ExpeditionEngine
    {
        static readonly string[] RouteNamePool =
        {
            "林间小径",
            "矿石通道",
            "废弃营地",
            "潮湿洞窟",
            "断裂石桥",
            "低语森林"
        };

        static readonly string[] RouteFlavorPool =
        {
            "看起来平静，但哥布林从不远离道路。",
            "窄道两侧是峭壁，适合埋伏。",
            "有人在此扎营的痕迹，如今只剩敌人。",
            "水滴声回荡，危险潜藏在暗处。",
            "桥板吱呀作响，前方有动静。",
            "树影婆娑，你感到注视。",
        };

        readonly ExpeditionConfig _config;
        readonly BattleRng _rng;
        readonly ExpeditionRunState _run = new();

        public ExpeditionEngine(ExpeditionConfig config)
        {
            _config = config ?? new ExpeditionConfig();
            _rng = new BattleRng(_config.RunSeed);
            _run.TargetBattleCount = _config.TargetBattleCount > 0 ? _config.TargetBattleCount : 3;
        }

        public ExpeditionRunState Run => _run;

        public void StartRun()
        {
            _run.Phase = ExpeditionPhase.InBattle;
            _run.BattlesWon = 0;
            _run.Gold = 0;
            _run.LastGoldReward = 0;
            _run.Party.Clear();
            _run.PendingRoutes.Clear();
            _run.CurrentBattleConfig = BuildBattleFromEncounter(0, applyPartyHp: false);
        }

        public void OnBattleFinished(BattleState state)
        {
            if (state == null)
                return;

            _run.Party.Clear();
            _run.Party.AddRange(ExpeditionBattleConfigBuilder.CaptureParty(state));

            if (state.Outcome == BattleOutcome.PlayerDefeat)
            {
                _run.Phase = ExpeditionPhase.RunFailed;
                _run.PendingRoutes.Clear();
                return;
            }

            if (state.Outcome != BattleOutcome.PlayerVictory)
                return;

            _run.BattlesWon++;
            _run.LastGoldReward = ExpeditionEconomy.RollVictoryGold(_config, _rng);
            _run.Gold += _run.LastGoldReward;

            if (_run.BattlesWon >= _run.TargetBattleCount)
            {
                _run.Phase = ExpeditionPhase.RunComplete;
                _run.PendingRoutes.Clear();
                return;
            }

            GenerateRouteOptions();
            _run.Phase = ExpeditionPhase.RouteSelect;
        }

        public bool TrySelectRoute(int routeIndex)
        {
            if (_run.Phase != ExpeditionPhase.RouteSelect)
                return false;

            if (routeIndex < 0 || routeIndex >= _run.PendingRoutes.Count)
                return false;

            var route = _run.PendingRoutes[routeIndex];
            _run.PendingRoutes.Clear();
            _run.Phase = ExpeditionPhase.InBattle;
            _run.CurrentBattleConfig = BuildBattleFromEncounter(route.EncounterIndex, applyPartyHp: true);
            return true;
        }

        public int CurrentBattleNumber => _run.BattlesWon + 1;

        void GenerateRouteOptions()
        {
            _run.PendingRoutes.Clear();
            var routeCount = _config.RoutesPerVictory > 0 ? _config.RoutesPerVictory : 3;

            var nameOffset = _rng.NextIndex(RouteNamePool.Length);
            for (var i = 0; i < routeCount; i++)
            {
                var nameIndex = (nameOffset + i) % RouteNamePool.Length;
                var flavorIndex = (_rng.NextIndex(RouteFlavorPool.Length) + i) % RouteFlavorPool.Length;
                var encounterIndex = PickEncounterIndex();

                _run.PendingRoutes.Add(new ExpeditionRouteOption
                {
                    Id = $"route_{_run.BattlesWon}_{i}",
                    DisplayName = RouteNamePool[nameIndex],
                    Description = RouteFlavorPool[flavorIndex],
                    NodeType = ExpeditionNodeType.Combat,
                    EncounterIndex = encounterIndex
                });
            }
        }

        BattleConfig BuildBattleFromEncounter(int encounterIndex, bool applyPartyHp)
        {
            if (_config.CombatEncounters.Count == 0)
                throw new System.InvalidOperationException("ExpeditionConfig.CombatEncounters is empty.");

            var index = encounterIndex % _config.CombatEncounters.Count;
            var template = _config.CombatEncounters[index];
            var seed = _rng.NextInt(1, int.MaxValue);
            return ExpeditionBattleConfigBuilder.BuildEncounter(
                template,
                _run.Party,
                seed,
                applyPartyHp);
        }

        int PickEncounterIndex()
        {
            if (_config.CombatEncounters.Count == 0)
                return 0;

            return _rng.NextIndex(_config.CombatEncounters.Count);
        }
    }
}
