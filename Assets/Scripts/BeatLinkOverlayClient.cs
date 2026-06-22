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
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

public sealed partial class BeatLinkOverlayClient : MonoBehaviour
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
    private const int BltOverviewWaveHeight = 52;
    private const int BltWaveDetailScale = 8;
    private const int SourceLayerCount = 10;
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
    private const float YoutubeZoomWaveformWindowSeconds = 8f;
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
    private const int CaptureAudioSpectrumBandCount = 96;
    private const int CaptureAudioEqBandCount = 4;
    private const int CaptureAudioFftBinCount = 256;
    private const float CaptureAudioSpectrumDisplayMinHz = 20f;
    private const float CaptureAudioSpectrumDbFloor = -72f;
    private const float CaptureAudioSpectrumDbCeiling = 24f;
    private const float CaptureAudioSpectrumDisplayMaxHz = 20000f;
    private const float CaptureAudioEqGainMinDb = -18f;
    private const float CaptureAudioEqGainMaxDb = 18f;
    private const float CaptureAudioEqInputGainMinDb = -24f;
    private const float CaptureAudioEqInputGainMaxDb = 24f;
    private const float CaptureAudioEqOutputGainMinDb = -24f;
    private const float CaptureAudioEqOutputGainMaxDb = 24f;
    private const float CaptureAudioEqDisplayGainMinDb = 6f;
    private const float CaptureAudioEqDisplayGainMaxDb = 24f;
    private const float CaptureAudioEqQMin = 0.35f;
    private const float CaptureAudioEqQMax = 5f;
    private const float CaptureAudioAnalysisIntervalSeconds = 1f / 20f;
    private const float LayerScaleMin = 0.25f;
    private const float LayerScaleMax = 4f;
    private const long MediaResilienceCacheMaxVideoBytes = 128L * 1024L * 1024L;
    private const long MediaResilienceCacheMaxImageBytes = 32L * 1024L * 1024L;
    private const float MediaResilienceProbeIntervalSeconds = 0.75f;
    private const string CaptureAudioFftEnabledPrefKey = "BeatLinkOverlay.CaptureAudio.FftEnabled";
    private const string CaptureAudioSpectrumViewMinHzPrefKey = "BeatLinkOverlay.CaptureAudio.SpectrumViewMinHz";
    private const string CaptureAudioSpectrumViewMaxHzPrefKey = "BeatLinkOverlay.CaptureAudio.SpectrumViewMaxHz";
    private const string CaptureAudioEqBandFrequencyPrefKeyPrefix = "BeatLinkOverlay.CaptureAudio.EqBandFrequency.";
    private const string CaptureAudioEqBandGainPrefKeyPrefix = "BeatLinkOverlay.CaptureAudio.EqBandGain.";
    private const string CaptureAudioEqBandQPrefKeyPrefix = "BeatLinkOverlay.CaptureAudio.EqBandQ.";
    private const string CaptureAudioEqBandTypePrefKeyPrefix = "BeatLinkOverlay.CaptureAudio.EqBandType.";
    private const string CaptureAudioEqInputGainPrefKey = "BeatLinkOverlay.CaptureAudio.EqInputGainDb";
    private const string CaptureAudioEqOutputGainPrefKey = "BeatLinkOverlay.CaptureAudio.EqOutputGainDb";
    private const string CaptureAudioEqDisplayMaxGainPrefKey = "BeatLinkOverlay.CaptureAudio.EqDisplayMaxGainDb";
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
    private const string DjLinkInputModePrefKey = "BeatLinkOverlay.DjLink.InputMode";
    private const string DjLinkSimulatorDeckCountPrefKey = "BeatLinkOverlay.DjLink.SimulatorDeckCount";
    private const string DjLinkSimulatorBpmPrefKey = "BeatLinkOverlay.DjLink.SimulatorBpm";
    private const string DjLinkSimulatorPlayingPrefKey = "BeatLinkOverlay.DjLink.SimulatorPlaying";
    private const string PreviewExternalWindowPrefKey = "BeatLinkOverlay.PreviewExternalWindow";
    private const string SettingsExternalWindowPrefKey = "BeatLinkOverlay.SettingsExternalWindow";
    private const string SecondDisplayUiPrefKey = "BeatLinkOverlay.SecondDisplayUi";
    private const string CdjWaveDisplayPrefKey = "BeatLinkOverlay.Route.CdjWaveDisplay";
    private const string LayerSettingsDisplayPrefKey = "BeatLinkOverlay.Route.LayerSettingsDisplay";
    private const string EffectSettingsDisplayPrefKey = "BeatLinkOverlay.Route.EffectSettingsDisplay";
    private const string MainShortcutPrefKey = "BeatLinkOverlay.Shortcut.Main";
    private const string CdjWaveShortcutPrefKey = "BeatLinkOverlay.Shortcut.CdjWave";
    private const string LayerSettingsShortcutPrefKey = "BeatLinkOverlay.Shortcut.LayerSettings";
    private const string EffectSettingsShortcutPrefKey = "BeatLinkOverlay.Shortcut.EffectSettings";
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
    private DateTime lastOfficialBltStartAttemptUtc;
    private DateTime lastBltServerSeenUtc;
    private bool bltReceived;
    private bool bltServerReachable;
    private int bltActivePlayerCount;
    private bool useBltBridgeMode;
    private bool pendingBltBridgeMode;
    private DjLinkInputMode pendingDjLinkInputMode = DjLinkInputMode.InternalDirect;
    private bool useSimulatedDjLinkMode;
    private string pendingBltParamsUrl;
    private string officialBltExecutablePath;
    private int simulatedDjLinkDeckCount = 2;
    private float simulatedDjLinkBpm = 128f;
    private bool simulatedDjLinkPlaying = true;
    private int simulatedDjLinkTrackSeed;
    private float simulatedDjLinkTrackStartTime;
    private int simulatedDjLinkPausedPlaybackMs;
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
    private StateSnapshot cachedStateSnapshot;
    private int cachedStateSnapshotFrame = -1;
    private SettingsViewData cachedSettingsViewData;
    private int cachedSettingsViewDataFrame = -1;
    private int cachedSettingsViewLayerIndex = -1;
    private SettingsScreenTab selectedSettingsTab = SettingsScreenTab.Output;
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
    private string mediaResilienceCacheRoot;
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
#if UNITY_EDITOR
    private float nextEditorViewRepaintTime;
