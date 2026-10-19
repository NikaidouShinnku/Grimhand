using System.Collections.Generic;
using Grimhand.Battle.Model;
namespace Grimhand.Battle.Rules
{
    public static class BerserkerCardCatalog
    {
        public sealed class Entry
        {
            public readonly string Id, Name, Description;
            public readonly int Cost, Rarity;
            public readonly CardType Type;
            public Entry(string id,string name,int cost,CardType type,int rarity,string description)
            {Id=id;Name=name;Cost=cost;Type=type;Rarity=rarity;Description=description;}
        }
        public static readonly IReadOnlyList<Entry> All = new Entry[]
        {
            new("b_axe_cut","裂斧斩",1,CardType.Attack,0,"【前/中】造成8伤害。有狂怒时消耗1点，额外造成4伤害。"),
            new("b_iron_guard","铁臂架挡",1,CardType.Defense,0,"获得10护甲。"),
            new("b_counter_cut","反击重劈",2,CardType.Attack,0,"【前/中】造成14伤害。最多消耗2点狂怒，每点使本牌伤害+4。"),
            new("b_hold_anger","压住怒火",1,CardType.Status,0,"【快速启动】【消耗】获得1点狂怒。"),
            new("b_axe_feint","斧柄佯攻",1,CardType.Attack,0,"【前/中】造成5伤害，使目标获得10%虚弱（2回合）。"),
            new("b_stand_firm","立稳脚步",1,CardType.Defense,0,"获得8护甲。若自身处于前排，与中排友方交换位置。"),
            new("b_sweeping_edge","横斧破阵",2,CardType.Attack,1,"【AOE】造成7伤害。消耗1点狂怒，清除自身的虚弱和易伤；没有狂怒仍可攻击。"),
            new("b_axe_crush","破甲重劈",2,CardType.Attack,1,"【前/中】造成12伤害。伤害前移除目标一半护甲，每移除2护甲，本牌伤害+1。"),
            new("b_measured_guard","忍痛架挡",1,CardType.Defense,1,"获得9护甲；消耗所有狂怒，若消耗了狂怒，额外获得等同于出牌时自身已有护甲的护甲，最多30点。"),
            new("b_advance","踏阵",1,CardType.Status,1,"【快速启动】与前排友方交换位置；已在前排则不换位。获得5护甲。"),
            new("b_mark_prey","锁定猎物",1,CardType.Status,1,"【前/中/后】使目标获得15%易伤（2回合）。"),
            new("b_turn_aside","侧斧卸力",1,CardType.Defense,1,"【应对攻击】获得45%减伤。成功应对时获得1点狂怒；本牌每次使用最多获得1点。"),
            new("b_short_hack","短柄追击",1,CardType.Attack,1,"【前/中】造成7伤害。若目标本回合已被攻击，额外造成4伤害。"),
            new("b_quiet_anger","沉怒",1,CardType.Status,1,"【消耗】获得2点狂怒。"),
            new("b_split_helm","裂盔",3,CardType.Attack,2,"【前/中】造成24伤害。最多消耗3点狂怒，每点使伤害+5。"),
            new("b_two_hacks","回斧连斩",2,CardType.Attack,2,"【前/中】对同一目标造成6伤害两次。最多消耗2点狂怒，每点使每段伤害+2。"),
            new("b_rear_cleave","贯阵斧",2,CardType.Attack,2,"【前/中】造成13伤害，并对目标身后一格的敌人造成7伤害。最多消耗2点狂怒，每点使两处伤害分别+2。"),
            new("b_bloodied_edge","浴血斧锋",2,CardType.Attack,2,"【前/中】造成16伤害。自身HP不高于50%时额外造成6伤害。"),
            new("b_iron_breath","铁肺",2,CardType.Defense,2,"获得17护甲，清除自身的虚弱。"),
            new("b_brace_assault","迎锋",2,CardType.Defense,2,"【应对攻击】获得60%减伤，反击攻击者8伤害。最多消耗2点狂怒，每点使反击伤害+3。"),
            new("b_vent_anger","怒吼驱怯",1,CardType.Status,2,"清除自身的易伤。最多消耗2点狂怒，每点获得6护甲。"),
            new("b_axe_execute","追猎终结",3,CardType.Attack,2,"【前/中】造成20伤害。击败目标后，下回合抽牌后，从自己的弃牌堆选择1张1费或以下的攻击牌加入手牌。"),
            new("b_guard_partner","并肩抵御",2,CardType.Defense,2,"自身与一名其他友方各获得12护甲。"),
            new("b_concentrated_fury","专注怒意",2,CardType.Status,2,"【消耗】本场战斗中，自身每回合首次因敌方攻击牌损失HP时，额外获得1点狂怒。"),
            new("b_whirlwind","旋斧风暴",3,CardType.Attack,3,"【AOE】造成12伤害，敌方存活人数每少于3人一人，额外造成8伤害。消耗所有狂怒；若消耗了狂怒，本牌伤害增加30%。"),
            new("b_break_line","撕开战线",3,CardType.Attack,3,"【前/中】造成19伤害。目标存活且能被换位时，与其身后一格的敌人交换位置。"),
            new("b_last_reserve","留力再战",2,CardType.Defense,3,"获得14护甲。本回合结束时，留住手中未打出的裂斧攻击牌。"),
            new("b_tempered_rage","淬怒",2,CardType.Status,3,"【消耗】本场战斗中，每回合第一次通过卡牌耗尽狂怒后，下回合抽牌后，从自己的弃牌堆选择1张防御牌加入手牌。"),
            new("b_hunters_breath","猎者喘息",1,CardType.Status,3,"【消耗】消耗最多3点狂怒，每点回复4HP。无狂怒时不能使用。"),
            new("b_duel_to_end","不死不休",3,CardType.Status,4,"【消耗】与一名敌人【死斗】。它被击败后，若战斗继续，下回合从弃牌堆取回最多3张自己的攻击牌，使其在手中时费用为0。"),
            new("b_berserker_soul","狂战之魂",5,CardType.Status,4,BerserkerSoulRules.Description),
            new("b_burning_standard","燃尽战旗",4,CardType.Status,4,"【消耗】从下回合起，本场战斗中狂怒不再流失，全队出牌自动优先消耗狂怒抵扣费用：每点抵扣1费，每牌最多2费，X费牌除外。狂怒不再强化卡牌效果。"),
        };
        public static Entry Get(string id) {foreach(var e in All) if(e.Id==id)return e;return null;}
        public static string Describe(CardInstanceState card)
        {
            var e=Get(card.DefinitionId);if(e==null)return null;
            var text=e.Description;
            if(card.UpgradeLevel<=0||!Grimhand.Core.CardUpgradeCatalog.TryGetByDisplayName(e.Name,out var spec))return text;
            var n=card.UpgradeLevel;
            string ReplaceFirst(string pattern,int extra) => new System.Text.RegularExpressions.Regex(pattern).Replace(text,m=>m.Groups[1].Value+(int.Parse(m.Groups[2].Value)+extra)+m.Groups[3].Value,1);
            if(spec.DamagePerLevel>0)text=ReplaceFirst("(造成|反击攻击者)([0-9]+)(伤害)",spec.DamagePerLevel*n);
            if(spec.BlockPerLevel>0)text=ReplaceFirst("(获得|各获得)([0-9]+)(护甲)",spec.BlockPerLevel*n);
            if(spec.HealPerLevel>0)text=ReplaceFirst("(每点回复)([0-9]+)(HP)",spec.HealPerLevel*n);
            if(spec.VulnerablePerLevel>0)text=ReplaceFirst("(获得)([0-9]+)(%易伤)",spec.VulnerablePerLevel*n);
            if(spec.RespondMitigationPerLevel>0)text=ReplaceFirst("(获得)([0-9]+)(%减伤)",spec.RespondMitigationPerLevel*n);
            return text;
        }
    }
}

