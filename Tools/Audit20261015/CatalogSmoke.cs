using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Core;

// Read-only YAML fixture loader for a runtime smoke check outside Unity.
// This deliberately does not claim to verify card descriptions or scene presentation.
public static class CatalogSmoke
{
    static string Decode(string value) => value.StartsWith("\"") ? JsonSerializer.Deserialize<string>(value) : value;
    static void Set(object target,string key,string raw)
    {
        var property=target.GetType().GetProperty(key);
        if(property==null || !property.CanWrite) return;
        var type=property.PropertyType;
        object value=type==typeof(string)?Decode(raw): type==typeof(bool)?raw=="1":
            type.IsEnum?Enum.ToObject(type,int.Parse(raw)):
            Convert.ChangeType(raw,type,System.Globalization.CultureInfo.InvariantCulture);
        property.SetValue(target,value);
    }
    public static CardTemplate Load(string path)
    {
        var card=new CardTemplate(); EffectActionSpec action=null; bool keywords=false;
        foreach(var line in File.ReadLines(path))
        {
            if(line.StartsWith("  Actions:")) { keywords=false; continue; }
            if(line.StartsWith("  Keywords:")) { keywords=true; continue; }
            if(keywords && line.StartsWith("  - ")) { card.Keywords.Add(Decode(line.Substring(4))); continue; }
            if(line.StartsWith("  - Type:")) { action=new EffectActionSpec(); card.Actions.Add(action); Set(action,"Type",line.Substring(9).Trim()); continue; }
            if(action!=null && line.StartsWith("    "))
            {
                var text=line.Trim(); var split=text.IndexOf(':');
                if(split>=0) Set(action,text.Substring(0,split),text.Substring(split+1).Trim());
                continue;
            }
            if(line.StartsWith("  CardArt:")) break;
            if(!line.StartsWith("  ") || line.StartsWith("    ")) continue;
            var item=line.Trim(); var index=item.IndexOf(':'); if(index<0) continue;
            var key=item.Substring(0,index); var raw=item.Substring(index+1).Trim();
            if(key=="CardId") card.DefinitionId=Decode(raw);
            else if(key=="DisplayName" || key=="OwnerCharacterId" || key=="Cost" || key=="CardType") Set(card,key,raw);
        }
        GhostQueenCardCatalog.TryApplyCanonical(card);
        V09BossCardCatalog.TryApplyCanonical(card);
        AbyssMonsterCardCatalog.TryApplyCanonical(card);
        return card;
    }
    public static CardInstanceState Instance(CardTemplate t,int id,string owner)
    {
        var c=new CardInstanceState {InstanceId=id,DefinitionId=t.DefinitionId,DisplayName=t.DisplayName,OwnerCharacterId=t.OwnerCharacterId,
            OwnerCombatantId=owner,Cost=t.Cost,BaseCost=t.Cost,CardType=t.CardType};
        c.Keywords.AddRange(t.Keywords); c.Actions.AddRange(t.Actions.Select(EffectActionSpec.Clone)); return c;
    }
    public static string Run()
    {
        var templates=Directory.GetFiles("Assets/_Project/Data/Cards","*.asset").Select(Load).ToArray();
        var failures=new List<string>(); int ran=0;
        var playerIds=new HashSet<string>{"char_knight","char_mage","char_ranger","char_snake_queen","char_lich_queen"};
        foreach(var template in templates)
        foreach(var level in new[]{0,1,5})
        {
            try
            {
                var s=new BattleState {Phase=TurnPhase.Planning,EnergyMax=50,EnergyCurrent=50};
                foreach(var t in templates) s.Config.CardCatalog[t.DefinitionId]=t;
                foreach(var owner in templates.Select(t=>t.OwnerCharacterId).Distinct())
                    s.Config.SummonTemplates[owner]=new CombatantConfig {CharacterDefinitionId=owner,MaxHp=100,Team=TeamSide.Enemy};
                var team=playerIds.Contains(template.OwnerCharacterId)?TeamSide.Player:TeamSide.Enemy;
                for(int i=0;i<6;i++)
                {
                    var t=i<3?team:(team==TeamSide.Player?TeamSide.Enemy:TeamSide.Player);
                    s.Combatants.Add(new CombatantState {Id="u"+i,DisplayName="Unit"+i,Team=t,Slot=(FormationSlot)(i%3+1),Hp=100,MaxHp=200,Speed=7-i%3,
                        CharacterDefinitionId=i==0?template.OwnerCharacterId:(i==1?"char_mage":"char_ranger")});
                }
                var c=Instance(template,1,"u0");
                if(level>0) c=CardInstanceUpgradeApplier.ApplyBonusLevels(c,level);
                s.CardsById[1]=c; s.NextCardInstanceId=2; s.GetHand(team).Add(c);
                var targets=CardRules.GetValidTargetCandidates(s,c,s.Combatants[0]);
                if(targets.Count>0) s.ResolutionTargets[1]=targets[0].Id;
                var events=new List<BattleEvent>(); var rng=new BattleRng(123);
                if(SpecialCardRules.IsSpecialCard(c)) SpecialCardRules.TryResolve(s,s.Combatants[0],c,events,rng);
                else EffectActionExecutor.ExecuteAll(s,s.Combatants[0],c,events,rng);
                foreach(var unit in s.Combatants)
                    if(unit.Hp<0 || unit.Block<0 || unit.MaxHp<=0) throw new Exception("Invalid unit HP/block");
                ran++;
            }
            catch(Exception e) {failures.Add(template.DefinitionId+" level+"+level+": "+e.GetType().Name+" "+e.Message);}
        }
        return "Cards="+templates.Length+" Executions="+(ran+failures.Count)+" Passed="+ran+" Failed="+failures.Count+Environment.NewLine+string.Join(Environment.NewLine,failures);
    }

