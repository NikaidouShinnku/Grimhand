using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>按 Sprite 不透明像素判定点击（需贴图 Read/Write Enabled）。</summary>
    public sealed class CampShapeImage : Image
    {
        protected override void Awake()
        {
            base.Awake();
            alphaHitTestMinimumThreshold = 0.12f;
        }
    }
}
