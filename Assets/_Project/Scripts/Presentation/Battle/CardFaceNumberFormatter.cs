namespace Grimhand.Presentation.Battle
{
    /// <summary>牌面数值相对基础值的颜色：升高绿、降低红、不变不着色。</summary>
    public static class CardFaceNumberFormatter
    {
        public const string RiseColorHex = "1F9E38";
        public const string FallColorHex = "D12E2E";

        public static string Format(int settled, int baseline)
        {
            if (settled == baseline)
                return settled.ToString();

            var hex = settled > baseline ? RiseColorHex : FallColorHex;
            return $"<color=#{hex}>{settled}</color>";
        }

        public static string FormatDurationTurns(int settledTurns, int baselineTurns)
        {
            if (settledTurns <= 0 && baselineTurns <= 0)
                return "本回合";

            if (settledTurns < 0)
                return settledTurns == baselineTurns ? "永久" : $"<color=#{RiseColorHex}>永久</color>";

            var label = $"{Format(settledTurns, baselineTurns)} 回合";
            return label;
        }
    }
}
