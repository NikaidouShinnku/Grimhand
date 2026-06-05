using System.Collections.Generic;
using Grimhand.Expedition.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "ExpeditionSetup", menuName = "Grimhand/Expedition Setup")]
    public class ExpeditionSetupSO : ScriptableObject
    {
        [Tooltip("远征随机种子；0 表示每次开局随机。每场战斗仍单独随机种子。")]
        public int RunSeed = 0;

        [Tooltip("通关所需战斗场数。")]
        public int TargetBattleCount = 3;

        [Tooltip("每场胜利后提供的路线数量。")]
        public int RoutesPerVictory = 3;

        [Tooltip("普通战斗胜利金币下限（含）。")]
        public int GoldMinPerVictory = 15;

        [Tooltip("普通战斗胜利金币上限（含）。")]
        public int GoldMaxPerVictory = 25;

        [Tooltip("普通战斗胜利每场 XP（每名存活角色）。")]
        public int XpPerVictory = 16;

        [Tooltip("普通战斗遭遇；Demo 可只填一张并复用。")]
        public List<BattleSetupSO> CombatEncounters = new();

        public ExpeditionConfig ToExpeditionConfig()
        {
            var runSeed = RunSeed;
            if (runSeed <= 0)
                runSeed = Random.Range(1, int.MaxValue);

            var config = new ExpeditionConfig
            {
                RunSeed = runSeed,
                TargetBattleCount = TargetBattleCount,
                RoutesPerVictory = RoutesPerVictory,
                GoldMinPerVictory = GoldMinPerVictory,
                GoldMaxPerVictory = GoldMaxPerVictory,
                XpPerVictory = XpPerVictory
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
