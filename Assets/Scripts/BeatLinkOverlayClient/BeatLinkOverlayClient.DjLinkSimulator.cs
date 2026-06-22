using System;
using System.Globalization;
using UnityEngine;

public sealed partial class BeatLinkOverlayClient : MonoBehaviour
{
    private enum DjLinkInputMode
    {
        InternalDirect = 0,
        ExternalBlt = 1,
        Simulator = 2
    }

    private static readonly string[] SimulatedTrackTitles =
    {
        "Simulated Track Alpha",
        "Simulated Track Beta",
        "Simulated Track Gamma",
        "Simulated Track Delta",
        "Simulated Track Epsilon",
        "Simulated Track Zeta"
    };

    private static readonly string[] SimulatedTrackArtists =
    {
        "Test Artist One",
        "Test Artist Two",
        "Test Artist Three",
        "Test Artist Four"
    };

    private static readonly string[] SimulatedTrackComments =
    {
        "Simulator metadata stream",
        "UDP test fallback",
        "Waveform preview test",
        "No hardware connected"
    };

    private DjLinkInputMode CurrentDjLinkInputMode()
    {
        if (useSimulatedDjLinkMode)
        {
            return DjLinkInputMode.Simulator;
        }

        return useBltBridgeMode ? DjLinkInputMode.ExternalBlt : DjLinkInputMode.InternalDirect;
    }

    private static string DjLinkInputModeLabel(DjLinkInputMode mode)
    {
        switch (mode)
        {
            case DjLinkInputMode.ExternalBlt:
                return "External mode";
            case DjLinkInputMode.Simulator:
                return "Simulator mode";
            default:
                return "Internal mode";
        }
    }

