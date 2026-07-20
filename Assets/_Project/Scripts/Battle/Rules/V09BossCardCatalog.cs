using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// v0.9 三 Boss 卡权威定义（在 Battle 程序集内，避免引用 Expedition）。
    /// SO / ToTemplate / EncounterBuilder 一律以此处为准。
    /// </summary>
    public static class V09BossCardCatalog
    {
        static readonly Dictionary<string, CardTemplate> Canonical = BuildMap();

        public static bool TryApplyCanonical(CardTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                return false;

            if (!Canonical.TryGetValue(template.DefinitionId, out var source) || source == null)
                return false;

            template.DisplayName = source.DisplayName;
            template.OwnerCharacterId = source.OwnerCharacterId;
            template.Cost = source.Cost;
            template.CardType = source.CardType;
            template.Keywords.Clear();
            template.Keywords.AddRange(source.Keywords);
            template.Actions.Clear();
            foreach (var action in source.Actions)
                template.Actions.Add(EffectActionSpec.Clone(action));
            return true;
        }

        public static IReadOnlyList<CardTemplate> AllCanonicalCards()
        {
            var list = new List<CardTemplate>(Canonical.Count);
            foreach (var pair in Canonical)
                list.Add(CloneTemplate(pair.Value));
            return list;
        }

        public static CardTemplate PunishmentCombo() => CloneTemplate(Canonical["m_warden_punishment_combo"]);
        public static CardTemplate BrandMark() => CloneTemplate(Canonical["m_warden_brand"]);
        public static CardTemplate IronGate() => CloneTemplate(Canonical["m_warden_iron_gate"]);
        public static CardTemplate OpenCage() => CloneTemplate(Canonical["m_warden_open_cage"]);
        public static CardTemplate OppressionAura() => CloneTemplate(Canonical["m_warden_oppression"]);
        public static CardTemplate IronSanction() => CloneTemplate(Canonical["m_warden_iron_sanction"]);
        public static CardTemplate LockDown() => CloneTemplate(Canonical["m_warden_lock"]);
        public static CardTemplate Judgment() => CloneTemplate(Canonical["m_warden_judgment"]);

        public static CardTemplate WitherStrike() => CloneTemplate(Canonical["m_dark_knight_wither"]);
        public static CardTemplate SoulDrain() => CloneTemplate(Canonical["m_dark_knight_soul_drain"]);
        public static CardTemplate DarkShield() => CloneTemplate(Canonical["m_dark_knight_shield"]);
        public static CardTemplate PlagueTide() => CloneTemplate(Canonical["m_dark_knight_plague"]);
        public static CardTemplate CommandDead() => CloneTemplate(Canonical["m_dark_knight_command_dead"]);
        public static CardTemplate Snowball() => CloneTemplate(Canonical["m_dark_knight_snowball"]);

        public static CardTemplate CorruptedNet() => CloneTemplate(Canonical["m_ocean_corrupted_net"]);
        public static CardTemplate OceanShield() => CloneTemplate(Canonical["m_ocean_shield"]);
        public static CardTemplate TidePower() => CloneTemplate(Canonical["m_ocean_tide_power"]);
        public static CardTemplate VortexPull() => CloneTemplate(Canonical["m_ocean_vortex"]);
        public static CardTemplate AbyssDevour() => CloneTemplate(Canonical["m_ocean_abyss_devour"]);
        public static CardTemplate GoddessWrath() => CloneTemplate(Canonical["m_ocean_goddess_wrath"]);
        public static CardTemplate TideControl() => CloneTemplate(Canonical["m_ocean_tide_control"]);
        public static CardTemplate DemonTide() => CloneTemplate(Canonical["m_ocean_demon_tide"]);

        static Dictionary<string, CardTemplate> BuildMap()
        {
            var map = new Dictionary<string, CardTemplate>();
            void Add(CardTemplate card) => map[card.DefinitionId] = card;

            Add(BuildPunishmentCombo());
            Add(BuildBrandMark());
            Add(BuildIronGate());
            Add(BuildOpenCage());
            Add(BuildOppressionAura());
            Add(BuildIronSanction());
            Add(BuildLockDown());
            Add(BuildJudgment());

            Add(BuildWitherStrike());
            Add(BuildSoulDrain());
            Add(BuildDarkShield());
            Add(BuildPlagueTide());
            Add(BuildCommandDead());
            Add(BuildSnowball());

            Add(BuildCorruptedNet());
            Add(BuildOceanShield());
            Add(BuildTidePower());
            Add(BuildVortexPull());
            Add(BuildAbyssDevour());
            Add(BuildGoddessWrath());
            Add(BuildTideControl());
            Add(BuildDemonTide());
            return map;
        }

        static CardTemplate BuildPunishmentCombo()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_punishment_combo", "刑法连击", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 30,
                Reach = TargetReach.FrontAndMiddle,
                SplashBehindTarget = true,
                SplashPowerPercent = 50
            });
            return card;
        }

        static CardTemplate BuildBrandMark()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_brand", "刻上烙印", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemy,
                StatusId = StatusCatalog.BrandMark,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate BuildIronGate()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_iron_gate", "铁壁牢门", 1, CardType.Defense, "parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 70,
                Condition = ReactionConditionType.LastActionAttackOnSelf,
                RespondSideEffectAllyDamage = 30,
                RespondSideEffectAllyCharacterId = CharacterTraitCatalog.PrisonCageCharacterId
            });
            return card;
        }

        static CardTemplate BuildOpenCage()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_open_cage", "打开囚笼", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamageRandomCharacterAlly,
                Target = EffectTarget.RandomAllyByCharacterId,
                SummonCharacterId = CharacterTraitCatalog.PrisonCageCharacterId,
                Value = 150
            });
            return card;
        }

        static CardTemplate BuildOppressionAura()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_oppression", "压迫气场", 2, CardType.Status, "aoe", "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Slow,
                Stacks = 2,
                Duration = 2,
                Reach = TargetReach.Any
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 20,
                Duration = 2,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildIronSanction()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_iron_sanction", "铁腕制裁", 3, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 30,
                Reach = TargetReach.FrontAndMiddle
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Vulnerable,
                Stacks = 100,
                Duration = 2
            });
            return card;
        }

        static CardTemplate BuildLockDown()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_lock", "上锁", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.DefenseDownPercent,
                Stacks = 100,
                Duration = 2,
                Reach = TargetReach.FrontAndMiddle
            });
            return card;
        }

        static CardTemplate BuildJudgment()
        {
            var card = Base(CharacterTraitCatalog.WardenCharacterId, "m_warden_judgment", "审判裁决", 3, CardType.Status, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.BrandMark,
                Stacks = 1,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildWitherStrike()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_wither", "凋零刺击", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 25,
                Reach = TargetReach.FrontAndMiddle,
                BonusIfTargetHasStatusId = StatusCatalog.Poison,
                BonusIfTargetHasStatusFlat = 15
            });
            return card;
        }

        static CardTemplate BuildSoulDrain()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_soul_drain", "灵魂吸取", 1, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 15,
                Reach = TargetReach.FrontAndMiddle,
                LifestealPercent = 100
            });
            return card;
        }

        static CardTemplate BuildDarkShield()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_shield", "黑暗护盾", 1, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 20
            });
            return card;
        }

        static CardTemplate BuildPlagueTide()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_plague", "瘟疫之潮", 2, CardType.Status, "aoe", "poison");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildCommandDead()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_command_dead", "号令亡者", 2, CardType.Status, "exhaust", "summon");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SummonOrGainBlock,
                Target = EffectTarget.Self,
                SummonCharacterId = "char_spider_lady",
                FallbackBlockValue = 15
            });
            return card;
        }

        static CardTemplate BuildSnowball()
        {
            var card = Base(CharacterTraitCatalog.DarkKnightCharacterId, "m_dark_knight_snowball", "雪上加霜", 2, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 10,
                Reach = TargetReach.Any,
                BonusIfTargetHasStatusId = StatusCatalog.Poison,
                BonusIfTargetHasStatusFlat = 10
            });
            return card;
        }

        static CardTemplate BuildCorruptedNet()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_corrupted_net", "腐化电网", 1, CardType.Attack, "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 20,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildOceanShield()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_shield", "海洋神盾", 1, CardType.Defense);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 30
            });
            return card;
        }

        static CardTemplate BuildTidePower()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_tide_power", "潮汐神力", 2, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyAttackUpPerSelfStatusStack,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 20,
                Duration = 2,
                RepeatPerStatusId = StatusCatalog.RisingTide
            });
            return card;
        }

        static CardTemplate BuildVortexPull()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_vortex", "漩涡吸引", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SwapRandomEnemies,
                Target = EffectTarget.AllEnemies,
                Value = 1
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemies,
                Value = 2,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1
            });
            return card;
        }

        static CardTemplate BuildAbyssDevour()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_abyss_devour", "深渊吞噬", 2, CardType.Attack);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.StripBlockThenDealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 12,
                Stacks = 5,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildGoddessWrath()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_goddess_wrath", "女神之怒", 3, CardType.Status, "orange");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockRisingTideStacks,
                Target = EffectTarget.Self,
                Duration = 2
            });
            return card;
        }

        static CardTemplate BuildTideControl()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_tide_control", "潮汐掌握", 1, CardType.Status);
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.AdjustSelfStatusRandom,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.RisingTide
            });
            return card;
        }

        static CardTemplate BuildDemonTide()
        {
            var card = Base(CharacterTraitCatalog.OceanGoddessCharacterId, "m_ocean_demon_tide", "魔化潮汐", 2, CardType.Status, "exhaust");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.TideEmpower,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static CardTemplate Base(string owner, string id, string name, int cost, CardType type, params string[] keywords)
        {
            var card = new CardTemplate
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = owner,
                Cost = cost,
                CardType = type
            };
            foreach (var keyword in keywords)
                card.Keywords.Add(keyword);
            return card;
        }

        static CardTemplate CloneTemplate(CardTemplate source)
        {
            var copy = new CardTemplate
            {
                DefinitionId = source.DefinitionId,
                DisplayName = source.DisplayName,
                OwnerCharacterId = source.OwnerCharacterId,
                Cost = source.Cost,
                CardType = source.CardType
            };
            copy.Keywords.AddRange(source.Keywords);
            foreach (var action in source.Actions)
                copy.Actions.Add(EffectActionSpec.Clone(action));
            return copy;
        }
    }
}