    public static string RunEnginePaths()
    {
        var templates=Directory.GetFiles("Assets/_Project/Data/Cards","*.asset").Select(Load).ToArray();
        var playerIds=new HashSet<string>{"char_knight","char_mage","char_ranger","char_snake_queen","char_lich_queen"};
        var failures=new List<string>(); var rejected=new List<string>(); int ran=0;
        foreach(var template in templates)
        {
            try
            {
                var config=new BattleConfig {Seed=123,EnergyCap=50,CardsDrawnPerTurn=0,ManualEnemyIntentsOnly=true,HandLimit=20};
                foreach(var t in templates) config.CardCatalog[t.DefinitionId]=t;
                var team=playerIds.Contains(template.OwnerCharacterId)?TeamSide.Player:TeamSide.Enemy;
                for(int i=0;i<6;i++)
                {
                    var side=i<3?team:(team==TeamSide.Player?TeamSide.Enemy:TeamSide.Player);
                    config.Combatants.Add(new CombatantConfig {Id="u"+i,DisplayName="Unit"+i,Team=side,Slot=(FormationSlot)(i%3+1),MaxHp=1000,Speed=7-i%3,
                        CharacterDefinitionId=i==0?template.OwnerCharacterId:(i==1?"char_mage":"char_ranger")});
                }
                foreach(var owner in templates.Select(t=>t.OwnerCharacterId).Distinct())
                {
                    var summon=new CombatantConfig {CharacterDefinitionId=owner,MaxHp=100,Team=TeamSide.Enemy};
                    summon.DeckTemplates.AddRange(templates.Where(t=>t.OwnerCharacterId==owner));
                    config.SummonTemplates[owner]=summon;
                }
                var engine=new BattleEngine(config); engine.StartBattle(); var s=engine.State;
                var c=SummonRules.CreateBoundCard(s,template,"u0");
                s.GetHand(team).Add(c);
                var actor=s.GetCombatant("u0");
                var candidates=CardRules.GetValidTargetCandidates(s,c,actor);
                if(team==TeamSide.Player)
                {
                    if(c.Keywords.Contains("quick_start"))
                    {
                        if(candidates.Count>0) s.ResolutionTargets[c.InstanceId]=candidates[0].Id;
                        if(!engine.TryResolveQuickStartCard(c.InstanceId)) { rejected.Add(c.DefinitionId); continue; }
                    }
                    else
                    {
                        if(!engine.Draft.TrySelectCard(c.InstanceId)) { rejected.Add(c.DefinitionId); continue; }
                        if(engine.Draft.AwaitingTargetCardId!=null && candidates.Count>0)
                            engine.Draft.TryAssignTargetAndSelect(candidates[0].Id);
                        if(!engine.CommitPlayerPlan()) { rejected.Add(c.DefinitionId); continue; }
                    }
                }
                else
                {
                    s.EnemyPlan.PlayQueue.Add(c.InstanceId);
                    if(candidates.Count>0) s.ResolutionTargets[c.InstanceId]=candidates[0].Id;
                    engine.SkipPlayerTurn();
                }
                engine.FlushPendingEndOfTurn();
                foreach(var unit in s.Combatants)
                    if(unit.Hp<0 || unit.Block<0 || unit.MaxHp<=0) throw new Exception("Invalid unit HP/block");
                foreach(var side in new[]{TeamSide.Player,TeamSide.Enemy})
                {
                    var pileIds=s.GetHand(side).Concat(s.GetDrawPile(side)).Concat(s.GetDiscardPile(side)).Concat(s.GetExhaustPile(side)).Select(c2=>c2.InstanceId).ToArray();
                    if(pileIds.Distinct().Count()!=pileIds.Length) throw new Exception("Card exists in multiple piles");
                }
                ran++;
            }
            catch(Exception e) { failures.Add(template.DefinitionId+": "+e.GetType().Name+" "+e.Message); }
        }
        return "EnginePaths Passed="+ran+" Failed="+failures.Count+" Rejected="+rejected.Count+Environment.NewLine+
            string.Join(Environment.NewLine,failures)+Environment.NewLine+"Rejected: "+string.Join(",",rejected);
    }

