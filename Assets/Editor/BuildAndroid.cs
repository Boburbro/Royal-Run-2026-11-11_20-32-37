using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Android APK yig'ish: menyu Royal Run > Build Android APK yoki buyruq qatoridan:
// Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildAndroid.Build
public static class BuildAndroid
{
    const string Output = "build/RoyalRun.apk";

    [MenuItem("Royal Run/Build Android APK")]
    public static void Build()
    {
        EditorUserBuildSettings.buildAppBundle = false; // telefonga to'g'ridan-to'g'ri o'rnatish uchun APK
        var opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opts);
        Debug.Log("ROYALRUN BUILD RESULT: " + r.summary.result + ", " + (r.summary.totalSize / (1024f * 1024f)).ToString("F1") + " MB -> " + Output);
        if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
