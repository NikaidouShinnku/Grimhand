using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>敌人按远征层数缩放：伤害每5层+1，护甲每5层+1，HP每层+1.5。</summary>
    public static class EnemyFloorScaling
    {
        public const float HpBonusPerFloor = 1.5f;
        public const int DamageBonusEveryFloors = 5;
        public const int BlockBonusEveryFloors = 5;

        public static void Apply(CombatantConfig combatant, int floor, BattleRng rng)
        {
            if (combatant == null || combatant.Team != TeamSide.Enemy || floor <= 1)
                return;

            var damageBonus = floor / DamageBonusEveryFloors;
            var blockBonus = floor / BlockBonusEveryFloors;
            var hpBonus = (int)System.Math.Round(HpBonusPerFloor * (floor - 1));

            combatant.MaxHp = System.Math.Max(1, combatant.MaxHp + hpBonus);
            combatant.BaseAttack = 0;
            combatant.BaseDefense = 0;

            AdditiveScaleTemplates(combatant.DeckTemplates, damageBonus, blockBonus);
            AdditiveScaleTemplates(combatant.SkillPoolCandidates, damageBonus, blockBonus);
        }

        static void AdditiveScaleTemplates(
            System.Collections.Generic.List<CardTemplate> templates,
            int damageBonus,
            int blockBonus)
        {
            if (templates == null)
                return;

            foreach (var template in templates)
                AdditiveScaleTemplate(template, damageBonus, blockBonus);
        }

        static void AdditiveScaleTemplate(CardTemplate template, int damageBonus, int blockBonus)
        {
            if (template?.Actions == null)
                return;

            foreach (var action in template.Actions)
            {
                action.ScaleWithAttack = false;
                action.ScaleWithDefense = false;

                if (action.Type == EffectActionType.DealDamage && action.Value > 0 && damageBonus > 0)
                    action.Value += damageBonus;

                if (action.Type == EffectActionType.GainBlock && action.Value > 0 && blockBonus > 0)
                    action.Value += blockBonus;
            }
        }
    }
}
