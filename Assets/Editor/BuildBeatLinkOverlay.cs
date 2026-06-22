using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BuildBeatLinkOverlay
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string DefaultBuildName = "BeatLinkUnityOverlay.exe";

    static BuildBeatLinkOverlay()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildFromUnityWindow);
    }

    [MenuItem("BeatLink/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        var options = new BuildPlayerOptions
        {
            locationPathName = Path.Combine(Path.GetFullPath("Build"), DefaultBuildName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildWithMainScene(options);
    }

    [MenuItem("BeatLink/Open Main Scene")]
    public static void OpenMainScene()
    {
        if (!File.Exists(MainScenePath))
        {
            throw new FileNotFoundException("Main scene not found.", MainScenePath);
        }

        EditorSceneManager.OpenScene(MainScenePath);
    }

    [MenuItem("BeatLink/Sync Build Settings")]
    public static void EnsureBuildSettingsScene()
    {
        var existing = EditorBuildSettings.scenes;
        if (existing != null)
        {
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && string.Equals(existing[i].path, MainScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!existing[i].enabled)
                    {
                        existing[i].enabled = true;
                        EditorBuildSettings.scenes = existing;
                    }
                    return;
                }
            }
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainScenePath, true)
        };
    }

    private static void BuildFromUnityWindow(BuildPlayerOptions options)
    {
        BuildWithMainScene(options);
    }

    private static void BuildWithMainScene(BuildPlayerOptions options)
    {
        EnsureMainSceneExists();
        EnsureBuildSettingsScene();
        EnsureMainSceneLoadedIfEditorHasBackupScene();

        var sanitizedOptions = SanitizeOptions(options);
        EditorSceneManager.SaveOpenScenes();

        var report = BuildPipeline.BuildPlayer(sanitizedOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("Build failed: " + report.summary.result);
        }

        Debug.Log("Built " + sanitizedOptions.locationPathName);
    }

    private static void EnsureMainSceneExists()
    {
        if (!File.Exists(MainScenePath))
        {
            throw new FileNotFoundException("Main scene not found.", MainScenePath);
        }
    }

    private static void EnsureMainSceneLoadedIfEditorHasBackupScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            OpenMainScene();
            return;
        }

        if (activeScene.path.Replace('\\', '/').Contains("/Temp/__Backupscenes/"))
        {
            Debug.LogWarning("Active scene was a Unity backup scene. Opening Assets/Scenes/Main.unity for build.");
            OpenMainScene();
        }
    }

    private static BuildPlayerOptions SanitizeOptions(BuildPlayerOptions options)
    {
        var sanitized = options;
        sanitized.scenes = new[] { MainScenePath };
        sanitized.target = BuildTarget.StandaloneWindows64;

        if (string.IsNullOrWhiteSpace(sanitized.locationPathName))
        {
            sanitized.locationPathName = Path.Combine(Path.GetFullPath("Build"), DefaultBuildName);
        }

        var outputDir = Path.GetDirectoryName(sanitized.locationPathName);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Path.GetFullPath("Build");
            sanitized.locationPathName = Path.Combine(outputDir, DefaultBuildName);
        }

        Directory.CreateDirectory(outputDir);
        return sanitized;
    }
}
