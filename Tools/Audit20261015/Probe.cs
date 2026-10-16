using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.AI;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

public static class AuditProbe20261015
{
    static BattleState State()
    {
        var s = new BattleState();
        s.Combatants.Add(new CombatantState { Id="p", CharacterDefinitionId="char_knight", DisplayName="Player", Team=TeamSide.Player, Slot=FormationSlot.Front, Hp=100, MaxHp=100 });
        s.Combatants.Add(new CombatantState { Id="e", CharacterDefinitionId="enemy", DisplayName="Enemy", Team=TeamSide.Enemy, Slot=FormationSlot.Front, Hp=100, MaxHp=100 });
        return s;
    }
    static CardInstanceState Card(BattleState s,int id,int cost,TeamSide side)
    {
        var c = new CardInstanceState { InstanceId=id, DefinitionId="audit_"+id, DisplayName="Audit"+id, Cost=cost, BaseCost=cost, CardType=CardType.Attack, OwnerCombatantId=side==TeamSide.Player?"p":"e" };
        c.Actions.Add(new EffectActionSpec { Type=EffectActionType.DealDamage, Target=EffectTarget.DefaultEnemy, Value=10 });
        s.CardsById[id]=c;
        s.GetHand(side).Add(c);
        return c;
    }
    public static void Main()
    {
        var s=State(); s.EnergyMax=8; s.EnergyCurrent=12;
        Console.WriteLine("ENERGY restore(1): delta="+EnergyRules.Restore(s,1)+", energy="+s.EnergyCurrent);
        Console.WriteLine("REDUCTION100 damage10="+CombatModifierRules.ApplyIncomingDamageModifiers(new CombatantState {IncomingDamageReductionPercent=100},10,0));
        foreach(var mode in new[]{"invulnerable","ethereal"})
        {
            s=State(); var c=Card(s,1,1,TeamSide.Player); var p=s.Combatants[0]; var e=s.Combatants[1];
            if(mode=="invulnerable") e.InvulnerableTurnsRemaining=1;
            else StatusRules.ApplyStatus(s,e,StatusCatalog.Ethereal,1,2,new List<BattleEvent>());
            var preview=CardPreviewRules.PreviewHpDamageAgainstTarget(s,p,c,c.Actions[0],e);
            DamageRules.ApplyDamage(s,p,e,10,CardType.Attack,new List<BattleEvent>(),sourceCardInstanceId:1);
            Console.WriteLine(mode+" preview="+preview+", actualHpLoss="+(100-e.Hp));
        }
        s=State(); Card(s,1,1,TeamSide.Enemy); Card(s,2,2,TeamSide.Enemy); Card(s,3,2,TeamSide.Enemy);
        var plan=EnemyTurnPlanner.PrepareEnemyTurn(s,new BattleRng(1),4);
        Console.WriteLine("AI budget4 costs1,2,2: spent="+plan.Plan.EnergySpent+", selected="+string.Join(",",plan.Plan.PlayQueue));
        s=State(); s.Config.RunModifiers.HealBonusPercent=50; var healer=s.Combatants[0]; healer.CharacterDefinitionId=RelicBattleRules.PharaohCharacterId; healer.Hp=50;
        var heal=new EffectActionSpec { Type=EffectActionType.Heal, Target=EffectTarget.Self, Value=10 };
        var shown=CardPreviewRules.ComputeExpectedHeal(s,healer,heal);
        DamageRules.ApplyHeal(s,healer,10,new List<BattleEvent>(),healer);
        Console.WriteLine("HEAL bonus50 preview="+shown+", actual="+(healer.Hp-50));
        s=State(); var a=s.Combatants[0]; var b=s.Combatants[1]; b.Team=TeamSide.Player; a.Hp=50; b.Hp=50;
        s.SoulBondPartnerByCombatantId[a.Id]=b.Id; s.SoulBondPartnerByCombatantId[b.Id]=a.Id;
        DamageRules.ApplyHeal(s,a,1,new List<BattleEvent>(),a);
        Console.WriteLine("SOUL_BOND heal1 from50/50: hp="+a.Hp+"/"+b.Hp);
        s=State(); var curse=Card(s,1,1,TeamSide.Player); curse.IsUsable=false; curse.Keywords.Add("curse");
        var locked=Card(s,2,1,TeamSide.Player); locked.IsUsable=false; locked.Keywords.Add("engraving_lock");
        CombatantDeathRules.RestoreUsableCards(s,s.Combatants[0]);
        Console.WriteLine("REVIVE curseUsable="+curse.IsUsable+", engravingLockedUsable="+locked.IsUsable);
        s=State(); var musket=Card(s,1,1,TeamSide.Enemy); musket.DefinitionId="m_musket_shot"; musket.Actions[0].Reach=TargetReach.BackOnly;
        Console.WriteLine("MUSKET one target: canPickFront="+TargetReachRules.CanPickUnit(s,musket,s.Combatants[0],s.Combatants[1])+", plannerCount="+EnemyTurnPlanner.PrepareEnemyTurn(s,new BattleRng(1),4).Plan.PlayQueue.Count);
        s=State(); var queen=s.Combatants[1]; queen.Hp=130; queen.MaxHp=220; queen.Traits.Add(CharacterTraitCatalog.GhostQueenEnrage);
        DamageRules.ApplyTrueDamage(s,s.Combatants[0],queen,20,new List<BattleEvent>());
        Console.WriteLine("QUEEN trueDamage 130->"+queen.Hp+": enrage="+queen.GhostQueenEnrageTriggered+", ethereal="+StatusRules.HasStatus(queen,StatusCatalog.Ethereal)+", nextTurnBonus="+s.PendingBossBonusHandsNextTurn.Count);
        s=State(); queen=s.Combatants[1]; queen.Hp=130; queen.MaxHp=220; queen.Traits.Add(CharacterTraitCatalog.GhostQueenEnrage);
        StatusRules.ApplyStatus(s,queen,StatusCatalog.Poison,20,3,new List<BattleEvent>());
        StatusRules.ProcessTurnStartStatuses(s,new List<BattleEvent>(),new BattleRng(1));
        Console.WriteLine("QUEEN poison 130->"+queen.Hp+": enrage="+queen.GhostQueenEnrageTriggered);
        s=State(); s.Combatants[0].Hp=3;
        StatusRules.ApplyStatus(s,s.Combatants[0],StatusCatalog.LastStand,1,2,new List<BattleEvent>());
        StatusRules.ApplyStatus(s,s.Combatants[0],StatusCatalog.Poison,5,3,new List<BattleEvent>());
        StatusRules.ProcessTurnStartStatuses(s,new List<BattleEvent>(),new BattleRng(1));
        Console.WriteLine("LAST_STAND hp3 poison5: hp="+s.Combatants[0].Hp);
    }
}
