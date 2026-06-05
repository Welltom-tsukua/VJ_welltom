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

public sealed class BeatLinkOverlayClient : MonoBehaviour
{
    private const int DeviceAnnouncementPort = 50000;
    private const int BeatAndPositionPort = 50001;
    private const int StatusPort = 50002;
    private const int DbServerQueryPort = 12523;
    private const byte DeviceAnnouncementType = 0x06;
    private const byte CdjStatusType = 0x0a;
    private const byte PrecisePositionType = 0x0b;
    private const int ActivitySamples = 96;
    private const int BltWaveWidth = 1280;
    private const int BltWaveHeight = 140;
    private const int BltWaveDetailScale = 8;
    private const int SourceLayerCount = 8;
    private const int MainEffectLayerCount = 4;
    private const int AllEffectLayerCount = 4;
    private const int VjLayerCount = SourceLayerCount + MainEffectLayerCount + AllEffectLayerCount;
    private const int SourceStartIndex = 0;
    private const int MainEffectStartIndex = SourceStartIndex + SourceLayerCount;
    private const int AllEffectStartIndex = MainEffectStartIndex + MainEffectLayerCount;
    private const int MetadataRequestingPlayer = 7;
    private const int MetadataQueryRetrySeconds = 8;
    private const int AnlzFileTagColorWaveformDetail = 0x35565750;
    private const int AnlzFileTypeExt = 0x00545845;
    private const int WaveformTextureMaxWidth = 16384;
    private const int YoutubeWaveformSampleRate = 8000;
    private const int YoutubeWaveformColumnsPerSecond = 150;
    private const int YoutubeWaveformHeight = 92;
    private const int YoutubeWaveformMaxWidth = 4096;
    private const string YoutubeWaveformAnalyzerVersion = "cdjmatch2";
    private const string YoutubeVideoCacheVersion = "hqcache1";
    private const int YoutubeRedirectServerPort = 17182;
    private const int ProgramOutputRenderLayer = 31;
    private const int GeneratorRenderLayerStart = 15;
    private const float GeneratorBaseObjectScale = 0.25f;
    private const float GeneratorBaseObjectHeight = 2.85f;
    private const float GeneratorBaseCameraHeight = 2.15f;
    private const float GeneratorDefaultCameraDistance = 1.0f;
    private const float GeneratorSingleObjectSceneScaleFactor = 1.35f;
    private const float GeneratorSurroundScreenRadius = 2.8f;
    private const float GeneratorSurroundScreenWidth = 0.62f;
    private const float GeneratorSurroundScreenHeight = 0.35f;
    private const int GeneratorSurroundScreenCount = 12;
    private const int GeneratorParticleCount = 30;
    private const float GeneratorParticleAreaX = 8.5f;
    private const float GeneratorParticleAreaZ = 11.0f;
    private const float GeneratorParticleTopY = 7.5f;
    private const float GeneratorParticleBottomY = -3.0f;
    private const float GeneratorParticleScaleMin = 0.09f;
    private const float GeneratorParticleScaleMax = 0.20f;
    private const float GeneratorParticleFallRatePerBeat = 0.14f;
    private const float JapaneseOtakuCityHalfExtent = 312.5f;
    private const float JapaneseOtakuCityBaseYOffset = -18.2f;
    private const float GeneratorCityCameraOrbitBeatsPerRevolution = 16f;
    private const float GeneratorCityObjectPathBeatsPerRevolution = 32f;
    private const float GeneratorCityObjectSpinSpeed = 2f;
    private const int GeneratorLightingMovingLightCount = 8;
    private const float GeneratorLightingMovingLightRadius = 8.2f;
    private const float GeneratorLightingMovingLightGroundOffset = 0.55f;
    private const float GeneratorLightingMovingLightBeamLength = 5.4f;
    private const float GeneratorLightingMovingLightIntensity = 1.45f;
    private const string PreferredLightingSceneKeyword = "JapaneseOtakuCity";
    private const float GeneratorLightingObjectHeightOffset = 8.2f;
    private const float GeneratorLightingSpotRange = 18f;
    private const float GeneratorLightingSpotAngle = 32f;
    private const float GeneratorLightingCameraOrbitRadius = 5.8f;
    private const float GeneratorLightingCameraOrbitHeightScale = 0.65f;
    private const int GeneratorTunnelColumns = 8;
    private const int GeneratorTunnelRows = 8;
    private const int GeneratorTunnelDepthSlices = 6;
    private const float GeneratorTunnelSpacingX = 2.80f;
    private const float GeneratorTunnelSpacingY = 2.10f;
    private const float GeneratorTunnelSpacingZ = 6.8f;
    private const float GeneratorTunnelTravelBeatsPerSlice = 4f;
    private const float GeneratorTunnelObjectScale = 0.42f;
    private const int StepSequencerMaxMediaLayers = 4;
    private const float BpmDisplayDeadband = 0.08f;
    private const int DefaultWaveformZoomIndex = 1;
    private const int CueListRefreshSeconds = 3;
    private const string DefaultBltParamsUrl = "http://127.0.0.1:17081/params.json";
    private const string OutputWidthPrefKey = "BeatLinkOverlay.OutputWidth";
    private const string OutputHeightPrefKey = "BeatLinkOverlay.OutputHeight";
    private const string GeneratorFaceFolderPrefKey = "BeatLinkOverlay.GeneratorFaceFolder";
    private const string BltBridgeModePrefKey = "BeatLinkOverlay.BltBridgeMode";
    private const string BltParamsUrlPrefKey = "BeatLinkOverlay.BltParamsUrl";
    private const string PreviewExternalWindowPrefKey = "BeatLinkOverlay.PreviewExternalWindow";
    private const string SettingsExternalWindowPrefKey = "BeatLinkOverlay.SettingsExternalWindow";
    private const string SecondDisplayUiPrefKey = "BeatLinkOverlay.SecondDisplayUi";
    private const float PreviewMonitorReferenceWidth = 1920f;
    private const float PreviewMonitorReferenceHeight = 1080f;

    [SerializeField] private string bltParamsUrl = DefaultBltParamsUrl;
    [SerializeField] private float bltPollIntervalSeconds = 0.35f;
    [SerializeField] private float bltWaveFrameIntervalSeconds = 1f / 30f;
    [SerializeField] private int bltRequestTimeoutSeconds = 2;

    private static readonly byte[] MagicHeader = Encoding.ASCII.GetBytes("Qspt1WmJOL");
    private static readonly byte[] DbServerQueryPacket = {
        0x00, 0x00, 0x00, 0x0f,
        0x52, 0x65, 0x6d, 0x6f, 0x74, 0x65, 0x44, 0x42,
        0x53, 0x65, 0x72, 0x76, 0x65, 0x72,
        0x00
    };
    private static readonly Color[] DirectWaveColors = {
        new Color(0f, 0.41f, 0.56f, 1f),
        new Color(0f, 0.53f, 0.69f, 1f),
        new Color(0f, 0.66f, 0.91f, 1f),
        new Color(0f, 0.72f, 0.85f, 1f),
        new Color(0.47f, 0.72f, 0.85f, 1f),
        new Color(0.53f, 0.75f, 0.91f, 1f),
        new Color(0.53f, 0.75f, 0.91f, 1f),
        new Color(0.78f, 0.88f, 0.91f, 1f)
    };
    private static readonly Color BeatMarkerColor = new Color(1f, 1f, 1f, 0.42f);
    private static readonly Color DownbeatMarkerColor = new Color(1f, 0.18f, 0.14f, 0.92f);
    private static readonly Color CueMarkerColor = new Color(1f, 0.28f, 0.22f, 0.95f);
    private static readonly Color HotCueMarkerColor = new Color(0.25f, 1f, 0.45f, 0.95f);
    private static readonly Color LoopCueMarkerColor = new Color(1f, 0.68f, 0.16f, 0.95f);
    private static readonly Color CurrentCueMarkerColor = new Color(1f, 1f, 1f, 0.98f);
    private static readonly float[] WaveformZoomLevels = { 0.5f, 1f, 2f, 4f, 8f };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif" };
    private static readonly string[] PreferredTextFontNames = {
        "Yu Gothic UI",
        "Yu Gothic",
        "Meiryo UI",
        "Meiryo",
        "BIZ UDPGothic",
        "MS UI Gothic",
        "MS Gothic",
        "Arial"
    };

    private readonly object stateLock = new object();
    private readonly object youtubeRedirectCacheLock = new object();
    private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
    private readonly Dictionary<string, YoutubeResolvedVideoInfo> youtubeRedirectCache = new Dictionary<string, YoutubeResolvedVideoInfo>(StringComparer.Ordinal);
    private readonly List<string> logLines = new List<string>();

    private UdpClient announcementClient;
    private UdpClient positionClient;
    private UdpClient statusClient;
    private Thread announcementThread;
    private Thread positionThread;
    private Thread statusThread;
    private Thread linkAnnouncementThread;
    private HttpListener youtubeRedirectServer;
    private Thread youtubeRedirectServerThread;
    private volatile bool youtubeRedirectServerRunning;
    private string youtubeRedirectServerBaseUrl;
    private volatile bool running;
    private string lastError;
    private DateTime startedUtc;
    private Vector2 scroll;
    private GUIStyle titleStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;
    private GUIStyle boxStyle;
    private GUIStyle markerStyle;
    private GUIStyle hotCueStyle;
    private Texture2D panelTexture;
    private Texture2D whiteTexture;
    private Texture2D triangleUpTexture;
    private Texture2D triangleDownTexture;
    private WebCamDevice[] captureDevices = new WebCamDevice[0];
    private WebCamTexture captureTexture;
    private string captureDeviceName;
    private string captureError;
    private int captureDeviceIndex = -1;
    private string bltError;
    private DateTime lastBltUpdateUtc;
    private DateTime lastBltBridgeStartAttemptUtc;
    private bool bltReceived;
    private bool useBltBridgeMode;
    private bool pendingBltBridgeMode;
    private string pendingBltParamsUrl;
    private int activeScreen;
    private int cdjDeckMode = 2;
    private int selectedLayerIndex;
    private int settingsMonitorSelectedLayerIndex = -1;
    private bool lowerPanelShowsEffects;
    private LayerState[] vjLayers;
    private BrowserVideoSlot[] browserPreviewSlots;
    private string mediaRootPath;
    private string mediaCurrentFolder;
    private string mediaRootInput;
    private string cachedMediaFolder;
    private readonly List<string> cachedFolders = new List<string>();
    private readonly List<string> cachedVideos = new List<string>();
    private readonly List<string> cachedImages = new List<string>();
    private readonly List<MediaPointerListRecord> mediaPointerLists = new List<MediaPointerListRecord>();
    private readonly HashSet<string> selectedBrowserMediaPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly object mediaScanLock = new object();
    private readonly Dictionary<string, string[]> mediaFolderChildrenCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> mediaFolderHasChildrenCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MediaFolderSnapshotRecord> mediaFolderSnapshotCache = new Dictionary<string, MediaFolderSnapshotRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VideoThumbnailCacheEntry> videoThumbnailCache = new Dictionary<string, VideoThumbnailCacheEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> videoThumbnailPreloadQueue = new Queue<string>();
    private readonly HashSet<string> queuedVideoThumbnailPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> visibleBrowserVideoPaths = new List<string>();
    private readonly HashSet<string> expandedMediaFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly object mediaLibraryCacheFileLock = new object();
    private readonly Dictionary<string, Texture2D> sourceImageTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> textFontOptions = new List<string>();
    private readonly Dictionary<string, Font> textFontCache = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
    private readonly List<GeneratorPresetRecord> generatorPresets = new List<GeneratorPresetRecord>();
    private Material[] generatorSkyboxMaterials = new Material[0];
    private GameObject[] generatorModelPrefabs = new GameObject[0];
    private Type lightBeamMovingLightType;
    private Type lightBeamPanType;
    private Type lightBeamTiltType;
    private Type lightBeamHeadType;
    private Type lightBeamNoribenBeamType;
    private Type lightBeamPerformanceType;
    private Type lightBeamLightGroupType;
    private bool generatorResourcesLoaded;
    private Vector2 mediaTreeScroll;
    private Vector2 mediaFileScroll;
    private Vector2 sourceScroll;
    private Vector2 effectScroll;
    private Vector2 layerSettingsScroll;
    private Vector2 settingsScroll;
    private int mediaPageIndex;
    private MediaBrowserMode mediaBrowserMode;
    private VideoBrowserFilter videoBrowserFilter;
    private int selectedMediaPointerListIndex;
    private int mediaSelectionAnchorIndex = -1;
    private bool browserContextMenuOpen;
    private Rect browserContextMenuRect;
    private readonly List<string> browserContextMenuTargets = new List<string>();
    private string mediaBrowserError;
    private string mediaScanFolder;
    private bool mediaScanInFlight;
    private MediaFolderScanResult pendingMediaFolderScan;
    private float nextThumbnailWorkTime;
    private bool rootThumbnailScanInFlight;
    private MediaLibraryScanResult pendingRootVideoScan;
    private bool mediaRootRescanRequested;
    private float nextMediaRootRescanTime;
    private FileSystemWatcher mediaRootWatcher;
    private bool mediaRootScanStarted;
    private string mediaLibraryCachePath;
    private string mediaThumbnailCacheRoot;
    private string generatorPresetConfigPath;
    private string generatorPresetThumbnailRoot;
    private bool browserPreviewSlotsDirty;
    private int mediaLibraryCacheSaveVersion;
    private bool rootPickerOpen;
    private string rootPickerPath;
    private string rootPickerError;
    private Vector2 rootPickerScroll;
    private bool youtubeSearchOpen;
    private bool youtubeLayerPickerOpen;
    private bool previewAuxWindowEnabled;
    private bool layerSettingsAuxWindowEnabled;
    private bool previewAuxWindowVisible;
    private bool layerSettingsAuxWindowVisible;
    private Rect previewAuxWindowRect = new Rect(72f, 72f, 1120f, 780f);
    private Rect layerSettingsAuxWindowRect = new Rect(132f, 96f, 1320f, 860f);
    private bool previewExternalWindowEnabled;
    private bool settingsExternalWindowEnabled;
    private bool secondDisplayUiEnabled;
    private string externalWindowDataRoot;
    private string externalPreviewJsonPath;
    private string externalSettingsJsonPath;
    private string externalPreviewImagePath;
    private string externalSettingsImagePath;
    private string externalPreviewCommandPath;
    private string externalSettingsCommandPath;
    private float nextExternalWindowSyncTime;
    private float nextMonitorDisplaySyncTime;
    private System.Diagnostics.Process previewExternalWindowProcess;
    private System.Diagnostics.Process settingsExternalWindowProcess;
    private int secondDisplayUiIndex = 1;
    private SecondDisplayUiMode secondDisplayUiMode;
    private Font monitorDisplayFont;
    private GameObject secondDisplayUiCameraRoot;
    private Camera secondDisplayUiCamera;
    private GameObject previewMonitorDisplayRoot;
    private Canvas previewMonitorDisplayCanvas;
    private Text previewMonitorTitleText;
    private Text previewMonitorStatusText;
    private Text previewMonitorListenerErrorText;
    private Text previewMonitorHintText;
    private UiButtonBundle previewMonitorDeck2Button;
    private UiButtonBundle previewMonitorDeck4Button;
    private AuxPreviewDeckUi[] previewMonitorDecks;
    private int previewMonitorLastDeckMode = -1;
    private bool previewMonitorMouseWasDown;
    private const double CdjMonitorStaleSeconds = 6.0;
    private GameObject settingsMonitorDisplayRoot;
    private Canvas settingsMonitorDisplayCanvas;
    private Text settingsMonitorHeaderText;
    private Text settingsMonitorSubtitleText;
    private Text settingsMonitorStatusText;
    private Text settingsMonitorPreviewTitleText;
    private RawImage settingsMonitorPreviewImage;
    private Image settingsMonitorPreviewPanel;
    private Image settingsMonitorDetailsPanel;
    private Text settingsMonitorPresetInfoText;
    private Text settingsMonitorAuxTitleText;
    private Text settingsMonitorAuxLabelA;
    private Text settingsMonitorAuxLabelB;
    private Text settingsMonitorAuxStatusText;
    private RawImage settingsMonitorAuxImageA;
    private RawImage settingsMonitorAuxImageB;
    private RectTransform settingsMonitorAuxImageARect;
    private RectTransform settingsMonitorAuxImageBRect;
    private UiButtonBundle[] settingsMonitorPresetButtons;
    private UiButtonBundle[] settingsMonitorFaceButtons;
    private Text settingsMonitorCommonTitleText;
    private Text settingsMonitorCommonText;
    private UiButtonBundle settingsMonitorEnabledButton;
    private UiButtonBundle settingsMonitorAudioButton;
    private UiButtonBundle settingsMonitorBlendAlphaButton;
    private UiButtonBundle settingsMonitorBlend50Button;
    private UiButtonBundle settingsMonitorBlend100Button;
    private UiButtonBundle settingsMonitorBlendMaskButton;
    private UiButtonBundle settingsMonitorColorNoneButton;
    private UiButtonBundle settingsMonitorColorInvertButton;
    private UiButtonBundle settingsMonitorColorEdgeButton;
    private UiButtonBundle settingsMonitorColorMonoButton;
    private Text settingsMonitorEffectTitleText;
    private Text settingsMonitorEffectText;
    private Text settingsMonitorMidiStatusText;
    private UiButtonBundle[] settingsMonitorEffectButtons;
    private Text settingsMonitorSourceTitleText;
    private Text settingsMonitorSourceText;
    private UiButtonBundle[] settingsMonitorSourceButtons;
    private InputField settingsMonitorInputFieldA;
    private InputField settingsMonitorInputFieldB;
    private UiButtonBundle settingsMonitorInputApplyButtonA;
    private UiButtonBundle settingsMonitorInputApplyButtonB;
    private EventSystem monitorDisplayEventSystem;
    private bool youtubeSearchInFlight;
    private int youtubeSearchDeck;
    private string youtubeSearchQuery = "";
    private string youtubeManualUrlInput = "";
    private string youtubeSearchStatus;
    private Vector2 youtubeSearchScroll;
    private Vector2 youtubeLayerPickerScroll;
    private readonly List<YoutubeSearchResult> youtubeSearchResults = new List<YoutubeSearchResult>();
    private YoutubeSearchResult pendingYoutubeResult;
    private bool youtubeResolverChecked;
    private string youtubeResolverPath;
    private bool youtubeJsRuntimeChecked;
    private string youtubeJsRuntimePath;
    private string youtubeJsRuntimeName;
    private bool ffmpegChecked;
    private string ffmpegPath;
    private bool klakHapChecked;
    private Type klakHapPlayerType;
    private Type klakHapPathModeType;
    private bool avproChecked;
    private Type avproMediaPlayerType;
    private Type avproMediaPathType;
    private string youtubeSelectionFile;
    private DateTime youtubeSelectionLastReadUtc;
    private readonly List<OutputDeviceOption> videoOutputDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> audioOutputDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> audioInputDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> outputResolutionOptions = new List<OutputDeviceOption>();
    private bool outputDevicesDirty = true;
    private bool videoOutputDropdownOpen;
    private bool audioOutputDropdownOpen;
    private bool audioInputDropdownOpen;
    private bool outputResolutionDropdownOpen;
    private int selectedVideoOutputIndex;
    private int selectedAudioOutputIndex;
    private int selectedAudioInputIndex;
    private int selectedOutputResolutionIndex;
    private string outputStatus;
    private int outputWidth = 1920;
    private int outputHeight = 1080;
    private bool audioInputDevicesDirty = true;
    private string captureAudioDeviceName;
    private string captureAudioStatus;
    private GameObject captureAudioRoot;
    private AudioSource captureAudioSource;
    private AudioClip captureAudioClip;
    private AudioListener captureAudioListener;
    private bool ownsCaptureAudioListener;
    private bool captureAudioMonitorEnabled = true;
    private float captureAudioMonitorVolume = 0.85f;
    private float captureAudioLevelRms;
    private float captureAudioLevelPeak;
    private int captureAudioMonitorLatencySamples = 4096;
    private string pendingCaptureAudioDeviceName;
    private float captureAudioStartAtRealtime;
    private bool captureAudioMonitorPrimed;
    private readonly float[] captureAudioAnalysisBuffer = new float[2048];
    private readonly List<MidiInputDeviceOption> midiInputDevices = new List<MidiInputDeviceOption>();
    private readonly List<MidiInputMessage> midiInputQueue = new List<MidiInputMessage>();
    private readonly List<MidiBinding> midiBindings = new List<MidiBinding>();
    private readonly Dictionary<string, float> midiBindingLastValues = new Dictionary<string, float>(StringComparer.Ordinal);
    private bool midiDevicesDirty = true;
    private bool midiInputDropdownOpen;
    private int selectedMidiInputIndex;
    private int activeMidiInputDeviceIndex = -1;
    private bool midiEditMode;
    private string midiStatus;
    private string midiLastMessageText;
    private MidiBindingAction midiLearningAction = MidiBindingAction.None;
    private int programSoloLayerIndex = -1;
    private bool bltMetadataPollStarted;
    private bool bltWavePollStarted;
    private int midiLearningIndex = -1;
    private int midiLearningSubIndex = -1;
    private string midiConfigPath;
    private IntPtr midiInputHandle = IntPtr.Zero;
    private MidiInProc midiInProc;
    private readonly object midiInputLock = new object();
    private GameObject programOutputRoot;
    private Camera programOutputCamera;
    private MeshRenderer[] programOutputRenderers;
    private Material layerEffectMaterial;
    private Material layerCompositeMaterial;
    private Material layerMaskCompositeMaterial;
    private RenderTexture mainSourceCompositeTexture;
    private RenderTexture mainEffectCompositeTexture;
    private RenderTexture overlayCompositeTexture;
    private RenderTexture finalCompositeTexture;
    private RenderTexture compositeScratchTexture;
    private int activeProgramDisplay = -1;
    private float nextProgramOutputValidationTime;
    private bool unityDisplaysActivated;
    private bool bpmDjLinkMode = true;
    private float manualBpm = 120f;
    private float manualBeatAnchorTime;
    private int manualBeatAnchorIndex;
    private float djLinkBeatAnchorTime;
    private float djLinkBeatAnchorBeatFloat;
    private float djLinkBeatClockBpm = 120f;
    private bool djLinkBeatClockInitialized;
    private readonly List<float> tapTimes = new List<float>();
    private IPAddress linkBroadcastAddress;
    private IPAddress linkLocalAddress;
    private byte[] linkHardwareAddress;

    private enum LayerSourceKind
    {
        None,
        VideoFile,
        Image,
        Capture,
        Generator3D,
        YouTube,
        Text,
        Effect
    }

    private enum MediaBrowserMode
    {
        Videos,
        Images
    }

    private enum VideoBrowserFilter
    {
        All,
        Mp4Only,
        MovOnly
    }

    private enum ExternalWindowKind
    {
        Preview,
        Settings
    }

    private enum MonitorDisplayKind
    {
        Preview,
        Settings
    }

    private enum SecondDisplayUiMode
    {
        None,
        Preview,
        Settings
    }

    private enum LayerSourceOrigin
    {
        None,
        SourcePanel,
        FileBrowser
    }

    private enum LayerSourceGroupMode
    {
        Main,
        Overlay
    }

    private enum VideoPlaybackMode
    {
        Bpm,
        Timeline
    }

    private enum LayerEffectKind
    {
        None,
        RgbEffect,
        QuadSplit,
        Blur,
        Glitch,
        Strobe,
        StepSequencer
    }

    private enum LayerRgbEffectMode
    {
        RedOnly,
        GreenOnly,
        BlueOnly,
        RgbInvert,
        EdgeExtract,
        Monochrome
    }

    private enum LayerColorMode
    {
        None,
        Invert,
        Edge,
        Monochrome
    }

    private enum LayerEffectMode
    {
        Horizontal,
        Vertical,
        Alternate,
        Zoom,
        Beat1,
        Beat2,
        Beat4,
        Beat8,
        Beat16,
        Manual
    }

    private enum GeneratorShape
    {
        Cube,
        Tetrahedron,
        Dodecahedron
    }

    private enum GeneratorCameraWorkMode
    {
        Orbit,
        EaseIn,
        EaseOut,
        JumpOrbit,
        JumpHold,
        TunnelForward,
        ScriptTimeline
    }

    private enum GeneratorObjectOrbitMode
    {
        None,
        Circular,
        Spherical,
        TunnelGrid
    }

    private enum GeneratorPresentationMode
    {
        AroundObject,
        Tunnel,
        Lighting
    }

    private enum GeneratorTunnelShiftStep
    {
        None,
        OddRowsRight,
        EvenRowsRight,
        OddColumnsUp,
        EvenColumnsUp,
        OddSlicesForward,
        EvenSlicesForward
    }

    private enum GeneratorLightingRigMode
    {
        AlternateBlink,
        InOutSweep,
        TangentSweep
    }

    private enum GeneratorSceneLayerKind
    {
        Buildings,
        Ground,
        Traffic,
        Vegetation
    }

    private enum DirectWaveformStyle
    {
        Blue,
        Rgb
    }

    private enum LayerBlendMode
    {
        Alpha,
        Add50,
        Add100,
        Mask
    }

    private enum LayerSlotGroup
    {
        Source,
        MainEffect,
        AllEffect
    }

    private enum MidiMessageKind
    {
        ControlChange,
        Note
    }

    private enum MidiBindingAction
    {
        None,
        LayerOpacity,
        LayerHue,
        LayerInvertAmount,
        LayerMonochromeAmount,
        LayerColorModeCycle,
        LayerColorSetMode,
        LayerToggle,
        LayerSelect,
        LayerBlendCycle,
        LayerProgramSolo,
        LayerProgramMute,
        LayerEffectToggle,
        LayerEffectModeCycle,
        LayerEffectSetMode,
        LayerEffectSetRgbMode,
        LayerEffectValue,
        ScreenToggle,
        OpenLayerSettings,
        OpenSettings,
        ReturnToVj,
        BpmTap,
        BpmDjLink,
        GeneratorPresetSelect
    }

    private enum GeneratorSavedTextureSourceKind
    {
        None = 0,
        ImageFile = 1,
        BltJacket = 2,
        LayerOutput = 3
    }

    private static LayerSlotGroup GetLayerSlotGroup(int index)
    {
        if (index >= SourceStartIndex && index < MainEffectStartIndex)
        {
            return LayerSlotGroup.Source;
        }
        if (index >= MainEffectStartIndex && index < AllEffectStartIndex)
        {
            return LayerSlotGroup.MainEffect;
        }
        return LayerSlotGroup.AllEffect;
    }

    private static bool IsSourceLayerSlot(int index)
    {
        return GetLayerSlotGroup(index) == LayerSlotGroup.Source;
    }

    private static bool IsMainEffectLayer(int index)
    {
        return GetLayerSlotGroup(index) == LayerSlotGroup.MainEffect;
    }

    private static bool IsAllEffectLayer(int index)
    {
        return GetLayerSlotGroup(index) == LayerSlotGroup.AllEffect;
    }

    private static bool IsEffectLayerSlot(int index)
    {
        return IsMainEffectLayer(index) || IsAllEffectLayer(index);
    }

    private static string LayerSlotLabel(int index)
    {
        if (IsSourceLayerSlot(index))
        {
            return "Layer " + (index - SourceStartIndex + 1).ToString(CultureInfo.InvariantCulture);
        }
        if (IsMainEffectLayer(index))
        {
            return "Main FX " + (index - MainEffectStartIndex + 1).ToString(CultureInfo.InvariantCulture);
        }
        return "All FX " + (index - AllEffectStartIndex + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsOverlaySourceLayer(LayerState layer)
    {
        return layer != null && layer.SourceGroupMode == LayerSourceGroupMode.Overlay;
    }

    private static bool IsMainMaterialSourceLayer(LayerState layer)
    {
        return layer != null && layer.SourceGroupMode != LayerSourceGroupMode.Overlay;
    }

    private static string SourceGroupModeLabel(LayerState layer)
    {
        return IsOverlaySourceLayer(layer) ? "OVR" : "MAIN";
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NativeMonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumDelegate callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfoEx info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MidiInCaps
    {
        public ushort ManufacturerId;
        public ushort ProductId;
        public uint DriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;
        public uint Support;
    }

    private delegate void MidiInProc(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2);

    [DllImport("winmm.dll")]
    private static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern uint midiInGetDevCaps(UIntPtr deviceId, out MidiInCaps caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint midiInOpen(out IntPtr handle, uint deviceId, MidiInProc callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern uint midiInStart(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInStop(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInReset(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInClose(IntPtr handle);

    private const uint MidiCallbackFunction = 0x00030000;
    private const uint MidiMessageData = 0x3C3;

    private const uint DeviceStateActive = 0x00000001;

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("bcde0395-e52f-467c-8e3d-c4579291692e")]
    private sealed class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private sealed class PolicyConfigClient
    {
    }

    [ComImport]
    [Guid("a95664d2-9614-4f35-a746-de8db63617e6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client);
        void UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("0bd7a1be-7a1a-44db-8397-cc5392387b5e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint deviceIndex, out IMMDevice device);
    }

    [ComImport]
    [Guid("d666063f-1587-4e43-81f1-b948e807363f")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        void OpenPropertyStore(uint access, out IPropertyStore properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out uint state);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr format);
        void GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int defaultFormat, IntPtr format);
        void ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName);
        void SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
        void GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int defaultPeriod, IntPtr defaultPeriodValue, IntPtr minimumPeriodValue);
        void SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr period);
        void GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);
        void SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);
        void GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ref PropertyKey key, IntPtr value);
        void SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ref PropertyKey key, ref PropVariant value);
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ERole role);
        void SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int visible);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;

        public string ToStringValue()
        {
            return vt == 31 && p != IntPtr.Zero ? Marshal.PtrToStringUni(p) : null;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant variant);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<BeatLinkOverlayClient>() != null)
        {
            return;
        }

        var go = new GameObject("Beat Link Direct Client");
        DontDestroyOnLoad(go);
        go.AddComponent<BeatLinkOverlayClient>();
    }

    private void Awake()
    {
        Application.runInBackground = true;
        startedUtc = DateTime.UtcNow;
        lowerPanelShowsEffects = true;
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            bltParamsUrl = DefaultBltParamsUrl;
        }
        useBltBridgeMode = string.Equals(Environment.GetEnvironmentVariable("BLT_BRIDGE_MODE"), "true", StringComparison.OrdinalIgnoreCase);
        LoadBltBridgePreference();
        pendingBltBridgeMode = useBltBridgeMode;
        pendingBltParamsUrl = bltParamsUrl;
        midiInProc = HandleMidiInMessage;
        midiConfigPath = Path.Combine(Application.persistentDataPath, "beatlink-midi-bindings.json");
        mediaLibraryCachePath = Path.Combine(Application.persistentDataPath, "beatlink-media-library-cache.json");
        mediaThumbnailCacheRoot = Path.Combine(Application.persistentDataPath, "video-thumbnails");
        generatorPresetConfigPath = Path.Combine(Application.persistentDataPath, "beatlink-generator-presets.json");
        generatorPresetThumbnailRoot = Path.Combine(Application.persistentDataPath, "generator-presets");
        externalWindowDataRoot = Path.Combine(Application.persistentDataPath, "external-windows");
        externalPreviewJsonPath = Path.Combine(externalWindowDataRoot, "preview.json");
        externalSettingsJsonPath = Path.Combine(externalWindowDataRoot, "settings.json");
        externalPreviewImagePath = Path.Combine(externalWindowDataRoot, "preview-selected.png");
        externalSettingsImagePath = Path.Combine(externalWindowDataRoot, "settings-selected.png");
        externalPreviewCommandPath = Path.Combine(externalWindowDataRoot, "preview.cmd");
        externalSettingsCommandPath = Path.Combine(externalWindowDataRoot, "settings.cmd");
        previewExternalWindowEnabled = false;
        settingsExternalWindowEnabled = false;
        if (PlayerPrefs.GetInt(PreviewExternalWindowPrefKey, 0) != 0 || PlayerPrefs.GetInt(SettingsExternalWindowPrefKey, 0) != 0)
        {
            PlayerPrefs.SetInt(PreviewExternalWindowPrefKey, 0);
            PlayerPrefs.SetInt(SettingsExternalWindowPrefKey, 0);
        }
        secondDisplayUiEnabled = PlayerPrefs.GetInt(SecondDisplayUiPrefKey, 0) != 0;
        secondDisplayUiMode = SecondDisplayUiMode.None;
        PlayerPrefs.Save();
        Directory.CreateDirectory(externalWindowDataRoot);
        InitializeOutputResolutionOptions();
        LoadOutputResolutionPreference();
        LoadMidiConfiguration();
        LoadMediaLibraryCache();
        EnsureDefaultMediaPointerList();
        selectedMediaPointerListIndex = Mathf.Clamp(selectedMediaPointerListIndex, 0, Mathf.Max(0, mediaPointerLists.Count - 1));
        LoadGeneratorPresets();
    }

    private void Start()
    {
        InitializeVideoLayers();
        InitializeProgramOutputScene();
        StartYoutubeRedirectServer();
        InitializeMediaBrowser();
        InitializeCaptureAudioMonitor();
        RefreshAudioInputDevices();
        AutoSelectCaptureAudioInput();
        RefreshMidiInputDevices();
        ApplyMidiInputSelection();
        EnsureBltPollingState();
    }

    private void Update()
    {
        ProcessMidiInputMessages();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (activeScreen != 2 && activeScreen != 3)
            {
                if (secondDisplayUiEnabled)
                {
                    secondDisplayUiMode = SecondDisplayUiMode.Preview;
                    EnsureMonitorDisplays();
                }
                else if (previewExternalWindowEnabled)
                {
                    EnsureExternalWindow(ExternalWindowKind.Preview);
                }
                else
                {
                    activeScreen = (activeScreen + 1) % 2;
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.E) && activeScreen == 0 && vjLayers != null && selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length)
        {
            if (secondDisplayUiEnabled)
            {
                settingsMonitorSelectedLayerIndex = selectedLayerIndex;
                secondDisplayUiMode = SecondDisplayUiMode.Settings;
                EnsureMonitorDisplays();
            }
            else if (settingsExternalWindowEnabled)
            {
                EnsureExternalWindow(ExternalWindowKind.Settings);
            }
            else
            {
                activeScreen = 2;
                layerSettingsScroll = Vector2.zero;
            }
        }
        if (Input.GetKeyDown(KeyCode.F) && activeScreen == 0)
        {
            lowerPanelShowsEffects = !lowerPanelShowsEffects;
        }
        if (Input.GetKeyDown(KeyCode.R) && activeScreen == 2)
        {
            activeScreen = 0;
        }
        if (Input.GetKeyDown(KeyCode.R) && secondDisplayUiEnabled && secondDisplayUiMode != SecondDisplayUiMode.None)
        {
            secondDisplayUiMode = SecondDisplayUiMode.None;
        }
        if ((Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Escape)) && activeScreen == 3)
        {
            activeScreen = 0;
        }
        if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.R)) && secondDisplayUiEnabled && secondDisplayUiMode != SecondDisplayUiMode.None)
        {
            secondDisplayUiMode = SecondDisplayUiMode.None;
        }
        HandleSecondDisplayPreviewInput();

        RefreshPlayerTextures();
        EnsureMediaBrowserBackgroundWork();
        UpdateBrowserPreviewSlotsIfNeeded();
        ApplyPendingMediaFolderScan();
        ApplyPendingRootVideoScan();
        UpdateMediaRootWatchRequests();
        EnsureBltPollingState();
        if (useBltBridgeMode)
        {
            MaybeAutoStartBltBridge(false);
        }
        UpdateCaptureAudioMonitoring();
        MaintainCriticalVideoPlayback();
        UpdateVideoBpmSyncLayers();
        UpdateGeneratorLayers();
        ValidateProgramOutputDisplayAvailability();
        UpdateProgramOutputLayers();
        PollExternalWindowCommands();
        UpdateExternalWindows();
        UpdateMonitorDisplays();
        CheckYoutubeSelectionFile();
        MaybeQueueCueListRefreshes();
        UpdateStaticBrowserThumbnails();
    }

    private void OnEnable()
    {
        if (useBltBridgeMode)
        {
            AddLog("BLT bridge mode active; direct DJ Link listeners disabled.");
        }
        else
        {
            StartListeners();
        }
        ApplyMidiInputSelection();
    }

    private void OnDisable()
    {
        StopListeners();
        StopYoutubeRedirectServer();
        StopCapturePreview();
        StopCaptureAudioInput();
        StopMidiInput();
        CloseExternalWindow(ref previewExternalWindowProcess);
        CloseExternalWindow(ref settingsExternalWindowProcess);
        DestroyMonitorDisplays();
    }

    private void OnDestroy()
    {
        StopListeners();
        StopYoutubeRedirectServer();
        StopCapturePreview();
        StopCaptureAudioInput();
        StopVideoLayers();
        DestroyProgramOutputScene();
        DestroySourceImageCache();
        StopMidiInput();
        CloseExternalWindow(ref previewExternalWindowProcess);
        CloseExternalWindow(ref settingsExternalWindowProcess);
        DestroyMonitorDisplays();
        if (layerEffectMaterial != null)
        {
            Destroy(layerEffectMaterial);
            layerEffectMaterial = null;
        }
        if (layerCompositeMaterial != null)
        {
            Destroy(layerCompositeMaterial);
            layerCompositeMaterial = null;
        }
        if (layerMaskCompositeMaterial != null)
        {
            Destroy(layerMaskCompositeMaterial);
            layerMaskCompositeMaterial = null;
        }
        DestroyCompositeRenderTextures();
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
        captureAudioSource.playOnAwake = false;
        captureAudioSource.loop = true;
        captureAudioSource.spatialBlend = 0f;
        captureAudioSource.volume = captureAudioMonitorVolume;
        captureAudioSource.mute = !captureAudioMonitorEnabled;
        captureAudioSource.ignoreListenerPause = true;
        captureAudioSource.bypassEffects = false;
        captureAudioSource.bypassListenerEffects = false;
        captureAudioSource.bypassReverbZones = true;

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

    private void UpdateCaptureAudioMonitoring()
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
            return;
        }

        var channels = Math.Max(1, captureAudioClip.channels);
        var sampleFrames = Math.Min(captureAudioAnalysisBuffer.Length / channels, 512);
        if (sampleFrames <= 0 || position <= 0)
        {
            captureAudioLevelRms = Mathf.Lerp(captureAudioLevelRms, 0f, 0.2f);
            captureAudioLevelPeak = Mathf.Max(0f, captureAudioLevelPeak - Time.unscaledDeltaTime * 0.8f);
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
    }

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
            TextSource = CreateTextSourceState(name + " Text", width, height, Mathf.Clamp(generatorLayer, 0, 31), renderTexture),
            Enabled = true,
            AudioOutputEnabled = true,
            Opacity = 1f,
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
            AudioOutputEnabled = false,
            Enabled = true,
            Opacity = 1f,
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
        DestroyTextSource(layer.TextSource);
        if (layer.OwnsStaticTexture && layer.StaticTexture != null)
        {
            Destroy(layer.StaticTexture);
            layer.StaticTexture = null;
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

    private void InitializeMediaBrowser()
    {
        if (!string.IsNullOrEmpty(mediaRootPath))
        {
            ConfigureMediaRootWatcher(mediaRootPath);
            RefreshMediaFolderCache();
            mediaRootScanStarted = false;
            return;
        }

        SetMediaRoot(ChooseDefaultMediaRoot());
    }

    private static string ChooseDefaultMediaRoot()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (!string.IsNullOrEmpty(videos) && Directory.Exists(videos))
        {
            return videos;
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(user) && Directory.Exists(user))
        {
            return user;
        }

        return Application.dataPath;
    }

    private void SetMediaRoot(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            mediaBrowserError = "Root folder not found.";
            return;
        }

        mediaRootPath = Path.GetFullPath(path);
        mediaCurrentFolder = mediaRootPath;
        mediaRootInput = mediaRootPath;
        cachedMediaFolder = null;
        mediaPageIndex = 0;
        mediaBrowserError = null;
        selectedBrowserMediaPaths.Clear();
        mediaSelectionAnchorIndex = -1;
        mediaFolderChildrenCache.Clear();
        mediaFolderHasChildrenCache.Clear();
        mediaFolderSnapshotCache.Clear();
        ResetVideoThumbnailCache();
        ConfigureMediaRootWatcher(mediaRootPath);
        EnsureDefaultMediaPointerList();
        selectedMediaPointerListIndex = 0;
        mediaRootScanStarted = false;
        StartRootVideoScan(mediaRootPath);
        SaveMediaLibraryCache();
    }

    private void EnsureDefaultMediaPointerList()
    {
        if (mediaPointerLists.Count > 0)
        {
            return;
        }

        mediaPointerLists.Add(new MediaPointerListRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "All Imported"
        });
    }

    private void RebuildImportedMediaCacheFromSnapshots()
    {
        cachedFolders.Clear();
        cachedVideos.Clear();
        cachedImages.Clear();

        var allVideos = new List<string>();
        var allImages = new List<string>();
        foreach (var pair in mediaFolderSnapshotCache)
        {
            var entry = pair.Value;
            if (entry == null)
            {
                continue;
            }

            if (entry.Videos != null)
            {
                allVideos.AddRange(entry.Videos);
            }
            if (entry.Images != null)
            {
                allImages.AddRange(entry.Images);
            }
        }

        allVideos.Sort(StringComparer.OrdinalIgnoreCase);
        allImages.Sort(StringComparer.OrdinalIgnoreCase);
        DeduplicateInPlace(allVideos);
        DeduplicateInPlace(allImages);
        cachedVideos.AddRange(allVideos);
        cachedImages.AddRange(allImages);
        browserPreviewSlotsDirty = true;
    }

    private static void DeduplicateInPlace(List<string> paths)
    {
        if (paths == null || paths.Count <= 1)
        {
            return;
        }

        var write = 1;
        for (var read = 1; read < paths.Count; read++)
        {
            if (!string.Equals(paths[read - 1], paths[read], StringComparison.OrdinalIgnoreCase))
            {
                paths[write++] = paths[read];
            }
        }

        if (write < paths.Count)
        {
            paths.RemoveRange(write, paths.Count - write);
        }
    }

    private List<string> DisplayedVideos()
    {
        var source = selectedMediaPointerListIndex <= 0 ? cachedVideos : ResolvePointerListMedia(selectedMediaPointerListIndex, true);
        var filtered = new List<string>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var path = source[i];
            if (MatchesVideoBrowserFilter(path))
            {
                filtered.Add(path);
            }
        }
        return filtered;
    }

    private List<string> DisplayedImages()
    {
        return selectedMediaPointerListIndex <= 0 ? new List<string>(cachedImages) : ResolvePointerListMedia(selectedMediaPointerListIndex, false);
    }

    private List<string> ResolvePointerListMedia(int listIndex, bool videos)
    {
        var resolved = new List<string>();
        if (listIndex < 0 || listIndex >= mediaPointerLists.Count)
        {
            return resolved;
        }

        var list = mediaPointerLists[listIndex];
        if (list == null || list.Paths == null)
        {
            return resolved;
        }

        for (var i = 0; i < list.Paths.Count; i++)
        {
            var path = list.Paths[i];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                continue;
            }

            if (videos ? IsVideoFile(path) : IsImageFile(path))
            {
                resolved.Add(path);
            }
        }

        resolved.Sort(StringComparer.OrdinalIgnoreCase);
        DeduplicateInPlace(resolved);
        return resolved;
    }

    private void CreateMediaPointerList()
    {
        EnsureDefaultMediaPointerList();
        var number = mediaPointerLists.Count;
        mediaPointerLists.Add(new MediaPointerListRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "List " + number.ToString(CultureInfo.InvariantCulture)
        });
        selectedMediaPointerListIndex = mediaPointerLists.Count - 1;
        SaveMediaLibraryCache();
    }

    private void DeleteSelectedMediaPointerList()
    {
        if (selectedMediaPointerListIndex <= 0 || selectedMediaPointerListIndex >= mediaPointerLists.Count)
        {
            return;
        }

        mediaPointerLists.RemoveAt(selectedMediaPointerListIndex);
        selectedMediaPointerListIndex = 0;
        SaveMediaLibraryCache();
    }

    private void AddMediaPathsToPointerList(int listIndex, List<string> paths)
    {
        if (listIndex <= 0 || listIndex >= mediaPointerLists.Count || paths == null || paths.Count == 0)
        {
            return;
        }

        var list = mediaPointerLists[listIndex];
        if (list.Paths == null)
        {
            list.Paths = new List<string>();
        }

        var existing = new HashSet<string>(list.Paths, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (existing.Add(path))
            {
                list.Paths.Add(path);
            }
        }

        list.Paths.Sort(StringComparer.OrdinalIgnoreCase);
        SaveMediaLibraryCache();
    }

    private void SetMediaCurrentFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        mediaCurrentFolder = Path.GetFullPath(path);
        cachedMediaFolder = null;
        mediaPageIndex = 0;
        ExpandMediaFolderPath(mediaCurrentFolder);
        RefreshMediaFolderCache();
        SaveMediaLibraryCache();
    }

    private void RefreshMediaFolderCache()
    {
        if (string.Equals(cachedMediaFolder, mediaCurrentFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MediaFolderSnapshotRecord cachedRecord;
        if (!string.IsNullOrEmpty(mediaCurrentFolder) && mediaFolderSnapshotCache.TryGetValue(mediaCurrentFolder, out cachedRecord) && cachedRecord != null)
        {
            cachedFolders.Clear();
            cachedVideos.Clear();
            cachedImages.Clear();
            cachedMediaFolder = mediaCurrentFolder;
            mediaBrowserError = null;
            if (cachedRecord.ChildFolders != null)
            {
                cachedFolders.AddRange(cachedRecord.ChildFolders);
            }
            if (cachedRecord.Videos != null)
            {
                cachedVideos.AddRange(cachedRecord.Videos);
            }
            if (cachedRecord.Images != null)
            {
                cachedImages.AddRange(cachedRecord.Images);
            }
            browserPreviewSlotsDirty = true;
            return;
        }

        StartMediaFolderScan(mediaCurrentFolder);
    }

    private static bool IsVideoFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".mov", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMp4File(string path)
    {
        return string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMovFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".mov", StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesVideoBrowserFilter(string path)
    {
        switch (videoBrowserFilter)
        {
            case VideoBrowserFilter.Mp4Only:
                return IsMp4File(path);
            case VideoBrowserFilter.MovOnly:
                return IsMovFile(path);
            default:
                return IsVideoFile(path);
        }
    }

    private List<string> FilteredVideos()
    {
        var filtered = new List<string>(cachedVideos.Count);
        for (var i = 0; i < cachedVideos.Count; i++)
        {
            var path = cachedVideos[i];
            if (MatchesVideoBrowserFilter(path))
            {
                filtered.Add(path);
            }
        }
        return filtered;
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        for (var i = 0; i < SupportedImageExtensions.Length; i++)
        {
            if (string.Equals(extension, SupportedImageExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateBrowserPreviewSlots()
    {
        if (browserPreviewSlots == null)
        {
            return;
        }

        for (var i = 0; i < browserPreviewSlots.Length; i++)
        {
            DestroyBrowserSlot(browserPreviewSlots[i]);
        }
        browserPreviewSlots = null;
    }

    private void UpdateBrowserPreviewSlotsIfNeeded()
    {
        if (!browserPreviewSlotsDirty || !IsVideoBrowserVisible())
        {
            return;
        }

        browserPreviewSlotsDirty = false;
        UpdateBrowserPreviewSlots();
    }

    private void StartMediaFolderScan(string folder)
    {
        lock (mediaScanLock)
        {
            if (string.IsNullOrEmpty(folder))
            {
                cachedMediaFolder = folder;
                mediaBrowserError = "Folder not found.";
                return;
            }

            if (mediaScanInFlight && string.Equals(mediaScanFolder, folder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            mediaScanFolder = folder;
            mediaScanInFlight = true;
            mediaBrowserError = "Scanning folder...";
            var scanFolder = folder;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = ScanMediaFolder(scanFolder);
                lock (mediaScanLock)
                {
                    pendingMediaFolderScan = result;
                    mediaScanInFlight = false;
                }
            });
        }
    }

    private static MediaFolderScanResult ScanMediaFolder(string folder)
    {
        var result = new MediaFolderScanResult
        {
            Folder = folder,
            Folders = new List<string>(),
            Videos = new List<string>(),
            Images = new List<string>()
        };

        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                result.Error = "Folder not found.";
                return result;
            }

            result.Folders.AddRange(Directory.GetDirectories(folder));
            result.Folders.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(folder))
            {
                if (IsVideoFile(file))
                {
                    result.Videos.Add(file);
                }
                else if (IsImageFile(file))
                {
                    result.Images.Add(file);
                }
            }

            result.Videos.Sort(StringComparer.OrdinalIgnoreCase);
            result.Images.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private void ApplyPendingMediaFolderScan()
    {
        MediaFolderScanResult result = null;
        lock (mediaScanLock)
        {
            if (pendingMediaFolderScan != null)
            {
                result = pendingMediaFolderScan;
                pendingMediaFolderScan = null;
            }
        }

        if (result == null || !string.Equals(result.Folder, mediaCurrentFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        cachedFolders.Clear();
        cachedVideos.Clear();
        cachedImages.Clear();
        cachedMediaFolder = result.Folder;
        mediaBrowserError = result.Error;
        if (result.Folders != null)
        {
            cachedFolders.AddRange(result.Folders);
        }
        if (result.Videos != null)
        {
            cachedVideos.AddRange(result.Videos);
        }
        if (result.Images != null)
        {
            cachedImages.AddRange(result.Images);
        }
        mediaFolderSnapshotCache[result.Folder] = new MediaFolderSnapshotRecord
        {
            Folder = result.Folder,
            ChildFolders = result.Folders == null ? new List<string>() : new List<string>(result.Folders),
            Videos = result.Videos == null ? new List<string>() : new List<string>(result.Videos),
            Images = result.Images == null ? new List<string>() : new List<string>(result.Images)
        };
        mediaFolderChildrenCache[result.Folder] = result.Folders == null ? Array.Empty<string>() : result.Folders.ToArray();
        mediaFolderHasChildrenCache[result.Folder] = result.Folders != null && result.Folders.Count > 0;
        browserPreviewSlotsDirty = true;
        SaveMediaLibraryCache();
    }

    private void StartRootVideoScan(string root)
    {
        lock (mediaScanLock)
        {
            if (string.IsNullOrEmpty(root) || rootThumbnailScanInFlight)
            {
                return;
            }

            rootThumbnailScanInFlight = true;
            var scanRoot = root;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = ScanRootVideos(scanRoot);
                lock (mediaScanLock)
                {
                    pendingRootVideoScan = result;
                    rootThumbnailScanInFlight = false;
                }
            });
        }
    }

    private static MediaLibraryScanResult ScanRootVideos(string root)
    {
        var result = new MediaLibraryScanResult
        {
            Root = root,
            Entries = new List<MediaFolderSnapshotRecord>()
        };

        try
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return result;
            }

            var pending = new Queue<string>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                var folder = pending.Dequeue();
                string[] childFolders;
                try
                {
                    childFolders = Directory.GetDirectories(folder);
                    Array.Sort(childFolders, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < childFolders.Length; i++)
                {
                    pending.Enqueue(childFolders[i]);
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(folder);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    continue;
                }

                var videos = new List<string>();
                var images = new List<string>();
                for (var i = 0; i < files.Length; i++)
                {
                    if (IsVideoFile(files[i]))
                    {
                        videos.Add(files[i]);
                    }
                    else if (IsImageFile(files[i]))
                    {
                        images.Add(files[i]);
                    }
                }

                result.Entries.Add(new MediaFolderSnapshotRecord
                {
                    Folder = folder,
                    ChildFolders = new List<string>(childFolders),
                    Videos = videos,
                    Images = images
                });
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private void ApplyPendingRootVideoScan()
    {
        MediaLibraryScanResult result = null;
        lock (mediaScanLock)
        {
            if (pendingRootVideoScan != null)
            {
                result = pendingRootVideoScan;
                pendingRootVideoScan = null;
            }
        }

        if (result == null || !string.Equals(result.Root, mediaRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mediaFolderSnapshotCache.Clear();
        mediaFolderChildrenCache.Clear();
        mediaFolderHasChildrenCache.Clear();
        if (result.Entries != null)
        {
            for (var i = 0; i < result.Entries.Count; i++)
            {
                var entry = result.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Folder))
                {
                    continue;
                }

                entry.Normalize();
                mediaFolderSnapshotCache[entry.Folder] = entry;
                var childFolders = entry.ChildFolders == null ? Array.Empty<string>() : entry.ChildFolders.ToArray();
                mediaFolderChildrenCache[entry.Folder] = childFolders;
                mediaFolderHasChildrenCache[entry.Folder] = childFolders.Length > 0;
            }
        }

        RebuildImportedMediaCacheFromSnapshots();
        SaveMediaLibraryCache();
    }

    private void LoadMediaLibraryCache()
    {
        if (string.IsNullOrEmpty(mediaLibraryCachePath) || !File.Exists(mediaLibraryCachePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(mediaLibraryCachePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var cacheFile = JsonUtility.FromJson<MediaLibraryCacheFile>(json);
            if (cacheFile == null || string.IsNullOrEmpty(cacheFile.RootPath))
            {
                return;
            }

            var root = Path.GetFullPath(cacheFile.RootPath);
            if (!Directory.Exists(root))
            {
                return;
            }

            mediaFolderSnapshotCache.Clear();
            mediaFolderChildrenCache.Clear();
            mediaFolderHasChildrenCache.Clear();

            if (cacheFile.Entries != null)
            {
                for (var i = 0; i < cacheFile.Entries.Count; i++)
                {
                    var entry = cacheFile.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Folder))
                    {
                        continue;
                    }

                    entry.Normalize();
                    mediaFolderSnapshotCache[entry.Folder] = entry;
                    var childFolders = entry.ChildFolders == null ? Array.Empty<string>() : entry.ChildFolders.ToArray();
                    mediaFolderChildrenCache[entry.Folder] = childFolders;
                    mediaFolderHasChildrenCache[entry.Folder] = childFolders.Length > 0;
                }
            }

            mediaRootPath = root;
            mediaRootInput = root;
            mediaCurrentFolder = root;
            if (!string.IsNullOrEmpty(cacheFile.CurrentFolder))
            {
                var current = Path.GetFullPath(cacheFile.CurrentFolder);
                if (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    mediaCurrentFolder = current;
                }
            }

            cachedMediaFolder = null;
            mediaPageIndex = 0;
            if (cacheFile.PointerLists != null && cacheFile.PointerLists.Count > 0)
            {
                mediaPointerLists.Clear();
                for (var i = 0; i < cacheFile.PointerLists.Count; i++)
                {
                    var list = cacheFile.PointerLists[i];
                    if (list != null)
                    {
                        list.Normalize();
                        mediaPointerLists.Add(list);
                    }
                }
            }
            EnsureDefaultMediaPointerList();
            selectedMediaPointerListIndex = Mathf.Clamp(cacheFile.SelectedPointerListIndex, 0, Mathf.Max(0, mediaPointerLists.Count - 1));
            RebuildImportedMediaCacheFromSnapshots();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Media library cache load failed: " + ex.Message);
        }
    }

    private void SaveMediaLibraryCache()
    {
        if (string.IsNullOrEmpty(mediaLibraryCachePath) || string.IsNullOrEmpty(mediaRootPath))
        {
            return;
        }

        var cacheFile = new MediaLibraryCacheFile
        {
            RootPath = mediaRootPath,
            CurrentFolder = mediaCurrentFolder,
            SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Entries = new List<MediaFolderSnapshotRecord>(mediaFolderSnapshotCache.Count),
            PointerLists = new List<MediaPointerListRecord>(mediaPointerLists.Count),
            SelectedPointerListIndex = selectedMediaPointerListIndex
        };

        foreach (var pair in mediaFolderSnapshotCache)
        {
            if (pair.Value == null)
            {
                continue;
            }
            cacheFile.Entries.Add(pair.Value.Clone());
        }
        for (var i = 0; i < mediaPointerLists.Count; i++)
        {
            if (mediaPointerLists[i] != null)
            {
                cacheFile.PointerLists.Add(mediaPointerLists[i].Clone());
            }
        }
        cacheFile.SortEntries();

        var version = Interlocked.Increment(ref mediaLibraryCacheSaveVersion);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var json = JsonUtility.ToJson(cacheFile, true);
                var directory = Path.GetDirectoryName(mediaLibraryCachePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (mediaLibraryCacheFileLock)
                {
                    if (version != mediaLibraryCacheSaveVersion)
                    {
                        return;
                    }

                    var tempPath = mediaLibraryCachePath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    if (File.Exists(mediaLibraryCachePath))
                    {
                        File.Copy(tempPath, mediaLibraryCachePath, true);
                        File.Delete(tempPath);
                    }
                    else
                    {
                        File.Move(tempPath, mediaLibraryCachePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Media library cache save failed: " + ex.Message);
            }
        });
    }

    private void LoadGeneratorPresets()
    {
        generatorPresets.Clear();
        if (string.IsNullOrEmpty(generatorPresetConfigPath) || !File.Exists(generatorPresetConfigPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(generatorPresetConfigPath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var library = JsonUtility.FromJson<GeneratorPresetLibraryFile>(json);
            if (library == null || library.Presets == null)
            {
                return;
            }

            for (var i = 0; i < library.Presets.Count; i++)
            {
                var preset = library.Presets[i];
                if (preset == null || string.IsNullOrEmpty(preset.Id))
                {
                    continue;
                }

                preset.ThumbnailTexture = LoadGeneratorPresetThumbnailTexture(preset.ThumbnailFileName);
                if (preset.State == null)
                {
                    preset.State = new GeneratorPresetState();
                }
                generatorPresets.Add(preset);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset load failed: " + ex.Message);
        }
    }

    private void SaveGeneratorPresets()
    {
        if (string.IsNullOrEmpty(generatorPresetConfigPath))
        {
            return;
        }

        try
        {
            var library = new GeneratorPresetLibraryFile
            {
                Presets = new List<GeneratorPresetRecord>(generatorPresets.Count)
            };

            for (var i = 0; i < generatorPresets.Count; i++)
            {
                var preset = generatorPresets[i];
                if (preset == null || string.IsNullOrEmpty(preset.Id))
                {
                    continue;
                }

                library.Presets.Add(new GeneratorPresetRecord
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    ThumbnailFileName = preset.ThumbnailFileName,
                    SavedAtUtc = preset.SavedAtUtc,
                    State = preset.State
                });
            }

            var directory = Path.GetDirectoryName(generatorPresetConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(generatorPresetConfigPath, JsonUtility.ToJson(library, true), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset save failed: " + ex.Message);
        }
    }

    private string GeneratorPresetThumbnailPath(string thumbnailFileName)
    {
        if (string.IsNullOrEmpty(generatorPresetThumbnailRoot) || string.IsNullOrEmpty(thumbnailFileName))
        {
            return null;
        }

        return Path.Combine(generatorPresetThumbnailRoot, thumbnailFileName);
    }

    private Texture2D LoadGeneratorPresetThumbnailTexture(string thumbnailFileName)
    {
        try
        {
            var path = GeneratorPresetThumbnailPath(thumbnailFileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch
        {
            return null;
        }
    }

    private void SaveGeneratorPresetThumbnail(string thumbnailFileName, Texture2D texture)
    {
        if (string.IsNullOrEmpty(generatorPresetThumbnailRoot) || string.IsNullOrEmpty(thumbnailFileName) || texture == null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(generatorPresetThumbnailRoot);
            var path = GeneratorPresetThumbnailPath(thumbnailFileName);
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset thumbnail save failed: " + ex.Message);
        }
    }

    private void UpdateMediaRootWatchRequests()
    {
        if (!IsMediaBrowserPanelVisible())
        {
            return;
        }

        if (!mediaRootRescanRequested || Time.unscaledTime < nextMediaRootRescanTime)
        {
            return;
        }

        mediaRootRescanRequested = false;
        nextMediaRootRescanTime = Time.unscaledTime + 2f;
        StartRootVideoScan(mediaRootPath);
        mediaRootScanStarted = true;
        cachedMediaFolder = null;
        RefreshMediaFolderCache();
    }

    private void EnsureMediaBrowserBackgroundWork()
    {
        if (!IsMediaBrowserPanelVisible())
        {
            return;
        }

        if (!mediaRootScanStarted && !string.IsNullOrEmpty(mediaRootPath))
        {
            StartRootVideoScan(mediaRootPath);
            mediaRootScanStarted = true;
        }
    }

    private bool IsMediaBrowserPanelVisible()
    {
        return activeScreen == 0 && !lowerPanelShowsEffects;
    }

    private void ConfigureMediaRootWatcher(string root)
    {
        if (mediaRootWatcher != null)
        {
            try
            {
                mediaRootWatcher.EnableRaisingEvents = false;
                mediaRootWatcher.Created -= HandleMediaRootFilesystemChanged;
                mediaRootWatcher.Deleted -= HandleMediaRootFilesystemChanged;
                mediaRootWatcher.Renamed -= HandleMediaRootFilesystemRenamed;
                mediaRootWatcher.Dispose();
            }
            catch
            {
            }
            mediaRootWatcher = null;
        }

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return;
        }

        try
        {
            mediaRootWatcher = new FileSystemWatcher(root);
            mediaRootWatcher.IncludeSubdirectories = true;
            mediaRootWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
            mediaRootWatcher.Created += HandleMediaRootFilesystemChanged;
            mediaRootWatcher.Deleted += HandleMediaRootFilesystemChanged;
            mediaRootWatcher.Renamed += HandleMediaRootFilesystemRenamed;
            mediaRootWatcher.EnableRaisingEvents = true;
        }
        catch
        {
            mediaRootWatcher = null;
        }
    }

    private void HandleMediaRootFilesystemChanged(object sender, FileSystemEventArgs args)
    {
        mediaRootRescanRequested = true;
    }

    private void HandleMediaRootFilesystemRenamed(object sender, RenamedEventArgs args)
    {
        mediaRootRescanRequested = true;
    }

    private void EnsureBrowserPreviewSlots(int count)
    {
        count = Mathf.Max(0, count);
        if (browserPreviewSlots != null && browserPreviewSlots.Length == count)
        {
            return;
        }

        var previous = browserPreviewSlots;
        browserPreviewSlots = new BrowserVideoSlot[count];
        for (var i = 0; i < count; i++)
        {
            browserPreviewSlots[i] = new BrowserVideoSlot();
        }

        if (previous == null)
        {
            return;
        }

        var copyCount = Math.Min(previous.Length, browserPreviewSlots.Length);
        for (var i = 0; i < copyCount; i++)
        {
            var oldSlot = previous[i];
            var newSlot = browserPreviewSlots[i];
            if (oldSlot == null || newSlot == null)
            {
                continue;
            }

            newSlot.Path = oldSlot.Path;
            newSlot.Texture = oldSlot.Texture;
            newSlot.ThumbnailReady = oldSlot.ThumbnailReady;
            newSlot.ThumbnailError = oldSlot.ThumbnailError;
            oldSlot.Texture = null;
        }

        for (var i = 0; i < previous.Length; i++)
        {
            DestroyBrowserSlot(previous[i]);
        }
    }

    private void AssignBrowserSlot(BrowserVideoSlot slot, string path)
    {
        if (slot == null)
        {
            return;
        }
        if (string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        slot.Path = path;
        slot.ThumbnailReady = false;
        slot.ThumbnailError = null;
        if (slot.Texture != null)
        {
            Destroy(slot.Texture);
            slot.Texture = null;
        }

        VideoThumbnailCacheEntry cached;
        if (!string.IsNullOrEmpty(path) && videoThumbnailCache.TryGetValue(path, out cached))
        {
            slot.Texture = cached.Texture;
            slot.ThumbnailReady = cached.Ready;
            slot.ThumbnailError = cached.Error;
        }
        else if (!string.IsNullOrEmpty(path))
        {
            if (TryLoadVideoThumbnailCache(path, out cached))
            {
                videoThumbnailCache[path] = cached;
                slot.Texture = cached.Texture;
                slot.ThumbnailReady = cached.Ready;
                slot.ThumbnailError = cached.Error;
            }
            else if (IsVideoBrowserVisible())
            {
                EnqueueVideoThumbnail(path);
            }
        }
    }

    private bool IsVideoBrowserVisible()
    {
        return activeScreen == 0 && !lowerPanelShowsEffects && mediaBrowserMode == MediaBrowserMode.Videos;
    }

    private void ResetVideoThumbnailCache()
    {
        foreach (var entry in videoThumbnailCache.Values)
        {
            if (entry != null && entry.Texture != null)
            {
                Destroy(entry.Texture);
            }
        }
        videoThumbnailCache.Clear();
        videoThumbnailPreloadQueue.Clear();
        queuedVideoThumbnailPaths.Clear();
        pendingRootVideoScan = null;
        rootThumbnailScanInFlight = false;
        visibleBrowserVideoPaths.Clear();
    }

    private void EnqueueVideoThumbnail(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        if (videoThumbnailCache.ContainsKey(path) || !queuedVideoThumbnailPaths.Add(path))
        {
            return;
        }

        videoThumbnailPreloadQueue.Enqueue(path);
    }

    private void UpdateStaticBrowserThumbnails()
    {
        if (Time.unscaledTime < nextThumbnailWorkTime)
        {
            return;
        }

        var showBrowserVideos = IsVideoBrowserVisible() && visibleBrowserVideoPaths.Count > 0;
        if (!showBrowserVideos && videoThumbnailPreloadQueue.Count == 0)
        {
            return;
        }

        var criticalPlayback = HasCriticalSourcePlayback();
        if (showBrowserVideos)
        {
            for (var i = 0; i < visibleBrowserVideoPaths.Count; i++)
            {
                var path = visibleBrowserVideoPaths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                VideoThumbnailCacheEntry cached;
                if (videoThumbnailCache.TryGetValue(path, out cached))
                {
                    continue;
                }

                if (TryGenerateVideoThumbnail(path, criticalPlayback))
                {
                    nextThumbnailWorkTime = Time.unscaledTime + (criticalPlayback ? 0.6f : 0.08f);
                    return;
                }
            }
        }

        if (videoThumbnailPreloadQueue.Count > 0)
        {
            string path = null;
            while (videoThumbnailPreloadQueue.Count > 0 && string.IsNullOrEmpty(path))
            {
                var candidate = videoThumbnailPreloadQueue.Dequeue();
                queuedVideoThumbnailPaths.Remove(candidate);
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate) && !videoThumbnailCache.ContainsKey(candidate))
                {
                    path = candidate;
                }
            }

            if (!string.IsNullOrEmpty(path) && TryGenerateVideoThumbnail(path, criticalPlayback))
            {
                nextThumbnailWorkTime = Time.unscaledTime + (criticalPlayback ? 1.0f : 0.12f);
            }
        }
    }

    private bool TryGenerateVideoThumbnail(string path, bool lowPriority)
    {
        if (string.IsNullOrEmpty(path) || videoThumbnailCache.ContainsKey(path))
        {
            return false;
        }

        var entry = new VideoThumbnailCacheEntry();
        try
        {
            var thumbnail = NativeThumbnailExtractor.Extract(path, lowPriority ? 160 : 320, lowPriority ? 90 : 180);
            if (thumbnail != null && thumbnail.Pixels != null && thumbnail.Pixels.Length > 0)
            {
                entry.Texture = new Texture2D(thumbnail.Width, thumbnail.Height, TextureFormat.RGBA32, false);
                entry.Texture.LoadRawTextureData(thumbnail.Pixels);
                entry.Texture.Apply(false, true);
                entry.Texture.filterMode = FilterMode.Bilinear;
                entry.Texture.wrapMode = TextureWrapMode.Clamp;
                SaveVideoThumbnailCache(path, entry.Texture);
            }
            entry.Ready = true;
        }
        catch (Exception ex)
        {
            entry.Ready = true;
            entry.Error = ex.Message;
        }

        videoThumbnailCache[path] = entry;
        return true;
    }

    private bool TryLoadVideoThumbnailCache(string path, out VideoThumbnailCacheEntry entry)
    {
        entry = null;
        try
        {
            var cachePath = VideoThumbnailCachePath(path);
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                return false;
            }

            var bytes = File.ReadAllBytes(cachePath);
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return false;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            entry = new VideoThumbnailCacheEntry
            {
                Texture = texture,
                Ready = true
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveVideoThumbnailCache(string path, Texture2D texture)
    {
        if (texture == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(mediaThumbnailCacheRoot))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(mediaThumbnailCacheRoot);
            var cachePath = VideoThumbnailCachePath(path);
            if (string.IsNullOrEmpty(cachePath))
            {
                return;
            }

            File.WriteAllBytes(cachePath, texture.EncodeToPNG());
        }
        catch
        {
        }
    }

    private string VideoThumbnailCachePath(string path)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(mediaThumbnailCacheRoot) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            var key = StableTextHash(path + "|" + info.Length.ToString(CultureInfo.InvariantCulture) + "|" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)).ToString("x8", CultureInfo.InvariantCulture);
            return Path.Combine(mediaThumbnailCacheRoot, key + ".png");
        }
        catch
        {
            return null;
        }
    }

    private bool HasCriticalSourcePlayback()
    {
        if (vjLayers == null)
        {
            return false;
        }

        for (var i = SourceStartIndex; i < MainEffectStartIndex; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled)
            {
                continue;
            }

            if (layer.SourceKind == LayerSourceKind.VideoFile && layer.Player != null && layer.Player.isPrepared)
            {
                if (layer.UsesKlakHapVideo)
                {
                    return true;
                }

                if (layer.Player.isPlaying || layer.Player.frame > 0 || layer.Player.time > 0.0)
                {
                    return true;
                }
            }

            if (layer.SourceKind == LayerSourceKind.YouTube && layer.Player != null && layer.Player.isPrepared)
            {
                if (layer.Player.isPlaying || layer.Player.frame > 0 || layer.Player.time > 0.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MaintainCriticalVideoPlayback()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = SourceStartIndex; i < MainEffectStartIndex; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled || layer.SourceKind != LayerSourceKind.VideoFile)
            {
                continue;
            }

            if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
            {
                var length = ReflectionDouble(layer.KlakHapPlayer, "streamDuration");
                var current = ReflectionDouble(layer.KlakHapPlayer, "time");
                if (length > 0.01 && current >= Math.Max(0.0, length - 0.10))
                {
                    try
                    {
                        SetReflectionValue(layer.KlakHapPlayer, "loop", true);
                        SetReflectionValue(layer.KlakHapPlayer, "speed", 1f);
                        SetReflectionValue(layer.KlakHapPlayer, "time", 0f);
                        SetReflectionValue(layer.KlakHapPlayer, "enabled", true);
                    }
                    catch
                    {
                    }
                }
                continue;
            }

            if (layer.Player == null)
            {
                continue;
            }

            var player = layer.Player;
            if (!player.isPrepared || player.isPlaying || !player.isLooping || string.IsNullOrEmpty(layer.Path) || !IsMp4File(layer.Path))
            {
                continue;
            }

            var nearLoopPoint = player.length > 0.01 && player.time >= Math.Max(0.0, player.length - 0.18);
            var endedFrames = player.frameCount > 0 && player.frame >= (long)Math.Max(0, player.frameCount - 2);
            if (!nearLoopPoint && !endedFrames)
            {
                continue;
            }

            try
            {
                player.time = 0.0;
                player.Play();
            }
            catch
            {
            }
        }
    }

    private void ExpandMediaFolderPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            {
                expandedMediaFolders.Add(current);
                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }
                current = parent.FullName;
            }
        }
        catch
        {
        }
    }

    private void LoadVideoIntoLayer(int layerIndex, string path)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex) || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        LoadVideoIntoLayerState(vjLayers[layerIndex], path, LayerSourceOrigin.FileBrowser, Path.GetFileName(path), true, LayerSlotLabel(layerIndex));
    }

    private void LoadImageIntoLayer(int layerIndex, string path)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex) || string.IsNullOrEmpty(path) || !File.Exists(path))
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
        if (layer == null || string.IsNullOrEmpty(path) || !File.Exists(path))
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
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);

        var loadToken = layer.VideoLoadToken;
        if (string.Equals(Path.GetExtension(path), ".mov", StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(LoadMovWithHapSupportInLayerState(layer, path, loadToken, logLabel));
            return;
        }

        StartCoroutine(LoadStandardVideoFileInLayerState(layer, path, loadToken, logLabel));
    }

    private void LoadImageIntoLayerState(LayerState layer, string path, LayerSourceOrigin origin, string sourceName, bool resetEffect, string logLabel)
    {
        if (layer == null || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        var texture = LoadImageTexture(path);
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
        SetGeneratorVisible(layer.Generator, false);
        SetTextSourceVisible(layer, false);
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
        layer.SourceKind = LayerSourceKind.Text;
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.SourceName = "Text";
        ResetLayerEffectState(layer);
        layer.Enabled = true;
        layer.VideoStatus = null;
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
            layer.VideoStatus = FirstNonEmptyLine(error) ?? "Video load failed.";
            AddLog("Video load failed: " + layer.VideoStatus + " file=" + Path.GetFileName(path));
            yield break;
        }

        layer.VideoResyncPending = false;
        layer.VideoStatus = null;
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

        layer.VideoStatus = FirstNonEmptyLine(klakStatus ?? "") ?? "KlakHap could not open this MOV.";
        AddLog("Hap/MOV KlakHap open failed: " + layer.VideoStatus + " file=" + Path.GetFileName(path));
    }

    private static bool TryGetCurrentVideoLoadState(LayerState layer, string path, int loadToken)
    {
        return layer != null &&
               layer.VideoLoadToken == loadToken &&
               string.Equals(layer.Path, path, StringComparison.OrdinalIgnoreCase);
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
                return lowered.Contains("hap");
            }
        }
        catch (Exception ex)
        {
            status = "MOV codec probe failed: " + ex.Message;
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
        if (youtube.WaveformTexture != null)
        {
            Destroy(youtube.WaveformTexture);
            youtube.WaveformTexture = null;
        }
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
        if (string.Equals(path, "/watch", StringComparison.OrdinalIgnoreCase))
        {
            var source = request.QueryString["v"];
            if (string.IsNullOrEmpty(source))
            {
                source = request.QueryString["url"];
            }

            var info = ResolveYoutubeSourceInfo(source, playbackMode);
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

            var info = ResolveYoutubeSourceInfo(source, playbackMode);
            var responseBody = new YoutubeServerVideoResponse
            {
                success = string.IsNullOrEmpty(info.Error) && !string.IsNullOrEmpty(info.DirectUrl),
                error = info.Error,
                url = info.DirectUrl,
                watchUrl = BuildYoutubeWatchRedirectUrl(info.Source, playbackMode),
                width = info.Width,
                height = info.Height
            };
            response.StatusCode = responseBody.success ? 200 : 502;
            response.ContentType = "application/json; charset=utf-8";
            WriteTextResponse(response, JsonUtility.ToJson(responseBody));
            return;
        }

        response.StatusCode = 404;
        response.ContentType = "application/json; charset=utf-8";
        WriteTextResponse(response, "{\"success\":false,\"error\":\"Not found\"}");
    }

    private YoutubeResolvedVideoInfo ResolveYoutubeSourceInfo(string source, YoutubePlaybackMode playbackMode)
    {
        var normalized = NormalizeYoutubeSource(source);
        if (string.IsNullOrEmpty(normalized))
        {
            return new YoutubeResolvedVideoInfo { Source = source, Error = "Missing YouTube URL or video id." };
        }

        if (TryGetCachedYoutubeResolvedVideo(normalized, playbackMode, out var cached))
        {
            return cached;
        }

        var resolver = FindYoutubeResolver();
        if (string.IsNullOrEmpty(resolver))
        {
            return new YoutubeResolvedVideoInfo { Source = normalized, Error = "yt-dlp not found. Put yt-dlp.exe in rebuild/tools." };
        }

        System.Diagnostics.Process process = null;
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = resolver,
                Arguments = BuildYoutubeResolverArguments("--no-playlist -f " + QuoteProcessArgument(YoutubeFormatSelector(playbackMode)) + " --print \"RES:%(width)sx%(height)s\" -g " + QuoteProcessArgument(normalized)),
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

    private bool TryGetCachedYoutubeResolvedVideo(string source, YoutubePlaybackMode playbackMode, out YoutubeResolvedVideoInfo info)
    {
        lock (youtubeRedirectCacheLock)
        {
            var key = YoutubeResolvedVideoCacheKey(source, playbackMode);
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
            youtubeRedirectCache[YoutubeResolvedVideoCacheKey(info.Source, info.PlaybackMode)] = info;
        }
    }

    private string BuildYoutubeWatchRedirectUrl(string source, YoutubePlaybackMode playbackMode)
    {
        if (string.IsNullOrEmpty(source))
        {
            return YoutubeRedirectBaseUrl() + "/watch";
        }

        return YoutubeRedirectBaseUrl() + "/watch?v=" + UnityWebRequest.EscapeURL(source) + "&mode=" + playbackMode.ToString();
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

    private static string YoutubeResolvedVideoCacheKey(string source, YoutubePlaybackMode playbackMode)
    {
        return playbackMode.ToString() + ":" + source;
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
                layer.YouTube.Resolving = false;
                layer.YouTube.Error = (!youtubeRedirectServerRunning
                    ? "Local YouTube redirect server could not start. "
                    : "Local YouTube server error: ") + request.error;
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
            layer.YouTube.Resolving = false;
            layer.YouTube.Error = payload != null && !string.IsNullOrEmpty(payload.error) ? payload.error : "Local YouTube server could not resolve this video.";
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
        youtube.WaveformAnalyzing = false;
        youtube.WaveformStatus = texture == null ? "Waveform unavailable" : "Waveform analyzed";
    }

    private string FindYoutubeResolver()
    {
        if (youtubeResolverChecked)
        {
            return youtubeResolverPath;
        }

        youtubeResolverChecked = true;
        var localCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "yt-dlp.exe")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", "yt-dlp.exe"))
        };
        for (var i = 0; i < localCandidates.Length; i++)
        {
            var local = localCandidates[i];
            if (File.Exists(local))
            {
                youtubeResolverPath = local;
                return youtubeResolverPath;
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
            var candidate = Path.Combine(dir.Trim(), "yt-dlp.exe");
            if (File.Exists(candidate))
            {
                youtubeResolverPath = candidate;
                return youtubeResolverPath;
            }
            candidate = Path.Combine(dir.Trim(), "yt-dlp");
            if (File.Exists(candidate))
            {
                youtubeResolverPath = candidate;
                return youtubeResolverPath;
            }
        }

        youtubeResolverPath = null;
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

    private string BuildYoutubeResolverArguments(string baseArguments)
    {
        FindYoutubeJsRuntime();
        if (!string.IsNullOrEmpty(youtubeJsRuntimeName))
        {
            return "--js-runtimes " + QuoteProcessArgument(youtubeJsRuntimeName) + " " + (baseArguments ?? "");
        }

        return baseArguments ?? "";
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
        if (!useBltBridgeMode)
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
                        ApplyBltParams(root);
                        bltError = null;
                        bltReceived = true;
                        lastBltUpdateUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        bltError = "BLT JSON parse error: " + ex.Message;
                    }
                }
                else
                {
                    bltError = "BLT metadata server: " + request.error;
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
            yield return FetchBltRuntimeWave(2);
            yield return new WaitForSeconds(Mathf.Max(0.01f, bltWaveFrameIntervalSeconds));
        }
    }

    private void ApplyBltParams(Dictionary<string, object> root)
    {
        var playersDict = Dict(root, "players");
        if (playersDict == null)
        {
            return;
        }

        foreach (var key in SortedKeys(playersDict))
        {
            int deviceNumber;
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out deviceNumber))
            {
                continue;
            }

            if (deviceNumber != 1 && deviceNumber != 2)
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

    private void MaybeAutoStartBltBridge(bool force)
    {
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            bltParamsUrl = DefaultBltParamsUrl;
        }

        if (!IsLocalBltParamsUrl(bltParamsUrl))
        {
            return;
        }

        if (!force)
        {
            if (bltReceived)
            {
                return;
            }

            if (lastBltBridgeStartAttemptUtc != DateTime.MinValue &&
                lastBltBridgeStartAttemptUtc.AddSeconds(15) > DateTime.UtcNow)
            {
                return;
            }

            if (!NeedsBltFallback())
            {
                return;
            }
        }

        var scriptPath = FindBltBridgeStartScript();
        if (string.IsNullOrEmpty(scriptPath))
        {
            return;
        }

        lastBltBridgeStartAttemptUtc = DateTime.UtcNow;
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)),
                Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\" -Port 17081",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(info);
            AddLog("Started BLT overlay bridge helper.");
        }
        catch (Exception ex)
        {
            bltError = "BLT bridge start failed: " + ex.Message;
            AddLog(bltError);
        }
    }

    private bool NeedsBltFallback()
    {
        lock (stateLock)
        {
            foreach (var player in players.Values)
            {
                if (!player.HasStatus)
                {
                    continue;
                }

                if (IsLegacyNexusDeck(player.DeviceName))
                {
                    return true;
                }

                if ((player.DeviceNumber == 1 || player.DeviceNumber == 2) &&
                    player.RekordboxId > 0 &&
                    string.IsNullOrEmpty(player.BltTitle))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLocalBltParamsUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        try
        {
            var uri = new Uri(url);
            return uri.IsLoopback;
        }
        catch
        {
            return false;
        }
    }

    private static string FindBltBridgeStartScript()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "beat-link-trigger-clean", "tools", "start-unity-overlay.ps1")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "beat-link-trigger-clean", "tools", "start-unity-overlay.ps1")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "rebuild", "beat-link-trigger-clean", "tools", "start-unity-overlay.ps1"))
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

    private void StartListeners()
    {
        if (running)
        {
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
            else if ("縺繧螟閾蝨荳譁髯蜷逡鬆驥蛯莠譚鬟隱".IndexOf(c) >= 0)
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

    private void OnGUI()
    {
        EnsureStyles();
        if (Event.current != null && Event.current.isMouse && IsMouseOnSecondDisplay())
        {
            Event.current.Use();
        }

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
        RefreshMediaFolderCache();
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
        if (GUI.Button(new Rect(margin + 460f, margin + 2f, 132f, 30f), secondDisplayUiEnabled ? "2nd Disp ON" : "2nd Disp"))
        {
            ToggleSecondDisplayUi();
        }
        GUI.Label(new Rect(margin + 600f, margin + 7f, 260f, 24f), secondDisplayUiEnabled ? "Display2: " + SecondDisplayUiModeLabel() : "", smallStyle);
        GUI.Label(new Rect(full.width - 520f, margin + 7f, 500f, 24f), "Space: Player screen   E: Layer settings   Selected: " + LayerSlotLabel(selectedLayerIndex), smallStyle);
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
        secondDisplayUiEnabled = !secondDisplayUiEnabled;
        if (!secondDisplayUiEnabled)
        {
            secondDisplayUiMode = SecondDisplayUiMode.None;
        }
        if (secondDisplayUiEnabled && activeProgramDisplay == 1)
        {
            DisableProgramOutput("Program output disabled because Display 2 is reserved for Second Display UI.");
        }
        PlayerPrefs.SetInt(SecondDisplayUiPrefKey, secondDisplayUiEnabled ? 1 : 0);
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
                return "Player";
            case SecondDisplayUiMode.Settings:
                return "Layer Settings";
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

        if (secondDisplayUiMode == SecondDisplayUiMode.Settings)
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
        secondDisplayUiIndex = Display.displays.Length > 1 || Application.isEditor ? 1 : -1;
        if (secondDisplayUiEnabled && secondDisplayUiIndex < 0)
        {
            outputStatus = "Unity Display 2 is unavailable.";
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
            var active = secondDisplayUiEnabled && secondDisplayUiIndex > 0 && secondDisplayUiMode == SecondDisplayUiMode.Settings;
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
            settingsMonitorCommonTitleText = null;
            settingsMonitorCommonText = null;
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
        }

        settingsMonitorDisplayRoot = new GameObject("Settings Monitor Display");
        settingsMonitorDisplayRoot.transform.SetParent(transform, false);
        settingsMonitorDisplayCanvas = settingsMonitorDisplayRoot.AddComponent<Canvas>();
        settingsMonitorDisplayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        settingsMonitorDisplayCanvas.pixelPerfect = false;
        settingsMonitorDisplayCanvas.sortingOrder = 50;
        settingsMonitorDisplayRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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
        settingsMonitorBlend50Button = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlend50Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -106f), new Vector2(218f, -76f), "50Add", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Add50;
            }
        });
        settingsMonitorBlend100Button = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorBlend100Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(226f, -106f), new Vector2(316f, -76f), "Add", () =>
        {
            var layerIndex = CurrentSettingsMonitorLayerIndex();
            if (vjLayers != null && layerIndex >= 0 && layerIndex < vjLayers.Length && vjLayers[layerIndex] != null)
            {
                vjLayers[layerIndex].BlendMode = LayerBlendMode.Add100;
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
        settingsMonitorCommonText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorCommon", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -152f), new Vector2(-14f, -36f), 16, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorEffectTitleText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffectTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -188f), new Vector2(-14f, -160f), 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorEffectText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffect", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -286f), new Vector2(-14f, -190f), 16, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorMidiStatusText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorMidiStatus", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 4f), new Vector2(-14f, 28f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
        settingsMonitorEffectButtons = new UiButtonBundle[14];
        for (var i = 0; i < settingsMonitorEffectButtons.Length; i++)
        {
            var column = i % 3;
            var row = i / 3;
            var xMin = 14f + column * 104f;
            var xMax = xMin + 96f;
            var yTop = -322f - row * 36f;
            var yBottom = yTop + 30f;
            var index = i;
            settingsMonitorEffectButtons[i] = CreateUiButton(settingsMonitorDetailsPanel.transform, "SettingsMonitorEffectButton" + i.ToString(CultureInfo.InvariantCulture), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xMin, yTop), new Vector2(xMax, yBottom), "-", () => OnSettingsMonitorEffectButton(index));
        }
        settingsMonitorSourceTitleText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorSourceTitle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -404f), new Vector2(-14f, -376f), 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        settingsMonitorSourceText = CreateUiText(settingsMonitorDetailsPanel.transform, "SettingsMonitorSource", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(-14f, -406f), 16, TextAnchor.UpperLeft, FontStyle.Normal);
        settingsMonitorSourceButtons = new UiButtonBundle[40];
        for (var i = 0; i < settingsMonitorSourceButtons.Length; i++)
        {
            var row = i / 4;
            var column = i % 4;
            var xMin = 14f + column * 78f;
            var xMax = xMin + 72f;
            var yTop = -440f - row * 36f;
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
        if (eventData == null || vjLayers == null || selectedLayerIndex < 0 || selectedLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var layer = vjLayers[selectedLayerIndex];
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
            return;
        }

        var layer = vjLayers[data.SelectedLayerIndex];
        if (layer == null)
        {
            return;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.YouTube:
                if (layer.YouTube != null)
                {
                    var snapshot = Snapshot();
                    var linkedPlayer = snapshot == null ? null : FindPlayer(snapshot.Players, layer.YouTube.WaveformPlayerNumber);
                    settingsMonitorAuxTitleText.text = "Waveform / Linked Player";
                    settingsMonitorAuxTitleText.gameObject.SetActive(true);
                    if (settingsMonitorAuxLabelA != null)
                    {
                        settingsMonitorAuxLabelA.text = "Waveform / position";
                        settingsMonitorAuxLabelA.gameObject.SetActive(true);
                    }
                    if (settingsMonitorAuxLabelB != null)
                    {
                        settingsMonitorAuxLabelB.text = "Player " + layer.YouTube.WaveformPlayerNumber.ToString(CultureInfo.InvariantCulture) + " full waveform";
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
                    settingsMonitorAuxImageB.texture = linkedPlayer == null ? null : linkedPlayer.DirectWaveformTexture;
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
                    UpdateSettingsMonitorPresetButtons(layer.Generator);
                    UpdateSettingsMonitorFaceButtons(layer.Generator);
                }
                break;
        }
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
        UpdateBlendButton(settingsMonitorBlend50Button, string.Equals(data.BlendMode, "add50", StringComparison.OrdinalIgnoreCase));
        UpdateBlendButton(settingsMonitorBlend100Button, string.Equals(data.BlendMode, "add100", StringComparison.OrdinalIgnoreCase) || string.Equals(data.BlendMode, "add", StringComparison.OrdinalIgnoreCase));
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
                    ConfigureSourceButton(settingsMonitorSourceButtons[5], true, "Resolve", () =>
                    {
                        if (!string.IsNullOrEmpty(layer.YouTube.Url))
                        {
                            layer.YouTube.Resolving = true;
                            layer.YouTube.Error = null;
                            StartCoroutine(ResolveYoutubeVideo(data.SelectedLayerIndex, layer.YouTube.Url));
                        }
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

        if (effect.Kind == LayerEffectKind.Blur || effect.Kind == LayerEffectKind.Glitch || effect.Kind == LayerEffectKind.RgbEffect)
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
            else if (effect.Kind == LayerEffectKind.Blur || effect.Kind == LayerEffectKind.Strobe)
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
                card.OverviewWavePath = SaveSnapshotTextureToFile(player.DirectWaveformTexture, overviewWavePath, 640, 52) ? overviewWavePath : null;
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
        LayerState layer = null;
        if (vjLayers != null && targetLayerIndex >= 0 && targetLayerIndex < vjLayers.Length)
        {
            layer = vjLayers[targetLayerIndex];
        }

        return new SettingsViewData
        {
            SelectedLayerIndex = targetLayerIndex,
            LayerLabel = LayerSlotLabel(targetLayerIndex),
            Title = layer == null ? "No layer selected" : LayerName(layer),
            Subtitle = layer == null ? "" : layer.SourceKind.ToString(),
            Status = layer == null ? "" : PreferText(layer.VideoStatus, ""),
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
            case LayerBlendMode.Add100:
                return "add100";
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
                        vjLayers[index].BlendMode = LayerBlendMode.Add100;
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
        if (IsSourceLayerSlot(layerIndex))
        {
            lines.Add("Group: " + layer.SourceGroupMode);
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
            lines.Add("Group: " + layer.SourceGroupMode);
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
        if (layer.Effect.Kind == LayerEffectKind.Blur || layer.Effect.Kind == LayerEffectKind.Glitch || layer.Effect.Kind == LayerEffectKind.RgbEffect)
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
        if (PlayerPrefs.HasKey(BltBridgeModePrefKey))
        {
            useBltBridgeMode = PlayerPrefs.GetInt(BltBridgeModePrefKey, useBltBridgeMode ? 1 : 0) != 0;
        }

        if (PlayerPrefs.HasKey(BltParamsUrlPrefKey))
        {
            bltParamsUrl = PlayerPrefs.GetString(BltParamsUrlPrefKey, DefaultBltParamsUrl);
        }

        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            bltParamsUrl = DefaultBltParamsUrl;
        }
    }

    private void SaveBltBridgePreference()
    {
        PlayerPrefs.SetInt(BltBridgeModePrefKey, useBltBridgeMode ? 1 : 0);
        PlayerPrefs.SetString(BltParamsUrlPrefKey, string.IsNullOrEmpty(bltParamsUrl) ? DefaultBltParamsUrl : bltParamsUrl);
        PlayerPrefs.Save();
    }

    private void ApplyBltBridgeModeSelection()
    {
        var nextUrl = string.IsNullOrWhiteSpace(pendingBltParamsUrl) ? DefaultBltParamsUrl : pendingBltParamsUrl.Trim();
        var modeChanged = useBltBridgeMode != pendingBltBridgeMode;
        var urlChanged = !string.Equals(bltParamsUrl, nextUrl, StringComparison.OrdinalIgnoreCase);

        bltParamsUrl = nextUrl;
        useBltBridgeMode = pendingBltBridgeMode;
        SaveBltBridgePreference();

        bltError = null;
        bltReceived = false;
        lastBltUpdateUtc = DateTime.MinValue;
        lastBltBridgeStartAttemptUtc = DateTime.MinValue;
        ResetPlayerMetadataCache();

        if (useBltBridgeMode)
        {
            StopListeners();
            EnsureBltPollingState();
            MaybeAutoStartBltBridge(true);
            outputStatus = modeChanged || urlChanged
                ? "Metadata mode: External BLT (" + bltParamsUrl + ")"
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
            }
        }
    }

    private void DrawSettingsScreen(Rect full)
    {
        RefreshOutputDevices();

        var margin = 18f;
        GUI.Label(new Rect(margin, margin, 320f, 34f), "Setting", titleStyle);
        GUI.Label(new Rect(full.width - 360f, margin + 7f, 340f, 24f), "R / Esc: Return", smallStyle);

        var tabRect = new Rect(margin, margin + 48f, 180f, full.height - margin * 2f - 48f);
        GUI.DrawTexture(tabRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.028f, 0.032f, 0.038f, 1f), 0f, 6f);
        GUI.Label(new Rect(tabRect.x + 14f, tabRect.y + 14f, tabRect.width - 28f, 24f), "Setting", normalStyle);
        GUI.DrawTexture(new Rect(tabRect.x + 12f, tabRect.y + 52f, tabRect.width - 24f, 32f), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.12f, 0.18f, 0.2f, 1f), 0f, 4f);
        GUI.Label(new Rect(tabRect.x + 22f, tabRect.y + 58f, tabRect.width - 44f, 22f), "Output", normalStyle);

        var panel = new Rect(tabRect.xMax + 14f, tabRect.y, full.width - tabRect.xMax - margin - 14f, tabRect.height);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);

        GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, panel.height - 36f));
        settingsScroll = GUILayout.BeginScrollView(settingsScroll);
        GUILayout.Label("Output", titleStyle);
        GUILayout.Space(10f);
        GUILayout.Label("Video", normalStyle);
        GUILayout.Label("Final program output fullscreen display", smallStyle);
        DrawOutputDropdown("Display", videoOutputDevices, ref selectedVideoOutputIndex, ref videoOutputDropdownOpen);
        DrawOutputDropdown("Resolution", outputResolutionOptions, ref selectedOutputResolutionIndex, ref outputResolutionDropdownOpen);
        var nextSecondDisplayUi = GUILayout.Toggle(secondDisplayUiEnabled, secondDisplayUiEnabled ? "Second Display UI ON (Display 2)" : "Second Display UI OFF", GUILayout.Width(340f));
        if (nextSecondDisplayUi != secondDisplayUiEnabled)
        {
            ToggleSecondDisplayUi();
        }
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
        GUILayout.Label("Audio", normalStyle);
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

        GUILayout.Space(18f);
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

        GUILayout.Space(18f);
        GUILayout.Label("MIDI", titleStyle);
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

        GUILayout.Space(18f);
        GUILayout.Label("DJ Link Metadata", titleStyle);
        GUILayout.Label("CDJ-2000nexus etc. can use External BLT mode for more stable metadata.", smallStyle);
        pendingBltBridgeMode = GUILayout.Toggle(pendingBltBridgeMode, "Use External BLT bridge instead of internal direct DJ Link", GUILayout.Width(520f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Params URL", GUILayout.Width(110f));
        pendingBltParamsUrl = GUILayout.TextField(string.IsNullOrEmpty(pendingBltParamsUrl) ? DefaultBltParamsUrl : pendingBltParamsUrl, GUILayout.MinWidth(360f), GUILayout.Height(26f));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Metadata Mode", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            ApplyBltBridgeModeSelection();
        }
        if (GUILayout.Button("Start Local BLT", GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            pendingBltBridgeMode = true;
            ApplyBltBridgeModeSelection();
        }
        if (GUILayout.Button("Open BLT Screen", GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            OpenBltScreen();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Current: " + (useBltBridgeMode ? "External BLT" : "Internal direct DJ Link"), smallStyle);
        GUILayout.Label("BLT status: " + BltStatusText(), smallStyle);

        GUILayout.Space(18f);
        GUILayout.Label("Status", normalStyle);
        GUILayout.Label(string.IsNullOrEmpty(outputStatus) ? "No output change applied." : outputStatus, smallStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
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
                    if (secondDisplayUiEnabled && secondDisplayUiMode == SecondDisplayUiMode.Settings)
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
            if (config != null)
            {
                if (config.Bindings != null)
                {
                    midiBindings.AddRange(config.Bindings);
                }
                midiDevicesDirty = true;
                RefreshMidiInputDevices();
                selectedMidiInputIndex = FindMidiInputOptionIndex(string.IsNullOrEmpty(config.SelectedInputId) ? "disabled" : config.SelectedInputId);
            }
        }
        catch (Exception ex)
        {
            midiStatus = "MIDI load failed: " + ex.Message;
        }
    }

    private void UpdateProgramOutputLayers()
    {
        EnsureCompositeRenderTextures();
        UpdateCompositeStageTextures();

        if (programOutputRoot == null || programOutputRenderers == null)
        {
            return;
        }

        var outputActive = activeProgramDisplay > 0 && activeProgramDisplay < Display.displays.Length;
        programOutputRoot.SetActive(outputActive);
        if (!outputActive || vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < programOutputRenderers.Length; i++)
        {
            var renderer = programOutputRenderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            if (finalCompositeTexture == null)
            {
                renderer.sharedMaterial.mainTexture = null;
                renderer.sharedMaterial.color = new Color(1f, 1f, 1f, 0f);
                continue;
            }

            renderer.sharedMaterial.mainTexture = finalCompositeTexture;
            ApplyBlendModeToMaterial(renderer.sharedMaterial, LayerBlendMode.Alpha);
            renderer.sharedMaterial.color = Color.white;
        }
    }

    private void ValidateProgramOutputDisplayAvailability()
    {
        if (Time.unscaledTime < nextProgramOutputValidationTime)
        {
            return;
        }
        nextProgramOutputValidationTime = Time.unscaledTime + 2f;

        if (activeProgramDisplay <= 0)
        {
            return;
        }

        if (activeProgramDisplay >= Display.displays.Length)
        {
            DisableProgramOutput("Program output disabled because the Unity secondary display is unavailable.");
            return;
        }

        var monitors = EnumerateWindowsMonitors();
        var nonPrimaryCount = 0;
        for (var i = 0; i < monitors.Count; i++)
        {
            if (monitors[i] != null && !monitors[i].Primary)
            {
                nonPrimaryCount++;
            }
        }

        if (nonPrimaryCount <= 0)
        {
            DisableProgramOutput("Program output disabled because no secondary monitor is currently available.");
        }
    }

    private void DisableProgramOutput(string status)
    {
        activeProgramDisplay = -1;
        if (programOutputCamera != null)
        {
            programOutputCamera.targetDisplay = 0;
        }
        if (programOutputRoot != null)
        {
            programOutputRoot.SetActive(false);
        }
        if (!string.IsNullOrEmpty(status))
        {
            outputStatus = status;
        }
    }

    private void UpdateCompositeStageTextures()
    {
        if (vjLayers == null)
        {
            return;
        }

        EnsureCompositeRenderTextures();
        if (programSoloLayerIndex >= SourceStartIndex &&
            programSoloLayerIndex < MainEffectStartIndex &&
            vjLayers != null &&
            programSoloLayerIndex < vjLayers.Length &&
            vjLayers[programSoloLayerIndex] != null)
        {
            CompositeSingleLayerToTarget(finalCompositeTexture, compositeScratchTexture, programSoloLayerIndex, vjLayers[programSoloLayerIndex]);
            return;
        }
        CompositeSourceLayersByMode(mainSourceCompositeTexture, compositeScratchTexture, LayerSourceGroupMode.Main, null);
        CompositeLayerRange(mainEffectCompositeTexture, compositeScratchTexture, MainEffectStartIndex, MainEffectLayerCount, mainSourceCompositeTexture);
        CompositeSourceLayersByMode(overlayCompositeTexture, compositeScratchTexture, LayerSourceGroupMode.Overlay, mainEffectCompositeTexture);
        CompositeLayerRange(finalCompositeTexture, compositeScratchTexture, AllEffectStartIndex, AllEffectLayerCount, overlayCompositeTexture);
    }

    private void CompositeSingleLayerToTarget(RenderTexture target, RenderTexture scratch, int layerIndex, LayerState layer)
    {
        if (target == null || scratch == null)
        {
            return;
        }

        Graphics.Blit(Texture2D.blackTexture, target);
        if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None)
        {
            return;
        }

        var layerTexture = LayerPreviewTexture(layerIndex, layer);
        if (layerTexture == null)
        {
            return;
        }

        var compositeMaterial = EnsureLayerCompositeMaterial();
        if (compositeMaterial == null)
        {
            Graphics.Blit(layerTexture, target);
            return;
        }

        compositeMaterial.SetTexture("_OverlayTex", layerTexture);
        compositeMaterial.SetFloat("_Opacity", Mathf.Clamp01(layer.Opacity));
        compositeMaterial.SetFloat("_BlendMode", (float)LayerBlendMode.Alpha);
        compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(layer.HueShift, 1f));
        compositeMaterial.SetFloat("_ColorMode", (float)layer.ColorMode);
        compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(layer.InvertAmount));
        compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(layer.MonochromeAmount));
        Graphics.Blit(target, scratch, compositeMaterial);
        Graphics.Blit(scratch, target);
    }

    private void CompositeSourceLayersByMode(RenderTexture target, RenderTexture scratch, LayerSourceGroupMode mode, Texture baseTexture)
    {
        if (target == null || scratch == null || vjLayers == null)
        {
            return;
        }

        if (mode == LayerSourceGroupMode.Main && baseTexture == null && CompositeMainSourceLayersWithMask(target, scratch))
        {
            return;
        }

        var compositeMaterial = EnsureLayerCompositeMaterial();
        var source = baseTexture ?? Texture2D.blackTexture;
        Graphics.Blit(source, target);
        var current = target;
        var next = scratch;

        for (var i = SourceStartIndex; i < MainEffectStartIndex; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None || layer.SourceGroupMode != mode)
            {
                continue;
            }
            if (layer.ProgramMuted)
            {
                continue;
            }

            var layerTexture = LayerPreviewTexture(i, layer);
            if (layerTexture == null)
            {
                continue;
            }

            if (compositeMaterial == null)
            {
                Graphics.Blit(layerTexture, current);
                continue;
            }

            var effectiveBlendMode = layer.BlendMode == LayerBlendMode.Mask && mode != LayerSourceGroupMode.Main
                ? LayerBlendMode.Alpha
                : layer.BlendMode;
            compositeMaterial.SetTexture("_OverlayTex", layerTexture);
            compositeMaterial.SetFloat("_Opacity", Mathf.Clamp01(layer.Opacity));
            compositeMaterial.SetFloat("_BlendMode", (float)effectiveBlendMode);
            compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(layer.HueShift, 1f));
            compositeMaterial.SetFloat("_ColorMode", (float)layer.ColorMode);
            compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(layer.InvertAmount));
            compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(layer.MonochromeAmount));
            Graphics.Blit(current, next, compositeMaterial);

            var swap = current;
            current = next;
            next = swap;
        }

        if (current != target)
        {
            Graphics.Blit(current, target);
        }
    }

    private bool CompositeMainSourceLayersWithMask(RenderTexture target, RenderTexture scratch)
    {
        var maskIndex = FindMainSourceMaskLayerIndex();
        if (maskIndex < SourceStartIndex)
        {
            return false;
        }

        var maskLayer = vjLayers[maskIndex];
        var maskTexture = LayerPreviewTexture(maskIndex, maskLayer);
        if (maskTexture == null)
        {
            return false;
        }

        var maskMaterial = EnsureLayerMaskCompositeMaterial();
        if (maskMaterial == null)
        {
            return false;
        }

        var lower = RenderTexture.GetTemporary(outputWidth, outputHeight, 0, target.format);
        var upper = RenderTexture.GetTemporary(outputWidth, outputHeight, 0, target.format);
        try
        {
            CompositeSourceLayerRangeToTexture(lower, scratch, SourceStartIndex, maskIndex, LayerSourceGroupMode.Main, null, maskIndex);
            CompositeSourceLayerRangeToTexture(upper, scratch, maskIndex + 1, MainEffectStartIndex, LayerSourceGroupMode.Main, null, maskIndex);
            maskMaterial.SetTexture("_UpperTex", upper);
            maskMaterial.SetTexture("_MaskTex", maskTexture);
            maskMaterial.SetFloat("_MaskOpacity", Mathf.Clamp01(maskLayer.Opacity));
            Graphics.Blit(lower, target, maskMaterial);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(lower);
            RenderTexture.ReleaseTemporary(upper);
        }

        return true;
    }

    private int FindMainSourceMaskLayerIndex()
    {
        if (vjLayers == null)
        {
            return -1;
        }

        for (var i = SourceStartIndex; i < MainEffectStartIndex && i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None || layer.ProgramMuted)
            {
                continue;
            }
            if (layer.SourceGroupMode == LayerSourceGroupMode.Main && layer.BlendMode == LayerBlendMode.Mask)
            {
                return i;
            }
        }

        return -1;
    }

    private void CompositeSourceLayerRangeToTexture(RenderTexture target, RenderTexture scratch, int startIndex, int endExclusive, LayerSourceGroupMode mode, Texture baseTexture, int skipLayerIndex)
    {
        if (target == null || scratch == null || vjLayers == null)
        {
            return;
        }

        var compositeMaterial = EnsureLayerCompositeMaterial();
        Graphics.Blit(baseTexture ?? Texture2D.blackTexture, target);
        var current = target;
        var next = scratch;

        for (var i = startIndex; i < endExclusive && i < vjLayers.Length; i++)
        {
            if (i == skipLayerIndex)
            {
                continue;
            }

            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None || layer.SourceGroupMode != mode || layer.ProgramMuted)
            {
                continue;
            }

            var layerTexture = LayerPreviewTexture(i, layer);
            if (layerTexture == null)
            {
                continue;
            }

            if (compositeMaterial == null)
            {
                Graphics.Blit(layerTexture, current);
                continue;
            }

            var blendMode = layer.BlendMode == LayerBlendMode.Mask ? LayerBlendMode.Alpha : layer.BlendMode;
            compositeMaterial.SetTexture("_OverlayTex", layerTexture);
            compositeMaterial.SetFloat("_Opacity", Mathf.Clamp01(layer.Opacity));
            compositeMaterial.SetFloat("_BlendMode", (float)blendMode);
            compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(layer.HueShift, 1f));
            compositeMaterial.SetFloat("_ColorMode", (float)layer.ColorMode);
            compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(layer.InvertAmount));
            compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(layer.MonochromeAmount));
            Graphics.Blit(current, next, compositeMaterial);

            var swap = current;
            current = next;
            next = swap;
        }

        if (current != target)
        {
            Graphics.Blit(current, target);
        }
    }

    private void CompositeLayerRange(RenderTexture target, RenderTexture scratch, int startIndex, int count, Texture baseTexture)
    {
        if (target == null || scratch == null)
        {
            return;
        }

        var compositeMaterial = EnsureLayerCompositeMaterial();
        var source = baseTexture ?? Texture2D.blackTexture;
        Graphics.Blit(source, target);
        var current = target;
        var next = scratch;

        for (var i = 0; i < count; i++)
        {
            var layerIndex = startIndex + i;
            if (layerIndex < 0 || layerIndex >= vjLayers.Length)
            {
                continue;
            }

            var layer = vjLayers[layerIndex];
            if (layer == null || !layer.Enabled || layer.SourceKind == LayerSourceKind.None || layer.ProgramMuted)
            {
                continue;
            }

            var layerTexture = LayerPreviewTexture(layerIndex, layer);
            if (layerTexture == null)
            {
                continue;
            }

            if (compositeMaterial == null)
            {
                Graphics.Blit(layerTexture, current);
                continue;
            }

            compositeMaterial.SetTexture("_OverlayTex", layerTexture);
            compositeMaterial.SetFloat("_Opacity", Mathf.Clamp01(layer.Opacity));
            compositeMaterial.SetFloat("_BlendMode", (float)LayerBlendMode.Alpha);
            compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(layer.HueShift, 1f));
            compositeMaterial.SetFloat("_ColorMode", (float)layer.ColorMode);
            compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(layer.InvertAmount));
            compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(layer.MonochromeAmount));
            Graphics.Blit(current, next, compositeMaterial);

            var swap = current;
            current = next;
            next = swap;
        }

        if (current != target)
        {
            Graphics.Blit(current, target);
        }
    }

    private static Color LayerBlendColor(LayerState layer)
    {
        if (layer == null)
        {
            return new Color(1f, 1f, 1f, 0f);
        }

        var opacity = Mathf.Clamp01(layer.Opacity);
        switch (layer.BlendMode)
        {
            case LayerBlendMode.Add50:
                return new Color(1f, 1f, 1f, opacity);
            case LayerBlendMode.Add100:
                return new Color(1f, 1f, 1f, opacity);
            default:
                return new Color(1f, 1f, 1f, opacity);
        }
    }

    private Material EnsureLayerEffectMaterial()
    {
        if (layerEffectMaterial != null)
        {
            return layerEffectMaterial;
        }

        var shader = Shader.Find("BeatLink/LayerEffect");
        if (shader == null)
        {
            return null;
        }

        layerEffectMaterial = new Material(shader);
        return layerEffectMaterial;
    }

    private Material EnsureLayerCompositeMaterial()
    {
        if (layerCompositeMaterial != null)
        {
            return layerCompositeMaterial;
        }

        var shader = Shader.Find("BeatLink/LayerComposite");
        if (shader == null)
        {
            return null;
        }

        layerCompositeMaterial = new Material(shader);
        return layerCompositeMaterial;
    }

    private Material EnsureLayerMaskCompositeMaterial()
    {
        if (layerMaskCompositeMaterial != null)
        {
            return layerMaskCompositeMaterial;
        }

        var shader = Shader.Find("BeatLink/LayerMaskComposite");
        if (shader == null)
        {
            return null;
        }

        layerMaskCompositeMaterial = new Material(shader);
        return layerMaskCompositeMaterial;
    }

    private Texture LayerSourceTexture(LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        switch (layer.SourceKind)
        {
            case LayerSourceKind.VideoFile:
                return AvproLayerTexture(layer) ?? layer.Texture;
            case LayerSourceKind.Image:
                return layer.StaticTexture;
            case LayerSourceKind.YouTube:
                return layer.Texture;
            case LayerSourceKind.Text:
                return layer.Texture;
            case LayerSourceKind.Capture:
                return captureTexture;
            case LayerSourceKind.Generator3D:
                return layer.Generator == null ? null : layer.Generator.Texture;
            case LayerSourceKind.Effect:
                return layer.EffectTexture;
            default:
                return null;
        }
    }

    private Texture LayerPreviewTexture(int targetIndex, LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        if (layer.SourceKind == LayerSourceKind.Effect)
        {
            if (layer.EffectRenderInProgress)
            {
                return layer.EffectRenderedOutput ?? layer.EffectScratchTexture ?? layer.EffectTexture;
            }
            if (layer.EffectRenderedFrame == Time.frameCount)
            {
                return layer.EffectRenderedOutput ?? layer.EffectScratchTexture ?? layer.EffectTexture;
            }
            return RenderEffectLayer(targetIndex, layer);
        }

        return LayerSourceTexture(layer);
    }

    private Texture LayerPreviewTextureForEffectInput(int targetIndex, LayerState layer)
    {
        if (layer == null)
        {
            return null;
        }

        return LayerPreviewTexture(targetIndex, layer);
    }

    private bool EffectUsesLowerLayerCompositeInput(int hostIndex, LayerEffectState effect)
    {
        if (effect == null || effect.Kind == LayerEffectKind.None)
        {
            return false;
        }

        return effect.Kind != LayerEffectKind.StepSequencer && effect.Kind != LayerEffectKind.Strobe && IsEffectLayerSlot(hostIndex);
    }

    private Texture BuildLowerLayerCompositeInput(int hostIndex, LayerState hostLayer)
    {
        if (hostLayer == null || hostLayer.EffectTexture == null || hostLayer.EffectScratchTexture == null || vjLayers == null)
        {
            return null;
        }

        var compositeMaterial = EnsureLayerCompositeMaterial();
        Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectTexture);
        var current = hostLayer.EffectTexture;
        var scratch = hostLayer.EffectScratchTexture;
        var hasInput = false;

        for (var i = 0; i < hostIndex; i++)
        {
            if (i < 0 || i >= vjLayers.Length)
            {
                continue;
            }

            var sourceLayer = vjLayers[i];
            if (sourceLayer == null || !sourceLayer.Enabled || sourceLayer.SourceKind == LayerSourceKind.None)
            {
                continue;
            }

            if (!CanUseLowerCompositeInputLayer(hostIndex, sourceLayer) || sourceLayer.ProgramMuted)
            {
                continue;
            }

            var sourceTexture = LayerPreviewTextureForEffectInput(i, sourceLayer);
            if (sourceTexture == null)
            {
                continue;
            }

            hasInput = true;
            if (compositeMaterial == null)
            {
                Graphics.Blit(sourceTexture, current);
                continue;
            }

            var effectiveBlendMode = IsMainMaterialSourceLayer(sourceLayer) ? sourceLayer.BlendMode : LayerBlendMode.Alpha;
            compositeMaterial.SetTexture("_OverlayTex", sourceTexture);
            compositeMaterial.SetFloat("_Opacity", Mathf.Clamp01(sourceLayer.Opacity));
            compositeMaterial.SetFloat("_BlendMode", (float)effectiveBlendMode);
            compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(sourceLayer.HueShift, 1f));
            compositeMaterial.SetFloat("_ColorMode", (float)sourceLayer.ColorMode);
            compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(sourceLayer.InvertAmount));
            compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(sourceLayer.MonochromeAmount));
            Graphics.Blit(current, scratch, compositeMaterial);
            var swap = current;
            current = scratch;
            scratch = swap;
        }

        if (!hasInput)
        {
            Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectTexture);
            return hostLayer.EffectTexture;
        }

        if (current != hostLayer.EffectTexture)
        {
            Graphics.Blit(current, hostLayer.EffectTexture);
        }
        return hostLayer.EffectTexture;
    }

    private static bool CanUseLowerCompositeInputLayer(int hostIndex, LayerState sourceLayer)
    {
        if (sourceLayer == null)
        {
            return false;
        }

        if (IsMainEffectLayer(hostIndex) && sourceLayer.SourceKind != LayerSourceKind.Effect && IsOverlaySourceLayer(sourceLayer))
        {
            return false;
        }

        return true;
    }

    private void ConfigureLayerEffectMaterial(Material material, LayerEffectState effect, float beatFloat, float bpm)
    {
        var mode = 0f;
        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                mode = effect.RgbMode == LayerRgbEffectMode.RedOnly ? 1f :
                    effect.RgbMode == LayerRgbEffectMode.GreenOnly ? 2f :
                    effect.RgbMode == LayerRgbEffectMode.BlueOnly ? 3f :
                    effect.RgbMode == LayerRgbEffectMode.RgbInvert ? 4f :
                    effect.RgbMode == LayerRgbEffectMode.EdgeExtract ? 5f : 9f;
                break;
            case LayerEffectKind.QuadSplit:
                mode = 10f;
                break;
            case LayerEffectKind.Blur:
                mode = 6f;
                break;
            case LayerEffectKind.Glitch:
                mode = 7f;
                break;
            case LayerEffectKind.Strobe:
                mode = 8f;
                break;
            case LayerEffectKind.StepSequencer:
                mode = 0f;
                break;
        }
        material.SetFloat("_Mode", mode);
        material.SetFloat("_Intensity", Mathf.Clamp01(effect.Intensity));
        material.SetFloat("_Phase", Mathf.Repeat(beatFloat, 1f));
        var axisMode = 0f;
        switch (effect.Mode)
        {
            case LayerEffectMode.Vertical:
                axisMode = 1f;
                break;
            case LayerEffectMode.Alternate:
                axisMode = 2f;
                break;
            case LayerEffectMode.Zoom:
                axisMode = 3f;
                break;
        }
        material.SetFloat("_Axis", axisMode);
        material.SetFloat("_TimeNow", Time.realtimeSinceStartup);
        material.SetFloat("_ManualHold", effect.Mode == LayerEffectMode.Manual && effect.ManualFlashHeld ? 1f : 0f);
        material.SetFloat("_BeatCount", beatFloat);

        var strobeDivision = 4f;
        switch (effect.Mode)
        {
            case LayerEffectMode.Beat1:
                strobeDivision = 1f;
                break;
            case LayerEffectMode.Beat2:
                strobeDivision = 2f;
                break;
            case LayerEffectMode.Beat4:
                strobeDivision = 4f;
                break;
            case LayerEffectMode.Manual:
                strobeDivision = 0f;
                break;
        }

        var continuousBeatFloat = GeneratorBeatFloat(bpm > 0.01f ? bpm : 120f);
        var strobePhase = effect.Mode == LayerEffectMode.Manual
            ? 0f
            : Mathf.Repeat(continuousBeatFloat * Mathf.Max(1f, strobeDivision), 1f);
        material.SetFloat("_StrobePhase", strobePhase);
    }

    private bool CanSelectStepSequencerTarget(int hostLayerIndex, int targetIndex)
    {
        if (targetIndex == hostLayerIndex || !IsSourceLayerSlot(targetIndex))
        {
            return false;
        }

        var targetLayer = vjLayers == null || targetIndex < 0 || targetIndex >= vjLayers.Length ? null : vjLayers[targetIndex];
        if (targetLayer == null)
        {
            return false;
        }

        if (IsSourceLayerSlot(hostLayerIndex))
        {
            var hostLayer = vjLayers == null || hostLayerIndex < 0 || hostLayerIndex >= vjLayers.Length ? null : vjLayers[hostLayerIndex];
            if (hostLayer == null)
            {
                return false;
            }

            if (IsOverlaySourceLayer(hostLayer))
            {
                return IsOverlaySourceLayer(targetLayer);
            }

            return IsMainMaterialSourceLayer(targetLayer);
        }

        if (IsMainEffectLayer(hostLayerIndex))
        {
            return IsMainMaterialSourceLayer(targetLayer);
        }

        if (IsAllEffectLayer(hostLayerIndex))
        {
            return true;
        }

        return false;
    }

    private static bool StepSequencerEntryMatchesLayer(StepSequencerEntry entry, int layerIndex)
    {
        return entry != null && entry.Kind == StepSequencerEntryKind.Layer && entry.LayerIndex == layerIndex;
    }

    private static bool StepSequencerEntryMatchesPath(StepSequencerEntry entry, string path)
    {
        return entry != null &&
               entry.Kind == StepSequencerEntryKind.MediaFile &&
               !string.IsNullOrEmpty(entry.Path) &&
               string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase);
    }

    private LayerEffectState SelectedStepSequencerEffect()
    {
        if (vjLayers == null || selectedLayerIndex < 0 || selectedLayerIndex >= vjLayers.Length)
        {
            return null;
        }

        var layer = vjLayers[selectedLayerIndex];
        if (layer == null || layer.Effect == null || layer.Effect.Kind != LayerEffectKind.StepSequencer)
        {
            return null;
        }

        return layer.Effect;
    }

    private int GetStepSequencerQueueIndexForLayer(int hostLayerIndex, int layerIndex)
    {
        var effect = SelectedStepSequencerEffect();
        if (effect == null || hostLayerIndex != selectedLayerIndex || effect.StepSequenceQueue == null)
        {
            return -1;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            if (StepSequencerEntryMatchesLayer(effect.StepSequenceQueue[i], layerIndex))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetStepSequencerQueueIndexForMediaPath(int hostLayerIndex, string path)
    {
        var effect = SelectedStepSequencerEffect();
        if (effect == null || hostLayerIndex != selectedLayerIndex || effect.StepSequenceQueue == null || string.IsNullOrEmpty(path))
        {
            return -1;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            if (StepSequencerEntryMatchesPath(effect.StepSequenceQueue[i], path))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsStepSequencerShiftAssignMode()
    {
        var evt = Event.current;
        return evt != null && evt.shift && SelectedStepSequencerEffect() != null;
    }

    private void ToggleStepSequencerLayerQueueEntry(int hostLayerIndex, int targetLayerIndex)
    {
        if (!CanSelectStepSequencerTarget(hostLayerIndex, targetLayerIndex))
        {
            return;
        }

        var effect = SelectedStepSequencerEffect();
        if (effect == null || hostLayerIndex != selectedLayerIndex)
        {
            return;
        }

        if (effect.StepSequenceQueue == null)
        {
            effect.StepSequenceQueue = new List<StepSequencerEntry>();
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            if (!StepSequencerEntryMatchesLayer(effect.StepSequenceQueue[i], targetLayerIndex))
            {
                continue;
            }

            effect.StepSequenceQueue.RemoveAt(i);
            SyncStepSequencerMediaSlots(hostLayerIndex, effect);
            return;
        }

        if (effect.StepSequenceQueue.Count >= StepSequencerMaxMediaLayers)
        {
            AddLog("Step Sequencer queue is limited to 4 items.");
            return;
        }

        effect.StepSequenceQueue.Add(new StepSequencerEntry
        {
            Kind = StepSequencerEntryKind.Layer,
            LayerIndex = targetLayerIndex
        });
        SyncStepSequencerMediaSlots(hostLayerIndex, effect);
    }

    private void ToggleStepSequencerMediaQueueEntry(int hostLayerIndex, string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        var effect = SelectedStepSequencerEffect();
        if (effect == null || hostLayerIndex != selectedLayerIndex)
        {
            return;
        }

        if (effect.StepSequenceQueue == null)
        {
            effect.StepSequenceQueue = new List<StepSequencerEntry>();
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            if (!StepSequencerEntryMatchesPath(effect.StepSequenceQueue[i], path))
            {
                continue;
            }

            effect.StepSequenceQueue.RemoveAt(i);
            SyncStepSequencerMediaSlots(hostLayerIndex, effect);
            return;
        }

        if (effect.StepSequenceQueue.Count >= StepSequencerMaxMediaLayers)
        {
            AddLog("Step Sequencer queue is limited to 4 items.");
            return;
        }

        effect.StepSequenceQueue.Add(new StepSequencerEntry
        {
            Kind = StepSequencerEntryKind.MediaFile,
            Path = path
        });
        SyncStepSequencerMediaSlots(hostLayerIndex, effect);
    }

    private List<StepSequencerEntry> CollectValidStepSequencerQueue(LayerEffectState effect, int hostLayerIndex)
    {
        var result = new List<StepSequencerEntry>();
        if (effect == null || effect.StepSequenceQueue == null)
        {
            return result;
        }

        for (var i = 0; i < effect.StepSequenceQueue.Count; i++)
        {
            if (result.Count >= StepSequencerMaxMediaLayers)
            {
                break;
            }

            var entry = effect.StepSequenceQueue[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.Kind == StepSequencerEntryKind.Layer)
            {
                if (CanSelectStepSequencerTarget(hostLayerIndex, entry.LayerIndex))
                {
                    result.Add(entry);
                }
                continue;
            }

            if (!string.IsNullOrEmpty(entry.Path) && File.Exists(entry.Path))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private void ClearStepSequencerMediaState(LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        if (effect.ActiveStepImageOwnsTexture && effect.ActiveStepImageTexture != null)
        {
            Destroy(effect.ActiveStepImageTexture);
        }

        effect.ActiveStepImageTexture = null;
        effect.ActiveStepImageOwnsTexture = false;
        effect.ActiveStepMediaPath = null;
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
            if (layer == null)
            {
                continue;
            }

            StopLayerVideoPlayback(layer);
            SetLayerStaticTexture(layer, null, false);
            if (layer.Texture != null)
            {
                Destroy(layer.Texture);
            }
            if (layer.Player != null)
            {
                Destroy(layer.Player.gameObject);
            }
            effect.StepSequenceMediaLayers[i] = null;
        }
    }

    private void EnsureStepSequencerMediaSlots(int hostLayerIndex, LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        if (effect.StepSequenceMediaLayers == null || effect.StepSequenceMediaLayers.Length != StepSequencerMaxMediaLayers)
        {
            effect.StepSequenceMediaLayers = new LayerState[StepSequencerMaxMediaLayers];
        }

        var hostLabel = hostLayerIndex >= 0 ? LayerSlotLabel(hostLayerIndex) : "Step Seq";
        for (var i = 0; i < effect.StepSequenceMediaLayers.Length; i++)
        {
            if (effect.StepSequenceMediaLayers[i] != null)
            {
                continue;
            }

            effect.StepSequenceMediaLayers[i] = CreateStepSequencerMediaSlotState(hostLabel + " Step Media " + (i + 1).ToString(CultureInfo.InvariantCulture), outputWidth, outputHeight);
        }
    }

    private void ClearStepSequencerMediaSlot(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        StopLayerVideoPlayback(layer);
        SetLayerStaticTexture(layer, null, false);
        layer.Path = null;
        layer.SourceKind = LayerSourceKind.None;
        layer.SourceName = null;
        layer.VideoStatus = null;
        layer.DetectedVideoCodec = null;
        layer.VideoLoadToken++;
    }

    private void SyncStepSequencerMediaSlots(int hostLayerIndex, LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        EnsureStepSequencerMediaSlots(hostLayerIndex, effect);
        var mediaEntries = new List<StepSequencerEntry>();
        if (effect.StepSequenceQueue != null)
        {
            for (var i = 0; i < effect.StepSequenceQueue.Count && i < StepSequencerMaxMediaLayers; i++)
            {
                var entry = effect.StepSequenceQueue[i];
                if (entry == null || entry.Kind != StepSequencerEntryKind.MediaFile || string.IsNullOrEmpty(entry.Path) || !File.Exists(entry.Path))
                {
                    continue;
                }

                mediaEntries.Add(entry);
            }
        }

        for (var i = 0; i < StepSequencerMaxMediaLayers; i++)
        {
            var slot = effect.StepSequenceMediaLayers[i];
            if (slot == null)
            {
                continue;
            }

            if (i >= mediaEntries.Count)
            {
                ClearStepSequencerMediaSlot(slot);
                continue;
            }

            var path = mediaEntries[i].Path;
            if (string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            var logLabel = hostLayerIndex >= 0
                ? LayerSlotLabel(hostLayerIndex) + " Step " + (i + 1).ToString(CultureInfo.InvariantCulture)
                : "Step " + (i + 1).ToString(CultureInfo.InvariantCulture);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                LoadImageIntoLayerState(slot, path, LayerSourceOrigin.FileBrowser, Path.GetFileName(path), false, logLabel);
            }
            else
            {
                LoadVideoIntoLayerState(slot, path, LayerSourceOrigin.FileBrowser, Path.GetFileName(path), false, logLabel);
            }
        }
    }

    private int GetStepSequencerMediaSlotIndex(LayerEffectState effect, string path)
    {
        if (effect == null || effect.StepSequenceQueue == null || string.IsNullOrEmpty(path))
        {
            return -1;
        }

        var mediaIndex = 0;
        for (var i = 0; i < effect.StepSequenceQueue.Count && i < StepSequencerMaxMediaLayers; i++)
        {
            var entry = effect.StepSequenceQueue[i];
            if (entry == null || entry.Kind != StepSequencerEntryKind.MediaFile)
            {
                continue;
            }

            if (StepSequencerEntryMatchesPath(entry, path))
            {
                return mediaIndex < StepSequencerMaxMediaLayers ? mediaIndex : -1;
            }

            mediaIndex++;
        }

        return -1;
    }

    private Texture ResolveStepSequencerMediaTexture(LayerEffectState effect, string path)
    {
        if (effect == null || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        var slotIndex = GetStepSequencerMediaSlotIndex(effect, path);
        if (slotIndex < 0 || effect.StepSequenceMediaLayers == null || slotIndex >= effect.StepSequenceMediaLayers.Length)
        {
            return BrowserPreviewTexture(path);
        }

        var slot = effect.StepSequenceMediaLayers[slotIndex];
        if (slot == null)
        {
            return BrowserPreviewTexture(path);
        }

        if (slot.StaticTexture != null)
        {
            return slot.StaticTexture;
        }

        if (slot.UsesKlakHapVideo && slot.KlakHapPlayer != null)
        {
            return slot.Texture ?? BrowserPreviewTexture(path);
        }

        if (slot.Player != null && (slot.Player.isPrepared || slot.Player.isPlaying || slot.Player.texture != null))
        {
            slot.VideoStatus = null;
            return slot.Texture;
        }

        return BrowserPreviewTexture(path);
    }

    private static void SetExclusiveEffectTarget(LayerEffectState effect, int hostLayerIndex, int targetIndex)
    {
        if (effect == null || effect.TargetLayers == null)
        {
            return;
        }

        var valid = targetIndex >= 0 && targetIndex < effect.TargetLayers.Length && targetIndex != hostLayerIndex;
        for (var i = 0; i < effect.TargetLayers.Length; i++)
        {
            effect.TargetLayers[i] = valid && i == targetIndex;
        }
    }

    private static int CountEnabledTargets(LayerEffectState effect, int hostLayerIndex)
    {
        if (effect == null || effect.TargetLayers == null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < effect.TargetLayers.Length; i++)
        {
            if (i != hostLayerIndex && effect.TargetLayers[i])
            {
                count++;
            }
        }
        return count;
    }

    private List<int> CollectEffectTargetIndices(LayerEffectState effect, int hostLayerIndex, int maxCount)
    {
        var result = new List<int>();
        if (effect == null || effect.TargetLayers == null)
        {
            return result;
        }

        for (var i = 0; i < effect.TargetLayers.Length; i++)
        {
            if (i == hostLayerIndex || !effect.TargetLayers[i])
            {
                continue;
            }

            if (!CanSelectStepSequencerTarget(hostLayerIndex, i))
            {
                continue;
            }

            result.Add(i);
            if (maxCount > 0 && result.Count >= maxCount)
            {
                break;
            }
        }

        return result;
    }

    private static bool LayerHasAttachedEffect(LayerState layer)
    {
        return layer != null && layer.Effect != null && layer.Effect.HasEffect;
    }

    private static Color LayerCellBaseColor(LayerState layer)
    {
        if (LayerHasAttachedEffect(layer))
        {
            return new Color(0.82f, 0.18f, 0.18f, 1f);
        }
        if (layer != null && layer.SourceOrigin == LayerSourceOrigin.SourcePanel)
        {
            return new Color(0.18f, 0.42f, 0.88f, 1f);
        }
        if (layer != null && layer.SourceOrigin == LayerSourceOrigin.FileBrowser)
        {
            return new Color(0.88f, 0.74f, 0.16f, 1f);
        }
        return new Color(0.16f, 0.18f, 0.2f, 1f);
    }

    private void AttachEffectToLayer(int layerIndex, LayerEffectKind kind)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || kind == LayerEffectKind.None)
        {
            return;
        }

        var isStepSequencerSource = kind == LayerEffectKind.StepSequencer && IsSourceLayerSlot(layerIndex);
        if (!isStepSequencerSource && !IsEffectLayerSlot(layerIndex))
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
        else if (layer.Effect.Kind == LayerEffectKind.StepSequencer && kind != LayerEffectKind.StepSequencer)
        {
            DestroyStepSequencerMediaSlots(layer.Effect);
            ClearStepSequencerMediaState(layer.Effect);
        }

        StopAvproLayer(layer);
        if (layer.Player != null)
        {
            layer.Player.Stop();
            layer.Player.url = "";
        }
        SetLayerStaticTexture(layer, null, false);
        layer.Path = null;
        layer.SourceKind = LayerSourceKind.Effect;
        layer.Effect.Kind = kind;
        layer.Effect.Enabled = true;
        layer.Effect.Intensity = 1f;
        layer.Effect.RgbMode = LayerRgbEffectMode.RgbInvert;
        layer.Effect.Mode = kind == LayerEffectKind.Blur ? LayerEffectMode.Horizontal :
            kind == LayerEffectKind.Strobe ? LayerEffectMode.Beat4 : LayerEffectMode.Horizontal;
        layer.Effect.StepSequenceLength = 2;
        layer.Effect.ManualRateHz = 8f;
        layer.Effect.ManualRateInput = "8.0";
        DestroyStepSequencerMediaSlots(layer.Effect);
        ClearStepSequencerMediaState(layer.Effect);
        if (layer.Effect.StepSequenceQueue == null)
        {
            layer.Effect.StepSequenceQueue = new List<StepSequencerEntry>();
        }
        else
        {
            layer.Effect.StepSequenceQueue.Clear();
        }
        if (layer.Effect.TargetLayers == null || layer.Effect.TargetLayers.Length != VjLayerCount)
        {
            layer.Effect.TargetLayers = new bool[VjLayerCount];
        }
        for (var i = 0; i < layer.Effect.TargetLayers.Length; i++)
        {
            layer.Effect.TargetLayers[i] = false;
        }
        layer.SourceName = EffectKindLabel(kind);
        layer.SourceOrigin = LayerSourceOrigin.SourcePanel;
        layer.EffectRenderedOutput = null;
        layer.EffectRenderedFrame = -1;
        layer.EffectRenderInProgress = false;
        if (isStepSequencerSource)
        {
            layer.SourceGroupMode = layer.SourceGroupMode == LayerSourceGroupMode.Overlay ? LayerSourceGroupMode.Overlay : LayerSourceGroupMode.Main;
        }
        SetTextSourceVisible(layer, false);
    }

    private static void CycleLayerEffectMode(LayerEffectState effect)
    {
        if (effect == null)
        {
            return;
        }

        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                switch (effect.RgbMode)
                {
                    case LayerRgbEffectMode.RedOnly:
                        effect.RgbMode = LayerRgbEffectMode.GreenOnly;
                        break;
                    case LayerRgbEffectMode.GreenOnly:
                        effect.RgbMode = LayerRgbEffectMode.BlueOnly;
                        break;
                    case LayerRgbEffectMode.BlueOnly:
                        effect.RgbMode = LayerRgbEffectMode.RgbInvert;
                        break;
                    case LayerRgbEffectMode.RgbInvert:
                        effect.RgbMode = LayerRgbEffectMode.EdgeExtract;
                        break;
                    case LayerRgbEffectMode.EdgeExtract:
                        effect.RgbMode = LayerRgbEffectMode.Monochrome;
                        break;
                    default:
                        effect.RgbMode = LayerRgbEffectMode.RedOnly;
                        break;
                }
                break;
            case LayerEffectKind.Blur:
                switch (effect.Mode)
                {
                    case LayerEffectMode.Horizontal:
                        effect.Mode = LayerEffectMode.Vertical;
                        break;
                    case LayerEffectMode.Vertical:
                        effect.Mode = LayerEffectMode.Alternate;
                        break;
                    case LayerEffectMode.Alternate:
                        effect.Mode = LayerEffectMode.Zoom;
                        break;
                    default:
                        effect.Mode = LayerEffectMode.Horizontal;
                        break;
                }
                break;
            case LayerEffectKind.QuadSplit:
                effect.Intensity = effect.Intensity >= 0.99f ? 0.5f : 1f;
                break;
            case LayerEffectKind.StepSequencer:
                effect.StepSequenceLength = effect.StepSequenceLength == 4 ? 2 : 4;
                break;
            case LayerEffectKind.Strobe:
                switch (effect.Mode)
                {
                    case LayerEffectMode.Beat1:
                        effect.Mode = LayerEffectMode.Beat2;
                        break;
                    case LayerEffectMode.Beat2:
                        effect.Mode = LayerEffectMode.Beat4;
                        break;
                    case LayerEffectMode.Beat4:
                        effect.Mode = LayerEffectMode.Manual;
                        break;
                    default:
                        effect.Mode = LayerEffectMode.Beat1;
                        break;
                }
                break;
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
            if (layer != null && layer.SourceKind == LayerSourceKind.VideoFile)
            {
                ResyncVideoLayerToCurrentBeat(layer);
            }
        }
    }

    private static void ApplyBlendModeToMaterial(Material material, LayerBlendMode mode)
    {
        if (material == null)
        {
            return;
        }

        var texture = material.mainTexture;
        Shader shader;
        if (mode == LayerBlendMode.Add50)
        {
            shader = Shader.Find("BeatLink/LayerAdd50");
        }
        else if (mode == LayerBlendMode.Add100)
        {
            shader = Shader.Find("BeatLink/LayerAdd");
        }
        else
        {
            shader = Shader.Find("BeatLink/LayerAlpha");
        }
        if (shader == null)
        {
            shader = mode == LayerBlendMode.Add100 ? Shader.Find("Legacy Shaders/Particles/Additive") : Shader.Find("Unlit/Transparent");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }
        material.mainTexture = texture;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        switch (mode)
        {
            case LayerBlendMode.Add100:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                break;
            case LayerBlendMode.Add50:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                break;
            default:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                break;
        }
    }

    private void RefreshOutputDevices()
    {
        if (!outputDevicesDirty)
        {
            return;
        }

        outputDevicesDirty = false;
        var selectedVideoId = selectedVideoOutputIndex >= 0 && selectedVideoOutputIndex < videoOutputDevices.Count ? videoOutputDevices[selectedVideoOutputIndex].Id : null;
        var selectedAudioId = selectedAudioOutputIndex >= 0 && selectedAudioOutputIndex < audioOutputDevices.Count ? audioOutputDevices[selectedAudioOutputIndex].Id : null;

        videoOutputDevices.Clear();
        audioOutputDevices.Clear();
        AddVideoOutputDevices(videoOutputDevices);
        AddAudioOutputDevices(audioOutputDevices);
        selectedVideoOutputIndex = FindOutputOptionIndex(videoOutputDevices, selectedVideoId);
        selectedAudioOutputIndex = FindOutputOptionIndex(audioOutputDevices, selectedAudioId);
    }

    private static int FindOutputOptionIndex(List<OutputDeviceOption> options, string id)
    {
        if (options == null || options.Count == 0)
        {
            return 0;
        }
        if (!string.IsNullOrEmpty(id))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }
        return 0;
    }

    private static void AddVideoOutputDevices(List<OutputDeviceOption> target)
    {
        target.Add(new OutputDeviceOption { Id = "unselected", Label = "(not selected)", Index = -1 });
        var displays = Display.displays;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < displays.Length; i++)
        {
            var id = "display-" + i.ToString(CultureInfo.InvariantCulture);
            target.Add(new OutputDeviceOption
            {
                Id = id,
                Label = "Display " + (i + 1).ToString(CultureInfo.InvariantCulture) + "  Program Fullscreen  " +
                        displays[i].systemWidth.ToString(CultureInfo.InvariantCulture) + "x" +
                        displays[i].systemHeight.ToString(CultureInfo.InvariantCulture),
                Index = i,
                Width = displays[i].systemWidth,
                Height = displays[i].systemHeight
            });
            seenIds.Add(id);
        }

        var monitors = EnumerateWindowsMonitors();
        for (var i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            if (monitor == null || monitor.Primary)
            {
                continue;
            }

            var id = "monitor-" + monitor.DeviceName;
            if (!seenIds.Add(id))
            {
                continue;
            }

            target.Add(new OutputDeviceOption
            {
                Id = id,
                Label = monitor.DeviceName + "  Program Fullscreen  " +
                        monitor.Width.ToString(CultureInfo.InvariantCulture) + "x" +
                        monitor.Height.ToString(CultureInfo.InvariantCulture),
                Index = i + 1,
                X = monitor.X,
                Y = monitor.Y,
                Width = monitor.Width,
                Height = monitor.Height
            });
        }
        if (target.Count == 1)
        {
            target.Add(new OutputDeviceOption
            {
                Id = "no-secondary",
                Label = "No secondary display detected",
                Index = -1
            });
        }
    }

    private static void AddAudioOutputDevices(List<OutputDeviceOption> target)
    {
        target.Add(new OutputDeviceOption { Id = "default", Label = "Windows Default Audio Output", Index = -1 });
        var endpoints = EnumerateWindowsAudioRenderEndpoints();
        for (var i = 0; i < endpoints.Count; i++)
        {
            target.Add(new OutputDeviceOption
            {
                Id = endpoints[i].Id,
                Label = endpoints[i].Label,
                Index = i
            });
        }
        if (endpoints.Count > 0)
        {
            return;
        }

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_SoundDevice | Where-Object { $_.Status -eq 'OK' } | ForEach-Object { $_.Name }\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                if (process == null)
                {
                    return;
                }
                if (!process.WaitForExit(2500))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                    return;
                }

                var output = process.StandardOutput.ReadToEnd();
                var lines = output.Replace("\r", "").Split('\n');
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var index = 0;
                for (var i = 0; i < lines.Length; i++)
                {
                    var name = lines[i].Trim();
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    {
                        continue;
                    }
                    target.Add(new OutputDeviceOption
                    {
                        Id = "audio-" + index.ToString(CultureInfo.InvariantCulture) + "-" + StableTextHash(name).ToString("x8", CultureInfo.InvariantCulture),
                        Label = name,
                        Index = index++
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Audio output device enumeration failed: " + ex.Message);
        }
    }

    private void ApplyVideoOutputSelection()
    {
        if (selectedVideoOutputIndex < 0 || selectedVideoOutputIndex >= videoOutputDevices.Count)
        {
            DisableProgramOutput("Program output not selected.");
            return;
        }

        var option = videoOutputDevices[selectedVideoOutputIndex];
        if (option.Index <= 0)
        {
            DisableProgramOutput("Program output not selected.");
            return;
        }
        if (secondDisplayUiEnabled && option.Index == 1)
        {
            DisableProgramOutput("Display 2 is reserved for Second Display UI.");
            return;
        }

        if (option.Index < Display.displays.Length)
        {
            ActivateAvailableUnityDisplays();
            activeProgramDisplay = option.Index;
            if (programOutputCamera != null)
            {
                programOutputCamera.targetDisplay = option.Index;
            }
            if (programOutputRoot != null)
            {
                programOutputRoot.SetActive(true);
            }
            outputStatus = "Program output fullscreen: " + option.Label;
            return;
        }

        activeProgramDisplay = -1;
        if (programOutputRoot != null)
        {
            programOutputRoot.SetActive(false);
        }
        outputStatus = "Windows sees " + option.Label + ", but Unity exposed only " + Display.displays.Length.ToString(CultureInfo.InvariantCulture) + " display. Restart the app after the monitor is connected.";
    }

    private void ActivateAvailableUnityDisplays()
    {
        if (unityDisplaysActivated)
        {
            return;
        }

        try
        {
            var displays = Display.displays;
            for (var i = 1; i < displays.Length; i++)
            {
                displays[i].Activate();
            }
            unityDisplaysActivated = true;
        }
        catch (Exception ex)
        {
            outputStatus = "Unity display activation failed: " + ex.Message;
        }
    }

    private void ApplyOutputResolutionSelection()
    {
        if (selectedOutputResolutionIndex < 0 || selectedOutputResolutionIndex >= outputResolutionOptions.Count)
        {
            outputStatus = "Output resolution selection is invalid.";
            return;
        }

        var option = outputResolutionOptions[selectedOutputResolutionIndex];
        if (option.Width <= 0 || option.Height <= 0)
        {
            outputStatus = "Output resolution selection is invalid.";
            return;
        }

        if (outputWidth == option.Width && outputHeight == option.Height)
        {
            outputStatus = "Output resolution unchanged: " + option.Label;
            SaveOutputResolutionPreference();
            return;
        }

        outputWidth = option.Width;
        outputHeight = option.Height;
        ResizeVideoLayerRenderTargets(outputWidth, outputHeight);
        DestroyCompositeRenderTextures();
        EnsureCompositeRenderTextures();
        SaveOutputResolutionPreference();
        outputStatus = "Output resolution applied: " + option.Label;
    }

    private void InitializeOutputResolutionOptions()
    {
        if (outputResolutionOptions.Count > 0)
        {
            return;
        }

        outputResolutionOptions.Add(new OutputDeviceOption { Id = "1280x720", Label = "1280x720  HD", Width = 1280, Height = 720 });
        outputResolutionOptions.Add(new OutputDeviceOption { Id = "1920x1080", Label = "1920x1080  Full HD", Width = 1920, Height = 1080 });
        outputResolutionOptions.Add(new OutputDeviceOption { Id = "2560x1440", Label = "2560x1440  QHD", Width = 2560, Height = 1440 });
        outputResolutionOptions.Add(new OutputDeviceOption { Id = "3840x2160", Label = "3840x2160  4K", Width = 3840, Height = 2160 });
        selectedOutputResolutionIndex = FindResolutionOptionIndex(1920, 1080);
    }

    private int FindResolutionOptionIndex(int width, int height)
    {
        for (var i = 0; i < outputResolutionOptions.Count; i++)
        {
            var option = outputResolutionOptions[i];
            if (option != null && option.Width == width && option.Height == height)
            {
                return i;
            }
        }
        return 0;
    }

    private void LoadOutputResolutionPreference()
    {
        var width = PlayerPrefs.GetInt(OutputWidthPrefKey, 1920);
        var height = PlayerPrefs.GetInt(OutputHeightPrefKey, 1080);
        selectedOutputResolutionIndex = FindResolutionOptionIndex(width, height);
        if (selectedOutputResolutionIndex >= 0 && selectedOutputResolutionIndex < outputResolutionOptions.Count)
        {
            outputWidth = outputResolutionOptions[selectedOutputResolutionIndex].Width;
            outputHeight = outputResolutionOptions[selectedOutputResolutionIndex].Height;
        }
        else
        {
            outputWidth = 1920;
            outputHeight = 1080;
            selectedOutputResolutionIndex = FindResolutionOptionIndex(outputWidth, outputHeight);
        }
    }

    private void SaveOutputResolutionPreference()
    {
        PlayerPrefs.SetInt(OutputWidthPrefKey, outputWidth);
        PlayerPrefs.SetInt(OutputHeightPrefKey, outputHeight);
        PlayerPrefs.Save();
    }

    private void ResizeVideoLayerRenderTargets(int width, int height)
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            ResizeLayerRenderTargets(vjLayers[i], width, height);
        }
    }

    private void ResizeLayerRenderTargets(LayerState layer, int width, int height)
    {
        if (layer == null)
        {
            return;
        }

        var newTexture = CreateManagedRenderTexture((string.IsNullOrEmpty(layer.SourceName) ? "Layer" : layer.SourceName) + " Source", width, height, 0);
        var newEffectTexture = CreateManagedRenderTexture((string.IsNullOrEmpty(layer.SourceName) ? "Layer" : layer.SourceName) + " Effect", width, height, 0);
        var newEffectScratchTexture = CreateManagedRenderTexture((string.IsNullOrEmpty(layer.SourceName) ? "Layer" : layer.SourceName) + " Effect Scratch", width, height, 0);

        var oldTexture = layer.Texture;
        var oldEffectTexture = layer.EffectTexture;
        var oldEffectScratchTexture = layer.EffectScratchTexture;

        layer.Texture = newTexture;
        layer.EffectTexture = newEffectTexture;
        layer.EffectScratchTexture = newEffectScratchTexture;
        layer.EffectRenderedOutput = null;
        layer.EffectRenderedFrame = -1;

        if (layer.Player != null)
        {
            layer.Player.targetTexture = newTexture;
        }
        if (layer.TextSource != null && layer.TextSource.Camera != null)
        {
            layer.TextSource.Camera.targetTexture = newTexture;
        }
        if (layer.Generator != null)
        {
            var oldGeneratorTexture = layer.Generator.Texture;
            layer.Generator.Texture = CreateManagedRenderTexture((string.IsNullOrEmpty(layer.SourceName) ? "Layer" : layer.SourceName) + " Generator", width, height, 24);
            if (layer.Generator.Camera != null)
            {
                layer.Generator.Camera.targetTexture = layer.Generator.Texture;
            }
            if (oldGeneratorTexture != null)
            {
                oldGeneratorTexture.Release();
                Destroy(oldGeneratorTexture);
            }
        }

        UpdateTextLayerTexture(layer);

        if (oldTexture != null)
        {
            oldTexture.Release();
            Destroy(oldTexture);
        }
        if (oldEffectTexture != null)
        {
            oldEffectTexture.Release();
            Destroy(oldEffectTexture);
        }
        if (oldEffectScratchTexture != null)
        {
            oldEffectScratchTexture.Release();
            Destroy(oldEffectScratchTexture);
        }
    }

    private void ApplyAudioOutputSelection()
    {
        if (selectedAudioOutputIndex < 0 || selectedAudioOutputIndex >= audioOutputDevices.Count)
        {
            outputStatus = "Audio output: no device selected.";
            return;
        }

        var option = audioOutputDevices[selectedAudioOutputIndex];
        EnableDirectAudioForVideoPlayers();
        if (string.Equals(option.Id, "default", StringComparison.Ordinal))
        {
            outputStatus = "Audio output: using Windows default output.";
            return;
        }

        if (SetWindowsDefaultAudioEndpoint(option.Id))
        {
            outputStatus = "Audio output selected: " + option.Label + ". Windows default output was changed for Unity Direct audio.";
        }
        else
        {
            outputStatus = "Audio output selection failed: " + option.Label;
        }
    }

    private void EnableDirectAudioForVideoPlayers()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer != null && layer.Player != null)
            {
                layer.Player.audioOutputMode = VideoAudioOutputMode.Direct;
                ApplyLayerAudioOutputState(layer);
            }
        }
    }

    private static void ApplyLayerAudioOutputState(LayerState layer)
    {
        if (layer == null || layer.Player == null)
        {
            return;
        }

        try
        {
            layer.Player.audioOutputMode = VideoAudioOutputMode.Direct;
            var trackCount = Math.Max(1, (int)layer.Player.audioTrackCount);
            for (ushort track = 0; track < trackCount; track++)
            {
                layer.Player.EnableAudioTrack(track, true);
                layer.Player.SetDirectAudioMute(track, !layer.AudioOutputEnabled);
                layer.Player.SetDirectAudioVolume(track, layer.AudioOutputEnabled ? 1f : 0f);
            }
        }
        catch
        {
        }
    }

    private static List<MonitorOutputInfo> EnumerateWindowsMonitors()
    {
        var monitors = new List<MonitorOutputInfo>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect rect, IntPtr data) =>
            {
                var info = new NativeMonitorInfoEx();
                info.Size = Marshal.SizeOf(typeof(NativeMonitorInfoEx));
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    monitors.Add(new MonitorOutputInfo
                    {
                        DeviceName = string.IsNullOrEmpty(info.DeviceName) ? "monitor-" + monitors.Count.ToString(CultureInfo.InvariantCulture) : info.DeviceName,
                        X = info.Monitor.Left,
                        Y = info.Monitor.Top,
                        Width = Math.Max(1, info.Monitor.Right - info.Monitor.Left),
                        Height = Math.Max(1, info.Monitor.Bottom - info.Monitor.Top),
                        Primary = (info.Flags & 1) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Monitor enumeration failed: " + ex.Message);
        }
        return monitors;
    }

    private static bool TryMoveUnityWindow(int x, int y, int width, int height)
    {
        try
        {
            var handle = GetActiveWindow();
            if (handle == IntPtr.Zero)
            {
                return false;
            }
            return SetWindowPos(handle, IntPtr.Zero, x, y, Math.Max(1, width), Math.Max(1, height), 0x0004 | 0x0040);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Window move failed: " + ex.Message);
            return false;
        }
    }

    private static List<OutputDeviceOption> EnumerateWindowsAudioRenderEndpoints()
    {
        var result = new List<OutputDeviceOption>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("bcde0395-e52f-467c-8e3d-c4579291692e")));
            IMMDeviceCollection collection;
            enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out collection);
            uint count;
            collection.GetCount(out count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice device;
                collection.Item(i, out device);
                string id;
                device.GetId(out id);
                var label = GetAudioEndpointFriendlyName(device);
                result.Add(new OutputDeviceOption
                {
                    Id = id,
                    Label = PreferText(label, "Audio Output " + (i + 1).ToString(CultureInfo.InvariantCulture)),
                    Index = (int)i
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("CoreAudio endpoint enumeration failed: " + ex.Message);
        }
        return result;
    }

    private static string GetAudioEndpointFriendlyName(IMMDevice device)
    {
        if (device == null)
        {
            return null;
        }

        IPropertyStore store = null;
        try
        {
            device.OpenPropertyStore(0, out store);
            var friendlyNameKey = new PropertyKey
            {
                fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                pid = 14
            };
            PropVariant value;
            store.GetValue(ref friendlyNameKey, out value);
            var text = value.ToStringValue();
            PropVariantClear(ref value);
            return text;
        }
        catch
        {
            return null;
        }
    }

    private static bool SetWindowsDefaultAudioEndpoint(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
        {
            return false;
        }

        try
        {
            var policyConfig = (IPolicyConfig)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")));
            policyConfig.SetDefaultEndpoint(endpointId, ERole.Console);
            policyConfig.SetDefaultEndpoint(endpointId, ERole.Multimedia);
            policyConfig.SetDefaultEndpoint(endpointId, ERole.Communications);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("CoreAudio default endpoint change failed: " + ex.Message);
            return false;
        }
    }

    private void DrawLayerSettingsScreen(Rect full)
    {
        DrawLayerSettingsScreenContent(full, false);
    }

    private void DrawLayerSettingsScreenContent(Rect full, bool inWindow)
    {
        var snapshot = Snapshot();
        var margin = 18f;
        var layer = vjLayers != null && selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length ? vjLayers[selectedLayerIndex] : null;
        var left = full.x + margin;
        var top = full.y + margin;

        GUI.Label(new Rect(left, top, 720f, 34f), LayerSlotLabel(selectedLayerIndex) + " Settings", titleStyle);
        if (GUI.Button(new Rect(left + 260f, top + 2f, 104f, 30f), midiEditMode ? "MIDI Edit ON" : "MIDI Edit"))
        {
            ToggleMidiEditMode();
        }
        if (!inWindow)
        {
            GUI.Label(new Rect(full.x + full.width - 360f, top + 7f, 340f, 24f), "R: Return to VJ screen", smallStyle);
        }

        var previewRect = new Rect(left, top + 52f, Mathf.Min(560f, full.width * 0.36f), Mathf.Min(360f, (full.width * 0.36f) * 9f / 16f + 42f));
        GUI.DrawTexture(previewRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(previewRect.x + 12f, previewRect.y + 8f, previewRect.width - 24f, 24f), LayerName(layer), normalStyle);
        var textureRect = new Rect(previewRect.x + 12f, previewRect.y + 40f, previewRect.width - 24f, (previewRect.width - 24f) * 9f / 16f);
        GUI.DrawTexture(textureRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
        var texture = LayerPreviewTexture(selectedLayerIndex, layer);
        if (texture != null)
        {
            GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit, false);
        }

        if (layer != null && layer.SourceKind == LayerSourceKind.Generator3D && layer.Generator != null)
        {
            var presetRect = new Rect(previewRect.x, previewRect.yMax + 12f, previewRect.width, full.height - previewRect.yMax - margin - 12f);
            DrawGeneratorPresetPanel(presetRect, layer, snapshot);
        }

        var panel = new Rect(previewRect.xMax + 18f, top + 52f, full.x + full.width - previewRect.xMax - 18f - margin, full.height - margin * 2f - 52f);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 14f, panel.width - 28f, panel.height - 28f));
        layerSettingsScroll = GUILayout.BeginScrollView(layerSettingsScroll);

        if (layer == null)
        {
            GUILayout.Label("No layer selected.", normalStyle);
        }
        else
        {
            DrawLayerCommonControls(layer);
            GUILayout.Space(10f);
            if (LayerHasAttachedEffect(layer))
            {
                DrawLayerEffectControls(layer);
                GUILayout.Space(10f);
            }
            if (layer.SourceKind == LayerSourceKind.Generator3D)
            {
                DrawGeneratorDetailControls(layer.Generator, snapshot);
            }
            else if (layer.SourceKind == LayerSourceKind.VideoFile)
            {
                DrawVideoFileDetailControls(layer, snapshot);
            }
            else if (layer.SourceKind == LayerSourceKind.Text)
            {
                DrawTextDetailControls(layer);
            }
            else if (layer.SourceKind == LayerSourceKind.YouTube)
            {
                DrawYoutubeDetailControls(layer, snapshot);
            }
            else
            {
                GUILayout.Label("No detailed settings for this source.", normalStyle);
                GUILayout.Label("Select a YouTube or 3D Object source first, then press E again.", smallStyle);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawTextDetailControls(LayerState layer)
    {
        if (layer == null)
        {
            return;
        }

        GUILayout.Label("Text Detail", normalStyle);
        GUILayout.Label("Text source edits are here. Source panel only attaches a text layer.", smallStyle);
        GUILayout.Space(6f);
        layer.TextInput = GUILayout.TextArea(layer.TextInput ?? layer.TextContent ?? "TEXT", GUILayout.MinHeight(120f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Font Size", GUILayout.Width(64f));
        layer.TextFontSizeInput = GUILayout.TextField(layer.TextFontSizeInput ?? layer.TextFontSize.ToString(CultureInfo.InvariantCulture), GUILayout.Width(80f));
        if (GUILayout.Button("Apply", GUILayout.Width(80f), GUILayout.Height(26f)))
        {
            layer.TextContent = string.IsNullOrEmpty(layer.TextInput) ? "TEXT" : layer.TextInput;
            var fontSize = layer.TextFontSize;
            if (!int.TryParse(layer.TextFontSizeInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize))
            {
                fontSize = layer.TextFontSize;
            }
            layer.TextFontSize = Mathf.Clamp(fontSize, 12, 256);
            layer.TextFontSizeInput = layer.TextFontSize.ToString(CultureInfo.InvariantCulture);
            UpdateTextLayerTexture(layer);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);
        GUILayout.Label("Font", normalStyle);
        EnsureTextFontOptions();
        if (string.IsNullOrEmpty(layer.TextFontName))
        {
            layer.TextFontName = DefaultTextFontName();
            UpdateTextLayerTexture(layer);
        }
        GUILayout.Label("Current: " + (string.IsNullOrEmpty(layer.TextFontName) ? "Built-in" : layer.TextFontName), smallStyle);
        GUILayout.BeginHorizontal();
        var buttonCount = 0;
        for (var i = 0; i < textFontOptions.Count; i++)
        {
            var fontName = textFontOptions[i];
            if (GUILayout.Button(string.Equals(layer.TextFontName, fontName, StringComparison.OrdinalIgnoreCase) ? "> " + fontName : fontName, GUILayout.Height(26f)))
            {
                layer.TextFontName = fontName;
                UpdateTextLayerTexture(layer);
            }
            buttonCount++;
            if (buttonCount >= 3 && i < textFontOptions.Count - 1)
            {
                buttonCount = 0;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawYoutubeDetailControls(LayerState layer, StateSnapshot snapshot)
    {
        if (layer == null)
        {
            return;
        }
        if (layer.YouTube == null)
        {
            layer.YouTube = new YoutubeState { SpeedInput = "1.00", TimeInput = "0.0" };
        }

        var youtube = layer.YouTube;
        GUILayout.Label("YouTube Detail", normalStyle);
        GUILayout.Label(string.IsNullOrEmpty(youtube.Title) ? "No video loaded." : youtube.Title, titleStyle);
        if (!string.IsNullOrEmpty(youtube.Author))
        {
            GUILayout.Label(youtube.Author, smallStyle);
        }
        GUILayout.Label("Resolution " + DescribeLayerSourceResolution(layer), smallStyle);
        GUILayout.Label("Mode: " + YoutubePlaybackModeLabel(youtube.PlaybackMode), smallStyle);
        if (youtube.Resolving)
        {
            GUILayout.Label("Resolving stream URL...", normalStyle);
        }
        if (!string.IsNullOrEmpty(youtube.Error))
        {
            GUILayout.Label("Error: " + youtube.Error, normalStyle);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Playback Source", normalStyle);
        GUILayout.BeginHorizontal();
        DrawYoutubePlaybackModeButton(layer, youtube, YoutubePlaybackMode.UrlCompatible, "URL Compatible");
        DrawYoutubePlaybackModeButton(layer, youtube, YoutubePlaybackMode.UrlBest, "URL Best");
        DrawYoutubePlaybackModeButton(layer, youtube, YoutubePlaybackMode.LocalCache, "Local Cache HQ");
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(layer.Player != null && layer.Player.isPlaying ? "Pause" : "Play", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            if (layer.Player != null)
            {
                if (layer.Player.isPlaying)
                {
                    layer.Player.Pause();
                }
                else
                {
                    layer.Player.Play();
                }
            }
        }
        if (GUILayout.Button("Restart", GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            if (layer.Player != null)
            {
                layer.Player.time = 0;
                layer.Player.Play();
            }
        }
        if (GUILayout.Button("Resolve Again", GUILayout.Width(120f), GUILayout.Height(28f)) && !string.IsNullOrEmpty(youtube.Url))
        {
            youtube.Resolving = true;
            youtube.Error = null;
            StartCoroutine(ResolveYoutubeVideo(selectedLayerIndex, youtube.Url));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Position sec", GUILayout.Width(88f));
        youtube.TimeInput = GUILayout.TextField(youtube.TimeInput ?? "0.0", GUILayout.Width(90f));
        if (GUILayout.Button("Seek", GUILayout.Width(64f)))
        {
            if (layer.Player != null && double.TryParse(youtube.TimeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                layer.Player.time = Math.Max(0.0, seconds);
            }
        }
        GUILayout.Label("Speed", GUILayout.Width(48f));
        youtube.SpeedInput = GUILayout.TextField(youtube.SpeedInput ?? "1.00", GUILayout.Width(72f));
        if (GUILayout.Button("Apply", GUILayout.Width(70f)))
        {
            if (float.TryParse(youtube.SpeedInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
            {
                youtube.PlaybackSpeed = Mathf.Clamp(speed, 0.05f, 4f);
                if (layer.Player != null)
                {
                    layer.Player.playbackSpeed = youtube.PlaybackSpeed;
                }
            }
        }
        GUILayout.EndHorizontal();

        var current = layer.Player == null ? 0.0 : layer.Player.time;
        var length = layer.Player == null ? 0.0 : layer.Player.length;
        GUILayout.Label("Time " + current.ToString("0.0", CultureInfo.InvariantCulture) + " / " +
                        (length > 0.001 ? length.ToString("0.0", CultureInfo.InvariantCulture) : "--") +
                        "   Speed x" + youtube.PlaybackSpeed.ToString("0.00", CultureInfo.InvariantCulture), smallStyle);

        GUILayout.Space(8f);
        DrawYoutubeWaveformControl(layer, GUILayoutUtility.GetRect(1f, 92f, GUILayout.ExpandWidth(true)));

        GUILayout.Space(8f);
        DrawYoutubePlayerWaveformSelector(youtube, snapshot);
        if (GUILayout.Button("Auto Align To Player", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            AutoAlignYoutubeToPlayer(layer, snapshot);
        }
        if (!string.IsNullOrEmpty(youtube.AlignmentStatus))
        {
            GUILayout.Label(youtube.AlignmentStatus, smallStyle);
        }
        DrawYoutubeLinkedPlayerWaveform(youtube, snapshot, GUILayoutUtility.GetRect(1f, 74f, GUILayout.ExpandWidth(true)));
    }

    private void DrawYoutubePlaybackModeButton(LayerState layer, YoutubeState youtube, YoutubePlaybackMode mode, string label)
    {
        if (GUILayout.Button(youtube.PlaybackMode == mode ? "> " + label : label, GUILayout.Height(28f)))
        {
            if (youtube.PlaybackMode != mode)
            {
                youtube.PlaybackMode = mode;
                youtube.Error = null;
                if (!string.IsNullOrEmpty(youtube.Url))
                {
                    youtube.Resolving = true;
                    StartCoroutine(ResolveYoutubeVideo(selectedLayerIndex, youtube.Url));
                }
            }
        }
    }

    private static string YoutubePlaybackModeLabel(YoutubePlaybackMode mode)
    {
        switch (mode)
        {
            case YoutubePlaybackMode.UrlCompatible:
                return "URL Compatible";
            case YoutubePlaybackMode.UrlBest:
                return "URL Best";
            case YoutubePlaybackMode.LocalCache:
                return "Local Cache HQ";
            default:
                return mode.ToString();
        }
    }

    private void DrawLayerCommonControls(LayerState layer)
    {
        GUILayout.Label("Layer Blend", normalStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(layer.BlendMode == LayerBlendMode.Alpha ? "> Alpha" : "Alpha", GUILayout.Height(26f)))
        {
            layer.BlendMode = LayerBlendMode.Alpha;
        }
        if (GUILayout.Button(layer.BlendMode == LayerBlendMode.Add50 ? "> 50Add" : "50Add", GUILayout.Height(26f)))
        {
            layer.BlendMode = LayerBlendMode.Add50;
        }
        if (GUILayout.Button(layer.BlendMode == LayerBlendMode.Add100 ? "> Add" : "Add", GUILayout.Height(26f)))
        {
            layer.BlendMode = LayerBlendMode.Add100;
        }
        if (GUILayout.Button(layer.BlendMode == LayerBlendMode.Mask ? "> Mask" : "Mask", GUILayout.Height(26f)))
        {
            layer.BlendMode = LayerBlendMode.Mask;
        }
        GUILayout.EndHorizontal();

        if (IsSourceLayerSlot(selectedLayerIndex))
        {
            GUILayout.Space(8f);
            GUILayout.Label("Group", normalStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(layer.SourceGroupMode == LayerSourceGroupMode.Main ? "> Main Material" : "Main Material", GUILayout.Height(26f)))
            {
                layer.SourceGroupMode = LayerSourceGroupMode.Main;
            }
            if (GUILayout.Button(layer.SourceGroupMode == LayerSourceGroupMode.Overlay ? "> Overlay" : "Overlay", GUILayout.Height(26f)))
            {
                layer.SourceGroupMode = LayerSourceGroupMode.Overlay;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Audio", normalStyle);
            var supportsVideoAudio = layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube;
            if (supportsVideoAudio)
            {
                var nextAudio = GUILayout.Toggle(layer.AudioOutputEnabled, layer.AudioOutputEnabled ? "Audio Output ON" : "Audio Output OFF", GUILayout.Width(180f));
                if (nextAudio != layer.AudioOutputEnabled)
                {
                    layer.AudioOutputEnabled = nextAudio;
                    ApplyLayerAudioOutputState(layer);
                }
            }
            else if (layer.SourceKind == LayerSourceKind.Capture)
            {
                GUILayout.Label("Capture audio is controlled from Setting > Capture Audio.", smallStyle);
            }
            else
            {
                GUILayout.Label("No audio output control for this source.", smallStyle);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Hue", normalStyle);
            var hueRect = GUILayoutUtility.GetRect(260f, 24f, GUILayout.ExpandWidth(true));
            if (!midiEditMode)
            {
                layer.HueShift = GUI.HorizontalSlider(hueRect, Mathf.Repeat(layer.HueShift, 1f), 0f, 1f);
            }
            else
            {
                DrawStaticHorizontalSlider(hueRect, Mathf.Repeat(layer.HueShift, 1f));
                DrawMidiLearnOverlay(hueRect, MidiBindingAction.LayerHue, selectedLayerIndex, "Hue");
            }
            GUILayout.Label("Hue Shift " + Mathf.RoundToInt(Mathf.Repeat(layer.HueShift, 1f) * 360f).ToString(CultureInfo.InvariantCulture) + "°", smallStyle);

            GUILayout.Space(6f);
            GUILayout.Label("Invert", normalStyle);
            var invertRect = GUILayoutUtility.GetRect(260f, 24f, GUILayout.ExpandWidth(true));
            if (!midiEditMode)
            {
                layer.InvertAmount = GUI.HorizontalSlider(invertRect, Mathf.Clamp01(layer.InvertAmount), 0f, 1f);
            }
            else
            {
                DrawStaticHorizontalSlider(invertRect, Mathf.Clamp01(layer.InvertAmount));
                DrawMidiLearnOverlay(invertRect, MidiBindingAction.LayerInvertAmount, selectedLayerIndex, "Invert");
            }
            GUILayout.Label("Invert " + Mathf.RoundToInt(Mathf.Clamp01(layer.InvertAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%", smallStyle);

            GUILayout.Space(6f);
            GUILayout.Label("B/W", normalStyle);
            var monoRect = GUILayoutUtility.GetRect(260f, 24f, GUILayout.ExpandWidth(true));
            if (!midiEditMode)
            {
                layer.MonochromeAmount = GUI.HorizontalSlider(monoRect, Mathf.Clamp01(layer.MonochromeAmount), 0f, 1f);
            }
            else
            {
                DrawStaticHorizontalSlider(monoRect, Mathf.Clamp01(layer.MonochromeAmount));
                DrawMidiLearnOverlay(monoRect, MidiBindingAction.LayerMonochromeAmount, selectedLayerIndex, "B/W");
            }
            GUILayout.Label("B/W " + Mathf.RoundToInt(Mathf.Clamp01(layer.MonochromeAmount) * 100f).ToString(CultureInfo.InvariantCulture) + "%", smallStyle);

            GUILayout.Space(8f);
            GUILayout.Label("Color Mode", normalStyle);
            GUILayout.BeginHorizontal();
            DrawLayerColorModeButton(layer, LayerColorMode.None);
            DrawLayerColorModeButton(layer, LayerColorMode.Invert);
            DrawLayerColorModeButton(layer, LayerColorMode.Edge);
            DrawLayerColorModeButton(layer, LayerColorMode.Monochrome);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Program Output", normalStyle);
            var programSolo = programSoloLayerIndex == selectedLayerIndex;
            var programSoloRect = GUILayoutUtility.GetRect(220f, 28f, GUILayout.Width(220f));
            var programMute = layer.ProgramMuted;
            var programMuteRect = GUILayoutUtility.GetRect(220f, 28f, GUILayout.Width(220f));
            if (!midiEditMode)
            {
                var soloHeld = GUI.RepeatButton(programSoloRect, programSolo ? "> PGM Solo ON" : "PGM Solo");
                if (soloHeld)
                {
                    programSoloLayerIndex = selectedLayerIndex;
                }
                else if (programSoloLayerIndex == selectedLayerIndex)
                {
                    programSoloLayerIndex = -1;
                }
                layer.ProgramMuted = GUI.RepeatButton(programMuteRect, programMute ? "> PGM Mute ON" : "PGM Mute");
            }
            else
            {
                GUI.Label(programSoloRect, programSolo ? "> PGM Solo ON" : "PGM Solo", normalStyle);
                DrawMidiLearnOverlay(programSoloRect, MidiBindingAction.LayerProgramSolo, selectedLayerIndex, "PGM Solo");
                GUI.Label(programMuteRect, programMute ? "> PGM Mute ON" : "PGM Mute", normalStyle);
                DrawMidiLearnOverlay(programMuteRect, MidiBindingAction.LayerProgramMute, selectedLayerIndex, "PGM Mute");
            }
        }

        if (!string.IsNullOrEmpty(layer.VideoStatus))
        {
            GUILayout.Label(layer.VideoStatus, smallStyle);
        }
    }

    private void DrawLayerEffectControls(LayerState layer)
    {
        if (layer == null || layer.Effect == null || !layer.Effect.HasEffect)
        {
            return;
        }

        var effect = layer.Effect;
        GUILayout.Label("Effect", normalStyle);
        GUILayout.Label(EffectKindLabel(effect.Kind), titleStyle);

        GUILayout.BeginHorizontal();
        var enabledRect = GUILayoutUtility.GetRect(90f, 22f, GUILayout.Width(90f));
        if (midiEditMode)
        {
            DrawStaticButton(enabledRect, effect.Enabled ? "Enabled" : "Disabled");
            DrawMidiLearnOverlay(enabledRect, MidiBindingAction.LayerEffectToggle, selectedLayerIndex, "Enabled");
        }
        else
        {
            effect.Enabled = GUI.Toggle(enabledRect, effect.Enabled, "Enabled");
        }
        if (GUILayout.Button("Clear Effect", GUILayout.Width(110f), GUILayout.Height(26f)))
        {
            StopLayerVideoPlayback(layer);
            ResetLayerEffectState(layer);
            layer.SourceKind = LayerSourceKind.None;
            layer.SourceOrigin = LayerSourceOrigin.None;
            layer.SourceName = null;
            layer.EffectRenderedOutput = null;
            layer.EffectRenderedFrame = -1;
            return;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        if (effect.Kind == LayerEffectKind.Strobe)
        {
            for (var i = 0; i < effect.TargetLayers.Length; i++)
            {
                effect.TargetLayers[i] = false;
            }
            GUILayout.Label("No input. White flash envelope only.", smallStyle);
        }
        else if (effect.Kind == LayerEffectKind.StepSequencer)
        {
            GUILayout.Label("Shift + click layer cells or media browser items to build the queue.", smallStyle);
            GUILayout.Label("Click the same target again to remove it. Queue order is shown on tiles.", smallStyle);
        }
        else
        {
            GUILayout.Label("Input: composite of all lower layers", smallStyle);
        }

        GUILayout.Space(6f);
        switch (effect.Kind)
        {
            case LayerEffectKind.RgbEffect:
                GUILayout.BeginHorizontal();
                GUILayout.Label("RGB Mode", GUILayout.Width(76f));
                DrawRgbModeButton(effect, LayerRgbEffectMode.RedOnly);
                DrawRgbModeButton(effect, LayerRgbEffectMode.GreenOnly);
                DrawRgbModeButton(effect, LayerRgbEffectMode.BlueOnly);
                DrawRgbModeButton(effect, LayerRgbEffectMode.RgbInvert);
                DrawRgbModeButton(effect, LayerRgbEffectMode.EdgeExtract);
                DrawRgbModeButton(effect, LayerRgbEffectMode.Monochrome);
                GUILayout.EndHorizontal();
                if (effect.RgbMode == LayerRgbEffectMode.EdgeExtract)
                {
                    DrawEffectIntensityControls(effect, 0f, 1f);
                }
                break;
            case LayerEffectKind.QuadSplit:
                GUILayout.Label("4-way tile split.", smallStyle);
                break;
            case LayerEffectKind.Blur:
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mode", GUILayout.Width(56f));
                DrawEffectModeButton(effect, LayerEffectMode.Horizontal, "Horizontal");
                DrawEffectModeButton(effect, LayerEffectMode.Vertical, "Vertical");
                DrawEffectModeButton(effect, LayerEffectMode.Alternate, "H>V");
                DrawEffectModeButton(effect, LayerEffectMode.Zoom, "Zoom");
                GUILayout.EndHorizontal();
                DrawEffectIntensityControls(effect, 0f, 1f);
                break;
            case LayerEffectKind.Glitch:
                DrawEffectIntensityControls(effect, 0f, 1f);
                break;
            case LayerEffectKind.StepSequencer:
                var queueCount = effect.StepSequenceQueue == null ? 0 : effect.StepSequenceQueue.Count;
                GUILayout.Label("Queue: " + queueCount.ToString(CultureInfo.InvariantCulture) + " items. Output changes every beat.", smallStyle);
                break;
            case LayerEffectKind.Strobe:
                GUILayout.BeginHorizontal();
                GUILayout.Label("Rate", GUILayout.Width(56f));
                DrawStrobeModeButton(effect, LayerEffectMode.Beat1, "1");
                DrawStrobeModeButton(effect, LayerEffectMode.Beat2, "1/2");
                DrawStrobeModeButton(effect, LayerEffectMode.Beat4, "1/4");
                DrawStrobeModeButton(effect, LayerEffectMode.Manual, "Manual");
                GUILayout.EndHorizontal();
                if (effect.Mode == LayerEffectMode.Manual)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Flash", GUILayout.Width(56f));
                    var holdRect = GUILayoutUtility.GetRect(92f, 24f, GUILayout.Width(92f), GUILayout.Height(24f));
                    if (midiEditMode)
                    {
                        DrawStaticButton(holdRect, effect.ManualFlashHeld ? "> HOLD" : "HOLD");
                        DrawMidiLearnOverlay(holdRect, MidiBindingAction.LayerEffectValue, selectedLayerIndex, "Hold");
                    }
                    else
                    {
                        effect.ManualFlashHeld = GUI.RepeatButton(holdRect, effect.ManualFlashHeld ? "> HOLD" : "HOLD");
                    }
                    GUILayout.EndHorizontal();
                }
                break;
        }
    }

    private void DrawLayerColorModeButton(LayerState layer, LayerColorMode mode)
    {
        var rect = GUILayoutUtility.GetRect(86f, 24f, GUILayout.Width(86f));
        var label = IsLayerColorPresetActive(layer, mode) ? "> " + LayerColorModeLabel(mode) : LayerColorModeLabel(mode);
        if (midiEditMode)
        {
            DrawStaticButton(rect, label);
            DrawMidiLearnOverlay(rect, MidiBindingAction.LayerColorSetMode, selectedLayerIndex, LayerColorModeLabel(mode), (int)mode);
            return;
        }

        if (GUI.Button(rect, label))
        {
            ApplyLayerColorPreset(layer, mode);
        }
    }

    private static bool IsLayerColorPresetActive(LayerState layer, LayerColorMode mode)
    {
        if (layer == null)
        {
            return false;
        }

        switch (mode)
        {
            case LayerColorMode.None:
                return layer.ColorMode == LayerColorMode.None && layer.InvertAmount <= 0.001f && layer.MonochromeAmount <= 0.001f;
            case LayerColorMode.Invert:
                return layer.InvertAmount >= 0.999f;
            case LayerColorMode.Edge:
                return layer.ColorMode == LayerColorMode.Edge;
            case LayerColorMode.Monochrome:
                return layer.MonochromeAmount >= 0.999f;
            default:
                return false;
        }
    }

    private static LayerColorMode CurrentLayerColorPreset(LayerState layer)
    {
        if (layer == null)
        {
            return LayerColorMode.None;
        }
        if (layer.ColorMode == LayerColorMode.Edge)
        {
            return LayerColorMode.Edge;
        }
        if (layer.MonochromeAmount >= 0.999f)
        {
            return LayerColorMode.Monochrome;
        }
        if (layer.InvertAmount >= 0.999f)
        {
            return LayerColorMode.Invert;
        }
        return LayerColorMode.None;
    }

    private static void ApplyLayerColorPreset(LayerState layer, LayerColorMode mode)
    {
        if (layer == null)
        {
            return;
        }

        switch (mode)
        {
            case LayerColorMode.None:
                layer.ColorMode = LayerColorMode.None;
                layer.InvertAmount = 0f;
                layer.MonochromeAmount = 0f;
                break;
            case LayerColorMode.Invert:
                layer.ColorMode = LayerColorMode.None;
                layer.InvertAmount = 1f;
                break;
            case LayerColorMode.Edge:
                layer.ColorMode = layer.ColorMode == LayerColorMode.Edge ? LayerColorMode.None : LayerColorMode.Edge;
                break;
            case LayerColorMode.Monochrome:
                layer.ColorMode = LayerColorMode.None;
                layer.MonochromeAmount = 1f;
                break;
        }
    }

    private void DrawEffectModeCycleControls(LayerEffectState effect, string label)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(76f));
        GUILayout.Label(EffectKindLabel(effect.Kind), smallStyle, GUILayout.Width(140f));
        GUILayout.EndHorizontal();
    }

    private void DrawRgbModeButton(LayerEffectState effect, LayerRgbEffectMode mode)
    {
        var rect = GUILayoutUtility.GetRect(70f, 24f, GUILayout.Width(70f), GUILayout.Height(24f));
        if (midiEditMode)
        {
            DrawStaticButton(rect, effect.RgbMode == mode ? "> " + RgbEffectModeLabel(mode) : RgbEffectModeLabel(mode));
            DrawMidiLearnOverlay(rect, MidiBindingAction.LayerEffectSetRgbMode, selectedLayerIndex, RgbEffectModeLabel(mode), (int)mode);
            return;
        }
        var clicked = GUI.Button(rect, effect.RgbMode == mode ? "> " + RgbEffectModeLabel(mode) : RgbEffectModeLabel(mode));
        if (clicked)
        {
            effect.RgbMode = mode;
        }
    }

    private void DrawEffectIntensityControls(LayerEffectState effect, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Amount", GUILayout.Width(56f));
        var sliderRect = GUILayoutUtility.GetRect(180f, 18f, GUILayout.Width(180f));
        if (midiEditMode)
        {
            DrawStaticHorizontalSlider(sliderRect, effect.Intensity);
            DrawMidiLearnOverlay(sliderRect, MidiBindingAction.LayerEffectValue, selectedLayerIndex, "Amount");
        }
        else
        {
            var nextIntensity = GUI.HorizontalSlider(sliderRect, effect.Intensity, min, max);
            effect.Intensity = nextIntensity;
        }
        GUILayout.Label(Mathf.RoundToInt(effect.Intensity * 100f).ToString(CultureInfo.InvariantCulture), GUILayout.Width(40f));
        GUILayout.EndHorizontal();
    }

    private void DrawStrobeModeButton(LayerEffectState effect, LayerEffectMode mode, string label)
    {
        var rect = GUILayoutUtility.GetRect(52f, 24f, GUILayout.Width(52f), GUILayout.Height(24f));
        if (midiEditMode)
        {
            DrawStaticButton(rect, effect.Mode == mode ? "> " + label : label);
            DrawMidiLearnOverlay(rect, MidiBindingAction.LayerEffectModeCycle, selectedLayerIndex, label);
            return;
        }
        var clicked = GUI.Button(rect, effect.Mode == mode ? "> " + label : label);
        if (clicked)
        {
            effect.Mode = mode;
        }
    }

    private void DrawEffectModeButton(LayerEffectState effect, LayerEffectMode mode, string label)
    {
        var rect = GUILayoutUtility.GetRect(84f, 24f, GUILayout.MinWidth(72f), GUILayout.Height(24f));
        var text = effect.Mode == mode ? "> " + label : label;
        if (midiEditMode)
        {
            DrawStaticButton(rect, text);
            DrawMidiLearnOverlay(rect, MidiBindingAction.LayerEffectSetMode, selectedLayerIndex, label, (int)mode);
            return;
        }

        if (GUI.Button(rect, text))
        {
            effect.Mode = mode;
        }
    }

    private void DrawStepCountButton(LayerEffectState effect, int count)
    {
        var rect = GUILayoutUtility.GetRect(52f, 24f, GUILayout.Width(52f), GUILayout.Height(24f));
        var text = effect.StepSequenceLength == count ? "> " + count.ToString(CultureInfo.InvariantCulture) : count.ToString(CultureInfo.InvariantCulture);
        if (midiEditMode)
        {
            DrawStaticButton(rect, text);
            DrawMidiLearnOverlay(rect, MidiBindingAction.LayerEffectModeCycle, selectedLayerIndex, "Steps " + count.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (GUI.Button(rect, text))
        {
            effect.StepSequenceLength = count;
        }
    }

    private static string EffectModeLabel(LayerEffectMode mode)
    {
        switch (mode)
        {
            case LayerEffectMode.Horizontal:
                return "Horizontal";
            case LayerEffectMode.Vertical:
                return "Vertical";
            case LayerEffectMode.Alternate:
                return "H>V";
            case LayerEffectMode.Zoom:
                return "Zoom";
            case LayerEffectMode.Beat1:
                return "1";
            case LayerEffectMode.Beat2:
                return "1/2";
            case LayerEffectMode.Beat4:
                return "1/4";
            case LayerEffectMode.Manual:
                return "Manual";
            default:
                return mode.ToString();
        }
    }

    private static string EffectKindLabel(LayerEffectKind kind)
    {
        switch (kind)
        {
            case LayerEffectKind.RgbEffect:
                return "RGB Effect";
            case LayerEffectKind.QuadSplit:
                return "4 Split";
            case LayerEffectKind.Blur:
                return "BPM Blur";
            case LayerEffectKind.Glitch:
                return "Glitch";
            case LayerEffectKind.Strobe:
                return "Strobe";
            case LayerEffectKind.StepSequencer:
                return "Step Sequencer";
            default:
                return "None";
        }
    }

    private static string RgbEffectModeLabel(LayerRgbEffectMode mode)
    {
        switch (mode)
        {
            case LayerRgbEffectMode.RedOnly:
                return "Red";
            case LayerRgbEffectMode.GreenOnly:
                return "Green";
            case LayerRgbEffectMode.BlueOnly:
                return "Blue";
            case LayerRgbEffectMode.RgbInvert:
                return "Invert";
            case LayerRgbEffectMode.EdgeExtract:
                return "Edge";
            case LayerRgbEffectMode.Monochrome:
                return "B/W";
            default:
                return "RGB";
        }
    }

    private void DrawVideoFileDetailControls(LayerState layer, StateSnapshot snapshot)
    {
        if (layer == null)
        {
            return;
        }

        GUILayout.Label("Video Detail", normalStyle);
        GUILayout.Label(string.IsNullOrEmpty(layer.SourceName) ? "No video loaded." : layer.SourceName, titleStyle);
        if (!string.IsNullOrEmpty(layer.Path))
        {
            GUILayout.Label(ShortPath(layer.Path), smallStyle);
        }
        GUILayout.Label("Resolution " + DescribeLayerSourceResolution(layer), smallStyle);
        GUILayout.Label("Codec " + (string.IsNullOrEmpty(layer.DetectedVideoCodec) ? "--" : layer.DetectedVideoCodec), smallStyle);

        GUILayout.Space(8f);
        GUILayout.Label("Playback Mode", normalStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(layer.VideoMode == VideoPlaybackMode.Bpm ? "> BPM" : "BPM", GUILayout.Height(26f)))
        {
            if (layer.VideoMode != VideoPlaybackMode.Bpm)
            {
                layer.VideoMode = VideoPlaybackMode.Bpm;
                layer.VideoResyncPending = false;
            }
        }
        if (GUILayout.Button(layer.VideoMode == VideoPlaybackMode.Timeline ? "> Timeline" : "Timeline", GUILayout.Height(26f)))
        {
            if (layer.VideoMode != VideoPlaybackMode.Timeline)
            {
                layer.VideoMode = VideoPlaybackMode.Timeline;
                layer.VideoResyncPending = false;
                if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
                {
                    SetReflectionValue(layer.KlakHapPlayer, "speed", 1f);
                    SetReflectionValue(layer.KlakHapPlayer, "enabled", true);
                }
                else if (layer.Player != null)
                {
                    layer.Player.playbackSpeed = 1f;
                    if (!layer.Player.isPlaying)
                    {
                        layer.Player.Play();
                    }
                }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Base BPM", GUILayout.Width(76f));
        layer.VideoBaseBpmInput = GUILayout.TextField(string.IsNullOrEmpty(layer.VideoBaseBpmInput) ? layer.VideoBaseBpm.ToString("0.#", CultureInfo.InvariantCulture) : layer.VideoBaseBpmInput, GUILayout.Width(72f));
        if (GUILayout.Button("Apply", GUILayout.Width(70f), GUILayout.Height(26f)))
        {
            if (float.TryParse(layer.VideoBaseBpmInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var baseBpm))
            {
                layer.VideoBaseBpm = Mathf.Clamp(baseBpm, 1f, 400f);
                layer.VideoBaseBpmInput = layer.VideoBaseBpm.ToString("0.#", CultureInfo.InvariantCulture);
            }
        }
        foreach (var bpmPreset in new[] { 60f, 90f, 120f, 128f, 140f })
        {
            if (GUILayout.Button(bpmPreset.ToString("0", CultureInfo.InvariantCulture), GUILayout.Width(44f), GUILayout.Height(26f)))
            {
                layer.VideoBaseBpm = bpmPreset;
                layer.VideoBaseBpmInput = bpmPreset.ToString("0", CultureInfo.InvariantCulture);
            }
        }
        GUILayout.EndHorizontal();

        var bpm = CurrentVisualBpm();
        var sourceLabel = bpmDjLinkMode ? "DJ LINK master" : "Tap counter";
        var length = LayerPlaybackLengthSeconds(layer);
        var current = LayerPlaybackTimeSeconds(layer);
        var targetSpeed = Mathf.Clamp(bpm / Mathf.Max(1f, layer.VideoBaseBpm), 0.05f, 16f);
        GUILayout.Label("Sync source: " + sourceLabel + "   BPM " + bpm.ToString("0.0", CultureInfo.InvariantCulture), smallStyle);
        GUILayout.Label("Base BPM " + layer.VideoBaseBpm.ToString("0.#", CultureInfo.InvariantCulture) + "   Clip " +
                        (length > 0.001 ? length.ToString("0.00", CultureInfo.InvariantCulture) + "s" : "--") +
                        "   Speed x" + (layer.VideoMode == VideoPlaybackMode.Bpm ? targetSpeed.ToString("0.00", CultureInfo.InvariantCulture) : "1.00"), smallStyle);
        GUILayout.Label("Time " + current.ToString("0.0", CultureInfo.InvariantCulture) + " / " +
                        (length > 0.001 ? length.ToString("0.0", CultureInfo.InvariantCulture) : "--") +
                        (layer.VideoMode == VideoPlaybackMode.Bpm ? "   BPM mode" : "   Timeline mode"), smallStyle);
        if (!string.IsNullOrEmpty(layer.VideoStatus))
        {
            GUILayout.Label(layer.VideoStatus, smallStyle);
        }
    }

    private void DrawYoutubeWaveformControl(LayerState layer, Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.012f, 0.015f, 0.018f, 1f), 0f, 4f);
        if (layer == null || layer.YouTube == null)
        {
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 22f), "YouTube waveform", smallStyle);
            return;
        }

        var player = layer.Player;
        var current = player == null ? 0.0 : player.time;
        var length = player == null ? 0.0 : player.length;
        if (length <= 0.001)
        {
            length = layer.YouTube.KnownLengthSeconds > 0.001 ? layer.YouTube.KnownLengthSeconds : 0.0;
        }
        else
        {
            layer.YouTube.KnownLengthSeconds = length;
        }

        var pad = 10f;
        var wave = new Rect(rect.x + pad, rect.y + 24f, rect.width - pad * 2f, rect.height - 40f);
        GUI.Label(new Rect(rect.x + pad, rect.y + 5f, rect.width - pad * 2f, 18f), "Waveform / position", smallStyle);
        GUI.DrawTexture(wave, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.005f, 0.006f, 0.008f, 1f), 0f, 2f);

        if (layer.YouTube.WaveformTexture != null)
        {
            GUI.DrawTexture(wave, layer.YouTube.WaveformTexture, ScaleMode.StretchToFill, true);
        }
        else
        {
            var columns = Mathf.Clamp(Mathf.FloorToInt(wave.width / 3f), 64, 360);
            var columnW = wave.width / columns;
            var seed = StableTextHash(layer.YouTube.VideoId ?? layer.YouTube.Url ?? layer.YouTube.Title ?? "");
            var centerY = wave.y + wave.height * 0.5f;
            for (var i = 0; i < columns; i++)
            {
                var value = YoutubeWaveformValue(seed, i);
                var h = Mathf.Clamp(value * wave.height * 0.92f, 2f, wave.height);
                var x = wave.x + i * columnW;
                var color = YoutubeWaveformColor(seed, i);
                GUI.DrawTexture(new Rect(x + 0.5f, centerY - h * 0.5f, Mathf.Max(1f, columnW - 1f), h), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
            }
        }

        var progress = length > 0.001 ? Mathf.Clamp01((float)(current / length)) : 0f;
        var markerX = wave.x + wave.width * progress;
        GUI.DrawTexture(new Rect(markerX - 1.5f, wave.y, 3f, wave.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.9f, 0.16f, 0.98f), 0f, 0f);
        GUI.Label(new Rect(wave.x, wave.yMax + 4f, wave.width, 18f),
            FormatSeconds(current) + " / " + (length > 0.001 ? FormatSeconds(length) : "--:--") +
            "   " + YoutubeWaveformStatus(layer.YouTube), smallStyle);

        var evt = Event.current;
        if (evt != null && evt.type == EventType.MouseDown && wave.Contains(evt.mousePosition) && player != null && length > 0.001)
        {
            var next = Mathf.Clamp01((evt.mousePosition.x - wave.x) / wave.width) * length;
            player.time = next;
            layer.YouTube.TimeInput = next.ToString("0.0", CultureInfo.InvariantCulture);
            evt.Use();
        }
    }

    private void DrawYoutubePlayerWaveformSelector(YoutubeState youtube, StateSnapshot snapshot)
    {
        if (youtube == null)
        {
            return;
        }

        if (youtube.WaveformPlayerNumber < 1 || youtube.WaveformPlayerNumber > 4)
        {
            youtube.WaveformPlayerNumber = 1;
        }

        GUILayout.Label("DJ Link Player Waveform", smallStyle);
        GUILayout.BeginHorizontal();
        for (var i = 1; i <= 4; i++)
        {
            var player = snapshot == null ? null : FindPlayer(snapshot.Players, i);
            var prefix = youtube.WaveformPlayerNumber == i ? "> " : "";
            var state = player != null && player.DirectWaveformTexture != null ? "" : " --";
            if (GUILayout.Button(prefix + "P" + i.ToString(CultureInfo.InvariantCulture) + state, GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                youtube.WaveformPlayerNumber = i;
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawYoutubeLinkedPlayerWaveform(YoutubeState youtube, StateSnapshot snapshot, Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.012f, 0.015f, 0.018f, 1f), 0f, 4f);
        if (youtube == null)
        {
            return;
        }

        var player = snapshot == null ? null : FindPlayer(snapshot.Players, youtube.WaveformPlayerNumber);
        var pad = 10f;
        GUI.Label(new Rect(rect.x + pad, rect.y + 5f, rect.width - pad * 2f, 18f),
            "Player " + youtube.WaveformPlayerNumber.ToString(CultureInfo.InvariantCulture) + " full waveform", smallStyle);
        var wave = new Rect(rect.x + pad, rect.y + 25f, rect.width - pad * 2f, rect.height - 36f);
        GUI.DrawTexture(wave, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.005f, 0.006f, 0.008f, 1f), 0f, 2f);

        if (player != null && player.DirectWaveformTexture != null)
        {
            DrawDirectOverviewWaveform(player, wave);
            DrawOverviewPlaybackMarker(player, wave);
            DrawCurrentCueOnOverview(player, wave);
        }
        else
        {
            GUI.Label(new Rect(wave.x + 10f, wave.y + 6f, wave.width - 20f, 20f), "Waiting for DJ Link waveform", smallStyle);
        }
    }

    private void AutoAlignYoutubeToPlayer(LayerState layer, StateSnapshot snapshot)
    {
        if (layer == null || layer.YouTube == null || layer.Player == null)
        {
            return;
        }

        var youtube = layer.YouTube;
        var player = snapshot == null ? null : FindPlayer(snapshot.Players, youtube.WaveformPlayerNumber);
        if (player == null || player.DirectWaveformBytes == null || player.DirectWaveformBytes.Length == 0)
        {
            youtube.AlignmentStatus = "Selected player waveform is not available.";
            return;
        }

        var youtubeProfile = youtube.WaveformTexture != null ? ExtractWaveformProfileFromTexture(youtube.WaveformTexture) : null;
        if (youtubeProfile == null || youtubeProfile.Length < 64)
        {
            youtube.AlignmentStatus = "YouTube analyzed waveform is not available.";
            return;
        }

        var playerProfile = ExtractPlayerWaveformProfile(player);
        if (playerProfile == null || playerProfile.Length < 64)
        {
            youtube.AlignmentStatus = "Player waveform profile is not available.";
            return;
        }

        const int sampleCount = 768;
        var yt = ResampleAndNormalizeProfile(youtubeProfile, sampleCount);
        var pl = ResampleAndNormalizeProfile(playerProfile, sampleCount);
        var bestLag = 0;
        var bestScore = float.NegativeInfinity;
        var maxLag = sampleCount / 3;
        var minOverlap = sampleCount / 3;
        for (var lag = -maxLag; lag <= maxLag; lag++)
        {
            var start = Mathf.Max(0, -lag);
            var end = Mathf.Min(sampleCount, sampleCount - lag);
            var count = end - start;
            if (count < minOverlap)
            {
                continue;
            }

            var score = 0f;
            for (var i = start; i < end; i++)
            {
                score += yt[i] * pl[i + lag];
            }
            score /= count;
            if (score > bestScore)
            {
                bestScore = score;
                bestLag = lag;
            }
        }

        if (float.IsNegativeInfinity(bestScore))
        {
            youtube.AlignmentStatus = "Waveform comparison failed.";
            return;
        }

        var playerProgress = PlaybackProgress(player);
        var lagProgress = bestLag / (float)(sampleCount - 1);
        var targetProgress = Mathf.Clamp01(playerProgress - lagProgress);
        var youtubeLength = layer.Player.length > 0.001 ? layer.Player.length : youtube.KnownLengthSeconds;
        if (youtubeLength <= 0.001)
        {
            youtube.AlignmentStatus = "YouTube length is not known yet.";
            return;
        }

        var targetTime = targetProgress * youtubeLength;
        layer.Player.time = targetTime;
        youtube.TimeInput = targetTime.ToString("0.0", CultureInfo.InvariantCulture);
        youtube.AlignmentStatus = "Aligned to P" + youtube.WaveformPlayerNumber.ToString(CultureInfo.InvariantCulture) +
                                  "  offset " + (lagProgress * 100f).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) +
                                  "%  score " + bestScore.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static int StableTextHash(string text)
    {
        unchecked
        {
            var hash = 23;
            if (!string.IsNullOrEmpty(text))
            {
                for (var i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }
            }
            return hash;
        }
    }

    private static float YoutubeWaveformValue(int seed, int index)
    {
        unchecked
        {
            var n = seed + index * 1103515245 + 12345;
            n ^= n << 13;
            n ^= n >> 17;
            n ^= n << 5;
            var random = (n & 0x7fffffff) / 2147483647f;
            var slow = 0.5f + 0.5f * Mathf.Sin((index + (seed & 31)) * 0.115f);
            var fast = 0.5f + 0.5f * Mathf.Sin((index + (seed & 63)) * 0.41f);
            return Mathf.Clamp01(0.18f + random * 0.42f + slow * 0.24f + fast * 0.16f);
        }
    }

    private static Texture2D BuildYoutubeWaveformTexture(byte[] pcmBytes)
    {
        if (pcmBytes == null || pcmBytes.Length < 2)
        {
            return null;
        }

        var sampleCount = pcmBytes.Length / 2;
        var baseSamplesPerColumn = Mathf.Max(1, YoutubeWaveformSampleRate / YoutubeWaveformColumnsPerSecond);
        var baseColumnCount = Mathf.Max(1, Mathf.CeilToInt(sampleCount / (float)baseSamplesPerColumn));
        var columnsPerTextureColumn = Mathf.Max(1, Mathf.CeilToInt(baseColumnCount / (float)YoutubeWaveformMaxWidth));
        var samplesPerColumn = baseSamplesPerColumn * columnsPerTextureColumn;
        var width = Mathf.Clamp(Mathf.CeilToInt(sampleCount / (float)samplesPerColumn), 1, YoutubeWaveformMaxWidth);
        var height = Mathf.Max(24, YoutubeWaveformHeight);
        var amplitude = new float[width];
        var red = new float[width];
        var green = new float[width];
        var blue = new float[width];
        var lowState = 0f;
        var midState = 0f;
        var lowEnvelope = 0f;
        var midEnvelope = 0f;
        var highEnvelope = 0f;

        for (var x = 0; x < width; x++)
        {
            var startSample = x * samplesPerColumn;
            var endSample = Math.Min(sampleCount, startSample + samplesPerColumn);
            if (endSample <= startSample)
            {
                continue;
            }

            var ampSum = 0f;
            var lowSum = 0f;
            var midSum = 0f;
            var highSum = 0f;
            var peak = 0f;
            var count = 0;

            for (var sample = startSample; sample < endSample; sample++)
            {
                var byteIndex = sample * 2;
                var raw = (short)(pcmBytes[byteIndex] | (pcmBytes[byteIndex + 1] << 8));
                var value = raw / 32768f;
                lowState += (value - lowState) * 0.032f;
                var nonLow = value - lowState;
                midState += (nonLow - midState) * 0.18f;
                var high = nonLow - midState;
                var absValue = Mathf.Abs(value);
                lowEnvelope = Mathf.Max(Mathf.Abs(lowState), lowEnvelope * 0.992f);
                midEnvelope = Mathf.Max(Mathf.Abs(midState), midEnvelope * 0.988f);
                highEnvelope = Mathf.Max(Mathf.Abs(high), highEnvelope * 0.984f);

                ampSum += value * value;
                lowSum += lowEnvelope * lowEnvelope;
                midSum += midEnvelope * midEnvelope;
                highSum += highEnvelope * highEnvelope;
                peak = Mathf.Max(peak, absValue);
                count++;
            }

            if (count <= 0)
            {
                continue;
            }

            amplitude[x] = Mathf.Max(peak * 0.58f, Mathf.Sqrt(ampSum / count));
            red[x] = Mathf.Sqrt(lowSum / count);
            green[x] = Mathf.Sqrt(midSum / count);
            blue[x] = Mathf.Sqrt(highSum / count);
        }

        var amplitudeReference = PercentileReference(amplitude, 0.985f);
        var redReference = PercentileReference(red, 0.985f);
        var greenReference = PercentileReference(green, 0.985f);
        var blueReference = PercentileReference(blue, 0.985f);
        var pixels = new Color32[width * height];
        var clear = new Color32(0, 0, 0, 0);
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }

        var centerY = Mathf.RoundToInt(height * 0.5f);
        for (var x = 0; x < width; x++)
        {
            var normalizedAmplitude = Mathf.Clamp01(amplitude[x] / amplitudeReference);
            var cdjHeightStep = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(normalizedAmplitude, 0.72f) * 31f), 1, 31);
            var barHeight = Mathf.Clamp(Mathf.RoundToInt(cdjHeightStep / 31f * height * 0.84f), 2, height);
            var yMin = Mathf.Clamp(centerY - barHeight / 2, 0, height - 1);
            var yMax = Mathf.Clamp(centerY + (barHeight + 1) / 2, 0, height - 1);
            var color = CdjLikeYoutubeColor(red[x], green[x], blue[x], redReference, greenReference, blueReference);
            var color32 = (Color32)color;
            for (var y = yMin; y <= yMax; y++)
            {
                pixels[y * width + x] = color32;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static float[] ExtractWaveformProfileFromTexture(Texture2D texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
        {
            return null;
        }

        try
        {
            var pixels = texture.GetPixels32();
            var profile = new float[texture.width];
            var center = (texture.height - 1) * 0.5f;
            for (var x = 0; x < texture.width; x++)
            {
                var weighted = 0f;
                var alpha = 0f;
                for (var y = 0; y < texture.height; y++)
                {
                    var a = pixels[y * texture.width + x].a / 255f;
                    if (a <= 0f)
                    {
                        continue;
                    }
                    weighted += a * Mathf.Abs(y - center);
                    alpha += a;
                }
                profile[x] = alpha <= 0f ? 0f : weighted / Math.Max(1f, center * alpha);
            }
            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static float[] ExtractPlayerWaveformProfile(PlayerState player)
    {
        if (player == null || player.DirectWaveformBytes == null)
        {
            return null;
        }

        var frameCount = DirectWaveformFrameCount(player.DirectWaveformBytes, player.DirectWaveformStyle);
        if (frameCount <= 0)
        {
            return null;
        }

        var profile = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            if (player.DirectWaveformStyle == DirectWaveformStyle.Rgb)
            {
                var baseIndex = frame * 2;
                var bits = (player.DirectWaveformBytes[baseIndex] << 8) | player.DirectWaveformBytes[baseIndex + 1];
                profile[frame] = ((bits >> 2) & 0x1f) / 31f;
            }
            else
            {
                profile[frame] = (player.DirectWaveformBytes[frame] & 0x1f) / 31f;
            }
        }
        return profile;
    }

    private static float[] ResampleAndNormalizeProfile(float[] source, int count)
    {
        var result = new float[Mathf.Max(1, count)];
        if (source == null || source.Length == 0)
        {
            return result;
        }

        if (result.Length == 1)
        {
            result[0] = source[0];
        }
        else
        {
            for (var i = 0; i < result.Length; i++)
            {
                var pos = i * (source.Length - 1f) / (result.Length - 1f);
                var left = Mathf.Clamp(Mathf.FloorToInt(pos), 0, source.Length - 1);
                var right = Mathf.Clamp(left + 1, 0, source.Length - 1);
                var t = pos - left;
                result[i] = Mathf.Lerp(source[left], source[right], t);
            }
        }

        var mean = 0f;
        for (var i = 0; i < result.Length; i++)
        {
            mean += result[i];
        }
        mean /= result.Length;

        var variance = 0f;
        for (var i = 0; i < result.Length; i++)
        {
            var centered = result[i] - mean;
            result[i] = centered;
            variance += centered * centered;
        }

        var std = Mathf.Sqrt(Mathf.Max(0.000001f, variance / result.Length));
        for (var i = 0; i < result.Length; i++)
        {
            result[i] /= std;
        }
        return result;
    }

    private static float PercentileReference(float[] values, float percentile)
    {
        if (values == null || values.Length == 0)
        {
            return 0.0001f;
        }

        var copy = new float[values.Length];
        Array.Copy(values, copy, values.Length);
        Array.Sort(copy);
        var index = Mathf.Clamp(Mathf.RoundToInt((copy.Length - 1) * Mathf.Clamp01(percentile)), 0, copy.Length - 1);
        return Mathf.Max(copy[index], 0.0001f);
    }

    private static Color CdjLikeYoutubeColor(float low, float mid, float high, float lowReference, float midReference, float highReference)
    {
        var lowLevel = Mathf.Clamp01(low / Mathf.Max(0.0001f, lowReference));
        var midLevel = Mathf.Clamp01(mid / Mathf.Max(0.0001f, midReference));
        var highLevel = Mathf.Clamp01(high / Mathf.Max(0.0001f, highReference));
        var total = Mathf.Max(0.0001f, lowLevel + midLevel + highLevel);

        var lowMix = lowLevel / total;
        var midMix = midLevel / total;
        var highMix = highLevel / total;
        var intensity = Mathf.Clamp01(Mathf.Pow(total / 1.65f, 0.62f));

        var red = Mathf.Clamp01((lowMix * 1.08f + midMix * 0.25f) * intensity);
        var green = Mathf.Clamp01((midMix * 1.02f + highMix * 0.42f + lowMix * 0.12f) * intensity);
        var blue = Mathf.Clamp01((highMix * 1.08f + midMix * 0.32f) * intensity);

        return new Color(
            Mathf.Clamp01(red * 0.88f + 0.03f),
            Mathf.Clamp01(green * 0.9f + 0.03f),
            Mathf.Clamp01(blue * 0.95f + 0.03f),
            1f);
    }

    private static string YoutubeWaveformStatus(YoutubeState youtube)
    {
        if (youtube == null)
        {
            return "";
        }
        if (youtube.WaveformTexture != null)
        {
            return "analyzed RGB";
        }
        if (!string.IsNullOrEmpty(youtube.WaveformStatus))
        {
            return youtube.WaveformStatus;
        }
        return "pseudo RGB until analyzed";
    }

    private static Color YoutubeWaveformColor(int seed, int index)
    {
        var red = YoutubeWaveformValue(seed ^ 0x2d31, index + 11);
        var green = YoutubeWaveformValue(seed ^ 0x5a17, index + 29);
        var blue = YoutubeWaveformValue(seed ^ 0x71c3, index + 47);
        var max = Mathf.Max(0.001f, Mathf.Max(red, Mathf.Max(green, blue)));
        return new Color(
            Mathf.Lerp(0.08f, 1f, red / max),
            Mathf.Lerp(0.08f, 1f, green / max),
            Mathf.Lerp(0.08f, 1f, blue / max),
            0.9f);
    }

    private static string FormatSeconds(double seconds)
    {
        if (seconds < 0.0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return "--:--";
        }
        var total = Math.Max(0, (int)Math.Round(seconds));
        return (total / 60).ToString("0", CultureInfo.InvariantCulture) + ":" +
               (total % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    private double LayerPlaybackTimeSeconds(LayerState layer)
    {
        if (layer == null)
        {
            return 0.0;
        }

        if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
        {
            return ReflectionDouble(layer.KlakHapPlayer, "time");
        }

        return layer.Player == null ? 0.0 : layer.Player.time;
    }

    private double LayerPlaybackLengthSeconds(LayerState layer)
    {
        if (layer == null)
        {
            return 0.0;
        }

        if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
        {
            return ReflectionDouble(layer.KlakHapPlayer, "streamDuration");
        }

        return layer.Player == null ? 0.0 : layer.Player.length;
    }

    private void DrawMainPipelineSection(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "Source Layers", normalStyle);

        var pad = 12f;
        var content = new Rect(rect.x + pad, rect.y + 38f, rect.width - pad * 2f, rect.height - 50f);
        GUI.Label(new Rect(content.x, content.y, content.width, 20f), "Layers 1-8   (Main / Overlay per layer)", smallStyle);
        var sourceArea = new Rect(content.x, content.y + 24f, content.width, content.height - 24f);
        DrawSourceLayerGrid(sourceArea);
    }

    private void DrawOverlaySection(Rect rect)
    {
        DrawTexturePreviewPanel(rect, "Program Preview", finalCompositeTexture, null);
    }

    private void DrawAllEffectSection(Rect rect)
    {
        var layer = vjLayers != null && selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length ? vjLayers[selectedLayerIndex] : null;
        var preview = layer == null ? null : LayerPreviewTexture(selectedLayerIndex, layer);
        DrawTexturePreviewPanel(rect, "Selected Preview  -  " + LayerSlotLabel(selectedLayerIndex), preview, layer);
    }

    private void DrawLayerRow(Rect rect, int startIndex, int count, bool showPreview)
    {
        var gap = 8f;
        var cellWidth = (rect.width - gap * (count - 1)) / Mathf.Max(1, count);
        for (var i = 0; i < count; i++)
        {
            var index = startIndex + i;
            var cell = new Rect(rect.x + i * (cellWidth + gap), rect.y, cellWidth, rect.height);
            DrawLayerCell(index, cell, showPreview);
        }
    }

    private void DrawSourceLayerGrid(Rect rect)
    {
        var gap = 8f;
        var columns = 4;
        var rows = 2;
        var cellWidth = (rect.width - gap * (columns - 1)) / columns;
        var cellHeight = (rect.height - gap * (rows - 1)) / rows;

        for (var i = 0; i < SourceLayerCount; i++)
        {
            var index = SourceStartIndex + i;
            var column = i % columns;
            var row = i / columns;
            var cell = new Rect(rect.x + column * (cellWidth + gap), rect.y + row * (cellHeight + gap), cellWidth, cellHeight);
            DrawLayerCell(index, cell, true);
        }
    }

    private void DrawEffectLayerRow(Rect rect, int startIndex, int count)
    {
        var gap = 8f;
        var cellWidth = (rect.width - gap * (count - 1)) / Mathf.Max(1, count);
        for (var i = 0; i < count; i++)
        {
            var index = startIndex + i;
            var cell = new Rect(rect.x + i * (cellWidth + gap), rect.y, cellWidth, rect.height);
            DrawEffectLayerCell(index, cell);
        }
    }

    private void DrawCombinedEffectLayerRow(Rect rect)
    {
        var gap = 8f;
        var totalCount = MainEffectLayerCount + AllEffectLayerCount;
        var cellWidth = (rect.width - gap * (totalCount - 1)) / totalCount;
        for (var i = 0; i < totalCount; i++)
        {
            var index = i < MainEffectLayerCount
                ? MainEffectStartIndex + i
                : AllEffectStartIndex + (i - MainEffectLayerCount);
            var cell = new Rect(rect.x + i * (cellWidth + gap), rect.y, cellWidth, rect.height);
            DrawEffectLayerCell(index, cell);
        }
    }

    private void DrawStagePreview(Rect rect, string label, Texture texture)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.022f, 0.028f, 1f), 0f, 4f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f), label, smallStyle);
        var inner = new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, Mathf.Max(40f, rect.height - 36f));
        GUI.DrawTexture(inner, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 4f);
        if (texture != null)
        {
            GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, false);
        }
    }

    private void DrawTexturePreviewPanel(Rect rect, string label, Texture texture, LayerState layer)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), label, normalStyle);
        var previewWidth = rect.width - 24f;
        var previewHeight = Mathf.Min((rect.height - 52f), previewWidth * 9f / 16f);
        previewHeight = Mathf.Max(80f, previewHeight);
        var preview = new Rect(rect.x + 12f, rect.y + 40f, previewWidth, previewHeight);
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
        if (texture != null)
        {
            GUI.DrawTexture(preview, texture, ScaleMode.ScaleToFit, false);
        }
        else
        {
            GUI.Label(new Rect(preview.x + 8f, preview.y + 8f, preview.width - 16f, 24f), layer == null || layer.SourceKind == LayerSourceKind.None ? "empty" : LayerName(layer), smallStyle);
        }
        DrawLayerBusyOverlay(preview, layer);
    }

    private void DrawLayerBusyOverlay(Rect rect, LayerState layer)
    {
        if (!IsLayerBusyLoading(layer))
        {
            return;
        }

        var overlay = new Rect(rect.x + 10f, rect.y + rect.height * 0.5f - 14f, rect.width - 20f, 28f);
        GUI.DrawTexture(overlay, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.48f), 0f, 4f);
        GUI.Label(overlay, BusySpinnerText() + "  " + layer.VideoStatus, markerStyle);
    }

    private static bool IsLayerBusyLoading(LayerState layer)
    {
        if (layer == null || string.IsNullOrEmpty(layer.VideoStatus))
        {
            return false;
        }

        return layer.VideoStatus.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
               layer.VideoStatus.IndexOf("Waiting", StringComparison.OrdinalIgnoreCase) >= 0 ||
               layer.VideoStatus.IndexOf("Converting", StringComparison.OrdinalIgnoreCase) >= 0 ||
               layer.VideoStatus.IndexOf("Resolving", StringComparison.OrdinalIgnoreCase) >= 0 ||
               layer.VideoStatus.IndexOf("Downloading", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BusySpinnerText()
    {
        switch (Mathf.Abs(Mathf.FloorToInt(Time.realtimeSinceStartup * 8f)) % 4)
        {
            case 1:
                return "/";
            case 2:
                return "-";
            case 3:
                return "\\";
            default:
                return "|";
        }
    }

    private void DrawLayerCell(int index, Rect cell, bool showPreview)
    {
        var selected = selectedLayerIndex == index;
        var borderColor = LayerCellBaseColor(vjLayers != null && index < vjLayers.Length ? vjLayers[index] : null);
        GUI.DrawTexture(cell, whiteTexture, ScaleMode.StretchToFill, false, 0f, borderColor, 0f, 4f);
        if (selected)
        {
            DrawRectOutline(cell, 3f, new Color(0.2f, 0.85f, 0.75f, 1f));
        }

        var faderW = 24f;
        var previewHeight = showPreview
            ? Mathf.Max(40f, Mathf.Min(cell.height - 52f, (cell.width - faderW - 10f) * 9f / 16f))
            : Mathf.Max(40f, Mathf.Min(54f, cell.height - 52f));
        var inner = new Rect(cell.x + 3f, cell.y + 3f, cell.width - faderW - 10f, previewHeight);
        GUI.DrawTexture(inner, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);

        var layer = vjLayers != null && index < vjLayers.Length ? vjLayers[index] : null;
        var stepQueueIndex = GetStepSequencerQueueIndexForLayer(selectedLayerIndex, index);
        var layerTexture = showPreview ? LayerPreviewTexture(index, layer) : null;
        if (showPreview && layer != null && layerTexture != null && layer.SourceKind != LayerSourceKind.None)
        {
            var color = Color.white;
            if (!layer.Enabled)
            {
                color.a = 0.25f;
            }
            GUI.color = color;
            GUI.DrawTexture(inner, layerTexture, ScaleMode.ScaleToFit, false);
            GUI.color = Color.white;
        }
        else
        {
            var text = layer == null || layer.SourceKind == LayerSourceKind.None
                ? "empty"
                : showPreview ? "empty" : LayerName(layer);
            GUI.Label(new Rect(inner.x + 8f, inner.y + 8f, inner.width - 16f, 24f), text, smallStyle);
        }
        DrawLayerBusyOverlay(inner, layer);
        if (layer != null && !string.IsNullOrEmpty(layer.VideoStatus))
        {
            GUI.Label(new Rect(inner.x + 8f, inner.yMax - 22f, inner.width - 16f, 18f), layer.VideoStatus, smallStyle);
        }
        if (stepQueueIndex >= 0)
        {
            DrawStepSequencerQueueBadge(inner, stepQueueIndex + 1);
        }

        var faderRect = new Rect(cell.xMax - faderW - 4f, inner.y + 2f, faderW, inner.height - 4f);
        if (layer != null)
        {
            if (!midiEditMode)
            {
                layer.Opacity = GUI.VerticalSlider(faderRect, Mathf.Clamp01(layer.Opacity), 1f, 0f);
            }
            else
            {
                DrawStaticVerticalFader(faderRect, Mathf.Clamp01(layer.Opacity));
            }
            GUI.Label(new Rect(faderRect.x - 4f, faderRect.yMax + 1f, faderW + 8f, 16f), Mathf.RoundToInt(layer.Opacity * 100f).ToString(CultureInfo.InvariantCulture), smallStyle);
        }

        var progressRect = new Rect(inner.x, inner.yMax + 3f, inner.width, 5f);
        GUI.DrawTexture(progressRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.005f, 0.006f, 0.008f, 1f), 0f, 0f);
        if (showPreview && layer != null && layer.SourceKind != LayerSourceKind.None)
        {
            var progress = LayerPlaybackProgress(layer);
            GUI.DrawTexture(new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.2f, 0.85f, 0.75f, 0.95f), 0f, 0f);
        }

        var toggleRect = new Rect(cell.xMax - 48f, progressRect.yMax + 4f, 40f, 22f);
        var blendRect = new Rect(cell.xMax - 104f, progressRect.yMax + 4f, 52f, 22f);
        var showAudioRect = IsSourceLayerSlot(index);
        var audioRect = new Rect(cell.xMax - 158f, progressRect.yMax + 4f, 50f, 22f);
        var showModeRect = IsSourceLayerSlot(index);
        var modeRect = new Rect(cell.xMax - 212f, progressRect.yMax + 4f, 50f, 22f);
        var selectRect = new Rect(inner.x, inner.y, inner.width, Mathf.Max(24f, progressRect.yMax - inner.y + 2f));
        var reservedWidth = 114f;
        if (showAudioRect)
        {
            reservedWidth += 54f;
        }
        if (showModeRect)
        {
            reservedWidth += 54f;
        }
        GUI.Label(new Rect(cell.x + 8f, progressRect.yMax + 3f, cell.width - reservedWidth, 24f), LayerSlotLabel(index) + "  " + LayerName(layer), smallStyle);
        if (showModeRect)
        {
            var modeLabel = SourceGroupModeLabel(layer);
            if (!midiEditMode && GUI.Button(modeRect, modeLabel))
            {
                if (layer != null)
                {
                    layer.SourceGroupMode = layer.SourceGroupMode == LayerSourceGroupMode.Main
                        ? LayerSourceGroupMode.Overlay
                        : LayerSourceGroupMode.Main;
                }
            }
            else if (midiEditMode)
            {
                DrawStaticButton(modeRect, modeLabel);
            }
        }
        if (showAudioRect)
        {
            var supportsAudioToggle = layer != null && (layer.SourceKind == LayerSourceKind.VideoFile || layer.SourceKind == LayerSourceKind.YouTube);
            var audioLabel = !supportsAudioToggle ? "AUD --" : (layer.AudioOutputEnabled ? "AUD ON" : "AUD OFF");
            if (!midiEditMode && GUI.Button(audioRect, audioLabel))
            {
                if (supportsAudioToggle)
                {
                    layer.AudioOutputEnabled = !layer.AudioOutputEnabled;
                    ApplyLayerAudioOutputState(layer);
                }
            }
            else if (midiEditMode)
            {
                DrawStaticButton(audioRect, audioLabel);
            }
        }
        if (!midiEditMode && GUI.Button(blendRect, layer == null ? "Alpha" : LayerBlendLabel(layer.BlendMode)))
        {
            if (layer != null)
            {
                layer.BlendMode = NextLayerBlendMode(layer.BlendMode);
            }
        }
        else if (midiEditMode)
        {
            DrawStaticButton(blendRect, layer == null ? "Alpha" : LayerBlendLabel(layer.BlendMode));
        }
        if (!midiEditMode && GUI.Button(toggleRect, layer != null && layer.Enabled ? "ON" : "OFF"))
        {
            if (layer != null)
            {
                layer.Enabled = !layer.Enabled;
            }
        }
        else if (midiEditMode)
        {
            DrawStaticButton(toggleRect, layer != null && layer.Enabled ? "ON" : "OFF");
        }

        var mouseEvent = Event.current;
        if (midiEditMode)
        {
            DrawMidiLearnOverlay(selectRect, MidiBindingAction.LayerSelect, index, "Select");
            DrawMidiLearnOverlay(faderRect, MidiBindingAction.LayerOpacity, index, "Opacity");
            DrawMidiLearnOverlay(blendRect, MidiBindingAction.LayerBlendCycle, index, "Blend");
            DrawMidiLearnOverlay(toggleRect, MidiBindingAction.LayerToggle, index, "On/Off");
        }
        else if (mouseEvent.type == EventType.MouseDown &&
                 cell.Contains(mouseEvent.mousePosition) &&
                 !toggleRect.Contains(mouseEvent.mousePosition) &&
                 !blendRect.Contains(mouseEvent.mousePosition) &&
                 !(showAudioRect && audioRect.Contains(mouseEvent.mousePosition)) &&
                 !(showModeRect && modeRect.Contains(mouseEvent.mousePosition)))
        {
            if (mouseEvent.button == 0 && IsStepSequencerShiftAssignMode())
            {
                ToggleStepSequencerLayerQueueEntry(selectedLayerIndex, index);
                mouseEvent.Use();
                return;
            }
            selectedLayerIndex = index;
            if (secondDisplayUiEnabled && secondDisplayUiMode == SecondDisplayUiMode.Settings)
            {
                settingsMonitorSelectedLayerIndex = index;
            }
            mouseEvent.Use();
        }
    }

    private void DrawStepSequencerQueueBadge(Rect rect, int order)
    {
        var size = Mathf.Min(42f, Mathf.Min(rect.width, rect.height) * 0.42f);
        var badge = new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
        var badgeStyle = new GUIStyle(normalStyle);
        badgeStyle.alignment = TextAnchor.MiddleCenter;
        badgeStyle.fontSize = Mathf.Max(18, normalStyle.fontSize + 4);
        badgeStyle.normal.textColor = new Color(0.94f, 0.16f, 0.16f, 0.98f);
        GUI.Label(badge, order.ToString(CultureInfo.InvariantCulture), badgeStyle);
    }

    private void DrawEffectLayerCell(int index, Rect cell)
    {
        var selected = selectedLayerIndex == index;
        var layer = vjLayers != null && index < vjLayers.Length ? vjLayers[index] : null;
        GUI.DrawTexture(cell, whiteTexture, ScaleMode.StretchToFill, false, 0f, LayerCellBaseColor(layer), 0f, 4f);
        if (selected)
        {
            DrawRectOutline(cell, 3f, new Color(0.2f, 0.85f, 0.75f, 1f));
        }

        var faderRect = new Rect(cell.xMax - 28f, cell.y + 6f, 24f, cell.height - 12f);
        var infoRect = new Rect(cell.x + 8f, cell.y + 8f, cell.width - 44f, cell.height - 16f);
        GUI.Label(new Rect(infoRect.x, infoRect.y, infoRect.width, 18f), LayerSlotLabel(index), smallStyle);
        var effectName = layer == null || !LayerHasAttachedEffect(layer) ? "empty" : EffectKindLabel(layer.Effect.Kind);
        GUI.Label(new Rect(infoRect.x, infoRect.y + 22f, infoRect.width, 20f), effectName, normalStyle);
        if (layer != null && LayerHasAttachedEffect(layer))
        {
            var modeText = layer.Effect.Kind == LayerEffectKind.RgbEffect
                ? RgbEffectModeLabel(layer.Effect.RgbMode)
                : layer.Effect.Kind == LayerEffectKind.Blur || layer.Effect.Kind == LayerEffectKind.Strobe
                    ? EffectModeLabel(layer.Effect.Mode)
                    : "";
            if (!string.IsNullOrEmpty(modeText))
            {
                GUI.Label(new Rect(infoRect.x, infoRect.y + 44f, infoRect.width, 18f), modeText, smallStyle);
            }
        }

        if (layer != null)
        {
            if (!midiEditMode)
            {
                layer.Opacity = GUI.VerticalSlider(faderRect, Mathf.Clamp01(layer.Opacity), 1f, 0f);
            }
            else
            {
                DrawStaticVerticalFader(faderRect, Mathf.Clamp01(layer.Opacity));
            }
        }

        var mouseEvent = Event.current;
        if (midiEditMode)
        {
            DrawMidiLearnOverlay(infoRect, MidiBindingAction.LayerSelect, index, "Select");
            DrawMidiLearnOverlay(faderRect, MidiBindingAction.LayerOpacity, index, "Opacity");
        }
        else if (mouseEvent.type == EventType.MouseDown && cell.Contains(mouseEvent.mousePosition) && !faderRect.Contains(mouseEvent.mousePosition))
        {
            selectedLayerIndex = index;
            if (secondDisplayUiEnabled && secondDisplayUiMode == SecondDisplayUiMode.Settings)
            {
                settingsMonitorSelectedLayerIndex = index;
            }
            mouseEvent.Use();
        }
    }

    private void DrawStaticButton(Rect rect, string text)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.16f, 0.18f, 0.2f, 1f), 0f, 3f);
        GUI.Label(rect, text, smallStyle);
    }

    private void DrawStaticVerticalFader(Rect rect, float value)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.09f, 0.11f, 1f), 0f, 3f);
        var trackRect = new Rect(rect.x + rect.width * 0.38f, rect.y + 4f, rect.width * 0.24f, rect.height - 8f);
        GUI.DrawTexture(trackRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.16f, 0.18f, 0.21f, 1f), 0f, 2f);
        var knobY = Mathf.Lerp(trackRect.yMax - 10f, trackRect.y, Mathf.Clamp01(value));
        GUI.DrawTexture(new Rect(rect.x + 3f, knobY, rect.width - 6f, 10f), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.78f, 0.82f, 0.88f, 1f), 0f, 2f);
    }

    private void DrawStaticHorizontalSlider(Rect rect, float value)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.09f, 0.11f, 1f), 0f, 3f);
        var trackRect = new Rect(rect.x + 4f, rect.y + rect.height * 0.38f, rect.width - 8f, rect.height * 0.24f);
        GUI.DrawTexture(trackRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.16f, 0.18f, 0.21f, 1f), 0f, 2f);
        var knobX = Mathf.Lerp(trackRect.x, trackRect.xMax - 10f, Mathf.Clamp01(value));
        GUI.DrawTexture(new Rect(knobX, rect.y + 3f, 10f, rect.height - 6f), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.78f, 0.82f, 0.88f, 1f), 0f, 2f);
    }

    private void DrawMidiLearnOverlay(Rect rect, MidiBindingAction action, int index, string shortLabel)
    {
        DrawMidiLearnOverlay(rect, action, index, shortLabel, -1);
    }

    private void DrawMidiLearnOverlay(Rect rect, MidiBindingAction action, int index, string shortLabel, int subIndex)
    {
        if (!midiEditMode)
        {
            return;
        }

        var binding = FindMidiBinding(action, index, subIndex);
        var learning = midiLearningAction == action && midiLearningIndex == index && midiLearningSubIndex == subIndex;
        var fill = learning ? new Color(0.18f, 0.92f, 1f, 0.38f) : new Color(0.18f, 0.8f, 1f, 0.2f);
        var outline = learning ? new Color(0.48f, 1f, 1f, 0.95f) : new Color(0.36f, 0.88f, 1f, 0.82f);
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, fill, 0f, 4f);
        DrawRectOutline(rect, 2f, outline);

        var labelRect = new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, Mathf.Min(34f, rect.height - 6f));
        var bindingText = binding == null ? shortLabel : shortLabel + "  " + FormatMidiBinding(binding);
        GUI.Label(labelRect, bindingText, smallStyle);

        var mouseEvent = Event.current;
        if (mouseEvent.type == EventType.MouseDown && rect.Contains(mouseEvent.mousePosition))
        {
            BeginMidiLearn(action, index, subIndex);
            mouseEvent.Use();
        }
    }

    private static LayerBlendMode NextLayerBlendMode(LayerBlendMode mode)
    {
        switch (mode)
        {
            case LayerBlendMode.Alpha:
                return LayerBlendMode.Add50;
            case LayerBlendMode.Add50:
                return LayerBlendMode.Add100;
            case LayerBlendMode.Add100:
                return LayerBlendMode.Mask;
            default:
                return LayerBlendMode.Alpha;
        }
    }

    private static LayerColorMode NextLayerColorMode(LayerColorMode mode)
    {
        switch (mode)
        {
            case LayerColorMode.None:
                return LayerColorMode.Invert;
            case LayerColorMode.Invert:
                return LayerColorMode.Edge;
            case LayerColorMode.Edge:
                return LayerColorMode.Monochrome;
            default:
                return LayerColorMode.None;
        }
    }

    private static string LayerBlendLabel(LayerBlendMode mode)
    {
        switch (mode)
        {
            case LayerBlendMode.Add50:
                return "50Add";
            case LayerBlendMode.Add100:
                return "Add";
            case LayerBlendMode.Mask:
                return "Mask";
            default:
                return "Alpha";
        }
    }

    private static string LayerColorModeLabel(LayerColorMode mode)
    {
        switch (mode)
        {
            case LayerColorMode.Invert:
                return "Invert";
            case LayerColorMode.Edge:
                return "Edge";
            case LayerColorMode.Monochrome:
                return "B/W";
            default:
                return "Normal";
        }
    }

    private static float LayerPlaybackProgress(LayerState layer)
    {
        if (layer == null ||
            (layer.SourceKind != LayerSourceKind.VideoFile && layer.SourceKind != LayerSourceKind.YouTube))
        {
            return 0f;
        }

        if (layer.SourceKind == LayerSourceKind.VideoFile && layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
        {
            var length = ReflectionDouble(layer.KlakHapPlayer, "streamDuration");
            var current = ReflectionDouble(layer.KlakHapPlayer, "time");
            if (length > 0.001)
            {
                return Mathf.Clamp01((float)(current / length));
            }
            return 0f;
        }

        if (layer.Player == null)
        {
            return 0f;
        }

        if (layer.Player.length > 0.001)
        {
            return Mathf.Clamp01((float)(layer.Player.time / layer.Player.length));
        }

        if (layer.Player.frameCount > 0 && layer.Player.frame >= 0)
        {
            return Mathf.Clamp01((float)(layer.Player.frame / (double)layer.Player.frameCount));
        }

        return 0f;
    }

    private void DrawBpmCounter(Rect rect, StateSnapshot snapshot)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.025f, 0.03f, 0.035f, 1f), 0f, 4f);

        var master = FindMasterPlayer(snapshot == null ? null : snapshot.Players);
        var hasDjLinkBpm = master != null && DisplayEffectiveBpm(master) > 0.01f;
        var bpm = bpmDjLinkMode && hasDjLinkBpm ? DisplayEffectiveBpm(master) : manualBpm;
        var beatIndex = bpmDjLinkMode
            ? Mathf.Abs(Mathf.FloorToInt(CurrentVisualBeatFloat(bpm))) % 4
            : ManualBeatIndex(bpm);
        var x = rect.x + 8f;

        GUI.Label(new Rect(x, rect.y + 7f, 86f, 20f), "BPM " + FormatBpm(bpm), smallStyle);
        x += 92f;
        for (var i = 0; i < 4; i++)
        {
            var active = i == beatIndex;
            var color = active ? new Color(0.95f, 0.92f, 0.18f, 1f) : new Color(0.13f, 0.15f, 0.17f, 1f);
            GUI.DrawTexture(new Rect(x + i * 24f, rect.y + 7f, 18f, 18f), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 2f);
        }
        x += 104f;

        if (GUI.Button(new Rect(x, rect.y + 4f, 72f, 26f), bpmDjLinkMode ? "DJ LINK" : "Link"))
        {
            EnableDjLinkBpmMode();
        }
        x += 78f;
        if (GUI.Button(new Rect(x, rect.y + 4f, 52f, 26f), "Tap"))
        {
            RegisterBpmTap();
        }
        x += 58f;

        var mode = bpmDjLinkMode && hasDjLinkBpm ? "master P" + master.DeviceNumber + " BPM only" : "manual";
        GUI.Label(new Rect(x, rect.y + 7f, Mathf.Max(80f, rect.xMax - x - 6f), 20f), mode, smallStyle);
    }

    private static PlayerState FindMasterPlayer(List<PlayerState> snapshotPlayers)
    {
        if (snapshotPlayers == null || snapshotPlayers.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < snapshotPlayers.Count; i++)
        {
            if (snapshotPlayers[i] != null && snapshotPlayers[i].IsTempoMaster)
            {
                return snapshotPlayers[i];
            }
        }

        for (var i = 0; i < snapshotPlayers.Count; i++)
        {
            if (snapshotPlayers[i] != null && snapshotPlayers[i].HasStatus)
            {
                return snapshotPlayers[i];
            }
        }

        return snapshotPlayers[0];
    }

    private static int DjLinkBeatIndex(PlayerState player)
    {
        if (player == null)
        {
            return 0;
        }

        if (player.BeatWithinBar >= 1 && player.BeatWithinBar <= 4)
        {
            return player.BeatWithinBar - 1;
        }

        return player.BeatNumber >= 0 ? player.BeatNumber % 4 : 0;
    }

    private int ManualBeatIndex(float bpm)
    {
        if (bpm <= 0.01f)
        {
            return manualBeatAnchorIndex;
        }

        var secondsPerBeat = 60f / bpm;
        var elapsedBeats = Mathf.FloorToInt(Mathf.Max(0f, Time.realtimeSinceStartup - manualBeatAnchorTime) / secondsPerBeat);
        return (manualBeatAnchorIndex + elapsedBeats) % 4;
    }

    private void RegisterBpmTap()
    {
        bpmDjLinkMode = false;
        var now = Time.realtimeSinceStartup;
        if (tapTimes.Count > 0 && now - tapTimes[tapTimes.Count - 1] > 2.5f)
        {
            tapTimes.Clear();
            manualBeatAnchorIndex = 0;
        }

        tapTimes.Add(now);
        while (tapTimes.Count > 8)
        {
            tapTimes.RemoveAt(0);
        }

        if (tapTimes.Count >= 2)
        {
            var intervalSum = 0f;
            var intervalCount = 0;
            for (var i = 1; i < tapTimes.Count; i++)
            {
                var interval = tapTimes[i] - tapTimes[i - 1];
                if (interval > 0.18f && interval < 2.5f)
                {
                    intervalSum += interval;
                    intervalCount++;
                }
            }
            if (intervalCount > 0)
            {
                manualBpm = Mathf.Clamp(60f / (intervalSum / intervalCount), 40f, 240f);
            }
        }

        manualBeatAnchorTime = now;
        manualBeatAnchorIndex = (manualBeatAnchorIndex + 1) % 4;
    }

    private void DrawCompositePreview(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "Program Preview", normalStyle);
        var preview = new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, (rect.width - 24f) * 9f / 16f);
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);

        if (vjLayers != null)
        {
            for (var i = 0; i < vjLayers.Length; i++)
            {
                var layer = vjLayers[i];
                var layerTexture = LayerPreviewTexture(i, layer);
                if (layer == null || !layer.Enabled || layerTexture == null || layer.SourceKind == LayerSourceKind.None)
                {
                    continue;
                }

                GUI.color = LayerBlendColor(layer);
                GUI.DrawTexture(preview, layerTexture, ScaleMode.ScaleToFit, true);
            }
            GUI.color = Color.white;
        }
    }

    private void DrawMiniCapturePreview(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "HDMI Preview", normalStyle);
        var preview = new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, Mathf.Max(80f, rect.height - 52f));
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);

        if (captureTexture != null && captureTexture.width > 16 && captureTexture.height > 16)
        {
            GUI.DrawTexture(FitRect(preview, captureTexture.width, captureTexture.height), captureTexture, ScaleMode.StretchToFill, false);
        }
        else
        {
            GUI.Label(new Rect(preview.x + 10f, preview.y + 10f, preview.width - 20f, 28f), string.IsNullOrEmpty(captureError) ? "Waiting for capture..." : captureError, smallStyle);
        }
    }

    private void DrawSourceWindow(Rect rect, StateSnapshot snapshot)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "Source  ->  " + LayerSlotLabel(selectedLayerIndex), normalStyle);
        var body = new Rect(rect.x + 12f, rect.y + 38f, rect.width - 24f, rect.height - 50f);
        GUILayout.BeginArea(body);
        if (!IsSourceLayerSlot(selectedLayerIndex))
        {
            GUILayout.Label("Select one of Layers 1-8 to load a source.", smallStyle);
            GUILayout.EndArea();
            return;
        }
        sourceScroll = GUILayout.BeginScrollView(sourceScroll);

        GUILayout.Label("Inputs", smallStyle);
        if (captureDevices != null && captureDevices.Length > 0)
        {
            for (var i = 0; i < captureDevices.Length; i++)
            {
                var label = CaptureSourceName(captureDevices[i], i);
                if (GUILayout.Button(label, GUILayout.Height(26f)))
                {
                    LoadCaptureIntoLayer(selectedLayerIndex, i);
                }
            }
        }
        else
        {
            GUILayout.Label("No HDMI capture", smallStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Spout", smallStyle);
        GUILayout.Label("No Spout receiver plugin", smallStyle);

        GUILayout.Space(6f);
        GUILayout.Label("YouTube", smallStyle);
        if (GUILayout.Button("YouTube", GUILayout.Height(28f)))
        {
            LoadYoutubeSourceIntoLayer(selectedLayerIndex);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Image", smallStyle);
        var imageCount = Mathf.Min(cachedImages.Count, 6);
        if (imageCount == 0)
        {
            GUILayout.Label("Open a folder to show PNG / JPG / JPEG images.", smallStyle);
        }
        else
        {
            for (var i = 0; i < imageCount; i++)
            {
                var imagePath = cachedImages[i];
                if (GUILayout.Button(Path.GetFileName(imagePath), GUILayout.Height(24f)))
                {
                    LoadImageIntoLayer(selectedLayerIndex, imagePath);
                }
            }
        }

        GUILayout.Space(6f);
        GUILayout.Label("Text", smallStyle);
        if (GUILayout.Button("Text", GUILayout.Height(28f)))
        {
            LoadTextIntoLayer(selectedLayerIndex, "TEXT", 96);
        }
        GUILayout.Label("Edit text, size, and font in Layer Settings (E).", smallStyle);

        GUILayout.Space(6f);
        DrawGeneratorSourceControls(snapshot);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawEffectWindow(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "Effect  ->  " + LayerSlotLabel(selectedLayerIndex), normalStyle);
        var body = new Rect(rect.x + 12f, rect.y + 38f, rect.width - 24f, rect.height - 50f);
        GUILayout.BeginArea(body);
        if (!IsEffectLayerSlot(selectedLayerIndex))
        {
            GUILayout.Label("Select Main FX or All FX to attach an effect.", smallStyle);
            GUILayout.EndArea();
            return;
        }
        effectScroll = GUILayout.BeginScrollView(effectScroll);

        GUILayout.Label("RGB Effect", smallStyle);
        DrawEffectAttachButton("RGB Effect", LayerEffectKind.RgbEffect);

        GUILayout.Space(6f);
        GUILayout.Label("Split", smallStyle);
        DrawEffectAttachButton("4 Split", LayerEffectKind.QuadSplit);

        GUILayout.Space(6f);
        GUILayout.Label("Blur", smallStyle);
        DrawEffectAttachButton("BPM Blur", LayerEffectKind.Blur);

        GUILayout.Space(6f);
        GUILayout.Label("Glitch", smallStyle);
        DrawEffectAttachButton("Glitch", LayerEffectKind.Glitch);

        GUILayout.Space(6f);
        GUILayout.Label("Strobe", smallStyle);
        DrawEffectAttachButton("Strobe", LayerEffectKind.Strobe);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawEffectBrowserPanel(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "Effect Faders  ->  " + LayerSlotLabel(selectedLayerIndex), normalStyle);

        var pad = 12f;
        var rowHeight = Mathf.Clamp(rect.height * 0.36f, 118f, 168f);
        var rowRect = new Rect(rect.x + pad, rect.y + 38f, rect.width - pad * 2f, rowHeight);
        DrawCombinedEffectLayerRow(rowRect);

        var bodyRect = new Rect(rect.x + pad, rowRect.yMax + 10f, rect.width - pad * 2f, rect.yMax - rowRect.yMax - 22f);
        GUILayout.BeginArea(bodyRect);
        if (!IsEffectLayerSlot(selectedLayerIndex))
        {
            GUILayout.Label("Select Main FX or All FX to attach and control an effect.", smallStyle);
            GUILayout.EndArea();
            return;
        }

        effectScroll = GUILayout.BeginScrollView(effectScroll);
        GUILayout.Label("Attach Effect", smallStyle);
        DrawEffectAttachButton("RGB Effect", LayerEffectKind.RgbEffect);
        GUILayout.Space(6f);
        DrawEffectAttachButton("4 Split", LayerEffectKind.QuadSplit);
        GUILayout.Space(6f);
        DrawEffectAttachButton("BPM Blur", LayerEffectKind.Blur);
        GUILayout.Space(6f);
        DrawEffectAttachButton("Glitch", LayerEffectKind.Glitch);
        GUILayout.Space(6f);
        DrawEffectAttachButton("Strobe", LayerEffectKind.Strobe);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawEffectAttachButton(string label, LayerEffectKind kind)
    {
        if (GUILayout.Button(label, GUILayout.Height(26f)))
        {
            AttachEffectToLayer(selectedLayerIndex, kind);
        }
    }

    private static string CaptureSourceName(WebCamDevice device, int index)
    {
        var name = device.name ?? "";
        var prefix = name.IndexOf("hdmi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("usb", StringComparison.OrdinalIgnoreCase) >= 0
            ? "HDMI Capture "
            : "Capture ";
        return prefix + (index + 1).ToString(CultureInfo.InvariantCulture) + "  " + name;
    }

    private void DrawGeneratorSourceControls(StateSnapshot snapshot)
    {
        GUILayout.Label("3D Object", smallStyle);
        if (GUILayout.Button("3D Object", GUILayout.Height(28f)))
        {
            var texture = LatestAlbumArtTexture(snapshot);
            var sourceName = "DJ Link Jacket";
            if (texture == null && cachedImages.Count > 0)
            {
                texture = LoadImageTexture(cachedImages[0]);
                sourceName = Path.GetFileName(cachedImages[0]);
            }
            if (texture == null)
            {
                texture = Texture2D.whiteTexture;
                sourceName = "White";
            }
            LoadGeneratorIntoLayer(selectedLayerIndex, texture, sourceName);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Sequence", smallStyle);
        if (GUILayout.Button("Step Sequencer", GUILayout.Height(28f)))
        {
            AttachEffectToLayer(selectedLayerIndex, LayerEffectKind.StepSequencer);
        }
    }

    private void DrawGeneratorDetailControls(GeneratorState generator, StateSnapshot snapshot)
    {
        if (generator == null)
        {
            return;
        }
        if (!string.IsNullOrEmpty(generator.FaceImageFolderPath) &&
            (generator.FaceImagePaths == null || generator.FaceImagePaths.Count == 0) &&
            string.IsNullOrEmpty(generator.FaceImageError))
        {
            RefreshGeneratorFaceImageFolder(generator);
        }

        GUILayout.Space(8f);
        GUILayout.Label("3D Object Detail", smallStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cube", GUILayout.Height(24f)))
        {
            SetGeneratorShape(generator, GeneratorShape.Cube);
        }
        if (GUILayout.Button("Tetra", GUILayout.Height(24f)))
        {
            SetGeneratorShape(generator, GeneratorShape.Tetrahedron);
        }
        if (GUILayout.Button("Dodeca", GUILayout.Height(24f)))
        {
            SetGeneratorShape(generator, GeneratorShape.Dodecahedron);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Scene Mode", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(generator.PresentationMode == GeneratorPresentationMode.AroundObject ? "> 3Dobject Center" : "3Dobject Center", GUILayout.Height(24f)))
        {
            generator.PresentationMode = GeneratorPresentationMode.AroundObject;
            if (generator.ObjectOrbitMode == GeneratorObjectOrbitMode.TunnelGrid)
            {
                generator.ObjectOrbitMode = GeneratorObjectOrbitMode.None;
            }
            if (generator.CameraWorkMode == GeneratorCameraWorkMode.TunnelForward)
            {
                generator.CameraWorkMode = GeneratorCameraWorkMode.Orbit;
            }
            SetGeneratorSceneLayersVisible(generator, false);
            SetGeneratorLightingVisible(generator, false);
            SetGeneratorSurroundScreensVisible(generator, generator.SurroundScreensEnabled && generator.SurroundScreenSourceLayerIndex >= 0);
            SetGeneratorParticlesVisible(generator, generator.ParticlesEnabled);
        }
        if (GUILayout.Button(generator.PresentationMode == GeneratorPresentationMode.Tunnel ? "> Tunnel" : "Tunnel", GUILayout.Height(24f)))
        {
            generator.PresentationMode = GeneratorPresentationMode.Tunnel;
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.TunnelGrid;
            generator.CameraWorkMode = GeneratorCameraWorkMode.TunnelForward;
            SetGeneratorSceneLayersVisible(generator, false);
            SetGeneratorLightingVisible(generator, false);
            SetGeneratorSurroundScreensVisible(generator, false);
            SetGeneratorParticlesVisible(generator, false);
        }
        if (GUILayout.Button(generator.PresentationMode == GeneratorPresentationMode.Lighting ? "> Lighting" : "Lighting", GUILayout.Height(24f)))
        {
            generator.PresentationMode = GeneratorPresentationMode.Lighting;
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.None;
            if (generator.CameraWorkMode == GeneratorCameraWorkMode.TunnelForward)
            {
                generator.CameraWorkMode = GeneratorCameraWorkMode.Orbit;
            }
            EnsureGeneratorLightingSceneSetup(generator);
            SetGeneratorSceneLayersVisible(generator, true);
            SetGeneratorLightingVisible(generator, true);
            SetGeneratorSurroundScreensVisible(generator, false);
            SetGeneratorParticlesVisible(generator, false);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Object Motion", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(generator.ObjectOrbitMode == GeneratorObjectOrbitMode.None ? "> None" : "None", GUILayout.Height(24f)))
        {
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.None;
        }
        if (generator.PresentationMode == GeneratorPresentationMode.AroundObject &&
            GUILayout.Button(generator.ObjectOrbitMode == GeneratorObjectOrbitMode.Circular ? "> Circular" : "Circular", GUILayout.Height(24f)))
        {
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.Circular;
        }
        if (generator.PresentationMode == GeneratorPresentationMode.AroundObject &&
            GUILayout.Button(generator.ObjectOrbitMode == GeneratorObjectOrbitMode.Spherical ? "> Spherical" : "Spherical", GUILayout.Height(24f)))
        {
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.Spherical;
        }
        if (generator.PresentationMode == GeneratorPresentationMode.Tunnel &&
            GUILayout.Button(generator.ObjectOrbitMode == GeneratorObjectOrbitMode.TunnelGrid ? "> Tunnel Grid" : "Tunnel Grid", GUILayout.Height(24f)))
        {
            generator.ObjectOrbitMode = GeneratorObjectOrbitMode.TunnelGrid;
        }
        GUILayout.EndHorizontal();
        if (generator.PresentationMode == GeneratorPresentationMode.Tunnel)
        {
            GUILayout.Label("Tunnel flow grid", smallStyle);
        }
        else if (generator.PresentationMode == GeneratorPresentationMode.Lighting)
        {
            GUILayout.Label("3Dobject fixed at scene center with moving lights around it.", smallStyle);
        }
        else
        {
            GUILayout.Label("Object orbit: 32 beats / rev", smallStyle);
        }
        generator.ObjectSpinEnabled = GUILayout.Toggle(generator.ObjectSpinEnabled, "Self spin");
        generator.ObjectBpmPulseEnabled = GUILayout.Toggle(generator.ObjectBpmPulseEnabled, "BPM size pulse");
        if (generator.PresentationMode == GeneratorPresentationMode.Tunnel)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Auto sequence: odd -> even -> odd -> even", smallStyle);
            GUILayout.Label("Axis changes between horizontal/vertical only and never repeats consecutively.", smallStyle);
        }
        else if (generator.PresentationMode == GeneratorPresentationMode.Lighting)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Preferred scene building: JapaneseOtakuCity. Falls back to current SceneBuildings_* if not found.", smallStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Moving Light Mode", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(generator.LightingRigMode == GeneratorLightingRigMode.AlternateBlink ? "> Alternate" : "Alternate", GUILayout.Height(24f)))
            {
                generator.LightingRigMode = GeneratorLightingRigMode.AlternateBlink;
            }
            if (GUILayout.Button(generator.LightingRigMode == GeneratorLightingRigMode.InOutSweep ? "> In/Out" : "In/Out", GUILayout.Height(24f)))
            {
                generator.LightingRigMode = GeneratorLightingRigMode.InOutSweep;
            }
            if (GUILayout.Button(generator.LightingRigMode == GeneratorLightingRigMode.TangentSweep ? "> Tangent" : "Tangent", GUILayout.Height(24f)))
            {
                generator.LightingRigMode = GeneratorLightingRigMode.TangentSweep;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Alternate: odd/even blink each beat. In/Out: radial sweep. Tangent: circumferential sweep.", smallStyle);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Camera Work", smallStyle);
        if (generator.PresentationMode == GeneratorPresentationMode.Tunnel)
        {
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.TunnelForward, "Tunnel forward");
        }
        else
        {
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.Orbit, "Orbit around object");
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.EaseIn, "Ease in");
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.EaseOut, "Ease out");
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.JumpOrbit, "Random jump");
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.JumpHold, "Jump + hold");
            DrawGeneratorCameraWorkButton(generator, GeneratorCameraWorkMode.ScriptTimeline, "Script timeline");
        }

        var currentBpm = Mathf.Max(1f, CurrentVisualBpm());
        GUILayout.Space(6f);
        GUILayout.Label("Current BPM: " + currentBpm.ToString("0.0", CultureInfo.InvariantCulture), smallStyle);
        if (generator.CameraWorkMode == GeneratorCameraWorkMode.JumpHold)
        {
            GUILayout.Label("Camera orbit: " + OrbitSecondsPerRevolution(currentBpm, GeneratorCityCameraOrbitBeatsPerRevolution).ToString("0.00", CultureInfo.InvariantCulture) + " sec / rev", smallStyle);
        }
        else if (generator.CameraWorkMode == GeneratorCameraWorkMode.Orbit)
        {
            GUILayout.Label("Camera orbit: continuous spherical path", smallStyle);
        }
        if (generator.ObjectOrbitMode != GeneratorObjectOrbitMode.None)
        {
            GUILayout.Label("Object orbit: " + OrbitSecondsPerRevolution(currentBpm, GeneratorCityObjectPathBeatsPerRevolution).ToString("0.00", CultureInfo.InvariantCulture) + " sec / rev", smallStyle);
        }

        if (generator.CameraWorkMode == GeneratorCameraWorkMode.ScriptTimeline)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Timeline Script", smallStyle);
            GUILayout.Label("Use beat keyframes: t=0 ease=inout objPos=0,0,0 objRot=0,0,0 objScale=1 camPos=0,0,-4 camRot=0,0,0 camFov=45", smallStyle);
            var nextText = GUILayout.TextArea(generator.ScriptText ?? "", GUILayout.MinHeight(190f));
            if (!string.Equals(nextText, generator.ScriptText, StringComparison.Ordinal))
            {
                generator.ScriptText = nextText;
                generator.ScriptDirty = true;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Script", GUILayout.Height(26f)))
            {
                generator.ScriptDirty = true;
                EnsureGeneratorScriptParsed(generator);
            }
            if (GUILayout.Button("Load Sample", GUILayout.Height(26f)))
            {
                generator.ScriptText = DefaultGeneratorScript();
                generator.ScriptDirty = true;
                EnsureGeneratorScriptParsed(generator);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(generator.ScriptError) ? "Script OK" : generator.ScriptError, smallStyle);
        }

        GUILayout.Space(10f);
        GUILayout.Label("Screen Display", smallStyle);
        var nextScreenVisible = GUILayout.Toggle(generator.ScreenVisible, "Show 3D object screen");
        if (nextScreenVisible != generator.ScreenVisible)
        {
            generator.ScreenVisible = nextScreenVisible;
            SetGeneratorVisible(generator, generator.ScreenVisible);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Particles", smallStyle);
        generator.ParticlesEnabled = GUILayout.Toggle(generator.ParticlesEnabled, "Enable falling tetra particles");

        GUILayout.Space(10f);
        GUILayout.Label("Skybox / Background", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(!generator.TransparentBackground ? "> Black" : "Black", GUILayout.Height(24f)))
        {
            generator.TransparentBackground = false;
            ApplyGeneratorCameraBackground(generator);
        }
        if (GUILayout.Button(generator.TransparentBackground ? "> Transparent" : "Transparent", GUILayout.Height(24f)))
        {
            generator.TransparentBackground = true;
            ApplyGeneratorCameraBackground(generator);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Skybox", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("None", GUILayout.Height(24f)))
        {
            SetGeneratorSkybox(generator, null);
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(86f), GUILayout.Height(24f)))
        {
            RefreshGeneratorResources();
        }
        GUILayout.EndHorizontal();
        if (generatorSkyboxMaterials.Length == 0)
        {
            GUILayout.Label("No skybox materials in Assets/Resources/Skyboxes.", smallStyle);
        }
        for (var i = 0; i < generatorSkyboxMaterials.Length; i++)
        {
            var material = generatorSkyboxMaterials[i];
            if (material == null)
            {
                continue;
            }

            DrawGeneratorTexturePreviewButton(GeneratorSkyboxPreviewTexture(material), (material == generator.SkyboxMaterial ? "> " : "  ") + material.name, 290f, () =>
            {
                SetGeneratorSkybox(generator, material);
            });
        }

        GUILayout.Space(10f);
        GUILayout.Label("FBX / Other Object", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Model", GUILayout.Height(24f)))
        {
            ClearGeneratorModel(generator);
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(86f), GUILayout.Height(24f)))
        {
            RefreshGeneratorResources();
        }
        GUILayout.EndHorizontal();
        if (generatorModelPrefabs.Length == 0)
        {
            GUILayout.Label("No model prefabs/FBX in Assets/Resources/Models.", smallStyle);
        }
        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab != null
                && (prefab.name.StartsWith("SceneBuildings_", StringComparison.OrdinalIgnoreCase)
                    || prefab.name.StartsWith("SceneGround_", StringComparison.OrdinalIgnoreCase)
                    || prefab.name.StartsWith("SceneTraffic_", StringComparison.OrdinalIgnoreCase)
                    || prefab.name.StartsWith("SceneVegetation_", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (prefab != null && GUILayout.Button((string.Equals(generator.ModelName, prefab.name, StringComparison.Ordinal) ? "> " : "  ") + prefab.name, GUILayout.Height(24f)))
            {
                LoadGeneratorModel(generator, prefab);
            }
        }
        GUILayout.Label("Model Transform", smallStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Pos", GUILayout.Width(44f));
        generator.ModelPositionInput = GUILayout.TextField(generator.ModelPositionInput ?? "0,0,0", GUILayout.Width(150f));
        GUILayout.Label("Rot", GUILayout.Width(44f));
        generator.ModelRotationInput = GUILayout.TextField(generator.ModelRotationInput ?? "0,0,0", GUILayout.Width(150f));
        GUILayout.Label("Scale", GUILayout.Width(52f));
        generator.ModelScaleInput = GUILayout.TextField(generator.ModelScaleInput ?? "1,1,1", GUILayout.Width(150f));
        if (GUILayout.Button("Apply", GUILayout.Width(70f)))
        {
            ApplyGeneratorModelTransform(generator);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.Label("Scene Layers", smallStyle);
        DrawGeneratorSceneLayerSection(generator, "Buildings", GeneratorSceneLayerKind.Buildings, "SceneBuildings_");
        DrawGeneratorSceneLayerSection(generator, "Ground", GeneratorSceneLayerKind.Ground, "SceneGround_");
        DrawGeneratorSceneLayerSection(generator, "Traffic", GeneratorSceneLayerKind.Traffic, "SceneTraffic_");
        DrawGeneratorSceneLayerSection(generator, "Vegetation", GeneratorSceneLayerKind.Vegetation, "SceneVegetation_");

        GUILayout.Space(10f);
        GUILayout.Label("Face Texture Input", smallStyle);
        GUILayout.Label("Layer output ignores that layer's opacity.", smallStyle);
        generator.FaceImageFolderInput = generator.FaceImageFolderInput ?? generator.FaceImageFolderPath ?? "";
        if (generator.FaceImagePaths == null)
        {
            generator.FaceImagePaths = new List<string>();
        }
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(310f));
        GUILayout.Label("Image Folder", smallStyle);
        generator.FaceImageFolderInput = GUILayout.TextField(generator.FaceImageFolderInput, GUILayout.Height(24f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Folder...", GUILayout.Width(90f), GUILayout.Height(24f)))
        {
            OpenGeneratorFaceImageFolderDialog(generator);
        }
        if (GUILayout.Button("Apply", GUILayout.Width(70f), GUILayout.Height(24f)))
        {
            generator.FaceImageFolderPath = generator.FaceImageFolderInput;
            RefreshGeneratorFaceImageFolder(generator);
            SaveGeneratorFaceFolderPreference(generator.FaceImageFolderPath);
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(80f), GUILayout.Height(24f)))
        {
            RefreshGeneratorFaceImageFolder(generator);
        }
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(generator.FaceImageError))
        {
            GUILayout.Label(generator.FaceImageError, smallStyle);
        }

        generator.FaceImageScroll = GUILayout.BeginScrollView(generator.FaceImageScroll, GUILayout.Height(360f));
        var artPlayers = CollectAlbumArtPlayers(snapshot);
        for (var i = 0; i < artPlayers.Count; i++)
        {
            var player = artPlayers[i];
            var jacketLabel = "BLT Jacket P" + player.DeviceNumber.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(player.BltTitle))
            {
                jacketLabel += "  " + player.BltTitle;
            }
            var artTexture = player.AlbumArtTexture;
            DrawGeneratorTexturePreviewButton(artTexture, jacketLabel, 290f, () =>
            {
                ClearGeneratorLayerTextureSource(generator);
                SetGeneratorBltJacketSource(generator, player.DeviceNumber);
                ApplyGeneratorTexture(generator, artTexture);
            });
        }
        for (var i = 0; i < generator.FaceImagePaths.Count; i++)
        {
            var imagePath = generator.FaceImagePaths[i];
            var imageTexture = LoadImageTexture(imagePath);
            if (imageTexture == null)
            {
                continue;
            }
            var label = Path.GetFileName(imagePath);
            DrawGeneratorTexturePreviewButton(imageTexture, label, 290f, () =>
            {
                ClearGeneratorLayerTextureSource(generator);
                SetGeneratorManualImageSource(generator, imagePath);
                ApplyGeneratorTexture(generator, imageTexture);
            });
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(10f);
        GUILayout.BeginVertical();
        if (GUILayout.Button(generator.TextureSourceLayerIndex < 0 ? "> Manual / Jacket / Image" : "Manual / Jacket / Image", GUILayout.Height(24f)))
        {
            ClearGeneratorLayerTextureSource(generator);
        }
        for (var layerIndex = 0; layerIndex < VjLayerCount; layerIndex++)
        {
            if (layerIndex == selectedLayerIndex)
            {
                continue;
            }

            var sourceLayer = vjLayers == null || layerIndex >= vjLayers.Length ? null : vjLayers[layerIndex];
            if (sourceLayer == null || sourceLayer.SourceKind == LayerSourceKind.None)
            {
                continue;
            }

            var selected = generator.TextureSourceLayerIndex == layerIndex;
            var label = (selected ? "> " : "  ") + LayerSlotLabel(layerIndex) + "  " + (string.IsNullOrEmpty(sourceLayer.SourceName) ? sourceLayer.SourceKind.ToString() : sourceLayer.SourceName);
            if (GUILayout.Button(label, GUILayout.Height(24f)))
            {
                SetGeneratorLayerTextureSource(generator, layerIndex);
            }
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.Label("3DObject Screens", smallStyle);
        generator.SurroundScreensEnabled = GUILayout.Toggle(generator.SurroundScreensEnabled, "Enable surround screens");
        GUILayout.Label("Screen inputs ignore source layer opacity.", smallStyle);
        if (GUILayout.Button(generator.SurroundScreenSourceLayerIndex < 0 ? "> No screen source" : "No screen source", GUILayout.Height(24f)))
        {
            generator.SurroundScreenSourceLayerIndex = -1;
            generator.SurroundScreenSourceLayerName = null;
            generator.SurroundScreenCurrentTexture = null;
            UpdateGeneratorSurroundScreens(generator, BaseGeneratorObjectPosition());
        }
        for (var layerIndex = 0; layerIndex < VjLayerCount; layerIndex++)
        {
            if (layerIndex == selectedLayerIndex)
            {
                continue;
            }

            var sourceLayer = vjLayers == null || layerIndex >= vjLayers.Length ? null : vjLayers[layerIndex];
            if (sourceLayer == null || sourceLayer.SourceKind == LayerSourceKind.None)
            {
                continue;
            }

            var selected = generator.SurroundScreenSourceLayerIndex == layerIndex;
            var label = (selected ? "> " : "  ") + LayerSlotLabel(layerIndex) + "  " + (string.IsNullOrEmpty(sourceLayer.SourceName) ? sourceLayer.SourceKind.ToString() : sourceLayer.SourceName);
            if (GUILayout.Button(label, GUILayout.Height(24f)))
            {
                generator.SurroundScreensEnabled = true;
                generator.SurroundScreenSourceLayerIndex = layerIndex;
                generator.SurroundScreenSourceLayerName = LayerSlotLabel(layerIndex);
                generator.SurroundScreenCurrentTexture = null;
                UpdateGeneratorSurroundScreens(generator, BaseGeneratorObjectPosition());
            }
        }
    }

    private void DrawGeneratorSceneLayerSection(GeneratorState generator, string label, GeneratorSceneLayerKind kind, string prefix)
    {
        GUILayout.Label(label, smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("None", GUILayout.Height(24f)))
        {
            ClearGeneratorSceneLayer(generator, kind);
        }
        GUILayout.EndHorizontal();

        var found = 0;
        for (var i = 0; i < generatorModelPrefabs.Length; i++)
        {
            var prefab = generatorModelPrefabs[i];
            if (prefab == null || !prefab.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found++;
            var selected = string.Equals(GetGeneratorSceneLayerName(generator, kind), prefab.name, StringComparison.Ordinal);
            if (GUILayout.Button((selected ? "> " : "  ") + prefab.name, GUILayout.Height(24f)))
            {
                LoadGeneratorSceneLayer(generator, prefab, kind);
            }
        }

        if (found == 0)
        {
            GUILayout.Label("No " + label + " models found with prefix " + prefix + ".", smallStyle);
        }
    }

    private void DrawGeneratorCameraWorkButton(GeneratorState generator, GeneratorCameraWorkMode mode, string label)
    {
        var selected = generator != null && generator.CameraWorkMode == mode;
        if (GUILayout.Button((selected ? "> " : "  ") + label, GUILayout.Height(24f)) && generator != null)
        {
            generator.CameraWorkMode = mode;
            generator.MotionSeed = UnityEngine.Random.value * 1000f;
            if (mode == GeneratorCameraWorkMode.ScriptTimeline)
            {
                generator.ScriptDirty = true;
                EnsureGeneratorScriptParsed(generator);
            }
        }
    }

    private void DrawGeneratorTexturePreviewButton(Texture texture, string label, float width, Action onClick)
    {
        var rect = GUILayoutUtility.GetRect(width, 82f, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.09f, 0.1f, 1f), 0f, 4f);
        var buttonPressed = GUI.Button(rect, GUIContent.none);
        var thumbRect = new Rect(rect.x + 6f, rect.y + 6f, 70f, 70f);
        GUI.DrawTexture(thumbRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
        if (texture != null)
        {
            GUI.DrawTexture(thumbRect, texture, ScaleMode.ScaleToFit, false);
        }
        GUI.Label(new Rect(thumbRect.xMax + 8f, rect.y + 8f, rect.width - 92f, rect.height - 16f), label, smallStyle);
        if (buttonPressed && onClick != null)
        {
            onClick();
        }
    }

    private Texture GeneratorSkyboxPreviewTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        var propertyNames = new[] { "_MainTex", "_FrontTex", "_Tex", "_RightTex", "_LeftTex", "_UpTex", "_DownTex", "_BackTex" };
        for (var i = 0; i < propertyNames.Length; i++)
        {
            var propertyName = propertyNames[i];
            if (material.HasProperty(propertyName))
            {
                var texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private Texture2D CaptureTextureSnapshot(Texture source, int width, int height)
    {
        if (source == null || width <= 0 || height <= 0)
        {
            return null;
        }

        var temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, temp);
            RenderTexture.active = temp;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            texture.Apply(false, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch
        {
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
        }
    }

    private void DrawGeneratorPresetPanel(Rect rect, LayerState layer, StateSnapshot snapshot)
    {
        if (layer == null || layer.Generator == null)
        {
            return;
        }

        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);
        var generator = layer.Generator;
        var pad = 12f;
        GUI.Label(new Rect(rect.x + pad, rect.y + 8f, rect.width - pad * 2f, 24f), "3D Presets", normalStyle);
        generator.PresetNameInput = GUI.TextField(new Rect(rect.x + pad, rect.y + 36f, rect.width - 120f, 24f), generator.PresetNameInput ?? "Preset");
        if (GUI.Button(new Rect(rect.x + rect.width - 96f, rect.y + 34f, 84f, 28f), "Save"))
        {
            SaveGeneratorPreset(generator, layer);
        }

        var viewRect = new Rect(rect.x + pad, rect.y + 68f, rect.width - pad * 2f, rect.height - 80f);
        var contentWidth = Mathf.Max(0f, viewRect.width - 18f);
        var columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / 128f));
        var cellWidth = columns <= 0 ? contentWidth : Mathf.Floor((contentWidth - (columns - 1) * 8f) / columns);
        var cellHeight = cellWidth * 9f / 16f + 36f;
        var rows = Mathf.Max(1, Mathf.CeilToInt(generatorPresets.Count / (float)columns));
        var contentHeight = rows * (cellHeight + 8f);
        var scrollRect = new Rect(0f, 0f, contentWidth, contentHeight);
        generator.PresetScroll = GUI.BeginScrollView(viewRect, generator.PresetScroll, scrollRect, false, true);

        for (var i = 0; i < generatorPresets.Count; i++)
        {
            var preset = generatorPresets[i];
            if (preset == null)
            {
                continue;
            }

            var column = i % columns;
            var row = i / columns;
            var x = column * (cellWidth + 8f);
            var y = row * (cellHeight + 8f);
            var buttonRect = new Rect(x, y, cellWidth, cellHeight);
            GUI.DrawTexture(buttonRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.09f, 0.10f, 1f), 0f, 4f);
            var thumbRect = new Rect(buttonRect.x + 4f, buttonRect.y + 4f, buttonRect.width - 8f, Mathf.Max(40f, buttonRect.height - 34f));
            GUI.DrawTexture(thumbRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
            if (preset.ThumbnailTexture != null)
            {
                GUI.DrawTexture(thumbRect, preset.ThumbnailTexture, ScaleMode.ScaleToFit, false);
            }
            GUI.Label(new Rect(buttonRect.x + 6f, thumbRect.yMax + 4f, buttonRect.width - 12f, 28f), preset.Name, smallStyle);

            if (midiEditMode)
            {
                DrawMidiLearnOverlay(buttonRect, MidiBindingAction.GeneratorPresetSelect, i, preset.Name);
            }
            else if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
            {
                ApplyGeneratorPreset(generator, preset, snapshot);
            }
        }

        GUI.EndScrollView();
    }

    private void DrawMediaBrowser(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.03f, 0.035f, 0.04f, 1f), 0f, 6f);

        var pad = 12f;
        var top = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, 30f);
        if (GUI.Button(new Rect(top.x, top.y, 112f, top.height), "Import..."))
        {
            OpenRootFolderDialog();
        }
        if (GUI.Button(new Rect(top.x + 120f, top.y, 92f, top.height), "New List"))
        {
            CreateMediaPointerList();
        }
        if (GUI.Button(new Rect(top.x + 220f, top.y, 92f, top.height), "Delete List"))
        {
            DeleteSelectedMediaPointerList();
        }
        if (GUI.Button(new Rect(top.x + 320f, top.y, 76f, top.height), mediaBrowserMode == MediaBrowserMode.Videos ? "> Video" : "Video"))
        {
            mediaBrowserMode = MediaBrowserMode.Videos;
            mediaPageIndex = 0;
            mediaFileScroll = Vector2.zero;
            browserPreviewSlotsDirty = true;
        }
        if (GUI.Button(new Rect(top.x + 404f, top.y, 76f, top.height), mediaBrowserMode == MediaBrowserMode.Images ? "> Image" : "Image"))
        {
            mediaBrowserMode = MediaBrowserMode.Images;
            mediaPageIndex = 0;
            mediaFileScroll = Vector2.zero;
        }
        if (mediaBrowserMode == MediaBrowserMode.Videos)
        {
            var filterX = top.x + 488f;
            if (GUI.Button(new Rect(filterX, top.y, 60f, top.height), videoBrowserFilter == VideoBrowserFilter.All ? "> All" : "All"))
            {
                videoBrowserFilter = VideoBrowserFilter.All;
                mediaFileScroll = Vector2.zero;
            }
            if (GUI.Button(new Rect(filterX + 68f, top.y, 64f, top.height), videoBrowserFilter == VideoBrowserFilter.Mp4Only ? "> MP4" : "MP4"))
            {
                videoBrowserFilter = VideoBrowserFilter.Mp4Only;
                mediaFileScroll = Vector2.zero;
            }
            if (GUI.Button(new Rect(filterX + 140f, top.y, 64f, top.height), videoBrowserFilter == VideoBrowserFilter.MovOnly ? "> MOV" : "MOV"))
            {
                videoBrowserFilter = VideoBrowserFilter.MovOnly;
                mediaFileScroll = Vector2.zero;
            }
            GUI.Label(new Rect(filterX + 214f, top.y + 5f, top.width - 214f, 20f), "Root: " + ShortPath(mediaRootPath), smallStyle);
        }
        else
        {
            GUI.Label(new Rect(top.x + 488f, top.y + 5f, top.width - 488f, 20f), "Root: " + ShortPath(mediaRootPath), smallStyle);
        }
        if (mediaScanInFlight)
        {
            GUI.Label(new Rect(top.x + top.width - 180f, top.y + 5f, 170f, 20f), "Scanning...", smallStyle);
        }
        var summaryRect = new Rect(rect.x + pad, top.yMax + 6f, rect.width - pad * 2f, 20f);
        GUI.Label(summaryRect, "Collection import. Right click thumbnails to add selected items to a list.", smallStyle);

        var body = new Rect(rect.x + pad, summaryRect.yMax + 6f, rect.width - pad * 2f, rect.height - top.height - pad * 2f - 36f);
        var listWidth = Mathf.Clamp(body.width * 0.24f, 220f, 320f);
        DrawMediaPointerLists(new Rect(body.x, body.y, listWidth, body.height));
        var fileRect = new Rect(body.x + listWidth + 10f, body.y, body.width - listWidth - 10f, body.height);
        if (mediaBrowserMode == MediaBrowserMode.Images)
        {
            DrawImageFileGrid(fileRect);
        }
        else
        {
            DrawVideoFileGrid(fileRect);
        }

        DrawMediaContextMenu(rect);
    }

    private void DrawMediaPointerLists(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.023f, 0.026f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        GUILayout.Label("Lists", normalStyle);
        scroll = GUILayout.BeginScrollView(scroll);
        EnsureDefaultMediaPointerList();
        for (var i = 0; i < mediaPointerLists.Count; i++)
        {
            var list = mediaPointerLists[i];
            if (list == null)
            {
                continue;
            }

            var selected = selectedMediaPointerListIndex == i;
            var label = (selected ? "> " : "") + list.Name;
            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                selectedMediaPointerListIndex = i;
                mediaFileScroll = Vector2.zero;
                selectedBrowserMediaPaths.Clear();
                mediaSelectionAnchorIndex = -1;
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void OpenRootPicker()
    {
        rootPickerPath = Directory.Exists(mediaCurrentFolder) ? mediaCurrentFolder :
                         Directory.Exists(mediaRootPath) ? mediaRootPath :
                         ChooseDefaultMediaRoot();
        mediaRootInput = rootPickerPath;
        rootPickerError = null;
        rootPickerScroll = Vector2.zero;
        rootPickerOpen = true;
    }

    private void DrawRootFolderPicker(Rect full)
    {
        GUI.DrawTexture(full, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.58f), 0f, 0f);

        var width = Mathf.Min(780f, full.width - 60f);
        var height = Mathf.Min(620f, full.height - 60f);
        var panel = new Rect((full.width - width) * 0.5f, (full.height - height) * 0.5f, width, height);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.035f, 0.04f, 0.046f, 1f), 0f, 6f);

        var pad = 16f;
        var x = panel.x + pad;
        var y = panel.y + pad;
        var w = panel.width - pad * 2f;
        GUI.Label(new Rect(x, y, w - 80f, 28f), "Select Root Folder", titleStyle);
        if (GUI.Button(new Rect(panel.xMax - pad - 70f, y, 70f, 28f), "Cancel"))
        {
            rootPickerOpen = false;
            return;
        }

        y += 38f;
        mediaRootInput = GUI.TextField(new Rect(x, y, w - 64f, 28f), string.IsNullOrEmpty(mediaRootInput) ? rootPickerPath : mediaRootInput);
        if (GUI.Button(new Rect(x + w - 56f, y, 56f, 28f), "Go"))
        {
            NavigateRootPicker(mediaRootInput);
        }

        y += 38f;
        if (GUI.Button(new Rect(x, y, 130f, 30f), "Use This Folder"))
        {
            if (Directory.Exists(rootPickerPath))
            {
                SetMediaRoot(rootPickerPath);
                rootPickerOpen = false;
                return;
            }
            rootPickerError = "Folder not found.";
        }
        if (GUI.Button(new Rect(x + 140f, y, 76f, 30f), "Parent"))
        {
            var parent = Directory.Exists(rootPickerPath) ? Directory.GetParent(rootPickerPath) : null;
            if (parent != null)
            {
                NavigateRootPicker(parent.FullName);
            }
        }
        GUI.Label(new Rect(x + 226f, y + 6f, w - 226f, 22f), ShortPath(rootPickerPath), smallStyle);

        y += 40f;
        DrawRootPickerDrives(new Rect(x, y, w, 34f));
        y += 42f;

        if (!string.IsNullOrEmpty(rootPickerError))
        {
            GUI.Label(new Rect(x, y, w, 22f), rootPickerError, smallStyle);
            y += 24f;
        }

        var listRect = new Rect(x, y, w, panel.yMax - y - pad);
        GUI.DrawTexture(listRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.018f, 0.021f, 0.024f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(listRect.x + 8f, listRect.y + 8f, listRect.width - 16f, listRect.height - 16f));
        rootPickerScroll = GUILayout.BeginScrollView(rootPickerScroll);
        DrawRootPickerFolderList();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawRootPickerDrives(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.022f, 0.025f, 0.029f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Drives", smallStyle, GUILayout.Width(48f));
        try
        {
            var drives = DriveInfo.GetDrives();
            for (var i = 0; i < drives.Length; i++)
            {
                var drive = drives[i];
                var label = drive.Name;
                if (!drive.IsReady)
                {
                    label += " offline";
                }
                if (GUILayout.Button(label, GUILayout.Height(24f), GUILayout.Width(92f)))
                {
                    NavigateRootPicker(drive.RootDirectory.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            GUILayout.Label(ex.Message, smallStyle);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawRootPickerFolderList()
    {
        if (string.IsNullOrEmpty(rootPickerPath) || !Directory.Exists(rootPickerPath))
        {
            GUILayout.Label("Select a drive or enter a folder path.", normalStyle);
            return;
        }

        try
        {
            var folders = Directory.GetDirectories(rootPickerPath);
            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
            if (folders.Length == 0)
            {
                GUILayout.Label("No subfolders.", normalStyle);
                return;
            }

            var limit = Math.Min(folders.Length, 400);
            for (var i = 0; i < limit; i++)
            {
                var path = folders[i];
                var name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name))
                {
                    name = path;
                }
                if (GUILayout.Button(name, GUILayout.Height(26f)))
                {
                    NavigateRootPicker(path);
                }
            }
            if (folders.Length > limit)
            {
                GUILayout.Label("Showing first " + limit.ToString(CultureInfo.InvariantCulture) + " folders.", smallStyle);
            }
        }
        catch (Exception ex)
        {
            rootPickerError = ex.Message;
            GUILayout.Label(rootPickerError, normalStyle);
        }
    }

    private void NavigateRootPicker(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            rootPickerError = "Folder not found.";
            return;
        }

        rootPickerPath = Path.GetFullPath(path);
        mediaRootInput = rootPickerPath;
        rootPickerError = null;
        rootPickerScroll = Vector2.zero;
    }

    private void DrawFolderTree(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.023f, 0.026f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f));
        mediaTreeScroll = GUILayout.BeginScrollView(mediaTreeScroll);
        var visited = 0;
        DrawFolderTreeNode(mediaRootPath, 0, ref visited);
        if (visited >= 2000)
        {
            GUILayout.Label("Showing first 2000 folders.", smallStyle);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawFolderTreeNode(string path, int indent, ref int visited)
    {
        if (string.IsNullOrEmpty(path) || visited >= 2000)
        {
            return;
        }

        visited++;
        DrawFolderButton(path, indent);

        if (!expandedMediaFolders.Contains(path))
        {
            return;
        }

        var folders = GetCachedChildFolders(path);
        if (folders == null)
        {
            return;
        }

        for (var i = 0; i < folders.Length && visited < 2000; i++)
        {
            DrawFolderTreeNode(folders[i], indent + 1, ref visited);
        }
    }

    private void DrawFolderButton(string path, int indent)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indent * 16f);
        var hasChildren = FolderHasChildrenCached(path);

        if (hasChildren)
        {
            var isExpanded = expandedMediaFolders.Contains(path);
            if (GUILayout.Button(isExpanded ? "v" : ">", GUILayout.Width(22f), GUILayout.Height(24f)))
            {
                if (isExpanded)
                {
                    expandedMediaFolders.Remove(path);
                }
                else
                {
                    expandedMediaFolders.Add(path);
                }
            }
        }
        else
        {
            GUILayout.Space(26f);
        }

        var name = indent == 0 ? "[root] " + Path.GetFileName(path) : Path.GetFileName(path);
        if (string.IsNullOrEmpty(name) || name == "[root] ")
        {
            name = path;
        }
        var isCurrent = string.Equals(Path.GetFullPath(path), Path.GetFullPath(mediaCurrentFolder ?? ""), StringComparison.OrdinalIgnoreCase);
        var previousColor = GUI.color;
        if (isCurrent)
        {
            GUI.color = new Color(0.42f, 0.78f, 1f, 1f);
            name = "> " + name;
        }
        if (GUILayout.Button(name, GUILayout.Height(24f)))
        {
            ExpandMediaFolderPath(path);
            SetMediaCurrentFolder(path);
        }
        GUI.color = previousColor;
        GUILayout.EndHorizontal();
    }

    private string[] GetCachedChildFolders(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string[] cached;
        if (mediaFolderChildrenCache.TryGetValue(path, out cached))
        {
            return cached;
        }

        try
        {
            cached = Directory.GetDirectories(path);
            Array.Sort(cached, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            cached = Array.Empty<string>();
        }

        mediaFolderChildrenCache[path] = cached;
        mediaFolderHasChildrenCache[path] = cached.Length > 0;
        return cached;
    }

    private bool FolderHasChildrenCached(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        bool cached;
        if (mediaFolderHasChildrenCache.TryGetValue(path, out cached))
        {
            return cached;
        }

        return GetCachedChildFolders(path).Length > 0;
    }

    private string BuildMediaFolderBreadcrumb()
    {
        if (string.IsNullOrEmpty(mediaRootPath) || string.IsNullOrEmpty(mediaCurrentFolder))
        {
            return "";
        }

        try
        {
            var root = Path.GetFullPath(mediaRootPath);
            var current = Path.GetFullPath(mediaCurrentFolder);
            if (!current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return ShortPath(current);
            }

            var relative = current.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootName))
            {
                rootName = root;
            }
            if (string.IsNullOrEmpty(relative))
            {
                return rootName;
            }

            var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return rootName + " > " + string.Join(" > ", parts);
        }
        catch
        {
            return ShortPath(mediaCurrentFolder);
        }
    }

    private void DrawVideoFileGrid(Rect rect)
    {
        var filteredVideos = DisplayedVideos();
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.023f, 0.026f, 1f), 0f, 4f);
        var inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
        GUI.BeginGroup(inner);

        var y = 0f;
        if (!string.IsNullOrEmpty(mediaBrowserError))
        {
            GUI.Label(new Rect(0f, y, inner.width, 22f), mediaBrowserError, normalStyle);
            y += 24f;
        }
        GUI.Label(new Rect(0f, y, inner.width, 18f), filteredVideos.Count.ToString(CultureInfo.InvariantCulture) + " videos", smallStyle);
        y += 20f;
        GUI.Label(
            new Rect(0f, y, inner.width, 18f),
            IsSourceLayerSlot(selectedLayerIndex)
                ? "Click to load into " + LayerSlotLabel(selectedLayerIndex)
                : "Select one of Layers 1-8 to load video files.  Shift/Ctrl select supported.",
            smallStyle);
        y += 22f;

        var scrollRect = new Rect(0f, y, inner.width, inner.height - y);
        var columns = Mathf.Min(8, Mathf.Max(4, Mathf.FloorToInt((rect.width - 20f) / 110f)));
        var tileWidth = Mathf.Max(92f, (rect.width - 36f - (columns - 1) * 8f) / columns);
        var tileHeight = tileWidth * 9f / 16f + 26f;
        var rowHeight = tileHeight + 8f;
        var totalRows = Mathf.CeilToInt(filteredVideos.Count / (float)columns);
        var viewHeight = Mathf.Max(scrollRect.height, totalRows * rowHeight);
        var viewRect = new Rect(0f, 0f, scrollRect.width - 18f, viewHeight);

        visibleBrowserVideoPaths.Clear();
        mediaFileScroll = GUI.BeginScrollView(scrollRect, mediaFileScroll, viewRect);
        var startRow = Mathf.Max(0, Mathf.FloorToInt(mediaFileScroll.y / Mathf.Max(1f, rowHeight)) - 1);
        var visibleRows = Mathf.CeilToInt(scrollRect.height / Mathf.Max(1f, rowHeight)) + 2;
        var endRow = Mathf.Min(totalRows, startRow + visibleRows);

        for (var row = startRow; row < endRow; row++)
        {
            var rowY = row * rowHeight;
            for (var col = 0; col < columns; col++)
            {
                var index = row * columns + col;
                if (index >= filteredVideos.Count)
                {
                    break;
                }

                var path = filteredVideos[index];
                visibleBrowserVideoPaths.Add(path);
                var tileRect = new Rect(col * (tileWidth + 8f), rowY, tileWidth, tileHeight);
                DrawVideoTile(path, tileRect, filteredVideos, index);
            }
        }

        GUI.EndScrollView();
        GUI.EndGroup();
    }

    private void DrawImageFileGrid(Rect rect)
    {
        var images = DisplayedImages();
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.023f, 0.026f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        if (!string.IsNullOrEmpty(mediaBrowserError))
        {
            GUILayout.Label(mediaBrowserError, normalStyle);
        }
        GUILayout.Label(images.Count.ToString(CultureInfo.InvariantCulture) + " images", smallStyle);
        GUILayout.Label(
            IsSourceLayerSlot(selectedLayerIndex)
                ? "PNG / JPG / JPEG  -> click to load into " + LayerSlotLabel(selectedLayerIndex)
                : "Select one of Layers 1-8 to load image files.  Shift/Ctrl select supported.",
            smallStyle);

        mediaFileScroll = GUILayout.BeginScrollView(mediaFileScroll);
        var columns = Mathf.Max(2, Mathf.FloorToInt((rect.width - 28f) / 190f));
        var tileWidth = Mathf.Max(150f, (rect.width - 36f - (columns - 1) * 10f) / columns);
        for (var i = 0; i < images.Count; i += columns)
        {
            GUILayout.BeginHorizontal();
            for (var col = 0; col < columns; col++)
            {
                var index = i + col;
                if (index < images.Count)
                {
                    DrawImageTile(images[index], tileWidth, images, index);
                }
                else
                {
                    GUILayout.Space(tileWidth + 10f);
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawVideoTile(string path, Rect rect, List<string> visiblePaths, int visibleIndex)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.085f, 0.09f, 1f), 0f, 4f);
        if (selectedBrowserMediaPaths.Contains(path))
        {
            DrawRectOutline(rect, 3f, new Color(0.24f, 0.76f, 1f, 0.95f));
        }
        var stepQueueIndex = GetStepSequencerQueueIndexForMediaPath(selectedLayerIndex, path);
        if (stepQueueIndex >= 0)
        {
            DrawRectOutline(rect, 3f, new Color(0.94f, 0.16f, 0.16f, 0.98f));
            DrawStepSequencerQueueBadge(rect, stepQueueIndex + 1);
        }

        var preview = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, (rect.width - 8f) * 9f / 16f);
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
        var texture = BrowserPreviewTexture(path);
        if (texture != null)
        {
            GUI.DrawTexture(preview, texture, ScaleMode.ScaleToFit, false);
        }
        else
        {
            GUI.Label(new Rect(preview.x + 6f, preview.y + 6f, preview.width - 12f, 20f), Path.GetExtension(path).Trim('.').ToUpperInvariant(), smallStyle);
        }

        GUI.Label(new Rect(rect.x + 4f, preview.yMax + 2f, rect.width - 8f, 20f), ShortFileName(Path.GetFileName(path), rect.width < 120f ? 12 : 18), smallStyle);
        var evt = Event.current;
        if (evt != null && evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            if (evt.button == 1)
            {
                HandleBrowserMediaSelection(path, visiblePaths, visibleIndex, true);
                OpenBrowserContextMenu(evt.mousePosition);
                evt.Use();
                return;
            }

            if (evt.button == 0)
            {
                if (IsStepSequencerShiftAssignMode())
                {
                    ToggleStepSequencerMediaQueueEntry(selectedLayerIndex, path);
                    evt.Use();
                    return;
                }

                HandleBrowserMediaSelection(path, visiblePaths, visibleIndex, false);
                evt.Use();
                if (IsSourceLayerSlot(selectedLayerIndex))
                {
                    LoadVideoIntoLayer(selectedLayerIndex, path);
                }
            }
        }
    }

    private void DrawImageTile(string path, float width, List<string> visiblePaths, int visibleIndex)
    {
        var height = width * 9f / 16f + 32f;
        var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.08f, 0.085f, 0.09f, 1f), 0f, 4f);
        if (selectedBrowserMediaPaths.Contains(path))
        {
            DrawRectOutline(rect, 3f, new Color(0.24f, 0.76f, 1f, 0.95f));
        }
        var stepQueueIndex = GetStepSequencerQueueIndexForMediaPath(selectedLayerIndex, path);
        if (stepQueueIndex >= 0)
        {
            DrawRectOutline(rect, 3f, new Color(0.94f, 0.16f, 0.16f, 0.98f));
            DrawStepSequencerQueueBadge(rect, stepQueueIndex + 1);
        }

        var preview = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, (rect.width - 8f) * 9f / 16f);
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 3f);
        var texture = LoadImageTexture(path);
        if (texture != null)
        {
            GUI.DrawTexture(preview, texture, ScaleMode.ScaleToFit, false);
        }
        else
        {
            GUI.Label(new Rect(preview.x + 8f, preview.y + 8f, preview.width - 16f, 24f), "image", smallStyle);
        }

        GUI.Label(new Rect(rect.x + 6f, preview.yMax + 4f, rect.width - 12f, 24f), Path.GetFileName(path), smallStyle);
        var evt = Event.current;
        if (evt != null && evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            if (evt.button == 1)
            {
                HandleBrowserMediaSelection(path, visiblePaths, visibleIndex, true);
                OpenBrowserContextMenu(evt.mousePosition);
                evt.Use();
            }
            else if (evt.button == 0)
            {
                if (IsStepSequencerShiftAssignMode())
                {
                    ToggleStepSequencerMediaQueueEntry(selectedLayerIndex, path);
                    evt.Use();
                    return;
                }

                HandleBrowserMediaSelection(path, visiblePaths, visibleIndex, false);
                evt.Use();
                if (IsSourceLayerSlot(selectedLayerIndex))
                {
                    LoadImageIntoLayer(selectedLayerIndex, path);
                }
            }
        }
        GUILayout.Space(10f);
    }

    private void HandleBrowserMediaSelection(string path, List<string> visiblePaths, int visibleIndex, bool preserveMultiSelection)
    {
        var evt = Event.current;
        if (evt == null || string.IsNullOrEmpty(path))
        {
            return;
        }

        if (evt.shift && mediaSelectionAnchorIndex >= 0 && visiblePaths != null && visibleIndex >= 0)
        {
            selectedBrowserMediaPaths.Clear();
            var start = Math.Min(mediaSelectionAnchorIndex, visibleIndex);
            var end = Math.Max(mediaSelectionAnchorIndex, visibleIndex);
            for (var i = start; i <= end && i < visiblePaths.Count; i++)
            {
                selectedBrowserMediaPaths.Add(visiblePaths[i]);
            }
            return;
        }

        if (preserveMultiSelection)
        {
            if (!selectedBrowserMediaPaths.Contains(path))
            {
                selectedBrowserMediaPaths.Clear();
                selectedBrowserMediaPaths.Add(path);
            }
            mediaSelectionAnchorIndex = visibleIndex;
            return;
        }

        if (evt.control || evt.command)
        {
            if (!selectedBrowserMediaPaths.Add(path))
            {
                selectedBrowserMediaPaths.Remove(path);
            }
            mediaSelectionAnchorIndex = visibleIndex;
            return;
        }

        selectedBrowserMediaPaths.Clear();
        selectedBrowserMediaPaths.Add(path);
        mediaSelectionAnchorIndex = visibleIndex;
    }

    private void OpenBrowserContextMenu(Vector2 mousePosition)
    {
        browserContextMenuTargets.Clear();
        foreach (var path in selectedBrowserMediaPaths)
        {
            browserContextMenuTargets.Add(path);
        }
        if (browserContextMenuTargets.Count == 0)
        {
            return;
        }

        browserContextMenuRect = new Rect(mousePosition.x + 8f, mousePosition.y + 8f, 220f, Mathf.Max(72f, 36f + mediaPointerLists.Count * 30f));
        browserContextMenuOpen = true;
    }

    private void DrawMediaContextMenu(Rect browserRect)
    {
        if (!browserContextMenuOpen)
        {
            return;
        }

        var evt = Event.current;
        var menuRect = browserContextMenuRect;
        menuRect.x = Mathf.Clamp(menuRect.x, browserRect.x + 8f, browserRect.xMax - menuRect.width - 8f);
        menuRect.y = Mathf.Clamp(menuRect.y, browserRect.y + 8f, browserRect.yMax - menuRect.height - 8f);
        browserContextMenuRect = menuRect;

        GUI.DrawTexture(menuRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.045f, 0.05f, 0.056f, 0.98f), 0f, 4f);
        GUI.Label(new Rect(menuRect.x + 10f, menuRect.y + 8f, menuRect.width - 20f, 20f), "Add to List", normalStyle);

        var y = menuRect.y + 34f;
        for (var i = 1; i < mediaPointerLists.Count; i++)
        {
            var list = mediaPointerLists[i];
            if (list == null)
            {
                continue;
            }

            if (GUI.Button(new Rect(menuRect.x + 8f, y, menuRect.width - 16f, 24f), list.Name))
            {
                AddMediaPathsToPointerList(i, browserContextMenuTargets);
                browserContextMenuOpen = false;
                return;
            }
            y += 28f;
        }

        if (mediaPointerLists.Count <= 1)
        {
            GUI.Label(new Rect(menuRect.x + 10f, y, menuRect.width - 20f, 20f), "Create a list first.", smallStyle);
        }

        if (evt != null && evt.type == EventType.MouseDown && !menuRect.Contains(evt.mousePosition))
        {
            browserContextMenuOpen = false;
            evt.Use();
        }
    }

    private Texture BrowserPreviewTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        VideoThumbnailCacheEntry cached;
        if (videoThumbnailCache.TryGetValue(path, out cached))
        {
            return cached.Texture;
        }

        if (TryLoadVideoThumbnailCache(path, out cached))
        {
            videoThumbnailCache[path] = cached;
            return cached.Texture;
        }

        if (IsVideoBrowserVisible())
        {
            EnqueueVideoThumbnail(path);
        }

        return null;
    }

    private static string ShortFileName(string name, int maxChars)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxChars)
        {
            return name;
        }

        if (maxChars < 6)
        {
            return name.Substring(0, Math.Max(1, maxChars));
        }

        var extension = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var keep = Math.Max(1, maxChars - extension.Length - 1);
        if (stem.Length > keep)
        {
            stem = stem.Substring(0, Math.Max(1, keep - 1)) + "…";
        }
        return stem + extension;
    }

    private void DrawAuxWindows(Rect full)
    {
        if (previewAuxWindowEnabled && previewAuxWindowVisible)
        {
            previewAuxWindowRect = GUI.Window(9201, previewAuxWindowRect, DrawPreviewAuxWindow, "Player Preview");
            previewAuxWindowRect.x = Mathf.Clamp(previewAuxWindowRect.x, 0f, Mathf.Max(0f, full.width - previewAuxWindowRect.width));
            previewAuxWindowRect.y = Mathf.Clamp(previewAuxWindowRect.y, 0f, Mathf.Max(0f, full.height - previewAuxWindowRect.height));
        }
        if (layerSettingsAuxWindowEnabled && layerSettingsAuxWindowVisible)
        {
            layerSettingsAuxWindowRect = GUI.Window(9202, layerSettingsAuxWindowRect, DrawLayerSettingsAuxWindow, LayerSlotLabel(selectedLayerIndex) + " Settings");
            layerSettingsAuxWindowRect.x = Mathf.Clamp(layerSettingsAuxWindowRect.x, 0f, Mathf.Max(0f, full.width - layerSettingsAuxWindowRect.width));
            layerSettingsAuxWindowRect.y = Mathf.Clamp(layerSettingsAuxWindowRect.y, 0f, Mathf.Max(0f, full.height - layerSettingsAuxWindowRect.height));
        }
    }

    private void DrawPreviewAuxWindow(int id)
    {
        var closeRect = new Rect(previewAuxWindowRect.width - 30f, 6f, 22f, 20f);
        if (GUI.Button(closeRect, "x"))
        {
            previewAuxWindowVisible = false;
        }
        DrawCdjScreenContent(new Rect(0f, 0f, previewAuxWindowRect.width, previewAuxWindowRect.height), true);
        GUI.DragWindow(new Rect(0f, 0f, previewAuxWindowRect.width - 36f, 26f));
    }

    private void DrawLayerSettingsAuxWindow(int id)
    {
        var closeRect = new Rect(layerSettingsAuxWindowRect.width - 30f, 6f, 22f, 20f);
        if (GUI.Button(closeRect, "x"))
        {
            layerSettingsAuxWindowVisible = false;
        }
        DrawLayerSettingsScreenContent(new Rect(0f, 0f, layerSettingsAuxWindowRect.width, layerSettingsAuxWindowRect.height), true);
        GUI.DragWindow(new Rect(0f, 0f, layerSettingsAuxWindowRect.width - 36f, 26f));
    }

    private void DrawCdjScreen(Rect full)
    {
        DrawCdjScreenContent(full, false);
    }

    private void DrawCdjScreenContent(Rect full, bool inWindow)
    {
        var screenLayout = BuildCdjMonitorScreenLayout(full);
        GUI.Label(screenLayout.TitleRect, "CDJ Player Monitor", titleStyle);
        DrawCdjDeckModeButtons(screenLayout.DeckButtonsRect);
        if (!inWindow)
        {
            GUI.Label(screenLayout.HintRect, "Space: VJ screen   BLT metadata: " + BltStatusText(), smallStyle);
        }

        var monitorData = BuildCdjMonitorViewData();
        if (!string.IsNullOrEmpty(monitorData.LastError))
        {
            GUI.Label(screenLayout.ErrorRect, "Listener error: " + monitorData.LastError, smallStyle);
        }

        var playerCount = screenLayout.FourDeck ? 4 : 2;
        for (var i = 0; i < playerCount && i < screenLayout.PanelRects.Length; i++)
        {
            DrawCdjPlayerPanel(FindCdjMonitorDeckCard(monitorData, i + 1), screenLayout.PanelRects[i]);
        }
    }

    private void DrawCdjDeckModeButtons(Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.028f, 0.033f, 0.038f, 1f), 0f, 4f);
        var buttonGap = 6f;
        var buttonW = (rect.width - buttonGap) * 0.5f;
        if (GUI.Button(new Rect(rect.x, rect.y, buttonW, rect.height), cdjDeckMode == 2 ? "> 2 Deck" : "2 Deck"))
        {
            cdjDeckMode = 2;
        }
        if (GUI.Button(new Rect(rect.x + buttonW + buttonGap, rect.y, buttonW, rect.height), cdjDeckMode == 4 ? "> 4 Deck" : "4 Deck"))
        {
            cdjDeckMode = 4;
        }
    }

    private CdjMonitorScreenLayout BuildCdjMonitorScreenLayout(Rect full)
    {
        var margin = 18f;
        var left = full.x + margin;
        var top = full.y + margin;
        var panelGap = 14f;
        var panelY = full.y + 86f;
        var availableW = full.width - margin * 2f;
        var panelH = (full.height - panelY - margin - panelGap) * 0.5f;
        var layout = new CdjMonitorScreenLayout
        {
            FourDeck = cdjDeckMode == 4,
            TitleRect = new Rect(left, top, 300f, 34f),
            DeckButtonsRect = new Rect(left + 310f, top + 2f, 230f, 30f),
            HintRect = new Rect(full.x + full.width - 520f, top + 7f, 500f, 24f),
            ErrorRect = new Rect(left, full.y + 54f, full.width - margin * 2f, 24f),
            PanelRects = new Rect[4]
        };

        if (layout.FourDeck)
        {
            var columnW = (availableW - panelGap) * 0.5f;
            var rightX = left + columnW + panelGap;
            layout.PanelRects[0] = new Rect(left, panelY, columnW, panelH);
            layout.PanelRects[1] = new Rect(left, panelY + panelH + panelGap, columnW, panelH);
            layout.PanelRects[2] = new Rect(rightX, panelY, columnW, panelH);
            layout.PanelRects[3] = new Rect(rightX, panelY + panelH + panelGap, columnW, panelH);
        }
        else
        {
            layout.PanelRects[0] = new Rect(left, panelY, availableW, panelH);
            layout.PanelRects[1] = new Rect(left, panelY + panelH + panelGap, availableW, panelH);
        }

        return layout;
    }

    private static CdjPlayerPanelLayout BuildCdjPlayerPanelLayout(Rect rect)
    {
        var pad = 18f;
        var x = rect.x + pad;
        var y = rect.y + pad;
        var w = rect.width - pad * 2f;
        var albumArtRect = new Rect(x + 190f, y, 64f, 64f);
        var titleX = albumArtRect.xMax + 14f;
        var titleRect = new Rect(titleX, y, w - (titleX - x), 34f);
        var artistRect = new Rect(titleX, y + 38f, w - (titleX - x), 28f);
        var commentRect = new Rect(titleX, y + 68f, w - (titleX - x), 24f);
        var previewStartY = y + 98f;
        var previewHeight = Mathf.Clamp(rect.height * 0.32f, 105f, 150f);
        var runtimeWaveRect = new Rect(x, previewStartY, w, previewHeight);
        var overviewHeight = Mathf.Clamp(rect.height * 0.13f, 34f, 52f);
        var overviewWaveRect = new Rect(x, runtimeWaveRect.yMax + 8f, w, overviewHeight);
        var rowTop = overviewWaveRect.yMax + 14f;
        return new CdjPlayerPanelLayout
        {
            ContentLeft = x,
            ContentTop = y,
            ContentWidth = w,
            HeaderRect = new Rect(x, y, 180f, 32f),
            FlagsRect = new Rect(rect.xMax - 360f, y + 7f, 240f, 24f),
            SearchButtonRect = new Rect(rect.xMax - 112f, y + 2f, 94f, 26f),
            AlbumArtRect = albumArtRect,
            DeviceRect = new Rect(x, y, 180f, 28f),
            TitleRect = titleRect,
            ArtistRect = artistRect,
            CommentRect = commentRect,
            RuntimeWaveRect = runtimeWaveRect,
            RuntimeWavePlaceholderRect = new Rect(runtimeWaveRect.x + 12f, runtimeWaveRect.y + 12f, runtimeWaveRect.width - 24f, 24f),
            OverviewWaveRect = overviewWaveRect,
            OverviewWavePlaceholderRect = new Rect(overviewWaveRect.x + 10f, overviewWaveRect.y + 7f, overviewWaveRect.width - 20f, 20f),
            BpmLineRect = new Rect(x, rowTop, w, 34f),
            TimeLineRect = new Rect(x, rowTop + 34f, w, 34f),
            BeatLineRect = new Rect(x, rowTop + 68f, w, 34f)
        };
    }

    private void DrawCdjPlayerPanel(CdjMonitorDeckCardViewData card, Rect rect)
    {
        var panelLayout = BuildCdjPlayerPanelLayout(rect);
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.025f, 0.03f, 0.035f, 1f), 0f, 6f);
        var number = card == null ? 0 : card.Number;
        var player = card == null ? null : card.Player;

        GUI.Label(panelLayout.HeaderRect, "PLAYER " + number, titleStyle);
        var flags = card == null ? "NO LINK" : card.Flags;
        GUI.Label(panelLayout.FlagsRect, flags, smallStyle);
        if (GUI.Button(panelLayout.SearchButtonRect, "YT Search"))
        {
            OpenYoutubeSearch(number, player);
        }

        GUI.DrawTexture(panelLayout.AlbumArtRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.01f, 0.012f, 0.015f, 1f), 0f, 4f);
        if (card != null && card.AlbumArtTexture != null)
        {
            GUI.DrawTexture(panelLayout.AlbumArtRect, card.AlbumArtTexture, ScaleMode.ScaleToFit, false);
        }

        GUI.Label(panelLayout.TitleRect, card == null ? "No player connected" : card.Title, titleStyle);
        GUI.Label(panelLayout.ArtistRect, player == null ? "" : TrackArtist(player), normalStyle);
        var comment = card == null ? "" : card.Comment;
        if (!string.IsNullOrEmpty(comment))
        {
            GUI.Label(panelLayout.CommentRect, comment, smallStyle);
        }

        var preview = panelLayout.RuntimeWaveRect;
        GUI.DrawTexture(preview, whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 4f);
        if (card != null && card.RuntimeWaveTexture != null)
        {
            GUI.DrawTexture(preview, card.RuntimeWaveTexture, ScaleMode.StretchToFill, false);
        }
        else if (player != null && player.DirectWaveformBytes != null)
        {
            DrawDirectRuntimeWaveform(player, preview, BltWaveDetailScale);
        }
        else
        {
            GUI.Label(new Rect(preview.x + 12f, preview.y + 12f, preview.width - 24f, 24f), player == null ? "Player not connected" : "Waiting for runtime waveform detail", normalStyle);
        }
        DrawPlaybackMarker(player, preview);

        var overview = panelLayout.OverviewWaveRect;
        GUI.DrawTexture(overview, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.005f, 0.006f, 0.008f, 1f), 0f, 4f);
        if (player != null && player.DirectWaveformBytes != null)
        {
            DrawDirectOverviewWaveform(player, overview);
        }
        else
        {
            GUI.Label(new Rect(overview.x + 10f, overview.y + 7f, overview.width - 20f, 20f), "Waiting for full waveform", smallStyle);
        }
        DrawOverviewPlaybackMarker(player, overview);
        DrawCurrentCueOnOverview(player, overview);

        GUI.Label(panelLayout.BpmLineRect, "BPM " + (player == null ? "--" : FormatBpm(DisplayEffectiveBpm(player))) +
                                      "   Track BPM " + (player == null ? "--" : FormatBpm(DisplayTrackBpm(player))) +
                                      "   Pitch " + (player == null ? "--" : player.PitchPercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%"), normalStyle);
        GUI.Label(panelLayout.TimeLineRect, "Time " + (player == null ? "--:--" : DisplayPlayedTime(player)) +
                                      " / " + (player == null ? "--:--" : DisplayDuration(player)) +
                                      "   Remain " + (player == null ? "--:--" : DisplayRemainingTime(player)), normalStyle);
        GUI.Label(panelLayout.BeatLineRect, "Beat " + (player == null ? "--" : FormatBeat(player)) +
                                      "   Source " + (player == null ? "--" : "P" + player.TrackSourcePlayer + " " + FormatTrackSourceSlot(player.TrackSourceSlot)) +
                                      "   Last " + (player == null ? "never" : AgeText(player.LastSeenUtc)), smallStyle);
    }

    private CdjMonitorViewData BuildCdjMonitorViewData()
    {
        var snapshot = Snapshot();
        var data = new CdjMonitorViewData
        {
            Title = "CDJ Player Monitor",
            Status = BltStatusText(),
            FourDeck = cdjDeckMode == 4,
            LastError = snapshot.LastError
        };

        var playerCount = cdjDeckMode == 4 ? 4 : 2;
        for (var number = 1; number <= playerCount; number++)
        {
            var player = FindFreshMonitorPlayer(snapshot.Players, number);
            data.Players.Add(BuildCdjMonitorDeckCard(player, number));
        }

        return data;
    }

    private CdjMonitorDeckCardViewData BuildCdjMonitorDeckCard(PlayerState player, int number)
    {
        var overviewPlaybackRatio = 0f;
        var hasOverviewPlaybackRatio = false;
        var overviewCueRatio = 0f;
        var hasOverviewCueRatio = false;
        if (player != null)
        {
            var durationMs = DisplayDurationMs(player);
            if (durationMs > 0)
            {
                overviewPlaybackRatio = Mathf.Clamp01(CurrentPlaybackMs(player) / (float)durationMs);
                hasOverviewPlaybackRatio = true;
                if (player.CueCountdown >= 0 && player.CueCountdown < 511 && player.BeatGrid != null && player.BeatGrid.Count > 0 && player.BeatNumber >= 0)
                {
                    var beatIndex = Mathf.Clamp(player.BeatNumber + player.CueCountdown - 1, 0, player.BeatGrid.Count - 1);
                    overviewCueRatio = Mathf.Clamp01(player.BeatGrid[beatIndex].TimeMs / (float)durationMs);
                    hasOverviewCueRatio = true;
                }
            }
        }

        return new CdjMonitorDeckCardViewData
        {
            Number = number,
            Player = player,
            Flags = player == null ? "NO LINK" : BuildFlags(player),
            Device = player == null ? "No player connected" : PreferText(player.DeviceName, "Unknown device") + "  " + player.Address,
            Title = player == null ? "No player connected" : TrackTitle(player),
            Artist = player == null ? "" : TrackArtist(player),
            Comment = player == null ? "" : TrackComment(player),
            BpmLine = player == null ? "BPM --" : "BPM " + FormatBpm(DisplayEffectiveBpm(player)) + "   Track BPM " + FormatBpm(DisplayTrackBpm(player)) + "   Pitch " + player.PitchPercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%",
            TimeLine = player == null ? "" : "Time " + DisplayPlayedTime(player) + " / " + DisplayDuration(player) + "   Remain " + DisplayRemainingTime(player),
            BeatLine = player == null ? "" : "Beat " + FormatBeat(player) + "   Source P" + player.TrackSourcePlayer + " " + FormatTrackSourceSlot(player.TrackSourceSlot) + "   Last " + AgeText(player.LastSeenUtc),
            AlbumArtTexture = player == null ? null : player.AlbumArtTexture,
            RuntimeWaveTexture = player == null ? null : (Texture)(player.BltWavePreview != null ? player.BltWavePreview : player.DirectWaveformTexture),
            OverviewWaveTexture = player == null ? null : player.DirectWaveformTexture,
            HasOverviewPlaybackRatio = hasOverviewPlaybackRatio,
            OverviewPlaybackRatio = overviewPlaybackRatio,
            HasOverviewCueRatio = hasOverviewCueRatio,
            OverviewCueRatio = overviewCueRatio
        };
    }

    private static CdjMonitorDeckCardViewData FindCdjMonitorDeckCard(CdjMonitorViewData data, int number)
    {
        if (data == null || data.Players == null)
        {
            return null;
        }

        for (var i = 0; i < data.Players.Count; i++)
        {
            var card = data.Players[i];
            if (card != null && card.Number == number)
            {
                return card;
            }
        }

        return null;
    }

    private static PlayerState FindFreshMonitorPlayer(List<PlayerState> players, int number)
    {
        var player = FindPlayer(players, number);
        if (!IsFreshMonitorPlayer(player))
        {
            return null;
        }
        return player;
    }

    private static bool IsFreshMonitorPlayer(PlayerState player)
    {
        if (player == null)
        {
            return false;
        }

        if (player.LastSeenUtc == default(DateTime))
        {
            return false;
        }

        return (DateTime.UtcNow - player.LastSeenUtc).TotalSeconds <= CdjMonitorStaleSeconds;
    }

    private void OpenYoutubeSearch(int deckNumber, PlayerState player)
    {
        youtubeSearchDeck = deckNumber;
        youtubeSearchQuery = player == null ? "" : (TrackTitle(player) + " " + TrackArtist(player)).Trim();
        youtubeSearchStatus = "";
        youtubeSearchOpen = false;
        youtubeLayerPickerOpen = false;
        pendingYoutubeResult = null;
        youtubeManualUrlInput = "";
        youtubeSearchScroll = Vector2.zero;
        if (!LaunchYoutubeWebView(youtubeSearchQuery, selectedLayerIndex))
        {
            youtubeSearchStatus = "WebView helper not found. Falling back to built-in search list.";
            youtubeSearchOpen = true;
            if (!string.IsNullOrEmpty(youtubeSearchQuery))
            {
                StartYoutubeSearch();
            }
        }
    }

    private bool LaunchYoutubeWebView(string query, int layerIndex)
    {
        var helper = FindYoutubeWebViewBridge();
        if (string.IsNullOrEmpty(helper))
        {
            AddLog("YouTube WebView helper not found.");
            return false;
        }

        youtubeSelectionFile = Path.Combine(Application.temporaryCachePath, "beatlink-youtube-selection.txt");
        try
        {
            if (File.Exists(youtubeSelectionFile))
            {
                File.Delete(youtubeSelectionFile);
            }

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = helper,
                Arguments = "--query \"" + EscapeProcessArgument(query ?? "") + "\" --output \"" + EscapeProcessArgument(youtubeSelectionFile) + "\" --layer \"" + layerIndex.ToString(CultureInfo.InvariantCulture) + "\"",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            System.Diagnostics.Process.Start(info);
            AddLog("YouTube WebView opened for Layer " + (layerIndex + 1).ToString(CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception ex)
        {
            AddLog("YouTube WebView error: " + ex.Message);
            return false;
        }
    }

    private string FindYoutubeWebViewBridge()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "YoutubeWebViewBridge", "bin", "Release", "YoutubeWebViewBridge.exe")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "YoutubeWebViewBridge", "bin", "Release", "YoutubeWebViewBridge.exe"))
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

    private static string EscapeProcessArgument(string value)
    {
        return (value ?? "").Replace("\"", "\\\"");
    }

    private void CheckYoutubeSelectionFile()
    {
        if (string.IsNullOrEmpty(youtubeSelectionFile) || !File.Exists(youtubeSelectionFile) ||
            youtubeSelectionLastReadUtc.AddMilliseconds(350) > DateTime.UtcNow)
        {
            return;
        }
        youtubeSelectionLastReadUtc = DateTime.UtcNow;

        try
        {
            var text = File.ReadAllText(youtubeSelectionFile, Encoding.UTF8).Trim();
            File.Delete(youtubeSelectionFile);
            var parts = text.Split('\t');
            if (parts.Length < 2)
            {
                return;
            }
            int layerIndex;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out layerIndex))
            {
                layerIndex = selectedLayerIndex;
            }
            var videoId = parts[1];
            var title = "YouTube " + videoId;
            if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
            {
                try
                {
                    title = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                }
                catch
                {
                }
            }
            LoadYoutubeVideoIntoLayer(layerIndex, new YoutubeSearchResult
            {
                VideoId = videoId,
                Title = title,
                Author = ""
            });
        }
        catch (Exception ex)
        {
            AddLog("YouTube selection error: " + ex.Message);
        }
    }

    private void StartYoutubeSearch()
    {
        if (youtubeSearchInFlight)
        {
            return;
        }
        var query = youtubeSearchQuery ?? "";
        if (string.IsNullOrEmpty(query.Trim()))
        {
            youtubeSearchStatus = "Enter a search query.";
            return;
        }
        youtubeSearchResults.Clear();
        youtubeSearchStatus = "Loading YouTube results...";
        StartCoroutine(SearchYoutube(query));
    }

    private IEnumerator SearchYoutube(string query)
    {
        youtubeSearchInFlight = true;
        var url = "https://www.youtube.com/results?search_query=" + UnityWebRequest.EscapeURL(query ?? "");
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                youtubeSearchStatus = request.error;
            }
            else
            {
                youtubeSearchResults.Clear();
                ParseYoutubeSearchResults(request.downloadHandler.text, youtubeSearchResults);
                youtubeSearchStatus = youtubeSearchResults.Count == 0 ? "No results." : youtubeSearchResults.Count.ToString(CultureInfo.InvariantCulture) + " results";
            }
        }
        youtubeSearchInFlight = false;
    }

    private static void ParseYoutubeSearchResults(string html, List<YoutubeSearchResult> results)
    {
        if (string.IsNullOrEmpty(html) || results == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var regex = new Regex("\"videoId\":\"(?<id>[^\"]+)\".{0,2400}?\"title\":\\{\"runs\":\\[\\{\"text\":\"(?<title>(?:\\\\.|[^\"])*)\"\\}", RegexOptions.Singleline);
        var matches = regex.Matches(html);
        for (var i = 0; i < matches.Count && results.Count < 16; i++)
        {
            var id = matches[i].Groups["id"].Value;
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
            {
                continue;
            }
            var title = DecodeYoutubeText(matches[i].Groups["title"].Value);
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }
            results.Add(new YoutubeSearchResult
            {
                VideoId = id,
                Title = title,
                Author = ""
            });
        }
    }

    private static string DecodeYoutubeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        try
        {
            return WebUtility.HtmlDecode(Regex.Unescape(text));
        }
        catch
        {
            return text;
        }
    }

    private void DrawYoutubeSearchWindow(Rect full)
    {
        GUI.DrawTexture(full, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.58f), 0f, 0f);
        var width = Mathf.Min(900f, full.width - 70f);
        var height = Mathf.Min(680f, full.height - 70f);
        var panel = new Rect((full.width - width) * 0.5f, (full.height - height) * 0.5f, width, height);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.035f, 0.04f, 0.046f, 1f), 0f, 6f);

        var pad = 16f;
        var x = panel.x + pad;
        var y = panel.y + pad;
        var w = panel.width - pad * 2f;
        GUI.Label(new Rect(x, y, w - 80f, 30f), "YouTube Search  Deck " + youtubeSearchDeck.ToString(CultureInfo.InvariantCulture), titleStyle);
        if (GUI.Button(new Rect(panel.xMax - pad - 70f, y, 70f, 28f), "Close"))
        {
            youtubeSearchOpen = false;
            return;
        }

        y += 42f;
        youtubeSearchQuery = GUI.TextField(new Rect(x, y, w - 100f, 30f), youtubeSearchQuery ?? "");
        GUI.enabled = !youtubeSearchInFlight;
        if (GUI.Button(new Rect(x + w - 90f, y, 90f, 30f), "Search"))
        {
            StartYoutubeSearch();
        }
        GUI.enabled = true;

        y += 40f;
        GUI.Label(new Rect(x, y, w, 22f), (youtubeSearchStatus ?? "") + "   Target: " + LayerSlotLabel(selectedLayerIndex), smallStyle);
        y += 28f;

        var list = new Rect(x, y, w, panel.yMax - y - pad - 82f);
        GUI.DrawTexture(list, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.018f, 0.021f, 0.024f, 1f), 0f, 4f);
        GUILayout.BeginArea(new Rect(list.x + 8f, list.y + 8f, list.width - 16f, list.height - 16f));
        youtubeSearchScroll = GUILayout.BeginScrollView(youtubeSearchScroll);
        if (youtubeSearchResults.Count == 0)
        {
            GUILayout.Label(youtubeSearchInFlight ? "Loading..." : "No results loaded.", normalStyle);
        }
        for (var i = 0; i < youtubeSearchResults.Count; i++)
        {
            var result = youtubeSearchResults[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label((i + 1).ToString(CultureInfo.InvariantCulture), smallStyle, GUILayout.Width(28f));
            if (GUILayout.Button(result.Title, GUILayout.Height(34f)))
            {
                LoadYoutubeVideoIntoLayer(selectedLayerIndex, result);
                youtubeSearchOpen = false;
                youtubeLayerPickerOpen = false;
                pendingYoutubeResult = null;
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(result.Author))
            {
                GUILayout.Label("    " + result.Author, smallStyle);
            }
            GUILayout.Space(4f);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        y = list.yMax + 10f;
        GUI.Label(new Rect(x, y, w, 20f), "Direct URL / Video ID", smallStyle);
        y += 24f;
        youtubeManualUrlInput = GUI.TextField(new Rect(x, y, w - 130f, 30f), youtubeManualUrlInput ?? "");
        if (GUI.Button(new Rect(x + w - 120f, y, 120f, 30f), "Load Target"))
        {
            var videoId = ExtractYoutubeVideoId(youtubeManualUrlInput);
            if (string.IsNullOrEmpty(videoId))
            {
                youtubeSearchStatus = "Invalid YouTube URL or video ID.";
            }
            else
            {
                pendingYoutubeResult = new YoutubeSearchResult
                {
                    VideoId = videoId,
                    Title = "YouTube " + videoId,
                    Author = ""
                };
                LoadYoutubeVideoIntoLayer(selectedLayerIndex, pendingYoutubeResult);
                youtubeSearchOpen = false;
                youtubeLayerPickerOpen = false;
                pendingYoutubeResult = null;
            }
        }
    }

    private void DrawYoutubeLayerPicker(Rect full)
    {
        GUI.DrawTexture(full, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.66f), 0f, 0f);
        var width = Mathf.Min(520f, full.width - 80f);
        var height = Mathf.Min(520f, full.height - 80f);
        var panel = new Rect((full.width - width) * 0.5f, (full.height - height) * 0.5f, width, height);
        GUI.DrawTexture(panel, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.04f, 0.045f, 0.052f, 1f), 0f, 6f);

        var pad = 16f;
        GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, panel.height - pad * 2f));
        GUILayout.Label("Load YouTube To Layer", titleStyle);
        GUILayout.Label(pendingYoutubeResult == null ? "" : pendingYoutubeResult.Title, normalStyle);
        GUILayout.Space(8f);
        youtubeLayerPickerScroll = GUILayout.BeginScrollView(youtubeLayerPickerScroll);
        for (var i = 0; i < VjLayerCount; i++)
        {
            if (!IsSourceLayerSlot(i))
            {
                continue;
            }
            var layer = vjLayers != null && i < vjLayers.Length ? vjLayers[i] : null;
            var label = LayerSlotLabel(i) + "  " + LayerName(layer);
            if (GUILayout.Button(label, GUILayout.Height(30f)))
            {
                LoadYoutubeVideoIntoLayer(i, pendingYoutubeResult);
                youtubeLayerPickerOpen = false;
                youtubeSearchOpen = false;
                pendingYoutubeResult = null;
            }
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Cancel", GUILayout.Height(30f)))
        {
            youtubeLayerPickerOpen = false;
            pendingYoutubeResult = null;
        }
        GUILayout.EndArea();
    }

    private void DrawPlaybackMarker(PlayerState player, Rect rect)
    {
        if (player == null)
        {
            return;
        }

        var x = rect.x + rect.width * 0.5f;
        GUI.DrawTexture(new Rect(x - 1.5f, rect.y, 3f, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.92f, 0.18f, 0.95f), 0f, 0f);
    }

    private void DrawOverviewPlaybackMarker(PlayerState player, Rect rect)
    {
        if (player == null)
        {
            return;
        }

        var x = OverviewX(player, rect, CurrentPlaybackMs(player));
        if (x < rect.x || x > rect.xMax)
        {
            return;
        }

        GUI.DrawTexture(new Rect(x - 1.5f, rect.y, 3f, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.92f, 0.18f, 0.95f), 0f, 0f);
    }

    private static int CurrentPlaybackMs(PlayerState player)
    {
        return player.BltTimePlayedMs > 0 ? player.BltTimePlayedMs : player.PlaybackPositionMs;
    }

    private static int DirectWaveformFrameCount(byte[] data, DirectWaveformStyle style)
    {
        if (data == null)
        {
            return 0;
        }
        return style == DirectWaveformStyle.Rgb ? data.Length / 2 : data.Length;
    }

    private static int ClampWaveformZoomIndex(int index)
    {
        return Mathf.Clamp(index, 0, WaveformZoomLevels.Length - 1);
    }

    private static float WaveformZoomForIndex(int index)
    {
        return WaveformZoomLevels[ClampWaveformZoomIndex(index)];
    }

    private static int WaveformTextureScaleForZoom(int frameCount, float zoom)
    {
        var requestedScale = Mathf.Max(1, Mathf.RoundToInt(BltWaveDetailScale / Mathf.Max(0.01f, zoom)));
        var maxWidthScale = frameCount <= 0 ? 1 : Mathf.CeilToInt(frameCount / (float)WaveformTextureMaxWidth);
        return Mathf.Max(requestedScale, maxWidthScale);
    }

    private static Texture2D BuildDirectWaveformTexture(byte[] data, DirectWaveformStyle style, int scale, int height)
    {
        var frameCount = DirectWaveformFrameCount(data, style);
        if (frameCount <= 0)
        {
            return null;
        }

        var framesPerColumn = Mathf.Clamp(scale, 1, 4096);
        var width = Mathf.Clamp(Mathf.CeilToInt(frameCount / (float)framesPerColumn), 1, WaveformTextureMaxWidth);
        var textureHeight = Mathf.Max(24, height);
        var pixels = new Color32[width * textureHeight];
        var transparent = new Color32(0, 0, 0, 0);
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        var centerY = Mathf.RoundToInt(textureHeight * 0.5f);
        for (var x = 0; x < width; x++)
        {
            var startFrame = x * framesPerColumn;
            var endFrame = Math.Min(frameCount, startFrame + framesPerColumn);
            var heightSum = 0;
            var redSum = 0;
            var greenSum = 0;
            var blueSum = 0;
            var blueColorSum = 0;
            var count = 0;

            for (var frame = startFrame; frame < endFrame; frame++)
            {
                if (style == DirectWaveformStyle.Rgb)
                {
                    var baseIndex = frame * 2;
                    var bits = (data[baseIndex] << 8) | data[baseIndex + 1];
                    heightSum += (bits >> 2) & 0x1f;
                    redSum += (bits >> 13) & 0x07;
                    greenSum += (bits >> 10) & 0x07;
                    blueSum += (bits >> 7) & 0x07;
                }
                else
                {
                    var value = data[frame];
                    heightSum += value & 0x1f;
                    blueColorSum += (value & 0xe0) >> 5;
                }
                count++;
            }

            if (count == 0)
            {
                continue;
            }

            Color color;
            if (style == DirectWaveformStyle.Rgb)
            {
                color = new Color(redSum / (count * 7f), greenSum / (count * 7f), blueSum / (count * 7f), 1f);
            }
            else
            {
                var colorIndex = Mathf.Clamp(Mathf.RoundToInt(blueColorSum / (float)count), 0, DirectWaveColors.Length - 1);
                color = DirectWaveColors[colorIndex];
            }

            var heightValue = heightSum / (float)count;
            var barHeight = Mathf.Clamp(Mathf.RoundToInt(heightValue / 31f * textureHeight * 0.84f), 2, textureHeight);
            var yMin = Mathf.Clamp(centerY - barHeight / 2, 0, textureHeight - 1);
            var yMax = Mathf.Clamp(centerY + (barHeight + 1) / 2, 0, textureHeight - 1);
            var color32 = (Color32)color;
            for (var y = yMin; y <= yMax; y++)
            {
                pixels[y * width + x] = color32;
            }
        }

        var texture = new Texture2D(width, textureHeight, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private void DrawDirectRuntimeWaveform(PlayerState player, Rect rect, int scale)
    {
        if (player.DirectWaveformTexture == null || player.DirectWaveformTextureWidth <= 0 || player.DirectWaveformTextureFrameCount <= 0)
        {
            return;
        }

        var zoomIndex = HandleWaveformZoom(player, rect);
        var requestedZoom = WaveformZoomForIndex(zoomIndex);
        var texture = player.DirectWaveformTexture;
        var played = player.BltTimePlayedMs > 0 ? player.BltTimePlayedMs : player.PlaybackPositionMs;
        var centerFrame = Mathf.Clamp((int)(played * 15L / 100L), 0, player.DirectWaveformTextureFrameCount - 1);
        var textureScale = Mathf.Max(1, player.DirectWaveformTextureScale);
        var centerColumn = Mathf.RoundToInt(centerFrame / (float)textureScale);
        var visibleColumns = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        var leftColumn = centerColumn - visibleColumns / 2;
        var drawX = rect.x;
        var drawWidth = rect.width;
        var sourceLeft = leftColumn;

        if (sourceLeft < 0)
        {
            var blankColumns = -sourceLeft;
            drawX += blankColumns;
            drawWidth -= blankColumns;
            sourceLeft = 0;
        }

        var sourceWidth = drawWidth;
        if (sourceLeft + sourceWidth > texture.width)
        {
            sourceWidth = texture.width - sourceLeft;
            drawWidth = sourceWidth;
        }

        if (drawWidth > 0f && sourceWidth > 0f)
        {
            var texCoords = new Rect(sourceLeft / (float)texture.width, 0f, sourceWidth / texture.width, 1f);
            GUI.DrawTextureWithTexCoords(new Rect(drawX, rect.y, drawWidth, rect.height), texture, texCoords, true);
        }
        DrawWaveformGridAndCues(player, rect, leftColumn, textureScale, requestedZoom);
        GUI.Label(new Rect(rect.x + 8f, rect.yMax - 22f, 190f, 18f),
            "Zoom x" + requestedZoom.ToString("0.0", CultureInfo.InvariantCulture) +
            "  res 1:" + textureScale.ToString(CultureInfo.InvariantCulture), smallStyle);
    }

    private void DrawDirectOverviewWaveform(PlayerState player, Rect rect)
    {
        if (player.DirectWaveformTexture == null || player.DirectWaveformTextureWidth <= 0)
        {
            return;
        }

        GUI.DrawTexture(rect, player.DirectWaveformTexture, ScaleMode.StretchToFill, true);
    }

    private int HandleWaveformZoom(PlayerState player, Rect rect)
    {
        var zoomIndex = ClampWaveformZoomIndex(player.WaveformZoomIndex);
        var evt = Event.current;
        if (evt != null && evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition))
        {
            zoomIndex = ClampWaveformZoomIndex(zoomIndex + (evt.delta.y < 0f ? 1 : -1));
            player.WaveformZoomIndex = zoomIndex;
            player.DirectWaveformTextureKey = null;
            lock (stateLock)
            {
                if (players.TryGetValue(player.DeviceNumber, out var livePlayer))
                {
                    livePlayer.WaveformZoomIndex = zoomIndex;
                    livePlayer.DirectWaveformTextureKey = null;
                }
            }
            evt.Use();
        }
        return zoomIndex;
    }

    private void DrawWaveformGridAndCues(PlayerState player, Rect rect, int leftColumn, int textureScale, float zoom)
    {
        if (player.BeatGrid != null)
        {
            var lastBeatX = -9999f;
            var hideAuxiliaryTicks = zoom <= 1f;
            for (var i = 0; i < player.BeatGrid.Count; i++)
            {
                var marker = player.BeatGrid[i];
                var x = MarkerX(rect, leftColumn, textureScale, marker.TimeMs);
                if (x < rect.x || x > rect.xMax)
                {
                    continue;
                }
                var downbeat = marker.BeatWithinBar == 1;
                if (!downbeat && hideAuxiliaryTicks)
                {
                    continue;
                }
                if (!downbeat && x - lastBeatX < 4f)
                {
                    continue;
                }
                lastBeatX = x;
                DrawBeatTick(rect, x, downbeat);
            }
        }

        if (player.CueMarkers == null)
        {
            return;
        }

        DrawLoopOverlays(player, rect, leftColumn, textureScale);

        for (var i = 0; i < player.CueMarkers.Count; i++)
        {
            var cue = player.CueMarkers[i];
            var x = MarkerX(rect, leftColumn, textureScale, cue.TimeMs);
            if (x < rect.x || x > rect.xMax)
            {
                continue;
            }

            var color = cue.IsLoop ? LoopCueMarkerColor : cue.HotCueNumber > 0 ? HotCueMarkerColor : CueMarkerColor;
            if (cue.HotCueNumber > 0)
            {
                DrawHotCueMarker(rect, x, cue.HotCueNumber, color);
            }
            else
            {
                DrawMemoryCueMarker(rect, x, color);
            }

        }
    }

    private void DrawCurrentCueFromCountdown(PlayerState player, Rect rect, int leftColumn, int textureScale)
    {
        if (player.CueCountdown < 0 || player.CueCountdown >= 511 || player.BeatGrid == null || player.BeatGrid.Count == 0 || player.BeatNumber < 0)
        {
            return;
        }

        var beatIndex = Mathf.Clamp(player.BeatNumber + player.CueCountdown - 1, 0, player.BeatGrid.Count - 1);
        var x = MarkerX(rect, leftColumn, textureScale, player.BeatGrid[beatIndex].TimeMs);
        if (x < rect.x || x > rect.xMax)
        {
            return;
        }
        DrawCueMarker(rect, x, CurrentCueMarkerColor);
    }

    private void DrawCurrentCueOnOverview(PlayerState player, Rect rect)
    {
        if (player == null || player.CueCountdown < 0 || player.CueCountdown >= 511 || player.BeatGrid == null || player.BeatGrid.Count == 0 || player.BeatNumber < 0)
        {
            return;
        }

        var beatIndex = Mathf.Clamp(player.BeatNumber + player.CueCountdown - 1, 0, player.BeatGrid.Count - 1);
        var x = OverviewX(player, rect, player.BeatGrid[beatIndex].TimeMs);
        if (x < rect.x || x > rect.xMax)
        {
            return;
        }

        DrawOverviewCueMarker(rect, x, CurrentCueMarkerColor);
    }

    private static float OverviewX(PlayerState player, Rect rect, long timeMs)
    {
        if (player != null && player.DirectWaveformTextureFrameCount > 0)
        {
            var frame = Mathf.Clamp((int)(timeMs * 15L / 100L), 0, player.DirectWaveformTextureFrameCount);
            return rect.x + Mathf.Clamp01(frame / (float)player.DirectWaveformTextureFrameCount) * rect.width;
        }

        var duration = DisplayDurationMs(player);
        if (duration <= 0)
        {
            duration = 1;
        }
        return rect.x + Mathf.Clamp01((float)(timeMs / (double)duration)) * rect.width;
    }

    private void DrawLoopOverlays(PlayerState player, Rect rect, int leftColumn, int textureScale)
    {
        if (!player.HasActiveLoop || player.ActiveLoopEndMs <= player.ActiveLoopStartMs)
        {
            return;
        }

        var startX = MarkerX(rect, leftColumn, textureScale, player.ActiveLoopStartMs);
        var endX = MarkerX(rect, leftColumn, textureScale, player.ActiveLoopEndMs);
        var xMin = Mathf.Max(rect.x, Mathf.Min(startX, endX));
        var xMax = Mathf.Min(rect.xMax, Mathf.Max(startX, endX));
        if (xMax <= rect.x || xMin >= rect.xMax || xMax - xMin < 1f)
        {
            return;
        }

        var loopRect = new Rect(xMin, rect.y + rect.height * 0.12f, xMax - xMin, rect.height * 0.76f);
        GUI.DrawTexture(loopRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.83f, 0.16f, 0.18f), 0f, 0f);
        GUI.DrawTexture(new Rect(xMin, loopRect.y, 2f, loopRect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.83f, 0.16f, 0.55f), 0f, 0f);
        GUI.DrawTexture(new Rect(xMax - 2f, loopRect.y, 2f, loopRect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.83f, 0.16f, 0.55f), 0f, 0f);
    }

    private void DrawBeatTick(Rect rect, float x, bool downbeat)
    {
        var width = downbeat ? 2f : 1f;
        var tickLength = WaveformTickLength(rect);
        var color = downbeat ? DownbeatMarkerColor : BeatMarkerColor;
        GUI.DrawTexture(new Rect(x - width * 0.5f, rect.y, width, tickLength), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        GUI.DrawTexture(new Rect(x - width * 0.5f, rect.yMax - tickLength, width, tickLength), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
    }

    private static float WaveformTickLength(Rect rect)
    {
        return Mathf.Clamp(rect.height * 0.12f, 8f, 16f);
    }

    private void DrawMemoryCueMarker(Rect rect, float x, Color color)
    {
        var size = 18f;
        var anchorY = rect.y + WaveformTickLength(rect) * 0.25f;
        GUI.DrawTexture(new Rect(x - size * 0.5f, anchorY, size, size), triangleUpTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
    }

    private void DrawCueMarker(Rect rect, float x, Color color)
    {
        var size = 18f;
        var anchorY = rect.yMax - WaveformTickLength(rect) * 0.25f;
        GUI.DrawTexture(new Rect(x - size * 0.5f, anchorY - size, size, size), triangleDownTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
    }

    private void DrawOverviewCueMarker(Rect rect, float x, Color color)
    {
        var size = Mathf.Clamp(rect.height * 0.5f, 12f, 20f);
        GUI.DrawTexture(new Rect(x - size * 0.5f, rect.yMax - size, size, size), triangleDownTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
    }

    private void DrawHotCueMarker(Rect rect, float x, int hotCueNumber, Color color)
    {
        var size = 36f;
        var anchorY = rect.y + WaveformTickLength(rect) * 0.5f;
        var markerRect = new Rect(Mathf.Clamp(x - size * 0.5f, rect.x + 2f, rect.xMax - size - 2f), anchorY - size, size, size);
        var borderColor = new Color(color.r, color.g, color.b, 0.95f);
        var fillColor = new Color(color.r, color.g, color.b, 0.95f);
        GUI.DrawTexture(markerRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, fillColor, 0f, 0f);
        DrawRectOutline(markerRect, 4f, borderColor);

        var oldColor = hotCueStyle.normal.textColor;
        hotCueStyle.normal.textColor = Color.black;
        GUI.Label(markerRect, HotCueLetter(hotCueNumber), hotCueStyle);
        hotCueStyle.normal.textColor = oldColor;
    }

    private void DrawRectOutline(Rect rect, float width, Color color)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - width, rect.width, width), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        GUI.DrawTexture(new Rect(rect.xMax - width, rect.y, width, rect.height), whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
    }

    private void DrawLoopEndMarker(Rect rect, float x, Color color)
    {
        GUI.Label(new Rect(x - 13f, rect.y + 21f, 30f, 18f), "OUT", smallStyle);
    }

    private static float MarkerX(Rect rect, int leftColumn, int textureScale, long timeMs)
    {
        var halfFrame = timeMs * 15L / 100L;
        var column = halfFrame / Math.Max(1, textureScale);
        return rect.x + (column - leftColumn);
    }

    private static PlayerState FindPlayer(List<PlayerState> players, int number)
    {
        if (players == null)
        {
            return null;
        }
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].DeviceNumber == number)
            {
                return players[i];
            }
        }
        return null;
    }

    private static float PlaybackProgress(PlayerState player)
    {
        if (player == null)
        {
            return 0f;
        }

        var played = player.BltTimePlayedMs > 0 ? player.BltTimePlayedMs : player.PlaybackPositionMs;
        var duration = DisplayDurationMs(player);
        if (played <= 0 || duration <= 0)
        {
            return 0f;
        }
        return Mathf.Clamp01(played / (float)duration);
    }

    private static string LayerName(LayerState layer)
    {
        if (layer == null || layer.SourceKind == LayerSourceKind.None)
        {
            return "empty";
        }
        if (!string.IsNullOrEmpty(layer.SourceName))
        {
            return layer.SourceName;
        }
        return string.IsNullOrEmpty(layer.Path) ? layer.SourceKind.ToString() : Path.GetFileName(layer.Path);
    }

    private Texture2D LatestAlbumArtTexture(StateSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Players == null)
        {
            return null;
        }

        for (var i = 0; i < snapshot.Players.Count; i++)
        {
            var player = snapshot.Players[i];
            if (player != null && player.IsTempoMaster && player.AlbumArtTexture != null)
            {
                return player.AlbumArtTexture;
            }
        }

        for (var i = 0; i < snapshot.Players.Count; i++)
        {
            var player = snapshot.Players[i];
            if (player != null && player.AlbumArtTexture != null)
            {
                return player.AlbumArtTexture;
            }
        }

        return null;
    }

    private List<PlayerState> CollectAlbumArtPlayers(StateSnapshot snapshot)
    {
        var result = new List<PlayerState>();
        if (snapshot == null || snapshot.Players == null)
        {
            return result;
        }

        for (var i = 0; i < snapshot.Players.Count; i++)
        {
            var player = snapshot.Players[i];
            if (player != null && player.AlbumArtTexture != null)
            {
                result.Add(player);
            }
        }

        result.Sort((a, b) =>
        {
            if (a == null && b == null)
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            if (a.IsTempoMaster != b.IsTempoMaster)
            {
                return a.IsTempoMaster ? -1 : 1;
            }
            return a.DeviceNumber.CompareTo(b.DeviceNumber);
        });

        return result;
    }

    private Texture RenderEffectLayer(int hostIndex, LayerState hostLayer)
    {
        if (hostLayer == null || hostLayer.Effect == null || !hostLayer.Effect.HasEffect || hostLayer.EffectTexture == null || hostLayer.EffectScratchTexture == null || vjLayers == null)
        {
            return null;
        }

        if (hostLayer.Effect.TargetLayers == null || hostLayer.Effect.TargetLayers.Length != VjLayerCount)
        {
            hostLayer.Effect.TargetLayers = new bool[VjLayerCount];
        }

        hostLayer.EffectRenderInProgress = true;
        try
        {
            var compositeMaterial = EnsureLayerCompositeMaterial();
            var effectMaterial = EnsureLayerEffectMaterial();
            if (hostLayer.Effect.Kind == LayerEffectKind.StepSequencer)
            {
                var queue = CollectValidStepSequencerQueue(hostLayer.Effect, hostIndex);
                SyncStepSequencerMediaSlots(hostIndex, hostLayer.Effect);
                if (queue.Count == 0)
                {
                    Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectScratchTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                var beat = Mathf.Abs(Mathf.FloorToInt(CurrentVisualBeatFloat(CurrentVisualBpm())));
                var activeEntry = queue[beat % queue.Count];
                Texture sourceTexture = null;
                if (activeEntry.Kind == StepSequencerEntryKind.Layer)
                {
                    var sourceLayer = activeEntry.LayerIndex >= 0 && activeEntry.LayerIndex < vjLayers.Length ? vjLayers[activeEntry.LayerIndex] : null;
                    sourceTexture = LayerPreviewTextureForEffectInput(activeEntry.LayerIndex, sourceLayer);
                }
                else
                {
                    sourceTexture = ResolveStepSequencerMediaTexture(hostLayer.Effect, activeEntry.Path);
                }

                if (sourceTexture == null)
                {
                    Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectScratchTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                Graphics.Blit(sourceTexture, hostLayer.EffectScratchTexture);
                hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return hostLayer.EffectScratchTexture;
            }
            if (hostLayer.Effect.Kind == LayerEffectKind.Strobe)
            {
                if (effectMaterial == null)
                {
                    Graphics.Blit(Texture2D.whiteTexture, hostLayer.EffectScratchTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                var bpm = CurrentVisualBpm();
                ConfigureLayerEffectMaterial(effectMaterial, hostLayer.Effect, CurrentVisualBeatFloat(bpm), bpm);
                Graphics.Blit(Texture2D.whiteTexture, hostLayer.EffectScratchTexture, effectMaterial);
                hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return hostLayer.EffectScratchTexture;
            }

            if (EffectUsesLowerLayerCompositeInput(hostIndex, hostLayer.Effect))
            {
                var lowerCompositeInput = BuildLowerLayerCompositeInput(hostIndex, hostLayer);
                if (lowerCompositeInput == null)
                {
                    Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectScratchTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                if (effectMaterial == null)
                {
                    Graphics.Blit(lowerCompositeInput, hostLayer.EffectScratchTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                var lowerCompositeBpm = CurrentVisualBpm();
                ConfigureLayerEffectMaterial(effectMaterial, hostLayer.Effect, CurrentVisualBeatFloat(lowerCompositeBpm), lowerCompositeBpm);
                Graphics.Blit(lowerCompositeInput, hostLayer.EffectScratchTexture, effectMaterial);
                hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return hostLayer.EffectScratchTexture;
            }

            Graphics.Blit(Texture2D.blackTexture, hostLayer.EffectTexture);
            var current = hostLayer.EffectTexture;
            var scratch = hostLayer.EffectScratchTexture;
            Texture fallbackTexture = null;
            var hasCompositeInput = false;

            for (var i = 0; i < vjLayers.Length; i++)
            {
                if (i == hostIndex || i >= hostLayer.Effect.TargetLayers.Length || !hostLayer.Effect.TargetLayers[i])
                {
                    continue;
                }

                var sourceLayer = vjLayers[i];
                if (sourceLayer == null || !sourceLayer.Enabled || sourceLayer.SourceKind == LayerSourceKind.None)
                {
                    continue;
                }

                var sourceTexture = LayerPreviewTextureForEffectInput(i, sourceLayer);
                if (sourceTexture == null)
                {
                    continue;
                }

                if (fallbackTexture == null)
                {
                    fallbackTexture = sourceTexture;
                }
                hasCompositeInput = true;

                if (compositeMaterial == null)
                {
                    continue;
                }

                compositeMaterial.SetTexture("_OverlayTex", sourceTexture);
                compositeMaterial.SetFloat("_Opacity", 1f);
                compositeMaterial.SetFloat("_BlendMode", (float)sourceLayer.BlendMode);
                compositeMaterial.SetFloat("_HueShift", Mathf.Repeat(sourceLayer.HueShift, 1f));
                compositeMaterial.SetFloat("_ColorMode", (float)sourceLayer.ColorMode);
                compositeMaterial.SetFloat("_InvertAmount", Mathf.Clamp01(sourceLayer.InvertAmount));
                compositeMaterial.SetFloat("_MonochromeAmount", Mathf.Clamp01(sourceLayer.MonochromeAmount));
                Graphics.Blit(current, scratch, compositeMaterial);
                var swapComposite = current;
                current = scratch;
                scratch = swapComposite;
            }

            if (!hasCompositeInput)
            {
                Graphics.Blit(Texture2D.blackTexture, scratch);
                hostLayer.EffectRenderedOutput = scratch;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return scratch;
            }

            if (compositeMaterial == null)
            {
                hostLayer.EffectRenderedOutput = fallbackTexture;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return fallbackTexture;
            }

            if (effectMaterial == null)
            {
                hostLayer.EffectRenderedOutput = current;
                hostLayer.EffectRenderedFrame = Time.frameCount;
                return current;
            }

            var bpmNow = CurrentVisualBpm();
            ConfigureLayerEffectMaterial(effectMaterial, hostLayer.Effect, CurrentVisualBeatFloat(bpmNow), bpmNow);
            Graphics.Blit(current, scratch, effectMaterial);
            hostLayer.EffectRenderedOutput = scratch;
            hostLayer.EffectRenderedFrame = Time.frameCount;
            return scratch;
        }
        finally
        {
            hostLayer.EffectRenderInProgress = false;
        }
    }

    private void UpdateEffectLayers()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = 0; i < vjLayers.Length; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || !layer.Enabled || layer.SourceKind != LayerSourceKind.Effect || layer.Effect == null || !layer.Effect.HasEffect)
            {
                continue;
            }

            if (layer.EffectRenderedFrame != Time.frameCount)
            {
                RenderEffectLayer(i, layer);
            }
        }
    }

    private Texture2D LoadImageTexture(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            Texture2D cached;
            if (sourceImageTextureCache.TryGetValue(path, out cached) && cached != null)
            {
                return cached;
            }

            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sourceImageTextureCache[path] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            mediaBrowserError = ex.Message;
            return null;
        }
    }

    private void UpdateTextLayerTexture(LayerState layer)
    {
        if (layer == null || layer.TextSource == null || layer.TextSource.TextMesh == null)
        {
            return;
        }

        ApplyTextFontToLayer(layer);
        var text = string.IsNullOrEmpty(layer.TextContent) ? "TEXT" : layer.TextContent;
        layer.TextSource.TextMesh.text = text;
        layer.TextSource.TextMesh.fontSize = Mathf.Clamp(layer.TextFontSize, 12, 256);
        layer.TextSource.TextMesh.characterSize = Mathf.Clamp(96f / Mathf.Max(12f, layer.TextFontSize) * 0.12f, 0.045f, 0.16f);
    }

    private void EnsureTextFontOptions()
    {
        if (textFontOptions.Count > 0)
        {
            return;
        }

        try
        {
            var installed = Font.GetOSInstalledFontNames();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < PreferredTextFontNames.Length; i++)
            {
                var preferred = PreferredTextFontNames[i];
                for (var j = 0; j < installed.Length; j++)
                {
                    if (!string.Equals(installed[j], preferred, StringComparison.OrdinalIgnoreCase) || !seen.Add(installed[j]))
                    {
                        continue;
                    }
                    textFontOptions.Add(installed[j]);
                    break;
                }
            }

            if (textFontOptions.Count == 0)
            {
                Array.Sort(installed, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < installed.Length && textFontOptions.Count < 8; i++)
                {
                    if (!seen.Add(installed[i]))
                    {
                        continue;
                    }
                    textFontOptions.Add(installed[i]);
                }
            }
        }
        catch
        {
        }

        if (textFontOptions.Count == 0)
        {
            textFontOptions.Add("Arial");
        }
    }

    private string DefaultTextFontName()
    {
        EnsureTextFontOptions();
        return textFontOptions.Count > 0 ? textFontOptions[0] : "Arial";
    }

    private Font GetTextFont(string fontName, int fontSize)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (textFontCache.TryGetValue(fontName, out var cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var created = Font.CreateDynamicFontFromOSFont(fontName, Mathf.Clamp(fontSize, 12, 256));
            if (created != null)
            {
                textFontCache[fontName] = created;
                return created;
            }
        }
        catch
        {
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void ApplyTextFontToLayer(LayerState layer)
    {
        if (layer == null || layer.TextSource == null || layer.TextSource.TextMesh == null)
        {
            return;
        }

        var font = GetTextFont(layer.TextFontName, layer.TextFontSize);
        if (font == null)
        {
            return;
        }

        layer.TextSource.TextMesh.font = font;
        if (layer.TextSource.Renderer != null)
        {
            layer.TextSource.Renderer.sharedMaterial = font.material;
        }
    }

    private string DescribeLayerSourceResolution(LayerState layer)
    {
        if (layer == null)
        {
            return "--";
        }

        if (layer.SourceKind == LayerSourceKind.YouTube && layer.YouTube != null && layer.YouTube.StreamWidth > 0 && layer.YouTube.StreamHeight > 0)
        {
            return layer.YouTube.StreamWidth.ToString(CultureInfo.InvariantCulture) + "x" +
                   layer.YouTube.StreamHeight.ToString(CultureInfo.InvariantCulture);
        }

        if (layer.Player != null)
        {
            var playerWidth = (int)layer.Player.width;
            var playerHeight = (int)layer.Player.height;
            if (playerWidth > 0 && playerHeight > 0)
            {
                return playerWidth.ToString(CultureInfo.InvariantCulture) + "x" + playerHeight.ToString(CultureInfo.InvariantCulture);
            }

            var playerTexture = layer.Player.texture;
            if (playerTexture != null && playerTexture.width > 0 && playerTexture.height > 0)
            {
                return playerTexture.width.ToString(CultureInfo.InvariantCulture) + "x" + playerTexture.height.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (layer.UsesAvproVideo)
        {
            var avproTexture = AvproLayerTexture(layer);
            if (avproTexture != null && avproTexture.width > 0 && avproTexture.height > 0)
            {
                return avproTexture.width.ToString(CultureInfo.InvariantCulture) + "x" + avproTexture.height.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
        {
            var width = ReflectionInt(layer.KlakHapPlayer, "width");
            var height = ReflectionInt(layer.KlakHapPlayer, "height");
            if (width > 0 && height > 0)
            {
                return width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            }

            width = ReflectionInt(layer.KlakHapPlayer, "frameWidth");
            height = ReflectionInt(layer.KlakHapPlayer, "frameHeight");
            if (width > 0 && height > 0)
            {
                return width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (layer.StaticTexture != null && layer.StaticTexture.width > 0 && layer.StaticTexture.height > 0)
        {
            return layer.StaticTexture.width.ToString(CultureInfo.InvariantCulture) + "x" + layer.StaticTexture.height.ToString(CultureInfo.InvariantCulture);
        }

        return "--";
    }

    private static string FirstNonEmptyLineExcludingPrefix(string value, string excludedPrefix)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var lines = value.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            if (!string.IsNullOrEmpty(excludedPrefix) && line.StartsWith(excludedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            return line;
        }
        return null;
    }

    private static int ReflectionInt(object target, string memberName)
    {
        var value = GetReflectionValue(target, memberName);
        if (value == null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static double ReflectionDouble(object target, string memberName)
    {
        var value = GetReflectionValue(target, memberName);
        if (value == null)
        {
            return 0.0;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0.0;
        }
    }

    private static Vector2Int ParsePrintedResolution(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Vector2Int.zero;
        }

        var lines = value.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("RES:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = Regex.Match(line, @"RES:(\d+)x(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
                int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                return new Vector2Int(width, height);
            }
        }

        return Vector2Int.zero;
    }


    private void DestroySourceImageCache()
    {
        foreach (var texture in sourceImageTextureCache.Values)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        sourceImageTextureCache.Clear();
    }

    private static string ShortPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(user) && path.StartsWith(user, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(user.Length);
        }
        return path;
    }

    private void DrawCapturePreview(float width)
    {
        GUILayout.Space(10f);
        GUILayout.Label("HDMI Capture Preview", normalStyle);

        var texture = captureTexture;
        var previewHeight = Mathf.Clamp(width * 9f / 16f, 180f, 420f);
        var rect = GUILayoutUtility.GetRect(1f, previewHeight, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.02f, 0.025f, 0.03f, 1f), 0f, 4f);

        if (texture != null && texture.width > 16 && texture.height > 16)
        {
            var target = FitRect(rect, texture.width, texture.height);
            GUI.DrawTexture(target, texture, ScaleMode.StretchToFill, false);
            GUILayout.Label("Device: " + captureDeviceName + "   " + texture.width + "x" + texture.height, smallStyle);
        }
        else
        {
            var message = string.IsNullOrEmpty(captureError) ? "Waiting for capture frames..." : captureError;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 28f), message, normalStyle);
            GUILayout.Label("Device: " + PreferText(captureDeviceName, "--"), smallStyle);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Next Capture Device", GUILayout.Width(180f), GUILayout.Height(28f)))
        {
            SelectNextCaptureDevice();
        }
        if (GUILayout.Button("Restart Capture", GUILayout.Width(150f), GUILayout.Height(28f)))
        {
            StartCapturePreview();
        }
        GUILayout.Label("Devices: " + (captureDevices == null ? 0 : captureDevices.Length), smallStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Capture Audio", normalStyle);
        GUILayout.Label(string.IsNullOrEmpty(captureAudioDeviceName) ? "Input: disabled" : "Input: " + captureAudioDeviceName, smallStyle);
        var meterRect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        DrawCaptureAudioLevelMeter(meterRect);
        GUILayout.Label(captureAudioMonitorEnabled
                ? "Monitor: on  Vol " + Mathf.RoundToInt(captureAudioMonitorVolume * 100f).ToString(CultureInfo.InvariantCulture)
                : "Monitor: off",
            smallStyle);
    }

    private static Rect FitRect(Rect outer, float contentWidth, float contentHeight)
    {
        var contentAspect = contentWidth / Mathf.Max(1f, contentHeight);
        var outerAspect = outer.width / Mathf.Max(1f, outer.height);
        if (contentAspect > outerAspect)
        {
            var height = outer.width / contentAspect;
            return new Rect(outer.x, outer.y + (outer.height - height) * 0.5f, outer.width, height);
        }

        var width = outer.height * contentAspect;
        return new Rect(outer.x + (outer.width - width) * 0.5f, outer.y, width, outer.height);
    }

    private StateSnapshot Snapshot()
    {
        lock (stateLock)
        {
            var result = new StateSnapshot();
            result.LastError = lastError;
            foreach (var player in players.Values)
            {
                result.Players.Add(player.Clone());
            }
            result.Players.Sort((a, b) => a.DeviceNumber.CompareTo(b.DeviceNumber));
            result.LogLines.AddRange(logLines);
            return result;
        }
    }

    private void DrawPlayers(List<PlayerState> snapshotPlayers)
    {
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var player in snapshotPlayers)
        {
            GUILayout.Space(12f);
            GUILayout.Label("Player " + player.DeviceNumber + "  " + BuildFlags(player), normalStyle);
            GUILayout.Label(PreferText(player.DeviceName, "Unknown device") + "  " + player.Address, smallStyle);

            GUILayout.Label(TrackTitle(player), titleStyle);
            GUILayout.Label(TrackArtist(player), normalStyle);
            if (!string.IsNullOrEmpty(TrackComment(player)))
            {
                GUILayout.Label(TrackComment(player), smallStyle);
            }
            GUILayout.Label("BPM " + FormatBpm(DisplayTrackBpm(player)) +
                            "   Effective " + FormatBpm(DisplayEffectiveBpm(player)) +
                            "   Pitch " + player.PitchPercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%", smallStyle);
            GUILayout.Label("Time " + DisplayPlayedTime(player) +
                            " / " + DisplayDuration(player) +
                            "   Remain " + DisplayRemainingTime(player) +
                            "   Beat " + FormatBeat(player) +
                            "   Track #" + player.TrackNumber +
                            "   Rekordbox ID " + player.RekordboxId, smallStyle);
            GUILayout.Label("Source P" + player.TrackSourcePlayer +
                            " " + FormatTrackSourceSlot(player.TrackSourceSlot) +
                            "   " + FormatTrackType(player.TrackType) +
                            "   DB " + FormatDbServer(player) +
                            "   Last " + AgeText(player.LastSeenUtc), smallStyle);
            DrawWaveformPlaceholder(player);
        }
        GUILayout.EndScrollView();
    }

    private void DrawLogs(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        GUILayout.Label("Log", smallStyle);
        for (var i = 0; i < lines.Count; i++)
        {
            GUILayout.Label(lines[i], smallStyle);
        }
    }

    private void DrawWaveformPlaceholder(PlayerState player)
    {
        var rect = GUILayoutUtility.GetRect(1f, 72f, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.05f, 0.07f, 0.09f, 0.95f), 0f, 4f);

        if (player.BltWavePreview != null)
        {
            GUI.DrawTexture(rect, player.BltWavePreview, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 22f), "BLT runtime waveform", smallStyle);
            return;
        }
        if (player.DirectWaveformBytes != null)
        {
            DrawDirectRuntimeWaveform(player, rect, BltWaveDetailScale);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 22f), "Direct DJ Link runtime waveform", smallStyle);
            return;
        }

        var center = rect.y + rect.height * 0.55f;
        var sampleWidth = rect.width / player.Activity.Length;
        for (var i = 0; i < player.Activity.Length; i++)
        {
            var index = (player.ActivityIndex + i) % player.Activity.Length;
            var value = Mathf.Clamp01(player.Activity[index]);
            var h = Mathf.Max(2f, value * rect.height * 0.72f);
            var x = rect.x + i * sampleWidth;
            var bar = new Rect(x, center - h * 0.5f, Mathf.Max(1f, sampleWidth - 1f), h);
            GUI.DrawTexture(bar, whiteTexture, ScaleMode.StretchToFill, false, 0f, player.IsTempoMaster ? new Color(0.2f, 0.95f, 0.65f, 0.95f) : new Color(0.3f, 0.7f, 1f, 0.9f), 0f, 0f);
        }

        if (!player.HasPrecisePosition && !player.HasStatus)
        {
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 22f), "Waiting for status packets", smallStyle);
        }
        else
        {
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 22f), "Waveform image: metadata/waveform protocol pending", smallStyle);
        }
    }

    private string BuildFlags(PlayerState player)
    {
        var flags = new List<string>();
        if (player.IsTempoMaster)
        {
            flags.Add("MASTER");
        }
        if (player.IsPlaying)
        {
            flags.Add("PLAY");
        }
        if (player.IsSynced)
        {
            flags.Add("SYNC");
        }
        if (player.IsBpmOnlySynced)
        {
            flags.Add("BPM SYNC");
        }
        if (player.IsOnAir)
        {
            flags.Add("ON AIR");
        }
        return flags.Count == 0 ? "" : "[" + string.Join("] [", flags.ToArray()) + "]";
    }

    private static string TrackTitle(PlayerState player)
    {
        if (!string.IsNullOrEmpty(player.BltTitle))
        {
            return player.BltTitle;
        }
        if (player.RekordboxId > 0)
        {
            return "Track metadata pending";
        }
        return player.TrackNumber > 0 ? "Track #" + player.TrackNumber : "(no track)";
    }

    private static string TrackArtist(PlayerState player)
    {
        if (!string.IsNullOrEmpty(player.BltArtist))
        {
            var details = player.BltArtist;
            if (!string.IsNullOrEmpty(player.BltKey))
            {
                details += "   Key " + player.BltKey;
            }
            return details;
        }
        if (player.RekordboxId > 0)
        {
            return string.IsNullOrEmpty(player.MetadataError) || IsIgnorableDjLinkMetadataError(player.MetadataError)
                ? "Direct DJ Link metadata pending."
                : "Metadata error: " + player.MetadataError;
        }
        return "";
    }

    private static string TrackComment(PlayerState player)
    {
        return player == null ? "" : PreferText(player.BltComment, "");
    }

    private static bool IsIgnorableDjLinkMetadataError(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        return error.IndexOf("Unexpected metadata response 0x0100", StringComparison.OrdinalIgnoreCase) >= 0 ||
               error.IndexOf("Unexpected beat grid response 0x0100", StringComparison.OrdinalIgnoreCase) >= 0 ||
               error.IndexOf("Attempted to read past the end of the stream", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SupportsAdvancedDjLinkExtras(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            return false;
        }

        return deviceName.IndexOf("CDJ-3000", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLegacyNexusDeck(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            return false;
        }

        return deviceName.IndexOf("CDJ-2000NXS", StringComparison.OrdinalIgnoreCase) >= 0 ||
               deviceName.IndexOf("CDJ-2000NEXUS", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FormatBeat(PlayerState player)
    {
        var beat = player.BeatNumber >= 0 ? player.BeatNumber.ToString(CultureInfo.InvariantCulture) : "--";
        var bar = player.BeatWithinBar > 0 ? player.BeatWithinBar.ToString(CultureInfo.InvariantCulture) : "--";
        return beat + " (" + bar + "/4)";
    }

    private static string FormatBpm(float bpm)
    {
        return bpm > 0.01f ? bpm.ToString("0.0", CultureInfo.InvariantCulture) : "--";
    }

    private static void ApplyBpm(PlayerState player, float trackBpm, float effectiveBpm)
    {
        if (player == null)
        {
            return;
        }

        if (trackBpm > 0.01f)
        {
            player.Bpm = trackBpm;
            player.StableBpm = StabilizeBpm(player.StableBpm, trackBpm);
        }
        if (effectiveBpm > 0.01f)
        {
            player.EffectiveBpm = effectiveBpm;
            player.StableEffectiveBpm = StabilizeBpm(player.StableEffectiveBpm, effectiveBpm);
        }
    }

    private static float StabilizeBpm(float currentDisplayBpm, float nextBpm)
    {
        var rounded = Mathf.Round(nextBpm * 10f) / 10f;
        if (currentDisplayBpm <= 0.01f || Mathf.Abs(nextBpm - currentDisplayBpm) >= BpmDisplayDeadband)
        {
            return rounded;
        }
        return currentDisplayBpm;
    }

    private static float DisplayEffectiveBpm(PlayerState player)
    {
        if (player.StableEffectiveBpm > 0.01f)
        {
            return player.StableEffectiveBpm;
        }
        return player.EffectiveBpm > 0.01f ? player.EffectiveBpm : player.BltTempo;
    }

    private static float DisplayTrackBpm(PlayerState player)
    {
        if (player.StableBpm > 0.01f)
        {
            return player.StableBpm;
        }
        return player.Bpm > 0.01f ? player.Bpm : player.BltTempo;
    }

    private static string DisplayPlayedTime(PlayerState player)
    {
        if (!string.IsNullOrEmpty(player.BltTimePlayedDisplay))
        {
            return player.BltTimePlayedDisplay;
        }
        return FormatMillis(player.PlaybackPositionMs);
    }

    private static string DisplayRemainingTime(PlayerState player)
    {
        if (!string.IsNullOrEmpty(player.BltTimeRemainingDisplay))
        {
            return player.BltTimeRemainingDisplay;
        }
        if (player.BltTimeRemainingMs > 0)
        {
            return FormatMillis(player.BltTimeRemainingMs);
        }

        var duration = DisplayDurationMs(player);
        var played = CurrentPlaybackMs(player);
        return duration > 0 && played >= 0 ? FormatMillis(Mathf.Max(0, duration - played)) : "--:--";
    }

    private static string DisplayDuration(PlayerState player)
    {
        var duration = DisplayDurationMs(player);
        return duration > 0 ? FormatMillis(duration) : "--:--";
    }

    private static int DisplayDurationMs(PlayerState player)
    {
        if (player == null)
        {
            return 0;
        }
        if (player.BltDurationSeconds > 0)
        {
            return player.BltDurationSeconds * 1000;
        }
        return player.TrackLengthMs > 0 ? player.TrackLengthMs : 0;
    }

    private static string FormatMillis(int millis)
    {
        if (millis <= 0)
        {
            return "--:--";
        }

        var totalSeconds = millis / 1000;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string AgeText(DateTime utc)
    {
        if (utc == default(DateTime))
        {
            return "never";
        }

        var seconds = Math.Max(0.0, (DateTime.UtcNow - utc).TotalSeconds);
        return seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s ago";
    }

    private static string FormatTrackSourceSlot(int slot)
    {
        switch (slot)
        {
            case 0:
                return "No track";
            case 1:
                return "CD";
            case 2:
                return "SD";
            case 3:
                return "USB";
            case 4:
                return "Collection";
            default:
                return "Slot 0x" + slot.ToString("x2", CultureInfo.InvariantCulture);
        }
    }

    private static string FormatTrackType(int trackType)
    {
        switch (trackType)
        {
            case 0:
                return "No track";
            case 1:
                return "Rekordbox";
            case 2:
                return "Unanalyzed";
            case 5:
                return "CD audio";
            default:
                return "Type 0x" + trackType.ToString("x2", CultureInfo.InvariantCulture);
        }
    }

    private static string FormatDbServer(PlayerState player)
    {
        if (player.MetadataQueryInFlight)
        {
            return "metadata";
        }
        if (player.DbServerPort > 0 && player.DbServerPort < 65535)
        {
            return player.DbServerPort.ToString(CultureInfo.InvariantCulture);
        }
        if (player.DbServerQueryInFlight)
        {
            return "querying";
        }
        if (!string.IsNullOrEmpty(player.DbServerError))
        {
            return "not found";
        }
        return "--";
    }

    private string BltStatusText()
    {
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            return "Unity direct";
        }
        if (!useBltBridgeMode)
        {
            return "disabled (direct mode)";
        }
        if (!string.IsNullOrEmpty(bltError))
        {
            return bltError;
        }
        if (!bltReceived)
        {
            return "waiting at " + bltParamsUrl;
        }
        return "connected " + lastBltUpdateUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        whiteTexture = Texture2D.whiteTexture;
        panelTexture = MakeTexture(new Color(0f, 0f, 0f, 0.78f));
        triangleUpTexture = MakeTriangleTexture(true);
        triangleDownTexture = MakeTriangleTexture(false);

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;

        normalStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true
        };
        normalStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true
        };
        smallStyle.normal.textColor = new Color(0.82f, 0.9f, 0.94f, 1f);

        markerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        markerStyle.normal.textColor = Color.white;

        hotCueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };
        hotCueStyle.normal.textColor = Color.white;

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(16, 16, 16, 16)
        };
        boxStyle.normal.background = panelTexture;
    }

    private static Texture2D MakeTexture(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeTriangleTexture(bool up)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color(1f, 1f, 1f, 0f);
        var solid = Color.white;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var row = up ? y : size - 1 - y;
                var halfWidth = row * 0.5f;
                var center = (size - 1) * 0.5f;
                tex.SetPixel(x, y, Mathf.Abs(x - center) <= halfWidth ? solid : clear);
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static bool HasMagicHeader(byte[] data)
    {
        if (data.Length < MagicHeader.Length + 1)
        {
            return false;
        }

        for (var i = 0; i < MagicHeader.Length; i++)
        {
            if (data[i] != MagicHeader[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int ReadByte(byte[] data, int offset)
    {
        return offset >= 0 && offset < data.Length ? data[offset] & 0xff : 0;
    }

    private static long ReadUInt(byte[] data, int offset, int count)
    {
        long result = 0;
        for (var i = 0; i < count; i++)
        {
            result = (result << 8) + ReadByte(data, offset + i);
        }
        return result;
    }

    private static int ReadSignedInt(byte[] data, int offset, int count)
    {
        var value = ReadUInt(data, offset, count);
        var signBit = 1L << ((count * 8) - 1);
        if ((value & signBit) != 0)
        {
            value -= 1L << (count * 8);
        }
        return (int)value;
    }

    private static string ReadText(byte[] data, int offset, int count)
    {
        if (offset < 0 || offset >= data.Length)
        {
            return "";
        }

        var safeCount = Math.Min(count, data.Length - offset);
        return Encoding.ASCII.GetString(data, offset, safeCount).Trim('\0', ' ', '\t', '\r', '\n');
    }

    private static string FormatHardwareAddress(byte[] data, int offset)
    {
        if (data.Length < offset + 6)
        {
            return "";
        }

        var parts = new string[6];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = data[offset + i].ToString("x2", CultureInfo.InvariantCulture);
        }
        return string.Join(":", parts);
    }

    private static int PercentageToPitch(double percentage)
    {
        return (int)Math.Round((percentage * 1048576.0 / 100.0) + 1048576.0);
    }

    private static float PitchToPercentage(int pitchRaw)
    {
        if (pitchRaw <= 0)
        {
            return 0f;
        }
        return (float)((pitchRaw - 1048576) / 10485.76);
    }

    private static int ClampToInt(long value)
    {
        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (value < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)value;
    }

    private static string PreferText(string first, string fallback)
    {
        return string.IsNullOrEmpty(first) ? fallback : first;
    }

    private static Dictionary<string, object> Dict(Dictionary<string, object> dict, string key)
    {
        object value;
        return dict != null && dict.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
    }

    private static string Text(Dictionary<string, object> dict, string key, string fallback)
    {
        object value;
        if (dict == null || !dict.TryGetValue(key, out value) || value == null)
        {
            return fallback;
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static int Int(Dictionary<string, object> dict, string key, int fallback)
    {
        object value;
        if (dict == null || !dict.TryGetValue(key, out value) || value == null)
        {
            return fallback;
        }
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static float Float(Dictionary<string, object> dict, string key, float fallback)
    {
        object value;
        if (dict == null || !dict.TryGetValue(key, out value) || value == null)
        {
            return fallback;
        }
        try
        {
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool Bool(Dictionary<string, object> dict, string key, bool fallback)
    {
        object value;
        if (dict == null || !dict.TryGetValue(key, out value) || value == null)
        {
            return fallback;
        }
        if (value is bool)
        {
            return (bool)value;
        }
        bool parsed;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : fallback;
    }

    private static List<string> SortedKeys(Dictionary<string, object> dict)
    {
        var keys = new List<string>(dict.Keys);
        keys.Sort((a, b) =>
        {
            int ai;
            int bi;
            if (int.TryParse(a, out ai) && int.TryParse(b, out bi))
            {
                return ai.CompareTo(bi);
            }
            return string.CompareOrdinal(a, b);
        });
        return keys;
    }

    private static class NativeFolderPicker
    {
        private const int ErrorCancelled = unchecked((int)0x800704C7);
        private const uint SigdnFileSystemPath = 0x80058000;

        public static string PickFolder(string initialPath)
        {
            IFileOpenDialog dialog = null;
            IShellItem initialFolder = null;
            IShellItem result = null;
            IntPtr displayName = IntPtr.Zero;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                FileOpenOptions options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options |
                                  FileOpenOptions.PickFolders |
                                  FileOpenOptions.ForceFileSystem |
                                  FileOpenOptions.PathMustExist |
                                  FileOpenOptions.NoChangeDir);
                dialog.SetTitle("Select media root folder");
                dialog.SetOkButtonLabel("Use This Folder");

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    var iid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref iid, out initialFolder) == 0 && initialFolder != null)
                    {
                        dialog.SetFolder(initialFolder);
                    }
                }

                var hr = dialog.Show(IntPtr.Zero);
                if (hr == ErrorCancelled)
                {
                    return null;
                }
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                dialog.GetResult(out result);
                result.GetDisplayName(SigdnFileSystemPath, out displayName);
                return Marshal.PtrToStringUni(displayName);
            }
            finally
            {
                if (displayName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(displayName);
                }
                if (result != null)
                {
                    Marshal.ReleaseComObject(result);
                }
                if (initialFolder != null)
                {
                    Marshal.ReleaseComObject(initialFolder);
                }
                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItem ppv);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport]
        [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(FileOpenOptions fos);
            void GetOptions(out FileOpenOptions pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(IntPtr ppenum);
            void GetSelectedItems(IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [Flags]
        private enum FileOpenOptions : uint
        {
            NoChangeDir = 0x00000008,
            PickFolders = 0x00000020,
            ForceFileSystem = 0x00000040,
            PathMustExist = 0x00000800
        }
    }

    private sealed class NativeThumbnail
    {
        public int Width;
        public int Height;
        public byte[] Pixels;
    }

    private static class NativeThumbnailExtractor
    {
        private const uint SisiThumbnailOnly = 0x00000008;

        public static NativeThumbnail Extract(string path, int width, int height)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            IShellItem shellItem = null;
            IShellItemImageFactory factory = null;
            IntPtr bitmap = IntPtr.Zero;
            try
            {
                var iid = typeof(IShellItem).GUID;
                var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out shellItem);
                if (hr < 0 || shellItem == null)
                {
                    return null;
                }

                factory = shellItem as IShellItemImageFactory;
                if (factory == null)
                {
                    return null;
                }

                var size = new NativeSize { cx = width, cy = height };
                hr = factory.GetImage(size, SisiThumbnailOnly, out bitmap);
                if (hr < 0 || bitmap == IntPtr.Zero)
                {
                    return null;
                }

                return BitmapToTextureBytes(bitmap);
            }
            finally
            {
                if (bitmap != IntPtr.Zero)
                {
                    DeleteObject(bitmap);
                }
                if (factory != null)
                {
                    Marshal.ReleaseComObject(factory);
                }
                else if (shellItem != null)
                {
                    Marshal.ReleaseComObject(shellItem);
                }
            }
        }

        private static NativeThumbnail BitmapToTextureBytes(IntPtr bitmap)
        {
            NativeBitmap nativeBitmap;
            if (GetObject(bitmap, Marshal.SizeOf(typeof(NativeBitmap)), out nativeBitmap) == 0 ||
                nativeBitmap.Width <= 0 || nativeBitmap.Height <= 0)
            {
                return null;
            }

            var width = nativeBitmap.Width;
            var height = nativeBitmap.Height;
            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader)),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    SizeImage = (uint)(width * height * 4)
                }
            };

            var bgra = new byte[width * height * 4];
            var screen = GetDC(IntPtr.Zero);
            try
            {
                var lines = GetDIBits(screen, bitmap, 0, (uint)height, bgra, ref bitmapInfo, 0);
                if (lines == 0)
                {
                    return null;
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
            }

            var rgba = new byte[bgra.Length];
            for (var i = 0; i < bgra.Length; i += 4)
            {
                rgba[i] = bgra[i + 2];
                rgba[i + 1] = bgra[i + 1];
                rgba[i + 2] = bgra[i];
                rgba[i + 3] = bgra[i + 3] == 0 ? (byte)255 : bgra[i + 3];
            }

            return new NativeThumbnail { Width = width, Height = height, Pixels = rgba };
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItem ppv);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, out NativeBitmap lpObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BitmapInfo lpbmi, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(NativeSize size, uint flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeBitmap
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public IntPtr Bits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }
    }

    private sealed class StateSnapshot
    {
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<string> LogLines = new List<string>();
        public string LastError;
    }

    private sealed class OutputDeviceOption
    {
        public string Id;
        public string Label;
        public int Index;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    private sealed class MidiInputDeviceOption
    {
        public string Id;
        public string Label;
        public int Index;
    }

    [Serializable]
    private sealed class MidiBindingConfig
    {
        public string SelectedInputId;
        public List<MidiBinding> Bindings = new List<MidiBinding>();
    }

    [Serializable]
    private sealed class MidiBinding
    {
        public MidiMessageKind Kind;
        public int Channel;
        public int Number;
        public MidiBindingAction Action;
        public int Index;
        public int SubIndex = -1;
    }

    private sealed class MidiInputMessage
    {
        public MidiMessageKind Kind;
        public int Channel;
        public int Number;
        public int Value;
        public float NormalizedValue;
    }

    private sealed class MonitorOutputInfo
    {
        public string DeviceName;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool Primary;
    }

    private sealed class LayerState
    {
        public VideoPlayer Player;
        public RenderTexture Texture;
        public RenderTexture EffectTexture;
        public RenderTexture EffectScratchTexture;
        public Texture EffectRenderedOutput;
        public int EffectRenderedFrame = -1;
        public bool EffectRenderInProgress;
        public string Path;
        public LayerSourceKind SourceKind;
        public LayerSourceOrigin SourceOrigin;
        public LayerSourceGroupMode SourceGroupMode = LayerSourceGroupMode.Main;
        public string SourceName;
        public int CaptureDeviceIndex = -1;
        public GeneratorState Generator;
        public TextSourceState TextSource;
        public YoutubeState YouTube;
        public LayerBlendMode BlendMode = LayerBlendMode.Alpha;
        public string VideoStatus;
        public int VideoLoadToken;
        public Component AvproMediaPlayer;
        public bool UsesAvproVideo;
        public Component KlakHapPlayer;
        public bool UsesKlakHapVideo;
        public Texture2D StaticTexture;
        public bool OwnsStaticTexture;
        public bool Enabled;
        public bool AudioOutputEnabled = true;
        public bool ProgramMuted;
        public string DetectedVideoCodec;
        public float Opacity;
        public float HueShift;
        public float InvertAmount;
        public float MonochromeAmount;
        public LayerColorMode ColorMode;
        public VideoPlaybackMode VideoMode = VideoPlaybackMode.Bpm;
        public float VideoBaseBpm = 120f;
        public string VideoBaseBpmInput = "120";
        public bool VideoResyncPending = true;
        public string TextContent = "TEXT";
        public string TextInput = "TEXT";
        public int TextFontSize = 96;
        public string TextFontSizeInput = "96";
        public string TextFontName;
        public LayerEffectState Effect = new LayerEffectState();
    }

    private sealed class LayerEffectState
    {
        public LayerEffectKind Kind;
        public LayerRgbEffectMode RgbMode = LayerRgbEffectMode.RgbInvert;
        public bool Enabled = true;
        public bool[] TargetLayers = new bool[VjLayerCount];
        public List<StepSequencerEntry> StepSequenceQueue = new List<StepSequencerEntry>();
        public LayerState[] StepSequenceMediaLayers = new LayerState[StepSequencerMaxMediaLayers];
        public float Intensity = 1f;
        public LayerEffectMode Mode = LayerEffectMode.Horizontal;
        public int StepSequenceLength = 2;
        public float ManualRateHz = 8f;
        public string ManualRateInput = "8.0";
        public bool ManualFlashHeld;
        public string ActiveStepMediaPath;
        public Texture2D ActiveStepImageTexture;
        public bool ActiveStepImageOwnsTexture;

        public bool HasEffect
        {
            get { return Kind != LayerEffectKind.None; }
        }
    }

    private sealed class YoutubeState
    {
        public string VideoId;
        public string Title;
        public string Author;
        public string Url;
        public string UrlInput;
        public string DirectUrl;
        public string Error;
        public bool Resolving;
        public bool WaveformAnalyzing;
        public string WaveformStatus;
        public string WaveformKey;
        public string WaveformCachePath;
        public string LocalCachePath;
        public Texture2D WaveformTexture;
        public string AlignmentStatus;
        public int StreamWidth;
        public int StreamHeight;
        public YoutubePlaybackMode PlaybackMode = YoutubePlaybackMode.UrlBest;
        public float PlaybackSpeed = 1f;
        public double KnownLengthSeconds;
        public string SpeedInput = "1.00";
        public string TimeInput = "0.0";
        public int WaveformPlayerNumber = 1;
    }

    private enum YoutubePlaybackMode
    {
        UrlCompatible = 0,
        UrlBest = 1,
        LocalCache = 2
    }

    private enum StepSequencerEntryKind
    {
        Layer = 0,
        MediaFile = 1
    }

    [Serializable]
    private sealed class StepSequencerEntry
    {
        public StepSequencerEntryKind Kind;
        public int LayerIndex = -1;
        public string Path;
    }

    [Serializable]
    private sealed class ExternalPreviewWindowData
    {
        public string Title;
        public string Status;
        public bool FourDeck;
        public List<ExternalPreviewPlayerCard> Players = new List<ExternalPreviewPlayerCard>();
    }

    [Serializable]
    private sealed class ExternalPreviewPlayerCard
    {
        public int Number;
        public string Flags;
        public string Device;
        public string Title;
        public string Artist;
        public string Comment;
        public string BpmLine;
        public string TimeLine;
        public string BeatLine;
        public string RuntimeWavePath;
        public string OverviewWavePath;
        public string AlbumArtPath;
    }

    private sealed class CdjMonitorViewData
    {
        public string Title;
        public string Status;
        public bool FourDeck;
        public string LastError;
        public List<CdjMonitorDeckCardViewData> Players = new List<CdjMonitorDeckCardViewData>();
    }

    private sealed class CdjMonitorDeckCardViewData
    {
        public int Number;
        public PlayerState Player;
        public string Flags;
        public string Device;
        public string Title;
        public string Artist;
        public string Comment;
        public string BpmLine;
        public string TimeLine;
        public string BeatLine;
        public Texture AlbumArtTexture;
        public Texture RuntimeWaveTexture;
        public Texture OverviewWaveTexture;
        public bool HasOverviewPlaybackRatio;
        public float OverviewPlaybackRatio;
        public bool HasOverviewCueRatio;
        public float OverviewCueRatio;
    }

    [Serializable]
    private sealed class ExternalSettingsWindowData
    {
        public string LayerLabel;
        public string Title;
        public string Subtitle;
        public string Status;
        public string PreviewPath;
        public string[] Details;
        public int SelectedLayerIndex = -1;
        public bool Enabled;
        public bool AudioEnabled;
        public float Opacity;
        public string BlendMode;
        public List<ExternalSettingsWindowLayerOption> LayerOptions = new List<ExternalSettingsWindowLayerOption>();
    }

    [Serializable]
    private sealed class ExternalSettingsWindowLayerOption
    {
        public int Index;
        public string Label;
        public string Name;
    }

    private sealed class SettingsViewData
    {
        public int SelectedLayerIndex = -1;
        public string LayerLabel;
        public string Title;
        public string Subtitle;
        public string Status;
        public string[] Details;
        public string PresetInfo;
        public string[] CommonDetails;
        public string[] EffectDetails;
        public string SourceDetailTitle;
        public string[] SourceDetails;
        public bool Enabled;
        public bool AudioEnabled;
        public float Opacity;
        public string BlendMode;
        public Texture PreviewTexture;
    }

    private struct CdjMonitorScreenLayout
    {
        public bool FourDeck;
        public Rect TitleRect;
        public Rect DeckButtonsRect;
        public Rect HintRect;
        public Rect ErrorRect;
        public Rect[] PanelRects;
    }

    private struct CdjPlayerPanelLayout
    {
        public float ContentLeft;
        public float ContentTop;
        public float ContentWidth;
        public Rect HeaderRect;
        public Rect FlagsRect;
        public Rect SearchButtonRect;
        public Rect AlbumArtRect;
        public Rect DeviceRect;
        public Rect TitleRect;
        public Rect ArtistRect;
        public Rect CommentRect;
        public Rect RuntimeWaveRect;
        public Rect RuntimeWavePlaceholderRect;
        public Rect OverviewWaveRect;
        public Rect OverviewWavePlaceholderRect;
        public Rect BpmLineRect;
        public Rect TimeLineRect;
        public Rect BeatLineRect;
    }

    private sealed class AuxPreviewDeckUi
    {
        public GameObject Root;
        public Image Panel;
        public Text Header;
        public Text Flags;
        public UiButtonBundle SearchButton;
        public RawImage AlbumArt;
        public Text Device;
        public Text Title;
        public Text Artist;
        public Text Comment;
        public RawImage RuntimeWave;
        public Text RuntimeWavePlaceholder;
        public Image RuntimeWaveMarker;
        public RawImage OverviewWave;
        public Text OverviewWavePlaceholder;
        public Image OverviewPlaybackMarker;
        public Image OverviewCueMarker;
        public Text BpmLine;
        public Text TimeLine;
        public Text BeatLine;
    }

    private sealed class UiButtonBundle
    {
        public Button Button;
        public Text Label;
        public Image Background;
        public RawImage Thumbnail;
        public Text Badge;
    }

    private sealed class YoutubeSearchResult
    {
        public string VideoId;
        public string Title;
        public string Author;
    }

    private sealed class YoutubeResolvedVideoInfo
    {
        public string Source;
        public string DirectUrl;
        public int Width;
        public int Height;
        public string Error;
        public YoutubePlaybackMode PlaybackMode;
        public DateTime CachedAtUtc;
    }

    [Serializable]
    private sealed class YoutubeServerVideoResponse
    {
        public bool success;
        public string error;
        public string url;
        public string watchUrl;
        public int width;
        public int height;
    }

    private sealed class GeneratorState
    {
        public GameObject Root;
        public Transform Pivot;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public List<GeneratorPart> Parts;
        public List<GeneratorPart> TunnelParts;
        public Material Material;
        public Camera Camera;
        public Skybox Skybox;
        public GeneratorPresentationMode PresentationMode;
        public GameObject Ground;
        public MeshRenderer GroundRenderer;
        public Material GroundMaterial;
        public GameObject SurroundScreenRoot;
        public List<GeneratorScreen> SurroundScreens;
        public Texture CurrentTexture;
        public Texture SurroundScreenCurrentTexture;
        public RenderTexture Texture;
        public int RenderLayer;
        public int TextureSourceLayerIndex = -1;
        public string TextureSourceLayerName;
        public GeneratorShape Shape;
        public GeneratorCameraWorkMode CameraWorkMode;
        public GeneratorObjectOrbitMode ObjectOrbitMode;
        public bool ObjectSpinEnabled;
        public bool ObjectBpmPulseEnabled;
        public float CameraDistance;
        public bool ScreenVisible;
        public bool ParticlesEnabled;
        public bool SurroundScreensEnabled;
        public int SurroundScreenSourceLayerIndex = -1;
        public string SurroundScreenSourceLayerName;
        public int TunnelPermutationBeatIndex;
        public int TunnelPermutationSignature;
        public GeneratorTunnelShiftStep[] TunnelPlannedSteps;
        public int TunnelPlannedCycleIndex;
        public GeneratorTunnelShiftStep TunnelLastShiftStep;
        public float[] TunnelCommittedRowShiftX;
        public float[] TunnelCommittedColumnShiftY;
        public float[] TunnelCommittedSliceShiftZ;
        public string FaceImageFolderPath;
        public string FaceImageFolderInput;
        public Vector2 FaceImageScroll;
        public List<string> FaceImagePaths = new List<string>();
        public string FaceImageError;
        public string FaceTextureImagePath;
        public int FaceTextureBltPlayerNumber;
        public GeneratorSavedTextureSourceKind SavedTextureSourceKind;
        public bool TransparentBackground;
        public float MotionSeed;
        public string ScriptText;
        public string ScriptError;
        public bool ScriptDirty = true;
        public List<GeneratorKeyframe> ScriptKeys;
        public Material SkyboxMaterial;
        public string SkyboxName;
        public GameObject ModelInstance;
        public string ModelName;
        public string ModelPositionInput;
        public string ModelRotationInput;
        public string ModelScaleInput;
        public GameObject SceneBuildingsInstance;
        public string SceneBuildingsName;
        public GameObject SceneGroundInstance;
        public string SceneGroundName;
        public GameObject SceneTrafficInstance;
        public string SceneTrafficName;
        public GameObject SceneVegetationInstance;
        public string SceneVegetationName;
        public GameObject ParticleRoot;
        public List<GeneratorPart> ParticleParts;
        public GameObject LightingRoot;
        public List<GeneratorMovingLight> LightingLights;
        public GeneratorLightingController LightingController;
        public bool LightingSceneInitialized;
        public GeneratorLightingRigMode LightingRigMode;
        public string PresetNameInput = "Preset";
        public Vector2 PresetScroll;
    }

    private sealed class GeneratorScreen
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Material Material;
        public Vector3 Direction;
    }

    private sealed class GeneratorPart
    {
        public Transform Transform;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public Material Material;
    }

    private sealed class GeneratorMovingLight
    {
        public Transform Root;
        public Transform PanTransform;
        public Transform TiltTransform;
        public Transform HeadTransform;
        public MeshRenderer HeadRenderer;
        public MeshRenderer BeamRenderer;
        public Material HeadMaterial;
        public Material BeamMaterial;
        public Light SpotLight;
        public Component MovingLightComponent;
        public Component PanComponent;
        public Component TiltComponent;
        public Component HeadComponent;
        public Component BeamComponent;
    }

    private sealed class GeneratorLightingController
    {
        public GameObject Root;
        public Transform Target;
        public Component GroupComponent;
        public Component PerformanceComponent;
    }

    private sealed class GeneratorKeyframe
    {
        public float Beat;
        public string Ease = "linear";
        public Vector3 ObjPos = Vector3.zero;
        public Vector3 ObjRot = Vector3.zero;
        public float ObjScale = 1f;
        public Vector3 CamPos = new Vector3(0f, 0f, -4f);
        public Vector3 CamRot = Vector3.zero;
        public float CamFov = 45f;

        public static GeneratorKeyframe Default()
        {
            return new GeneratorKeyframe();
        }

        public GeneratorKeyframe Clone()
        {
            return new GeneratorKeyframe
            {
                Beat = Beat,
                Ease = Ease,
                ObjPos = ObjPos,
                ObjRot = ObjRot,
                ObjScale = ObjScale,
                CamPos = CamPos,
                CamRot = CamRot,
                CamFov = CamFov
            };
        }
    }

    private sealed class BrowserVideoSlot
    {
        public Texture2D Texture;
        public string Path;
        public bool ThumbnailReady;
        public string ThumbnailError;
    }

    private sealed class MediaFolderScanResult
    {
        public string Folder;
        public List<string> Folders;
        public List<string> Videos;
        public List<string> Images;
        public string Error;
    }

    [Serializable]
    private sealed class MediaFolderSnapshotRecord
    {
        public string Folder;
        public List<string> ChildFolders = new List<string>();
        public List<string> Videos = new List<string>();
        public List<string> Images = new List<string>();

        public void Normalize()
        {
            if (!string.IsNullOrEmpty(Folder))
            {
                Folder = Path.GetFullPath(Folder);
            }

            ChildFolders = NormalizePaths(ChildFolders);
            Videos = NormalizePaths(Videos);
            Images = NormalizePaths(Images);
        }

        public MediaFolderSnapshotRecord Clone()
        {
            return new MediaFolderSnapshotRecord
            {
                Folder = Folder,
                ChildFolders = ChildFolders == null ? new List<string>() : new List<string>(ChildFolders),
                Videos = Videos == null ? new List<string>() : new List<string>(Videos),
                Images = Images == null ? new List<string>() : new List<string>(Images)
            };
        }

        public static List<string> NormalizePaths(List<string> paths)
        {
            var normalized = new List<string>();
            if (paths == null)
            {
                return normalized;
            }

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                try
                {
                    normalized.Add(Path.GetFullPath(path));
                }
                catch
                {
                }
            }

            normalized.Sort(StringComparer.OrdinalIgnoreCase);
            return normalized;
        }
    }

    [Serializable]
    private sealed class MediaLibraryCacheFile
    {
        public string RootPath;
        public string CurrentFolder;
        public string SavedAtUtc;
        public List<MediaFolderSnapshotRecord> Entries = new List<MediaFolderSnapshotRecord>();
        public List<MediaPointerListRecord> PointerLists = new List<MediaPointerListRecord>();
        public int SelectedPointerListIndex;

        public void SortEntries()
        {
            if (Entries == null)
            {
                Entries = new List<MediaFolderSnapshotRecord>();
                return;
            }

            Entries.Sort((left, right) => string.Compare(left == null ? "" : left.Folder, right == null ? "" : right.Folder, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    private sealed class MediaPointerListRecord
    {
        public string Id;
        public string Name;
        public List<string> Paths = new List<string>();

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = "List";
            }
            Paths = MediaFolderSnapshotRecord.NormalizePaths(Paths);
        }

        public MediaPointerListRecord Clone()
        {
            return new MediaPointerListRecord
            {
                Id = Id,
                Name = Name,
                Paths = Paths == null ? new List<string>() : new List<string>(Paths)
            };
        }
    }

    private sealed class MediaLibraryScanResult
    {
        public string Root;
        public List<MediaFolderSnapshotRecord> Entries;
        public string Error;
    }

    [Serializable]
    private sealed class GeneratorPresetLibraryFile
    {
        public List<GeneratorPresetRecord> Presets = new List<GeneratorPresetRecord>();
    }

    [Serializable]
    private sealed class GeneratorPresetRecord
    {
        public string Id;
        public string Name;
        public string ThumbnailFileName;
        public string SavedAtUtc;
        public GeneratorPresetState State = new GeneratorPresetState();
        [NonSerialized] public Texture2D ThumbnailTexture;
    }

    [Serializable]
    private sealed class GeneratorPresetState
    {
        public GeneratorShape Shape;
        public GeneratorPresentationMode PresentationMode;
        public GeneratorCameraWorkMode CameraWorkMode;
        public GeneratorObjectOrbitMode ObjectOrbitMode;
        public bool ObjectSpinEnabled;
        public bool ObjectBpmPulseEnabled;
        public bool ScreenVisible = true;
        public bool ParticlesEnabled;
        public bool SurroundScreensEnabled;
        public int SurroundScreenSourceLayerIndex = -1;
        public bool TransparentBackground;
        public string SkyboxName;
        public string ModelName;
        public string ModelPositionInput;
        public string ModelRotationInput;
        public string ModelScaleInput;
        public string SceneBuildingsName;
        public string SceneGroundName;
        public string SceneTrafficName;
        public string SceneVegetationName;
        public string FaceImageFolderPath;
        public GeneratorSavedTextureSourceKind SavedTextureSourceKind;
        public string FaceTextureImagePath;
        public int FaceTextureBltPlayerNumber;
        public int TextureSourceLayerIndex = -1;
        public GeneratorLightingRigMode LightingRigMode;
        public string ScriptText;
    }

    private sealed class VideoThumbnailCacheEntry
    {
        public Texture2D Texture;
        public bool Ready;
        public string Error;
    }

    private sealed class TextSourceState
    {
        public GameObject Root;
        public Camera Camera;
        public TextMesh TextMesh;
        public MeshRenderer Renderer;
    }

    private sealed class MetadataQueryArgs
    {
        public int DeckPlayer;
        public int SourcePlayer;
        public string SourceDeviceName;
        public IPAddress Address;
        public int Port;
        public int TrackSourceSlot;
        public int TrackType;
        public long RekordboxId;
        public string Key;
    }

    private sealed class DirectTrackMetadata
    {
        public string Title;
        public string Artist;
        public string Album;
        public string Key;
        public string Genre;
        public int DurationSeconds;
        public int TempoRaw;
        public int ArtworkId;
    }

    private sealed class DirectWaveformResult
    {
        public byte[] Bytes;
        public DirectWaveformStyle Style;
    }

    private enum DbFieldKind
    {
        Number,
        Binary,
        String
    }

    private sealed class DbField
    {
        public DbFieldKind Kind;
        public int Size;
        public long Number;
        public byte[] Bytes;
        public string Text;
    }

    private sealed class DbFieldValue
    {
        public DbFieldValue(byte[] numberBytes)
        {
            Bytes = numberBytes;
            ArgumentTag = 0x06;
        }

        public byte ArgumentTag;
        public byte[] Bytes;
    }

    private sealed class DbMessage
    {
        public long Transaction;
        public int Type;
        public List<DbField> Arguments;
    }

    private sealed class PlayerState
    {
        public PlayerState(int deviceNumber, int activitySamples)
        {
            DeviceNumber = deviceNumber;
            Activity = new float[activitySamples];
        }

        public int DeviceNumber;
        public string DeviceName;
        public string Address;
        public string HardwareAddress;
        public int PeerCount;
        public bool HasAnnouncement;
        public bool HasPrecisePosition;
        public bool HasStatus;
        public bool LoggedAnnouncement;
        public bool LoggedStatus;
        public DateTime LastAnnouncementUtc;
        public DateTime LastPositionUtc;
        public DateTime LastStatusUtc;
        public DateTime LastSeenUtc;
        public DateTime LastActivitySampleUtc;
        public int TrackSourcePlayer;
        public int TrackSourceSlot;
        public int TrackType;
        public long RekordboxId;
        public int TrackNumber;
        public int StatusFlags;
        public int PitchRaw;
        public float PitchPercent;
        public int BpmRaw;
        public float Bpm;
        public float StableBpm;
        public float EffectiveBpm;
        public float StableEffectiveBpm;
        public int MasterHandoffDevice;
        public int BeatNumber = -1;
        public int CueCountdown;
        public int BeatWithinBar;
        public long PacketNumber;
        public bool IsTempoMaster;
        public bool IsPlaying;
        public bool IsSynced;
        public bool IsOnAir;
        public bool IsBpmOnlySynced;
        public bool HasActiveLoop;
        public int ActiveLoopStartMs;
        public int ActiveLoopEndMs;
        public int ActiveLoopBeats;
        public int TrackLengthMs;
        public int PlaybackPositionMs;
        public int DbServerPort;
        public bool DbServerQueryInFlight;
        public DateTime LastDbServerQueryUtc;
        public string DbServerError;
        public bool MetadataQueryInFlight;
        public DateTime LastMetadataQueryUtc;
        public string LastMetadataKey;
        public string MetadataError;
        public bool CueListQueryInFlight;
        public DateTime LastCueListQueryUtc;
        public string LastCueListKey;
        public bool BltSeen;
        public DateTime LastBltUpdateUtc;
        public string BltTitle;
        public string BltArtist;
        public string BltAlbum;
        public string BltComment;
        public string BltKey;
        public string BltGenre;
        public string BltColor;
        public string BltSlot;
        public string BltType;
        public int BltDurationSeconds;
        public int BltTimePlayedMs;
        public int BltTimeRemainingMs;
        public string BltTimePlayedDisplay;
        public string BltTimeRemainingDisplay;
        public float BltTempo;
        public int ArtworkId;
        public byte[] PendingAlbumArtBytes;
        public Texture2D AlbumArtTexture;
        public int AlbumArtWidth;
        public int AlbumArtHeight;
        public Texture2D BltWavePreview;
        public byte[] DirectWaveformBytes;
        public DirectWaveformStyle DirectWaveformStyle;
        public string DirectWaveformKey;
        public Texture2D DirectWaveformTexture;
        public string DirectWaveformTextureKey;
        public int DirectWaveformTextureScale;
        public int DirectWaveformTextureFrameCount;
        public int DirectWaveformTextureWidth;
        public int WaveformZoomIndex = DefaultWaveformZoomIndex;
        public List<BeatMarker> BeatGrid = new List<BeatMarker>();
        public List<CueMarker> CueMarkers = new List<CueMarker>();
        public float[] Activity;
        public int ActivityIndex;

        public void PushActivitySample(float value)
        {
            if (Activity == null || Activity.Length == 0)
            {
                return;
            }

            Activity[ActivityIndex] = value;
            ActivityIndex = (ActivityIndex + 1) % Activity.Length;
        }

        public PlayerState Clone()
        {
            var clone = (PlayerState)MemberwiseClone();
            clone.Activity = new float[Activity.Length];
            Array.Copy(Activity, clone.Activity, Activity.Length);
            clone.BeatGrid = BeatGrid == null ? new List<BeatMarker>() : new List<BeatMarker>(BeatGrid);
            clone.CueMarkers = CueMarkers == null ? new List<CueMarker>() : new List<CueMarker>(CueMarkers);
            return clone;
        }
    }

    private sealed class BeatMarker
    {
        public long TimeMs;
        public int BeatWithinBar;
        public int BpmRaw;
    }

    private sealed class CueMarker
    {
        public long TimeMs;
        public long LoopEndMs;
        public int HotCueNumber;
        public bool IsLoop;
        public string Label;
    }

    private static class MiniJson
    {
        public static object Parse(string json)
        {
            return new Parser(json).ParseValue();
        }

        private sealed class Parser
        {
            private readonly string json;
            private int index;

            public Parser(string json)
            {
                this.json = json ?? "";
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                var c = json[index];
                if (c == '{')
                {
                    return ParseObject();
                }
                if (c == '[')
                {
                    return ParseArray();
                }
                if (c == '"')
                {
                    return ParseString();
                }
                if (c == '-' || char.IsDigit(c))
                {
                    return ParseNumber();
                }
                if (Match("true"))
                {
                    return true;
                }
                if (Match("false"))
                {
                    return false;
                }
                if (Match("null"))
                {
                    return null;
                }

                throw new FormatException("Unexpected token at " + index.ToString(CultureInfo.InvariantCulture) + ".");
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();
                if (Peek('}'))
                {
                    index++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (Peek('}'))
                    {
                        index++;
                        return result;
                    }
                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (Peek(']'))
                {
                    index++;
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (Peek(']'))
                    {
                        index++;
                        return result;
                    }
                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (index < json.Length)
                {
                    var c = json[index++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (index >= json.Length)
                    {
                        throw new FormatException("Unterminated escape sequence.");
                    }
                    var esc = json[index++];
                    switch (esc)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            sb.Append(esc);
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            if (index + 4 > json.Length)
                            {
                                throw new FormatException("Invalid unicode escape.");
                            }
                            var hex = json.Substring(index, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default:
                            throw new FormatException("Invalid escape character: " + esc);
                    }
                }

                throw new FormatException("Unterminated string.");
            }

            private object ParseNumber()
            {
                var start = index;
                if (Peek('-'))
                {
                    index++;
                }
                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }

                var isFloat = false;
                if (Peek('.'))
                {
                    isFloat = true;
                    index++;
                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }
                if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
                {
                    isFloat = true;
                    index++;
                    if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    {
                        index++;
                    }
                    while (index < json.Length && char.IsDigit(json[index]))
                    {
                        index++;
                    }
                }

                var token = json.Substring(start, index - start);
                if (isFloat)
                {
                    return double.Parse(token, CultureInfo.InvariantCulture);
                }
                return long.Parse(token, CultureInfo.InvariantCulture);
            }

            private bool Match(string token)
            {
                if (string.CompareOrdinal(json, index, token, 0, token.Length) != 0)
                {
                    return false;
                }
                index += token.Length;
                return true;
            }

            private bool Peek(char c)
            {
                return index < json.Length && json[index] == c;
            }

            private void Expect(char c)
            {
                if (!Peek(c))
                {
                    throw new FormatException("Expected '" + c + "' at " + index.ToString(CultureInfo.InvariantCulture) + ".");
                }
                index++;
            }

            private void SkipWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }
        }
    }
}
