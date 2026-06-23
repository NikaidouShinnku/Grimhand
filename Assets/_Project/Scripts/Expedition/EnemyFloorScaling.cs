using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>敌人按远征层数缩放（v0.8：仅 HP 与卡牌固定数值，不再缩放 ATK/DEF）。</summary>
    public static class EnemyFloorScaling
    {
        public const float HpGrowthPerFloor = 0.05f;
        public const float CardValueGrowthPerFloor = 0.03f;

        public static void Apply(CombatantConfig combatant, int floor, BattleRng rng)
        {
            if (combatant == null || combatant.Team != TeamSide.Enemy || floor <= 1)
                return;

            var tiers = floor - 1;
            var hpMult = 1f + HpGrowthPerFloor * tiers;
            var cardMult = 1f + CardValueGrowthPerFloor * tiers;
            var variance = rng != null ? rng.NextInt(90, 111) / 100f : 1f;

            combatant.MaxHp = Scale(combatant.MaxHp, hpMult * variance);
            combatant.BaseAttack = 0;
            combatant.BaseDefense = 0;

            var scaledCardMult = cardMult * variance;
            ScaleTemplates(combatant.DeckTemplates, scaledCardMult);
            ScaleTemplates(combatant.SkillPoolCandidates, scaledCardMult);
        }

        static void ScaleTemplates(System.Collections.Generic.List<CardTemplate> templates, float multiplier)
        {
            if (templates == null)
                return;

            foreach (var template in templates)
                ScaleTemplate(template, multiplier);
        }

        static void ScaleTemplate(CardTemplate template, float multiplier)
        {
            if (template?.Actions == null)
                return;

            foreach (var action in template.Actions)
            {
                if (action.Type != EffectActionType.DealDamage && action.Type != EffectActionType.GainBlock)
                    continue;

                if (action.Value > 0)
                    action.Value = Scale(action.Value, multiplier);

                action.ScaleWithAttack = false;
                action.ScaleWithDefense = false;
            }
        }

        static int Scale(int baseValue, float multiplier)
        {
            if (baseValue <= 0)
                return baseValue;

            return System.Math.Max(1, (int)System.Math.Round(baseValue * multiplier, System.MidpointRounding.AwayFromZero));
        }
    }
}
