using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>从多切片遗物贴图中选取主图标（避开自动切片产生的色块碎片）。</summary>
    public static class RelicSpriteResolver
    {
        const float MinMainEdge = 32f;

        public static Sprite PickBest(Object[] assets, string relicId)
        {
            if (assets == null || assets.Length == 0)
                return null;

            // 主图约定为 {id}_0；但若 _0 是碎片（如旧 burning_boots），改选最大有效切片。
            var preferredName = string.IsNullOrEmpty(relicId) ? null : $"{relicId}_0";
            Sprite preferred = null;
            Sprite best = null;
            var bestArea = 0f;

            foreach (var asset in assets)
            {
                if (asset is not Sprite sprite)
                    continue;

                var w = sprite.rect.width;
                var h = sprite.rect.height;
                if (w < MinMainEdge || h < MinMainEdge)
                    continue;

                var area = w * h;
                if (!string.IsNullOrEmpty(preferredName) && sprite.name == preferredName)
                    preferred = sprite;

                if (area > bestArea)
                {
                    bestArea = area;
                    best = sprite;
                }
            }

            if (preferred != null)
            {
                var preferredArea = preferred.rect.width * preferred.rect.height;
                // _0 足够大才采用，否则用最大切片（与其他遗物一致）
                if (preferredArea >= bestArea * 0.5f)
                    return preferred;
            }

            return best ?? preferred;
        }
    }
}
