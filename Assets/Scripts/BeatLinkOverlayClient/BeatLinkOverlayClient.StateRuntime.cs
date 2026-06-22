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
    private void SetError(string message)
    {
        lock (stateLock)
        {
            lastError = message;
            AddLog("DJ Link listener error: " + message);
        }
    }

    private PlayerState GetOrCreatePlayer(int deviceNumber)
    {
        PlayerState player;
        if (!players.TryGetValue(deviceNumber, out player))
        {
            player = new PlayerState(deviceNumber, ActivitySamples);
            players.Add(deviceNumber, player);
            AddLog("Discovered player " + deviceNumber + ".");
        }
        return player;
    }

    private void AddLog(string message)
    {
        lock (stateLock)
        {
            logLines.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + message);
            while (logLines.Count > 6)
            {
                logLines.RemoveAt(0);
            }
        }
    }

    private void RefreshPlayerTextures()
    {
        lock (stateLock)
        {
            foreach (var player in players.Values)
            {
                if (player.PendingAlbumArtBytes != null && player.PendingAlbumArtBytes.Length > 0)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(player.PendingAlbumArtBytes))
                    {
                        if (player.AlbumArtTexture != null)
                        {
                            Destroy(player.AlbumArtTexture);
                        }
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        player.AlbumArtTexture = texture;
                        player.AlbumArtWidth = texture.width;
                        player.AlbumArtHeight = texture.height;
                        Debug.Log("Player " + player.DeviceNumber.ToString(CultureInfo.InvariantCulture) +
                                  " album art decoded: " +
                                  texture.width.ToString(CultureInfo.InvariantCulture) + "x" +
                                  texture.height.ToString(CultureInfo.InvariantCulture) +
                                  " bytes=" + player.PendingAlbumArtBytes.Length.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Destroy(texture);
                    }
                    player.PendingAlbumArtBytes = null;
                }

                if (player.DirectWaveformBytes != null && player.DirectWaveformBytes.Length > 0)
                {
                    var frameCount = DirectWaveformFrameCount(player.DirectWaveformBytes, player.DirectWaveformStyle);
                    var zoomIndex = ClampWaveformZoomIndex(player.WaveformZoomIndex);
                    var zoom = WaveformZoomForIndex(zoomIndex);
                    var textureScale = WaveformTextureScaleForZoom(frameCount, zoom);
                    var textureKey = player.DirectWaveformKey + ":" +
                                     player.DirectWaveformStyle + ":" +
                                     player.DirectWaveformBytes.Length.ToString(CultureInfo.InvariantCulture) + ":" +
                                     zoomIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                                     textureScale.ToString(CultureInfo.InvariantCulture);
                    if (player.DirectWaveformTexture == null || player.DirectWaveformTextureKey != textureKey)
                    {
                        if (player.DirectWaveformTexture != null)
                        {
                            Destroy(player.DirectWaveformTexture);
                        }

                        player.DirectWaveformTexture = BuildDirectWaveformTexture(player.DirectWaveformBytes, player.DirectWaveformStyle, textureScale, BltWaveHeight);
                        player.DirectWaveformTextureKey = textureKey;
                        player.DirectWaveformTextureScale = textureScale;
                        player.DirectWaveformTextureFrameCount = frameCount;
                        player.DirectWaveformTextureWidth = player.DirectWaveformTexture == null ? 0 : player.DirectWaveformTexture.width;
                        player.WaveformZoomIndex = zoomIndex;
                    }
                }
            }
        }
    }
}
