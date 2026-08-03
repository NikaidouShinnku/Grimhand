using Grimhand.Content;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;

namespace Grimhand.Presentation.Camp
{
    public static class PlayerProfileFactory
    {
        /// <summary>新档默认局外金币（够一次普通卡祭坛刻印）。</summary>
        public const int DemoStartingAccountGold = 300;

        public static PlayerProfileState CreateNew(BattleSetupSO battleSetup, ExpeditionSetupSO expeditionSetup)
        {
            var roster = CampRosterBuilder.CreateDefault(battleSetup, expeditionSetup);
            return new PlayerProfileState
            {
                Meta = CampMetaState.CreateNewProfile(),
                Roster = roster,
                Collection = new CampCollectionState(),
                Codex = new CodexProgressState(),
                AccountGold = DemoStartingAccountGold,
                CollectionCapacity = CampCollectionState.DefaultCapacity
            };
        }
    }
}
