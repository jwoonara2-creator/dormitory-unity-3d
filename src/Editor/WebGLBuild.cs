using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// WebGL build for the Flutter facility tab.
//   Menu: Tools > Dorm > Build WebGL (for app)
//   Batch: Unity -batchmode -quit -projectPath <p> -executeMethod WebGLBuild.BuildFromCommandLine -outDir <folder>
public static class WebGLBuild
{
    const string DefaultOut = "Builds/WebGL";

    [MenuItem("Tools/Dorm/Build WebGL (for app)")]
    public static void BuildFromMenu() => Build(DefaultOut);

    public static void BuildFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "-outDir");
        var ok = Build(i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOut);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Build(string outDir)
    {
        // Main (map with the facilities) is the start scene; its room/gym/laundry/study scenes follow,
        // then the floor / corridor / room flow scenes (reached from the app's floor tabs).
        var names = new[]
        {
            "Main", "GymRoom", "GymRoomView", "LaundryRoom", "LaundryRoomView", "StudyRoom", "StudyRoomView",
        }.Concat(NavBuilder.FlowScenes);
        EditorBuildSettings.scenes = names
            .Select(n => "Assets/Scenes/" + n + ".unity")
            .Where(File.Exists)
            .Select(p => new EditorBuildSettingsScene(p, true))
            .ToArray();

        PlayerSettings.WebGL.template = "PROJECT:AppEmbed";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // hosting adds its own compression
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.dataCaching = true;

        // size: smaller code, no C++ exception tables, aggressive managed stripping, no Unity splash
        // pass -fast on the command line for a quick test build (bigger wasm, ~5 min instead of ~25)
        var fast = Environment.GetCommandLineArgs().Contains("-fast");
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = fast
            ? UnityEditor.WebGL.WasmCodeOptimization.BuildTimes
            : UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SplashScreen.show = false;
        // phones: ASTC (desktop-only DXT would be unpacked to RGBA32 on most phone GPUs)
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        WebGLTextureOptimizer.Apply();

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        Directory.CreateDirectory(outDir);
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"WebGLBuild: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}, errors={summary.totalErrors}");
        return summary.result == BuildResult.Succeeded;
    }
}