    private void UpdateSimulatedDjLinkPlayers()
    {
        if (!useSimulatedDjLinkMode)
        {
            return;
        }

        var clampedDeckCount = Mathf.Clamp(simulatedDjLinkDeckCount, 1, 4);
        var durationMs = 214000 + Mathf.Abs(simulatedDjLinkTrackSeed % 5) * 11000;
        var elapsedSeconds = simulatedDjLinkPlaying ? Mathf.Max(0f, Time.realtimeSinceStartup - simulatedDjLinkTrackStartTime) : 0f;
        var playbackMs = simulatedDjLinkPlaying
            ? Mathf.Clamp(Mathf.RoundToInt(elapsedSeconds * 1000f), 0, durationMs)
            : Mathf.Clamp(simulatedDjLinkPausedPlaybackMs, 0, durationMs);
        if (playbackMs >= durationMs && simulatedDjLinkPlaying)
        {
            simulatedDjLinkTrackStartTime = Time.realtimeSinceStartup;
            simulatedDjLinkPausedPlaybackMs = 0;
            playbackMs = 0;
        }

        var totalBeats = playbackMs / 1000f * simulatedDjLinkBpm / 60f;
        var beatNumber = Mathf.Max(1, Mathf.FloorToInt(totalBeats) + 1);
        var beatWithinBar = ((beatNumber - 1) % 4) + 1;
        var title = SimulatedTrackTitles[Mathf.Abs(simulatedDjLinkTrackSeed) % SimulatedTrackTitles.Length];
        var artist = SimulatedTrackArtists[Mathf.Abs(simulatedDjLinkTrackSeed / 2) % SimulatedTrackArtists.Length];
        var comment = SimulatedTrackComments[Mathf.Abs(simulatedDjLinkTrackSeed / 3) % SimulatedTrackComments.Length];
        var nowUtc = DateTime.UtcNow;

        lock (stateLock)
        {
            for (var deck = 1; deck <= 4; deck++)
            {
                var player = GetOrCreatePlayer(deck);
                if (deck > clampedDeckCount)
                {
                    ClearSimulatedPlayer(player);
                    continue;
                }

                player.IsSimulated = true;
                player.DeviceNumber = deck;
                player.DeviceName = deck <= 2 ? "SIM-CDJ-2000NXS" : "SIM-CDJ-3000";
                player.Address = "127.0.0." + (100 + deck).ToString(CultureInfo.InvariantCulture);
                player.HardwareAddress = "SIM-" + deck.ToString(CultureInfo.InvariantCulture);
                player.PeerCount = clampedDeckCount;
                player.HasAnnouncement = true;
                player.HasStatus = true;
                player.HasPrecisePosition = true;
                player.LastAnnouncementUtc = nowUtc;
                player.LastStatusUtc = nowUtc;
                player.LastPositionUtc = nowUtc;
                player.LastSeenUtc = nowUtc;
                player.LastActivitySampleUtc = nowUtc;
                player.TrackSourcePlayer = deck;
                player.TrackSourceSlot = 3;
                player.TrackType = 1;
                player.TrackNumber = 100 + deck;
                player.RekordboxId = 0;
                player.StatusFlags = 0;
                player.PitchRaw = 0;
                player.PitchPercent = deck == 1 ? 0f : ((deck % 2 == 0) ? 0.12f : -0.09f);
                player.BpmRaw = Mathf.RoundToInt(simulatedDjLinkBpm * 100f);
                player.MasterHandoffDevice = 1;
                player.BeatNumber = beatNumber;
                player.CueCountdown = -1;
                player.BeatWithinBar = beatWithinBar;
                player.PacketNumber++;
                player.IsTempoMaster = deck == 1;
                player.IsPlaying = simulatedDjLinkPlaying;
                player.IsSynced = deck != 1;
                player.IsOnAir = deck == 1 || deck == 2;
                player.IsBpmOnlySynced = false;
                player.HasActiveLoop = false;
                player.ActiveLoopStartMs = 0;
                player.ActiveLoopEndMs = 0;
                player.ActiveLoopBeats = 0;
                player.TrackLengthMs = durationMs;
                player.PlaybackPositionMs = playbackMs;
                player.DbServerPort = 0;
                player.DbServerQueryInFlight = false;
                player.LastDbServerQueryUtc = default(DateTime);
                player.DbServerError = null;
                player.MetadataQueryInFlight = false;
                player.LastMetadataQueryUtc = default(DateTime);
                player.LastMetadataKey = null;
                player.MetadataError = null;
                player.CueListQueryInFlight = false;
                player.LastCueListQueryUtc = default(DateTime);
                player.LastCueListKey = null;
                player.BltSeen = false;
                player.LastBltUpdateUtc = default(DateTime);
                player.BltTitle = title + " P" + deck.ToString(CultureInfo.InvariantCulture);
                player.BltArtist = artist;
                player.BltAlbum = "Simulator";
                player.BltComment = comment;
                player.BltKey = deck % 2 == 0 ? "Am" : "F#m";
                player.BltGenre = "Test";
                player.BltColor = deck % 2 == 0 ? "Blue" : "Red";
                player.BltSlot = "USB";
                player.BltType = "Simulator";
                player.BltDurationSeconds = Mathf.RoundToInt(durationMs / 1000f);
                player.BltTimePlayedMs = playbackMs;
                player.BltTimeRemainingMs = Mathf.Max(0, durationMs - playbackMs);
                player.BltTimePlayedDisplay = FormatMillis(playbackMs);
                player.BltTimeRemainingDisplay = FormatMillis(Mathf.Max(0, durationMs - playbackMs));
                player.BltTempo = simulatedDjLinkBpm;
                player.ArtworkId = 0;
                player.PendingAlbumArtBytes = null;
                ApplyBpm(player, simulatedDjLinkBpm, simulatedDjLinkBpm);
                player.PushActivitySample(0.38f + 0.46f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 1.7f + deck)));
                EnsureSimulatedPlayerVisuals(player, deck, durationMs);
            }
        }
    }

    private void ClearSimulatedPlayer(PlayerState player)
    {
        if (player == null || !player.IsSimulated)
        {
            return;
        }

        player.IsSimulated = false;
        player.HasAnnouncement = false;
        player.HasStatus = false;
        player.HasPrecisePosition = false;
        player.LastAnnouncementUtc = default(DateTime);
        player.LastStatusUtc = default(DateTime);
        player.LastPositionUtc = default(DateTime);
        player.LastSeenUtc = default(DateTime);
        player.BltTitle = null;
        player.BltArtist = null;
        player.BltAlbum = null;
        player.BltComment = null;
        player.BltKey = null;
        player.BltGenre = null;
        player.BltColor = null;
        player.BltSlot = null;
        player.BltType = null;
        player.BltDurationSeconds = 0;
        player.BltTimePlayedMs = 0;
        player.BltTimeRemainingMs = 0;
        player.BltTimePlayedDisplay = null;
        player.BltTimeRemainingDisplay = null;
        player.BltTempo = 0f;
        player.TrackLengthMs = 0;
        player.PlaybackPositionMs = 0;
        player.BeatNumber = -1;
        player.BeatWithinBar = 0;
        player.IsTempoMaster = false;
        player.IsPlaying = false;
        player.IsSynced = false;
        player.IsOnAir = false;
        player.IsBpmOnlySynced = false;
        if (player.BltWavePreview != null)
        {
            Destroy(player.BltWavePreview);
            player.BltWavePreview = null;
        }
        if (player.BltWaveOverview != null)
        {
            Destroy(player.BltWaveOverview);
            player.BltWaveOverview = null;
        }
        if (player.AlbumArtTexture != null)
        {
            Destroy(player.AlbumArtTexture);
            player.AlbumArtTexture = null;
        }
        player.AlbumArtWidth = 0;
        player.AlbumArtHeight = 0;
    }

    private void ResetSimulatedDjLinkPlayers()
    {
        lock (stateLock)
        {
            foreach (var player in players.Values)
            {
                ClearSimulatedPlayer(player);
            }
        }
    }

    private void TriggerSimulatedDjLinkMetadataSend()
    {
        simulatedDjLinkTrackSeed++;
        simulatedDjLinkTrackStartTime = Time.realtimeSinceStartup;
        simulatedDjLinkPausedPlaybackMs = 0;
        ResetSimulatedDjLinkPlayers();
        UpdateSimulatedDjLinkPlayers();
    }

    private void EnsureSimulatedPlayerVisuals(PlayerState player, int deck, int durationMs)
    {
        var identity = deck * 1000 + simulatedDjLinkTrackSeed;
        var expectedPreviewName = "sim-preview-" + identity.ToString(CultureInfo.InvariantCulture);
        var expectedOverviewName = "sim-overview-" + identity.ToString(CultureInfo.InvariantCulture);
        var expectedArtName = "sim-art-" + identity.ToString(CultureInfo.InvariantCulture);

        if (player.BltWavePreview == null || player.BltWavePreview.name != expectedPreviewName)
        {
            if (player.BltWavePreview != null)
            {
                Destroy(player.BltWavePreview);
            }
            player.BltWavePreview = BuildSimulatedWaveTexture(640, BltWaveHeight, deck, simulatedDjLinkTrackSeed, false);
            player.BltWavePreview.name = expectedPreviewName;
        }

        if (player.BltWaveOverview == null || player.BltWaveOverview.name != expectedOverviewName)
        {
            if (player.BltWaveOverview != null)
            {
                Destroy(player.BltWaveOverview);
            }
            player.BltWaveOverview = BuildSimulatedWaveTexture(1280, BltOverviewWaveHeight, deck, simulatedDjLinkTrackSeed, true);
            player.BltWaveOverview.name = expectedOverviewName;
        }

        if (player.AlbumArtTexture == null || player.AlbumArtTexture.name != expectedArtName)
        {
            if (player.AlbumArtTexture != null)
            {
                Destroy(player.AlbumArtTexture);
            }
            player.AlbumArtTexture = BuildSimulatedAlbumArtTexture(192, 192, deck, simulatedDjLinkTrackSeed);
            player.AlbumArtTexture.name = expectedArtName;
            player.AlbumArtWidth = player.AlbumArtTexture.width;
            player.AlbumArtHeight = player.AlbumArtTexture.height;
        }

        if (player.BeatGrid == null)
        {
            player.BeatGrid = new System.Collections.Generic.List<BeatMarker>();
        }
        player.BeatGrid.Clear();
        var beatMs = 60000f / Mathf.Max(1f, simulatedDjLinkBpm);
        var beatCount = Mathf.Max(1, Mathf.FloorToInt(durationMs / beatMs));
        for (var i = 0; i < beatCount; i++)
        {
            player.BeatGrid.Add(new BeatMarker
            {
                TimeMs = Mathf.RoundToInt(i * beatMs),
                BeatWithinBar = (i % 4) + 1
            });
        }
    }

    private Texture2D BuildSimulatedAlbumArtTexture(int width, int height, int deck, int seed)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[width * height];
        var hue = Mathf.Repeat(0.12f * deck + 0.07f * (seed % 9), 1f);
        var baseColor = Color.HSVToRGB(hue, 0.75f, 0.95f);
        var accentColor = Color.HSVToRGB(Mathf.Repeat(hue + 0.24f, 1f), 0.72f, 1f);
        for (var y = 0; y < height; y++)
        {
            var fy = y / (float)Mathf.Max(1, height - 1);
            for (var x = 0; x < width; x++)
            {
                var fx = x / (float)Mathf.Max(1, width - 1);
                var ring = Mathf.Abs(Mathf.Sin((fx - fy) * 8.4f + deck));
                var mix = Mathf.Clamp01(0.35f + 0.65f * ring);
                var color = Color.Lerp(baseColor, accentColor, mix);
                if (Mathf.Abs(fx - 0.5f) < 0.07f || Mathf.Abs(fy - 0.5f) < 0.07f)
                {
                    color = Color.Lerp(color, Color.white, 0.32f);
                }
                pixels[y * width + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private Texture2D BuildSimulatedWaveTexture(int width, int height, int deck, int seed, bool overview)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[width * height];
        var background = new Color32(2, 4, 6, 255);
        var baseColor = deck % 2 == 0
            ? new Color32(78, 202, 255, 255)
            : new Color32(255, 178, 74, 255);
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        var center = height * 0.5f;
        for (var x = 0; x < width; x++)
        {
            var t = x / (float)Mathf.Max(1, width - 1);
            var amplitude = 0.18f +
                            0.24f * Mathf.Abs(Mathf.Sin((t * (overview ? 14f : 22f) + seed * 0.17f) * Mathf.PI)) +
                            0.13f * Mathf.Abs(Mathf.Sin((t * (overview ? 35f : 57f) + deck * 0.11f) * Mathf.PI));
            var pulse = 0.08f * Mathf.Abs(Mathf.Sin((t * 64f + seed) * Mathf.PI));
            var halfHeight = Mathf.Clamp(Mathf.RoundToInt((amplitude + pulse) * height * 0.48f), 1, Mathf.Max(2, height / 2 - 1));
            var startY = Mathf.Clamp(Mathf.RoundToInt(center) - halfHeight, 0, height - 1);
            var endY = Mathf.Clamp(Mathf.RoundToInt(center) + halfHeight, 0, height - 1);
            for (var y = startY; y <= endY; y++)
            {
                var index = y * width + x;
                var dist = Mathf.Abs(y - center) / Mathf.Max(1f, halfHeight);
                var alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 72f, dist));
                pixels[index] = new Color32(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
