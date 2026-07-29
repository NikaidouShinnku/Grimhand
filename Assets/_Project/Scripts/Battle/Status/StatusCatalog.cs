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
        public const string Invulnerable = "invulnerable";
        public const string Deterrence = "deterrence";
        /// <summary>摄魂：下回合玩家能量回复减少（展示用，实际扣减由 PendingPlayerEnergyRegenPenaltyNextTurn）。</summary>
        public const string SoulDrain = "soul_drain";
        public const string DamageUp = "damage_up";
        public const string Weaken = "weaken";
        public const string ArmorUp = "armor_up";
        public const string ArmorDown = "armor_down";
        public const string Vulnerable = "vulnerable";
        /// <summary>蜘蛛贵妇：按玩家中毒层数同步的易伤展示/结算（每层 +1% 受伤）。</summary>
        public const string SpiderPoisonVulnerable = "spider_poison_vulnerable";
        /// <summary>鬼灵海盗船长被动：条件满足时 +33% 增伤（不叠多次）。</summary>
        public const string PhantomCaptainFrenzyAtk = "phantom_captain_frenzy_atk";
        /// <summary>鬼灵海盗船长被动：条件满足时 +20% 易伤（不叠多次）。</summary>
        public const string PhantomCaptainFrenzyVuln = "phantom_captain_frenzy_vuln";
        /// <summary>潮汐之力：劈砍/破浪斩费用 -1（持续回合）。</summary>
        public const string MermaidTidalCostCut = "mermaid_tidal_cost_cut";
        public const string DamageReduction = "damage_reduction";
        public const string FinalSummonPending = "final_summon_pending";

        // v0.9 玩家卡牌被动状态（均为 Permanent 标记“本场战斗已激活”，由 PassiveCardMechanicsRules 钩子消费）
        public const string RespondStance = "respond_stance";       // 应对姿态：应对触发得8护甲
        public const string BattleWill = "battle_will";             // 战意觉醒：掉血得5%增伤
        public const string HeavyArmor = "heavy_armor";             // 重甲强化：获得护甲+20%
        public const string FinalBulwark = "final_bulwark";         // 最终壁垒：回合末仅清50%护甲
        public const string LastStand = "last_stand";               // 背水一战：2回合 HP不降至1以下
        public const string PlagueSpread = "plague_spread";         // 瘟疫蔓延：中毒tick 30%传染相邻
        public const string HolyInfusionPending = "holy_infusion_pending"; // 旧版：下一张重复（v0.9 已改为结算时重复上一张）
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
        public const string SealNextStatusCard = "seal_next_status";       // 禁用目标下一张状态牌
        public const string DespairSoulRecall = "despair_soul_recall";     // 绝望之魂：获虚化时从弃牌堆加入手牌
        public const string HandCostZero = "hand_cost_zero";               // 灵界降临：本回合手牌0费
        public const string EternalVoid = "eternal_void";                  // 永恒虚无：永久虚化，每回合受25%最大HP真伤
        public const string SnakeGodChanneling = "snake_god_channeling";   // 蛇神回应链中继标记
        public const string SnakeSwiftness = "snake_swiftness";             // 蛇之疾速：+1SPD（天赋）
        public const string SpeedUp = "spd_up";                             // 加速：每层 +1SPD
        public const string DebuffImmune = "debuff_immune";                 // 减益免疫（永久）

        // v0.91 新增卡牌状态
        public const string ThornArmor = "thorn_armor";
        public const string BattleRoar = "battle_roar";
        public const string DoomProphecy = "doom_prophecy";
        public const string LifeSpring = "life_spring";
        public const string PainConvert = "pain_convert";
        public const string SnakeNest = "snake_nest";
        public const string PsionicArrowRain = "psionic_arrow_rain";
        public const string PsionicMastery = "psionic_mastery";
        public const string SoulBond = "soul_bond";

        // v0.92 新增卡牌状态
        /// <summary>借机攻击架势：本场战斗中敌人换位时对其造成 Stacks 伤害。</summary>
        public const string OpportunisticStance = "opportunistic_stance";
        /// <summary>鲜血傀儡庇护：下次受到攻击后与施法者换位，并给施法者减伤。</summary>
        public const string BloodPuppetShelter = "blood_puppet_shelter";

        // v0.9 Boss：典狱长 / 腐化海洋女神
        public const string BrandMark = "brand_mark";
        /// <summary>脚标展示用：闪避率（非真实 StatusInstance，由 FootStatusIconAggregator 合成）。</summary>
        public const string DodgeChance = "dodge_chance";
        public const string RisingTide = "rising_tide";
        /// <summary>踏潮守卫被动「浪潮」：同位置速度优势带来的攻击增伤（层数=增伤百分比）。</summary>
        public const string WaveSurge = "wave_surge";
        public const string EbbingTide = "ebbing_tide";
        public const string TideEmpower = "tide_empower";
        public const string TideLocked = "tide_locked";

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
                // 层数=每回合跳伤强度；持续时间由卡面 Duration 决定（-1=永久，N=N回合）
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
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
                // 每层 -1% 造成的伤害
                OutgoingDamagePercentPerStack = -1
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
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
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
            map[Invulnerable] = new StatusDefinition
            {
                Id = Invulnerable,
                DisplayName = "隐身",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[Deterrence] = new StatusDefinition
            {
                Id = Deterrence,
                DisplayName = "威慑",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[SoulDrain] = new StatusDefinition
            {
                Id = SoulDrain,
                DisplayName = "摄魂",
                DurationKind = StatusDurationKind.Permanent
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
                // 每层 -1% 造成的伤害（20 层 = 20% 虚弱）
                OutgoingDamagePercentPerStack = -1
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
            map[SpiderPoisonVulnerable] = new StatusDefinition
            {
                Id = SpiderPoisonVulnerable,
                DisplayName = "易伤",
                DurationKind = StatusDurationKind.Permanent,
                IncomingDamagePercentPerStack = 1
            };
            map[PhantomCaptainFrenzyAtk] = new StatusDefinition
            {
                Id = PhantomCaptainFrenzyAtk,
                DisplayName = "狂怒增伤",
                DurationKind = StatusDurationKind.Permanent,
                AttackPercentBonusPerStack = 1
            };
            map[PhantomCaptainFrenzyVuln] = new StatusDefinition
            {
                Id = PhantomCaptainFrenzyVuln,
                DisplayName = "狂怒易伤",
                DurationKind = StatusDurationKind.Permanent,
                IncomingDamagePercentPerStack = 1
            };
            map[MermaidTidalCostCut] = new StatusDefinition
            {
                Id = MermaidTidalCostCut,
                DisplayName = "潮汐减耗",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
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
                DefaultDuration = 3
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
            map[SealNextStatusCard] = new StatusDefinition
            {
                Id = SealNextStatusCard,
                DisplayName = "状态封印",
                DurationKind = StatusDurationKind.Permanent
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
            map[SpeedUp] = new StatusDefinition
            {
                Id = SpeedUp,
                DisplayName = "加速",
                DurationKind = StatusDurationKind.Permanent,
                SpeedModifierPerStack = 1
            };
            map[DebuffImmune] = new StatusDefinition
            {
                Id = DebuffImmune,
                DisplayName = "减益免疫",
                DurationKind = StatusDurationKind.Permanent
            };
            map[BrandMark] = new StatusDefinition
            {
                Id = BrandMark,
                DisplayName = "烙印",
                DurationKind = StatusDurationKind.Permanent,
                // 文案见 BattleUiFormatters / KeywordCatalog：累计三层即死
            };
            map[RisingTide] = new StatusDefinition
            {
                Id = RisingTide,
                DisplayName = "涨潮",
                DurationKind = StatusDurationKind.Permanent,
                IncomingDamageReductionPercentPerStack = 15,
                AttackPercentBonusPerStack = 10
            };
            map[WaveSurge] = new StatusDefinition
            {
                Id = WaveSurge,
                DisplayName = "浪潮",
                DurationKind = StatusDurationKind.Permanent,
                AttackPercentBonusPerStack = 1
            };
            map[EbbingTide] = new StatusDefinition
            {
                Id = EbbingTide,
                DisplayName = "退潮",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2,
                IncomingDamagePercentPerStack = 50
            };
            map[TideEmpower] = new StatusDefinition
            {
                Id = TideEmpower,
                DisplayName = "魔化潮汐",
                DurationKind = StatusDurationKind.Permanent
            };
            map[TideLocked] = new StatusDefinition
            {
                Id = TideLocked,
                DisplayName = "女神之怒",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 2
            };
            map[ThornArmor] = new StatusDefinition
            {
                Id = ThornArmor,
                DisplayName = "荆棘护甲",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[BattleRoar] = new StatusDefinition
            {
                Id = BattleRoar,
                DisplayName = "战斗咆哮",
                DurationKind = StatusDurationKind.Permanent
            };
            map[DoomProphecy] = new StatusDefinition
            {
                Id = DoomProphecy,
                DisplayName = "末日预言",
                DurationKind = StatusDurationKind.Permanent
            };
            map[LifeSpring] = new StatusDefinition
            {
                Id = LifeSpring,
                DisplayName = "生命之泉",
                DurationKind = StatusDurationKind.Permanent
            };
            map[PainConvert] = new StatusDefinition
            {
                Id = PainConvert,
                DisplayName = "苦痛转化",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[SnakeNest] = new StatusDefinition
            {
                Id = SnakeNest,
                DisplayName = "千蛇窟",
                DurationKind = StatusDurationKind.Permanent
            };
            map[PsionicArrowRain] = new StatusDefinition
            {
                Id = PsionicArrowRain,
                DisplayName = "灵能箭雨",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 3
            };
            map[PsionicMastery] = new StatusDefinition
            {
                Id = PsionicMastery,
                DisplayName = "灵能掌握",
                DurationKind = StatusDurationKind.Permanent
            };
            map[SoulBond] = new StatusDefinition
            {
                Id = SoulBond,
                DisplayName = "灵魂纽带",
                DurationKind = StatusDurationKind.Turns,
                DefaultDuration = 1
            };
            map[OpportunisticStance] = new StatusDefinition
            {
                Id = OpportunisticStance,
                DisplayName = "借机攻击架势",
                DurationKind = StatusDurationKind.Permanent
            };
            map[BloodPuppetShelter] = new StatusDefinition
            {
                Id = BloodPuppetShelter,
                DisplayName = "鲜血傀儡庇护",
                DurationKind = StatusDurationKind.Permanent
            };
            return map;
        }
    }
}
