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
        [Tooltip("卡面预绘半身（card/card_profile/card_profile_*.png），直接覆盖椭圆区。")]
        public Sprite CardProfilePortrait;
        public List<Sprite> IdleAnimationFrames = new();
        [Tooltip("相对 Assets/ 的 idle GIF 路径；若填写则优先于 IdleAnimationFrames 播放。")]
        public string IdleAnimationGifPath = "";
        [Tooltip("为 true 时不镜像立绘，受击 pose 也不自动翻转。玩家朝右、敌人朝左的原画应始终为 true。")]
        public bool PreserveOriginalFacing = true;
        [Tooltip("相对 Boss/敌人默认缩放的额外倍率；用于立绘留白较多的 Boss。")]
        public float PortraitScaleMultiplier = 1f;
    }

    [CreateAssetMenu(fileName = "CharacterVisualCatalog", menuName = "Grimhand/Character Visual Catalog")]
    public class CharacterVisualCatalogSO : ScriptableObject
    {
        public Sprite DefaultPortrait;
        [Tooltip("已废弃：各角色请使用 Entries[].CardProfilePortrait（card/card_profile/）。")]
        public Sprite MonsterCardProfilePortrait;
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
            var frames = GetIdleAnimationFrames(characterDefinitionId);
            if (frames.Count > 0)
                return frames[0];

            var entry = GetEntry(characterDefinitionId);
            if (entry?.IdlePortrait != null)
                return entry.IdlePortrait;
            return DefaultPortrait;
        }

        /// <summary>卡面立绘：优先角色专属 card_profile；否则 Idle / Default。</summary>
        public Sprite GetCardPortrait(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            if (entry?.CardProfilePortrait != null)
                return entry.CardProfilePortrait;

            if (MonsterCardProfilePortrait != null && !BossCharacterRules.IsBoss(characterDefinitionId))
                return MonsterCardProfilePortrait;

            if (entry?.IdlePortrait != null)
                return entry.IdlePortrait;

            return GetPortrait(characterDefinitionId);
        }

        public Sprite GetPortraitReference(string characterDefinitionId)
        {
            var frames = GetIdleAnimationFrames(characterDefinitionId);
            if (frames.Count > 0)
                return frames[0];

            return GetPortrait(characterDefinitionId);
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

            // 战斗立绘：128×128 完整图可用；过小碎切片才回退 idle
            if (sprite != null && !IsValidCombatPortrait(sprite))
                sprite = entry.IdlePortrait;

            if (sprite != null && entry.IdlePortrait != null && pose != PortraitPoseKind.Idle)
            {
                var poseArea = sprite.rect.width * sprite.rect.height;
                var idleArea = entry.IdlePortrait.rect.width * entry.IdlePortrait.rect.height;
                if (idleArea > 0f && poseArea < idleArea * 0.45f)
                    sprite = entry.IdlePortrait;
            }

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

        /// <summary>战斗立绘：允许正好 128×128 的完整贴图；过小裁切片仍拒绝以免误用碎图。</summary>
        public static bool IsValidCombatPortrait(Sprite sprite) =>
            sprite != null
            && sprite.rect.width >= 64f
            && sprite.rect.height >= 64f;

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

        public bool GetPreserveOriginalFacing(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            return entry != null && entry.PreserveOriginalFacing;
        }

        public float GetPortraitScaleMultiplier(string characterDefinitionId)
        {
            var entry = GetEntry(characterDefinitionId);
            if (entry == null || entry.PortraitScaleMultiplier <= 0f)
                return 1f;

            return entry.PortraitScaleMultiplier;
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
