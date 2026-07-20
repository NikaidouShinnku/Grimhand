using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Content
{
    /// <summary>
    /// 卡面半身图：Assets/The Grimhands Asset/card/card_profile/card_profile_*.png
    /// 按角色定义 Id 映射；卡牌运行时通过 OwnerCharacterId → GetCardPortrait 取图。
    /// </summary>
    public static class CardProfileArt
    {
        public const string ProfileFolder = "Assets/The Grimhands Asset/card/card_profile";

        static readonly Dictionary<string, string> CharacterIdToFileStem = new()
        {
            ["char_knight"] = "card_profile_warrior",
            ["char_mage"] = "card_profile_pharoah",
            ["char_ranger"] = "card_profile_devil",
            ["char_snake_queen"] = "card_profile_snakequeen",
            ["char_lich_queen"] = "card_profile_lichqueen",
            ["char_goblin"] = "card_profile_goblin",
            ["char_slime"] = "card_profile_slime",
            ["char_skeleton"] = "card_profile_skeleton",
            ["char_skeleton_elite"] = "card_profile_skeleton2",
            ["char_wraith"] = "card_profile_wraith",
            ["char_wraith_elite"] = "card_profile_wraith2",
            ["char_ogre"] = "card_profile_green_ogre",
            ["char_bat"] = "card_profile_bat_girl",
            ["char_skeleton_king"] = "card_profile_skeletonking",
            ["char_explosive_skull"] = "card_profile_skeletonhead",
            ["char_ghost_queen"] = "card_profile_ghostqueen",
            ["char_rat"] = "card_profile_boxer_rat",
            ["char_chain_wraith"] = "card_profile_chained_wraith",
            ["char_gargoyle"] = "card_profile_gargoyle",
            ["char_spider_lady"] = "card_profile_spider_girl",
            ["char_stone_golem"] = "card_profile_stone_golem",
            ["char_seahorse_guard"] = "card_profile_seahorse_guard",
            ["char_jellyfish_caster"] = "card_profile_jellyfish_caster",
            ["char_mermaid_warrior"] = "card_profile_mermaid_warrior",
            ["char_abyss_creature"] = "card_profile_abyss_creature",
            ["char_corrupted_crab"] = "card_profile_corrupted_crab",
            ["char_phantom_captain"] = "card_profile_phantom_captain",
            ["char_warden"] = "card_profile_warden",
            ["char_prison_cage"] = "card_profile_prisoner_cage",
            ["char_dark_knight"] = "card_profile_darkknight",
            ["char_corrupted_ocean_goddess"] = "card_profile_corrupted_oceangoddess",
            ["char_dummy"] = "card_profile_dummy",
        };

        public static string GetAssetPath(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return null;
            if (!CharacterIdToFileStem.TryGetValue(characterDefinitionId, out var stem))
                return null;
            return $"{ProfileFolder}/{stem}.png";
        }

#if UNITY_EDITOR
        public static Sprite LoadSprite(string characterDefinitionId)
        {
            var path = GetAssetPath(characterDefinitionId);
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void BindAllProfiles(CharacterVisualCatalogSO catalog)
        {
            if (catalog == null)
                return;

            catalog.MonsterCardProfilePortrait = null;
            foreach (var pair in CharacterIdToFileStem)
            {
                var sprite = LoadSprite(pair.Key);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Grimhand] 缺少卡面：{pair.Key} → {GetAssetPath(pair.Key)}");
                    continue;
                }

                CharacterVisualEntry entry = null;
                foreach (var e in catalog.Entries)
                {
                    if (e != null && e.CharacterId == pair.Key)
                    {
                        entry = e;
                        break;
                    }
                }

                if (entry == null)
                {
                    entry = new CharacterVisualEntry { CharacterId = pair.Key };
                    catalog.Entries.Add(entry);
                }

                entry.CardProfilePortrait = sprite;
            }
        }
#endif
    }
}
