using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>营地训练场：单假人、不出牌、超高血量。</summary>
    public static class TrainingGroundEncounterBuilder
    {
        public const string DummyCharacterId = "char_dummy";
        public const string DummyDisplayName = "训练假人";
        public const int DummyMaxHp = 99999;

        public static BattleConfig BuildTemplate(BattleConfig playerBaseline)
        {
            var source = playerBaseline ?? new BattleConfig();
            var config = ExpeditionBattleConfigBuilder.CloneTemplate(source);

            for (var i = config.Combatants.Count - 1; i >= 0; i--)
            {
                if (config.Combatants[i].Team == TeamSide.Enemy)
                    config.Combatants.RemoveAt(i);
            }

            config.EnemyCardsDrawnPerTurn = 0;
            config.EnemyTurnEnergyBudget = 0;
            config.SkipFloorScaling = true;
            config.ManualEnemyIntentsOnly = true;
            config.VictoryOnCharacterDeathId = DummyCharacterId;
            config.HandLimit = 10;

            config.Combatants.Add(new CombatantConfig
            {
                Id = "enemy_dummy_0",
                DisplayName = DummyDisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = DummyCharacterId,
                Level = 1,
                MaxHp = DummyMaxHp,
                StartHp = DummyMaxHp,
                BaseAttack = 0,
                BaseDefense = 0,
                // 必须高于玩家，否则威慑/命令等「打断/应对」类效果总在玩家出完牌后才结算，测起来像完全没生效
                Speed = 99,
                UseSkillPool = true
            });

            // 图鉴/假人出牌测「召唤骨之王座」等时必须有召唤模板，否则静默失败
            config.SummonTemplates[SummonRules.ExplosiveSkullCharacterId] =
                SummonRules.CreateDefaultExplosiveSkullTemplate();

            // 黑暗骑士「号令亡者」/ 典狱长囚笼相关
            if (!config.SummonTemplates.ContainsKey("char_spider_lady"))
            {
                var spider = new CombatantConfig
                {
                    DisplayName = "蜘蛛贵妇",
                    Team = TeamSide.Enemy,
                    Slot = FormationSlot.Back,
                    CharacterDefinitionId = "char_spider_lady",
                    MaxHp = 60,
                    BaseAttack = 9,
                    BaseDefense = 4,
                    Speed = 7,
                    UseSkillPool = true
                };
                spider.Traits.Add(MinionTraitCatalog.SpiderLadyPoisonVulnerability);
                config.SummonTemplates["char_spider_lady"] = spider;
            }

            if (!config.SummonTemplates.ContainsKey(CharacterTraitCatalog.PrisonCageCharacterId))
            {
                var cage = new CombatantConfig
                {
                    DisplayName = "囚笼",
                    Team = TeamSide.Enemy,
                    Slot = FormationSlot.Middle,
                    CharacterDefinitionId = CharacterTraitCatalog.PrisonCageCharacterId,
                    MaxHp = 150,
                    Speed = 5
                };
                cage.Traits.Add(CharacterTraitCatalog.PrisonCage);
                config.SummonTemplates[CharacterTraitCatalog.PrisonCageCharacterId] = cage;
            }

            // 囚笼死亡替换模板（训练场测典狱长特性）
            void EnsureReplacement(string id, string name, int hp, int spd, string trait = null)
            {
                if (config.SummonTemplates.ContainsKey(id))
                    return;
                var unit = new CombatantConfig
                {
                    DisplayName = name,
                    Team = TeamSide.Enemy,
                    Slot = FormationSlot.Middle,
                    CharacterDefinitionId = id,
                    MaxHp = hp,
                    Speed = spd,
                    UseSkillPool = true
                };
                if (!string.IsNullOrEmpty(trait))
                    unit.Traits.Add(trait);
                config.SummonTemplates[id] = unit;
            }

            EnsureReplacement("char_skeleton_elite", "骷髅精英", 45, 5, MinionTraitCatalog.SkeletonEliteCardStats);
            EnsureReplacement("char_wraith_elite", "幽灵精英", 35, 8);
            EnsureReplacement("char_bat", "巨翼蝙蝠", 55, 9);
            // 终焉召唤：强制 1 层基础 MaxHp=95（召唤 HP = 95 + 海巫MaxHp/2）
            ForceAbyssCreatureSummonTemplate(config, 95, 4);

            return config;
        }

        static void ForceAbyssCreatureSummonTemplate(BattleConfig config, int maxHp, int speed)
        {
            var id = MinionTraitCatalog.AbyssCreatureCharacterId;
            if (config.SummonTemplates.TryGetValue(id, out var existing) && existing != null)
            {
                existing.MaxHp = maxHp;
                existing.Speed = speed;
                existing.DisplayName = "深渊怪物";
                existing.CharacterDefinitionId = id;
                existing.Team = TeamSide.Enemy;
                if (!existing.Traits.Contains(MinionTraitCatalog.AbyssCreaturePoisonOnDamage))
                    existing.Traits.Add(MinionTraitCatalog.AbyssCreaturePoisonOnDamage);
                return;
            }

            var unit = new CombatantConfig
            {
                DisplayName = "深渊怪物",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = id,
                MaxHp = maxHp,
                Speed = speed,
                UseSkillPool = true
            };
            unit.Traits.Add(MinionTraitCatalog.AbyssCreaturePoisonOnDamage);
            config.SummonTemplates[id] = unit;
        }
    }
}
