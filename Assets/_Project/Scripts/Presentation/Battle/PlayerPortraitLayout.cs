using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>One player ground line and idle body height, regardless of sprite canvas/padding.</summary>
    public static class PlayerPortraitLayout
    {
        public static bool TryGetGeometry(RectTransform root,CharacterVisualCatalogSO visuals,out float ground,out float bodyHeight)
        {
            ground=bodyHeight=0;
            if(root==null||visuals==null||root.rect.width<=0||root.rect.height<=0)return false;
            var frames=visuals.GetIdleAnimationFrames("char_knight");
            var reference=frames.Count>0?frames[0]:visuals.GetPortrait("char_knight");
            if(reference==null)return false;
            var bounds=IdleAnimationFrameNormalizer.GetVisibleBounds(reference);
            var scale=Mathf.Min(root.rect.width/reference.rect.width,root.rect.height/reference.rect.height);
            ground=(root.rect.height-reference.rect.height*scale)*.5f+bounds.yMin*scale;
            bodyHeight=bounds.height*scale;
            return bodyHeight>0;
        }

        public static void Apply(RectTransform root,Image image,CharacterVisualCatalogSO visuals,string characterId,Sprite sprite)
        {
            if(image==null||sprite==null||!TryGetGeometry(root,visuals,out var ground,out var bodyHeight))return;
            var reference=visuals.GetPortrait(characterId);
            var frames=visuals.GetIdleAnimationFrames(characterId);
            // Actions keep their scale relative to the static idle; GIF frames use
            // their own pixel grid. Do not shrink a raised axe or a falling body.
            foreach(var frame in frames)if(frame==sprite){reference=frames[0];break;}
            if(reference==null)return;
            var referenceBounds=IdleAnimationFrameNormalizer.GetVisibleBounds(reference);
            if(referenceBounds.height<=0)return;
            var scale=bodyHeight/referenceBounds.height;
            var visible=IdleAnimationFrameNormalizer.GetVisibleBounds(sprite);
            var rt=image.rectTransform;
            image.preserveAspect=false;
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);
            rt.pivot=new Vector2(.5f,0);
            rt.sizeDelta=new Vector2(sprite.rect.width*scale,sprite.rect.height*scale);
            rt.anchoredPosition=new Vector2(0,ground-visible.yMin*scale);
        }

        public static Vector3 GroundWorldPosition(RectTransform root,CharacterVisualCatalogSO visuals)
        {
            if(!TryGetGeometry(root,visuals,out var ground,out _))return root.position;
            return root.TransformPoint(new Vector3(root.rect.center.x,root.rect.yMin+ground,0));
        }

        public static Vector3 VisibleSpriteFootWorldPosition(Image image)
        {
            if(image==null||image.sprite==null)return image!=null?image.transform.position:Vector3.zero;
            var sprite=image.sprite;var rt=image.rectTransform;var rect=rt.rect;
            var bounds=IdleAnimationFrameNormalizer.GetVisibleBounds(sprite);
            var scaleX=rect.width/sprite.rect.width;var scaleY=rect.height/sprite.rect.height;
            if(image.preserveAspect)scaleX=scaleY=Mathf.Min(scaleX,scaleY);
            var bottom=rect.center.y-sprite.rect.height*scaleY*.5f+(bounds.yMin+sprite.textureRectOffset.y)*scaleY;
            return rt.TransformPoint(new Vector3(rect.center.x,bottom,0));
        }
    }
}
