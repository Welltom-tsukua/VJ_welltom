using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BeatLinkAutoPlay
{
    private const string MarkerFile = ".beatlink-autoplay";
    private static int attempts;

    static BeatLinkAutoPlay()
    {
        EditorApplication.update += TryStart;
    }

    private static void TryStart()
    {
        var markerPath = Path.Combine(Directory.GetCurrentDirectory(), MarkerFile);
        if (!File.Exists(markerPath))
        {
            EditorApplication.update -= TryStart;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        attempts++;
        if (attempts < 20)
        {
            return;
        }

        File.Delete(markerPath);
        Debug.Log("BeatLinkAutoPlay starting Play Mode.");
        EditorApplication.EnterPlaymode();
        EditorApplication.update -= TryStart;
    }
}
