using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Video;

public sealed partial class BeatLinkOverlayClient : MonoBehaviour
{
    private const string VideoOutputPrefKey = "BeatLinkOverlay.VideoOutput";
    private const string AudioOutputPrefKey = "BeatLinkOverlay.AudioOutput";
    private const int LayerAutomationSampleCount = 9;

    private enum LayerAutomationTarget
    {
        None,
        Opacity,
        Scale,
        ScaleX,
        ScaleY,
        Hue,
        Invert,
        Monochrome
    }

    private enum LayerInputDisplayStyle
    {
        Percent,
        Multiplier,
        Degrees,
        Scalar
    }

    private int layerInputModePopupLayerIndex = -1;
    private LayerAutomationTarget layerInputModePopupTarget = LayerAutomationTarget.None;
    private Rect layerInputModePopupRect;
    private Rect layerInputModePopupButtonRect;
    private bool layerInputModePopupAnchorValid;
    private int layerAutomationDragLayerIndex = -1;
    private LayerAutomationTarget layerAutomationDragTarget = LayerAutomationTarget.None;
    private int layerAutomationDragSampleIndex = -1;
    private bool layerAutomationDragOutgoingHandle;
    private int layerAutomationSelectedLayerIndex = -1;
    private LayerAutomationTarget layerAutomationSelectedTarget = LayerAutomationTarget.None;
    private int layerAutomationSelectedSegmentIndex;
    private int layerAutomationSelectedNodeIndex;
    private int layerEnvelopeRangeDragLayerIndex = -1;
    private LayerAutomationTarget layerEnvelopeRangeDragTarget = LayerAutomationTarget.None;
    private bool layerEnvelopeRangeDragUpperHandle;

    private void InitializeOutputResolutionOptions()
    {
        outputResolutionOptions.Clear();

        AddOutputResolutionOption(1280, 720);
        AddOutputResolutionOption(1600, 900);
        AddOutputResolutionOption(1920, 1080);
        AddOutputResolutionOption(2560, 1440);
        AddOutputResolutionOption(3840, 2160);

        if (FindOutputOptionIndex(outputResolutionOptions, ResolutionOptionId(outputWidth, outputHeight)) < 0)
        {
            AddOutputResolutionOption(outputWidth, outputHeight);
        }
    }

    private void LoadOutputResolutionPreference()
    {
        outputWidth = Mathf.Max(320, PlayerPrefs.GetInt(OutputWidthPrefKey, outputWidth));
        outputHeight = Mathf.Max(180, PlayerPrefs.GetInt(OutputHeightPrefKey, outputHeight));

        if (FindOutputOptionIndex(outputResolutionOptions, ResolutionOptionId(outputWidth, outputHeight)) < 0)
        {
            AddOutputResolutionOption(outputWidth, outputHeight);
        }

        selectedOutputResolutionIndex = Mathf.Max(0, FindOutputOptionIndex(outputResolutionOptions, ResolutionOptionId(outputWidth, outputHeight)));
    }

