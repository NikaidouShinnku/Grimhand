using Grimhand.Content;
using Grimhand.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Battle.Tests
{
    public class PlayerPortraitLayoutTests
    {
        [TestCase("char_knight")][TestCase("char_mage")][TestCase("char_ranger")]
        [TestCase("char_snake_queen")][TestCase("char_lich_queen")][TestCase("char_berserker")]
        public void ActualPlayerIdleAssetsUseTheSameGroundAndBodyHeight(string id)
        {
            var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>("Assets/_Project/Data/CharacterVisualCatalog_Demo.asset");
            var frames=catalog.GetIdleAnimationFrames(id);Assert.IsNotEmpty(frames);
            var root=new GameObject("Root",typeof(RectTransform)).GetComponent<RectTransform>();root.sizeDelta=new Vector2(200,300);root.pivot=new Vector2(.5f,0);
            var image=new GameObject("Portrait",typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(root,false);
            try
            {
                Assert.IsTrue(PlayerPortraitLayout.TryGetGeometry(root,catalog,out var ground,out var height));
                image.sprite=frames[0];PlayerPortraitLayout.Apply(root,image,catalog,id,frames[0]);
                var bounds=IdleAnimationFrameNormalizer.GetVisibleBounds(frames[0]);
                var scale=image.rectTransform.sizeDelta.y/frames[0].rect.height;
                Assert.AreEqual(ground,image.rectTransform.anchoredPosition.y+bounds.yMin*scale,.001f,id);
                Assert.AreEqual(height,bounds.height*scale,.001f,id);
            }
            finally{Object.DestroyImmediate(root.gameObject);}
        }
        static Sprite MakeSprite(int width,int height,RectInt body)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var pixels=new Color[width*height];
            for(var y=body.yMin;y<body.yMax;y++)for(var x=body.xMin;x<body.xMax;x++)pixels[y*width+x]=Color.white;
            texture.SetPixels(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f));
        }
        [Test] public void TransparentPaddingDoesNotChangePlayerFeetOrIdleHeight()
        {
            var knight=MakeSprite(128,128,new RectInt(30,12,60,104));
            var berserker=MakeSprite(256,256,new RectInt(50,2,140,230));
            var catalog=ScriptableObject.CreateInstance<CharacterVisualCatalogSO>();
            catalog.Entries.Add(new CharacterVisualEntry{CharacterId="char_knight",IdlePortrait=knight});
            catalog.Entries.Add(new CharacterVisualEntry{CharacterId="char_berserker",IdlePortrait=berserker});
            var root=new GameObject("Root",typeof(RectTransform)).GetComponent<RectTransform>();root.sizeDelta=new Vector2(200,300);root.pivot=new Vector2(.5f,0);
            var image=new GameObject("Portrait",typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(root,false);
            try
            {
                Assert.IsTrue(PlayerPortraitLayout.TryGetGeometry(root,catalog,out var ground,out var height));
                foreach(var pair in new[]{("char_knight",knight),("char_berserker",berserker)})
                {
                    image.sprite=pair.Item2;PlayerPortraitLayout.Apply(root,image,catalog,pair.Item1,pair.Item2);
                    var bounds=IdleAnimationFrameNormalizer.GetVisibleBounds(pair.Item2);
                    var pixelScale=image.rectTransform.sizeDelta.y/pair.Item2.rect.height;
                    Assert.AreEqual(ground,image.rectTransform.anchoredPosition.y+bounds.yMin*pixelScale,.001f);
                    Assert.AreEqual(height,bounds.height*pixelScale,.001f);
                    Assert.IsFalse(image.preserveAspect);
                }
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(knight.texture);Object.DestroyImmediate(knight);
                Object.DestroyImmediate(berserker.texture);Object.DestroyImmediate(berserker);
            }
        }
    }
}