    static CombatantConfig LoadCharacter(string path,Dictionary<string,CardTemplate> byGuid)
    {
        var c=new CombatantConfig(); string group="";
        foreach(var line in File.ReadLines(path))
        {
            if(line.StartsWith("  - "))
            {
                if(group=="Traits") c.Traits.Add(Decode(line.Substring(4)));
                else
                {
                    var match=Regex.Match(line,@"guid: ([a-f0-9]+)");
                    if(match.Success && byGuid.TryGetValue(match.Groups[1].Value,out var card))
                    {
                        if(group=="Deck") c.DeckTemplates.Add(card);
                        if(group=="SkillPool") c.SkillPoolCandidates.Add(card);
                    }
                }
                continue;
            }
            if(!line.StartsWith("  ") || line.StartsWith("    ")) continue;
            var text=line.Trim(); var split=text.IndexOf(':'); if(split<0) continue;
            var key=text.Substring(0,split); var raw=text.Substring(split+1).Trim();
            group=key;
            if(key=="CharacterId") c.CharacterDefinitionId=Decode(raw);
            else Set(c,key,raw);
        }
        c.UseSkillPool=c.Team==TeamSide.Enemy && c.SkillPoolCandidates.Count>0;
        return c;
    }

    static CombatantConfig CopyCharacter(CombatantConfig source,string id,FormationSlot slot)
    {
        var c=new CombatantConfig {Id=id,CharacterDefinitionId=source.CharacterDefinitionId,DisplayName=source.DisplayName,Team=source.Team,
            MaxHp=source.MaxHp,Speed=source.Speed,Slot=slot,UseSkillPool=source.UseSkillPool};
        c.DeckTemplates.AddRange(source.DeckTemplates.Select(EnemyDeckBuilder.CloneTemplate));
        c.SkillPoolCandidates.AddRange(source.SkillPoolCandidates.Select(EnemyDeckBuilder.CloneTemplate)); c.Traits.AddRange(source.Traits);
        return c;
    }

