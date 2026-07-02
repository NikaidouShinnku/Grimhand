namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 军营编队槽位与战斗站位对齐。
    /// Members[0]=前排(Front，战场靠右/近敌)，[1]=中排，[2]=后排(Back，战场靠左)。
    /// 军营 UI 从左到右与战斗画面一致：后排 → 中排 → 前排。
    /// </summary>
    public static class CampFormationDisplay
    {
        public static readonly int[] VisualOrderMemberIndices = { 2, 1, 0 };

        public static string SlotLabel(int memberIndex) =>
            memberIndex switch
            {
                0 => "前排",
                1 => "中排",
                2 => "后排",
                _ => $"槽{memberIndex + 1}"
            };
    }
}
