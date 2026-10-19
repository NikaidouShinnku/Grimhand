using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class BerserkerRules
    {
        public const string InnateDescription="受到敌方攻击并实际损失HP后获得1点狂怒，同一敌牌一次，此来源每回合最多2点。狂怒上限4，从第2回合起回合开始流失1点。毒、烧、反伤、自伤、献祭及友伤不产生狂怒。";
        public static bool HasRageTalent(BattleState s) => Talent(s,1,1)||Talent(s,1,3)||Talent(s,1,7)||Talent(s,1,9);
        public static bool ShowRage(BattleState s, CombatantState owner)
        {
            var b=Get(s,owner);
            return b!=null && (HasRageTalent(s)||b.Rage>0||BannerActive(s,b));
        }
        public static BerserkerBattleContext Get(BattleState s,CombatantState owner)
        {
            if(s==null||owner?.CharacterDefinitionId!=BerserkerSoulRules.CharacterId)return null;
            if(!s.Berserkers.TryGetValue(owner.Id,out var b)){b=new BerserkerBattleContext();s.Berserkers.Add(owner.Id,b);}return b;
        }
        public static bool Talent(BattleState s,int slot,int level)=>TalentBattleRules.HasTalent(s,$"talent_berserker_s{slot}_lv{level}");
        public static bool BannerActive(BattleState s,BerserkerBattleContext b)=>b!=null&&b.BannerStartTurn>0&&s.TurnNumber>=b.BannerStartTurn;
        public static void ChangeRage(BattleState s,CombatantState owner,int amount,List<BattleEvent> events,string reason="狂怒")
        {
            var b=Get(s,owner);if(b==null||!owner.IsAlive)return;
            var previous=b.Rage;b.Rage=Math.Clamp(b.Rage+amount,0,4);
            if(previous==b.Rage)return;
            events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,$"{reason}{(b.Rage>previous?"+":"")}{b.Rage-previous}（{b.Rage}/4）"){CombatantId=owner.Id,Amount=b.Rage-previous});
        }
        public static void TurnStart(BattleState s,List<BattleEvent> events)
        {
            s.BerserkerRagePayments.Clear();
            foreach(var owner in s.Combatants)
            {
                var b=Get(s,owner);if(b==null)continue;
                if(!owner.IsAlive){b.Rage=0;continue;}
                if(!b.Initialized){b.Initialized=true;if(Talent(s,1,1))ChangeRage(s,owner,1,events);}
                else if(!BannerActive(s,b)&&!(Talent(s,1,9)&&!b.AttackedThisTurn))ChangeRage(s,owner,-1,events,"狂怒流失");
                b.BaseGainThisTurn=0;b.EnemyAttacksSeen.Clear();b.SpentByPlay.Clear();b.SuccessfulResponseCards.Clear();b.AttackedThisTurn=false;
                b.BehindByPlay.Clear();b.SplashDamageByPlay.Clear();
                b.ConcentratedUsed=b.HealUsed=b.SwapUsed=b.EmptyUsed=b.BlockUsed=b.KillUsed=b.StripUsed=false;
            }
        }
        static void BaseGain(BattleState s,CombatantState owner,int enemyCardId,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||!HasRageTalent(s)||!owner.IsAlive||enemyCardId<=0||!b.EnemyAttacksSeen.Add(enemyCardId))return;
            var n=Math.Min(2-b.BaseGainThisTurn,1);
            var before=b.Rage;ChangeRage(s,owner,n,events);b.BaseGainThisTurn+=b.Rage-before;
        }
        public static void IncomingAttack(BattleState s,CombatantState attacker,CombatantState owner,int hpDamage,int cardId,bool sacrifice,List<BattleEvent> events)
        {
            var source=s?.GetCard(cardId);var b=Get(s,owner);
            if(b==null||hpDamage<=0||sacrifice||attacker?.Team!=TeamSide.Enemy||source?.CardType!=CardType.Attack||!owner.IsAlive)return;
            BaseGain(s,owner,cardId,events);
            if(b.Concentrated&&!b.ConcentratedUsed){b.ConcentratedUsed=true;ChangeRage(s,owner,1,events);}
        }
        public static void Responded(BattleState s,CombatantState owner,CardInstanceState card,int enemyId,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||!b.SuccessfulResponseCards.Add(card.InstanceId))return;
            if(Talent(s,1,3))ChangeRage(s,owner,1,events);
            if(card.DefinitionId=="b_turn_aside")ChangeRage(s,owner,1,events);
        }
        public static void Healed(BattleState s,CombatantState owner,CombatantState healer,int amount,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||amount<=0||healer==null||healer.Id==owner.Id||healer.Team!=owner.Team||b.HealUsed||!Talent(s,2,8))return;
            b.HealUsed=true;ChangeRage(s,owner,1,events);
        }
        public static void Swapped(BattleState s,CombatantState owner,bool wasFront,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||!Talent(s,1,7))return;
            ChangeRage(s,owner,1,events);
        }
        public static int Spend(BattleState s,CombatantState owner,CardInstanceState card,int max,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||BannerActive(s,b))return 0;
            var n=Math.Min(max,b.Rage);if(n<=0)return 0;
            ChangeRage(s,owner,-n,events);OnSpend(s,owner,card,n,b.Rage==0,events);return n;
        }
        public static void OnSpend(BattleState s,CombatantState owner,CardInstanceState card,int n,bool emptied,List<BattleEvent> events)
        {
            var b=Get(s,owner);if(b==null||n<=0)return;
            b.SpentByPlay[card.InstanceId]=n;
            if(!b.StripUsed&&card.CardType==CardType.Attack&&card.OwnerCharacterId==BerserkerSoulRules.CharacterId&&n>=2&&Talent(s,2,6))
            {
                var targets=new HashSet<CombatantState>();
                foreach(var action in card.Actions)
                {
                    if(action.Type!=EffectActionType.DealDamage)continue;
                    if(action.Target==EffectTarget.AllEnemies)
                    {foreach(var unit in s.Combatants)if(unit.IsAlive&&unit.Team!=owner.Team)targets.Add(unit);}
                    else
                    {
                        var target=TargetRules.ResolveTarget(s,owner,action.Target,card.InstanceId,action:action);
                        if(target?.IsAlive==true)targets.Add(target);
                    }
                }
                if(card.DefinitionId=="b_rear_cleave")
                {
                    var primary=TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);
                    var behind=primary!=null?PositionRules.GetCombatantBehind(s,primary):null;
                    if(behind?.IsAlive==true)targets.Add(behind);
                }
                if(targets.Count>0)b.StripUsed=true;
                foreach(var target in targets)
                {var removed=Math.Min(6,target.Block);target.Block-=removed;events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,$"凿甲：移除{removed}护甲"){CombatantId=owner.Id,TargetId=target.Id,Amount=removed});}
            }
            if(emptied&&!b.EmptyUsed)
            {
                b.EmptyUsed=true;if(Talent(s,2,10))s.PendingDrawNextTurn++;
                if(b.Tempered)QueueRecall(s,owner,"淬怒",CardType.Defense,1);
            }
        }
        public static CardInstanceState Prepare(BattleState s,CombatantState owner,CardInstanceState original,List<BattleEvent> events,bool preview=false,int availableRage=-1,CombatantState previewTarget=null)
        {
            if(!preview)foreach(var previous in s.Berserkers.Values)previous.SpentByPlay.Remove(original.InstanceId);
            if(!preview&&s.BerserkerRagePayments.TryGetValue(original.InstanceId,out var payment))
            {
                s.BerserkerRagePayments.Remove(original.InstanceId);
                var supplier=s.GetCombatant(payment.OwnerId);
                if(supplier?.IsAlive==true)OnSpend(s,supplier,original,payment.Amount,payment.Emptied,events);
            }
            var b=Get(s,owner);if(b==null||original==null)return original;
            if(!preview&&original.CardType==CardType.Attack)b.AttackedThisTurn=true;
            if(BerserkerCardCatalog.Get(original.DefinitionId)==null)return original;
            var card=Clone(original);var bonus=0;var blockBonus=0;var id=card.DefinitionId;
            var remaining=availableRage>=0?availableRage:b.Rage;
            int SpendUpTo(int max)
            {
                if(!preview)return Spend(s,owner,card,max,events);
                if(BannerActive(s,b))return 0;
                var n=Math.Min(max,remaining);remaining-=n;return n;
            }
            switch(id)
            {
                case "b_axe_cut":bonus=SpendUpTo(1)*4;break;
                case "b_counter_cut":bonus=SpendUpTo(2)*4;break;
                case "b_sweeping_edge":
                    SpendUpTo(1);
                    break;
                case "b_axe_crush":
                    var armored=previewTarget??TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);
                    var stripped=Math.Max(0,armored?.Block??0)/2;
                    bonus=stripped/2;
                    if(!preview&&armored!=null)armored.Block-=stripped;
                    break;
                case "b_two_hacks":case "b_rear_cleave":bonus=SpendUpTo(2)*2;break;
                case "b_split_helm":bonus=SpendUpTo(3)*5;break;
                case "b_whirlwind":
                    var livingEnemies=0;
                    foreach(var unit in s.Combatants)if(unit.IsAlive&&unit.Team!=owner.Team)livingEnemies++;
                    bonus=Math.Max(0,3-livingEnemies)*8;
                    var empowered=SpendUpTo(4)>0;
                    foreach(var action in card.Actions)
                        if(action.Type==EffectActionType.DealDamage)
                            action.Value=empowered?(int)Math.Round((action.Value+bonus)*1.3):action.Value+bonus;
                    bonus=0;break;
                case "b_brace_assault":bonus=SpendUpTo(2)*3;break;
                case "b_measured_guard":if(SpendUpTo(4)>0)blockBonus=Math.Min(30,Math.Max(0,owner.Block));break;
                case "b_bloodied_edge":if(owner.Hp*2<=owner.MaxHp)bonus=6;break;
                case "b_short_hack":
                    var target=previewTarget??TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);if(target?.HitThisTurn==true)bonus=4;break;
            }
            foreach(var a in card.Actions)
            {if(a.Type==EffectActionType.DealDamage)a.Value+=bonus;if(a.Type==EffectActionType.GainBlock)a.Value+=blockBonus;}
            if(!preview&&id=="b_rear_cleave")
            {
                var target=TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);
                var behind=target!=null?PositionRules.GetCombatantBehind(s,target):null;
                if(behind!=null){b.BehindByPlay[card.InstanceId]=behind.Id;b.SplashDamageByPlay[card.InstanceId]=7+bonus;}
            }
            return card;
        }
        public static CardInstanceState PreviewForPlanning(BattleState s,Planning.PlanningDraft draft,CardInstanceState card,CombatantState previewTarget=null)
        {
            if(s==null||card==null||BerserkerCardCatalog.Get(card.DefinitionId)==null)return card;
            var owner=s.GetCombatant(PositionRules.GetOwnerCombatantId(s,card));var b=Get(s,owner);if(b==null)return card;
            var rage=b.Rage;
            if(draft!=null&&!BannerActive(s,b))
                foreach(var id in draft.SelectedQueue)
                {
                    if(id==card.InstanceId)break;
                    var earlier=s.GetCard(id);if(earlier==null||PositionRules.GetOwnerCombatantId(s,earlier)!=owner.Id)continue;
                    var max=earlier.DefinitionId switch
                    {
                        "b_axe_cut" or "b_sweeping_edge"=>1,
                        "b_counter_cut" or "b_two_hacks" or "b_rear_cleave" or "b_brace_assault" or "b_vent_anger"=>2,
                        "b_split_helm" or "b_hunters_breath"=>3,
                        "b_whirlwind" or "b_measured_guard"=>4,
                        _=>0
                    };
                    rage=Math.Max(0,rage-max);
                }
            return Prepare(s,owner,card,null,preview:true,availableRage:rage,previewTarget:previewTarget);
        }
        public static CardInstanceState Clone(CardInstanceState c)
        {
            var copy=new CardInstanceState{InstanceId=c.InstanceId,SpectralRepeatSourceId=c.SpectralRepeatSourceId,DefinitionId=c.DefinitionId,DisplayName=c.DisplayName,OwnerCharacterId=c.OwnerCharacterId,OwnerCombatantId=c.OwnerCombatantId,Cost=c.Cost,BaseCost=c.BaseCost,CostBeforeHandDiscount=c.CostBeforeHandDiscount,UpgradeLevel=c.UpgradeLevel,CardType=c.CardType,IsUsable=c.IsUsable,IsBonusHandCard=c.IsBonusHandCard};
            copy.Keywords.AddRange(c.Keywords);foreach(var a in c.Actions)copy.Actions.Add(EffectActionSpec.Clone(a));return copy;
        }
        public static bool ResolveSpecial(BattleState s,CombatantState owner,CardInstanceState card,List<BattleEvent> events,BattleRng rng)
        {
            var b=Get(s,owner);if(b==null)return false;
            switch(card.DefinitionId)
            {
                case "b_advance":
                    var allies=PositionRules.GetAliveSortedByPhysicalSlot(s,owner.Team);
                    if(allies.Count>0&&allies[0].Id!=owner.Id)PositionRules.SwapCombatants(s,owner,allies[0],events,"踏阵：与前排交换");
                    DamageRules.ApplyBlock(owner,5+card.UpgradeLevel,events,s,rng);return true;
                case "b_hold_anger":ChangeRage(s,owner,1,events);return true;
                case "b_quiet_anger":ChangeRage(s,owner,2,events);return true;
                case "b_concentrated_fury":b.Concentrated=true;return true;
                case "b_tempered_rage":b.Tempered=true;return true;
                case "b_hunters_breath":var n=Spend(s,owner,card,3,events);DamageRules.ApplyHeal(s,owner,n*(4+card.UpgradeLevel),events,healer:owner);return true;
                case "b_burning_standard":b.BannerStartTurn=s.TurnNumber+1;return true;
                case "b_duel_to_end":
                    var target=TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);
                    if(target!=null){b.DuelTargetId=target.Id;events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,$"死斗：{owner.DisplayName}与{target.DisplayName}"){CombatantId=owner.Id,TargetId=target.Id});}return true;
                default:return false;
            }
        }
        public static bool IsSpecial(string id)=>id is "b_advance" or "b_hold_anger" or "b_quiet_anger" or "b_concentrated_fury" or "b_tempered_rage" or "b_hunters_breath" or "b_burning_standard" or "b_duel_to_end";
        public static void RefundRagePayment(BattleState s,CardInstanceState card,List<BattleEvent> events)
        {
            if(s.BerserkerRagePayments.TryGetValue(card.InstanceId,out var p))
            {s.BerserkerRagePayments.Remove(card.InstanceId);ChangeRage(s,s.GetCombatant(p.OwnerId),p.Amount,events,"未出牌返还狂怒");}
        }
        public static CombatantState DuelTarget(BattleState s,CombatantState owner,CardInstanceState card)
        {
            var b=Get(s,owner);
            if(b?.DuelTargetId==null||card?.CardType!=CardType.Attack||card.Keywords.Contains("aoe"))return null;
            var target=s.GetCombatant(b.DuelTargetId);return target?.IsAlive==true?target:null;
        }
        public static CombatantState EnemyDuelTarget(BattleState s,CombatantState actor,EffectActionSpec action)
        {
            if(s==null||actor?.Team!=TeamSide.Enemy||action?.Type!=EffectActionType.DealDamage||action.Target!=EffectTarget.DefaultEnemy)return null;
            foreach(var pair in s.Berserkers)
            {
                var hero=s.GetCombatant(pair.Key);
                if(pair.Value.DuelTargetId==actor.Id&&hero?.IsAlive==true&&TargetReachRules.IsSlotAllowed(action.Reach,PositionRules.GetEffectiveSlot(s,hero)))return hero;
            }
            return null;
        }
        public static bool CanPlay(BattleState s,CombatantState owner,CardInstanceState card)
        {
            var b=Get(s,owner);
            return card.DefinitionId!="b_hunters_breath"||b!=null&&b.Rage>0&&!BannerActive(s,b);
        }
        public static void After(BattleState s,CombatantState owner,CardInstanceState card,List<BattleEvent> events,BattleRng rng)
        {
            var b=Get(s,owner);if(b==null)return;
            if(card.DefinitionId=="b_sweeping_edge"&&!BannerActive(s,b)&&b.SpentByPlay.ContainsKey(card.InstanceId))
            {
                StatusRules.RemoveStatus(owner,StatusCatalog.Weaken,int.MaxValue,events);
                StatusRules.RemoveStatus(owner,StatusCatalog.Vulnerable,int.MaxValue,events);
            }
            if(card.DefinitionId=="b_stand_firm"&&PositionRules.GetEffectiveSlot(s,owner)==FormationSlot.Front)
                foreach(var ally in s.Combatants)
                    if(ally.IsAlive&&ally.Team==owner.Team&&ally.Id!=owner.Id&&PositionRules.GetEffectiveSlot(s,ally)==FormationSlot.Middle)
                    {PositionRules.SwapCombatants(s,owner,ally,events,"立稳脚步：与中排友方交换位置");break;}
            if(card.DefinitionId=="b_rear_cleave"&&b.BehindByPlay.TryGetValue(card.InstanceId,out var behindId))
            {var behind=s.GetCombatant(behindId);if(behind?.IsAlive==true)DamageRules.ApplyDamage(s,owner,behind,b.SplashDamageByPlay[card.InstanceId],card.CardType,events,rng:rng,sourceCardInstanceId:card.InstanceId);b.BehindByPlay.Remove(card.InstanceId);}
            if(card.DefinitionId=="b_break_line")
            {var target=TargetRules.ResolveTarget(s,owner,EffectTarget.DefaultEnemy,card.InstanceId);if(target?.IsAlive==true)PositionRules.SwapTargetWithBehind(s,target,events);}
            if(card.DefinitionId=="b_vent_anger")DamageRules.ApplyBlock(owner,Spend(s,owner,card,2,events)*(6+card.UpgradeLevel),events,s,rng);
            if(card.DefinitionId=="b_last_reserve")
                foreach(var c in s.PlayerHand)
                    if(c.OwnerCharacterId==BerserkerSoulRules.CharacterId&&c.CardType==CardType.Attack&&!c.Keywords.Contains("inherit"))
                    {c.Keywords.Add("inherit");b.TemporaryRetains.Add(c.InstanceId);}
        }
        public static void AfterDiscard(BattleState s)
        {
            foreach(var b in s.Berserkers.Values)
            {foreach(var id in b.TemporaryRetains){var c=s.GetCard(id);var owner=c!=null?s.GetCombatant(PositionRules.GetOwnerCombatantId(s,c)):null;if(c!=null&&!BerserkerSoulRules.IsActive(s,owner))c.Keywords.Remove("inherit");}b.TemporaryRetains.Clear();}
        }
        public static void Killed(BattleState s,CombatantState attacker,CombatantState target,int cardId,List<BattleEvent> events)
        {
            var card=s?.GetCard(cardId);var b=Get(s,attacker);
            if(b!=null&&attacker.IsAlive&&card!=null&&card.CardType==CardType.Attack)
            {
                if(!b.KillUsed&&b.SpentByPlay.ContainsKey(cardId)&&Talent(s,2,4)){b.KillUsed=true;ChangeRage(s,attacker,1,events);}
                if(card.DefinitionId=="b_axe_execute")QueueRecall(s,attacker,"追猎终结",CardType.Attack,1,1);
            }
        }
        public static void Died(BattleState s,CombatantState target)
        {
            var own=Get(s,target);if(own!=null){own.Rage=0;own.DuelTargetId=null;s.BerserkerRecalls.RemoveAll(r=>r.OwnerId==target.Id);}
            foreach(var pair in s.Berserkers)
                if(pair.Value.DuelTargetId==target.Id){pair.Value.DuelTargetId=null;var hero=s.GetCombatant(pair.Key);if(hero?.IsAlive==true)QueueRecall(s,hero,"死斗胜利",CardType.Attack,3,free:true);}
        }
        public static void QueueRecall(BattleState s,CombatantState owner,string label,CardType type,int count,int maxCost=int.MaxValue,bool free=false)
        {s.BerserkerRecalls.Add(new BerserkerRecall{OwnerId=owner.Id,Label=label,Type=type,Remaining=count,MaxCost=maxCost,DueTurn=s.TurnNumber+1,FreeInHand=free,AllowSkip=count>1||free});}
        public static List<CardInstanceState> RecallCandidates(BattleState s,BerserkerRecall r)
        {var list=new List<CardInstanceState>();foreach(var c in s.PlayerDiscardPile)if(c.SpectralRepeatSourceId==0&&c.CardType==r.Type&&c.Cost<=r.MaxCost&&PositionRules.GetOwnerCombatantId(s,c)==r.OwnerId&&c.IsUsable)list.Add(c);return list;}
        public static BerserkerRecall PendingRecall(BattleState s)
        {
            if(s==null||s.Outcome!=BattleOutcome.Ongoing)return null;
            s.BerserkerRecalls.RemoveAll(r=>s.GetCombatant(r.OwnerId)?.IsAlive!=true||r.Remaining<=0||r.DueTurn<=s.TurnNumber&&(s.PlayerHand.Count>=s.Config.HandLimit||RecallCandidates(s,r).Count==0));
            return s.BerserkerRecalls.Find(r=>r.DueTurn<=s.TurnNumber);
        }
        public static bool ChooseRecall(BattleState s,int cardId,List<BattleEvent> events)
        {
            var r=PendingRecall(s);if(r==null)return false;
            if(cardId==0){if(!r.AllowSkip)return false;s.BerserkerRecalls.Remove(r);return true;}
            var card=RecallCandidates(s,r).Find(c=>c.InstanceId==cardId);if(card==null)return false;
            s.PlayerDiscardPile.Remove(card);s.PlayerHand.Add(card);r.Remaining--;
            if(r.FreeInHand)DeckRules.ApplyHandCostReduction(card,int.MaxValue);
            events.Add(new BattleEvent(BattleEventKind.CardDrawn,$"{r.Label}：取回{card.DisplayName}"){CombatantId=r.OwnerId,CardInstanceId=cardId});return true;
        }
    }
}
