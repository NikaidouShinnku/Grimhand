using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Persistence;

namespace Grimhand.Presentation.Camp
{
    public static class SaveValidationContextBuilder
    {
        public static SaveValidationContext Build(ExpeditionSetupSO expeditionSetup)
        {
            var context = new SaveValidationContext();
            foreach (var talent in TalentCatalog.GetAll())
            {
                if (talent != null && !string.IsNullOrEmpty(talent.Id))
                    context.ValidTalentIds.Add(talent.Id);
            }

            if (expeditionSetup?.PlayerCardCatalog == null)
                return context;

            foreach (var card in expeditionSetup.PlayerCardCatalog)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                context.ValidCardIds.Add(card.CardId);
                context.CardOwnerById[card.CardId] = card.OwnerCharacterId ?? "";
            }

            return context;
        }
    }
}
