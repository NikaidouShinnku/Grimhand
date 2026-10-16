using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class AssetLinkAudit
{
    public static string Run()
    {
        var map=new Dictionary<string,string>();
        var metas=Directory.EnumerateFiles("Assets","*.meta",SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles("Library/PackageCache","*.meta",SearchOption.AllDirectories));
        foreach(var meta in metas)
        {
            var match=Regex.Match(File.ReadAllText(meta),@"(?m)^\s*guid: ([a-f0-9]{32})");
            if(match.Success) map[match.Groups[1].Value]=meta.Substring(0,meta.Length-5);
        }
        var problems=new List<string>(); int references=0, spriteIds=0, gifs=0;
        foreach(var path in Directory.EnumerateFiles("Assets/_Project","*",SearchOption.AllDirectories)
            .Where(p=>p.EndsWith(".asset")||p.EndsWith(".prefab")||p.EndsWith(".unity")))
        {
            var text=File.ReadAllText(path);
            foreach(Match m in Regex.Matches(text,@"\{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}"))
            {
                references++; var id=m.Groups[1].Value; var guid=m.Groups[2].Value;
                if(guid.StartsWith("0000000000000000")) continue;
                if(!map.TryGetValue(guid,out var target)) { problems.Add(path+": MISSING GUID "+guid); continue; }
                if(!File.Exists(target)) { problems.Add(path+": MISSING FILE "+target); continue; }
                if(target.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
                {
                    spriteIds++;
                    var meta=File.ReadAllText(target+".meta");
                    if(id!="21300000" && !Regex.IsMatch(meta,@"(?<!\d)"+Regex.Escape(id)+@"(?!\d)"))
                        problems.Add(path+": MISSING SPRITE ID "+id+" in "+target);
                }
            }
            foreach(Match m in Regex.Matches(text,@"(?m)^\s+IdleAnimationGifPath: (.+)$"))
            {
                var raw=m.Groups[1].Value.Trim();
                if(raw.Length==0 || raw=="\"\"") continue;
                var gif=raw.StartsWith("\"")?System.Text.Json.JsonSerializer.Deserialize<string>(raw):raw;
                gifs++;
                if(!File.Exists(Path.Combine("Assets",gif))) problems.Add(path+": MISSING GIF "+gif);
            }
        }
        File.WriteAllLines("Tools/Audit20261015/asset-link-audit.txt",problems);
        return "AssetReferences="+references+" SpriteReferences="+spriteIds+" GifPaths="+gifs+" Problems="+problems.Count+Environment.NewLine+string.Join(Environment.NewLine,problems.Take(30));
    }
}
