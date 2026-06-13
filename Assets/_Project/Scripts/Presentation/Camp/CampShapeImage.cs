using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>按 Sprite 不透明像素判定点击（贴图需 Read/Write Enabled，否则退化为矩形点击）。</summary>
    public sealed class CampShapeImage : Image
    {
        const float ShapeHitThreshold = 0.12f;

        /// <summary>在 sprite 赋值后调用；不可读贴图则跳过，避免 Console 警告/异常。</summary>
        public void ApplyShapeHitTestIfSupported()
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable)
                return;

            alphaHitTestMinimumThreshold = ShapeHitThreshold;
        }
    }
}
