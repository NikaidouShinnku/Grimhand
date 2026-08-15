using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class BattleActionEffectResolver
    {
        public static Sprite ResolveDamageEffect(BattleActionEffectCatalogSO catalog, string characterDefinitionId)
        {
            if (catalog == null || string.IsNullOrEmpty(characterDefinitionId))
                return null;

            return characterDefinitionId switch
            {
                "char_knight" or "char_warrior" => catalog.WarriorDamage,
                "char_mage" or "char_pharaoh" => catalog.PharaohDamage,
                "char_ranger" or "char_demon" => catalog.DevilDamage,
                "char_snake_queen" or "char_viper_queen" => catalog.SnakeQueenDamage,
                "char_lich_queen" or "char_lich" => catalog.LichQueenDamage,
                BossCharacterRules.SkeletonKing => catalog.SkeletonKingDamage,
                BossCharacterRules.GhostQueen => catalog.GhostQueenDamage,
                BossCharacterRules.Warden => catalog.WardenDamage,
                BossCharacterRules.DarkKnight => catalog.DarkKnightDamage,
                BossCharacterRules.CorruptedOceanGoddess => catalog.CorruptedOceanGoddessDamage,
                _ => null
            };
        }

        public static Sprite ResolvePlayerDamage(BattleActionEffectCatalogSO catalog, string characterDefinitionId) =>
            ResolveDamageEffect(catalog, characterDefinitionId);

        public static Sprite ResolveStatus(BattleActionEffectCatalogSO catalog, string statusId)
        {
            if (catalog == null || string.IsNullOrEmpty(statusId))
                return null;

            return statusId switch
            {
                StatusCatalog.Poison or StatusCatalog.NecroticPoison => catalog.Poisoning,
                StatusCatalog.Burn => catalog.Burning,
                _ => null
            };
        }
    }
}