#endif
    private System.Diagnostics.Process previewExternalWindowProcess;
    private System.Diagnostics.Process settingsExternalWindowProcess;
    private readonly object mediaResilienceCacheLock = new object();
    private readonly HashSet<string> mediaResilienceCopyInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
    private GeneratorState settingsMonitorLastButtonGenerator;
    private string settingsMonitorLastPresetButtonSignature;
    private string settingsMonitorLastFaceButtonSignature;
    private Text settingsMonitorCommonTitleText;
    private Text settingsMonitorCommonText;
    private UiButtonBundle[] settingsMonitorCommonButtons;
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
    private const float GeneratorTexturePreviewRowHeight = 86f;
    private const int GeneratorTexturePreviewOverscanRows = 2;
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
    private readonly List<OutputDeviceOption> displayRouteDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> audioOutputDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> audioInputDevices = new List<OutputDeviceOption>();
    private readonly List<OutputDeviceOption> outputResolutionOptions = new List<OutputDeviceOption>();
    private bool outputDevicesDirty = true;
    private bool videoOutputDropdownOpen;
    private bool cdjWaveDisplayDropdownOpen;
    private bool layerSettingsDisplayDropdownOpen;
    private bool effectSettingsDisplayDropdownOpen;
    private bool displayRouteDragActive;
    private RoutedScreenKind displayRouteDragKind;
    private bool audioOutputDropdownOpen;
    private bool audioInputDropdownOpen;
    private bool outputResolutionDropdownOpen;
    private int layerBlendPopupLayerIndex = -1;
    private Rect layerBlendPopupRect;
    private Rect layerBlendPopupButtonRect;
    private bool layerBlendPopupAnchorValid;
    private int selectedVideoOutputIndex;
    private int selectedCdjWaveDisplayIndex;
    private int selectedLayerSettingsDisplayIndex;
    private int selectedEffectSettingsDisplayIndex;
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
    private AudioHighPassFilter captureAudioHighPassFilter;
    private AudioLowPassFilter captureAudioLowPassFilter;
    private AudioListener captureAudioListener;
    private bool ownsCaptureAudioListener;
    private bool captureAudioMonitorEnabled = true;
    private float captureAudioMonitorVolume = 0.85f;
    private float captureAudioLevelRms;
    private float captureAudioLevelPeak;
    private float captureAudioFftDrive;
    private bool captureAudioFftEnabled = true;
    private readonly CaptureAudioEqBandState[] captureAudioEqBands = new CaptureAudioEqBandState[CaptureAudioEqBandCount];
    private int captureAudioEqDragBand = -1;
    private int captureAudioEqSelectedBand;
    private float captureAudioEqInputGainDb;
    private float captureAudioEqOutputGainDb;
    private float captureAudioEqDisplayMaxGainDb = 18f;
    private float captureAudioSpectrumViewMinHz;
    private float captureAudioSpectrumViewMaxHz = CaptureAudioSpectrumDisplayMaxHz;
    private int captureAudioMonitorLatencySamples = 4096;
    private Coroutine captureAudioAnalysisCoroutine;
    private string pendingCaptureAudioDeviceName;
    private float captureAudioStartAtRealtime;
    private bool captureAudioMonitorPrimed;
    private readonly float[] captureAudioAnalysisBuffer = new float[4096];
    private readonly float[] captureAudioMonoAnalysisBuffer = new float[1024];
    private readonly float[] captureAudioFilteredMonoAnalysisBuffer = new float[1024];
    private readonly float[] captureAudioSpectrumBins = new float[CaptureAudioFftBinCount];
    private readonly float[] captureAudioFilteredSpectrumBins = new float[CaptureAudioFftBinCount];
    private readonly float[] captureAudioSpectrumBands = new float[CaptureAudioSpectrumBandCount];
    private readonly float[] captureAudioSpectrumBandDb = new float[CaptureAudioSpectrumBandCount];
    private readonly float[] captureAudioFilteredSpectrumBandDb = new float[CaptureAudioSpectrumBandCount];
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
    private int cdjWaveDisplayIndex = 0;
    private int layerSettingsDisplayIndex = 0;
    private int effectSettingsDisplayIndex = 0;
    private KeyCode mainLayerShortcutKey = KeyCode.R;
    private KeyCode cdjWaveShortcutKey = KeyCode.Space;
    private KeyCode layerSettingsShortcutKey = KeyCode.E;
    private KeyCode effectSettingsShortcutKey = KeyCode.E;
    private string mainLayerShortcutInput = "R";
    private string cdjWaveShortcutInput = "Space";
    private string layerSettingsShortcutInput = "E";
    private string effectSettingsShortcutInput = "E";
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
        Settings,
        EffectSettings
    }

    private enum RoutedScreenKind
    {
        MainLayers,
        CdjWave,
        LayerSettings,
        EffectSettings
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
        Mirror,
        Kaleido,
        Pixelate,
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

    private enum LayerScaleInputMode
    {
        Manual,
        Fft,
        Envelope
    }

    private enum LayerAutomationInterpolationMode
    {
        Linear,
        Exponential,
        Logarithmic,
        Step
    }

    private enum SettingsScreenTab
    {
        Output,
        Input,
        Midi,
        ProDjLink,
        Fft
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
        Alpha = 0,
        Add50 = 1,
        Add = 2,
        Mask = 3,
        Screen = 4,
        Multiply = 5,
        Lighten = 6,
        Darken = 7,
        Difference = 8,
        Overlay = 9,
        Subtract = 10
    }

    private enum LayerMediaAvailabilityState
    {
        Online,
        Offline,
        ReconnectAvailable,
        Reloading
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
        LayerScale,
        LayerScaleX,
        LayerScaleY,
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
        useBltBridgeMode = false;
        LoadBltBridgePreference();
        pendingBltBridgeMode = useBltBridgeMode;
        pendingDjLinkInputMode = CurrentDjLinkInputMode();
        useBltBridgeMode = false;
        useSimulatedDjLinkMode = false;
        pendingBltParamsUrl = bltParamsUrl;
        midiInProc = HandleMidiInMessage;
        midiConfigPath = Path.Combine(Application.persistentDataPath, "beatlink-midi-bindings.json");
        mediaLibraryCachePath = Path.Combine(Application.persistentDataPath, "beatlink-media-library-cache.json");
        mediaThumbnailCacheRoot = Path.Combine(Application.persistentDataPath, "video-thumbnails");
        mediaResilienceCacheRoot = Path.Combine(Application.persistentDataPath, "media-resilience-cache");
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
        secondDisplayUiMode = SecondDisplayUiMode.None;
        LoadDisplayRoutingPreferences();
        LoadScreenShortcutPreferences();
        PlayerPrefs.Save();
        Directory.CreateDirectory(externalWindowDataRoot);
        Directory.CreateDirectory(mediaResilienceCacheRoot);
        InitializeOutputResolutionOptions();
        LoadOutputResolutionPreference();
        LoadCaptureAudioFftPreferences();
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
        EnsureCaptureAudioAnalysisSequence();
    }

    private void Update()
    {
        ProcessMidiInputMessages();
        HandleRoutedScreenShortcuts();
        UpdateSimulatedDjLinkPlayers();
        if (Input.GetKeyDown(KeyCode.F) && activeScreen == 0)
        {
            lowerPanelShowsEffects = !lowerPanelShowsEffects;
        }
        if ((ShortcutPressed(mainLayerShortcutKey) || Input.GetKeyDown(KeyCode.Escape)) && activeScreen == 3)
        {
            activeScreen = 0;
        }
        if ((Input.GetKeyDown(KeyCode.Escape) || ShortcutPressed(mainLayerShortcutKey)) && secondDisplayUiEnabled && secondDisplayUiMode != SecondDisplayUiMode.None)
        {
            secondDisplayUiMode = SecondDisplayUiMode.None;
        }
        HandleSecondDisplayPreviewInput();

        RefreshPlayerTextures();
        UpdateLayerMediaAvailability();
        EnsureMediaBrowserBackgroundWork();
        UpdateBrowserPreviewSlotsIfNeeded();
        ApplyPendingMediaFolderScan();
        ApplyPendingRootVideoScan();
        UpdateMediaRootWatchRequests();
        EnsureBltPollingState();
        UpdateCaptureAudioPlaybackSequence();
        MaintainCriticalVideoPlayback();
        UpdateVideoBpmSyncLayers();
        UpdateGeneratorLayers();
        RefreshContinuousLayerPreviews();
        ValidateProgramOutputDisplayAvailability();
        UpdateProgramOutputLayers();
        PollExternalWindowCommands();
        UpdateExternalWindows();
        UpdateMonitorDisplays();
        CheckYoutubeSelectionFile();
        MaybeQueueCueListRefreshes();
        UpdateStaticBrowserThumbnails();
#if UNITY_EDITOR
        RequestEditorViewRepaint();
#endif
    }

#if UNITY_EDITOR
    private void RequestEditorViewRepaint()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextEditorViewRepaintTime)
        {
            return;
        }

        nextEditorViewRepaintTime = Time.unscaledTime + (1f / 60f);
        EditorApplication.QueuePlayerLoopUpdate();
        InternalEditorUtility.RepaintAllViews();
        SceneView.RepaintAll();
        RepaintGameViews();
    }

    private static void RepaintGameViews()
    {
        var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
        {
            return;
        }

        var gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameViews == null)
        {
            return;
        }

        for (var i = 0; i < gameViews.Length; i++)
        {
            var window = gameViews[i] as EditorWindow;
            if (window != null)
            {
                window.Repaint();
            }
        }
    }
