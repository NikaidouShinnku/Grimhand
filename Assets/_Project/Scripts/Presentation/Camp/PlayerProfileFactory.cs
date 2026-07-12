using Grimhand.Content;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;

namespace Grimhand.Presentation.Camp
{
    public static class PlayerProfileFactory
    {
        /// <summary>Demo 新档默认局外金币，便于测试商店。</summary>
        public const int DemoStartingAccountGold = 10000;

        public static PlayerProfileState CreateNew(BattleSetupSO battleSetup, ExpeditionSetupSO expeditionSetup)
        {
            var roster = CampRosterBuilder.CreateDefault(battleSetup, expeditionSetup);
            return new PlayerProfileState
            {
                Meta = CampMetaState.CreateNewProfile(),
                Roster = roster,
                Collection = new CampCollectionState(),
                AccountGold = DemoStartingAccountGold,
                CollectionCapacity = CampCollectionState.DefaultCapacity
            };
        }
    }
}
