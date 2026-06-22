using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed partial class BeatLinkOverlayClient : MonoBehaviour
{
    private float CurrentVisualBpm()
    {
        if (!bpmDjLinkMode)
        {
            return manualBpm;
        }

        lock (stateLock)
        {
            PlayerState master = null;
            foreach (var player in players.Values)
            {
                if (player.IsTempoMaster)
                {
                    master = player;
                    break;
                }
                if (master == null && player.HasStatus)
                {
                    master = player;
                }
            }
            return master == null ? manualBpm : DisplayEffectiveBpm(master);
        }
    }

    private float CurrentVisualBeatFloat(float bpm)
    {
        var effectiveBpm = bpm > 0.01f ? bpm : 120f;
        if (!bpmDjLinkMode)
        {
            return Mathf.Max(0f, Time.realtimeSinceStartup - manualBeatAnchorTime) * effectiveBpm / 60f;
        }
        return DjLinkBeatFloat(effectiveBpm);
    }

    private float DjLinkBeatFloat(float bpm)
    {
        var effectiveBpm = bpm > 0.01f ? bpm : 120f;
        var now = Time.realtimeSinceStartup;
        if (!djLinkBeatClockInitialized)
        {
            djLinkBeatClockInitialized = true;
            djLinkBeatAnchorTime = now;
            djLinkBeatAnchorBeatFloat = 0f;
            djLinkBeatClockBpm = effectiveBpm;
        }

        if (Mathf.Abs(djLinkBeatClockBpm - effectiveBpm) > 0.01f)
        {
            djLinkBeatAnchorBeatFloat += Mathf.Max(0f, now - djLinkBeatAnchorTime) * djLinkBeatClockBpm / 60f;
            djLinkBeatAnchorTime = now;
            djLinkBeatClockBpm = effectiveBpm;
        }

        return djLinkBeatAnchorBeatFloat + Mathf.Max(0f, now - djLinkBeatAnchorTime) * djLinkBeatClockBpm / 60f;
    }

    private void EnableDjLinkBpmMode()
    {
        var now = Time.realtimeSinceStartup;
        var currentBeatFloat = bpmDjLinkMode
            ? DjLinkBeatFloat(djLinkBeatClockBpm)
            : Mathf.Max(0f, now - manualBeatAnchorTime) * Mathf.Max(0.01f, manualBpm) / 60f;
        var bpm = CurrentVisualBpm();

        bpmDjLinkMode = true;
        djLinkBeatClockInitialized = true;
        djLinkBeatAnchorTime = now;
        djLinkBeatAnchorBeatFloat = currentBeatFloat;
        djLinkBeatClockBpm = bpm > 0.01f ? bpm : 120f;
    }

    private static Mesh BuildCubeMesh()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        AddTexturedQuad(vertices, triangles, uv, new Vector3(-1,-1, 1), new Vector3( 1,-1, 1), new Vector3( 1, 1, 1), new Vector3(-1, 1, 1), Vector3.forward);
        AddTexturedQuad(vertices, triangles, uv, new Vector3( 1,-1,-1), new Vector3(-1,-1,-1), new Vector3(-1, 1,-1), new Vector3( 1, 1,-1), Vector3.back);
        AddTexturedQuad(vertices, triangles, uv, new Vector3(-1, 1, 1), new Vector3( 1, 1, 1), new Vector3( 1, 1,-1), new Vector3(-1, 1,-1), Vector3.up);
        AddTexturedQuad(vertices, triangles, uv, new Vector3(-1,-1,-1), new Vector3( 1,-1,-1), new Vector3( 1,-1, 1), new Vector3(-1,-1, 1), Vector3.down);
        AddTexturedQuad(vertices, triangles, uv, new Vector3( 1,-1, 1), new Vector3( 1,-1,-1), new Vector3( 1, 1,-1), new Vector3( 1, 1, 1), Vector3.right);
        AddTexturedQuad(vertices, triangles, uv, new Vector3(-1,-1,-1), new Vector3(-1,-1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1,-1), Vector3.left);

        return BuildMesh(vertices.ToArray(), triangles.ToArray(), uv.ToArray());
    }

    private static Mesh BuildGroundPlaneMesh()
    {
        var vertices = new[]
        {
            new Vector3(-1f, 0f, -1f),
            new Vector3( 1f, 0f, -1f),
            new Vector3( 1f, 0f,  1f),
            new Vector3(-1f, 0f,  1f)
        };
        var triangles = new[] { 0, 2, 1, 0, 3, 2 };
        var uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        return BuildMesh(vertices, triangles, uv);
    }

    private static Mesh BuildGeneratorScreenMesh()
    {
        var vertices = new[]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3( 1f, -1f, 0f),
            new Vector3( 1f,  1f, 0f),
            new Vector3(-1f,  1f, 0f)
        };
        var triangles = new[] { 0, 1, 2, 0, 2, 3 };
        var uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        return BuildMesh(vertices, triangles, uv);
    }

    private static Vector3[] BuildGeneratorScreenDirections(int count)
    {
        var clampedCount = Mathf.Max(1, count);
        var directions = new Vector3[clampedCount];
        var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (var i = 0; i < clampedCount; i++)
        {
            var t = clampedCount == 1 ? 0.5f : (i + 0.5f) / clampedCount;
            var y = Mathf.Lerp(0.78f, -0.52f, t);
            var radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            var theta = goldenAngle * i;
            directions[i] = new Vector3(Mathf.Cos(theta) * radial, y, Mathf.Sin(theta) * radial).normalized;
        }
        return directions;
    }

    private static Mesh BuildTetrahedronMesh()
    {
        var p0 = new Vector3( 1, 1, 1);
        var p1 = new Vector3(-1,-1, 1);
        var p2 = new Vector3(-1, 1,-1);
        var p3 = new Vector3( 1,-1,-1);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();

        AddTexturedTriangle(vertices, triangles, uv, p0, p1, p2, (p0 + p1 + p2).normalized);
        AddTexturedTriangle(vertices, triangles, uv, p0, p3, p1, (p0 + p3 + p1).normalized);
        AddTexturedTriangle(vertices, triangles, uv, p0, p2, p3, (p0 + p2 + p3).normalized);
        AddTexturedTriangle(vertices, triangles, uv, p1, p3, p2, (p1 + p3 + p2).normalized);

        return BuildMesh(vertices.ToArray(), triangles.ToArray(), uv.ToArray());
    }

    private static Mesh BuildDodecahedronMesh()
    {
        var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var icoVertices = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        var icoTriangles = new[]
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

        var faceCenters = new Vector3[icoTriangles.Length / 3];
        for (var i = 0; i < faceCenters.Length; i++)
        {
            var a = icoVertices[icoTriangles[i * 3]];
            var b = icoVertices[icoTriangles[i * 3 + 1]];
            var c = icoVertices[icoTriangles[i * 3 + 2]];
            faceCenters[i] = ((a + b + c) / 3f).normalized;
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();
        var pentagonUv = new[]
        {
            new Vector2(0.5f, 1f),
            new Vector2(0.975f, 0.655f),
            new Vector2(0.794f, 0.095f),
            new Vector2(0.206f, 0.095f),
            new Vector2(0.025f, 0.655f)
        };

        for (var vertexIndex = 0; vertexIndex < icoVertices.Length; vertexIndex++)
        {
            var adjacent = new List<int>(5);
            for (var faceIndex = 0; faceIndex < faceCenters.Length; faceIndex++)
            {
                if (icoTriangles[faceIndex * 3] == vertexIndex ||
                    icoTriangles[faceIndex * 3 + 1] == vertexIndex ||
                    icoTriangles[faceIndex * 3 + 2] == vertexIndex)
                {
                    adjacent.Add(faceIndex);
                }
            }

            var normal = icoVertices[vertexIndex].normalized;
            var axisA = Vector3.Cross(normal, Vector3.up);
            if (axisA.sqrMagnitude < 0.0001f)
            {
                axisA = Vector3.Cross(normal, Vector3.right);
            }
            axisA.Normalize();
            var axisB = Vector3.Cross(axisA, normal).normalized;
            adjacent.Sort((a, b) =>
            {
                var da = faceCenters[a];
                var db = faceCenters[b];
                var aa = Mathf.Atan2(Vector3.Dot(da, axisB), Vector3.Dot(da, axisA));
                var ab = Mathf.Atan2(Vector3.Dot(db, axisB), Vector3.Dot(db, axisA));
                return aa.CompareTo(ab);
            });

            var start = vertices.Count;
            for (var i = 0; i < adjacent.Count; i++)
            {
                vertices.Add(faceCenters[adjacent[i]] * 1.35f);
                uv.Add(pentagonUv[i]);
            }

            AddOutwardTriangle(vertices, triangles, start, start + 1, start + 2, normal);
            AddOutwardTriangle(vertices, triangles, start, start + 2, start + 3, normal);
            AddOutwardTriangle(vertices, triangles, start, start + 3, start + 4, normal);
        }
        return BuildMesh(vertices.ToArray(), triangles.ToArray(), uv.ToArray());
    }

    private static void AddOutwardTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 outward)
    {
        var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (Vector3.Dot(cross, outward) < 0f)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            return;
        }

        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private static void AddTexturedQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
    {
        var start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uv.Add(new Vector2(0f, 0f));
        uv.Add(new Vector2(1f, 0f));
        uv.Add(new Vector2(1f, 1f));
        uv.Add(new Vector2(0f, 1f));
        AddOutwardTriangle(vertices, triangles, start, start + 1, start + 2, outward);
        AddOutwardTriangle(vertices, triangles, start, start + 2, start + 3, outward);
    }

    private static void AddTexturedTriangle(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        var start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        uv.Add(new Vector2(0.5f, 1f));
        uv.Add(new Vector2(0f, 0f));
        uv.Add(new Vector2(1f, 0f));
        AddOutwardTriangle(vertices, triangles, start, start + 1, start + 2, outward);
    }

    private static Mesh BuildMesh(Vector3[] vertices, int[] triangles, Vector2[] uv)
    {
        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static string ToFileUrl(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private void OpenRootFolderDialog()
    {
        try
        {
            var selected = BrowseForFolderWithWindowsDialog(Directory.Exists(mediaRootPath) ? mediaRootPath : ChooseDefaultMediaRoot());
            if (!string.IsNullOrEmpty(selected) && Directory.Exists(selected))
            {
                SetMediaRoot(selected);
            }
            return;
        }
        catch (Exception ex)
        {
            mediaBrowserError = "Folder dialog failed: " + ex.Message;
        }
    }

    private static string BrowseForFolderWithWindowsDialog(string initialPath)
    {
        string selected = null;
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                selected = NativeFolderPicker.PickFolder(Directory.Exists(initialPath) ? initialPath : ChooseDefaultMediaRoot());
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw new InvalidOperationException(failure.Message, failure);
        }
        return selected;
    }

    private void OpenGeneratorFaceImageFolderDialog(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        try
        {
            var initialPath = Directory.Exists(generator.FaceImageFolderPath) ? generator.FaceImageFolderPath : mediaRootPath;
            var selected = BrowseForFolderWithWindowsDialog(initialPath);
            if (!string.IsNullOrEmpty(selected) && Directory.Exists(selected))
            {
                generator.FaceImageFolderPath = selected;
                generator.FaceImageFolderInput = selected;
                RefreshGeneratorFaceImageFolder(generator);
                SaveGeneratorFaceFolderPreference(selected);
            }
        }
        catch (Exception ex)
        {
            generator.FaceImageError = "Folder dialog failed: " + ex.Message;
        }
    }

    private static string BrowseForFolderWithShell(string initialPath)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType == null)
        {
            return null;
        }

        var shell = Activator.CreateInstance(shellType);
        if (shell == null)
        {
            return null;
        }

        try
        {
            const int options = 0x00000001 | 0x00000010;
            var folder = shellType.InvokeMember("BrowseForFolder",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { 0, "Select media root folder", options, initialPath });
            if (folder == null)
            {
                return null;
            }

            var self = folder.GetType().InvokeMember("Self",
                System.Reflection.BindingFlags.GetProperty,
                null,
                folder,
                null);
            if (self == null)
            {
                return null;
            }

            var path = self.GetType().InvokeMember("Path",
                System.Reflection.BindingFlags.GetProperty,
                null,
                self,
                null) as string;
            return path;
        }
        finally
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(shell))
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }
    }

    private void SelectNextCaptureDevice()
    {
        captureDevices = WebCamTexture.devices;
        if (captureDevices == null || captureDevices.Length == 0)
        {
            captureError = "No capture devices found.";
            captureDeviceIndex = -1;
            return;
        }

        captureDeviceIndex = captureDeviceIndex < 0 ? 0 : (captureDeviceIndex + 1) % captureDevices.Length;
        StartCapturePreview();
    }

    private static int SelectCaptureDevice(WebCamDevice[] devices, int currentIndex)
    {
        if (currentIndex >= 0 && currentIndex < devices.Length)
        {
            return currentIndex;
        }

        var preferred = Environment.GetEnvironmentVariable("HDMI_CAPTURE_DEVICE");
        if (!string.IsNullOrEmpty(preferred))
        {
            for (var i = 0; i < devices.Length; i++)
            {
                if (devices[i].name.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        var bestIndex = 0;
        var bestScore = int.MinValue;
        for (var i = 0; i < devices.Length; i++)
        {
            var score = ScoreCaptureDevice(devices[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static int ScoreCaptureDevice(WebCamDevice device)
    {
        var name = device.name ?? "";
        var score = device.isFrontFacing ? -10 : 0;
        if (name.IndexOf("hdmi", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 30;
        }
        if (name.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 25;
        }
        if (name.IndexOf("usb video", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 20;
        }
        if (name.IndexOf("uvc", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 15;
        }
        if (name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 5;
        }
        return score;
    }

    private static string FormatCaptureDeviceList(WebCamDevice[] devices)
    {
        if (devices == null || devices.Length == 0)
        {
            return "(none)";
        }

        var names = new string[devices.Length];
        for (var i = 0; i < devices.Length; i++)
        {
            names[i] = devices[i].name;
        }
        return string.Join(", ", names);
    }

    private void EnsureBltPollingState()
    {
        if (!useBltBridgeMode || useSimulatedDjLinkMode)
        {
            return;
        }

        if (!bltMetadataPollStarted)
        {
            bltMetadataPollStarted = true;
            StartCoroutine(PollBltMetadataLoop());
        }

        if (!bltWavePollStarted)
        {
            bltWavePollStarted = true;
            StartCoroutine(PollBltRuntimeWaveLoop());
        }
    }

    private IEnumerator PollBltMetadataLoop()
    {
        while (true)
        {
            if (!useBltBridgeMode)
            {
                yield return new WaitForSeconds(2f);
                continue;
            }

            if (string.IsNullOrEmpty(bltParamsUrl))
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            using (var request = UnityWebRequest.Get(bltParamsUrl))
            {
                request.timeout = bltRequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var root = MiniJson.Parse(request.downloadHandler.text) as Dictionary<string, object>;
                        var activePlayerCount = ApplyBltParams(root);
                        bltError = null;
                        bltServerReachable = true;
                        lastBltServerSeenUtc = DateTime.UtcNow;
                        bltActivePlayerCount = activePlayerCount;
                        bltReceived = activePlayerCount > 0;
                        if (bltReceived)
                        {
                            lastBltUpdateUtc = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        bltError = "BLT JSON parse error: " + ex.Message;
                        bltServerReachable = false;
                        bltActivePlayerCount = 0;
                        bltReceived = false;
                    }
                }
                else
                {
                    bltError = "BLT metadata server: " + request.error;
                    bltServerReachable = false;
                    bltActivePlayerCount = 0;
                    bltReceived = false;
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, bltPollIntervalSeconds));
        }
    }

    private IEnumerator PollBltRuntimeWaveLoop()
    {
        while (true)
        {
            if (!useBltBridgeMode)
            {
                yield return new WaitForSeconds(2f);
                continue;
            }

            yield return FetchBltRuntimeWave(1);
            yield return FetchBltOverviewWave(1);
            yield return FetchBltRuntimeWave(2);
            yield return FetchBltOverviewWave(2);
            yield return FetchBltRuntimeWave(3);
            yield return FetchBltOverviewWave(3);
            yield return FetchBltRuntimeWave(4);
            yield return FetchBltOverviewWave(4);
            yield return new WaitForSeconds(Mathf.Max(0.01f, bltWaveFrameIntervalSeconds));
        }
    }

    private int ApplyBltParams(Dictionary<string, object> root)
    {
        var playersDict = Dict(root, "players");
        if (playersDict == null)
        {
            return 0;
        }

        var activePlayerCount = 0;
        foreach (var key in SortedKeys(playersDict))
        {
            int deviceNumber;
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out deviceNumber))
            {
                continue;
            }

            if (deviceNumber < 1 || deviceNumber > 4)
            {
                continue;
            }

            var playerDict = playersDict[key] as Dictionary<string, object>;
            if (playerDict == null)
            {
                continue;
            }

            var track = Dict(playerDict, "track");
            var timePlayed = Dict(playerDict, "time-played");
            var timeRemaining = Dict(playerDict, "time-remaining");
            activePlayerCount++;

            lock (stateLock)
            {
                var player = GetOrCreatePlayer(deviceNumber);
                player.BltSeen = true;
                player.LastBltUpdateUtc = DateTime.UtcNow;
                player.BltTitle = Text(track, "title", player.BltTitle);
                player.BltArtist = Text(track, "artist", player.BltArtist);
                player.BltAlbum = Text(track, "album", player.BltAlbum);
                player.BltComment = Text(track, "comment", player.BltComment);
                player.BltKey = Text(track, "key", player.BltKey);
                player.BltGenre = Text(track, "genre", player.BltGenre);
                player.BltColor = Text(track, "color", player.BltColor);
                player.BltSlot = Text(track, "slot", player.BltSlot);
                player.BltType = Text(track, "type", player.BltType);
                player.BltDurationSeconds = Int(track, "duration", player.BltDurationSeconds);
                player.BltTimePlayedMs = Int(timePlayed, "raw-milliseconds", player.BltTimePlayedMs);
                player.BltTimeRemainingMs = Int(timeRemaining, "raw-milliseconds", player.BltTimeRemainingMs);
                player.BltTimePlayedDisplay = Text(timePlayed, "display", player.BltTimePlayedDisplay);
                player.BltTimeRemainingDisplay = Text(timeRemaining, "display", player.BltTimeRemainingDisplay);

                var tempo = Float(playerDict, "tempo", 0f);
                if (tempo > 0.01f)
                {
                    player.BltTempo = tempo;
                    if (!player.HasStatus)
                    {
                        ApplyBpm(player, player.Bpm > 0.01f ? player.Bpm : tempo, tempo);
                    }
                }

                if (!player.HasStatus)
                {
                    player.IsTempoMaster = Bool(playerDict, "is-tempo-master", player.IsTempoMaster);
                    player.IsPlaying = Bool(playerDict, "is-playing", player.IsPlaying);
                    player.IsSynced = Bool(playerDict, "is-synced", player.IsSynced);
                    player.IsOnAir = Bool(playerDict, "is-on-air", player.IsOnAir);
                    player.IsBpmOnlySynced = Bool(playerDict, "is-bpm-only-synced", player.IsBpmOnlySynced);
                    player.BeatNumber = Int(playerDict, "beat-number", player.BeatNumber);
                    player.BeatWithinBar = Int(playerDict, "beat-within-bar", player.BeatWithinBar);
                    player.TrackNumber = Int(playerDict, "track-number", player.TrackNumber);
                    var trackBpm = Float(playerDict, "track-bpm", player.Bpm);
                    ApplyBpm(player, trackBpm, tempo > 0.01f ? tempo : trackBpm);
                    player.PitchPercent = Float(playerDict, "pitch", player.PitchPercent);
                }
            }
        }

        return activePlayerCount;
    }

    private IEnumerator FetchBltRuntimeWave(int playerNumber)
    {
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            yield break;
        }

        var url = BltBaseUrl() + "/wave-detail/" + playerNumber.ToString(CultureInfo.InvariantCulture) +
                  "?width=" + BltWaveWidth.ToString(CultureInfo.InvariantCulture) +
                  "&height=" + BltWaveHeight.ToString(CultureInfo.InvariantCulture) +
                  "&scale=" + BltWaveDetailScale.ToString(CultureInfo.InvariantCulture) +
                  "&_=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);

        using (var request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = bltRequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                yield break;
            }

            lock (stateLock)
            {
                var player = GetOrCreatePlayer(playerNumber);
                if (player.BltWavePreview != null && player.BltWavePreview != texture)
                {
                    Destroy(player.BltWavePreview);
                }
                player.BltWavePreview = texture;
            }
        }
    }

    private string BltBaseUrl()
    {
        try
        {
            var uri = new Uri(bltParamsUrl);
            return uri.GetLeftPart(UriPartial.Authority);
        }
        catch
        {
            return "http://127.0.0.1:17081";
        }
    }

    private IEnumerator FetchBltOverviewWave(int playerNumber)
    {
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            yield break;
        }

        var url = BltBaseUrl() + "/wave-preview/" + playerNumber.ToString(CultureInfo.InvariantCulture) +
                  "?width=" + BltWaveWidth.ToString(CultureInfo.InvariantCulture) +
                  "&height=" + BltOverviewWaveHeight.ToString(CultureInfo.InvariantCulture) +
                  "&_=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);

        using (var request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = bltRequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                yield break;
            }

            lock (stateLock)
            {
                var player = GetOrCreatePlayer(playerNumber);
                if (player.BltWaveOverview != null && player.BltWaveOverview != texture)
                {
                    Destroy(player.BltWaveOverview);
                }
                player.BltWaveOverview = texture;
            }
        }
    }

    private void EnsureOfficialBltRunning(bool force)
    {
        if (!useBltBridgeMode)
        {
            return;
        }

        if (!force)
        {
            if (bltServerReachable)
            {
                return;
            }

            if (lastOfficialBltStartAttemptUtc != DateTime.MinValue &&
                lastOfficialBltStartAttemptUtc.AddSeconds(15) > DateTime.UtcNow)
            {
                return;
            }
        }

        if (IsOfficialBltProcessRunning())
        {
            return;
        }

        var executablePath = GetOfficialBltExecutablePath();
        if (string.IsNullOrEmpty(executablePath))
        {
            bltError = "Official Beat Link Trigger app was not found. Install Deep-Symmetry/beat-link-trigger.";
            return;
        }

        lastOfficialBltStartAttemptUtc = DateTime.UtcNow;
        officialBltExecutablePath = executablePath;

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            };
            System.Diagnostics.Process.Start(info);
            AddLog("Started official Beat Link Trigger.");
            bltError = null;
        }
        catch (Exception ex)
        {
            bltError = "Beat Link Trigger start failed: " + ex.Message;
            AddLog(bltError);
        }
    }

    private void StartListeners()
    {
        if (useSimulatedDjLinkMode)
        {
            running = false;
            lastError = null;
            return;
        }

        if (running)
        {
            return;
        }

        var conflictReason = GetDirectDjLinkConflictReason();
        if (!string.IsNullOrEmpty(conflictReason))
        {
            running = false;
            lastError = conflictReason;
            AddLog(conflictReason);
            return;
        }

        running = true;
        lastError = null;

        try
        {
            announcementClient = OpenUdpListener(DeviceAnnouncementPort);
            positionClient = OpenUdpListener(BeatAndPositionPort);
            statusClient = OpenUdpListener(StatusPort);

            announcementThread = StartThread("DJ Link Device Listener", ReceiveDeviceAnnouncements);
            positionThread = StartThread("DJ Link Position Listener", ReceivePrecisePositions);
            statusThread = StartThread("DJ Link Status Listener", ReceiveCdjStatuses);
            linkAnnouncementThread = StartThread("DJ Link Unity Announcement Sender", SendLinkAnnouncements);

            AddLog("Listening on UDP 50000, 50001, 50002 without BLT.");
            Debug.Log("Beat Link Unity direct client started.");
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            AddLog("Failed to start DJ Link listeners: " + ex.Message);
            StopListeners();
        }
    }

    private string GetDirectDjLinkConflictReason()
    {
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                string processName;
                string windowTitle;
                try
                {
                    processName = process.ProcessName ?? string.Empty;
                }
                catch
                {
                    processName = string.Empty;
                }

                try
                {
                    windowTitle = process.MainWindowTitle ?? string.Empty;
                }
                catch
                {
                    windowTitle = string.Empty;
                }

                if (processName.IndexOf("rekordbox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Internal direct DJ Link is unavailable because rekordbox is running. Close rekordbox or switch to External BLT mode.";
                }

                if (processName.IndexOf("beat-link-trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    processName.IndexOf("Beat Link Trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    windowTitle.IndexOf("Beat Link Trigger", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Internal direct DJ Link is unavailable because Beat Link Trigger is running. Close BLT or switch to External BLT mode.";
                }
            }
        }
        catch
        {
        }

        var configuredBltPort = GetConfiguredLoopbackBltPort();
        if (configuredBltPort > 0 && IsTcpPortInUse(configuredBltPort))
        {
            return "Internal direct DJ Link is unavailable because a Beat Link Trigger overlay server is already running on TCP " +
                   configuredBltPort.ToString(CultureInfo.InvariantCulture) + ". Switch to External BLT mode.";
        }

        if (IsUdpPortInUse(DeviceAnnouncementPort) || IsUdpPortInUse(BeatAndPositionPort) || IsUdpPortInUse(StatusPort))
        {
            return "Internal direct DJ Link is unavailable because another DJ Link client is already using UDP 50000-50002.";
        }

        return null;
    }

    private static bool IsUdpPortInUse(int port)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveUdpListeners();
        for (var i = 0; i < listeners.Length; i++)
        {
            if (listeners[i].Port == port)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTcpPortInUse(int port)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveTcpListeners();
        for (var i = 0; i < listeners.Length; i++)
        {
            if (listeners[i].Port == port)
            {
                return true;
            }
        }

        return false;
    }

    private int GetConfiguredLoopbackBltPort()
    {
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            return 17081;
        }

        try
        {
            var uri = new Uri(bltParamsUrl);
            if (uri.IsLoopback)
            {
                return uri.Port;
            }
        }
        catch
        {
        }

        return 17081;
    }

    private static bool IsOfficialBltProcessRunning()
    {
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("Beat Link Trigger"))
            {
                if (process != null)
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private string GetOfficialBltExecutablePath()
    {
        if (!string.IsNullOrEmpty(officialBltExecutablePath) && File.Exists(officialBltExecutablePath))
        {
            return officialBltExecutablePath;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Deep Symmetry", "Beat Link Trigger", "Beat Link Trigger.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Deep Symmetry", "Beat Link Trigger", "Beat Link Trigger.exe")
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private void StopListeners()
    {
        running = false;
        CloseClient(ref announcementClient);
        CloseClient(ref positionClient);
        CloseClient(ref statusClient);
        JoinThread(ref announcementThread);
        JoinThread(ref positionThread);
        JoinThread(ref statusThread);
        JoinThread(ref linkAnnouncementThread);
    }

    private static UdpClient OpenUdpListener(int port)
    {
        var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.ExclusiveAddressUse = false;
        udp.EnableBroadcast = true;
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.ReceiveTimeout = 1000;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        return udp;
    }

    private static Thread StartThread(string name, ThreadStart start)
    {
        var thread = new Thread(start)
        {
            IsBackground = true,
            Name = name
        };
        thread.Start();
        return thread;
    }

    private static void CloseClient(ref UdpClient client)
    {
        var existing = client;
        client = null;
        if (existing != null)
        {
            existing.Close();
        }
    }

    private static void JoinThread(ref Thread thread)
    {
        var existing = thread;
        thread = null;
        if (existing != null && existing.IsAlive)
        {
            existing.Join(300);
        }
    }

    private void ReceiveDeviceAnnouncements()
    {
        ReceiveLoop(announcementClient, ParseDeviceAnnouncement);
    }

    private void ReceivePrecisePositions()
    {
        ReceiveLoop(positionClient, ParsePrecisePosition);
    }

    private void ReceiveCdjStatuses()
    {
        ReceiveLoop(statusClient, ParseCdjStatus);
    }

    private void SendLinkAnnouncements()
    {
        while (running)
        {
            try
            {
                var target = linkBroadcastAddress;
                var local = linkLocalAddress;
                var mac = linkHardwareAddress;
                var client = announcementClient;
                if (target != null && local != null && mac != null && client != null)
                {
                    var packet = BuildLinkAnnouncementPacket(local, mac);
                    client.Send(packet, packet.Length, new IPEndPoint(target, DeviceAnnouncementPort));
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (running)
                {
                    Debug.LogWarning("DJ Link announcement send error: " + ex.Message);
                }
            }

            Thread.Sleep(1500);
        }
    }

    private static byte[] BuildLinkAnnouncementPacket(IPAddress localAddress, byte[] hardwareAddress)
    {
        var packet = new byte[] {
            0x51, 0x73, 0x70, 0x74, 0x31, 0x57, 0x6d, 0x4a, 0x4f, 0x4c, 0x06, 0x00,
            0x55, 0x4e, 0x49, 0x54, 0x59, 0x2d, 0x56, 0x4a, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x36,
            (byte)MetadataRequestingPlayer, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x64
        };

        if (hardwareAddress != null && hardwareAddress.Length >= 6)
        {
            Array.Copy(hardwareAddress, 0, packet, 0x26, 6);
        }
        var addressBytes = localAddress.GetAddressBytes();
        if (addressBytes.Length == 4)
        {
            Array.Copy(addressBytes, 0, packet, 0x2c, 4);
        }
        return packet;
    }

    private void ConfigureLinkAnnouncementTarget(IPAddress remoteAddress)
    {
        if (remoteAddress == null || remoteAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return;
        }
        if (linkBroadcastAddress != null && linkLocalAddress != null && linkHardwareAddress != null)
        {
            return;
        }

        try
        {
            var local = FindLocalAddressFor(remoteAddress);
            var mac = FindHardwareAddress(local);
            if (local == null || mac == null)
            {
                return;
            }

            linkLocalAddress = local;
            linkHardwareAddress = mac;
            linkBroadcastAddress = GuessBroadcastAddress(local, remoteAddress);
            Debug.Log("DJ Link Unity announcements enabled: local=" + local +
                      " broadcast=" + linkBroadcastAddress +
                      " virtualPlayer=" + MetadataRequestingPlayer);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("DJ Link announcement target setup failed: " + ex.Message);
        }
    }

    private static IPAddress FindLocalAddressFor(IPAddress remoteAddress)
    {
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            socket.Connect(remoteAddress, DeviceAnnouncementPort);
            var endpoint = socket.LocalEndPoint as IPEndPoint;
            return endpoint == null ? null : endpoint.Address;
        }
    }

    private static byte[] FindHardwareAddress(IPAddress localAddress)
    {
        if (localAddress == null)
        {
            return null;
        }

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties = networkInterface.GetIPProperties();
            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.Equals(localAddress))
                {
                    var bytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
                    return bytes.Length >= 6 ? bytes : null;
                }
            }
        }
        return null;
    }

    private static IPAddress GuessBroadcastAddress(IPAddress localAddress, IPAddress remoteAddress)
    {
        var local = localAddress.GetAddressBytes();
        var remote = remoteAddress.GetAddressBytes();
        if (local.Length == 4 && remote.Length == 4 && local[0] == 169 && local[1] == 254)
        {
            return new IPAddress(new byte[] { local[0], local[1], 255, 255 });
        }
        return IPAddress.Broadcast;
    }

    private void ReceiveLoop(UdpClient client, Action<byte[], IPEndPoint> parser)
    {
        while (running)
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Any, 0);
                var data = client.Receive(ref endpoint);
                parser(data, endpoint);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.TimedOut && running)
                {
                    SetError(ex.Message);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (running)
                {
                    SetError(ex.Message);
                }
            }
        }
    }

    private void ParseDeviceAnnouncement(byte[] data, IPEndPoint endpoint)
    {
        if (data == null || data.Length != 54 || !HasMagicHeader(data) || data[10] != DeviceAnnouncementType)
        {
            return;
        }

        var deviceNumber = ReadByte(data, 36);
        var deviceName = ReadText(data, 12, 20);
        var hardwareAddress = FormatHardwareAddress(data, 38);
        var peerCount = ReadByte(data, 48);
        var queryDbServerPort = false;
        var now = DateTime.UtcNow;

        if (deviceNumber == MetadataRequestingPlayer && string.Equals(deviceName, "UNITY-VJ", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ConfigureLinkAnnouncementTarget(endpoint.Address);

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(deviceNumber);
            player.DeviceNumber = deviceNumber;
            player.DeviceName = PreferText(deviceName, player.DeviceName);
            player.Address = endpoint.Address.ToString();
            player.HardwareAddress = hardwareAddress;
            player.PeerCount = peerCount;
            player.LastAnnouncementUtc = now;
            player.LastSeenUtc = player.LastAnnouncementUtc;
            player.HasAnnouncement = true;
            if (!player.LoggedAnnouncement)
            {
                player.LoggedAnnouncement = true;
                Debug.Log("DJ Link announcement: player=" + deviceNumber +
                          " name=\"" + PreferText(deviceName, "") +
                          "\" address=" + endpoint.Address);
            }
            queryDbServerPort = player.DbServerPort <= 0 &&
                                !player.DbServerQueryInFlight &&
                                player.LastDbServerQueryUtc.AddSeconds(5) <= now;
            if (queryDbServerPort)
            {
                player.DbServerQueryInFlight = true;
                player.LastDbServerQueryUtc = now;
            }
        }

        if (queryDbServerPort)
        {
            ThreadPool.QueueUserWorkItem(_ => QueryDbServerPort(deviceNumber, endpoint.Address));
        }
    }

    private void QueryDbServerPort(int deviceNumber, IPAddress address)
    {
        var port = -1;
        string error = null;

        for (var attempt = 0; attempt < 4 && running; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(1000 * attempt);
            }

            try
            {
                port = RequestDbServerPort(address);
                error = null;
                break;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(deviceNumber);
            player.DbServerQueryInFlight = false;
            player.DbServerPort = port;
            player.DbServerError = error;
            player.LastDbServerQueryUtc = DateTime.UtcNow;
            if (port > 0 && port < 65535)
            {
                AddLog("Player " + deviceNumber + " dbserver port " + port + ".");
                Debug.Log("Player " + deviceNumber + " dbserver port " + port + ".");
            }
        }

        MaybeQueueMetadataQuery(deviceNumber);
    }

    private static int RequestDbServerPort(IPAddress address)
    {
        Socket socket = null;
        try
        {
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 2000;
            socket.ReceiveTimeout = 2000;
            var endpoint = new IPEndPoint(address, DbServerQueryPort);
            var asyncResult = socket.BeginConnect(endpoint, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(2000))
            {
                throw new TimeoutException("Timed out querying dbserver port.");
            }

            socket.EndConnect(asyncResult);
            socket.Send(DbServerQueryPacket);

            var response = new byte[2];
            var received = 0;
            while (received < response.Length)
            {
                var count = socket.Receive(response, received, response.Length - received, SocketFlags.None);
                if (count <= 0)
                {
                    throw new SocketException();
                }
                received += count;
            }

            return (response[0] << 8) + response[1];
        }
        finally
        {
            if (socket != null)
            {
                socket.Close();
            }
        }
    }

    private void MaybeQueueMetadataQuery(int deviceNumber)
    {
        MetadataQueryArgs args = null;
        var now = DateTime.UtcNow;

        lock (stateLock)
        {
            PlayerState player;
            if (!players.TryGetValue(deviceNumber, out player))
            {
                return;
            }

            var sourcePlayerNumber = player.TrackSourcePlayer > 0 ? player.TrackSourcePlayer : player.DeviceNumber;
            PlayerState sourcePlayer;
            if (!players.TryGetValue(sourcePlayerNumber, out sourcePlayer))
            {
                sourcePlayer = player;
            }

            if (player.RekordboxId <= 0 ||
                player.TrackSourceSlot <= 0 ||
                player.TrackType <= 0 ||
                sourcePlayer.DbServerPort <= 0 ||
                sourcePlayer.DbServerPort >= 65535 ||
                string.IsNullOrEmpty(sourcePlayer.Address) ||
                player.MetadataQueryInFlight)
            {
                return;
            }

            var key = sourcePlayerNumber.ToString(CultureInfo.InvariantCulture) + ":" +
                      player.TrackSourceSlot.ToString(CultureInfo.InvariantCulture) + ":" +
                      player.TrackType.ToString(CultureInfo.InvariantCulture) + ":" +
                      player.RekordboxId.ToString(CultureInfo.InvariantCulture);
            if (player.LastMetadataKey == key &&
                (!string.IsNullOrEmpty(player.BltTitle) || player.LastMetadataQueryUtc.AddSeconds(MetadataQueryRetrySeconds) > now))
            {
                return;
            }

            IPAddress address;
            if (!IPAddress.TryParse(sourcePlayer.Address, out address))
            {
                return;
            }

            player.MetadataQueryInFlight = true;
            player.LastMetadataQueryUtc = now;
            player.LastMetadataKey = key;
            args = new MetadataQueryArgs
            {
                DeckPlayer = deviceNumber,
                SourcePlayer = sourcePlayerNumber,
                SourceDeviceName = sourcePlayer.DeviceName,
                Address = address,
                Port = sourcePlayer.DbServerPort,
                TrackSourceSlot = player.TrackSourceSlot,
                TrackType = player.TrackType,
                RekordboxId = player.RekordboxId,
                Key = key
            };
        }

        if (args != null)
        {
            ThreadPool.QueueUserWorkItem(_ => QueryTrackMetadata(args));
        }
    }

    private void MaybeQueueCueListRefreshes()
    {
        List<MetadataQueryArgs> requests = null;
        var now = DateTime.UtcNow;

        lock (stateLock)
        {
            foreach (var player in players.Values)
            {
                if (!player.HasStatus || !player.BltSeen || player.RekordboxId <= 0 || player.TrackSourceSlot <= 0 || player.TrackType <= 0 ||
                    player.CueListQueryInFlight || player.LastCueListQueryUtc.AddSeconds(CueListRefreshSeconds) > now)
                {
                    continue;
                }

                var sourcePlayerNumber = player.TrackSourcePlayer > 0 ? player.TrackSourcePlayer : player.DeviceNumber;
                PlayerState sourcePlayer;
                if (!players.TryGetValue(sourcePlayerNumber, out sourcePlayer))
                {
                    sourcePlayer = player;
                }

                if (sourcePlayer.DbServerPort <= 0 || sourcePlayer.DbServerPort >= 65535 || string.IsNullOrEmpty(sourcePlayer.Address))
                {
                    continue;
                }

                IPAddress address;
                if (!IPAddress.TryParse(sourcePlayer.Address, out address))
                {
                    continue;
                }

                var key = sourcePlayerNumber.ToString(CultureInfo.InvariantCulture) + ":" +
                          player.TrackSourceSlot.ToString(CultureInfo.InvariantCulture) + ":" +
                          player.TrackType.ToString(CultureInfo.InvariantCulture) + ":" +
                          player.RekordboxId.ToString(CultureInfo.InvariantCulture);

                player.CueListQueryInFlight = true;
                player.LastCueListQueryUtc = now;
                player.LastCueListKey = key;
                if (requests == null)
                {
                    requests = new List<MetadataQueryArgs>();
                }
                requests.Add(new MetadataQueryArgs
                {
                    DeckPlayer = player.DeviceNumber,
                    SourcePlayer = sourcePlayerNumber,
                    SourceDeviceName = sourcePlayer.DeviceName,
                    Address = address,
                    Port = sourcePlayer.DbServerPort,
                    TrackSourceSlot = player.TrackSourceSlot,
                    TrackType = player.TrackType,
                    RekordboxId = player.RekordboxId,
                    Key = key
                });
            }
        }

        if (requests == null)
        {
            return;
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var args = requests[i];
            ThreadPool.QueueUserWorkItem(_ => QueryCueList(args));
        }
    }

    private void QueryCueList(MetadataQueryArgs args)
    {
        List<CueMarker> cueMarkers = null;
        string error = null;
        try
        {
            cueMarkers = RequestCueList(args);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning("Player " + args.DeckPlayer + " cue list refresh error: " + ex.Message);
        }

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(args.DeckPlayer);
            player.CueListQueryInFlight = false;
            player.LastCueListQueryUtc = DateTime.UtcNow;
            if (cueMarkers != null)
            {
                player.CueMarkers = cueMarkers;
            }
        }
    }

    private void QueryTrackMetadata(MetadataQueryArgs args)
    {
        DirectTrackMetadata metadata = null;
        DirectWaveformResult waveform = null;
        List<BeatMarker> beatGrid = null;
        List<CueMarker> cueMarkers = null;
        byte[] albumArt = null;
        string error = null;

        try
        {
            metadata = RequestTrackMetadata(args);
            if (metadata != null && metadata.ArtworkId > 0)
            {
                try
                {
                    albumArt = RequestAlbumArt(args, metadata.ArtworkId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Player " + args.DeckPlayer + " album art error: " + ex.Message);
                }
            }
            try
            {
                waveform = RequestWaveformDetail(args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Player " + args.DeckPlayer + " waveform detail error: " + ex.Message);
            }
            if (SupportsAdvancedDjLinkExtras(args.SourceDeviceName))
            {
                try
                {
                    beatGrid = RequestBeatGrid(args);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Player " + args.DeckPlayer + " beat grid error: " + ex.Message);
                }
                try
                {
                    cueMarkers = RequestCueList(args);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Player " + args.DeckPlayer + " cue list error: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(args.DeckPlayer);
            player.MetadataQueryInFlight = false;
            if (IsIgnorableDjLinkMetadataError(error))
            {
                error = null;
            }
            player.MetadataError = error;
            player.LastMetadataQueryUtc = DateTime.UtcNow;

            if (metadata != null)
            {
                player.BltTitle = PreferText(metadata.Title, player.BltTitle);
                player.BltArtist = PreferText(metadata.Artist, player.BltArtist);
                player.BltAlbum = PreferText(metadata.Album, player.BltAlbum);
                player.BltKey = PreferText(metadata.Key, player.BltKey);
                player.BltGenre = PreferText(metadata.Genre, player.BltGenre);
                player.BltDurationSeconds = metadata.DurationSeconds > 0 ? metadata.DurationSeconds : player.BltDurationSeconds;
                player.ArtworkId = metadata.ArtworkId;
                if (metadata.TempoRaw > 0)
                {
                    var metadataBpm = metadata.TempoRaw / 100f;
                    player.BltTempo = metadataBpm;
                    player.BpmRaw = player.BpmRaw > 0 ? player.BpmRaw : metadata.TempoRaw;
                    ApplyBpm(player, player.Bpm > 0.01f ? player.Bpm : metadataBpm, player.EffectiveBpm > 0.01f ? player.EffectiveBpm : metadataBpm);
                }
                player.BltSeen = true;
                player.LastBltUpdateUtc = DateTime.UtcNow;
                if (albumArt != null && albumArt.Length > 0)
                {
                    player.PendingAlbumArtBytes = albumArt;
                }
                if (waveform != null && waveform.Bytes != null && waveform.Bytes.Length > 0)
                {
                    player.DirectWaveformBytes = waveform.Bytes;
                    player.DirectWaveformStyle = waveform.Style;
                    player.DirectWaveformKey = args.Key;
                }
                player.BeatGrid = beatGrid ?? new List<BeatMarker>();
                player.CueMarkers = cueMarkers ?? new List<CueMarker>();
                AddLog("Player " + args.DeckPlayer + " metadata: " + PreferText(metadata.Title, "(untitled)") +
                       " / " + PreferText(metadata.Artist, "(unknown artist)") + ".");
                Debug.Log("Player " + args.DeckPlayer + " metadata: title=\"" + PreferText(metadata.Title, "") +
                          "\" artist=\"" + PreferText(metadata.Artist, "") +
                          "\" bpm=" + (metadata.TempoRaw / 100f).ToString("0.00", CultureInfo.InvariantCulture) +
                          " duration=" + metadata.DurationSeconds.ToString(CultureInfo.InvariantCulture) +
                          " waveformStyle=" + (waveform == null ? "none" : waveform.Style.ToString()) +
                          " waveformBytes=" + (waveform == null || waveform.Bytes == null ? 0 : waveform.Bytes.Length).ToString(CultureInfo.InvariantCulture) +
                          " artBytes=" + (albumArt == null ? 0 : albumArt.Length).ToString(CultureInfo.InvariantCulture) +
                          " beats=" + (beatGrid == null ? 0 : beatGrid.Count).ToString(CultureInfo.InvariantCulture) +
                          " cues=" + (cueMarkers == null ? 0 : cueMarkers.Count).ToString(CultureInfo.InvariantCulture));
            }
            else if (!string.IsNullOrEmpty(error))
            {
                AddLog("Player " + args.DeckPlayer + " metadata error: " + error);
                Debug.LogWarning("Player " + args.DeckPlayer + " metadata error: " + error);
            }
        }
    }

    private static DirectTrackMetadata RequestTrackMetadata(MetadataQueryArgs args)
    {
        var primaryRequestType = args.TrackType == 1 ? 0x2002 : 0x2202;
        try
        {
            return RequestTrackMetadata(args, primaryRequestType);
        }
        catch (Exception ex)
        {
            if (!IsLegacyNexusDeck(args == null ? null : args.SourceDeviceName))
            {
                throw;
            }

            var fallbackRequestType = primaryRequestType == 0x2002 ? 0x2202 : 0x2002;
            try
            {
                return RequestTrackMetadata(args, fallbackRequestType);
            }
            catch
            {
                throw ex;
            }
        }
    }

    private static DirectTrackMetadata RequestTrackMetadata(MetadataQueryArgs args, int requestType)
    {
        using (var client = new TcpClient(args.Address.AddressFamily))
        {
            client.SendTimeout = 3000;
            client.ReceiveTimeout = 3000;
            var asyncResult = client.BeginConnect(args.Address, args.Port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
            {
                throw new TimeoutException("Timed out connecting to dbserver.");
            }
            client.EndConnect(asyncResult);

            using (var stream = client.GetStream())
            {
                var posingAsPlayer = DbRequestPosingPlayer(args);
                stream.WriteTimeout = 3000;
                stream.ReadTimeout = 3000;
                WriteNumberField(stream, 1, 4);
                var greeting = ReadDbField(stream);
                if (greeting.Kind != DbFieldKind.Number || greeting.Size != 4 || greeting.Number != 1)
                {
                    throw new IOException("Unexpected dbserver greeting.");
                }

                WriteDbMessage(stream, 0xfffffffeL, 0x0000, new DbFieldValue(NumberBytes(posingAsPlayer, 4)));
                var setup = ReadDbMessage(stream);
                if (setup.Type != 0x4000)
                {
                    throw new IOException("Unexpected setup response 0x" + setup.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }

                var rmst = BuildRmst(posingAsPlayer, 1, args.TrackSourceSlot, args.TrackType);
                var transaction = 1L;
                WriteDbMessage(stream, transaction++, requestType,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)));

                var available = ReadDbMessage(stream);
                if (available.Transaction != 1 || available.Type != 0x4000)
                {
                    throw new IOException("Unexpected metadata response 0x" + available.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }
                if (available.Arguments.Count < 2)
                {
                    return null;
                }

                var count = available.Arguments[1].Number;
                if (count == 0 || count == 0xffffffffL)
                {
                    return null;
                }

                var metadata = new DirectTrackMetadata();
                var offset = 0;
                var remaining = (int)Math.Min(count, 256);
                while (remaining > 0)
                {
                    var batch = Math.Min(remaining, 64);
                    var renderTransaction = transaction++;
                    WriteDbMessage(stream, renderTransaction, 0x3000,
                        new DbFieldValue(NumberBytes(rmst, 4)),
                        new DbFieldValue(NumberBytes(offset, 4)),
                        new DbFieldValue(NumberBytes(batch, 4)),
                        new DbFieldValue(NumberBytes(0, 4)),
                        new DbFieldValue(NumberBytes((int)count, 4)),
                        new DbFieldValue(NumberBytes(0, 4)));

                    var header = ReadDbMessage(stream);
                    if (header.Transaction != renderTransaction || header.Type != 0x4001)
                    {
                        throw new IOException("Unexpected render header 0x" + header.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                    }

                    while (true)
                    {
                        var item = ReadDbMessage(stream);
                        if (item.Type == 0x4201)
                        {
                            break;
                        }
                        if (item.Type != 0x4101)
                        {
                            throw new IOException("Unexpected render item 0x" + item.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                        }
                        ApplyMetadataItem(metadata, item);
                    }

                    offset += batch;
                    remaining -= batch;
                }

                try
                {
                    WriteDbMessage(stream, 0xfffffffeL, 0x0100);
                }
                catch
                {
                    // The player closes the session during teardown on some firmware versions.
                }
                return metadata;
            }
        }
    }

    private static DirectWaveformResult RequestWaveformDetail(MetadataQueryArgs args)
    {
        using (var client = new TcpClient(args.Address.AddressFamily))
        {
            client.SendTimeout = 3000;
            client.ReceiveTimeout = 3000;
            var asyncResult = client.BeginConnect(args.Address, args.Port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
            {
                throw new TimeoutException("Timed out connecting to dbserver for waveform.");
            }
            client.EndConnect(asyncResult);

            using (var stream = client.GetStream())
            {
                var posingAsPlayer = DbRequestPosingPlayer(args);
                stream.WriteTimeout = 3000;
                stream.ReadTimeout = 3000;
                WriteNumberField(stream, 1, 4);
                var greeting = ReadDbField(stream);
                if (greeting.Kind != DbFieldKind.Number || greeting.Size != 4 || greeting.Number != 1)
                {
                    throw new IOException("Unexpected dbserver greeting.");
                }

                WriteDbMessage(stream, 0xfffffffeL, 0x0000, new DbFieldValue(NumberBytes(posingAsPlayer, 4)));
                var setup = ReadDbMessage(stream);
                if (setup.Type != 0x4000)
                {
                    throw new IOException("Unexpected setup response 0x" + setup.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }

                var rmst = BuildRmst(posingAsPlayer, 1, args.TrackSourceSlot, args.TrackType);
                WriteDbMessage(stream, 1, 0x2c04,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)),
                    new DbFieldValue(NumberBytes(AnlzFileTagColorWaveformDetail, 4)),
                    new DbFieldValue(NumberBytes(AnlzFileTypeExt, 4)));

                var response = ReadDbMessage(stream);
                if (response.Type == 0x4f02 && response.Arguments.Count >= 4 &&
                    response.Arguments[3].Kind == DbFieldKind.Binary &&
                    response.Arguments[3].Bytes != null &&
                    response.Arguments[3].Bytes.Length > 28)
                {
                    var rawColor = response.Arguments[3].Bytes;
                    var colorDetail = new byte[rawColor.Length - 28];
                    Array.Copy(rawColor, 28, colorDetail, 0, colorDetail.Length);
                    TryTeardownDbSession(stream);
                    return new DirectWaveformResult { Bytes = colorDetail, Style = DirectWaveformStyle.Rgb };
                }

                WriteDbMessage(stream, 2, 0x2904,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)),
                    new DbFieldValue(NumberBytes(0, 4)));

                response = ReadDbMessage(stream);
                if (response.Type == 0x4003)
                {
                    return null;
                }
                if (response.Type != 0x4a02 || response.Arguments.Count < 4 || response.Arguments[3].Kind != DbFieldKind.Binary)
                {
                    throw new IOException("Unexpected waveform response 0x" + response.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }

                TryTeardownDbSession(stream);

                var raw = response.Arguments[3].Bytes;
                const int leadingJunk = 19;
                if (raw == null || raw.Length <= leadingJunk)
                {
                    return null;
                }
                var detail = new byte[raw.Length - leadingJunk];
                Array.Copy(raw, leadingJunk, detail, 0, detail.Length);
                return new DirectWaveformResult { Bytes = detail, Style = DirectWaveformStyle.Blue };
            }
        }
    }

    private static List<BeatMarker> RequestBeatGrid(MetadataQueryArgs args)
    {
        using (var client = new TcpClient(args.Address.AddressFamily))
        {
            client.SendTimeout = 3000;
            client.ReceiveTimeout = 3000;
            var asyncResult = client.BeginConnect(args.Address, args.Port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
            {
                throw new TimeoutException("Timed out connecting to dbserver for beat grid.");
            }
            client.EndConnect(asyncResult);

            using (var stream = client.GetStream())
            {
                SetupDbSession(stream, args);
                var posingAsPlayer = DbRequestPosingPlayer(args);
                var rmst = BuildRmst(posingAsPlayer, 1, args.TrackSourceSlot, args.TrackType);
                WriteDbMessage(stream, 1, 0x2204,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)));

                var response = ReadDbMessage(stream);
                TryTeardownDbSession(stream);
                if (response.Type == 0x4003)
                {
                    return new List<BeatMarker>();
                }
                if (response.Type != 0x4602 || response.Arguments.Count < 4 || response.Arguments[3].Kind != DbFieldKind.Binary)
                {
                    throw new IOException("Unexpected beat grid response 0x" + response.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }
                return ParseBeatGrid(response.Arguments[3].Bytes);
            }
        }
    }

    private static List<CueMarker> RequestCueList(MetadataQueryArgs args)
    {
        using (var client = new TcpClient(args.Address.AddressFamily))
        {
            client.SendTimeout = 3000;
            client.ReceiveTimeout = 3000;
            var asyncResult = client.BeginConnect(args.Address, args.Port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
            {
                throw new TimeoutException("Timed out connecting to dbserver for cue list.");
            }
            client.EndConnect(asyncResult);

            using (var stream = client.GetStream())
            {
                SetupDbSession(stream, args);
                var posingAsPlayer = DbRequestPosingPlayer(args);
                var rmst = BuildRmst(posingAsPlayer, 1, args.TrackSourceSlot, args.TrackType);

                WriteDbMessage(stream, 1, 0x2b04,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)),
                    new DbFieldValue(NumberBytes(0, 4)));
                var response = ReadDbMessage(stream);
                if (response.Type == 0x4e02 && response.Arguments.Count >= 5 &&
                    response.Arguments[3].Kind == DbFieldKind.Binary &&
                    response.Arguments[4].Kind == DbFieldKind.Number)
                {
                    TryTeardownDbSession(stream);
                    return ParseExtendedCueList(response.Arguments[3].Bytes, ClampToInt(response.Arguments[4].Number));
                }

                WriteDbMessage(stream, 2, 0x2104,
                    new DbFieldValue(NumberBytes(rmst, 4)),
                    new DbFieldValue(NumberBytes(args.RekordboxId, 4)));
                response = ReadDbMessage(stream);
                TryTeardownDbSession(stream);
                if (response.Type == 0x4003)
                {
                    return new List<CueMarker>();
                }
                if (response.Type != 0x4702 || response.Arguments.Count < 4 || response.Arguments[3].Kind != DbFieldKind.Binary)
                {
                    throw new IOException("Unexpected cue list response 0x" + response.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }
                return ParseNexusCueList(response.Arguments[3].Bytes);
            }
        }
    }

    private static byte[] RequestAlbumArt(MetadataQueryArgs args, int artworkId)
    {
        try
        {
            var highResolutionArt = RequestAlbumArt(args, artworkId, true);
            if (highResolutionArt != null && highResolutionArt.Length > 0)
            {
                return highResolutionArt;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Player " + args.DeckPlayer + " high-resolution album art error: " + ex.Message);
        }

        return RequestAlbumArt(args, artworkId, false);
    }

    private static byte[] RequestAlbumArt(MetadataQueryArgs args, int artworkId, bool highResolution)
    {
        using (var client = new TcpClient(args.Address.AddressFamily))
        {
            client.SendTimeout = 3000;
            client.ReceiveTimeout = 3000;
            var asyncResult = client.BeginConnect(args.Address, args.Port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
            {
                throw new TimeoutException("Timed out connecting to dbserver for album art.");
            }
            client.EndConnect(asyncResult);

            using (var stream = client.GetStream())
            {
                var posingAsPlayer = DbRequestPosingPlayer(args);
                stream.WriteTimeout = 3000;
                stream.ReadTimeout = 3000;
                WriteNumberField(stream, 1, 4);
                var greeting = ReadDbField(stream);
                if (greeting.Kind != DbFieldKind.Number || greeting.Size != 4 || greeting.Number != 1)
                {
                    throw new IOException("Unexpected dbserver greeting.");
                }

                WriteDbMessage(stream, 0xfffffffeL, 0x0000, new DbFieldValue(NumberBytes(posingAsPlayer, 4)));
                var setup = ReadDbMessage(stream);
                if (setup.Type != 0x4000)
                {
                    throw new IOException("Unexpected setup response 0x" + setup.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }

                var rmst = BuildRmst(posingAsPlayer, 1, args.TrackSourceSlot, args.TrackType);
                if (highResolution)
                {
                    WriteDbMessage(stream, 1, 0x2003,
                        new DbFieldValue(NumberBytes(rmst, 4)),
                        new DbFieldValue(NumberBytes(artworkId, 4)),
                        new DbFieldValue(NumberBytes(1, 4)));
                }
                else
                {
                    WriteDbMessage(stream, 1, 0x2003,
                        new DbFieldValue(NumberBytes(rmst, 4)),
                        new DbFieldValue(NumberBytes(artworkId, 4)));
                }

                var response = ReadDbMessage(stream);
                if (response.Type == 0x4003)
                {
                    return null;
                }
                if (response.Type != 0x4002 || response.Arguments.Count < 4 || response.Arguments[3].Kind != DbFieldKind.Binary)
                {
                    throw new IOException("Unexpected album art response 0x" + response.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
                }

                TryTeardownDbSession(stream);
                return response.Arguments[3].Bytes;
            }
        }
    }

    private static void SetupDbSession(Stream stream, MetadataQueryArgs args)
    {
        var posingAsPlayer = DbRequestPosingPlayer(args);
        stream.WriteTimeout = 3000;
        stream.ReadTimeout = 3000;
        WriteNumberField(stream, 1, 4);
        var greeting = ReadDbField(stream);
        if (greeting.Kind != DbFieldKind.Number || greeting.Size != 4 || greeting.Number != 1)
        {
            throw new IOException("Unexpected dbserver greeting.");
        }

        WriteDbMessage(stream, 0xfffffffeL, 0x0000, new DbFieldValue(NumberBytes(posingAsPlayer, 4)));
        var setup = ReadDbMessage(stream);
        if (setup.Type != 0x4000)
        {
            throw new IOException("Unexpected setup response 0x" + setup.Type.ToString("x4", CultureInfo.InvariantCulture) + ".");
        }
    }

    private static void TryTeardownDbSession(Stream stream)
    {
        try
        {
            WriteDbMessage(stream, 0xfffffffeL, 0x0100);
        }
        catch
        {
        }
    }

    private static int DbRequestPosingPlayer(MetadataQueryArgs args)
    {
        if (args != null && IsLegacyNexusDeck(args.SourceDeviceName))
        {
            return MetadataRequestingPlayer;
        }

        return args != null && args.SourcePlayer > 0 ? args.SourcePlayer : MetadataRequestingPlayer;
    }

    private static void ApplyMetadataItem(DirectTrackMetadata metadata, DbMessage item)
    {
        if (item.Arguments.Count < 7 || item.Arguments[6].Kind != DbFieldKind.Number)
        {
            return;
        }

        var itemType = item.Arguments[6].Number & 0xffff;
        switch (itemType)
        {
            case 0x0004:
                metadata.Title = DbString(item, 3, metadata.Title);
                metadata.ArtworkId = DbInt(item, 8, metadata.ArtworkId);
                break;
            case 0x0007:
                metadata.Artist = DbString(item, 3, metadata.Artist);
                break;
            case 0x0002:
                metadata.Album = DbString(item, 3, metadata.Album);
                break;
            case 0x000b:
                metadata.DurationSeconds = DbInt(item, 1, metadata.DurationSeconds);
                break;
            case 0x000d:
                metadata.TempoRaw = DbInt(item, 1, metadata.TempoRaw);
                break;
            case 0x000f:
                metadata.Key = DbString(item, 3, metadata.Key);
                break;
            case 0x0006:
                metadata.Genre = DbString(item, 3, metadata.Genre);
                break;
        }
    }

    private static string DbString(DbMessage message, int index, string fallback)
    {
        if (index < 0 || index >= message.Arguments.Count || message.Arguments[index].Kind != DbFieldKind.String)
        {
            return fallback;
        }
        return PreferText(message.Arguments[index].Text, fallback);
    }

    private static int DbInt(DbMessage message, int index, int fallback)
    {
        if (index < 0 || index >= message.Arguments.Count || message.Arguments[index].Kind != DbFieldKind.Number)
        {
            return fallback;
        }
        return ClampToInt(message.Arguments[index].Number);
    }

    private static long BuildRmst(int requestingPlayer, int targetMenu, int slot, int trackType)
    {
        return ((long)(requestingPlayer & 0xff) << 24) |
               ((long)(targetMenu & 0xff) << 16) |
               ((long)(slot & 0xff) << 8) |
               (long)(trackType & 0xff);
    }

    private static byte[] NumberBytes(long value, int size)
    {
        var bytes = new byte[size];
        for (var i = size - 1; i >= 0; i--)
        {
            bytes[i] = (byte)(value & 0xff);
            value >>= 8;
        }
        return bytes;
    }

    private static void WriteDbMessage(Stream stream, long transaction, int messageType, params DbFieldValue[] arguments)
    {
        var bytes = new List<byte>();
        AppendNumberField(bytes, 0x872349aeL, 4);
        AppendNumberField(bytes, transaction, 4);
        AppendNumberField(bytes, messageType, 2);
        AppendNumberField(bytes, arguments.Length, 1);

        var argTags = new byte[12];
        for (var i = 0; i < arguments.Length && i < argTags.Length; i++)
        {
            argTags[i] = arguments[i].ArgumentTag;
        }
        AppendBinaryField(bytes, argTags);

        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].ArgumentTag == 0x06)
            {
                AppendNumberField(bytes, BytesToLong(arguments[i].Bytes), arguments[i].Bytes.Length);
            }
            else if (arguments[i].ArgumentTag == 0x03)
            {
                AppendBinaryField(bytes, arguments[i].Bytes);
            }
        }

        var data = bytes.ToArray();
        stream.Write(data, 0, data.Length);
    }

    private static void WriteNumberField(Stream stream, long value, int size)
    {
        var bytes = new List<byte>();
        AppendNumberField(bytes, value, size);
        var data = bytes.ToArray();
        stream.Write(data, 0, data.Length);
    }

    private static void AppendNumberField(List<byte> bytes, long value, int size)
    {
        bytes.Add(size == 1 ? (byte)0x0f : size == 2 ? (byte)0x10 : (byte)0x11);
        var number = NumberBytes(value, size);
        for (var i = 0; i < number.Length; i++)
        {
            bytes.Add(number[i]);
        }
    }

    private static void AppendBinaryField(List<byte> bytes, byte[] value)
    {
        bytes.Add(0x14);
        var length = NumberBytes(value == null ? 0 : value.Length, 4);
        for (var i = 0; i < length.Length; i++)
        {
            bytes.Add(length[i]);
        }
        if (value == null)
        {
            return;
        }
        for (var i = 0; i < value.Length; i++)
        {
            bytes.Add(value[i]);
        }
    }

    private static DbMessage ReadDbMessage(Stream stream)
    {
        var start = ReadDbField(stream);
        if (start.Kind != DbFieldKind.Number || start.Size != 4 || start.Number != 0x872349aeL)
        {
            throw new IOException("Invalid dbserver message start.");
        }

        var transaction = ReadDbField(stream);
        var type = ReadDbField(stream);
        var argumentCount = ReadDbField(stream);
        var argumentTypes = ReadDbField(stream);
        if (transaction.Kind != DbFieldKind.Number ||
            type.Kind != DbFieldKind.Number ||
            argumentCount.Kind != DbFieldKind.Number ||
            argumentTypes.Kind != DbFieldKind.Binary)
        {
            throw new IOException("Invalid dbserver message header.");
        }

        var count = (int)argumentCount.Number;
        var args = new List<DbField>(count);
        DbField lastArg = null;
        for (var i = 0; i < count; i++)
        {
            var argTag = i < argumentTypes.Bytes.Length ? argumentTypes.Bytes[i] : (byte)0;
            DbField arg;
            if (argTag == 0x03 && lastArg != null && lastArg.Kind == DbFieldKind.Number && lastArg.Number == 0)
            {
                arg = new DbField { Kind = DbFieldKind.Binary, Bytes = new byte[0], Size = 0 };
            }
            else
            {
                arg = ReadDbField(stream);
            }
            args.Add(arg);
            lastArg = arg;
        }

        return new DbMessage
        {
            Transaction = transaction.Number,
            Type = (int)type.Number,
            Arguments = args
        };
    }

    private static DbField ReadDbField(Stream stream)
    {
        var tag = ReadByteFromStream(stream);
        if (tag == 0x0f || tag == 0x10 || tag == 0x11)
        {
            var size = tag == 0x0f ? 1 : tag == 0x10 ? 2 : 4;
            var payload = ReadExact(stream, size);
            return new DbField
            {
                Kind = DbFieldKind.Number,
                Size = size,
                Number = BytesToLong(payload)
            };
        }
        if (tag == 0x14)
        {
            var length = (int)BytesToLong(ReadExact(stream, 4));
            return new DbField
            {
                Kind = DbFieldKind.Binary,
                Size = length,
                Bytes = ReadExact(stream, length)
            };
        }
        if (tag == 0x26)
        {
            var chars = (int)BytesToLong(ReadExact(stream, 4));
            var payload = ReadExact(stream, chars * 2);
            var textLength = Math.Max(0, payload.Length - 2);
            var text = Encoding.BigEndianUnicode.GetString(payload, 0, textLength);
            return new DbField
            {
                Kind = DbFieldKind.String,
                Size = payload.Length,
                Text = RepairUtf8AsShiftJisMojibake(text)
            };
        }

        throw new IOException("Unknown dbserver field tag 0x" + tag.ToString("x2", CultureInfo.InvariantCulture) + ".");
    }

    private static string RepairUtf8AsShiftJisMojibake(string text)
    {
        if (string.IsNullOrEmpty(text) || MojibakeScore(text) <= 0)
        {
            return text;
        }

        try
        {
            var shiftJis = Encoding.GetEncoding(932);
            var repaired = Encoding.UTF8.GetString(shiftJis.GetBytes(text));
            if (!string.IsNullOrEmpty(repaired) &&
                !repaired.Contains("\uFFFD") &&
                MojibakeScore(repaired) < MojibakeScore(text))
            {
                return repaired;
            }
        }
        catch
        {
        }

        return text;
    }

    private static int MojibakeScore(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var score = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\uFFFD')
            {
                score += 4;
            }
            else if (c >= '\uFF61' && c <= '\uFF9F')
            {
                score += 2;
            }
            else if ("邵ｺ郢ｧ陞滄明陜ｨ闕ｳ隴・ｫｯ陷ｷ騾｡鬯・ｩ･陋ｯ闔隴夐ｬ滄坡".IndexOf(c) >= 0)
            {
                score += 2;
            }
        }
        return score;
    }

    private static int ReadByteFromStream(Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException();
        }
        return value;
    }

    private static byte[] ReadExact(Stream stream, int count)
    {
        var bytes = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(bytes, offset, count - offset);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }
            offset += read;
        }
        return bytes;
    }

    private static long BytesToLong(byte[] bytes)
    {
        long value = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            value = (value << 8) | bytes[i];
        }
        return value;
    }

    private static long BytesToLongLittleEndian(byte[] bytes, int offset, int size)
    {
        if (bytes == null || offset < 0 || size <= 0 || offset + size > bytes.Length)
        {
            return 0;
        }

        long value = 0;
        for (var i = size - 1; i >= 0; i--)
        {
            value = (value << 8) | bytes[offset + i];
        }
        return value;
    }

    private static List<BeatMarker> ParseBeatGrid(byte[] gridBytes)
    {
        var markers = new List<BeatMarker>();
        if (gridBytes == null || gridBytes.Length <= 20)
        {
            return markers;
        }

        var beatCount = Math.Max(0, (gridBytes.Length - 20) / 16);
        for (var beatNumber = 0; beatNumber < beatCount; beatNumber++)
        {
            var offset = 20 + beatNumber * 16;
            markers.Add(new BeatMarker
            {
                TimeMs = BytesToLongLittleEndian(gridBytes, offset + 4, 4),
                BeatWithinBar = (int)BytesToLongLittleEndian(gridBytes, offset, 2),
                BpmRaw = (int)BytesToLongLittleEndian(gridBytes, offset + 2, 2)
            });
        }
        return markers;
    }

    private static List<CueMarker> ParseNexusCueList(byte[] entryBytes)
    {
        var cues = new List<CueMarker>();
        if (entryBytes == null || entryBytes.Length < 36)
        {
            return cues;
        }

        var entryCount = entryBytes.Length / 36;
        for (var i = 0; i < entryCount; i++)
        {
            var offset = i * 36;
            var cueFlag = entryBytes[offset + 1] & 0xff;
            var hotCueNumber = entryBytes[offset + 2] & 0xff;
            if (cueFlag == 0 && hotCueNumber == 0)
            {
                continue;
            }

            var position = BytesToLongLittleEndian(entryBytes, offset + 12, 4);
            var isLoop = entryBytes[offset] != 0;
            var loopPosition = isLoop ? BytesToLongLittleEndian(entryBytes, offset + 16, 4) : 0;
            cues.Add(new CueMarker
            {
                TimeMs = HalfFrameToTimeMs(position),
                LoopEndMs = isLoop ? HalfFrameToTimeMs(loopPosition) : 0,
                HotCueNumber = hotCueNumber,
                IsLoop = isLoop,
                Label = CueLabel(hotCueNumber, isLoop)
            });
        }
        cues.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return cues;
    }

    private static List<CueMarker> ParseExtendedCueList(byte[] entryBytes, int entryCount)
    {
        var cues = new List<CueMarker>();
        if (entryBytes == null || entryBytes.Length == 0 || entryCount <= 0)
        {
            return cues;
        }

        var offset = 0;
        for (var i = 0; i < entryCount && offset + 18 <= entryBytes.Length; i++)
        {
            var entrySize = (int)BytesToLongLittleEndian(entryBytes, offset, 4);
            if (entrySize <= 0 || offset + entrySize > entryBytes.Length)
            {
                break;
            }

            var hotCueNumber = entryBytes[offset + 4] & 0xff;
            var cueFlag = entryBytes[offset + 6] & 0xff;
            if (cueFlag != 0 || hotCueNumber != 0)
            {
                var timeMs = BytesToLongLittleEndian(entryBytes, offset + 12, 4);
                var isLoop = cueFlag == 2;
                var loopTimeMs = isLoop ? BytesToLongLittleEndian(entryBytes, offset + 16, 4) : 0;
                var comment = "";
                var commentSize = 0;
                if (entrySize > 0x49)
                {
                    commentSize = (int)BytesToLongLittleEndian(entryBytes, offset + 0x48, 2);
                }
                if (commentSize > 2 && offset + 0x4a + commentSize - 2 <= entryBytes.Length)
                {
                    comment = Encoding.Unicode.GetString(entryBytes, offset + 0x4a, commentSize - 2).Trim();
                }

                cues.Add(new CueMarker
                {
                    TimeMs = timeMs,
                    LoopEndMs = loopTimeMs,
                    HotCueNumber = hotCueNumber,
                    IsLoop = isLoop,
                    Label = PreferText(comment, CueLabel(hotCueNumber, isLoop))
                });
            }
            offset += entrySize;
        }
        cues.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return cues;
    }

    private static long HalfFrameToTimeMs(long halfFrame)
    {
        return halfFrame * 100L / 15L;
    }

    private static string CueLabel(int hotCueNumber, bool isLoop)
    {
        if (hotCueNumber > 0)
        {
            var letter = (char)('A' + Mathf.Clamp(hotCueNumber - 1, 0, 25));
            return isLoop ? "Loop " + letter : "Cue " + letter;
        }
        return isLoop ? "Loop" : "Memory";
    }

    private static string HotCueLetter(int hotCueNumber)
    {
        if (hotCueNumber <= 0)
        {
            return "";
        }
        return ((char)('A' + Mathf.Clamp(hotCueNumber - 1, 0, 25))).ToString();
    }

    private void ParsePrecisePosition(byte[] data, IPEndPoint endpoint)
    {
        if (data == null || data.Length != 60 || !HasMagicHeader(data) || data[10] != PrecisePositionType)
        {
            return;
        }

        var deviceNumber = ReadByte(data, 33);
        var deviceName = ReadText(data, 11, 20);
        var trackLengthMs = ReadUInt(data, 36, 4);
        var playbackPositionMs = ReadUInt(data, 40, 4);
        var pitchPercentHundredths = ReadSignedInt(data, 44, 4);
        var pitchRaw = PercentageToPitch(pitchPercentHundredths / 100.0);
        var bpmRaw = (int)ReadUInt(data, 56, 4) * 10;

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(deviceNumber);
            player.DeviceNumber = deviceNumber;
            player.DeviceName = PreferText(deviceName, player.DeviceName);
            player.Address = endpoint.Address.ToString();
            player.TrackLengthMs = ClampToInt(trackLengthMs);
            player.PlaybackPositionMs = ClampToInt(playbackPositionMs);
            player.PitchRaw = pitchRaw;
            player.PitchPercent = PitchToPercentage(pitchRaw);
            if (bpmRaw > 0 && !player.HasStatus)
            {
                player.BpmRaw = bpmRaw;
                var bpm = bpmRaw / 100f;
                ApplyBpm(player, bpm, bpm);
            }
            player.LastPositionUtc = DateTime.UtcNow;
            player.LastSeenUtc = player.LastPositionUtc;
            player.HasPrecisePosition = true;
        }
    }

    private void ParseCdjStatus(byte[] data, IPEndPoint endpoint)
    {
        if (data == null || data.Length < 204 || !HasMagicHeader(data) || data[10] != CdjStatusType)
        {
            return;
        }

        var deviceNumber = ReadByte(data, 33);
        var deviceName = ReadText(data, 11, 20);
        var trackSourcePlayer = ReadByte(data, 40);
        var trackSourceSlot = ReadByte(data, 41);
        var trackType = ReadByte(data, 42);
        var rekordboxId = ReadUInt(data, 44, 4);
        var trackNumber = ReadUInt(data, 50, 2);
        var statusFlags = ReadByte(data, 137);
        var playState1 = ReadByte(data, 123);
        var pitchRaw = (int)ReadUInt(data, 141, 3);
        var bpmRaw = (int)ReadUInt(data, 146, 2);
        var masterHandoffDevice = ReadByte(data, 159);
        var beatNumberRaw = ReadUInt(data, 160, 4);
        var cueCountdown = (int)ReadUInt(data, 164, 2);
        var beatWithinBar = ReadByte(data, 166);
        var packetNumber = ReadUInt(data, 200, 4);
        var hasLoopStatus = data.Length >= 0x1ca;
        var activeLoopStartMs = hasLoopStatus ? ClampToInt(ReadUInt(data, 0x1b6, 4) * 65536L / 1000L) : 0;
        var activeLoopEndMs = hasLoopStatus ? ClampToInt(ReadUInt(data, 0x1be, 4) * 65536L / 1000L) : 0;
        var activeLoopBeats = hasLoopStatus ? ClampToInt(ReadUInt(data, 0x1c8, 2)) : 0;
        var now = DateTime.UtcNow;
        var bpm = bpmRaw / 100f;
        var pitchMultiplier = pitchRaw > 0 ? pitchRaw / 1048576f : 1f;
        var queryDbServerPort = false;

        lock (stateLock)
        {
            var player = GetOrCreatePlayer(deviceNumber);
            var previousBeatNumber = player.BeatNumber;
            var previousRekordboxId = player.RekordboxId;
            player.DeviceNumber = deviceNumber;
            player.DeviceName = PreferText(deviceName, player.DeviceName);
            player.Address = endpoint.Address.ToString();
            player.TrackSourcePlayer = trackSourcePlayer;
            player.TrackSourceSlot = trackSourceSlot;
            player.TrackType = trackType;
            player.RekordboxId = rekordboxId;
            player.TrackNumber = ClampToInt(trackNumber);
            player.StatusFlags = statusFlags;
            player.PitchRaw = pitchRaw;
            player.PitchPercent = PitchToPercentage(pitchRaw);
            player.BpmRaw = bpmRaw;
            ApplyBpm(player, bpm, bpm * pitchMultiplier);
            player.MasterHandoffDevice = masterHandoffDevice == 255 ? 0 : masterHandoffDevice;
            player.BeatNumber = beatNumberRaw == 0xffffffff ? -1 : ClampToInt(beatNumberRaw);
            player.CueCountdown = cueCountdown;
            player.BeatWithinBar = beatWithinBar;
            player.PacketNumber = packetNumber;
            player.IsTempoMaster = (statusFlags & 0x20) != 0;
            player.IsPlaying = data.Length >= 212 ? (statusFlags & 0x40) != 0 : playState1 != 0;
            player.IsSynced = (statusFlags & 0x10) != 0;
            player.IsOnAir = (statusFlags & 0x08) != 0;
            player.IsBpmOnlySynced = (statusFlags & 0x02) != 0;
            player.HasActiveLoop = hasLoopStatus && playState1 == 4 && activeLoopEndMs > activeLoopStartMs;
            player.ActiveLoopStartMs = player.HasActiveLoop ? activeLoopStartMs : 0;
            player.ActiveLoopEndMs = player.HasActiveLoop ? activeLoopEndMs : 0;
            player.ActiveLoopBeats = player.HasActiveLoop ? activeLoopBeats : 0;
            player.HasStatus = true;
            player.LastStatusUtc = now;
            player.LastSeenUtc = now;

            if (!player.LoggedStatus || previousRekordboxId != rekordboxId)
            {
                player.LoggedStatus = true;
                Debug.Log("DJ Link status: player=" + deviceNumber +
                          " source=P" + trackSourcePlayer +
                          " slot=" + trackSourceSlot +
                          " type=" + trackType +
                          " rekordboxId=" + rekordboxId +
                          " bpm=" + bpm.ToString("0.00", CultureInfo.InvariantCulture) +
                          " address=" + endpoint.Address);
            }

            if (previousRekordboxId != rekordboxId)
            {
                player.BltSeen = false;
                player.BltTitle = null;
                player.BltArtist = null;
                player.BltAlbum = null;
                player.BltComment = null;
                player.BltKey = null;
                player.BltGenre = null;
                player.BltColor = null;
                player.BltDurationSeconds = 0;
                player.BltTempo = 0f;
                player.ArtworkId = 0;
                player.PendingAlbumArtBytes = null;
                player.AlbumArtTexture = null;
                player.DirectWaveformBytes = null;
                player.DirectWaveformKey = null;
                player.MetadataError = null;
                player.CueMarkers = new List<CueMarker>();
                player.CueListQueryInFlight = false;
                player.LastCueListKey = null;
            }

            if (player.BeatNumber != previousBeatNumber || player.LastActivitySampleUtc.AddMilliseconds(120) <= now)
            {
                player.LastActivitySampleUtc = now;
                player.PushActivitySample(BuildActivitySample(player));
            }

            queryDbServerPort = player.DbServerPort <= 0 &&
                                !player.DbServerQueryInFlight &&
                                player.LastDbServerQueryUtc.AddSeconds(5) <= now;
            if (queryDbServerPort)
            {
                player.DbServerQueryInFlight = true;
                player.LastDbServerQueryUtc = now;
            }
        }

        if (queryDbServerPort)
        {
            ThreadPool.QueueUserWorkItem(_ => QueryDbServerPort(deviceNumber, endpoint.Address));
        }
        MaybeQueueMetadataQuery(deviceNumber);
    }

    private float BuildActivitySample(PlayerState player)
    {
        if (!player.IsPlaying)
        {
            return 0.08f;
        }

        var beat = player.BeatWithinBar > 0 ? player.BeatWithinBar : Math.Abs(player.BeatNumber % 4) + 1;
        return Mathf.Clamp01(0.18f + (beat / 4f) * 0.75f);
    }
}
