using System;
using System.IO;
using System.Linq;
using Grimhand.Battle.Rules;
using Grimhand.Expedition;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public static class BerserkerTextExport
    {
        [Serializable] public class Row { public string id,name,type,description; public int cost,rarity,level,slot; }
        [Serializable] public class Export { public Row[] cards,talents,keywords; public string loreTitle,loreBody,unlock; }
        [MenuItem("Grimhand/Validation/Export berserker review text")]
        public static void ExportText()
        {
            CharacterLoreCatalog.TryGet(TalentCatalog.BerserkerId,out var lore);
            var data=new Export{
                cards=BerserkerCardCatalog.All.Select(c=>new Row{id=c.Id,name=c.Name,type=c.Type.ToString(),cost=c.Cost,rarity=c.Rarity,description=c.Description}).ToArray(),
                talents=TalentCatalog.GetForCharacter(TalentCatalog.BerserkerId).Select(t=>new Row{id=t.Id,name=t.ShortTitle,description=t.Description,level=t.UnlockLevel,slot=t.Slot}).ToArray(),
                keywords=new[]{"berserker_repeat","berserker_duel","berserker_fuel"}.Select(id=>{KeywordCatalog.TryGet(id,out var k);return new Row{id=id,name=k.DisplayName,description=k.Description};}).ToArray(),
                loreTitle=lore.TitleLine,loreBody=lore.Story,unlock=CharacterUnlockRules.LockedDescription
            };
            Directory.CreateDirectory("outputs/grimhand-text-review-20261019");
            File.WriteAllText("outputs/grimhand-text-review-20261019/current-text.json",JsonUtility.ToJson(data,true));
            Debug.Log("[BerserkerReview] Exported current runtime text.");
        }
    }
}
