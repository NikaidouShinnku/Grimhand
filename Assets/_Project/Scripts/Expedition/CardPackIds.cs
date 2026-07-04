namespace Grimhand.Expedition
{
    public static class CardPackIds
    {
        public const string Common = "cardpack_common";
        public const string Advanced = "cardpack_advanced";
        public const string Master = "cardpack_master";

        public static string GetDisplayName(string packId) =>
            packId switch
            {
                Common => "普通卡包",
                Advanced => "高级卡包",
                Master => "大师卡包",
                _ => "卡包"
            };

        public static bool IsValid(string packId) =>
            packId is Common or Advanced or Master;
    }
}
