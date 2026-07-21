using System.Collections.Generic;

namespace Grimhand.Persistence
{
    /// <summary>图书馆图鉴进度：遇见过的敌人/敌卡、拥有过的遗物。</summary>
    public sealed class CodexProgressState
    {
        public HashSet<string> SeenEnemyIds { get; } = new();
        public HashSet<string> SeenEnemyCardIds { get; } = new();
        public HashSet<string> SeenRelicIds { get; } = new();
    }
}
