using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using Grimhand.Expedition;

// Diagnostic only: independent full-HP starting-deck battles, not expedition win rates.
public static class BalanceAudit20261017
{
    public static string Run()
    {
        var byGuid = new Dictionary<string, CardTemplate>();
        foreach (var path in Directory.GetFiles("Assets/_Project/Data/Cards", "*.asset"))
            byGuid[Regex.Match(File.ReadAllText(path+".meta"), @"guid: ([a-f0-9]+)").Groups[1].Value] = CatalogSmoke.Load(path);
        var loader = typeof(CatalogSmoke).GetMethod("LoadCharacter", BindingFlags.Static|BindingFlags.NonPublic);
        var chars = Directory.GetFiles("Assets/_Project/Data/Characters", "*.asset")
            .Select(p => (CombatantConfig)loader.Invoke(null,new object[]{p,byGuid})).ToArray();
        var players = chars.Where(c=>c.Team==TeamSide.Player).OrderBy(c=>c.CharacterDefinitionId).ToArray();
        var monsters = MonsterEncounterBuilder.BuildMonsterTemplateMap(chars.Where(c=>c.Team==TeamSide.Enemy));
        var rows = new List<object>();
        foreach (var floor in new[]{1,10,25,45})
        foreach (var encounter in MonsterEncounterCatalog.Entries.Where(e=>e.Weight>0 && e.MinFloor<=floor && e.MaxFloor>=floor))
        for (int a=0;a<players.Length-2;a++) for(int b=a+1;b<players.Length-1;b++) for(int c=b+1;c<players.Length;c++)
        foreach (var policy in new[]{"attack-first","defense-first"})
        for(int seed=1;seed<=6;seed++)
        {
            var party = new[]{players[a],players[b],players[c]};
            var baseline = new BattleConfig {Seed=seed, EnergyCap=8,CardsDrawnPerTurn=5,TurnStartEnergyRegen=4,HandLimit=10};
            foreach(var t in byGuid.Values) baseline.CardCatalog[t.DefinitionId]=t;
            foreach(var template in chars) baseline.SummonTemplates[template.CharacterDefinitionId]=template;
            // Rotate party formation across seeds; no formation damage multiplier.
            for(int i=0;i<3;i++)
            {
                var unit=ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(party[(i+seed-1)%3]);
                unit.Id="p"+i;unit.Slot=(FormationSlot)(i+1);unit.Level=1;
                var stats=CharacterProgression.GetStatsForCharacter(unit.CharacterDefinitionId,1);
                unit.MaxHp=stats.MaxHp;unit.Speed=stats.Speed;
                baseline.Combatants.Add(unit);
            }
            int turns=0; string outcome="error",error=""; int remaining=0,deaths=0;
            try
            {
                var config=MonsterEncounterBuilder.Build(baseline,encounter,monsters);
                foreach(var enemy in config.Combatants.Where(u=>u.Team==TeamSide.Enemy)) EnemyFloorScaling.Apply(enemy,floor,new BattleRng(seed));
                var engine=new BattleEngine(config);engine.StartBattle();
                for(;turns<60 && engine.State.Outcome==BattleOutcome.Ongoing;turns++)
                {
                    var s=engine.State;
                    // Deliberately simple two policies, not an optimal player or a balance oracle.
                    var hand=s.PlayerHand.OrderBy(x=> x.CardType==(policy=="attack-first"?CardType.Attack:CardType.Defense)?0:1)
                        .ThenBy(x=>x.Cost).ThenBy(x=>x.InstanceId).ToArray();
                    foreach(var card in hand)
                    {
                        if(s.Outcome!=BattleOutcome.Ongoing || !card.IsUsable) continue;
                        var owner=s.GetCombatant(PositionRules.GetOwnerCombatantId(s,card)??"");
                        if(owner==null || owner.Hp<=0) continue;
                        var targets=CardRules.GetValidTargetCandidates(s,card,owner);
                        var target=targets.OrderBy(u=>u.Hp).ThenBy(u=>u.Id).FirstOrDefault();
                        if(card.Keywords.Contains("quick_start"))
                        {
                            if(target!=null) s.ResolutionTargets[card.InstanceId]=target.Id;
                            engine.TryResolveQuickStartCard(card.InstanceId);
                        }
                        else if(engine.Draft.TrySelectCard(card.InstanceId) && engine.Draft.AwaitingTargetCardId!=null)
                        {
                            if(target!=null) engine.Draft.TryAssignTargetAndSelect(target.Id);
                            else engine.Draft.CancelAwaitingTarget();
                        }
                    }
                    if(s.Outcome!=BattleOutcome.Ongoing) break;
                    if(!engine.CommitPlayerPlan()) engine.SkipPlayerTurn();
                    engine.FlushPendingEndOfTurn();
                    if(s.PendingPsionicScryCards.Count>0) {outcome="choice-blocked";break;}
                }
                var state=engine.State;
                if(outcome!="choice-blocked") outcome=state.Outcome==BattleOutcome.PlayerVictory?"win":state.Outcome==BattleOutcome.PlayerDefeat?"loss":"turn-limit";
                remaining=state.Combatants.Where(u=>u.Team==TeamSide.Player).Sum(u=>Math.Max(0,u.Hp));
                deaths=state.Combatants.Count(u=>u.Team==TeamSide.Player && u.Hp<=0);
            }
            catch(Exception e){error=e.GetType().Name+": "+e.Message;}
            rows.Add(new{floor,encounter=encounter.Id,elite=encounter.IsElite,party=string.Join("+",party.Select(u=>u.CharacterDefinitionId)),policy,seed,outcome,turns,remainingHp=remaining,deaths,error});
        }
        var catalog=byGuid.Values.OrderBy(t=>t.OwnerCharacterId).ThenBy(t=>t.DefinitionId).Select(t=>{
            string description;
            if(!Grimhand.Content.CardDescriptionCatalog.TryGetByCardId(t.DefinitionId,out description))
                Grimhand.Content.CardDescriptionCatalog.TryGetByDisplayName(t.DisplayName,out description);
            return new {id=t.DefinitionId,name=t.DisplayName,owner=t.OwnerCharacterId,cost=t.Cost,type=t.CardType.ToString(),description=description??"未找到静态文案",keywords=string.Join(",",t.Keywords)};
        }).ToArray();
        return JsonSerializer.Serialize(new {scope="Full-HP level-1 starting decks; no talents, meta, relics, card upgrades, rewards or inter-battle attrition. Floors above1 are stress tests, NOT progression balance. Two simple non-optimal policies;6 seeds rotating3 formations. 60-turn limits and blocked choices separated.",catalog,rows},new JsonSerializerOptions{WriteIndented=true});
    }
}
