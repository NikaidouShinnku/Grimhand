#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    /// <summary>Idempotent install of the reviewed character. Does not regenerate existing characters.</summary>
    public static class BerserkerContentInstaller
    {
        const string Root="Assets/_Project/Data";
        const string Art="Assets/The Grimhands Asset/characters/berserker";
        [MenuItem("Grimhand/Content/Install Berserker")]
        public static void Install()
        {
            Directory.CreateDirectory(Art);
            // Approved art lives in Assets. Re-installing content must never restore old drafts.
            AssetDatabase.Refresh();
            const string effectPath="Assets/The Grimhands Asset/effects/berserker_damage_effect.png";
            var effectImporter=AssetImporter.GetAtPath(effectPath) as TextureImporter;
            if(effectImporter==null)throw new System.InvalidOperationException("Missing berserker attack effect");
            effectImporter.textureType=TextureImporterType.Sprite;
            effectImporter.spriteImportMode=SpriteImportMode.Single;
            effectImporter.filterMode=FilterMode.Point;effectImporter.mipmapEnabled=false;
            effectImporter.alphaIsTransparency=true;effectImporter.spritePixelsPerUnit=100;
            effectImporter.textureCompression=TextureImporterCompression.Uncompressed;
            effectImporter.SaveAndReimport();
            foreach(var effectGuid in AssetDatabase.FindAssets("t:BattleActionEffectCatalogSO",new[]{Root}))
            {
                var effects=AssetDatabase.LoadAssetAtPath<BattleActionEffectCatalogSO>(AssetDatabase.GUIDToAssetPath(effectGuid));
                effects.BerserkerDamage=AssetDatabase.LoadAssetAtPath<Sprite>(effectPath);EditorUtility.SetDirty(effects);
            }
            foreach(var name in new[]{"idle","attack","hit","defend","defeat","card_profile","rage"})
            {
                var importer=AssetImporter.GetAtPath($"{Art}/{name}.png") as TextureImporter;
                if(importer==null)throw new System.InvalidOperationException("Missing berserker art: "+name);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                if(name=="card_profile" || name=="idle")
                {
                    // Same 1024px source / cropped oval sprite convention as the original heroes.
                    importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable 618
                    var rect=name=="card_profile"?new Rect(359,519,306,362):new Rect(207,11,762,946);
                    importer.spritesheet=new[]{new SpriteMetaData{name=name+"_berserker",rect=rect,alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}};
#pragma warning restore 618
                }
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spritePixelsPerUnit=100;importer.SaveAndReimport();
            }
            var cards=new Dictionary<string,CardDefinitionSO>();
            foreach(var e in BerserkerCardCatalog.All)
            {
                var c=LoadOrCreate<CardDefinitionSO>($"{Root}/Cards/{e.Id}.asset");
                var template=BerserkerCardActions.Create(e.Id);
                c.CardId=e.Id;c.DisplayName=e.Name;c.Cost=e.Cost;c.CardType=e.Type;c.OwnerCharacterId=BerserkerSoulRules.CharacterId;c.Rarity=(CardRarity)e.Rarity;
                c.Keywords=new List<string>(template.Keywords);c.Actions.Clear();
                foreach(var a in template.Actions)
                    c.Actions.Add(new EffectActionDefinition{Type=a.Type,Target=a.Target,Value=a.Value,StatusId=a.StatusId,Stacks=a.Stacks,Duration=a.Duration,Condition=a.Condition,Reach=a.Reach,HitCount=a.HitCount});
                cards.Add(e.Id,c);EditorUtility.SetDirty(c);
            }
            var hero=LoadOrCreate<CharacterDefinitionSO>($"{Root}/Characters/Character_Berserker.asset");
            hero.CharacterId=BerserkerSoulRules.CharacterId;hero.DisplayName="裂斧";hero.Team=TeamSide.Player;hero.Slot=FormationSlot.Front;
            hero.Level=1;hero.MaxHp=76;hero.Speed=5;hero.BaseAttack=hero.BaseDefense=0;
            hero.Traits=new List<string>();hero.Deck=new List<CardDefinitionSO>{cards["b_axe_cut"],cards["b_axe_cut"],cards["b_axe_cut"],cards["b_iron_guard"],cards["b_iron_guard"],cards["b_counter_cut"],cards["b_hold_anger"]};EditorUtility.SetDirty(hero);
            var visuals=AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>($"{Root}/CharacterVisualCatalog_Demo.asset");
            if(visuals==null)throw new System.InvalidOperationException("Character visual catalog missing");
            var v=visuals.GetEntry(hero.CharacterId);if(v==null){v=new CharacterVisualEntry{CharacterId=hero.CharacterId};visuals.Entries.Add(v);}
            Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{name}.png");
            v.IdlePortrait=Sprite("idle");v.AttackPortrait=Sprite("attack");v.HitPortrait=Sprite("hit");v.DefensePortrait=Sprite("defend");v.DeathPortrait=Sprite("defeat");v.CardProfilePortrait=Sprite("card_profile");
            v.PreserveOriginalFacing=true;v.HitPortraitFacesRight=true;v.IdleAnimationGifPath="The Grimhands Asset/characters/berserker/berserker_anim.gif";v.IdleAnimationFrames.Clear();EditorUtility.SetDirty(visuals);
            foreach(var guid in AssetDatabase.FindAssets("t:BattleUiIconCatalogSO",new[]{Root}))
            {
                var icons=AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(AssetDatabase.GUIDToAssetPath(guid));
                icons.StatusBerserkerRage=Sprite("rage");EditorUtility.SetDirty(icons);
            }
            var setup=AssetDatabase.LoadAssetAtPath<BattleSetupSO>($"{Root}/Setups/BattleSetup_Demo.asset");
            if(setup==null)throw new System.InvalidOperationException("Default roster setup missing");
            if(!setup.Combatants.Contains(hero))setup.Combatants.Add(hero);EditorUtility.SetDirty(setup);
            PlayerCardCatalogBinder.BindPlayerCardCatalogSilent();
            CharacterDefinitionCatalogBinder.BindSilent();CardDefinitionCatalogBinder.BindSilent();
            AssetDatabase.SaveAssets();Debug.Log("[Berserker] Installed character, 32 cards and five approved pose sprites.");
        }
        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a!=null)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
    }
}
#endif