    private void LoadMidiConfiguration()
    {
        midiBindings.Clear();
        if (string.IsNullOrEmpty(midiConfigPath) || !File.Exists(midiConfigPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(midiConfigPath);
            var config = JsonUtility.FromJson<MidiBindingConfig>(json);
            if (config != null && config.Bindings != null)
            {
                midiBindings.AddRange(config.Bindings);
            }

            if (!string.IsNullOrEmpty(config == null ? null : config.SelectedInputId))
            {
                RefreshMidiInputDevices();
                for (var i = 0; i < midiInputDevices.Count; i++)
                {
                    if (string.Equals(midiInputDevices[i].Id, config.SelectedInputId, StringComparison.Ordinal))
                    {
                        selectedMidiInputIndex = i;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            midiStatus = "MIDI load failed: " + ex.Message;
        }
    }

    private void ValidateProgramOutputDisplayAvailability()
    {
        if (activeProgramDisplay < 0)
        {
            return;
        }

        if (IsDisplayReservedForAuxUi(activeProgramDisplay))
        {
            DisableProgramOutput("Program output disabled because the display is reserved for routed UI.");
            return;
        }

        if (Display.displays == null || activeProgramDisplay >= Display.displays.Length)
        {
            DisableProgramOutput("Program output display is no longer available.");
        }
    }

    private void UpdateProgramOutputLayers()
    {
        if (programOutputRoot == null || vjLayers == null)
        {
            return;
        }

        EnsureCompositeRenderTextures();
        UpdateEffectLayers();

        var current = mainSourceCompositeTexture;
        Graphics.Blit(Texture2D.blackTexture, current);
        var mainComposite = BuildMainLayerCompositeRange(0, vjLayers.Length);
        if (mainComposite != null)
        {
            Graphics.Blit(mainComposite, current);
            ReleaseTemporaryComposite(mainComposite);
        }

        var overlayScratch = compositeScratchTexture;
        ComposeOverlayLayersOnto(0, vjLayers.Length, current, overlayScratch);

        Graphics.Blit(current, finalCompositeTexture);

        if (programOutputRenderers != null)
        {
            for (var i = 0; i < programOutputRenderers.Length; i++)
            {
                var renderer = programOutputRenderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }
                renderer.sharedMaterial.mainTexture = finalCompositeTexture;
                renderer.enabled = activeProgramDisplay >= 0;
            }
        }

        if (programOutputCamera != null)
        {
            programOutputCamera.targetDisplay = Mathf.Max(0, activeProgramDisplay);
            programOutputCamera.enabled = activeProgramDisplay >= 0;
        }

        if (programOutputRoot != null)
        {
            programOutputRoot.SetActive(activeProgramDisplay >= 0);
        }
    }

    private bool IsLayerActiveForProgramOutput(int layerIndex, LayerState layer)
    {
        if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None)
        {
            return false;
        }

        var soloEnabled = programSoloLayerIndex >= 0 && programSoloLayerIndex < vjLayers.Length;
        if (soloEnabled)
        {
            return layerIndex == programSoloLayerIndex;
        }

        return !layer.ProgramMuted;
    }

    private static bool IsOverlayBlendLayer(LayerState layer)
    {
        return layer != null && layer.BlendMode == LayerBlendMode.Overlay;
    }

    private static bool IsMaskBlendLayer(LayerState layer)
    {
        return layer != null && layer.BlendMode == LayerBlendMode.Mask;
    }

    private RenderTexture AcquireTemporaryComposite(string name)
    {
        var texture = RenderTexture.GetTemporary(Mathf.Max(320, outputWidth), Mathf.Max(180, outputHeight), 0, RenderTextureFormat.ARGB32);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private static void ReleaseTemporaryComposite(RenderTexture texture)
    {
        if (texture != null)
        {
            RenderTexture.ReleaseTemporary(texture);
        }
    }

    private RenderTexture BuildMainLayerCompositeRange(int startIndex, int endIndexExclusive)
    {
        var current = AcquireTemporaryComposite("Main Range Current");
        var scratch = AcquireTemporaryComposite("Main Range Scratch");
        Graphics.Blit(Texture2D.blackTexture, current);

        var wroteAnything = false;
        for (var i = startIndex; i < endIndexExclusive; i++)
        {
            var layer = vjLayers[i];
            if (!IsLayerActiveForProgramOutput(i, layer) || IsOverlayBlendLayer(layer))
            {
                continue;
            }

            if (IsMaskBlendLayer(layer))
            {
                var maskTexture = LayerPreviewTexture(i, layer);
                if (maskTexture == null)
                {
                    continue;
                }

                var upperComposite = BuildMainLayerCompositeRange(i + 1, endIndexExclusive);
                if (upperComposite == null)
                {
                    continue;
                }

                ApplyLayerMaskComposite(current, upperComposite, maskTexture, layer, scratch);
                ReleaseTemporaryComposite(upperComposite);
                var maskSwap = current;
                current = scratch;
                scratch = maskSwap;
                wroteAnything = true;
                break;
            }

            var texture = ResolveLayerSourceTexture(i, layer);
            if (texture == null)
            {
                continue;
            }

            var material = EnsureLayerCompositeMaterial();
            if (material == null)
            {
                Graphics.Blit(texture, scratch);
            }
            else
            {
                ApplyLayerCompositeProperties(material, texture, layer, 1f, layer.BlendMode);
                Graphics.Blit(current, scratch, material);
            }

            var swap = current;
            current = scratch;
            scratch = swap;
            wroteAnything = true;
        }

        ReleaseTemporaryComposite(scratch);
        if (!wroteAnything)
        {
            ReleaseTemporaryComposite(current);
            return null;
        }

        return current;
    }

    private void ComposeOverlayLayersOnto(int startIndex, int endIndexExclusive, RenderTexture current, RenderTexture scratch)
    {
        if (current == null || scratch == null)
        {
            return;
        }

        for (var i = startIndex; i < endIndexExclusive; i++)
        {
            var layer = vjLayers[i];
            if (!IsLayerActiveForProgramOutput(i, layer) || !IsOverlayBlendLayer(layer))
            {
                continue;
            }

            var texture = ResolveLayerSourceTexture(i, layer);
            if (texture == null)
            {
                continue;
            }

            var material = EnsureLayerCompositeMaterial();
            if (material == null)
            {
                Graphics.Blit(texture, scratch);
            }
            else
            {
                ApplyLayerCompositeProperties(material, texture, layer, 1f, LayerBlendMode.Add);
                Graphics.Blit(current, scratch, material);
            }

            var swap = current;
            current = scratch;
            scratch = swap;
        }

        if (!ReferenceEquals(current, mainSourceCompositeTexture))
        {
            Graphics.Blit(current, mainSourceCompositeTexture);
        }
    }

    private Texture ResolveLayerSourceTexture(int layerIndex, LayerState layer)
    {
        if (layer == null || layer.SourceKind == LayerSourceKind.None)
        {
            return null;
        }

        if (layer.SourceKind == LayerSourceKind.Effect)
        {
            if (layer.EffectRenderedFrame != Time.frameCount && !layer.EffectRenderInProgress && layerIndex >= 0)
            {
                RenderEffectLayer(layerIndex, layer);
            }
            if (layer.EffectRenderedOutput != null)
            {
                return layer.EffectRenderedOutput;
            }
        }

        if (layer.StaticTexture != null)
        {
            return layer.StaticTexture;
        }

        if (layer.MediaAvailabilityState == LayerMediaAvailabilityState.Offline && layer.OfflineHoldTexture != null)
        {
            return layer.OfflineHoldTexture;
        }

        if (layer.SourceKind == LayerSourceKind.Generator3D &&
            layer.Generator != null &&
            layer.Generator.Texture != null)
        {
            return layer.Generator.Texture;
        }

        if (layer.SourceKind == LayerSourceKind.Text &&
            layer.TextSource != null &&
            layer.Texture != null)
        {
            return layer.Texture;
        }

        if (layer.Player != null)
        {
            if (layer.Player.texture != null)
            {
                return layer.Player.texture;
            }
            if (layer.Texture != null)
            {
                return layer.Texture;
            }
        }

        if (layer.TextSource != null && layer.Texture != null)
        {
            return layer.Texture;
        }

        if (layer.Generator != null && layer.Generator.Texture != null)
        {
            return layer.Generator.Texture;
        }

        if (layer.OfflineHoldTexture != null)
        {
            return layer.OfflineHoldTexture;
        }

        return layer.Texture;
    }

    private Texture LayerPreviewTexture(int layerIndex, LayerState layer)
    {
        var sourceTexture = ResolveLayerSourceTexture(layerIndex, layer);
        if (layer == null || sourceTexture == null)
        {
            return sourceTexture;
        }

        if (layer.PreviewTexture == null)
        {
            return sourceTexture;
        }

        if (layer.PreviewRenderedFrame == Time.frameCount)
        {
            return layer.PreviewTexture;
        }

        var targetWidth = sourceTexture.width > 0 ? sourceTexture.width : outputWidth;
        var targetHeight = sourceTexture.height > 0 ? sourceTexture.height : outputHeight;
        layer.PreviewTexture = EnsureCompositeRenderTexture(layer.PreviewTexture, (layer.PreviewTexture == null ? "Layer Preview" : layer.PreviewTexture.name), targetWidth, targetHeight);
        var material = EnsureLayerCompositeMaterial();
        if (material == null)
        {
            Graphics.Blit(sourceTexture, layer.PreviewTexture);
        }
        else
        {
            ApplyLayerCompositeProperties(material, sourceTexture, layer, 1f, LayerBlendMode.Alpha);
            Graphics.Blit(Texture2D.blackTexture, layer.PreviewTexture, material);
        }
        layer.PreviewRenderedFrame = Time.frameCount;
        return layer.PreviewTexture;
    }

    private void RefreshContinuousLayerPreviews()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (!RequiresContinuousPreviewRefresh(i, layer))
            {
                continue;
            }

            LayerPreviewTexture(i, layer);
        }
    }

    private bool RequiresContinuousPreviewRefresh(int layerIndex, LayerState layer)
    {
        if (layer == null || layer.SourceKind == LayerSourceKind.None)
        {
            return false;
        }

        if (layerIndex == selectedLayerIndex)
        {
            return true;
        }

        if (IsDynamicLayerSource(layer))
        {
            return true;
        }

        return layer.OpacityInputMode != LayerScaleInputMode.Manual ||
               layer.ScaleInputMode != LayerScaleInputMode.Manual ||
               layer.ScaleXInputMode != LayerScaleInputMode.Manual ||
               layer.ScaleYInputMode != LayerScaleInputMode.Manual;
    }

    private static bool IsDynamicLayerSource(LayerState layer)
    {
        if (layer == null)
        {
            return false;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
            case LayerSourceKind.YouTube:
            case LayerSourceKind.Generator3D:
            case LayerSourceKind.Effect:
                return true;
            default:
                return false;
        }
    }

    private static Color LayerCellBaseColor(LayerState layer)
    {
        if (layer == null || layer.SourceKind == LayerSourceKind.None)
        {
            return new Color(0.07f, 0.075f, 0.085f, 1f);
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
            case LayerSourceKind.YouTube:
                return new Color(0.11f, 0.17f, 0.19f, 1f);
            case LayerSourceKind.Image:
                return new Color(0.16f, 0.12f, 0.1f, 1f);
            case LayerSourceKind.Text:
                return new Color(0.16f, 0.14f, 0.09f, 1f);
            case LayerSourceKind.Generator3D:
                return new Color(0.12f, 0.11f, 0.18f, 1f);
            case LayerSourceKind.Effect:
                return new Color(0.16f, 0.08f, 0.12f, 1f);
            case LayerSourceKind.Capture:
                return new Color(0.1f, 0.16f, 0.1f, 1f);
            default:
                return new Color(0.09f, 0.095f, 0.105f, 1f);
        }
    }

    private static Color LayerBlendColor(LayerState layer)
    {
        if (layer == null)
        {
            return Color.white;
        }

        switch (layer.BlendMode)
        {
            case LayerBlendMode.Add50:
                return new Color(1f, 1f, 1f, 0.82f);
            case LayerBlendMode.Add:
            case LayerBlendMode.Overlay:
            case LayerBlendMode.Screen:
            case LayerBlendMode.Lighten:
                return new Color(1f, 1f, 1f, 0.92f);
            case LayerBlendMode.Multiply:
            case LayerBlendMode.Darken:
                return new Color(0.82f, 0.9f, 1f, 0.88f);
            case LayerBlendMode.Difference:
            case LayerBlendMode.Subtract:
                return new Color(1f, 0.94f, 0.86f, 0.9f);
            case LayerBlendMode.Mask:
                return new Color(1f, 1f, 1f, 0.88f);
            default:
                return new Color(1f, 1f, 1f, Mathf.Clamp01(layer.Opacity));
        }
    }

    private int GetStepSequencerQueueIndexForLayer(int hostLayerIndex, int targetLayerIndex)
    {
        var effect = GetStepSequencerEffect(hostLayerIndex);
        if (effect == null || effect.StepSequenceQueue == null)
        {
            return -1;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            var entry = effect.StepSequenceQueue[i];
            if (entry != null && entry.Kind == StepSequencerEntryKind.Layer && entry.LayerIndex == targetLayerIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyLayerAudioOutputState(LayerState layer)
    {
        if (layer == null || layer.Player == null)
        {
            return;
        }

        try
        {
            layer.Player.audioOutputMode = VideoAudioOutputMode.Direct;
            layer.Player.EnableAudioTrack(0, true);
            var mute = !layer.AudioOutputEnabled || layer.ProgramMuted || layer.SourceKind == LayerSourceKind.Effect;
            layer.Player.SetDirectAudioMute(0, mute);
            layer.Player.SetDirectAudioVolume(0, mute ? 0f : 1f);
        }
        catch
        {
        }
    }

    private bool IsStepSequencerShiftAssignMode()
    {
        if (vjLayers == null || selectedLayerIndex < 0 || selectedLayerIndex >= vjLayers.Length)
        {
            return false;
        }

        var layer = vjLayers[selectedLayerIndex];
        var effect = layer == null ? null : layer.Effect;
        return effect != null &&
               effect.Kind == LayerEffectKind.StepSequencer &&
               (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
    }

    private void ToggleStepSequencerLayerQueueEntry(int hostLayerIndex, int targetLayerIndex)
    {
        var effect = GetStepSequencerEffect(hostLayerIndex);
        if (effect == null || hostLayerIndex == targetLayerIndex)
        {
            return;
        }

        var index = GetStepSequencerQueueIndexForLayer(hostLayerIndex, targetLayerIndex);
        if (index >= 0)
        {
            effect.StepSequenceQueue.RemoveAt(index);
            return;
        }

        if (effect.StepSequenceQueue.Count >= StepSequencerMaxMediaLayers)
        {
            return;
        }

        effect.StepSequenceQueue.Add(new StepSequencerEntry
        {
            Kind = StepSequencerEntryKind.Layer,
            LayerIndex = targetLayerIndex
        });
    }

    private static bool LayerHasAttachedEffect(LayerState layer)
    {
        return layer != null && layer.Effect != null && layer.Effect.HasEffect;
    }

    private int GetStepSequencerQueueIndexForMediaPath(int hostLayerIndex, string path)
    {
        var effect = GetStepSequencerEffect(hostLayerIndex);
        if (effect == null || effect.StepSequenceQueue == null || string.IsNullOrEmpty(path))
        {
            return -1;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            var entry = effect.StepSequenceQueue[i];
            if (entry != null &&
                entry.Kind == StepSequencerEntryKind.MediaFile &&
                string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ToggleStepSequencerMediaQueueEntry(int hostLayerIndex, string path)
    {
        var effect = GetStepSequencerEffect(hostLayerIndex);
        if (effect == null || string.IsNullOrEmpty(path))
        {
            return;
        }

        var index = GetStepSequencerQueueIndexForMediaPath(hostLayerIndex, path);
        if (index >= 0)
        {
            effect.StepSequenceQueue.RemoveAt(index);
            SyncStepSequencerMediaSlots(hostLayerIndex, effect);
            return;
        }

        if (effect.StepSequenceQueue.Count >= StepSequencerMaxMediaLayers)
        {
            return;
        }

        effect.StepSequenceQueue.Add(new StepSequencerEntry
        {
            Kind = StepSequencerEntryKind.MediaFile,
            Path = path
        });
        SyncStepSequencerMediaSlots(hostLayerIndex, effect);
    }

    private void DrawLayerSettingsScreen(Rect full)
    {
        DrawLayerSettingsScreenContent(full, false);
    }

    private void DrawLayerSettingsScreenContent(Rect full, bool compact)
    {
        var data = BuildSettingsViewData();
        var margin = compact ? 10f : 18f;
        var gap = compact ? 10f : 14f;

        GUI.DrawTexture(full, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.024f, 0.03f, 1f), 0f, 0f);
        GUI.Label(new Rect(margin, margin, full.width - margin * 2f, 28f), data.LayerLabel + "  " + data.Title, titleStyle);
        GUI.Label(new Rect(margin, margin + 30f, full.width - margin * 2f, 22f), PreferText(data.Subtitle, "") + (string.IsNullOrEmpty(data.Status) ? "" : "  /  " + data.Status), smallStyle);
        var midiButtonRect = new Rect(full.xMax - margin - 136f, margin, 136f, 28f);
        if (GUI.Button(midiButtonRect, midiEditMode ? "MIDI Learn ON" : "MIDI Learn"))
        {
            midiEditMode = !midiEditMode;
            if (!midiEditMode)
            {
                midiLearningAction = MidiBindingAction.None;
                midiLearningIndex = -1;
                midiLearningSubIndex = -1;
            }
        }

        var previewWidth = Mathf.Min(full.width * 0.42f, 520f);
        var previewRect = new Rect(margin, margin + 62f, previewWidth, Mathf.Min(full.height - margin * 2f - 62f, previewWidth * 9f / 16f + 128f));
        GUI.DrawTexture(previewRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 4f);
        var previewImageRect = new Rect(previewRect.x + 4f, previewRect.y + 4f, previewRect.width - 8f, Mathf.Max(120f, previewWidth * 9f / 16f));
        if (data.PreviewTexture != null)
        {
            GUI.DrawTexture(previewImageRect, data.PreviewTexture, ScaleMode.ScaleToFit, false);
        }
        DrawLayerPreviewParameters(new Rect(previewRect.x + 8f, previewImageRect.yMax + 6f, previewRect.width - 16f, previewRect.yMax - previewImageRect.yMax - 10f), data.SelectedLayerIndex);

        var detailRect = new Rect(previewRect.xMax + gap, previewRect.y, full.width - previewRect.xMax - margin - gap, full.height - previewRect.y - margin);
        GUILayout.BeginArea(detailRect);
        layerSettingsScroll = GUILayout.BeginScrollView(layerSettingsScroll);
        DrawLayerScaleSettingsSection(data.SelectedLayerIndex);
        GUILayout.Space(12f);
        DrawLayerInteractiveSettingsSection(data.SelectedLayerIndex);
        GUILayout.Space(12f);
        DrawSettingsLinesSection("Layer", data.Details);
        DrawSettingsLinesSection("Common", data.CommonDetails);
        DrawSettingsLinesSection("Effect", data.EffectDetails);
        DrawSettingsLinesSection(data.SourceDetailTitle, data.SourceDetails);
        if (!string.IsNullOrEmpty(data.PresetInfo))
        {
            DrawSettingsLinesSection("Preset", data.PresetInfo.Split(new[] { '\n' }, StringSplitOptions.None));
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EnsureLayerAutomationCurves(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        EnsureLayerAutomationCurveState(ref layer.ScaleAutomation);
        EnsureLayerAutomationCurveState(ref layer.OpacityAutomation);
        EnsureLayerAutomationCurveState(ref layer.ScaleXAutomation);
        EnsureLayerAutomationCurveState(ref layer.ScaleYAutomation);
        EnsureLayerAutomationCurveState(ref layer.HueAutomation);
        EnsureLayerAutomationCurveState(ref layer.InvertAutomation);
        EnsureLayerAutomationCurveState(ref layer.MonochromeAutomation);
        EnsureLayerEnvelopeRange(ref layer.OpacityEnvelopeMin, ref layer.OpacityEnvelopeMax, layer.Opacity, 0f, 0f, 1f);
        EnsureLayerEnvelopeRange(ref layer.ScaleEnvelopeMin, ref layer.ScaleEnvelopeMax, layer.Scale, 1f, LayerScaleMin, LayerScaleMax);
        EnsureLayerEnvelopeRange(ref layer.ScaleXEnvelopeMin, ref layer.ScaleXEnvelopeMax, layer.ScaleX, 1f, LayerScaleMin, LayerScaleMax);
        EnsureLayerEnvelopeRange(ref layer.ScaleYEnvelopeMin, ref layer.ScaleYEnvelopeMax, layer.ScaleY, 1f, LayerScaleMin, LayerScaleMax);
        EnsureLayerEnvelopeRange(ref layer.HueEnvelopeMin, ref layer.HueEnvelopeMax, layer.HueShift, 0f, 0f, 1f);
        EnsureLayerEnvelopeRange(ref layer.InvertEnvelopeMin, ref layer.InvertEnvelopeMax, layer.InvertAmount, 0f, 0f, 1f);
        EnsureLayerEnvelopeRange(ref layer.MonochromeEnvelopeMin, ref layer.MonochromeEnvelopeMax, layer.MonochromeAmount, 0f, 0f, 1f);
    }

    private static void EnsureLayerEnvelopeRange(ref float envelopeMin, ref float envelopeMax, float value, float neutralValue, float minValue, float maxValue)
    {
        if (float.IsNaN(envelopeMin) || float.IsInfinity(envelopeMin))
        {
            envelopeMin = neutralValue;
        }
        if (float.IsNaN(envelopeMax) || float.IsInfinity(envelopeMax))
        {
            envelopeMax = value;
        }

        if (Mathf.Abs(envelopeMin) < 0.0001f &&
            Mathf.Abs(envelopeMax) < 0.0001f &&
            (Mathf.Abs(neutralValue) > 0.0001f || Mathf.Abs(value) > 0.0001f))
        {
            envelopeMin = neutralValue;
            envelopeMax = value;
        }

        envelopeMin = Mathf.Clamp(envelopeMin, minValue, maxValue);
        envelopeMax = Mathf.Clamp(envelopeMax, minValue, maxValue);
        if (envelopeMin > envelopeMax)
        {
            var swap = envelopeMin;
            envelopeMin = envelopeMax;
            envelopeMax = swap;
        }
    }

    private void EnsureLayerAutomationCurveState(ref LayerAutomationCurveState curve)
    {
        if (curve == null || curve.Samples == null || curve.Samples.Length != LayerAutomationSampleCount)
        {
            curve = CreateDefaultLayerAutomationCurve();
            return;
        }

        var segmentCount = Mathf.Max(0, curve.Samples.Length - 1);
        if (curve.SegmentInterpolationModes == null || curve.SegmentInterpolationModes.Length != segmentCount)
        {
            var newModes = new LayerAutomationInterpolationMode[segmentCount];
            for (var i = 0; i < newModes.Length; i++)
            {
                newModes[i] = curve.SegmentInterpolationModes != null && i < curve.SegmentInterpolationModes.Length
                    ? curve.SegmentInterpolationModes[i]
                    : LayerAutomationInterpolationMode.Linear;
                if (newModes[i] == LayerAutomationInterpolationMode.Step)
                {
                    newModes[i] = LayerAutomationInterpolationMode.Linear;
                }
            }
            curve.SegmentInterpolationModes = newModes;
        }

        if (curve.NodeDiscontinuities == null || curve.NodeDiscontinuities.Length != LayerAutomationSampleCount)
        {
            var newFlags = new bool[LayerAutomationSampleCount];
            if (curve.NodeDiscontinuities != null)
            {
                Array.Copy(curve.NodeDiscontinuities, newFlags, Math.Min(curve.NodeDiscontinuities.Length, newFlags.Length));
            }
            curve.NodeDiscontinuities = newFlags;
        }

        if (curve.OutgoingSamples == null || curve.OutgoingSamples.Length != LayerAutomationSampleCount)
        {
            var newOutgoing = new float[LayerAutomationSampleCount];
            for (var i = 0; i < newOutgoing.Length; i++)
            {
                newOutgoing[i] = curve.Samples != null && i < curve.Samples.Length ? curve.Samples[i] : 0f;
            }
            if (curve.OutgoingSamples != null)
            {
                Array.Copy(curve.OutgoingSamples, newOutgoing, Math.Min(curve.OutgoingSamples.Length, newOutgoing.Length));
            }
            curve.OutgoingSamples = newOutgoing;
        }

        SyncLayerAutomationLoopEndpoint(curve);
    }

    private static LayerAutomationCurveState CreateDefaultLayerAutomationCurve()
    {
        return new LayerAutomationCurveState
        {
            Samples = new[] { 0f, 1f, 1f, 1f, 0.7f, 0.45f, 0.2f, 0f, 0f },
            OutgoingSamples = new[] { 0f, 1f, 1f, 1f, 0.7f, 0.45f, 0.2f, 0f, 0f },
            NodeDiscontinuities = new bool[LayerAutomationSampleCount],
            SegmentInterpolationModes = new[]
            {
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear,
                LayerAutomationInterpolationMode.Linear
            }
        };
    }

    private static void SyncLayerAutomationLoopEndpoint(LayerAutomationCurveState curve)
    {
        if (curve == null || curve.Samples == null || curve.Samples.Length < 2)
        {
            return;
        }

        curve.Samples[curve.Samples.Length - 1] = curve.Samples[0];
        if (curve.OutgoingSamples != null && curve.OutgoingSamples.Length == curve.Samples.Length)
        {
            curve.OutgoingSamples[curve.OutgoingSamples.Length - 1] = curve.OutgoingSamples[0];
        }
        if (curve.NodeDiscontinuities != null && curve.NodeDiscontinuities.Length == curve.Samples.Length)
        {
            curve.NodeDiscontinuities[curve.NodeDiscontinuities.Length - 1] = curve.NodeDiscontinuities[0];
        }
    }

    private float CurrentLayerAutomationPhase()
    {
        var bpm = CurrentVisualBpm();
        var beatFloat = CurrentVisualBeatFloat(bpm);
        return Mathf.Repeat(beatFloat / 4f, 1f);
    }

    private static float LayerAutomationSegmentStartValue(LayerAutomationCurveState curve, int segmentIndex)
    {
        if (curve == null || curve.Samples == null || segmentIndex < 0 || segmentIndex >= curve.Samples.Length)
        {
            return 0f;
        }

        var start = Mathf.Clamp01(curve.Samples[segmentIndex]);
        if (curve.NodeDiscontinuities != null &&
            curve.OutgoingSamples != null &&
            segmentIndex < curve.NodeDiscontinuities.Length &&
            segmentIndex < curve.OutgoingSamples.Length &&
            curve.NodeDiscontinuities[segmentIndex])
        {
            start = Mathf.Clamp01(curve.OutgoingSamples[segmentIndex]);
        }

        return start;
    }

    private static int CanonicalAutomationNodeIndex(LayerAutomationCurveState curve, int nodeIndex)
    {
        if (curve == null || curve.Samples == null || curve.Samples.Length == 0)
        {
            return 0;
        }

        var clamped = Mathf.Clamp(nodeIndex, 0, curve.Samples.Length - 1);
        return clamped == curve.Samples.Length - 1 ? 0 : clamped;
    }

    private static float EvaluateLayerAutomationCurve(LayerAutomationCurveState curve, float phase)
    {
        if (curve == null || curve.Samples == null || curve.Samples.Length == 0)
        {
            return 1f;
        }

        if (curve.Samples.Length == 1)
        {
            return Mathf.Clamp01(curve.Samples[0]);
        }

        var scaled = Mathf.Clamp01(phase) * (curve.Samples.Length - 1);
        var left = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, curve.Samples.Length - 1);
        var right = Mathf.Clamp(left + 1, 0, curve.Samples.Length - 1);
        var t = scaled - left;
        var leftValue = LayerAutomationSegmentStartValue(curve, left);
        var rightValue = Mathf.Clamp01(curve.Samples[right]);
        var mode = curve.SegmentInterpolationModes == null || left >= curve.SegmentInterpolationModes.Length
            ? LayerAutomationInterpolationMode.Linear
            : curve.SegmentInterpolationModes[left];
        switch (mode)
        {
            case LayerAutomationInterpolationMode.Exponential:
                t = Mathf.Pow(t, 2.2f);
                break;
            case LayerAutomationInterpolationMode.Logarithmic:
                t = 1f - Mathf.Pow(1f - t, 2.2f);
                break;
        }
        return Mathf.Lerp(leftValue, rightValue, t);
    }

    private int GetSelectedLayerAutomationSegment(int layerIndex, LayerAutomationTarget target, LayerAutomationCurveState curve)
    {
        var maxIndex = curve == null || curve.Samples == null ? 0 : Mathf.Max(0, curve.Samples.Length - 2);
        if (layerAutomationSelectedLayerIndex != layerIndex || layerAutomationSelectedTarget != target)
        {
            layerAutomationSelectedLayerIndex = layerIndex;
            layerAutomationSelectedTarget = target;
            layerAutomationSelectedSegmentIndex = Mathf.Clamp(layerAutomationSelectedSegmentIndex, 0, maxIndex);
            layerAutomationSelectedNodeIndex = Mathf.Clamp(layerAutomationSelectedNodeIndex, 0, curve == null || curve.Samples == null ? 0 : Mathf.Max(0, curve.Samples.Length - 1));
        }
        layerAutomationSelectedSegmentIndex = Mathf.Clamp(layerAutomationSelectedSegmentIndex, 0, maxIndex);
        return layerAutomationSelectedSegmentIndex;
    }

    private void SetSelectedLayerAutomationSegment(int layerIndex, LayerAutomationTarget target, int segmentIndex, LayerAutomationCurveState curve)
    {
        layerAutomationSelectedLayerIndex = layerIndex;
        layerAutomationSelectedTarget = target;
        layerAutomationSelectedSegmentIndex = Mathf.Clamp(segmentIndex, 0, curve == null || curve.Samples == null ? 0 : Mathf.Max(0, curve.Samples.Length - 2));
    }

    private int GetSelectedLayerAutomationNode(int layerIndex, LayerAutomationTarget target, LayerAutomationCurveState curve)
    {
        var maxIndex = curve == null || curve.Samples == null ? 0 : Mathf.Max(0, curve.Samples.Length - 1);
        if (layerAutomationSelectedLayerIndex != layerIndex || layerAutomationSelectedTarget != target)
        {
            layerAutomationSelectedLayerIndex = layerIndex;
            layerAutomationSelectedTarget = target;
        }

        layerAutomationSelectedNodeIndex = Mathf.Clamp(layerAutomationSelectedNodeIndex, 0, maxIndex);
        return layerAutomationSelectedNodeIndex;
    }

    private void SetSelectedLayerAutomationNode(int layerIndex, LayerAutomationTarget target, int nodeIndex, LayerAutomationCurveState curve)
    {
        layerAutomationSelectedLayerIndex = layerIndex;
        layerAutomationSelectedTarget = target;
        layerAutomationSelectedNodeIndex = Mathf.Clamp(nodeIndex, 0, curve == null || curve.Samples == null ? 0 : Mathf.Max(0, curve.Samples.Length - 1));
    }

    private float ResolveLayerInputValue(float value, LayerScaleInputMode mode, float fftAmount, LayerAutomationCurveState automation, float envelopeMin, float envelopeMax, float neutralValue, float minValue, float maxValue)
    {
        var resolved = value;
        var fftDrive = captureAudioFftEnabled ? captureAudioFftDrive : 0f;
        switch (mode)
        {
            case LayerScaleInputMode.Fft:
                if (Mathf.Abs(neutralValue) < 0.0001f)
                {
                    resolved += fftDrive * Mathf.Max(0f, fftAmount);
                }
                else
                {
                    resolved *= 1f + fftDrive * Mathf.Max(0f, fftAmount);
                }
                break;
            case LayerScaleInputMode.Envelope:
                EnsureLayerEnvelopeRange(ref envelopeMin, ref envelopeMax, value, neutralValue, minValue, maxValue);
                resolved = Mathf.Lerp(envelopeMin, envelopeMax, EvaluateLayerAutomationCurve(automation, CurrentLayerAutomationPhase()));
                break;
        }

        return Mathf.Clamp(resolved, minValue, maxValue);
    }

    private static string LayerInputModeLabel(LayerScaleInputMode mode)
    {
        switch (mode)
        {
            case LayerScaleInputMode.Fft: return "FFT";
            case LayerScaleInputMode.Envelope: return "Envelope";
            default: return "Manual";
        }
    }

    private static string LayerAutomationInterpolationLabel(LayerAutomationInterpolationMode mode)
    {
        switch (mode)
        {
            case LayerAutomationInterpolationMode.Exponential: return "Exp";
            case LayerAutomationInterpolationMode.Logarithmic: return "Log";
            case LayerAutomationInterpolationMode.Step: return "Step";
            default: return "Linear";
        }
    }

    private static string AutomationTargetLabel(LayerAutomationTarget target)
    {
        switch (target)
        {
            case LayerAutomationTarget.Opacity: return "Opacity";
            case LayerAutomationTarget.Scale: return "Scale";
            case LayerAutomationTarget.ScaleX: return "Scale X";
            case LayerAutomationTarget.ScaleY: return "Scale Y";
            case LayerAutomationTarget.Hue: return "Hue";
            case LayerAutomationTarget.Invert: return "Invert";
            case LayerAutomationTarget.Monochrome: return "B/W";
            default: return string.Empty;
        }
    }

    private bool IsLayerInputModePopupOpen(int layerIndex, LayerAutomationTarget target)
    {
        return layerInputModePopupLayerIndex == layerIndex && layerInputModePopupTarget == target;
    }

    private float LayerInputRowHeight(int layerIndex, LayerAutomationTarget target, LayerScaleInputMode mode)
    {
        var popupExtra = IsLayerInputModePopupOpen(layerIndex, target) ? 98f : 0f;
        switch (mode)
        {
            case LayerScaleInputMode.Fft: return 78f + popupExtra;
            case LayerScaleInputMode.Envelope: return 106f + popupExtra;
            default: return 50f + popupExtra;
        }
    }

    private static string FormatLayerInputValue(float value, float minValue, float maxValue, LayerInputDisplayStyle displayStyle)
    {
        var clamped = Mathf.Clamp(value, minValue, maxValue);
        switch (displayStyle)
        {
            case LayerInputDisplayStyle.Percent:
                return Mathf.RoundToInt(Mathf.Clamp01(clamped) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            case LayerInputDisplayStyle.Multiplier:
                return "x" + clamped.ToString("0.00", CultureInfo.InvariantCulture);
            case LayerInputDisplayStyle.Degrees:
                return Mathf.RoundToInt(Mathf.Repeat(clamped, 1f) * 360f).ToString(CultureInfo.InvariantCulture) + " deg";
            default:
                return clamped.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    private float DrawReadableHorizontalSlider(Rect rect, float value, float minValue, float maxValue, Color fillColor, bool interactive = true)
    {
        var trackRect = new Rect(rect.x, rect.y + rect.height * 0.5f - 5f, rect.width, 10f);
        GUI.DrawTexture(trackRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.17f, 0.18f, 0.2f, 1f), 0f, 3f);
        DrawRectOutline(trackRect, 1f, new Color(0.34f, 0.36f, 0.4f, 1f));
        var normalized = Mathf.InverseLerp(minValue, maxValue, value);
        if (normalized > 0f)
        {
            var fillRect = new Rect(trackRect.x + 1f, trackRect.y + 1f, Mathf.Max(0f, (trackRect.width - 2f) * normalized), trackRect.height - 2f);
            GUI.DrawTexture(fillRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, fillColor, 0f, 2f);
        }

        var thumbX = Mathf.Lerp(trackRect.x, trackRect.xMax, normalized);
        var thumbRect = new Rect(thumbX - 6f, rect.y + rect.height * 0.5f - 11f, 12f, 22f);
        GUI.DrawTexture(thumbRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.white, 0f, 3f);
        DrawRectOutline(thumbRect, 1f, Color.black);

        var evt = Event.current;
        if (interactive && evt != null)
        {
            var interactiveRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && interactiveRect.Contains(evt.mousePosition))
            {
                value = Mathf.Lerp(minValue, maxValue, Mathf.Clamp01((evt.mousePosition.x - trackRect.x) / Mathf.Max(1f, trackRect.width)));
                evt.Use();
            }
        }

        return Mathf.Clamp(value, minValue, maxValue);
    }

    private void DrawReadableHorizontalRangeSlider(
        Rect rect,
        int layerIndex,
        LayerAutomationTarget target,
        ref float rangeMin,
        ref float rangeMax,
        float minValue,
        float maxValue,
        Color fillColor)
    {
        var trackRect = new Rect(rect.x, rect.y + rect.height * 0.5f - 5f, rect.width, 10f);
        GUI.DrawTexture(trackRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.17f, 0.18f, 0.2f, 1f), 0f, 3f);
        DrawRectOutline(trackRect, 1f, new Color(0.34f, 0.36f, 0.4f, 1f));

        var minNormalized = Mathf.InverseLerp(minValue, maxValue, rangeMin);
        var maxNormalized = Mathf.InverseLerp(minValue, maxValue, rangeMax);
        var minX = Mathf.Lerp(trackRect.x, trackRect.xMax, minNormalized);
        var maxX = Mathf.Lerp(trackRect.x, trackRect.xMax, maxNormalized);
        var fillRect = new Rect(Mathf.Min(minX, maxX), trackRect.y + 1f, Mathf.Abs(maxX - minX), trackRect.height - 2f);
        if (fillRect.width > 0f)
        {
            GUI.DrawTexture(fillRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, fillColor, 0f, 2f);
        }

        var lowerThumbRect = new Rect(minX - 6f, rect.y + rect.height * 0.5f - 11f, 12f, 22f);
        var upperThumbRect = new Rect(maxX - 6f, rect.y + rect.height * 0.5f - 11f, 12f, 22f);
        GUI.DrawTexture(lowerThumbRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.52f, 0.88f, 1f, 1f), 0f, 3f);
        GUI.DrawTexture(upperThumbRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.82f, 0.32f, 1f), 0f, 3f);
        DrawRectOutline(lowerThumbRect, 1f, Color.black);
        DrawRectOutline(upperThumbRect, 1f, Color.black);

        var evt = Event.current;
        if (evt == null)
        {
            return;
        }

        var isActiveDrag = layerEnvelopeRangeDragLayerIndex == layerIndex && layerEnvelopeRangeDragTarget == target;
        if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
        {
            var distanceToMin = Mathf.Abs(evt.mousePosition.x - minX);
            var distanceToMax = Mathf.Abs(evt.mousePosition.x - maxX);
            layerEnvelopeRangeDragLayerIndex = layerIndex;
            layerEnvelopeRangeDragTarget = target;
            layerEnvelopeRangeDragUpperHandle = distanceToMax < distanceToMin;
            evt.Use();
            isActiveDrag = true;
        }

        if (evt.type == EventType.MouseDrag && evt.button == 0 && isActiveDrag)
        {
            var nextValue = Mathf.Lerp(minValue, maxValue, Mathf.Clamp01((evt.mousePosition.x - trackRect.x) / Mathf.Max(1f, trackRect.width)));
            if (layerEnvelopeRangeDragUpperHandle)
            {
                rangeMax = Mathf.Clamp(nextValue, rangeMin, maxValue);
            }
            else
            {
                rangeMin = Mathf.Clamp(nextValue, minValue, rangeMax);
            }
            evt.Use();
            return;
        }

        if (evt.rawType == EventType.MouseUp && isActiveDrag)
        {
            layerEnvelopeRangeDragLayerIndex = -1;
            layerEnvelopeRangeDragTarget = LayerAutomationTarget.None;
            layerEnvelopeRangeDragUpperHandle = false;
        }
    }

    private bool TryGetLayerInputMode(int layerIndex, LayerAutomationTarget target, out LayerScaleInputMode mode)
    {
        mode = LayerScaleInputMode.Manual;
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return false;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return false;
        }

        switch (target)
        {
            case LayerAutomationTarget.Opacity:
                mode = layer.OpacityInputMode;
                return true;
            case LayerAutomationTarget.Scale:
                mode = layer.ScaleInputMode;
                return true;
            case LayerAutomationTarget.ScaleX:
                mode = layer.ScaleXInputMode;
                return true;
            case LayerAutomationTarget.ScaleY:
                mode = layer.ScaleYInputMode;
                return true;
            case LayerAutomationTarget.Hue:
                mode = layer.HueInputMode;
                return true;
            case LayerAutomationTarget.Invert:
                mode = layer.InvertInputMode;
                return true;
            case LayerAutomationTarget.Monochrome:
                mode = layer.MonochromeInputMode;
                return true;
            default:
                return false;
        }
    }

    private bool SetLayerInputMode(int layerIndex, LayerAutomationTarget target, LayerScaleInputMode mode)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return false;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return false;
        }

        switch (target)
        {
            case LayerAutomationTarget.Opacity:
                layer.OpacityInputMode = mode;
                return true;
            case LayerAutomationTarget.Scale:
                layer.ScaleInputMode = mode;
                return true;
            case LayerAutomationTarget.ScaleX:
                layer.ScaleXInputMode = mode;
                return true;
            case LayerAutomationTarget.ScaleY:
                layer.ScaleYInputMode = mode;
                return true;
            case LayerAutomationTarget.Hue:
                layer.HueInputMode = mode;
                return true;
            case LayerAutomationTarget.Invert:
                layer.InvertInputMode = mode;
                return true;
            case LayerAutomationTarget.Monochrome:
                layer.MonochromeInputMode = mode;
                return true;
            default:
                return false;
        }
    }

    private Rect LayerInputModePopupBounds(Rect buttonRect)
    {
        var popupWidth = Mathf.Max(164f, buttonRect.width + 56f);
        return new Rect(buttonRect.xMax - popupWidth, buttonRect.yMax + 6f, popupWidth, 92f);
    }

    private void DrawLayerInputModePopup(Rect buttonRect, int layerIndex, LayerAutomationTarget target, LayerScaleInputMode mode)
    {
        if (layerInputModePopupLayerIndex != layerIndex || layerInputModePopupTarget != target)
        {
            return;
        }

        var popupRect = LayerInputModePopupBounds(buttonRect);
        layerInputModePopupRect = popupRect;
        GUI.DrawTexture(popupRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.06f, 0.07f, 0.085f, 1f), 0f, 4f);
        DrawRectOutline(popupRect, 1f, new Color(0.36f, 0.4f, 0.46f, 1f));
        var modes = new[] { LayerScaleInputMode.Manual, LayerScaleInputMode.Fft, LayerScaleInputMode.Envelope };
        var evt = Event.current;
        for (var i = 0; i < modes.Length; i++)
        {
            var optionRect = new Rect(popupRect.x + 6f, popupRect.y + 6f + i * 28f, popupRect.width - 12f, 24f);
            var active = mode == modes[i];
            GUI.DrawTexture(optionRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, active ? new Color(0.18f, 0.42f, 0.6f, 1f) : new Color(0.12f, 0.13f, 0.15f, 1f), 0f, 3f);
            GUI.Label(optionRect, "  " + LayerInputModeLabel(modes[i]), smallStyle);
            if (evt != null && evt.type == EventType.MouseDown && optionRect.Contains(evt.mousePosition))
            {
                SetLayerInputMode(layerIndex, target, modes[i]);
                if (modes[i] == LayerScaleInputMode.Envelope)
                {
                    layerAutomationSelectedLayerIndex = layerIndex;
                    layerAutomationSelectedTarget = target;
                }
                layerInputModePopupLayerIndex = -1;
                layerInputModePopupTarget = LayerAutomationTarget.None;
                layerInputModePopupAnchorValid = false;
                evt.Use();
                return;
            }
        }

        if (evt != null &&
            evt.type == EventType.MouseDown &&
            !buttonRect.Contains(evt.mousePosition) &&
            !popupRect.Contains(evt.mousePosition))
        {
            layerInputModePopupLayerIndex = -1;
            layerInputModePopupTarget = LayerAutomationTarget.None;
            layerInputModePopupAnchorValid = false;
            evt.Use();
        }
    }

    private void HandleFloatingLayerInputModePopupInput()
    {
        if (activeScreen != 2 ||
            layerInputModePopupLayerIndex < 0 ||
            !layerInputModePopupAnchorValid ||
            layerInputModePopupTarget == LayerAutomationTarget.None)
        {
            return;
        }

        var evt = Event.current;
        if (evt == null || evt.type != EventType.MouseDown)
        {
            return;
        }

        var buttonRect = ScreenRectToGuiRect(layerInputModePopupButtonRect);
        var popupRect = LayerInputModePopupBounds(buttonRect);
        layerInputModePopupRect = popupRect;

        LayerScaleInputMode currentMode;
        if (!TryGetLayerInputMode(layerInputModePopupLayerIndex, layerInputModePopupTarget, out currentMode))
        {
            layerInputModePopupLayerIndex = -1;
            layerInputModePopupTarget = LayerAutomationTarget.None;
            layerInputModePopupAnchorValid = false;
            return;
        }

        if (!popupRect.Contains(evt.mousePosition))
        {
            layerInputModePopupLayerIndex = -1;
            layerInputModePopupTarget = LayerAutomationTarget.None;
            layerInputModePopupAnchorValid = false;
            evt.Use();
            return;
        }

        var modes = new[] { LayerScaleInputMode.Manual, LayerScaleInputMode.Fft, LayerScaleInputMode.Envelope };
        for (var i = 0; i < modes.Length; i++)
        {
            var optionRect = new Rect(popupRect.x + 6f, popupRect.y + 6f + i * 28f, popupRect.width - 12f, 24f);
            if (!optionRect.Contains(evt.mousePosition))
            {
                continue;
            }

            SetLayerInputMode(layerInputModePopupLayerIndex, layerInputModePopupTarget, modes[i]);
            if (modes[i] == LayerScaleInputMode.Envelope)
            {
                layerAutomationSelectedLayerIndex = layerInputModePopupLayerIndex;
                layerAutomationSelectedTarget = layerInputModePopupTarget;
            }
            layerInputModePopupLayerIndex = -1;
            layerInputModePopupTarget = LayerAutomationTarget.None;
            layerInputModePopupAnchorValid = false;
            evt.Use();
            return;
        }

        evt.Use();
    }

    private void ConsumeLayerInputModePopupMouseEvents()
    {
        if (activeScreen != 2 ||
            layerInputModePopupLayerIndex < 0 ||
            !layerInputModePopupAnchorValid ||
            layerInputModePopupTarget == LayerAutomationTarget.None)
        {
            return;
        }

        var evt = Event.current;
        if (evt == null)
        {
            return;
        }

        switch (evt.type)
        {
            case EventType.MouseDown:
            case EventType.MouseUp:
            case EventType.MouseDrag:
            case EventType.ScrollWheel:
                evt.Use();
                break;
        }
    }

    private void DrawFloatingLayerInputModePopup()
    {
        if (activeScreen != 2 ||
            layerInputModePopupLayerIndex < 0 ||
            !layerInputModePopupAnchorValid ||
            layerInputModePopupTarget == LayerAutomationTarget.None)
        {
            return;
        }

        LayerScaleInputMode currentMode;
        if (!TryGetLayerInputMode(layerInputModePopupLayerIndex, layerInputModePopupTarget, out currentMode))
        {
            layerInputModePopupLayerIndex = -1;
            layerInputModePopupTarget = LayerAutomationTarget.None;
            layerInputModePopupAnchorValid = false;
            return;
        }

        DrawLayerInputModePopup(ScreenRectToGuiRect(layerInputModePopupButtonRect), layerInputModePopupLayerIndex, layerInputModePopupTarget, currentMode);
    }

    private void DrawLayerAutomationGraph(Rect rect, int layerIndex, LayerAutomationTarget target, LayerAutomationCurveState curve)
    {
        if (curve == null || curve.Samples == null || curve.Samples.Length == 0)
        {
            return;
        }

        EnsureLayerAutomationCurveState(ref curve);
        var selectedSegmentIndex = GetSelectedLayerAutomationSegment(layerIndex, target, curve);
        var selectedNodeIndex = GetSelectedLayerAutomationNode(layerIndex, target, curve);
        var selectedInterpolationMode = curve.SegmentInterpolationModes[selectedSegmentIndex];
        var selectedNodeJump = curve.NodeDiscontinuities != null &&
                               selectedNodeIndex >= 0 &&
                               selectedNodeIndex < curve.NodeDiscontinuities.Length &&
                               curve.NodeDiscontinuities[selectedNodeIndex];

        var buttonsRect = new Rect(rect.x, rect.y, rect.width, 22f);
        var graphRect = new Rect(rect.x, rect.y + 28f, rect.width, rect.height - 28f);
        GUI.DrawTexture(graphRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.05f, 0.055f, 0.065f, 1f), 0f, 4f);
        DrawRectOutline(graphRect, 1f, new Color(0.3f, 0.34f, 0.4f, 1f));
        var interpolationModes = new[] {
            LayerAutomationInterpolationMode.Linear,
            LayerAutomationInterpolationMode.Exponential,
            LayerAutomationInterpolationMode.Logarithmic
        };
        var buttonWidth = Mathf.Max(60f, (buttonsRect.width - 16f) / 4f);
        for (var i = 0; i < interpolationModes.Length; i++)
        {
            var mode = interpolationModes[i];
            var buttonRect = new Rect(buttonsRect.x + i * (buttonWidth + 4f), buttonsRect.y, buttonWidth, 22f);
            var active = selectedInterpolationMode == mode;
            GUI.DrawTexture(buttonRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, active ? new Color(0.18f, 0.42f, 0.6f, 1f) : new Color(0.12f, 0.13f, 0.15f, 1f), 0f, 3f);
            GUI.Label(buttonRect, "  " + LayerAutomationInterpolationLabel(mode), smallStyle);
            if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition))
            {
                curve.SegmentInterpolationModes[selectedSegmentIndex] = mode;
                Event.current.Use();
            }
        }
        var jumpRect = new Rect(buttonsRect.x + 3f * (buttonWidth + 4f), buttonsRect.y, buttonWidth, 22f);
        GUI.DrawTexture(jumpRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, selectedNodeJump ? new Color(0.72f, 0.36f, 0.22f, 1f) : new Color(0.12f, 0.13f, 0.15f, 1f), 0f, 3f);
        GUI.Label(jumpRect, "  " + (selectedNodeJump ? "Jump ON" : "Jump OFF"), smallStyle);
        if (Event.current.type == EventType.MouseDown && jumpRect.Contains(Event.current.mousePosition))
        {
            var toggleNodeIndex = CanonicalAutomationNodeIndex(curve, selectedNodeIndex);
            curve.NodeDiscontinuities[toggleNodeIndex] = !curve.NodeDiscontinuities[toggleNodeIndex];
            if (!curve.NodeDiscontinuities[toggleNodeIndex])
            {
                curve.OutgoingSamples[toggleNodeIndex] = curve.Samples[toggleNodeIndex];
            }
            SyncLayerAutomationLoopEndpoint(curve);
            Event.current.Use();
        }

