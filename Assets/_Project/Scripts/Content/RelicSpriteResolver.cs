using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>从多切片遗物贴图中选取主图标（避免误绑碎片 sprite）。</summary>
    public static class RelicSpriteResolver
    {
        public static Sprite PickBest(Object[] assets, string relicId)
        {
            if (assets == null || assets.Length == 0)
                return null;

            // 主图约定为 {id}_0；旧逻辑优先 _1，会误绑到色块碎片（如赤红烈焰靴）。
            var preferredName = string.IsNullOrEmpty(relicId) ? null : $"{relicId}_0";
            Sprite best = null;
            var bestArea = 0f;

            foreach (var asset in assets)
            {
                if (asset is not Sprite sprite)
                    continue;

                if (!string.IsNullOrEmpty(preferredName) && sprite.name == preferredName)
                    return sprite;

                var area = sprite.rect.width * sprite.rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sprite;
                }
            }

            return best;
        }
    }
}
