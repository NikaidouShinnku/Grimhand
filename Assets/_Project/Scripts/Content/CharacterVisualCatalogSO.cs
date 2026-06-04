using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    public enum PortraitPoseKind
    {
        Idle,
        Attack,
        Defense,
        Hit,
        Death
    }

    [Serializable]
    public sealed class CharacterVisualEntry
    {
        public string CharacterId = "";
        public Sprite IdlePortrait;
        public Sprite AttackPortrait;
        public Sprite DefensePortrait;
        public Sprite HitPortrait;
        [Tooltip("受击图原始朝向是否朝右；展示时会按阵营翻转，使角色始终面向战场中央。")]
        public bool HitPortraitFacesRight = true;
        public Sprite DeathPortrait;
        public List<Sprite> IdleAnimationFrames = new();
        [Tooltip("相对 Assets/ 的 idle GIF 路径；若填写则优先于 IdleAnimationFrames 播放。")]
        public string IdleAnimationGifPath = "";
    }

    [CreateAssetMenu(fileName = "CharacterVisualCatalog", menuName = "Grimhand/Character Visual Catalog")]
    public class CharacterVisualCatalogSO : ScriptableObject
    {
        public Sprite DefaultPortrait;
        public List<CharacterVisualEntry> Entries = new();

        public CharacterVisualEntry GetEntry(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return null;

            foreach (var entry in Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CharacterId))
                    continue;
                if (entry.CharacterId == characterDefinitionId)
                    return entry;
            }

            return null;
        }

        public Sprite GetPortrait(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            if (entry?.IdlePortrait != null)
                return entry.IdlePortrait;
            return DefaultPortrait;
        }

        public Sprite GetPoseSprite(string characterDefinitionId, PortraitPoseKind pose)
        {
            var entry = GetEntry(characterDefinitionId);
            if (entry == null)
                return DefaultPortrait;

            var sprite = pose switch
            {
                PortraitPoseKind.Attack => entry.AttackPortrait,
                PortraitPoseKind.Defense => entry.DefensePortrait,
                PortraitPoseKind.Hit => entry.HitPortrait,
                PortraitPoseKind.Death => entry.DeathPortrait,
                _ => entry.IdlePortrait
            };

            if (sprite != null)
                return sprite;

            return entry.IdlePortrait != null ? entry.IdlePortrait : DefaultPortrait;
        }

        public const float MinFrameWidth = 128f;
        public const float MinFrameHeight = 128f;

        public static bool IsValidAnimationFrame(Sprite sprite) =>
            sprite != null
            && sprite.rect.width >= MinFrameWidth
            && sprite.rect.height >= MinFrameHeight;

        public static List<Sprite> FilterAnimationFrames(IEnumerable<Sprite> frames)
        {
            var result = new List<Sprite>();
            if (frames == null)
                return result;

            foreach (var frame in frames)
            {
                if (IsValidAnimationFrame(frame))
                    result.Add(frame);
            }

            return result;
        }

        public bool GetHitPortraitFacesRight(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            return entry?.HitPortraitFacesRight ?? true;
        }

        public IReadOnlyList<Sprite> GetIdleAnimationFrames(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            if (entry == null)
                return Array.Empty<Sprite>();

            if (!string.IsNullOrEmpty(entry.IdleAnimationGifPath))
            {
                var ppu = entry.IdlePortrait != null ? entry.IdlePortrait.pixelsPerUnit : 100f;
                var gifFrames = IdleAnimationGifLoader.GetSprites(entry.IdleAnimationGifPath, ppu);
                if (gifFrames.Count > 1)
                    return gifFrames;
            }

            if (entry.IdleAnimationFrames == null || entry.IdleAnimationFrames.Count == 0)
                return Array.Empty<Sprite>();

            var filtered = FilterAnimationFrames(entry.IdleAnimationFrames);
            return filtered.Count > 0 ? filtered : Array.Empty<Sprite>();
        }
    }
}
