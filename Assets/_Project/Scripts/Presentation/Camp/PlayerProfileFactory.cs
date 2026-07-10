using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;

namespace Grimhand.Presentation.Camp
{
    public static class PlayerProfileFactory
    {
        public static PlayerProfileState CreateNew(BattleSetupSO battleSetup, ExpeditionSetupSO expeditionSetup)
        {
            var roster = CampRosterBuilder.CreateDefault(battleSetup, expeditionSetup);
            var collection = CampCollectionBuilder.BuildInitialFromRoster(roster);
            return new PlayerProfileState
            {
                Meta = CampMetaState.CreateNewProfile(),
                Roster = roster,
                Collection = collection,
                AccountGold = 0,
                CollectionCapacity = CampCollectionState.DefaultCapacity
            };
        }
    }
}
