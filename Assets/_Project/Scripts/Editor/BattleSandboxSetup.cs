using UnityEditor;

namespace Grimhand.Editor
{
    /// <summary>旧菜单入口，转发到一键测试场景流程。</summary>
    public static class BattleSandboxSetup
    {
        [MenuItem("Grimhand/Setup Battle Sandbox Scene", priority = 1)]
        public static void SetupScene()
        {
            GrimhandBattleSceneBootstrap.OpenBattleTestScene();
        }
    }
}
