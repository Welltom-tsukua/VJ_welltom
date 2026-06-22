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
    private void OnGUI()
    {
        EnsureStyles();
        if (Event.current != null && Event.current.isMouse && IsMouseOnSecondDisplay())
        {
            Event.current.Use();
        }
        HandleFloatingLayerBlendPopupInput();

        var full = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.DrawTexture(full, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.015f, 0.017f, 0.02f, 1f), 0f, 0f);

        if (activeScreen == 0)
        {
            DrawVjScreen(full);
        }
        else if (activeScreen == 2)
        {
            DrawLayerSettingsScreen(full);
        }
        else if (activeScreen == 3)
        {
            DrawSettingsScreen(full);
        }
        else
        {
            DrawCdjScreen(full);
        }

        DrawFloatingLayerBlendPopup();

        if (youtubeSearchOpen)
        {
            DrawYoutubeSearchWindow(full);
        }
        if (youtubeLayerPickerOpen)
        {
            DrawYoutubeLayerPicker(full);
        }
    }

    private void DrawVjScreen(Rect full)
    {
        var snapshot = Snapshot();

        var margin = 18f;
        var gap = 14f;
        var headerHeight = 42f;
        var browserHeight = Mathf.Clamp(full.height * 0.24f, 180f, 280f);
        var topY = margin + headerHeight;
        var topHeight = Mathf.Clamp(full.height * 0.43f, 300f, 430f);
        var lowerTop = topY + topHeight + gap;
        var lowerHeight = Mathf.Max(150f, full.height - browserHeight - margin - lowerTop - gap);

        if (GUI.Button(new Rect(margin, margin + 2f, 96f, 30f), "Setting"))
        {
            OpenSettingsScreen();
        }
        if (GUI.Button(new Rect(margin + 104f, margin + 2f, 104f, 30f), midiEditMode ? "MIDI Edit ON" : "MIDI Edit"))
        {
            ToggleMidiEditMode();
        }
        if (GUI.Button(new Rect(margin + 216f, margin + 2f, 108f, 30f), "Resync Videos"))
        {
            ResyncAllVideoLayers();
        }
        if (GUI.Button(new Rect(margin + 332f, margin + 2f, 120f, 30f), lowerPanelShowsEffects ? "F: Effects" : "F: Browser"))
        {
            lowerPanelShowsEffects = !lowerPanelShowsEffects;
        }
        if (GUI.Button(new Rect(margin + 460f, margin + 2f, 132f, 30f), "Displays"))
        {
            OpenSettingsScreen();
        }
        GUI.Label(new Rect(margin + 600f, margin + 7f, 320f, 24f), secondDisplayUiEnabled ? "Aux display: " + SecondDisplayUiModeLabel() : "", smallStyle);
        GUI.Label(new Rect(full.width - 560f, margin + 7f, 540f, 24f), cdjWaveShortcutInput + ": Player screen   " + layerSettingsShortcutInput + ": Settings   Selected: " + LayerSlotLabel(selectedLayerIndex), smallStyle);
        DrawBpmCounter(new Rect(margin + 700f, margin + 2f, Mathf.Min(540f, Mathf.Max(220f, full.width - 1380f)), 34f), snapshot);
        if (midiEditMode)
        {
            GUI.Label(new Rect(margin + 680f, margin + 7f, 520f, 24f),
                midiLearningAction != MidiBindingAction.None
                    ? "Click cyan area -> move MIDI control   Learning: " + MidiActionLabel(midiLearningAction, midiLearningIndex, midiLearningSubIndex)
                    : "MIDI edit mode: click cyan area, then move a MIDI control",
                smallStyle);
        }
        GUI.Label(new Rect(full.width - 860f, margin + 7f, 320f, 24f), lowerPanelShowsEffects ? "F: Effect Rack" : "F: Media Browser", smallStyle);

        var topRect = new Rect(margin, topY, full.width - margin * 2f, topHeight);
        var leftWidth = Mathf.Max(760f, topRect.width * 0.73f);
        leftWidth = Mathf.Min(leftWidth, topRect.width - 280f);
        var rightWidth = topRect.width - leftWidth - gap;
        var mainRect = new Rect(topRect.x, topRect.y, leftWidth, topRect.height);
        var rightRect = new Rect(mainRect.xMax + gap, topRect.y, rightWidth, topRect.height);

        DrawMainPipelineSection(mainRect);

        var previewPanelHeight = Mathf.Min(rightRect.height * 0.40f, rightRect.width * 9f / 16f + 54f);
        var overlayRect = new Rect(rightRect.x, rightRect.y, rightRect.width, Mathf.Max(150f, previewPanelHeight));
        DrawOverlaySection(overlayRect);

        var allEffectRect = new Rect(rightRect.x, overlayRect.yMax + gap, rightRect.width, Mathf.Max(120f, rightRect.yMax - overlayRect.yMax - gap));
        DrawAllEffectSection(allEffectRect);

        var sourceWidth = Mathf.Max(220f, full.width * 0.2f);
        var browserWidth = full.width - margin * 2f - sourceWidth - gap;
        var browserRect = new Rect(margin, lowerTop, browserWidth, lowerHeight + browserHeight + gap);
        var sourceRect = new Rect(browserRect.xMax + gap, lowerTop, sourceWidth, browserRect.height);
        DrawSourceWindow(sourceRect, snapshot);
        if (lowerPanelShowsEffects)
        {
            DrawEffectBrowserPanel(browserRect);
        }
        else
        {
            DrawMediaBrowser(browserRect);
        }
    }

    private void ToggleMidiEditMode()
    {
        midiEditMode = !midiEditMode;
        if (!midiEditMode)
        {
            midiLearningAction = MidiBindingAction.None;
            midiLearningIndex = -1;
            midiLearningSubIndex = -1;
            midiStatus = "MIDI edit mode off.";
        }
        else
        {
            midiStatus = "MIDI edit mode on. Click a cyan control area.";
        }
    }

    private void OpenSettingsScreen()
    {
        activeScreen = 3;
        settingsScroll = Vector2.zero;
        pendingBltBridgeMode = useBltBridgeMode;
        pendingDjLinkInputMode = CurrentDjLinkInputMode();
        pendingBltParamsUrl = string.IsNullOrEmpty(bltParamsUrl) ? DefaultBltParamsUrl : bltParamsUrl;
        outputDevicesDirty = true;
        audioInputDevicesDirty = true;
        midiDevicesDirty = true;
        RefreshOutputDevices();
        RefreshAudioInputDevices();
        RefreshMidiInputDevices();
    }

    private void ToggleExternalWindow(ExternalWindowKind kind)
    {
        if (kind == ExternalWindowKind.Preview)
        {
            previewExternalWindowEnabled = !previewExternalWindowEnabled;
            PlayerPrefs.SetInt(PreviewExternalWindowPrefKey, previewExternalWindowEnabled ? 1 : 0);
            if (previewExternalWindowEnabled)
            {
                EnsureExternalWindow(kind);
            }
            else
            {
                CloseExternalWindow(ref previewExternalWindowProcess);
            }
        }
        else
        {
            settingsExternalWindowEnabled = !settingsExternalWindowEnabled;
            PlayerPrefs.SetInt(SettingsExternalWindowPrefKey, settingsExternalWindowEnabled ? 1 : 0);
            if (settingsExternalWindowEnabled)
            {
                EnsureExternalWindow(kind);
            }
            else
            {
                CloseExternalWindow(ref settingsExternalWindowProcess);
            }
        }
        PlayerPrefs.Save();
    }

    private void ToggleMonitorDisplay(MonitorDisplayKind kind)
    {
        ToggleSecondDisplayUi();
    }

    private void ToggleSecondDisplayUi()
    {
        secondDisplayUiEnabled = !secondDisplayUiEnabled && HasAuxDisplayRoute();
        if (!secondDisplayUiEnabled)
        {
            secondDisplayUiMode = SecondDisplayUiMode.None;
        }
        if (secondDisplayUiEnabled && IsDisplayReservedForAuxUi(activeProgramDisplay))
        {
            DisableProgramOutput("Program output disabled because the selected display is reserved for routed UI.");
        }
        if (secondDisplayUiEnabled)
        {
            settingsMonitorSelectedLayerIndex = selectedLayerIndex;
            EnsureMonitorDisplays();
            UpdateMonitorDisplays(force: true);
        }
        else
        {
            ApplyMonitorDisplayTargets();
        }
        PlayerPrefs.Save();
    }

    private string SecondDisplayUiModeLabel()
    {
        switch (secondDisplayUiMode)
        {
            case SecondDisplayUiMode.Preview:
                return "CDJ Wave";
            case SecondDisplayUiMode.Settings:
                return "Layer Settings";
            case SecondDisplayUiMode.EffectSettings:
                return "Effect Settings";
            default:
                return "Idle";
        }
    }

    private void UpdateExternalWindows()
    {
        if (!previewExternalWindowEnabled && !settingsExternalWindowEnabled)
        {
            return;
        }

        if (Time.unscaledTime < nextExternalWindowSyncTime)
        {
            return;
        }
        nextExternalWindowSyncTime = Time.unscaledTime + 0.25f;

        if (previewExternalWindowEnabled)
        {
            EnsureExternalWindow(ExternalWindowKind.Preview);
            ExportPreviewExternalWindowData();
        }

        if (settingsExternalWindowEnabled)
        {
            EnsureExternalWindow(ExternalWindowKind.Settings);
            ExportSettingsExternalWindowData();
        }
    }

    private void EnsureMonitorDisplays()
    {
        if (!secondDisplayUiEnabled)
        {
            return;
        }

        EnsureMonitorDisplayEventSystem();
        ActivateAvailableUnityDisplays();
        RefreshMonitorDisplayAssignments();
        EnsureSecondDisplayUiCamera();
        EnsurePreviewMonitorDisplayScene();
        EnsureSettingsMonitorDisplayScene();
        ApplyMonitorDisplayTargets();
    }

    private void UpdateMonitorDisplays(bool force = false)
    {
        if (!secondDisplayUiEnabled)
        {
            ApplyMonitorDisplayTargets();
            return;
        }

        if (!force && Time.unscaledTime < nextMonitorDisplaySyncTime)
        {
            return;
        }
        nextMonitorDisplaySyncTime = Time.unscaledTime + 0.2f;

        EnsureMonitorDisplays();
        RefreshMonitorDisplayAssignments();
        ApplyMonitorDisplayTargets();

        if (secondDisplayUiMode == SecondDisplayUiMode.Preview)
        {
            UpdatePreviewMonitorDisplayContent();
        }

        if (secondDisplayUiMode == SecondDisplayUiMode.Settings || secondDisplayUiMode == SecondDisplayUiMode.EffectSettings)
        {
            UpdateSettingsMonitorDisplayContent();
        }
    }

    private void HandleSecondDisplayPreviewInput()
    {
        if (!secondDisplayUiEnabled || secondDisplayUiMode != SecondDisplayUiMode.Preview)
        {
            previewMonitorMouseWasDown = Input.GetMouseButton(0);
            return;
        }

        var mouseDown = Input.GetMouseButton(0);
        var clicked = mouseDown && !previewMonitorMouseWasDown;
        previewMonitorMouseWasDown = mouseDown;
        if (!clicked)
        {
            return;
        }

        if (!TryGetSecondDisplayGuiMousePosition(out var guiPosition, out var full, true))
        {
            return;
        }

        ProcessCdjMonitorClick(guiPosition, full);
    }

    private bool TryGetSecondDisplayGuiMousePosition(out Vector2 guiPosition, out Rect full, bool allowEditorFallback)
    {
        guiPosition = Vector2.zero;
        full = Rect.zero;
        if (!secondDisplayUiEnabled || secondDisplayUiIndex <= 0)
        {
            return false;
        }

        var relative = Display.RelativeMouseAt(Input.mousePosition);
        if (Display.displays != null && secondDisplayUiIndex < Display.displays.Length && Mathf.RoundToInt(relative.z) == secondDisplayUiIndex)
        {
            var display = Display.displays[secondDisplayUiIndex];
            var displayWidth = display.systemWidth > 0 ? display.systemWidth : display.renderingWidth;
            var displayHeight = display.systemHeight > 0 ? display.systemHeight : display.renderingHeight;
            if (displayWidth <= 0 || displayHeight <= 0)
            {
                return false;
            }

            guiPosition = new Vector2(relative.x * (PreviewMonitorReferenceWidth / displayWidth), (displayHeight - relative.y) * (PreviewMonitorReferenceHeight / displayHeight));
            full = PreviewMonitorReferenceRect();
            return true;
        }

        if (!Application.isEditor || !allowEditorFallback)
        {
            return false;
        }

        var width = Mathf.Max(1, Screen.width);
        var height = Mathf.Max(1, Screen.height);
        guiPosition = new Vector2(Input.mousePosition.x * (PreviewMonitorReferenceWidth / width), (height - Input.mousePosition.y) * (PreviewMonitorReferenceHeight / height));
        full = PreviewMonitorReferenceRect();
        return true;
    }

    private bool IsMouseOnSecondDisplay()
    {
        return TryGetSecondDisplayGuiMousePosition(out _, out _, false);
    }

    private void ProcessCdjMonitorClick(Vector2 guiPosition, Rect full)
    {
        var screenLayout = BuildCdjMonitorScreenLayout(full);
        if (screenLayout.DeckButtonsRect.Contains(guiPosition))
        {
            var buttonGap = 6f;
            var buttonW = (screenLayout.DeckButtonsRect.width - buttonGap) * 0.5f;
            var deck2Rect = new Rect(screenLayout.DeckButtonsRect.x, screenLayout.DeckButtonsRect.y, buttonW, screenLayout.DeckButtonsRect.height);
            var deck4Rect = new Rect(screenLayout.DeckButtonsRect.x + buttonW + buttonGap, screenLayout.DeckButtonsRect.y, buttonW, screenLayout.DeckButtonsRect.height);
            if (deck2Rect.Contains(guiPosition))
            {
                cdjDeckMode = 2;
            }
            else if (deck4Rect.Contains(guiPosition))
            {
                cdjDeckMode = 4;
            }
            UpdatePreviewMonitorDisplayContent();
            return;
        }

        var monitorData = BuildCdjMonitorViewData();
        var playerCount = screenLayout.FourDeck ? 4 : 2;
        for (var i = 0; i < playerCount && i < screenLayout.PanelRects.Length; i++)
        {
            var panelLayout = BuildCdjPlayerPanelLayout(screenLayout.PanelRects[i]);
            if (!panelLayout.SearchButtonRect.Contains(guiPosition))
            {
                continue;
            }

            var playerNumber = i + 1;
            var card = FindCdjMonitorDeckCard(monitorData, playerNumber);
            OpenYoutubeSearch(playerNumber, card == null ? null : card.Player);
            return;
        }
    }

    private static Rect PreviewMonitorReferenceRect()
    {
        return new Rect(0f, 0f, PreviewMonitorReferenceWidth, PreviewMonitorReferenceHeight);
    }

    private void RefreshMonitorDisplayAssignments()
    {
        secondDisplayUiIndex = RouteDisplayIndexForMode(secondDisplayUiMode);
        if (secondDisplayUiEnabled && secondDisplayUiIndex < 0)
        {
            outputStatus = SecondDisplayUiModeLabel() + " display is unavailable.";
        }
    }

    private void ApplyMonitorDisplayTargets()
    {
        if (secondDisplayUiCamera != null)
        {
            var active = secondDisplayUiEnabled && secondDisplayUiIndex > 0;
            secondDisplayUiCamera.targetDisplay = active ? secondDisplayUiIndex : 0;
            secondDisplayUiCamera.enabled = active;
            if (secondDisplayUiCameraRoot != null)
            {
                secondDisplayUiCameraRoot.SetActive(active);
            }
        }

        if (previewMonitorDisplayCanvas != null)
        {
            var active = secondDisplayUiEnabled && secondDisplayUiIndex > 0 && secondDisplayUiMode == SecondDisplayUiMode.Preview;
            previewMonitorDisplayCanvas.targetDisplay = active ? secondDisplayUiIndex : 0;
            previewMonitorDisplayRoot.SetActive(active);
        }

        if (settingsMonitorDisplayCanvas != null)
        {
            var active = secondDisplayUiEnabled && secondDisplayUiIndex > 0 &&
                         (secondDisplayUiMode == SecondDisplayUiMode.Settings || secondDisplayUiMode == SecondDisplayUiMode.EffectSettings);
            settingsMonitorDisplayCanvas.targetDisplay = active ? secondDisplayUiIndex : 0;
            settingsMonitorDisplayRoot.SetActive(active);
        }
    }

    private void EnsureSecondDisplayUiCamera()
    {
        if (secondDisplayUiIndex <= 0)
        {
            return;
        }

        if (secondDisplayUiCamera != null)
        {
            secondDisplayUiCamera.targetDisplay = secondDisplayUiIndex;
            secondDisplayUiCamera.enabled = secondDisplayUiEnabled && secondDisplayUiIndex > 0;
            if (secondDisplayUiCameraRoot != null)
            {
                secondDisplayUiCameraRoot.SetActive(secondDisplayUiEnabled && secondDisplayUiIndex > 0);
            }
            return;
        }

        secondDisplayUiCameraRoot = new GameObject("Second Display UI Camera");
        secondDisplayUiCameraRoot.transform.SetParent(transform, false);
        secondDisplayUiCamera = secondDisplayUiCameraRoot.AddComponent<Camera>();
        secondDisplayUiCamera.clearFlags = CameraClearFlags.SolidColor;
        secondDisplayUiCamera.backgroundColor = new Color(0.015f, 0.017f, 0.02f, 1f);
        secondDisplayUiCamera.cullingMask = 0;
        secondDisplayUiCamera.depth = -100f;
        secondDisplayUiCamera.targetDisplay = secondDisplayUiIndex;
        secondDisplayUiCamera.nearClipPlane = 0.1f;
        secondDisplayUiCamera.farClipPlane = 1f;
    }

    private void DestroyMonitorDisplays()
    {
        if (secondDisplayUiCameraRoot != null)
        {
            Destroy(secondDisplayUiCameraRoot);
            secondDisplayUiCameraRoot = null;
            secondDisplayUiCamera = null;
        }

        if (previewMonitorDisplayRoot != null)
        {
            Destroy(previewMonitorDisplayRoot);
            previewMonitorDisplayRoot = null;
            previewMonitorDisplayCanvas = null;
            previewMonitorTitleText = null;
            previewMonitorStatusText = null;
            previewMonitorListenerErrorText = null;
            previewMonitorDecks = null;
            previewMonitorLastDeckMode = -1;
        }

        if (settingsMonitorDisplayRoot != null)
        {
            Destroy(settingsMonitorDisplayRoot);
            settingsMonitorDisplayRoot = null;
            settingsMonitorDisplayCanvas = null;
            settingsMonitorHeaderText = null;
            settingsMonitorSubtitleText = null;
            settingsMonitorStatusText = null;
            settingsMonitorPreviewTitleText = null;
            settingsMonitorPreviewImage = null;
            settingsMonitorPreviewPanel = null;
            settingsMonitorDetailsPanel = null;
            settingsMonitorPresetInfoText = null;
            settingsMonitorAuxTitleText = null;
            settingsMonitorAuxImageA = null;
            settingsMonitorAuxImageB = null;
            settingsMonitorPresetButtons = null;
            settingsMonitorFaceButtons = null;
            settingsMonitorLastButtonGenerator = null;
            settingsMonitorLastPresetButtonSignature = null;
            settingsMonitorLastFaceButtonSignature = null;
            settingsMonitorCommonTitleText = null;
            settingsMonitorCommonText = null;
            settingsMonitorCommonButtons = null;
            settingsMonitorEffectTitleText = null;
            settingsMonitorEffectText = null;
            settingsMonitorEffectButtons = null;
            settingsMonitorSourceTitleText = null;
            settingsMonitorSourceText = null;
            settingsMonitorSourceButtons = null;
            settingsMonitorInputFieldA = null;
            settingsMonitorInputFieldB = null;
            settingsMonitorInputApplyButtonA = null;
            settingsMonitorInputApplyButtonB = null;
            settingsMonitorColorNoneButton = null;
            settingsMonitorColorInvertButton = null;
            settingsMonitorColorEdgeButton = null;
            settingsMonitorColorMonoButton = null;
        }
    }

    private void EnsureMonitorDisplayEventSystem()
    {
        if (monitorDisplayEventSystem != null)
        {
            return;
        }

        monitorDisplayEventSystem = FindObjectOfType<EventSystem>();
        if (monitorDisplayEventSystem != null)
        {
            return;
        }

        var go = new GameObject("Monitor Display EventSystem");
        monitorDisplayEventSystem = go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private Font MonitorDisplayFont()
    {
        if (monitorDisplayFont == null)
        {
            try
            {
                monitorDisplayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                monitorDisplayFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
        return monitorDisplayFont;
    }

    private void EnsurePreviewMonitorDisplayScene()
    {
        if (previewMonitorDisplayRoot != null)
        {
            if (previewMonitorDisplayCanvas != null && previewMonitorTitleText != null && previewMonitorDecks != null)
            {
                return;
            }

            Destroy(previewMonitorDisplayRoot);
            previewMonitorDisplayRoot = null;
            previewMonitorDisplayCanvas = null;
            previewMonitorTitleText = null;
            previewMonitorStatusText = null;
            previewMonitorListenerErrorText = null;
            previewMonitorHintText = null;
            previewMonitorDeck2Button = null;
            previewMonitorDeck4Button = null;
            previewMonitorDecks = null;
        }

        previewMonitorDisplayRoot = new GameObject("Preview Monitor Display");
        previewMonitorDisplayRoot.transform.SetParent(transform, false);
        previewMonitorDisplayCanvas = previewMonitorDisplayRoot.AddComponent<Canvas>();
        previewMonitorDisplayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        previewMonitorDisplayCanvas.pixelPerfect = true;
        previewMonitorDisplayCanvas.sortingOrder = 50;
        var scaler = previewMonitorDisplayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PreviewMonitorReferenceWidth, PreviewMonitorReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        previewMonitorDisplayRoot.AddComponent<GraphicRaycaster>();

        CreateUiPanel(previewMonitorDisplayRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0.02f, 0.025f, 0.03f, 1f));
        previewMonitorTitleText = CreateUiText(previewMonitorDisplayRoot.transform, "PreviewMonitorTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -54f), new Vector2(-20f, -12f), 24, TextAnchor.MiddleLeft, FontStyle.Bold);
        previewMonitorStatusText = CreateUiText(previewMonitorDisplayRoot.transform, "PreviewMonitorStatus", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -88f), new Vector2(-20f, -58f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        previewMonitorListenerErrorText = CreateUiText(previewMonitorDisplayRoot.transform, "PreviewMonitorListenerError", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -118f), new Vector2(-20f, -88f), 16, TextAnchor.MiddleLeft, FontStyle.Normal);
        previewMonitorListenerErrorText.color = new Color(1f, 0.65f, 0.4f, 1f);
        previewMonitorListenerErrorText.gameObject.SetActive(false);
        previewMonitorHintText = CreateUiText(previewMonitorDisplayRoot.transform, "PreviewMonitorHint", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-520f, -54f), new Vector2(-20f, -24f), 14, TextAnchor.MiddleRight, FontStyle.Normal);
        previewMonitorDeck2Button = CreateUiButton(previewMonitorDisplayRoot.transform, "PreviewMonitorDeck2Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, -56f), new Vector2(420f, -22f), "2 Deck", () =>
        {
            cdjDeckMode = 2;
            UpdatePreviewMonitorDisplayContent();
        });
        previewMonitorDeck4Button = CreateUiButton(previewMonitorDisplayRoot.transform, "PreviewMonitorDeck4Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(426f, -56f), new Vector2(516f, -22f), "4 Deck", () =>
        {
            cdjDeckMode = 4;
            UpdatePreviewMonitorDisplayContent();
        });

        previewMonitorDecks = new AuxPreviewDeckUi[4];
        for (var i = 0; i < previewMonitorDecks.Length; i++)
        {
            previewMonitorDecks[i] = CreatePreviewMonitorDeckUi(previewMonitorDisplayRoot.transform, i);
        }
    }

    private void EnsureSettingsMonitorDisplayScene()
    {
        if (settingsMonitorDisplayRoot != null)
        {
            if (settingsMonitorDisplayCanvas != null && settingsMonitorHeaderText != null && settingsMonitorDetailsPanel != null)
            {
                return;
            }

            Destroy(settingsMonitorDisplayRoot);
            settingsMonitorDisplayRoot = null;
            settingsMonitorDisplayCanvas = null;
            settingsMonitorHeaderText = null;
            settingsMonitorSubtitleText = null;
            settingsMonitorStatusText = null;
            settingsMonitorPreviewTitleText = null;
            settingsMonitorPreviewImage = null;
            settingsMonitorPreviewPanel = null;
            settingsMonitorDetailsPanel = null;
            settingsMonitorCommonButtons = null;
        }

        settingsMonitorDisplayRoot = new GameObject("Settings Monitor Display");
        settingsMonitorDisplayRoot.transform.SetParent(transform, false);
        settingsMonitorDisplayCanvas = settingsMonitorDisplayRoot.AddComponent<Canvas>();
        settingsMonitorDisplayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        settingsMonitorDisplayCanvas.pixelPerfect = true;
        settingsMonitorDisplayCanvas.sortingOrder = 50;
        var scaler = settingsMonitorDisplayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PreviewMonitorReferenceWidth, PreviewMonitorReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        settingsMonitorDisplayRoot.AddComponent<GraphicRaycaster>();

        CreateUiPanel(settingsMonitorDisplayRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0.018f, 0.02f, 0.025f, 1f));
        settingsMonitorHeaderText = CreateUiText(settingsMonitorDisplayRoot.transform, "SettingsMonitorHeader", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -56f), new Vector2(-24f, -14f), 28, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorSubtitleText = CreateUiText(settingsMonitorDisplayRoot.transform, "SettingsMonitorSubtitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -92f), new Vector2(-24f, -58f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        settingsMonitorStatusText = CreateUiText(settingsMonitorDisplayRoot.transform, "SettingsMonitorStatus", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -124f), new Vector2(-24f, -90f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        settingsMonitorPreviewPanel = CreateUiPanel(settingsMonitorDisplayRoot.transform, new Vector2(0f, 0f), new Vector2(0.38f, 0.92f), new Vector2(18f, 18f), new Vector2(-10f, -10f), new Color(0.03f, 0.035f, 0.04f, 1f));
        settingsMonitorPreviewTitleText = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorPreviewTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -8f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        settingsMonitorPreviewImage = CreateUiRawImage(settingsMonitorPreviewPanel.transform, "SettingsMonitorPreview", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -340f), new Vector2(-12f, -40f), Color.white);
        settingsMonitorPresetInfoText = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorPresetInfo", new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(12f, 12f), new Vector2(-12f, -12f), 16, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorPresetInfoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        settingsMonitorPresetInfoText.verticalOverflow = VerticalWrapMode.Overflow;
        settingsMonitorPresetInfoText.alignment = TextAnchor.UpperLeft;
        settingsMonitorAuxTitleText = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxTitle", new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(12f, 12f), new Vector2(-12f, -112f), 15, TextAnchor.UpperLeft, FontStyle.Bold);
        settingsMonitorAuxLabelA = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxLabelA", new Vector2(0f, 0f), new Vector2(0.5f, 0.32f), new Vector2(12f, 104f), new Vector2(-6f, -92f), 13, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorAuxLabelB = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxLabelB", new Vector2(0.5f, 0f), new Vector2(1f, 0.32f), new Vector2(6f, 104f), new Vector2(-12f, -92f), 13, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorAuxStatusText = CreateUiText(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxStatus", new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(12f, 82f), new Vector2(-12f, -70f), 12, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorAuxImageA = CreateUiRawImage(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxImageA", new Vector2(0f, 0f), new Vector2(0.5f, 0.24f), new Vector2(12f, 12f), new Vector2(-6f, -12f), Color.white);
        settingsMonitorAuxImageB = CreateUiRawImage(settingsMonitorPreviewPanel.transform, "SettingsMonitorAuxImageB", new Vector2(0.5f, 0f), new Vector2(1f, 0.24f), new Vector2(6f, 12f), new Vector2(-12f, -12f), Color.white);
        settingsMonitorAuxImageARect = settingsMonitorAuxImageA.rectTransform;
        settingsMonitorAuxImageBRect = settingsMonitorAuxImageB.rectTransform;
        AttachPointerClick(settingsMonitorAuxImageA.gameObject, OnSettingsMonitorAuxImageAClick);
        AttachPointerClick(settingsMonitorAuxImageB.gameObject, OnSettingsMonitorAuxImageBClick);
        settingsMonitorPresetButtons = new UiButtonBundle[8];
        for (var i = 0; i < settingsMonitorPresetButtons.Length; i++)
        {
            var row = i / 4;
            var column = i % 4;
            var xMin = 12f + column * 68f;
            var xMax = xMin + 62f;
            var yTop = -156f - row * 58f;
            var yBottom = yTop + 52f;
            var index = i;
            settingsMonitorPresetButtons[i] = CreateUiThumbnailButton(settingsMonitorPreviewPanel.transform, "SettingsMonitorPresetButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(xMin, -yBottom), new Vector2(xMax, -yTop), "-", () => OnSettingsMonitorPresetButton(index));
        }
        settingsMonitorFaceButtons = new UiButtonBundle[8];
        for (var i = 0; i < settingsMonitorFaceButtons.Length; i++)
        {
            var row = i / 4;
            var column = i % 4;
            var xMin = 12f + column * 68f;
            var xMax = xMin + 62f;
            var yTop = -278f - row * 58f;
            var yBottom = yTop + 52f;
            var index = i;
            settingsMonitorFaceButtons[i] = CreateUiThumbnailButton(settingsMonitorPreviewPanel.transform, "SettingsMonitorFaceButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(xMin, -yBottom), new Vector2(xMax, -yTop), "-", () => OnSettingsMonitorFaceButton(index));
        }
        settingsMonitorDetailsPanel = CreateUiPanel(settingsMonitorDisplayRoot.transform, new Vector2(0.38f, 0f), new Vector2(1f, 0.92f), new Vector2(8f, 18f), new Vector2(-18f, -10f), new Color(0.03f, 0.035f, 0.04f, 1f));
        settingsMonitorCommonTitleText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorCommonTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -34f), new Vector2(-14f, -8f), 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorEnabledButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorEnabledButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -70f), new Vector2(124f, -40f), "ON", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].Enabled = !vjLayers[layerIndex].Enabled;
            }
        });
        settingsMonitorAudioButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorAudioButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(136f, -70f), new Vector2(316f, -40f), "Audio", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                var layer = vjLayers[layerIndex];
                layer.AudioOutputEnabled = !layer.AudioOutputEnabled;
                ApplyLayerAudioOutputState(layer);
            }
        });
        settingsMonitorBlendAlphaButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlendAlphaButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -106f), new Vector2(104f, -76f), "Alpha", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Alpha;
            }
        });
        settingsMonitorBlend50Button = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlend50Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -106f), new Vector2(218f, -76f), "Add", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Add;
            }
        });
        settingsMonitorBlend100Button = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlend100Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(226f, -106f), new Vector2(316f, -76f), "Screen", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Screen;
            }
        });
        settingsMonitorBlendMaskButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlendMaskButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(324f, -106f), new Vector2(414f, -76f), "Mask", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Mask;
            }
        });
        settingsMonitorColorNoneButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorColorNoneButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -142f), new Vector2(104f, -112f), "Normal", () => SetSettingsMonitorColorMode(LayerColorMode.None));
        settingsMonitorColorInvertButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorColorInvertButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -142f), new Vector2(218f, -112f), "Invert", () => SetSettingsMonitorColorMode(LayerColorMode.Invert));
        settingsMonitorColorEdgeButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorColorEdgeButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(226f, -142f), new Vector2(316f, -112f), "Edge", () => SetSettingsMonitorColorMode(LayerColorMode.Edge));
        settingsMonitorColorMonoButton = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorColorMonoButton", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(324f, -142f), new Vector2(414f, -112f), "B/W", () => SetSettingsMonitorColorMode(LayerColorMode.Monochrome));
        settingsMonitorCommonButtons = new UiButtonBundle[10];
        for (var i = 0; i < settingsMonitorCommonButtons.Length; i++)
        {
            var column = i % 5;
            var row = i / 5;
            var xMin = 14f + column * 82f;
            var xMax = xMin + 76f;
            var yTop = -178f - row * 36f;
            var yBottom = yTop + 30f;
            var index = i;
            settingsMonitorCommonButtons[i] = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorCommonButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xMin, yTop), new Vector2(xMax, yBottom), "-", () => OnSettingsMonitorCommonButton(index));
        }
        settingsMonitorCommonText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorCommon", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(430f, -214f), new Vector2(-14f, -36f), 15, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorEffectTitleText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffectTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -258f), new Vector2(-14f, -230f), 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorEffectText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffect", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(430f, -356f), new Vector2(-14f, -260f), 15, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorMidiStatusText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorMidiStatus", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 4f), new Vector2(-14f, 28f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        settingsMonitorEffectButtons = new UiButtonBundle[14];
        for (var i = 0; i < settingsMonitorEffectButtons.Length; i++)
        {
            var column = i % 3;
            var row = i / 3;
            var xMin = 14f + column * 104f;
            var xMax = xMin + 96f;
            var yTop = -294f - row * 36f;
            var yBottom = yTop + 30f;
            var index = i;
            settingsMonitorEffectButtons[i] = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffectButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xMin, yTop), new Vector2(xMax, yBottom), "-", () => OnSettingsMonitorEffectButton(index));
        }
        settingsMonitorSourceTitleText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorSourceTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -486f), new Vector2(-14f, -458f), 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorSourceText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorSource", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(430f, 116f), new Vector2(-14f, -488f), 15, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorSourceButtons = new UiButtonBundle[40];
        for (var i = 0; i < settingsMonitorSourceButtons.Length; i++)
        {
            var row = i / 4;
            var column = i % 4;
            var xMin = 14f + column * 78f;
            var xMax = xMin + 72f;
            var yTop = -522f - row * 36f;
            var yBottom = yTop + 30f;
            var index = i;
            settingsMonitorSourceButtons[i] = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorSourceButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xMin, yTop), new Vector2(xMax, yBottom), "-", () => OnSettingsMonitorSourceButton(index));
        }
        settingsMonitorInputFieldA = CreateUiInputField(settingsMonitorDetailsPanel.transform, "SettingsMonitorInputA", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 70f), new Vector2(-100f, 100f));
        settingsMonitorInputFieldB = CreateUiInputField(settingsMonitorDetailsPanel.transform, "SettingsMonitorInputB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 34f), new Vector2(-100f, 64f));
        settingsMonitorInputApplyButtonA = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorInputApplyA", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-92f, 70f), new Vector2(-14f, 100f), "Apply", () => ApplySettingsMonitorInputA());
        settingsMonitorInputApplyButtonB = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorInputApplyB", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-92f, 34f), new Vector2(-14f, 64f), "Apply", () => ApplySettingsMonitorInputB());
        ConfigureSettingsPanelText(settingsMonitorCommonText);
        ConfigureSettingsPanelText(settingsMonitorEffectText);
        ConfigureSettingsPanelText(settingsMonitorSourceText);
    }

    private AuxPreviewDeckUi CreatePreviewMonitorDeckUi(Transform parent, int index)
    {
        var ui = new AuxPreviewDeckUi();
        ui.Root = new GameObject("PreviewDeck" + (index + 1).ToString(CultureInfo.InvariantCulture));
        ui.Root.transform.SetParent(parent, false);
        var rootRect = ui.Root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        ui.Panel = CreateUiPanel(ui.Root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.025f, 0.03f, 0.035f, 1f));
        ui.Header = CreateUiText(ui.Root.transform, "Header", new Vector2(0f, 1f), new Vector2(0.35f, 1f), new Vector2(12f, -34f), new Vector2(-4f, -8f), 24, TextAnchor.MiddleLeft, FontStyle.Bold);
        ui.Flags = CreateUiText(ui.Root.transform, "Flags", new Vector2(0.35f, 1f), new Vector2(1f, 1f), new Vector2(4f, -34f), new Vector2(-12f, -8f), 14, TextAnchor.MiddleRight, FontStyle.Normal);
        ui.SearchButton = CreateUiButton(ui.Root.transform, "SearchButton", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-112f, -38f), new Vector2(-18f, -10f), "YT Search", null);
        ui.AlbumArt = CreateUiRawImage(ui.Root.transform, "AlbumArt", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -98f), new Vector2(254f, -34f), new Color(0.01f, 0.012f, 0.015f, 1f));
        ui.Device = CreateUiText(ui.Root.transform, "Device", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -64f), new Vector2(180f, -40f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        ui.Title = CreateUiText(ui.Root.transform, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(266f, -66f), new Vector2(-12f, -34f), 24, TextAnchor.MiddleLeft, FontStyle.Bold);
        ui.Artist = CreateUiText(ui.Root.transform, "Artist", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(266f, -96f), new Vector2(-12f, -64f), 16, TextAnchor.MiddleLeft, FontStyle.Normal);
        ui.Comment = CreateUiText(ui.Root.transform, "Comment", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(266f, -124f), new Vector2(-12f, -92f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        ui.RuntimeWave = CreateUiRawImage(ui.Root.transform, "RuntimeWave", new Vector2(0f, 0.32f), new Vector2(1f, 0.66f), new Vector2(12f, 8f), new Vector2(-12f, -8f), Color.black);
        ui.RuntimeWavePlaceholder = CreateUiText(ui.RuntimeWave.transform, "RuntimeWavePlaceholder", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 8f), new Vector2(-10f, -8f), 15, TextAnchor.UpperLeft, FontStyle.Normal);
        ui.RuntimeWaveMarker = CreateUiMarker(ui.RuntimeWave.transform, "RuntimeWaveMarker", new Color(1f, 0.92f, 0.18f, 0.95f), 0.5f);
        ui.OverviewWave = CreateUiRawImage(ui.Root.transform, "OverviewWave", new Vector2(0f, 0.18f), new Vector2(1f, 0.30f), new Vector2(12f, 4f), new Vector2(-12f, -4f), new Color(0.005f, 0.006f, 0.008f, 1f));
        ui.OverviewWavePlaceholder = CreateUiText(ui.OverviewWave.transform, "OverviewWavePlaceholder", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 8f), new Vector2(-10f, -8f), 14, TextAnchor.UpperLeft, FontStyle.Normal);
        ui.OverviewPlaybackMarker = CreateUiMarker(ui.OverviewWave.transform, "OverviewPlaybackMarker", new Color(1f, 0.92f, 0.18f, 0.95f), 0f);
        ui.OverviewCueMarker = CreateUiMarker(ui.OverviewWave.transform, "OverviewCueMarker", CurrentCueMarkerColor, 0f);
        ui.BpmLine = CreateUiText(ui.Root.transform, "BpmLine", new Vector2(0f, 0.11f), new Vector2(1f, 0.17f), new Vector2(12f, 0f), new Vector2(-12f, 0f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        ui.TimeLine = CreateUiText(ui.Root.transform, "TimeLine", new Vector2(0f, 0.05f), new Vector2(1f, 0.11f), new Vector2(12f, 0f), new Vector2(-12f, 0f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        ui.BeatLine = CreateUiText(ui.Root.transform, "BeatLine", new Vector2(0f, 0f), new Vector2(1f, 0.05f), new Vector2(12f, 0f), new Vector2(-12f, 0f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        ApplyPreviewDeckChildLayout(ui, new Rect(0f, 0f, 600f, 320f));
        return ui;
    }

    private static void SetRectTransformRect(RectTransform rectTransform, Rect rect)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(rect.x, -rect.y);
        rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
    }

    private void ApplyPreviewDeckChildLayout(AuxPreviewDeckUi ui, Rect rect)
    {
        if (ui == null || ui.Root == null)
        {
            return;
        }

        var panelLayout = BuildCdjPlayerPanelLayout(rect);

        if (ui.Panel != null)
        {
            var panelRect = ui.Panel.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        SetRectTransformRect(ui.Header == null ? null : ui.Header.rectTransform, new Rect(panelLayout.HeaderRect.x - rect.x, panelLayout.HeaderRect.y - rect.y, panelLayout.HeaderRect.width, panelLayout.HeaderRect.height));
        SetRectTransformRect(ui.Flags == null ? null : ui.Flags.rectTransform, new Rect(panelLayout.FlagsRect.x - rect.x, panelLayout.FlagsRect.y - rect.y, panelLayout.FlagsRect.width, panelLayout.FlagsRect.height));
        if (ui.SearchButton != null && ui.SearchButton.Button != null)
        {
            SetRectTransformRect(ui.SearchButton.Button.GetComponent<RectTransform>(), new Rect(panelLayout.SearchButtonRect.x - rect.x, panelLayout.SearchButtonRect.y - rect.y, panelLayout.SearchButtonRect.width, panelLayout.SearchButtonRect.height));
            if (ui.SearchButton.Label != null)
            {
                ui.SearchButton.Label.fontSize = 14;
            }
        }

        SetRectTransformRect(ui.AlbumArt == null ? null : ui.AlbumArt.rectTransform, new Rect(panelLayout.AlbumArtRect.x - rect.x, panelLayout.AlbumArtRect.y - rect.y, panelLayout.AlbumArtRect.width, panelLayout.AlbumArtRect.height));
        SetRectTransformRect(ui.Device == null ? null : ui.Device.rectTransform, new Rect(panelLayout.DeviceRect.x - rect.x, panelLayout.DeviceRect.y - rect.y, panelLayout.DeviceRect.width, panelLayout.DeviceRect.height));
        SetRectTransformRect(ui.Title == null ? null : ui.Title.rectTransform, new Rect(panelLayout.TitleRect.x - rect.x, panelLayout.TitleRect.y - rect.y, panelLayout.TitleRect.width, panelLayout.TitleRect.height));
        SetRectTransformRect(ui.Artist == null ? null : ui.Artist.rectTransform, new Rect(panelLayout.ArtistRect.x - rect.x, panelLayout.ArtistRect.y - rect.y, panelLayout.ArtistRect.width, panelLayout.ArtistRect.height));
        SetRectTransformRect(ui.Comment == null ? null : ui.Comment.rectTransform, new Rect(panelLayout.CommentRect.x - rect.x, panelLayout.CommentRect.y - rect.y, panelLayout.CommentRect.width, panelLayout.CommentRect.height));
        SetRectTransformRect(ui.RuntimeWave == null ? null : ui.RuntimeWave.rectTransform, new Rect(panelLayout.RuntimeWaveRect.x - rect.x, panelLayout.RuntimeWaveRect.y - rect.y, panelLayout.RuntimeWaveRect.width, panelLayout.RuntimeWaveRect.height));
        if (ui.RuntimeWavePlaceholder != null)
        {
            ui.RuntimeWavePlaceholder.fontSize = 18;
            SetRectTransformRect(ui.RuntimeWavePlaceholder.rectTransform, new Rect(panelLayout.RuntimeWavePlaceholderRect.x - panelLayout.RuntimeWaveRect.x, panelLayout.RuntimeWavePlaceholderRect.y - panelLayout.RuntimeWaveRect.y, panelLayout.RuntimeWavePlaceholderRect.width, panelLayout.RuntimeWavePlaceholderRect.height));
        }

        SetRectTransformRect(ui.OverviewWave == null ? null : ui.OverviewWave.rectTransform, new Rect(panelLayout.OverviewWaveRect.x - rect.x, panelLayout.OverviewWaveRect.y - rect.y, panelLayout.OverviewWaveRect.width, panelLayout.OverviewWaveRect.height));
        if (ui.OverviewWavePlaceholder != null)
        {
            ui.OverviewWavePlaceholder.fontSize = 14;
            SetRectTransformRect(ui.OverviewWavePlaceholder.rectTransform, new Rect(panelLayout.OverviewWavePlaceholderRect.x - panelLayout.OverviewWaveRect.x, panelLayout.OverviewWavePlaceholderRect.y - panelLayout.OverviewWaveRect.y, panelLayout.OverviewWavePlaceholderRect.width, panelLayout.OverviewWavePlaceholderRect.height));
        }

        SetRectTransformRect(ui.BpmLine == null ? null : ui.BpmLine.rectTransform, new Rect(panelLayout.BpmLineRect.x - rect.x, panelLayout.BpmLineRect.y - rect.y, panelLayout.BpmLineRect.width, panelLayout.BpmLineRect.height));
        SetRectTransformRect(ui.TimeLine == null ? null : ui.TimeLine.rectTransform, new Rect(panelLayout.TimeLineRect.x - rect.x, panelLayout.TimeLineRect.y - rect.y, panelLayout.TimeLineRect.width, panelLayout.TimeLineRect.height));
        SetRectTransformRect(ui.BeatLine == null ? null : ui.BeatLine.rectTransform, new Rect(panelLayout.BeatLineRect.x - rect.x, panelLayout.BeatLineRect.y - rect.y, panelLayout.BeatLineRect.width, panelLayout.BeatLineRect.height));
    }

    private Image CreateUiMarker(Transform parent, string name, Color color, float anchorX)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorX, 0f);
        rect.anchorMax = new Vector2(anchorX, 1f);
        rect.offsetMin = new Vector2(-1.5f, 0f);
        rect.offsetMax = new Vector2(1.5f, 0f);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image CreateUiPanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateUiText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor anchor, FontStyle fontStyle)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var text = go.AddComponent<Text>();
        text.font = MonitorDisplayFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = Color.white;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RawImage CreateUiRawImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = go.AddComponent<RawImage>();
        image.color = color;
        return image;
    }

    private UiButtonBundle CreateUiButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string label, Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = MonitorDisplayFont();
        text.fontSize = 15;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Normal;
        text.color = Color.white;
        text.text = label;

        var badgeGo = new GameObject("Badge");
        badgeGo.transform.SetParent(go.transform, false);
        var badgeRect = badgeGo.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.offsetMin = new Vector2(4f, -16f);
        badgeRect.offsetMax = new Vector2(-4f, -2f);
        var badge = badgeGo.AddComponent<Text>();
        badge.font = MonitorDisplayFont();
        badge.fontSize = 10;
        badge.alignment = TextAnchor.UpperRight;
        badge.fontStyle = FontStyle.Bold;
        badge.color = new Color(0.55f, 0.8f, 1f, 1f);
        badge.text = "";
        badge.gameObject.SetActive(false);

        return new UiButtonBundle
        {
            Button = button,
            Label = text,
            Background = image,
            Badge = badge
        };
    }

    private static void AttachPointerClick(GameObject go, Action<PointerEventData> handler)
    {
        if (go == null || handler == null)
        {
            return;
        }

        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = go.AddComponent<EventTrigger>();
        }

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(evtData => handler(evtData as PointerEventData));
        trigger.triggers.Add(entry);
    }

    private UiButtonBundle CreateUiThumbnailButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string label, Action onClick)
    {
        var bundle = CreateUiButton(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, label, onClick);
        if (bundle == null || bundle.Button == null)
        {
            return bundle;
        }

        var thumb = CreateUiRawImage(bundle.Button.transform, "Thumb", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 18f), new Vector2(-4f, -4f), Color.white);
        thumb.raycastTarget = false;
        bundle.Thumbnail = thumb;
        if (bundle.Label != null)
        {
            bundle.Label.fontSize = 10;
            bundle.Label.alignment = TextAnchor.LowerCenter;
        }
        return bundle;
    }

    private InputField CreateUiInputField(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var background = go.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.11f, 1f);
        var input = go.AddComponent<InputField>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        var text = textGo.AddComponent<Text>();
        text.font = MonitorDisplayFont();
        text.fontSize = 15;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.supportRichText = false;

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(go.transform, false);
        var placeholderRect = placeholderGo.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(8f, 4f);
        placeholderRect.offsetMax = new Vector2(-8f, -4f);
        var placeholder = placeholderGo.AddComponent<Text>();
        placeholder.font = MonitorDisplayFont();
        placeholder.fontSize = 15;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.text = "";

        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = background;
        return input;
    }

    private static void ConfigureSettingsPanelText(Text text)
    {
        if (text == null)
        {
            return;
        }

        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.UpperLeft;
    }

    private void UpdatePreviewMonitorDisplayContent()
    {
        if (previewMonitorDisplayRoot == null || previewMonitorDecks == null)
        {
            return;
        }

        var monitorData = BuildCdjMonitorViewData();
        LayoutPreviewMonitorHeaderAndDecks();
        previewMonitorTitleText.text = monitorData.Title;
        previewMonitorStatusText.text = string.Empty;
        previewMonitorStatusText.gameObject.SetActive(false);
        if (previewMonitorListenerErrorText != null)
        {
            previewMonitorListenerErrorText.text = string.IsNullOrEmpty(monitorData.LastError) ? string.Empty : "Listener error: " + monitorData.LastError;
            previewMonitorListenerErrorText.gameObject.SetActive(!string.IsNullOrEmpty(monitorData.LastError));
        }
        if (previewMonitorHintText != null)
        {
            previewMonitorHintText.text = "Space: VJ screen   BLT metadata: " + monitorData.Status;
        }
        if (previewMonitorDeck2Button != null && previewMonitorDeck2Button.Label != null)
        {
            previewMonitorDeck2Button.Label.text = cdjDeckMode == 2 ? "> 2 Deck" : "2 Deck";
            UpdatePreviewMonitorButton(previewMonitorDeck2Button, cdjDeckMode == 2);
        }
        if (previewMonitorDeck4Button != null && previewMonitorDeck4Button.Label != null)
        {
            previewMonitorDeck4Button.Label.text = cdjDeckMode == 4 ? "> 4 Deck" : "4 Deck";
            UpdatePreviewMonitorButton(previewMonitorDeck4Button, cdjDeckMode == 4);
        }

        var playerCount = monitorData.FourDeck ? 4 : 2;
        for (var i = 0; i < previewMonitorDecks.Length; i++)
        {
            var ui = previewMonitorDecks[i];
            var visible = i < playerCount;
            if (ui == null || ui.Root == null)
            {
                continue;
            }
            ui.Root.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            var playerNumber = i + 1;
            var card = FindCdjMonitorDeckCard(monitorData, playerNumber);
            ui.Header.text = "PLAYER " + playerNumber.ToString(CultureInfo.InvariantCulture);
            ui.Flags.text = card == null ? "NO LINK" : card.Flags;
            ui.Device.text = string.Empty;
            ui.Title.text = card == null ? "No player connected" : card.Title;
            ui.Artist.text = card == null ? "" : card.Artist;
            ui.Comment.text = card == null ? "" : card.Comment;
            ConfigureSourceButton(ui.SearchButton, true, "YT Search", () => OpenYoutubeSearch(playerNumber, card == null ? null : card.Player));
            if (ui.SearchButton != null && ui.SearchButton.Background != null)
            {
                UpdatePreviewMonitorButton(ui.SearchButton, false);
            }
            SetPreviewRawImage(ui.AlbumArt, card == null ? null : card.AlbumArtTexture, new Color(0.01f, 0.012f, 0.015f, 1f));
            SetPreviewRawImage(ui.RuntimeWave, card == null ? null : card.RuntimeWaveTexture, Color.black);
            if (ui.RuntimeWavePlaceholder != null)
            {
                ui.RuntimeWavePlaceholder.text = card == null || card.Player == null ? "Player not connected" : "Waiting for runtime waveform detail";
                ui.RuntimeWavePlaceholder.gameObject.SetActive(card == null || card.RuntimeWaveTexture == null);
            }
            if (ui.RuntimeWaveMarker != null)
            {
                ui.RuntimeWaveMarker.gameObject.SetActive(card != null && card.Player != null);
            }
            SetPreviewRawImage(ui.OverviewWave, card == null ? null : card.OverviewWaveTexture, new Color(0.005f, 0.006f, 0.008f, 1f));
            if (ui.OverviewWavePlaceholder != null)
            {
                ui.OverviewWavePlaceholder.text = "Waiting for full waveform";
                ui.OverviewWavePlaceholder.gameObject.SetActive(card == null || card.OverviewWaveTexture == null);
            }
            if (ui.OverviewPlaybackMarker != null)
            {
                ui.OverviewPlaybackMarker.gameObject.SetActive(card != null && card.HasOverviewPlaybackRatio);
                SetUiMarkerPosition(ui.OverviewPlaybackMarker, card == null ? 0f : card.OverviewPlaybackRatio);
            }
            if (ui.OverviewCueMarker != null)
            {
                ui.OverviewCueMarker.gameObject.SetActive(card != null && card.HasOverviewCueRatio);
                SetUiMarkerPosition(ui.OverviewCueMarker, card == null ? 0f : card.OverviewCueRatio);
            }
            ui.BpmLine.text = card == null ? "BPM --" : card.BpmLine;
            ui.TimeLine.text = card == null ? "" : card.TimeLine;
            ui.BeatLine.text = card == null ? "" : card.BeatLine;
        }
    }

    private void LayoutPreviewMonitorHeaderAndDecks()
    {
        if (previewMonitorDisplayCanvas == null || previewMonitorDecks == null)
        {
            return;
        }

        var canvasRect = previewMonitorDisplayCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        var canvasFull = canvasRect.rect;
        if (canvasFull.width <= 0f || canvasFull.height <= 0f)
        {
            return;
        }

        var screenLayout = BuildCdjMonitorScreenLayout(PreviewMonitorReferenceRect());
        SetRectTransformRect(previewMonitorTitleText == null ? null : previewMonitorTitleText.rectTransform, screenLayout.TitleRect);
        if (previewMonitorHintText != null)
        {
            SetRectTransformRect(previewMonitorHintText.rectTransform, screenLayout.HintRect);
        }
        if (previewMonitorListenerErrorText != null)
        {
            SetRectTransformRect(previewMonitorListenerErrorText.rectTransform, screenLayout.ErrorRect);
        }
        if (previewMonitorDeck2Button != null && previewMonitorDeck2Button.Button != null)
        {
            var deckButtonRect = screenLayout.DeckButtonsRect;
            var buttonGap = 6f;
            var buttonW = (deckButtonRect.width - buttonGap) * 0.5f;
            SetRectTransformRect(previewMonitorDeck2Button.Button.GetComponent<RectTransform>(), new Rect(deckButtonRect.x, deckButtonRect.y, buttonW, deckButtonRect.height));
        }
        if (previewMonitorDeck4Button != null && previewMonitorDeck4Button.Button != null)
        {
            var deckButtonRect = screenLayout.DeckButtonsRect;
            var buttonGap = 6f;
            var buttonW = (deckButtonRect.width - buttonGap) * 0.5f;
            SetRectTransformRect(previewMonitorDeck4Button.Button.GetComponent<RectTransform>(), new Rect(deckButtonRect.x + buttonW + buttonGap, deckButtonRect.y, buttonW, deckButtonRect.height));
        }

        LayoutPreviewMonitorDecks();
    }

    private static void SetUiMarkerPosition(Image marker, float ratio)
    {
        if (marker == null)
        {
            return;
        }

        var rect = marker.rectTransform;
        if (rect == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);
        rect.anchorMin = new Vector2(ratio, 0f);
        rect.anchorMax = new Vector2(ratio, 1f);
        rect.offsetMin = new Vector2(-1.5f, 0f);
        rect.offsetMax = new Vector2(1.5f, 0f);
    }

    private static void SetPreviewRawImage(RawImage image, Texture texture, Color emptyColor)
    {
        if (image == null)
        {
            return;
        }

        image.texture = texture;
        image.color = texture == null ? emptyColor : Color.white;
    }

    private void LayoutPreviewMonitorDecks()
    {
        if (previewMonitorDecks == null)
        {
            return;
        }

        if (previewMonitorDisplayCanvas == null)
        {
            return;
        }

        var canvasRect = previewMonitorDisplayCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        var canvasFull = canvasRect.rect;
        if (canvasFull.width <= 0f || canvasFull.height <= 0f)
        {
            return;
        }

        var screenLayout = BuildCdjMonitorScreenLayout(PreviewMonitorReferenceRect());

        for (var i = 0; i < previewMonitorDecks.Length; i++)
        {
            var ui = previewMonitorDecks[i];
            if (ui == null || ui.Root == null)
            {
                continue;
            }

            var rect = ui.Root.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = ui.Root.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }

            if (i < screenLayout.PanelRects.Length)
            {
                var panelRect = screenLayout.PanelRects[i];
                SetRectTransformRect(rect, panelRect);
                ApplyPreviewDeckChildLayout(ui, new Rect(0f, 0f, panelRect.width, panelRect.height));
            }
        }

        previewMonitorLastDeckMode = cdjDeckMode;
    }

    private void UpdateSettingsMonitorDisplayContent()
    {
        if (settingsMonitorDisplayRoot == null)
        {
            return;
        }

        var data = BuildSettingsViewData();
        settingsMonitorHeaderText.text = data.LayerLabel + " Settings";
        settingsMonitorSubtitleText.text = string.IsNullOrEmpty(data.Subtitle) ? data.Title : data.Title + "   /   " + data.Subtitle;
        settingsMonitorStatusText.text = PreferText(data.Status, "");
        if (settingsMonitorPreviewTitleText != null)
        {
            settingsMonitorPreviewTitleText.text = data.Title;
        }
        settingsMonitorPreviewImage.texture = data.PreviewTexture;
        if (settingsMonitorPresetInfoText != null)
        {
            settingsMonitorPresetInfoText.text = PreferText(data.PresetInfo, "");
        }
        UpdateSettingsMonitorAuxVisuals(data);
        if (settingsMonitorCommonTitleText != null)
        {
            settingsMonitorCommonTitleText.text = "Layer Blend";
        }
        UpdateSettingsMonitorButtons(data);
        UpdateSettingsMonitorCommonButtons(data);
        if (settingsMonitorCommonText != null)
        {
            settingsMonitorCommonText.text = data.CommonDetails == null ? "" : string.Join("\n", data.CommonDetails);
        }
        if (settingsMonitorEffectTitleText != null)
        {
            settingsMonitorEffectTitleText.text = "Effect";
        }
        UpdateSettingsMonitorEffectButtons(data);
        if (settingsMonitorEffectText != null)
        {
            settingsMonitorEffectText.text = data.EffectDetails == null ? "" : string.Join("\n", data.EffectDetails);
        }
        if (settingsMonitorMidiStatusText != null)
        {
            var midiText = midiEditMode ? "MIDI Learn ON" : "MIDI Learn OFF";
            if (midiEditMode && midiLearningAction != MidiBindingAction.None && midiLearningIndex == data.SelectedLayerIndex)
            {
                midiText += "  /  " + midiLearningAction.ToString();
            }
            settingsMonitorMidiStatusText.text = midiText;
        }
        if (settingsMonitorSourceTitleText != null)
        {
            settingsMonitorSourceTitleText.text = data.SourceDetailTitle;
        }
        if (settingsMonitorSourceText != null)
        {
            settingsMonitorSourceText.text = data.SourceDetails == null ? "" : string.Join("\n", data.SourceDetails);
        }
        UpdateSettingsMonitorSourceButtons(data);
        UpdateSettingsMonitorInputs(data);
    }

    private int CurrentSettingsMonitorLayerIndex()
    {
        if (vjLayers == null || vjLayers.Length == 0)
        {
            return -1;
        }

        if (settingsMonitorSelectedLayerIndex >= 0 && settingsMonitorSelectedLayerIndex < vjLayers.Length)
        {
            return settingsMonitorSelectedLayerIndex;
        }

        if (selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length)
        {
            return selectedLayerIndex;
        }

        return -1;
    }

    private void SetSettingsMonitorColorMode(LayerColorMode mode)
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
        {
            ApplyLayerColorPreset(vjLayers[layerIndex], mode);
        }
    }

    private void OnSettingsMonitorAuxImageAClick(PointerEventData eventData)
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (eventData == null || vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null || layer.SourceKind != LayerSourceKind.YouTube || layer.YouTube == null || layer.Player == null || settingsMonitorAuxImageARect == null)
        {
            return;
        }

        var length = layer.Player.length > 0.001 ? layer.Player.length : layer.YouTube.KnownLengthSeconds;
        if (length <= 0.001)
        {
            return;
        }

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(settingsMonitorAuxImageARect, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        var rect = settingsMonitorAuxImageARect.rect;
        var normalized = Mathf.Clamp01((localPoint.x - rect.xMin) / Mathf.Max(1f, rect.width));
        var next = normalized * length;
        layer.Player.time = next;
        layer.YouTube.TimeInput = next.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void OnSettingsMonitorAuxImageBClick(PointerEventData eventData)
    {
    }

    private void UpdateSettingsMonitorAuxVisuals(SettingsViewData data)
    {
        if (settingsMonitorAuxTitleText == null || settingsMonitorAuxImageA == null || settingsMonitorAuxImageB == null)
        {
            return;
        }

        settingsMonitorAuxTitleText.gameObject.SetActive(false);
        if (settingsMonitorAuxLabelA != null) settingsMonitorAuxLabelA.gameObject.SetActive(false);
        if (settingsMonitorAuxLabelB != null) settingsMonitorAuxLabelB.gameObject.SetActive(false);
        if (settingsMonitorAuxStatusText != null) settingsMonitorAuxStatusText.gameObject.SetActive(false);
        settingsMonitorAuxImageA.gameObject.SetActive(false);
        settingsMonitorAuxImageB.gameObject.SetActive(false);
        settingsMonitorAuxImageA.texture = null;
        settingsMonitorAuxImageB.texture = null;

        if (vjLayers == null || data == null || data.SelectedLayerIndex < 0 || data.SelectedLayerIndex >= vjLayers.Length)
        {
            if (settingsMonitorLastButtonGenerator != null)
            {
                UpdateSettingsMonitorGeneratorButtons(null);
            }
            return;
        }

        var layer = vjLayers[data.SelectedLayerIndex];
        if (layer == null)
        {
            if (settingsMonitorLastButtonGenerator != null)
            {
                UpdateSettingsMonitorGeneratorButtons(null);
            }
            return;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.YouTube:
                if (settingsMonitorLastButtonGenerator != null)
                {
                    UpdateSettingsMonitorGeneratorButtons(null);
                }
                if (layer.YouTube != null)
                {
                    settingsMonitorAuxTitleText.text = "Waveform / Zoomed";
                    settingsMonitorAuxTitleText.gameObject.SetActive(true);
                    if (settingsMonitorAuxLabelA != null)
                    {
                        settingsMonitorAuxLabelA.text = "Waveform / position";
                        settingsMonitorAuxLabelA.gameObject.SetActive(true);
                    }
                    if (settingsMonitorAuxLabelB != null)
                    {
                        settingsMonitorAuxLabelB.text = "Zoomed waveform";
                        settingsMonitorAuxLabelB.gameObject.SetActive(true);
                    }
                    if (settingsMonitorAuxStatusText != null)
                    {
                        var current = layer.Player == null ? 0.0 : layer.Player.time;
                        var length = layer.Player != null && layer.Player.length > 0.001 ? layer.Player.length : layer.YouTube.KnownLengthSeconds;
                        settingsMonitorAuxStatusText.text = FormatSeconds(current) + " / " + (length > 0.001 ? FormatSeconds(length) : "--:--") +
                                                            "   " + YoutubeWaveformStatus(layer.YouTube);
                        settingsMonitorAuxStatusText.gameObject.SetActive(true);
                    }
                    settingsMonitorAuxImageA.texture = layer.YouTube.WaveformTexture;
                    var zoomWidth = settingsMonitorAuxImageBRect == null ? 640 : Mathf.RoundToInt(settingsMonitorAuxImageBRect.rect.width);
                    var zoomHeight = settingsMonitorAuxImageBRect == null ? 120 : Mathf.RoundToInt(settingsMonitorAuxImageBRect.rect.height);
                    settingsMonitorAuxImageB.texture = EnsureYoutubeZoomWaveformTexture(layer, zoomWidth, zoomHeight);
                    settingsMonitorAuxImageA.gameObject.SetActive(settingsMonitorAuxImageA.texture != null);
                    settingsMonitorAuxImageB.gameObject.SetActive(settingsMonitorAuxImageB.texture != null);
                }
                break;
            case LayerSourceKind.Generator3D:
                if (layer.Generator != null)
                {
                    settingsMonitorAuxTitleText.text = "Preset / Face Source";
                    settingsMonitorAuxTitleText.gameObject.SetActive(true);
                    if (settingsMonitorAuxLabelA != null)
                    {
                        settingsMonitorAuxLabelA.text = "Current preset";
                        settingsMonitorAuxLabelA.gameObject.SetActive(true);
                    }
                    if (settingsMonitorAuxLabelB != null)
                    {
                        settingsMonitorAuxLabelB.text = "Current face source";
                        settingsMonitorAuxLabelB.gameObject.SetActive(true);
                    }
                    if (settingsMonitorAuxStatusText != null)
                    {
                        settingsMonitorAuxStatusText.text = "Preset " + PreferText(layer.Generator.PresetNameInput, "(none)") +
                                                            "   /   Face " + DescribeGeneratorFaceSource(layer.Generator);
                        settingsMonitorAuxStatusText.gameObject.SetActive(true);
                    }
                    settingsMonitorAuxImageA.texture = ResolveGeneratorPresetThumbnail(layer.Generator);
                    settingsMonitorAuxImageB.texture = ResolveGeneratorFacePreviewTexture(layer.Generator);
                    settingsMonitorAuxImageA.gameObject.SetActive(settingsMonitorAuxImageA.texture != null);
                    settingsMonitorAuxImageB.gameObject.SetActive(settingsMonitorAuxImageB.texture != null);
                    UpdateSettingsMonitorGeneratorButtons(layer.Generator);
                }
                break;
            default:
                if (settingsMonitorLastButtonGenerator != null)
                {
                    UpdateSettingsMonitorGeneratorButtons(null);
                }
                break;
        }
    }

    private void UpdateSettingsMonitorGeneratorButtons(GeneratorState generator)
    {
        if (generator == null)
        {
            settingsMonitorLastButtonGenerator = null;
            settingsMonitorLastPresetButtonSignature = null;
            settingsMonitorLastFaceButtonSignature = null;
            UpdateSettingsMonitorPresetButtons(null);
            UpdateSettingsMonitorFaceButtons(null);
            return;
        }

        if (!ReferenceEquals(settingsMonitorLastButtonGenerator, generator))
        {
            settingsMonitorLastButtonGenerator = generator;
            settingsMonitorLastPresetButtonSignature = null;
            settingsMonitorLastFaceButtonSignature = null;
        }

        var presetSignature = BuildSettingsMonitorPresetButtonSignature(generator);
        if (!string.Equals(settingsMonitorLastPresetButtonSignature, presetSignature, StringComparison.Ordinal))
        {
            settingsMonitorLastPresetButtonSignature = presetSignature;
            UpdateSettingsMonitorPresetButtons(generator);
        }

        var faceSignature = BuildSettingsMonitorFaceButtonSignature(generator);
        if (!string.Equals(settingsMonitorLastFaceButtonSignature, faceSignature, StringComparison.Ordinal))
        {
            settingsMonitorLastFaceButtonSignature = faceSignature;
            UpdateSettingsMonitorFaceButtons(generator);
        }
    }

    private string BuildSettingsMonitorPresetButtonSignature(GeneratorState generator)
    {
        var currentIndex = FindGeneratorPresetIndex(generator);
        return currentIndex.ToString(CultureInfo.InvariantCulture) + "|" +
               generatorPresets.Count.ToString(CultureInfo.InvariantCulture) + "|" +
               midiEditMode.ToString() + "|" +
               midiLearningAction.ToString() + "|" +
               midiLearningIndex.ToString(CultureInfo.InvariantCulture);
    }

    private string BuildSettingsMonitorFaceButtonSignature(GeneratorState generator)
    {
        var faceCount = generator.FaceImagePaths == null ? 0 : generator.FaceImagePaths.Count;
        return faceCount.ToString(CultureInfo.InvariantCulture) + "|" +
               PreferText(generator.FaceImageFolderPath, "") + "|" +
               PreferText(generator.FaceTextureImagePath, "") + "|" +
               midiEditMode.ToString();
    }

    private void UpdateSettingsMonitorPresetButtons(GeneratorState generator)
    {
        if (settingsMonitorPresetButtons == null)
        {
            return;
        }

        for (var i = 0; i < settingsMonitorPresetButtons.Length; i++)
        {
            ConfigureSourceButton(settingsMonitorPresetButtons[i], false, "-", null);
            if (settingsMonitorPresetButtons[i] != null && settingsMonitorPresetButtons[i].Thumbnail != null)
            {
                settingsMonitorPresetButtons[i].Thumbnail.texture = null;
            }
        }

        if (generator == null || generatorPresets == null || generatorPresets.Count == 0)
        {
            return;
        }

        var currentIndex = FindGeneratorPresetIndex(generator);
        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var start = Mathf.Max(0, currentIndex - 1);
        for (var i = 0; i < settingsMonitorPresetButtons.Length; i++)
        {
            var presetIndex = start + i;
            if (presetIndex < 0 || presetIndex >= generatorPresets.Count)
            {
                continue;
            }

            var preset = generatorPresets[presetIndex];
            if (preset == null)
            {
                continue;
            }

            var label = presetIndex == currentIndex ? "> " + ShortPresetLabel(preset.Name, 10) : ShortPresetLabel(preset.Name, 10);
            ConfigureSourceButton(settingsMonitorPresetButtons[i], true, label, () =>
            {
                generator.PresetNameInput = preset.Name;
                ApplyGeneratorPreset(generator, preset, Snapshot());
            });
            if (settingsMonitorPresetButtons[i] != null && settingsMonitorPresetButtons[i].Thumbnail != null)
            {
                settingsMonitorPresetButtons[i].Thumbnail.texture = preset.ThumbnailTexture;
            }
            if (midiEditMode && midiLearningAction == MidiBindingAction.GeneratorPresetSelect && midiLearningIndex == presetIndex)
            {
                TintButton(settingsMonitorPresetButtons[i], new Color(0.10f, 0.20f, 0.42f, 1f));
                SetButtonBadge(settingsMonitorPresetButtons[i], "MIDI", new Color(0.55f, 0.8f, 1f, 1f));
            }
        }
    }

    private void UpdateSettingsMonitorFaceButtons(GeneratorState generator)
    {
        if (settingsMonitorFaceButtons == null)
        {
            return;
        }

        for (var i = 0; i < settingsMonitorFaceButtons.Length; i++)
        {
            ConfigureSourceButton(settingsMonitorFaceButtons[i], false, "-", null);
            if (settingsMonitorFaceButtons[i] != null && settingsMonitorFaceButtons[i].Thumbnail != null)
            {
                settingsMonitorFaceButtons[i].Thumbnail.texture = null;
            }
        }

        if (generator == null || generator.FaceImagePaths == null || generator.FaceImagePaths.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < generator.FaceImagePaths.Count; i++)
        {
            if (string.Equals(generator.FaceImagePaths[i], generator.FaceTextureImagePath, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }
        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var start = Mathf.Max(0, currentIndex - 1);
        for (var i = 0; i < settingsMonitorFaceButtons.Length; i++)
        {
            var faceIndex = start + i;
            if (faceIndex < 0 || faceIndex >= generator.FaceImagePaths.Count)
            {
                continue;
            }

            var path = generator.FaceImagePaths[faceIndex];
            var label = ShortPresetLabel(Path.GetFileName(path), 10);
            if (faceIndex == currentIndex)
            {
                label = "> " + label;
            }
            ConfigureSourceButton(settingsMonitorFaceButtons[i], true, label, () =>
            {
                var texture = LoadImageTexture(path);
                if (texture == null)
                {
                    return;
                }
                ClearGeneratorLayerTextureSource(generator);
                SetGeneratorManualImageSource(generator, path);
                ApplyGeneratorTexture(generator, texture);
            });
            if (settingsMonitorFaceButtons[i] != null && settingsMonitorFaceButtons[i].Thumbnail != null)
            {
                settingsMonitorFaceButtons[i].Thumbnail.texture = LoadImageTexture(path);
            }
        }
    }

    private void OnSettingsMonitorPresetButton(int index)
    {
        if (settingsMonitorPresetButtons == null || index < 0 || index >= settingsMonitorPresetButtons.Length)
        {
            return;
        }

        var button = settingsMonitorPresetButtons[index];
        if (button != null && button.Button != null)
        {
            button.Button.onClick.Invoke();
        }
    }

    private void OnSettingsMonitorFaceButton(int index)
    {
        if (settingsMonitorFaceButtons == null || index < 0 || index >= settingsMonitorFaceButtons.Length)
        {
            return;
        }

        var button = settingsMonitorFaceButtons[index];
        if (button != null && button.Button != null)
        {
            button.Button.onClick.Invoke();
        }
    }

    private Texture ResolveGeneratorPresetThumbnail(GeneratorState generator)
    {
        if (generator == null || generatorPresets == null || generatorPresets.Count == 0)
        {
            return null;
        }

        var index = FindGeneratorPresetIndex(generator);
        if (index < 0 || index >= generatorPresets.Count)
        {
            return null;
        }

        var preset = generatorPresets[index];
        return preset == null ? null : preset.ThumbnailTexture;
    }

    private Texture ResolveGeneratorFacePreviewTexture(GeneratorState generator)
    {
        if (generator == null)
        {
            return null;
        }

        if (generator.TextureSourceLayerIndex >= 0)
        {
            return GeneratorInputLayerTexture(generator.TextureSourceLayerIndex);
        }

        switch (generator.SavedTextureSourceKind)
        {
            case GeneratorSavedTextureSourceKind.ImageFile:
                return string.IsNullOrEmpty(generator.FaceTextureImagePath) ? null : LoadImageTexture(generator.FaceTextureImagePath);
            case GeneratorSavedTextureSourceKind.BltJacket:
                return generator.FaceTextureBltPlayerNumber > 0 ? FindAlbumArtTextureForPlayer(Snapshot(), generator.FaceTextureBltPlayerNumber) : null;
            default:
                return generator.CurrentTexture;
        }
    }

    private void UpdateSettingsMonitorButtons(SettingsViewData data)
    {
        var isSourceLayer = IsSourceLayerSlot(data.SelectedLayerIndex);
        var canAudio = isSourceLayer && data.Subtitle != null && (data.Subtitle == LayerSourceKind.VideoFile.ToString() || data.Subtitle == LayerSourceKind.YouTube.ToString());
        if (settingsMonitorEnabledButton != null && settingsMonitorEnabledButton.Label != null)
        {
            settingsMonitorEnabledButton.Label.text = data.Enabled ? "ON" : "OFF";
            settingsMonitorEnabledButton.Background.color = data.Enabled ? new Color(0.16f, 0.32f, 0.18f, 1f) : new Color(0.2f, 0.1f, 0.1f, 1f);
        }
        if (settingsMonitorAudioButton != null)
        {
            settingsMonitorAudioButton.Button.gameObject.SetActive(isSourceLayer);
            if (settingsMonitorAudioButton.Label != null)
            {
                settingsMonitorAudioButton.Label.text = data.AudioEnabled ? "Audio ON" : "Audio OFF";
            }
            if (settingsMonitorAudioButton.Background != null)
            {
                settingsMonitorAudioButton.Background.color = canAudio
                    ? (data.AudioEnabled ? new Color(0.16f, 0.24f, 0.38f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f))
                    : new Color(0.08f, 0.08f, 0.08f, 1f);
            }
        }
        UpdateBlendButton(settingsMonitorBlendAlphaButton, string.Equals(data.BlendMode, "alpha", StringComparison.OrdinalIgnoreCase));
        UpdateBlendButton(settingsMonitorBlend50Button, string.Equals(data.BlendMode, "add50", StringComparison.OrdinalIgnoreCase) || string.Equals(data.BlendMode, "add", StringComparison.OrdinalIgnoreCase));
        UpdateBlendButton(settingsMonitorBlend100Button, string.Equals(data.BlendMode, "screen", StringComparison.OrdinalIgnoreCase));
        UpdateBlendButton(settingsMonitorBlendMaskButton, string.Equals(data.BlendMode, "mask", StringComparison.OrdinalIgnoreCase));
        var currentLayer = vjLayers != null && data.SelectedLayerIndex >= 0 && data.SelectedLayerIndex < vjLayers.Length ? vjLayers[data.SelectedLayerIndex] : null;
        UpdateBlendButton(settingsMonitorColorNoneButton, IsLayerColorPresetActive(currentLayer, LayerColorMode.None));
        UpdateBlendButton(settingsMonitorColorInvertButton, IsLayerColorPresetActive(currentLayer, LayerColorMode.Invert));
        UpdateBlendButton(settingsMonitorColorEdgeButton, IsLayerColorPresetActive(currentLayer, LayerColorMode.Edge));
        UpdateBlendButton(settingsMonitorColorMonoButton, IsLayerColorPresetActive(currentLayer, LayerColorMode.Monochrome));
    }

    private static void UpdateBlendButton(UiButtonBundle button, bool active)
    {
        if (button == null || button.Background == null)
        {
            return;
        }

        button.Background.color = active ? new Color(0.26f, 0.2f, 0.08f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f);
    }

    private static void UpdatePreviewMonitorButton(UiButtonBundle button, bool active)
    {
        if (button == null)
        {
            return;
        }

        if (button.Background != null)
        {
            button.Background.color = active ? new Color(0.18f, 0.2f, 0.22f, 1f) : new Color(0.028f, 0.033f, 0.038f, 1f);
        }
        if (button.Button != null)
        {
            button.Button.transition = Selectable.Transition.None;
            button.Button.targetGraphic = button.Background;
        }
        if (button.Label != null)
        {
            button.Label.fontSize = 14;
            button.Label.color = Color.white;
            button.Label.fontStyle = FontStyle.Normal;
            button.Label.alignment = TextAnchor.MiddleCenter;
        }
    }

    private void UpdateSettingsMonitorSourceButtons(SettingsViewData data)
    {
        if (settingsMonitorSourceButtons == null)
        {
            return;
        }

        for (var i = 0; i < settingsMonitorSourceButtons.Length; i++)
        {
            ConfigureSourceButton(settingsMonitorSourceButtons[i], false, "-", null);
        }

        if (vjLayers == null || data.SelectedLayerIndex < 0 || data.SelectedLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[data.SelectedLayerIndex];
        if (layer == null)
        {
            return;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                ConfigureSourceButton(settingsMonitorSourceButtons[0], true, layer.VideoMode == VideoPlaybackMode.Bpm ? "> BPM" : "BPM", () =>
                {
                    layer.VideoMode = VideoPlaybackMode.Bpm;
                    layer.VideoResyncPending = false;
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[1], true, layer.VideoMode == VideoPlaybackMode.Timeline ? "> Timeline" : "Timeline", () =>
                {
                    layer.VideoMode = VideoPlaybackMode.Timeline;
                    layer.VideoResyncPending = false;
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[2], true, "Resync", () => { layer.VideoResyncPending = true; });
                ConfigureSourceButton(settingsMonitorSourceButtons[3], true, "90", () => { layer.VideoBaseBpm = 90f; layer.VideoBaseBpmInput = "90"; });
                ConfigureSourceButton(settingsMonitorSourceButtons[4], true, "120", () => { layer.VideoBaseBpm = 120f; layer.VideoBaseBpmInput = "120"; });
                ConfigureSourceButton(settingsMonitorSourceButtons[5], true, "140", () => { layer.VideoBaseBpm = 140f; layer.VideoBaseBpmInput = "140"; });
                ConfigureSourceButton(settingsMonitorSourceButtons[6], true, "100", () => { layer.VideoBaseBpm = 100f; layer.VideoBaseBpmInput = "100"; });
                ConfigureSourceButton(settingsMonitorSourceButtons[7], true, "128", () => { layer.VideoBaseBpm = 128f; layer.VideoBaseBpmInput = "128"; });
                break;
            case LayerSourceKind.YouTube:
                if (layer.YouTube != null)
                {
                    ConfigureSourceButton(settingsMonitorSourceButtons[0], true, layer.YouTube.PlaybackMode == YoutubePlaybackMode.UrlCompatible ? "> URL" : "URL", () =>
                    {
                        layer.YouTube.PlaybackMode = YoutubePlaybackMode.UrlCompatible;
                        layer.YouTube.Error = null;
                        if (!string.IsNullOrEmpty(layer.YouTube.Url))
                        {
                            layer.YouTube.Resolving = true;
                            StartCoroutine(ResolveYoutubeVideo(data.SelectedLayerIndex, layer.YouTube.Url));
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[1], true, layer.YouTube.PlaybackMode == YoutubePlaybackMode.UrlBest ? "> Best" : "Best", () =>
                    {
                        layer.YouTube.PlaybackMode = YoutubePlaybackMode.UrlBest;
                        layer.YouTube.Error = null;
                        if (!string.IsNullOrEmpty(layer.YouTube.Url))
                        {
                            layer.YouTube.Resolving = true;
                            StartCoroutine(ResolveYoutubeVideo(data.SelectedLayerIndex, layer.YouTube.Url));
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[2], true, layer.YouTube.PlaybackMode == YoutubePlaybackMode.LocalCache ? "> Cache" : "Cache", () =>
                    {
                        layer.YouTube.PlaybackMode = YoutubePlaybackMode.LocalCache;
                        layer.YouTube.Error = null;
                        if (!string.IsNullOrEmpty(layer.YouTube.Url))
                        {
                            layer.YouTube.Resolving = true;
                            StartCoroutine(ResolveYoutubeVideo(data.SelectedLayerIndex, layer.YouTube.Url));
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[3], true, layer.Player != null && layer.Player.isPlaying ? "Pause" : "Play", () =>
                    {
                        if (layer.Player == null)
                        {
                            return;
                        }

                        if (layer.Player.isPlaying)
                        {
                            layer.Player.Pause();
                        }
                        else
                        {
                            layer.Player.Play();
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[4], true, "Restart", () =>
                    {
                        if (layer.Player != null)
                        {
                            layer.Player.time = 0;
                            layer.Player.Play();
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[5], true, "Search", () =>
                    {
                        OpenYoutubeSearchForLayer(data.SelectedLayerIndex, false);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[6], true, "Seek 0", () =>
                    {
                        if (layer.Player != null)
                        {
                            layer.Player.time = 0;
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[7], true, "x0.5", () =>
                    {
                        layer.YouTube.PlaybackSpeed = 0.5f;
                        layer.YouTube.SpeedInput = "0.50";
                        if (layer.Player != null)
                        {
                            layer.Player.playbackSpeed = 0.5f;
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[8], true, "x1.0", () =>
                    {
                        layer.YouTube.PlaybackSpeed = 1f;
                        layer.YouTube.SpeedInput = "1.00";
                        if (layer.Player != null)
                        {
                            layer.Player.playbackSpeed = 1f;
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[9], true, "x1.5", () =>
                    {
                        layer.YouTube.PlaybackSpeed = 1.5f;
                        layer.YouTube.SpeedInput = "1.50";
                        if (layer.Player != null)
                        {
                            layer.Player.playbackSpeed = 1.5f;
                        }
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[10], true, "Align", () =>
                    {
                        AutoAlignYoutubeToPlayer(layer, Snapshot());
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[11], true, layer.YouTube.WaveformPlayerNumber == 1 ? "> P1" : "P1", () => { layer.YouTube.WaveformPlayerNumber = 1; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[12], true, layer.YouTube.WaveformPlayerNumber == 2 ? "> P2" : "P2", () => { layer.YouTube.WaveformPlayerNumber = 2; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[13], true, layer.YouTube.WaveformPlayerNumber == 3 ? "> P3" : "P3", () => { layer.YouTube.WaveformPlayerNumber = 3; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[14], true, layer.YouTube.WaveformPlayerNumber == 4 ? "> P4" : "P4", () => { layer.YouTube.WaveformPlayerNumber = 4; });
                }
                break;
            case LayerSourceKind.Text:
                EnsureTextFontOptions();
                ConfigureSourceButton(settingsMonitorSourceButtons[0], true, "Size -", () =>
                {
                    layer.TextFontSize = Mathf.Clamp(layer.TextFontSize - 8, 12, 256);
                    layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                    UpdateTextLayerTexture(layer);
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[1], true, "Size +", () =>
                {
                    layer.TextFontSize = Mathf.Clamp(layer.TextFontSize + 8, 12, 256);
                    layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                    UpdateTextLayerTexture(layer);
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[2], true, "Font Next", () =>
                {
                    CycleTextFont(layer, 1);
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[3], true, "TEXT", () =>
                {
                    layer.TextContent = "TEXT";
                    layer.TextInput = "TEXT";
                    UpdateTextLayerTexture(layer);
                });
                ConfigureSourceButton(settingsMonitorSourceButtons[4], true, "Font Prev", () =>
                {
                    CycleTextFont(layer, -1);
                });
                var fontCurrentIndex = textFontOptions.FindIndex(f => string.Equals(f, layer.TextFontName, StringComparison.OrdinalIgnoreCase));
                fontCurrentIndex = fontCurrentIndex < 0 ? 0 : fontCurrentIndex;
                var fontStart = Mathf.Max(0, fontCurrentIndex - 2);
                for (var i = 0; i < 5 && fontStart + i < textFontOptions.Count; i++)
                {
                    var fontName = textFontOptions[fontStart + i];
                    var label = string.Equals(layer.TextFontName, fontName, StringComparison.OrdinalIgnoreCase) ? "> " + fontName : fontName;
                    var buttonIndex = 5 + i;
                    ConfigureSourceButton(settingsMonitorSourceButtons[buttonIndex], true, label, () =>
                    {
                        layer.TextFontName = fontName;
                        UpdateTextLayerTexture(layer);
                    });
                }
                break;
            case LayerSourceKind.Generator3D:
                if (layer.Generator != null)
                {
                    ConfigureSourceButton(settingsMonitorSourceButtons[0], true, layer.Generator.PresentationMode == GeneratorPresentationMode.AroundObject ? "> Center" : "Center", () => { layer.Generator.PresentationMode = GeneratorPresentationMode.AroundObject; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[1], true, layer.Generator.PresentationMode == GeneratorPresentationMode.Tunnel ? "> Tunnel" : "Tunnel", () => { layer.Generator.PresentationMode = GeneratorPresentationMode.Tunnel; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[2], true, layer.Generator.PresentationMode == GeneratorPresentationMode.Lighting ? "> Lighting" : "Lighting", () => { layer.Generator.PresentationMode = GeneratorPresentationMode.Lighting; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[3], true, layer.Generator.CameraWorkMode == GeneratorCameraWorkMode.Orbit ? "> Orbit" : "Orbit", () => { layer.Generator.CameraWorkMode = GeneratorCameraWorkMode.Orbit; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[4], true, layer.Generator.CameraWorkMode == GeneratorCameraWorkMode.EaseIn ? "> EaseIn" : "EaseIn", () => { layer.Generator.CameraWorkMode = GeneratorCameraWorkMode.EaseIn; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[5], true, layer.Generator.CameraWorkMode == GeneratorCameraWorkMode.EaseOut ? "> EaseOut" : "EaseOut", () => { layer.Generator.CameraWorkMode = GeneratorCameraWorkMode.EaseOut; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[6], true, layer.Generator.ObjectOrbitMode == GeneratorObjectOrbitMode.None ? "> None" : "None", () => { layer.Generator.ObjectOrbitMode = GeneratorObjectOrbitMode.None; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[7], true, layer.Generator.ObjectOrbitMode == GeneratorObjectOrbitMode.Circular ? "> Circ" : "Circ", () => { layer.Generator.ObjectOrbitMode = GeneratorObjectOrbitMode.Circular; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[8], true, layer.Generator.ObjectOrbitMode == GeneratorObjectOrbitMode.Spherical ? "> Sphere" : "Sphere", () => { layer.Generator.ObjectOrbitMode = GeneratorObjectOrbitMode.Spherical; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[9], true, layer.Generator.ObjectSpinEnabled ? "Spin ON" : "Spin OFF", () => { layer.Generator.ObjectSpinEnabled = !layer.Generator.ObjectSpinEnabled; });
                    ConfigureSourceButton(settingsMonitorSourceButtons[10], true, layer.Generator.ObjectBpmPulseEnabled ? "Pulse ON" : "Pulse OFF", () => { layer.Generator.ObjectBpmPulseEnabled = !layer.Generator.ObjectBpmPulseEnabled; });
                    if (layer.Generator.PresentationMode == GeneratorPresentationMode.Lighting)
                    {
                        ConfigureSourceButton(settingsMonitorSourceButtons[11], true,
                            layer.Generator.LightingRigMode == GeneratorLightingRigMode.AlternateBlink ? "Alt" :
                            layer.Generator.LightingRigMode == GeneratorLightingRigMode.InOutSweep ? "InOut" : "Tangent",
                            () =>
                            {
                                if (layer.Generator.LightingRigMode == GeneratorLightingRigMode.AlternateBlink)
                                {
                                    layer.Generator.LightingRigMode = GeneratorLightingRigMode.InOutSweep;
                                }
                                else if (layer.Generator.LightingRigMode == GeneratorLightingRigMode.InOutSweep)
                                {
                                    layer.Generator.LightingRigMode = GeneratorLightingRigMode.TangentSweep;
                                }
                                else
                                {
                                    layer.Generator.LightingRigMode = GeneratorLightingRigMode.AlternateBlink;
                                }
                            });
                    }
                    ConfigureSourceButton(settingsMonitorSourceButtons[12], true, layer.Generator.TransparentBackground ? "Bg:Trans" : "Bg:Black", () =>
                    {
                        layer.Generator.TransparentBackground = !layer.Generator.TransparentBackground;
                        ApplyGeneratorCameraBackground(layer.Generator);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[13], true, layer.Generator.SurroundScreensEnabled ? "Screens ON" : "Screens OFF", () =>
                    {
                        layer.Generator.SurroundScreensEnabled = !layer.Generator.SurroundScreensEnabled;
                        SetGeneratorSurroundScreensVisible(layer.Generator, layer.Generator.SurroundScreensEnabled && layer.Generator.SurroundScreenSourceLayerIndex >= 0);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[14], true, layer.Generator.ParticlesEnabled ? "Parts ON" : "Parts OFF", () =>
                    {
                        layer.Generator.ParticlesEnabled = !layer.Generator.ParticlesEnabled;
                        SetGeneratorParticlesVisible(layer.Generator, layer.Generator.ParticlesEnabled);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[15], true, "Skybox Next", () =>
                    {
                        CycleGeneratorSkybox(layer.Generator);
                    });
                ConfigureSourceButton(settingsMonitorSourceButtons[16], true, "Preset <", () =>
                    {
                        CycleGeneratorPreset(layer.Generator, -1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[17], true, "Preset >", () =>
                    {
                        CycleGeneratorPreset(layer.Generator, 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[18], true, "Preset Apply", () =>
                    {
                        ApplyCurrentGeneratorPreset(layer.Generator);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[19], true, "Model Next", () =>
                    {
                        CycleGeneratorModel(layer.Generator, 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[20], true, "Face Clear", () =>
                    {
                        ClearGeneratorLayerTextureSource(layer.Generator);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[21], true, "Face Img", () =>
                    {
                        CycleGeneratorFaceImage(layer.Generator, 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[22], true, "Face Jkt", () =>
                    {
                        CycleGeneratorFaceJacket(layer.Generator, Snapshot());
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[23], true, "Face Layer", () =>
                    {
                        CycleGeneratorFaceLayerSource(layer.Generator, data.SelectedLayerIndex, 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[24], true, "Bld Next", () =>
                    {
                        CycleGeneratorSceneLayer(layer.Generator, GeneratorSceneLayerKind.Buildings, "SceneBuildings_", 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[25], true, "Grd Next", () =>
                    {
                        CycleGeneratorSceneLayer(layer.Generator, GeneratorSceneLayerKind.Ground, "SceneGround_", 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[26], true, "Trf Next", () =>
                    {
                        CycleGeneratorSceneLayer(layer.Generator, GeneratorSceneLayerKind.Traffic, "SceneTraffic_", 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[27], true, "Veg Next", () =>
                    {
                        CycleGeneratorSceneLayer(layer.Generator, GeneratorSceneLayerKind.Vegetation, "SceneVegetation_", 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[28], true, "ScreenSrc", () =>
                    {
                        CycleGeneratorSurroundScreenSource(layer.Generator, data.SelectedLayerIndex, 1);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[29], true, "Reset Xf", () =>
                    {
                        ResetGeneratorModelTransform(layer.Generator);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[30], true, "Sky None", () =>
                    {
                        SetGeneratorSkybox(layer.Generator, null);
                    });
                    ConfigureSourceButton(settingsMonitorSourceButtons[31], true, "Face Img<", () =>
                    {
                        CycleGeneratorFaceImage(layer.Generator, -1);
                    });
                }
                break;
        }
    }

    private void UpdateSettingsMonitorEffectButtons(SettingsViewData data)
    {
        if (settingsMonitorEffectButtons == null)
        {
            return;
        }

        for (var i = 0; i < settingsMonitorEffectButtons.Length; i++)
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[i], false, "-", null);
        }

        if (vjLayers == null || data.SelectedLayerIndex < 0 || data.SelectedLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[data.SelectedLayerIndex];
        if (layer == null || layer.Effect == null || !layer.Effect.HasEffect)
        {
            return;
        }

        var effect = layer.Effect;
        ConfigureSourceButton(settingsMonitorEffectButtons[0], true, effect.Enabled ? "Disable" : "Enable", () =>
        {
            effect.Enabled = !effect.Enabled;
        });
        ConfigureSourceButton(settingsMonitorEffectButtons[1], true, "Clear", () =>
        {
            StopLayerVideoPlayback(layer);
            ResetLayerEffectState(layer);
            layer.SourceKind = LayerSourceKind.None;
            layer.SourceOrigin = LayerSourceOrigin.None;
            layer.SourceName = null;
            layer.EffectRenderedOutput = null;
            layer.EffectRenderedFrame = -1;
        });

        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                ConfigureSourceButton(settingsMonitorEffectButtons[2], true, effect.RgbMode == LayerRgbEffectMode.RedOnly ? "> Red" : "Red", () => { effect.RgbMode = LayerRgbEffectMode.RedOnly; });
                ConfigureSourceButton(settingsMonitorEffectButtons[3], true, effect.RgbMode == LayerRgbEffectMode.GreenOnly ? "> Green" : "Green", () => { effect.RgbMode = LayerRgbEffectMode.GreenOnly; });
                ConfigureSourceButton(settingsMonitorEffectButtons[4], true, effect.RgbMode == LayerRgbEffectMode.BlueOnly ? "> Blue" : "Blue", () => { effect.RgbMode = LayerRgbEffectMode.BlueOnly; });
                ConfigureSourceButton(settingsMonitorEffectButtons[5], true, effect.RgbMode == LayerRgbEffectMode.RgbInvert ? "> Invert" : "Invert", () => { effect.RgbMode = LayerRgbEffectMode.RgbInvert; });
                ConfigureSourceButton(settingsMonitorEffectButtons[6], true, effect.RgbMode == LayerRgbEffectMode.Monochrome ? "> B/W" : "B/W", () => { effect.RgbMode = LayerRgbEffectMode.Monochrome; });
                break;
            case LayerEffectKind.Blur:
                ConfigureSourceButton(settingsMonitorEffectButtons[2], true, effect.Mode == LayerEffectMode.Horizontal ? "> H" : "H", () => { effect.Mode = LayerEffectMode.Horizontal; });
                ConfigureSourceButton(settingsMonitorEffectButtons[3], true, effect.Mode == LayerEffectMode.Vertical ? "> V" : "V", () => { effect.Mode = LayerEffectMode.Vertical; });
                ConfigureSourceButton(settingsMonitorEffectButtons[4], true, effect.Mode == LayerEffectMode.Alternate ? "> H>V" : "H>V", () => { effect.Mode = LayerEffectMode.Alternate; });
                ConfigureSourceButton(settingsMonitorEffectButtons[5], true, effect.Mode == LayerEffectMode.Zoom ? "> Zoom" : "Zoom", () => { effect.Mode = LayerEffectMode.Zoom; });
                break;
            case LayerEffectKind.Mirror:
                ConfigureSourceButton(settingsMonitorEffectButtons[2], true, effect.Mode == LayerEffectMode.Horizontal ? "> H" : "H", () => { effect.Mode = LayerEffectMode.Horizontal; });
                ConfigureSourceButton(settingsMonitorEffectButtons[3], true, effect.Mode == LayerEffectMode.Vertical ? "> V" : "V", () => { effect.Mode = LayerEffectMode.Vertical; });
                ConfigureSourceButton(settingsMonitorEffectButtons[4], true, effect.Mode == LayerEffectMode.Alternate ? "> Quad" : "Quad", () => { effect.Mode = LayerEffectMode.Alternate; });
                break;
            case LayerEffectKind.Strobe:
                ConfigureSourceButton(settingsMonitorEffectButtons[2], true, effect.Mode == LayerEffectMode.Beat1 ? "> 1" : "1", () => { effect.Mode = LayerEffectMode.Beat1; });
                ConfigureSourceButton(settingsMonitorEffectButtons[3], true, effect.Mode == LayerEffectMode.Beat2 ? "> 1/2" : "1/2", () => { effect.Mode = LayerEffectMode.Beat2; });
                ConfigureSourceButton(settingsMonitorEffectButtons[4], true, effect.Mode == LayerEffectMode.Beat4 ? "> 1/4" : "1/4", () => { effect.Mode = LayerEffectMode.Beat4; });
                ConfigureSourceButton(settingsMonitorEffectButtons[5], true, effect.Mode == LayerEffectMode.Manual ? "> Manual" : "Manual", () => { effect.Mode = LayerEffectMode.Manual; });
                break;
            case LayerEffectKind.StepSequencer:
                ConfigureSourceButton(settingsMonitorEffectButtons[2], true, "Queue " + (effect.StepSequenceQueue == null ? 0 : effect.StepSequenceQueue.Count).ToString(CultureInfo.InvariantCulture), null);
                break;
        }

        if (effect.Kind == LayerEffectKind.Blur ||
            effect.Kind == LayerEffectKind.Glitch ||
            effect.Kind == LayerEffectKind.RgbEffect ||
            effect.Kind == LayerEffectKind.Kaleido ||
            effect.Kind == LayerEffectKind.Pixelate)
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[7], true, "Amt -", () =>
            {
                effect.Intensity = Mathf.Clamp01(effect.Intensity - 0.1f);
            });
            ConfigureSourceButton(settingsMonitorEffectButtons[8], true, "Amt +", () =>
            {
                effect.Intensity = Mathf.Clamp01(effect.Intensity + 0.1f);
            });
            ConfigureSourceButton(settingsMonitorEffectButtons[9], true, Mathf.RoundToInt(effect.Intensity * 100f).ToString(CultureInfo.InvariantCulture), null);
        }
        else if (effect.Kind == LayerEffectKind.StepSequencer)
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[7], true, "QueueClr", () =>
            {
                if (effect.StepSequenceQueue != null)
                {
                    effect.StepSequenceQueue.Clear();
                }
                SyncStepSequencerMediaSlots(data.SelectedLayerIndex, effect);
            });
            ConfigureSourceButton(settingsMonitorEffectButtons[8], true, "Queue " + (effect.StepSequenceQueue == null ? 0 : effect.StepSequenceQueue.Count).ToString(CultureInfo.InvariantCulture), null);
            ConfigureSourceButton(settingsMonitorEffectButtons[9], true, effect.StepSequenceLength == 2 ? "> 2" : "2", () =>
            {
                effect.StepSequenceLength = 2;
            });
            if (settingsMonitorEffectButtons.Length > 10)
            {
                ConfigureSourceButton(settingsMonitorEffectButtons[10], true, effect.StepSequenceLength == 4 ? "> 4" : "4", () =>
                {
                    effect.StepSequenceLength = 4;
                });
            }
        }
        else if (effect.Kind == LayerEffectKind.Strobe && effect.Mode == LayerEffectMode.Manual)
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[7], true, effect.ManualFlashHeld ? "HOLD ON" : "HOLD", () =>
            {
                effect.ManualFlashHeld = !effect.ManualFlashHeld;
            });
        }

        ConfigureSourceButton(settingsMonitorEffectButtons[10], true, midiEditMode ? "MIDI ON" : "MIDI", () =>
        {
            midiEditMode = !midiEditMode;
            if (!midiEditMode)
            {
                midiLearningAction = MidiBindingAction.None;
                midiLearningIndex = -1;
                midiLearningSubIndex = -1;
            }
        });
        ConfigureSourceButton(settingsMonitorEffectButtons[11], true, "LearnMode", () =>
        {
            if (!midiEditMode)
            {
                midiEditMode = true;
            }

            if (effect.Kind == LayerEffectKind.RgbEffect)
            {
                BeginMidiLearn(MidiBindingAction.LayerEffectSetRgbMode, data.SelectedLayerIndex, (int)effect.RgbMode);
            }
            else if (effect.Kind == LayerEffectKind.Blur ||
                     effect.Kind == LayerEffectKind.Mirror ||
                     effect.Kind == LayerEffectKind.Strobe)
            {
                BeginMidiLearn(MidiBindingAction.LayerEffectSetMode, data.SelectedLayerIndex, (int)effect.Mode);
            }
            else
            {
                BeginMidiLearn(MidiBindingAction.LayerEffectModeCycle, data.SelectedLayerIndex);
            }
        });
        if (settingsMonitorEffectButtons.Length > 12)
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[12], true, "LearnVal", () =>
            {
                if (!midiEditMode)
                {
                    midiEditMode = true;
                }
                BeginMidiLearn(MidiBindingAction.LayerEffectValue, data.SelectedLayerIndex);
            });
        }
        else
        {
            ConfigureSourceButton(settingsMonitorEffectButtons[11], true, "LearnVal", () =>
            {
                if (!midiEditMode)
                {
                    midiEditMode = true;
                }
                BeginMidiLearn(MidiBindingAction.LayerEffectValue, data.SelectedLayerIndex);
            });
        }

        if (midiEditMode && midiLearningIndex == data.SelectedLayerIndex)
        {
            if (midiLearningAction == MidiBindingAction.LayerEffectToggle)
            {
                TintButton(settingsMonitorEffectButtons[0], new Color(0.10f, 0.20f, 0.42f, 1f));
                SetButtonBadge(settingsMonitorEffectButtons[0], "MIDI", new Color(0.55f, 0.8f, 1f, 1f));
            }
            else if (midiLearningAction == MidiBindingAction.LayerEffectSetRgbMode ||
                     midiLearningAction == MidiBindingAction.LayerEffectSetMode ||
                     midiLearningAction == MidiBindingAction.LayerEffectModeCycle)
            {
                TintButton(settingsMonitorEffectButtons[11], new Color(0.10f, 0.20f, 0.42f, 1f));
                SetButtonBadge(settingsMonitorEffectButtons[11], "MIDI", new Color(0.55f, 0.8f, 1f, 1f));
            }
            else if (midiLearningAction == MidiBindingAction.LayerEffectValue)
            {
                var target = settingsMonitorEffectButtons.Length > 12 ? settingsMonitorEffectButtons[12] : settingsMonitorEffectButtons[11];
                TintButton(target, new Color(0.10f, 0.20f, 0.42f, 1f));
                SetButtonBadge(target, "MIDI", new Color(0.55f, 0.8f, 1f, 1f));
            }
        }
    }

    private static void TintButton(UiButtonBundle button, Color color)
    {
        if (button == null || button.Background == null)
        {
            return;
        }

        button.Background.color = color;
    }

    private static void SetButtonBadge(UiButtonBundle button, string text, Color color)
    {
        if (button == null || button.Badge == null)
        {
            return;
        }

        button.Badge.text = text;
        button.Badge.color = color;
        button.Badge.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private static string ShortPresetLabel(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text.Substring(0, Math.Max(1, maxChars - 3)) + "...";
    }

    private void ConfigureSourceButton(UiButtonBundle button, bool visible, string label, Action action)
    {
        if (button == null || button.Button == null)
        {
            return;
        }

        button.Button.gameObject.SetActive(visible);
        if (!visible)
        {
            if (button.Thumbnail != null)
            {
                button.Thumbnail.gameObject.SetActive(false);
            }
            if (button.Badge != null)
            {
                button.Badge.gameObject.SetActive(false);
            }
            return;
        }

        if (button.Label != null)
        {
            button.Label.text = label;
        }
        if (button.Thumbnail != null)
        {
            button.Thumbnail.gameObject.SetActive(button.Thumbnail.texture != null);
        }
        if (button.Badge != null)
        {
            button.Badge.gameObject.SetActive(false);
        }
        button.Button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.Button.onClick.AddListener(() => action());
        }
        if (button.Background != null)
        {
            button.Background.color = label.StartsWith(">", StringComparison.Ordinal) ? new Color(0.26f, 0.2f, 0.08f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f);
        }
    }

    private void OnSettingsMonitorSourceButton(int index)
    {
        if (settingsMonitorSourceButtons == null || index < 0 || index >= settingsMonitorSourceButtons.Length)
        {
            return;
        }

        var button = settingsMonitorSourceButtons[index];
        if (button == null || button.Button == null)
        {
            return;
        }

        button.Button.onClick.Invoke();
    }

    private void UpdateSettingsMonitorCommonButtons(SettingsViewData data)
    {
        if (settingsMonitorCommonButtons == null)
        {
            return;
        }

        for (var i = 0; i < settingsMonitorCommonButtons.Length; i++)
        {
            ConfigureSourceButton(settingsMonitorCommonButtons[i], false, "-", null);
        }

        if (vjLayers == null || data == null || data.SelectedLayerIndex < 0 || data.SelectedLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[data.SelectedLayerIndex];
        if (layer == null)
        {
            return;
        }

        var isSourceLayer = IsSourceLayerSlot(data.SelectedLayerIndex);
        ConfigureSourceButton(settingsMonitorCommonButtons[0], true, "Op -", () => AdjustSettingsMonitorOpacity(-0.05f));
        ConfigureSourceButton(settingsMonitorCommonButtons[1], true, "Op +", () => AdjustSettingsMonitorOpacity(0.05f));
        ConfigureSourceButton(settingsMonitorCommonButtons[2], true, "Hue -", () => AdjustSettingsMonitorHue(-1f / 36f));
        ConfigureSourceButton(settingsMonitorCommonButtons[3], true, "Hue +", () => AdjustSettingsMonitorHue(1f / 36f));
        ConfigureSourceButton(settingsMonitorCommonButtons[4], false, "-", null);

        ConfigureSourceButton(settingsMonitorCommonButtons[5], true, "Inv -", () => AdjustSettingsMonitorInvert(-0.1f));
        ConfigureSourceButton(settingsMonitorCommonButtons[6], true, "Inv +", () => AdjustSettingsMonitorInvert(0.1f));
        ConfigureSourceButton(settingsMonitorCommonButtons[7], true, "B/W -", () => AdjustSettingsMonitorMonochrome(-0.1f));
        ConfigureSourceButton(settingsMonitorCommonButtons[8], true, "B/W +", () => AdjustSettingsMonitorMonochrome(0.1f));
        ConfigureSourceButton(settingsMonitorCommonButtons[9], false, "-", null);

        if (midiEditMode)
        {
            SetButtonBadge(settingsMonitorCommonButtons[0], "Opacity", new Color(0.55f, 0.8f, 1f, 1f));
            SetButtonBadge(settingsMonitorCommonButtons[2], "Hue", new Color(0.55f, 0.8f, 1f, 1f));
            SetButtonBadge(settingsMonitorCommonButtons[5], "Invert", new Color(0.55f, 0.8f, 1f, 1f));
            SetButtonBadge(settingsMonitorCommonButtons[7], "B/W", new Color(0.55f, 0.8f, 1f, 1f));
        }
    }

    private void OnSettingsMonitorCommonButton(int index)
    {
        if (settingsMonitorCommonButtons == null || index < 0 || index >= settingsMonitorCommonButtons.Length)
        {
            return;
        }

        var button = settingsMonitorCommonButtons[index];
        if (button == null || button.Button == null)
        {
            return;
        }

        button.Button.onClick.Invoke();
    }

    private LayerState CurrentSettingsMonitorLayer()
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return null;
        }

        return vjLayers[layerIndex];
    }

    private void AdjustSettingsMonitorOpacity(float delta)
    {
        var layer = CurrentSettingsMonitorLayer();
        if (layer == null)
        {
            return;
        }

        layer.Opacity = Mathf.Clamp01(layer.Opacity + delta);
    }

    private void AdjustSettingsMonitorHue(float delta)
    {
        var layer = CurrentSettingsMonitorLayer();
        if (layer == null)
        {
            return;
        }

        layer.HueShift = Mathf.Repeat(layer.HueShift + delta, 1f);
    }

    private void AdjustSettingsMonitorInvert(float delta)
    {
        var layer = CurrentSettingsMonitorLayer();
        if (layer == null)
        {
            return;
        }

        layer.InvertAmount = Mathf.Clamp01(layer.InvertAmount + delta);
        if (layer.InvertAmount > 0.001f)
        {
            layer.ColorMode = LayerColorMode.Invert;
        }
    }

    private void AdjustSettingsMonitorMonochrome(float delta)
    {
        var layer = CurrentSettingsMonitorLayer();
        if (layer == null)
        {
            return;
        }

        layer.MonochromeAmount = Mathf.Clamp01(layer.MonochromeAmount + delta);
        if (layer.MonochromeAmount > 0.001f)
        {
            layer.ColorMode = LayerColorMode.Monochrome;
        }
    }

    private void SetSettingsMonitorSourceGroupMode(LayerSourceGroupMode mode)
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (!IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        var layer = CurrentSettingsMonitorLayer();
        if (layer == null)
        {
            return;
        }

        layer.SourceGroupMode = mode;
    }

    private void CycleGeneratorSkybox(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        EnsureGeneratorResources();
        if (generatorSkyboxMaterials == null || generatorSkyboxMaterials.Length == 0)
        {
            SetGeneratorSkybox(generator, null);
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < generatorSkyboxMaterials.Length; i++)
        {
            var material = generatorSkyboxMaterials[i];
            if (material != null && material == generator.SkyboxMaterial)
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = currentIndex + 1;
        if (nextIndex >= generatorSkyboxMaterials.Length)
        {
            SetGeneratorSkybox(generator, null);
            return;
        }

        SetGeneratorSkybox(generator, generatorSkyboxMaterials[nextIndex]);
    }

    private int FindGeneratorPresetIndex(GeneratorState generator)
    {
        if (generator == null || generatorPresets == null || generatorPresets.Count == 0)
        {
            return -1;
        }

        for (var i = 0; i < generatorPresets.Count; i++)
        {
            var preset = generatorPresets[i];
            if (preset != null && string.Equals(preset.Name, generator.PresetNameInput, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void CycleGeneratorPreset(GeneratorState generator, int delta)
    {
        if (generator == null || generatorPresets == null || generatorPresets.Count == 0)
        {
            return;
        }

        var currentIndex = FindGeneratorPresetIndex(generator);
        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % generatorPresets.Count;
        if (nextIndex < 0)
        {
            nextIndex += generatorPresets.Count;
        }

        var preset = generatorPresets[nextIndex];
        if (preset != null)
        {
            generator.PresetNameInput = preset.Name;
        }
    }

    private void ApplyCurrentGeneratorPreset(GeneratorState generator)
    {
        if (generator == null || generatorPresets == null || generatorPresets.Count == 0)
        {
            return;
        }

        var index = FindGeneratorPresetIndex(generator);
        if (index < 0 || index >= generatorPresets.Count)
        {
            index = 0;
            if (generatorPresets[index] != null)
            {
                generator.PresetNameInput = generatorPresets[index].Name;
            }
        }

        var preset = generatorPresets[index];
        if (preset != null)
        {
            ApplyGeneratorPreset(generator, preset, Snapshot());
        }
    }

    private void CycleTextFont(LayerState layer, int delta)
    {
        if (layer == null)
        {
            return;
        }

        EnsureTextFontOptions();
        if (textFontOptions.Count <= 0)
        {
            return;
        }

        var currentIndex = textFontOptions.FindIndex(f => string.Equals(f, layer.TextFontName, StringComparison.OrdinalIgnoreCase));
        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % textFontOptions.Count;
        if (nextIndex < 0)
        {
            nextIndex += textFontOptions.Count;
        }

        layer.TextFontName = textFontOptions[nextIndex];
        UpdateTextLayerTexture(layer);
    }

    private void CycleGeneratorModel(GeneratorState generator, int delta)
    {
        if (generator == null || generatorModelPrefabs == null || generatorModelPrefabs.Length == 0)
        {
            return;
        }

        var candidates = new List<GameObject>();
        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            if (prefab.name.StartsWith("SceneBuildings_", StringComparison.OrdinalIgnoreCase) ||
                prefab.name.StartsWith("SceneGround_", StringComparison.OrdinalIgnoreCase) ||
                prefab.name.StartsWith("SceneTraffic_", StringComparison.OrdinalIgnoreCase) ||
                prefab.name.StartsWith("SceneVegetation_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(prefab);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i].name, generator.ModelName, StringComparison.Ordinal))
            {
                currentIndex = i;
                break;
            }
        }

        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % candidates.Count;
        if (nextIndex < 0)
        {
            nextIndex += candidates.Count;
        }

        LoadGeneratorModel(generator, candidates[nextIndex]);
    }

    private void ResetGeneratorModelTransform(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        Vector3 defaultPosition;
        Vector3 defaultRotation;
        Vector3 defaultScale;
        ApplyGeneratorAssetDefaultTransform(generator.ModelName, out defaultPosition, out defaultRotation, out defaultScale, false);
        generator.ModelPositionInput = FormatVector3(defaultPosition);
        generator.ModelRotationInput = FormatVector3(defaultRotation);
        generator.ModelScaleInput = FormatVector3(defaultScale);
        ApplyGeneratorModelTransform(generator);
    }

    private void CycleGeneratorSceneLayer(GeneratorState generator, GeneratorSceneLayerKind kind, string prefix, int delta)
    {
        if (generator == null || generatorModelPrefabs == null || generatorModelPrefabs.Length == 0)
        {
            return;
        }

        var candidates = new List<GameObject>();
        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab != null && prefab.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(prefab);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var currentName = GetGeneratorSceneLayerName(generator, kind);
        var currentIndex = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i].name, currentName, StringComparison.Ordinal))
            {
                currentIndex = i;
                break;
            }
        }

        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % candidates.Count;
        if (nextIndex < 0)
        {
            nextIndex += candidates.Count;
        }

        LoadGeneratorSceneLayer(generator, candidates[nextIndex], kind);
    }

    private void CycleGeneratorFaceImage(GeneratorState generator, int delta)
    {
        if (generator == null)
        {
            return;
        }

        if (generator.FaceImagePaths == null || generator.FaceImagePaths.Count == 0)
        {
            RefreshGeneratorFaceImageFolder(generator);
        }

        if (generator.FaceImagePaths == null || generator.FaceImagePaths.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < generator.FaceImagePaths.Count; i++)
        {
            if (string.Equals(generator.FaceImagePaths[i], generator.FaceTextureImagePath, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % generator.FaceImagePaths.Count;
        if (nextIndex < 0)
        {
            nextIndex += generator.FaceImagePaths.Count;
        }

        var path = generator.FaceImagePaths[nextIndex];
        var texture = LoadImageTexture(path);
        if (texture == null)
        {
            return;
        }

        ClearGeneratorLayerTextureSource(generator);
        SetGeneratorManualImageSource(generator, path);
        ApplyGeneratorTexture(generator, texture);
    }

    private void CycleGeneratorFaceJacket(GeneratorState generator, StateSnapshot snapshot)
    {
        if (generator == null || snapshot == null || snapshot.Players == null || snapshot.Players.Count == 0)
        {
            return;
        }

        var players = CollectAlbumArtPlayers(snapshot);
        if (players.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i] != null && players[i].DeviceNumber == generator.FaceTextureBltPlayerNumber)
            {
                currentIndex = i;
                break;
            }
        }

        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + 1) % players.Count;
        var player = players[nextIndex];
        if (player == null || player.AlbumArtTexture == null)
        {
            return;
        }

        ClearGeneratorLayerTextureSource(generator);
        SetGeneratorBltJacketSource(generator, player.DeviceNumber);
        ApplyGeneratorTexture(generator, player.AlbumArtTexture);
    }

    private void CycleGeneratorFaceLayerSource(GeneratorState generator, int hostLayerIndex, int delta)
    {
        if (generator == null || vjLayers == null || vjLayers.Length == 0)
        {
            return;
        }

        var candidates = new List<int>();
        for (var i = 0; i < vjLayers.Length; i++)
        {
            if (i == hostLayerIndex)
            {
                continue;
            }

            var layer = vjLayers[i];
            if (layer == null || layer.SourceKind == LayerSourceKind.None)
            {
                continue;
            }

            candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == generator.TextureSourceLayerIndex)
            {
                currentIndex = i;
                break;
            }
        }

        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % candidates.Count;
        if (nextIndex < 0)
        {
            nextIndex += candidates.Count;
        }

        SetGeneratorLayerTextureSource(generator, candidates[nextIndex]);
    }

    private void CycleGeneratorSurroundScreenSource(GeneratorState generator, int hostLayerIndex, int delta)
    {
        if (generator == null || vjLayers == null || vjLayers.Length == 0)
        {
            return;
        }

        var candidates = new List<int> { -1 };
        for (var i = 0; i < vjLayers.Length; i++)
        {
            if (i == hostLayerIndex)
            {
                continue;
            }

            var layer = vjLayers[i];
            if (layer == null || layer.SourceKind == LayerSourceKind.None)
            {
                continue;
            }

            candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var currentIndex = candidates.FindIndex(index => index == generator.SurroundScreenSourceLayerIndex);
        currentIndex = currentIndex < 0 ? 0 : currentIndex;
        var nextIndex = (currentIndex + delta) % candidates.Count;
        if (nextIndex < 0)
        {
            nextIndex += candidates.Count;
        }

        var selected = candidates[nextIndex];
        if (selected < 0)
        {
            generator.SurroundScreenSourceLayerIndex = -1;
            generator.SurroundScreenSourceLayerName = null;
            generator.SurroundScreenCurrentTexture = null;
            UpdateGeneratorSurroundScreens(generator, BaseGeneratorObjectPosition());
            return;
        }

        generator.SurroundScreensEnabled = true;
        generator.SurroundScreenSourceLayerIndex = selected;
        generator.SurroundScreenSourceLayerName = LayerSlotLabel(selected);
        generator.SurroundScreenCurrentTexture = null;
        UpdateGeneratorSurroundScreens(generator, BaseGeneratorObjectPosition());
    }

    private void OnSettingsMonitorEffectButton(int index)
    {
        if (settingsMonitorEffectButtons == null || index < 0 || index >= settingsMonitorEffectButtons.Length)
        {
            return;
        }

        var button = settingsMonitorEffectButtons[index];
        if (button == null || button.Button == null)
        {
            return;
        }

        button.Button.onClick.Invoke();
    }

    private void UpdateSettingsMonitorInputs(SettingsViewData data)
    {
        var showA = false;
        var showB = false;
        var textA = "";
        var textB = "";
        var applyALabel = "Apply";
        var applyBLabel = "Apply";

        LayerState layer = null;
        if (vjLayers != null && data.SelectedLayerIndex >= 0 && data.SelectedLayerIndex < vjLayers.Length)
        {
            layer = vjLayers[data.SelectedLayerIndex];
        }

        if (layer != null)
        {
            switch (layer.SourceKind)
            {
                case LayerSourceKind.Text:
                    showA = true;
                    showB = true;
                    textA = layer.TextInput ?? layer.TextContent ?? "TEXT";
                    textB = layer.TextFontSizeInput ?? layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                    applyALabel = "Text";
                    applyBLabel = "Size";
                    break;
                case LayerSourceKind.YouTube:
                    if (layer.YouTube != null)
                    {
                        showA = true;
                        showB = true;
                        textA = layer.YouTube.TimeInput ?? "0.0";
                        textB = layer.YouTube.SpeedInput ?? "1.00";
                        applyALabel = "Seek";
                        applyBLabel = "Speed";
                    }
                    break;
                case LayerSourceKind.Generator3D:
                    if (layer.Generator != null)
                    {
                        showA = true;
                        textA = layer.Generator.PresetNameInput ?? "Preset";
                        applyALabel = "Save";
                    }
                    break;
            }
        }

        SetInputFieldState(settingsMonitorInputFieldA, showA, textA);
        SetInputFieldState(settingsMonitorInputFieldB, showB, textB);
        SetButtonState(settingsMonitorInputApplyButtonA, showA, applyALabel);
        SetButtonState(settingsMonitorInputApplyButtonB, showB, applyBLabel);
    }

    private static void SetInputFieldState(InputField input, bool visible, string value)
    {
        if (input == null)
        {
            return;
        }

        input.gameObject.SetActive(visible);
        if (visible && input.text != value)
        {
            input.SetTextWithoutNotify(value ?? "");
        }
    }

    private static void SetButtonState(UiButtonBundle button, bool visible, string label)
    {
        if (button == null || button.Button == null)
        {
            return;
        }

        button.Button.gameObject.SetActive(visible);
        if (visible && button.Label != null)
        {
            button.Label.text = label;
        }
    }

    private void ApplySettingsMonitorInputA()
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.Text:
                layer.TextContent = string.IsNullOrEmpty(settingsMonitorInputFieldA == null ? null : settingsMonitorInputFieldA.text) ? "TEXT" : settingsMonitorInputFieldA.text;
                layer.TextInput = layer.TextContent;
                UpdateTextLayerTexture(layer);
                break;
            case LayerSourceKind.YouTube:
                if (layer.Player != null && settingsMonitorInputFieldA != null)
                {
                    double seconds;
                    if (double.TryParse(settingsMonitorInputFieldA.text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                    {
                        layer.YouTube.TimeInput = seconds.ToString("0.0", CultureInfo.InvariantCulture);
                        layer.Player.time = Math.Max(0.0, seconds);
                    }
                }
                break;
            case LayerSourceKind.Generator3D:
                if (layer.Generator != null && settingsMonitorInputFieldA != null)
                {
                    layer.Generator.PresetNameInput = string.IsNullOrWhiteSpace(settingsMonitorInputFieldA.text) ? "Preset" : settingsMonitorInputFieldA.text.Trim();
                    SaveGeneratorPreset(layer.Generator, layer);
                }
                break;
        }
    }

    private void ApplySettingsMonitorInputB()
    {
        var layerIndex = CurrentSettingsMonitorLayerIndex();
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.Text:
                if (settingsMonitorInputFieldB != null)
                {
                    int fontSize;
                    if (int.TryParse(settingsMonitorInputFieldB.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize))
                    {
                        layer.TextFontSize = Mathf.Clamp(fontSize, 12, 256);
                        layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                        UpdateTextLayerTexture(layer);
                    }
                }
                break;
            case LayerSourceKind.YouTube:
                if (layer.YouTube != null && settingsMonitorInputFieldB != null)
                {
                    float speed;
                    if (float.TryParse(settingsMonitorInputFieldB.text, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
                    {
                        layer.YouTube.PlaybackSpeed = Mathf.Clamp(speed, 0.05f, 4f);
                        layer.YouTube.SpeedInput = layer.YouTube.PlaybackSpeed.ToString("0.00", CultureInfo.InvariantCulture);
                        if (layer.Player != null)
                        {
                            layer.Player.playbackSpeed = layer.YouTube.PlaybackSpeed;
                        }
                    }
                }
                break;
        }
    }

    private void EnsureExternalWindow(ExternalWindowKind kind)
    {
        var process = kind == ExternalWindowKind.Preview ? previewExternalWindowProcess : settingsExternalWindowProcess;
        try
        {
            if (process != null && !process.HasExited)
            {
                return;
            }
        }
        catch
        {
        }

        var helper = FindOverlayWindowBridge();
        if (string.IsNullOrEmpty(helper))
        {
            outputStatus = "OverlayWindowBridge.exe not found.";
            return;
        }

        var dataPath = kind == ExternalWindowKind.Preview ? externalPreviewJsonPath : externalSettingsJsonPath;
        var modeArg = kind == ExternalWindowKind.Preview ? "preview" : "settings";
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = helper,
                Arguments = "--mode \"" + EscapeProcessArgument(modeArg) + "\" --data \"" + EscapeProcessArgument(dataPath) + "\" --command \"" +
                            EscapeProcessArgument(kind == ExternalWindowKind.Preview ? externalPreviewCommandPath : externalSettingsCommandPath) + "\"",
                WorkingDirectory = Path.GetDirectoryName(helper),
                UseShellExecute = false
            };
            var started = System.Diagnostics.Process.Start(info);
            if (kind == ExternalWindowKind.Preview)
            {
                previewExternalWindowProcess = started;
            }
            else
            {
                settingsExternalWindowProcess = started;
            }
        }
        catch (Exception ex)
        {
            outputStatus = "Failed to start overlay window: " + ex.Message;
        }
    }

    private static void CloseExternalWindow(ref System.Diagnostics.Process process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                process.WaitForExit(400);
            }
        }
        catch
        {
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
            process = null;
        }
    }

    private string FindOverlayWindowBridge()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "OverlayWindowBridge", "bin", "Release", "OverlayWindowBridge.exe")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "OverlayWindowBridge", "bin", "Release", "OverlayWindowBridge.exe"))
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

    private void ExportPreviewExternalWindowData()
    {
        Directory.CreateDirectory(externalWindowDataRoot);
        var monitorData = BuildCdjMonitorViewData();
        var data = new ExternalPreviewWindowData
        {
            Title = monitorData.Title,
            Status = monitorData.Status,
            FourDeck = monitorData.FourDeck
        };

        var playerCount = monitorData.FourDeck ? 4 : 2;
        for (var number = 1; number <= playerCount; number++)
        {
            var cardData = FindCdjMonitorDeckCard(monitorData, number);
            var player = cardData == null ? null : cardData.Player;
            var card = new ExternalPreviewPlayerCard
            {
                Number = number,
                Flags = cardData == null ? "NO LINK" : cardData.Flags,
                Device = cardData == null ? "No player connected" : cardData.Device,
                Title = cardData == null ? "No player connected" : cardData.Title,
                Artist = cardData == null ? "" : cardData.Artist,
                Comment = cardData == null ? "" : cardData.Comment,
                BpmLine = cardData == null ? "BPM --" : cardData.BpmLine,
                TimeLine = cardData == null ? "" : cardData.TimeLine,
                BeatLine = cardData == null ? "" : cardData.BeatLine
            };

            if (player != null)
            {
                var runtimeWavePath = Path.Combine(externalWindowDataRoot, "preview-wave-" + number.ToString(CultureInfo.InvariantCulture) + ".png");
                var overviewWavePath = Path.Combine(externalWindowDataRoot, "preview-overview-" + number.ToString(CultureInfo.InvariantCulture) + ".png");
                var albumArtPath = Path.Combine(externalWindowDataRoot, "preview-art-" + number.ToString(CultureInfo.InvariantCulture) + ".png");
                card.RuntimeWavePath = SaveSnapshotTextureToFile(player.BltWavePreview != null ? player.BltWavePreview : player.DirectWaveformTexture, runtimeWavePath, 640, 140) ? runtimeWavePath : null;
                card.OverviewWavePath = SaveSnapshotTextureToFile(player.BltWaveOverview != null ? player.BltWaveOverview : player.DirectWaveformTexture, overviewWavePath, 640, 52) ? overviewWavePath : null;
                card.AlbumArtPath = SaveSnapshotTextureToFile(player.AlbumArtTexture, albumArtPath, 128, 128) ? albumArtPath : null;
            }

            data.Players.Add(card);
        }

        WriteJsonAtomic(externalPreviewJsonPath, JsonUtility.ToJson(data, true));
    }

    private void ExportSettingsExternalWindowData()
    {
        Directory.CreateDirectory(externalWindowDataRoot);
        var model = BuildSettingsViewData();
        var data = new ExternalSettingsWindowData();
        data.LayerLabel = model.LayerLabel;
        data.Title = model.Title;
        data.Subtitle = model.Subtitle;
        data.Status = model.Status;
        data.Details = model.Details;
        data.SelectedLayerIndex = model.SelectedLayerIndex;
        data.Enabled = model.Enabled;
        data.AudioEnabled = model.AudioEnabled;
        data.Opacity = model.Opacity;
        data.BlendMode = model.BlendMode;
        if (vjLayers != null)
        {
            for (var i = 0; i < vjLayers.Length; i++)
            {
                var source = vjLayers[i];
                data.LayerOptions.Add(new ExternalSettingsWindowLayerOption
                {
                    Index = i,
                    Label = LayerSlotLabel(i),
                    Name = source == null ? "empty" : LayerName(source)
                });
            }
        }

        data.PreviewPath = SaveSnapshotTextureToFile(model.PreviewTexture, externalSettingsImagePath, 960, 540) ? externalSettingsImagePath : null;
        WriteJsonAtomic(externalSettingsJsonPath, JsonUtility.ToJson(data, true));
    }

    private SettingsViewData BuildSettingsViewData()
    {
        var targetLayerIndex = CurrentSettingsMonitorLayerIndex();
        if (cachedSettingsViewData != null &&
            cachedSettingsViewDataFrame == Time.frameCount &&
            cachedSettingsViewLayerIndex == targetLayerIndex)
        {
            return cachedSettingsViewData;
        }

        LayerState layer = null;
        if (vjLayers != null && targetLayerIndex >= 0 && targetLayerIndex < vjLayers.Length)
        {
            layer = vjLayers[targetLayerIndex];
        }

        cachedSettingsViewData = new SettingsViewData
        {
            SelectedLayerIndex = targetLayerIndex,
            LayerLabel = LayerSlotLabel(targetLayerIndex),
            Title = layer == null ? "No layer selected" : LayerName(layer),
            Subtitle = layer == null ? "" : layer.SourceKind.ToString(),
            Status = layer == null ? "" : PreferText(LayerDisplayStatus(layer), ""),
            Details = BuildExternalSettingsDetailLines(layer, targetLayerIndex),
            CommonDetails = BuildExternalSettingsCommonLines(layer, targetLayerIndex),
            EffectDetails = BuildExternalSettingsEffectLines(layer),
            SourceDetailTitle = BuildExternalSettingsSourceTitle(layer),
            SourceDetails = BuildExternalSettingsSourceLines(layer),
            Enabled = layer != null && layer.Enabled,
            AudioEnabled = layer != null && layer.AudioOutputEnabled,
            Opacity = layer == null ? 0f : Mathf.Clamp01(layer.Opacity),
            BlendMode = layer == null ? "alpha" : ExternalBlendModeId(layer.BlendMode),
            PreviewTexture = layer == null ? null : LayerPreviewTexture(targetLayerIndex, layer),
            PresetInfo = BuildSettingsPresetInfo(layer)
        };
        cachedSettingsViewDataFrame = Time.frameCount;
        cachedSettingsViewLayerIndex = targetLayerIndex;
        return cachedSettingsViewData;
    }

    private string BuildSettingsPresetInfo(LayerState layer)
    {
        if (layer == null || layer.SourceKind != LayerSourceKind.Generator3D || layer.Generator == null)
        {
            return string.Empty;
        }

        var presetCount = generatorPresets == null ? 0 : generatorPresets.Count;
        return "3D Presets\nSaved presets: " + presetCount.ToString(CultureInfo.InvariantCulture) +
               "\nCurrent: " + PreferText(layer.Generator.PresetNameInput, "(none)") +
               "\nFace: " + DescribeGeneratorFaceSource(layer.Generator);
    }

    private static string ExternalBlendModeId(LayerBlendMode mode)
    {
        switch (mode)
        {
            case LayerBlendMode.Add50:
                return "add50";
            case LayerBlendMode.Add:
                return "add";
            case LayerBlendMode.Mask:
                return "mask";
            case LayerBlendMode.Screen:
                return "screen";
            case LayerBlendMode.Multiply:
                return "multiply";
            case LayerBlendMode.Lighten:
                return "lighten";
            case LayerBlendMode.Darken:
                return "darken";
            case LayerBlendMode.Difference:
                return "difference";
            case LayerBlendMode.Overlay:
                return "overlay";
            case LayerBlendMode.Subtract:
                return "subtract";
            default:
                return "alpha";
        }
    }

    private void PollExternalWindowCommands()
    {
        PollExternalWindowCommandFile(externalSettingsCommandPath);
    }

    private void PollExternalWindowCommandFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var text = File.ReadAllText(path, Encoding.UTF8).Trim();
            File.Delete(path);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var parts = text.Split('\t');
            if (parts.Length == 0)
            {
                return;
            }

            if (string.Equals(parts[0], "select-layer", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
            {
                int index;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    vjLayers != null && index >= 0 && index < vjLayers.Length)
                {
                    selectedLayerIndex = index;
                }
            }
            else if (string.Equals(parts[0], "set-enabled", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                int index;
                int enabled;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out enabled) &&
                    vjLayers != null && index >= 0 && index < vjLayers.Length && vjLayers[index] != null)
                {
                    vjLayers[index].Enabled = enabled != 0;
                }
            }
            else if (string.Equals(parts[0], "set-audio", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                int index;
                int enabled;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out enabled) &&
                    vjLayers != null && index >= 0 && index < vjLayers.Length && vjLayers[index] != null)
                {
                    vjLayers[index].AudioOutputEnabled = enabled != 0;
                    ApplyLayerAudioOutputState(vjLayers[index]);
                }
            }
            else if (string.Equals(parts[0], "set-opacity", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                int index;
                float opacity;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out opacity) &&
                    vjLayers != null && index >= 0 && index < vjLayers.Length && vjLayers[index] != null)
                {
                    vjLayers[index].Opacity = Mathf.Clamp01(opacity);
                }
            }
            else if (string.Equals(parts[0], "set-blend", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                int index;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    vjLayers != null && index >= 0 && index < vjLayers.Length && vjLayers[index] != null)
                {
                    var value = parts[2];
                    if (string.Equals(value, "add50", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Add50;
                    }
                    else if (string.Equals(value, "add100", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "add", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Add;
                    }
                    else if (string.Equals(value, "screen", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Screen;
                    }
                    else if (string.Equals(value, "multiply", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Multiply;
                    }
                    else if (string.Equals(value, "lighten", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Lighten;
                    }
                    else if (string.Equals(value, "darken", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Darken;
                    }
                    else if (string.Equals(value, "difference", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Difference;
                    }
                    else if (string.Equals(value, "overlay", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Overlay;
                    }
                    else if (string.Equals(value, "subtract", StringComparison.OrdinalIgnoreCase))
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Subtract;
                    }
                    else
                    {
                        vjLayers[index].BlendMode = LayerBlendMode.Alpha;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private string[] BuildExternalSettingsDetailLines(LayerState layer, int layerIndex)
    {
        if (layer == null)
        {
            return new string[0];
        }

        var lines = new List<string>();
        lines.Add("Source: " + layer.SourceKind);
        lines.Add("Name: " + LayerName(layer));
        lines.Add("Resolution: " + DescribeLayerSourceResolution(layer));
        if (!string.IsNullOrEmpty(layer.DetectedVideoCodec))
        {
            lines.Add("Codec: " + layer.DetectedVideoCodec);
        }
        if (!string.IsNullOrEmpty(layer.Path))
        {
            lines.Add("Path: " + layer.Path);
        }
        lines.Add("");
        lines.Add("Layer Blend");
        lines.Add("Enabled: " + (layer.Enabled ? "ON" : "OFF"));
        lines.Add("Blend: " + layer.BlendMode);
        lines.Add("Scale: x" + Mathf.Clamp(layer.Scale, LayerScaleMin, LayerScaleMax).ToString("0.00", CultureInfo.InvariantCulture));
        lines.Add("Scale X: x" + Mathf.Clamp(layer.ScaleX, LayerScaleMin, LayerScaleMax).ToString("0.00", CultureInfo.InvariantCulture));
        lines.Add("Scale Y: x" + Mathf.Clamp(layer.ScaleY, LayerScaleMin, LayerScaleMax).ToString("0.00", CultureInfo.InvariantCulture));
        lines.Add("Scale Input: " + DescribeLayerInputMode(layer.ScaleInputMode, layer.ScaleFftAmount));
        lines.Add("Opacity Input: " + DescribeLayerInputMode(layer.OpacityInputMode, layer.OpacityFftAmount));
        lines.Add("Scale X Input: " + DescribeLayerInputMode(layer.ScaleXInputMode, layer.ScaleXFftAmount));
        lines.Add("Scale Y Input: " + DescribeLayerInputMode(layer.ScaleYInputMode, layer.ScaleYFftAmount));
        if (IsSourceLayerSlot(layerIndex))
        {
            var supportsVideoAudio = layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube;
            lines.Add("Audio: " + (supportsVideoAudio ? (layer.AudioOutputEnabled ? "Output ON" : "Output OFF") : layer.SourceKind == LayerSourceKind.Capture ? "Capture Audio from Setting" : "No audio control"));
            lines.Add("Hue: " + Mathf.RoundToInt(Mathf.Repeat(layer.HueShift, 1f) * 360f).ToString(CultureInfo.InvariantCulture) + "°");
            lines.Add("Invert: " + Mathf.RoundToInt(Mathf.Clamp01(layer.InvertAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            lines.Add("B/W: " + Mathf.RoundToInt(Mathf.Clamp01(layer.MonochromeAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            lines.Add("Color: " + LayerColorModeLabel(layer.ColorMode));
            lines.Add("PGM Solo: " + (programSoloLayerIndex == layerIndex ? "ON" : "OFF"));
            lines.Add("PGM Mute: " + (layer.ProgramMuted ? "ON" : "OFF"));
        }
        lines.Add("Opacity: " + Mathf.RoundToInt(Mathf.Clamp01(layer.Opacity) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
        if (layer.Effect != null && layer.Effect.HasEffect)
        {
            lines.Add("");
            lines.Add("Effect");
            lines.Add("Effect: " + layer.Effect.Kind);
        }
        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                lines.Add("");
                lines.Add("Video Detail");
                lines.Add("Playback Mode: " + layer.VideoMode);
                lines.Add("Base BPM: " + layer.VideoBaseBpm.ToString("0.#", CultureInfo.InvariantCulture));
                lines.Add("Sync BPM: " + CurrentVisualBpm().ToString("0.0", CultureInfo.InvariantCulture));
                lines.Add("Clip Time: " + LayerPlaybackTimeSeconds(layer).ToString("0.00", CultureInfo.InvariantCulture) +
                          " / " +
                          (LayerPlaybackLengthSeconds(layer) > 0.001
                              ? LayerPlaybackLengthSeconds(layer).ToString("0.00", CultureInfo.InvariantCulture) + "s"
                              : "--"));
                lines.Add("Speed: x" + (layer.VideoMode == VideoPlaybackMode.Bpm
                    ? Mathf.Clamp(CurrentVisualBpm() / Mathf.Max(1f, layer.VideoBaseBpm), 0.05f, 16f).ToString("0.00", CultureInfo.InvariantCulture)
                    : "1.00"));
                break;
            case LayerSourceKind.Text:
                lines.Add("");
                lines.Add("Text Detail");
                lines.Add("Text: " + (string.IsNullOrEmpty(layer.TextContent) ? "TEXT" : layer.TextContent));
                lines.Add("Font Size: " + layer.TextFontSize.ToString(CultureInfo.InvariantCulture));
                lines.Add("Font: " + PreferText(layer.TextFontName, "Built-in"));
                break;
            case LayerSourceKind.YouTube:
                if (layer.YouTube != null)
                {
                    lines.Add("");
                    lines.Add("YouTube Detail");
                    lines.Add("Title: " + PreferText(layer.YouTube.Title, layer.YouTube.VideoId));
                    if (!string.IsNullOrEmpty(layer.YouTube.Author))
                    {
                        lines.Add("Author: " + layer.YouTube.Author);
                    }
                    lines.Add("Mode: " + YoutubePlaybackModeLabel(layer.YouTube.PlaybackMode));
                    lines.Add("Speed: x" + layer.YouTube.PlaybackSpeed.ToString("0.00", CultureInfo.InvariantCulture));
                    lines.Add("Waveform Player: P" + layer.YouTube.WaveformPlayerNumber.ToString(CultureInfo.InvariantCulture));
                    if (layer.YouTube.Resolving)
                    {
                        lines.Add("Resolving stream URL...");
                    }
                    if (!string.IsNullOrEmpty(layer.YouTube.Error))
                    {
                        lines.Add("Error: " + layer.YouTube.Error);
                    }
                }
                break;
            case LayerSourceKind.Generator3D:
                if (layer.Generator != null)
                {
                    lines.Add("");
                    lines.Add("3D Object Detail");
                    lines.Add("Shape: " + layer.Generator.Shape);
                    lines.Add("Scene Mode: " + layer.Generator.PresentationMode);
                    lines.Add("Object Motion: " + layer.Generator.ObjectOrbitMode);
                    lines.Add("Camera Work: " + layer.Generator.CameraWorkMode);
                    lines.Add("Self Spin: " + (layer.Generator.ObjectSpinEnabled ? "ON" : "OFF"));
                    lines.Add("BPM Size Pulse: " + (layer.Generator.ObjectBpmPulseEnabled ? "ON" : "OFF"));
                    lines.Add("Background: " + (layer.Generator.TransparentBackground ? "Transparent" : "Black"));
                    lines.Add("Screens: " + (layer.Generator.SurroundScreensEnabled ? "ON" : "OFF"));
                    lines.Add("Particles: " + (layer.Generator.ParticlesEnabled ? "ON" : "OFF"));
                    if (layer.Generator.PresentationMode == GeneratorPresentationMode.Lighting)
                    {
                        lines.Add("Moving Light Mode: " + layer.Generator.LightingRigMode);
                    }
                }
                break;
        }
        return lines.ToArray();
    }

    private string[] BuildExternalSettingsCommonLines(LayerState layer, int layerIndex)
    {
        if (layer == null)
        {
            return new string[0];
        }

        var lines = new List<string>();
        lines.Add("Enabled: " + (layer.Enabled ? "ON" : "OFF"));
        lines.Add("Blend: " + layer.BlendMode);
        if (IsSourceLayerSlot(layerIndex))
        {
            var supportsVideoAudio = layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube;
            lines.Add("Audio: " + (supportsVideoAudio ? (layer.AudioOutputEnabled ? "Output ON" : "Output OFF") : layer.SourceKind == LayerSourceKind.Capture ? "Capture Audio from Setting" : "No audio control"));
            lines.Add("Hue: " + Mathf.RoundToInt(Mathf.Repeat(layer.HueShift, 1f) * 360f).ToString(CultureInfo.InvariantCulture) + " deg");
            lines.Add("Invert: " + Mathf.RoundToInt(Mathf.Clamp01(layer.InvertAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            lines.Add("B/W: " + Mathf.RoundToInt(Mathf.Clamp01(layer.MonochromeAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            lines.Add("Color: " + LayerColorModeLabel(layer.ColorMode));
        }
        lines.Add("Opacity: " + Mathf.RoundToInt(Mathf.Clamp01(layer.Opacity) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
        return lines.ToArray();
    }

    private string[] BuildExternalSettingsEffectLines(LayerState layer)
    {
        if (layer == null || layer.Effect == null || !layer.Effect.HasEffect)
        {
            return new[] { "No effect attached." };
        }

        var lines = new List<string>();
        lines.Add("Effect: " + layer.Effect.Kind);
        lines.Add("Enabled: " + (layer.Effect.Enabled ? "ON" : "OFF"));
        switch (layer.Effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                lines.Add("Mode: " + layer.Effect.RgbMode);
                break;
            case LayerEffectKind.Blur:
            case LayerEffectKind.Mirror:
            case LayerEffectKind.Strobe:
                lines.Add("Mode: " + layer.Effect.Mode);
                break;
            case LayerEffectKind.StepSequencer:
                lines.Add("Queue: " + (layer.Effect.StepSequenceQueue == null ? 0 : layer.Effect.StepSequenceQueue.Count).ToString(CultureInfo.InvariantCulture));
                if (layer.Effect.StepSequenceQueue != null)
                {
                    for (var i = 0; i < Mathf.Min(4, layer.Effect.StepSequenceQueue.Count); i++)
                    {
                        lines.Add("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + DescribeStepSequencerEntry(layer.Effect.StepSequenceQueue[i]));
                    }
                }
                break;
        }
        if (layer.Effect.Kind == LayerEffectKind.Blur ||
            layer.Effect.Kind == LayerEffectKind.Glitch ||
            layer.Effect.Kind == LayerEffectKind.RgbEffect ||
            layer.Effect.Kind == LayerEffectKind.Kaleido ||
            layer.Effect.Kind == LayerEffectKind.Pixelate)
        {
            lines.Add("Amount: " + Mathf.RoundToInt(layer.Effect.Intensity * 100f).ToString(CultureInfo.InvariantCulture));
        }
        if (midiEditMode)
        {
            lines.Add("MIDI: Learn mode active");
            if (midiLearningAction != MidiBindingAction.None && midiLearningIndex == selectedLayerIndex)
            {
                lines.Add("Learn: " + midiLearningAction.ToString());
            }
        }
        return lines.ToArray();
    }

    private string BuildExternalSettingsSourceTitle(LayerState layer)
    {
        if (layer == null)
        {
            return "Source Detail";
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                return "Video Detail";
            case LayerSourceKind.Text:
                return "Text Detail";
            case LayerSourceKind.YouTube:
                return "YouTube Detail";
            case LayerSourceKind.Generator3D:
                return "3D Object Detail";
            default:
                return "Source Detail";
        }
    }

    private string[] BuildExternalSettingsSourceLines(LayerState layer)
    {
        if (layer == null)
        {
            return new string[0];
        }

        var lines = new List<string>();
        lines.Add("Source: " + layer.SourceKind);
        lines.Add("Name: " + LayerName(layer));
        lines.Add("Resolution: " + DescribeLayerSourceResolution(layer));
        if (!string.IsNullOrEmpty(layer.DetectedVideoCodec))
        {
            lines.Add("Codec: " + layer.DetectedVideoCodec);
        }
        if (!string.IsNullOrEmpty(layer.Path))
        {
            lines.Add("Path: " + layer.Path);
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                lines.Add("Playback Mode: " + layer.VideoMode);
                lines.Add("Base BPM: " + layer.VideoBaseBpm.ToString("0.#", CultureInfo.InvariantCulture));
                lines.Add("Sync BPM: " + CurrentVisualBpm().ToString("0.0", CultureInfo.InvariantCulture));
                lines.Add("Clip Time: " + LayerPlaybackTimeSeconds(layer).ToString("0.00", CultureInfo.InvariantCulture) +
                          " / " +
                          (LayerPlaybackLengthSeconds(layer) > 0.001
                              ? LayerPlaybackLengthSeconds(layer).ToString("0.00", CultureInfo.InvariantCulture) + "s"
                              : "--"));
                lines.Add("Speed: x" + (layer.VideoMode == VideoPlaybackMode.Bpm
                    ? Mathf.Clamp(CurrentVisualBpm() / Mathf.Max(1f, layer.VideoBaseBpm), 0.05f, 16f).ToString("0.00", CultureInfo.InvariantCulture)
                    : "1.00"));
                break;
            case LayerSourceKind.Text:
                lines.Add("Text: " + (string.IsNullOrEmpty(layer.TextContent) ? "TEXT" : layer.TextContent));
                lines.Add("Font Size: " + layer.TextFontSize.ToString(CultureInfo.InvariantCulture));
                lines.Add("Font: " + PreferText(layer.TextFontName, "Built-in"));
                break;
            case LayerSourceKind.YouTube:
                if (layer.YouTube != null)
                {
                    lines.Add("Title: " + PreferText(layer.YouTube.Title, layer.YouTube.VideoId));
                    if (!string.IsNullOrEmpty(layer.YouTube.Author))
                    {
                        lines.Add("Author: " + layer.YouTube.Author);
                    }
                    lines.Add("Mode: " + YoutubePlaybackModeLabel(layer.YouTube.PlaybackMode));
                    lines.Add("Speed: x" + layer.YouTube.PlaybackSpeed.ToString("0.00", CultureInfo.InvariantCulture));
                    lines.Add("Waveform Player: P" + layer.YouTube.WaveformPlayerNumber.ToString(CultureInfo.InvariantCulture));
                    if (!string.IsNullOrEmpty(layer.YouTube.AlignmentStatus))
                    {
                        lines.Add("Align: " + layer.YouTube.AlignmentStatus);
                    }
                    if (layer.YouTube.Resolving)
                    {
                        lines.Add("Resolving stream URL...");
                    }
                    if (!string.IsNullOrEmpty(layer.YouTube.Error))
                    {
                        lines.Add("Error: " + layer.YouTube.Error);
                    }
                }
                break;
            case LayerSourceKind.Generator3D:
                if (layer.Generator != null)
                {
                    lines.Add("Shape: " + layer.Generator.Shape);
                    lines.Add("Scene Mode: " + layer.Generator.PresentationMode);
                    lines.Add("Object Motion: " + layer.Generator.ObjectOrbitMode);
                    lines.Add("Camera Work: " + layer.Generator.CameraWorkMode);
                    lines.Add("Self Spin: " + (layer.Generator.ObjectSpinEnabled ? "ON" : "OFF"));
                    lines.Add("BPM Size Pulse: " + (layer.Generator.ObjectBpmPulseEnabled ? "ON" : "OFF"));
                    lines.Add("Background: " + (layer.Generator.TransparentBackground ? "Transparent" : "Black"));
                    lines.Add("Skybox: " + PreferText(layer.Generator.SkyboxName, "None"));
                    lines.Add("Model: " + PreferText(layer.Generator.ModelName, "None"));
                    lines.Add("Model Pos: " + PreferText(layer.Generator.ModelPositionInput, "0,0,0"));
                    lines.Add("Model Rot: " + PreferText(layer.Generator.ModelRotationInput, "0,0,0"));
                    lines.Add("Model Scale: " + PreferText(layer.Generator.ModelScaleInput, "1,1,1"));
                    lines.Add("Face Folder: " + PreferText(layer.Generator.FaceImageFolderPath, "(none)"));
                    lines.Add("Face Source: " + DescribeGeneratorFaceTextureSource(layer.Generator));
                    lines.Add("Screens: " + (layer.Generator.SurroundScreensEnabled ? "ON" : "OFF"));
                    lines.Add("Screen Source: " + (layer.Generator.SurroundScreenSourceLayerIndex >= 0 ? LayerSlotLabel(layer.Generator.SurroundScreenSourceLayerIndex) : "None"));
                    lines.Add("Particles: " + (layer.Generator.ParticlesEnabled ? "ON" : "OFF"));
                    if (layer.Generator.PresentationMode == GeneratorPresentationMode.Lighting)
                    {
                        lines.Add("Moving Light Mode: " + layer.Generator.LightingRigMode);
                    }
                }
                break;
        }

        return lines.ToArray();
    }

    private string DescribeGeneratorFaceSource(GeneratorState generator)
    {
        if (generator == null)
        {
            return "(none)";
        }

        if (generator.TextureSourceLayerIndex >= 0)
        {
            return "Layer " + LayerSlotLabel(generator.TextureSourceLayerIndex);
        }

        switch (generator.SavedTextureSourceKind)
        {
            case GeneratorSavedTextureSourceKind.ImageFile:
                return string.IsNullOrEmpty(generator.FaceTextureImagePath) ? "Image" : Path.GetFileName(generator.FaceTextureImagePath);
            case GeneratorSavedTextureSourceKind.BltJacket:
                return "BLT Jacket P" + generator.FaceTextureBltPlayerNumber.ToString(CultureInfo.InvariantCulture);
            default:
                return generator.CurrentTexture == null ? "(none)" : "Manual";
        }
    }

    private string DescribeStepSequencerEntry(StepSequencerEntry entry)
    {
        if (entry == null)
        {
            return "(null)";
        }

        switch (entry.Kind)
        {
            case StepSequencerEntryKind.Layer:
                return "Layer " + LayerSlotLabel(entry.LayerIndex);
            case StepSequencerEntryKind.MediaFile:
                return Path.GetFileName(entry.Path);
            default:
                return entry.Kind.ToString();
        }
    }

    private string DescribeGeneratorFaceTextureSource(GeneratorState generator)
    {
        if (generator == null)
        {
            return "None";
        }

        if (generator.TextureSourceLayerIndex >= 0)
        {
            return LayerSlotLabel(generator.TextureSourceLayerIndex);
        }

        switch (generator.SavedTextureSourceKind)
        {
            case GeneratorSavedTextureSourceKind.ImageFile:
                return string.IsNullOrEmpty(generator.FaceTextureImagePath) ? "Image" : Path.GetFileName(generator.FaceTextureImagePath);
            case GeneratorSavedTextureSourceKind.BltJacket:
                return generator.FaceTextureBltPlayerNumber > 0
                    ? "BLT Jacket P" + generator.FaceTextureBltPlayerNumber.ToString(CultureInfo.InvariantCulture)
                    : "BLT Jacket";
            default:
                return "Manual";
        }
    }

    private bool SaveSnapshotTextureToFile(Texture source, string path, int width, int height)
    {
        if (source == null || string.IsNullOrEmpty(path))
        {
            return false;
        }

        var snapshot = CaptureTextureSnapshot(source, width, height);
        if (snapshot == null)
        {
            return false;
        }

        try
        {
            SaveTexturePngAtomic(path, snapshot);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Destroy(snapshot);
        }
    }

    private static void SaveTexturePngAtomic(string path, Texture2D texture)
    {
        if (texture == null || string.IsNullOrEmpty(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, texture.EncodeToPNG());
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tempPath, path);
    }

    private static void WriteJsonAtomic(string path, string json)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json ?? "{}", new UTF8Encoding(false));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tempPath, path);
    }

    private void LoadBltBridgePreference()
    {
        if (PlayerPrefs.HasKey(DjLinkInputModePrefKey))
        {
            pendingDjLinkInputMode = (DjLinkInputMode)Mathf.Clamp(PlayerPrefs.GetInt(DjLinkInputModePrefKey, 0), 0, 2);
            useBltBridgeMode = pendingDjLinkInputMode == DjLinkInputMode.ExternalBlt;
            useSimulatedDjLinkMode = pendingDjLinkInputMode == DjLinkInputMode.Simulator;
        }
        else if (PlayerPrefs.HasKey(BltBridgeModePrefKey))
        {
            useBltBridgeMode = PlayerPrefs.GetInt(BltBridgeModePrefKey, useBltBridgeMode ? 1 : 0) != 0;
            useSimulatedDjLinkMode = false;
            pendingDjLinkInputMode = useBltBridgeMode ? DjLinkInputMode.ExternalBlt : DjLinkInputMode.InternalDirect;
        }

        if (PlayerPrefs.HasKey(BltParamsUrlPrefKey))
        {
            bltParamsUrl = PlayerPrefs.GetString(BltParamsUrlPrefKey, DefaultBltParamsUrl);
        }

        simulatedDjLinkDeckCount = Mathf.Clamp(PlayerPrefs.GetInt(DjLinkSimulatorDeckCountPrefKey, simulatedDjLinkDeckCount), 1, 4);
        simulatedDjLinkBpm = Mathf.Clamp(PlayerPrefs.GetFloat(DjLinkSimulatorBpmPrefKey, simulatedDjLinkBpm), 40f, 240f);
        simulatedDjLinkPlaying = PlayerPrefs.GetInt(DjLinkSimulatorPlayingPrefKey, simulatedDjLinkPlaying ? 1 : 0) != 0;

        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            bltParamsUrl = DefaultBltParamsUrl;
        }

        pendingBltBridgeMode = pendingDjLinkInputMode == DjLinkInputMode.ExternalBlt;
    }

    private void LoadDisplayRoutingPreferences()
    {
        cdjWaveDisplayIndex = Mathf.Max(0, PlayerPrefs.GetInt(CdjWaveDisplayPrefKey, 0));
        layerSettingsDisplayIndex = Mathf.Max(0, PlayerPrefs.GetInt(LayerSettingsDisplayPrefKey, 0));
        effectSettingsDisplayIndex = Mathf.Max(0, PlayerPrefs.GetInt(EffectSettingsDisplayPrefKey, 0));
        secondDisplayUiEnabled = HasAuxDisplayRoute();
    }

    private void SaveDisplayRoutingPreferences()
    {
        PlayerPrefs.SetInt(CdjWaveDisplayPrefKey, Mathf.Max(0, cdjWaveDisplayIndex));
        PlayerPrefs.SetInt(LayerSettingsDisplayPrefKey, Mathf.Max(0, layerSettingsDisplayIndex));
        PlayerPrefs.SetInt(EffectSettingsDisplayPrefKey, Mathf.Max(0, effectSettingsDisplayIndex));
        secondDisplayUiEnabled = HasAuxDisplayRoute();
        PlayerPrefs.SetInt(SecondDisplayUiPrefKey, secondDisplayUiEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private bool HasAuxDisplayRoute()
    {
        return cdjWaveDisplayIndex > 0 || layerSettingsDisplayIndex > 0 || effectSettingsDisplayIndex > 0;
    }

    private int RouteDisplayIndexForMode(SecondDisplayUiMode mode)
    {
        var index = 0;
        switch (mode)
        {
            case SecondDisplayUiMode.Preview:
                index = cdjWaveDisplayIndex;
                break;
            case SecondDisplayUiMode.Settings:
                index = layerSettingsDisplayIndex;
                break;
            case SecondDisplayUiMode.EffectSettings:
                index = effectSettingsDisplayIndex;
                break;
        }

        if (index <= 0)
        {
            return -1;
        }
        if (Display.displays == null || index >= Display.displays.Length)
        {
            return -1;
        }
        return index;
    }

    private bool IsDisplayReservedForAuxUi(int displayIndex)
    {
        if (displayIndex <= 0)
        {
            return false;
        }
        return cdjWaveDisplayIndex == displayIndex ||
               layerSettingsDisplayIndex == displayIndex ||
               effectSettingsDisplayIndex == displayIndex;
    }

    private bool IsSettingsMonitorDisplayMode()
    {
        return secondDisplayUiMode == SecondDisplayUiMode.Settings || secondDisplayUiMode == SecondDisplayUiMode.EffectSettings;
    }

    private void LoadScreenShortcutPreferences()
    {
        mainLayerShortcutKey = LoadShortcutKey(MainShortcutPrefKey, KeyCode.R);
        cdjWaveShortcutKey = LoadShortcutKey(CdjWaveShortcutPrefKey, KeyCode.Space);
        layerSettingsShortcutKey = LoadShortcutKey(LayerSettingsShortcutPrefKey, KeyCode.E);
        effectSettingsShortcutKey = LoadShortcutKey(EffectSettingsShortcutPrefKey, KeyCode.E);
        mainLayerShortcutInput = KeyCodeToShortcutText(mainLayerShortcutKey);
        cdjWaveShortcutInput = KeyCodeToShortcutText(cdjWaveShortcutKey);
        layerSettingsShortcutInput = KeyCodeToShortcutText(layerSettingsShortcutKey);
        effectSettingsShortcutInput = KeyCodeToShortcutText(effectSettingsShortcutKey);
    }

    private static KeyCode LoadShortcutKey(string prefKey, KeyCode fallback)
    {
        return ParseShortcutKey(PlayerPrefs.GetString(prefKey, KeyCodeToShortcutText(fallback)), fallback);
    }

    private void SaveScreenShortcutPreferences()
    {
        PlayerPrefs.SetString(MainShortcutPrefKey, KeyCodeToShortcutText(mainLayerShortcutKey));
        PlayerPrefs.SetString(CdjWaveShortcutPrefKey, KeyCodeToShortcutText(cdjWaveShortcutKey));
        PlayerPrefs.SetString(LayerSettingsShortcutPrefKey, KeyCodeToShortcutText(layerSettingsShortcutKey));
        PlayerPrefs.SetString(EffectSettingsShortcutPrefKey, KeyCodeToShortcutText(effectSettingsShortcutKey));
        PlayerPrefs.Save();
    }

    private static KeyCode ParseShortcutKey(string text, KeyCode fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var normalized = text.Trim();
        if (string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase))
        {
            return KeyCode.None;
        }
        if (string.Equals(normalized, "Esc", StringComparison.OrdinalIgnoreCase))
        {
            return KeyCode.Escape;
        }
        if (string.Equals(normalized, "Spacebar", StringComparison.OrdinalIgnoreCase))
        {
            return KeyCode.Space;
        }

        KeyCode parsed;
        return Enum.TryParse(normalized, true, out parsed) ? parsed : fallback;
    }

    private static string KeyCodeToShortcutText(KeyCode key)
    {
        return key == KeyCode.None ? "None" : key.ToString();
    }

    private void SaveBltBridgePreference()
    {
        PlayerPrefs.SetInt(BltBridgeModePrefKey, useBltBridgeMode ? 1 : 0);
        PlayerPrefs.SetString(BltParamsUrlPrefKey, string.IsNullOrEmpty(bltParamsUrl) ? DefaultBltParamsUrl : bltParamsUrl);
        PlayerPrefs.SetInt(DjLinkInputModePrefKey, (int)CurrentDjLinkInputMode());
        PlayerPrefs.SetInt(DjLinkSimulatorDeckCountPrefKey, Mathf.Clamp(simulatedDjLinkDeckCount, 1, 4));
        PlayerPrefs.SetFloat(DjLinkSimulatorBpmPrefKey, Mathf.Clamp(simulatedDjLinkBpm, 40f, 240f));
        PlayerPrefs.SetInt(DjLinkSimulatorPlayingPrefKey, simulatedDjLinkPlaying ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyBltBridgeModeSelection()
    {
        var nextUrl = string.IsNullOrWhiteSpace(pendingBltParamsUrl) ? DefaultBltParamsUrl : pendingBltParamsUrl.Trim();
        var currentMode = CurrentDjLinkInputMode();
        var modeChanged = currentMode != pendingDjLinkInputMode;
        var urlChanged = !string.Equals(bltParamsUrl, nextUrl, StringComparison.OrdinalIgnoreCase);
        var nextMode = pendingDjLinkInputMode;
        var nextUsesExternalBlt = nextMode == DjLinkInputMode.ExternalBlt;
        var nextUsesSimulator = nextMode == DjLinkInputMode.Simulator;

        if (nextMode == DjLinkInputMode.InternalDirect)
        {
            var conflictReason = GetDirectDjLinkConflictReason();
            if (!string.IsNullOrEmpty(conflictReason))
            {
                lastError = conflictReason;
                outputStatus = conflictReason;
                pendingDjLinkInputMode = currentMode;
                pendingBltBridgeMode = currentMode == DjLinkInputMode.ExternalBlt;
                pendingBltParamsUrl = bltParamsUrl;
                return;
            }
        }

        bltParamsUrl = nextUrl;
        useBltBridgeMode = nextUsesExternalBlt;
        useSimulatedDjLinkMode = nextUsesSimulator;
        pendingBltBridgeMode = nextUsesExternalBlt;
        SaveBltBridgePreference();

        bltError = null;
        bltReceived = false;
        bltServerReachable = false;
        bltActivePlayerCount = 0;
        lastBltUpdateUtc = DateTime.MinValue;
        lastBltServerSeenUtc = DateTime.MinValue;
        lastOfficialBltStartAttemptUtc = DateTime.MinValue;
        ResetPlayerMetadataCache();
        ResetSimulatedDjLinkPlayers();

        if (useSimulatedDjLinkMode)
        {
            StopListeners();
            simulatedDjLinkTrackStartTime = Time.realtimeSinceStartup;
            TriggerSimulatedDjLinkMetadataSend();
            outputStatus = modeChanged
                ? "Metadata mode: Simulator"
                : "Metadata mode unchanged.";
        }
        else if (useBltBridgeMode)
        {
            StopListeners();
            EnsureBltPollingState();
            outputStatus = modeChanged || urlChanged
                ? "Metadata mode: External BLT (" + bltParamsUrl + "). Start Beat Link Trigger manually if needed."
                : "Metadata mode unchanged.";
        }
        else
        {
            StartListeners();
            outputStatus = modeChanged || urlChanged
                ? "Metadata mode: Internal direct DJ Link"
                : "Metadata mode unchanged.";
        }
    }

    private void OpenBltScreen()
    {
        try
        {
            if (!useBltBridgeMode)
            {
                outputStatus = "Open BLT Screen is available only in External BLT mode.";
                return;
            }
            if (!string.IsNullOrEmpty(bltError))
            {
                outputStatus = "BLT error: " + bltError;
                return;
            }
            if (!bltReceived)
            {
                outputStatus = "BLT is not receiving Pro DJ Link data.";
                return;
            }

            var baseUrl = BltBaseUrl();
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }
            var launchUrl = baseUrl;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = launchUrl,
                        UseShellExecute = true
                    }))
                    {
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Open BLT screen failed: " + ex.Message);
                }
            });
            outputStatus = "Opening BLT screen...";
        }
        catch (Exception ex)
        {
            outputStatus = "Failed to open BLT screen: " + ex.Message;
        }
    }

    private void ResetPlayerMetadataCache()
    {
        lock (stateLock)
        {
            foreach (var player in players.Values)
            {
                player.BltSeen = false;
                player.LastBltUpdateUtc = DateTime.MinValue;
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
            }
        }
    }

}
