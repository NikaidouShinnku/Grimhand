using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "BattleUiIconCatalog", menuName = "Grimhand/Battle UI Icon Catalog")]
    public class BattleUiIconCatalogSO : ScriptableObject
    {
        public Sprite HpIcon;
        public Sprite AttackIcon;
        public Sprite DefenseIcon;
        public Sprite SpeedIcon;
        public Sprite EnergyIcon;
        public Sprite ConfirmPlayIcon;
        public Sprite SkipIcon;
    }
}