    public static string RunBattles()
    {
        var byGuid=new Dictionary<string,CardTemplate>();
        foreach(var path in Directory.GetFiles("Assets/_Project/Data/Cards","*.asset"))
        {
            var match=Regex.Match(File.ReadAllText(path+".meta"),@"guid: ([a-f0-9]+)");
            byGuid[match.Groups[1].Value]=Load(path);
        }
        var characters=Directory.GetFiles("Assets/_Project/Data/Characters","*.asset").Select(p=>LoadCharacter(p,byGuid)).ToArray();
        var players=characters.Where(c=>c.Team==TeamSide.Player).ToArray();
        var enemies=characters.Where(c=>c.Team==TeamSide.Enemy).ToArray();
        var failures=new List<string>(); int completed=0,limited=0,totalTurns=0;
        for(int seed=1;seed<=100;seed++)
        {
            string enemyName="";
            try
            {
                var rng=new BattleRng(seed); var config=new BattleConfig {Seed=seed,CardsDrawnPerTurn=8,EnergyCap=12,EnemyTurnEnergyBudget=4,EnemyCardsDrawnPerTurn=5};
                foreach(var t in byGuid.Values) config.CardCatalog[t.DefinitionId]=t;
                foreach(var character in characters) config.SummonTemplates[character.CharacterDefinitionId]=character;
                for(int i=0;i<3;i++) config.Combatants.Add(CopyCharacter(players[(seed+i)%players.Length],"p"+i,(FormationSlot)(i+1)));
                var enemy=enemies[seed%enemies.Length]; enemyName=enemy.CharacterDefinitionId;
                var boss=enemy.Traits.Any(t=>t.StartsWith("boss_") || t==CharacterTraitCatalog.WardenCageMaster || t==CharacterTraitCatalog.OceanGoddessTide || t==CharacterTraitCatalog.DarkKnightPoisonAura || t==CharacterTraitCatalog.GhostQueenEnrage);
                config.Combatants.Add(CopyCharacter(enemy,"e0",FormationSlot.Front));
                if(!boss)
                    for(int i=1;i<3;i++) config.Combatants.Add(CopyCharacter(enemies[(seed+i)%enemies.Length],"e"+i,(FormationSlot)(i+1)));
                var engine=new BattleEngine(config); engine.StartBattle();
                for(int turn=0;turn<40 && engine.State.Outcome==BattleOutcome.Ongoing;turn++)
                {
                    var s=engine.State;
                    foreach(var c in s.PlayerHand.ToArray())
                    {
                        if(s.Outcome!=BattleOutcome.Ongoing || !c.IsUsable) continue;
                        var owner=s.GetCombatant(PositionRules.GetOwnerCombatantId(s,c)??"");
                        if(owner==null) continue;
                        var candidates=CardRules.GetValidTargetCandidates(s,c,owner);
                        if(c.Keywords.Contains("quick_start"))
                        {
                            if(candidates.Count>0) s.ResolutionTargets[c.InstanceId]=candidates[rng.NextIndex(candidates.Count)].Id;
                            engine.TryResolveQuickStartCard(c.InstanceId);
                        }
                        else if(engine.Draft.TrySelectCard(c.InstanceId) && engine.Draft.AwaitingTargetCardId!=null)
                        {
                            if(candidates.Count>0) engine.Draft.TryAssignTargetAndSelect(candidates[rng.NextIndex(candidates.Count)].Id);
                            else engine.Draft.CancelAwaitingTarget();
                        }
                    }
                    if(s.Outcome!=BattleOutcome.Ongoing) break;
                    if(!engine.CommitPlayerPlan()) engine.SkipPlayerTurn();
                    engine.FlushPendingEndOfTurn(); totalTurns++;
                    foreach(var unit in s.Combatants)
                        if(unit.Hp<0 || unit.Block<0 || unit.MaxHp<=0) throw new Exception("Invalid unit HP/block");
                    foreach(var side in new[]{TeamSide.Player,TeamSide.Enemy})
                    {
                        var ids=s.GetHand(side).Concat(s.GetDrawPile(side)).Concat(s.GetDiscardPile(side)).Concat(s.GetExhaustPile(side)).Select(c=>c.InstanceId).ToArray();
                        if(ids.Distinct().Count()!=ids.Length) throw new Exception("Card exists in multiple piles");
                    }
                }
                if(engine.State.Outcome==BattleOutcome.Ongoing) limited++; else completed++;
            }
            catch(Exception e) {failures.Add("seed="+seed+" enemy="+enemyName+": "+e.GetType().Name+" "+e.Message+" "+e.StackTrace);}
        }
        return "Battles=100 Completed="+completed+" TurnLimited="+limited+" Turns="+totalTurns+" Failed="+failures.Count+Environment.NewLine+string.Join(Environment.NewLine,failures);
    }
}
