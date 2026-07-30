using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [Serializable]
    public sealed class CardVisualEntry
    {
        public string CardId = "";
        public Sprite CardArt;
        public Sprite CardFrame;
        public Sprite CardIcon;
    }

    [Serializable]
    public sealed class CardFrameRaritySet
    {
        public CardRarity Rarity = CardRarity.Common;
        public Sprite AttackFrame;
        public Sprite DefenseFrame;
        public Sprite StatusFrame;
    }

    [CreateAssetMenu(fileName = "CardVisualCatalog", menuName = "Grimhand/Card Visual Catalog")]
    public class CardVisualCatalogSO : ScriptableObject
    {
        public Sprite DefaultCardArt;
        /// <summary>诅咒牌（如混沌之触）卡面插图。</summary>
        public Sprite CurseCardArt;
        public Sprite DefaultFrameAttack;
        public Sprite DefaultFrameDefense;
        public Sprite DefaultFrameStatus;
        public List<CardFrameRaritySet> FrameSets = new();
        public List<CardVisualEntry> Entries = new();

        public CardVisual Resolve(string cardId, CardType cardType, CardRarity rarity = CardRarity.Common)
        {
            foreach (var entry in Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CardId))
                    continue;
                if (entry.CardId != cardId)
                    continue;

                return new CardVisual(
                    entry.CardArt != null ? entry.CardArt : DefaultCardArt,
                    entry.CardFrame != null ? entry.CardFrame : GetFrame(cardType, rarity),
                    entry.CardIcon);
            }

            var art = IsCurseCardId(cardId) && CurseCardArt != null ? CurseCardArt : DefaultCardArt;
            return new CardVisual(
                art,
                GetFrame(cardType, rarity),
                null);
        }

        static bool IsCurseCardId(string cardId) =>
            !string.IsNullOrEmpty(cardId) && cardId.StartsWith("curse_");


        public Sprite GetDefaultFrame(CardType cardType) => GetFrame(cardType, CardRarity.Common);

        public Sprite GetFrame(CardType cardType, CardRarity rarity)
        {
            foreach (var set in FrameSets)
            {
                if (set == null || set.Rarity != rarity)
                    continue;

                return PickFrameFromSet(set, cardType);
            }

            if (FrameSets.Count > 0 && FrameSets[0] != null)
                return PickFrameFromSet(FrameSets[0], cardType);

            switch (cardType)
            {
                case CardType.Defense:
                    return DefaultFrameDefense != null ? DefaultFrameDefense : DefaultFrameAttack;
                case CardType.Status:
                    return DefaultFrameStatus != null ? DefaultFrameStatus : DefaultFrameAttack;
                default:
                    return DefaultFrameAttack;
            }
        }

        static Sprite PickFrameFromSet(CardFrameRaritySet set, CardType cardType)
        {
            switch (cardType)
            {
                case CardType.Defense:
                    return set.DefenseFrame != null ? set.DefenseFrame : set.AttackFrame;
                case CardType.Status:
                    return set.StatusFrame != null ? set.StatusFrame : set.AttackFrame;
                default:
                    return set.AttackFrame;
            }
        }
    }

    public sealed class CardVisual
    {
        public static readonly CardVisual Empty = new CardVisual(null, null, null);

        public CardVisual(Sprite art, Sprite frame, Sprite icon)
        {
            Art = art;
            Frame = frame;
            Icon = icon;
        }

        public Sprite Art { get; }
        public Sprite Frame { get; }
        public Sprite Icon { get; }
    }
}
