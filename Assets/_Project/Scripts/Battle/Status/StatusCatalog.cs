using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Status
{
    public static class StatusCatalog
    {
        public const string Poison = "poison";
        public const string Slow = "slow";
        public const string Burn = "burn";
        public const string AttackUp = "attack_up";
        public const string AttackDown = "attack_down";
        public const string DefenseUp = "defense_up";
        public const string Taunt = "taunt";
        public const string Guard = "guard";
        public const string VampAura = "vamp_aura";
        public const string ReviveBlessing = "revive_blessing";
        public const string Unyielding = "unyielding";
        public const string NecroticPoison = "necrotic_poison";
        public const string BoneWorkshop = "bone_workshop";
        public const string AnubisAvatar = "anubis_avatar";
        public const string Ethereal = "ethereal";
        public const string GhostQueenWrath = "ghost_queen_wrath";
        public const string FinalBloodRitual = "final_blood_ritual";
        public const string SandSpearReforge = "sand_spear_reforge";
        public const string RatSwarmCall = "rat_swarm_call";
        public const string GodDescends = "god_descends";
        public const string AttackUpPercent = "attack_up_pct";
        public const string DefenseUpPercent = "defense_up_pct";
        public const string DefenseDownPercent = "defense_down_pct";
        public const string DamageUp = "damage_up";
        public const string Weaken = "weaken";
        public const string ArmorUp = "armor_up";
        public const string ArmorDown = "armor_down";
        public const string Vulnerable = "vulnerable";
        public const string DamageReduction = "damage_reduction";
        public const string FinalSummonPending = "final_summon_pending";

        // v0.9 玩家卡牌被动状态（均为 Permanent 标记“本场战斗已激活”，由 PassiveCardMechanicsRules 钩子消费）
        public const string RespondStance = "respond_stance";       // 应对姿态：应对触发得8护甲
        public const string BattleWill = "battle_will";             // 战意觉醒：掉血得5%增伤
        public const string HeavyArmor = "heavy_armor";             // 重甲强化：获得护甲+20%
        public const string FinalBulwark = "final_bulwark";         // 最终壁垒：回合末仅清50%护甲
        public const string LastStand = "last_stand";               // 背水一战：2回合 HP不降至1以下
        public const string PlagueSpread = "plague_spread";         // 瘟疫蔓延：中毒tick 30%传染相邻
        public const string HolyInfusionPending = "holy_infusion_pending"; // 神圣灌注：重复下一张牌
        public const string RotAvatar = "rot_avatar";               // 腐朽化身：敌人回合开始2层中毒
        public const string BloodFrenzy = "blood_frenzy";           // 鲜血狂欢：献祭后5%增伤
        public const string BloodlineLegacy = "bloodline_legacy";   // 血族传承：150%最大HP
        public const string BloodSharing = "blood_sharing";         // 分血仪式：回复时治疗其他我方30%

        // v0.9 毒蛇女王 / 巫妖女王 新增状态
        public const string Constrict = "constrict";                       // 缠绕：每回合开始受 Stacks 伤害
        public const string VenomSacBurst = "venom_sac_burst";             // 毒囊破裂：施毒+1层
        public const string ImmortalShed = "immortal_shed";                // 不朽蛇蜕：获得中毒时+10%增伤5回合
        public const string PrayAncientSnakeGod = "pray_ancient_snake_god"; // 祈求远古蛇神：每回合注入蛇神的回应
        public const string DelayedDamage = "delayed_damage";              // 延迟伤害：下回合开始受 Stacks 伤害
        public const string EtherealOnNextHit = "ethereal_on_next_hit";    // 两界行者：下次受击后获虚化
        public const string PsionicBody = "psionic_body";                  // 灵能体：非战斗回合+20%增伤（占位）
        public const string SealedNextCard = "sealed_next_card";           // 灵界封印：敌方下张牌失效（占位）
        public const string DespairSoulRecall = "despair_soul_recall";     // 绝望之魂：获虚化时从弃牌堆加入手牌
        public const string HandCostZero = "hand_cost_zero";               // 灵界降临：本回合手牌0费
        public const string EternalVoid = "eternal_void";                  // 永恒虚无：永久虚化，每回合受25%最大HP真伤
        public const string SnakeGodChanneling = "snake_god_channeling";   // 蛇神回应链中继标记
        public const string SnakeSwiftness = "snake_swiftness";             // 蛇之疾速：+1SPD（天赋）

        static readonly Dictionary<string, StatusDefinition> Definitions = Build();

        public static StatusDefinition Get(string id)
        {
            Definitions.TryGetValue(id, out var def);
            return def;
        }

        static Dictionary<string, StatusDefinition> Build()
        {
            var map = new Dictionary<string, StatusDefinition>();
            map[Poison] = new StatusDefinition
            {
                Id = Poison,
                DisplayName = "中毒",
                DurationKind = StatusDurationKind.Permanent,
                TurnStartDamagePerStack = 1,
                TickIgnoresBlock = true
            };
            map[Slow] = new StatusDefinition
            {
                Id = Slow,
                DisplayName = "减速",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                SpeedModifierPerStack = -1
            };
            map[Burn] = new StatusDefinition
            {
                Id = Burn,
                DisplayName = "灼烧",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                TurnStartDamagePerStack = 2,
                TickIgnoresDefense = true
            };
            map[AttackUp] = new StatusDefinition
            {
                Id = AttackUp,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                OutgoingDamageFlatPerStack = 1
            };
            map[AttackDown] = new StatusDefinition
            {
                Id = AttackDown,
                DisplayName = "虚弱",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                OutgoingDamageReductionFlatPerStack = 1
            };
            map[DefenseUp] = new StatusDefinition
            {
                Id = DefenseUp,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                BlockGainFlatPerStack = 1
            };
            map[Taunt] = new StatusDefinition
            {
                Id = Taunt,
                DisplayName = "嘲讽",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[Guard] = new StatusDefinition
            {
                Id = Guard,
                DisplayName = "誓死守护",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[VampAura] = new StatusDefinition
            {
                Id = VampAura,
                DisplayName = "吸血光环",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[ReviveBlessing] = new StatusDefinition
            {
                Id = ReviveBlessing,
                DisplayName = "复活祝福",
                DurationKind = StatusDurationKind.Permanent
            };
            map[Unyielding] = new StatusDefinition
            {
                Id = Unyielding,
                DisplayName = "不屈意志",
                DurationKind = StatusDurationKind.Permanent
            };
            map[NecroticPoison] = new StatusDefinition
            {
                Id = NecroticPoison,
                DisplayName = "亡灵毒",
                DurationKind = StatusDurationKind.Permanent,
                TurnStartDamagePerStack = 1,
                TickIgnoresBlock = true
            };
            map[BoneWorkshop] = new StatusDefinition
            {
                Id = BoneWorkshop,
                DisplayName = "骨之王座",
                DurationKind = StatusDurationKind.Permanent
            };
            map[AnubisAvatar] = new StatusDefinition
            {
                Id = AnubisAvatar,
                DisplayName = "阿努比斯化身",
                DurationKind = StatusDurationKind.Permanent,
                MaxHpPercentBonusPerStack = AnubisAvatarRules.StatPercentBonus,
                AttackPercentBonusPerStack = AnubisAvatarRules.StatPercentBonus,
                DefensePercentBonusPerStack = AnubisAvatarRules.StatPercentBonus
            };
            map[Ethereal] = new StatusDefinition
            {
                Id = Ethereal,
                DisplayName = "虚化",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[GhostQueenWrath] = new StatusDefinition
            {
                Id = GhostQueenWrath,
                DisplayName = "幽灵女王之怒",
                DurationKind = StatusDurationKind.Permanent,
                AttackPercentBonusPerStack = 100
            };
            map[FinalBloodRitual] = new StatusDefinition
            {
                Id = FinalBloodRitual,
                DisplayName = "最终鲜血仪式",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SandSpearReforge] = new StatusDefinition
            {
                Id = SandSpearReforge,
                DisplayName = "沙矛重塑",
                DurationKind = StatusDurationKind.Permanent
            };
            map[RatSwarmCall] = new StatusDefinition
            {
                Id = RatSwarmCall,
                DisplayName = "鼠群呼唤",
                DurationKind = StatusDurationKind.Permanent
            };
            map[GodDescends] = new StatusDefinition
            {
                Id = GodDescends,
                DisplayName = "天神下凡",
                DurationKind = StatusDurationKind.Permanent
            };
            map[AttackUpPercent] = new StatusDefinition
            {
                Id = AttackUpPercent,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 3,
                AttackPercentBonusPerStack = 1
            };
            map[DefenseUpPercent] = new StatusDefinition
            {
                Id = DefenseUpPercent,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                DefensePercentBonusPerStack = 1
            };
            map[DefenseDownPercent] = new StatusDefinition
            {
                Id = DefenseDownPercent,
                DisplayName = "破损",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                BlockGainReductionPercentPerStack = 1
            };
            map[DamageUp] = new StatusDefinition
            {
                Id = DamageUp,
                DisplayName = "增伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                OutgoingDamageFlatPerStack = 1
            };
            map[Weaken] = new StatusDefinition
            {
                Id = Weaken,
                DisplayName = "虚弱",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                OutgoingDamageReductionFlatPerStack = 1
            };
            map[ArmorUp] = new StatusDefinition
            {
                Id = ArmorUp,
                DisplayName = "强固",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                BlockGainFlatPerStack = 1
            };
            map[ArmorDown] = new StatusDefinition
            {
                Id = ArmorDown,
                DisplayName = "破损",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                BlockGainReductionPercentPerStack = 1
            };
            map[Vulnerable] = new StatusDefinition
            {
                Id = Vulnerable,
                DisplayName = "易伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                IncomingDamagePercentPerStack = 1
            };
            map[DamageReduction] = new StatusDefinition
            {
                Id = DamageReduction,
                DisplayName = "减伤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1,
                IncomingDamageReductionPercentPerStack = 1
            };
            map[FinalSummonPending] = new StatusDefinition
            {
                Id = FinalSummonPending,
                DisplayName = "终焉召唤",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[RespondStance] = new StatusDefinition
            {
                Id = RespondStance,
                DisplayName = "应对姿态",
                DurationKind = StatusDurationKind.Permanent
            };
            map[BattleWill] = new StatusDefinition
            {
                Id = BattleWill,
                DisplayName = "战意觉醒",
                DurationKind = StatusDurationKind.Permanent
            };
            map[HeavyArmor] = new StatusDefinition
            {
                Id = HeavyArmor,
                DisplayName = "重甲强化",
                DurationKind = StatusDurationKind.Permanent
            };
            map[FinalBulwark] = new StatusDefinition
            {
                Id = FinalBulwark,
                DisplayName = "最终壁垒",
                DurationKind = StatusDurationKind.Permanent
            };
            map[LastStand] = new StatusDefinition
            {
                Id = LastStand,
                DisplayName = "背水一战",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[PlagueSpread] = new StatusDefinition
            {
                Id = PlagueSpread,
                DisplayName = "瘟疫蔓延",
                DurationKind = StatusDurationKind.Permanent
            };
            map[HolyInfusionPending] = new StatusDefinition
            {
                Id = HolyInfusionPending,
                DisplayName = "神圣灌注",
                DurationKind = StatusDurationKind.Permanent
            };
            map[RotAvatar] = new StatusDefinition
            {
                Id = RotAvatar,
                DisplayName = "腐朽化身",
                DurationKind = StatusDurationKind.Permanent
            };
            map[BloodFrenzy] = new StatusDefinition
            {
                Id = BloodFrenzy,
                DisplayName = "鲜血狂欢",
                DurationKind = StatusDurationKind.Permanent
            };
            map[BloodlineLegacy] = new StatusDefinition
            {
                Id = BloodlineLegacy,
                DisplayName = "血族传承",
                DurationKind = StatusDurationKind.Permanent,
                MaxHpPercentBonusPerStack = 50
            };
            map[BloodSharing] = new StatusDefinition
            {
                Id = BloodSharing,
                DisplayName = "分血仪式",
                DurationKind = StatusDurationKind.Permanent
            };
            // v0.9 毒蛇/巫妖 新状态定义
            map[Constrict] = new StatusDefinition
            {
                Id = Constrict,
                DisplayName = "缠绕",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[VenomSacBurst] = new StatusDefinition
            {
                Id = VenomSacBurst,
                DisplayName = "毒囊破裂",
                DurationKind = StatusDurationKind.Permanent
            };
            map[ImmortalShed] = new StatusDefinition
            {
                Id = ImmortalShed,
                DisplayName = "不朽蛇蜕",
                DurationKind = StatusDurationKind.Permanent
            };
            map[PrayAncientSnakeGod] = new StatusDefinition
            {
                Id = PrayAncientSnakeGod,
                DisplayName = "祈求远古蛇神",
                DurationKind = StatusDurationKind.Permanent
            };
            map[DelayedDamage] = new StatusDefinition
            {
                Id = DelayedDamage,
                DisplayName = "延迟伤害",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[EtherealOnNextHit] = new StatusDefinition
            {
                Id = EtherealOnNextHit,
                DisplayName = "两界行者",
                DurationKind = StatusDurationKind.Permanent
            };
            map[PsionicBody] = new StatusDefinition
            {
                Id = PsionicBody,
                DisplayName = "灵能体",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SealedNextCard] = new StatusDefinition
            {
                Id = SealedNextCard,
                DisplayName = "灵界封印",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[DespairSoulRecall] = new StatusDefinition
            {
                Id = DespairSoulRecall,
                DisplayName = "绝望之魂",
                DurationKind = StatusDurationKind.Permanent
            };
            map[HandCostZero] = new StatusDefinition
            {
                Id = HandCostZero,
                DisplayName = "灵界降临",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[EternalVoid] = new StatusDefinition
            {
                Id = EternalVoid,
                DisplayName = "永恒虚无",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SnakeGodChanneling] = new StatusDefinition
            {
                Id = SnakeGodChanneling,
                DisplayName = "蛇神降临",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SnakeSwiftness] = new StatusDefinition
            {
                Id = SnakeSwiftness,
                DisplayName = "蛇之疾速",
                DurationKind = StatusDurationKind.Permanent,
                SpeedModifierPerStack = 1
            };
            return map;
        }
    }
}
