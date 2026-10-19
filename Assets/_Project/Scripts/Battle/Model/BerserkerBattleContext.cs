using System.Collections.Generic;
namespace Grimhand.Battle.Model
{
    public sealed class BerserkerRagePayment
    {
        public string OwnerId;
        public int Amount;
        public bool Emptied;
    }
    public sealed class BerserkerRecall
    {
        public string OwnerId, Label;
        public CardType Type;
        public int Remaining, MaxCost=int.MaxValue, DueTurn;
        public bool FreeInHand;
        public bool AllowSkip;
    }
    public sealed class BerserkerBattleContext
    {
        public int Rage, BaseGainThisTurn;
        public bool Initialized, AttackedThisTurn, Concentrated, Tempered, ConcentratedUsed, HealUsed, SwapUsed, EmptyUsed, BlockUsed, KillUsed, StripUsed;
        public int BannerStartTurn;
        public string DuelTargetId;
        public readonly HashSet<int> EnemyAttacksSeen=new();
        public readonly Dictionary<int,int> SpentByPlay=new();
        public readonly Dictionary<int,string> BehindByPlay=new();
        public readonly Dictionary<int,int> SplashDamageByPlay=new();
        public readonly HashSet<int> SuccessfulResponseCards=new();
        public readonly HashSet<int> TemporaryRetains=new();
    }
}
