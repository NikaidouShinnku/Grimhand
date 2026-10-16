using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Presentation.Battle;
using Grimhand.Expedition;

public static class PresentationAudit
{
    public static void Run()
    {
        var rows = new List<object>();
        int missing = 0, empty = 0, exceptions = 0;
        var unsupported=new SortedSet<string>();
        var describe=typeof(BattleUiFormatters).GetMethod("DescribeEffectClause",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach (var path in Directory.GetFiles("Assets/_Project/Data/Cards", "*.asset").OrderBy(x => x))
        {
            var template = CatalogSmoke.Load(path);
            var card = CatalogSmoke.Instance(template, 1, "owner");
            foreach(var action in card.Actions)
                if(string.IsNullOrWhiteSpace((string)describe.Invoke(null,new object[]{action,null,false,TargetPickSide.None,null,card,false,false,null})))
                    unsupported.Add(action.Type.ToString());
            var definition = new CardDefinitionSO { CardId=template.DefinitionId, DisplayName=template.DisplayName,
                OwnerCharacterId=template.OwnerCharacterId, Cost=template.Cost, CardType=template.CardType };
            definition.Keywords.AddRange(template.Keywords);
            definition.Actions.AddRange(template.Actions.Select(EffectActionDefinition.FromSpec));
            var defs = new Dictionary<string,CardDefinitionSO> { [template.DefinitionId] = definition };
            if (!CardDescriptionCatalog.TryGetByCardId(template.DefinitionId, out var catalog)
                && !CardDescriptionCatalog.TryGetByDisplayName(template.DisplayName, out catalog)) missing++;
            foreach (var mode in new[]{"battle-bonus","expedition"})
            foreach (var lv in new[]{0,1,5})
            {
                try
                {
                    var c = lv==0 ? card : CardInstanceUpgradeApplier.ApplyBonusLevels(card,lv);
                    if (mode=="expedition")
                    {
                        var upgraded=CatalogSmoke.Load(path);
                        CardUpgradeRules.ApplyToTemplate(upgraded,lv);
                        upgraded.UpgradeLevel=Grimhand.Core.CardUpgradeCatalog.TryGetByDisplayName(upgraded.DisplayName,out _)?lv:0;
                        c=CatalogSmoke.Instance(upgraded,1,"owner");
                        c.UpgradeLevel=upgraded.UpgradeLevel;
                    }
                    var text = BattleUiFormatters.BuildCardStatsLinePreview(c,defs);
                    if (string.IsNullOrWhiteSpace(text)) empty++;
                    rows.Add(new {mode,id=c.DefinitionId,name=c.DisplayName,owner=c.OwnerCharacterId,requestedLevel=lv,
                        actualLevel=c.UpgradeLevel,cost=c.Cost,keywords=c.Keywords,description=text,catalog,
                        actionSummary=string.Join("; ",c.Actions.Select(a=>a.Type+"("+a.Target+") V="+a.Value+" "+a.StatusId+" S="+a.Stacks+" D="+a.Duration+" hits="+a.HitCount+" altHeal="+a.AlternateValueIfHealed)),actions=c.Actions});
                }
                catch (Exception e) { exceptions++; Console.WriteLine("ERROR "+template.DefinitionId+": "+e); }
            }
        }
        File.WriteAllText("Tools/Audit20261015/presentation-audit.json",JsonSerializer.Serialize(rows,new JsonSerializerOptions {WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Console.WriteLine("DescriptionRows="+rows.Count+" MissingCatalog="+missing+" Empty="+empty+" Exceptions="+exceptions);
        Console.WriteLine("UnsupportedActionDescriptions="+string.Join(",",unsupported));
    }
}
