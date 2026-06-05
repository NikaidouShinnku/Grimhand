using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public sealed class ExpeditionEngine
    {
        static readonly string[] CombatRouteNamePool =
        {
            "暗影通道",
            "哥布林哨站",
            "断裂石桥",
            "低语深坑",
            "蛮兵营地"
        };

        static readonly string[] CombatRouteFlavorPool =
        {
            "敌人盘踞在前方，避无可避。",
            "战鼓隐约可闻，冲突一触即发。",
            "窄道两侧是峭壁，适合埋伏。",
            "水滴声回荡，危险潜藏在暗处。",
            "有人在此扎营的痕迹，如今只剩敌人。"
        };

        static readonly string[] TreasureRouteNamePool =
        {
            "尘封侧室",
            "古老宝箱厅",
            "发光矿脉",
            "坍塌藏宝洞",
            "秘藏龛室"
        };

        static readonly string[] TreasureRouteFlavorPool =
        {
            "门后没有敌人，只有等待开启的宝箱。",
            "金币在暗处闪光，或许还有别的收获。",
            "岩壁上镶嵌着宝石，战利品就在深处。",
            "木箱堆叠在角落，看起来尚未被洗劫。",
            "空气里有金属与魔法残留的味道。"
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
            _run.LastXpReward = 0;
            _run.Party.Clear();
            _run.Relics.Clear();
            _run.PendingRoutes.Clear();
            _run.PendingVictoryRewards = null;
            _run.PendingChestReward = null;
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
                _run.PendingVictoryRewards = null;
                _run.PendingChestReward = null;
                return;
            }

            if (state.Outcome != BattleOutcome.PlayerVictory)
                return;

            _run.BattlesWon++;
            _run.LastXpReward = _config.XpPerVictory > 0 ? _config.XpPerVictory : 16;
            ExpeditionBattleConfigBuilder.GrantXpToParty(_run.Party, _run.LastXpReward);

            _run.PendingVictoryRewards = ExpeditionRewardRoller.RollVictoryRewards(_config, _run, _rng);
            _run.LastGoldReward = _run.PendingVictoryRewards.Gold;
            GenerateRouteOptions();
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

            if (route.NodeType == ExpeditionNodeType.Treasure)
            {
                _run.PendingChestReward = ExpeditionRewardRoller.RollChestReward(_config, _run, _rng);
                _run.Phase = ExpeditionPhase.TreasureLoot;
                return true;
            }

            _run.Phase = ExpeditionPhase.InBattle;
            _run.CurrentBattleConfig = BuildBattleFromEncounter(route.EncounterIndex, applyPartyHp: true);
            return true;
        }

        public bool TryAddRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId) || !RelicDatabase.TryGet(relicId, out _))
                return false;

            if (_run.Relics.Contains(relicId))
                return false;

            _run.Relics.Add(relicId);
            return true;
        }

        public int CurrentBattleNumber => _run.BattlesWon + 1;

        void TryAdvanceFromVictoryRewards()
        {
            if (_run.Phase != ExpeditionPhase.VictoryRewards)
                return;

            var rewards = _run.PendingVictoryRewards;
            if (rewards != null && !rewards.IsFullyResolved)
                return;

            _run.PendingVictoryRewards = null;

            if (_run.BattlesWon >= _run.TargetBattleCount)
            {
                _run.Phase = ExpeditionPhase.RunComplete;
                _run.PendingRoutes.Clear();
                return;
            }

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
            GenerateRouteOptions();
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

        void GenerateRouteOptions()
        {
            _run.PendingRoutes.Clear();
            var routeCount = _config.RoutesPerVictory > 0 ? _config.RoutesPerVictory : 3;

            for (var i = 0; i < routeCount; i++)
            {
                var nodeType = ExpeditionRewardRoller.RollRouteNodeType(_config, _rng);
                var encounterIndex = nodeType == ExpeditionNodeType.Combat
                    ? PickEncounterIndex()
                    : 0;

                var namePool = nodeType == ExpeditionNodeType.Treasure
                    ? TreasureRouteNamePool
                    : CombatRouteNamePool;
                var flavorPool = nodeType == ExpeditionNodeType.Treasure
                    ? TreasureRouteFlavorPool
                    : CombatRouteFlavorPool;

                var nameIndex = _rng.NextIndex(namePool.Length);
                var flavorIndex = (_rng.NextIndex(flavorPool.Length) + i) % flavorPool.Length;

                _run.PendingRoutes.Add(new ExpeditionRouteOption
                {
                    Id = $"route_{_run.BattlesWon}_{i}",
                    DisplayName = namePool[nameIndex],
                    Description = flavorPool[flavorIndex],
                    NodeType = nodeType,
                    EncounterIndex = encounterIndex,
                    PathSpriteIndex = ExpeditionRewardRoller.RollPathSpriteIndex(_rng)
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
                _run.Relics,
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
