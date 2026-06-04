using System;
using System.Collections.Generic;
using System.IO;
using MG.GIF;
using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>
    /// 从 .gif 文件解码逐帧 Sprite，绕过 PNG spritesheet 的自动裁剪。
    /// </summary>
    public static class IdleAnimationGifLoader
    {
        static readonly Dictionary<string, List<Sprite>> Cache = new();

        public static IReadOnlyList<Sprite> GetSprites(string relativeToAssetsFolder, float pixelsPerUnit)
        {
            if (string.IsNullOrEmpty(relativeToAssetsFolder))
                return Array.Empty<Sprite>();

            var cacheKey = $"{relativeToAssetsFolder}:{pixelsPerUnit:F2}";
            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var fullPath = Path.Combine(Application.dataPath, relativeToAssetsFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[Grimhand] 未找到 idle GIF：{fullPath}");
                return Array.Empty<Sprite>();
            }

            var sprites = DecodeGif(File.ReadAllBytes(fullPath), pixelsPerUnit);
            Cache[cacheKey] = sprites;
            return sprites;
        }

        static List<Sprite> DecodeGif(byte[] bytes, float pixelsPerUnit)
        {
            var sprites = new List<Sprite>();
            using var decoder = new Decoder(bytes);
            var img = decoder.NextImage();
            while (img != null)
            {
                var texture = img.CreateTexture();
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprites.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect));
                img = decoder.NextImage();
            }

            return sprites;
        }
    }
}
