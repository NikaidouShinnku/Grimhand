using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Model
{
    public enum ExpeditionCardOfferContext
    {
        RewardPickup,
        Shop,
        Event,
        Altar
    }

    public sealed class ExpeditionPendingCardOffer
    {
        public string OwnerCharacterId { get; set; } = "";
        public CardTemplate Template { get; set; }
        public ExpeditionCardOfferContext Context { get; set; }
    }

    public sealed class ExpeditionCardAltarMemberDraft
    {
        public int CollectionCardIndex { get; set; } = -1;
        public string ReplaceDeckCardKey { get; set; } = "";
        public bool HasSelection => CollectionCardIndex >= 0;
    }

    public sealed class ExpeditionCardAltarState
    {
        public int SourceLayer { get; set; }

        public Dictionary<string, ExpeditionCardAltarMemberDraft> Drafts { get; } = new();

        public ExpeditionCardAltarMemberDraft GetOrCreateDraft(string memberId)
        {
            if (!Drafts.TryGetValue(memberId, out var draft))
            {
                draft = new ExpeditionCardAltarMemberDraft();
                Drafts[memberId] = draft;
            }

            return draft;
        }
    }
}
