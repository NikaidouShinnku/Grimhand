using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class CardTemplate
    {
        public string DefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string OwnerCharacterId { get; set; } = "";
        public int Cost { get; set; }
        public CardType CardType { get; set; }
        public List<string> Keywords { get; } = new();
        public List<EffectActionSpec> Actions { get; } = new();

        public static CardTemplate Create(
            string definitionId,
            string displayName,
            string ownerCharacterId,
            int cost,
            CardType cardType,
            params EffectActionSpec[] actions)
        {
            var template = new CardTemplate
            {
                DefinitionId = definitionId,
                DisplayName = displayName,
                OwnerCharacterId = ownerCharacterId,
                Cost = cost,
                CardType = cardType
            };
            template.Actions.AddRange(actions);
            return template;
        }

        public static CardTemplate FromLegacy(
            string definitionId,
            string displayName,
            string ownerCharacterId,
            int cost,
            CardType cardType,
            CardEffectKind effectKind,
            int power,
            int drawNextTurn = 0)
        {
            var template = new CardTemplate
            {
                DefinitionId = definitionId,
                DisplayName = displayName,
                OwnerCharacterId = ownerCharacterId,
                Cost = cost,
                CardType = cardType
            };

            switch (effectKind)
            {
                case CardEffectKind.DealDamage:
                    template.Actions.Add(new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Value = power,
                        ScaleWithAttack = true,
                        Reach = TargetReach.FrontAndMiddle
                    });
                    break;
                case CardEffectKind.GainBlock:
                    template.Actions.Add(new EffectActionSpec
                    {
                        Type = EffectActionType.GainBlock,
                        Target = EffectTarget.Self,
                        Value = power,
                        ScaleWithDefense = true
                    });
                    break;
                case CardEffectKind.Heal:
                    template.Actions.Add(new EffectActionSpec
                    {
                        Type = EffectActionType.Heal,
                        Target = EffectTarget.Self,
                        Value = power
                    });
                    break;
                case CardEffectKind.DrawCards:
                    template.Actions.Add(new EffectActionSpec
                    {
                        Type = EffectActionType.DrawCardsNextTurn,
                        Target = EffectTarget.Self,
                        Value = drawNextTurn > 0 ? drawNextTurn : power
                    });
                    break;
            }

            return template;
        }
    }
}
