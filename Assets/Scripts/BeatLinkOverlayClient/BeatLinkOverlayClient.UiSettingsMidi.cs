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
    private void DrawSettingsScreen(Rect full)
    {
        RefreshOutputDevices();

        var margin = 18f;
        GUI.Label(new Rect(margin, margin, 320f, 34f), "Setting", titleStyle);
        GUI.Label(new Rect(full.width - 360f, margin + 7f, 340f, 24f), "R / Esc: Return", smallStyle);

        var tabRect = new Rect(margin, margin + 48f, 180f, full.height - margin * 2f - 48f);
        GUI.DrawTexture(tabRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.028f, 0.032f, 0.038f, 1f), 0f, 6f);
        GUI.Label(new Rect(tabRect.x + 14f, tabRect.y + 14f, tabRect.width - 28f, 24f), "Setting", normalStyle);
        var tabY = tabRect.y + 52f;
        DrawSettingsTabButton(new Rect(tabRect.x + 12f, tabY, tabRect.width - 24f, 32f), SettingsScreenTab.Output, "Output");
        DrawSettingsTabButton(new Rect(tabRect.x + 12f, tabY + 38f, tabRect.width - 24f, 32f), SettingsScreenTab.Input, "Input");
        DrawSettingsTabButton(new Rect(tabRect.x + 12f, tabY + 76f, tabRect.width - 24f, 32f), SettingsScreenTab.Midi, "MIDI");
        DrawSettingsTabButton(new Rect(tabRect.x + 12f, tabY + 114f, tabRect.width - 24f, 32f), SettingsScreenTab.ProDjLink, "Pro DJ Link");
        DrawSettingsTabButton(new Rect(tabRect.x + 12f, tabY + 152f, tabRect.width - 24f, 32f), SettingsScreenTab.Fft, "FFT");

        var panel = new Rect(tabRect.xMax + 14f, tabRect.y, full.width - tabRect.xMax - margin - 14f, tabRect.height);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);

        GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, panel.height - 36f));
        settingsScroll = GUILayout.BeginScrollView(settingsScroll);
        DrawSelectedSettingsTab();
        GUILayout.Space(18f);
        GUILayout.Label("Status", normalStyle);
        GUILayout.Label(string.IsNullOrEmpty(outputStatus) ? "No output change applied." : outputStatus, smallStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSelectedSettingsTab()
    {
        switch (selectedSettingsTab)
        {
            case SettingsScreenTab.Output:
                DrawSettingsOutputTab();
                break;
            case SettingsScreenTab.Input:
                DrawSettingsInputTab();
                break;
            case SettingsScreenTab.Midi:
                DrawSettingsMidiTab();
                break;
            case SettingsScreenTab.ProDjLink:
                DrawSettingsProDjLinkTab();
                break;
            case SettingsScreenTab.Fft:
                DrawSettingsFftTab();
                break;
        }
    }

    private void DrawSettingsTabButton(Rect rect, SettingsScreenTab tab, string label)
    {
        var selected = selectedSettingsTab == tab;
        var background = selected ? new Color(0.2f, 0.14f, 0.05f, 1f) : new Color(0.07f, 0.08f, 0.095f, 1f);
        var border = selected ? new Color(0.92f, 0.58f, 0.18f, 1f) : new Color(0.14f, 0.16f, 0.2f, 1f);
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, background, 0f, 4f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            selectedSettingsTab = tab;
        }
        GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f), label, smallStyle);
    }

    private void DrawSettingsOutputTab()
    {
        GUILayout.Label("Output", titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label("Video", normalStyle);
        GUILayout.Label("Final program output fullscreen display", smallStyle);
        DrawOutputDropdown("Display", videoOutputDevices, ref selectedVideoOutputIndex, ref videoOutputDropdownOpen);
        DrawOutputDropdown("Resolution", outputResolutionOptions, ref selectedOutputResolutionIndex, ref outputResolutionDropdownOpen);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Video Output", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyVideoOutputSelection();
        }
        if (GUILayout.Button("Apply Resolution", GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            ApplyOutputResolutionSelection();
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            outputDevicesDirty = true;
            RefreshOutputDevices();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Render / program resolution: " + outputWidth.ToString(CultureInfo.InvariantCulture) + "x" + outputHeight.ToString(CultureInfo.InvariantCulture) + "   Source layers and effect layers follow this size.", smallStyle);

        GUILayout.Space(18f);
        DrawDisplayRoutingSettingsSection();

        GUILayout.Space(18f);
        GUILayout.Label("Audio Output", normalStyle);
        DrawOutputDropdown("Audio Device", audioOutputDevices, ref selectedAudioOutputIndex, ref audioOutputDropdownOpen);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Audio Output", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyAudioOutputSelection();
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            outputDevicesDirty = true;
            RefreshOutputDevices();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Unity cannot route VideoPlayer audio to a specific Windows device without a native CoreAudio output layer. This selection is saved for the next routing step.", smallStyle);
    }

    private void DrawSettingsInputTab()
    {
        GUILayout.Label("Input", titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label("Capture Audio", normalStyle);
        GUILayout.Label("HDMI capture / iPad audio input", smallStyle);
        DrawOutputDropdown("Input Device", audioInputDevices, ref selectedAudioInputIndex, ref audioInputDropdownOpen);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Audio Input", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyCaptureAudioInputSelection();
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            audioInputDevicesDirty = true;
            RefreshAudioInputDevices();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        captureAudioMonitorEnabled = GUILayout.Toggle(captureAudioMonitorEnabled, "Monitor / mix to app output", GUILayout.Width(220f));
        GUILayout.Label("Volume", GUILayout.Width(52f));
        var nextMonitorVolume = GUILayout.HorizontalSlider(captureAudioMonitorVolume, 0f, 1f, GUILayout.Width(180f));
        GUILayout.Label(Mathf.RoundToInt(nextMonitorVolume * 100f).ToString(CultureInfo.InvariantCulture), GUILayout.Width(40f));
        if (Mathf.Abs(nextMonitorVolume - captureAudioMonitorVolume) > 0.001f)
        {
            captureAudioMonitorVolume = nextMonitorVolume;
            UpdateCaptureAudioMonitorState();
        }
        GUILayout.EndHorizontal();
        DrawCaptureAudioLevelMeter(GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true)));
        GUILayout.Label(string.IsNullOrEmpty(captureAudioStatus) ? "No capture audio input active." : captureAudioStatus, smallStyle);
    }

    private void DrawSettingsMidiTab()
    {
        GUILayout.Label("MIDI", titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label("MIDI Controller", normalStyle);
        DrawMidiInputDropdown();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply MIDI Input", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyMidiInputSelection();
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            midiDevicesDirty = true;
            RefreshMidiInputDevices();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(string.IsNullOrEmpty(midiStatus) ? "No MIDI status." : midiStatus, smallStyle);
        GUILayout.Label("Last input: " + (string.IsNullOrEmpty(midiLastMessageText) ? "--" : midiLastMessageText), smallStyle);
        GUILayout.Space(10f);
        GUILayout.Label("Assignment", normalStyle);
        GUILayout.Label("Use MIDI Edit on the layer screen. Cyan areas can be learned directly from the VJ layer UI.", smallStyle);
    }

    private void DrawSettingsProDjLinkTab()
    {
        GUILayout.Label("Pro DJ Link", titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label("Mode selection", normalStyle);
        DrawMetadataModeButton(DjLinkInputMode.InternalDirect, "Internal mode", "Unity direct UDP");
        GUILayout.Space(6f);
        DrawMetadataModeButton(DjLinkInputMode.ExternalBlt, "External mode", "Beat Link Trigger");
        GUILayout.Space(6f);
        DrawMetadataModeButton(DjLinkInputMode.Simulator, "Simulator mode", "Fake players + metadata for testing");
        GUILayout.Space(6f);
        var recognizedBltPath = GetOfficialBltExecutablePath();
        var bltRecognized = !string.IsNullOrEmpty(recognizedBltPath);
        GUILayout.Label("Beat Link Trigger: " + (bltRecognized ? "recognized" : "not recognized"), smallStyle);
        if (bltRecognized)
        {
            GUILayout.Label(recognizedBltPath, smallStyle);
        }
        switch (pendingDjLinkInputMode)
        {
            case DjLinkInputMode.ExternalBlt:
                GUILayout.Label("External mode uses the local Beat Link Trigger app for metadata and waveform.", smallStyle);
                break;
            case DjLinkInputMode.Simulator:
                GUILayout.Label("Simulator mode generates fake decks, metadata, timing, and waveform previews inside Unity.", smallStyle);
                break;
            default:
                GUILayout.Label("Internal mode uses Unity direct UDP. If rekordbox or BLT is running, this mode is blocked.", smallStyle);
                break;
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Params URL", GUILayout.Width(110f));
        pendingBltParamsUrl = GUILayout.TextField(string.IsNullOrEmpty(pendingBltParamsUrl) ? DefaultBltParamsUrl : pendingBltParamsUrl, GUILayout.MinWidth(360f), GUILayout.Height(26f));
        GUILayout.EndHorizontal();
        if (pendingDjLinkInputMode == DjLinkInputMode.Simulator || CurrentDjLinkInputMode() == DjLinkInputMode.Simulator)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Simulator", normalStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Decks", GUILayout.Width(110f));
            if (GUILayout.Button(simulatedDjLinkDeckCount == 2 ? "> 2" : "2", GUILayout.Width(48f), GUILayout.Height(26f)))
            {
                simulatedDjLinkDeckCount = 2;
            }
            if (GUILayout.Button(simulatedDjLinkDeckCount == 4 ? "> 4" : "4", GUILayout.Width(48f), GUILayout.Height(26f)))
            {
                simulatedDjLinkDeckCount = 4;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("BPM", GUILayout.Width(110f));
            var nextSimulatorBpm = GUILayout.HorizontalSlider(simulatedDjLinkBpm, 60f, 180f, GUILayout.Width(240f));
            if (Mathf.Abs(nextSimulatorBpm - simulatedDjLinkBpm) > 0.01f)
            {
                simulatedDjLinkBpm = nextSimulatorBpm;
            }
            GUILayout.Label(simulatedDjLinkBpm.ToString("0.0", CultureInfo.InvariantCulture), GUILayout.Width(54f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(simulatedDjLinkPlaying ? "Pause Sim" : "Play Sim", GUILayout.Width(120f), GUILayout.Height(26f)))
            {
                if (simulatedDjLinkPlaying)
                {
                    simulatedDjLinkPausedPlaybackMs = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, Time.realtimeSinceStartup - simulatedDjLinkTrackStartTime) * 1000f));
                }
                simulatedDjLinkPlaying = !simulatedDjLinkPlaying;
                if (simulatedDjLinkPlaying)
                {
                    simulatedDjLinkTrackStartTime = Time.realtimeSinceStartup - simulatedDjLinkPausedPlaybackMs / 1000f;
                }
            }
            if (GUILayout.Button("Send Metadata", GUILayout.Width(140f), GUILayout.Height(26f)))
            {
                TriggerSimulatedDjLinkMetadataSend();
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Metadata Mode", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyBltBridgeModeSelection();
        }
        if (bltRecognized && GUILayout.Button(IsOfficialBltProcessRunning() ? "BLT Running" : "Start BLT", GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            EnsureOfficialBltRunning(true);
        }
        if (GUILayout.Button("Open BLT Screen", GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            OpenBltScreen();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Selected: " + DjLinkInputModeLabel(pendingDjLinkInputMode), smallStyle);
        GUILayout.Label("Current: " + DjLinkInputModeLabel(CurrentDjLinkInputMode()), smallStyle);
        GUILayout.Label("BLT status: " + BltStatusText(), smallStyle);
    }

    private void DrawSettingsFftTab()
    {
        EnsureCaptureAudioEqDefaults();
        GUILayout.Label("FFT", titleStyle);
        GUILayout.Space(10f);
        var nextEnabled = GUILayout.Toggle(captureAudioFftEnabled, "Enable FFT analysis");
        if (nextEnabled != captureAudioFftEnabled)
        {
            captureAudioFftEnabled = nextEnabled;
            SaveCaptureAudioFftPreferences();
        }

        GUILayout.Label("Audio input follows the device selected in Input.", smallStyle);
        GUILayout.Label("4-band parametric EQ. Drag points for frequency/gain, mouse wheel on a point for Q.", smallStyle);
        GUILayout.Label("Band 1 toggles Low Shelf / High Pass. Band 4 toggles High Shelf / Low Pass.", smallStyle);
        GUILayout.Space(8f);

        var visibleUpperHz = Mathf.Max(1000f, Mathf.Min(CaptureAudioSpectrumDisplayMaxHz, CaptureAudioSpectrumUpperHz()));
        GUILayout.Label("Visible Range", smallStyle);
        var nextViewMinHz = GUILayout.HorizontalSlider(captureAudioSpectrumViewMinHz, CaptureAudioSpectrumDisplayMinHz, Mathf.Max(CaptureAudioSpectrumDisplayMinHz, visibleUpperHz - 500f), GUILayout.Width(360f));
        if (Mathf.Abs(nextViewMinHz - captureAudioSpectrumViewMinHz) > 0.5f)
        {
            captureAudioSpectrumViewMinHz = nextViewMinHz;
            if (captureAudioSpectrumViewMaxHz < captureAudioSpectrumViewMinHz + 500f)
            {
                captureAudioSpectrumViewMaxHz = captureAudioSpectrumViewMinHz + 500f;
            }
            SaveCaptureAudioFftPreferences();
        }
        GUILayout.Label("Min: " + Mathf.RoundToInt(captureAudioSpectrumViewMinHz).ToString(CultureInfo.InvariantCulture) + " Hz", smallStyle);

        var nextViewMaxHz = GUILayout.HorizontalSlider(captureAudioSpectrumViewMaxHz, Mathf.Min(500f, visibleUpperHz), visibleUpperHz, GUILayout.Width(360f));
        if (Mathf.Abs(nextViewMaxHz - captureAudioSpectrumViewMaxHz) > 0.5f)
        {
            captureAudioSpectrumViewMaxHz = Mathf.Max(nextViewMaxHz, captureAudioSpectrumViewMinHz + 500f);
            SaveCaptureAudioFftPreferences();
        }
        GUILayout.Label("Max: " + Mathf.RoundToInt(captureAudioSpectrumViewMaxHz).ToString(CultureInfo.InvariantCulture) + " Hz", smallStyle);

        GUILayout.Space(8f);
        DrawCaptureAudioSpectrum(GUILayoutUtility.GetRect(1f, 420f, GUILayout.ExpandWidth(true)));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset EQ", GUILayout.Width(120f), GUILayout.Height(26f)))
        {
            for (var i = 0; i < captureAudioEqBands.Length; i++)
            {
                ResetCaptureAudioEqBand(i);
            }
            SaveCaptureAudioFftPreferences();
        }
        var selectedBand = captureAudioEqBands[Mathf.Clamp(captureAudioEqSelectedBand, 0, captureAudioEqBands.Length - 1)];
        if (GUILayout.Button("Mode: " + CaptureAudioEqBandTypeLabel(selectedBand.Type), GUILayout.Width(140f), GUILayout.Height(26f)))
        {
            CycleCaptureAudioEqBandMode(captureAudioEqSelectedBand);
            SaveCaptureAudioFftPreferences();
        }
        GUILayout.Label(
            "Band " + (captureAudioEqSelectedBand + 1).ToString(CultureInfo.InvariantCulture) +
            "  " + Mathf.RoundToInt(selectedBand.FrequencyHz).ToString(CultureInfo.InvariantCulture) + " Hz" +
            "  " + (CaptureAudioEqBandSupportsGain(selectedBand) ? selectedBand.GainDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB" : "Cut") +
            "  Q " + selectedBand.Q.ToString("0.00", CultureInfo.InvariantCulture),
            smallStyle);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Input Gain", GUILayout.Width(110f));
        var nextInputGain = GUILayout.HorizontalSlider(captureAudioEqInputGainDb, CaptureAudioEqInputGainMinDb, CaptureAudioEqInputGainMaxDb, GUILayout.Width(240f));
        if (Mathf.Abs(nextInputGain - captureAudioEqInputGainDb) > 0.01f)
        {
            captureAudioEqInputGainDb = nextInputGain;
            SaveCaptureAudioFftPreferences();
        }
        GUILayout.Label(captureAudioEqInputGainDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB", smallStyle);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Output Gain", GUILayout.Width(110f));
        var nextOutputGain = GUILayout.HorizontalSlider(captureAudioEqOutputGainDb, CaptureAudioEqOutputGainMinDb, CaptureAudioEqOutputGainMaxDb, GUILayout.Width(240f));
        if (Mathf.Abs(nextOutputGain - captureAudioEqOutputGainDb) > 0.01f)
        {
            captureAudioEqOutputGainDb = nextOutputGain;
            SaveCaptureAudioFftPreferences();
        }
        GUILayout.Label(captureAudioEqOutputGainDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB", smallStyle);
        GUILayout.EndHorizontal();
        GUILayout.Label(
            "Input gain changes analyzer level before EQ. Output gain changes level after EQ. EQ drive: " + captureAudioFftDrive.ToString("0.00", CultureInfo.InvariantCulture),
            smallStyle);
    }

    private void DrawDisplayRoutingSettingsSection()
    {
        GUILayout.Label("Display", titleStyle);
        GUILayout.Label("Detected Unity displays", normalStyle);
        if (displayRouteDevices.Count == 0)
        {
            GUILayout.Label("No Unity display information is available.", smallStyle);
        }
        else
        {
            GUILayout.BeginVertical(boxStyle);
            for (var i = 0; i < displayRouteDevices.Count; i++)
            {
                var option = displayRouteDevices[i];
                var role = option.Index == 0 ? "Main layers" : DisplayRoleSummary(option.Index);
                var row = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
                if (displayRouteDragActive && Event.current.type == EventType.MouseUp && row.Contains(Event.current.mousePosition))
                {
                    AssignDisplayRouteSelection(displayRouteDragKind, i);
                    displayRouteDragActive = false;
                    Event.current.Use();
                }
                var color = displayRouteDragActive && row.Contains(Event.current.mousePosition)
                    ? new Color(0.12f, 0.2f, 0.24f, 1f)
                    : new Color(0.045f, 0.05f, 0.058f, 1f);
                GUI.DrawTexture(row, whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 3f);
                GUI.Label(new Rect(row.x + 8f, row.y + 3f, row.width - 16f, row.height - 4f), option.Label + "  /  " + role, smallStyle);
            }
            GUILayout.EndVertical();
        }

        var windowsMonitors = EnumerateWindowsMonitors();
        if (windowsMonitors.Count > displayRouteDevices.Count)
        {
            GUILayout.Label("Windows monitors not exposed by Unity may require app restart after connecting the monitor.", smallStyle);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Screen routing", normalStyle);
        DrawFixedDisplayRouteRow("Main layer screen", "Display 1", ref mainLayerShortcutInput);
        DrawDisplayRouteRow("CDJ waveform / BLT screen", RoutedScreenKind.CdjWave, displayRouteDevices, ref selectedCdjWaveDisplayIndex, ref cdjWaveDisplayDropdownOpen, ref cdjWaveShortcutInput);
        DrawDisplayRouteRow("Layer settings screen", RoutedScreenKind.LayerSettings, displayRouteDevices, ref selectedLayerSettingsDisplayIndex, ref layerSettingsDisplayDropdownOpen, ref layerSettingsShortcutInput);
        DrawDisplayRouteRow("Effect edit screen", RoutedScreenKind.EffectSettings, displayRouteDevices, ref selectedEffectSettingsDisplayIndex, ref effectSettingsDisplayDropdownOpen, ref effectSettingsShortcutInput);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Display Routing", GUILayout.Width(190f), GUILayout.Height(28f)))
        {
            ApplyDisplayRoutingSelection();
        }
        if (GUILayout.Button("Refresh Displays", GUILayout.Width(150f), GUILayout.Height(28f)))
        {
            outputDevicesDirty = true;
            RefreshOutputDevices();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Drag a screen-routing label onto a detected display row, or use the dropdown. Display 1 means the current main window. Display 2+ uses Unity multi-display output.", smallStyle);
    }

    private string DisplayRoleSummary(int displayIndex)
    {
        var roles = new List<string>();
        if (activeProgramDisplay == displayIndex)
        {
            roles.Add("Program output");
        }
        if (cdjWaveDisplayIndex == displayIndex)
        {
            roles.Add("CDJ waveform");
        }
        if (layerSettingsDisplayIndex == displayIndex)
        {
            roles.Add("Layer settings");
        }
        if (effectSettingsDisplayIndex == displayIndex)
        {
            roles.Add("Effect settings");
        }
        return roles.Count == 0 ? "Available" : string.Join(", ", roles.ToArray());
    }

    private void DrawFixedDisplayRouteRow(string label, string displayLabel, ref string shortcutInput)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(190f));
        GUILayout.Label(displayLabel, GUILayout.Width(320f));
        GUILayout.Label("Shortcut", GUILayout.Width(70f));
        shortcutInput = GUILayout.TextField(shortcutInput, GUILayout.Width(92f), GUILayout.Height(26f));
        GUILayout.EndHorizontal();
    }

    private void DrawDisplayRouteRow(string label, RoutedScreenKind kind, List<OutputDeviceOption> options, ref int selectedIndex, ref bool open, ref string shortcutInput)
    {
        GUILayout.BeginHorizontal();
        var labelRect = GUILayoutUtility.GetRect(190f, 26f, GUILayout.Width(190f), GUILayout.Height(26f));
        GUI.Label(labelRect, label, smallStyle);
        HandleDisplayRouteDragSource(labelRect, kind);
        var text = options != null && selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex].Label : "(none)";
        if (GUILayout.Button(text, GUILayout.Width(320f), GUILayout.Height(26f)))
        {
            open = !open;
        }
        GUILayout.Label("Shortcut", GUILayout.Width(70f));
        shortcutInput = GUILayout.TextField(shortcutInput, GUILayout.Width(92f), GUILayout.Height(26f));
        GUILayout.EndHorizontal();

        if (!open)
        {
            return;
        }

        GUILayout.BeginVertical(boxStyle);
        if (options == null || options.Count == 0)
        {
            GUILayout.Label("No displays found.", smallStyle);
        }
        else
        {
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var itemLabel = (i == selectedIndex ? "> " : "  ") + option.Label;
                if (GUILayout.Button(itemLabel, GUILayout.Height(24f)))
                {
                    selectedIndex = i;
                    open = false;
                }
            }
        }
        GUILayout.EndVertical();
    }

    private void HandleDisplayRouteDragSource(Rect rect, RoutedScreenKind kind)
    {
        var evt = Event.current;
        if (evt == null)
        {
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
        {
            displayRouteDragActive = true;
            displayRouteDragKind = kind;
            evt.Use();
        }
        else if (displayRouteDragActive && evt.type == EventType.MouseUp)
        {
            displayRouteDragActive = false;
        }

        if (displayRouteDragActive && displayRouteDragKind == kind)
        {
            DrawRectOutline(rect, 2f, new Color(0.18f, 0.58f, 0.95f, 1f));
        }
    }

    private void AssignDisplayRouteSelection(RoutedScreenKind kind, int selectedIndex)
    {
        switch (kind)
        {
            case RoutedScreenKind.CdjWave:
                selectedCdjWaveDisplayIndex = selectedIndex;
                break;
            case RoutedScreenKind.LayerSettings:
                selectedLayerSettingsDisplayIndex = selectedIndex;
                break;
            case RoutedScreenKind.EffectSettings:
                selectedEffectSettingsDisplayIndex = selectedIndex;
                break;
        }
    }

    private void ApplyDisplayRoutingSelection()
    {
        cdjWaveDisplayIndex = SelectedDisplayRouteIndex(selectedCdjWaveDisplayIndex);
        layerSettingsDisplayIndex = SelectedDisplayRouteIndex(selectedLayerSettingsDisplayIndex);
        effectSettingsDisplayIndex = SelectedDisplayRouteIndex(selectedEffectSettingsDisplayIndex);
        mainLayerShortcutKey = ParseShortcutKey(mainLayerShortcutInput, KeyCode.R);
        cdjWaveShortcutKey = ParseShortcutKey(cdjWaveShortcutInput, KeyCode.Space);
        layerSettingsShortcutKey = ParseShortcutKey(layerSettingsShortcutInput, KeyCode.E);
        effectSettingsShortcutKey = ParseShortcutKey(effectSettingsShortcutInput, KeyCode.E);
        mainLayerShortcutInput = KeyCodeToShortcutText(mainLayerShortcutKey);
        cdjWaveShortcutInput = KeyCodeToShortcutText(cdjWaveShortcutKey);
        layerSettingsShortcutInput = KeyCodeToShortcutText(layerSettingsShortcutKey);
        effectSettingsShortcutInput = KeyCodeToShortcutText(effectSettingsShortcutKey);
        SaveDisplayRoutingPreferences();
        SaveScreenShortcutPreferences();
        if (IsDisplayReservedForAuxUi(activeProgramDisplay))
        {
            DisableProgramOutput("Program output disabled because the display is now reserved for routed UI.");
        }
        if (secondDisplayUiMode != SecondDisplayUiMode.None)
        {
            secondDisplayUiEnabled = HasAuxDisplayRoute();
            EnsureMonitorDisplays();
            UpdateMonitorDisplays(force: true);
        }
        outputStatus = "Display routing applied.";
    }

    private int SelectedDisplayRouteIndex(int selectedIndex)
    {
        if (displayRouteDevices == null || selectedIndex < 0 || selectedIndex >= displayRouteDevices.Count)
        {
            return 0;
        }
        return Mathf.Max(0, displayRouteDevices[selectedIndex].Index);
    }

    private void DrawOutputDropdown(string label, List<OutputDeviceOption> options, ref int selectedIndex, ref bool open)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110f));
        var text = options != null && selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex].Label : "(none)";
        if (GUILayout.Button(text, GUILayout.MinWidth(320f), GUILayout.Height(28f)))
        {
            open = !open;
        }
        GUILayout.EndHorizontal();

        if (!open)
        {
            return;
        }

        GUILayout.BeginVertical(boxStyle);
        if (options == null || options.Count == 0)
        {
            GUILayout.Label("No devices found.", smallStyle);
        }
        else
        {
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var itemLabel = (i == selectedIndex ? "> " : "  ") + option.Label;
                if (GUILayout.Button(itemLabel, GUILayout.Height(26f)))
                {
                    selectedIndex = i;
                    open = false;
                }
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawCaptureAudioLevelMeter(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.015f, 0.018f, 0.022f, 1f), 0f, 3f);
        var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
        GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(captureAudioLevelRms * 4.5f), inner.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.16f, 0.82f, 0.58f, 0.95f), 0f, 2f);
        GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(captureAudioLevelPeak * 4.5f), inner.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.86f, 0.18f, 0.95f), 0f, 0f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 18f),
            "Input level   RMS " + captureAudioLevelRms.ToString("0.000", CultureInfo.InvariantCulture) +
            "   Peak " + captureAudioLevelPeak.ToString("0.000", CultureInfo.InvariantCulture),
            smallStyle);
    }

    private void DrawCaptureAudioSpectrum(Rect rect)
    {
        EnsureCaptureAudioEqDefaults();
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.012f, 0.015f, 0.018f, 1f), 0f, 3f);
        var inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        GUI.DrawTexture(inner, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 2f);

        if (captureAudioSpectrumBands == null || captureAudioSpectrumBands.Length == 0)
        {
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 18f), "FFT unavailable", smallStyle);
            return;
        }

        HandleCaptureAudioSpectrumDrag(inner);

        var graph = CaptureAudioSpectrumGraphRect(inner);
        DrawCaptureAudioSpectrumAxes(inner, graph);
        DrawSpectrumCurve(graph, new Color(0.22f, 0.72f, 1f, 0.95f), SpectrumCurveMode.Raw);
        DrawSpectrumCurve(graph, new Color(1f, 0.92f, 0.28f, 0.98f), SpectrumCurveMode.Filtered);
        DrawSpectrumCurve(graph, new Color(1f, 0.4f, 0.26f, 0.86f), SpectrumCurveMode.FilterResponse);
        DrawCaptureAudioSpectrumBandPass(graph);

        GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 18f),
            (captureAudioFftEnabled ? "FFT ON" : "FFT OFF") +
            "   Log analyzer   Blue: Raw   Yellow: EQ output   Red: EQ curve" +
            "   Drive " + captureAudioFftDrive.ToString("0.00", CultureInfo.InvariantCulture),
            smallStyle);
    }

    private enum SpectrumCurveMode
    {
        Raw,
        Filtered,
        FilterResponse
    }

    private Rect CaptureAudioSpectrumGraphRect(Rect inner)
    {
        const float axisLeft = 64f;
        const float axisBottom = 72f;
        const float axisTop = 28f;
        const float axisRight = 82f;
        return new Rect(inner.x + axisLeft, inner.y + axisTop, Mathf.Max(220f, inner.width - axisLeft - axisRight), Mathf.Max(180f, inner.height - axisTop - axisBottom));
    }

    private void DrawSpectrumCurve(Rect graph, Color color, SpectrumCurveMode mode)
    {
        var previousPoint = Vector2.zero;
        var hasPreviousPoint = false;
        var pointCount = Mathf.Max(160, Mathf.RoundToInt(graph.width * 0.6f));
        for (var i = 0; i < pointCount; i++)
        {
            var x = Mathf.Lerp(graph.x, graph.xMax, i / (float)Mathf.Max(1, pointCount - 1));
            var frequencyHz = GraphXToFrequencyHz(graph, x);
            var db = mode == SpectrumCurveMode.FilterResponse
                ? CaptureAudioBandPassResponseDb(frequencyHz)
                : SampleSpectrumDb(mode == SpectrumCurveMode.Filtered ? captureAudioFilteredSpectrumBandDb : captureAudioSpectrumBandDb, frequencyHz);
            var point = new Vector2(x, DbToGraphY(graph, db));
            if (hasPreviousPoint)
            {
                DrawLine(previousPoint, point, color, mode == SpectrumCurveMode.FilterResponse ? 1.6f : 2.5f);
            }
            previousPoint = point;
            hasPreviousPoint = true;
        }
    }

    private void DrawCaptureAudioSpectrumAxes(Rect inner, Rect graph)
    {
        GUI.DrawTexture(graph, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.024f, 0.03f, 1f), 0f, 2f);
        DrawLine(new Vector2(graph.x, graph.yMax), new Vector2(graph.xMax, graph.yMax), new Color(0.45f, 0.48f, 0.54f, 1f), 1f);
        DrawLine(new Vector2(graph.x, graph.y), new Vector2(graph.x, graph.yMax), new Color(0.45f, 0.48f, 0.54f, 1f), 1f);

        var dbMarks = new[] { 24f, 18f, 12f, 6f, 0f, -12f, -24f, -36f, -48f, -60f, -72f };
        for (var i = 0; i < dbMarks.Length; i++)
        {
            var db = dbMarks[i];
            if (db > captureAudioEqDisplayMaxGainDb + 0.01f)
            {
                continue;
            }
            var y = DbToGraphY(graph, db);
            var color = Mathf.Abs(db) < 0.01f
                ? new Color(0.34f, 0.38f, 0.44f, 1f)
                : new Color(0.14f, 0.17f, 0.2f, 1f);
            DrawLine(new Vector2(graph.x, y), new Vector2(graph.xMax, y), color, Mathf.Abs(db) < 0.01f ? 1.4f : 1f);
            GUI.Label(new Rect(inner.x + 2f, y - 8f, 46f, 16f), Mathf.RoundToInt(db).ToString(CultureInfo.InvariantCulture), smallStyle);
        }

        var hzMarks = BuildSpectrumHzTicks();
        for (var i = 0; i < hzMarks.Length; i++)
        {
            var hz = hzMarks[i];
            if (hz < captureAudioSpectrumViewMinHz || hz > captureAudioSpectrumViewMaxHz)
            {
                continue;
            }
            var x = BandFrequencyToGraphX(graph, hz);
            DrawLine(new Vector2(x, graph.y), new Vector2(x, graph.yMax), new Color(0.1f, 0.12f, 0.16f, 1f), 1f);
            var label = hz >= 1000f
                ? (hz / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k"
                : Mathf.RoundToInt(hz).ToString(CultureInfo.InvariantCulture);
            GUI.Label(new Rect(x - 28f, graph.yMax + 10f, 56f, 18f), label, smallStyle);
        }

        GUI.Label(new Rect(inner.x + 2f, graph.y - 16f, 40f, 16f), "dB", smallStyle);
        GUI.Label(new Rect(graph.xMax - 24f, graph.yMax + 34f, 24f, 18f), "Hz", smallStyle);
        var sliderRect = new Rect(graph.xMax + 18f, graph.y + 8f, 18f, graph.height - 16f);
        var nextDisplayMax = GUI.VerticalSlider(sliderRect, captureAudioEqDisplayMaxGainDb, CaptureAudioEqDisplayGainMaxDb, CaptureAudioEqDisplayGainMinDb);
        if (Mathf.Abs(nextDisplayMax - captureAudioEqDisplayMaxGainDb) > 0.01f)
        {
            captureAudioEqDisplayMaxGainDb = nextDisplayMax;
            SaveCaptureAudioFftPreferences();
        }
        GUI.Label(new Rect(graph.xMax + 42f, graph.y - 2f, 40f, 18f), "Max", smallStyle);
        GUI.Label(new Rect(graph.xMax + 42f, graph.y + 14f, 40f, 18f), "+" + Mathf.RoundToInt(captureAudioEqDisplayMaxGainDb).ToString(CultureInfo.InvariantCulture), smallStyle);
        GUI.Label(new Rect(graph.xMax + 42f, graph.y + 30f, 40f, 18f), "dB", smallStyle);
    }

    private void DrawCaptureAudioSpectrumBandPass(Rect graph)
    {
        for (var i = 0; i < captureAudioEqBands.Length; i++)
        {
            var band = captureAudioEqBands[i];
            var displayGain = CaptureAudioEqBandSupportsGain(band) ? band.GainDb : 0f;
            var center = new Vector2(BandFrequencyToGraphX(graph, band.FrequencyHz), DbToGraphY(graph, displayGain));
            var zeroY = DbToGraphY(graph, 0f);
            var color = CaptureAudioEqBandColor(i);
            DrawLine(new Vector2(center.x, zeroY), center, new Color(color.r, color.g, color.b, 0.32f), 1.2f);
            DrawSpectrumHandle(center, captureAudioEqDragBand == i || captureAudioEqSelectedBand == i, color);
        }
    }

    private void DrawSpectrumHandle(Vector2 center, bool active, Color color)
    {
        var size = active ? 14f : 11f;
        GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 6f);
        if (active)
        {
            DrawRectOutline(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), 1.5f, Color.white);
        }
    }

    private void HandleCaptureAudioSpectrumDrag(Rect inner)
    {
        EnsureCaptureAudioEqDefaults();
        var evt = Event.current;
        if (evt == null)
        {
            return;
        }

        var graph = CaptureAudioSpectrumGraphRect(inner);

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            var bandIndex = FindCaptureAudioEqBandHandle(graph, evt.mousePosition);
            if (bandIndex >= 0)
            {
                captureAudioEqDragBand = bandIndex;
                captureAudioEqSelectedBand = bandIndex;
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseDown && evt.button == 1)
        {
            var bandIndex = FindCaptureAudioEqBandHandle(graph, evt.mousePosition);
            if (bandIndex >= 0)
            {
                ResetCaptureAudioEqBand(bandIndex);
                captureAudioEqSelectedBand = bandIndex;
                SaveCaptureAudioFftPreferences();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseDrag && captureAudioEqDragBand >= 0)
        {
            var band = captureAudioEqBands[captureAudioEqDragBand];
            band.FrequencyHz = Mathf.Clamp(GraphXToFrequencyHz(graph, evt.mousePosition.x), CaptureAudioSpectrumDisplayMinHz, CaptureAudioSpectrumDisplayMaxHz);
            if (CaptureAudioEqBandSupportsGain(band))
            {
                band.GainDb = Mathf.Clamp(GraphYToDb(graph, evt.mousePosition.y), CaptureAudioEqGainMinDb, CaptureAudioEqGainMaxDb);
            }
            else
            {
                band.GainDb = 0f;
            }
            SaveCaptureAudioFftPreferences();
            evt.Use();
        }
        else if (evt.type == EventType.ScrollWheel)
        {
            var bandIndex = FindCaptureAudioEqBandHandle(graph, evt.mousePosition);
            if (bandIndex >= 0)
            {
                var band = captureAudioEqBands[bandIndex];
                band.Q = Mathf.Clamp(band.Q + (-evt.delta.y * 0.05f), CaptureAudioEqQMin, CaptureAudioEqQMax);
                captureAudioEqSelectedBand = bandIndex;
                SaveCaptureAudioFftPreferences();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseUp && captureAudioEqDragBand >= 0)
        {
            captureAudioEqDragBand = -1;
            evt.Use();
        }
    }

    private int FindCaptureAudioEqBandHandle(Rect graph, Vector2 mousePosition)
    {
        var bestIndex = -1;
        var bestDistance = 16f;
        for (var i = 0; i < captureAudioEqBands.Length; i++)
        {
            var band = captureAudioEqBands[i];
            var displayGain = CaptureAudioEqBandSupportsGain(band) ? band.GainDb : 0f;
            var center = new Vector2(BandFrequencyToGraphX(graph, band.FrequencyHz), DbToGraphY(graph, displayGain));
            var distance = Vector2.Distance(mousePosition, center);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static Color CaptureAudioEqBandColor(int index)
    {
        switch (index)
        {
            case 0: return new Color(0.22f, 0.86f, 0.82f, 1f);
            case 1: return new Color(0.46f, 0.84f, 0.28f, 1f);
            case 2: return new Color(1f, 0.75f, 0.22f, 1f);
            default: return new Color(1f, 0.44f, 0.34f, 1f);
        }
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        var savedMatrix = GUI.matrix;
        var savedColor = GUI.color;
        var angle = Vector3.Angle(b - a, Vector2.right);
        if (a.y > b.y)
        {
            angle = -angle;
        }
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, (b - a).magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = savedMatrix;
        GUI.color = savedColor;
    }

    private float CaptureAudioSpectrumUpperHz()
    {
        if (captureAudioClip != null && captureAudioClip.frequency > 0)
        {
            return captureAudioClip.frequency * 0.5f;
        }
        return 24000f;
    }

    private float CaptureAudioSpectrumDisplayUpperHz()
    {
        return Mathf.Min(CaptureAudioSpectrumDisplayMaxHz, CaptureAudioSpectrumUpperHz());
    }

    private float BandCenterFrequencyHz(int bandIndex)
    {
        var nyquist = CaptureAudioSpectrumUpperHz();
        var t = (bandIndex + 0.5f) / Mathf.Max(1f, CaptureAudioSpectrumBandCount);
        return Mathf.Clamp(t * nyquist, 0f, nyquist);
    }

    private float BandStartFrequencyHz(int bandIndex)
    {
        var nyquist = CaptureAudioSpectrumUpperHz();
        var t = bandIndex / Mathf.Max(1f, CaptureAudioSpectrumBandCount);
        return Mathf.Clamp(t * nyquist, 0f, nyquist);
    }

    private float BandEndFrequencyHz(int bandIndex)
    {
        var nyquist = CaptureAudioSpectrumUpperHz();
        var t = (bandIndex + 1f) / Mathf.Max(1f, CaptureAudioSpectrumBandCount);
        return Mathf.Clamp(t * nyquist, 0f, nyquist);
    }

    private float BandFrequencyToGraphX(Rect graph, float frequencyHz)
    {
        var minHz = Mathf.Clamp(captureAudioSpectrumViewMinHz, CaptureAudioSpectrumDisplayMinHz, Mathf.Max(CaptureAudioSpectrumDisplayMinHz, captureAudioSpectrumViewMaxHz - 1f));
        var maxHz = Mathf.Max(minHz + 1f, captureAudioSpectrumViewMaxHz);
        var minLog = Mathf.Log10(minHz);
        var maxLog = Mathf.Log10(maxHz);
        var valueLog = Mathf.Log10(Mathf.Clamp(frequencyHz, minHz, maxHz));
        return graph.x + Mathf.Clamp01((valueLog - minLog) / Mathf.Max(0.0001f, maxLog - minLog)) * graph.width;
    }

    private float DbToGraphY(Rect graph, float db)
    {
        var t = Mathf.InverseLerp(CaptureAudioSpectrumDbFloor, captureAudioEqDisplayMaxGainDb, Mathf.Clamp(db, CaptureAudioSpectrumDbFloor, captureAudioEqDisplayMaxGainDb));
        return Mathf.Lerp(graph.yMax, graph.y, t);
    }

    private float GraphYToDb(Rect graph, float y)
    {
        var t = Mathf.Clamp01((graph.yMax - y) / Mathf.Max(1f, graph.height));
        return Mathf.Lerp(CaptureAudioSpectrumDbFloor, captureAudioEqDisplayMaxGainDb, t);
    }

    private int GraphXToBandIndex(Rect graph, float x)
    {
        var minHz = Mathf.Clamp(captureAudioSpectrumViewMinHz, CaptureAudioSpectrumDisplayMinHz, Mathf.Max(CaptureAudioSpectrumDisplayMinHz, captureAudioSpectrumViewMaxHz - 1f));
        var maxHz = Mathf.Max(minHz + 1f, captureAudioSpectrumViewMaxHz);
        var minLog = Mathf.Log10(minHz);
        var maxLog = Mathf.Log10(maxHz);
        var tGraph = Mathf.Clamp01((x - graph.x) / Mathf.Max(1f, graph.width));
        var frequency = Mathf.Pow(10f, Mathf.Lerp(minLog, maxLog, tGraph));
        var nyquist = Mathf.Max(1f, CaptureAudioSpectrumUpperHz());
        var t = Mathf.Clamp01(frequency / nyquist);
        return Mathf.Clamp(Mathf.RoundToInt(t * (CaptureAudioSpectrumBandCount - 1)), 0, CaptureAudioSpectrumBandCount - 1);
    }

    private float GraphXToFrequencyHz(Rect graph, float x)
    {
        var minHz = Mathf.Clamp(captureAudioSpectrumViewMinHz, CaptureAudioSpectrumDisplayMinHz, Mathf.Max(CaptureAudioSpectrumDisplayMinHz, captureAudioSpectrumViewMaxHz - 1f));
        var maxHz = Mathf.Max(minHz + 1f, captureAudioSpectrumViewMaxHz);
        var minLog = Mathf.Log10(minHz);
        var maxLog = Mathf.Log10(maxHz);
        var t = Mathf.Clamp01((x - graph.x) / Mathf.Max(1f, graph.width));
        return Mathf.Pow(10f, Mathf.Lerp(minLog, maxLog, t));
    }

    private float SampleSpectrumDb(float[] spectrumBandDb, float frequencyHz)
    {
        if (spectrumBandDb == null || spectrumBandDb.Length == 0)
        {
            return CaptureAudioSpectrumDbFloor;
        }

        var nyquist = Mathf.Max(1f, CaptureAudioSpectrumUpperHz());
        var normalized = Mathf.Clamp01(frequencyHz / nyquist);
        var bandPosition = Mathf.Clamp((normalized * CaptureAudioSpectrumBandCount) - 0.5f, 0f, spectrumBandDb.Length - 1f);
        var left = Mathf.Clamp(Mathf.FloorToInt(bandPosition), 0, spectrumBandDb.Length - 1);
        var right = Mathf.Clamp(left + 1, 0, spectrumBandDb.Length - 1);
        var t = bandPosition - left;
        var leftDb = SmoothedSpectrumDb(spectrumBandDb, left);
        var rightDb = SmoothedSpectrumDb(spectrumBandDb, right);
        return Mathf.Lerp(leftDb, rightDb, t);
    }

    private float SmoothedSpectrumDb(float[] spectrumBandDb, int bandIndex)
    {
        var weightedDb = 0f;
        var totalWeight = 0f;
        for (var offset = -3; offset <= 3; offset++)
        {
            var index = Mathf.Clamp(bandIndex + offset, 0, spectrumBandDb.Length - 1);
            var weight = offset == 0 ? 1.8f : 1f / (1f + Mathf.Abs(offset));
            weightedDb += spectrumBandDb[index] * weight;
            totalWeight += weight;
        }
        return totalWeight <= 0.0001f ? spectrumBandDb[bandIndex] : weightedDb / totalWeight;
    }

    private float[] BuildSpectrumHzTicks()
    {
        return new[]
        {
            20f, 30f, 50f, 70f, 100f, 200f, 300f, 500f, 700f,
            1000f, 2000f, 3000f, 5000f, 7000f, 10000f, 15000f, 20000f
        };
    }

    private void DrawMidiInputDropdown()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Input Device", GUILayout.Width(110f));
        var text = selectedMidiInputIndex >= 0 && selectedMidiInputIndex < midiInputDevices.Count
            ? midiInputDevices[selectedMidiInputIndex].Label
            : "(none)";
        if (GUILayout.Button(text, GUILayout.MinWidth(320f), GUILayout.Height(28f)))
        {
            midiInputDropdownOpen = !midiInputDropdownOpen;
        }
        GUILayout.EndHorizontal();

        if (!midiInputDropdownOpen)
        {
            return;
        }

        GUILayout.BeginVertical(boxStyle);
        for (var i = 0; i < midiInputDevices.Count; i++)
        {
            var itemLabel = (i == selectedMidiInputIndex ? "> " : "  ") + midiInputDevices[i].Label;
            if (GUILayout.Button(itemLabel, GUILayout.Height(26f)))
            {
                selectedMidiInputIndex = i;
                midiInputDropdownOpen = false;
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawMidiBindingRow(MidiBindingAction action, int index)
    {
        var binding = FindMidiBinding(action, index);
        var learning = midiLearningAction == action && midiLearningIndex == index;

        GUILayout.BeginHorizontal();
        GUILayout.Label(MidiActionLabel(action, index), GUILayout.Width(220f));
        GUILayout.Label(learning ? "Learning..." : FormatMidiBinding(binding), GUILayout.Width(220f));
        if (GUILayout.Button(learning ? "Cancel" : "Learn", GUILayout.Width(80f), GUILayout.Height(24f)))
        {
            if (learning)
            {
                midiLearningAction = MidiBindingAction.None;
                midiLearningIndex = -1;
                midiStatus = "MIDI learn cancelled.";
            }
            else
            {
                BeginMidiLearn(action, index);
            }
        }
        if (GUILayout.Button("Clear", GUILayout.Width(70f), GUILayout.Height(24f)))
        {
            ClearMidiBinding(action, index);
        }
        GUILayout.EndHorizontal();
    }

    private void RefreshMidiInputDevices()
    {
        if (!midiDevicesDirty && midiInputDevices.Count > 0)
        {
            return;
        }

        midiDevicesDirty = false;
        var selectedId = selectedMidiInputIndex >= 0 && selectedMidiInputIndex < midiInputDevices.Count
            ? midiInputDevices[selectedMidiInputIndex].Id
            : "disabled";

        midiInputDevices.Clear();
        midiInputDevices.Add(new MidiInputDeviceOption
        {
            Id = "disabled",
            Label = "Disabled",
            Index = -1
        });

        var count = midiInGetNumDevs();
        for (uint i = 0; i < count; i++)
        {
            MidiInCaps caps;
            if (midiInGetDevCaps((UIntPtr)i, out caps, (uint)Marshal.SizeOf(typeof(MidiInCaps))) != 0)
            {
                continue;
            }

            var label = string.IsNullOrEmpty(caps.Name)
                ? "MIDI In " + i.ToString(CultureInfo.InvariantCulture)
                : caps.Name;
            midiInputDevices.Add(new MidiInputDeviceOption
            {
                Id = "midi-" + i.ToString(CultureInfo.InvariantCulture),
                Label = label,
                Index = (int)i
            });
        }

        selectedMidiInputIndex = FindMidiInputOptionIndex(selectedId);
    }

    private int FindMidiInputOptionIndex(string id)
    {
        for (var i = 0; i < midiInputDevices.Count; i++)
        {
            if (string.Equals(midiInputDevices[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return 0;
    }

    private void ApplyMidiInputSelection()
    {
        RefreshMidiInputDevices();
        var option = selectedMidiInputIndex >= 0 && selectedMidiInputIndex < midiInputDevices.Count
            ? midiInputDevices[selectedMidiInputIndex]
            : null;
        var targetIndex = option == null ? -1 : option.Index;
        if (activeMidiInputDeviceIndex == targetIndex && ((targetIndex < 0 && midiInputHandle == IntPtr.Zero) || (targetIndex >= 0 && midiInputHandle != IntPtr.Zero)))
        {
            return;
        }

        StopMidiInput();
        activeMidiInputDeviceIndex = -1;

        if (option == null || option.Index < 0)
        {
            midiStatus = "MIDI input disabled.";
            SaveMidiConfiguration();
            return;
        }

        IntPtr handle;
        var openResult = midiInOpen(out handle, (uint)option.Index, midiInProc, IntPtr.Zero, MidiCallbackFunction);
        if (openResult != 0 || handle == IntPtr.Zero)
        {
            midiStatus = "Failed to open MIDI input: " + option.Label;
            return;
        }

        var startResult = midiInStart(handle);
        if (startResult != 0)
        {
            midiInClose(handle);
            midiStatus = "Failed to start MIDI input: " + option.Label;
            return;
        }

        midiInputHandle = handle;
        activeMidiInputDeviceIndex = option.Index;
        midiStatus = "Listening: " + option.Label;
        SaveMidiConfiguration();
    }

    private void StopMidiInput()
    {
        if (midiInputHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            midiInStop(midiInputHandle);
            midiInReset(midiInputHandle);
            midiInClose(midiInputHandle);
        }
        catch
        {
        }
        finally
        {
            midiInputHandle = IntPtr.Zero;
            activeMidiInputDeviceIndex = -1;
        }
    }

    private void HandleMidiInMessage(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2)
    {
        if (message != MidiMessageData)
        {
            return;
        }

        var raw = unchecked((uint)param1.ToInt64());
        var status = (int)(raw & 0xFF);
        var data1 = (int)((raw >> 8) & 0xFF);
        var data2 = (int)((raw >> 16) & 0xFF);
        var command = status & 0xF0;
        var channel = status & 0x0F;

        MidiInputMessage midiMessage;
        if (command == 0xB0)
        {
            midiMessage = new MidiInputMessage
            {
                Kind = MidiMessageKind.ControlChange,
                Channel = channel,
                Number = data1,
                Value = data2,
                NormalizedValue = Mathf.Clamp01(data2 / 127f)
            };
        }
        else if (command == 0x90 || command == 0x80)
        {
            var value = command == 0x80 ? 0 : data2;
            midiMessage = new MidiInputMessage
            {
                Kind = MidiMessageKind.Note,
                Channel = channel,
                Number = data1,
                Value = value,
                NormalizedValue = Mathf.Clamp01(value / 127f)
            };
        }
        else
        {
            return;
        }

        lock (midiInputLock)
        {
            midiInputQueue.Add(midiMessage);
            if (midiInputQueue.Count > 256)
            {
                midiInputQueue.RemoveAt(0);
            }
        }
    }

    private void ProcessMidiInputMessages()
    {
        if (midiInputQueue.Count == 0 && midiLearningAction == MidiBindingAction.None)
        {
            return;
        }

        List<MidiInputMessage> messages = null;
        lock (midiInputLock)
        {
            if (midiInputQueue.Count > 0)
            {
                messages = new List<MidiInputMessage>(midiInputQueue);
                midiInputQueue.Clear();
            }
        }

        if (messages == null)
        {
            return;
        }

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            midiLastMessageText = FormatMidiMessage(message);

            if (midiLearningAction != MidiBindingAction.None)
            {
                AssignMidiBinding(midiLearningAction, midiLearningIndex, midiLearningSubIndex, message);
                midiLearningAction = MidiBindingAction.None;
                midiLearningIndex = -1;
                midiLearningSubIndex = -1;
                continue;
            }

            if (midiEditMode)
            {
                continue;
            }

            for (var j = 0; j < midiBindings.Count; j++)
            {
                var binding = midiBindings[j];
                if (binding == null)
                {
                    continue;
                }
                if (binding.Kind != message.Kind || binding.Channel != message.Channel || binding.Number != message.Number)
                {
                    continue;
                }
                ApplyMidiBinding(binding, message);
            }
        }
    }

    private void ApplyMidiBinding(MidiBinding binding, MidiInputMessage message)
    {
        if (binding == null)
        {
            return;
        }

        if (binding.Action == MidiBindingAction.LayerOpacity)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].Opacity = message.NormalizedValue;
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerScale)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].Scale = Mathf.Lerp(LayerScaleMin, LayerScaleMax, message.NormalizedValue);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerScaleX)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].ScaleX = Mathf.Lerp(LayerScaleMin, LayerScaleMax, message.NormalizedValue);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerScaleY)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].ScaleY = Mathf.Lerp(LayerScaleMin, LayerScaleMax, message.NormalizedValue);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerHue)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].HueShift = Mathf.Repeat(message.NormalizedValue, 1f);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerInvertAmount)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].InvertAmount = Mathf.Clamp01(message.NormalizedValue);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerMonochromeAmount)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].MonochromeAmount = Mathf.Clamp01(message.NormalizedValue);
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerProgramSolo)
        {
            if (binding.Index >= SourceStartIndex && binding.Index < MainEffectStartIndex)
            {
                if (message.NormalizedValue >= 0.5f)
                {
                    programSoloLayerIndex = binding.Index;
                }
                else if (programSoloLayerIndex == binding.Index)
                {
                    programSoloLayerIndex = -1;
                }
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerProgramMute)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
            {
                vjLayers[binding.Index].ProgramMuted = message.NormalizedValue >= 0.5f;
            }
            return;
        }
        if (binding.Action == MidiBindingAction.LayerEffectValue)
        {
            if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null && vjLayers[binding.Index].Effect != null)
            {
                var effect = vjLayers[binding.Index].Effect;
                if (effect.Kind == LayerEffectKind.Strobe && effect.Mode == LayerEffectMode.Manual)
                {
                    effect.ManualFlashHeld = message.NormalizedValue >= 0.5f;
                }
                else
                {
                    effect.Intensity = message.NormalizedValue;
                }
            }
            return;
        }

        var key = BindingKey(binding.Action, binding.Index, binding.SubIndex);
        float previous;
        midiBindingLastValues.TryGetValue(key, out previous);
        midiBindingLastValues[key] = message.NormalizedValue;
        if (!(previous < 0.5f && message.NormalizedValue >= 0.5f))
        {
            return;
        }

        switch (binding.Action)
        {
            case MidiBindingAction.LayerToggle:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
                {
                    vjLayers[binding.Index].Enabled = !vjLayers[binding.Index].Enabled;
                }
                break;
            case MidiBindingAction.LayerColorModeCycle:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
                {
                    ApplyLayerColorPreset(vjLayers[binding.Index], NextLayerColorMode(CurrentLayerColorPreset(vjLayers[binding.Index])));
                }
                break;
            case MidiBindingAction.LayerColorSetMode:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
                {
                    ApplyLayerColorPreset(vjLayers[binding.Index], (LayerColorMode)binding.SubIndex);
                }
                break;
            case MidiBindingAction.LayerProgramSolo:
                break;
            case MidiBindingAction.LayerProgramMute:
                break;
            case MidiBindingAction.LayerSelect:
                if (binding.Index >= 0 && binding.Index < VjLayerCount)
                {
                    selectedLayerIndex = binding.Index;
                    if (secondDisplayUiEnabled && IsSettingsMonitorDisplayMode())
                    {
                        settingsMonitorSelectedLayerIndex = binding.Index;
                    }
                }
                break;
            case MidiBindingAction.LayerBlendCycle:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null)
                {
                    vjLayers[binding.Index].BlendMode = NextLayerBlendMode(vjLayers[binding.Index].BlendMode);
                }
                break;
            case MidiBindingAction.LayerEffectToggle:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null && vjLayers[binding.Index].Effect != null)
                {
                    vjLayers[binding.Index].Effect.Enabled = !vjLayers[binding.Index].Effect.Enabled;
                }
                break;
            case MidiBindingAction.LayerEffectModeCycle:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null && vjLayers[binding.Index].Effect != null)
                {
                    CycleLayerEffectMode(vjLayers[binding.Index].Effect);
                }
                break;
            case MidiBindingAction.LayerEffectSetMode:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null && vjLayers[binding.Index].Effect != null)
                {
                    vjLayers[binding.Index].Effect.Mode = (LayerEffectMode)binding.SubIndex;
                }
                break;
            case MidiBindingAction.LayerEffectSetRgbMode:
                if (vjLayers != null && binding.Index >= 0 && binding.Index < vjLayers.Length && vjLayers[binding.Index] != null && vjLayers[binding.Index].Effect != null)
                {
                    vjLayers[binding.Index].Effect.RgbMode = (LayerRgbEffectMode)binding.SubIndex;
                }
                break;
            case MidiBindingAction.ScreenToggle:
                if (activeScreen != 2 && activeScreen != 3)
                {
                    activeScreen = (activeScreen + 1) % 2;
                }
                break;
            case MidiBindingAction.OpenLayerSettings:
                if (activeScreen == 0 && vjLayers != null && selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length)
                {
                    activeScreen = 2;
                    layerSettingsScroll = Vector2.zero;
                }
                break;
            case MidiBindingAction.OpenSettings:
                OpenSettingsScreen();
                break;
            case MidiBindingAction.ReturnToVj:
                if (activeScreen == 2 || activeScreen == 3)
                {
                    activeScreen = 0;
                }
                break;
            case MidiBindingAction.BpmTap:
                RegisterBpmTap();
                break;
            case MidiBindingAction.BpmDjLink:
                EnableDjLinkBpmMode();
                break;
            case MidiBindingAction.GeneratorPresetSelect:
                if (binding.Index >= 0 && binding.Index < generatorPresets.Count &&
                    vjLayers != null &&
                    selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length &&
                    vjLayers[selectedLayerIndex] != null &&
                    vjLayers[selectedLayerIndex].SourceKind == LayerSourceKind.Generator3D &&
                    vjLayers[selectedLayerIndex].Generator != null)
                {
                    ApplyGeneratorPreset(vjLayers[selectedLayerIndex].Generator, generatorPresets[binding.Index], Snapshot());
                }
                break;
        }
    }

    private void AssignMidiBinding(MidiBindingAction action, int index, MidiInputMessage message)
    {
        AssignMidiBinding(action, index, -1, message);
    }

    private void AssignMidiBinding(MidiBindingAction action, int index, int subIndex, MidiInputMessage message)
    {
        if (action == MidiBindingAction.None)
        {
            return;
        }

        for (var i = midiBindings.Count - 1; i >= 0; i--)
        {
            var binding = midiBindings[i];
            if (binding != null && binding.Action == action && binding.Index == index && binding.SubIndex == subIndex)
            {
                midiBindings.RemoveAt(i);
            }
        }

        midiBindings.Add(new MidiBinding
        {
            Action = action,
            Index = index,
            SubIndex = subIndex,
            Kind = message.Kind,
            Channel = message.Channel,
            Number = message.Number
        });
        midiStatus = "Assigned " + MidiActionLabel(action, index, subIndex) + " <- " + FormatMidiMessage(message);
        SaveMidiConfiguration();
    }

    private void BeginMidiLearn(MidiBindingAction action, int index)
    {
        BeginMidiLearn(action, index, -1);
    }

    private void BeginMidiLearn(MidiBindingAction action, int index, int subIndex)
    {
        midiLearningAction = action;
        midiLearningIndex = index;
        midiLearningSubIndex = subIndex;
        midiStatus = "Learning " + MidiActionLabel(action, index, subIndex) + ". Move a control.";
    }

    private void ClearMidiBinding(MidiBindingAction action, int index)
    {
        ClearMidiBinding(action, index, -1);
    }

    private void ClearMidiBinding(MidiBindingAction action, int index, int subIndex)
    {
        var removed = false;
        for (var i = midiBindings.Count - 1; i >= 0; i--)
        {
            var binding = midiBindings[i];
            if (binding != null && binding.Action == action && binding.Index == index && binding.SubIndex == subIndex)
            {
                midiBindings.RemoveAt(i);
                removed = true;
            }
        }

        if (midiLearningAction == action && midiLearningIndex == index && midiLearningSubIndex == subIndex)
        {
            midiLearningAction = MidiBindingAction.None;
            midiLearningIndex = -1;
            midiLearningSubIndex = -1;
        }

        if (removed)
        {
            midiStatus = "Cleared " + MidiActionLabel(action, index, subIndex);
            SaveMidiConfiguration();
        }
    }

    private MidiBinding FindMidiBinding(MidiBindingAction action, int index)
    {
        return FindMidiBinding(action, index, -1);
    }

    private MidiBinding FindMidiBinding(MidiBindingAction action, int index, int subIndex)
    {
        for (var i = 0; i < midiBindings.Count; i++)
        {
            var binding = midiBindings[i];
            if (binding != null && binding.Action == action && binding.Index == index && binding.SubIndex == subIndex)
            {
                return binding;
            }
        }
        return null;
    }

    private static string BindingKey(MidiBindingAction action, int index)
    {
        return BindingKey(action, index, -1);
    }

    private static string BindingKey(MidiBindingAction action, int index, int subIndex)
    {
        return action.ToString() + ":" + index.ToString(CultureInfo.InvariantCulture) + ":" + subIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatMidiMessage(MidiInputMessage message)
    {
        if (message.Kind == MidiMessageKind.Note)
        {
            return "Note Ch" + (message.Channel + 1).ToString(CultureInfo.InvariantCulture) +
                   " #" + message.Number.ToString(CultureInfo.InvariantCulture) +
                   " v" + message.Value.ToString(CultureInfo.InvariantCulture);
        }

        return "CC Ch" + (message.Channel + 1).ToString(CultureInfo.InvariantCulture) +
               " #" + message.Number.ToString(CultureInfo.InvariantCulture) +
               " v" + message.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatMidiBinding(MidiBinding binding)
    {
        return binding == null ? "(none)" : FormatMidiMessage(new MidiInputMessage
        {
            Kind = binding.Kind,
            Channel = binding.Channel,
            Number = binding.Number,
            Value = 127
        });
    }

    private static string MidiActionLabel(MidiBindingAction action, int index)
    {
        return MidiActionLabel(action, index, -1);
    }

    private static string MidiActionLabel(MidiBindingAction action, int index, int subIndex)
    {
        switch (action)
        {
            case MidiBindingAction.LayerOpacity:
                return LayerSlotLabel(index) + " Opacity";
            case MidiBindingAction.LayerScale:
                return LayerSlotLabel(index) + " Scale";
            case MidiBindingAction.LayerScaleX:
                return LayerSlotLabel(index) + " Scale X";
            case MidiBindingAction.LayerScaleY:
                return LayerSlotLabel(index) + " Scale Y";
            case MidiBindingAction.LayerHue:
                return LayerSlotLabel(index) + " Hue";
            case MidiBindingAction.LayerInvertAmount:
                return LayerSlotLabel(index) + " Invert";
            case MidiBindingAction.LayerMonochromeAmount:
                return LayerSlotLabel(index) + " B/W";
            case MidiBindingAction.LayerColorModeCycle:
                return LayerSlotLabel(index) + " Color";
            case MidiBindingAction.LayerColorSetMode:
                return LayerSlotLabel(index) + " Color " + LayerColorModeLabel((LayerColorMode)subIndex);
            case MidiBindingAction.LayerToggle:
                return LayerSlotLabel(index) + " Toggle";
            case MidiBindingAction.LayerSelect:
                return LayerSlotLabel(index) + " Select";
            case MidiBindingAction.LayerBlendCycle:
                return LayerSlotLabel(index) + " Blend";
            case MidiBindingAction.LayerProgramSolo:
                return LayerSlotLabel(index) + " PGM Solo";
            case MidiBindingAction.LayerProgramMute:
                return LayerSlotLabel(index) + " PGM Mute";
            case MidiBindingAction.LayerEffectToggle:
                return LayerSlotLabel(index) + " Effect Toggle";
            case MidiBindingAction.LayerEffectModeCycle:
                return LayerSlotLabel(index) + " Effect Mode";
            case MidiBindingAction.LayerEffectSetMode:
                return LayerSlotLabel(index) + " Effect " + EffectModeLabel((LayerEffectMode)subIndex);
            case MidiBindingAction.LayerEffectSetRgbMode:
                return LayerSlotLabel(index) + " Effect " + RgbEffectModeLabel((LayerRgbEffectMode)subIndex);
            case MidiBindingAction.LayerEffectValue:
                return LayerSlotLabel(index) + " Effect Value";
            case MidiBindingAction.ScreenToggle:
                return "Screen Toggle";
            case MidiBindingAction.OpenLayerSettings:
                return "Open Layer Settings";
            case MidiBindingAction.OpenSettings:
                return "Open Settings";
            case MidiBindingAction.ReturnToVj:
                return "Return";
            case MidiBindingAction.BpmTap:
                return "BPM Tap";
            case MidiBindingAction.BpmDjLink:
                return "BPM DJ LINK";
            case MidiBindingAction.GeneratorPresetSelect:
                return "3D Preset " + (index + 1).ToString(CultureInfo.InvariantCulture);
            default:
                return action.ToString();
        }
    }

    private void DrawMetadataModeButton(DjLinkInputMode mode, string title, string subtitle)
    {
        var isSelected = pendingDjLinkInputMode == mode;
        var rect = GUILayoutUtility.GetRect(1f, 54f, GUILayout.ExpandWidth(true));
        var background = isSelected
            ? new Color(0.28f, 0.17f, 0.07f, 1f)
            : new Color(0.035f, 0.04f, 0.046f, 1f);
        var border = isSelected
            ? new Color(1f, 0.69f, 0.22f, 1f)
            : new Color(0.16f, 0.2f, 0.24f, 1f);
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, background, 0f, 4f);
        var borderWidth = isSelected ? 2f : 1f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, borderWidth), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, borderWidth, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, border, 0f, 0f);

        var badgeRect = new Rect(rect.x + 10f, rect.y + 10f, 88f, 18f);
        GUI.DrawTexture(badgeRect, whiteTexture, ScaleMode.StretchToFill, false, 0f,
            isSelected ? new Color(0.82f, 0.48f, 0.08f, 1f) : new Color(0.12f, 0.16f, 0.20f, 1f), 0f, 3f);
        GUI.Label(new Rect(badgeRect.x + 8f, badgeRect.y + 1f, badgeRect.width - 16f, badgeRect.height),
            isSelected ? "SELECTED" : "AVAILABLE", smallStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            pendingDjLinkInputMode = mode;
            pendingBltBridgeMode = mode == DjLinkInputMode.ExternalBlt;
        }

        GUI.Label(new Rect(rect.x + 112f, rect.y + 8f, rect.width - 124f, 22f), title, normalStyle);
        GUI.Label(new Rect(rect.x + 112f, rect.y + 28f, rect.width - 124f, 18f), subtitle, smallStyle);
    }

    private void SaveMidiConfiguration()
    {
        if (string.IsNullOrEmpty(midiConfigPath))
        {
            return;
        }

        try
        {
            var config = new MidiBindingConfig
            {
                SelectedInputId = selectedMidiInputIndex >= 0 && selectedMidiInputIndex < midiInputDevices.Count
                    ? midiInputDevices[selectedMidiInputIndex].Id
                    : "disabled",
                Bindings = new List<MidiBinding>(midiBindings)
            };
            File.WriteAllText(midiConfigPath, JsonUtility.ToJson(config, true));
        }
        catch (Exception ex)
        {
            midiStatus = "MIDI save failed: " + ex.Message;
        }
    }
}