#endif

    private void HandleRoutedScreenShortcuts()
    {
        if (ShortcutPressed(mainLayerShortcutKey))
        {
            ShowRoutedScreen(RoutedScreenKind.MainLayers);
            return;
        }

        if (ShortcutPressed(cdjWaveShortcutKey))
        {
            if (activeScreen != 2 && activeScreen != 3)
            {
                ShowRoutedScreen(RoutedScreenKind.CdjWave);
            }
            return;
        }

        var canOpenSelectedSettings = activeScreen == 0 && vjLayers != null && selectedLayerIndex >= 0 && selectedLayerIndex < vjLayers.Length;
        if (!canOpenSelectedSettings)
        {
            return;
        }

        if (ShortcutPressed(effectSettingsShortcutKey) && IsEffectSettingsSelection())
        {
            ShowRoutedScreen(RoutedScreenKind.EffectSettings);
            return;
        }

        if (ShortcutPressed(layerSettingsShortcutKey))
        {
            ShowRoutedScreen(RoutedScreenKind.LayerSettings);
        }
    }

    private static bool ShortcutPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private bool IsEffectSettingsSelection()
    {
        if (vjLayers == null || selectedLayerIndex < 0 || selectedLayerIndex >= vjLayers.Length)
        {
            return false;
        }

        var layer = vjLayers[selectedLayerIndex];
        return IsEffectLayerSlot(selectedLayerIndex) || (layer != null && layer.SourceKind == LayerSourceKind.Effect);
    }

    private void ShowRoutedScreen(RoutedScreenKind kind)
    {
        if (kind == RoutedScreenKind.MainLayers)
        {
            activeScreen = 0;
            if (secondDisplayUiMode != SecondDisplayUiMode.None)
            {
                secondDisplayUiMode = SecondDisplayUiMode.None;
                ApplyMonitorDisplayTargets();
            }
            return;
        }

        if (kind == RoutedScreenKind.CdjWave)
        {
            ShowCdjWaveScreen();
            return;
        }

        if (kind == RoutedScreenKind.LayerSettings || kind == RoutedScreenKind.EffectSettings)
        {
            ShowLayerOrEffectSettingsScreen(kind);
        }
    }

    private void ShowCdjWaveScreen()
    {
        if (cdjWaveDisplayIndex > 0)
        {
            secondDisplayUiEnabled = true;
            secondDisplayUiMode = SecondDisplayUiMode.Preview;
            EnsureMonitorDisplays();
            UpdateMonitorDisplays(force: true);
            return;
        }

        if (previewExternalWindowEnabled)
        {
            EnsureExternalWindow(ExternalWindowKind.Preview);
            return;
        }

        secondDisplayUiMode = SecondDisplayUiMode.None;
        activeScreen = activeScreen == 1 ? 0 : 1;
    }

    private void ShowLayerOrEffectSettingsScreen(RoutedScreenKind kind)
    {
        if (vjLayers == null || selectedLayerIndex < 0 || selectedLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var displayIndex = kind == RoutedScreenKind.EffectSettings ? effectSettingsDisplayIndex : layerSettingsDisplayIndex;
        settingsMonitorSelectedLayerIndex = selectedLayerIndex;
        if (displayIndex > 0)
        {
            secondDisplayUiEnabled = true;
            secondDisplayUiMode = kind == RoutedScreenKind.EffectSettings ? SecondDisplayUiMode.EffectSettings : SecondDisplayUiMode.Settings;
            EnsureMonitorDisplays();
            UpdateMonitorDisplays(force: true);
            return;
        }

        if (settingsExternalWindowEnabled)
        {
            EnsureExternalWindow(ExternalWindowKind.Settings);
            return;
        }

        secondDisplayUiMode = SecondDisplayUiMode.None;
        activeScreen = 2;
        layerSettingsScroll = Vector2.zero;
    }

    private void OnEnable()
    {
        if (useSimulatedDjLinkMode)
        {
            simulatedDjLinkTrackStartTime = Time.realtimeSinceStartup;
            TriggerSimulatedDjLinkMetadataSend();
        }
        else if (useBltBridgeMode)
        {
            AddLog("BLT bridge mode active; using external Beat Link Trigger overlay server.");
        }
        else
        {
            StartListeners();
        }
        ApplyMidiInputSelection();
        EnsureCaptureAudioAnalysisSequence();
    }

    private void OnDisable()
    {
        StopListeners();
        StopYoutubeRedirectServer();
        StopCapturePreview();
        StopCaptureAudioInput();
        StopCaptureAudioAnalysisSequence();
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
        StopCaptureAudioAnalysisSequence();
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
            case LayerEffectKind.Mirror:
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mode", GUILayout.Width(56f));
                DrawEffectModeButton(effect, LayerEffectMode.Horizontal, "H");
                DrawEffectModeButton(effect, LayerEffectMode.Vertical, "V");
                DrawEffectModeButton(effect, LayerEffectMode.Alternate, "Quad");
                GUILayout.EndHorizontal();
                break;
            case LayerEffectKind.Kaleido:
                GUILayout.Label("Beat-synced radial kaleidoscope.", smallStyle);
                DrawEffectIntensityControls(effect, 0f, 1f);
                break;
            case LayerEffectKind.Pixelate:
                GUILayout.Label("Block size follows amount.", smallStyle);
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
            case LayerEffectKind.Mirror:
                return "Mirror";
            case LayerEffectKind.Kaleido:
                return "Kaleido";
            case LayerEffectKind.Pixelate:
                return "Pixelate";
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
        var layerStatus = LayerDisplayStatus(layer);
        if (!string.IsNullOrEmpty(layerStatus))
        {
            GUILayout.Label(layerStatus, smallStyle);
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

    private Texture2D EnsureYoutubeZoomWaveformTexture(LayerState layer, int width, int height)
    {
        if (layer == null || layer.YouTube == null)
        {
            return null;
        }

        var youtube = layer.YouTube;
        if (youtube.WaveformProfile == null && youtube.WaveformTexture != null)
        {
            youtube.WaveformProfile = ExtractWaveformProfileFromTexture(youtube.WaveformTexture);
        }

        var profile = youtube.WaveformProfile;
        if (profile == null || profile.Length == 0)
        {
            return null;
        }

        var player = layer.Player;
        var current = player == null ? 0.0 : player.time;
        var length = player != null && player.length > 0.001 ? player.length : youtube.KnownLengthSeconds;
        if (length <= 0.001)
        {
            return null;
        }

        width = Mathf.Clamp(width, 64, 2048);
        height = Mathf.Clamp(height, 32, 512);
        var centerIndex = Mathf.Clamp(Mathf.RoundToInt((float)(current / length) * (profile.Length - 1)), 0, profile.Length - 1);
        var halfWindowSamples = Mathf.Max(24, Mathf.RoundToInt((profile.Length / (float)length) * (YoutubeZoomWaveformWindowSeconds * 0.5f)));
        var textureKey = width.ToString(CultureInfo.InvariantCulture) + "x" +
                         height.ToString(CultureInfo.InvariantCulture) + ":" +
                         centerIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                         halfWindowSamples.ToString(CultureInfo.InvariantCulture) + ":" +
                         profile.Length.ToString(CultureInfo.InvariantCulture);

        if (youtube.ZoomWaveformTexture != null && string.Equals(youtube.ZoomWaveformTextureKey, textureKey, StringComparison.Ordinal))
        {
            return youtube.ZoomWaveformTexture;
        }

        if (youtube.ZoomWaveformTexture != null)
        {
            Destroy(youtube.ZoomWaveformTexture);
            youtube.ZoomWaveformTexture = null;
        }

        youtube.ZoomWaveformTexture = BuildYoutubeZoomWaveformTexture(profile, centerIndex, halfWindowSamples, width, height);
        youtube.ZoomWaveformTextureKey = textureKey;
        return youtube.ZoomWaveformTexture;
    }

    private static Texture2D BuildYoutubeZoomWaveformTexture(float[] profile, int centerIndex, int halfWindowSamples, int width, int height)
    {
        if (profile == null || profile.Length == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        var background = new Color32(1, 2, 3, 255);
        var grid = new Color32(18, 22, 28, 255);
        var wave = new Color32(67, 206, 255, 255);
        var center = new Color32(255, 214, 64, 255);
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        var midY = (height - 1) * 0.5f;
        for (var x = 0; x < width; x++)
        {
            if (x == width / 2 || (width >= 8 && x % Mathf.Max(1, width / 8) == 0))
            {
                for (var y = 0; y < height; y++)
                {
                    pixels[y * width + x] = x == width / 2 ? center : grid;
                }
            }
        }

        var centerRow = Mathf.Clamp(Mathf.RoundToInt(midY), 0, height - 1);
        for (var x = 0; x < width; x++)
        {
            pixels[centerRow * width + x] = grid;
        }

        var left = centerIndex - halfWindowSamples;
        var right = centerIndex + halfWindowSamples;
        if (right <= left)
        {
            right = left + 1;
        }

        for (var x = 0; x < width; x++)
        {
            var t = width <= 1 ? 0f : x / (float)(width - 1);
            var sampleIndex = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(left, right, t)), 0, profile.Length - 1);
            var amplitude = Mathf.Clamp01(profile[sampleIndex]);
            var halfHeight = Mathf.Clamp(Mathf.RoundToInt(amplitude * (height * 0.46f)), 1, Mathf.Max(1, height / 2 - 1));
            var yMin = Mathf.Clamp(centerRow - halfHeight, 0, height - 1);
            var yMax = Mathf.Clamp(centerRow + halfHeight, 0, height - 1);
            for (var y = yMin; y <= yMax; y++)
            {
                pixels[y * width + x] = x == width / 2 ? center : wave;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
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

    private void DrawYoutubeLinkedPlayerWaveform(LayerState layer, Rect rect)
    {
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.012f, 0.015f, 0.018f, 1f), 0f, 4f);
        if (layer == null || layer.YouTube == null)
        {
            return;
        }

        var pad = 10f;
        GUI.Label(new Rect(rect.x + pad, rect.y + 5f, rect.width - pad * 2f, 18f),
            "Zoomed waveform", smallStyle);
        var wave = new Rect(rect.x + pad, rect.y + 25f, rect.width - pad * 2f, rect.height - 36f);
        GUI.DrawTexture(wave, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.005f, 0.006f, 0.008f, 1f), 0f, 2f);

        var texture = EnsureYoutubeZoomWaveformTexture(layer, Mathf.RoundToInt(wave.width), Mathf.RoundToInt(wave.height));
        if (texture != null)
        {
            GUI.DrawTexture(wave, texture, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.Label(new Rect(wave.x + 10f, wave.y + 6f, wave.width - 20f, 20f), "Waiting for analyzed waveform", smallStyle);
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
        GUI.Label(new Rect(content.x, content.y, content.width, 20f), "Layers 1-10", smallStyle);
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
        var columns = 5;
        var rows = 2;
        var cellHeight = (rect.height - gap * (rows - 1)) / rows;
        var previewHeight = Mathf.Max(40f, cellHeight - 52f);
        var targetCellWidth = previewHeight * 16f / 9f + 34f;
        var maxCellWidth = (rect.width - gap * (columns - 1)) / columns;
        var cellWidth = Mathf.Min(maxCellWidth, targetCellWidth);
        var totalWidth = cellWidth * columns + gap * (columns - 1);
        var xOffset = Mathf.Max(0f, (rect.width - totalWidth) * 0.5f);

        for (var i = 0; i < SourceLayerCount; i++)
        {
            var index = SourceStartIndex + i;
            var column = i % columns;
            var row = i / columns;
            var cell = new Rect(rect.x + xOffset + column * (cellWidth + gap), rect.y + row * (cellHeight + gap), cellWidth, cellHeight);
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

        var overlay = new Rect(rect.center.x - 18f, rect.center.y - 18f, 36f, 36f);
        GUI.DrawTexture(overlay, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.48f), 0f, 4f);
        GUI.Label(overlay, BusySpinnerText(), markerStyle);
    }

    private bool IsLayerBusyLoading(LayerState layer)
    {
        var status = LayerDisplayStatus(layer);
        if (layer == null || string.IsNullOrEmpty(status))
        {
            return false;
        }

        return status.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
               status.IndexOf("Waiting", StringComparison.OrdinalIgnoreCase) >= 0 ||
               status.IndexOf("Converting", StringComparison.OrdinalIgnoreCase) >= 0 ||
               status.IndexOf("Resolving", StringComparison.OrdinalIgnoreCase) >= 0 ||
               status.IndexOf("Downloading", StringComparison.OrdinalIgnoreCase) >= 0 ||
               status.IndexOf("Reload", StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (!showPreview)
            {
                var text = layer == null || layer.SourceKind == LayerSourceKind.None ? "empty" : LayerName(layer);
                GUI.Label(new Rect(inner.x + 8f, inner.y + 8f, inner.width - 16f, 24f), text, smallStyle);
            }
        }
        DrawLayerBusyOverlay(inner, layer);
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
        var blendRect = new Rect(cell.xMax - 128f, progressRect.yMax + 4f, 76f, 22f);
        var selectRect = new Rect(inner.x, inner.y, inner.width, Mathf.Max(24f, progressRect.yMax - inner.y + 2f));
        var badgeRect = new Rect(cell.x + 8f, progressRect.yMax + 4f, 22f, 22f);
        DrawLayerIndexBadge(badgeRect, index, selected);
        GUI.Label(new Rect(badgeRect.xMax + 8f, progressRect.yMax + 3f, Mathf.Max(40f, blendRect.x - badgeRect.xMax - 14f), 24f), LayerName(layer), smallStyle);
        if (!midiEditMode && GUI.Button(blendRect, (layer == null ? "Alpha" : LayerBlendLabel(layer.BlendMode)) + " v"))
        {
            if (layerBlendPopupLayerIndex == index)
            {
                layerBlendPopupLayerIndex = -1;
                layerBlendPopupAnchorValid = false;
            }
            else
            {
                layerBlendPopupLayerIndex = index;
                layerBlendPopupButtonRect = blendRect;
                layerBlendPopupAnchorValid = true;
            }
        }
        else if (midiEditMode)
        {
            DrawStaticButton(blendRect, layer == null ? "Alpha" : LayerBlendLabel(layer.BlendMode));
        }
        if (layerBlendPopupLayerIndex == index)
        {
            layerBlendPopupButtonRect = blendRect;
            layerBlendPopupAnchorValid = true;
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
                 !(layerBlendPopupLayerIndex == index && layerBlendPopupRect.Contains(mouseEvent.mousePosition)))
        {
            if (mouseEvent.button == 0 && IsStepSequencerShiftAssignMode())
            {
                ToggleStepSequencerLayerQueueEntry(selectedLayerIndex, index);
                mouseEvent.Use();
                return;
            }
            selectedLayerIndex = index;
            if (secondDisplayUiEnabled && IsSettingsMonitorDisplayMode())
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

    private void DrawLayerPreviewParameterOverlay(Rect previewRect, LayerState layer)
    {
        if (layer == null || previewRect.width < 120f || previewRect.height < 72f)
        {
            return;
        }

        var overlayHeight = 34f;
        var overlayRect = new Rect(previewRect.x + 2f, previewRect.yMax - overlayHeight - 2f, previewRect.width - 4f, overlayHeight);
        GUI.DrawTexture(overlayRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.58f), 0f, 2f);

        var style = new GUIStyle(smallStyle);
        style.fontSize = Mathf.Max(9, smallStyle.fontSize - 1);
        style.normal.textColor = new Color(0.9f, 0.94f, 0.98f, 0.96f);

        var hueDegrees = Mathf.Repeat(layer.HueShift, 1f) * 360f;
        var modeFlags = string.Empty;
        if (layer.OpacityInputMode != LayerScaleInputMode.Manual)
        {
            modeFlags += "Op:" + layer.OpacityInputMode + " ";
        }
        if (layer.ScaleInputMode != LayerScaleInputMode.Manual)
        {
            modeFlags += "S:" + layer.ScaleInputMode + " ";
        }
        if (layer.ScaleXInputMode != LayerScaleInputMode.Manual)
        {
            modeFlags += "X:" + layer.ScaleXInputMode + " ";
        }
        if (layer.ScaleYInputMode != LayerScaleInputMode.Manual)
        {
            modeFlags += "Y:" + layer.ScaleYInputMode + " ";
        }
        modeFlags = modeFlags.TrimEnd();
        var line1 = "H " + hueDegrees.ToString("0", CultureInfo.InvariantCulture) +
                    "  Inv " + layer.InvertAmount.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  B/W " + layer.MonochromeAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var line2 = "S " + layer.Scale.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  X " + layer.ScaleX.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  Y " + layer.ScaleY.ToString("0.00", CultureInfo.InvariantCulture) +
                    (string.IsNullOrEmpty(modeFlags) ? string.Empty : "  " + modeFlags);

        GUI.Label(new Rect(overlayRect.x + 6f, overlayRect.y + 2f, overlayRect.width - 12f, 14f), line1, style);
        GUI.Label(new Rect(overlayRect.x + 6f, overlayRect.y + 16f, overlayRect.width - 12f, 14f), line2, style);
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
                : layer.Effect.Kind == LayerEffectKind.Blur ||
                  layer.Effect.Kind == LayerEffectKind.Mirror ||
                  layer.Effect.Kind == LayerEffectKind.Strobe
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
            if (secondDisplayUiEnabled && IsSettingsMonitorDisplayMode())
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
                return LayerBlendMode.Add;
            case LayerBlendMode.Add50:
            case LayerBlendMode.Add:
                return LayerBlendMode.Screen;
            case LayerBlendMode.Screen:
                return LayerBlendMode.Multiply;
            case LayerBlendMode.Multiply:
                return LayerBlendMode.Lighten;
            case LayerBlendMode.Lighten:
                return LayerBlendMode.Darken;
            case LayerBlendMode.Darken:
                return LayerBlendMode.Difference;
            case LayerBlendMode.Difference:
                return LayerBlendMode.Overlay;
            case LayerBlendMode.Overlay:
                return LayerBlendMode.Subtract;
            case LayerBlendMode.Subtract:
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
            case LayerBlendMode.Add:
                return "Add";
            case LayerBlendMode.Screen:
                return "Screen";
            case LayerBlendMode.Multiply:
                return "Multiply";
            case LayerBlendMode.Lighten:
                return "Lighten";
            case LayerBlendMode.Darken:
                return "Darken";
            case LayerBlendMode.Difference:
                return "Difference";
            case LayerBlendMode.Overlay:
                return "Overlay";
            case LayerBlendMode.Subtract:
                return "Subtract";
            case LayerBlendMode.Mask:
                return "Mask";
            default:
                return "Alpha";
        }
    }

    private void DrawLayerIndexBadge(Rect rect, int index, bool selected)
    {
        var background = selected
            ? new Color(0.2f, 0.85f, 0.75f, 1f)
            : new Color(0.14f, 0.16f, 0.2f, 1f);
        GUI.DrawTexture(rect, whiteTexture, ScaleMode.StretchToFill, false, 0f, background, 0f, 5f);

        var badgeStyle = new GUIStyle(smallStyle);
        badgeStyle.alignment = TextAnchor.MiddleCenter;
        badgeStyle.fontSize = Mathf.Max(11, smallStyle.fontSize);
        badgeStyle.normal.textColor = selected ? new Color(0.02f, 0.03f, 0.04f, 1f) : new Color(0.92f, 0.95f, 0.98f, 1f);
        GUI.Label(rect, (index - SourceStartIndex + 1).ToString(CultureInfo.InvariantCulture), badgeStyle);
    }

    private static LayerBlendMode[] LayerBlendModes()
    {
        return new[]
        {
            LayerBlendMode.Alpha,
            LayerBlendMode.Add,
            LayerBlendMode.Screen,
            LayerBlendMode.Multiply,
            LayerBlendMode.Lighten,
            LayerBlendMode.Darken,
            LayerBlendMode.Difference,
            LayerBlendMode.Overlay,
            LayerBlendMode.Subtract,
            LayerBlendMode.Mask
        };
    }

    private static bool IsBlendModeSelectableForLayer(LayerState layer, LayerBlendMode mode)
    {
        return true;
    }

    private void DrawLayerBlendPopup(Rect buttonRect, int layerIndex, LayerState layer)
    {
        if (layerBlendPopupLayerIndex != layerIndex)
        {
            return;
        }

        var popupWidth = 168f;
        var modes = LayerBlendModes();
        layerBlendPopupRect = new Rect(buttonRect.xMax - popupWidth, buttonRect.yMax + 4f, popupWidth, modes.Length * 26f + 8f);
        GUI.DrawTexture(layerBlendPopupRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.055f, 0.06f, 0.07f, 1f), 0f, 5f);
        DrawRectOutline(layerBlendPopupRect, 1f, new Color(0.38f, 0.44f, 0.52f, 1f));

        var evt = Event.current;
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var enabled = IsBlendModeSelectableForLayer(layer, mode);
            var optionRect = new Rect(layerBlendPopupRect.x + 4f, layerBlendPopupRect.y + 4f + i * 26f, layerBlendPopupRect.width - 8f, 22f);
            var active = layer != null && layer.BlendMode == mode;
            var bg = active ? new Color(0.18f, 0.42f, 0.6f, 1f) : new Color(0.12f, 0.13f, 0.15f, 1f);
            if (!enabled)
            {
                bg = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f, 1f);
            }
            GUI.DrawTexture(optionRect, whiteTexture, ScaleMode.StretchToFill, false, 0f, bg, 0f, 3f);
            GUI.Label(optionRect, (active ? "> " : "  ") + LayerBlendLabel(mode) + (enabled ? "" : "  (Main only)"), smallStyle);

            if (evt != null && evt.type == EventType.MouseDown && optionRect.Contains(evt.mousePosition))
            {
                if (enabled && layer != null)
                {
                    layer.BlendMode = mode;
                }
                layerBlendPopupLayerIndex = -1;
                evt.Use();
                return;
            }
        }

        if (evt != null &&
            evt.type == EventType.MouseDown &&
            !buttonRect.Contains(evt.mousePosition) &&
            !layerBlendPopupRect.Contains(evt.mousePosition))
        {
            layerBlendPopupLayerIndex = -1;
            layerBlendPopupAnchorValid = false;
        }
    }

    private void DrawFloatingLayerBlendPopup()
    {
        if (activeScreen != 0 || layerBlendPopupLayerIndex < 0 || !layerBlendPopupAnchorValid || vjLayers == null)
        {
            return;
        }

        if (layerBlendPopupLayerIndex >= vjLayers.Length)
        {
            layerBlendPopupLayerIndex = -1;
            layerBlendPopupAnchorValid = false;
            return;
        }

        DrawLayerBlendPopup(layerBlendPopupButtonRect, layerBlendPopupLayerIndex, vjLayers[layerBlendPopupLayerIndex]);
    }

    private void HandleFloatingLayerBlendPopupInput()
    {
        if (activeScreen != 0 ||
            layerBlendPopupLayerIndex < 0 ||
            !layerBlendPopupAnchorValid ||
            vjLayers == null ||
            layerBlendPopupLayerIndex >= vjLayers.Length)
        {
            return;
        }

        var evt = Event.current;
        if (evt == null || evt.type != EventType.MouseDown)
        {
            return;
        }

        var popupWidth = 168f;
        var modes = LayerBlendModes();
        var popupRect = new Rect(
            layerBlendPopupButtonRect.xMax - popupWidth,
            layerBlendPopupButtonRect.yMax + 4f,
            popupWidth,
            modes.Length * 26f + 8f);
        layerBlendPopupRect = popupRect;

        if (!popupRect.Contains(evt.mousePosition))
        {
            return;
        }

        var layer = vjLayers[layerBlendPopupLayerIndex];
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var optionRect = new Rect(popupRect.x + 4f, popupRect.y + 4f + i * 26f, popupRect.width - 8f, 22f);
            if (!optionRect.Contains(evt.mousePosition))
            {
                continue;
            }

            if (layer != null && IsBlendModeSelectableForLayer(layer, mode))
            {
                layer.BlendMode = mode;
            }

            layerBlendPopupLayerIndex = -1;
            layerBlendPopupAnchorValid = false;
            evt.Use();
            return;
        }

        evt.Use();
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
            GUILayout.Label("Select one of Layers 1-10 to load a source.", smallStyle);
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
            OpenYoutubeSearchForLayer(selectedLayerIndex, true);
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
        GUILayout.Label("Mirror", smallStyle);
        DrawEffectAttachButton("Mirror", LayerEffectKind.Mirror);

        GUILayout.Space(6f);
        GUILayout.Label("Kaleido", smallStyle);
        DrawEffectAttachButton("Kaleido", LayerEffectKind.Kaleido);

        GUILayout.Space(6f);
        GUILayout.Label("Pixelate", smallStyle);
        DrawEffectAttachButton("Pixelate", LayerEffectKind.Pixelate);

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
        DrawEffectAttachButton("Mirror", LayerEffectKind.Mirror);
        GUILayout.Space(6f);
        DrawEffectAttachButton("Kaleido", LayerEffectKind.Kaleido);
        GUILayout.Space(6f);
        DrawEffectAttachButton("Pixelate", LayerEffectKind.Pixelate);
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

        var hostLayerIndex = CurrentSettingsMonitorLayerIndex();
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
            generator.ScreenVisible = true;
            SetGeneratorShape(generator, GeneratorShape.Cube);
            SetGeneratorVisible(generator, true);
        }
        if (GUILayout.Button("Tetra", GUILayout.Height(24f)))
        {
            generator.ScreenVisible = true;
            SetGeneratorShape(generator, GeneratorShape.Tetrahedron);
            SetGeneratorVisible(generator, true);
        }
        if (GUILayout.Button("Dodeca", GUILayout.Height(24f)))
        {
            generator.ScreenVisible = true;
            SetGeneratorShape(generator, GeneratorShape.Dodecahedron);
            SetGeneratorVisible(generator, true);
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
        var nextScreenVisible = GUILayout.Toggle(generator.ScreenVisible, "Show 3D object");
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
        var visibleRowCount = Mathf.CeilToInt(360f / GeneratorTexturePreviewRowHeight) + GeneratorTexturePreviewOverscanRows * 2;
        var faceListScrollOffset = Mathf.Max(0f, generator.FaceImageScroll.y - artPlayers.Count * GeneratorTexturePreviewRowHeight);
        var faceStartIndex = Mathf.Max(0, Mathf.FloorToInt(faceListScrollOffset / GeneratorTexturePreviewRowHeight) - GeneratorTexturePreviewOverscanRows);
        var faceEndIndex = Mathf.Min(generator.FaceImagePaths.Count, faceStartIndex + visibleRowCount);
        if (faceStartIndex > 0)
        {
            GUILayout.Space(faceStartIndex * GeneratorTexturePreviewRowHeight);
        }
        for (var i = faceStartIndex; i < faceEndIndex; i++)
        {
            var imagePath = generator.FaceImagePaths[i];
            var imageTexture = LoadImageTexture(imagePath);
            var label = Path.GetFileName(imagePath);
            DrawGeneratorTexturePreviewButton(imageTexture, label, 290f, () =>
            {
                if (imageTexture == null)
                {
                    return;
                }
                ClearGeneratorLayerTextureSource(generator);
                SetGeneratorManualImageSource(generator, imagePath);
                ApplyGeneratorTexture(generator, imageTexture);
            });
        }
        if (faceEndIndex < generator.FaceImagePaths.Count)
        {
            GUILayout.Space((generator.FaceImagePaths.Count - faceEndIndex) * GeneratorTexturePreviewRowHeight);
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
            if (layerIndex == hostLayerIndex)
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
            if (layerIndex == hostLayerIndex)
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
                : "Select one of Layers 1-10 to load video files.  Shift/Ctrl select supported.",
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
                : "Select one of Layers 1-10 to load image files.  Shift/Ctrl select supported.",
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
            stem = stem.Substring(0, Math.Max(1, keep - 1)) + "窶ｦ";
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
            var player = FindFreshMonitorPlayer(snapshot.Players, number, useBltBridgeMode);
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
            BeatLine = player == null ? "" : "Beat " + FormatBeat(player) + "   Source P" + player.TrackSourcePlayer + " " + FormatTrackSourceSlot(player.TrackSourceSlot) + "   Last " + AgeText(DisplayMonitorLastUpdateUtc(player)),
            AlbumArtTexture = player == null ? null : player.AlbumArtTexture,
            RuntimeWaveTexture = player == null ? null : (Texture)player.BltWavePreview,
            OverviewWaveTexture = player == null ? null : (Texture)(player.BltWaveOverview != null ? player.BltWaveOverview : player.DirectWaveformTexture),
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

    private PlayerState FindFreshMonitorPlayer(List<PlayerState> players, int number, bool allowBltFresh)
    {
        var player = FindPlayer(players, number);
        if (!IsFreshMonitorPlayer(player, allowBltFresh))
        {
            return null;
        }
        return player;
    }

    private static bool IsFreshMonitorPlayer(PlayerState player, bool allowBltFresh)
    {
        if (player == null)
        {
            return false;
        }

        if (allowBltFresh && player.LastBltUpdateUtc != default(DateTime))
        {
            return (DateTime.UtcNow - player.LastBltUpdateUtc).TotalSeconds <= CdjMonitorStaleSeconds;
        }

        if (player.LastSeenUtc == default(DateTime))
        {
            return false;
        }

        return (DateTime.UtcNow - player.LastSeenUtc).TotalSeconds <= CdjMonitorStaleSeconds;
    }

    private static DateTime DisplayMonitorLastUpdateUtc(PlayerState player)
    {
        if (player == null)
        {
            return default(DateTime);
        }

        return player.LastSeenUtc != default(DateTime) ? player.LastSeenUtc : player.LastBltUpdateUtc;
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
            youtubeSearchStatus = string.IsNullOrEmpty(youtubeSearchQuery)
                ? "YouTube helper not found. Enter a search query."
                : "YouTube helper not found. Falling back to built-in search list.";
            youtubeSearchOpen = true;
            if (!string.IsNullOrEmpty(youtubeSearchQuery))
            {
                StartYoutubeSearch();
            }
        }
    }

    private void OpenYoutubeSearchForLayer(int layerIndex, bool prepareSource)
    {
        if (vjLayers == null || layerIndex < 0 || layerIndex >= vjLayers.Length || !IsSourceLayerSlot(layerIndex))
        {
            return;
        }

        selectedLayerIndex = layerIndex;
        var layer = vjLayers[layerIndex];
        if (prepareSource || layer == null || layer.SourceKind != LayerSourceKind.YouTube || layer.YouTube == null)
        {
            LoadYoutubeSourceIntoLayer(layerIndex);
            layer = vjLayers[layerIndex];
        }

        var preferredDeck = layer != null && layer.YouTube != null && layer.YouTube.WaveformPlayerNumber >= 1
            ? layer.YouTube.WaveformPlayerNumber
            : 1;
        var snapshot = Snapshot();
        var player = snapshot == null ? null : FindFreshMonitorPlayer(snapshot.Players, preferredDeck, useBltBridgeMode);
        if (player == null && snapshot != null && snapshot.Players != null)
        {
            for (var deck = 1; deck <= 4; deck++)
            {
                player = FindFreshMonitorPlayer(snapshot.Players, deck, useBltBridgeMode);
                if (player != null)
                {
                    preferredDeck = deck;
                    break;
                }
            }
        }

        if (layer != null && layer.YouTube != null)
        {
            layer.YouTube.WaveformPlayerNumber = preferredDeck;
        }

        OpenYoutubeSearch(preferredDeck, player);
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
                WorkingDirectory = Path.GetDirectoryName(helper),
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
            };
            var process = System.Diagnostics.Process.Start(info);
            AddLog("YouTube WebView started: " + (process == null ? helper : ("PID " + process.Id.ToString(CultureInfo.InvariantCulture))));
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
                    ReleaseTemporaryComposite(lowerCompositeInput as RenderTexture);
                    hostLayer.EffectRenderedOutput = hostLayer.EffectScratchTexture;
                    hostLayer.EffectRenderedFrame = Time.frameCount;
                    return hostLayer.EffectScratchTexture;
                }

                var lowerCompositeBpm = CurrentVisualBpm();
                ConfigureLayerEffectMaterial(effectMaterial, hostLayer.Effect, CurrentVisualBeatFloat(lowerCompositeBpm), lowerCompositeBpm);
                Graphics.Blit(lowerCompositeInput, hostLayer.EffectScratchTexture, effectMaterial);
                ReleaseTemporaryComposite(lowerCompositeInput as RenderTexture);
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

                ApplyLayerCompositeProperties(compositeMaterial, sourceTexture, sourceLayer, 1f, sourceLayer.BlendMode);
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
        var spectrumRect = GUILayoutUtility.GetRect(1f, 88f, GUILayout.ExpandWidth(true));
        DrawCaptureAudioSpectrum(spectrumRect);
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
        if (cachedStateSnapshot != null && cachedStateSnapshotFrame == Time.frameCount)
        {
            return cachedStateSnapshot;
        }

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
            cachedStateSnapshot = result;
            cachedStateSnapshotFrame = Time.frameCount;
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

    private static Rect GuiRectToScreenRect(Rect rect)
    {
        var min = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMin));
        var max = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Rect ScreenRectToGuiRect(Rect rect)
    {
        var min = GUIUtility.ScreenToGUIPoint(new Vector2(rect.xMin, rect.yMin));
        var max = GUIUtility.ScreenToGUIPoint(new Vector2(rect.xMax, rect.yMax));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
        if (useSimulatedDjLinkMode)
        {
            return "simulator active (" +
                   Mathf.Clamp(simulatedDjLinkDeckCount, 1, 4).ToString(CultureInfo.InvariantCulture) +
                   " deck, BPM " +
                   simulatedDjLinkBpm.ToString("0.0", CultureInfo.InvariantCulture) +
                   ")";
        }
        if (string.IsNullOrEmpty(bltParamsUrl))
        {
            return "Unity direct";
        }
        if (!useBltBridgeMode)
        {
            return string.IsNullOrEmpty(lastError) ? "disabled (direct mode)" : "direct mode blocked: " + lastError;
        }
        if (!string.IsNullOrEmpty(bltError))
        {
            return bltError;
        }
        if (useBltBridgeMode && !bltServerReachable && IsOfficialBltProcessRunning())
        {
            return "official BLT running, waiting for overlay server at " + bltParamsUrl;
        }
        if (bltServerReachable && !bltReceived)
        {
            return "local BLT detected, waiting for DJ Link players at " +
                   lastBltServerSeenUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        if (!bltReceived)
        {
            return "waiting at " + bltParamsUrl;
        }
        return "connected " + bltActivePlayerCount.ToString(CultureInfo.InvariantCulture) +
               " player(s) " + lastBltUpdateUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
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
        public RenderTexture PreviewTexture;
        public Texture EffectRenderedOutput;
        public int EffectRenderedFrame = -1;
        public int PreviewRenderedFrame = -1;
        public bool EffectRenderInProgress;
        public string Path;
        public string ActiveMediaPath;
        public string CachedMediaPath;
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
        public Texture2D OfflineHoldTexture;
        public bool OwnsOfflineHoldTexture;
        public bool Enabled;
        public bool AudioOutputEnabled = true;
        public bool ProgramMuted;
        public string DetectedVideoCodec;
        public LayerMediaAvailabilityState MediaAvailabilityState;
        public float NextMediaProbeTime;
        public bool MediaPrecacheRequested;
        public float Opacity;
        public float Scale = 1f;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public LayerScaleInputMode ScaleInputMode;
        public float ScaleFftAmount = 1f;
        public float ScaleEnvelopeMin = 1f;
        public float ScaleEnvelopeMax = 1f;
        public LayerScaleInputMode OpacityInputMode;
        public float OpacityFftAmount = 0.5f;
        public float OpacityEnvelopeMin;
        public float OpacityEnvelopeMax = 1f;
        public LayerScaleInputMode ScaleXInputMode;
        public float ScaleXFftAmount = 1f;
        public float ScaleXEnvelopeMin = 1f;
        public float ScaleXEnvelopeMax = 1f;
        public LayerScaleInputMode ScaleYInputMode;
        public float ScaleYFftAmount = 1f;
        public float ScaleYEnvelopeMin = 1f;
        public float ScaleYEnvelopeMax = 1f;
        public LayerAutomationCurveState ScaleAutomation;
        public LayerAutomationCurveState OpacityAutomation;
        public LayerAutomationCurveState ScaleXAutomation;
        public LayerAutomationCurveState ScaleYAutomation;
        public float HueShift;
        public LayerScaleInputMode HueInputMode;
        public float HueFftAmount = 1f;
        public float HueEnvelopeMin;
        public float HueEnvelopeMax = 1f;
        public LayerAutomationCurveState HueAutomation;
        public float InvertAmount;
        public LayerScaleInputMode InvertInputMode;
        public float InvertFftAmount = 1f;
        public float InvertEnvelopeMin;
        public float InvertEnvelopeMax = 1f;
        public LayerAutomationCurveState InvertAutomation;
        public float MonochromeAmount;
        public LayerScaleInputMode MonochromeInputMode;
        public float MonochromeFftAmount = 1f;
        public float MonochromeEnvelopeMin;
        public float MonochromeEnvelopeMax = 1f;
        public LayerAutomationCurveState MonochromeAutomation;
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

    private sealed class LayerAutomationCurveState
    {
        public float[] Samples;
        public LayerAutomationInterpolationMode[] SegmentInterpolationModes;
        public bool[] NodeDiscontinuities;
        public float[] OutgoingSamples;
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
        public float[] WaveformProfile;
        public Texture2D ZoomWaveformTexture;
        public string ZoomWaveformTextureKey;
        public string AlignmentStatus;
        public int StreamWidth;
        public int StreamHeight;
        public YoutubePlaybackMode PlaybackMode = YoutubePlaybackMode.LocalCache;
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
        public string CliName;
        public string Options;
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
        public Texture2D BltWaveOverview;
        public byte[] DirectWaveformBytes;
        public DirectWaveformStyle DirectWaveformStyle;
        public string DirectWaveformKey;
        public Texture2D DirectWaveformTexture;
        public string DirectWaveformTextureKey;
        public int DirectWaveformTextureScale;
        public int DirectWaveformTextureFrameCount;
        public int DirectWaveformTextureWidth;
        public bool IsSimulated;
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
