namespace Grimhand.Expedition.Model
{
    /// <summary>天赋在整局远征内的累计状态。</summary>
    public sealed class ExpeditionTalentRunState
    {
        public bool MageReviveUsed { get; set; }
        public int RangerSacrificeHpTotal { get; set; }
        public bool EndlessBladeInjected { get; set; }
        public bool SnakeDetonateVenomInjected { get; set; }
        public bool LichRealmSealInjected { get; set; }

        public void Reset()
        {
            MageReviveUsed = false;
            RangerSacrificeHpTotal = 0;
            EndlessBladeInjected = false;
            SnakeDetonateVenomInjected = false;
            LichRealmSealInjected = false;
        }
    }
}
