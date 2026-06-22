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
    private void InitializeVideoLayers()
    {
        if (vjLayers != null)
        {
            return;
        }

        vjLayers = new LayerState[VjLayerCount];
        for (var i = 0; i < vjLayers.Length; i++)
        {
            vjLayers[i] = CreateLayerState(LayerSlotLabel(i), outputWidth, outputHeight, GeneratorRenderLayerStart + i);
        }
        browserPreviewSlots = new BrowserVideoSlot[0];
    }

    private LayerState CreateLayerState(string name, int width, int height, int generatorLayer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var renderTexture = CreateManagedRenderTexture(name + " Source", width, height, 0);
        var effectTexture = CreateManagedRenderTexture(name + " Effect", width, height, 0);
        var effectScratchTexture = CreateManagedRenderTexture(name + " Effect Scratch", width, height, 0);
        var previewTexture = CreateManagedRenderTexture(name + " Preview", width, height, 0);

        var player = go.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.skipOnDrop = true;

        var layer = new LayerState
        {
            Player = player,
            Texture = renderTexture,
            EffectTexture = effectTexture,
            EffectScratchTexture = effectScratchTexture,
            PreviewTexture = previewTexture,
            TextSource = CreateTextSourceState(name + " Text", width, height, Mathf.Clamp(generatorLayer, 0, 31), renderTexture),
            Enabled = true,
            AudioOutputEnabled = true,
            Opacity = 1f,
            Scale = 1f,
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleFftAmount = 1f,
            BlendMode = LayerBlendMode.Alpha,
            SourceKind = LayerSourceKind.None,
            Generator = CreateGeneratorState(name + " Generator", width, height, generatorLayer)
        };
        layer.Effect.TargetLayers[Mathf.Clamp(generatorLayer - GeneratorRenderLayerStart, 0, VjLayerCount - 1)] = true;
        return layer;
    }

    private LayerState CreateStepSequencerMediaSlotState(string name, int width, int height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var renderTexture = CreateManagedRenderTexture(name + " Source", width, height, 0);
        var previewTexture = CreateManagedRenderTexture(name + " Preview", width, height, 0);
        var player = go.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.skipOnDrop = true;

        return new LayerState
        {
            Player = player,
            Texture = renderTexture,
            PreviewTexture = previewTexture,
            AudioOutputEnabled = false,
            Enabled = true,
            Opacity = 1f,
            Scale = 1f,
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleFftAmount = 1f,
            BlendMode = LayerBlendMode.Alpha,
            SourceKind = LayerSourceKind.None
        };
    }

    private void InitializeProgramOutputScene()
    {
        if (programOutputRoot != null)
        {
            return;
        }

        EnsureCompositeRenderTextures();

        programOutputRoot = new GameObject("Program Output Root");
        programOutputRoot.transform.SetParent(transform, false);
        programOutputRoot.SetActive(false);

        var cameraGo = new GameObject("Program Output Camera");
        cameraGo.transform.SetParent(programOutputRoot.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        programOutputCamera = cameraGo.AddComponent<Camera>();
        programOutputCamera.orthographic = true;
        programOutputCamera.orthographicSize = 4.5f;
        programOutputCamera.clearFlags = CameraClearFlags.SolidColor;
        programOutputCamera.backgroundColor = Color.black;
        programOutputCamera.cullingMask = 1 << ProgramOutputRenderLayer;
        programOutputCamera.targetDisplay = 0;
        programOutputCamera.nearClipPlane = 0.1f;
        programOutputCamera.farClipPlane = 30f;

        programOutputRenderers = new MeshRenderer[1];
        var shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        for (var i = 0; i < programOutputRenderers.Length; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Program Output Final";
            quad.transform.SetParent(programOutputRoot.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0f);
            quad.transform.localScale = new Vector3(16f, 9f, 1f);
            SetLayerRecursive(quad, ProgramOutputRenderLayer);
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            var renderer = quad.GetComponent<MeshRenderer>();
            var material = new Material(shader);
            material.color = new Color(1f, 1f, 1f, 0f);
            ApplyBlendModeToMaterial(material, LayerBlendMode.Alpha);
            renderer.sharedMaterial = material;
            programOutputRenderers[i] = renderer;
        }
    }

    private void DestroyProgramOutputScene()
    {
        if (programOutputRoot != null)
        {
            Destroy(programOutputRoot);
            programOutputRoot = null;
            programOutputCamera = null;
            programOutputRenderers = null;
            activeProgramDisplay = -1;
        }
        DestroyCompositeRenderTextures();
    }

    private void EnsureCompositeRenderTextures()
    {
        mainSourceCompositeTexture = EnsureCompositeRenderTexture(mainSourceCompositeTexture, "Main Source Composite", outputWidth, outputHeight);
        mainEffectCompositeTexture = EnsureCompositeRenderTexture(mainEffectCompositeTexture, "Main Effect Composite", outputWidth, outputHeight);
        overlayCompositeTexture = EnsureCompositeRenderTexture(overlayCompositeTexture, "Overlay Composite", outputWidth, outputHeight);
        finalCompositeTexture = EnsureCompositeRenderTexture(finalCompositeTexture, "Final Composite", outputWidth, outputHeight);
        compositeScratchTexture = EnsureCompositeRenderTexture(compositeScratchTexture, "Composite Scratch", outputWidth, outputHeight);
    }

    private static RenderTexture EnsureCompositeRenderTexture(RenderTexture texture, string name, int width, int height)
    {
        width = Mathf.Max(320, width);
        height = Mathf.Max(180, height);
        if (texture != null && texture.width == width && texture.height == height)
        {
            if (!texture.IsCreated())
            {
                texture.Create();
            }
            return texture;
        }

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }

        texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Create();
        return texture;
    }

    private static RenderTexture CreateManagedRenderTexture(string name, int width, int height, int depth)
    {
        var texture = new RenderTexture(Mathf.Max(320, width), Mathf.Max(180, height), depth, RenderTextureFormat.ARGB32);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Create();
        return texture;
    }

    private void DestroyCompositeRenderTextures()
    {
        ReleaseRenderTexture(ref mainSourceCompositeTexture);
        ReleaseRenderTexture(ref mainEffectCompositeTexture);
        ReleaseRenderTexture(ref overlayCompositeTexture);
        ReleaseRenderTexture(ref finalCompositeTexture);
        ReleaseRenderTexture(ref compositeScratchTexture);
    }

    private static void ReleaseRenderTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        Destroy(texture);
        texture = null;
    }

    private void StopVideoLayers()
    {
        if (vjLayers != null)
        {
            for (var i = 0; i < vjLayers.Length; i++)
            {
                DestroyLayer(vjLayers[i]);
            }
            vjLayers = null;
        }

        if (browserPreviewSlots != null)
        {
            for (var i = 0; i < browserPreviewSlots.Length; i++)
            {
                DestroyBrowserSlot(browserPreviewSlots[i]);
            }
            browserPreviewSlots = null;
        }
    }

    private static void DestroyLayer(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }
        if (layer.Player != null)
        {
            layer.Player.Stop();
            Destroy(layer.Player.gameObject);
        }
        if (layer.Texture != null)
        {
            layer.Texture.Release();
            Destroy(layer.Texture);
        }
        if (layer.EffectTexture != null)
        {
            layer.EffectTexture.Release();
            Destroy(layer.EffectTexture);
        }
        if (layer.EffectScratchTexture != null)
        {
            layer.EffectScratchTexture.Release();
            Destroy(layer.EffectScratchTexture);
        }
        if (layer.PreviewTexture != null)
        {
            layer.PreviewTexture.Release();
            Destroy(layer.PreviewTexture);
        }
        DestroyTextSource(layer.TextSource);
        if (layer.OwnsStaticTexture && layer.StaticTexture != null)
        {
            Destroy(layer.StaticTexture);
            layer.StaticTexture = null;
        }
        if (layer.OwnsOfflineHoldTexture && layer.OfflineHoldTexture != null)
        {
            Destroy(layer.OfflineHoldTexture);
            layer.OfflineHoldTexture = null;
        }
        DestroyGenerator(layer.Generator);
    }

    private TextSourceState CreateTextSourceState(string name, int width, int height, int renderLayer, RenderTexture targetTexture)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        var cameraGo = new GameObject(name + " Camera");
        cameraGo.transform.SetParent(root.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        SetLayerRecursive(cameraGo, renderLayer);
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << renderLayer;
        camera.targetTexture = targetTexture;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30f;

        var textGo = new GameObject(name + " Mesh");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = Vector3.zero;
        SetLayerRecursive(textGo, renderLayer);
        var textMesh = textGo.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.fontSize = 96;
        textMesh.characterSize = 0.12f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (font != null)
        {
            textMesh.font = font;
            var renderer = textGo.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
            }
        }

        return new TextSourceState
        {
            Root = root,
            Camera = camera,
            TextMesh = textMesh,
            Renderer = textGo.GetComponent<MeshRenderer>()
        };
    }

    private static void DestroyTextSource(TextSourceState state)
    {
        if (state == null || state.Root == null)
        {
            return;
        }
        Destroy(state.Root);
    }

    private static void DestroyBrowserSlot(BrowserVideoSlot slot)
    {
        if (slot == null)
        {
            return;
        }
        if (slot.Texture != null)
        {
            Destroy(slot.Texture);
            slot.Texture = null;
        }
    }

}
