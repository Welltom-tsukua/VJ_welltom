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
    private static string ExtractYoutubeVideoId(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var text = value.Trim();
        var direct = Regex.Match(text, "^[A-Za-z0-9_-]{11}$");
        if (direct.Success)
        {
            return text;
        }

        var patterns = new[]
        {
            "[?&]v=([A-Za-z0-9_-]{11})",
            "youtu\\.be/([A-Za-z0-9_-]{11})",
            "/shorts/([A-Za-z0-9_-]{11})",
            "/embed/([A-Za-z0-9_-]{11})"
        };
        for (var i = 0; i < patterns.Length; i++)
        {
            var match = Regex.Match(text, patterns[i]);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private GeneratorState CreateGeneratorState(string name, int width, int height, int renderLayer)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.layer = renderLayer;

        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        Material primaryMaterial = null;
        var parts = new List<GeneratorPart>(5);
        for (var i = 0; i < 5; i++)
        {
            var meshObject = new GameObject(name + " Part " + (i + 1).ToString(CultureInfo.InvariantCulture));
            meshObject.transform.SetParent(root.transform, false);
            meshObject.layer = renderLayer;
            var filter = meshObject.AddComponent<MeshFilter>();
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            var material = CreateGeneratorMaterial();
            if (material != null)
            {
                ApplyMaterialTexture(material, Texture2D.whiteTexture);
                renderer.sharedMaterial = material;
                if (primaryMaterial == null)
                {
                    primaryMaterial = material;
                }
            }
            filter.sharedMesh = BuildCubeMesh();
            parts.Add(new GeneratorPart
            {
                Transform = meshObject.transform,
                Filter = filter,
                Renderer = renderer,
                Material = material
            });
        }

        var tunnelParts = new List<GeneratorPart>(GeneratorTunnelColumns * GeneratorTunnelRows * GeneratorTunnelDepthSlices);
        for (var i = 0; i < GeneratorTunnelColumns * GeneratorTunnelRows * GeneratorTunnelDepthSlices; i++)
        {
            var meshObject = new GameObject(name + " Tunnel Part " + (i + 1).ToString(CultureInfo.InvariantCulture));
            meshObject.transform.SetParent(root.transform, false);
            meshObject.layer = renderLayer;
            var filter = meshObject.AddComponent<MeshFilter>();
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            var material = CreateGeneratorMaterial();
            if (material != null)
            {
                ApplyMaterialTexture(material, Texture2D.whiteTexture);
                renderer.sharedMaterial = material;
            }
            filter.sharedMesh = BuildCubeMesh();
            tunnelParts.Add(new GeneratorPart
            {
                Transform = meshObject.transform,
                Filter = filter,
                Renderer = renderer,
                Material = material
            });
        }

        var particleRoot = new GameObject(name + " Particles");
        particleRoot.transform.SetParent(root.transform, false);
        particleRoot.layer = renderLayer;
        var particleParts = new List<GeneratorPart>(GeneratorParticleCount);
        for (var i = 0; i < GeneratorParticleCount; i++)
        {
            var particleObject = new GameObject(name + " Particle " + (i + 1).ToString(CultureInfo.InvariantCulture));
            particleObject.transform.SetParent(particleRoot.transform, false);
            particleObject.layer = renderLayer;
            var filter = particleObject.AddComponent<MeshFilter>();
            var renderer = particleObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            var material = CreateGeneratorMaterial();
            if (material != null)
            {
                ApplyMaterialTexture(material, Texture2D.whiteTexture);
                renderer.sharedMaterial = material;
            }
            filter.sharedMesh = BuildTetrahedronMesh();
            particleParts.Add(new GeneratorPart
            {
                Transform = particleObject.transform,
                Filter = filter,
                Renderer = renderer,
                Material = material
            });
        }

        var lightingRoot = new GameObject(name + " Lighting");
        lightingRoot.transform.SetParent(root.transform, false);
        lightingRoot.layer = renderLayer;
        var lightingLights = new List<GeneratorMovingLight>(GeneratorLightingMovingLightCount);
        var movingLightCubeMesh = BuildCubeMesh();
        var movingLightCylinderMesh = GetBuiltinPrimitiveMesh(PrimitiveType.Cylinder);
        for (var i = 0; i < GeneratorLightingMovingLightCount; i++)
        {
            var movingLight = CreateGeneratorMovingLight(name + " Moving Light " + (i + 1).ToString(CultureInfo.InvariantCulture), lightingRoot.transform, renderLayer, movingLightCubeMesh, movingLightCylinderMesh);
            lightingLights.Add(movingLight);
        }
        var lightingController = CreateGeneratorLightingController(name + " Lighting", lightingRoot.transform, lightingLights);

        var groundObject = new GameObject(name + " Ground");
        groundObject.transform.SetParent(root.transform, false);
        groundObject.transform.localPosition = new Vector3(0f, -2.15f, 0f);
        groundObject.transform.localRotation = Quaternion.identity;
        groundObject.transform.localScale = new Vector3(8f, 1f, 8f);
        groundObject.layer = renderLayer;
        var groundFilter = groundObject.AddComponent<MeshFilter>();
        groundFilter.sharedMesh = BuildGroundPlaneMesh();
        var groundRenderer = groundObject.AddComponent<MeshRenderer>();
        groundRenderer.enabled = false;
        var groundMaterial = CreateGeneratorMaterial();
        if (groundMaterial != null)
        {
            ApplyMaterialTexture(groundMaterial, Texture2D.whiteTexture);
            if (groundMaterial.HasProperty("_Color"))
            {
                groundMaterial.SetColor("_Color", new Color(0.28f, 0.30f, 0.32f, 1f));
            }
            groundRenderer.sharedMaterial = groundMaterial;
        }

        var surroundScreenRoot = new GameObject(name + " Surround Screens");
        surroundScreenRoot.transform.SetParent(root.transform, false);
        surroundScreenRoot.layer = renderLayer;
        var surroundScreens = new List<GeneratorScreen>(GeneratorSurroundScreenCount);
        var surroundDirections = BuildGeneratorScreenDirections(GeneratorSurroundScreenCount);
        for (var i = 0; i < surroundDirections.Length; i++)
        {
            var screenObject = new GameObject(name + " Surround Screen " + (i + 1).ToString(CultureInfo.InvariantCulture));
            screenObject.transform.SetParent(surroundScreenRoot.transform, false);
            screenObject.layer = renderLayer;
            var screenFilter = screenObject.AddComponent<MeshFilter>();
            screenFilter.sharedMesh = BuildGeneratorScreenMesh();
            var screenRenderer = screenObject.AddComponent<MeshRenderer>();
            screenRenderer.enabled = false;
            var screenMaterial = CreateGeneratorMaterial();
            if (screenMaterial != null)
            {
                ApplyMaterialTexture(screenMaterial, Texture2D.whiteTexture);
                screenRenderer.sharedMaterial = screenMaterial;
            }
            surroundScreens.Add(new GeneratorScreen
            {
                Transform = screenObject.transform,
                Renderer = screenRenderer,
                Material = screenMaterial,
                Direction = surroundDirections[i]
            });
        }

        var cameraObject = new GameObject(name + " Camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.layer = renderLayer;
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);
        cameraObject.transform.localRotation = Quaternion.identity;
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.targetTexture = renderTexture;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << renderLayer;
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 50f;
        var skybox = cameraObject.AddComponent<Skybox>();
        skybox.enabled = false;

        return new GeneratorState
        {
            Root = root,
            Pivot = parts[0].Transform,
            Filter = parts[0].Filter,
            Renderer = parts[0].Renderer,
            Parts = parts,
            TunnelParts = tunnelParts,
            ParticleRoot = particleRoot,
            ParticleParts = particleParts,
            LightingRoot = lightingRoot,
            LightingLights = lightingLights,
            LightingController = lightingController,
            Material = primaryMaterial,
            Camera = camera,
            Skybox = skybox,
            Ground = groundObject,
            GroundRenderer = groundRenderer,
            GroundMaterial = groundMaterial,
            SurroundScreenRoot = surroundScreenRoot,
            SurroundScreens = surroundScreens,
            CurrentTexture = Texture2D.whiteTexture,
            Texture = renderTexture,
            RenderLayer = renderLayer,
            Shape = GeneratorShape.Cube,
            PresentationMode = GeneratorPresentationMode.AroundObject,
            CameraWorkMode = GeneratorCameraWorkMode.Orbit,
            ObjectOrbitMode = GeneratorObjectOrbitMode.None,
            ObjectSpinEnabled = true,
            ObjectBpmPulseEnabled = true,
            CameraDistance = GeneratorDefaultCameraDistance,
            ScreenVisible = true,
            ParticlesEnabled = false,
            SurroundScreensEnabled = false,
            SurroundScreenSourceLayerIndex = -1,
            FaceImageFolderPath = PlayerPrefs.GetString(GeneratorFaceFolderPrefKey, ""),
            FaceImageFolderInput = PlayerPrefs.GetString(GeneratorFaceFolderPrefKey, ""),
            TransparentBackground = true,
            LightingRigMode = GeneratorLightingRigMode.AlternateBlink,
            MotionSeed = UnityEngine.Random.value * 1000f,
            ScriptText = DefaultGeneratorScript(),
            ModelPositionInput = "0,0,0",
            ModelRotationInput = "0,0,0",
            ModelScaleInput = "1,1,1"
        };
    }

    private static void DestroyGenerator(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }
        if (generator.Texture != null)
        {
            generator.Texture.Release();
            Destroy(generator.Texture);
        }
        if (generator.Parts != null)
        {
            for (var i = 0; i < generator.Parts.Count; i++)
            {
                if (generator.Parts[i] != null && generator.Parts[i].Material != null)
                {
                    Destroy(generator.Parts[i].Material);
                    generator.Parts[i].Material = null;
                }
            }
            generator.Material = null;
        }
        if (generator.TunnelParts != null)
        {
            for (var i = 0; i < generator.TunnelParts.Count; i++)
            {
                if (generator.TunnelParts[i] != null && generator.TunnelParts[i].Material != null)
                {
                    Destroy(generator.TunnelParts[i].Material);
                    generator.TunnelParts[i].Material = null;
                }
            }
        }
        if (generator.ParticleParts != null)
        {
            for (var i = 0; i < generator.ParticleParts.Count; i++)
            {
                if (generator.ParticleParts[i] != null && generator.ParticleParts[i].Material != null)
                {
                    Destroy(generator.ParticleParts[i].Material);
                    generator.ParticleParts[i].Material = null;
                }
            }
        }
        if (generator.LightingLights != null)
        {
            for (var i = 0; i < generator.LightingLights.Count; i++)
            {
                var light = generator.LightingLights[i];
                if (light == null)
                {
                    continue;
                }
                if (light.HeadMaterial != null)
                {
                    Destroy(light.HeadMaterial);
                    light.HeadMaterial = null;
                }
                if (light.BeamMaterial != null)
                {
                    Destroy(light.BeamMaterial);
                    light.BeamMaterial = null;
                }
            }
        }
        if (generator.Material != null)
        {
            Destroy(generator.Material);
            generator.Material = null;
        }
        if (generator.Root != null)
        {
            Destroy(generator.Root);
        }
    }

    private void RefreshGeneratorResources()
    {
        generatorSkyboxMaterials = Resources.LoadAll<Material>("Skyboxes") ?? new Material[0];
        generatorModelPrefabs = Resources.LoadAll<GameObject>("Models") ?? new GameObject[0];
        Array.Sort(generatorSkyboxMaterials, (a, b) => string.Compare(a == null ? "" : a.name, b == null ? "" : b.name, StringComparison.OrdinalIgnoreCase));
        Array.Sort(generatorModelPrefabs, (a, b) => string.Compare(a == null ? "" : a.name, b == null ? "" : b.name, StringComparison.OrdinalIgnoreCase));
        generatorResourcesLoaded = true;
        AddLog("3D Object resources: skyboxes=" + generatorSkyboxMaterials.Length.ToString(CultureInfo.InvariantCulture) + " models=" + generatorModelPrefabs.Length.ToString(CultureInfo.InvariantCulture));
    }

    private void EnsureLightBeamPerformanceTypes()
    {
        if (lightBeamMovingLightType != null &&
            lightBeamPanType != null &&
            lightBeamTiltType != null &&
            lightBeamHeadType != null &&
            lightBeamNoribenBeamType != null &&
            lightBeamPerformanceType != null &&
            lightBeamLightGroupType != null)
        {
            return;
        }

        lightBeamMovingLightType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.MovingLight, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.MovingLight");
        lightBeamPanType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.Pan, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.Pan");
        lightBeamTiltType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.Tilt, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.Tilt");
        lightBeamHeadType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.LightHead, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.LightHead");
        lightBeamNoribenBeamType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.NoribenLightBeam, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.NoribenLightBeam");
        lightBeamPerformanceType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.LightBeamPerformance, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.LightBeamPerformance");
        lightBeamLightGroupType = ResolveLightBeamPerformanceType(
            "ProjectBlue.LightBeamPerformance.LightGroup, LightBeamPerformance",
            "ProjectBlue.LightBeamPerformance.LightGroup");
    }

    private static Mesh GetBuiltinPrimitiveMesh(PrimitiveType primitiveType)
    {
        var primitive = GameObject.CreatePrimitive(primitiveType);
        try
        {
            var filter = primitive.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
        finally
        {
            Destroy(primitive);
        }
    }

    private static Material CreateMovingLightBeamMaterial()
    {
        var shader = Shader.Find("Noriben/BeamLight");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }
        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat("_Intensity", 0.5f);
        }
        if (material.HasProperty("_ConeWidth"))
        {
            material.SetFloat("_ConeWidth", 0.08f);
        }
        if (material.HasProperty("_ConeLength"))
        {
            material.SetFloat("_ConeLength", 1.0f);
        }
        return material;
    }

    private static Material CreateMovingLightHeadMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.black);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        return material;
    }

    private GeneratorMovingLight CreateGeneratorMovingLight(string name, Transform parent, int renderLayer, Mesh cubeMesh, Mesh cylinderMesh)
    {
        EnsureLightBeamPerformanceTypes();

        var prefab = Resources.Load<GameObject>("LightBeamPerformance/MovingLight");
        if (prefab != null)
        {
            var instance = Instantiate(prefab, parent, false);
            instance.name = name;
            SetLayerRecursive(instance, renderLayer);

            var prefabMovingLightComponent = lightBeamMovingLightType == null ? null : instance.GetComponent(lightBeamMovingLightType);
            var prefabPanComponent = lightBeamPanType == null ? null : instance.GetComponentInChildren(lightBeamPanType, true) as Component;
            var prefabTiltComponent = lightBeamTiltType == null ? null : instance.GetComponentInChildren(lightBeamTiltType, true) as Component;
            var prefabHeadComponent = lightBeamHeadType == null ? null : instance.GetComponentInChildren(lightBeamHeadType, true) as Component;
            var prefabBeamComponent = lightBeamNoribenBeamType == null ? null : instance.GetComponentInChildren(lightBeamNoribenBeamType, true) as Component;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer prefabHeadRenderer = null;
            MeshRenderer prefabBeamRenderer = null;
            for (var r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null)
                {
                    continue;
                }
                renderer.enabled = true;
                if (renderer.gameObject.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0 && prefabHeadRenderer == null)
                {
                    prefabHeadRenderer = renderer;
                }
                if (renderer.gameObject.name.IndexOf("Beam", StringComparison.OrdinalIgnoreCase) >= 0 && prefabBeamRenderer == null)
                {
                    prefabBeamRenderer = renderer;
                }
            }

            var prefabSpotLight = instance.GetComponentInChildren<Light>(true);
            if (prefabSpotLight == null && prefabHeadComponent is Component headTransformComponent)
            {
                prefabSpotLight = headTransformComponent.gameObject.AddComponent<Light>();
                prefabSpotLight.type = LightType.Spot;
            }
            if (prefabSpotLight != null)
            {
                prefabSpotLight.enabled = true;
                prefabSpotLight.intensity = 0f;
                prefabSpotLight.range = GeneratorLightingSpotRange;
                prefabSpotLight.spotAngle = GeneratorLightingSpotAngle;
                prefabSpotLight.color = Color.white;
                prefabSpotLight.shadows = LightShadows.None;
            }

            return new GeneratorMovingLight
            {
                Root = instance.transform,
                PanTransform = prefabPanComponent == null ? null : prefabPanComponent.transform,
                TiltTransform = prefabTiltComponent == null ? null : prefabTiltComponent.transform,
                HeadTransform = prefabHeadComponent == null ? null : prefabHeadComponent.transform,
                HeadRenderer = prefabHeadRenderer,
                BeamRenderer = prefabBeamRenderer,
                SpotLight = prefabSpotLight,
                MovingLightComponent = prefabMovingLightComponent,
                PanComponent = prefabPanComponent,
                TiltComponent = prefabTiltComponent,
                HeadComponent = prefabHeadComponent,
                BeamComponent = prefabBeamComponent
            };
        }

        var rootObject = new GameObject(name);
        rootObject.transform.SetParent(parent, false);
        rootObject.layer = renderLayer;

        var panObject = new GameObject(name + " Pan");
        panObject.transform.SetParent(rootObject.transform, false);
        panObject.layer = renderLayer;
        var panFilter = panObject.AddComponent<MeshFilter>();
        panFilter.sharedMesh = cubeMesh;
        var panRenderer = panObject.AddComponent<MeshRenderer>();
        var panMaterial = CreateMovingLightHeadMaterial();
        if (panMaterial != null)
        {
            panRenderer.sharedMaterial = panMaterial;
        }
        panObject.transform.localScale = new Vector3(0.34f, 0.16f, 0.34f);
        panObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);

        var panComponent = lightBeamPanType == null ? null : panObject.AddComponent(lightBeamPanType);

        var tiltObject = new GameObject(name + " Tilt");
        tiltObject.transform.SetParent(panObject.transform, false);
        tiltObject.layer = renderLayer;
        var tiltFilter = tiltObject.AddComponent<MeshFilter>();
        tiltFilter.sharedMesh = cubeMesh;
        var tiltRenderer = tiltObject.AddComponent<MeshRenderer>();
        var tiltMaterial = CreateMovingLightHeadMaterial();
        if (tiltMaterial != null)
        {
            tiltRenderer.sharedMaterial = tiltMaterial;
        }
        tiltObject.transform.localScale = new Vector3(0.22f, 0.20f, 0.22f);
        tiltObject.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        var tiltComponent = lightBeamTiltType == null ? null : tiltObject.AddComponent(lightBeamTiltType);

        var headObject = new GameObject(name + " Head");
        headObject.transform.SetParent(tiltObject.transform, false);
        headObject.layer = renderLayer;
        var headFilter = headObject.AddComponent<MeshFilter>();
        headFilter.sharedMesh = cubeMesh;
        var headRenderer = headObject.AddComponent<MeshRenderer>();
        headRenderer.enabled = true;
        var headMaterial = CreateMovingLightHeadMaterial();
        if (headMaterial != null)
        {
            headRenderer.sharedMaterial = headMaterial;
        }
        headObject.transform.localScale = new Vector3(0.16f, 0.14f, 0.28f);
        headObject.transform.localPosition = new Vector3(0f, 0.08f, 0.16f);

        var spotLight = headObject.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.enabled = true;
        spotLight.intensity = 0f;
        spotLight.range = GeneratorLightingSpotRange;
        spotLight.spotAngle = GeneratorLightingSpotAngle;
        spotLight.color = Color.white;
        spotLight.shadows = LightShadows.None;

        var beamObject = new GameObject(name + " Beam");
        beamObject.transform.SetParent(headObject.transform, false);
        beamObject.layer = renderLayer;
        var beamFilter = beamObject.AddComponent<MeshFilter>();
        beamFilter.sharedMesh = cylinderMesh;
        var beamRenderer = beamObject.AddComponent<MeshRenderer>();
        beamRenderer.enabled = true;
        var beamMaterial = CreateMovingLightBeamMaterial();
        if (beamMaterial != null)
        {
            beamRenderer.sharedMaterial = beamMaterial;
        }
        beamObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        beamObject.transform.localPosition = new Vector3(0f, 0f, GeneratorLightingMovingLightBeamLength * 0.5f);
        beamObject.transform.localScale = new Vector3(0.10f, GeneratorLightingMovingLightBeamLength * 0.5f, 0.10f);

        var beamComponent = lightBeamNoribenBeamType == null ? null : beamObject.AddComponent(lightBeamNoribenBeamType);
        if (beamComponent != null)
        {
            SetReflectionValue(beamComponent, "beamGeometry", beamRenderer);
        }
        var headComponent = lightBeamHeadType == null ? null : headObject.AddComponent(lightBeamHeadType);
        if (headComponent != null)
        {
            SetReflectionValue(headComponent, "beam", beamComponent);
            SetReflectionValue(headComponent, "renderer", headRenderer);
            SetReflectionValue(headComponent, "litShader", headMaterial == null ? null : headMaterial.shader);
        }

        var movingLightComponent = lightBeamMovingLightType == null ? null : rootObject.AddComponent(lightBeamMovingLightType);
        if (movingLightComponent != null)
        {
            SetReflectionValue(movingLightComponent, "head", headComponent);
            SetReflectionValue(movingLightComponent, "pan", panComponent);
            SetReflectionValue(movingLightComponent, "tilt", tiltComponent);
        }

        return new GeneratorMovingLight
        {
            Root = rootObject.transform,
            PanTransform = panObject.transform,
            TiltTransform = tiltObject.transform,
            HeadTransform = headObject.transform,
            HeadRenderer = headRenderer,
            BeamRenderer = beamRenderer,
            HeadMaterial = headMaterial,
            BeamMaterial = beamMaterial,
            SpotLight = spotLight,
            MovingLightComponent = movingLightComponent,
            PanComponent = panComponent,
            TiltComponent = tiltComponent,
            HeadComponent = headComponent,
            BeamComponent = beamComponent
        };
    }

    private GeneratorLightingController CreateGeneratorLightingController(string name, Transform parent, List<GeneratorMovingLight> lightingLights)
    {
        EnsureLightBeamPerformanceTypes();
        if (lightBeamPerformanceType == null || lightBeamLightGroupType == null || lightBeamMovingLightType == null)
        {
            return null;
        }

        var controllerRoot = new GameObject(name + " Controller");
        controllerRoot.transform.SetParent(parent, false);

        var targetObject = new GameObject(name + " Target");
        targetObject.transform.SetParent(controllerRoot.transform, false);

        var groupComponent = controllerRoot.AddComponent(lightBeamLightGroupType);
        var movingLightListType = typeof(List<>).MakeGenericType(lightBeamMovingLightType);
        var movingLights = Activator.CreateInstance(movingLightListType) as System.Collections.IList;
        if (movingLights != null && lightingLights != null)
        {
            for (var i = 0; i < lightingLights.Count; i++)
            {
                var movingLight = lightingLights[i];
                if (movingLight != null && movingLight.MovingLightComponent != null)
                {
                    movingLights.Add(movingLight.MovingLightComponent);
                }
            }
            SetReflectionValue(groupComponent, "lights", movingLights);
        }

        var performanceComponent = controllerRoot.AddComponent(lightBeamPerformanceType);
        var groupListType = typeof(List<>).MakeGenericType(lightBeamLightGroupType);
        var groups = Activator.CreateInstance(groupListType) as System.Collections.IList;
        if (groups != null)
        {
            groups.Add(groupComponent);
            SetReflectionValue(performanceComponent, "lightGroup", groups);
        }
        SetReflectionValue(performanceComponent, "Target", targetObject.transform);
        TryInvokeReflectionMethod(performanceComponent, "Initialize");

        return new GeneratorLightingController
        {
            Root = controllerRoot,
            Target = targetObject.transform,
            GroupComponent = groupComponent,
            PerformanceComponent = performanceComponent
        };
    }

    private GameObject FindPreferredLightingScenePrefab()
    {
        if (generatorModelPrefabs == null || generatorModelPrefabs.Length == 0)
        {
            return null;
        }

        GameObject fallback = null;
        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab == null || !prefab.name.StartsWith("SceneBuildings_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = prefab;
            }

            if (prefab.name.IndexOf(PreferredLightingSceneKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return prefab;
            }
        }

        return fallback;
    }

    private void EnsureGeneratorLightingSceneSetup(GeneratorState generator)
    {
        if (generator == null || generator.PresentationMode != GeneratorPresentationMode.Lighting)
        {
            return;
        }

        EnsureGeneratorResources();
        var prefab = FindPreferredLightingScenePrefab();
        if (prefab == null)
        {
            return;
        }

        if (generator.SceneBuildingsInstance != null && string.Equals(generator.SceneBuildingsName, prefab.name, StringComparison.Ordinal))
        {
            return;
        }

        LoadGeneratorSceneLayer(generator, prefab, GeneratorSceneLayerKind.Buildings);
        generator.LightingSceneInitialized = true;
    }

    private void EnsureGeneratorResources()
    {
        if (!generatorResourcesLoaded)
        {
            RefreshGeneratorResources();
        }
    }

    private void SetGeneratorSkybox(GeneratorState generator, Material material)
    {
        if (generator == null || generator.Camera == null)
        {
            return;
        }

        generator.SkyboxMaterial = material;
        generator.SkyboxName = material == null ? "None" : material.name;
        if (generator.Skybox != null)
        {
            generator.Skybox.material = material;
            generator.Skybox.enabled = material != null;
        }
        ApplyGeneratorCameraBackground(generator);
    }

    private static void ApplyGeneratorCameraBackground(GeneratorState generator)
    {
        if (generator == null || generator.Camera == null)
        {
            return;
        }

        var hasSkybox = generator.SkyboxMaterial != null;
        if (generator.TransparentBackground)
        {
            generator.Camera.clearFlags = CameraClearFlags.SolidColor;
            generator.Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            if (generator.Skybox != null)
            {
                generator.Skybox.enabled = false;
            }
            return;
        }

        generator.Camera.clearFlags = hasSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        generator.Camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
        if (generator.Skybox != null)
        {
            generator.Skybox.enabled = hasSkybox;
        }
    }

    private static bool IsJapaneseOtakuCityGeneratorAsset(string name)
    {
        return !string.IsNullOrEmpty(name) && name.IndexOf("JapaneseOtakuCity", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyGeneratorAssetDefaultTransform(string assetName, out Vector3 position, out Vector3 rotation, out Vector3 scale, bool sceneLayer)
    {
        if (IsJapaneseOtakuCityGeneratorAsset(assetName))
        {
            position = sceneLayer
                ? new Vector3(0f, JapaneseOtakuCityBaseYOffset + GeneratorLightingMovingLightGroundOffset, 0f)
                : Vector3.zero;
            rotation = Vector3.zero;
            scale = Vector3.one;
            return;
        }

        position = sceneLayer ? new Vector3(0f, -3.2f, 0f) : Vector3.zero;
        rotation = Vector3.zero;
        scale = sceneLayer ? new Vector3(8f, 18f, 8f) : Vector3.one;
    }

    private void LoadGeneratorModel(GeneratorState generator, GameObject prefab)
    {
        if (generator == null || prefab == null)
        {
            return;
        }

        if (generator.ModelInstance != null)
        {
            Destroy(generator.ModelInstance);
            generator.ModelInstance = null;
        }
        UpdateGeneratorGroundVisibility(generator);

        var instance = Instantiate(prefab, generator.Root == null ? transform : generator.Root.transform);
        instance.name = "Model " + prefab.name;
        SetLayerRecursive(instance, generator.RenderLayer);
        generator.ModelInstance = instance;
        generator.ModelName = prefab.name;
        Vector3 defaultPosition;
        Vector3 defaultRotation;
        Vector3 defaultScale;
        ApplyGeneratorAssetDefaultTransform(prefab.name, out defaultPosition, out defaultRotation, out defaultScale, false);
        generator.ModelPositionInput = FormatVector3(defaultPosition);
        generator.ModelRotationInput = FormatVector3(defaultRotation);
        generator.ModelScaleInput = FormatVector3(defaultScale);
        UpdateGeneratorGroundVisibility(generator);
        ApplyGeneratorModelTransform(generator);
    }

    private void ClearGeneratorModel(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }
        if (generator.ModelInstance != null)
        {
            Destroy(generator.ModelInstance);
            generator.ModelInstance = null;
        }
        generator.ModelName = null;
        UpdateGeneratorGroundVisibility(generator);
    }

    private void ResetGeneratorToDefaultScene(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        generator.PresentationMode = GeneratorPresentationMode.AroundObject;
        generator.CameraWorkMode = GeneratorCameraWorkMode.Orbit;
        generator.ObjectOrbitMode = GeneratorObjectOrbitMode.None;
        generator.ObjectSpinEnabled = true;
        generator.ObjectBpmPulseEnabled = true;
        generator.CameraDistance = GeneratorDefaultCameraDistance;
        generator.ScreenVisible = true;
        generator.TransparentBackground = true;
        generator.ParticlesEnabled = false;
        generator.SurroundScreensEnabled = false;
        generator.SurroundScreenSourceLayerIndex = -1;
        generator.SurroundScreenSourceLayerName = null;
        generator.SurroundScreenCurrentTexture = null;
        generator.MotionSeed = UnityEngine.Random.value * 1000f;
        generator.ModelPositionInput = "0,0,0";
        generator.ModelRotationInput = "0,0,0";
        generator.ModelScaleInput = "1,1,1";

        SetGeneratorShape(generator, GeneratorShape.Cube);
        ClearGeneratorModel(generator);
        ClearGeneratorSceneLayer(generator, GeneratorSceneLayerKind.Buildings);
        ClearGeneratorSceneLayer(generator, GeneratorSceneLayerKind.Ground);
        ClearGeneratorSceneLayer(generator, GeneratorSceneLayerKind.Traffic);
        ClearGeneratorSceneLayer(generator, GeneratorSceneLayerKind.Vegetation);
        SetGeneratorVisible(generator, true);
        SetGeneratorSceneLayersVisible(generator, false);
        SetGeneratorLightingVisible(generator, false);
        SetGeneratorParticlesVisible(generator, false);
        SetGeneratorSurroundScreensVisible(generator, false);
        ApplyGeneratorCameraBackground(generator);
    }

    private void LoadGeneratorSceneLayer(GeneratorState generator, GameObject prefab, GeneratorSceneLayerKind kind)
    {
        if (generator == null || prefab == null)
        {
            return;
        }

        ClearGeneratorSceneLayer(generator, kind);

        var instance = Instantiate(prefab, generator.Root == null ? transform : generator.Root.transform);
        instance.name = kind.ToString() + " " + prefab.name;
        SetLayerRecursive(instance, generator.RenderLayer);
        ApplyGeneratorSceneLayerTransform(instance, prefab.name);
        SetGeneratorSceneLayerState(generator, kind, instance, prefab.name);
        UpdateGeneratorGroundVisibility(generator);
    }

    private void ClearGeneratorSceneLayer(GeneratorState generator, GeneratorSceneLayerKind kind)
    {
        if (generator == null)
        {
            return;
        }

        var instance = GetGeneratorSceneLayerInstance(generator, kind);
        if (instance != null)
        {
            Destroy(instance);
        }
        SetGeneratorSceneLayerState(generator, kind, null, null);
        UpdateGeneratorGroundVisibility(generator);
    }

    private static void ApplyGeneratorSceneLayerTransform(GameObject instance, string assetName)
    {
        if (instance == null)
        {
            return;
        }

        Vector3 position;
        Vector3 rotation;
        Vector3 scale;
        ApplyGeneratorAssetDefaultTransform(assetName, out position, out rotation, out scale, true);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(rotation);
        instance.transform.localScale = scale;
    }

    private static GameObject GetGeneratorSceneLayerInstance(GeneratorState generator, GeneratorSceneLayerKind kind)
    {
        if (generator == null)
        {
            return null;
        }

        switch (kind)
        {
            case GeneratorSceneLayerKind.Buildings:
                return generator.SceneBuildingsInstance;
            case GeneratorSceneLayerKind.Ground:
                return generator.SceneGroundInstance;
            case GeneratorSceneLayerKind.Traffic:
                return generator.SceneTrafficInstance;
            case GeneratorSceneLayerKind.Vegetation:
                return generator.SceneVegetationInstance;
            default:
                return null;
        }
    }

    private static string GetGeneratorSceneLayerName(GeneratorState generator, GeneratorSceneLayerKind kind)
    {
        if (generator == null)
        {
            return null;
        }

        switch (kind)
        {
            case GeneratorSceneLayerKind.Buildings:
                return generator.SceneBuildingsName;
            case GeneratorSceneLayerKind.Ground:
                return generator.SceneGroundName;
            case GeneratorSceneLayerKind.Traffic:
                return generator.SceneTrafficName;
            case GeneratorSceneLayerKind.Vegetation:
                return generator.SceneVegetationName;
            default:
                return null;
        }
    }

    private static void SetGeneratorSceneLayerState(GeneratorState generator, GeneratorSceneLayerKind kind, GameObject instance, string name)
    {
        if (generator == null)
        {
            return;
        }

        switch (kind)
        {
            case GeneratorSceneLayerKind.Buildings:
                generator.SceneBuildingsInstance = instance;
                generator.SceneBuildingsName = name;
                break;
            case GeneratorSceneLayerKind.Ground:
                generator.SceneGroundInstance = instance;
                generator.SceneGroundName = name;
                break;
            case GeneratorSceneLayerKind.Traffic:
                generator.SceneTrafficInstance = instance;
                generator.SceneTrafficName = name;
                break;
            case GeneratorSceneLayerKind.Vegetation:
                generator.SceneVegetationInstance = instance;
                generator.SceneVegetationName = name;
                break;
        }
    }

    private static void UpdateGeneratorGroundVisibility(GeneratorState generator)
    {
        var showLegacyGround = false;
        SetGeneratorGroundVisible(generator, showLegacyGround);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null)
        {
            return;
        }
        go.layer = layer;
        for (var i = 0; i < go.transform.childCount; i++)
        {
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }

    private static void ApplyGeneratorModelTransform(GeneratorState generator)
    {
        if (generator == null || generator.ModelInstance == null)
        {
            return;
        }

        Vector3 pos;
        Vector3 rot;
        Vector3 scale;
        if (!TryParseVector3(generator.ModelPositionInput, out pos))
        {
            pos = Vector3.zero;
        }
        if (!TryParseVector3(generator.ModelRotationInput, out rot))
        {
            rot = Vector3.zero;
        }
        if (!TryParseVector3(generator.ModelScaleInput, out scale))
        {
            scale = Vector3.one;
        }
        generator.ModelInstance.transform.localPosition = pos;
        generator.ModelInstance.transform.localRotation = Quaternion.Euler(rot);
        generator.ModelInstance.transform.localScale = new Vector3(Mathf.Max(0.001f, scale.x), Mathf.Max(0.001f, scale.y), Mathf.Max(0.001f, scale.z));
    }

    private static void ApplyGeneratorTexture(GeneratorState generator, Texture texture)
    {
        if (generator == null || texture == null)
        {
            return;
        }
        if (generator.Parts != null && generator.Parts.Count > 0)
        {
            for (var i = 0; i < generator.Parts.Count; i++)
            {
                if (generator.Parts[i] != null && generator.Parts[i].Material != null)
                {
                    ApplyMaterialTexture(generator.Parts[i].Material, texture);
                }
            }
            generator.Material = generator.Parts[0] == null ? null : generator.Parts[0].Material;
        }
        else if (generator.Material != null)
        {
            ApplyMaterialTexture(generator.Material, texture);
        }
        if (generator.TunnelParts != null)
        {
            for (var i = 0; i < generator.TunnelParts.Count; i++)
            {
                if (generator.TunnelParts[i] != null && generator.TunnelParts[i].Material != null)
                {
                    ApplyMaterialTexture(generator.TunnelParts[i].Material, texture);
                }
            }
        }
        if (generator.ParticleParts != null)
        {
            for (var i = 0; i < generator.ParticleParts.Count; i++)
            {
                if (generator.ParticleParts[i] != null && generator.ParticleParts[i].Material != null)
                {
                    ApplyMaterialTexture(generator.ParticleParts[i].Material, texture);
                }
            }
        }
        generator.CurrentTexture = texture;
    }

    private static void ApplyGeneratorSurroundScreenTexture(GeneratorState generator, Texture texture)
    {
        if (generator == null || texture == null || generator.SurroundScreens == null)
        {
            return;
        }

        for (var i = 0; i < generator.SurroundScreens.Count; i++)
        {
            var screen = generator.SurroundScreens[i];
            if (screen != null && screen.Material != null)
            {
                ApplyMaterialTexture(screen.Material, texture);
            }
        }
        generator.SurroundScreenCurrentTexture = texture;
    }

    private void UpdateGeneratorSurroundScreens(GeneratorState generator, Vector3 objectPosition)
    {
        if (generator == null || generator.SurroundScreens == null)
        {
            return;
        }

        var active = generator.SurroundScreensEnabled && generator.SurroundScreenSourceLayerIndex >= 0;
        Texture sourceTexture = null;
        if (active)
        {
            sourceTexture = GeneratorInputLayerTexture(generator.SurroundScreenSourceLayerIndex);
            active = sourceTexture != null;
        }

        if (!active)
        {
            for (var i = 0; i < generator.SurroundScreens.Count; i++)
            {
                var screen = generator.SurroundScreens[i];
                if (screen != null && screen.Renderer != null)
                {
                    screen.Renderer.enabled = false;
                }
            }
            return;
        }

        if (sourceTexture != generator.SurroundScreenCurrentTexture)
        {
            ApplyGeneratorSurroundScreenTexture(generator, sourceTexture);
        }

        for (var i = 0; i < generator.SurroundScreens.Count; i++)
        {
            var screen = generator.SurroundScreens[i];
            if (screen == null || screen.Transform == null)
            {
                continue;
            }

            var direction = screen.Direction.sqrMagnitude > 0.001f ? screen.Direction.normalized : Vector3.forward;
            var position = objectPosition + direction * GeneratorSurroundScreenRadius;
            screen.Transform.localPosition = position;
            screen.Transform.localRotation = Quaternion.LookRotation((objectPosition - position).normalized, Vector3.up);
            screen.Transform.localScale = new Vector3(GeneratorSurroundScreenWidth, GeneratorSurroundScreenHeight, 1f);
            if (screen.Renderer != null)
            {
                screen.Renderer.enabled = true;
            }
        }
    }

    private static void ClearGeneratorLayerTextureSource(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        generator.TextureSourceLayerIndex = -1;
        generator.TextureSourceLayerName = null;
        if (generator.SavedTextureSourceKind == GeneratorSavedTextureSourceKind.LayerOutput)
        {
            generator.SavedTextureSourceKind = GeneratorSavedTextureSourceKind.None;
        }
    }

    private static void SetGeneratorManualImageSource(GeneratorState generator, string imagePath)
    {
        if (generator == null)
        {
            return;
        }

        generator.SavedTextureSourceKind = GeneratorSavedTextureSourceKind.ImageFile;
        generator.FaceTextureImagePath = imagePath;
        generator.FaceTextureBltPlayerNumber = 0;
    }

    private static void SetGeneratorBltJacketSource(GeneratorState generator, int playerNumber)
    {
        if (generator == null)
        {
            return;
        }

        generator.SavedTextureSourceKind = GeneratorSavedTextureSourceKind.BltJacket;
        generator.FaceTextureImagePath = null;
        generator.FaceTextureBltPlayerNumber = playerNumber;
    }

    private void SetGeneratorLayerTextureSource(GeneratorState generator, int layerIndex)
    {
        if (generator == null || vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return;
        }

        generator.TextureSourceLayerIndex = layerIndex;
        generator.TextureSourceLayerName = LayerSlotLabel(layerIndex);
        generator.SavedTextureSourceKind = GeneratorSavedTextureSourceKind.LayerOutput;
        generator.FaceTextureImagePath = null;
        generator.FaceTextureBltPlayerNumber = 0;
        var sourceTexture = GeneratorInputLayerTexture(layerIndex);
        if (sourceTexture != null)
        {
            ApplyGeneratorTexture(generator, sourceTexture);
        }
    }

    private void RefreshGeneratorFaceImageFolder(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        if (generator.FaceImagePaths == null)
        {
            generator.FaceImagePaths = new List<string>();
        }
        generator.FaceImagePaths.Clear();
        generator.FaceImageError = null;

        var folder = generator.FaceImageFolderPath;
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }
        if (!Directory.Exists(folder))
        {
            generator.FaceImageError = "Image folder not found.";
            return;
        }

        try
        {
            var files = Directory.GetFiles(folder);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                if (IsImageFile(files[i]))
                {
                    generator.FaceImagePaths.Add(files[i]);
                }
            }
        }
        catch (Exception ex)
        {
            generator.FaceImageError = "Image scan failed: " + ex.Message;
        }
    }

    private void SaveGeneratorFaceFolderPreference(string folder)
    {
        PlayerPrefs.SetString(GeneratorFaceFolderPrefKey, folder ?? "");
        PlayerPrefs.Save();
    }

    private GeneratorPresetState CaptureGeneratorPresetState(GeneratorState generator)
    {
        return new GeneratorPresetState
        {
            Shape = generator.Shape,
            PresentationMode = generator.PresentationMode,
            CameraWorkMode = generator.CameraWorkMode,
            ObjectOrbitMode = generator.ObjectOrbitMode,
            ObjectSpinEnabled = generator.ObjectSpinEnabled,
            ObjectBpmPulseEnabled = generator.ObjectBpmPulseEnabled,
            ScreenVisible = generator.ScreenVisible,
            ParticlesEnabled = generator.ParticlesEnabled,
            SurroundScreensEnabled = generator.SurroundScreensEnabled,
            SurroundScreenSourceLayerIndex = generator.SurroundScreenSourceLayerIndex,
            TransparentBackground = generator.TransparentBackground,
            SkyboxName = generator.SkyboxMaterial == null ? null : generator.SkyboxMaterial.name,
            ModelName = generator.ModelName,
            ModelPositionInput = generator.ModelPositionInput,
            ModelRotationInput = generator.ModelRotationInput,
            ModelScaleInput = generator.ModelScaleInput,
            SceneBuildingsName = generator.SceneBuildingsName,
            SceneGroundName = generator.SceneGroundName,
            SceneTrafficName = generator.SceneTrafficName,
            SceneVegetationName = generator.SceneVegetationName,
            FaceImageFolderPath = generator.FaceImageFolderPath,
            SavedTextureSourceKind = generator.SavedTextureSourceKind,
            FaceTextureImagePath = generator.FaceTextureImagePath,
            FaceTextureBltPlayerNumber = generator.FaceTextureBltPlayerNumber,
            TextureSourceLayerIndex = generator.TextureSourceLayerIndex,
            LightingRigMode = generator.LightingRigMode,
            ScriptText = generator.ScriptText
        };
    }

    private Texture2D CaptureGeneratorPresetThumbnail(LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        return CaptureTextureSnapshot(LayerPreviewTexture(selectedLayerIndex, layer), 320, 180);
    }

    private void SaveGeneratorPreset(GeneratorState generator, LayerState layer)
    {
        if (generator == null || layer == null)
        {
            return;
        }

        var presetName = string.IsNullOrWhiteSpace(generator.PresetNameInput)
            ? "Preset " + (generatorPresets.Count + 1).ToString(CultureInfo.InvariantCulture)
            : generator.PresetNameInput.Trim();
        generator.PresetNameInput = presetName;
        var presetId = Guid.NewGuid().ToString("N");
        var thumbnailFileName = presetId + ".png";
        var thumbnail = CaptureGeneratorPresetThumbnail(layer);
        if (thumbnail != null)
        {
            SaveGeneratorPresetThumbnail(thumbnailFileName, thumbnail);
        }

        generatorPresets.Add(new GeneratorPresetRecord
        {
            Id = presetId,
            Name = presetName,
            ThumbnailFileName = thumbnailFileName,
            SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            State = CaptureGeneratorPresetState(generator),
            ThumbnailTexture = thumbnail
        });
        SaveGeneratorPresets();
    }

    private GameObject FindGeneratorModelPrefabByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName) || generatorModelPrefabs == null)
        {
            return null;
        }

        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.Ordinal))
            {
                return prefab;
            }
        }

        return null;
    }

    private void ApplyGeneratorSceneLayerPreset(GeneratorState generator, GeneratorSceneLayerKind kind, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            ClearGeneratorSceneLayer(generator, kind);
            return;
        }

        var prefab = FindGeneratorModelPrefabByName(prefabName);
        if (prefab == null)
        {
            ClearGeneratorSceneLayer(generator, kind);
            return;
        }

        LoadGeneratorSceneLayer(generator, prefab, kind);
    }

    private Texture FindAlbumArtTextureForPlayer(StateSnapshot snapshot, int playerNumber)
    {
        if (snapshot == null || snapshot.Players == null)
        {
            return null;
        }

        for (var i = 0; i < snapshot.Players.Count; i++)
        {
            var player = snapshot.Players[i];
            if (player != null && player.DeviceNumber == playerNumber && player.AlbumArtTexture != null)
            {
                return player.AlbumArtTexture;
            }
        }

        return null;
    }

    private void ApplyGeneratorPreset(GeneratorState generator, GeneratorPresetRecord preset, StateSnapshot snapshot)
    {
        if (generator == null || preset == null || preset.State == null)
        {
            return;
        }

        var state = preset.State;
        generator.PresetNameInput = preset.Name;
        generator.PresentationMode = state.PresentationMode;
        generator.CameraWorkMode = state.CameraWorkMode;
        generator.ObjectOrbitMode = state.ObjectOrbitMode;
        generator.ObjectSpinEnabled = state.ObjectSpinEnabled;
        generator.ObjectBpmPulseEnabled = state.ObjectBpmPulseEnabled;
        generator.ScreenVisible = state.ScreenVisible;
        generator.ParticlesEnabled = state.ParticlesEnabled;
        generator.SurroundScreensEnabled = state.SurroundScreensEnabled;
        generator.SurroundScreenSourceLayerIndex = state.SurroundScreenSourceLayerIndex;
        generator.TransparentBackground = state.TransparentBackground;
        generator.LightingRigMode = state.LightingRigMode;
        generator.ScriptText = string.IsNullOrEmpty(state.ScriptText) ? DefaultGeneratorScript() : state.ScriptText;
        generator.ScriptDirty = true;
        generator.FaceImageFolderPath = state.FaceImageFolderPath ?? "";
        generator.FaceImageFolderInput = generator.FaceImageFolderPath;
        generator.SavedTextureSourceKind = state.SavedTextureSourceKind;
        generator.FaceTextureImagePath = state.FaceTextureImagePath;
        generator.FaceTextureBltPlayerNumber = state.FaceTextureBltPlayerNumber;
        RefreshGeneratorFaceImageFolder(generator);
        SaveGeneratorFaceFolderPreference(generator.FaceImageFolderPath);

        SetGeneratorShape(generator, state.Shape);
        ApplyGeneratorCameraBackground(generator);
        SetGeneratorVisible(generator, generator.ScreenVisible);

        if (!string.IsNullOrEmpty(state.SkyboxName))
        {
            for (var i = 0; i < generatorSkyboxMaterials.Length; i++)
            {
                var material = generatorSkyboxMaterials[i];
                if (material != null && string.Equals(material.name, state.SkyboxName, StringComparison.Ordinal))
                {
                    SetGeneratorSkybox(generator, material);
                    break;
                }
            }
        }
        else
        {
            SetGeneratorSkybox(generator, null);
        }

        if (!string.IsNullOrEmpty(state.ModelName))
        {
            var prefab = FindGeneratorModelPrefabByName(state.ModelName);
            if (prefab != null)
            {
                LoadGeneratorModel(generator, prefab);
            }
            else
            {
                ClearGeneratorModel(generator);
            }
        }
        else
        {
            ClearGeneratorModel(generator);
        }

        generator.ModelPositionInput = string.IsNullOrEmpty(state.ModelPositionInput) ? "0,0,0" : state.ModelPositionInput;
        generator.ModelRotationInput = string.IsNullOrEmpty(state.ModelRotationInput) ? "0,0,0" : state.ModelRotationInput;
        generator.ModelScaleInput = string.IsNullOrEmpty(state.ModelScaleInput) ? "1,1,1" : state.ModelScaleInput;
        ApplyGeneratorModelTransform(generator);

        ApplyGeneratorSceneLayerPreset(generator, GeneratorSceneLayerKind.Buildings, state.SceneBuildingsName);
        ApplyGeneratorSceneLayerPreset(generator, GeneratorSceneLayerKind.Ground, state.SceneGroundName);
        ApplyGeneratorSceneLayerPreset(generator, GeneratorSceneLayerKind.Traffic, state.SceneTrafficName);
        ApplyGeneratorSceneLayerPreset(generator, GeneratorSceneLayerKind.Vegetation, state.SceneVegetationName);

        if (state.TextureSourceLayerIndex >= 0)
        {
            SetGeneratorLayerTextureSource(generator, state.TextureSourceLayerIndex);
        }
        else
        {
            ClearGeneratorLayerTextureSource(generator);
            if (state.SavedTextureSourceKind == GeneratorSavedTextureSourceKind.ImageFile && !string.IsNullOrEmpty(state.FaceTextureImagePath))
            {
                var imageTexture = LoadImageTexture(state.FaceTextureImagePath);
                if (imageTexture != null)
                {
                    SetGeneratorManualImageSource(generator, state.FaceTextureImagePath);
                    ApplyGeneratorTexture(generator, imageTexture);
                }
            }
            else if (state.SavedTextureSourceKind == GeneratorSavedTextureSourceKind.BltJacket && state.FaceTextureBltPlayerNumber > 0)
            {
                var artTexture = FindAlbumArtTextureForPlayer(snapshot, state.FaceTextureBltPlayerNumber);
                if (artTexture != null)
                {
                    SetGeneratorBltJacketSource(generator, state.FaceTextureBltPlayerNumber);
                    ApplyGeneratorTexture(generator, artTexture);
                }
            }
        }
    }

    private Texture GeneratorInputLayerTexture(int layerIndex)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length)
        {
            return null;
        }

        var sourceLayer = vjLayers[layerIndex];
        if (sourceLayer == null || !sourceLayer.Enabled || sourceLayer.SourceKind == LayerSourceKind.None)
        {
            return null;
        }

        return LayerPreviewTexture(layerIndex, sourceLayer);
    }

    private static void SetGeneratorGroundVisible(GeneratorState generator, bool visible)
    {
        if (generator == null || generator.GroundRenderer == null)
        {
            return;
        }

        generator.GroundRenderer.enabled = visible;
    }

    private static void SetGeneratorVisible(GeneratorState generator, bool visible)
    {
        if (generator == null)
        {
            return;
        }

        if (generator.Parts != null && generator.Parts.Count > 0)
        {
            for (var i = 0; i < generator.Parts.Count; i++)
            {
                if (generator.Parts[i] != null && generator.Parts[i].Renderer != null)
                {
                    generator.Parts[i].Renderer.enabled = visible;
                }
            }
        }
        if (generator.TunnelParts != null && generator.TunnelParts.Count > 0)
        {
            for (var i = 0; i < generator.TunnelParts.Count; i++)
            {
                if (generator.TunnelParts[i] != null && generator.TunnelParts[i].Renderer != null)
                {
                    generator.TunnelParts[i].Renderer.enabled = visible;
                }
            }
        }
        if (generator.ParticleParts != null && generator.ParticleParts.Count > 0)
        {
            for (var i = 0; i < generator.ParticleParts.Count; i++)
            {
                if (generator.ParticleParts[i] != null && generator.ParticleParts[i].Renderer != null)
                {
                    generator.ParticleParts[i].Renderer.enabled = visible && generator.ParticlesEnabled;
                }
            }
        }

        if (generator.Renderer != null)
        {
            generator.Renderer.enabled = visible;
        }
    }

    private static void SetGeneratorLightingVisible(GeneratorState generator, bool visible)
    {
        if (generator == null || generator.LightingLights == null)
        {
            return;
        }

        if (generator.LightingRoot != null)
        {
            generator.LightingRoot.SetActive(visible);
        }

        for (var i = 0; i < generator.LightingLights.Count; i++)
        {
            var light = generator.LightingLights[i];
            if (light == null)
            {
                continue;
            }

            if (light.HeadRenderer != null)
            {
                light.HeadRenderer.enabled = visible;
            }
            if (light.BeamRenderer != null)
            {
                light.BeamRenderer.enabled = visible;
            }
            if (light.SpotLight != null)
            {
                light.SpotLight.enabled = visible;
            }
        }
    }

    private static Material CreateGeneratorMaterial()
    {
        var resourceShader = Resources.Load<Shader>("BeatLinkGeneratorUnlitOpaque");
        if (resourceShader != null)
        {
            var resourceMaterial = new Material(resourceShader);
            ConfigureGeneratorMaterial(resourceMaterial);
            return resourceMaterial;
        }

        var shaderNames = new[]
        {
            "BeatLink/GeneratorUnlitOpaque",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Unlit/Texture",
            "Standard"
        };

        for (var i = 0; i < shaderNames.Length; i++)
        {
            var shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                var material = new Material(shader);
                ConfigureGeneratorMaterial(material);
                return material;
            }
        }

        Debug.LogWarning("3D Object generator material was not created because no built-in shader was found.");
        return null;
    }

    private static void ConfigureGeneratorMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = 3000;
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }
        if (material.HasProperty("_ZTest"))
        {
            material.SetFloat("_ZTest", 4f);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 2f);
        }
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void ApplyMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
        material.mainTexture = texture;
    }

    private void SetGeneratorShape(GeneratorState generator, GeneratorShape shape)
    {
        if (generator == null)
        {
            return;
        }
        generator.Shape = shape;
        Mesh mesh;
        switch (shape)
        {
            case GeneratorShape.Tetrahedron:
                mesh = BuildTetrahedronMesh();
                break;
            case GeneratorShape.Dodecahedron:
                mesh = BuildDodecahedronMesh();
                break;
            default:
                mesh = BuildCubeMesh();
                break;
        }

        if (generator.Parts != null && generator.Parts.Count > 0)
        {
            for (var i = 0; i < generator.Parts.Count; i++)
            {
                if (generator.Parts[i] != null && generator.Parts[i].Filter != null)
                {
                    generator.Parts[i].Filter.sharedMesh = mesh;
                }
            }
        }
        if (generator.TunnelParts != null && generator.TunnelParts.Count > 0)
        {
            for (var i = 0; i < generator.TunnelParts.Count; i++)
            {
                if (generator.TunnelParts[i] != null && generator.TunnelParts[i].Filter != null)
                {
                    generator.TunnelParts[i].Filter.sharedMesh = mesh;
                }
            }
        }

        if (generator.Filter != null)
        {
            generator.Filter.sharedMesh = mesh;
        }
    }

    private void UpdateGeneratorLayers()
    {
        if (vjLayers == null)
        {
            return;
        }

        var bpm = CurrentVisualBpm();
        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || layer.SourceKind != LayerSourceKind.Generator3D || layer.Generator == null)
            {
                continue;
            }

            var generator = layer.Generator;
            if (generator.TextureSourceLayerIndex >= 0 && generator.TextureSourceLayerIndex != i)
            {
                var sourceTexture = GeneratorInputLayerTexture(generator.TextureSourceLayerIndex);
                if (sourceTexture != null && sourceTexture != generator.CurrentTexture)
                {
                    ApplyGeneratorTexture(generator, sourceTexture);
                }
            }
            ApplyGeneratorMotion(generator, bpm);
            if (generator.Camera != null)
            {
                generator.Camera.Render();
            }
        }
    }

    private void UpdateVideoBpmSyncLayers()
    {
        if (vjLayers == null)
        {
            return;
        }

        var bpm = Mathf.Max(1f, CurrentVisualBpm());
        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || layer.SourceKind != LayerSourceKind.VideoFile)
            {
                continue;
            }

            if (layer.VideoMode != VideoPlaybackMode.Bpm)
            {
                if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
                {
                    SetReflectionValue(layer.KlakHapPlayer, "speed", 1f);
                }
                else if (layer.Player != null && Mathf.Abs(layer.Player.playbackSpeed - 1f) > 0.001f)
                {
                    layer.Player.playbackSpeed = 1f;
                }
                continue;
            }

            var baseBpm = Mathf.Clamp(layer.VideoBaseBpm, 1f, 400f);
            var targetSpeed = Mathf.Clamp(bpm / baseBpm, 0.05f, 16f);
            if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
            {
                SetReflectionValue(layer.KlakHapPlayer, "speed", targetSpeed);
                SetReflectionValue(layer.KlakHapPlayer, "loop", true);
                SetReflectionValue(layer.KlakHapPlayer, "enabled", true);
                if (layer.VideoResyncPending)
                {
                    SetReflectionValue(layer.KlakHapPlayer, "time", 0f);
                    layer.VideoResyncPending = false;
                }
                continue;
            }

            var player = layer.Player;
            if (player == null || !player.isPrepared || player.length <= 0.001)
            {
                continue;
            }

            if (Mathf.Abs(player.playbackSpeed - targetSpeed) > 0.001f)
            {
                player.playbackSpeed = targetSpeed;
            }

            if (!player.isPlaying)
            {
                player.Play();
            }

            if (layer.VideoResyncPending)
            {
                player.time = 0.0;
                player.Play();
                layer.VideoResyncPending = false;
            }
        }
    }

    private void ApplyGeneratorMotion(GeneratorState generator, float bpm)
    {
        if (generator == null)
        {
            return;
        }

        EnsureGeneratorLightingSceneSetup(generator);
        var beatFloat = GeneratorBeatFloat(bpm);
        var beatPhase = beatFloat - Mathf.Floor(beatFloat);
        var beatIndex = Mathf.FloorToInt(beatFloat);
        var beatPulse = 1f + Mathf.Exp(-beatPhase * 8f) * 0.18f;
        var twoBeatPhase = Mathf.Repeat(beatFloat * 0.5f, 1f);
        var fourBeatPhase = Mathf.Repeat(beatFloat * 0.25f, 1f);
        var objectPosition = EvaluateGeneratorObjectOrbitPosition(generator, beatFloat, bpm);
        var objectRotation = generator.ObjectSpinEnabled ? Time.realtimeSinceStartup * GeneratorCityObjectSpinSpeed : 0f;
        var objectScale = generator.ObjectBpmPulseEnabled ? beatPulse : 1f;

        if (generator.CameraWorkMode == GeneratorCameraWorkMode.ScriptTimeline)
        {
            SetGeneratorSceneLayersVisible(generator, false);
            SetGeneratorLightingVisible(generator, false);
            SetGeneratorSurroundScreensVisible(generator, false);
            SetGeneratorParticlesVisible(generator, false);
            var scriptedObjectPosition = ApplyGeneratorScriptTimeline(generator, beatFloat, objectRotation, objectScale);
            return;
        }

        if (generator.PresentationMode == GeneratorPresentationMode.Tunnel || generator.ObjectOrbitMode == GeneratorObjectOrbitMode.TunnelGrid)
        {
            SetGeneratorSceneLayersVisible(generator, false);
            SetGeneratorLightingVisible(generator, false);
            SetGeneratorSurroundScreensVisible(generator, false);
            SetGeneratorParticlesVisible(generator, false);
            ApplyGeneratorTunnelGridMotion(generator, beatFloat, bpm, objectRotation, objectScale);
            switch (generator.CameraWorkMode)
            {
                case GeneratorCameraWorkMode.TunnelForward:
                {
                    var swayX = Mathf.Sin(beatFloat * 0.23f) * 0.08f;
                    var swayY = Mathf.Cos(beatFloat * 0.19f) * 0.05f;
                    generator.Camera.fieldOfView = 58f;
                    var cameraPosition = new Vector3(swayX, GeneratorBaseObjectHeight + swayY, -2.0f);
                    var targetPosition = new Vector3(swayX * 0.35f, GeneratorBaseObjectHeight + swayY * 0.25f, 8.8f);
                    SetGeneratorCameraTarget(generator, cameraPosition, targetPosition);
                    break;
                }
                default:
                    if (generator.Camera != null)
                    {
                        generator.Camera.fieldOfView = 58f;
                    }
                    SetGeneratorCameraTarget(generator, new Vector3(0f, GeneratorBaseObjectHeight, -2.0f), new Vector3(0f, GeneratorBaseObjectHeight, 8.8f));
                    break;
            }
            return;
        }

        if (generator.PresentationMode == GeneratorPresentationMode.Lighting)
        {
            SetGeneratorSceneLayersVisible(generator, true);
            var lightingObjectPosition = BaseGeneratorLightingObjectPosition();
            ApplySingleObjectMotion(generator, objectRotation, objectScale * GeneratorSingleObjectSceneScaleFactor, lightingObjectPosition);
            SetGeneratorSurroundScreensVisible(generator, false);
            SetGeneratorParticlesVisible(generator, false);
            SetGeneratorLightingVisible(generator, true);
            ApplyGeneratorLightingRigMotion(generator, lightingObjectPosition, beatFloat, bpm);

            switch (generator.CameraWorkMode)
            {
                case GeneratorCameraWorkMode.EaseIn:
                {
                    var easeCycleIndex = Mathf.FloorToInt(beatFloat / 4f);
                    var t = Smooth01(fourBeatPhase);
                    var distance = Mathf.Lerp(6.8f, 3.2f, t);
                    var startDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2, generator.MotionSeed + 0.19f);
                    var endDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 1, generator.MotionSeed + 1.13f);
                    var direction = Vector3.Slerp(startDirection, endDirection, t).normalized;
                    SetGeneratorCameraTarget(generator, lightingObjectPosition + direction * distance, lightingObjectPosition);
                    break;
                }
                case GeneratorCameraWorkMode.EaseOut:
                {
                    var easeCycleIndex = Mathf.FloorToInt(beatFloat / 4f);
                    var t = Smooth01(fourBeatPhase);
                    var distance = Mathf.Lerp(3.2f, 6.8f, t);
                    var startDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 1, generator.MotionSeed + 2.07f);
                    var endDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 2, generator.MotionSeed + 2.91f);
                    var direction = Vector3.Slerp(startDirection, endDirection, t).normalized;
                    SetGeneratorCameraTarget(generator, lightingObjectPosition + direction * distance, lightingObjectPosition);
                    break;
                }
                case GeneratorCameraWorkMode.JumpOrbit:
                    SetGeneratorCameraTarget(generator, lightingObjectPosition + RandomOrbitCameraOffset(beatIndex, generator.MotionSeed), lightingObjectPosition);
                    break;
                case GeneratorCameraWorkMode.JumpHold:
                    ApplyGeneratorJumpHoldCameraWork(generator, lightingObjectPosition, beatFloat);
                    break;
                default:
                    SetGeneratorCameraTarget(generator, EvaluateContinuousObjectOrbitCameraPosition(generator, lightingObjectPosition, bpm, GeneratorLightingCameraOrbitRadius, GeneratorLightingCameraOrbitHeightScale), lightingObjectPosition);
                    break;
            }
            return;
        }

        SetGeneratorSceneLayersVisible(generator, false);
        SetGeneratorLightingVisible(generator, false);
        ApplySingleObjectMotion(generator, objectRotation, objectScale * GeneratorSingleObjectSceneScaleFactor, objectPosition);
        UpdateGeneratorSurroundScreens(generator, objectPosition);
        UpdateGeneratorParticles(generator, beatFloat);

        switch (generator.CameraWorkMode)
        {
            case GeneratorCameraWorkMode.EaseIn:
            {
                var easeCycleIndex = Mathf.FloorToInt(beatFloat / 4f);
                var t = Smooth01(fourBeatPhase);
                var distance = Mathf.Lerp(2.2f, 0.9f, t) * GeneratorSingleObjectSceneScaleFactor;
                var startDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2, generator.MotionSeed + 0.19f);
                var endDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 1, generator.MotionSeed + 1.13f);
                var direction = Vector3.Slerp(startDirection, endDirection, t).normalized;
                var cameraOffset = direction * distance;
                SetGeneratorCameraTarget(generator, objectPosition + cameraOffset, objectPosition);
                break;
            }
            case GeneratorCameraWorkMode.EaseOut:
            {
                var easeCycleIndex = Mathf.FloorToInt(beatFloat / 4f);
                var t = Smooth01(fourBeatPhase);
                var distance = Mathf.Lerp(0.9f, 2.2f, t) * GeneratorSingleObjectSceneScaleFactor;
                var startDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 1, generator.MotionSeed + 2.07f);
                var endDirection = EvaluateOrbitSphereDirection(easeCycleIndex * 2 + 2, generator.MotionSeed + 2.91f);
                var direction = Vector3.Slerp(startDirection, endDirection, t).normalized;
                var cameraOffset = direction * distance;
                SetGeneratorCameraTarget(generator, objectPosition + cameraOffset, objectPosition);
                break;
            }
            case GeneratorCameraWorkMode.JumpOrbit:
                SetGeneratorCameraTarget(generator, objectPosition + RandomOrbitCameraOffset(beatIndex, generator.MotionSeed), objectPosition);
                break;
            case GeneratorCameraWorkMode.JumpHold:
                ApplyGeneratorJumpHoldCameraWork(generator, objectPosition, beatFloat);
                break;
            case GeneratorCameraWorkMode.TunnelForward:
                SetGeneratorCameraTarget(generator, new Vector3(0f, GeneratorBaseObjectHeight, -0.12f), new Vector3(0f, GeneratorBaseObjectHeight, 4.8f));
                break;
            default:
                SetGeneratorCameraTarget(generator, EvaluateContinuousObjectOrbitCameraPosition(generator, objectPosition, bpm, 0.92f * GeneratorSingleObjectSceneScaleFactor, 0.16f * GeneratorSingleObjectSceneScaleFactor), objectPosition);
                break;
        }
    }

    private static float GeneratorBeatFloat(float bpm)
    {
        var effectiveBpm = bpm > 0.01f ? bpm : 120f;
        return Time.realtimeSinceStartup * effectiveBpm / 60f;
    }

    private static void ApplySingleObjectMotion(GeneratorState generator, float rotation, float scale)
    {
        ApplySingleObjectMotion(generator, rotation, scale, BaseGeneratorObjectPosition());
    }

    private static void ApplySingleObjectMotion(GeneratorState generator, float rotation, float scale, Vector3 position)
    {
        if (generator.TunnelParts != null)
        {
            for (var i = 0; i < generator.TunnelParts.Count; i++)
            {
                if (generator.TunnelParts[i] != null && generator.TunnelParts[i].Renderer != null)
                {
                    generator.TunnelParts[i].Renderer.enabled = false;
                }
            }
        }
        if (generator.Parts == null || generator.Parts.Count == 0)
        {
            if (generator.Pivot != null)
            {
                generator.Pivot.localPosition = position;
                generator.Pivot.localRotation = Quaternion.Euler(rotation * 0.6f, rotation, rotation * 0.35f);
                generator.Pivot.localScale = Vector3.one * (scale * GeneratorBaseObjectScale);
            }
            return;
        }

        for (var i = 0; i < generator.Parts.Count; i++)
        {
            var part = generator.Parts[i];
            if (part == null || part.Transform == null)
            {
                continue;
            }
            if (part.Renderer != null)
            {
                part.Renderer.enabled = i == 0;
            }
            SetGeneratorPartAlpha(part, i == 0 ? 1f : 0f);
            part.Transform.localPosition = position;
            part.Transform.localRotation = Quaternion.Euler(rotation * 0.6f, rotation, rotation * 0.35f);
            part.Transform.localScale = Vector3.one * (scale * GeneratorBaseObjectScale);
        }
    }

    private static void UpdateGeneratorParticles(GeneratorState generator, float beatFloat)
    {
        if (generator == null || generator.ParticleParts == null || generator.ParticleParts.Count == 0)
        {
            return;
        }

        for (var i = 0; i < generator.ParticleParts.Count; i++)
        {
            var part = generator.ParticleParts[i];
            if (part == null || part.Transform == null)
            {
                continue;
            }

            if (part.Renderer != null)
            {
                part.Renderer.enabled = generator.ParticlesEnabled;
            }
            if (!generator.ParticlesEnabled)
            {
                continue;
            }

            var phaseSeed = generator.MotionSeed + i * 1.137f;
            var fallPhase = Mathf.Repeat(beatFloat * GeneratorParticleFallRatePerBeat + Hash01(phaseSeed * 0.71f), 1f);
            var x = Mathf.Lerp(-GeneratorParticleAreaX, GeneratorParticleAreaX, Hash01(phaseSeed * 1.17f));
            var z = Mathf.Lerp(-GeneratorParticleAreaZ, GeneratorParticleAreaZ, Hash01(phaseSeed * 1.61f));
            var y = Mathf.Lerp(GeneratorParticleTopY, GeneratorParticleBottomY, fallPhase);
            var scale = Mathf.Lerp(GeneratorParticleScaleMin, GeneratorParticleScaleMax, Hash01(phaseSeed * 2.03f));
            var rotX = Hash01(phaseSeed * 2.47f) * 360f + beatFloat * 9f;
            var rotY = Hash01(phaseSeed * 2.93f) * 360f + beatFloat * 21f;
            var rotZ = Hash01(phaseSeed * 3.31f) * 360f + beatFloat * 13f;

            part.Transform.localPosition = new Vector3(x, y, z);
            part.Transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);
            part.Transform.localScale = Vector3.one * scale;
        }
    }

    private static void SetGeneratorParticlesVisible(GeneratorState generator, bool visible)
    {
        if (generator == null || generator.ParticleParts == null)
        {
            return;
        }

        for (var i = 0; i < generator.ParticleParts.Count; i++)
        {
            var part = generator.ParticleParts[i];
            if (part != null && part.Renderer != null)
            {
                part.Renderer.enabled = visible;
            }
        }
    }

    private static void SetGeneratorSurroundScreensVisible(GeneratorState generator, bool visible)
    {
        if (generator == null || generator.SurroundScreens == null)
        {
            return;
        }

        for (var i = 0; i < generator.SurroundScreens.Count; i++)
        {
            var screen = generator.SurroundScreens[i];
            if (screen != null && screen.Renderer != null)
            {
                screen.Renderer.enabled = visible;
            }
        }
    }

    private static void SetGeneratorSceneLayersVisible(GeneratorState generator, bool visible)
    {
        if (generator == null)
        {
            return;
        }

        var instances = new[]
        {
            generator.SceneBuildingsInstance,
            generator.SceneGroundInstance,
            generator.SceneTrafficInstance,
            generator.SceneVegetationInstance
        };

        for (var i = 0; i < instances.Length; i++)
        {
            if (instances[i] != null)
            {
                instances[i].SetActive(visible);
            }
        }
    }

    private void ApplyGeneratorLightingRigMotion(GeneratorState generator, Vector3 objectPosition, float beatFloat, float bpm)
    {
        if (generator == null || generator.LightingLights == null || generator.LightingLights.Count == 0)
        {
            return;
        }

        if (generator.LightingController != null)
        {
            if (generator.LightingController.Target != null)
            {
                generator.LightingController.Target.localPosition = objectPosition;
            }
            TryInvokeReflectionMethod(generator.LightingController.PerformanceComponent, "ChangeBpm", Mathf.Max(1f, bpm));
        }

        var pulse = 0.72f + Mathf.Exp(-(beatFloat - Mathf.Floor(beatFloat)) * 8f) * 0.45f;
        var effectiveBpm = Mathf.Max(1f, bpm);
        var baseHue = Mathf.Repeat(Time.realtimeSinceStartup * effectiveBpm / 480f, 1f);
        var beatPhase = beatFloat - Mathf.Floor(beatFloat);
        var beatIndex = Mathf.FloorToInt(beatFloat);
        var sweepT = 0.5f - 0.5f * Mathf.Cos(beatPhase * Mathf.PI * 2f);

        for (var i = 0; i < generator.LightingLights.Count; i++)
        {
            var movingLight = generator.LightingLights[i];
            if (movingLight == null || movingLight.Root == null)
            {
                continue;
            }

            var angle = (Mathf.PI * 2f * i) / Mathf.Max(1, generator.LightingLights.Count);
            var wobble = Mathf.Sin(beatFloat * 0.18f + i * 0.77f) * 0.18f;
            var radius = GeneratorLightingMovingLightRadius + Mathf.Sin(beatFloat * 0.11f + i * 0.43f) * 0.22f;
            var groundY = BaseGeneratorLightingGroundY();
            var position = new Vector3(
                objectPosition.x + Mathf.Cos(angle + wobble) * radius,
                groundY,
                objectPosition.z + Mathf.Sin(angle + wobble) * radius);
            movingLight.Root.localPosition = position;
            movingLight.Root.localRotation = Quaternion.identity;

            var color = Color.HSVToRGB(Mathf.Repeat(baseHue + i / (float)generator.LightingLights.Count, 1f), 0.72f, 1f);
            var radialDirection = (position - objectPosition);
            if (radialDirection.sqrMagnitude < 0.0001f)
            {
                radialDirection = Vector3.forward;
            }
            radialDirection.Normalize();
            var tangentDirection = Vector3.Cross(Vector3.up, radialDirection).normalized;
            Vector3 focusPosition;
            float intensity;

            switch (generator.LightingRigMode)
            {
                case GeneratorLightingRigMode.InOutSweep:
                {
                    var inwardTarget = objectPosition;
                    var outwardTarget = objectPosition + radialDirection * (radius * 1.95f);
                    focusPosition = Vector3.Lerp(inwardTarget, outwardTarget, sweepT);
                    intensity = GeneratorLightingMovingLightIntensity * (0.85f + pulse * 0.35f);
                    break;
                }
                case GeneratorLightingRigMode.TangentSweep:
                {
                    var tangentAmplitude = 4.4f;
                    var tangentOffset = Mathf.Lerp(-tangentAmplitude, tangentAmplitude, sweepT);
                    focusPosition = objectPosition + tangentDirection * tangentOffset + radialDirection * 0.35f;
                    intensity = GeneratorLightingMovingLightIntensity * (0.9f + pulse * 0.30f);
                    break;
                }
                default:
                {
                    var isOddGroup = (i & 1) == 0;
                    var oddActive = (beatIndex & 1) == 0;
                    var active = isOddGroup ? oddActive : !oddActive;
                    focusPosition = objectPosition;
                    intensity = active ? GeneratorLightingMovingLightIntensity * (0.8f + pulse * 0.55f) : 0f;
                    break;
                }
            }

            TryInvokeReflectionMethod(movingLight.MovingLightComponent, "SetColor", color);
            TryInvokeReflectionMethod(movingLight.MovingLightComponent, "SetIntensity", intensity);
            TryInvokeReflectionMethod(movingLight.MovingLightComponent, "LookAt", focusPosition);
            TryInvokeReflectionMethod(movingLight.MovingLightComponent, "Process");
            if (movingLight.SpotLight != null)
            {
                movingLight.SpotLight.color = color;
                movingLight.SpotLight.intensity = intensity * 6f;
                movingLight.SpotLight.range = GeneratorLightingSpotRange;
                movingLight.SpotLight.spotAngle = GeneratorLightingSpotAngle;
            }
        }
    }

    private static void ApplyGeneratorTunnelGridMotion(GeneratorState generator, float beatFloat, float bpm, float rotation, float scale)
    {
        if (generator == null)
        {
            return;
        }

        if (generator.Parts != null)
        {
            for (var i = 0; i < generator.Parts.Count; i++)
            {
                if (generator.Parts[i] != null && generator.Parts[i].Renderer != null)
                {
                    generator.Parts[i].Renderer.enabled = false;
                }
            }
        }

        if (generator.TunnelParts == null || generator.TunnelParts.Count == 0)
        {
            return;
        }

        EnsureGeneratorTunnelPermutationState(generator);
        if (generator.TunnelPermutationSignature != 1)
        {
            ResetGeneratorTunnelPermutationState(generator);
        }

        var operationIndex = Mathf.FloorToInt(beatFloat * 0.5f);
        var operationPhase = Smooth01(Mathf.Repeat(beatFloat * 0.5f, 1f));
        EnsureGeneratorTunnelStepPlan(generator, operationIndex);
        var currentStep = GeneratorTunnelShiftStepForBeat(generator, operationIndex);

        var index = 0;
        for (var z = 0; z < GeneratorTunnelDepthSlices; z++)
        {
            for (var y = 0; y < GeneratorTunnelRows; y++)
            {
                for (var x = 0; x < GeneratorTunnelColumns; x++)
                {
                    if (index >= generator.TunnelParts.Count)
                    {
                        return;
                    }

                    var part = generator.TunnelParts[index++];
                    if (part == null || part.Transform == null)
                    {
                        continue;
                    }

                    var shiftedCoords = EvaluateGeneratorTunnelShiftedCoordinates(generator, x, y, z, currentStep, operationPhase);
                    part.Transform.localPosition = EvaluateTunnelGridCellPosition(shiftedCoords, beatFloat);
                    part.Transform.localRotation = Quaternion.Euler(rotation * 0.6f, rotation, rotation * 0.35f);
                    part.Transform.localScale = Vector3.one * (scale * GeneratorTunnelObjectScale);
                    if (part.Renderer != null)
                    {
                        part.Renderer.enabled = generator.ScreenVisible;
                    }
                }
            }
        }

        for (; index < generator.TunnelParts.Count; index++)
        {
            var extra = generator.TunnelParts[index];
            if (extra != null && extra.Renderer != null)
            {
                extra.Renderer.enabled = false;
            }
        }
    }

    private static void ApplyBeatSplitMotion(GeneratorState generator, float beatFloat, int beatIndex, float beatPhase)
    {
        var splitForward = (beatIndex & 1) == 0;
        var split = splitForward ? Smooth01(beatPhase) : 1f - Smooth01(beatPhase);
        var offsets = new[]
        {
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(-1f, -1f, 0f),
            new Vector3(1f, -1f, 0f)
        };
        var baseRotation = beatFloat * 65f;
        var distance = split * 1.2f;
        var childScale = Mathf.Lerp(0.72f, 0.56f, split);
        var centerScale = Mathf.Lerp(1f, 0.72f, split);
        var centerAlpha = 1f - split;
        var splitAlpha = split;

        if (generator.Parts == null || generator.Parts.Count == 0)
        {
            ApplySingleObjectMotion(generator, baseRotation, 1f);
            return;
        }

        var basePosition = BaseGeneratorObjectPosition();

        for (var i = 0; i < generator.Parts.Count; i++)
        {
            var part = generator.Parts[i];
            if (part == null || part.Transform == null)
            {
                continue;
            }

            if (i == 0)
            {
                if (part.Renderer != null)
                {
                    part.Renderer.enabled = centerAlpha > 0.02f;
                }
                SetGeneratorPartAlpha(part, centerAlpha);
                part.Transform.localPosition = basePosition;
                part.Transform.localRotation = Quaternion.Euler(baseRotation * 0.45f, baseRotation, baseRotation * 0.22f);
                part.Transform.localScale = Vector3.one * (centerScale * GeneratorBaseObjectScale);
                continue;
            }

            if (part.Renderer != null)
            {
                part.Renderer.enabled = splitAlpha > 0.02f;
            }
            SetGeneratorPartAlpha(part, splitAlpha);
            var offset = offsets[(i - 1) % offsets.Length].normalized * (distance * GeneratorBaseObjectScale);
            part.Transform.localPosition = basePosition + offset;
            part.Transform.localRotation = Quaternion.Euler(baseRotation * 0.52f + i * 11f, baseRotation + i * 37f, baseRotation * 0.28f);
            part.Transform.localScale = Vector3.one * (childScale * GeneratorBaseObjectScale);
        }
    }

    private static void SetGeneratorPartAlpha(GeneratorPart part, float alpha)
    {
        if (part == null || part.Material == null)
        {
            return;
        }
        alpha = Mathf.Clamp01(alpha);
        if (part.Material.HasProperty("_Color"))
        {
            var color = part.Material.GetColor("_Color");
            color.a = alpha;
            part.Material.SetColor("_Color", color);
        }
    }

    private static void SetGeneratorCamera(GeneratorState generator, Vector3 localPosition)
    {
        SetGeneratorCameraTarget(generator, localPosition, Vector3.zero);
    }

    private static void SetGeneratorCameraTarget(GeneratorState generator, Vector3 localPosition, Vector3 targetPosition)
    {
        if (generator == null || generator.Camera == null)
        {
            return;
        }

        var cameraTransform = generator.Camera.transform;
        cameraTransform.localPosition = localPosition;
        var direction = targetPosition - localPosition;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }
        cameraTransform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Vector3 RandomOrbitCameraOffset(int beatIndex, float seed)
    {
        var r = Mathf.Lerp(0.62f, 1.3f, Hash01(beatIndex * 9.91f + seed * 0.71f)) * GeneratorSingleObjectSceneScaleFactor;
        return EvaluateOrbitSphereDirection(beatIndex, seed) * r;
    }

    private static Vector3 BaseGeneratorObjectPosition()
    {
        return new Vector3(0f, GeneratorBaseObjectHeight, 0f);
    }

    private static Vector3 BaseGeneratorLightingObjectPosition()
    {
        return new Vector3(0f, JapaneseOtakuCityBaseYOffset + GeneratorLightingObjectHeightOffset, 0f);
    }

    private static float BaseGeneratorLightingGroundY()
    {
        return JapaneseOtakuCityBaseYOffset + GeneratorLightingMovingLightGroundOffset;
    }

    private static void ApplyGeneratorJumpHoldCameraWork(GeneratorState generator, Vector3 objectPosition, float beatFloat)
    {
        var beatIndex = Mathf.FloorToInt(beatFloat);
        var beatPhase = Mathf.Repeat(beatFloat, 1f);
        var orbitIndex = beatIndex / 2;
        var inTransition = (beatIndex & 1) == 0;
        var previousOffset = EvaluateStaticCityOrbitOffset(generator.MotionSeed, orbitIndex - 1);
        var currentOffset = EvaluateStaticCityOrbitOffset(generator.MotionSeed, orbitIndex);
        Vector3 cameraPosition;
        if (inTransition)
        {
            cameraPosition = objectPosition + Vector3.Lerp(previousOffset, currentOffset, Smooth01(beatPhase));
        }
        else
        {
            cameraPosition = objectPosition + currentOffset;
        }

        SetGeneratorCameraTarget(generator, cameraPosition, objectPosition);
    }

    private static Vector3 EvaluateGeneratorObjectOrbitPosition(GeneratorState generator, float beatFloat, float bpm)
    {
        if (generator == null)
        {
            return BaseGeneratorObjectPosition();
        }

        switch (generator.ObjectOrbitMode)
        {
            case GeneratorObjectOrbitMode.Circular:
                return EvaluateCityCircularObjectPathPosition(beatFloat, bpm);
            case GeneratorObjectOrbitMode.Spherical:
                return EvaluateCitySphericalObjectPathPosition(bpm);
            default:
                return BaseGeneratorObjectPosition();
        }
    }

    private static Vector3 EvaluateCityCircularObjectPathPosition(float beatFloat, float bpm)
    {
        var objectAngle = OrbitAngleFromBpm(bpm, GeneratorCityObjectPathBeatsPerRevolution);
        var objectRadius = 4.4f;
        var objectHeight = GeneratorBaseObjectHeight + 0.45f * Mathf.Sin(beatFloat * 0.28f);
        return new Vector3(Mathf.Cos(objectAngle) * objectRadius, objectHeight, Mathf.Sin(objectAngle) * objectRadius * 0.72f);
    }

    private static Vector3 EvaluateCitySphericalObjectPathPosition(float bpm)
    {
        var phase = OrbitPhaseFromBpm(bpm, GeneratorCityObjectPathBeatsPerRevolution);
        var azimuth = phase * Mathf.PI * 2f;
        var elevation = Mathf.Sin(azimuth * 2f) * 0.62f;
        var horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - elevation * elevation));
        var direction = new Vector3(
            Mathf.Cos(azimuth) * horizontal,
            elevation,
            Mathf.Sin(azimuth) * horizontal).normalized;
        var orbitCenter = new Vector3(0f, GeneratorBaseObjectHeight + 2.9f, 0f);
        var radius = 3.0f;
        var verticalScale = 1.15f;
        return orbitCenter + new Vector3(direction.x * radius, direction.y * radius * verticalScale, direction.z * radius * 0.84f);
    }

    private static Vector3 EvaluateObjectOrbitCameraPosition(Vector3 objectPosition, float beatFloat, float bpm, float radius, float baseHeightOffset, float animatedHeightOffset)
    {
        var cameraAngle = OrbitAngleFromBpm(bpm, GeneratorCityCameraOrbitBeatsPerRevolution);
        var elevation = Mathf.Lerp(-0.32f, 0.95f, (Mathf.Sin(cameraAngle * 0.63f) + 1f) * 0.5f);
        var horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - elevation * elevation));
        var direction = new Vector3(
            Mathf.Sin(cameraAngle) * horizontal,
            elevation,
            -Mathf.Cos(cameraAngle) * horizontal).normalized;
        var offset = direction * radius;
        offset.y += baseHeightOffset + animatedHeightOffset * Mathf.Sin(beatFloat * 0.51f);
        return objectPosition + offset;
    }

    private static Vector3 EvaluateContinuousObjectOrbitCameraPosition(GeneratorState generator, Vector3 objectPosition, float bpm, float radius, float baseHeightOffset)
    {
        var effectiveBpm = Mathf.Max(1f, bpm);
        var secondsPerBeat = 60f / effectiveBpm;
        var seconds = Time.realtimeSinceStartup;
        var seed = generator != null ? generator.MotionSeed : 0f;
        var azimuth = seconds * (Mathf.PI * 2f / (secondsPerBeat * 16f)) + seed * 0.031f;
        var elevationAngle = Mathf.Sin(seconds * (Mathf.PI * 2f / (secondsPerBeat * 22f)) + seed * 0.117f) * 0.72f
                           + Mathf.Cos(seconds * (Mathf.PI * 2f / (secondsPerBeat * 37f)) + seed * 0.071f) * 0.18f;
        elevationAngle = Mathf.Clamp(elevationAngle, -0.85f, 1.02f);
        var horizontal = Mathf.Cos(elevationAngle);
        var radialScale = 0.78f + 0.32f * ((Mathf.Sin(seconds * (Mathf.PI * 2f / (secondsPerBeat * 12f)) + seed * 0.29f) + 1f) * 0.5f);
        var direction = new Vector3(
            Mathf.Sin(azimuth) * horizontal,
            Mathf.Sin(elevationAngle),
            -Mathf.Cos(azimuth) * horizontal).normalized;
        var offset = direction * (radius * radialScale);
        offset.y += baseHeightOffset;
        return objectPosition + offset;
    }

    private static void EnsureGeneratorTunnelPermutationState(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        if (generator.TunnelCommittedRowShiftX == null || generator.TunnelCommittedRowShiftX.Length != GeneratorTunnelRows ||
            generator.TunnelCommittedColumnShiftY == null || generator.TunnelCommittedColumnShiftY.Length != GeneratorTunnelColumns ||
            generator.TunnelCommittedSliceShiftZ == null || generator.TunnelCommittedSliceShiftZ.Length != GeneratorTunnelDepthSlices)
        {
            ResetGeneratorTunnelPermutationState(generator);
        }
    }

    private static void ResetGeneratorTunnelPermutationState(GeneratorState generator)
    {
        if (generator == null)
        {
            return;
        }

        generator.TunnelPermutationBeatIndex = int.MinValue;
        generator.TunnelPermutationSignature = 1;
        generator.TunnelPlannedCycleIndex = int.MinValue;
        generator.TunnelLastShiftStep = GeneratorTunnelShiftStep.None;
        generator.TunnelCommittedRowShiftX = new float[GeneratorTunnelRows];
        generator.TunnelCommittedColumnShiftY = new float[GeneratorTunnelColumns];
        generator.TunnelCommittedSliceShiftZ = new float[GeneratorTunnelDepthSlices];
    }

    private static GeneratorTunnelShiftStep GeneratorTunnelShiftStepForBeat(GeneratorState generator, int beatIndex)
    {
        if (generator == null)
        {
            return GeneratorTunnelShiftStep.None;
        }

        EnsureGeneratorTunnelStepPlan(generator, beatIndex);
        if (generator.TunnelPlannedSteps == null || generator.TunnelPlannedSteps.Length < 4)
        {
            return GeneratorTunnelShiftStep.None;
        }

        var normalized = Mathf.Abs(beatIndex % 4);
        return generator.TunnelPlannedSteps[normalized];
    }

    private static void EnsureGeneratorTunnelStepPlan(GeneratorState generator, int beatIndex)
    {
        if (generator == null)
        {
            return;
        }

        var cycleIndex = Mathf.FloorToInt(beatIndex / 4f);
        if (generator.TunnelPlannedSteps == null || generator.TunnelPlannedSteps.Length != 4)
        {
            generator.TunnelPlannedSteps = new GeneratorTunnelShiftStep[4];
            generator.TunnelPlannedCycleIndex = cycleIndex - 1;
            generator.TunnelLastShiftStep = GeneratorTunnelShiftStep.None;
        }

        if (generator.TunnelPlannedCycleIndex > cycleIndex)
        {
            generator.TunnelPlannedCycleIndex = cycleIndex - 1;
            generator.TunnelLastShiftStep = GeneratorTunnelShiftStep.None;
        }

        while (generator.TunnelPlannedCycleIndex < cycleIndex)
        {
            var nextCycle = generator.TunnelPlannedCycleIndex + 1;
            PopulateGeneratorTunnelStepCycle(generator, nextCycle);
            generator.TunnelPlannedCycleIndex = nextCycle;
            generator.TunnelLastShiftStep = generator.TunnelPlannedSteps[3];
        }
    }

    private static void PopulateGeneratorTunnelStepCycle(GeneratorState generator, int cycleIndex)
    {
        if (generator == null || generator.TunnelPlannedSteps == null || generator.TunnelPlannedSteps.Length < 4)
        {
            return;
        }

        var previous = generator.TunnelLastShiftStep;
        for (var i = 0; i < 4; i++)
        {
            var useOdd = (i & 1) == 0;
            var step = RandomTunnelShiftStep(generator.MotionSeed, cycleIndex, i, useOdd, previous);
            generator.TunnelPlannedSteps[i] = step;
            previous = step;
        }
    }

    private static GeneratorTunnelShiftStep RandomTunnelShiftStep(float seed, int cycleIndex, int stepIndex, bool useOdd, GeneratorTunnelShiftStep previousStep)
    {
        var candidates = useOdd
            ? new[] { GeneratorTunnelShiftStep.OddRowsRight, GeneratorTunnelShiftStep.OddColumnsUp }
            : new[] { GeneratorTunnelShiftStep.EvenRowsRight, GeneratorTunnelShiftStep.EvenColumnsUp };

        var previousAxis = TunnelShiftAxis(previousStep);
        var available = new List<GeneratorTunnelShiftStep>(3);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (TunnelShiftAxis(candidates[i]) != previousAxis)
            {
                available.Add(candidates[i]);
            }
        }
        if (available.Count == 0)
        {
            available.AddRange(candidates);
        }

        var h = Hash01(seed * 0.173f + cycleIndex * 13.71f + stepIndex * 5.19f);
        var pick = Mathf.Clamp(Mathf.FloorToInt(h * available.Count), 0, available.Count - 1);
        return available[pick];
    }

    private static int TunnelShiftAxis(GeneratorTunnelShiftStep step)
    {
        switch (step)
        {
            case GeneratorTunnelShiftStep.OddRowsRight:
            case GeneratorTunnelShiftStep.EvenRowsRight:
                return 0;
            case GeneratorTunnelShiftStep.OddColumnsUp:
            case GeneratorTunnelShiftStep.EvenColumnsUp:
                return 1;
            case GeneratorTunnelShiftStep.OddSlicesForward:
            case GeneratorTunnelShiftStep.EvenSlicesForward:
                return 2;
            default:
                return -1;
        }
    }

    private static Vector3 EvaluateGeneratorTunnelShiftedCoordinates(GeneratorState generator, int x, int y, int z, GeneratorTunnelShiftStep currentStep, float operationPhase)
    {
        var shiftedX = PositiveRepeat(x + (AffectsRowShift(currentStep, y) ? operationPhase : 0f), GeneratorTunnelColumns);
        var shiftedY = PositiveRepeat(y + (AffectsColumnShift(currentStep, x) ? operationPhase : 0f), GeneratorTunnelRows);
        var shiftedZ = PositiveRepeat(z + (AffectsSliceShift(currentStep, z) ? operationPhase : 0f), GeneratorTunnelDepthSlices);

        return new Vector3(shiftedX, shiftedY, shiftedZ);
    }

    private static bool AffectsRowShift(GeneratorTunnelShiftStep step, int row)
    {
        return (step == GeneratorTunnelShiftStep.OddRowsRight && (row & 1) == 0) ||
               (step == GeneratorTunnelShiftStep.EvenRowsRight && (row & 1) == 1);
    }

    private static bool AffectsColumnShift(GeneratorTunnelShiftStep step, int column)
    {
        return (step == GeneratorTunnelShiftStep.OddColumnsUp && (column & 1) == 0) ||
               (step == GeneratorTunnelShiftStep.EvenColumnsUp && (column & 1) == 1);
    }

    private static bool AffectsSliceShift(GeneratorTunnelShiftStep step, int slice)
    {
        return (step == GeneratorTunnelShiftStep.OddSlicesForward && (slice & 1) == 0) ||
               (step == GeneratorTunnelShiftStep.EvenSlicesForward && (slice & 1) == 1);
    }

    private static float PositiveRepeat(float value, int length)
    {
        if (length <= 0)
        {
            return value;
        }

        var result = value % length;
        if (result < 0f)
        {
            result += length;
        }
        return result;
    }

    private static Vector3 EvaluateTunnelGridCellPosition(Vector3Int cell, float beatFloat)
    {
        return EvaluateTunnelGridCellPosition(new Vector3(cell.x, cell.y, cell.z), beatFloat);
    }

    private static Vector3 EvaluateTunnelGridCellPosition(Vector3 cell, float beatFloat)
    {
        var slicePhase = Mathf.Repeat(beatFloat / GeneratorTunnelTravelBeatsPerSlice, 1f);
        var centerX = (GeneratorTunnelColumns - 1) * 0.5f;
        var centerY = (GeneratorTunnelRows - 1) * 0.5f;
        var localZ = (cell.z - slicePhase) * GeneratorTunnelSpacingZ;
        var offsetY = (cell.y - centerY) * GeneratorTunnelSpacingY;
        var offsetX = (cell.x - centerX) * GeneratorTunnelSpacingX;
        return new Vector3(offsetX, GeneratorBaseObjectHeight + offsetY, localZ + 1.4f);
    }

    private static float OrbitAngleFromBpm(float bpm, float beatsPerRevolution)
    {
        return OrbitPhaseFromBpm(bpm, beatsPerRevolution) * Mathf.PI * 2f;
    }

    private static float OrbitPhaseFromBpm(float bpm, float beatsPerRevolution)
    {
        var effectiveBpm = Mathf.Max(1f, bpm);
        var secondsPerRevolution = OrbitSecondsPerRevolution(effectiveBpm, beatsPerRevolution);
        return Mathf.Repeat(Time.realtimeSinceStartup / secondsPerRevolution, 1f);
    }

    private static float OrbitSecondsPerRevolution(float bpm, float beatsPerRevolution)
    {
        var effectiveBpm = Mathf.Max(1f, bpm);
        var secondsPerBeat = 60f / effectiveBpm;
        return secondsPerBeat * Mathf.Max(1f, beatsPerRevolution);
    }

    private static Vector3 EvaluateStaticCityOrbitPose(Vector3 objectPosition, float seed, int orbitIndex)
    {
        return objectPosition + EvaluateStaticCityOrbitOffset(seed, orbitIndex);
    }

    private static Vector3 EvaluateStaticCityOrbitOffset(float seed, int orbitIndex)
    {
        var stableIndex = Mathf.Max(0, orbitIndex);
        var radius = Mathf.Lerp(1.1f, 2.1f, Hash01(stableIndex * 7.1f + seed * 0.61f)) * GeneratorSingleObjectSceneScaleFactor;
        return EvaluateOrbitSphereDirection(stableIndex, seed * 1.37f) * radius;
    }

    private static Vector3 EvaluateOrbitSphereDirection(int index, float seed)
    {
        var stableIndex = Mathf.Max(0, index);
        var azimuth = Hash01(stableIndex * 11.7f + seed * 0.13f) * Mathf.PI * 2f;
        var elevationAngle = Mathf.Lerp(-0.48f, 0.92f, Hash01(stableIndex * 13.3f + seed * 0.83f));
        var horizontal = Mathf.Cos(elevationAngle);
        return new Vector3(
            Mathf.Sin(azimuth) * horizontal,
            Mathf.Sin(elevationAngle),
            -Mathf.Cos(azimuth) * horizontal).normalized;
    }

    private static float Hash01(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value) * 43758.5453f, 1f);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Vector3 ApplyGeneratorScriptTimeline(GeneratorState generator, float beatFloat, float additionalRotation, float scaleMultiplier)
    {
        EnsureGeneratorScriptParsed(generator);
        if (generator.ScriptKeys == null || generator.ScriptKeys.Count == 0)
        {
            ApplySingleObjectMotion(generator, Time.realtimeSinceStartup * 22f, 1f);
            SetGeneratorCameraTarget(generator, new Vector3(0f, GeneratorBaseCameraHeight, -GeneratorDefaultCameraDistance), BaseGeneratorObjectPosition());
            return BaseGeneratorObjectPosition();
        }

        var keys = generator.ScriptKeys;
        var duration = Mathf.Max(0.001f, keys[keys.Count - 1].Beat);
        var localBeat = Mathf.Repeat(beatFloat, duration);
        var a = keys[0];
        var b = keys[keys.Count - 1];
        for (var i = 0; i < keys.Count - 1; i++)
        {
            if (localBeat >= keys[i].Beat && localBeat <= keys[i + 1].Beat)
            {
                a = keys[i];
                b = keys[i + 1];
                break;
            }
        }

        var span = Mathf.Max(0.001f, b.Beat - a.Beat);
        var t = Ease01((localBeat - a.Beat) / span, b.Ease);
        var objPos = Vector3.Lerp(a.ObjPos, b.ObjPos, t);
        var objRot = Vector3.Lerp(a.ObjRot, b.ObjRot, t);
        objRot += new Vector3(additionalRotation * 0.6f, additionalRotation, additionalRotation * 0.35f);
        var objScale = Mathf.Lerp(a.ObjScale, b.ObjScale, t) * Mathf.Max(0.01f, scaleMultiplier);
        ApplyScriptObjectTransform(generator, objPos, objRot, objScale);

        var camPos = Vector3.Lerp(a.CamPos, b.CamPos, t);
        var camRot = Vector3.Lerp(a.CamRot, b.CamRot, t);
        SetGeneratorCameraExplicit(generator, camPos, camRot, Mathf.Lerp(a.CamFov, b.CamFov, t));
        ApplyGeneratorModelTransform(generator);
        return objPos + BaseGeneratorObjectPosition();
    }

    private static void ApplyScriptObjectTransform(GeneratorState generator, Vector3 position, Vector3 rotation, float scale)
    {
        if (generator.Parts == null || generator.Parts.Count == 0)
        {
            if (generator.Pivot != null)
            {
                generator.Pivot.localPosition = position + BaseGeneratorObjectPosition();
                generator.Pivot.localRotation = Quaternion.Euler(rotation);
                generator.Pivot.localScale = Vector3.one * Mathf.Max(0.01f, scale * GeneratorBaseObjectScale);
            }
            return;
        }

        for (var i = 0; i < generator.Parts.Count; i++)
        {
            var part = generator.Parts[i];
            if (part == null || part.Transform == null)
            {
                continue;
            }
            if (part.Renderer != null)
            {
                part.Renderer.enabled = i == 0;
            }
            SetGeneratorPartAlpha(part, i == 0 ? 1f : 0f);
            part.Transform.localPosition = position + BaseGeneratorObjectPosition();
            part.Transform.localRotation = Quaternion.Euler(rotation);
            part.Transform.localScale = Vector3.one * Mathf.Max(0.01f, scale * GeneratorBaseObjectScale);
        }
    }

    private static void SetGeneratorCameraExplicit(GeneratorState generator, Vector3 localPosition, Vector3 localEuler, float fov)
    {
        if (generator == null || generator.Camera == null)
        {
            return;
        }
        var cameraTransform = generator.Camera.transform;
        cameraTransform.localPosition = localPosition;
        cameraTransform.localRotation = Quaternion.Euler(localEuler);
        generator.Camera.fieldOfView = Mathf.Clamp(fov, 10f, 120f);
    }

    private static float Ease01(float value, string ease)
    {
        value = Mathf.Clamp01(value);
        if (string.Equals(ease, "in", StringComparison.OrdinalIgnoreCase))
        {
            return value * value;
        }
        if (string.Equals(ease, "out", StringComparison.OrdinalIgnoreCase))
        {
            return 1f - (1f - value) * (1f - value);
        }
        if (string.Equals(ease, "inout", StringComparison.OrdinalIgnoreCase) || string.Equals(ease, "smooth", StringComparison.OrdinalIgnoreCase))
        {
            return Smooth01(value);
        }
        if (string.Equals(ease, "expo", StringComparison.OrdinalIgnoreCase))
        {
            return value <= 0f ? 0f : Mathf.Pow(2f, 10f * (value - 1f));
        }
        return value;
    }

    private static void EnsureGeneratorScriptParsed(GeneratorState generator)
    {
        if (generator == null || generator.ScriptDirty == false)
        {
            return;
        }

        generator.ScriptDirty = false;
        generator.ScriptError = null;
        generator.ScriptKeys = ParseGeneratorScript(generator.ScriptText, out generator.ScriptError);
    }

    private static List<GeneratorKeyframe> ParseGeneratorScript(string text, out string error)
    {
        error = null;
        var keys = new List<GeneratorKeyframe>();
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex].Trim();
            if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var key = keys.Count == 0 ? GeneratorKeyframe.Default() : keys[keys.Count - 1].Clone();
            var tokens = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                var token = tokens[tokenIndex];
                var split = token.IndexOf('=');
                if (split <= 0)
                {
                    error = "Line " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture) + ": expected key=value.";
                    return keys;
                }

                var name = token.Substring(0, split);
                var value = token.Substring(split + 1);
                if (!ApplyGeneratorScriptToken(key, name, value))
                {
                    error = "Line " + (lineIndex + 1).ToString(CultureInfo.InvariantCulture) + ": invalid " + name + "=" + value;
                    return keys;
                }
            }
            keys.Add(key);
        }

        keys.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        if (keys.Count < 2)
        {
            error = "Need at least two keyframes.";
            return keys;
        }
        if (keys[0].Beat > 0.001f)
        {
            var first = keys[0].Clone();
            first.Beat = 0f;
            keys.Insert(0, first);
        }
        return keys;
    }

    private static bool ApplyGeneratorScriptToken(GeneratorKeyframe key, string name, string value)
    {
        if (string.Equals(name, "t", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "beat", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseFloat(value, out key.Beat);
        }
        if (string.Equals(name, "ease", StringComparison.OrdinalIgnoreCase))
        {
            key.Ease = value;
            return true;
        }
        if (string.Equals(name, "objPos", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVector3(value, out key.ObjPos);
        }
        if (string.Equals(name, "objRot", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVector3(value, out key.ObjRot);
        }
        if (string.Equals(name, "objScale", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseFloat(value, out key.ObjScale);
        }
        if (string.Equals(name, "camPos", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVector3(value, out key.CamPos);
        }
        if (string.Equals(name, "camRot", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVector3(value, out key.CamRot);
        }
        if (string.Equals(name, "camFov", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "camSize", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseFloat(value, out key.CamFov);
        }
        return false;
    }

    private static bool TryParseVector3(string value, out Vector3 result)
    {
        result = Vector3.zero;
        var parts = (value ?? "").Split(',');
        if (parts.Length != 3)
        {
            return false;
        }
        float x;
        float y;
        float z;
        if (!TryParseFloat(parts[0], out x) || !TryParseFloat(parts[1], out y) || !TryParseFloat(parts[2], out z))
        {
            return false;
        }
        result = new Vector3(x, y, z);
        return true;
    }

    private static string FormatVector3(Vector3 value)
    {
        return value.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
               value.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
               value.z.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string DefaultGeneratorScript()
    {
        return "# t is beats. ease: linear, in, out, inout, expo\n" +
               "# objPos/objRot/camPos/camRot = x,y,z  objScale = number  camFov = degrees\n" +
               "t=0 ease=inout objPos=0,0,0 objRot=0,0,0 objScale=1 camPos=0,0,-6 camRot=0,0,0 camFov=45\n" +
               "t=2 ease=inout objPos=0,0,0 objRot=0,180,20 objScale=1.35 camPos=0,0,-2.4 camRot=0,0,0 camFov=36\n" +
               "t=4 ease=out objPos=0.7,0.2,0 objRot=25,360,0 objScale=0.85 camPos=-1.2,0.5,-4 camRot=5,14,0 camFov=55\n" +
               "t=6 ease=inout objPos=0,0,0 objRot=0,540,0 objScale=1 camPos=0,0,-6 camRot=0,0,0 camFov=45";
    }

}
