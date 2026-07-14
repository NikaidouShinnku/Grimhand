using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition;

namespace Grimhand.Expedition.Model
{
    public enum ExpeditionCardOfferContext
    {
        RewardPickup,
        Shop,
        Event,
        Altar,
        CardPack
    }

    public sealed class ExpeditionPendingCardOffer
    {
        public string OwnerCharacterId { get; set; } = "";
        public CardTemplate Template { get; set; }
        public ExpeditionCardOfferContext Context { get; set; }
        public int SourceRewardPackIndex { get; set; } = -1;
        public int SourceShopSlotIndex { get; set; } = -1;
        public string SourcePackId { get; set; } = "";
    }

    public sealed class ExpeditionPendingCardPackOffer
    {
        public string PackId { get; set; } = "";
        public ExpeditionCardOfferContext Context { get; set; }
        public int RewardPackIndex { get; set; } = -1;
        public int ShopSlotIndex { get; set; } = -1;
        public List<CardPackChoice> Choices { get; } = new();
    }

    public sealed class ExpeditionCardAltarMemberDraft
    {
        public int CollectionCardIndex { get; set; } = -1;
        public string ReplaceDeckCardKey { get; set; } = "";
        /// <summary>本角色本趟祭坛是否已确认取出过一张。</summary>
        public bool Confirmed { get; set; }
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