        for (var i = 1; i < 8; i++)
        {
            var x = Mathf.Lerp(graphRect.x, graphRect.xMax, i / 8f);
            DrawLine(new Vector2(x, graphRect.y), new Vector2(x, graphRect.yMax), new Color(0.11f, 0.13f, 0.16f, 1f), 1f);
        }
        for (var i = 1; i < 4; i++)
        {
            var y = Mathf.Lerp(graphRect.yMax, graphRect.y, i / 4f);
            DrawLine(new Vector2(graphRect.x, y), new Vector2(graphRect.xMax, y), new Color(0.11f, 0.13f, 0.16f, 1f), 1f);
        }

        GUI.Label(new Rect(graphRect.x, graphRect.y - 18f, graphRect.width, 16f), AutomationTargetLabel(target) + " envelope   1 bar / 1-8   seg " + (selectedSegmentIndex + 1).ToString(CultureInfo.InvariantCulture) + "   " + LayerAutomationInterpolationLabel(selectedInterpolationMode) + "   node " + (selectedNodeIndex + 1).ToString(CultureInfo.InvariantCulture) + (selectedNodeJump ? " jump" : ""), smallStyle);
        GUI.Label(new Rect(graphRect.x, graphRect.yMax + 2f, graphRect.width * 0.5f, 16f), "Time", smallStyle);
        GUI.Label(new Rect(graphRect.xMax - 70f, graphRect.yMax + 2f, 70f, 16f), "Value", smallStyle);

