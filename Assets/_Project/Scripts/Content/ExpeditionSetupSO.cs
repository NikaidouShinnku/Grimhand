using System.Collections.Generic;
using Grimhand.Expedition.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "ExpeditionSetup", menuName = "Grimhand/Expedition Setup")]
    public class ExpeditionSetupSO : ScriptableObject
    {
        [Tooltip("远征随机种子；每场战斗仍单独随机种子。")]
        public int RunSeed = 42;

        [Tooltip("通关所需战斗场数。")]
        public int TargetBattleCount = 3;

        [Tooltip("每场胜利后提供的路线数量。")]
        public int RoutesPerVictory = 3;

        [Tooltip("普通战斗遭遇；Demo 可只填一张并复用。")]
        public List<BattleSetupSO> CombatEncounters = new();

        public ExpeditionConfig ToExpeditionConfig()
        {
            var config = new ExpeditionConfig
            {
                RunSeed = RunSeed,
                TargetBattleCount = TargetBattleCount,
                RoutesPerVictory = RoutesPerVictory
            };

            foreach (var encounter in CombatEncounters)
            {
                if (encounter == null)
                    continue;

                config.CombatEncounters.Add(encounter.ToBattleConfig());
            }

            return config;
        }
    }
}
