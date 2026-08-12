using System;
using System.Collections.Generic;
using System.IO;
using MG.GIF;
using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>
    /// 从 .gif 文件解码逐帧 Sprite，绕过 PNG spritesheet 的自动裁剪。
    /// 正式包从 StreamingAssets 读取（构建时由 IdleGifStreamingAssetsCopy 拷贝）。
    /// </summary>
    public static class IdleAnimationGifLoader
    {
        /// <summary>防止损坏/异常 GIF 解码成无限帧导致内存爆炸。</summary>
        const int MaxFrames = 48;
        const int MaxGifBytes = 8 * 1024 * 1024;

        static readonly Dictionary<string, List<Sprite>> Cache = new();

        public static IReadOnlyList<Sprite> GetSprites(string relativeToAssetsFolder, float pixelsPerUnit)
        {
            if (string.IsNullOrEmpty(relativeToAssetsFolder))
                return Array.Empty<Sprite>();

            var cacheKey = $"{relativeToAssetsFolder}:{pixelsPerUnit:F2}";
            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!TryReadGifBytes(relativeToAssetsFolder, out var bytes, out var resolvedPath))
            {
                Debug.LogWarning($"[Grimhand] 未找到 idle GIF：{relativeToAssetsFolder}");
                Cache[cacheKey] = new List<Sprite>();
                return Cache[cacheKey];
            }

            List<Sprite> sprites;
            try
            {
                sprites = DecodeGif(bytes, pixelsPerUnit);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Grimhand] idle GIF 解码异常：{resolvedPath}\n{ex.Message}");
                sprites = new List<Sprite>();
            }

            if (sprites.Count == 0)
                Debug.LogWarning($"[Grimhand] idle GIF 解码为空：{resolvedPath}");

            Cache[cacheKey] = sprites;
            return sprites;
        }

        public static IReadOnlyList<Sprite> GetSpritesFromBytes(byte[] bytes, float pixelsPerUnit, string cacheKey = null)
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<Sprite>();

            var key = string.IsNullOrEmpty(cacheKey)
                ? $"bytes:{bytes.Length}:{pixelsPerUnit:F2}"
                : $"{cacheKey}:{pixelsPerUnit:F2}";
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            List<Sprite> sprites;
            try
            {
                sprites = DecodeGif(bytes, pixelsPerUnit);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Grimhand] idle GIF bytes 解码异常：{ex.Message}");
                sprites = new List<Sprite>();
            }

            Cache[key] = sprites;
            return sprites;
        }

        static bool TryReadGifBytes(string relativeToAssetsFolder, out byte[] bytes, out string resolvedPath)
        {
            bytes = null;
            resolvedPath = "";
            var relative = relativeToAssetsFolder.Replace('\\', '/').TrimStart('/');

            foreach (var fullPath in EnumerateCandidatePaths(relative))
            {
                if (!File.Exists(fullPath))
                    continue;

                try
                {
                    var info = new FileInfo(fullPath);
                    if (info.Length <= 0 || info.Length > MaxGifBytes)
                    {
                        Debug.LogWarning($"[Grimhand] idle GIF 大小异常，已跳过：{fullPath} ({info.Length} bytes)");
                        continue;
                    }

                    bytes = File.ReadAllBytes(fullPath);
                    resolvedPath = fullPath;
                    return bytes != null && bytes.Length > 0;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Grimhand] 读取 idle GIF 失败：{fullPath}\n{ex.Message}");
                }
            }

            return false;
        }

        static IEnumerable<string> EnumerateCandidatePaths(string relative)
        {
            var normalized = relative.Replace('/', Path.DirectorySeparatorChar);

            // 编辑器：Assets/...
            yield return Path.Combine(Application.dataPath, normalized);

            // 正式包：StreamingAssets/...（构建时拷贝）
            yield return Path.Combine(Application.streamingAssetsPath, normalized);

            // 兜底：部分平台 StreamingAssets 下再套一层 Assets
            yield return Path.Combine(Application.streamingAssetsPath, "Assets", normalized);
        }

        static List<Sprite> DecodeGif(byte[] bytes, float pixelsPerUnit)
        {
            var sprites = new List<Sprite>();
            using var decoder = new Decoder(bytes);
            var img = decoder.NextImage();
            var frames = 0;
            while (img != null && frames < MaxFrames)
            {
                var texture = img.CreateTexture();
                if (texture == null)
                    break;

                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprites.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect));
                frames++;
                img = decoder.NextImage();
            }

            if (img != null && frames >= MaxFrames)
                Debug.LogWarning($"[Grimhand] idle GIF 超过 {MaxFrames} 帧，已截断以防内存爆炸");

            return sprites;
        }
    }
}