        for (var segment = 0; segment < curve.Samples.Length - 1; segment++)
        {
            var x0 = Mathf.Lerp(graphRect.x + 8f, graphRect.xMax - 8f, segment / (float)Mathf.Max(1, curve.Samples.Length - 1));
            var x1 = Mathf.Lerp(graphRect.x + 8f, graphRect.xMax - 8f, (segment + 1) / (float)Mathf.Max(1, curve.Samples.Length - 1));
            var y0 = Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, LayerAutomationSegmentStartValue(curve, segment));
            var y1 = Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, Mathf.Clamp01(curve.Samples[segment + 1]));
            var mode = curve.SegmentInterpolationModes == null || segment >= curve.SegmentInterpolationModes.Length
                ? LayerAutomationInterpolationMode.Linear
                : curve.SegmentInterpolationModes[segment];

            var previous = new Vector2(x0, y0);
            const int samplesPerSegment = 20;
            for (var stepIndex = 1; stepIndex <= samplesPerSegment; stepIndex++)
            {
                var t = stepIndex / (float)samplesPerSegment;
                var shapedT = t;
                switch (mode)
                {
                    case LayerAutomationInterpolationMode.Exponential:
                        shapedT = Mathf.Pow(t, 2.2f);
                        break;
                    case LayerAutomationInterpolationMode.Logarithmic:
                        shapedT = 1f - Mathf.Pow(1f - t, 2.2f);
                        break;
                }

                var point = new Vector2(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, shapedT));
                DrawLine(previous, point, new Color(0.35f, 0.86f, 1f, 1f), 2f);
                previous = point;
            }
        }

        for (var i = 0; i < curve.Samples.Length; i++)
        {
            var point = new Vector2(
                Mathf.Lerp(graphRect.x + 8f, graphRect.xMax - 8f, i / (float)Mathf.Max(1, curve.Samples.Length - 1)),
                Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, Mathf.Clamp01(curve.Samples[i])));
            var handleRect = new Rect(point.x - 5f, point.y - 5f, 10f, 10f);
            GUI.DrawTexture(handleRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.white, 0f, 3f);
            DrawRectOutline(handleRect, 1f, Color.black);

            var hasJump = curve.NodeDiscontinuities != null &&
                          curve.OutgoingSamples != null &&
                          i < curve.NodeDiscontinuities.Length &&
                          i < curve.OutgoingSamples.Length &&
                          curve.NodeDiscontinuities[i];
            if (hasJump)
            {
                var jumpPoint = new Vector2(
                    point.x + 10f,
                    Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, Mathf.Clamp01(curve.OutgoingSamples[i])));
                DrawLine(point, new Vector2(point.x, jumpPoint.y), new Color(1f, 0.54f, 0.28f, 0.95f), 2f);
                var jumpRectHandle = new Rect(jumpPoint.x - 4f, jumpPoint.y - 4f, 8f, 8f);
                GUI.DrawTexture(jumpRectHandle, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.75f, 0.35f, 1f), 0f, 3f);
                DrawRectOutline(jumpRectHandle, 1f, Color.black);
            }
        }

        var phaseX = Mathf.Lerp(graphRect.x + 8f, graphRect.xMax - 8f, CurrentLayerAutomationPhase());
        DrawLine(new Vector2(phaseX, graphRect.y + 2f), new Vector2(phaseX, graphRect.yMax - 2f), new Color(1f, 0.42f, 0.28f, 0.95f), 2f);

        var evt = Event.current;
        if (evt == null)
        {
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            var handledMouseDown = false;
            for (var i = 0; i < curve.Samples.Length; i++)
            {
                var point = new Vector2(
                    Mathf.Lerp(graphRect.x + 8f, graphRect.xMax - 8f, i / (float)Mathf.Max(1, curve.Samples.Length - 1)),
                    Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, Mathf.Clamp01(curve.Samples[i])));
                if (Vector2.Distance(evt.mousePosition, point) <= 10f)
                {
                    var canonicalNodeIndex = CanonicalAutomationNodeIndex(curve, i);
                    layerAutomationDragLayerIndex = layerIndex;
                    layerAutomationDragTarget = target;
                    layerAutomationDragSampleIndex = canonicalNodeIndex;
                    layerAutomationDragOutgoingHandle = false;
                    SetSelectedLayerAutomationNode(layerIndex, target, canonicalNodeIndex, curve);
                    if (i < curve.SegmentInterpolationModes.Length)
                    {
                        SetSelectedLayerAutomationSegment(layerIndex, target, i, curve);
                    }
                    else if (i > 0)
                    {
                        SetSelectedLayerAutomationSegment(layerIndex, target, i - 1, curve);
                    }
                    handledMouseDown = true;
                    evt.Use();
                    break;
                }

                var hasJump = curve.NodeDiscontinuities != null &&
                              curve.OutgoingSamples != null &&
                              i < curve.NodeDiscontinuities.Length &&
                              i < curve.OutgoingSamples.Length &&
                              curve.NodeDiscontinuities[i];
                if (!hasJump)
                {
                    continue;
                }

                var jumpPoint = new Vector2(
                    point.x + 10f,
                    Mathf.Lerp(graphRect.yMax - 8f, graphRect.y + 8f, Mathf.Clamp01(curve.OutgoingSamples[i])));
                if (Vector2.Distance(evt.mousePosition, jumpPoint) <= 10f)
                {
                    var canonicalNodeIndex = CanonicalAutomationNodeIndex(curve, i);
                    layerAutomationDragLayerIndex = layerIndex;
                    layerAutomationDragTarget = target;
                    layerAutomationDragSampleIndex = canonicalNodeIndex;
                    layerAutomationDragOutgoingHandle = true;
                    SetSelectedLayerAutomationNode(layerIndex, target, canonicalNodeIndex, curve);
                    if (canonicalNodeIndex < curve.SegmentInterpolationModes.Length)
                    {
                        SetSelectedLayerAutomationSegment(layerIndex, target, canonicalNodeIndex, curve);
                    }
                    evt.Use();
                    handledMouseDown = true;
                    break;
                }
            }
            if (!handledMouseDown && graphRect.Contains(evt.mousePosition))
            {
                var normalizedX = Mathf.Clamp01((evt.mousePosition.x - (graphRect.x + 8f)) / Mathf.Max(1f, graphRect.width - 16f));
                var segmentIndex = Mathf.Clamp(Mathf.FloorToInt(normalizedX * (curve.Samples.Length - 1)), 0, curve.SegmentInterpolationModes.Length - 1);
                SetSelectedLayerAutomationSegment(layerIndex, target, segmentIndex, curve);
                SetSelectedLayerAutomationNode(layerIndex, target, segmentIndex, curve);
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseDrag &&
                 layerAutomationDragLayerIndex == layerIndex &&
                 layerAutomationDragTarget == target &&
                 layerAutomationDragSampleIndex >= 0)
        {
            var nextValue = Mathf.Clamp01(1f - ((evt.mousePosition.y - (graphRect.y + 8f)) / Mathf.Max(1f, graphRect.height - 16f)));
            if (layerAutomationDragOutgoingHandle &&
                curve.OutgoingSamples != null &&
                layerAutomationDragSampleIndex >= 0 &&
                layerAutomationDragSampleIndex < curve.OutgoingSamples.Length)
            {
                curve.OutgoingSamples[layerAutomationDragSampleIndex] = nextValue;
            }
            else
            {
                curve.Samples[layerAutomationDragSampleIndex] = nextValue;
                if (layerAutomationDragSampleIndex == 0 || layerAutomationDragSampleIndex == curve.Samples.Length - 1)
                {
                    curve.Samples[0] = nextValue;
                    curve.Samples[curve.Samples.Length - 1] = nextValue;
                }
            }
            SyncLayerAutomationLoopEndpoint(curve);
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp &&
                 layerAutomationDragLayerIndex == layerIndex &&
                 layerAutomationDragTarget == target)
        {
            layerAutomationDragLayerIndex = -1;
            layerAutomationDragTarget = LayerAutomationTarget.None;
            layerAutomationDragSampleIndex = -1;
            layerAutomationDragOutgoingHandle = false;
            evt.Use();
        }
    }

    private void DrawLayerScaleSettingsSection(int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        EnsureLayerAutomationCurves(layer);
        GUILayout.Label("Scale", titleStyle);
        GUILayout.Label("Each parameter can be Manual, FFT, or Envelope. Envelope follows the graph over 1 bar.", smallStyle);
        DrawLayerInputControlRow(layerIndex, "Opacity", MidiBindingAction.LayerOpacity, ref layer.Opacity, 0f, 1f, ref layer.OpacityInputMode, ref layer.OpacityFftAmount, ref layer.OpacityAutomation, ref layer.OpacityEnvelopeMin, ref layer.OpacityEnvelopeMax, 0f, LayerInputDisplayStyle.Percent, LayerAutomationTarget.Opacity);
        DrawLayerInputControlRow(layerIndex, "Scale", MidiBindingAction.LayerScale, ref layer.Scale, LayerScaleMin, LayerScaleMax, ref layer.ScaleInputMode, ref layer.ScaleFftAmount, ref layer.ScaleAutomation, ref layer.ScaleEnvelopeMin, ref layer.ScaleEnvelopeMax, 1f, LayerInputDisplayStyle.Multiplier, LayerAutomationTarget.Scale);
        DrawLayerInputControlRow(layerIndex, "Scale X", MidiBindingAction.LayerScaleX, ref layer.ScaleX, LayerScaleMin, LayerScaleMax, ref layer.ScaleXInputMode, ref layer.ScaleXFftAmount, ref layer.ScaleXAutomation, ref layer.ScaleXEnvelopeMin, ref layer.ScaleXEnvelopeMax, 1f, LayerInputDisplayStyle.Multiplier, LayerAutomationTarget.ScaleX);
        DrawLayerInputControlRow(layerIndex, "Scale Y", MidiBindingAction.LayerScaleY, ref layer.ScaleY, LayerScaleMin, LayerScaleMax, ref layer.ScaleYInputMode, ref layer.ScaleYFftAmount, ref layer.ScaleYAutomation, ref layer.ScaleYEnvelopeMin, ref layer.ScaleYEnvelopeMax, 1f, LayerInputDisplayStyle.Multiplier, LayerAutomationTarget.ScaleY);

        var selectedTarget = layerAutomationSelectedLayerIndex == layerIndex ? layerAutomationSelectedTarget : LayerAutomationTarget.None;
        LayerAutomationCurveState selectedCurve = null;
        switch (selectedTarget)
        {
            case LayerAutomationTarget.Opacity:
                if (layer.OpacityInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.OpacityAutomation;
                break;
            case LayerAutomationTarget.Scale:
                if (layer.ScaleInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.ScaleAutomation;
                break;
            case LayerAutomationTarget.ScaleX:
                if (layer.ScaleXInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.ScaleXAutomation;
                break;
            case LayerAutomationTarget.ScaleY:
                if (layer.ScaleYInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.ScaleYAutomation;
                break;
        }

        if (selectedCurve != null)
        {
            GUILayout.Space(8f);
            var graphRect = GUILayoutUtility.GetRect(1f, 170f, GUILayout.ExpandWidth(true));
            DrawLayerAutomationGraph(graphRect, layerIndex, selectedTarget, selectedCurve);
        }
    }

    private void DrawLayerInputControlRow(
        int layerIndex,
        string label,
        MidiBindingAction midiAction,
        ref float value,
        float minValue,
        float maxValue,
        ref LayerScaleInputMode mode,
        ref float fftAmount,
        ref LayerAutomationCurveState automation,
        ref float envelopeMin,
        ref float envelopeMax,
        float neutralValue,
        LayerInputDisplayStyle displayStyle,
        LayerAutomationTarget target)
    {
        if (automation == null || automation.Samples == null || automation.Samples.Length != LayerAutomationSampleCount)
        {
            automation = CreateDefaultLayerAutomationCurve();
        }
        EnsureLayerEnvelopeRange(ref envelopeMin, ref envelopeMax, value, neutralValue, minValue, maxValue);

        var rowRect = GUILayoutUtility.GetRect(1f, LayerInputRowHeight(layerIndex, target, mode), GUILayout.ExpandWidth(true));
        var labelRect = new Rect(rowRect.x, rowRect.y, 96f, 20f);
        var valueRect = new Rect(rowRect.x + 100f, rowRect.y, 90f, 20f);
        var modeRect = new Rect(rowRect.xMax - 108f, rowRect.y, 108f, 22f);
        var sliderRect = new Rect(rowRect.x + 194f, rowRect.y + 1f, Mathf.Max(120f, rowRect.width - 312f), 22f);
        var resolvedValue = ResolveLayerInputValue(value, mode, fftAmount, automation, envelopeMin, envelopeMax, neutralValue, minValue, maxValue);

        if (mode == LayerScaleInputMode.Envelope && Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            layerAutomationSelectedLayerIndex = layerIndex;
            layerAutomationSelectedTarget = target;
        }

        GUI.Label(labelRect, label, smallStyle);
        GUI.Label(
            valueRect,
            FormatLayerInputValue(mode == LayerScaleInputMode.Envelope ? resolvedValue : value, minValue, maxValue, displayStyle),
            smallStyle);

        var sliderInteractive = mode != LayerScaleInputMode.Envelope;
        var nextValue = DrawReadableHorizontalSlider(sliderRect, mode == LayerScaleInputMode.Envelope ? resolvedValue : value, minValue, maxValue, new Color(0.28f, 0.82f, 1f, 0.95f), sliderInteractive);
        if (sliderInteractive && Mathf.Abs(nextValue - value) > 0.0001f)
        {
            value = Mathf.Clamp(nextValue, minValue, maxValue);
        }

        if (GUI.Button(modeRect, LayerInputModeLabel(mode) + " v"))
        {
            if (layerInputModePopupLayerIndex == layerIndex && layerInputModePopupTarget == target)
            {
                layerInputModePopupLayerIndex = -1;
                layerInputModePopupTarget = LayerAutomationTarget.None;
                layerInputModePopupAnchorValid = false;
            }
            else
            {
                layerInputModePopupLayerIndex = layerIndex;
                layerInputModePopupTarget = target;
                layerInputModePopupButtonRect = GuiRectToScreenRect(modeRect);
                layerInputModePopupAnchorValid = true;
            }
        }

        if (layerInputModePopupLayerIndex == layerIndex && layerInputModePopupTarget == target)
        {
            layerInputModePopupButtonRect = GuiRectToScreenRect(modeRect);
            layerInputModePopupAnchorValid = true;
        }
        DrawLayerInputModePopup(modeRect, layerIndex, target, mode);
        if (midiEditMode)
        {
            DrawMidiLearnOverlay(sliderRect, midiAction, layerIndex, label);
        }

        if (mode == LayerScaleInputMode.Fft)
        {
            var fftLabelRect = new Rect(rowRect.x + 100f, rowRect.y + 28f, 90f, 18f);
            var fftRect = new Rect(rowRect.x + 194f, rowRect.y + 26f, Mathf.Max(120f, rowRect.width - 312f), 22f);
            GUI.Label(fftLabelRect, "FFT x" + fftAmount.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
            var nextAmount = DrawReadableHorizontalSlider(fftRect, fftAmount, 0f, 4f, new Color(1f, 0.72f, 0.22f, 0.95f));
            if (Mathf.Abs(nextAmount - fftAmount) > 0.0001f)
            {
                fftAmount = Mathf.Clamp(nextAmount, 0f, 4f);
            }
            GUI.Label(new Rect(fftRect.x, fftRect.yMax + 2f, fftRect.width, 16f), "Drive " + captureAudioFftDrive.ToString("0.00", CultureInfo.InvariantCulture) + "   " + (captureAudioFftEnabled ? "FFT ON" : "FFT OFF"), smallStyle);
        }
        else if (mode == LayerScaleInputMode.Envelope)
        {
            var active = layerAutomationSelectedLayerIndex == layerIndex && layerAutomationSelectedTarget == target;
            GUI.Label(new Rect(rowRect.x + 100f, rowRect.y + 28f, rowRect.width - 212f, 18f), active ? "Envelope editor selected below" : "Click this row to edit envelope below", smallStyle);

            var rangeLabelRect = new Rect(rowRect.x + 100f, rowRect.y + 48f, 94f, 18f);
            var rangeRect = new Rect(rowRect.x + 194f, rowRect.y + 46f, Mathf.Max(160f, rowRect.width - 312f), 22f);
            GUI.Label(rangeLabelRect, "Env Range", smallStyle);
            GUI.Label(new Rect(rangeRect.x, rangeRect.y - 14f, rangeRect.width * 0.5f, 14f), "Min " + FormatLayerInputValue(envelopeMin, minValue, maxValue, displayStyle), smallStyle);
            GUI.Label(new Rect(rangeRect.x + rangeRect.width * 0.5f, rangeRect.y - 14f, rangeRect.width * 0.5f, 14f), "Max " + FormatLayerInputValue(envelopeMax, minValue, maxValue, displayStyle), smallStyle);
            DrawReadableHorizontalRangeSlider(rangeRect, layerIndex, target, ref envelopeMin, ref envelopeMax, minValue, maxValue, new Color(0.76f, 0.86f, 1f, 0.92f));
        }
    }

    private readonly struct LayerSettingsActionButton
    {
        public readonly string Label;
        public readonly bool Visible;
        public readonly Action Action;

        public LayerSettingsActionButton(string label, bool visible, Action action)
        {
            Label = label;
            Visible = visible;
            Action = action;
        }
    }

    private void DrawLayerInteractiveSettingsSection(int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        settingsMonitorSelectedLayerIndex = layerIndex;

        GUILayout.Label("State", titleStyle);
        DrawLayerSettingsButtonGrid(
            new LayerSettingsActionButton(layer.Enabled ? "Enabled" : "Disabled", true, () => { layer.Enabled = !layer.Enabled; }),
            new LayerSettingsActionButton(layer.ProgramMuted ? "Mute ON" : "Mute OFF", true, () => { layer.ProgramMuted = !layer.ProgramMuted; }),
            new LayerSettingsActionButton(programSoloLayerIndex == layerIndex ? "Solo ON" : "Solo OFF", true, () =>
            {
                programSoloLayerIndex = programSoloLayerIndex == layerIndex ? -1 : layerIndex;
            }),
            new LayerSettingsActionButton(layer.AudioOutputEnabled ? "Audio ON" : "Audio OFF", SupportsLayerAudioToggle(layer), () =>
            {
                layer.AudioOutputEnabled = !layer.AudioOutputEnabled;
                ApplyLayerAudioOutputState(layer);
            })
        );

        GUILayout.Space(8f);
        GUILayout.Label("Blend Mode", titleStyle);
        DrawLayerSettingsButtonGrid(
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Alpha ? "> Alpha" : "Alpha", true, () => { layer.BlendMode = LayerBlendMode.Alpha; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Add ? "> Add" : "Add", true, () => { layer.BlendMode = LayerBlendMode.Add; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Screen ? "> Screen" : "Screen", true, () => { layer.BlendMode = LayerBlendMode.Screen; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Multiply ? "> Multiply" : "Multiply", true, () => { layer.BlendMode = LayerBlendMode.Multiply; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Lighten ? "> Lighten" : "Lighten", true, () => { layer.BlendMode = LayerBlendMode.Lighten; })
        );
        DrawLayerSettingsButtonGrid(
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Darken ? "> Darken" : "Darken", true, () => { layer.BlendMode = LayerBlendMode.Darken; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Difference ? "> Difference" : "Difference", true, () => { layer.BlendMode = LayerBlendMode.Difference; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Overlay ? "> Overlay" : "Overlay", true, () => { layer.BlendMode = LayerBlendMode.Overlay; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Subtract ? "> Subtract" : "Subtract", true, () => { layer.BlendMode = LayerBlendMode.Subtract; }),
            new LayerSettingsActionButton(layer.BlendMode == LayerBlendMode.Mask ? "> Mask" : "Mask", true, () => { layer.BlendMode = LayerBlendMode.Mask; })
        );

        GUILayout.Space(8f);
        GUILayout.Label("Color", titleStyle);
        DrawLayerSettingsButtonGrid(
            new LayerSettingsActionButton(IsLayerColorPresetActive(layer, LayerColorMode.None) ? "> Normal" : "Normal", true, () => { ApplyLayerColorPreset(layer, LayerColorMode.None); }),
            new LayerSettingsActionButton(IsLayerColorPresetActive(layer, LayerColorMode.Invert) ? "> Invert" : "Invert", true, () => { ApplyLayerColorPreset(layer, LayerColorMode.Invert); }),
            new LayerSettingsActionButton(IsLayerColorPresetActive(layer, LayerColorMode.Edge) ? "> Edge" : "Edge", true, () => { ApplyLayerColorPreset(layer, LayerColorMode.Edge); }),
            new LayerSettingsActionButton(IsLayerColorPresetActive(layer, LayerColorMode.Monochrome) ? "> B/W" : "B/W", true, () => { ApplyLayerColorPreset(layer, LayerColorMode.Monochrome); })
        );

        GUILayout.Space(8f);
        DrawLayerContinuousControlsSection(layerIndex, layer);
        GUILayout.Space(8f);
        DrawLayerSourceSpecificSettingsSection(layerIndex, layer);
        GUILayout.Space(8f);
        DrawLayerEffectSpecificSettingsSection(layerIndex, layer);
    }

    private void DrawLayerPreviewParameters(Rect rect, int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        var opacity = ResolveLayerInputValue(layer.Opacity, layer.OpacityInputMode, layer.OpacityFftAmount, layer.OpacityAutomation, layer.OpacityEnvelopeMin, layer.OpacityEnvelopeMax, 0f, 0f, 1f);
        var hueShift = ResolveLayerInputValue(layer.HueShift, layer.HueInputMode, layer.HueFftAmount, layer.HueAutomation, layer.HueEnvelopeMin, layer.HueEnvelopeMax, 0f, 0f, 1f);
        var invertAmount = ResolveLayerInputValue(layer.InvertAmount, layer.InvertInputMode, layer.InvertFftAmount, layer.InvertAutomation, layer.InvertEnvelopeMin, layer.InvertEnvelopeMax, 0f, 0f, 1f);
        var monochromeAmount = ResolveLayerInputValue(layer.MonochromeAmount, layer.MonochromeInputMode, layer.MonochromeFftAmount, layer.MonochromeAutomation, layer.MonochromeEnvelopeMin, layer.MonochromeEnvelopeMax, 0f, 0f, 1f);
        var scale = ResolveLayerInputValue(layer.Scale, layer.ScaleInputMode, layer.ScaleFftAmount, layer.ScaleAutomation, layer.ScaleEnvelopeMin, layer.ScaleEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);
        var scaleX = ResolveLayerInputValue(layer.ScaleX, layer.ScaleXInputMode, layer.ScaleXFftAmount, layer.ScaleXAutomation, layer.ScaleXEnvelopeMin, layer.ScaleXEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);
        var scaleY = ResolveLayerInputValue(layer.ScaleY, layer.ScaleYInputMode, layer.ScaleYFftAmount, layer.ScaleYAutomation, layer.ScaleYEnvelopeMin, layer.ScaleYEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), "Blend " + layer.BlendMode + "   Opacity " + Mathf.RoundToInt(opacity * 100f).ToString(CultureInfo.InvariantCulture) + "%", smallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 18f), "Hue " + (Mathf.Repeat(hueShift, 1f) * 360f).ToString("0", CultureInfo.InvariantCulture) + " deg   Invert " + invertAmount.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 36f, rect.width, 18f), "B/W " + monochromeAmount.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 54f, rect.width, 18f), "Scale " + scale.ToString("0.00", CultureInfo.InvariantCulture) + "   X " + scaleX.ToString("0.00", CultureInfo.InvariantCulture) + "   Y " + scaleY.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 72f, rect.width, 18f), "Mode  Op " + InputModeSummary(layer.OpacityInputMode, layer.OpacityFftAmount) + "   S " + InputModeSummary(layer.ScaleInputMode, layer.ScaleFftAmount) + "   X " + InputModeSummary(layer.ScaleXInputMode, layer.ScaleXFftAmount) + "   Y " + InputModeSummary(layer.ScaleYInputMode, layer.ScaleYFftAmount), smallStyle);
    }

    private static string InputModeSummary(LayerScaleInputMode mode, float amount)
    {
        switch (mode)
        {
            case LayerScaleInputMode.Fft:
                return "FFT x" + Mathf.Max(0f, amount).ToString("0.00", CultureInfo.InvariantCulture);
            case LayerScaleInputMode.Envelope:
                return "Env";
            default:
                return "Manual";
        }
    }

    private string DescribeLayerInputMode(LayerScaleInputMode mode, float amount)
    {
        switch (mode)
        {
            case LayerScaleInputMode.Fft:
                return "FFT x" + Mathf.Max(0f, amount).ToString("0.00", CultureInfo.InvariantCulture) + "  Drive " + captureAudioFftDrive.ToString("0.00", CultureInfo.InvariantCulture);
            case LayerScaleInputMode.Envelope:
                return "Envelope";
            default:
                return "Manual";
        }
    }

    private void DrawLayerContinuousControlsSection(int layerIndex, LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        GUILayout.Label("Continuous", titleStyle);
        EnsureLayerAutomationCurves(layer);
        DrawLayerInputControlRow(layerIndex, "Hue", MidiBindingAction.LayerHue, ref layer.HueShift, 0f, 1f, ref layer.HueInputMode, ref layer.HueFftAmount, ref layer.HueAutomation, ref layer.HueEnvelopeMin, ref layer.HueEnvelopeMax, 0f, LayerInputDisplayStyle.Degrees, LayerAutomationTarget.Hue);
        DrawLayerInputControlRow(layerIndex, "Invert", MidiBindingAction.LayerInvertAmount, ref layer.InvertAmount, 0f, 1f, ref layer.InvertInputMode, ref layer.InvertFftAmount, ref layer.InvertAutomation, ref layer.InvertEnvelopeMin, ref layer.InvertEnvelopeMax, 0f, LayerInputDisplayStyle.Scalar, LayerAutomationTarget.Invert);
        DrawLayerInputControlRow(layerIndex, "B/W", MidiBindingAction.LayerMonochromeAmount, ref layer.MonochromeAmount, 0f, 1f, ref layer.MonochromeInputMode, ref layer.MonochromeFftAmount, ref layer.MonochromeAutomation, ref layer.MonochromeEnvelopeMin, ref layer.MonochromeEnvelopeMax, 0f, LayerInputDisplayStyle.Scalar, LayerAutomationTarget.Monochrome);

        var selectedTarget = layerAutomationSelectedLayerIndex == layerIndex ? layerAutomationSelectedTarget : LayerAutomationTarget.None;
        LayerAutomationCurveState selectedCurve = null;
        switch (selectedTarget)
        {
            case LayerAutomationTarget.Hue:
                if (layer.HueInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.HueAutomation;
                break;
            case LayerAutomationTarget.Invert:
                if (layer.InvertInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.InvertAutomation;
                break;
            case LayerAutomationTarget.Monochrome:
                if (layer.MonochromeInputMode == LayerScaleInputMode.Envelope) selectedCurve = layer.MonochromeAutomation;
                break;
        }

        if (selectedCurve != null)
        {
            GUILayout.Space(8f);
            var graphRect = GUILayoutUtility.GetRect(1f, 170f, GUILayout.ExpandWidth(true));
            DrawLayerAutomationGraph(graphRect, layerIndex, selectedTarget, selectedCurve);
        }
    }

    private static bool SupportsLayerAudioToggle(LayerState layer)
    {
        return layer != null && (layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube);
    }

    private void DrawLayerSourceSpecificSettingsSection(int layerIndex, LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        GUILayout.Label("Source", titleStyle);
        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                DrawLayerSettingsButtonGrid(
                    new LayerSettingsActionButton(layer.VideoMode == VideoPlaybackMode.Bpm ? "> BPM" : "BPM", true, () => { layer.VideoMode = VideoPlaybackMode.Bpm; layer.VideoResyncPending = false; }),
                    new LayerSettingsActionButton(layer.VideoMode == VideoPlaybackMode.Timeline ? "> Timeline" : "Timeline", true, () => { layer.VideoMode = VideoPlaybackMode.Timeline; layer.VideoResyncPending = false; }),
                    new LayerSettingsActionButton("Resync", true, () => { layer.VideoResyncPending = true; }),
                    new LayerSettingsActionButton("Restart", layer.Player != null, () => { layer.Player.time = 0; layer.Player.Play(); }),
                    new LayerSettingsActionButton("Reconnect", true, () => ReloadLayerMediaSource(layerIndex, layer)),
                    new LayerSettingsActionButton("Reload", true, () => ReloadLayerMediaSource(layerIndex, layer)),
                    new LayerSettingsActionButton("Pre-cache", true, () => BeginMediaPrecache(layer, layer.Path)),
                    new LayerSettingsActionButton("90", true, () => { layer.VideoBaseBpm = 90f; layer.VideoBaseBpmInput = "90"; }),
                    new LayerSettingsActionButton("100", true, () => { layer.VideoBaseBpm = 100f; layer.VideoBaseBpmInput = "100"; }),
                    new LayerSettingsActionButton("120", true, () => { layer.VideoBaseBpm = 120f; layer.VideoBaseBpmInput = "120"; }),
                    new LayerSettingsActionButton("128", true, () => { layer.VideoBaseBpm = 128f; layer.VideoBaseBpmInput = "128"; }),
                    new LayerSettingsActionButton("140", true, () => { layer.VideoBaseBpm = 140f; layer.VideoBaseBpmInput = "140"; })
                );
                GUILayout.Label("Media: " + PreferText(LayerDisplayStatus(layer), "Online"), smallStyle);
                GUILayout.Label("Cache: " + (!string.IsNullOrEmpty(layer.CachedMediaPath) && File.Exists(layer.CachedMediaPath) ? "Ready" : (layer.MediaPrecacheRequested ? "Queued" : "Off")), smallStyle);
                var syncBpm = CurrentVisualBpm();
                var clipLengthSeconds = LayerPlaybackLengthSeconds(layer);
                var clipTimeSeconds = LayerPlaybackTimeSeconds(layer);
                var clipSpeed = Mathf.Clamp(syncBpm / Mathf.Max(1f, layer.VideoBaseBpm), 0.05f, 16f);
                GUILayout.Label("Sync BPM: " + syncBpm.ToString("0.0", CultureInfo.InvariantCulture), smallStyle);
                GUILayout.Label("Clip Time: " +
                                clipTimeSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                                " / " +
                                (clipLengthSeconds > 0.001
                                    ? clipLengthSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s"
                                    : "--"),
                    smallStyle);
                GUILayout.Label("Playback Speed: x" +
                                (layer.VideoMode == VideoPlaybackMode.Bpm
                                    ? clipSpeed.ToString("0.00", CultureInfo.InvariantCulture)
                                    : "1.00"),
                    smallStyle);
                if (!string.IsNullOrEmpty(layer.CachedMediaPath))
                {
                    GUILayout.Label("Cache path: " + layer.CachedMediaPath, smallStyle);
                }
                break;

            case LayerSourceKind.Image:
                DrawLayerSettingsButtonGrid(
                    new LayerSettingsActionButton("Reconnect", true, () => ReloadLayerMediaSource(layerIndex, layer)),
                    new LayerSettingsActionButton("Reload", true, () => ReloadLayerMediaSource(layerIndex, layer)),
                    new LayerSettingsActionButton("Pre-cache", true, () => BeginMediaPrecache(layer, layer.Path))
                );
                GUILayout.Label("Media: " + PreferText(LayerDisplayStatus(layer), "Online"), smallStyle);
                GUILayout.Label("Cache: " + (!string.IsNullOrEmpty(layer.CachedMediaPath) && File.Exists(layer.CachedMediaPath) ? "Ready" : (layer.MediaPrecacheRequested ? "Queued" : "Off")), smallStyle);
                break;

            case LayerSourceKind.YouTube:
                if (layer.YouTube == null)
                {
                    GUILayout.Label("No YouTube state.", smallStyle);
                    return;
                }

                DrawLayerSettingsButtonGrid(
                    new LayerSettingsActionButton(layer.YouTube.PlaybackMode == YoutubePlaybackMode.UrlCompatible ? "> URL" : "URL", true, () => SetLayerYoutubePlaybackMode(layerIndex, layer, YoutubePlaybackMode.UrlCompatible)),
                    new LayerSettingsActionButton(layer.YouTube.PlaybackMode == YoutubePlaybackMode.UrlBest ? "> Best" : "Best", true, () => SetLayerYoutubePlaybackMode(layerIndex, layer, YoutubePlaybackMode.UrlBest)),
                    new LayerSettingsActionButton(layer.YouTube.PlaybackMode == YoutubePlaybackMode.LocalCache ? "> Cache" : "Cache", true, () => SetLayerYoutubePlaybackMode(layerIndex, layer, YoutubePlaybackMode.LocalCache)),
                    new LayerSettingsActionButton(layer.Player != null && layer.Player.isPlaying ? "Pause" : "Play", layer.Player != null, () =>
                    {
                        if (layer.Player.isPlaying) layer.Player.Pause(); else layer.Player.Play();
                    }),

                    new LayerSettingsActionButton("Restart", layer.Player != null, () => { layer.Player.time = 0; layer.Player.Play(); }),
                    new LayerSettingsActionButton("Search", true, () => OpenYoutubeSearchForLayer(layerIndex, false)),
                    new LayerSettingsActionButton("Seek 0", layer.Player != null, () => { layer.Player.time = 0; }),
                    new LayerSettingsActionButton("Align", true, () => AutoAlignYoutubeToPlayer(layer, Snapshot())),

                    new LayerSettingsActionButton("x0.5", true, () => SetLayerYoutubeSpeed(layer, 0.5f)),
                    new LayerSettingsActionButton("x1.0", true, () => SetLayerYoutubeSpeed(layer, 1f)),
                    new LayerSettingsActionButton("x1.5", true, () => SetLayerYoutubeSpeed(layer, 1.5f)),
                    new LayerSettingsActionButton(layer.YouTube.WaveformPlayerNumber == 1 ? "> P1" : "P1", true, () => { layer.YouTube.WaveformPlayerNumber = 1; }),
                    new LayerSettingsActionButton(layer.YouTube.WaveformPlayerNumber == 2 ? "> P2" : "P2", true, () => { layer.YouTube.WaveformPlayerNumber = 2; }),
                    new LayerSettingsActionButton(layer.YouTube.WaveformPlayerNumber == 3 ? "> P3" : "P3", true, () => { layer.YouTube.WaveformPlayerNumber = 3; }),
                    new LayerSettingsActionButton(layer.YouTube.WaveformPlayerNumber == 4 ? "> P4" : "P4", true, () => { layer.YouTube.WaveformPlayerNumber = 4; })
                );

                DrawLayerYoutubeContinuousControls(layer);
                break;

            case LayerSourceKind.Text:
                DrawLayerSettingsButtonGrid(
                    new LayerSettingsActionButton("Size -", true, () =>
                    {
                        layer.TextFontSize = Mathf.Clamp(layer.TextFontSize - 8, 12, 256);
                        layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                        UpdateTextLayerTexture(layer);
                    }),
                    new LayerSettingsActionButton("Size +", true, () =>
                    {
                        layer.TextFontSize = Mathf.Clamp(layer.TextFontSize + 8, 12, 256);
                        layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
                        UpdateTextLayerTexture(layer);
                    }),
                    new LayerSettingsActionButton("Font Prev", true, () => CycleTextFont(layer, -1)),
                    new LayerSettingsActionButton("Font Next", true, () => CycleTextFont(layer, 1)),
                    new LayerSettingsActionButton("TEXT", true, () =>
                    {
                        layer.TextContent = "TEXT";
                        layer.TextInput = "TEXT";
                        UpdateTextLayerTexture(layer);
                    })
                );
                break;

            case LayerSourceKind.Generator3D:
                if (layer.Generator == null)
                {
                    GUILayout.Label("No 3D generator state.", smallStyle);
                    return;
                }
                DrawGeneratorDetailControls(layer.Generator, Snapshot());
                break;

            default:
                GUILayout.Label("No source-specific controls.", smallStyle);
                break;
        }
    }

    private void DrawLayerEffectSpecificSettingsSection(int layerIndex, LayerState layer)
    {
        if (layer == null || layer.Effect == null || !layer.Effect.HasEffect)
        {
            return;
        }

        var effect = layer.Effect;
        GUILayout.Label("Effect", titleStyle);
        var buttons = new List<LayerSettingsActionButton>
        {
            new LayerSettingsActionButton(effect.Enabled ? "Disable" : "Enable", true, () => { effect.Enabled = !effect.Enabled; }),
            new LayerSettingsActionButton("Clear", true, () =>
            {
                StopLayerVideoPlayback(layer);
                ResetLayerEffectState(layer);
                layer.SourceKind = LayerSourceKind.None;
                layer.SourceOrigin = LayerSourceOrigin.None;
                layer.SourceName = null;
                layer.EffectRenderedOutput = null;
                layer.EffectRenderedFrame = -1;
            })
        };

        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                buttons.Add(new LayerSettingsActionButton(effect.RgbMode == LayerRgbEffectMode.RedOnly ? "> Red" : "Red", true, () => { effect.RgbMode = LayerRgbEffectMode.RedOnly; }));
                buttons.Add(new LayerSettingsActionButton(effect.RgbMode == LayerRgbEffectMode.GreenOnly ? "> Green" : "Green", true, () => { effect.RgbMode = LayerRgbEffectMode.GreenOnly; }));
                buttons.Add(new LayerSettingsActionButton(effect.RgbMode == LayerRgbEffectMode.BlueOnly ? "> Blue" : "Blue", true, () => { effect.RgbMode = LayerRgbEffectMode.BlueOnly; }));
                buttons.Add(new LayerSettingsActionButton(effect.RgbMode == LayerRgbEffectMode.RgbInvert ? "> Invert" : "Invert", true, () => { effect.RgbMode = LayerRgbEffectMode.RgbInvert; }));
                buttons.Add(new LayerSettingsActionButton(effect.RgbMode == LayerRgbEffectMode.Monochrome ? "> B/W" : "B/W", true, () => { effect.RgbMode = LayerRgbEffectMode.Monochrome; }));
                break;

            case LayerEffectKind.Blur:
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Horizontal ? "> H" : "H", true, () => { effect.Mode = LayerEffectMode.Horizontal; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Vertical ? "> V" : "V", true, () => { effect.Mode = LayerEffectMode.Vertical; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Alternate ? "> H>V" : "H>V", true, () => { effect.Mode = LayerEffectMode.Alternate; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Zoom ? "> Zoom" : "Zoom", true, () => { effect.Mode = LayerEffectMode.Zoom; }));
                break;

            case LayerEffectKind.Mirror:
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Horizontal ? "> H" : "H", true, () => { effect.Mode = LayerEffectMode.Horizontal; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Vertical ? "> V" : "V", true, () => { effect.Mode = LayerEffectMode.Vertical; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Alternate ? "> Quad" : "Quad", true, () => { effect.Mode = LayerEffectMode.Alternate; }));
                break;

            case LayerEffectKind.Strobe:
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Beat1 ? "> 1" : "1", true, () => { effect.Mode = LayerEffectMode.Beat1; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Beat2 ? "> 1/2" : "1/2", true, () => { effect.Mode = LayerEffectMode.Beat2; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Beat4 ? "> 1/4" : "1/4", true, () => { effect.Mode = LayerEffectMode.Beat4; }));
                buttons.Add(new LayerSettingsActionButton(effect.Mode == LayerEffectMode.Manual ? "> Manual" : "Manual", true, () => { effect.Mode = LayerEffectMode.Manual; }));
                if (effect.Mode == LayerEffectMode.Manual)
                {
                    buttons.Add(new LayerSettingsActionButton(effect.ManualFlashHeld ? "HOLD ON" : "HOLD", true, () => { effect.ManualFlashHeld = !effect.ManualFlashHeld; }));
                }
                break;

            case LayerEffectKind.StepSequencer:
                buttons.Add(new LayerSettingsActionButton("QueueClr", true, () =>
                {
                    if (effect.StepSequenceQueue != null)
                    {
                        effect.StepSequenceQueue.Clear();
                    }
                    SyncStepSequencerMediaSlots(layerIndex, effect);
                }));
                buttons.Add(new LayerSettingsActionButton(effect.StepSequenceLength == 2 ? "> 2" : "2", true, () => { effect.StepSequenceLength = 2; }));
                buttons.Add(new LayerSettingsActionButton(effect.StepSequenceLength == 4 ? "> 4" : "4", true, () => { effect.StepSequenceLength = 4; }));
                break;
        }

        if (effect.Kind == LayerEffectKind.Blur ||
            effect.Kind == LayerEffectKind.Glitch ||
            effect.Kind == LayerEffectKind.RgbEffect ||
            effect.Kind == LayerEffectKind.Kaleido ||
            effect.Kind == LayerEffectKind.Pixelate)
        {
            buttons.Add(new LayerSettingsActionButton("Amt -", true, () => { effect.Intensity = Mathf.Clamp01(effect.Intensity - 0.1f); }));
            buttons.Add(new LayerSettingsActionButton("Amt +", true, () => { effect.Intensity = Mathf.Clamp01(effect.Intensity + 0.1f); }));
        }

        DrawLayerSettingsButtonGrid(buttons.ToArray());
    }

    private void DrawLayerYoutubeContinuousControls(LayerState layer)
    {
        if (layer == null || layer.YouTube == null)
        {
            return;
        }

        GUILayout.Space(6f);
        GUILayout.Label("YouTube Detail", smallStyle);
        GUILayout.Label("Title: " + PreferText(layer.YouTube.Title, layer.YouTube.VideoId), smallStyle);
        if (!string.IsNullOrEmpty(layer.YouTube.Author))
        {
            GUILayout.Label("Author: " + layer.YouTube.Author, smallStyle);
        }
        GUILayout.Label("Status: " + PreferText(layer.YouTube.AlignmentStatus, PreferText(layer.YouTube.Error, layer.VideoStatus)), smallStyle);

        var length = layer.Player != null && layer.Player.length > 0.001 ? layer.Player.length : layer.YouTube.KnownLengthSeconds;
        if (layer.Player != null && length > 0.001)
        {
            GUILayout.Label("Position  " + layer.Player.time.ToString("0.0", CultureInfo.InvariantCulture) + " / " + length.ToString("0.0", CultureInfo.InvariantCulture) + " s", smallStyle);
            var nextTime = GUILayout.HorizontalSlider((float)layer.Player.time, 0f, (float)length, GUILayout.Width(340f));
            if (Mathf.Abs(nextTime - (float)layer.Player.time) > 0.05f)
            {
                layer.Player.time = nextTime;
                layer.YouTube.TimeInput = nextTime.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        GUILayout.Label("Speed  x" + layer.YouTube.PlaybackSpeed.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
        var nextSpeed = GUILayout.HorizontalSlider(layer.YouTube.PlaybackSpeed, 0.05f, 4f, GUILayout.Width(340f));
        if (Mathf.Abs(nextSpeed - layer.YouTube.PlaybackSpeed) > 0.0001f)
        {
            SetLayerYoutubeSpeed(layer, nextSpeed);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Time", GUILayout.Width(46f));
        layer.YouTube.TimeInput = GUILayout.TextField(layer.YouTube.TimeInput ?? "0.0", GUILayout.Width(96f));
        if (GUILayout.Button("Apply", GUILayout.Width(70f)))
        {
            double seconds;
            if (double.TryParse(layer.YouTube.TimeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && layer.Player != null)
            {
                layer.Player.time = Math.Max(0.0, seconds);
                layer.YouTube.TimeInput = Math.Max(0.0, seconds).ToString("0.0", CultureInfo.InvariantCulture);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Speed", GUILayout.Width(46f));
        layer.YouTube.SpeedInput = GUILayout.TextField(layer.YouTube.SpeedInput ?? "1.00", GUILayout.Width(96f));
        if (GUILayout.Button("Apply", GUILayout.Width(70f)))
        {
            float speed;
            if (float.TryParse(layer.YouTube.SpeedInput, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
            {
                SetLayerYoutubeSpeed(layer, Mathf.Clamp(speed, 0.05f, 4f));
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLayerGeneratorExtendedControls(int layerIndex, LayerState layer)
    {
        if (layer == null || layer.Generator == null)
        {
            return;
        }

        var generator = layer.Generator;
        GUILayout.Space(6f);
        GUILayout.Label("3D Object Detail", smallStyle);
        GUILayout.Label("Preset: " + PreferText(generator.PresetNameInput, "(none)"), smallStyle);
        GUILayout.Label("Model: " + PreferText(generator.ModelName, "None"), smallStyle);
        GUILayout.Label("Skybox: " + PreferText(generator.SkyboxName, "None"), smallStyle);
        GUILayout.Label("Face: " + DescribeGeneratorFaceTextureSource(generator), smallStyle);
        GUILayout.Label("Screen Source: " + (generator.SurroundScreenSourceLayerIndex >= 0 ? LayerSlotLabel(generator.SurroundScreenSourceLayerIndex) : "None"), smallStyle);

        GUILayout.Label("Camera distance  " + generator.CameraDistance.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);
        var nextCameraDistance = GUILayout.HorizontalSlider(generator.CameraDistance, 0.25f, 12f, GUILayout.Width(340f));
        if (Mathf.Abs(nextCameraDistance - generator.CameraDistance) > 0.0001f)
        {
            generator.CameraDistance = nextCameraDistance;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Preset", GUILayout.Width(52f));
        generator.PresetNameInput = GUILayout.TextField(generator.PresetNameInput ?? "Preset", GUILayout.Width(180f));
        if (GUILayout.Button("Save", GUILayout.Width(70f)))
        {
            SaveGeneratorPreset(generator, layer);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Pos", GUILayout.Width(36f));
        generator.ModelPositionInput = GUILayout.TextField(generator.ModelPositionInput ?? "0,0,0", GUILayout.Width(120f));
        GUILayout.Label("Rot", GUILayout.Width(36f));
        generator.ModelRotationInput = GUILayout.TextField(generator.ModelRotationInput ?? "0,0,0", GUILayout.Width(120f));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Scale", GUILayout.Width(36f));
        generator.ModelScaleInput = GUILayout.TextField(generator.ModelScaleInput ?? "1,1,1", GUILayout.Width(120f));
        if (GUILayout.Button("Apply Xf", GUILayout.Width(86f)))
        {
            ApplyGeneratorModelTransform(generator);
        }
        GUILayout.EndHorizontal();

        generator.FaceImageFolderInput = generator.FaceImageFolderInput ?? generator.FaceImageFolderPath ?? "";
        GUILayout.BeginHorizontal();
        GUILayout.Label("Face Folder", GUILayout.Width(76f));
        generator.FaceImageFolderInput = GUILayout.TextField(generator.FaceImageFolderInput, GUILayout.Width(210f));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Folder...", GUILayout.Width(86f)))
        {
            OpenGeneratorFaceImageFolderDialog(generator);
        }
        if (GUILayout.Button("Apply Folder", GUILayout.Width(96f)))
        {
            generator.FaceImageFolderPath = generator.FaceImageFolderInput;
            RefreshGeneratorFaceImageFolder(generator);
            SaveGeneratorFaceFolderPreference(generator.FaceImageFolderPath);
        }
        if (GUILayout.Button("Refresh Face", GUILayout.Width(96f)))
        {
            RefreshGeneratorFaceImageFolder(generator);
        }
        GUILayout.EndHorizontal();
    }

    private void SetLayerYoutubePlaybackMode(int layerIndex, LayerState layer, YoutubePlaybackMode mode)
    {
        if (layer == null || layer.YouTube == null)
        {
            return;
        }

        layer.YouTube.PlaybackMode = mode;
        layer.YouTube.Error = null;
        ResolveLayerYoutube(layerIndex, layer);
    }

    private void ResolveLayerYoutube(int layerIndex, LayerState layer)
    {
        if (layer == null || layer.YouTube == null || string.IsNullOrEmpty(layer.YouTube.Url))
        {
            return;
        }

        layer.YouTube.Resolving = true;
        layer.YouTube.Error = null;
        StartCoroutine(ResolveYoutubeVideo(layerIndex, layer.YouTube.Url));
    }

    private static void SetLayerYoutubeSpeed(LayerState layer, float speed)
    {
        if (layer == null || layer.YouTube == null)
        {
            return;
        }

        layer.YouTube.PlaybackSpeed = speed;
        layer.YouTube.SpeedInput = speed.ToString("0.00", CultureInfo.InvariantCulture);
        if (layer.Player != null)
        {
            layer.Player.playbackSpeed = speed;
        }
    }

    private void DrawLayerSettingsButtonGrid(params LayerSettingsActionButton[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
        {
            return;
        }

        const int columns = 4;
        var visibleButtons = new List<LayerSettingsActionButton>();
        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].Visible)
            {
                visibleButtons.Add(buttons[i]);
            }
        }

        for (var start = 0; start < visibleButtons.Count; start += columns)
        {
            GUILayout.BeginHorizontal();
            for (var column = 0; column < columns; column++)
            {
                var index = start + column;
                if (index >= visibleButtons.Count)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                var button = visibleButtons[index];
                if (GUILayout.Button(button.Label, GUILayout.Width(120f), GUILayout.Height(26f)) && button.Action != null)
                {
                    button.Action();
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    private Material EnsureLayerCompositeMaterial()
    {
        if (layerCompositeMaterial == null)
        {
            var shader = Shader.Find("BeatLink/LayerComposite");
            if (shader != null)
            {
                layerCompositeMaterial = new Material(shader);
            }
        }

        return layerCompositeMaterial;
    }

    private Material EnsureLayerMaskCompositeMaterial()
    {
        if (layerMaskCompositeMaterial == null)
        {
            var shader = Shader.Find("BeatLink/LayerMaskComposite");
            if (shader != null)
            {
                layerMaskCompositeMaterial = new Material(shader);
            }
        }

        return layerMaskCompositeMaterial;
    }

    private Material EnsureLayerEffectMaterial()
    {
        if (layerEffectMaterial == null)
        {
            var shader = Shader.Find("BeatLink/LayerEffect");
            if (shader != null)
            {
                layerEffectMaterial = new Material(shader);
            }
        }

        return layerEffectMaterial;
    }

    private List<StepSequencerEntry> CollectValidStepSequencerQueue(LayerEffectState effect, int hostLayerIndex)
    {
        var result = new List<StepSequencerEntry>();
        if (effect == null || effect.StepSequenceQueue == null)
        {
            return result;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count && result.Count < StepSequencerMaxMediaLayers; i++)
        {
            var entry = effect.StepSequenceQueue[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.Kind == StepSequencerEntryKind.Layer)
            {
                if (entry.LayerIndex < 0 || entry.LayerIndex >= VjLayerCount || entry.LayerIndex == hostLayerIndex)
                {
                    continue;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(entry.Path) || !File.Exists(entry.Path))
                {
                    continue;
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private void SyncStepSequencerMediaSlots(int hostLayerIndex, LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        var queue = CollectValidStepSequencerQueue(effect, hostLayerIndex);
        if (effect.StepSequenceMediaLayers == null || effect.StepSequenceMediaLayers.Length != StepSequencerMaxMediaLayers)
        {
            effect.StepSequenceMediaLayers = new LayerState[StepSequencerMaxMediaLayers];
        }

        var mediaSlot = 0;
        for (var i = 0; i < queue.Count && mediaSlot < StepSequencerMaxMediaLayers; i++)
        {
            var entry = queue[i];
            if (entry.Kind != StepSequencerEntryKind.MediaFile)
            {
                continue;
            }

            if (effect.StepSequenceMediaLayers[mediaSlot] == null)
            {
                effect.StepSequenceMediaLayers[mediaSlot] = CreateStepSequencerMediaSlotState("Step Sequencer Media " + (mediaSlot + 1).ToString(CultureInfo.InvariantCulture), outputWidth, outputHeight);
            }

            var slotLayer = effect.StepSequenceMediaLayers[mediaSlot];
            if (slotLayer == null || string.Equals(slotLayer.Path, entry.Path, StringComparison.OrdinalIgnoreCase))
            {
                mediaSlot++;
                continue;
            }

            if (IsImageFile(entry.Path))
            {
                LoadImageIntoLayerState(slotLayer, entry.Path, LayerSourceOrigin.FileBrowser, Path.GetFileName(entry.Path), false, null);
            }
            else
            {
                LoadVideoIntoLayerState(slotLayer, entry.Path, LayerSourceOrigin.FileBrowser, Path.GetFileName(entry.Path), false, null);
            }

            mediaSlot++;
        }

        for (var i = mediaSlot; i < effect.StepSequenceMediaLayers.Length; i++)
        {
            var slot = effect.StepSequenceMediaLayers[i];
            if (slot == null)
            {
                continue;
            }
            DestroyLayer(slot);
            effect.StepSequenceMediaLayers[i] = null;
        }
    }

    private Texture LayerPreviewTextureForEffectInput(int layerIndex, LayerState layer)
    {
        return ResolveLayerSourceTexture(layerIndex, layer);
    }

    private Texture ResolveStepSequencerMediaTexture(LayerEffectState effect, string path)
    {
        if (effect == null || effect.StepSequenceMediaLayers == null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        for (var i = 0; i < effect.StepSequenceMediaLayers.Length; i++)
        {
            var slot = effect.StepSequenceMediaLayers[i];
            if (slot == null || !string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            return ResolveLayerSourceTexture(-1, slot);
        }

        return null;
    }

    private void ConfigureLayerEffectMaterial(Material material, LayerEffectState effect, float beatFloat, float bpm)
    {
        if (material == null || effect == null)
        {
            return;
        }

        var modeValue = 0f;
        var axisValue = 0f;
        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                modeValue = 1f + Mathf.Clamp((int)effect.RgbMode, 0, 5);
                break;
            case LayerEffectKind.Blur:
                modeValue = 7f;
                axisValue = EffectAxisValue(effect.Mode);
                break;
            case LayerEffectKind.Glitch:
                modeValue = 8f;
                break;
            case LayerEffectKind.Mirror:
                modeValue = 11f;
                axisValue = EffectAxisValue(effect.Mode);
                break;
            case LayerEffectKind.Kaleido:
                modeValue = 12f;
                break;
            case LayerEffectKind.Pixelate:
                modeValue = 13f;
                break;
            case LayerEffectKind.Strobe:
                modeValue = 9f;
                break;
            case LayerEffectKind.QuadSplit:
                modeValue = 10f;
                break;
        }

        material.SetFloat("_Mode", modeValue);
        material.SetFloat("_Intensity", Mathf.Clamp01(effect.Intensity));
        material.SetFloat("_Axis", axisValue);
        material.SetFloat("_Phase", Mathf.Repeat(beatFloat * 0.25f, 1f));
        material.SetFloat("_TimeNow", Time.realtimeSinceStartup);
        material.SetFloat("_StrobePhase", Mathf.Repeat(beatFloat, 1f));
        material.SetFloat("_ManualHold", effect.ManualFlashHeld ? 1f : 0f);
        material.SetFloat("_BeatCount", beatFloat);
    }

    private static bool EffectUsesLowerLayerCompositeInput(int hostIndex, LayerEffectState effect)
    {
        return effect != null &&
               effect.Kind != LayerEffectKind.None &&
               effect.Kind != LayerEffectKind.Strobe &&
               effect.Kind != LayerEffectKind.StepSequencer;
    }

    private Texture BuildLowerLayerCompositeInput(int hostIndex, LayerState hostLayer)
    {
        if (vjLayers == null || hostIndex <= 0)
        {
            return null;
        }
        return BuildMainLayerCompositeRange(0, hostIndex);
    }

    private void ApplyLayerMaskComposite(RenderTexture lowerComposite, RenderTexture upperComposite, Texture maskTexture, LayerState layer, RenderTexture target)
    {
        if (lowerComposite == null || upperComposite == null || target == null)
        {
            return;
        }

        var material = EnsureLayerMaskCompositeMaterial();
        if (material == null)
        {
            Graphics.Blit(upperComposite, target);
            return;
        }

        material.SetTexture("_MainTex", lowerComposite);
        material.SetTexture("_UpperTex", upperComposite);
        material.SetTexture("_MaskTex", maskTexture == null ? Texture2D.blackTexture : maskTexture);
        material.SetFloat("_MaskOpacity", layer == null ? 1f : Mathf.Clamp01(layer.Opacity));
        Graphics.Blit(lowerComposite, target, material);
    }

    private void ApplyLayerCompositeProperties(Material material, Texture overlayTexture, LayerState layer, float opacityScale, LayerBlendMode blendMode)
    {
        if (material == null)
        {
            return;
        }

        material.SetTexture("_OverlayTex", overlayTexture == null ? Texture2D.blackTexture : overlayTexture);
        if (layer != null)
        {
            EnsureLayerAutomationCurves(layer);
        }
        var opacity = layer == null ? 1f : ResolveLayerInputValue(layer.Opacity, layer.OpacityInputMode, layer.OpacityFftAmount, layer.OpacityAutomation, layer.OpacityEnvelopeMin, layer.OpacityEnvelopeMax, 0f, 0f, 1f);
        material.SetFloat("_Opacity", Mathf.Clamp01(opacity * opacityScale));
        material.SetFloat("_BlendMode", BlendModeToShaderValue(blendMode));
        var hueShift = layer == null ? 0f : ResolveLayerInputValue(layer.HueShift, layer.HueInputMode, layer.HueFftAmount, layer.HueAutomation, layer.HueEnvelopeMin, layer.HueEnvelopeMax, 0f, 0f, 1f);
        material.SetFloat("_HueShift", Mathf.Repeat(hueShift, 1f));
        material.SetFloat("_ColorMode", layer == null ? 0f : (float)layer.ColorMode);
        var invertAmount = layer == null ? 0f : ResolveLayerInputValue(layer.InvertAmount, layer.InvertInputMode, layer.InvertFftAmount, layer.InvertAutomation, layer.InvertEnvelopeMin, layer.InvertEnvelopeMax, 0f, 0f, 1f);
        var monochromeAmount = layer == null ? 0f : ResolveLayerInputValue(layer.MonochromeAmount, layer.MonochromeInputMode, layer.MonochromeFftAmount, layer.MonochromeAutomation, layer.MonochromeEnvelopeMin, layer.MonochromeEnvelopeMax, 0f, 0f, 1f);
        material.SetFloat("_InvertAmount", Mathf.Clamp01(invertAmount));
        material.SetFloat("_MonochromeAmount", Mathf.Clamp01(monochromeAmount));

        var scale = layer == null ? 1f : ResolveLayerInputValue(layer.Scale, layer.ScaleInputMode, layer.ScaleFftAmount, layer.ScaleAutomation, layer.ScaleEnvelopeMin, layer.ScaleEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);
        var scaleX = layer == null ? 1f : ResolveLayerInputValue(layer.ScaleX, layer.ScaleXInputMode, layer.ScaleXFftAmount, layer.ScaleXAutomation, layer.ScaleXEnvelopeMin, layer.ScaleXEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);
        var scaleY = layer == null ? 1f : ResolveLayerInputValue(layer.ScaleY, layer.ScaleYInputMode, layer.ScaleYFftAmount, layer.ScaleYAutomation, layer.ScaleYEnvelopeMin, layer.ScaleYEnvelopeMax, 1f, LayerScaleMin, LayerScaleMax);
        material.SetFloat("_Scale", Mathf.Clamp(scale, LayerScaleMin, LayerScaleMax));
        material.SetFloat("_ScaleX", Mathf.Clamp(scale * scaleX, LayerScaleMin, LayerScaleMax));
        material.SetFloat("_ScaleY", Mathf.Clamp(scale * scaleY, LayerScaleMin, LayerScaleMax));
    }

    private int FindOutputOptionIndex(List<OutputDeviceOption> options, string id)
    {
        if (options == null || options.Count == 0)
        {
            return 0;
        }

        if (string.IsNullOrEmpty(id))
        {
            return 0;
        }

        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private static void ApplyBlendModeToMaterial(Material material, LayerBlendMode blendMode)
    {
        if (material == null)
        {
            return;
        }

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (blendMode == LayerBlendMode.Add50 || blendMode == LayerBlendMode.Add)
        {
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
    }

    private void DestroyStepSequencerMediaSlots(LayerEffectState effect)
    {
        if (effect == null || effect.StepSequenceMediaLayers == null)
        {
            return;
        }

        for (var i = 0; i < effect.StepSequenceMediaLayers.Length; i++)
        {
            var layer = effect.StepSequenceMediaLayers[i];
            if (layer != null)
            {
                DestroyLayer(layer);
                effect.StepSequenceMediaLayers[i] = null;
            }
        }
    }

    private void ClearStepSequencerMediaState(LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        effect.ActiveStepMediaPath = null;
        if (effect.ActiveStepImageOwnsTexture && effect.ActiveStepImageTexture != null)
        {
            Destroy(effect.ActiveStepImageTexture);
        }
        effect.ActiveStepImageTexture = null;
        effect.ActiveStepImageOwnsTexture = false;
        if (effect.StepSequenceQueue != null)
        {
            effect.StepSequenceQueue.Clear();
        }
    }

    private void ResyncAllVideoLayers()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer == null)
            {
                continue;
            }

            if (layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube)
            {
                ResyncVideoLayerToCurrentBeat(layer);
            }
        }
    }

    private void RefreshOutputDevices()
    {
        if (!outputDevicesDirty && videoOutputDevices.Count > 0 && displayRouteDevices.Count > 0)
        {
            return;
        }

        outputDevicesDirty = false;

        var selectedVideoId = selectedVideoOutputIndex >= 0 && selectedVideoOutputIndex < videoOutputDevices.Count
            ? videoOutputDevices[selectedVideoOutputIndex].Id
            : "disabled";
        var selectedAudioId = selectedAudioOutputIndex >= 0 && selectedAudioOutputIndex < audioOutputDevices.Count
            ? audioOutputDevices[selectedAudioOutputIndex].Id
            : PlayerPrefs.GetString(AudioOutputPrefKey, "default");

        videoOutputDevices.Clear();
        displayRouteDevices.Clear();
        audioOutputDevices.Clear();

        videoOutputDevices.Add(new OutputDeviceOption { Id = "disabled", Label = "Disabled", Index = -1 });
        audioOutputDevices.Add(new OutputDeviceOption { Id = "default", Label = "Default System Output", Index = -1 });

        var displays = Display.displays;
        var displayCount = displays == null || displays.Length == 0 ? 1 : displays.Length;
        for (var i = 0; i < displayCount; i++)
        {
            var width = i == 0 ? Screen.width : Mathf.Max(0, displays[i].systemWidth);
            var height = i == 0 ? Screen.height : Mathf.Max(0, displays[i].systemHeight);
            if (width <= 0 || height <= 0)
            {
                width = outputWidth;
                height = outputHeight;
            }

            var label = "Display " + (i + 1).ToString(CultureInfo.InvariantCulture) +
                        "  " + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            var option = new OutputDeviceOption
            {
                Id = "display-" + i.ToString(CultureInfo.InvariantCulture),
                Label = label,
                Index = i,
                Width = width,
                Height = height
            };
            videoOutputDevices.Add(option);
            displayRouteDevices.Add(option);
        }

        var renderDevices = EnumerateAudioRenderDevices();
        for (var i = 0; i < renderDevices.Count; i++)
        {
            audioOutputDevices.Add(renderDevices[i]);
        }

        selectedVideoOutputIndex = FindOutputOptionIndex(videoOutputDevices, selectedVideoId);
        selectedAudioOutputIndex = FindOutputOptionIndex(audioOutputDevices, selectedAudioId);
        selectedCdjWaveDisplayIndex = FindDisplayRouteSelectionIndex(cdjWaveDisplayIndex);
        selectedLayerSettingsDisplayIndex = FindDisplayRouteSelectionIndex(layerSettingsDisplayIndex);
        selectedEffectSettingsDisplayIndex = FindDisplayRouteSelectionIndex(effectSettingsDisplayIndex);
    }

    private void DisableProgramOutput(string reason = null)
    {
        activeProgramDisplay = -1;
        if (programOutputRoot != null)
        {
            programOutputRoot.SetActive(false);
        }
        if (programOutputCamera != null)
        {
            programOutputCamera.enabled = false;
        }
        if (!string.IsNullOrEmpty(reason))
        {
            outputStatus = reason;
        }
    }

    private void ActivateAvailableUnityDisplays()
    {
        if (unityDisplaysActivated || Display.displays == null)
        {
            return;
        }

        for (var i = 1; i < Display.displays.Length; i++)
        {
            try
            {
                Display.displays[i].Activate();
            }
            catch
            {
            }
        }

        unityDisplaysActivated = true;
    }

    private string YoutubePlaybackModeLabel(YoutubePlaybackMode mode)
    {
        switch (mode)
        {
            case YoutubePlaybackMode.UrlCompatible:
                return "URL compatible";
            case YoutubePlaybackMode.UrlBest:
                return "URL best";
            case YoutubePlaybackMode.LocalCache:
                return "Local cache";
            default:
                return mode.ToString();
        }
    }

    private void ApplyVideoOutputSelection()
    {
        RefreshOutputDevices();
        if (selectedVideoOutputIndex < 0 || selectedVideoOutputIndex >= videoOutputDevices.Count)
        {
            DisableProgramOutput("Program output disabled.");
            PlayerPrefs.SetString(VideoOutputPrefKey, "disabled");
            return;
        }

        var option = videoOutputDevices[selectedVideoOutputIndex];
        PlayerPrefs.SetString(VideoOutputPrefKey, option.Id ?? "disabled");
        PlayerPrefs.Save();

        if (option.Index < 0)
        {
            DisableProgramOutput("Program output disabled.");
            return;
        }

        if (IsDisplayReservedForAuxUi(option.Index))
        {
            DisableProgramOutput("Selected display is reserved for routed UI.");
            return;
        }

        ActivateAvailableUnityDisplays();
        activeProgramDisplay = option.Index;
        if (programOutputCamera != null)
        {
            programOutputCamera.targetDisplay = option.Index;
            programOutputCamera.enabled = true;
        }
        if (programOutputRoot != null)
        {
            programOutputRoot.SetActive(true);
        }
        outputStatus = "Program output routed to " + option.Label + ".";
    }

    private void ApplyOutputResolutionSelection()
    {
        if (selectedOutputResolutionIndex < 0 || selectedOutputResolutionIndex >= outputResolutionOptions.Count)
        {
            return;
        }

        var option = outputResolutionOptions[selectedOutputResolutionIndex];
        outputWidth = Mathf.Max(320, option.Width);
        outputHeight = Mathf.Max(180, option.Height);
        PlayerPrefs.SetInt(OutputWidthPrefKey, outputWidth);
        PlayerPrefs.SetInt(OutputHeightPrefKey, outputHeight);
        PlayerPrefs.Save();

        EnsureCompositeRenderTextures();
        ResizeAllLayerTargets(outputWidth, outputHeight);
        outputStatus = "Output resolution set to " + outputWidth.ToString(CultureInfo.InvariantCulture) + "x" + outputHeight.ToString(CultureInfo.InvariantCulture) + ".";
    }

    private void ApplyAudioOutputSelection()
    {
        RefreshOutputDevices();
        if (selectedAudioOutputIndex < 0 || selectedAudioOutputIndex >= audioOutputDevices.Count)
        {
            return;
        }

        var option = audioOutputDevices[selectedAudioOutputIndex];
        PlayerPrefs.SetString(AudioOutputPrefKey, option.Id ?? "default");
        PlayerPrefs.Save();
        outputStatus = "Audio output preference saved: " + PreferText(option.Label, "Default System Output") + ".";
    }

    private List<MonitorOutputInfo> EnumerateWindowsMonitors()
    {
        var result = new List<MonitorOutputInfo>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdcMonitor, ref NativeRect monitorRect, IntPtr data) =>
            {
                var info = new NativeMonitorInfoEx();
                info.Size = Marshal.SizeOf(typeof(NativeMonitorInfoEx));
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return true;
                }

                result.Add(new MonitorOutputInfo
                {
                    DeviceName = info.DeviceName,
                    X = info.Monitor.Left,
                    Y = info.Monitor.Top,
                    Width = info.Monitor.Right - info.Monitor.Left,
                    Height = info.Monitor.Bottom - info.Monitor.Top,
                    Primary = (info.Flags & 1) != 0
                });
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
        }

        return result;
    }

    private void CycleLayerEffectMode(int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var effect = vjLayers[layerIndex] == null ? null : vjLayers[layerIndex].Effect;
        if (effect == null || !effect.HasEffect)
        {
            return;
        }

        var next = (int)effect.Mode + 1;
        if (next > (int)LayerEffectMode.Manual)
        {
            next = 0;
        }
        effect.Mode = (LayerEffectMode)next;
    }

    private void CycleLayerEffectMode(LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        var next = (int)effect.Mode + 1;
        if (next > (int)LayerEffectMode.Manual)
        {
            next = 0;
        }
        effect.Mode = (LayerEffectMode)next;
    }

    private void AttachEffectToLayer(int layerIndex, LayerEffectKind kind)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[layerIndex];
        if (layer == null)
        {
            return;
        }

        if (layer.Effect == null)
        {
            layer.Effect = new LayerEffectState();
        }

        if (layer.Effect.Kind != kind)
        {
            DestroyStepSequencerMediaSlots(layer.Effect);
            ClearStepSequencerMediaState(layer.Effect);
        }

        layer.Effect.Kind = kind;
        layer.Effect.Enabled = true;
        layer.Enabled = true;
        layer.SourceKind = LayerSourceKind.Effect;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = EffectKindLabel(kind);
        layer.VideoStatus = null;

        if (layer.Effect.TargetLayers == null || layer.Effect.TargetLayers.Length != VjLayerCount)
        {
            layer.Effect.TargetLayers = new bool[VjLayerCount];
        }

        var hasTarget = false;
        for (var i = 0; i < layer.Effect.TargetLayers.Length; i++)
        {
            hasTarget |= layer.Effect.TargetLayers[i];
        }

        if (!hasTarget)
        {
            var defaultTarget = Mathf.Clamp(layerIndex - 1, 0, VjLayerCount - 1);
            layer.Effect.TargetLayers[defaultTarget] = true;
        }
    }

    private static float BlendModeToShaderValue(LayerBlendMode blendMode)
    {
        switch (blendMode)
        {
            case LayerBlendMode.Add50:
                return 1f;
            case LayerBlendMode.Add:
            case LayerBlendMode.Overlay:
                return 2f;
            case LayerBlendMode.Mask:
                return 3f;
            case LayerBlendMode.Screen:
                return 4f;
            case LayerBlendMode.Multiply:
                return 5f;
            case LayerBlendMode.Lighten:
                return 6f;
            case LayerBlendMode.Darken:
                return 7f;
            case LayerBlendMode.Difference:
                return 8f;
            case LayerBlendMode.Subtract:
                return 10f;
            default:
                return 0f;
        }
    }

    private static float EffectAxisValue(LayerEffectMode mode)
    {
        switch (mode)
        {
            case LayerEffectMode.Vertical:
                return 1f;
            case LayerEffectMode.Alternate:
                return 2f;
            case LayerEffectMode.Zoom:
                return 3f;
            default:
                return 0f;
        }
    }

    private void DrawSettingsLinesSection(string title, string[] lines)
    {
        if (string.IsNullOrEmpty(title) || lines == null || lines.Length == 0)
        {
            return;
        }

        GUILayout.Label(title, normalStyle);
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                GUILayout.Space(4f);
            }
            else
            {
                GUILayout.Label(lines[i], smallStyle);
            }
        }
        GUILayout.Space(8f);
    }

    private void AddOutputResolutionOption(int width, int height)
    {
        outputResolutionOptions.Add(new OutputDeviceOption
        {
            Id = ResolutionOptionId(width, height),
            Label = width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture),
            Width = width,
            Height = height
        });
    }

    private static string ResolutionOptionId(int width, int height)
    {
        return width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
    }

    private LayerEffectState GetStepSequencerEffect(int hostLayerIndex)
    {
        if (vjLayers == null || hostLayerIndex < 0 || hostLayerIndex >= vjLayers.Length)
        {
            return null;
        }

        var layer = vjLayers[hostLayerIndex];
        return layer != null && layer.Effect != null && layer.Effect.Kind == LayerEffectKind.StepSequencer
            ? layer.Effect
            : null;
    }

    private int FindDisplayRouteSelectionIndex(int displayIndex)
    {
        if (displayRouteDevices == null || displayRouteDevices.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < displayRouteDevices.Count; i++)
        {
            if (displayRouteDevices[i].Index == displayIndex)
            {
                return i;
            }
        }

        return 0;
    }

    private List<OutputDeviceOption> EnumerateAudioRenderDevices()
    {
        var result = new List<OutputDeviceOption>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            IMMDeviceCollection devices;
            enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out devices);
            if (devices == null)
            {
                return result;
            }

            uint count;
            devices.GetCount(out count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice device;
                devices.Item(i, out device);
                if (device == null)
                {
                    continue;
                }

                string id;
                device.GetId(out id);
                result.Add(new OutputDeviceOption
                {
                    Id = id,
                    Label = GetAudioDeviceFriendlyName(device, id),
                    Index = (int)i
                });
            }
        }
        catch
        {
        }

        return result;
    }

    private static string GetAudioDeviceFriendlyName(IMMDevice device, string fallback)
    {
        try
        {
            IPropertyStore store;
            device.OpenPropertyStore(0, out store);
            if (store != null)
            {
                var key = new PropertyKey
                {
                    fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                    pid = 14
                };
                PropVariant value;
                store.GetValue(ref key, out value);
                var text = value.ToStringValue();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }
        catch
        {
        }

        return string.IsNullOrEmpty(fallback) ? "Audio Output" : fallback;
    }

    private void ResizeAllLayerTargets(int width, int height)
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            ResizeLayerTargets(vjLayers[i], width, height);
        }
    }

    private static void ResizeLayerTargets(LayerState layer, int width, int height)
    {
        if (layer == null)
        {
            return;
        }

        layer.Texture = ReplaceRenderTexture(layer.Texture, width, height);
        layer.EffectTexture = ReplaceRenderTexture(layer.EffectTexture, width, height);
        layer.EffectScratchTexture = ReplaceRenderTexture(layer.EffectScratchTexture, width, height);

        if (layer.Player != null)
        {
            layer.Player.targetTexture = layer.Texture;
        }
        if (layer.TextSource != null && layer.TextSource.Camera != null)
        {
            layer.TextSource.Camera.targetTexture = layer.Texture;
        }
        if (layer.Generator != null && layer.Generator.Camera != null)
        {
            layer.Generator.Texture = ReplaceRenderTexture(layer.Generator.Texture, width, height);
            layer.Generator.Camera.targetTexture = layer.Generator.Texture;
        }
        if (layer.Effect != null && layer.Effect.StepSequenceMediaLayers != null)
        {
            for (var i = 0; i < layer.Effect.StepSequenceMediaLayers.Length; i++)
            {
                ResizeLayerTargets(layer.Effect.StepSequenceMediaLayers[i], width, height);
            }
        }
    }

    private static RenderTexture ReplaceRenderTexture(RenderTexture texture, int width, int height)
    {
        if (texture != null && texture.width == width && texture.height == height)
        {
            return texture;
        }

        var replacement = CreateManagedRenderTexture(texture == null ? "Render Texture" : texture.name, width, height, 0);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
        return replacement;
    }
}
