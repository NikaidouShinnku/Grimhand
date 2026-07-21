using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class V09BossMechanicsTests
    {
        [Test]
        public void BrandMark_DetonatesAtThreeStacks()
        {
            var state = new BattleState();
            var player = new CombatantState
            {
                Id = "p1",
                Team = TeamSide.Player,
                MaxHp = 100,
                Hp = 100,
                DisplayName = "骑士"
            };
            state.Combatants.Add(player);

            var events = new List<Events.BattleEvent>();
            StatusRules.ApplyStatus(state, player, StatusCatalog.BrandMark, 1, -1, events);
            StatusRules.ApplyStatus(state, player, StatusCatalog.BrandMark, 1, -1, events);
            StatusRules.ApplyStatus(state, player, StatusCatalog.BrandMark, 1, -1, events);

            Assert.AreEqual(0, player.Hp);
            Assert.IsFalse(StatusRules.HasStatus(player, StatusCatalog.BrandMark));
        }

        [Test]
        public void WardenBuilder_IncludesCageSummonTemplate()
        {
            var config = WardenBossEncounterBuilder.BuildTemplate(null);
            Assert.IsTrue(config.SummonTemplates.ContainsKey(CharacterTraitCatalog.PrisonCageCharacterId));
            Assert.AreEqual(250, config.Combatants.Find(c => c.CharacterDefinitionId == WardenBossEncounterBuilder.CharacterId).MaxHp);
            Assert.AreEqual(FormationSlot.Back,
                config.Combatants.Find(c => c.CharacterDefinitionId == WardenBossEncounterBuilder.CharacterId).Slot);
            Assert.AreEqual(WardenBossEncounterBuilder.CharacterId, config.VictoryOnCharacterDeathId);
        }

        [Test]
        public void WardenBuilder_UsesMonsterTemplatesForCageReplacements()
        {
            var skeletonTemplate = new CombatantConfig
            {
                CharacterDefinitionId = "char_skeleton_elite",
                DisplayName = "骷髅精英",
                Team = TeamSide.Enemy,
                MaxHp = 45,
                UseSkillPool = true
            };
            skeletonTemplate.Traits.Add(MinionTraitCatalog.SkeletonEliteCardStats);
            skeletonTemplate.SkillPoolCandidates.Add(new CardTemplate { DefinitionId = "m_elite_bone_wall" });

            var map = new Dictionary<string, CombatantConfig>
            {
                ["char_skeleton_elite"] = skeletonTemplate
            };

            var config = WardenBossEncounterBuilder.BuildTemplate(null, map);
            var elite = config.SummonTemplates["char_skeleton_elite"];

            Assert.AreEqual(MinionTraitCatalog.SkeletonEliteCardStats, elite.Traits[0]);
            Assert.AreEqual(1, elite.SkillPoolCandidates.Count);
        }

        [Test]
        public void Warden_ProcessBattleStart_SpawnsTwoCagesWithoutCollectionModifiedError()
        {
            var config = WardenBossEncounterBuilder.BuildTemplate(null);
            var state = new BattleState { Config = config };
            var warden = new CombatantState
            {
                Id = "warden",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = WardenBossEncounterBuilder.CharacterId,
                MaxHp = 250,
                Hp = 250,
                DisplayName = "典狱长"
            };
            warden.Traits.Add(CharacterTraitCatalog.WardenCageMaster);
            state.Combatants.Add(warden);

            var events = new List<Events.BattleEvent>();
            Assert.DoesNotThrow(() => V09BossMechanicsRules.ProcessBattleStart(state, events, new BattleRng(7)));

            var cages = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == CharacterTraitCatalog.PrisonCageCharacterId && unit.IsAlive)
                    cages++;
            }

            Assert.AreEqual(3, state.Combatants.Count);
            Assert.AreEqual(2, cages);
            Assert.AreEqual(FormationSlot.Back, warden.Slot);

            var cageSlots = new HashSet<FormationSlot>();
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == CharacterTraitCatalog.PrisonCageCharacterId)
                    cageSlots.Add(unit.Slot);
            }

            Assert.IsTrue(cageSlots.Contains(FormationSlot.Front));
            Assert.IsTrue(cageSlots.Contains(FormationSlot.Middle));
        }

        [Test]
        public void Warden_ProcessBattleStart_SpawnsTwoCagesWhenWardenStartsInFront()
        {
            var config = WardenBossEncounterBuilder.BuildTemplate(null);
            var state = new BattleState { Config = config };
            var warden = new CombatantState
            {
                Id = "warden",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = WardenBossEncounterBuilder.CharacterId,
                MaxHp = 250,
                Hp = 250,
                DisplayName = "典狱长"
            };
            warden.Traits.Add(CharacterTraitCatalog.WardenCageMaster);
            state.Combatants.Add(warden);

            var events = new List<Events.BattleEvent>();
            V09BossMechanicsRules.ProcessBattleStart(state, events, new BattleRng(11));

            var cages = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == CharacterTraitCatalog.PrisonCageCharacterId && unit.IsAlive)
                    cages++;
            }

            Assert.AreEqual(3, state.Combatants.Count);
            Assert.AreEqual(2, cages);
            Assert.AreEqual(FormationSlot.Back, warden.Slot);
        }

        [Test]
        public void OceanGoddess_LockRisingTide_DoesNotTriggerEbb()
        {
            var state = new BattleState { Config = new BattleConfig() };
            var goddess = new CombatantState
            {
                Id = "boss",
                Team = TeamSide.Enemy,
                DisplayName = "女神",
                MaxHp = 400,
                Hp = 400
            };
            goddess.Traits.Add(CharacterTraitCatalog.OceanGoddessTide);
            state.Combatants.Add(goddess);

            var events = new List<Events.BattleEvent>();
            V09BossMechanicsRules.LockRisingTide(state, goddess, 2, events);

            Assert.AreEqual(V09BossMechanicsRules.TideLockedStackCount,
                StatusRules.GetStatusStacks(goddess, StatusCatalog.RisingTide));
            Assert.IsTrue(StatusRules.HasStatus(goddess, StatusCatalog.TideLocked));
            // 卡面 2 回合 → 写入 3，抵消当回合末 tick
            Assert.AreEqual(3, StatusRules.FindStatus(goddess, StatusCatalog.TideLocked)?.RemainingTurns ?? 0);
            Assert.IsFalse(StatusRules.HasStatus(goddess, StatusCatalog.EbbingTide));
        }

        [Test]
        public void WardenVictory_TriggersWhenWardenDiesWithMinionsAlive()
        {
            var config = WardenBossEncounterBuilder.BuildTemplate(null);
            config.Combatants.Add(new CombatantConfig
            {
                Id = "player",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_warrior",
                MaxHp = 80
            });

            var engine = new BattleEngine(config);
            var state = engine.State;

            CombatantState warden = null;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == WardenBossEncounterBuilder.CharacterId)
                    warden = unit;
            }

            Assert.NotNull(warden);
            warden.Hp = 0;

            state.Combatants.Add(new CombatantState
            {
                Id = "bat",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = "char_bat",
                MaxHp = 55,
                Hp = 55,
                DisplayName = "巨翼蝙蝠"
            });

            engine.EvaluateOutcomeForTests();

            Assert.AreEqual(BattleOutcome.PlayerVictory, state.Outcome);
        }

        [Test]
        public void OceanGoddess_RisingTideTriggersEbbAtSix()
        {
            var state = new BattleState { Config = new BattleConfig() };
            var goddess = new CombatantState
            {
                Id = "boss",
                Team = TeamSide.Enemy,
                DisplayName = "女神",
                MaxHp = 400,
                Hp = 400
            };
            goddess.Traits.Add(CharacterTraitCatalog.OceanGoddessTide);
            state.Combatants.Add(goddess);

            var events = new List<Events.BattleEvent>();
            V09BossMechanicsRules.AdjustRisingTideStacks(state, goddess, 6, events);

            Assert.IsFalse(StatusRules.HasStatus(goddess, StatusCatalog.RisingTide));
            Assert.IsTrue(StatusRules.HasStatus(goddess, StatusCatalog.EbbingTide));
        }

        [Test]
        public void IronGate_RespondSideEffect_DamagesRandomCage()
        {
            var state = new BattleState { Config = new BattleConfig() };
            var player = new CombatantState
            {
                Id = "p1",
                Team = TeamSide.Player,
                MaxHp = 100,
                Hp = 100,
                DisplayName = "骑士"
            };
            var warden = new CombatantState
            {
                Id = "warden",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = CharacterTraitCatalog.WardenCharacterId,
                MaxHp = 250,
                Hp = 250,
                DisplayName = "典狱长"
            };
            warden.Traits.Add(CharacterTraitCatalog.WardenCageMaster);
            var cage = new CombatantState
            {
                Id = "cage",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = CharacterTraitCatalog.PrisonCageCharacterId,
                MaxHp = 150,
                Hp = 150,
                DisplayName = "囚笼"
            };
            cage.Traits.Add(CharacterTraitCatalog.PrisonCage);
            state.Combatants.Add(player);
            state.Combatants.Add(warden);
            state.Combatants.Add(cage);

            Reactions.DefenderRespondArmRules.ArmMitigation(
                state,
                warden.Id,
                mitigationPercent: 70,
                sideEffectAllyDamage: 30,
                sideEffectAllyCharacterId: CharacterTraitCatalog.PrisonCageCharacterId);

            var events = new List<Events.BattleEvent>();
            var hpDamage = 40;
            CombatantState recipient = warden;
            var triggered = Reactions.DefenderRespondArmRules.TryConsumeForIncomingPlayerAttack(
                state, player, ref recipient, ref hpDamage, events, out _, new BattleRng(3));

            Assert.IsTrue(triggered);
            Assert.AreEqual(12, hpDamage); // 40 * 30%
            // 副作用延后到 ApplyConsumedSideEffects
            Assert.AreEqual(150, cage.Hp);

            Reactions.DefenderRespondArmRules.ApplyConsumedSideEffects(
                state, warden,
                new Reactions.DefenderRespondArm
                {
                    SideEffectAllyDamage = 30,
                    SideEffectAllyCharacterId = CharacterTraitCatalog.PrisonCageCharacterId
                },
                events, new BattleRng(3));

            Assert.AreEqual(120, cage.Hp); // 150 - 30
        }

        [Test]
        public void Warden_NoCage_GainsPermanentAttackUp()
        {
            var state = new BattleState { Config = new BattleConfig() };
            var warden = new CombatantState
            {
                Id = "warden",
                Team = TeamSide.Enemy,
                MaxHp = 250,
                Hp = 250,
                DisplayName = "典狱长"
            };
            warden.Traits.Add(CharacterTraitCatalog.WardenCageMaster);
            state.Combatants.Add(warden);

            var events = new List<Events.BattleEvent>();
            V09BossMechanicsRules.ProcessTurnStart(state, events, new BattleRng(1));

            Assert.AreEqual(
                V09BossMechanicsRules.WardenNoCageAttackBonusPercent,
                StatusRules.GetStatusStacks(warden, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void PrisonCage_Death_SpawnsReplacementAndClearsBrand()
        {
            var config = new BattleConfig();
            config.SummonTemplates["char_bat"] = new CombatantConfig
            {
                DisplayName = "巨翼蝙蝠",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_bat",
                MaxHp = 55,
                Speed = 9
            };
            var state = new BattleState { Config = config };
            var player = new CombatantState
            {
                Id = "p1",
                Team = TeamSide.Player,
                MaxHp = 100,
                Hp = 100
            };
            StatusRules.ApplyStatus(state, player, StatusCatalog.BrandMark, 2, -1, new List<Events.BattleEvent>());
            var cage = new CombatantState
            {
                Id = "cage",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = CharacterTraitCatalog.PrisonCageCharacterId,
                MaxHp = 10,
                Hp = 10,
                DisplayName = "囚笼"
            };
            cage.Traits.Add(CharacterTraitCatalog.PrisonCage);
            state.Combatants.Add(player);
            state.Combatants.Add(cage);

            // Force hash path to pick bat (depends on id hash) — register all three so any pick works
            config.SummonTemplates["char_skeleton_elite"] = new CombatantConfig
            {
                DisplayName = "骷髅精英",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_skeleton_elite",
                MaxHp = 45
            };
            config.SummonTemplates["char_wraith_elite"] = new CombatantConfig
            {
                DisplayName = "幽灵精英",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_wraith_elite",
                MaxHp = 35
            };

            cage.Hp = 0;
            var events = new List<Events.BattleEvent>();
            CombatantDeathRules.OnCharacterDied(state, cage, events, new BattleRng(0));

            Assert.IsFalse(StatusRules.HasStatus(player, StatusCatalog.BrandMark));
            var spawned = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.IsAlive
                    && unit.Id != cage.Id
                    && unit.Team == TeamSide.Enemy
                    && unit.Slot == FormationSlot.Middle)
                    spawned++;
            }

            Assert.AreEqual(1, spawned);
        }
    }
}
