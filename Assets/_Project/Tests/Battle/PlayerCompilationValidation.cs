using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public static class PlayerCompilationValidation
    {
        [MenuItem("Grimhand/Validation/Compile Windows player scripts")]
        public static void Compile()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before compiling player scripts.");
            var output=Path.GetFullPath("Tools/UnityValidation20261018/player-script-check");
            Directory.CreateDirectory(output);
            var settings=new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone};
            var result=PlayerBuildInterface.CompilePlayerScripts(settings,output);
            if(result.assemblies==null||result.assemblies.Count==0)
                throw new InvalidOperationException("Player script compilation produced no assemblies.");
            if(!File.Exists(Path.Combine(output,"Grimhand.Steam.dll")))
                throw new InvalidOperationException("Steam player assembly was not compiled.");
            Debug.Log("[RuntimeValidation] Windows player scripts compiled, including non-Editor Steam cloud API paths.");
        }
    }
}
