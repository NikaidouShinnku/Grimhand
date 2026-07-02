using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class StatusIconSpriteResolver
    {
        public static Sprite Resolve(BattleUiIconCatalogSO icons, string statusId)
        {
            if (icons == null || string.IsNullOrEmpty(statusId))
                return null;

            switch (statusId)
            {
                case StatusCatalog.AttackUpPercent:
                case StatusCatalog.AttackUp:
                case StatusCatalog.DamageUp:
                case StatusCatalog.BloodFrenzy:
                case StatusCatalog.ImmortalShed:
                case StatusCatalog.HandCostZero:
                case StatusCatalog.BattleWill:
                case StatusCatalog.SandSpearReforge:
                case StatusCatalog.GodDescends:
                case StatusCatalog.FinalBloodRitual:
                case StatusCatalog.GhostQueenWrath:
                    return icons.StatusDamageUp;

                case StatusCatalog.Weaken:
                case StatusCatalog.AttackDown:
                case StatusCatalog.Constrict:
                case StatusCatalog.DelayedDamage:
                    return icons.StatusDamageDown;

                case StatusCatalog.Vulnerable:
                case StatusCatalog.DefenseDownPercent:
                    return icons.StatusDefenseDown;

                case StatusCatalog.DamageReduction:
                case StatusCatalog.DefenseUp:
                case StatusCatalog.DefenseUpPercent:
                case StatusCatalog.HeavyArmor:
                case StatusCatalog.FinalBulwark:
                case StatusCatalog.RespondStance:
                case StatusCatalog.Guard:
                case StatusCatalog.Unyielding:
                case StatusCatalog.LastStand:
                    return icons.StatusDefenseUp;

                case StatusCatalog.ArmorUp:
                    return icons.StatusArmorAcqUp;

                case StatusCatalog.ArmorDown:
                    return icons.StatusArmorAcqDown;

                case StatusCatalog.Slow:
                    return icons.StatusSpdDown;

                case StatusCatalog.SnakeSwiftness:
                    return icons.StatusSpdUp != null ? icons.StatusSpdUp : icons.StatusSpdDown;

                case StatusCatalog.Poison:
                case StatusCatalog.NecroticPoison:
                case StatusCatalog.RotAvatar:
                case StatusCatalog.VenomSacBurst:
                case StatusCatalog.PlagueSpread:
                    return icons.StatusPoisoning;

                case StatusCatalog.Burn:
                    return icons.StatusBurning;

                case StatusCatalog.Taunt:
                case StatusCatalog.VampAura:
                case StatusCatalog.ReviveBlessing:
                case StatusCatalog.BoneWorkshop:
                case StatusCatalog.AnubisAvatar:
                case StatusCatalog.Ethereal:
                case StatusCatalog.EtherealOnNextHit:
                case StatusCatalog.RatSwarmCall:
                case StatusCatalog.BloodlineLegacy:
                case StatusCatalog.BloodSharing:
                case StatusCatalog.HolyInfusionPending:
                case StatusCatalog.PrayAncientSnakeGod:
                case StatusCatalog.PsionicBody:
                case StatusCatalog.SealedNextCard:
                case StatusCatalog.DespairSoulRecall:
                case StatusCatalog.EternalVoid:
                case StatusCatalog.SnakeGodChanneling:
                case StatusCatalog.FinalSummonPending:
                    return icons.NoteIcon;

                default:
                    return icons.NoteIcon != null ? icons.NoteIcon : icons.StatusDefenseUp;
            }
        }
    }
}
