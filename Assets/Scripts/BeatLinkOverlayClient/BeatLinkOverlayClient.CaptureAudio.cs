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
    private enum CaptureAudioEqBandType
    {
        LowShelf,
        LowMidBell,
        HighMidBell,
        HighShelf,
        HighPass,
        LowPass
    }

    private sealed class CaptureAudioEqBandState
    {
        public CaptureAudioEqBandType Type;
        public float FrequencyHz;
        public float GainDb;
        public float Q;
    }

    private void LoadCaptureAudioFftPreferences()
    {
        captureAudioFftEnabled = PlayerPrefs.GetInt(CaptureAudioFftEnabledPrefKey, 1) != 0;
        captureAudioSpectrumViewMinHz = Mathf.Max(CaptureAudioSpectrumDisplayMinHz, PlayerPrefs.GetFloat(CaptureAudioSpectrumViewMinHzPrefKey, CaptureAudioSpectrumDisplayMinHz));
        captureAudioSpectrumViewMaxHz = Mathf.Max(1000f, PlayerPrefs.GetFloat(CaptureAudioSpectrumViewMaxHzPrefKey, CaptureAudioSpectrumDisplayMaxHz));
        EnsureCaptureAudioEqDefaults();
        captureAudioEqInputGainDb = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqInputGainPrefKey, 0f), CaptureAudioEqInputGainMinDb, CaptureAudioEqInputGainMaxDb);
        captureAudioEqOutputGainDb = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqOutputGainPrefKey, 0f), CaptureAudioEqOutputGainMinDb, CaptureAudioEqOutputGainMaxDb);
        captureAudioEqDisplayMaxGainDb = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqDisplayMaxGainPrefKey, 18f), CaptureAudioEqDisplayGainMinDb, CaptureAudioEqDisplayGainMaxDb);
        for (var i = 0; i < captureAudioEqBands.Length; i++)
        {
            var band = captureAudioEqBands[i];
            band.Type = ParseCaptureAudioEqBandType(PlayerPrefs.GetInt(CaptureAudioEqBandTypePrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), (int)band.Type), band.Type, i);
            band.FrequencyHz = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqBandFrequencyPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.FrequencyHz), CaptureAudioSpectrumDisplayMinHz, CaptureAudioSpectrumDisplayMaxHz);
            band.GainDb = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqBandGainPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.GainDb), CaptureAudioEqGainMinDb, CaptureAudioEqGainMaxDb);
            band.Q = Mathf.Clamp(PlayerPrefs.GetFloat(CaptureAudioEqBandQPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.Q), CaptureAudioEqQMin, CaptureAudioEqQMax);
        }
        NormalizeCaptureAudioSpectrumViewRange();
        for (var i = 0; i < captureAudioSpectrumBandDb.Length; i++)
        {
            captureAudioSpectrumBandDb[i] = CaptureAudioSpectrumDbFloor;
            captureAudioFilteredSpectrumBandDb[i] = CaptureAudioSpectrumDbFloor;
        }
    }

    private void SaveCaptureAudioFftPreferences()
    {
        EnsureCaptureAudioEqDefaults();
        NormalizeCaptureAudioSpectrumViewRange();
        PlayerPrefs.SetInt(CaptureAudioFftEnabledPrefKey, captureAudioFftEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(CaptureAudioSpectrumViewMinHzPrefKey, captureAudioSpectrumViewMinHz);
        PlayerPrefs.SetFloat(CaptureAudioSpectrumViewMaxHzPrefKey, captureAudioSpectrumViewMaxHz);
        PlayerPrefs.SetFloat(CaptureAudioEqInputGainPrefKey, captureAudioEqInputGainDb);
        PlayerPrefs.SetFloat(CaptureAudioEqOutputGainPrefKey, captureAudioEqOutputGainDb);
        PlayerPrefs.SetFloat(CaptureAudioEqDisplayMaxGainPrefKey, captureAudioEqDisplayMaxGainDb);
        for (var i = 0; i < captureAudioEqBands.Length; i++)
        {
            var band = captureAudioEqBands[i];
            PlayerPrefs.SetInt(CaptureAudioEqBandTypePrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), (int)band.Type);
            PlayerPrefs.SetFloat(CaptureAudioEqBandFrequencyPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.FrequencyHz);
            PlayerPrefs.SetFloat(CaptureAudioEqBandGainPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.GainDb);
            PlayerPrefs.SetFloat(CaptureAudioEqBandQPrefKeyPrefix + i.ToString(CultureInfo.InvariantCulture), band.Q);
        }
        PlayerPrefs.Save();
        SyncCaptureAudioMonitorFilters();
    }

    private void EnsureCaptureAudioEqDefaults()
    {
        if (captureAudioEqBands[0] == null)
        {
            captureAudioEqBands[0] = new CaptureAudioEqBandState { Type = CaptureAudioEqBandType.LowShelf, FrequencyHz = 80f, GainDb = 0f, Q = 0.72f };
            captureAudioEqBands[1] = new CaptureAudioEqBandState { Type = CaptureAudioEqBandType.LowMidBell, FrequencyHz = 320f, GainDb = 0f, Q = 0.95f };
            captureAudioEqBands[2] = new CaptureAudioEqBandState { Type = CaptureAudioEqBandType.HighMidBell, FrequencyHz = 3200f, GainDb = 0f, Q = 0.95f };
            captureAudioEqBands[3] = new CaptureAudioEqBandState { Type = CaptureAudioEqBandType.HighShelf, FrequencyHz = 12000f, GainDb = 0f, Q = 0.72f };
        }
        captureAudioEqSelectedBand = Mathf.Clamp(captureAudioEqSelectedBand, 0, CaptureAudioEqBandCount - 1);
        captureAudioEqInputGainDb = Mathf.Clamp(captureAudioEqInputGainDb, CaptureAudioEqInputGainMinDb, CaptureAudioEqInputGainMaxDb);
        captureAudioEqOutputGainDb = Mathf.Clamp(captureAudioEqOutputGainDb, CaptureAudioEqOutputGainMinDb, CaptureAudioEqOutputGainMaxDb);
        captureAudioEqDisplayMaxGainDb = Mathf.Clamp(captureAudioEqDisplayMaxGainDb, CaptureAudioEqDisplayGainMinDb, CaptureAudioEqDisplayGainMaxDb);
    }

    private void ResetCaptureAudioEqBand(int index)
    {
        EnsureCaptureAudioEqDefaults();
        if (index < 0 || index >= captureAudioEqBands.Length)
        {
            return;
        }

        switch (index)
        {
            case 0:
                captureAudioEqBands[index].Type = CaptureAudioEqBandType.LowShelf;
                captureAudioEqBands[index].FrequencyHz = 80f;
                captureAudioEqBands[index].GainDb = 0f;
                captureAudioEqBands[index].Q = 0.72f;
                break;
            case 1:
                captureAudioEqBands[index].Type = CaptureAudioEqBandType.LowMidBell;
                captureAudioEqBands[index].FrequencyHz = 320f;
                captureAudioEqBands[index].GainDb = 0f;
                captureAudioEqBands[index].Q = 0.95f;
                break;
            case 2:
                captureAudioEqBands[index].Type = CaptureAudioEqBandType.HighMidBell;
                captureAudioEqBands[index].FrequencyHz = 3200f;
                captureAudioEqBands[index].GainDb = 0f;
                captureAudioEqBands[index].Q = 0.95f;
                break;
            default:
                captureAudioEqBands[index].Type = CaptureAudioEqBandType.HighShelf;
                captureAudioEqBands[index].FrequencyHz = 12000f;
                captureAudioEqBands[index].GainDb = 0f;
                captureAudioEqBands[index].Q = 0.72f;
                break;
        }
    }

    private static CaptureAudioEqBandType ParseCaptureAudioEqBandType(int raw, CaptureAudioEqBandType fallback, int index)
    {
        if (!Enum.IsDefined(typeof(CaptureAudioEqBandType), raw))
        {
            return fallback;
        }

        var parsed = (CaptureAudioEqBandType)raw;
        if (index == 0)
        {
            return parsed == CaptureAudioEqBandType.HighPass ? parsed : CaptureAudioEqBandType.LowShelf;
        }
        if (index == CaptureAudioEqBandCount - 1)
        {
            return parsed == CaptureAudioEqBandType.LowPass ? parsed : CaptureAudioEqBandType.HighShelf;
        }
        return parsed == CaptureAudioEqBandType.LowMidBell || parsed == CaptureAudioEqBandType.HighMidBell ? parsed : fallback;
    }

    private bool CaptureAudioEqBandSupportsGain(CaptureAudioEqBandState band)
    {
        return band != null && band.Type != CaptureAudioEqBandType.HighPass && band.Type != CaptureAudioEqBandType.LowPass;
    }

    private void CycleCaptureAudioEqBandMode(int index)
    {
        EnsureCaptureAudioEqDefaults();
        if (index < 0 || index >= captureAudioEqBands.Length)
        {
            return;
        }

        var band = captureAudioEqBands[index];
        if (index == 0)
        {
            band.Type = band.Type == CaptureAudioEqBandType.HighPass ? CaptureAudioEqBandType.LowShelf : CaptureAudioEqBandType.HighPass;
            if (!CaptureAudioEqBandSupportsGain(band))
            {
                band.GainDb = 0f;
            }
        }
        else if (index == captureAudioEqBands.Length - 1)
        {
            band.Type = band.Type == CaptureAudioEqBandType.LowPass ? CaptureAudioEqBandType.HighShelf : CaptureAudioEqBandType.LowPass;
            if (!CaptureAudioEqBandSupportsGain(band))
            {
                band.GainDb = 0f;
            }
        }
    }

    private static string CaptureAudioEqBandTypeLabel(CaptureAudioEqBandType type)
    {
        switch (type)
        {
            case CaptureAudioEqBandType.LowShelf: return "Low Shelf";
            case CaptureAudioEqBandType.HighShelf: return "High Shelf";
            case CaptureAudioEqBandType.HighPass: return "High Pass";
            case CaptureAudioEqBandType.LowPass: return "Low Pass";
            case CaptureAudioEqBandType.LowMidBell: return "Bell";
            case CaptureAudioEqBandType.HighMidBell: return "Bell";
            default: return type.ToString();
        }
    }

    private void NormalizeCaptureAudioSpectrumViewRange()
    {
        var upper = Mathf.Max(1000f, Mathf.Min(CaptureAudioSpectrumDisplayMaxHz, CaptureAudioSpectrumUpperHz()));
        captureAudioSpectrumViewMinHz = Mathf.Clamp(captureAudioSpectrumViewMinHz, CaptureAudioSpectrumDisplayMinHz, Mathf.Max(CaptureAudioSpectrumDisplayMinHz, upper - 500f));
        captureAudioSpectrumViewMaxHz = Mathf.Clamp(captureAudioSpectrumViewMaxHz, captureAudioSpectrumViewMinHz + 500f, upper);
    }

    private void SyncCaptureAudioMonitorFilters()
    {
        if (captureAudioHighPassFilter == null || captureAudioLowPassFilter == null)
        {
            return;
        }

        captureAudioHighPassFilter.enabled = false;
        captureAudioLowPassFilter.enabled = false;
    }

    private float CaptureAudioAnalysisSampleRate()
    {
        if (captureAudioClip != null && captureAudioClip.frequency > 0)
        {
            return captureAudioClip.frequency;
        }

        return 48000f;
    }

    private readonly struct BiquadCoefficients
    {
        public readonly bool Active;
        public readonly float B0;
        public readonly float B1;
        public readonly float B2;
        public readonly float A1;
        public readonly float A2;

        public BiquadCoefficients(bool active, float b0, float b1, float b2, float a1, float a2)
        {
            Active = active;
            B0 = b0;
            B1 = b1;
            B2 = b2;
            A1 = a1;
            A2 = a2;
        }
    }

    private static BiquadCoefficients CreateLowPassBiquad(float sampleRate, float cutoffHz)
    {
        if (sampleRate <= 0f || cutoffHz <= 10f || cutoffHz >= sampleRate * 0.5f - 10f)
        {
            return default;
        }

        var omega = (2f * Mathf.PI * cutoffHz) / sampleRate;
        var sin = Mathf.Sin(omega);
        var cos = Mathf.Cos(omega);
        var alpha = sin / (2f * 0.7071f);
        var a0 = 1f + alpha;
        return new BiquadCoefficients(
            true,
            ((1f - cos) * 0.5f) / a0,
            (1f - cos) / a0,
            ((1f - cos) * 0.5f) / a0,
            (-2f * cos) / a0,
            (1f - alpha) / a0);
    }

    private static BiquadCoefficients CreateHighPassBiquad(float sampleRate, float cutoffHz)
    {
        if (sampleRate <= 0f || cutoffHz <= 10f || cutoffHz >= sampleRate * 0.5f - 10f)
        {
            return default;
        }

        var omega = (2f * Mathf.PI * cutoffHz) / sampleRate;
        var sin = Mathf.Sin(omega);
        var cos = Mathf.Cos(omega);
        var alpha = sin / (2f * 0.7071f);
        var a0 = 1f + alpha;
        return new BiquadCoefficients(
            true,
            ((1f + cos) * 0.5f) / a0,
            (-(1f + cos)) / a0,
            ((1f + cos) * 0.5f) / a0,
            (-2f * cos) / a0,
            (1f - alpha) / a0);
    }

    private static float ProcessBiquadSample(BiquadCoefficients coefficients, float input, ref float z1, ref float z2)
    {
        if (!coefficients.Active)
        {
            return input;
        }

        var output = coefficients.B0 * input + z1;
        z1 = coefficients.B1 * input - coefficients.A1 * output + z2;
        z2 = coefficients.B2 * input - coefficients.A2 * output;
        return output;
    }

    private static float BiquadMagnitudeDb(BiquadCoefficients coefficients, float sampleRate, float frequencyHz)
    {
        if (!coefficients.Active)
        {
            return 0f;
        }

        var omega = (2f * Mathf.PI * Mathf.Clamp(frequencyHz, 1f, sampleRate * 0.5f - 1f)) / sampleRate;
        var cos1 = Mathf.Cos(omega);
        var sin1 = Mathf.Sin(omega);
        var cos2 = Mathf.Cos(2f * omega);
        var sin2 = Mathf.Sin(2f * omega);
        var numeratorReal = coefficients.B0 + coefficients.B1 * cos1 + coefficients.B2 * cos2;
        var numeratorImaginary = -coefficients.B1 * sin1 - coefficients.B2 * sin2;
        var denominatorReal = 1f + coefficients.A1 * cos1 + coefficients.A2 * cos2;
        var denominatorImaginary = -coefficients.A1 * sin1 - coefficients.A2 * sin2;
        var numeratorMagnitude = Mathf.Sqrt(numeratorReal * numeratorReal + numeratorImaginary * numeratorImaginary);
        var denominatorMagnitude = Mathf.Sqrt(denominatorReal * denominatorReal + denominatorImaginary * denominatorImaginary);
        var magnitude = numeratorMagnitude / Mathf.Max(0.000001f, denominatorMagnitude);
        return 20f * Mathf.Log10(Mathf.Max(0.000001f, magnitude));
    }

    private static BiquadCoefficients CreatePeakingBiquad(float sampleRate, float frequencyHz, float q, float gainDb)
    {
        if (sampleRate <= 0f || frequencyHz <= 10f || frequencyHz >= sampleRate * 0.5f - 10f || Mathf.Abs(gainDb) < 0.001f)
        {
            return default;
        }

        q = Mathf.Clamp(q, CaptureAudioEqQMin, CaptureAudioEqQMax);
        var a = Mathf.Pow(10f, gainDb / 40f);
        var omega = (2f * Mathf.PI * frequencyHz) / sampleRate;
        var sin = Mathf.Sin(omega);
        var cos = Mathf.Cos(omega);
        var alpha = sin / (2f * q);
        var a0 = 1f + alpha / a;
        return new BiquadCoefficients(
            true,
            (1f + alpha * a) / a0,
            (-2f * cos) / a0,
            (1f - alpha * a) / a0,
            (-2f * cos) / a0,
            (1f - alpha / a) / a0);
    }

    private static BiquadCoefficients CreateLowShelfBiquad(float sampleRate, float frequencyHz, float q, float gainDb)
    {
        if (sampleRate <= 0f || frequencyHz <= 10f || frequencyHz >= sampleRate * 0.5f - 10f || Mathf.Abs(gainDb) < 0.001f)
        {
            return default;
        }

        q = Mathf.Clamp(q, CaptureAudioEqQMin, CaptureAudioEqQMax);
        var a = Mathf.Pow(10f, gainDb / 40f);
        var omega = (2f * Mathf.PI * frequencyHz) / sampleRate;
        var sin = Mathf.Sin(omega);
        var cos = Mathf.Cos(omega);
        var alpha = sin / (2f * q);
        var twoSqrtAAlpha = 2f * Mathf.Sqrt(a) * alpha;
        var aPlusOne = a + 1f;
        var aMinusOne = a - 1f;
        var a0 = aPlusOne + aMinusOne * cos + twoSqrtAAlpha;
        return new BiquadCoefficients(
            true,
            (a * (aPlusOne - aMinusOne * cos + twoSqrtAAlpha)) / a0,
            (2f * a * (aMinusOne - aPlusOne * cos)) / a0,
            (a * (aPlusOne - aMinusOne * cos - twoSqrtAAlpha)) / a0,
            (-2f * (aMinusOne + aPlusOne * cos)) / a0,
            (aPlusOne + aMinusOne * cos - twoSqrtAAlpha) / a0);
    }

    private static BiquadCoefficients CreateHighShelfBiquad(float sampleRate, float frequencyHz, float q, float gainDb)
    {
        if (sampleRate <= 0f || frequencyHz <= 10f || frequencyHz >= sampleRate * 0.5f - 10f || Mathf.Abs(gainDb) < 0.001f)
        {
            return default;
        }

        q = Mathf.Clamp(q, CaptureAudioEqQMin, CaptureAudioEqQMax);
        var a = Mathf.Pow(10f, gainDb / 40f);
        var omega = (2f * Mathf.PI * frequencyHz) / sampleRate;
        var sin = Mathf.Sin(omega);
        var cos = Mathf.Cos(omega);
        var alpha = sin / (2f * q);
        var twoSqrtAAlpha = 2f * Mathf.Sqrt(a) * alpha;
        var aPlusOne = a + 1f;
        var aMinusOne = a - 1f;
        var a0 = aPlusOne - aMinusOne * cos + twoSqrtAAlpha;
        return new BiquadCoefficients(
            true,
            (a * (aPlusOne + aMinusOne * cos + twoSqrtAAlpha)) / a0,
            (-2f * a * (aMinusOne + aPlusOne * cos)) / a0,
            (a * (aPlusOne + aMinusOne * cos - twoSqrtAAlpha)) / a0,
            (2f * (aMinusOne - aPlusOne * cos)) / a0,
            (aPlusOne - aMinusOne * cos - twoSqrtAAlpha) / a0);
    }

    private BiquadCoefficients CreateCaptureAudioEqBiquad(float sampleRate, CaptureAudioEqBandState band)
    {
        if (band == null)
        {
            return default;
        }

        switch (band.Type)
        {
            case CaptureAudioEqBandType.LowShelf:
                return CreateLowShelfBiquad(sampleRate, band.FrequencyHz, band.Q, band.GainDb);
            case CaptureAudioEqBandType.HighShelf:
                return CreateHighShelfBiquad(sampleRate, band.FrequencyHz, band.Q, band.GainDb);
            case CaptureAudioEqBandType.HighPass:
                return CreateHighPassBiquad(sampleRate, band.FrequencyHz);
            case CaptureAudioEqBandType.LowPass:
                return CreateLowPassBiquad(sampleRate, band.FrequencyHz);
            default:
                return CreatePeakingBiquad(sampleRate, band.FrequencyHz, band.Q, band.GainDb);
        }
    }

    private float CaptureAudioBandPassResponseDb(float frequencyHz)
    {
        if (!captureAudioFftEnabled)
        {
            return 0f;
        }

        EnsureCaptureAudioEqDefaults();
        var sampleRate = CaptureAudioAnalysisSampleRate();
        var responseDb = 0f;
        for (var i = 0; i < captureAudioEqBands.Length; i++)
        {
            responseDb += BiquadMagnitudeDb(CreateCaptureAudioEqBiquad(sampleRate, captureAudioEqBands[i]), sampleRate, frequencyHz);
        }
        responseDb += captureAudioEqOutputGainDb;

        return Mathf.Clamp(responseDb, CaptureAudioSpectrumDbFloor, CaptureAudioSpectrumDbCeiling);
    }

    private void StartCapturePreview()
    {
        StopCapturePreview();

        try
        {
            captureDevices = WebCamTexture.devices;
            Debug.Log("Capture devices found: " + FormatCaptureDeviceList(captureDevices));
            if (captureDevices == null || captureDevices.Length == 0)
            {
                captureDevices = new WebCamDevice[0];
                captureError = "No capture devices found.";
                AddLog(captureError);
                Debug.LogWarning(captureError);
                return;
            }

            captureDeviceIndex = SelectCaptureDevice(captureDevices, captureDeviceIndex);
            captureDeviceName = captureDevices[captureDeviceIndex].name;
            captureTexture = new WebCamTexture(captureDeviceName, 1920, 1080, 60);
            captureTexture.Play();
            audioInputDevicesDirty = true;
            RefreshAudioInputDevices();
            AutoSelectCaptureAudioInput();
            ApplyCaptureAudioInputSelection();
            captureError = null;
            AddLog("Capture preview started: " + captureDeviceName);
            Debug.Log("Capture preview started: " + captureDeviceName);
        }
        catch (Exception ex)
        {
            captureError = ex.Message;
            AddLog("Capture preview error: " + ex.Message);
            Debug.LogWarning("Capture preview error: " + ex.Message);
            StopCapturePreview();
        }
    }

    private void StopCapturePreview()
    {
        if (captureTexture != null)
        {
            if (captureTexture.isPlaying)
            {
                captureTexture.Stop();
            }
            Destroy(captureTexture);
            captureTexture = null;
        }
    }

    private void InitializeCaptureAudioMonitor()
    {
        if (captureAudioRoot != null && captureAudioSource != null)
        {
            return;
        }

        captureAudioRoot = new GameObject("Capture Audio Monitor");
        captureAudioRoot.transform.SetParent(transform, false);
        captureAudioSource = captureAudioRoot.AddComponent<AudioSource>();
        captureAudioHighPassFilter = captureAudioRoot.AddComponent<AudioHighPassFilter>();
        captureAudioLowPassFilter = captureAudioRoot.AddComponent<AudioLowPassFilter>();
        captureAudioSource.playOnAwake = false;
        captureAudioSource.loop = true;
        captureAudioSource.spatialBlend = 0f;
        captureAudioSource.volume = captureAudioMonitorVolume;
        captureAudioSource.mute = !captureAudioMonitorEnabled;
        captureAudioSource.ignoreListenerPause = true;
        captureAudioSource.bypassEffects = false;
        captureAudioSource.bypassListenerEffects = false;
        captureAudioSource.bypassReverbZones = true;
        SyncCaptureAudioMonitorFilters();

        captureAudioListener = FindAnyObjectByType<AudioListener>();
        if (captureAudioListener == null)
        {
            captureAudioListener = captureAudioRoot.AddComponent<AudioListener>();
            ownsCaptureAudioListener = true;
        }
    }

    private void RefreshAudioInputDevices()
    {
        if (!audioInputDevicesDirty && audioInputDevices.Count > 0)
        {
            return;
        }

        audioInputDevicesDirty = false;
        var selectedId = selectedAudioInputIndex >= 0 && selectedAudioInputIndex < audioInputDevices.Count
            ? audioInputDevices[selectedAudioInputIndex].Id
            : "disabled";

        audioInputDevices.Clear();
        audioInputDevices.Add(new OutputDeviceOption { Id = "disabled", Label = "Disabled", Index = -1 });

        var devices = Microphone.devices;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < devices.Length; i++)
        {
            var name = devices[i];
            if (string.IsNullOrEmpty(name) || !seen.Add(name))
            {
                continue;
            }

            audioInputDevices.Add(new OutputDeviceOption
            {
                Id = "input-" + StableTextHash(name).ToString("x8", CultureInfo.InvariantCulture),
                Label = name,
                Index = i
            });
        }

        selectedAudioInputIndex = FindOutputOptionIndex(audioInputDevices, selectedId);
    }

    private void AutoSelectCaptureAudioInput()
    {
        if (selectedAudioInputIndex > 0 || audioInputDevices.Count <= 1)
        {
            return;
        }

        var bestIndex = -1;
        for (var i = 1; i < audioInputDevices.Count; i++)
        {
            var label = audioInputDevices[i].Label ?? "";
            if ((!string.IsNullOrEmpty(captureDeviceName) && label.IndexOf(captureDeviceName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                label.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("cam link", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("hdmi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bestIndex = i;
                break;
            }
        }

        if (bestIndex > 0)
        {
            selectedAudioInputIndex = bestIndex;
        }
    }

    private void ApplyCaptureAudioInputSelection()
    {
        InitializeCaptureAudioMonitor();
        RefreshAudioInputDevices();

        if (selectedAudioInputIndex < 0 || selectedAudioInputIndex >= audioInputDevices.Count)
        {
            captureAudioStatus = "Capture audio: no input selected.";
            StopCaptureAudioInput();
            return;
        }

        var option = audioInputDevices[selectedAudioInputIndex];
        if (option.Index < 0)
        {
            captureAudioStatus = "Capture audio input disabled.";
            StopCaptureAudioInput();
            return;
        }

        var deviceName = option.Label;
        if (string.Equals(captureAudioDeviceName, deviceName, StringComparison.Ordinal) && captureAudioClip != null)
        {
            UpdateCaptureAudioMonitorState();
            return;
        }

        StopCaptureAudioInput();
        QueueCaptureAudioInputStart(deviceName, true);
    }

    private void QueueCaptureAudioInputStart(string deviceName, bool restart)
    {
        pendingCaptureAudioDeviceName = deviceName;
        var delay = restart ? 0.15f : 0.05f;
        captureAudioStartAtRealtime = Time.realtimeSinceStartup + delay;
        captureAudioMonitorPrimed = false;
        captureAudioStatus = "Capture audio reinitializing: " + deviceName;
    }

    private void TryStartPendingCaptureAudioInput()
    {
        if (string.IsNullOrEmpty(pendingCaptureAudioDeviceName))
        {
            return;
        }

        if (Time.realtimeSinceStartup < captureAudioStartAtRealtime)
        {
            return;
        }

        var deviceName = pendingCaptureAudioDeviceName;
        pendingCaptureAudioDeviceName = null;
        StartCaptureAudioInput(deviceName);
    }

    private void StartCaptureAudioInput(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            captureAudioStatus = "Capture audio: invalid input device.";
            return;
        }

        try
        {
            InitializeCaptureAudioMonitor();

            int minFreq;
            int maxFreq;
            Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
            var sampleRate = 48000;
            if ((minFreq > 0 || maxFreq > 0) && ((minFreq > 0 && sampleRate < minFreq) || (maxFreq > 0 && sampleRate > maxFreq)))
            {
                sampleRate = maxFreq >= 44100 ? maxFreq : (minFreq >= 8000 ? minFreq : 48000);
            }

            if (sampleRate < 8000)
            {
                sampleRate = 48000;
            }

            captureAudioSource.Stop();
            captureAudioSource.clip = null;
            captureAudioSource.timeSamples = 0;
            captureAudioClip = Microphone.Start(deviceName, true, 2, sampleRate);
            captureAudioDeviceName = deviceName;
            captureAudioSource.clip = captureAudioClip;
            SyncCaptureAudioMonitorFilters();
            captureAudioMonitorPrimed = false;
            captureAudioLevelRms = 0f;
            captureAudioLevelPeak = 0f;
            captureAudioStatus = "Capture audio listening: " + deviceName + " @ " + sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz";
            UpdateCaptureAudioMonitorState();
        }
        catch (Exception ex)
        {
            captureAudioStatus = "Capture audio start failed: " + ex.Message;
            StopCaptureAudioInput();
        }
    }

    private void StopCaptureAudioInput()
    {
        if (!string.IsNullOrEmpty(captureAudioDeviceName))
        {
            try
            {
                if (Microphone.IsRecording(captureAudioDeviceName))
                {
                    Microphone.End(captureAudioDeviceName);
                }
            }
            catch
            {
            }
        }

        if (captureAudioSource != null)
        {
            captureAudioSource.Stop();
            captureAudioSource.timeSamples = 0;
            captureAudioSource.clip = null;
        }

        captureAudioClip = null;
        captureAudioDeviceName = null;
        pendingCaptureAudioDeviceName = null;
        captureAudioStartAtRealtime = 0f;
        captureAudioMonitorPrimed = false;
        captureAudioLevelRms = 0f;
        captureAudioLevelPeak = 0f;
    }

    private void EnsureCaptureAudioAnalysisSequence()
    {
        if (!isActiveAndEnabled || captureAudioAnalysisCoroutine != null)
        {
            return;
        }

        captureAudioAnalysisCoroutine = StartCoroutine(CaptureAudioAnalysisSequence());
    }

    private void StopCaptureAudioAnalysisSequence()
    {
        if (captureAudioAnalysisCoroutine == null)
        {
            return;
        }

        StopCoroutine(captureAudioAnalysisCoroutine);
        captureAudioAnalysisCoroutine = null;
    }

    private IEnumerator CaptureAudioAnalysisSequence()
    {
        while (true)
        {
            UpdateCaptureAudioMonitoring();
            var nextRunAt = Time.realtimeSinceStartup + CaptureAudioAnalysisIntervalSeconds;
            while (Time.realtimeSinceStartup < nextRunAt)
            {
                yield return null;
            }
        }
    }

    private void UpdateCaptureAudioPlaybackSequence()
    {
        if (captureAudioSource == null)
        {
            return;
        }

        TryStartPendingCaptureAudioInput();
        UpdateCaptureAudioMonitorState();
        if (captureAudioClip == null || string.IsNullOrEmpty(captureAudioDeviceName))
        {
            return;
        }

        var position = 0;
        try
        {
            position = Microphone.GetPosition(captureAudioDeviceName);
        }
        catch (Exception ex)
        {
            captureAudioStatus = "Capture audio read failed: " + ex.Message;
            return;
        }

        var startThreshold = Math.Max(captureAudioMonitorLatencySamples * 2, 8192);
        if (position >= startThreshold)
        {
            captureAudioMonitorPrimed = true;
        }

        if (captureAudioMonitorPrimed && position > captureAudioMonitorLatencySamples && !captureAudioSource.isPlaying && captureAudioMonitorEnabled)
        {
            captureAudioSource.timeSamples = Math.Max(0, position - captureAudioMonitorLatencySamples);
            captureAudioSource.Play();
        }
    }

    private void UpdateCaptureAudioMonitoring()
    {
        if (captureAudioSource == null)
        {
            return;
        }

        if (captureAudioClip == null || string.IsNullOrEmpty(captureAudioDeviceName))
        {
            UpdateCaptureAudioLevels(0);
            return;
        }

        var position = 0;
        try
        {
            position = Microphone.GetPosition(captureAudioDeviceName);
        }
        catch (Exception ex)
        {
            captureAudioStatus = "Capture audio read failed: " + ex.Message;
            return;
        }

        UpdateCaptureAudioLevels(position);
    }

    private void UpdateCaptureAudioMonitorState()
    {
        if (captureAudioSource == null)
        {
            return;
        }

        captureAudioSource.volume = captureAudioMonitorVolume;
        captureAudioSource.mute = !captureAudioMonitorEnabled;
        if (!captureAudioMonitorEnabled && captureAudioSource.isPlaying)
        {
            captureAudioSource.Pause();
        }
        else if (captureAudioMonitorEnabled && captureAudioClip != null && !captureAudioSource.isPlaying)
        {
            var position = 0;
            try
            {
                if (!string.IsNullOrEmpty(captureAudioDeviceName))
                {
                    position = Microphone.GetPosition(captureAudioDeviceName);
                }
            }
            catch
            {
            }
            var startThreshold = Math.Max(captureAudioMonitorLatencySamples * 2, 8192);
            if (position >= startThreshold)
            {
                captureAudioMonitorPrimed = true;
            }
            if (captureAudioMonitorPrimed && position > captureAudioMonitorLatencySamples)
            {
                captureAudioSource.timeSamples = Math.Max(0, position - captureAudioMonitorLatencySamples);
                captureAudioSource.UnPause();
                if (!captureAudioSource.isPlaying)
                {
                    captureAudioSource.Play();
                }
            }
        }
    }

    private void UpdateCaptureAudioLevels(int position)
    {
        if (captureAudioClip == null)
        {
            captureAudioLevelRms = Mathf.Lerp(captureAudioLevelRms, 0f, 0.2f);
            captureAudioLevelPeak = Mathf.Max(0f, captureAudioLevelPeak - Time.unscaledDeltaTime * 0.8f);
            DecayCaptureAudioSpectrum();
            return;
        }

        var channels = Math.Max(1, captureAudioClip.channels);
        var sampleFrames = Math.Min(captureAudioAnalysisBuffer.Length / channels, captureAudioMonoAnalysisBuffer.Length);
        if (sampleFrames <= 0 || position <= 0)
        {
            captureAudioLevelRms = Mathf.Lerp(captureAudioLevelRms, 0f, 0.2f);
            captureAudioLevelPeak = Mathf.Max(0f, captureAudioLevelPeak - Time.unscaledDeltaTime * 0.8f);
            DecayCaptureAudioSpectrum();
            return;
        }

        var offset = position - sampleFrames;
        if (offset < 0)
        {
            offset += captureAudioClip.samples;
        }

        try
        {
            captureAudioClip.GetData(captureAudioAnalysisBuffer, offset);
        }
        catch
        {
            return;
        }

        float sumSquares = 0f;
        float peak = 0f;
        var totalSamples = sampleFrames * channels;
        for (var i = 0; i < totalSamples; i++)
        {
            var sample = captureAudioAnalysisBuffer[i];
            sumSquares += sample * sample;
            var abs = Mathf.Abs(sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        var rms = Mathf.Sqrt(sumSquares / Mathf.Max(1, totalSamples));
        captureAudioLevelRms = Mathf.Lerp(captureAudioLevelRms, rms, 0.35f);
        captureAudioLevelPeak = Mathf.Max(peak, captureAudioLevelPeak - Time.unscaledDeltaTime * 0.8f);
        UpdateCaptureAudioSpectrum(sampleFrames, channels);
    }

    private void DecayCaptureAudioSpectrum()
    {
        captureAudioFftDrive = Mathf.Lerp(captureAudioFftDrive, 0f, 0.2f);
        for (var i = 0; i < captureAudioSpectrumBands.Length; i++)
        {
            captureAudioSpectrumBands[i] = Mathf.Lerp(captureAudioSpectrumBands[i], 0f, 0.2f);
            captureAudioSpectrumBandDb[i] = Mathf.Lerp(captureAudioSpectrumBandDb[i], CaptureAudioSpectrumDbFloor, 0.2f);
            captureAudioFilteredSpectrumBandDb[i] = Mathf.Lerp(captureAudioFilteredSpectrumBandDb[i], CaptureAudioSpectrumDbFloor, 0.2f);
        }
    }

    private void UpdateCaptureAudioSpectrum(int sampleFrames, int channels)
    {
        EnsureCaptureAudioEqDefaults();
        if (sampleFrames <= 0)
        {
            DecayCaptureAudioSpectrum();
            return;
        }

        var monoCount = Math.Min(sampleFrames, captureAudioMonoAnalysisBuffer.Length);
        var inputGain = Mathf.Pow(10f, captureAudioEqInputGainDb / 20f);
        for (var i = 0; i < monoCount; i++)
        {
            var sum = 0f;
            var baseIndex = i * channels;
            for (var ch = 0; ch < channels; ch++)
            {
                sum += captureAudioAnalysisBuffer[baseIndex + ch];
            }

            var monoSample = (sum / channels) * inputGain;
            captureAudioMonoAnalysisBuffer[i] = monoSample;
            captureAudioFilteredMonoAnalysisBuffer[i] = monoSample;
        }

        var sampleRate = CaptureAudioAnalysisSampleRate();
        var eqA = captureAudioFftEnabled ? CreateCaptureAudioEqBiquad(sampleRate, captureAudioEqBands[0]) : default;
        var eqB = captureAudioFftEnabled ? CreateCaptureAudioEqBiquad(sampleRate, captureAudioEqBands[1]) : default;
        var eqC = captureAudioFftEnabled ? CreateCaptureAudioEqBiquad(sampleRate, captureAudioEqBands[2]) : default;
        var eqD = captureAudioFftEnabled ? CreateCaptureAudioEqBiquad(sampleRate, captureAudioEqBands[3]) : default;
        var outputGain = Mathf.Pow(10f, captureAudioEqOutputGainDb / 20f);
        var z1A = 0f;
        var z2A = 0f;
        var z1B = 0f;
        var z2B = 0f;
        var z1C = 0f;
        var z2C = 0f;
        var z1D = 0f;
        var z2D = 0f;
        for (var i = 0; i < monoCount; i++)
        {
            var filtered = captureAudioFilteredMonoAnalysisBuffer[i];
            filtered = ProcessBiquadSample(eqA, filtered, ref z1A, ref z2A);
            filtered = ProcessBiquadSample(eqB, filtered, ref z1B, ref z2B);
            filtered = ProcessBiquadSample(eqC, filtered, ref z1C, ref z2C);
            filtered = ProcessBiquadSample(eqD, filtered, ref z1D, ref z2D);
            filtered *= outputGain;
            captureAudioFilteredMonoAnalysisBuffer[i] = filtered;
        }

        var fftBinCount = Math.Min(CaptureAudioFftBinCount, monoCount / 2);
        if (fftBinCount <= 0)
        {
            DecayCaptureAudioSpectrum();
            return;
        }

        Array.Clear(captureAudioSpectrumBins, 0, captureAudioSpectrumBins.Length);
        Array.Clear(captureAudioFilteredSpectrumBins, 0, captureAudioFilteredSpectrumBins.Length);
        for (var bin = 0; bin < fftBinCount; bin++)
        {
            var frequencyIndex = bin + 1;
            var rawReal = 0f;
            var rawImaginary = 0f;
            var filteredReal = 0f;
            var filteredImaginary = 0f;
            for (var sample = 0; sample < monoCount; sample++)
            {
                var angle = (2f * Mathf.PI * frequencyIndex * sample) / monoCount;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                var window = 0.5f - 0.5f * Mathf.Cos((2f * Mathf.PI * sample) / Mathf.Max(1, monoCount - 1));
                var rawValue = captureAudioMonoAnalysisBuffer[sample] * window;
                var filteredValue = captureAudioFilteredMonoAnalysisBuffer[sample] * window;
                rawReal += rawValue * cosine;
                rawImaginary -= rawValue * sine;
                filteredReal += filteredValue * cosine;
                filteredImaginary -= filteredValue * sine;
            }

            captureAudioSpectrumBins[bin] = Mathf.Sqrt(rawReal * rawReal + rawImaginary * rawImaginary) / monoCount;
            captureAudioFilteredSpectrumBins[bin] = Mathf.Sqrt(filteredReal * filteredReal + filteredImaginary * filteredImaginary) / monoCount;
        }

        var filteredEnergy = 0f;
        var filteredBandCount = 0;
        for (var band = 0; band < captureAudioSpectrumBands.Length; band++)
        {
            var start = Mathf.Clamp(Mathf.FloorToInt((band * fftBinCount) / (float)captureAudioSpectrumBands.Length), 0, fftBinCount - 1);
            var end = Mathf.Clamp(Mathf.FloorToInt(((band + 1) * fftBinCount) / (float)captureAudioSpectrumBands.Length), start + 1, fftBinCount);
            var magnitude = 0f;
            var filteredMagnitude = 0f;
            var count = 0;
            for (var bin = start; bin < end; bin++)
            {
                magnitude += captureAudioSpectrumBins[bin];
                filteredMagnitude += captureAudioFilteredSpectrumBins[bin];
                count++;
            }

            var average = count <= 0 ? 0f : magnitude / count;
            var filteredAverage = count <= 0 ? 0f : filteredMagnitude / count;
            var db = Mathf.Clamp(20f * Mathf.Log10(Mathf.Max(average, 0.000001f)), CaptureAudioSpectrumDbFloor, CaptureAudioSpectrumDbCeiling);
            var filteredDb = Mathf.Clamp(20f * Mathf.Log10(Mathf.Max(filteredAverage, 0.000001f)), CaptureAudioSpectrumDbFloor, CaptureAudioSpectrumDbCeiling);
            var normalized = Mathf.Clamp01(Mathf.Log10(1f + average * 180f) / Mathf.Log10(181f));
            var filteredNormalized = Mathf.Clamp01(Mathf.Log10(1f + filteredAverage * 180f) / Mathf.Log10(181f));
            captureAudioSpectrumBands[band] = Mathf.Lerp(captureAudioSpectrumBands[band], normalized, 0.38f);
            captureAudioSpectrumBandDb[band] = Mathf.Lerp(captureAudioSpectrumBandDb[band], db, 0.38f);
            captureAudioFilteredSpectrumBandDb[band] = Mathf.Lerp(captureAudioFilteredSpectrumBandDb[band], filteredDb, 0.38f);
            if (captureAudioFftEnabled)
            {
                filteredEnergy += filteredNormalized;
                filteredBandCount++;
            }
        }

        var drive = captureAudioFftEnabled && filteredBandCount > 0 ? filteredEnergy / filteredBandCount : 0f;
        captureAudioFftDrive = Mathf.Lerp(captureAudioFftDrive, drive, 0.38f);
    }

}
