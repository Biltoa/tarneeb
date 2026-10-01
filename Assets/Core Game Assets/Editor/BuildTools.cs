using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Command-line entry points: Unity -batchmode -buildTarget WebGL -executeMethod BuildTools.BuildWebGL</summary>
public static class BuildTools
{
    private static readonly string[] Scenes =
    {
        "Assets/Core Game Assets/Scenes/Menu.unity",
        "Assets/Core Game Assets/Scenes/Game.unity"
    };

    [MenuItem("Tarneeb/Build WebGL")]
    public static void BuildWebGL()
    {
        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = "Build/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log("[Build] WebGL " + s.result + " size=" + (s.totalSize / (1024f * 1024f)).ToString("F1") + " MB time=" + s.totalTime + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings);
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
