using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "BattleActionEffectCatalog", menuName = "Grimhand/Battle Action Effect Catalog")]
    public sealed class BattleActionEffectCatalogSO : ScriptableObject
    {
        public Sprite WarriorDamage;
        public Sprite PharaohDamage;
        public Sprite DevilDamage;
        public Sprite SnakeQueenDamage;
        public Sprite LichQueenDamage;
        public Sprite SkeletonKingDamage;
        public Sprite GhostQueenDamage;
        public Sprite WardenDamage;
        public Sprite DarkKnightDamage;
        public Sprite CorruptedOceanGoddessDamage;
        public Sprite Blocking;
        public Sprite Healing;
        public Sprite Poisoning;
        public Sprite Burning;
        public Sprite SacrificeBurst;
    }
}
