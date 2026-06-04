using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>
    /// 将 idle 各帧烘焙到统一紧凑画布：脚底区域 1:1 对齐，无缩放插值。
    /// </summary>
    public static class IdleAnimationFrameNormalizer
    {
        const float AlphaThreshold = 0.08f;
        const float DefaultFeetBandFraction = 0.18f;
        const float KnightFeetBandFraction = 0.10f;
        const float KnightFeetInnerMargin = 0.28f;
        const int CanvasPadding = 4;
        const string CacheVersion = "copy-v8";
        const string KnightCharacterId = "char_knight";

        static readonly Dictionary<string, List<Sprite>> SequenceCache = new();

        public static IReadOnlyList<Sprite> GetNormalizedFrames(
            IReadOnlyList<Sprite> sourceFrames,
            Sprite layoutReference = null,
            string characterDefinitionId = null)
        {
            if (sourceFrames == null || sourceFrames.Count == 0)
                return Array.Empty<Sprite>();

            var cacheKey = BuildCacheKey(sourceFrames, layoutReference, characterDefinitionId);
            if (SequenceCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var normalized = Normalize(sourceFrames, layoutReference, characterDefinitionId);
            SequenceCache[cacheKey] = normalized;
            return normalized;
        }

        static string BuildCacheKey(IReadOnlyList<Sprite> frames, Sprite layoutReference, string characterDefinitionId)
        {
            var layout = layoutReference != null
                ? $"{layoutReference.texture.name}:{layoutReference.rect.width}x{layoutReference.rect.height}"
                : "none";
            return $"{CacheVersion}:{characterDefinitionId}:{frames[0].texture.name}:{frames.Count}:{layout}";
        }

        static float GetFeetBandFraction(string characterDefinitionId) =>
            characterDefinitionId == KnightCharacterId ? KnightFeetBandFraction : DefaultFeetBandFraction;

        static List<Sprite> Normalize(
            IReadOnlyList<Sprite> frames,
            Sprite layoutReference,
            string characterDefinitionId)
        {
            var feetBand = GetFeetBandFraction(characterDefinitionId);
            var useKnightFeet = characterDefinitionId == KnightCharacterId;
            var placements = new List<FramePlacement>();

            foreach (var frame in frames)
            {
                if (!CharacterVisualCatalogSO.IsValidAnimationFrame(frame))
                    continue;

                var data = ReadFrameData(frame, feetBand, useKnightFeet);
                if (data == null)
                    continue;

                placements.Add(new FramePlacement
                {
                    Data = data,
                    PasteX = Mathf.RoundToInt(-data.FeetCenterX),
                    PasteY = -data.AnchorBottomY
                });
            }

            if (placements.Count == 0)
                return new List<Sprite>();

            var boundsLeft = int.MaxValue;
            var boundsRight = int.MinValue;
            var boundsTop = 0;

            foreach (var placement in placements)
            {
                boundsLeft = Mathf.Min(boundsLeft, placement.PasteX);
                boundsRight = Mathf.Max(boundsRight, placement.PasteX + placement.Data.Width);
                boundsTop = Mathf.Max(boundsTop, placement.PasteY + placement.Data.Height);
            }

            var alignedWidth = boundsRight - boundsLeft;
            var shiftX = alignedWidth / 2;

            var tightMinX = int.MaxValue;
            var tightMaxX = int.MinValue;
            var tightMinY = int.MaxValue;
            var tightMaxY = int.MinValue;

            foreach (var placement in placements)
            {
                AccumulateTightBounds(placement, boundsLeft, shiftX, ref tightMinX, ref tightMaxX, ref tightMinY, ref tightMaxY);
            }

            if (tightMaxX < tightMinX)
                return new List<Sprite>();

            var canvasWidth = tightMaxX - tightMinX + 1 + CanvasPadding * 2;
            var canvasHeight = tightMaxY - tightMinY + 1 + CanvasPadding * 2;
            var ppu = layoutReference != null
                ? layoutReference.pixelsPerUnit
                : frames[0].pixelsPerUnit;

            var result = new List<Sprite>(placements.Count);
            foreach (var placement in placements)
            {
                var offsetX = placement.PasteX - boundsLeft + shiftX - tightMinX + CanvasPadding;
                var offsetY = placement.PasteY - tightMinY + CanvasPadding;
                var texture = CreateFrameTexture(placement.Data, canvasWidth, canvasHeight, offsetX, offsetY);
                result.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, canvasWidth, canvasHeight),
                    new Vector2(0.5f, 0.5f),
                    ppu,
                    0,
                    SpriteMeshType.FullRect));
            }

            return result;
        }

        static void AccumulateTightBounds(
            FramePlacement placement,
            int boundsLeft,
            int shiftX,
            ref int tightMinX,
            ref int tightMaxX,
            ref int tightMinY,
            ref int tightMaxY)
        {
            var baseX = placement.PasteX - boundsLeft + shiftX;
            var baseY = placement.PasteY;
            var data = placement.Data;

            for (var y = 0; y < data.Height; y++)
            {
                for (var x = 0; x < data.Width; x++)
                {
                    if (data.Pixels[y * data.Width + x].a < AlphaThreshold)
                        continue;

                    var px = baseX + x;
                    var py = baseY + y;
                    if (px < tightMinX)
                        tightMinX = px;
                    if (px > tightMaxX)
                        tightMaxX = px;
                    if (py < tightMinY)
                        tightMinY = py;
                    if (py > tightMaxY)
                        tightMaxY = py;
                }
            }
        }

        static Texture2D CreateFrameTexture(FrameData data, int canvasWidth, int canvasHeight, int offsetX, int offsetY)
        {
            var buffer = new Color[canvasWidth * canvasHeight];
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = Color.clear;

            for (var y = 0; y < data.Height; y++)
            {
                var destY = offsetY + y;
                if (destY < 0 || destY >= canvasHeight)
                    continue;

                for (var x = 0; x < data.Width; x++)
                {
                    var color = data.Pixels[y * data.Width + x];
                    if (color.a < AlphaThreshold)
                        continue;

                    var destX = offsetX + x;
                    if (destX < 0 || destX >= canvasWidth)
                        continue;

                    buffer[destY * canvasWidth + destX] = color;
                }
            }

            var texture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(buffer);
            texture.Apply();
            return texture;
        }

        sealed class FramePlacement
        {
            public FrameData Data;
            public int PasteX;
            public int PasteY;
        }

        sealed class FrameData
        {
            public int Width;
            public int Height;
            public int AnchorBottomY;
            public float FeetCenterX;
            public Color[] Pixels;
        }

        static FrameData ReadFrameData(Sprite sprite, float feetBandFraction, bool knightFeet)
        {
            var rect = sprite.textureRect;
            var width = (int)rect.width;
            var height = (int)rect.height;
            if (width <= 0 || height <= 0)
                return null;

            var pixels = ReadTextureRegion(sprite.texture, rect);
            if (pixels == null || pixels.Length == 0)
                return null;

            var minY = height;
            var feetMinX = width;
            var feetMaxX = -1;
            var feetBottomY = height;
            var feetYStart = Mathf.Clamp((int)(height * (1f - feetBandFraction)), 0, height - 1);
            double feetSumX = 0d;
            double feetSumAlpha = 0d;

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var alpha = pixels[row + x].a;
                    if (alpha < AlphaThreshold)
                        continue;

                    if (y < minY)
                        minY = y;

                    if (y < feetYStart)
                        continue;

                    if (x < feetMinX)
                        feetMinX = x;
                    if (x > feetMaxX)
                        feetMaxX = x;
                    if (y < feetBottomY)
                        feetBottomY = y;

                    feetSumX += x * alpha;
                    feetSumAlpha += alpha;
                }
            }

            if (feetMaxX < 0)
                return null;

            var feetCenterX = feetSumAlpha > 0d
                ? (float)(feetSumX / feetSumAlpha)
                : (feetMinX + feetMaxX) * 0.5f;

            if (knightFeet)
            {
                var feetWidth = feetMaxX - feetMinX + 1;
                var innerLeft = feetMinX + feetWidth * KnightFeetInnerMargin;
                var innerRight = feetMaxX - feetWidth * KnightFeetInnerMargin;
                feetSumX = 0d;
                feetSumAlpha = 0d;

                for (var y = feetYStart; y < height; y++)
                {
                    var row = y * width;
                    for (var x = 0; x < width; x++)
                    {
                        if (x < innerLeft || x > innerRight)
                            continue;

                        var alpha = pixels[row + x].a;
                        if (alpha < AlphaThreshold)
                            continue;

                        feetSumX += x * alpha;
                        feetSumAlpha += alpha;
                    }
                }

                if (feetSumAlpha > 0d)
                    feetCenterX = (float)(feetSumX / feetSumAlpha);
            }

            return new FrameData
            {
                Width = width,
                Height = height,
                AnchorBottomY = knightFeet ? feetBottomY : minY,
                FeetCenterX = feetCenterX,
                Pixels = pixels
            };
        }

        static Color[] ReadTextureRegion(Texture source, Rect rect)
        {
            if (source == null)
                return null;

            var width = (int)rect.width;
            var height = (int)rect.height;
            var full = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, full);

            var previous = RenderTexture.active;
            RenderTexture.active = full;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            readable.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(full);

            var pixels = readable.GetPixels();
            UnityEngine.Object.Destroy(readable);
            return pixels;
        }
    }
}
