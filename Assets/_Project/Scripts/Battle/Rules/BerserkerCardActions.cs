using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public static class BerserkerCardActions
    {
        static EffectActionSpec Damage(int n, EffectTarget target = EffectTarget.DefaultEnemy, int hits = 1) => new()
        {Type=EffectActionType.DealDamage,Target=target,Value=n,HitCount=hits,Reach=target==EffectTarget.AllEnemies?TargetReach.Any:TargetReach.FrontAndMiddle};
        static EffectActionSpec Block(int n, EffectTarget target = EffectTarget.Self) => new(){Type=EffectActionType.GainBlock,Target=target,Value=n,Reach=TargetReach.Any};
        static EffectActionSpec Status(string id,int n,int turns,EffectTarget target=EffectTarget.DefaultEnemy) => new()
        {Type=EffectActionType.ApplyStatus,Target=target,StatusId=id,Stacks=n,Duration=turns,Reach=TargetReach.Any};

        public static CardTemplate Create(string id)
        {
            var e=BerserkerCardCatalog.Get(id);if(e==null)return null;
            var t=new CardTemplate{DefinitionId=id,DisplayName=e.Name,Cost=e.Cost,OwnerCharacterId=BerserkerSoulRules.CharacterId,CardType=e.Type};
            if(e.Description.Contains("【消耗】"))t.Keywords.Add("exhaust");
            if(e.Description.Contains("【快速启动】"))t.Keywords.Add("quick_start");
            if(e.Description.Contains("【AOE】"))t.Keywords.Add("aoe");
            if(e.Description.Contains("【应对攻击】"))t.Keywords.Add("parry");
            if(id==BerserkerSoulRules.CardId)t.Keywords.Add("berserker_repeat");
            if(id=="b_duel_to_end")t.Keywords.Add("berserker_duel");
            if(id=="b_burning_standard")t.Keywords.Add("berserker_fuel");
            switch(id)
            {
                case "b_axe_cut":t.Actions.Add(Damage(8));break;
                case "b_iron_guard":t.Actions.Add(Block(10));break;
                case "b_counter_cut":t.Actions.Add(Damage(14));break;
                case "b_axe_feint":t.Actions.Add(Damage(5));t.Actions.Add(Status(StatusCatalog.Weaken,10,2));break;
                case "b_stand_firm":t.Actions.Add(Block(8));break;
                case "b_sweeping_edge":t.Actions.Add(Damage(7,EffectTarget.AllEnemies));break;
                case "b_axe_crush":t.Actions.Add(Damage(12));break;
                case "b_measured_guard":t.Actions.Add(Block(9));break;
                case "b_advance":t.Actions.Add(new(){Type=EffectActionType.SwapPositionWithFrontAlly,Target=EffectTarget.Self});t.Actions.Add(Block(5));break;
                case "b_mark_prey":t.Actions.Add(Status(StatusCatalog.Vulnerable,15,2));break;
                case "b_turn_aside":t.Actions.Add(new(){Type=EffectActionType.GainBlockFromLastDamagePercent,Value=45,Target=EffectTarget.Self,Condition=ReactionConditionType.LastActionAttackOnSelf});break;
                case "b_short_hack":t.Actions.Add(Damage(7));break;
                case "b_split_helm":t.Actions.Add(Damage(24));break;
                case "b_two_hacks":t.Actions.Add(Damage(6,hits:2));break;
                case "b_rear_cleave":t.Actions.Add(Damage(13));break;
                case "b_bloodied_edge":t.Actions.Add(Damage(16));break;
                case "b_iron_breath":t.Actions.Add(Block(17));t.Actions.Add(new(){Type=EffectActionType.RemoveStatus,Target=EffectTarget.Self,StatusId=StatusCatalog.Weaken,Stacks=int.MaxValue});break;
                case "b_brace_assault":
                    t.Actions.Add(new(){Type=EffectActionType.GainBlockFromLastDamagePercent,Value=60,Target=EffectTarget.Self,Condition=ReactionConditionType.LastActionAttackOnSelf});
                    t.Actions.Add(new(){Type=EffectActionType.DealDamage,Value=8,Target=EffectTarget.LastActionActor,Condition=ReactionConditionType.LastActionAttackOnSelf});break;
                case "b_vent_anger":t.Actions.Add(new(){Type=EffectActionType.RemoveStatus,Target=EffectTarget.Self,StatusId=StatusCatalog.Vulnerable,Stacks=int.MaxValue});break;
                case "b_axe_execute":t.Actions.Add(Damage(20));break;
                case "b_guard_partner":t.Actions.Add(Block(12));t.Actions.Add(Block(12,EffectTarget.FrontAlly));break;
                case "b_whirlwind":t.Actions.Add(Damage(12,EffectTarget.AllEnemies));break;
                case "b_break_line":t.Actions.Add(Damage(19));break;
                case "b_last_reserve":t.Actions.Add(Block(14));break;
                case "b_duel_to_end":t.Actions.Add(Status("berserker_duel",1,-1));break;
            }
            return t;
        }
    }
}
