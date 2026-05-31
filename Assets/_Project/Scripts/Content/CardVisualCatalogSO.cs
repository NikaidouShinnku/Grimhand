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

    [CreateAssetMenu(fileName = "CardVisualCatalog", menuName = "Grimhand/Card Visual Catalog")]
    public class CardVisualCatalogSO : ScriptableObject
    {
        public Sprite DefaultCardArt;
        public Sprite DefaultFrameAttack;
        public Sprite DefaultFrameDefense;
        public Sprite DefaultFrameStatus;
        public List<CardVisualEntry> Entries = new();

        public CardVisual Resolve(string cardId, CardType cardType)
        {
            foreach (var entry in Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CardId))
                    continue;
                if (entry.CardId != cardId)
                    continue;

                return new CardVisual(
                    entry.CardArt != null ? entry.CardArt : DefaultCardArt,
                    entry.CardFrame != null ? entry.CardFrame : GetDefaultFrame(cardType),
                    entry.CardIcon);
            }

            return new CardVisual(
                DefaultCardArt,
                GetDefaultFrame(cardType),
                null);
        }

        public Sprite GetDefaultFrame(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.Defense: return DefaultFrameDefense != null ? DefaultFrameDefense : DefaultFrameAttack;
                case CardType.Status: return DefaultFrameStatus != null ? DefaultFrameStatus : DefaultFrameAttack;
                default: return DefaultFrameAttack;
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
