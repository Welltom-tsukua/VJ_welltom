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
    private void LoadVideoIntoLayer(int layerIndex, string path)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex) || string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadVideoIntoLayerState(vjLayers[layerIndex], path, LayerSourceOrigin.FileBrowser, Path.GetFileName(path), true, LayerSlotLabel(layerIndex));
    }

    private void LoadImageIntoLayer(int layerIndex, string path)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex) || string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadImageIntoLayerState(vjLayers[layerIndex], path, LayerSourceOrigin.FileBrowser, Path.GetFileName(path), true, LayerSlotLabel(layerIndex));
    }

    private void ResetLayerEffectState(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        DestroyStepSequencerMediaSlots(layer.Effect);
        ClearStepSequencerMediaState(layer.Effect);
        layer.Effect = new LayerEffectState();
    }

    private void StopLayerVideoPlayback(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        StopAvproLayer(layer);
        StopKlakHapLayer(layer);
        if (layer.Player != null)
        {
            layer.Player.Stop();
            layer.Player.url = "";
        }
    }

    private void LoadVideoIntoLayerState(LayerState layer, string path, LayerSourceOrigin origin, string sourceName, bool resetEffect, string logLabel)
    {
        if (layer == null || string.IsNullOrEmpty(path))
        {
            return;
        }

        var playbackPath = ResolveLayerPlayableMediaPath(layer, path);
        if (string.IsNullOrEmpty(playbackPath) || !File.Exists(playbackPath))
        {
            return;
        }

        if (resetEffect)
        {
            ResetLayerEffectState(layer);
        }

        CleanupLayerYoutubeLocalCache(layer);
        StopAvproLayer(layer);
        StopKlakHapLayer(layer);
        SetLayerStaticTexture(layer, null, false);
        layer.Path = path;
        layer.SourceKind = LayerSourceKind.VideoFile;
        layer.SourceOrigin = origin;
        layer.SourceName = sourceName;
        layer.Enabled = true;
        layer.VideoMode = VideoPlaybackMode.Bpm;
        layer.VideoBaseBpm = 120f;
        layer.VideoBaseBpmInput = "120";
        layer.VideoResyncPending = true;
        layer.VideoStatus = "Loading...";
        layer.DetectedVideoCodec = null;
        layer.VideoLoadToken++;
        layer.ActiveMediaPath = playbackPath;
        layer.MediaAvailabilityState = LayerMediaAvailabilityState.Reloading;
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);
        BeginMediaPrecache(layer, path);

        var loadToken = layer.VideoLoadToken;
        if (string.Equals(Path.GetExtension(playbackPath), ".mov", StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(LoadMovWithHapSupportInLayerState(layer, playbackPath, loadToken, logLabel));
            return;
        }

        StartCoroutine(LoadStandardVideoFileInLayerState(layer, playbackPath, loadToken, logLabel));
    }

    private void LoadImageIntoLayerState(LayerState layer, string path, LayerSourceOrigin origin, string sourceName, bool resetEffect, string logLabel)
    {
        if (layer == null || string.IsNullOrEmpty(path))
        {
            return;
        }

        var playbackPath = ResolveLayerPlayableMediaPath(layer, path);
        if (string.IsNullOrEmpty(playbackPath) || !File.Exists(playbackPath))
        {
            return;
        }

        var texture = LoadImageTexture(playbackPath);
        if (texture == null)
        {
            return;
        }

        if (resetEffect)
        {
            ResetLayerEffectState(layer);
        }

        CleanupLayerYoutubeLocalCache(layer);
        StopLayerVideoPlayback(layer);
        SetLayerStaticTexture(layer, texture, false);
        layer.Path = path;
        layer.SourceKind = LayerSourceKind.Image;
        layer.SourceOrigin = origin;
        layer.SourceName = sourceName;
        layer.Enabled = true;
        layer.VideoStatus = null;
        layer.DetectedVideoCodec = null;
        layer.VideoLoadToken++;
        MarkLayerMediaOnline(layer, playbackPath);
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);
        BeginMediaPrecache(layer, path);
        if (!string.IsNullOrEmpty(logLabel))
        {
            AddLog(logLabel + " image: " + Path.GetFileName(path));
        }
    }

    private void LoadTextIntoLayer(int layerIndex, string text, int fontSize)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        CleanupLayerYoutubeLocalCache(layer);
        StopAvproLayer(layer);
        if (layer.Player != null)
        {
            layer.Player.Stop();
            layer.Player.url = "";
        }
        SetLayerStaticTexture(layer, null, false);

        layer.TextContent = string.IsNullOrEmpty(text) ? "TEXT" : text;
        layer.TextInput = layer.TextContent;
        layer.TextFontSize = Mathf.Clamp(fontSize, 12, 256);
        layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(layer.TextFontName))
        {
            layer.TextFontName = DefaultTextFontName();
        }
        UpdateTextLayerTexture(layer);
        layer.Path = null;
        layer.ActiveMediaPath = null;
        layer.SourceKind = LayerSourceKind.Text;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = "Text";
        ResetLayerEffectState(layer);
        layer.Enabled = true;
        layer.VideoStatus = null;
        layer.MediaAvailabilityState = LayerMediaAvailabilityState.Online;
        SetLayerOfflineHoldTexture(layer, null, false);
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, true);
        AddLog(LayerSlotLabel(layerIndex) + " text source loaded");
    }

    private static void SetLayerStaticTexture(LayerState layer, Texture2D texture, bool ownsTexture)
    {
        if (layer == null)
        {
            return;
        }

        if (layer.OwnsStaticTexture && layer.StaticTexture != null && layer.StaticTexture != texture)
        {
            Destroy(layer.StaticTexture);
        }

        layer.StaticTexture = texture;
        layer.OwnsStaticTexture = ownsTexture;
    }

    private static void SetLayerOfflineHoldTexture(LayerState layer, Texture2D texture, bool ownsTexture)
    {
        if (layer == null)
        {
            return;
        }

        if (layer.OwnsOfflineHoldTexture && layer.OfflineHoldTexture != null && layer.OfflineHoldTexture != texture)
        {
            Destroy(layer.OfflineHoldTexture);
        }

        layer.OfflineHoldTexture = texture;
        layer.OwnsOfflineHoldTexture = ownsTexture;
    }

    private bool SupportsMediaResilience(LayerState layer)
    {
        return layer != null &&
               !string.IsNullOrEmpty(layer.Path) &&
               (layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.Image);
    }

    private string LayerDisplayStatus(LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        switch (layer.MediaAvailabilityState)
        {
            case LayerMediaAvailabilityState.Offline:
                return "Media Offline";
            case LayerMediaAvailabilityState.ReconnectAvailable:
                return "Reconnect / Reload";
            case LayerMediaAvailabilityState.Reloading:
                return "Reload";
            default:
                return layer.VideoStatus;
        }
    }

    private string ResolveLayerCachedMediaPath(LayerState layer, string originalPath)
    {
        if (!string.IsNullOrEmpty(layer != null ? layer.CachedMediaPath : null) && File.Exists(layer.CachedMediaPath))
        {
            return layer.CachedMediaPath;
        }

        if (string.IsNullOrEmpty(originalPath) || string.IsNullOrEmpty(mediaResilienceCacheRoot))
        {
            return null;
        }

        try
        {
            var extension = Path.GetExtension(originalPath);
            var key = StableTextHash((originalPath ?? string.Empty).ToLowerInvariant()).ToString("x8", CultureInfo.InvariantCulture);
            var cachePath = Path.Combine(mediaResilienceCacheRoot, key + extension);
            if (layer != null)
            {
                layer.CachedMediaPath = cachePath;
            }
            return File.Exists(cachePath) ? cachePath : cachePath;
        }
        catch
        {
            return null;
        }
    }

    private string ResolveLayerPlayableMediaPath(LayerState layer, string originalPath)
    {
        if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
        {
            return originalPath;
        }

        var cachePath = ResolveLayerCachedMediaPath(layer, originalPath);
        return !string.IsNullOrEmpty(cachePath) && File.Exists(cachePath) ? cachePath : null;
    }

    private bool IsMediaPrecacheEligible(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(path);
            if (IsImageFile(path))
            {
                return info.Length > 0 && info.Length <= MediaResilienceCacheMaxImageBytes;
            }

            return (IsMp4File(path) || IsMovFile(path)) &&
                   info.Length > 0 &&
                   info.Length <= MediaResilienceCacheMaxVideoBytes;
        }
        catch
        {
            return false;
        }
    }

    private void BeginMediaPrecache(LayerState layer, string originalPath)
    {
        if (layer == null || string.IsNullOrEmpty(originalPath))
        {
            return;
        }

        var cachePath = ResolveLayerCachedMediaPath(layer, originalPath);
        if (string.IsNullOrEmpty(cachePath))
        {
            return;
        }

        if (File.Exists(cachePath))
        {
            layer.CachedMediaPath = cachePath;
            layer.MediaPrecacheRequested = true;
            return;
        }

        if (!IsMediaPrecacheEligible(originalPath))
        {
            return;
        }

        layer.MediaPrecacheRequested = true;
        lock (mediaResilienceCacheLock)
        {
            if (!mediaResilienceCopyInFlight.Add(cachePath))
            {
                return;
            }
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.Copy(originalPath, cachePath, true);
            }
            catch
            {
            }
            finally
            {
                lock (mediaResilienceCacheLock)
                {
                    mediaResilienceCopyInFlight.Remove(cachePath);
                }
            }
        });
    }

    private Texture CaptureLayerSourceSnapshot(LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        if (layer.Player != null && layer.Player.texture != null)
        {
            return layer.Player.texture;
        }

        if (layer.Texture != null)
        {
            return layer.Texture;
        }

        if (layer.StaticTexture != null)
        {
            return layer.StaticTexture;
        }

        if (layer.PreviewTexture != null)
        {
            return layer.PreviewTexture;
        }

        return null;
    }

    private void CaptureLayerOfflineHoldFrame(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        var source = CaptureLayerSourceSnapshot(layer);
        if (source == null)
        {
            return;
        }

        var snapshot = CaptureTextureSnapshot(source, 640, 360);
        if (snapshot != null)
        {
            SetLayerOfflineHoldTexture(layer, snapshot, true);
        }
    }

    private void MarkLayerMediaOffline(LayerState layer)
    {
        if (!SupportsMediaResilience(layer))
        {
            return;
        }

        if (layer.MediaAvailabilityState != LayerMediaAvailabilityState.Offline)
        {
            CaptureLayerOfflineHoldFrame(layer);
        }

        layer.MediaAvailabilityState = LayerMediaAvailabilityState.Offline;
        if (layer.SourceKind == LayerSourceKind.VideoFile)
        {
            StopLayerVideoPlayback(layer);
        }
    }

    private void MarkLayerMediaOnline(LayerState layer, string activePath)
    {
        if (layer == null)
        {
            return;
        }

        layer.ActiveMediaPath = activePath;
        layer.MediaAvailabilityState = LayerMediaAvailabilityState.Online;
        SetLayerOfflineHoldTexture(layer, null, false);
    }

    private void UpdateLayerMediaAvailability()
    {
        if (vjLayers == null)
        {
            return;
        }

        var now = Time.unscaledTime;
        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (!SupportsMediaResilience(layer))
            {
                continue;
            }

            if (layer.NextMediaProbeTime > now)
            {
                continue;
            }

            layer.NextMediaProbeTime = now + MediaResilienceProbeIntervalSeconds;
            var originalExists = !string.IsNullOrEmpty(layer.Path) && File.Exists(layer.Path);
            var playablePath = ResolveLayerPlayableMediaPath(layer, layer.Path);
            var playableExists = !string.IsNullOrEmpty(playablePath) && File.Exists(playablePath);

            if (!originalExists && !playableExists)
            {
                MarkLayerMediaOffline(layer);
                continue;
            }

            if (layer.MediaAvailabilityState == LayerMediaAvailabilityState.Reloading)
            {
                continue;
            }

            if (!originalExists && playableExists)
            {
                var needsReconnect = layer.MediaAvailabilityState == LayerMediaAvailabilityState.Offline ||
                                     layer.MediaAvailabilityState == LayerMediaAvailabilityState.ReconnectAvailable ||
                                     !string.Equals(layer.ActiveMediaPath, playablePath, StringComparison.OrdinalIgnoreCase);
                if (needsReconnect)
                {
                    layer.MediaAvailabilityState = LayerMediaAvailabilityState.ReconnectAvailable;
                    ReloadLayerMediaSource(i, layer);
                }
                continue;
            }

            if (originalExists)
            {
                if (layer.MediaAvailabilityState == LayerMediaAvailabilityState.Offline ||
                    layer.MediaAvailabilityState == LayerMediaAvailabilityState.ReconnectAvailable)
                {
                    layer.MediaAvailabilityState = LayerMediaAvailabilityState.ReconnectAvailable;
                    ReloadLayerMediaSource(i, layer);
                }
                else if (string.Equals(layer.ActiveMediaPath, layer.Path, StringComparison.OrdinalIgnoreCase))
                {
                    layer.MediaAvailabilityState = LayerMediaAvailabilityState.Online;
                }
            }
        }
    }

    private void ReloadLayerMediaSource(int layerIndex, LayerState layer)
    {
        if (!SupportsMediaResilience(layer) || layerIndex < 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(ResolveLayerPlayableMediaPath(layer, layer.Path)))
        {
            MarkLayerMediaOffline(layer);
            return;
        }

        layer.MediaAvailabilityState = LayerMediaAvailabilityState.Reloading;
        var sourceName = string.IsNullOrEmpty(layer.SourceName) ? Path.GetFileName(layer.Path) : layer.SourceName;
        if (layer.SourceKind == LayerSourceKind.Image)
        {
            LoadImageIntoLayerState(layer, layer.Path, layer.SourceOrigin, sourceName, false, LayerSlotLabel(layerIndex));
        }
        else if (layer.SourceKind == LayerSourceKind.VideoFile)
        {
            LoadVideoIntoLayerState(layer, layer.Path, layer.SourceOrigin, sourceName, false, LayerSlotLabel(layerIndex));
        }
    }

    private static void SetTextSourceVisible(LayerState layer, bool visible)
    {
        if (layer == null || layer.TextSource == null || layer.TextSource.Root == null)
        {
            return;
        }
        layer.TextSource.Root.SetActive(visible);
    }

    private void PlayVideoFileInLayer(LayerState layer, string path)
    {
        if (layer == null || layer.Player == null)
        {
            return;
        }

        StopAvproLayer(layer);
        layer.Player.isLooping = true;
        layer.Player.Stop();
        layer.Player.url = ToFileUrl(path);
        layer.Player.playbackSpeed = 1f;
        layer.VideoResyncPending = false;
        layer.ActiveMediaPath = path;
        ApplyLayerAudioOutputState(layer);
        layer.Player.Play();
    }

    private IEnumerator LoadStandardVideoFileInLayer(int layerIndex, string path, int loadToken)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        yield return LoadStandardVideoFileInLayerState(vjLayers[layerIndex], path, loadToken, LayerSlotLabel(layerIndex));
    }

    private IEnumerator LoadStandardVideoFileInLayerState(LayerState layer, string path, int loadToken, string logLabel)
    {
        if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
        {
            yield break;
        }

        layer.VideoStatus = "Loading...";
        yield return null;

        var deferDeadline = Time.realtimeSinceStartup + 1.25f;
        while (HasCriticalSourcePlayback() && Time.realtimeSinceStartup < deferDeadline)
        {
            if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
            {
                yield break;
            }

            layer.VideoStatus = "Loading...";
            yield return new WaitForSecondsRealtime(0.05f);
        }

        if (!TryGetCurrentVideoLoadState(layer, path, loadToken) || layer.Player == null)
        {
            yield break;
        }

        var player = layer.Player;
        var prepared = false;
        var error = string.Empty;
        VideoPlayer.EventHandler preparedHandler = _ => prepared = true;
        VideoPlayer.ErrorEventHandler errorHandler = (_, message) => error = message;

        player.prepareCompleted += preparedHandler;
        player.errorReceived += errorHandler;
        player.isLooping = true;
        player.Stop();
        player.url = ToFileUrl(path);
        player.playbackSpeed = 1f;
        ApplyLayerAudioOutputState(layer);
        player.Prepare();

        var start = Time.realtimeSinceStartup;
        while (!prepared && string.IsNullOrEmpty(error) && Time.realtimeSinceStartup - start < 10f)
        {
            if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
            {
                player.prepareCompleted -= preparedHandler;
                player.errorReceived -= errorHandler;
                yield break;
            }

            layer.VideoStatus = "Loading...";
            yield return null;
        }

        player.prepareCompleted -= preparedHandler;
        player.errorReceived -= errorHandler;

        if (!TryGetCurrentVideoLoadState(layer, path, loadToken) || layer.Player == null)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(error))
        {
            if (string.IsNullOrEmpty(ResolveLayerPlayableMediaPath(layer, layer.Path)))
            {
                MarkLayerMediaOffline(layer);
            }
            else
            {
                layer.VideoStatus = FirstNonEmptyLine(error) ?? "Video load failed.";
            }
            AddLog("Video load failed: " + PreferText(layer.VideoStatus, "Media Offline") + " file=" + Path.GetFileName(path));
            yield break;
        }

        layer.VideoResyncPending = false;
        layer.VideoStatus = null;
        MarkLayerMediaOnline(layer, path);
        layer.Player.Play();
        if (!string.IsNullOrEmpty(logLabel))
        {
            AddLog(logLabel + " loaded: " + Path.GetFileName(path));
        }
    }

    private void ResyncVideoLayerToCurrentBeat(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        layer.VideoResyncPending = true;
        RestartVideoLayerPlayback(layer);
    }

    private void RestartVideoLayerPlayback(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
        {
            SetReflectionValue(layer.KlakHapPlayer, "loop", true);
            SetReflectionValue(layer.KlakHapPlayer, "speed", 1f);
            SetReflectionValue(layer.KlakHapPlayer, "time", 0f);
            SetReflectionValue(layer.KlakHapPlayer, "enabled", true);
            return;
        }

        if (layer.Player == null)
        {
            return;
        }

        var player = layer.Player;
        player.isLooping = true;
        player.playbackSpeed = 1f;
        player.Stop();
        player.Play();
    }

    private IEnumerator LoadMovWithHapSupport(int layerIndex, string path, int loadToken)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        yield return LoadMovWithHapSupportInLayerState(vjLayers[layerIndex], path, loadToken, LayerSlotLabel(layerIndex));
    }

    private IEnumerator LoadMovWithHapSupportInLayerState(LayerState layer, string path, int loadToken, string logLabel)
    {
        if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
        {
            yield break;
        }

        layer.VideoStatus = "Loading...";
        yield return null;

        var probeDone = 0;
        var isHapCodec = false;
        string klakStatus = "KlakHap skipped.";
        string probedCodec = null;
        string probeStatus = null;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                isHapCodec = TryProbeMovCodecIsHap(path, out probedCodec, out probeStatus);
            }
            finally
            {
                Interlocked.Exchange(ref probeDone, 1);
            }
        });

        while (Volatile.Read(ref probeDone) == 0)
        {
            if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
            {
                yield break;
            }

            layer.VideoStatus = "Loading...";
            yield return null;
        }

        if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
        {
            yield break;
        }

        layer.DetectedVideoCodec = string.IsNullOrEmpty(probedCodec) ? null : probedCodec;

        var deferDeadline = Time.realtimeSinceStartup + 1.25f;
        while (HasCriticalSourcePlayback() && Time.realtimeSinceStartup < deferDeadline)
        {
            if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
            {
                yield break;
            }

            layer.VideoStatus = "Loading...";
            yield return new WaitForSecondsRealtime(0.05f);
        }

        if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
        {
            yield break;
        }

        layer.VideoStatus = "Loading...";
        yield return null;

        if (isHapCodec && TryOpenKlakHapVideoInLayer(layer, path, out klakStatus))
        {
            layer.VideoStatus = "Hap/MOV via KlakHap";
            MarkLayerMediaOnline(layer, path);
            if (!string.IsNullOrEmpty(logLabel))
            {
                AddLog(logLabel + " Hap/MOV loaded via KlakHap: " + Path.GetFileName(path) + " codec=" + probedCodec);
            }
            yield break;
        }
        if (!isHapCodec)
        {
            klakStatus = string.IsNullOrEmpty(probeStatus)
                ? "KlakHap only supports Hap MOV" + (string.IsNullOrEmpty(probedCodec) ? "." : " (detected: " + probedCodec + ").")
                : probeStatus;
        }

        if (!TryGetCurrentVideoLoadState(layer, path, loadToken))
        {
            yield break;
        }

        if (string.IsNullOrEmpty(ResolveLayerPlayableMediaPath(layer, layer.Path)))
        {
            MarkLayerMediaOffline(layer);
        }
        else
        {
            layer.VideoStatus = FirstNonEmptyLine(klakStatus ?? "") ?? "KlakHap could not open this MOV.";
        }
        AddLog("Hap/MOV KlakHap open failed: " + PreferText(layer.VideoStatus, "Media Offline") + " file=" + Path.GetFileName(path));
    }

    private static bool TryGetCurrentVideoLoadState(LayerState layer, string path, int loadToken)
    {
        return layer != null &&
               layer.VideoLoadToken == loadToken &&
               (string.Equals(layer.Path, path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer.ActiveMediaPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetCurrentVideoLoadLayer(int layerIndex, string path, int loadToken, out LayerState layer)
    {
        layer = null;
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return false;
        }

        layer = vjLayers[layerIndex];
        return TryGetCurrentVideoLoadState(layer, path, loadToken);
    }

    private IEnumerator TryMovDirectThenFallback(int layerIndex, string path, int loadToken)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        var layer = vjLayers[layerIndex];
        var player = layer.Player;
        if (player == null)
        {
            yield break;
        }

        layer.VideoStatus = "Trying MOV direct playback...";
        var prepared = false;
        var error = "";
        VideoPlayer.EventHandler preparedHandler = source => prepared = true;
        VideoPlayer.ErrorEventHandler errorHandler = (source, message) => error = message;

        player.prepareCompleted += preparedHandler;
        player.errorReceived += errorHandler;
        player.Stop();
        player.isLooping = true;
        player.url = ToFileUrl(path);
        player.Prepare();

        var start = Time.realtimeSinceStartup;
        while (!prepared && string.IsNullOrEmpty(error) && Time.realtimeSinceStartup - start < 5f)
        {
            yield return null;
        }

        player.prepareCompleted -= preparedHandler;
        player.errorReceived -= errorHandler;

        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        layer = vjLayers[layerIndex];
        if (layer.VideoLoadToken != loadToken || !string.Equals(layer.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (prepared && string.IsNullOrEmpty(error))
        {
            layer.VideoStatus = "MOV direct playback";
            player.Play();
            AddLog(LayerSlotLabel(layerIndex) + " MOV direct loaded: " + Path.GetFileName(path));
            yield break;
        }

        layer.VideoStatus = string.IsNullOrEmpty(error) ? "MOV direct playback timed out. Converting..." : "MOV direct failed. Converting...";
        if (!string.IsNullOrEmpty(error))
        {
            AddLog("MOV direct playback failed: " + error);
        }
        yield return ConvertMovAndLoadVideo(layerIndex, path, loadToken);
    }

    private IEnumerator ConvertMovAndLoadVideo(int layerIndex, string path, int loadToken)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        var layer = vjLayers[layerIndex];
        var ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg))
        {
            layer.VideoStatus = "MOV needs ffmpeg.exe in rebuild/tools.";
            AddLog("MOV load failed: ffmpeg.exe not found.");
            yield break;
        }

        var cachePath = MovCachePath(path);
        if (File.Exists(cachePath))
        {
            layer.VideoStatus = "MOV cache loaded";
            PlayVideoFileInLayer(layer, cachePath);
            AddLog(LayerSlotLabel(layerIndex) + " MOV cache loaded: " + Path.GetFileName(path));
            yield break;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
        layer.VideoStatus = "Converting MOV for playback...";
        System.Diagnostics.Process process = null;
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = "-y -nostdin -hide_banner -loglevel error -i " + QuoteProcessArgument(path) +
                            " -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart " +
                            QuoteProcessArgument(cachePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            process = System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            layer.VideoStatus = "MOV convert error: " + ex.Message;
            yield break;
        }

        while (process != null && !process.HasExited)
        {
            yield return null;
        }

        var stderr = process == null ? "" : process.StandardError.ReadToEnd();
        var exitCode = process == null ? -1 : process.ExitCode;
        if (process != null)
        {
            process.Dispose();
        }

        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }
        layer = vjLayers[layerIndex];
        if (layer.VideoLoadToken != loadToken || !string.Equals(layer.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (exitCode != 0 || !File.Exists(cachePath))
        {
            layer.VideoStatus = "MOV convert failed: " + FirstNonEmptyLine(stderr);
            yield break;
        }

        layer.VideoStatus = "MOV converted";
        PlayVideoFileInLayer(layer, cachePath);
        AddLog(LayerSlotLabel(layerIndex) + " MOV loaded: " + Path.GetFileName(path));
    }

    private static string MovCachePath(string path)
    {
        var info = new FileInfo(path);
        var key = StableTextHash(path + "|" + info.Length.ToString(CultureInfo.InvariantCulture) + "|" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)).ToString("x8", CultureInfo.InvariantCulture);
        return Path.Combine(Application.persistentDataPath, "mov-cache", key + ".mp4");
    }

    private bool TryProbeMovCodecIsHap(string path, out string codecName, out string status)
    {
        codecName = null;
        status = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            status = "MOV codec probe failed: file not found.";
            return false;
        }

        var ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg))
        {
            status = "MOV codec probe skipped: ffmpeg.exe not found.";
            return false;
        }

        try
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                   {
                       FileName = ffmpeg,
                       Arguments = "-hide_banner -i " + QuoteProcessArgument(path),
                       UseShellExecute = false,
                       CreateNoWindow = true,
                       RedirectStandardError = true,
                       RedirectStandardOutput = true
                   }))
            {
                if (process == null)
                {
                    status = "MOV codec probe failed: process start failed.";
                    return false;
                }

                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                var match = Regex.Match(stderr ?? "", @"Video:\s*([^,\r\n]+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    status = "MOV codec probe failed: video stream not found.";
                    return false;
                }

                codecName = match.Groups[1].Value.Trim();
                var lowered = codecName.ToLowerInvariant();
                if (!lowered.StartsWith("hap", StringComparison.Ordinal))
                {
                    status = "KlakHap only supports Hap MOV" + (string.IsNullOrEmpty(codecName) ? "." : " (detected: " + codecName + ").");
                    return false;
                }

                if (!TryValidateMovCanDecodeSingleFrame(ffmpeg, path, out var decodeStatus))
                {
                    status = decodeStatus;
                    return false;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            status = "MOV codec probe failed: " + ex.Message;
            return false;
        }
    }

    private bool TryValidateMovCanDecodeSingleFrame(string ffmpeg, string path, out string status)
    {
        status = null;
        if (string.IsNullOrEmpty(ffmpeg) || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            status = "MOV decode probe failed: invalid path.";
            return false;
        }

        try
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                   {
                       FileName = ffmpeg,
                       Arguments = "-v error -i " + QuoteProcessArgument(path) + " -frames:v 1 -f null -",
                       UseShellExecute = false,
                       CreateNoWindow = true,
                       RedirectStandardError = true,
                       RedirectStandardOutput = true
                   }))
            {
                if (process == null)
                {
                    status = "MOV decode probe failed: process start failed.";
                    return false;
                }

                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    var line = FirstNonEmptyLine(stderr);
                    status = "MOV decode probe failed: " + (string.IsNullOrEmpty(line) ? "ffmpeg returned error." : line);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    var line = FirstNonEmptyLine(stderr);
                    if (!string.IsNullOrEmpty(line))
                    {
                        status = "MOV decode probe failed: " + line;
                        return false;
                    }
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            status = "MOV decode probe failed: " + ex.Message;
            return false;
        }
    }

    private bool TryOpenKlakHapVideoInLayer(LayerState layer, string path, out string status)
    {
        status = null;
        if (layer == null || layer.Player == null || layer.Texture == null || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            status = "Invalid video path.";
            return false;
        }

        var playerType = FindKlakHapPlayerType();
        if (playerType == null)
        {
            status = "KlakHap is not installed.";
            return false;
        }

        try
        {
            var component = layer.KlakHapPlayer;
            if (component == null || component.GetType() != playerType)
            {
                component = layer.Player.gameObject.GetComponent(playerType) as Component;
                if (component == null)
                {
                    component = layer.Player.gameObject.AddComponent(playerType);
                }
            }

            if (component == null)
            {
                status = "Could not create KlakHap player.";
                return false;
            }

            layer.Player.Stop();
            layer.Player.url = "";
            SetReflectionValue(component, "targetTexture", layer.Texture);
            SetReflectionValue(component, "loop", true);
            SetReflectionValue(component, "speed", 1f);
            SetReflectionValue(component, "time", 0f);
            TryAssignKlakPathMode(component, "LocalFileSystem");
            TryAssignKlakPathMode(component, "AbsolutePath");
            SetReflectionValue(component, "filePath", path);
            SetReflectionValue(component, "path", path);

            if (!TryInvokeKlakOpen(component, path))
            {
                if (component is Behaviour missingOpenBehaviour)
                {
                    missingOpenBehaviour.enabled = false;
                }
                Destroy(component);
                if (ReferenceEquals(layer.KlakHapPlayer, component))
                {
                    layer.KlakHapPlayer = null;
                }
                status = "KlakHap open API was not found.";
                return false;
            }

            layer.KlakHapPlayer = component;
            layer.UsesKlakHapVideo = true;
            layer.UsesAvproVideo = false;
            return true;
        }
        catch (TargetInvocationException ex)
        {
            StopKlakHapLayer(layer);
            status = "KlakHap error: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            StopKlakHapLayer(layer);
            status = "KlakHap error: " + ex.Message;
            return false;
        }
    }

    private bool TryOpenAvproVideoInLayer(LayerState layer, string path, out string status)
    {
        status = null;
        if (layer == null || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            status = "Invalid video path.";
            return false;
        }

        var mediaPlayerType = FindAvproMediaPlayerType();
        if (mediaPlayerType == null || avproMediaPathType == null)
        {
            status = "AVPro Video Ultra is not installed.";
            return false;
        }

        try
        {
            if (layer.Player != null)
            {
                layer.Player.Stop();
                layer.Player.url = "";
            }

            var component = layer.AvproMediaPlayer;
            if (component == null || component.GetType() != mediaPlayerType)
            {
                component = layer.Player != null
                    ? layer.Player.gameObject.GetComponent(mediaPlayerType) as Component
                    : null;
                if (component == null && layer.Player != null)
                {
                    component = layer.Player.gameObject.AddComponent(mediaPlayerType);
                }
            }

            if (component == null)
            {
                status = "Could not create AVPro MediaPlayer.";
                return false;
            }

            SetReflectionProperty(component, "AutoOpen", false);
            SetReflectionProperty(component, "AutoStart", true);
            SetReflectionProperty(component, "Loop", true);
            SetReflectionProperty(component, "TextureFilterMode", FilterMode.Bilinear);
            SetReflectionProperty(component, "TextureWrapMode", TextureWrapMode.Clamp);

            var pathType = Enum.Parse(avproMediaPathType, "AbsolutePathOrURL");
            var openMedia = mediaPlayerType.GetMethod("OpenMedia", new[] { avproMediaPathType, typeof(string), typeof(bool) });
            if (openMedia == null)
            {
                status = "AVPro OpenMedia(pathType, path, autoPlay) API was not found.";
                return false;
            }

            var result = openMedia.Invoke(component, new[] { pathType, path, true });
            if (result is bool && !(bool)result)
            {
                status = "AVPro refused this media.";
                return false;
            }

            layer.AvproMediaPlayer = component;
            layer.UsesAvproVideo = true;
            layer.UsesKlakHapVideo = false;
            return true;
        }
        catch (TargetInvocationException ex)
        {
            status = "AVPro error: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            status = "AVPro error: " + ex.Message;
            return false;
        }
    }

    private Type FindKlakHapPlayerType()
    {
        if (klakHapChecked)
        {
            return klakHapPlayerType;
        }

        klakHapChecked = true;
        klakHapPlayerType = FindTypeInLoadedAssemblies("Klak.Hap.HapPlayer");
        klakHapPathModeType = klakHapPlayerType == null
            ? null
            : klakHapPlayerType.GetNestedType("PathMode", BindingFlags.Public | BindingFlags.NonPublic);
        return klakHapPlayerType;
    }

    private bool TryAssignKlakPathMode(object target, string enumName)
    {
        if (target == null || klakHapPathModeType == null || string.IsNullOrEmpty(enumName) || !Enum.IsDefined(klakHapPathModeType, enumName))
        {
            return false;
        }

        return SetReflectionValue(target, "pathMode", Enum.Parse(klakHapPathModeType, enumName));
    }

    private bool TryInvokeKlakOpen(object target, string path)
    {
        if (target == null)
        {
            return false;
        }

        var type = target.GetType();
        var openNoArgs = type.GetMethod("Open", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (openNoArgs != null)
        {
            openNoArgs.Invoke(target, null);
            return true;
        }

        if (klakHapPathModeType != null)
        {
            var mode = Enum.IsDefined(klakHapPathModeType, "LocalFileSystem")
                ? Enum.Parse(klakHapPathModeType, "LocalFileSystem")
                : Enum.GetValues(klakHapPathModeType).GetValue(0);
            var openPathMode = type.GetMethod("Open", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), klakHapPathModeType }, null);
            if (openPathMode != null)
            {
                openPathMode.Invoke(target, new[] { (object)path, mode });
                return true;
            }

            var openModePath = type.GetMethod("Open", BindingFlags.Instance | BindingFlags.Public, null, new[] { klakHapPathModeType, typeof(string) }, null);
            if (openModePath != null)
            {
                openModePath.Invoke(target, new[] { mode, (object)path });
                return true;
            }
        }

        return false;
    }

    private Type FindAvproMediaPlayerType()
    {
        if (avproChecked)
        {
            return avproMediaPlayerType;
        }

        avproChecked = true;
        avproMediaPlayerType = FindTypeInLoadedAssemblies("RenderHeads.Media.AVProVideo.MediaPlayer");
        avproMediaPathType = FindTypeInLoadedAssemblies("RenderHeads.Media.AVProVideo.MediaPathType");
        if (avproMediaPathType == null)
        {
            avproMediaPathType = FindTypeInLoadedAssemblies("RenderHeads.Media.AVProVideo.MediaLocation");
        }
        return avproMediaPlayerType;
    }

    private static Type FindTypeInLoadedAssemblies(string fullName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var type = assemblies[i].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }

    private static void SetReflectionProperty(object target, string propertyName, object value)
    {
        if (target == null)
        {
            return;
        }

        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        if (value != null && !property.PropertyType.IsInstanceOfType(value))
        {
            if (property.PropertyType.IsEnum && value is string)
            {
                value = Enum.Parse(property.PropertyType, (string)value);
            }
            else
            {
                value = Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
            }
        }

        property.SetValue(target, value, null);
    }

    private static bool SetReflectionValue(object target, string memberName, object value)
    {
        if (target == null || string.IsNullOrEmpty(memberName))
        {
            return false;
        }

        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, CoerceReflectionValue(property.PropertyType, value), null);
            return true;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (field != null)
        {
            field.SetValue(target, CoerceReflectionValue(field.FieldType, value));
            return true;
        }

        if (string.Equals(memberName, "enabled", StringComparison.OrdinalIgnoreCase) && target is Behaviour behaviour)
        {
            behaviour.enabled = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static object GetReflectionValue(object target, string memberName)
    {
        if (target == null || string.IsNullOrEmpty(memberName))
        {
            return null;
        }

        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property != null && property.CanRead)
        {
            return property.GetValue(target, null);
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (field != null)
        {
            return field.GetValue(target);
        }

        return null;
    }

    private static bool TryInvokeReflectionMethod(object target, string methodName, params object[] args)
    {
        if (target == null || string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        var type = target.GetType();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (!string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != (args == null ? 0 : args.Length))
            {
                continue;
            }

            var invokeArgs = args == null ? Array.Empty<object>() : new object[args.Length];
            var compatible = true;
            for (var j = 0; j < parameters.Length; j++)
            {
                try
                {
                    invokeArgs[j] = CoerceReflectionValue(parameters[j].ParameterType, args[j]);
                }
                catch
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible)
            {
                continue;
            }

            method.Invoke(target, invokeArgs);
            return true;
        }

        return false;
    }

    private static Type ResolveLightBeamPerformanceType(params string[] fullNames)
    {
        if (fullNames == null)
        {
            return null;
        }

        for (var i = 0; i < fullNames.Length; i++)
        {
            var fullName = fullNames[i];
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            var type = Type.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var assembly = assemblies[i];
            if (assembly == null)
            {
                continue;
            }

            for (var j = 0; j < fullNames.Length; j++)
            {
                var fullName = fullNames[j];
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                var plainName = fullName.Split(',')[0].Trim();
                var type = assembly.GetType(plainName, false, true);
                if (type != null)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static object CoerceReflectionValue(Type targetType, object value)
    {
        if (targetType == null || value == null || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            if (value is string enumText)
            {
                return Enum.Parse(targetType, enumText);
            }
            return Enum.ToObject(targetType, value);
        }

        if (targetType == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private void StopAvproLayer(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        StopKlakHapLayer(layer);
        layer.UsesAvproVideo = false;
        if (layer.AvproMediaPlayer == null)
        {
            return;
        }

        try
        {
            var type = layer.AvproMediaPlayer.GetType();
            var close = type.GetMethod("CloseMedia", Type.EmptyTypes);
            if (close != null)
            {
                close.Invoke(layer.AvproMediaPlayer, null);
                return;
            }

            var pause = type.GetMethod("Pause", Type.EmptyTypes);
            if (pause != null)
            {
                pause.Invoke(layer.AvproMediaPlayer, null);
            }
        }
        catch (Exception ex)
        {
            AddLog("AVPro stop error: " + ex.Message);
        }
    }

    private void StopKlakHapLayer(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        layer.UsesKlakHapVideo = false;
        if (layer.KlakHapPlayer == null)
        {
            return;
        }

        try
        {
            if (layer.KlakHapPlayer is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }
            Destroy(layer.KlakHapPlayer);
        }
        catch (Exception ex)
        {
            AddLog("KlakHap stop error: " + ex.Message);
        }
        finally
        {
            layer.KlakHapPlayer = null;
        }
    }

    private Texture AvproLayerTexture(LayerState layer)
    {
        if (layer == null || !layer.UsesAvproVideo || layer.AvproMediaPlayer == null)
        {
            return null;
        }

        try
        {
            var producerProperty = layer.AvproMediaPlayer.GetType().GetProperty("TextureProducer", BindingFlags.Instance | BindingFlags.Public);
            var producer = producerProperty == null ? null : producerProperty.GetValue(layer.AvproMediaPlayer, null);
            if (producer == null)
            {
                return null;
            }

            var getTexture = producer.GetType().GetMethod("GetTexture", new[] { typeof(int) });
            if (getTexture != null)
            {
                return getTexture.Invoke(producer, new object[] { 0 }) as Texture;
            }

            getTexture = producer.GetType().GetMethod("GetTexture", Type.EmptyTypes);
            return getTexture == null ? null : getTexture.Invoke(producer, null) as Texture;
        }
        catch
        {
            return null;
        }
    }

    private void LoadCaptureIntoLayer(int layerIndex, int deviceIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex) || captureDevices == null || deviceIndex < 0 || deviceIndex >= captureDevices.Length)
        {
            return;
        }

        if (captureDeviceIndex != deviceIndex || captureTexture == null)
        {
            captureDeviceIndex = deviceIndex;
            StartCapturePreview();
        }

        var layer = vjLayers[layerIndex];
        CleanupLayerYoutubeLocalCache(layer);
        StopAvproLayer(layer);
        layer.Player.Stop();
        SetLayerStaticTexture(layer, null, false);
        layer.Path = null;
        layer.SourceKind = LayerSourceKind.Capture;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = "HDMI Capture " + (deviceIndex + 1).ToString(CultureInfo.InvariantCulture);
        ResetLayerEffectState(layer);
        layer.CaptureDeviceIndex = deviceIndex;
        layer.VideoStatus = null;
        layer.Enabled = true;
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);
        AddLog(LayerSlotLabel(layerIndex) + " source: " + layer.SourceName);
    }

    private void LoadGeneratorIntoLayer(int layerIndex, Texture texture, string sourceName)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        CleanupLayerYoutubeLocalCache(layer);
        StopAvproLayer(layer);
        layer.Player.Stop();
        SetLayerStaticTexture(layer, null, false);
        layer.Path = null;
        layer.SourceKind = LayerSourceKind.Generator3D;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = "3D Object " + sourceName;
        ResetLayerEffectState(layer);
        layer.VideoStatus = null;
        layer.Enabled = true;
        ResetGeneratorToDefaultScene(layer.Generator);
        ClearGeneratorLayerTextureSource(layer.Generator);
        if (layer.Generator != null && string.IsNullOrEmpty(layer.Generator.FaceImageFolderPath))
        {
            layer.Generator.FaceImageFolderPath = PlayerPrefs.GetString(GeneratorFaceFolderPrefKey, "");
            layer.Generator.FaceImageFolderInput = layer.Generator.FaceImageFolderPath;
            RefreshGeneratorFaceImageFolder(layer.Generator);
        }
        ApplyGeneratorTexture(layer.Generator, texture);
        SetGeneratorVisible(layer.Generator, layer.Generator.ScreenVisible);
        SetTextSourceVisible(layer, false);
        AddLog(LayerSlotLabel(layerIndex) + " source: " + layer.SourceName);
    }

    private void LoadYoutubeSourceIntoLayer(int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer.SourceKind != LayerSourceKind.YouTube)
        {
            CleanupLayerYoutubeLocalCache(layer);
        }
        StopAvproLayer(layer);
        layer.Player.Stop();
        layer.Player.url = "";
        SetLayerStaticTexture(layer, null, false);
        layer.Path = null;
        layer.SourceKind = LayerSourceKind.YouTube;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = layer.YouTube != null && !string.IsNullOrEmpty(layer.YouTube.Title) ? "YouTube " + layer.YouTube.Title : "YouTube";
        ResetLayerEffectState(layer);
        layer.VideoStatus = null;
        layer.Enabled = true;
        if (layer.YouTube == null)
        {
            layer.YouTube = new YoutubeState
            {
                SpeedInput = "1.00",
                TimeInput = "0.0"
            };
        }
        ApplyLayerAudioOutputState(layer);
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);
        AddLog(LayerSlotLabel(layerIndex) + " source: YouTube");
    }

    private void LoadYoutubeVideoIntoLayer(int layerIndex, YoutubeSearchResult result)
    {
        if (result == null || vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        LoadYoutubeSourceIntoLayer(layerIndex);
        var layer = vjLayers[layerIndex];
        var youtube = layer.YouTube;
        CleanupYoutubeLocalCache(youtube);
        youtube.VideoId = result.VideoId;
        youtube.Title = result.Title;
        youtube.Author = result.Author;
        youtube.Url = "https://www.youtube.com/watch?v=" + result.VideoId;
        youtube.DirectUrl = null;
        youtube.Error = null;
        youtube.Resolving = true;
        youtube.WaveformStatus = null;
        youtube.WaveformAnalyzing = false;
        youtube.WaveformCachePath = null;
        youtube.WaveformKey = null;
        youtube.WaveformProfile = null;
        if (youtube.WaveformTexture != null)
        {
            Destroy(youtube.WaveformTexture);
            youtube.WaveformTexture = null;
        }
        if (youtube.ZoomWaveformTexture != null)
        {
            Destroy(youtube.ZoomWaveformTexture);
            youtube.ZoomWaveformTexture = null;
        }
        youtube.ZoomWaveformTextureKey = null;
        layer.SourceName = "YouTube " + result.Title;
        StartCoroutine(ResolveYoutubeVideo(layerIndex, youtube.Url));
    }

    private void StartYoutubeRedirectServer()
    {
        if (youtubeRedirectServerRunning)
        {
            return;
        }

        var failures = new List<string>();
        var candidates = new[]
        {
            "http://127.0.0.1:" + YoutubeRedirectServerPort.ToString(CultureInfo.InvariantCulture),
            "http://localhost:" + YoutubeRedirectServerPort.ToString(CultureInfo.InvariantCulture)
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            var baseUrl = candidates[i];
            HttpListener listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                youtubeRedirectServer = listener;
                youtubeRedirectServerBaseUrl = baseUrl;
                youtubeRedirectServerRunning = true;
                youtubeRedirectServerThread = StartThread("YouTube Redirect Server", RunYoutubeRedirectServer);
                AddLog("YouTube redirect server started: " + youtubeRedirectServerBaseUrl);
                return;
            }
            catch (Exception ex)
            {
                failures.Add(baseUrl + " -> " + ex.Message);
                if (listener != null)
                {
                    try
                    {
                        listener.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        youtubeRedirectServerRunning = false;
        youtubeRedirectServer = null;
        youtubeRedirectServerBaseUrl = null;
        AddLog("YouTube redirect server failed: " + string.Join(" / ", failures.ToArray()));
    }

    private void StopYoutubeRedirectServer()
    {
        youtubeRedirectServerRunning = false;
        youtubeRedirectServerBaseUrl = null;
        var existing = youtubeRedirectServer;
        youtubeRedirectServer = null;
        if (existing != null)
        {
            try
            {
                existing.Close();
            }
            catch
            {
            }
        }
        JoinThread(ref youtubeRedirectServerThread);
    }

    private void RunYoutubeRedirectServer()
    {
        while (youtubeRedirectServerRunning)
        {
            var server = youtubeRedirectServer;
            if (server == null)
            {
                return;
            }

            HttpListenerContext context = null;
            try
            {
                context = server.GetContext();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            try
            {
                HandleYoutubeRedirectRequest(context);
            }
            catch (Exception ex)
            {
                try
                {
                    var response = context.Response;
                    response.StatusCode = 500;
                    response.ContentType = "application/json; charset=utf-8";
                    WriteTextResponse(response, "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
                }
                catch
                {
                }
            }
        }
    }

    private void HandleYoutubeRedirectRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url == null ? "/" : request.Url.AbsolutePath ?? "/";
        var playbackMode = ParseYoutubePlaybackMode(request.QueryString["mode"]);
        var options = request.QueryString["options"];
        var cliName = ParseYoutubeResolverCli(request.QueryString["cli"]);
        var schema = request.QueryString.GetValues("schema");
        if (string.Equals(path, "/watch", StringComparison.OrdinalIgnoreCase))
        {
            var source = request.QueryString["v"];
            if (string.IsNullOrEmpty(source))
            {
                source = request.QueryString["url"];
            }

            var info = ResolveYoutubeSourceInfo(source, playbackMode, options, cliName);
            if (!string.IsNullOrEmpty(info.Error) || string.IsNullOrEmpty(info.DirectUrl))
            {
                response.StatusCode = 502;
                response.ContentType = "application/json; charset=utf-8";
                WriteTextResponse(response, "{\"success\":false,\"error\":\"" + EscapeJson(string.IsNullOrEmpty(info.Error) ? "yt-dlp resolve failed." : info.Error) + "\"}");
                return;
            }

            response.StatusCode = 302;
            response.RedirectLocation = info.DirectUrl;
            response.Close();
            return;
        }

        if (string.Equals(path, "/v1/video", StringComparison.OrdinalIgnoreCase))
        {
            var source = request.QueryString["url"];
            if (string.IsNullOrEmpty(source))
            {
                source = request.QueryString["v"];
            }

            var info = ResolveYoutubeSourceInfo(source, playbackMode, options, cliName);
            var responseBody = new YoutubeServerVideoResponse
            {
                success = string.IsNullOrEmpty(info.Error) && !string.IsNullOrEmpty(info.DirectUrl),
                error = info.Error,
                url = info.DirectUrl,
                watchUrl = BuildYoutubeWatchRedirectUrl(info.Source, playbackMode, info.Options, info.CliName),
                width = info.Width,
                height = info.Height
            };
            response.StatusCode = responseBody.success ? 200 : 502;
            response.ContentType = "application/json; charset=utf-8";
            WriteTextResponse(response, BuildYoutubeServerResponseJson(responseBody, info, schema));
            return;
        }

        response.StatusCode = 404;
        response.ContentType = "application/json; charset=utf-8";
        WriteTextResponse(response, "{\"success\":false,\"error\":\"Not found\"}");
    }

    private YoutubeResolvedVideoInfo ResolveYoutubeSourceInfo(string source, YoutubePlaybackMode playbackMode, string extraOptions = null, string cliName = null)
    {
        var normalized = NormalizeYoutubeSource(source);
        if (string.IsNullOrEmpty(normalized))
        {
            return new YoutubeResolvedVideoInfo { Source = source, Error = "Missing YouTube URL or video id." };
        }

        cliName = NormalizeYoutubeResolverCli(cliName);
        extraOptions = string.IsNullOrWhiteSpace(extraOptions) ? null : extraOptions.Trim();

        if (TryGetCachedYoutubeResolvedVideo(normalized, playbackMode, extraOptions, cliName, out var cached))
        {
            return cached;
        }

        var resolver = FindYoutubeResolver(cliName);
        if (string.IsNullOrEmpty(resolver))
        {
            return new YoutubeResolvedVideoInfo
            {
                Source = normalized,
                CliName = cliName,
                Options = extraOptions,
                Error = string.Equals(cliName, "youtube-dl", StringComparison.OrdinalIgnoreCase)
                    ? "youtube-dl not found. Put youtube-dl.exe in rebuild/tools."
                    : "yt-dlp not found. Put yt-dlp.exe in rebuild/tools."
            };
        }

        System.Diagnostics.Process process = null;
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = resolver,
                Arguments = BuildYoutubeResolverArguments("--no-playlist -f " + QuoteProcessArgument(YoutubeFormatSelector(playbackMode)) + " --print \"RES:%(width)sx%(height)s\" -g " + QuoteProcessArgument(normalized), extraOptions),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ApplyYoutubeRuntimeEnvironment(info);
            process = System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            return new YoutubeResolvedVideoInfo { Source = normalized, Error = ex.Message };
        }

        if (process == null)
        {
            return new YoutubeResolvedVideoInfo { Source = normalized, Error = "yt-dlp could not start." };
        }

        process.WaitForExit();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        var exitCode = process.ExitCode;
        process.Dispose();

        var resolved = new YoutubeResolvedVideoInfo
        {
            Source = normalized,
            DirectUrl = FirstNonEmptyLineExcludingPrefix(stdout, "RES:"),
            CliName = cliName,
            Options = extraOptions,
            PlaybackMode = playbackMode,
            CachedAtUtc = DateTime.UtcNow
        };
        var size = ParsePrintedResolution(stdout);
        resolved.Width = size.x;
        resolved.Height = size.y;
        if (exitCode != 0 || string.IsNullOrEmpty(resolved.DirectUrl))
        {
            resolved.Error = FirstMeaningfulProcessErrorLine(stderr) ?? "yt-dlp could not resolve this video.";
            return resolved;
        }

        StoreCachedYoutubeResolvedVideo(resolved);
        return resolved;
    }

    private bool TryGetCachedYoutubeResolvedVideo(string source, YoutubePlaybackMode playbackMode, string extraOptions, string cliName, out YoutubeResolvedVideoInfo info)
    {
        lock (youtubeRedirectCacheLock)
        {
            var key = YoutubeResolvedVideoCacheKey(source, playbackMode, extraOptions, cliName);
            if (youtubeRedirectCache.TryGetValue(key, out info))
            {
                if ((DateTime.UtcNow - info.CachedAtUtc).TotalMinutes <= 10d && !string.IsNullOrEmpty(info.DirectUrl))
                {
                    return true;
                }

                youtubeRedirectCache.Remove(key);
            }
        }

        info = null;
        return false;
    }

    private void StoreCachedYoutubeResolvedVideo(YoutubeResolvedVideoInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.Source) || string.IsNullOrEmpty(info.DirectUrl))
        {
            return;
        }

        lock (youtubeRedirectCacheLock)
        {
            youtubeRedirectCache[YoutubeResolvedVideoCacheKey(info.Source, info.PlaybackMode, info.Options, info.CliName)] = info;
        }
    }

    private string BuildYoutubeWatchRedirectUrl(string source, YoutubePlaybackMode playbackMode, string extraOptions = null, string cliName = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return YoutubeRedirectBaseUrl() + "/watch";
        }

        var url = YoutubeRedirectBaseUrl() + "/watch?v=" + UnityWebRequest.EscapeURL(source) + "&mode=" + playbackMode.ToString();
        if (!string.IsNullOrWhiteSpace(extraOptions))
        {
            url += "&options=" + UnityWebRequest.EscapeURL(extraOptions);
        }
        if (!string.IsNullOrWhiteSpace(cliName))
        {
            url += "&cli=" + UnityWebRequest.EscapeURL(cliName);
        }
        return url;
    }

    private string YoutubeRedirectBaseUrl()
    {
        if (!string.IsNullOrEmpty(youtubeRedirectServerBaseUrl))
        {
            return youtubeRedirectServerBaseUrl;
        }
        return "http://127.0.0.1:" + YoutubeRedirectServerPort.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeYoutubeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        source = source.Trim();
        var videoId = ExtractYoutubeVideoId(source);
        if (!string.IsNullOrEmpty(videoId))
        {
            return "https://www.youtube.com/watch?v=" + videoId;
        }

        return source;
    }

    private static string YoutubeResolvedVideoCacheKey(string source, YoutubePlaybackMode playbackMode, string extraOptions, string cliName)
    {
        return playbackMode.ToString() + ":" + NormalizeYoutubeResolverCli(cliName) + ":" + (extraOptions ?? "") + ":" + source;
    }

    private static string YoutubeVideoCachePath(YoutubeState youtube)
    {
        var source = youtube == null ? "" : !string.IsNullOrEmpty(youtube.VideoId) ? youtube.VideoId : youtube.Url ?? youtube.Title ?? "";
        var key = StableTextHash(YoutubeVideoCacheVersion + ":" + source).ToString("x8", CultureInfo.InvariantCulture);
        return Path.Combine(Application.persistentDataPath, "youtube-video-cache", key + ".mp4");
    }

    private static string FindExistingYoutubeVideoCache(string preferredMp4Path)
    {
        if (!string.IsNullOrEmpty(preferredMp4Path) && File.Exists(preferredMp4Path))
        {
            return preferredMp4Path;
        }

        if (string.IsNullOrEmpty(preferredMp4Path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(preferredMp4Path);
        var stem = Path.GetFileNameWithoutExtension(preferredMp4Path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(stem) || !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(directory, stem + ".mp4"),
            Path.Combine(directory, stem + ".mkv"),
            Path.Combine(directory, stem + ".webm")
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

    private static YoutubePlaybackMode ParseYoutubePlaybackMode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return YoutubePlaybackMode.UrlBest;
        }

        if (Enum.TryParse(value, true, out YoutubePlaybackMode parsed))
        {
            return parsed;
        }

        return YoutubePlaybackMode.UrlBest;
    }

    private static string ParseYoutubeResolverCli(string value)
    {
        return NormalizeYoutubeResolverCli(value);
    }

    private static string NormalizeYoutubeResolverCli(string value)
    {
        if (string.Equals(value, "youtube-dl", StringComparison.OrdinalIgnoreCase))
        {
            return "youtube-dl";
        }

        return "yt-dlp";
    }

    private static string BuildYoutubeServerResponseJson(YoutubeServerVideoResponse responseBody, YoutubeResolvedVideoInfo info, string[] schema)
    {
        if (schema == null || schema.Length == 0)
        {
            return JsonUtility.ToJson(responseBody);
        }

        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        for (var i = 0; i < schema.Length; i++)
        {
            var key = schema[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string value;
            var lower = key.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "success":
                    value = responseBody != null && responseBody.success ? "true" : "false";
                    break;
                case "error":
                    value = "\"" + EscapeJson(responseBody == null ? null : responseBody.error) + "\"";
                    break;
                case "url":
                    value = "\"" + EscapeJson(responseBody == null ? null : responseBody.url) + "\"";
                    break;
                case "watchurl":
                    value = "\"" + EscapeJson(responseBody == null ? null : responseBody.watchUrl) + "\"";
                    break;
                case "width":
                    value = (responseBody == null ? 0 : responseBody.width).ToString(CultureInfo.InvariantCulture);
                    break;
                case "height":
                    value = (responseBody == null ? 0 : responseBody.height).ToString(CultureInfo.InvariantCulture);
                    break;
                case "source":
                    value = "\"" + EscapeJson(info == null ? null : info.Source) + "\"";
                    break;
                case "cli":
                    value = "\"" + EscapeJson(info == null ? null : info.CliName) + "\"";
                    break;
                default:
                    continue;
            }

            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append('"').Append(EscapeJson(key.Trim())).Append('"').Append(':').Append(value);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string YoutubeFormatSelector(YoutubePlaybackMode playbackMode)
    {
        switch (playbackMode)
        {
            case YoutubePlaybackMode.UrlCompatible:
                return "best[height<=1080][ext=mp4][vcodec^=avc1][acodec!=none][protocol^=http]/best[height<=1080][ext=mp4][acodec!=none][protocol^=http]/best[height<=1080][acodec!=none][protocol^=http]/best[ext=mp4][acodec!=none][protocol^=http]/best[acodec!=none][protocol^=http]/best[protocol^=http]";
            case YoutubePlaybackMode.UrlBest:
                return "best[ext=mp4][vcodec^=avc1][acodec!=none][protocol^=http]/best[ext=mp4][acodec!=none][protocol^=http]/best[vcodec^=avc1][acodec!=none][protocol^=http]/best[acodec!=none][protocol^=http]/best[protocol^=http]";
            default:
                return "best[ext=mp4]/best";
        }
    }

    private static void WriteTextResponse(HttpListenerResponse response, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.LongLength;
        using (var stream = response.OutputStream)
        {
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private IEnumerator ResolveYoutubeVideo(int layerIndex, string watchUrl)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        var layer = vjLayers[layerIndex];
        if (layer.YouTube == null)
        {
            yield break;
        }

        if (layer.YouTube.PlaybackMode == YoutubePlaybackMode.LocalCache)
        {
            yield return ResolveYoutubeVideoLocalCache(layerIndex, watchUrl);
            yield break;
        }

        if (!youtubeRedirectServerRunning)
        {
            StartYoutubeRedirectServer();
            yield return null;
        }

        var requestUrl = YoutubeRedirectBaseUrl() + "/v1/video?url=" + UnityWebRequest.EscapeURL(watchUrl) + "&mode=" + layer.YouTube.PlaybackMode.ToString();
        YoutubeServerVideoResponse payload = null;
        using (var request = UnityWebRequest.Get(requestUrl))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                AddLog("YouTube URL mode failed, fallback to cache: " + request.error);
                layer.YouTube.PlaybackMode = YoutubePlaybackMode.LocalCache;
                yield return ResolveYoutubeVideoLocalCache(layerIndex, watchUrl);
                yield break;
            }

            try
            {
                payload = JsonUtility.FromJson<YoutubeServerVideoResponse>(request.downloadHandler.text ?? "");
            }
            catch (Exception ex)
            {
                layer.YouTube.Resolving = false;
                layer.YouTube.Error = "Local YouTube server JSON error: " + ex.Message;
                yield break;
            }
        }

        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }
        layer = vjLayers[layerIndex];
        if (layer.SourceKind != LayerSourceKind.YouTube ||
            layer.YouTube == null ||
            !string.Equals(layer.YouTube.Url, watchUrl, StringComparison.Ordinal))
        {
            yield break;
        }

        if (payload == null || !payload.success || string.IsNullOrEmpty(payload.watchUrl) || string.IsNullOrEmpty(payload.url))
        {
            AddLog("YouTube URL mode payload failed, fallback to cache: " + (payload != null ? payload.error : "no payload"));
            layer.YouTube.PlaybackMode = YoutubePlaybackMode.LocalCache;
            yield return ResolveYoutubeVideoLocalCache(layerIndex, watchUrl);
            yield break;
        }

        layer.YouTube.DirectUrl = payload.url;
        layer.YouTube.StreamWidth = payload.width;
        layer.YouTube.StreamHeight = payload.height;
        layer.YouTube.Error = null;
        layer.YouTube.Resolving = false;
        layer.Player.Stop();
        layer.Player.isLooping = true;
        layer.Player.url = payload.watchUrl;
        layer.Player.playbackSpeed = Mathf.Max(0.05f, layer.YouTube.PlaybackSpeed);
        layer.Player.Play();
        StartCoroutine(AnalyzeYoutubeWaveform(layerIndex, watchUrl, payload.url));
        AddLog(LayerSlotLabel(layerIndex) + " YouTube loaded: " + layer.YouTube.Title);
        yield break;
    }

    private IEnumerator ResolveYoutubeVideoLocalCache(int layerIndex, string watchUrl)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        var layer = vjLayers[layerIndex];
        if (layer.YouTube == null)
        {
            yield break;
        }

        var resolver = FindYoutubeResolver();
        if (string.IsNullOrEmpty(resolver))
        {
            layer.YouTube.Resolving = false;
            layer.YouTube.Error = "yt-dlp not found. Put yt-dlp.exe in rebuild/tools.";
            yield break;
        }
        var ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg))
        {
            layer.YouTube.Resolving = false;
            layer.YouTube.Error = "ffmpeg.exe not found. Put ffmpeg.exe in rebuild/tools.";
            yield break;
        }

        var cachePath = YoutubeVideoCachePath(layer.YouTube);
        if (File.Exists(cachePath))
        {
            layer.YouTube.DirectUrl = ToFileUrl(cachePath);
            layer.YouTube.LocalCachePath = cachePath;
            layer.YouTube.Error = null;
            layer.YouTube.Resolving = false;
            layer.Player.Stop();
            layer.Player.isLooping = true;
            layer.Player.url = layer.YouTube.DirectUrl;
            layer.Player.playbackSpeed = Mathf.Max(0.05f, layer.YouTube.PlaybackSpeed);
            layer.Player.Play();
            StartCoroutine(AnalyzeYoutubeWaveform(layerIndex, watchUrl, cachePath));
            AddLog(LayerSlotLabel(layerIndex) + " YouTube cache loaded: " + layer.YouTube.Title);
            yield break;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
        layer.VideoStatus = "Downloading YouTube video cache...";
        System.Diagnostics.Process process = null;
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        try
        {
            var outputTemplate = Path.Combine(Path.GetDirectoryName(cachePath), Path.GetFileNameWithoutExtension(cachePath) + ".%(ext)s");
            var ffmpegDirectory = Path.GetDirectoryName(ffmpeg);
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = resolver,
                Arguments = BuildYoutubeResolverArguments("--no-playlist --no-progress --newline --ffmpeg-location " + QuoteProcessArgument(string.IsNullOrEmpty(ffmpegDirectory) ? ffmpeg : ffmpegDirectory) +
                            " -f \"bv*[vcodec^=avc1][ext=mp4]+ba[ext=m4a]/bv*[ext=mp4]+ba/best[ext=mp4]/best\" --merge-output-format mp4 --print \"RES:%(width)sx%(height)s\" --print after_move:\"FILE:%(filepath)s\" -o " +
                            QuoteProcessArgument(outputTemplate) + " " + QuoteProcessArgument(watchUrl)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ApplyYoutubeRuntimeEnvironment(info);
            process = System.Diagnostics.Process.Start(info);
            if (process != null)
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        stdoutBuilder.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        stderrBuilder.AppendLine(args.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
        }
        catch (Exception ex)
        {
            layer.YouTube.Resolving = false;
            layer.YouTube.Error = ex.Message;
            yield break;
        }

        while (process != null && !process.HasExited)
        {
            yield return null;
        }

        var stdout = stdoutBuilder.ToString();
        var stderr = stderrBuilder.ToString();
        var exitCode = process == null ? -1 : process.ExitCode;
        if (process != null)
        {
            process.WaitForExit();
            process.Dispose();
        }

        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }
        layer = vjLayers[layerIndex];
        if (layer.SourceKind != LayerSourceKind.YouTube ||
            layer.YouTube == null ||
            !string.Equals(layer.YouTube.Url, watchUrl, StringComparison.Ordinal))
        {
            yield break;
        }

        var downloadedPath = FirstPrintedFilePath(stdout);
        if (string.IsNullOrEmpty(downloadedPath))
        {
            downloadedPath = FindExistingYoutubeVideoCache(cachePath);
        }
        var resolvedSize = ParsePrintedResolution(stdout);
        if (exitCode != 0 || string.IsNullOrEmpty(downloadedPath) || !File.Exists(downloadedPath))
        {
            layer.VideoStatus = null;
            layer.YouTube.Resolving = false;
            layer.YouTube.Error = FirstMeaningfulProcessErrorLine(stderr) ??
                                  FirstMeaningfulProcessErrorLine(stdout) ??
                                  "yt-dlp cache download failed.";
            yield break;
        }

        layer.YouTube.DirectUrl = ToFileUrl(downloadedPath);
        layer.YouTube.LocalCachePath = downloadedPath;
        layer.YouTube.StreamWidth = resolvedSize.x;
        layer.YouTube.StreamHeight = resolvedSize.y;
        layer.YouTube.Error = null;
        layer.YouTube.Resolving = false;
        layer.VideoStatus = null;
        layer.Player.Stop();
        layer.Player.isLooping = true;
        layer.Player.url = layer.YouTube.DirectUrl;
        layer.Player.playbackSpeed = Mathf.Max(0.05f, layer.YouTube.PlaybackSpeed);
        layer.Player.Play();
        StartCoroutine(AnalyzeYoutubeWaveform(layerIndex, watchUrl, downloadedPath));
        AddLog(LayerSlotLabel(layerIndex) + " YouTube loaded from cache: " + layer.YouTube.Title);
    }

    private IEnumerator AnalyzeYoutubeWaveform(int layerIndex, string watchUrl, string directUrl)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }

        var layer = vjLayers[layerIndex];
        if (layer.YouTube == null || string.IsNullOrEmpty(directUrl))
        {
            yield break;
        }

        var youtube = layer.YouTube;
        var cacheKey = YoutubeWaveformCacheKey(youtube);
        var cachePath = YoutubeWaveformCachePath(cacheKey);
        youtube.WaveformKey = cacheKey;
        youtube.WaveformCachePath = cachePath;

        if (LoadYoutubeWaveformCache(youtube, cachePath))
        {
            youtube.WaveformStatus = "Waveform cache loaded";
            yield break;
        }

        var ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg))
        {
            youtube.WaveformStatus = "ffmpeg.exe not found. Put ffmpeg.exe in rebuild/tools.";
            yield break;
        }

        youtube.WaveformAnalyzing = true;
        youtube.WaveformStatus = "Analyzing audio waveform...";
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
        var pcmPath = Path.Combine(Application.temporaryCachePath, "beatlink-youtube-" + cacheKey + ".s16le");

        System.Diagnostics.Process process = null;
        try
        {
            if (File.Exists(pcmPath))
            {
                File.Delete(pcmPath);
            }
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = "-y -nostdin -hide_banner -loglevel error -i " + QuoteProcessArgument(directUrl) +
                            " -vn -ac 1 -ar " + YoutubeWaveformSampleRate.ToString(CultureInfo.InvariantCulture) +
                            " -f s16le " + QuoteProcessArgument(pcmPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            process = System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            youtube.WaveformAnalyzing = false;
            youtube.WaveformStatus = "Waveform analyze error: " + ex.Message;
            yield break;
        }

        while (process != null && !process.HasExited)
        {
            yield return null;
        }

        var stderr = process == null ? "" : process.StandardError.ReadToEnd();
        var exitCode = process == null ? -1 : process.ExitCode;
        if (process != null)
        {
            process.Dispose();
        }

        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            yield break;
        }
        layer = vjLayers[layerIndex];
        if (layer.SourceKind != LayerSourceKind.YouTube ||
            layer.YouTube == null ||
            !string.Equals(layer.YouTube.Url, watchUrl, StringComparison.Ordinal))
        {
            yield break;
        }
        youtube = layer.YouTube;

        if (exitCode != 0 || !File.Exists(pcmPath))
        {
            youtube.WaveformAnalyzing = false;
            youtube.WaveformStatus = "Waveform analyze failed: " + FirstNonEmptyLine(stderr);
            yield break;
        }

        Texture2D texture = null;
        try
        {
            texture = BuildYoutubeWaveformTexture(File.ReadAllBytes(pcmPath));
            if (texture != null)
            {
                File.WriteAllBytes(cachePath, texture.EncodeToPNG());
            }
        }
        catch (Exception ex)
        {
            youtube.WaveformAnalyzing = false;
            youtube.WaveformStatus = "Waveform build error: " + ex.Message;
            if (texture != null)
            {
                Destroy(texture);
            }
            yield break;
        }
        finally
        {
            try
            {
                if (File.Exists(pcmPath))
                {
                    File.Delete(pcmPath);
                }
            }
            catch
            {
            }
        }

        if (youtube.WaveformTexture != null)
        {
            Destroy(youtube.WaveformTexture);
        }
        youtube.WaveformTexture = texture;
        youtube.WaveformProfile = ExtractWaveformProfileFromTexture(texture);
        if (youtube.ZoomWaveformTexture != null)
        {
            Destroy(youtube.ZoomWaveformTexture);
            youtube.ZoomWaveformTexture = null;
        }
        youtube.ZoomWaveformTextureKey = null;
        youtube.WaveformAnalyzing = false;
        youtube.WaveformStatus = texture == null ? "Waveform unavailable" : "Waveform analyzed";
    }

    private string FindYoutubeResolver(string cliName = null)
    {
        cliName = NormalizeYoutubeResolverCli(cliName);
        var preferredFile = string.Equals(cliName, "youtube-dl", StringComparison.OrdinalIgnoreCase) ? "youtube-dl.exe" : "yt-dlp.exe";
        var fallbackFile = string.Equals(cliName, "youtube-dl", StringComparison.OrdinalIgnoreCase) ? "youtube-dl" : "yt-dlp";

        if (string.Equals(cliName, "yt-dlp", StringComparison.OrdinalIgnoreCase) && youtubeResolverChecked)
        {
            return youtubeResolverPath;
        }

        var localCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", preferredFile)),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", preferredFile))
        };
        for (var i = 0; i < localCandidates.Length; i++)
        {
            var local = localCandidates[i];
            if (File.Exists(local))
            {
                if (string.Equals(cliName, "yt-dlp", StringComparison.OrdinalIgnoreCase))
                {
                    youtubeResolverChecked = true;
                    youtubeResolverPath = local;
                    return youtubeResolverPath;
                }
                return local;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = path.Split(Path.PathSeparator);
        for (var i = 0; i < parts.Length; i++)
        {
            var dir = parts[i];
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }
            var candidate = Path.Combine(dir.Trim(), preferredFile);
            if (File.Exists(candidate))
            {
                if (string.Equals(cliName, "yt-dlp", StringComparison.OrdinalIgnoreCase))
                {
                    youtubeResolverChecked = true;
                    youtubeResolverPath = candidate;
                    return youtubeResolverPath;
                }
                return candidate;
            }
            candidate = Path.Combine(dir.Trim(), fallbackFile);
            if (File.Exists(candidate))
            {
                if (string.Equals(cliName, "yt-dlp", StringComparison.OrdinalIgnoreCase))
                {
                    youtubeResolverChecked = true;
                    youtubeResolverPath = candidate;
                    return youtubeResolverPath;
                }
                return candidate;
            }
        }

        if (string.Equals(cliName, "yt-dlp", StringComparison.OrdinalIgnoreCase))
        {
            youtubeResolverChecked = true;
            youtubeResolverPath = null;
        }
        return null;
    }

    private string FindFfmpeg()
    {
        if (ffmpegChecked)
        {
            return ffmpegPath;
        }

        ffmpegChecked = true;
        var localCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "ffmpeg.exe")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", "ffmpeg.exe"))
        };
        for (var i = 0; i < localCandidates.Length; i++)
        {
            var local = localCandidates[i];
            if (File.Exists(local))
            {
                ffmpegPath = local;
                return ffmpegPath;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = path.Split(Path.PathSeparator);
        for (var i = 0; i < parts.Length; i++)
        {
            var dir = parts[i];
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }
            var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
            if (File.Exists(candidate))
            {
                ffmpegPath = candidate;
                return ffmpegPath;
            }
            candidate = Path.Combine(dir.Trim(), "ffmpeg");
            if (File.Exists(candidate))
            {
                ffmpegPath = candidate;
                return ffmpegPath;
            }
        }

        ffmpegPath = null;
        return null;
    }

    private string FindYoutubeJsRuntime()
    {
        if (youtubeJsRuntimeChecked)
        {
            return youtubeJsRuntimePath;
        }

        youtubeJsRuntimeChecked = true;
        youtubeJsRuntimePath = null;
        youtubeJsRuntimeName = null;

        var localCandidates = new[]
        {
            new { Name = "node", Path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "node.exe")) },
            new { Name = "deno", Path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "deno.exe")) },
            new { Name = "node", Path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", "node.exe")) },
            new { Name = "deno", Path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", "deno.exe")) }
        };
        for (var i = 0; i < localCandidates.Length; i++)
        {
            if (File.Exists(localCandidates[i].Path))
            {
                youtubeJsRuntimeName = localCandidates[i].Name;
                youtubeJsRuntimePath = localCandidates[i].Path;
                return youtubeJsRuntimePath;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = path.Split(Path.PathSeparator);
        for (var i = 0; i < parts.Length; i++)
        {
            var dir = parts[i];
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            var node = Path.Combine(dir.Trim(), "node.exe");
            if (File.Exists(node))
            {
                youtubeJsRuntimeName = "node";
                youtubeJsRuntimePath = node;
                return youtubeJsRuntimePath;
            }

            var deno = Path.Combine(dir.Trim(), "deno.exe");
            if (File.Exists(deno))
            {
                youtubeJsRuntimeName = "deno";
                youtubeJsRuntimePath = deno;
                return youtubeJsRuntimePath;
            }
        }

        return null;
    }

    private string BuildYoutubeResolverArguments(string baseArguments, string extraOptions = null)
    {
        var exportedCookieFile = FindYoutubeExportedCookieFile();
        var cookieArguments = DetectYoutubeCookieArguments();
        FindYoutubeJsRuntime();
        var prefix = new StringBuilder();
        if (!string.IsNullOrEmpty(exportedCookieFile))
        {
            prefix.Append("--cookies ").Append(QuoteProcessArgument(exportedCookieFile));
        }
        else
        if (!string.IsNullOrEmpty(cookieArguments))
        {
            prefix.Append(cookieArguments);
        }
        if (!string.IsNullOrEmpty(youtubeJsRuntimeName))
        {
            if (prefix.Length > 0)
            {
                prefix.Append(' ');
            }
            prefix.Append("--js-runtimes ").Append(QuoteProcessArgument(youtubeJsRuntimeName));
        }
        if (!string.IsNullOrWhiteSpace(extraOptions))
        {
            if (prefix.Length > 0)
            {
                prefix.Append(' ');
            }
            prefix.Append(extraOptions.Trim());
        }

        if (prefix.Length == 0)
        {
            return baseArguments ?? "";
        }

        if (!string.IsNullOrEmpty(baseArguments))
        {
            prefix.Append(' ').Append(baseArguments);
        }
        return prefix.ToString();
    }

    private string FindYoutubeExportedCookieFile()
    {
        var candidate = Path.Combine(Application.temporaryCachePath, "beatlink-youtube-selection.txt.cookies.txt");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return null;
    }

    private string DetectYoutubeCookieArguments()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (HasAnyFile(
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Network", "Cookies"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cookies")))
        {
            return "--cookies-from-browser edge";
        }

        if (HasAnyFile(
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Network", "Cookies"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cookies")))
        {
            return "--cookies-from-browser chrome";
        }

        if (HasAnyFile(
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Network", "Cookies"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cookies")))
        {
            return "--cookies-from-browser brave";
        }

        var firefoxProfiles = Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            try
            {
                var profiles = Directory.GetDirectories(firefoxProfiles);
                for (var i = 0; i < profiles.Length; i++)
                {
                    if (File.Exists(Path.Combine(profiles[i], "cookies.sqlite")))
                    {
                        return "--cookies-from-browser firefox";
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool HasAnyFile(params string[] paths)
    {
        if (paths == null)
        {
            return false;
        }

        for (var i = 0; i < paths.Length; i++)
        {
            if (!string.IsNullOrEmpty(paths[i]) && File.Exists(paths[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyYoutubeRuntimeEnvironment(System.Diagnostics.ProcessStartInfo info)
    {
        if (info == null)
        {
            return;
        }

        var runtimePath = FindYoutubeJsRuntime();
        if (string.IsNullOrEmpty(runtimePath))
        {
            return;
        }

        var runtimeDir = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrEmpty(runtimeDir))
        {
            return;
        }

        var existingPath = info.EnvironmentVariables["PATH"];
        if (string.IsNullOrEmpty(existingPath))
        {
            existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        }

        if (existingPath.IndexOf(runtimeDir, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        info.EnvironmentVariables["PATH"] = runtimeDir + Path.PathSeparator + existingPath;
    }

    private static string QuoteProcessArgument(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
    }

    private static string YoutubeWaveformCacheKey(YoutubeState youtube)
    {
        var source = youtube == null ? "" : !string.IsNullOrEmpty(youtube.VideoId) ? youtube.VideoId : youtube.Url ?? youtube.Title ?? "";
        return StableTextHash(YoutubeWaveformAnalyzerVersion + ":" + source).ToString("x8", CultureInfo.InvariantCulture);
    }

    private static string YoutubeWaveformCachePath(string cacheKey)
    {
        var key = string.IsNullOrEmpty(cacheKey) ? "unknown" : cacheKey;
        return Path.Combine(Application.persistentDataPath, "youtube-waveforms", key + ".png");
    }

    private bool LoadYoutubeWaveformCache(YoutubeState youtube, string cachePath)
    {
        if (youtube == null || string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(cachePath)))
            {
                Destroy(texture);
                return false;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (youtube.WaveformTexture != null)
            {
                Destroy(youtube.WaveformTexture);
            }
            youtube.WaveformTexture = texture;
            youtube.WaveformProfile = ExtractWaveformProfileFromTexture(texture);
            if (youtube.ZoomWaveformTexture != null)
            {
                Destroy(youtube.ZoomWaveformTexture);
                youtube.ZoomWaveformTexture = null;
            }
            youtube.ZoomWaveformTextureKey = null;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("YouTube waveform cache load error: " + ex.Message);
            return false;
        }
    }

    private static string FirstNonEmptyLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                return line;
            }
        }
        return null;
    }

    private static string FirstMeaningfulProcessErrorLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var lines = text.Replace("\r", "").Split('\n');
        string warningLine = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
            {
                if (warningLine == null)
                {
                    warningLine = line;
                }
                continue;
            }

            if (line.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("[youtube]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line;
        }

        return warningLine;
    }

    private static string FirstPrintedFilePath(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
            {
                var path = line.Substring(5).Trim().Trim('"');
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private void CleanupLayerYoutubeLocalCache(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        CleanupYoutubeLocalCache(layer.YouTube);
    }

    private void CleanupYoutubeLocalCache(YoutubeState youtube)
    {
        if (youtube == null || string.IsNullOrEmpty(youtube.LocalCachePath))
        {
            return;
        }

        try
        {
            if (File.Exists(youtube.LocalCachePath))
            {
                File.Delete(youtube.LocalCachePath);
            }
        }
        catch (Exception ex)
        {
            AddLog("YouTube cache cleanup failed: " + ex.Message);
        }
        finally
        {
            youtube.LocalCachePath = null;
        }
    }
}
