using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildBeatLinkOverlay
{
    public static void BuildWindowsPlayer()
    {
        var sceneDir = "Assets/Scenes";
        Directory.CreateDirectory(sceneDir);

        var scenePath = Path.Combine(sceneDir, "Main.unity").Replace('\\', '/');
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = Color.black;
        EditorSceneManager.SaveScene(scene, scenePath);

        var buildDir = Path.GetFullPath("Build");
        Directory.CreateDirectory(buildDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = Path.Combine(buildDir, "BeatLinkUnityOverlay.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new System.Exception("Build failed: " + report.summary.result);
        }

        Debug.Log("Built " + options.locationPathName);
    }
}
