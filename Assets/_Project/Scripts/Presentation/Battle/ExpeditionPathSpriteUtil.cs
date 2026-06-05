using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class ExpeditionPathSpriteUtil
    {
        public const string UnknownPathFullSpriteName = "unknown_path_1";
        const float MinFullPathWidth = 200f;

        public static bool IsFullPathSprite(Sprite sprite) =>
            sprite != null && sprite.rect.width >= MinFullPathWidth;
    }
}
