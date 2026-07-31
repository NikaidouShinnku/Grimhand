using System.Collections.Generic;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    /// <summary>小怪/Boss 特性展示名与描述（对照小怪设计表与 CharacterTraitCatalog 注释）。</summary>
    public static class CharacterTraitDisplayCatalog
    {
        public readonly struct Entry
        {
            public readonly string Title;
            public readonly string Description;

            public Entry(string title, string description)
            {
                Title = title ?? "";
                Description = description ?? "";
            }
        }

        static readonly Dictionary<string, Entry> ById = Build();

        public static bool TryGet(string traitId, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(traitId))
                return false;

            return ById.TryGetValue(traitId, out entry);
        }

        static Dictionary<string, Entry> Build()
        {
            var map = new Dictionary<string, Entry>
            {
                [MinionTraitCatalog.SlimeRegen] = new(
                    "黏液再生",
                    "若回合内没受到伤害，在回合开始时回复2HP。"),
                [MinionTraitCatalog.SkeletonCardDef] = new(
                    "骨盾积蓄",
                    "每打出3张牌，获得+5护甲。"),
                [MinionTraitCatalog.SkeletonEliteCardStats] = new(
                    "精英骨盾",
                    "每打出3张牌，获得+8护甲和10%增伤（永久）。"),
                [MinionTraitCatalog.WraithLowHpSpeed] = new(
                    "低血迅捷",
                    "血量低于50%时，获得+2速度。"),
                [MinionTraitCatalog.WraithEliteLowHpEthereal] = new(
                    "虚化迅捷",
                    "血量低于50%时，获得1回合虚化，并获得+2速度（每场一次）。"),
                [MinionTraitCatalog.OgreBloodRage] = new(
                    "血怒",
                    "每受到1次伤害获得1层血怒（最高5层），每层使下张攻击牌增加15%伤害，并消耗所有血怒。"),
                [MinionTraitCatalog.BatFirstHitDodge] = new(
                    "首击闪避",
                    "每回合第一次受到攻击时，有50%概率完全闪避（失败也会消耗）。"),
                [MinionTraitCatalog.RatPackAttackOnAllyDeath] = new(
                    "鼠群狂怒",
                    "本场战斗中每有一只鼠人死亡，获得20%增伤（永久）。"),
                [MinionTraitCatalog.ChainWraithDebuffShare] = new(
                    "怨链共享",
                    "自身拥有的负面状态会同样作用于所有敌人。"),
                [MinionTraitCatalog.GargoyleFirstCardStance] = new(
                    "石像姿态",
                    "每回合根据第一张牌类型获得增益：攻击→25%增伤（1回合）；防御/状态→25%强固（1回合）。"),
                [MinionTraitCatalog.SpiderLadyPoisonVulnerability] = new(
                    "剧毒侵蚀",
                    "敌人每有5层中毒，额外视为拥有10%易伤。"),
                [MinionTraitCatalog.StoneGolemArmorRetain] = new(
                    "岩甲残留",
                    "若回合结束时仍有护甲，保留一半到下回合。"),
                [MinionTraitCatalog.SeahorseGuardSpeedAttack] = new(
                    "潮速压制",
                    "比同位置敌人每多1点速度，获得10%增伤（最多50%）；同位置无敌人则获得50%。"),
                [MinionTraitCatalog.JellyfishCasterSwapMaxHp] = new(
                    "相位生长",
                    "每当敌人换位时，自身获得+10最大HP。"),
                [MinionTraitCatalog.MermaidZeroCostAttack] = new(
                    "零费潮涌",
                    "每使用一张能量消耗为0的卡牌，获得5%增伤（永久）。"),
                [MinionTraitCatalog.AbyssCreaturePoisonOnDamage] = new(
                    "深渊毒触",
                    "对敌人血量造成伤害时施加中毒×5（永久）；多段攻击会多次施加。"),
                [MinionTraitCatalog.CorruptedCrabPoisonOnHit] = new(
                    "溃烂反噬",
                    "每当受到伤害（包括护甲被打），对随机一个目标施加中毒×5（永久）。"),
                [MinionTraitCatalog.PhantomCaptainFrenzy] = new(
                    "船长狂怒",
                    "当任意敌人血量低于25%或死亡时，自身获得33%增伤和20%易伤（不叠加）。"),

                [CharacterTraitCatalog.BossFirstHitBlock] = new(
                    "首击护甲",
                    "每回合首次受到伤害时获得10点护甲。"),
                [CharacterTraitCatalog.BossTurnDefenseUp] = new(
                    "愈战愈坚",
                    "每回合开始时永久+1基础防御。"),
                [CharacterTraitCatalog.SkullSelfDestructHand] = new(
                    "自爆手牌",
                    "存活时每回合开始将自爆牌加入手牌（不占抽牌上限）。"),
                [CharacterTraitCatalog.GhostQueenEnrage] = new(
                    "女王之怒",
                    "首次HP低于120时虚化，并在下回合获得「幽灵女王之怒」。"),
                [CharacterTraitCatalog.WardenCageMaster] = new(
                    "典狱长",
                    "开战召唤囚笼；场上无囚笼时永久获得50%增伤。"),
                [CharacterTraitCatalog.PrisonCage] = new(
                    "囚笼",
                    "回合开始自伤；死亡时召唤精英并清除玩家烙印。"),
                [CharacterTraitCatalog.DarkKnightPoisonAura] = new(
                    "毒雾光环",
                    "回合开始全体玩家+1永久中毒；玩家每层中毒视为 1 层易伤。"),
                [CharacterTraitCatalog.OceanGoddessTide] = new(
                    "潮汐主宰",
                    "腐化海洋女神：涨潮/退潮机制。")
            };

            return map;
        }
    }
}
