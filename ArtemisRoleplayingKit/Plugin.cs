#region Usings
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVLooseTextureCompiler.Networking;
using FFXIVVoicePackCreator;
using FFXIVVoicePackCreator.VoiceSorting;
using Penumbra.Api;
using RoleplayingVoice.Attributes;
using RoleplayingMediaCore;
using RoleplayingMediaCore.Twitch;
using RoleplayingVoiceDalamud;
using SoundFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XivCommon.Functions;
using Dalamud.Plugin.Services;
using ArtemisRoleplayingKit;
using Group = FFXIVVoicePackCreator.Json.Group;
using VfxEditor.ScdFormat;
using NAudio.Wave;
using SoundType = RoleplayingMediaCore.SoundType;
using Option = FFXIVVoicePackCreator.Json.Option;
using EventHandler = System.EventHandler;
using ScdFile = VfxEditor.ScdFormat.ScdFile;
using Dalamud.Utility;
using System.Collections.Concurrent;
using VfxEditor.TmbFormat;
using Penumbra.Api.Enums;
using RoleplayingVoiceCore;
using Dalamud.Plugin.Ipc;
using Emote = Lumina.Excel.GeneratedSheets.Emote;
using Glamourer.Utility;
using System.Windows.Forms;
using RoleplayingVoiceDalamud.Glamourer;
using Vector3 = System.Numerics.Vector3;
using System.Drawing.Imaging;
using System.Drawing;
using Rectangle = System.Drawing.Rectangle;
using RoleplayingVoiceDalamud.Voice;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.DragDrop;
using RoleplayingVoiceDalamud.Services;
using NAudio.Wave.SampleProviders;
using FFXIVClientStructs.FFXIV.Client.System.Timer;
using NAudio.MediaFoundation;
using Lumina.Excel.GeneratedSheets;
using RoleplayingVoiceDalamud.Animation;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Map = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using GameObject = Dalamud.Game.ClientState.Objects.Types.IGameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using RoleplayingVoiceDalamud.IPC;
using Race = RoleplayingVoiceDalamud.Glamourer.Race;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;
using RoleplayingVoiceDalamud.NPC;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using System.Buffers.Text;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using System.Diagnostics.Eventing.Reader;
using Newtonsoft.Json.Linq;
using Glamourer.Api.Enums;
using RoleplayingVoiceDalamud.Catalogue;
using Dalamud.Game.ClientState.Objects;
using NAudio.Lame;
using System.Xml.Linq;
using System.Numerics;
using RoleplayingVoiceDalamud.GameObjects;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Lua;
#endregion
namespace RoleplayingVoice {
    public class Plugin : IDalamudPlugin {
        #region Fields
        private int performanceLimiter;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IChatGui _chat;
        private readonly IClientState _clientState;
        private IObjectTable _objectTable;
        private IDataManager _dataManager;
        private IToastGui _toast;
        private IGameConfig _gameConfig;
        private ISigScanner _sigScanner;
        private IGameInteropProvider _interopProvider;
        private IFramework _framework;

        private readonly PluginCommandManager<Plugin> commandManager;
        private readonly Configuration config;
        private readonly WindowSystem windowSystem;
        private PluginWindow _window { get; init; }
        private NetworkedClient _networkedClient;
        private VideoWindow _videoWindow;
        private CatalogueWindow _catalogueWindow;
        private RedoLineWindow _redoLineWindow;
        private GposeWindow _gposeWindow;
        private readonly GposePhotoTakerWindow _gposePhotoTakerWindow;
        private AnimationCatalogue _animationCatalogue;
        private AnimationEmoteSelection _animationEmoteSelection;
        private NPCPersonalityWindow _npcPersonalityWindow;
        private DragAndDropTextureWindow _dragAndDropTextures;
        private static IPluginLog _plugin;
        private RoleplayingMediaManager _roleplayingMediaManager;

        private Stopwatch _stopwatch;
        private Stopwatch _timeSinceLastEmoteDone = new Stopwatch();
        private Stopwatch _cooldown;
        private Stopwatch _muteTimer;
        private Stopwatch _streamSetCooldown = new Stopwatch();
        private Stopwatch _maxDownloadLengthTimer = new Stopwatch();
        private Stopwatch _redrawCooldown = new Stopwatch();
        private Stopwatch _catalogueTimer = new Stopwatch();
        private Stopwatch _catalogueOffsetTimer = new Stopwatch();
        private Stopwatch _nativeSoundExpiryTimer = new Stopwatch();
        private Stopwatch _emoteSyncCheck = new Stopwatch();
        private Stopwatch _queueTimer = new Stopwatch();

        private EmoteReaderHooks _emoteReaderHook;
        private Chat _realChat;
        private Filter _filter;
        private MediaGameObject _playerObject;
        private MediaManager _mediaManager;
        private RaceVoice _raceVoice;
        private List<KeyValuePair<List<string>, int>> penumbraSoundPacks;
        private unsafe Camera* _camera;
        private MediaCameraObject _playerCamera;

        public EventHandler OnMuteTimerOver;

        Dictionary<string, MovingObject> gameObjectPositions = new Dictionary<string, MovingObject>();
        Queue<string> temporaryWhitelistQueue = new Queue<string>();
        List<string> temporaryWhitelist = new List<string>();
        private List<string> combinedSoundList;

        private int _muteLength = 4000;
        private int attackCount;
        private int castingCount;
        private int objectsRedrawn;
        private int redrawObjectCount;
        private bool staging;
        private bool isDownloadingZip;
        private bool ignoreAttack;
        private bool disposed;
        private bool voiceMuted;
        private bool streamWasPlaying;
        private bool _inGameSoundStartedAudio;
        private bool _penumbraReady = true;
        private string lastPrintedWarning;
        private string stagingPath;
        private string potentialStream;
        private string lastStreamURL;
        private string _currentStreamer;

        private Queue<Tuple<ICharacter, string, XivChatType>> _aiMessageQueue = new Queue<Tuple<ICharacter, string, XivChatType>>();
        private Queue<string> _messageQueue = new Queue<string>();
        private Queue<string> _fastMessageQueue = new Queue<string>();
        private Stopwatch _messageTimer = new Stopwatch();
        private ConcurrentDictionary<string, string> _scdReplacements = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, bool> _alreadyScannedMods = new ConcurrentDictionary<string, bool>();
        private ConcurrentDictionary<string, List<Tuple<string, string, bool>>> _papSorting = new ConcurrentDictionary<string, List<Tuple<string, string, bool>>>();
        private ConcurrentDictionary<string, List<KeyValuePair<string, bool>>> _mdlSorting = new ConcurrentDictionary<string, List<KeyValuePair<string, bool>>>();
        private ConcurrentDictionary<string, KeyValuePair<CustomNpcCharacter, NPCConversationManager>> _npcConversationManagers = new ConcurrentDictionary<string, KeyValuePair<CustomNpcCharacter, NPCConversationManager>>();
        private ConcurrentDictionary<string, Guid> _lastCharacterDesign = new ConcurrentDictionary<string, Guid>();
        private ConcurrentDictionary<string, KeyValuePair<string, List<string>>> _animationMods = new ConcurrentDictionary<string, KeyValuePair<string, List<string>>>();
        private ConcurrentDictionary<string, Task> _emoteWatchList = new ConcurrentDictionary<string, Task>();
        private ConcurrentDictionary<string, List<string>> _modelMods = new ConcurrentDictionary<string, List<string>>();
        private ConcurrentDictionary<string, List<string>> _modelDependancyMods = new ConcurrentDictionary<string, List<string>>();

        private Dictionary<string, RoleplayingMediaCore.IMediaGameObject> _loopEarlyQueue = new Dictionary<string, RoleplayingMediaCore.IMediaGameObject>();
        private WaveStream _nativeAudioStream;
        private MediaGameObject _lastPlayerToEmote;
        private string _voice;
        private string _voicePackPath;
        private string _voicePackStaging;
        private string _lastEmoteUsed;
        private Stopwatch _scdProcessingDelayTimer;
        private List<string> _animationModsAlreadyTriggered = new List<string>();
        private int _otherPlayerCombatTrigger;
        private SpeechToTextManager _speechToTextManager;
        private ushort _lastEmoteTriggered;
        private bool _hasBeenInitialized;
        private string[] _currentScreenshotList;
        private bool _catalogueMods;
        private List<string> _modelModList;
        private int _catalogueIndex;
        uint LockCode = 0x6D617265;
        private bool ignoreModSettingChanged;
        private int _catalogueStage;
        private string _lastModNameChecked;
        private CharacterCustomization _characterCustomizationTest;
        private (bool, bool, string) _currentClothingCollection;
        //private List<object> _currentClothingChangedItems;
        private int _currentChangedItemIndex;
        private string _currentModelMod;
        private bool _catalogueScreenShotTaken = false;
        private NPCVoiceManager _npcVoiceManager;
        private AddonTalkManager _addonTalkManager;
        private AddonTalkHandler _addonTalkHandler;
        private IpcSystem _ipcSystem;
        private IGameGui _gameGui;
        private IDragDropManager _dragDrop;
        private bool _mountingOccured;
        private bool _combatOccured;
        private string _lastMountingMessage;
        private bool _mountMusicWasPlayed;
        private int _recentCFPop;
        private int hurtCount;
        private MediaGameObject _lastStreamObject;
        private string[] _streamURLs;
        private bool _combatMusicWasPlayed;
        private bool _wasDoingFakeEmote;
        private bool _didRealEmote;
        private int _failCount;
        Stopwatch pollingTimer = new Stopwatch();
        private bool _playerDied;
        private static bool _blockDataRefreshes;
        private CharacterVoicePack _mainCharacterVoicePack;
        Dictionary<string, CharacterVoicePack> _characterVoicePacks = new Dictionary<string, CharacterVoicePack>();
        private Emote _lastEmoteAnimationUsed;
        private bool _isAlreadyRunningEmote;
        List<string> _preOccupiedWithEmoteCommand = new List<string>();
        private string _currentModPackRefreshGuid;
        private ICallGateSubscriber<ValueTuple<Guid, string>[]> _glamourerGetDesignListLegacyAlternate;
        private int _playerCount;
        private bool _catalogueStage0IsRunning;
        private bool _catalogueStage1IsRunning;
        Queue<string> _catalogueModsToEnable = new Queue<string>();
        Queue<EquipObject> _glamourerScreenshotQueue = new Queue<EquipObject>();
        private bool _equipmentFound;
        private EquipObject _currentClothingItem;
        private List<EquipObject> _currentClothingChangedItems;
        private Guid _catalogueCollectionName;
        private (bool ObjectValid, bool IndividualSet, (Guid Id, string Name) EffectiveCollection) _originalCollection;
        private ITargetManager _targetManager;
        private bool _objectRecentlyDidEmote;
        private Queue<Tuple<string[], string, ICharacter>> _checkAnimationModsQueue = new Queue<Tuple<string[], string, ICharacter>>();
        private Dictionary<string, IReadOnlyList<Emote>> _emoteList;

        public string Name => "Artemis Roleplaying Kit";

        public RoleplayingMediaManager RoleplayingMediaManager { get => _roleplayingMediaManager; set => _roleplayingMediaManager = value; }
        public NetworkedClient NetworkedClient { get => _networkedClient; set => _networkedClient = value; }
        public ISigScanner SigScanner { get => _sigScanner; set => _sigScanner = value; }
        internal Filter Filter {
            get {
                if (_filter == null) {
                    _filter = new Filter(this);
                    _filter.Enable();
                }
                return _filter;
            }
            set => _filter = value;
        }

        public IGameInteropProvider InteropProvider { get => _interopProvider; set => _interopProvider = value; }

        public Configuration Config => config;

        public Stopwatch TimeSinceLastEmoteDone { get => _timeSinceLastEmoteDone; set => _timeSinceLastEmoteDone = value; }
        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }
        public NPCVoiceManager NpcVoiceManager { get => _npcVoiceManager; set => _npcVoiceManager = value; }
        public IDataManager DataManager { get => _dataManager; set => _dataManager = value; }

        public IChatGui Chat => _chat;

        public Queue<string> FastMessageQueue { get => _fastMessageQueue; set => _fastMessageQueue = value; }
        public Queue<string> MessageQueue { get => _messageQueue; set => _messageQueue = value; }
        public IDragDropManager DragDrop { get => _dragDrop; set => _dragDrop = value; }
        internal GposeWindow GposeWindow { get => _gposeWindow; set => _gposeWindow = value; }
        public AddonTalkHandler AddonTalkHandler { get => _addonTalkHandler; set => _addonTalkHandler = value; }
        public static IPluginLog PluginLog { get => _plugin; set => _plugin = value; }
        internal AnimationCatalogue AnimationCatalogue { get => _animationCatalogue; set => _animationCatalogue = value; }
        public IpcSystem IpcSystem { get => _ipcSystem; set => _ipcSystem = value; }
        internal NPCPersonalityWindow NpcPersonalityWindow { get => _npcPersonalityWindow; set => _npcPersonalityWindow = value; }
        public IObjectTable ObjectTable { get => _objectTable; set => _objectTable = value; }

        public IClientState ClientState => _clientState;

        public IDataManager DataManager1 { get => _dataManager; set => _dataManager = value; }
        public unsafe Camera* Camera { get => _camera; set => _camera = value; }
        public IGameGui GameGui { get => _gameGui; set => _gameGui = value; }
        public List<string> ModelModList { get => _modelModList; set => _modelModList = value; }
        public ConcurrentDictionary<string, List<string>> ModelMods { get => _modelMods; set => _modelMods = value; }
        public ConcurrentDictionary<string, List<string>> ModelDependancyMods { get => _modelDependancyMods; set => _modelDependancyMods = value; }
        public static bool BlockDataRefreshes { get => _blockDataRefreshes; set => _blockDataRefreshes = value; }
        public RedoLineWindow RedoLineWindow { get => _redoLineWindow; set => _redoLineWindow = value; }
        #endregion
        #region Plugin Initiialization
        public Plugin(
            IDalamudPluginInterface pi,
            ICommandManager commands,
            IChatGui chat,
            IClientState clientState,
            ISigScanner scanner,
            IObjectTable objectTable,
            IToastGui toast,
            IDataManager dataManager,
            IGameConfig gameConfig,
            IFramework framework,
            IGameInteropProvider interopProvider,
            ICondition condition,
            IGameGui gameGui,
            IDragDropManager dragDrop,
            IPluginLog pluginLog,
             ITargetManager targetManager) {
            Plugin.PluginLog = pluginLog;
            this._chat = chat;
            #region Constructor
            try {
                Service.DataManager = dataManager;
                Service.SigScanner = scanner;
                Service.GameInteropProvider = interopProvider;
                Service.ChatGui = chat;
                Service.ClientState = clientState;
                Service.ObjectTable = objectTable;
                this.pluginInterface = pi;
                this._clientState = clientState;
                // Get or create a configuration object
                this.config = (Configuration)this.pluginInterface.GetPluginConfig()
                          ?? this.pluginInterface.Create<Configuration>();
                // Initialize the UI
                this.windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);
                _window = this.pluginInterface.Create<PluginWindow>();
                _videoWindow = this.pluginInterface.Create<VideoWindow>();
                _catalogueWindow = this.pluginInterface.Create<CatalogueWindow>();
                _redoLineWindow = this.pluginInterface.Create<RedoLineWindow>();
                _gposeWindow = this.pluginInterface.Create<GposeWindow>();
                _gposePhotoTakerWindow = this.pluginInterface.Create<GposePhotoTakerWindow>();
                _animationCatalogue = this.pluginInterface.Create<AnimationCatalogue>();
                _animationEmoteSelection = this.pluginInterface.Create<AnimationEmoteSelection>();
                _npcPersonalityWindow = this.pluginInterface.Create<NPCPersonalityWindow>();
                _dragAndDropTextures = this.pluginInterface.Create<DragAndDropTextureWindow>();
                _gposePhotoTakerWindow.GposeWindow = _gposeWindow;
                _npcPersonalityWindow.Plugin = this;
                pluginInterface.UiBuilder.DisableAutomaticUiHide = true;
                pluginInterface.UiBuilder.DisableGposeUiHide = true;
                _window.ClientState = this._clientState;
                _window.PluginReference = this;
                _window.PluginInterface = this.pluginInterface;
                _window.Configuration = this.config;
                _gposeWindow.Plugin = this;
                _animationCatalogue.Plugin = this;
                _animationEmoteSelection.Plugin = this;
                _targetManager = targetManager;
                if (_window is not null) {
                    this.windowSystem.AddWindow(_window);
                }
                if (_videoWindow is not null) {
                    this.windowSystem.AddWindow(_videoWindow);
                }
                if (_catalogueWindow is not null) {
                    this.windowSystem.AddWindow(_catalogueWindow);
                }
                if (_gposeWindow is not null) {
                    this.windowSystem.AddWindow(_gposeWindow);
                }
                if (_gposePhotoTakerWindow is not null) {
                    this.windowSystem.AddWindow(_gposePhotoTakerWindow);
                }
                if (_redoLineWindow is not null) {
                    this.windowSystem.AddWindow(_redoLineWindow);
                }
                if (_animationCatalogue is not null) {
                    this.windowSystem.AddWindow(_animationCatalogue);
                }
                if (_animationEmoteSelection is not null) {
                    this.windowSystem.AddWindow(_animationEmoteSelection);
                }
                if (_npcPersonalityWindow is not null) {
                    this.windowSystem.AddWindow(_npcPersonalityWindow);
                }
                if (_dragAndDropTextures is not null) {
                    this.windowSystem.AddWindow(_dragAndDropTextures);
                    _dragAndDropTextures.Plugin = this;
                    _dragAndDropTextures.IsOpen = true;
                }
                _cooldown = new Stopwatch();
                _muteTimer = new Stopwatch();
                this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;
                this.pluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

                // Load all of our commands
                this.commandManager = new PluginCommandManager<Plugin>(this, commands);
                _dataManager = dataManager;
                _toast = toast;
                _gameConfig = gameConfig;
                _sigScanner = scanner;
                _interopProvider = interopProvider;
                _objectTable = objectTable;
                _framework = framework;
                _framework.Update += framework_Update;
                NPCVoiceMapping.Initialize();
                Task.Run(async () => {
                    _npcVoiceManager = new NPCVoiceManager(await NPCVoiceMapping.GetVoiceMappings(), await NPCVoiceMapping.GetCharacterToCacheType(),
                        config.CacheFolder, "7fe29e49-2d45-423d-8efc-d8e2c1ceaf6d");
                    _addonTalkManager = new AddonTalkManager(_framework, _clientState, condition, gameGui);
                    _addonTalkHandler = new AddonTalkHandler(_addonTalkManager, _framework, _objectTable, clientState, this, chat, scanner, _redoLineWindow, _toast);
                    _ipcSystem = new IpcSystem(pluginInterface, _addonTalkHandler, this);
                    _gameGui = gameGui;
                    _dragDrop = dragDrop;
                    _videoWindow.WindowResized += _videoWindow_WindowResized;
                    _toast.ErrorToast += _toast_ErrorToast;
                });
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
                _chat?.PrintError("[Artemis Roleplaying Kit] Fatal Error, the plugin did not initialize correctly!");
            }
            pollingTimer.Start();
            #endregion
        }

        private void _videoWindow_WindowResized(object sender, EventArgs e) {
            ChangeStreamQuality();
        }

        private void InitializeEverything() {
            try {
                try {
                    new PenumbraAndGlamourerIpcWrapper(pluginInterface);
                    Penumbra.Api.IpcSubscribers.ModSettingChanged.Subscriber(pluginInterface).Event += modSettingChanged;
                    Penumbra.Api.IpcSubscribers.GameObjectRedrawn.Subscriber(pluginInterface).Event += gameObjectRedrawn;
                    Plugin.PluginLog.Debug("Penumbra connected to Artemis Roleplaying Kit");
                    _penumbraReady = true;
                } catch (Exception e) {
                    Plugin.PluginLog.Warning(e, e.Message);
                }
                AttemptConnection();
                if (config.ApiKey != null) {
                    InitialzeManager();
                }
                _window.RequestingReconnect += Window_RequestingReconnect;
                _window.OnMoveFailed += Window_OnMoveFailed;
                config.OnConfigurationChanged += Config_OnConfigurationChanged;
                _emoteReaderHook = new EmoteReaderHooks(_interopProvider, _clientState, _objectTable);
                _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as ICharacter, emoteId);
                _realChat = new Chat(_sigScanner);
                RaceVoice.LoadRacialVoiceInfo();
                CheckDependancies();
                Filter = new Filter(this);
                Filter.Enable();
                Filter.OnSoundIntercepted += _filter_OnSoundIntercepted;
                _chat.ChatMessage += Chat_ChatMessage;
                _clientState.Login += _clientState_Login;
                _clientState.Logout += _clientState_Logout;
                _clientState.TerritoryChanged += _clientState_TerritoryChanged;
                _clientState.LeavePvP += _clientState_LeavePvP;
                _clientState.CfPop += _clientState_CfPop;
                _window.OnWindowOperationFailed += Window_OnWindowOperationFailed;
                _catalogueWindow.Plugin = this;
                if (_clientState.IsLoggedIn && !config.NpcSpeechGenerationDisabled) {
                    _chat?.Print("Artemis Roleplaying Kit is now using Crowdsourced NPC Dialogue! If you wish to opt out, visit the plugin settings.");
                    _gposeWindow.Initialize();
                }
                RefreshData();
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
                _chat?.PrintError("[Artemis Roleplaying Kit] Fatal Error, the plugin did not initialize correctly!");
            }
        }

        private void Plugin_Event() {
            _penumbraReady = true;
        }

        private void _clientState_CfPop(Lumina.Excel.GeneratedSheets.ContentFinderCondition obj) {
            _recentCFPop = 1;
        }
        #endregion Plugin Initiialization
        #region Configuration
        private Configuration GetConfig() {
            string currentConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                               + @"\XIVLauncher\pluginConfigs\RoleplayingVoiceDalamud.json";
            if (File.Exists(currentConfig)) {
                return JsonConvert.DeserializeObject<Configuration>(
                    File.OpenText(currentConfig).ReadToEnd());
            }
            return new Configuration(this.pluginInterface);
        }
        private void _roleplayingVoiceManager_VoicesUpdated(object sender, EventArgs e) {
            config.CharacterVoices = _roleplayingMediaManager.CharacterVoices;
            config.Save();
            pluginInterface.SavePluginConfig(config);
        }
        unsafe private void CheckDependancies(bool forceNewAssignments = false) {
            if (_clientState.LocalPlayer != null) {
                if (_playerObject == null || forceNewAssignments) {
                    _playerObject = new MediaGameObject(_clientState.LocalPlayer);
                }
                if (_mediaManager == null || forceNewAssignments) {
                    _camera = CameraManager.Instance()->GetActiveCamera();
                    _playerCamera = new MediaCameraObject(_camera);
                    if (_mediaManager != null) {
                        _mediaManager.OnErrorReceived -= _mediaManager_OnErrorReceived;
                    }
                    _mediaManager = new MediaManager(_playerObject, _playerCamera, Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName));
                    _mediaManager.OnErrorReceived += _mediaManager_OnErrorReceived;
                    _videoWindow.MediaManager = _mediaManager;
                }
                if (_speechToTextManager == null || forceNewAssignments) {
                    _speechToTextManager = new SpeechToTextManager(Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName));
                    _speechToTextManager.RecordingFinished += _speechToTextManager_RecordingFinished;
                }
            }
        }

        private void _speechToTextManager_RecordingFinished(object sender, EventArgs e) {
            if (!_speechToTextManager.RpMode) {
                _messageQueue.Enqueue(_speechToTextManager.FinalText);
            } else {
                _messageQueue.Enqueue(@"/em says " + "\"" + _speechToTextManager.FinalText + "\"");
            }
        }

        private void Config_OnConfigurationChanged(object sender, EventArgs e) {
            if (config != null) {
                try {
                    if (_roleplayingMediaManager == null ||
                        !string.IsNullOrEmpty(config.ApiKey)
                        && config.ApiKey.All(c => char.IsAsciiLetterOrDigit(c))) {
                        InitialzeManager();
                    }
                    if (_networkedClient != null) {
                        _networkedClient.UpdateIPAddress(config.ConnectionIP);
                    }
                } catch {
                    InitialzeManager();
                }
                RefreshData();
            }
        }
        public void InitialzeManager() {
            if (_roleplayingMediaManager == null) {
                _roleplayingMediaManager = new RoleplayingMediaManager(config.ApiKey, config.CacheFolder, _networkedClient, config.CharacterVoices, _roleplayingMediaManager_InitializationStatus);
                if (config.PlayerVoiceEngine == 1) {
                    _roleplayingMediaManager.InitializeXTTS();
                }
                _roleplayingMediaManager.BasePath = Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName);
                _roleplayingMediaManager.XTTSStatus += _roleplayingMediaManager_XTTSStatus;
                _roleplayingMediaManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
                _roleplayingMediaManager.OnVoiceFailed += _roleplayingMediaManager_OnVoiceFailed;
                _window.Manager = _roleplayingMediaManager;
            }
            _window?.RefreshVoices();
        }
        private void _roleplayingMediaManager_InitializationStatus(object sender, string e) {
            try {
                if (_clientState.LocalPlayer != null) {
                    if (_chat != null) {
                        _chat.Print(e);
                    }
                }
            } catch (Exception error) {
                if (PluginLog != null) {
                    PluginLog.Warning(error, error.Message);
                }
            }
        }
        private void _roleplayingMediaManager_XTTSStatus(object sender, string e) {
            if (PluginLog != null) {
                PluginLog.Verbose(e);
            }
        }

        private void _roleplayingMediaManager_OnVoiceFailed(object sender, VoiceFailure e) {
            Plugin.PluginLog.Error(e.Exception, e.Exception.Message);
        }

        private void modSettingChanged(ModSettingChange arg1, Guid arg2, string arg3, bool arg4) {
            RefreshData();
        }
        #endregion
        #region Sound Management
        private void framework_Update(IFramework framework) {
            try {
                if (!disposed) {
                    if (!_hasBeenInitialized && _clientState.LocalPlayer != null) {
                        InitializeEverything();
                        _hasBeenInitialized = true;
                    }
                    if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                        CheckCataloging();
                    }
                    if (pollingTimer.ElapsedMilliseconds > 60 && _clientState.LocalPlayer != null && _clientState.IsLoggedIn && _hasBeenInitialized) {
                        pollingTimer.Restart();
                        CheckIfDied();
                        if (!Conditions.IsBoundByDuty) {
                            CheckForMovingObjects();
                        }
                        switch (performanceLimiter++) {
                            case 0:
                                break;
                            case 1:
                                if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                    CheckForNewDynamicEmoteRequests();
                                }
                                break;
                            case 2:
                                CheckForDownloadCancellation();
                                break;
                            case 3:
                                break;
                            case 4:
                                if (!Conditions.IsBoundByDuty) {
                                    CheckForCustomMountingAudio();
                                }
                                break;
                            case 5:
                                if (!Conditions.IsBoundByDuty) {
                                    CheckForCustomCombatAudio();
                                }
                                break;
                            case 6:
                                if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                    CheckForGPose();
                                }
                                break;
                            case 7:
                                if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                    CheckForCustomEmoteTriggers();
                                }
                                break;
                            case 8:
                                if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                    CheckForCustomNPCMinion();
                                }
                                break;
                            case 9:
                                if (config != null && _mediaManager != null && _objectTable != null && _gameConfig != null && !disposed) {
                                    CheckVolumeLevels();
                                    CheckForNewRefreshes();
                                }
                                break;
                            case 10:
                                if (_checkAnimationModsQueue.Count > 0 && !_queueTimer.IsRunning) {
                                    var item = _checkAnimationModsQueue.Dequeue();
                                    CheckAnimationMods(item.Item1, item.Item2, item.Item3);
                                    _queueTimer.Restart();
                                } else if (_queueTimer.ElapsedMilliseconds > 500) {
                                    _queueTimer.Reset();
                                }
                                performanceLimiter = 0;
                                break;
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog.Error(e, e.Message);
            }
        }

        private void CheckForCustomNPCMinion() {
            foreach (var gameObject in GetNearestObjects()) {
                var item = gameObject as ICharacter;
                if (item != null) {
                    foreach (var customNPC in config.CustomNpcCharacters) {
                        if (item.Name.TextValue.Contains(customNPC.MinionToReplace)) {
                            try {
                                if (!_lastCharacterDesign.ContainsKey(item.Name.TextValue)) {
                                    _lastCharacterDesign[item.Name.TextValue] = Guid.NewGuid();
                                }
                                if (_lastCharacterDesign[item.Name.TextValue].ToString() != customNPC.NpcGlamourerAppearanceString) {
                                    Guid design = Guid.Parse(customNPC.NpcGlamourerAppearanceString);
                                    ApplyByGuid(design, item);
                                    _lastCharacterDesign[item.Name.TextValue] = design;
                                }
                            } catch {
                                customNPC.NpcGlamourerAppearanceString = "";
                            }
                            unsafe {
                                var minionNPC = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(item as ICharacter).Address;
                                var minionNPCOwner = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address;
                                if (_targetManager.Target != null) {
                                    // Have the minion look at what the player is looking at.
                                    minionNPC->SetTargetId(_targetManager.Target.GameObjectId);
                                    minionNPC->TargetId = _targetManager.Target.GameObjectId;
                                } else {
                                    // Have thw minion look at the player.
                                    minionNPC->SetTargetId(_clientState.LocalPlayer.GameObjectId);
                                    minionNPC->TargetId = _clientState.LocalPlayer.GameObjectId;
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void CheckForCustomEmoteTriggers() {
            await Task.Run(delegate {
                if (config.UsePlayerSync && !Conditions.IsBoundByDuty) {
                    if (_emoteSyncCheck.ElapsedMilliseconds > 5000) {
                        _emoteSyncCheck.Restart();
                        try {
                            foreach (Dalamud.Game.ClientState.Objects.Types.IGameObject item in _objectTable) {
                                if ((item as GameObject).ObjectKind == ObjectKind.Player && item.Name.TextValue != _clientState.LocalPlayer.Name.TextValue) {
                                    string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(item.Name.TextValue)).Split(" ");
                                    bool isShoutYell = false;
                                    if (senderStrings.Length > 2) {
                                        string playerSender = senderStrings[0] + " " + senderStrings[2];
                                        if (GetCombinedWhitelist().Contains(playerSender) && !_emoteWatchList.ContainsKey(playerSender)) {
                                            var task = Task.Run(async delegate () {
                                                try {
                                                    Vector3 lastPosition = item.Position;
                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                    while (!disposed && _clientState.IsLoggedIn &&
                                                    startingTerritoryId == _clientState.TerritoryType && !Conditions.IsBoundByDuty) {
                                                        if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                                            Plugin.PluginLog?.Verbose("Checking " + playerSender);
                                                            Plugin.PluginLog?.Verbose("Getting emote.");
                                                            ushort animation = await _roleplayingMediaManager.GetShort(playerSender + "emote");
                                                            if (animation > 0) {
                                                                Plugin.PluginLog?.Verbose("Applying Emote.");
                                                                if (animation == ushort.MaxValue) {
                                                                    animation = 0;
                                                                }
                                                                _addonTalkHandler.TriggerEmote((item as ICharacter).Address, animation);
                                                                lastPosition = item.Position;
                                                                _ = Task.Run(() => {
                                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                                    while (true) {
                                                                        Thread.Sleep(500);
                                                                        if ((Vector3.Distance(item.Position, lastPosition) > 0.001f)) {
                                                                            _addonTalkHandler.StopEmote((item as ICharacter).Address);
                                                                            break;
                                                                        }
                                                                    }
                                                                });
                                                                Task.Run(async () => {
                                                                    ushort emoteId = await _roleplayingMediaManager.GetShort(playerSender + "emoteId");

                                                                    if (emoteId > 0) {
                                                                        OnEmote(item as ICharacter, emoteId);
                                                                    }
                                                                });
                                                                Thread.Sleep(3000);
                                                            }
                                                        } else {
                                                            CleanupEmoteWatchList();
                                                            break;
                                                        }
                                                        Thread.Sleep(1000);
                                                    }
                                                } catch (Exception e) {
                                                    Plugin.PluginLog?.Warning(e, e.Message);
                                                }
                                            });
                                            _emoteWatchList[playerSender] = task;
                                            task = Task.Run(async delegate () {
                                                try {
                                                    Vector3 lastPosition = item.Position;
                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                    while (!disposed && _clientState.IsLoggedIn &&
                                                    startingTerritoryId == _clientState.TerritoryType && !Conditions.IsBoundByDuty) {
                                                        if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                                            Plugin.PluginLog?.Verbose("Checking minion from" + playerSender);
                                                            Plugin.PluginLog?.Verbose("Getting Minion Emote.");
                                                            ushort animation = await _roleplayingMediaManager.GetShort(playerSender + "MinionEmote");
                                                            if (animation > 0) {
                                                                Plugin.PluginLog?.Verbose("Applying Minion Emote.");
                                                                if (animation == ushort.MaxValue) {
                                                                    animation = 0;
                                                                }
                                                                unsafe {
                                                                    var companion = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(item as ICharacter).Address;
                                                                    if (companion->CompanionObject != null) {
                                                                        _addonTalkHandler.TriggerEmote((nint)companion->CompanionObject, animation);
                                                                        var gameObject = ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)companion->CompanionObject);
                                                                        lastPosition = gameObject->Position;
                                                                        _ = Task.Run(() => {
                                                                            int startingTerritoryId = _clientState.TerritoryType;
                                                                            while (true) {
                                                                                Thread.Sleep(500);
                                                                                if ((Vector3.Distance(gameObject->Position, lastPosition) > 0.001f)) {
                                                                                    _addonTalkHandler.StopEmote((nint)gameObject);
                                                                                    break;
                                                                                }
                                                                            }
                                                                        });
                                                                    }
                                                                }
                                                                Task.Run(async () => {
                                                                    ushort emoteId = await _roleplayingMediaManager.GetShort(playerSender + "MinionEmoteId");
                                                                    if (emoteId > 0) {
                                                                        OnEmote(item as ICharacter, emoteId);
                                                                    }
                                                                });
                                                                Thread.Sleep(3000);
                                                            }
                                                        } else {
                                                            CleanupEmoteWatchList();
                                                            break;
                                                        }
                                                        Thread.Sleep(1000);
                                                    }
                                                } catch (Exception e) {
                                                    Plugin.PluginLog?.Warning(e, e.Message);
                                                }
                                            });
                                            _emoteWatchList[playerSender] = task;
                                        }
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }

                    if (!_emoteSyncCheck.IsRunning) {
                        _emoteSyncCheck.Start();
                    }
                }
            });
        }

        private void CheckIfDied() {
            if (config.VoicePackIsActive) {
                Task.Run(delegate {
                    if (_clientState.LocalPlayer.CurrentHp <= 0 && !_playerDied) {
                        if (_mainCharacterVoicePack == null) {
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        }
                        PlayVoiceLine(_mainCharacterVoicePack.GetDeath());
                        _playerDied = true;
                    } else if (_clientState.LocalPlayer.CurrentHp > 0 && _playerDied) {
                        if (_mainCharacterVoicePack == null) {
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        }
                        PlayVoiceLine(_mainCharacterVoicePack.GetRevive());
                        _playerDied = false;
                    }
                });
            }
        }

        private void PlayVoiceLine(string value) {
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] Playing sound: " + Path.GetFileName(value));
            }
            Stopwatch audioPlaybackTimer = Stopwatch.StartNew();
            _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default, delegate {
                Task.Run(delegate {
                    if (_clientState.LocalPlayer != null) {
                        _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                    }
                });
            },
            delegate (object sender, StreamVolumeEventArgs e) {
                Task.Run(delegate {
                    if (_clientState.LocalPlayer != null) {
                        if (e.MaxSampleValues.Length > 0) {
                            if (e.MaxSampleValues[0] > 0.2) {
                                _addonTalkHandler.TriggerLipSync(_clientState.LocalPlayer as ICharacter, 2);
                            } else {
                                _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                            }
                        }
                    }
                });
            });
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] " + Path.GetFileName(value) + " took " + audioPlaybackTimer.ElapsedMilliseconds + " milliseconds to load.");
            }
            if (!_muteTimer.IsRunning) {
                if (Filter != null) {
                    Filter.Muted = true;
                }
            }
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("Battle Voice Muted");
            }
            _muteTimer.Restart();
        }
        private void CheckForGPose() {
            if (_clientState != null && _gameGui != null) {
                if (_clientState.LocalPlayer != null) {
                    if (_clientState.IsGPosing && _gameGui.GameUiHidden) {
                        if (!_gposeWindow.IsOpen) {
                            _gposeWindow.RespectCloseHotkey = false;
                            _gposeWindow.IsOpen = true;
                            _gposePhotoTakerWindow.IsOpen = true;
                        }
                    } else if (_gposeWindow.IsOpen) {
                        _gposeWindow.IsOpen = false;
                        _gposePhotoTakerWindow.IsOpen = false;
                    }
                }
            }
        }

        private void CheckForCustomCombatAudio() {
            if (Conditions.IsInCombat && !Conditions.IsMounted && !Conditions.IsBoundByDuty) {
                if (!_combatOccured) {
                    Task.Run(delegate () {
                        if (_clientState.LocalPlayer != null) {
                            _combatOccured = true;
                            string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                            string path = config.CacheFolder + @"\VoicePack\" + voice;
                            string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                            if (_mainCharacterVoicePack == null) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                            }
                            bool isVoicedEmote = false;
                            string value = _mainCharacterVoicePack.GetMisc("Battle Song");
                            if (!string.IsNullOrEmpty(value)) {
                                //if (config.UsePlayerSync) {
                                //    Task.Run(async () => {
                                //        bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                //    });
                                //}
                                _mediaManager.PlayAudio(_playerObject, value, SoundType.LoopUntilStopped, false, 0);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _combatMusicWasPlayed = true;
                            }
                        }
                    });
                }
            } else {
                if (_combatOccured) {
                    Task.Run(delegate () {
                        _combatOccured = false;
                        if (_combatMusicWasPlayed) {
                            Task.Run(async () => {
                                Thread.Sleep(6000);
                                _mediaManager.StopAudio(_playerObject);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _combatMusicWasPlayed = false;
                            });
                        }
                    });
                }
            }
        }

        private unsafe void CheckForCustomMountingAudio() {
            if (!Conditions.IsInBetweenAreas && !Conditions.IsInBetweenAreas51 && _clientState.LocalPlayer != null && _recentCFPop != 2) {
                if (Conditions.IsMounted) {
                    if (!_mountingOccured) {
                        Task.Run(delegate () {
                            if (_clientState.LocalPlayer != null) {
                                if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                                    string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                                    string path = config.CacheFolder + @"\VoicePack\" + voice;
                                    string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                                    if (_mainCharacterVoicePack == null) {
                                        _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                                    }
                                    bool isVoicedEmote = false;
                                    var characterReference = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address;
                                    var mountId = characterReference->Mount.MountId;
                                    var mount = DataManager.GetExcelSheet<Mount>(ClientLanguage.English).GetRow(mountId);
                                    string value = _mainCharacterVoicePack.GetMisc(mount.Singular.RawString);
                                    if (!string.IsNullOrEmpty(value)) {
                                        //if (config.UsePlayerSync) {
                                        //    Task.Run(async () => {
                                        //        bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                        //    });
                                        //}
                                        _mediaManager.PlayAudio(_playerObject, value, SoundType.LoopUntilStopped, false, 0);
                                        try {
                                            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
                                        } catch (Exception e) {
                                            Plugin.PluginLog?.Warning(e, e.Message);
                                        }
                                        _mountMusicWasPlayed = true;
                                    }
                                }
                                _mountingOccured = true;
                            }
                        });
                    }
                } else {
                    if (_mountingOccured) {
                        Task.Run(delegate () {
                            _mountingOccured = false;
                            if (_mountMusicWasPlayed) {
                                _lastMountingMessage = null;
                                _mediaManager.StopAudio(_playerObject);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _mountMusicWasPlayed = false;
                            }
                        });
                    }
                }
            }
        }
        private void ScanClothingMods() {
            Task.Run(() => {
                while (_catalogueMods && !disposed) {
                    if (_catalogueIndex < _modelModList.Count) {
                        ignoreModSettingChanged = true;
                        _catalogueScreenShotTaken = false;
                        _catalogueOffsetTimer.Restart();
                        while (_glamourerScreenshotQueue.Count is not 0) {
                            Thread.Sleep(500);
                        }
                        while (_catalogueIndex < _modelModList.Count) {
                            _currentModelMod = _modelModList[_catalogueIndex];
                            if (!AlreadyHasScreenShots(_currentModelMod) && !_currentModelMod.ToLower().Contains("megapack")
                            && !_currentModelMod.ToLower().Contains("mega pack") && !_currentModelMod.ToLower().Contains("hrothgar & viera")) {
                                _catalogueModsToEnable.Enqueue(_currentModelMod);
                                break;
                            } else {
                                _catalogueIndex++;
                            }
                        }
                        if (_catalogueModsToEnable.Count > 0) {
                            var catalogueMod = _catalogueModsToEnable.Dequeue();
                            if (catalogueMod != null) {
                                //PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _modelMods.Keys, _modelDependancyMods.Keys);
                                //Thread.Sleep(300);
                                _currentClothingChangedItems = new List<EquipObject>();
                                var clothingChangedItems = new List<object>();
                                var items = PenumbraAndGlamourerHelperFunctions.GetChangedItemsForMod(catalogueMod, _modelMods.Keys).Values;
                                foreach (var changedItem in items) {
                                    try {
                                        string equipItemJson = JsonConvert.SerializeObject(changedItem,
                                    new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, PreserveReferencesHandling = PreserveReferencesHandling.Objects });
                                        if (equipItemJson.Length > 200) {
                                            var equipObject = JsonConvert.DeserializeObject<EquipObject>(equipItemJson);
                                            switch (equipObject.ItemId.Id) {
                                                case 9292:
                                                case 9293:
                                                case 9294:
                                                case 9295:
                                                case 10032:
                                                case 10033:
                                                case 10034:
                                                case 10035:
                                                case 10036:
                                                case 13775:
                                                case 0:
                                                    break;
                                                default:
                                                    _currentClothingChangedItems.Add(equipObject);
                                                    break;
                                            }
                                        }
                                    } catch (Exception e) {
                                        Plugin.PluginLog.Debug(e, e.Message);
                                    }
                                }
                                if (_currentClothingChangedItems.Count > 0) {
                                    PenumbraAndGlamourerHelperFunctions.SetClothingMod(catalogueMod, _modelMods.Keys, _catalogueCollectionName);
                                    Thread.Sleep(100);
                                    PenumbraAndGlamourerHelperFunctions.SetDependancies(catalogueMod, _modelMods.Keys, _catalogueCollectionName);
                                    Thread.Sleep(100);
                                    PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(_clientState.LocalPlayer.ObjectIndex);
                                }
                            }
                        }
                        Thread.Sleep(4000);
                        _equipmentFound = false;
                        while (!disposed && _currentChangedItemIndex <
                        _currentClothingChangedItems.Count && !AlreadyHasScreenShots(_currentModelMod)) {
                            try {
                                _currentClothingItem = _currentClothingChangedItems[_currentChangedItemIndex];
                                CleanEquipment(_clientState.LocalPlayer.ObjectIndex);
                                _glamourerScreenshotQueue.Enqueue(_currentClothingItem);
                                _catalogueScreenShotTaken = false;
                                while (!_catalogueScreenShotTaken) {
                                    Thread.Sleep(100);
                                }

                            } catch (Exception e) {
                                Plugin.PluginLog.Debug(e, e.Message);
                            }
                            _currentChangedItemIndex++;
                            if (_currentChangedItemIndex >= _currentClothingChangedItems.Count) {
                                _catalogueIndex++;
                                _catalogueStage = 0;
                                _currentChangedItemIndex = 0;
                                _currentClothingItem = null;
                                break;
                            }
                        }
                        _catalogueTimer.Restart();
                        _catalogueIndex++;
                    } else {
                        _catalogueIndex = 0;
                        _catalogueMods = false;
                        ignoreModSettingChanged = false;
                        _chat?.Print("Done Catalog");
                        _catalogueTimer.Reset();
                        RefreshData();
                        //PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _modelMods.Keys, _modelDependancyMods.Keys);
                        _catalogueWindow.ScanCatalogue();
                        PenumbraAndGlamourerIpcWrapper.Instance.SetCollectionForObject.Invoke(0, _originalCollection.Item3.Id, true, true);
                    }
                }
            });
        }
        private void CheckCataloging() {
            if (_glamourerScreenshotQueue.Count > 0) {
                var item = _glamourerScreenshotQueue.Dequeue();
                if (item != null && item != null) {
                    _equipmentFound = PenumbraAndGlamourerHelperFunctions.SetEquipment(item, _clientState.LocalPlayer.ObjectIndex);
                    if (_equipmentFound) {
                        _chat.Print("Screenshotting item " + item.Name + "! " + (((float)_catalogueIndex / (float)_modelModList.Count) * 100f) + "% complete!");
                        Task.Run(() => {
                            string path = Path.Combine(config.CacheFolder, "ClothingCatalogue\\" + _currentModelMod
                                + "@" + item.Type + "@" + item.ItemId.Id + ".jpg");
                            if (!File.Exists(path)) {
                                Thread.Sleep(500);
                                try {
                                    //NativeGameWindow.BringMainWindowToFront(Process.GetCurrentProcess().ProcessName);
                                } catch { }
                                TakeScreenshot(item, path);
                            }
                        });
                    } else {
                        _catalogueScreenShotTaken = true;
                    }
                }
            }
        }

        private bool AlreadyHasScreenShots(string name) {
            //_chat?.Print(name);
            foreach (var item in _currentScreenshotList) {
                if (Path.GetFileNameWithoutExtension(item.ToLower()).Contains(name.ToLower())) {
                    return true;
                }
            }
            return false;
        }

        private void TakeScreenshot(EquipObject clothingItem, string pathName) {
            if (clothingItem != null) {
                Rectangle bounds = Screen.GetBounds(Point.Empty);
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height)) {
                    using (Graphics g = Graphics.FromImage(bitmap)) {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    Directory.CreateDirectory(_catalogueWindow.CataloguePath);
                    new Bitmap(CropImage(new Bitmap(bitmap, 1920, 1080), new Rectangle(560, 200, 800, 800)), 250, 250).Save(pathName, ImageFormat.Jpeg);
                }
            }
            _catalogueScreenShotTaken = true;
        }
        private static Image CropImage(Image img, Rectangle cropArea) {
            Bitmap bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }
        private void PrintCustomization(CharacterCustomization customization) {
            _chat?.Print("Head: " + customization.Equipment.Head.ItemId +
                        ", Body: " + customization.Equipment.Body.ItemId +
                        ", Hands: " + customization.Equipment.Hands.ItemId +
                        ", Legs: " + customization.Equipment.Legs.ItemId +
                        ", Feet: " + customization.Equipment.Feet.ItemId +
                        ", Ears: " + customization.Equipment.Ears.ItemId +
                        ", Neck: " + customization.Equipment.Neck.ItemId +
                        ", Wrists: " + customization.Equipment.Wrists.ItemId +
                        ", RFinger: " + customization.Equipment.RFinger.ItemId +
                        ", LFinger: " + customization.Equipment.LFinger.ItemId);
        }
        private void Chat_ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!disposed) {
                CheckDependancies();
                string playerName = "";
                try {
                    foreach (var item in sender.Payloads) {
                        PlayerPayload player = item as PlayerPayload;
                        TextPayload text = item as TextPayload;
                        if (player != null) {
                            string possiblePlayerName = RemoveSpecialSymbols(player.PlayerName).Trim();
                            if (!string.IsNullOrEmpty(possiblePlayerName)) {
                                if (char.IsLower(possiblePlayerName[1])) {
                                    playerName = player.PlayerName;
                                    break;
                                }
                            }
                        }
                        if (text != null) {
                            string possiblePlayerName = RemoveSpecialSymbols(text.Text).Trim();
                            if (!string.IsNullOrEmpty(possiblePlayerName)) {
                                if (char.IsLower(possiblePlayerName[1])) {
                                    playerName = text.Text;
                                    break;
                                }
                            }
                        }
                    }
#if DEBUG
                    //_chat?.Print(playerName);
#endif
                    //playerName = sender.Payloads[0].ToString().Split(" ")[3].Trim() + " " + sender.Payloads[0].ToString().Split(" ")[4].Trim(',');
                    //if (playerName.ToLower().Contains("PlayerName")) {
                    //    playerName = sender.Payloads[0].ToString().Split(" ")[3].Trim();
                    //}
                } catch {

                }
                if (_roleplayingMediaManager != null) {
                    switch (type) {
                        case XivChatType.Say:
                        case XivChatType.Shout:
                        case XivChatType.Yell:
                        case XivChatType.CustomEmote:
                        case XivChatType.Party:
                        case XivChatType.CrossParty:
                        case XivChatType.TellIncoming:
                        case XivChatType.TellOutgoing:
                        case XivChatType.FreeCompany:
                        case XivChatType.Alliance:
                        case XivChatType.PvPTeam:
                            if ((type != XivChatType.Shout && type != XivChatType.Yell) || IsResidential()) {
                                ChatText(playerName, message, type, timestamp);
                            }
                            break;
                        case XivChatType.NPCDialogue:
                        case XivChatType.NPCDialogueAnnouncements:
                            //NPCText(playerName, message, type, senderId);
                            break;
                        case (XivChatType)2729:
                        case (XivChatType)2091:
                        case (XivChatType)2234:
                        case (XivChatType)2730:
                        case (XivChatType)2219:
                        case (XivChatType)2859:
                        case (XivChatType)2731:
                        case (XivChatType)2106:
                        case (XivChatType)10409:
                        case (XivChatType)8235:
                        case (XivChatType)9001:
                        case (XivChatType)4139:
                            BattleText(playerName, message, type);
                            break;
                    }
                } else {
                    InitialzeManager();
                }
            }
        }

        private void ChatText(string sender, SeString message, XivChatType type, int timeStamp, bool isCustomNPC = false) {
            try {
                if (_clientState.LocalPlayer != null) {
                    if (sender.Contains(_clientState.LocalPlayer.Name.TextValue)) {
                        if (config.PerformEmotesBasedOnWrittenText) {
                            if (type == XivChatType.CustomEmote ||
                                message.TextValue.Split("\"").Length > 1 ||
                                message.TextValue.Contains("*")) {
                                Task.Run(() => EmoteReaction(message.TextValue));
                            }
                        }
                        //if (!Conditions.IsBoundByDuty) {
                        if (true) {
                            Task.Run(async () => {
                                try {
                                    if (_playerCount is 1 || (type == XivChatType.Party && timeStamp == -1)) {
                                        foreach (var gameObject in GetNearestObjects()) {
                                            ICharacter character = gameObject as ICharacter;
                                            if (character != null) {
                                                if (character.ObjectKind == ObjectKind.Companion) {
                                                    if (!_npcConversationManagers.ContainsKey(character.Name.TextValue)) {
                                                        CustomNpcCharacter npcData = null;
                                                        npcData = GetCustomNPCObject(character, true);
                                                        if (npcData != null) {
                                                            _npcConversationManagers[character.Name.TextValue] = new KeyValuePair<CustomNpcCharacter, NPCConversationManager>(npcData,
                                                            new NPCConversationManager(npcData.NpcName, config.CacheFolder + @"\NPCMemories", this, character));
                                                        }
                                                    }
                                                    if (_npcConversationManagers.ContainsKey(character.Name.TextValue)) {
                                                        string formattedText = message.TextValue;
                                                        if (type != XivChatType.CustomEmote && formattedText.Split('"').Length < 2) {
                                                            formattedText = @"""" + message + @"""";
                                                        }

                                                        var npcConversationManager = _npcConversationManagers[character.Name.TextValue];
                                                        string aiResponse = await npcConversationManager.Value
                                                        .SendMessage(_clientState.LocalPlayer, character, npcConversationManager.Key.NpcName, npcConversationManager.Key.NPCGreeting,
                                                        formattedText, GetWrittenGameState(
                                                            _clientState.LocalPlayer.Name.TextValue.Split(" ")[0], character.Name.TextValue.Split(" ")[0]),
                                                        npcConversationManager.Key.NpcPersonality);

                                                        _aiMessageQueue.Enqueue(new Tuple<ICharacter, string, XivChatType>(character, aiResponse, type == XivChatType.Party ? XivChatType.Party : XivChatType.CustomEmote));
                                                        //ChatText(character.Name.TextValue, aiResponse, XivChatType.Say, true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                } catch (Exception e) {
                                    Plugin.PluginLog.Warning(e, e.Message);
                                }
                            });
                        }
                        //}
                        string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(
                        _clientState.LocalPlayer.Name.TextValue)).Split(" ");
                        string playerSender = senderStrings.Length == 2 ?
                            (senderStrings[0] + " " + senderStrings[1]) :
                            (senderStrings[0] + " " + senderStrings[2]);
                        string playerMessage = message.TextValue;
                        ICharacter player = (ICharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                        if (config.TwitchStreamTriggersIfShouter && !Conditions.IsBoundByDuty) {
                            TwitchChatCheck(message, type, player, playerSender);
                        }
                        if (config.AiVoiceActive && !string.IsNullOrEmpty(config.ApiKey)) {
                            bool lipWasSynced = true;
                            Task.Run(async () => {
                                string value = await GetPlayerVoice(playerSender, playerMessage, type);
                                _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerTts, false, 0, default, delegate {
                                    if (_addonTalkHandler != null) {
                                        if (_clientState.LocalPlayer != null) {
                                            _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                                        }
                                    }
                                }, delegate (object sender, StreamVolumeEventArgs e) {
                                    if (e.MaxSampleValues.Length > 0) {
                                        if (e.MaxSampleValues[0] > 0.2) {
                                            _addonTalkHandler.TriggerLipSync(_clientState.LocalPlayer as ICharacter, 5);
                                            lipWasSynced = true;
                                        } else {
                                            _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                                        }
                                    }
                                });
                            });
                        }
                        CheckForChatSoundEffectLocal(message);
                    } else {
                        string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender)).Split(" ");
                        bool isShoutYell = false;
                        isShoutYell = type == XivChatType.Shout
                        || type == XivChatType.Yell || type == XivChatType.Alliance
                        || type == XivChatType.FreeCompany || type == XivChatType.TellIncoming
                        || type == XivChatType.PvPTeam || type == XivChatType.NoviceNetwork;
                        if (senderStrings.Length > 0) {
                            string playerSender = senderStrings[0] + (senderStrings.Length > 2 ? " " + senderStrings[2] : "");
                            string playerMessage = message.TextValue;
                            bool audioFocus = false;
                            if (_clientState.LocalPlayer.TargetObject != null) {
                                if (_clientState.LocalPlayer.TargetObject.ObjectKind ==
                                    ObjectKind.Player) {
                                    audioFocus = _clientState.LocalPlayer.TargetObject.Name.TextValue == sender
                                        || type == XivChatType.Party
                                        || type == XivChatType.CrossParty || isShoutYell;
                                }
                            } else {
                                audioFocus = true;
                            }
                            ICharacter player = (ICharacter)_objectTable.FirstOrDefault(x => RemoveSpecialSymbols(GetCustomNPCObject(x as ICharacter).NpcName) == playerSender);
                            var playerMediaReference = player != null && !isShoutYell && !audioFocus ? new MediaGameObject(player) : new MediaGameObject(playerSender, _clientState.LocalPlayer.Position);
                            bool narratePlayer = false;
                            if (config.UsePlayerSync) {
                                if (GetCombinedWhitelist().Contains(playerSender)) {
                                    Task.Run(async () => {
                                        string value = await _roleplayingMediaManager.
                                        GetSound(GetCustomNPCObject(player).NpcName, playerMessage, audioFocus ?
                                        config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                        _clientState.LocalPlayer.Position, isShoutYell, @"\Incoming\");
                                        bool lipWasSynced = false; ;
                                        _mediaManager.PlayAudio(playerMediaReference, value, SoundType.OtherPlayerTts, (isShoutYell || audioFocus), 0, default, delegate {
                                            Task.Run(delegate {
                                                _addonTalkHandler.StopLipSync(player);
                                            });
                                        },
                                        delegate (object sender, StreamVolumeEventArgs e) {
                                            Task.Run(delegate {
                                                if (e.MaxSampleValues.Length > 0) {
                                                    if (e.MaxSampleValues[0] > 0.2) {
                                                        _addonTalkHandler.TriggerLipSync(player, 2);
                                                        lipWasSynced = true;
                                                    } else {
                                                        _addonTalkHandler.StopLipSync(player);
                                                    }
                                                }
                                            });
                                        });
                                    });
                                    CheckForChatSoundEffectOtherPlayer(sender, player, message);
                                } else {
                                    narratePlayer = config.LocalVoiceForNonWhitelistedPlayers;
                                }
                            } else {
                                narratePlayer = config.LocalVoiceForNonWhitelistedPlayers;
                            }
                            if (narratePlayer) {
                                Task.Run(async () => {
                                    var list = await _roleplayingMediaManager.GetVoiceListMicrosoftNarrator();
                                    var genderSortedLists = await _roleplayingMediaManager.GetGenderSortedVoiceListsMicrosoftNarrator();
                                    Random random = new Random(AudioConversionHelper.GetSimpleHash(playerSender));
                                    //if (list.Contains("Microsoft Brian Online")) {
                                    _roleplayingMediaManager.SetVoiceMicrosoftNarrator(
                                   PenumbraAndGlamourerHelperFunctions.GetGender(player) != 1 ?
                                   genderSortedLists.Item1[random.Next(list.Contains("Microsoft Brian Online") ? 1 : 0, genderSortedLists.Item1.Length)] :
                                   genderSortedLists.Item2[random.Next(list.Contains("Microsoft Brian Online") ? 1 : 0, genderSortedLists.Item2.Length)]);

                                    var value = await _roleplayingMediaManager.DoVoiceMicrosoftNarrator(playerSender, playerMessage,
                                    type == XivChatType.CustomEmote,
                                    config.PlayerCharacterVolume,
                                    _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                                    bool lipWasSynced = false;

                                    _mediaManager.PlayAudio(playerMediaReference, value, SoundType.OtherPlayerTts, (isShoutYell || audioFocus), 0, default, delegate {
                                        Task.Run(delegate {
                                            _addonTalkHandler.StopLipSync(player);
                                        });
                                    },
                                    delegate (object sender, StreamVolumeEventArgs e) {
                                        Task.Run(delegate {
                                            if (e.MaxSampleValues.Length > 0) {
                                                if (e.MaxSampleValues[0] > 0.2) {
                                                    _addonTalkHandler.TriggerLipSync(player, 2);
                                                    lipWasSynced = true;
                                                } else {
                                                    _addonTalkHandler.StopLipSync(player);
                                                }
                                            }
                                        });
                                    });
                                });
                            }
                            TwitchChatCheck(message, type, player, playerSender);
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }

        private CustomNpcCharacter GetCustomNPCObject(ICharacter character, bool returnNullIfNoFind = false) {
            if (character != null) {
                foreach (var customNPC in config.CustomNpcCharacters) {
                    if (!string.IsNullOrEmpty(customNPC.MinionToReplace) && character.Name.TextValue.Contains(customNPC.MinionToReplace)) {
                        return customNPC;
                    }
                }
                if (!returnNullIfNoFind) {
                    return new CustomNpcCharacter() { NpcName = character.Name.TextValue };
                }
            }
            return null;
        }
        private async Task<string> GetPlayerVoice(string playerSender, string playerMessage, XivChatType type) {
            switch (config.PlayerVoiceEngine) {
                case 0:
                    return await _roleplayingMediaManager.DoVoiceElevenlabs(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                case 1:
                    return await _roleplayingMediaManager.DoVoiceXTTS(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync, _window.XttsLanguageComboBox.Contents[config.XTTSLanguage]);
                case 2:
                    _roleplayingMediaManager.SetVoiceMicrosoftNarrator(config.Characters[_clientState.LocalPlayer.Name.TextValue]);
                    return await _roleplayingMediaManager.DoVoiceMicrosoftNarrator(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                default:
                    return "";
            }

        }

        private unsafe string GetWrittenGameState(string playerName, string npcName) {
            string locationValue = HousingManager.Instance()->IsInside() ? "inside a house" : "outside";
            string value = $"{playerName} and npc are currently {locationValue}. The current zone is "
                + DataManager.GetExcelSheet<TerritoryType>().GetRow(_clientState.TerritoryType).PlaceName.Value.Name.RawString +
                ". The date and time is " + DateTime.Now.ToString("MM/dd/yyyy h:mm tt") + ".";
            return value;
        }

        public void TwitchChatCheck(SeString message, XivChatType type, ICharacter player, string name) {
            if (type == XivChatType.Yell || type == XivChatType.Shout || type == XivChatType.TellIncoming) {
                if (config.TuneIntoTwitchStreams && IsResidential()) {
                    if (!_streamSetCooldown.IsRunning || _streamSetCooldown.ElapsedMilliseconds > 10000) {
                        var strings = message.TextValue.Split(' ');
                        foreach (string value in strings) {
                            if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                _lastStreamObject = player != null ?
                                    player != null ?
                                    new MediaGameObject(player.TargetObject) :
                                    new MediaGameObject(player) :
                                    new MediaGameObject(name, _clientState.LocalPlayer.Position);
                                var audioGameObject = _lastStreamObject;
                                if (_mediaManager.IsAllowedToStartStream(audioGameObject)) {
                                    TuneIntoStream(value
                                        .Trim('(').Trim(')')
                                        .Trim('[').Trim(']')
                                        .Trim('!').Trim('@'), audioGameObject, false);
                                }
                                break;
                            }
                        }
                    }
                } else {
                    if (config.TuneIntoTwitchStreamPrompt) {
                        var strings = message.TextValue.Split(' ');
                        foreach (string value in strings) {
                            if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                potentialStream = value;
                                lastStreamURL = value;
                                string cleanedURL = RemoveSpecialSymbols(value);
                                string streamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                                _chat?.Print(streamer + " is hosting a stream in this zone! Wanna tune in? You can do \"/artemis listen\"");
                            }
                        }
                    }
                }
            }
        }
        private async void CheckForChatSoundEffectOtherPlayer(string sender, ICharacter player, SeString message) {
            if (message.TextValue.Contains("<") && message.TextValue.Contains(">")) {
                string[] tokenArray = message.TextValue.Replace(">", "<").Split('<');
                string soundTrigger = tokenArray[1];
                string path = config.CacheFolder + @"\VoicePack\Others";
                string hash = RoleplayingMediaManager.Shai1Hash(sender);
                string clipPath = path + @"\" + hash;
                string playerSender = sender;
                int index = GetNumberFromString(soundTrigger);
                CharacterVoicePack characterVoicePack = null;
                if (!_characterVoicePacks.ContainsKey(playerSender)) {
                    characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                } else {
                    characterVoicePack = _characterVoicePacks[playerSender];
                }
                string value = index == -1 ? characterVoicePack.GetMisc(soundTrigger) : characterVoicePack.GetMiscSpecific(soundTrigger, index);
                try {
                    Directory.CreateDirectory(path);
                } catch {
                    _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                }

                bool isDownloadingZip = false;
                if (!Path.Exists(clipPath) || string.IsNullOrEmpty(value)) {
                    if (Path.Exists(clipPath)) {
                        RemoveFiles(clipPath);
                    }
                    if (_characterVoicePacks.ContainsKey(playerSender)) {
                        _characterVoicePacks.Remove(playerSender);
                    }
                    isDownloadingZip = true;
                    _maxDownloadLengthTimer.Restart();
                    await Task.Run(async () => {
                        try {
                            string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                        } catch { 
                        }
                        isDownloadingZip = false;
                    });
                }
                if (string.IsNullOrEmpty(value)) {
                    if (!_characterVoicePacks.ContainsKey(playerSender)) {
                        characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                    } else {
                        characterVoicePack = _characterVoicePacks[playerSender];
                    }
                    value = characterVoicePack.GetMisc(soundTrigger);
                }
                if (!string.IsNullOrEmpty(value)) {
                    _mediaManager.PlayAudio(new MediaGameObject(player), value, SoundType.ChatSound, false);
                }
            }
        }

        private void CheckForChatSoundEffectLocal(SeString message) {
            if (message.TextValue.Contains("<") && message.TextValue.Contains(">")) {
                string[] tokenArray = message.TextValue.Replace(">", "<").Split('<');
                string soundTrigger = tokenArray[1];
                int index = GetNumberFromString(soundTrigger);
                if (_mainCharacterVoicePack == null) {
                    _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                }
                string value = index == -1 ? _mainCharacterVoicePack.GetMisc(soundTrigger) : _mainCharacterVoicePack.GetMiscSpecific(soundTrigger, index);
                if (!string.IsNullOrEmpty(value)) {
                    _mediaManager.PlayAudio(new MediaGameObject(_clientState.LocalPlayer), value, SoundType.ChatSound, false);
                }
            }
        }

        public int GetNumberFromString(string value) {
            try {
                return int.Parse(value.Split('.')[1]) - 1;
            } catch {
                return -1;
            }
        }

        private void _toast_ErrorToast(ref SeString message, ref bool isHandled) {
            if (config.VoicePackIsActive) {
                if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                    if (!_cooldown.IsRunning || _cooldown.ElapsedMilliseconds > 9000) {
                        if (_mainCharacterVoicePack == null) {
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        }
                        string value = _mainCharacterVoicePack.GetMisc(message.TextValue);
                        if (!string.IsNullOrEmpty(value)) {
                            _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default, delegate {
                                _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                            },
                            delegate (object sender, StreamVolumeEventArgs e) {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(_clientState.LocalPlayer as ICharacter, 2);
                                    } else {
                                        _addonTalkHandler.StopLipSync(_clientState.LocalPlayer as ICharacter);
                                    }
                                }
                            });
                        }
                    }
                    _cooldown.Restart();
                }
            }
        }
        private void _filter_OnSoundIntercepted(object sender, InterceptedSound e) {
            if (config.MoveSCDBasedModsToPerformanceSlider) {
                if (_scdReplacements.ContainsKey(e.SoundPath)) {
                    if (!e.SoundPath.Contains("vo_emote") && !e.SoundPath.Contains("vo_battle")) {
                        Plugin.PluginLog.Debug("Sound Mod Intercepted");
                        int i = 0;
                        try {
                            _scdProcessingDelayTimer = new Stopwatch();
                            _scdProcessingDelayTimer.Start();
                            _mediaManager.StopAudio(new MediaGameObject(_clientState.LocalPlayer));
                            if (_lastPlayerToEmote != null) {
                                _mediaManager.StopAudio(_lastPlayerToEmote);
                            }
                            Task.Run(async () => {
                                try {
                                    ScdFile scdFile = null;
                                    string soundPath = "";
                                    soundPath = e.SoundPath;
                                    scdFile = GetScdFile(e.SoundPath);
                                    QueueSCDTrigger(scdFile);
                                    CheckForValidSCD(_lastPlayerToEmote, _lastEmoteUsed, stagingPath, soundPath, true);
                                } catch (Exception ex) {
                                    Plugin.PluginLog?.Warning(ex, ex.Message);
                                }
                            });
                        } catch (Exception ex) {
                            Plugin.PluginLog?.Warning(ex, ex.Message);
                        }
                    }
                }
            }
        }
        public void OnEmote(ICharacter instigator, ushort emoteId) {
            if (!disposed) {
                _lastPlayerToEmote = new MediaGameObject(instigator);
                if (instigator.Name.TextValue == _clientState.LocalPlayer.Name.TextValue || instigator.OwnerId == _clientState.LocalPlayer.GameObjectId) {
                    if (config.VoicePackIsActive) {
                        SendingEmote(instigator, emoteId);
                    }
                    if (!_blockDataRefreshes && !_isAlreadyRunningEmote && _clientState.LocalPlayer.Name.TextValue == instigator.Name.TextValue) {
                        _lastEmoteAnimationUsed = GetEmoteData(emoteId);
                    }
                    _timeSinceLastEmoteDone.Restart();
                    _lastEmoteTriggered = emoteId;
                } else {
                    Task.Run(() => {
                        ReceivingEmote(instigator, emoteId);
                    });
                    if (_timeSinceLastEmoteDone.ElapsedMilliseconds < 400 && _timeSinceLastEmoteDone.IsRunning && _timeSinceLastEmoteDone.ElapsedMilliseconds > 20) {
                        if (instigator != null) {
                            if (Vector3.Distance(instigator.Position, _clientState.LocalPlayer.Position) < 3) {
                                _fastMessageQueue.Enqueue(GetEmoteCommand(_lastEmoteTriggered).ToLower());
                                _timeSinceLastEmoteDone.Stop();
                                _timeSinceLastEmoteDone.Reset();
                                _lastEmoteTriggered = 0;
                            }
                        }
                    }
                }
            }
        }
        private void CheckForNewRefreshes() {
            Task.Run(delegate {
                if (_redrawCooldown.ElapsedMilliseconds > 100) {
                    if (temporaryWhitelistQueue.Count < redrawObjectCount - 1) {
                        foreach (var item in temporaryWhitelistQueue) {
                            temporaryWhitelist.Add(item);
                        }
                        temporaryWhitelistQueue.Clear();
                    }
                    _redrawCooldown.Stop();
                    _redrawCooldown.Reset();
                }
            });
        }
        private void CheckForMovingObjects() {
            Task.Run(delegate {
                try {
                    foreach (GameObject gameObject in _objectTable) {
                        if (gameObject.ObjectKind == ObjectKind.Player) {
                            string cleanedName = CleanSenderName(gameObject.Name.TextValue);
                            if (!string.IsNullOrEmpty(cleanedName)) {
                                if (gameObjectPositions.ContainsKey(cleanedName)) {
                                    var positionData = gameObjectPositions[cleanedName];
                                    if (Vector3.Distance(positionData.LastPosition, gameObject.Position) > 0.01f ||
                                        positionData.LastRotation.Y != gameObject.Rotation) {
                                        if (!positionData.IsMoving) {
                                            ObjectIsMoving(cleanedName, gameObject);
                                            positionData.IsMoving = true;
                                        }
                                    } else {
                                        positionData.IsMoving = false;
                                    }
                                    positionData.LastPosition = gameObject.Position;
                                    positionData.LastRotation = new Vector3(0, gameObject.Rotation, 0);
                                } else {
                                    gameObjectPositions[cleanedName] = new MovingObject(gameObject.Position, new Vector3(0, gameObject.Rotation, 0), false);
                                }
                                if (cleanedName == CleanSenderName(_clientState.LocalPlayer.Name.TextValue)) {
                                    MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, _mainCharacterVoicePack, _roleplayingMediaManager, _mediaManager);
                                } else {
                                    CheckOtherPlayerBoneMovement(cleanedName, gameObject);
                                }
                            }

                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
            }
            );
        }
        #region Audio Volume
        private void CheckVolumeLevels() {
            uint voiceVolume = 0;
            uint masterVolume = 0;
            uint soundEffectVolume = 0;
            uint soundMicPos = 0;
            try {
                _mediaManager.AudioPlayerType = (AudioOutputType)config.AudioOutputType;
                _mediaManager.SpatialAudioAccuracy = config.SpatialAudioAccuracy;
                _mediaManager.LowPerformanceMode = config.LowPerformanceMode;
                _mediaManager.IgnoreSpatialAudioForTTS = config.IgnoreSpatialAudioForTTS;
                if (_gameConfig.TryGet(SystemConfigOption.SoundVoice, out voiceVolume)) {
                    if (_gameConfig.TryGet(SystemConfigOption.SoundMaster, out masterVolume)) {
                        if (_gameConfig.TryGet(SystemConfigOption.SoundMicpos, out soundMicPos))
                            _mediaManager.MainPlayerVolume = config.PlayerCharacterVolume *
                            ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.OtherPlayerVolume = config.OtherCharacterVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.UnfocusedPlayerVolume = config.UnfocusedCharacterVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.NpcVolume = config.NpcVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.CameraAndPlayerPositionSlider = (float)soundMicPos / 100f;
                        if (_gameConfig.TryGet(SystemConfigOption.SoundPerform, out soundEffectVolume)) {
                            _mediaManager.SFXVolume = config.LoopingSFXVolume *
                            ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                            _mediaManager.LiveStreamVolume = config.LivestreamVolume *
                            ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                        }
                    }
                    if (_muteTimer.ElapsedMilliseconds > _muteLength) {
                        if (Filter != null) {
                            lock (Filter) {
                                Filter.Muted = voiceMuted = false;
                                RefreshPlayerVoiceMuted();
                                _muteTimer.Stop();
                                _muteTimer.Reset();
                                Plugin.PluginLog.Debug("Voice Mute End");
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }
        public void MuteVoiceCheck(int length = 6000) {
            if (!_muteTimer.IsRunning) {
                if (Filter != null) {
                    Filter.Muted = voiceMuted = true;
                }
                RefreshPlayerVoiceMuted();
                Plugin.PluginLog.Debug("Mute Triggered");
            }
            _muteTimer.Restart();
            _muteLength = length;
        }
        private void RefreshPlayerVoiceMuted() {
            if (_gameConfig != null) {
                try {
                    if (voiceMuted) {
                        _gameConfig.Set(SystemConfigOption.IsSndVoice, true);
                    } else {
                        _gameConfig.Set(SystemConfigOption.IsSndVoice, false);
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e.Message);
                }
            }
        }
        #endregion
        #region Combat
        private void BattleText(string playerName, SeString message, XivChatType type) {
            CheckDependancies();
            if ((type != (XivChatType)8235 && type != (XivChatType)4139) || message.TextValue.Contains("You")) {
                if (config.VoicePackIsActive) {
                    Task.Run(delegate () {
                        string value = "";
                        string playerMessage = message.TextValue.Replace("「", " ").Replace("」", " ").Replace("の", " の").Replace("に", " に");
                        string[] values = playerMessage.Split(' ');
                        if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                            string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                            bool attackIntended = false;
                            Stopwatch performanceTimer = Stopwatch.StartNew();
                            if (!Conditions.IsBoundByDuty && !Conditions.IsInCombat) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] voice pack took " + performanceTimer.ElapsedMilliseconds + " milliseconds to load.");
                                }
                            }
                            performanceTimer.Restart();
                            if (!message.TextValue.Contains("cancel")) {
                                if (Conditions.IsBoundByDuty || !IsDicipleOfTheHand(_clientState.LocalPlayer.ClassJob.GameData.Abbreviation)) {
                                    LocalPlayerCombat(playerName, _clientState.ClientLanguage == ClientLanguage.Japanese ?
                                        playerMessage.Replace(values[0], "").Replace(values[1], "") : playerMessage, type, _mainCharacterVoicePack, ref value, ref attackIntended);
                                } else {
                                    PlayerCrafting(playerName, playerMessage, type, _mainCharacterVoicePack, ref value);
                                }
                            }
                            if (config.DebugMode) {
                                Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] voice line decision took " + performanceTimer.ElapsedMilliseconds + " milliseconds to calculate.");
                            }
                            if (!string.IsNullOrEmpty(value) || attackIntended) {
                                if (!attackIntended) {
                                    if (config.DebugMode) {
                                        Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] Playing sound: " + Path.GetFileName(value));
                                    }
                                    Stopwatch audioPlaybackTimer = Stopwatch.StartNew();
                                    _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default,
                                        Conditions.IsBoundByDuty ?
                                        null
                                    : delegate {
                                        Task.Run(delegate {
                                            if (_clientState.LocalPlayer != null) {
                                                _addonTalkHandler.StopLipSync(_clientState.LocalPlayer);
                                            }
                                        });
                                    },
                                  Conditions.IsBoundByDuty ?
                                  null
                                    : delegate (object sender, StreamVolumeEventArgs e) {
                                        Task.Run(delegate {
                                            if (_clientState.LocalPlayer != null) {
                                                if (e.MaxSampleValues.Length > 0) {
                                                    if (e.MaxSampleValues[0] > 0.2) {
                                                        _addonTalkHandler.TriggerLipSync(_clientState.LocalPlayer, 2);
                                                    } else {
                                                        _addonTalkHandler.StopLipSync(_clientState.LocalPlayer);
                                                    }
                                                }
                                            }
                                        });
                                    });
                                    if (config.DebugMode) {
                                        Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] " + Path.GetFileName(value) +
                                       " took " + audioPlaybackTimer.ElapsedMilliseconds + " milliseconds to load.");
                                    }
                                }
                                if (!_muteTimer.IsRunning) {
                                    if (Filter != null) {
                                        Filter.Muted = true;
                                    }
                                    Task.Run(() => {
                                        if (config.UsePlayerSync && !Conditions.IsBoundByDuty) {
                                            Task.Run(async () => {
                                                if (_clientState.LocalPlayer != null) {
                                                    bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                                }
                                            });
                                        }
                                    });
                                }
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug("Battle Voice Muted");
                                }
                                _muteTimer.Restart();
                            }
                        }
                    });
                }
            } else {
                if (config.UsePlayerSync) {
                    Task.Run(delegate () {
                        string[] senderStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue))).Split(' ');
                        string[] messageStrings = RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue)).Split(' ');
                        bool isShoutYell = false;
                        if (senderStrings.Length > 2) {
                            int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                            string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
                            string path = config.CacheFolder + @"\VoicePack\Others";
                            string clipPath = path + @"\" + hash;
                            try {
                                Directory.CreateDirectory(path);
                            } catch {
                                _chat?.PrintError("Failed to write to disk, please make sure the cache folder does not require administrative access!");
                            }
                            Task.Run(delegate () {
                                if (GetCombinedWhitelist().Contains(playerSender)) {
                                    if (!isDownloadingZip) {
                                        if (!Path.Exists(clipPath)) {
                                            isDownloadingZip = true;
                                            _maxDownloadLengthTimer.Restart();
                                            Task.Run(async () => {
                                                try {
                                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                                } catch { }
                                                isDownloadingZip = false;
                                            });
                                        }
                                    }
                                    if (Path.Exists(clipPath) && !isDownloadingZip) {
                                        CharacterVoicePack characterVoicePack = null;
                                        if (!_characterVoicePacks.ContainsKey(playerSender)) {
                                            characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage);
                                        } else {
                                            characterVoicePack = _characterVoicePacks[playerSender];
                                        }
                                        string value = "";
                                        if (Conditions.IsBoundByDuty || !IsDicipleOfTheHand(_clientState.LocalPlayer.ClassJob.GameData.Abbreviation)) {
                                            OtherPlayerCombat(playerName, message, type, characterVoicePack, ref value);
                                        } else {
                                            PlayerCrafting(playerName, message, type, characterVoicePack, ref value);
                                        }
                                        IPlayerCharacter player = (IPlayerCharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                                        Task.Run(async () => {
                                            GameObject character = null;
                                            if (_otherPlayerCombatTrigger > 6 || type == (XivChatType)4139) {
                                                foreach (var item in _objectTable) {
                                                    string[] playerNameStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(item.Name.TextValue))).Split(' ');
                                                    string playerSenderStrings = playerNameStrings[0 + offset] + " " + playerNameStrings[2 + offset];
                                                    if (playerSenderStrings.Contains(playerSender)) {
                                                        character = item;
                                                        break;
                                                    }
                                                }
                                                _mediaManager.PlayAudio(new MediaGameObject((IPlayerCharacter)character,
                                                playerSender, character.Position), value, SoundType.OtherPlayerCombat, false, 0, default, delegate {
                                                    Task.Run(delegate {
                                                        _addonTalkHandler.StopLipSync(player as ICharacter);
                                                    });
                                                },
                                        delegate (object sender, StreamVolumeEventArgs e) {
                                            if (e.MaxSampleValues.Length > 0) {
                                                if (e.MaxSampleValues[0] > 0.2) {
                                                    _addonTalkHandler.TriggerLipSync(player as ICharacter, 2);
                                                } else {
                                                    _addonTalkHandler.StopLipSync(player as ICharacter);
                                                }
                                            }
                                        });
                                                _otherPlayerCombatTrigger = 0;
                                            } else {
                                                _otherPlayerCombatTrigger++;
                                            }
                                        });
                                        if (!_muteTimer.IsRunning) {
                                            Filter.Muted = true;
                                        }
                                        _muteLength = 500;
                                        _muteTimer.Restart();
                                    }
                                }
                            });
                        }
                    });
                }
            }
        }

        private void LocalPlayerCombat(string playerName, SeString message,
    XivChatType type, CharacterVoicePack characterVoicePack, ref string value, ref bool attackIntended) {
            if (type == (XivChatType)2729 ||
            type == (XivChatType)2091) {
                if (!LanguageSpecificMount(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        if (attackCount == 0) {
                            if (LanguageSpecificHit(_clientState.ClientLanguage, message)) {
                                value = characterVoicePack.GetMeleeAction(message.TextValue);
                            }
                            if (LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                                value = characterVoicePack.GetCastedAction(message.TextValue);
                            }
                            if (string.IsNullOrEmpty(value)) {
                                value = characterVoicePack.GetAction(message.TextValue);
                            }
                            attackCount++;
                        } else {
                            attackCount++;
                            if (attackCount >= GetMinAttackCounts()) {
                                attackCount = 0;
                            }
                            attackIntended = true;
                        }
                    }
                }
            } else if (type == (XivChatType)2730) {
                value = characterVoicePack.GetMissed();
            } else if (type == (XivChatType)2219) {
                if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetMisc(message.TextValue);
                        if (string.IsNullOrEmpty(value)) {
                            value = characterVoicePack.GetCastingHeal();
                        }
                        castingCount++;
                    } else {
                        castingCount++;
                        if (castingCount >= GetMinAttackCounts()) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2731) {
                if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetMisc(message.TextValue);
                        if (string.IsNullOrEmpty(value)) {
                            value = characterVoicePack.GetCastingAttack();
                        }
                        castingCount++;
                    } else {
                        castingCount++;
                        if (castingCount >= 3) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)10409) {
                if (hurtCount == 0) {
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetHurt();
                    }
                    hurtCount++;
                } else {
                    hurtCount++;
                    if (hurtCount >= 3) {
                        hurtCount = 0;
                    }
                }
            }
        }

        private int GetMinAttackCounts() {
            switch (_clientState.LocalPlayer.ClassJob.GameData.Abbreviation.RawString.ToLower()) {
                case "mch":
                    return 9;
                case "mnk":
                    return 6;
                case "nin":
                    return 6;
                default:
                    return 3;
            }
        }

        private void OtherPlayerCombat(string playerName, SeString message, XivChatType type,
            CharacterVoicePack characterVoicePack, ref string value) {
            if (LanguageSpecificHit(_clientState.ClientLanguage, message) ||
                LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                value = characterVoicePack.GetAction(message.TextValue);
            }
            if (string.IsNullOrEmpty(value)) {
                if (LanguageSpecificHit(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMeleeAction(message.TextValue);
                } else if (LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetCastedAction(message.TextValue);
                } else if (LanguageSpecificDefeat(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetDeath();
                } else if (LanguageSpecificMiss(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMissed();
                } else if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetReadying(message.TextValue);
                } else if (LanguageSpecificCasting(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetCastingAttack();
                } else if (LanguageSpecificRevive(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetRevive();
                } else if (LanguageSpecificHurt(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetHurt();
                }
            }
        }
        private bool LanguageSpecificHurt(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("damage");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("dégâts");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("schaden");
            }
            return false;
        }
        private bool LanguageSpecificRevive(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("revive");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("réanimes") || message.TextValue.ToLower().Contains("réanimée");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("belebst") || message.TextValue.ToLower().Contains("wiederbelebt");
            }
            return false;
        }
        private bool LanguageSpecificCasting(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("casting");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("lancer");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("ihren");
            }
            return false;
        }
        private bool LanguageSpecificMiss(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("misses");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("manque");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("ihrem");
            }
            return false;
        }
        private bool LanguageSpecificDefeat(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("defeated");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("vaincue");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("besiegt");
            }
            return false;
        }
        private bool LanguageSpecificHit(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("use") || message.TextValue.ToLower().Contains("uses");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("utiliser") || message.TextValue.ToLower().Contains("utilise");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("verwendest") || message.TextValue.ToLower().Contains("benutz");
                case ClientLanguage.Japanese:
                    return message.TextValue.ToLower().Contains("の攻撃");
            }
            return false;
        }
        private bool LanguageSpecificCast(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("hit") || message.TextValue.ToLower().Contains("hits");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("lancé")
                        || message.TextValue.ToLower().Contains("jette")
                        || message.TextValue.ToLower().Contains("jeté");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("sprichst") || message.TextValue.ToLower().Contains("spricht");
                case ClientLanguage.Japanese:
                    return message.TextValue.Contains("を唱えた");
            }
            return false;
        }
        private bool LanguageSpecificMount(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("mount") || message.TextValue.ToLower().Contains("mounts");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("montes") || message.TextValue.ToLower().Contains("monte");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("besteigst") || message.TextValue.ToLower().Contains("besteigt");
            }
            return false;
        }
        private bool LanguageSpecificReadying(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("ready") || message.TextValue.ToLower().Contains("readies");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("prépares") || message.TextValue.ToLower().Contains("prépare");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("bereitest") || message.TextValue.ToLower().Contains("bereitet");
                case ClientLanguage.Japanese:
                    return message.TextValue.Split(' ').Length == 5;
            }
            return false;
        }
        #endregion
        #region Player Movement Trigger
        private void ObjectIsMoving(string playerName, GameObject gameObject) {
            if (_clientState.LocalPlayer != null) {
                if (playerName == CleanSenderName(_clientState.LocalPlayer.Name.TextValue)) {
                    SendingMovement(playerName, gameObject);
                } else {
                    ReceivingMovement(playerName, gameObject);
                }
            }
        }

        private async void ReceivingMovement(string playerSender, GameObject gameObject) {
            string path = config.CacheFolder + @"\VoicePack\Others";
            try {
                Directory.CreateDirectory(path);
            } catch {
                _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
            }
            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (GetCombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            if (!Path.Exists(clipPath)) {
                                isDownloadingZip = true;
                                _maxDownloadLengthTimer.Restart();
                                await Task.Run(async () => {
                                    try {
                                        string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                    } catch { }
                                    isDownloadingZip = false;
                                });
                            }
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                            bool isVoicedEmote = false;
                            string value = characterVoicePack.GetMisc("moving");
                            if (!string.IsNullOrEmpty(value)) {
                                _mediaManager.PlayAudio(new MediaGameObject(gameObject), value, SoundType.LoopWhileMoving, false, 0);
                                if (isVoicedEmote) {
                                    MuteVoiceCheck(6000);
                                }
                            } else {
                                _mediaManager.StopAudio(new MediaGameObject(gameObject));
                            }
                            MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, characterVoicePack, _roleplayingMediaManager, _mediaManager);
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }
        private async void CheckOtherPlayerBoneMovement(string playerSender, GameObject gameObject) {
            string path = config.CacheFolder + @"\VoicePack\Others";
            try {
                Directory.CreateDirectory(path);
            } catch {
                _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
            }
            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (GetCombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            if (!Path.Exists(clipPath)) {
                                isDownloadingZip = true;
                                _maxDownloadLengthTimer.Restart();
                                await Task.Run(async () => {
                                    try {
                                        string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                    } catch {

                                    }
                                    isDownloadingZip = false;
                                });
                            }
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                            MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, characterVoicePack, _roleplayingMediaManager, _mediaManager);
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }

        private void SendingMovement(string playerName, IGameObject gameObject) {
            if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                string path = config.CacheFolder + @"\VoicePack\" + voice;
                string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                if (_mainCharacterVoicePack == null) {
                    _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                }
                string value = _mainCharacterVoicePack.GetMisc("moving");
                if (!string.IsNullOrEmpty(value)) {
                    if (config.UsePlayerSync) {
                        Task.Run(async () => {
                            bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                        });
                    }
                    _mediaManager.PlayAudio(_playerObject, value, SoundType.LoopWhileMoving, false, 0);
                }
            }
        }
        #endregion
        #region Crafting Checks
        private void PlayerCrafting(string playerName, SeString message,
            XivChatType type, CharacterVoicePack characterVoicePack, ref string value) {
            value = characterVoicePack.GetMisc(message.TextValue);
        }
        public bool IsDicipleOfTheHand(string value) {
            List<string> jobs = new List<string>() { "ALC", "ARM", "BSM", "CUL", "CRP", "GSM", "LTW", "WVR", "BTN", "MIN", "FSH" };
            return jobs.Contains(value.ToUpper());
        }
        #endregion
        #region SCD Management
        public async void CheckForValidSCD(MediaGameObject mediaObject, string emote = "",
    string stagingPath = "", string soundPath = "", bool isSending = false) {
            if (_nativeAudioStream != null) {
                if (isSending) {
                    Plugin.PluginLog.Debug("Emote Trigger Detected");
                    if (!string.IsNullOrEmpty(soundPath)) {
                        try {
                            Stream diskCopy = new MemoryStream();
                            if (!_mediaManager.LowPerformanceMode) {
                                try {
                                    _nativeAudioStream.Position = 0;
                                    _nativeAudioStream.CopyTo(diskCopy);
                                } catch (Exception e) {
                                    _nativeAudioStream.Position = 0;
                                    diskCopy = new FileStream(Path.Combine(config.CacheFolder, @"\temp\tempSound.temp"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                                }
                            }
                            _nativeAudioStream.Position = 0;
                            _nativeAudioStream.CurrentTime = _scdProcessingDelayTimer.Elapsed;
                            _scdProcessingDelayTimer.Stop();
                            bool lipWasSynced = false;
                            ICharacter character = mediaObject.GameObject as ICharacter;
                            _mediaManager.PlayAudioStream(mediaObject, _nativeAudioStream, RoleplayingMediaCore.SoundType.Loop, false, false, 1, 0, false, delegate {
                                _addonTalkHandler.StopLipSync(character);
                            }, delegate (object sender, StreamVolumeEventArgs e) {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(character, 4);
                                        lipWasSynced = true;
                                    } else {
                                        _addonTalkHandler.StopLipSync(character);
                                    }
                                }
                            });
                            if (!_mediaManager.LowPerformanceMode && diskCopy.Length > 0) {
                                _ = Task.Run(async () => {
                                    try {
                                        using (FileStream fileStream = new FileStream(stagingPath + @"\" + emote + ".mp3", FileMode.Create, FileAccess.Write)) {
                                            diskCopy.Position = 0;
                                            MediaFoundationEncoder.EncodeToMp3(new RawSourceWaveStream(diskCopy, _nativeAudioStream.WaveFormat), fileStream);
                                        }
                                        _nativeAudioStream = null;
                                        diskCopy?.Dispose();
                                    } catch (Exception e) {
                                        Plugin.PluginLog?.Warning(".scd conversion to .mp3 failed");
                                        diskCopy?.Dispose();
                                    }
                                });
                            }
                            soundPath = null;
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }
                } else {
                    Plugin.PluginLog?.Warning("Not currently sending");
                }
            } else {
                Plugin.PluginLog?.Warning("There is no available audio stream to play");
            }
        }
        private void QueueSCDTrigger(ScdFile scdFile) {
            if (scdFile != null) {
                if (scdFile.Audio != null) {
                    if (scdFile.Audio.Count == 1) {
                        _inGameSoundStartedAudio = true;
                        _nativeSoundExpiryTimer.Stop();
                        _nativeSoundExpiryTimer.Restart();
                        bool isVorbis = scdFile.Audio[0].Format == SscfWaveFormat.Vorbis;
                        _nativeAudioStream = !isVorbis ? new WaveChannel32(
                        WaveFormatConversionStream.CreatePcmStream(scdFile.Audio[0].Data.GetStream())) : scdFile.Audio[0].Data.GetStream();
                    }
                }
            }
        }
        public ScdFile GetScdFile(string soundPath) {
            using (FileStream fileStream = new FileStream(_scdReplacements[soundPath], FileMode.Open, FileAccess.Read)) {
                using (BinaryReader reader = new BinaryReader(fileStream)) {
                    return new ScdFile(reader);
                }
            }
        }
        #endregion
        #endregion
        #region Dynamic Emoting
        private void CheckForNewDynamicEmoteRequests() {
            try {
                if (_messageQueue.Count > 0 && !disposed) {
                    if (!_messageTimer.IsRunning) {
                        _messageTimer.Start();
                    } else {
                        if (_messageTimer.ElapsedMilliseconds > 1000) {
                            try {
                                _realChat.SendMessage(_messageQueue.Dequeue());
                            } catch (Exception e) {
                                Plugin.PluginLog?.Warning(e, e.Message);
                            }
                            _messageTimer.Restart();
                        }
                    }
                }
                if (_fastMessageQueue.Count > 0 && !disposed) {
                    try {
                        _realChat.SendMessage(_fastMessageQueue.Dequeue());
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
                if (_aiMessageQueue.Count > 0 && !disposed) {
                    var message = _aiMessageQueue.Dequeue();
                    _chat.Print(new XivChatEntry() {
                        Message = new SeString(new List<Payload>() {
                        new TextPayload(" " + message.Item2) }),
                        Name = GetCustomNPCObject(message.Item1).NpcName,
                        Type = message.Item3
                    });
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
            if (_lastEmoteAnimationUsed != null) {
                Emote value = _lastEmoteAnimationUsed;
                _lastEmoteAnimationUsed = null;
                if (!Conditions.IsWatchingCutscene) {
                    _isAlreadyRunningEmote = true;
                    Task.Run(() => {
                        if (value.EmoteMode.Value.ConditionMode is not 3) {
                            Thread.Sleep(2000);
                        }
                        if (config.DebugMode) {
                            _chat.Print("Attempt to find nearest objects.");
                        }
                        List<ICharacter> characters = new List<ICharacter>();
                        foreach (var gameObject in GetNearestObjects()) {
                            try {
                                ICharacter character = gameObject as ICharacter;
                                if (character != null) {
                                    if (config.DebugMode) {
                                        Plugin.PluginLog.Debug(character.Name.TextValue + " found!");
                                    }
                                    if (!character.IsDead) {
                                        if (((character.ObjectKind == ObjectKind.Retainer ||
                                            character.ObjectKind == ObjectKind.BattleNpc ||
                                            character.ObjectKind == ObjectKind.EventNpc) && config.DebugMode) ||
                                            character.ObjectKind == ObjectKind.Companion ||
                                            character.ObjectKind == ObjectKind.Housing) {
                                            if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                if (character.ObjectKind != ObjectKind.Companion || PenumbraAndGlamourerHelperFunctions.IsHumanoid(character)) {
                                                    characters.Add(character);
                                                } else if (config.DebugMode) {
                                                    _chat.Print("Cannot apply animations to non humanoid minions.");
                                                }
                                            } else {
                                                if (config.DebugMode) {
                                                    _chat.Print("Cannot affect NPC's with map markers.");
                                                }
                                                characters.Clear();
                                                break;
                                            }
                                        }
                                    }
                                }
                            } catch {
                                if (config.DebugMode) {
                                    _chat.Print("Could not trigger emote on " + gameObject.Name + ".");
                                }
                            }
                        }
                        foreach (ICharacter character in characters) {
                            try {
                                if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                    if (config.DebugMode) {
                                        _chat.Print("Triggering emote! " + value.ActionTimeline[0].Value.RowId);
                                        _chat.Print("Emote Unknowns: " + $"{value.EmoteMode.Value.ConditionMode} {value.Unknown8},{value.Unknown9},{value.Unknown10},{value.Unknown13},{value.Unknown14}," +
                                            $"{value.Unknown17},{value.Unknown24},");
                                    }
                                    if (config.UsePlayerSync) {
                                        unsafe {
                                            var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                            if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                                _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)value.ActionTimeline[0].Value.RowId);
                                                _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)value.ActionTimeline[0].Value.RowId);
                                                Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                            }
                                        }
                                    }
                                    if (value.EmoteMode.Value.ConditionMode == 3 || value.EmoteMode.Value.ConditionMode == 11) {
                                        _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, character, (ushort)value.ActionTimeline[0].Value.RowId);
                                    } else {
                                        _addonTalkHandler.TriggerEmoteTimed(character, (ushort)value.ActionTimeline[0].Value.RowId, 1000);
                                    }
                                    Thread.Sleep(500);
                                }
                            } catch {
                                if (config.DebugMode) {
                                    _chat.Print("Could not trigger emote on " + character.Name.TextValue + ".");
                                }
                            }
                        }
                        _isAlreadyRunningEmote = false;
                    });
                }
            }
        }

        private GameObject GetObjectByTargetId(ulong objectId) {
            foreach (var item in _objectTable) {
                if (item.GameObjectId == objectId) {
                    return item;
                }
            }
            return null;

        }
        public Dictionary<string, ICharacter> GetLocalCharacters(bool incognito) {
            var _objects = new Dictionary<string, ICharacter>();
            if (_clientState.LocalPlayer != null) {
                _objects.Add(incognito ? "Player Character" :
                _clientState.LocalPlayer.Name.TextValue, _clientState.LocalPlayer as ICharacter);
                bool oneMinionOnly = false;
                foreach (var item in GetNearestObjects()) {
                    ICharacter character = item as ICharacter;
                    if (character != null) {
                        if (character.ObjectKind == ObjectKind.Companion) {
                            if (!oneMinionOnly) {
                                string name = "";
                                foreach (var customNPC in config.CustomNpcCharacters) {
                                    if (character.Name.TextValue.ToLower().Contains(customNPC.MinionToReplace.ToLower())) {
                                        name = customNPC.NpcName;
                                    }
                                }
                                if (!string.IsNullOrEmpty(name)) {
                                    _objects.Add(name, character);
                                }
                                oneMinionOnly = true;
                            }
                        } else if (character.ObjectKind == ObjectKind.EventNpc) {
                            if (!string.IsNullOrEmpty(character.Name.TextValue)) {
                                if (!_objects.ContainsKey(character.Name.TextValue)) {
                                    _objects.Add(character.Name.TextValue, character);
                                }
                            }
                        }
                    }
                }
            }
            return _objects;
        }
        public unsafe bool IsPartOfQuestOrImportant(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject) {
            return ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address)->NamePlateIconId is not 0;
        }

        public Dalamud.Game.ClientState.Objects.Types.IGameObject[] GetNearestObjects() {
            _playerCount = 0;
            List<Dalamud.Game.ClientState.Objects.Types.IGameObject> gameObjects = new List<Dalamud.Game.ClientState.Objects.Types.IGameObject>();
            foreach (var item in _objectTable) {
                if (Vector3.Distance(_clientState.LocalPlayer.Position, item.Position) < 3f
                    && item.GameObjectId != _clientState.LocalPlayer.GameObjectId) {
                    if (item.IsValid()) {
                        gameObjects.Add((item as Dalamud.Game.ClientState.Objects.Types.IGameObject));
                    }
                }
                if (item.ObjectKind == ObjectKind.Player) {
                    _playerCount++;
                }
            }
            return gameObjects.ToArray();
        }
        private async void EmoteReaction(string messageValue) {
            var emotes = _dataManager.GetExcelSheet<Emote>();
            string[] messageEmotes = messageValue.Replace("*", " ").Split("\"");
            string emoteString = " ";
            for (int i = 1; i < messageEmotes.Length + 1; i++) {
                if ((i + 1) % 2 == 0) {
                    emoteString += messageEmotes[i - 1] + " ";
                }
            }
            foreach (var item in emotes) {
                if (!string.IsNullOrWhiteSpace(item.Name.RawString)) {
                    if ((emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + " ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ing ") ||
                        emoteString.ToLower().EndsWith(" " + item.Name.RawString.ToLower()) ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ing"))
                        || (emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower()) && item.Name.RawString.Length > 3)) {
                        _messageQueue.Enqueue("/" + item.Name.RawString.ToLower());
                        break;
                    }
                }
            }
        }
        #endregion
        #region Sound Sync
        private void CheckForDownloadCancellation() {
            Task.Run(delegate {
                if (_maxDownloadLengthTimer.ElapsedMilliseconds > 30000) {
                    isDownloadingZip = false;
                    _maxDownloadLengthTimer.Reset();
                }
            });
        }
        List<string> GetCombinedWhitelist() {
            List<string> list = new List<string>();
            list.AddRange(config.Whitelist);
            list.AddRange(temporaryWhitelist);
            return list;
        }
        #endregion
        #region Collect Sound Data
        public async void RefreshData(bool skipModelData = false, bool skipPenumbraScan = false) {
            if (!disposed) {
                if (!_blockDataRefreshes) {
                    if (config.PlayerVoiceEngine == 1) {
                        if (_roleplayingMediaManager != null) {
                            _roleplayingMediaManager?.InitializeXTTS();
                        }
                    }
                    _catalogueWindow.CataloguePath = Path.Combine(config.CacheFolder, "ClothingCatalogue\\");
                    _ = Task.Run(async () => {
                        try {
                            if (penumbraSoundPacks == null || penumbraSoundPacks.Count == 0 || !skipPenumbraScan) {
                                penumbraSoundPacks = await GetPrioritySortedModPacks(skipModelData);
                            }
                            combinedSoundList = await GetCombinedSoundList(penumbraSoundPacks);
                            IpcSystem?.InvokeOnVoicePackChanged();
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        } catch (Exception e) {
                            Plugin.PluginLog.Error(e.Message);
                        }
                    });
                    if (!config.VoicePackIsActive) {
                        try {
                            if (Filter != null) {
                                Filter.Muted = false;
                                voiceMuted = false;
                                RefreshPlayerVoiceMuted();
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog.Error(e.Message);
                        }
                    } else {
                    }
                }
            }
        }
        public async Task<List<string>> GetCombinedSoundList(List<KeyValuePair<List<string>, int>> sounds) {
            List<string> list = new List<string>();
            Dictionary<string, bool> keyValuePairs = new Dictionary<string, bool>();
            foreach (var sound in sounds) {
                foreach (string value in sound.Key) {
                    string strippedValue = CharacterVoicePack.StripNonCharacters(Path.GetFileNameWithoutExtension(value), _clientState.ClientLanguage);
                    bool allowedToAdd;
                    if (keyValuePairs.ContainsKey(strippedValue)) {
                        allowedToAdd = !keyValuePairs[strippedValue];
                    } else {
                        keyValuePairs[strippedValue] = false;
                        allowedToAdd = true;
                    }
                    if (allowedToAdd) {
                        list.Add(value);
                    }
                }
                foreach (string value in keyValuePairs.Keys) {
                    if (!string.IsNullOrEmpty(value)) {
                        try {
                            keyValuePairs[value] = true;
                        } catch { }
                    }
                }
            }
            _ = Task.Run(async () => {
                if (list != null) {
                    while (staging) {
                        Thread.Sleep(1000);
                    }
                    staging = true;
                    if (_clientState.LocalPlayer != null) {
                        stagingPath = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                        if (Directory.Exists(config.CacheFolder + @"\Staging")) {
                            foreach (string file in Directory.EnumerateFiles(config.CacheFolder + @"\Staging")) {
                                try {
                                    File.Delete(file);
                                } catch (Exception e) {
                                    //Plugin.PluginLog.Warning(e, e.Message);
                                }
                            }
                        }
                        try {
                            Directory.CreateDirectory(stagingPath);
                        } catch {
                            _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
                        }
                        if (Directory.Exists(stagingPath)) {
                            foreach (string file in Directory.EnumerateFiles(stagingPath)) {
                                try {
                                    File.Delete(file);
                                } catch (Exception e) {
                                }
                            }
                        }
                        foreach (var sound in list) {
                            try {
                                File.Copy(sound, Path.Combine(stagingPath, Path.GetFileName(sound)), true);
                            } catch (Exception e) {
                            }
                        }
                    }
                    staging = false;
                }
            });
            return list;
        }
        #endregion
        #region Data Cleanup
        public void RemoveFiles(string path) {
            try {
                Directory.Delete(path, true);
            } catch {
                foreach (string file in Directory.EnumerateFiles(path)) {
                    try {
                        File.Delete(file);
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
            }
        }
        private void gameObjectRedrawn(nint arg1, int arg2) {
            if (!disposed) {
                if (!_redrawCooldown.IsRunning) {
                    _redrawCooldown.Start();
                    redrawObjectCount = _objectTable.Count<GameObject>();
                }
                if (_redrawCooldown.IsRunning) {
                    objectsRedrawn++;
                }
                string senderName = CleanSenderName(_objectTable[arg2].Name.TextValue);
                string path = config.CacheFolder + @"\VoicePack\Others";
                string hash = RoleplayingMediaManager.Shai1Hash(senderName);
                string clipPath = path + @"\" + hash;
                if (GetCombinedWhitelist().Contains(senderName) &&
                    !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                    if (Directory.Exists(clipPath)) {
                        try {
                            RemoveFiles(clipPath);
                            if (_characterVoicePacks.ContainsKey(senderName)) {
                                _characterVoicePacks.Remove(senderName);
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }
                } else if (!temporaryWhitelist.Contains(senderName) && config.IgnoreWhitelist &&
                    !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                    temporaryWhitelistQueue.Enqueue(senderName);
                } else if (_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                    RefreshData();
                }
                if (_wasDoingFakeEmote && _clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                    _addonTalkHandler.StopEmote(_clientState.LocalPlayer.Address);
                    _wasDoingFakeEmote = false;
                }
            }
        }

        private void _clientState_LeavePvP() {
            CleanSounds();
        }

        private void _clientState_TerritoryChanged(ushort e) {
            if (config.DebugMode) {
                _chat?.Print("Territory is " + e);
            }
            CleanSounds();
            if (_recentCFPop > 0) {
                _recentCFPop++;
            }
            if (config.UsePlayerSync) {
                Task.Run(async () => {
                    if (_clientState.LocalPlayer != null && _clientState.IsLoggedIn) {
                        string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                        bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                    }
                });
            }
        }
        private void CleanupEmoteWatchList() {
            foreach (var item in _emoteWatchList.Values) {
                try {
                    item.Dispose();
                } catch {

                }
            }
            _emoteWatchList.Clear();
            _preOccupiedWithEmoteCommand.Clear();
        }
        private unsafe bool IsResidential() {
            return HousingManager.Instance()->IsInside() || HousingManager.Instance()->OutdoorTerritory != null;
        }

        private void _clientState_Logout() {
            CleanSounds();
        }

        private void _clientState_Login() {
            CleanSounds();
            CheckDependancies(true);
            RefreshData();
        }
        public void CleanSounds() {
            _mountingOccured = false;
            string othersPath = config.CacheFolder + @"\VoicePack\Others";
            string incomingPath = config.CacheFolder + @"\Incoming\";
            if (_mediaManager != null) {
                _mediaManager.CleanSounds();
            }
            ResetTwitchValues();
            temporaryWhitelist.Clear();
            if (Directory.Exists(othersPath)) {
                try {
                    Directory.Delete(othersPath, true);
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
            }
            if (Directory.Exists(incomingPath)) {
                try {
                    Directory.Delete(incomingPath, true);
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
            }
            CleanupEmoteWatchList();
        }
        public void ResetTwitchValues() {
            _lastStreamObject = null;
            _streamURLs = null;
            if (streamWasPlaying) {
                streamWasPlaying = false;
                _videoWindow.IsOpen = false;
                try {
                    _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
            }
            potentialStream = "";
            lastStreamURL = "";
            _currentStreamer = "";
            _streamSetCooldown.Stop();
            _streamSetCooldown.Reset();
        }
        #endregion
        #region Connection Attempts
        private void Window_RequestingReconnect(object sender, EventArgs e) {
            AttemptConnection();
        }

        private void AttemptConnection() {
            if (_networkedClient != null) {
                _networkedClient.Dispose();
            }
            if (config != null) {
                _networkedClient = new NetworkedClient(config.ConnectionIP);
                _networkedClient.OnConnectionFailed += _networkedClient_OnConnectionFailed;
                if (_roleplayingMediaManager != null) {
                    _roleplayingMediaManager.NetworkedClient = _networkedClient;
                }
            }
        }

        private void _networkedClient_OnConnectionFailed(object sender, FailureMessage e) {
            Plugin.PluginLog.Error(e.Message);
        }
        #endregion
        #region Emote Processing
        private async Task<bool> CheckEmoteExistsInDirectory(string path, string emoteString) {
            if (Directory.Exists(path)) {
                foreach (string file in Directory.EnumerateFiles(path)) {
                    if (file.ToLower().Contains(emoteString.ToLower())) {
                        return true;
                    }
                }
            }
            return false;
        }
        private async void ReceivingEmote(ICharacter instigator, ushort emoteId) {
            if (instigator != null) {
                try {
                    string[] senderStrings = SplitCamelCase(
                    RemoveActionPhrases(RemoveSpecialSymbols(instigator.Name.TextValue))).Split(' ');
                    bool isShoutYell = false;
                    if (senderStrings.Length > 2) {
                        int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                        string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                        string path = config.CacheFolder + @"\VoicePack\Others";
                        try {
                            Directory.CreateDirectory(path);
                        } catch {
                            _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                        }
                        string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
                        string clipPath = path + @"\" + hash;
                        try {
                            if (config.UsePlayerSync) {
                                if (GetCombinedWhitelist().Contains(playerSender)) {
                                    if (!isDownloadingZip) {
                                        if (!Path.Exists(clipPath) || !(await CheckEmoteExistsInDirectory(clipPath, GetEmoteName(emoteId)))) {
                                            if (Path.Exists(clipPath)) {
                                                RemoveFiles(clipPath);
                                            }
                                            if (_characterVoicePacks.ContainsKey(playerSender)) {
                                                _characterVoicePacks.Remove(playerSender);
                                            }
                                            isDownloadingZip = true;
                                            _maxDownloadLengthTimer.Restart();
                                            await Task.Run(async () => {
                                                try {
                                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                                } catch { }
                                                isDownloadingZip = false;
                                            });
                                        }
                                        await Task.Run(async () => {
                                            Stopwatch copyTimer = new Stopwatch();
                                            copyTimer.Start();
                                            while (isDownloadingZip) {
                                                Thread.Sleep(100);
                                            }
                                            if (Directory.Exists(path)) {
                                                CharacterVoicePack characterVoicePack = null;
                                                if (!_characterVoicePacks.ContainsKey(playerSender)) {
                                                    characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                                                } else {
                                                    characterVoicePack = _characterVoicePacks[playerSender];
                                                }
                                                bool isVoicedEmote = false;
                                                string value = GetEmotePath(characterVoicePack, emoteId, (int)copyTimer.Elapsed.TotalSeconds, out isVoicedEmote);
                                                if (!string.IsNullOrEmpty(value)) {
                                                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                                                    TimeCodeData data = RaceVoice.TimeCodeData[PenumbraAndGlamourerHelperFunctions.GetRace(instigator) + "_" + gender];
                                                    copyTimer.Stop();
                                                    bool lipWasSynced = false;
                                                    _mediaManager.PlayAudio(new MediaGameObject(instigator), value, SoundType.OtherPlayer, false,
                                                     characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0, copyTimer.Elapsed, delegate {
                                                         Task.Run(delegate {
                                                             _addonTalkHandler.StopLipSync(instigator);
                                                         });
                                                     }, delegate (object sender, StreamVolumeEventArgs e) {
                                                         Task.Run(delegate {
                                                             if (e.MaxSampleValues.Length > 0) {
                                                                 if (e.MaxSampleValues[0] > 0.2) {
                                                                     _addonTalkHandler.TriggerLipSync(instigator, 2);
                                                                     lipWasSynced = true;
                                                                 } else {
                                                                     _addonTalkHandler.StopLipSync(instigator);
                                                                 }
                                                             }
                                                         });
                                                     });
                                                    Task.Run(delegate {
                                                        Thread.Sleep((int)((decimal)1000m * data.TimeCodes[characterVoicePack.EmoteIndex]));
                                                        _addonTalkHandler.TriggerLipSync(instigator, 4);
                                                    });
                                                    if (isVoicedEmote) {
                                                        MuteVoiceCheck(6000);
                                                    }
                                                } else {
                                                    _mediaManager.StopAudio(new MediaGameObject(instigator));
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + e.Message);
                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + e.Message);
                }
            }
        }
        private void SendingEmote(ICharacter instigator, ushort emoteId) {
            Task.Run(delegate {
                if (config.CharacterVoicePacks.ContainsKey(instigator.Name.TextValue)) {
                    _voice = config.CharacterVoicePacks[instigator.Name.TextValue];
                    _voicePackPath = config.CacheFolder + @"\VoicePack\" + _voice;
                    _voicePackStaging = config.CacheFolder + @"\Staging\" + instigator.Name.TextValue;
                    if (_mainCharacterVoicePack == null) {
                        _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                    }
                    bool isVoicedEmote = false;
                    _lastEmoteUsed = GetEmoteName(emoteId);
                    string emotePath = GetEmotePath(_mainCharacterVoicePack, emoteId, 0, out isVoicedEmote);
                    if (!string.IsNullOrEmpty(emotePath)) {
                        string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                        TimeCodeData data = RaceVoice.TimeCodeData[PenumbraAndGlamourerHelperFunctions.GetRace(instigator) + "_" + gender];
                        _mediaManager.StopAudio(new MediaGameObject(instigator));
                        bool lipWasSynced = false;
                        _mediaManager.PlayAudio(_playerObject, emotePath, SoundType.Emote, false,
                        _mainCharacterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000m * data.TimeCodes[_mainCharacterVoicePack.EmoteIndex]) : 0, default, delegate {
                            Task.Run(delegate {
                                _addonTalkHandler.StopLipSync(instigator);
                            });
                        },
                        delegate (object sender, StreamVolumeEventArgs e) {
                            Task.Run(delegate {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(instigator, 2);
                                        lipWasSynced = true;
                                    } else {
                                        _addonTalkHandler.StopLipSync(instigator);
                                    }
                                }
                            });
                        });
                        Task.Run(delegate {
                            if (_mainCharacterVoicePack.EmoteIndex > -1) {
                                Thread.Sleep((int)((decimal)1000m * data.TimeCodes[_mainCharacterVoicePack.EmoteIndex]));
                            }
                            _addonTalkHandler.TriggerLipSync(instigator, 2);
                        });
                        if (isVoicedEmote) {
                            MuteVoiceCheck(10000);
                        }
                    } else {
                        if (!_inGameSoundStartedAudio) {
                            _mediaManager.StopAudio(new MediaGameObject(instigator));
                        }
                        _inGameSoundStartedAudio = false;
                    }
                    if (config.UsePlayerSync) {
                        Task.Run(async () => {
                            bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, _voicePackStaging);
                        });
                    }
                }
                _didRealEmote = true;
            });
        }
        private string GetEmoteName(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            if (emote != null) {
                return CleanSenderName(emote.Name).Replace(" ", "").ToLower();
            } else {
                return "";
            }
        }
        private Emote GetEmoteData(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            if (emote != null) {
                return emote;
            } else {
                return null;
            }
        }
        private string GetEmoteCommand(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            return CleanSenderName(emote.TextCommand.Value.Command.RawString).Replace(" ", "").ToLower();
        }
        private string GetEmotePath(CharacterVoicePack characterVoicePack, ushort emoteId, int delay, out bool isVoicedEmote) {
            Emote emoteEnglish = _dataManager.GetExcelSheet<Emote>(ClientLanguage.English).GetRow(emoteId);
            Emote emoteFrench = _dataManager.GetExcelSheet<Emote>(ClientLanguage.French).GetRow(emoteId);
            Emote emoteGerman = _dataManager.GetExcelSheet<Emote>(ClientLanguage.German).GetRow(emoteId);
            Emote emoteJapanese = _dataManager.GetExcelSheet<Emote>(ClientLanguage.Japanese).GetRow(emoteId);

            string emotePathId = characterVoicePack.GetMisc(emoteId.ToString(), delay, true);
            string emotePathEnglish = characterVoicePack.GetMisc(emoteEnglish.Name, delay, true);
            string emotePathFrench = characterVoicePack.GetMisc(emoteFrench.Name, delay, true);
            string emotePathGerman = characterVoicePack.GetMisc(emoteGerman.Name, delay, true);
            string emotePathJapanese = characterVoicePack.GetMisc(emoteJapanese.Name, delay, true);

            characterVoicePack.EmoteIndex = -1;
            isVoicedEmote = true;
            switch (emoteId) {
                case 1:
                    characterVoicePack.EmoteIndex = 0;
                    break;
                case 2:
                    characterVoicePack.EmoteIndex = 1;
                    break;
                case 3:
                    characterVoicePack.EmoteIndex = 2;
                    break;
                case 6:
                    characterVoicePack.EmoteIndex = 3;
                    break;
                case 13:
                    characterVoicePack.EmoteIndex = 4;
                    break;
                case 14:
                    characterVoicePack.EmoteIndex = 5;
                    break;
                case 17:
                    characterVoicePack.EmoteIndex = 6;
                    break;
                case 20:
                    characterVoicePack.EmoteIndex = 7;
                    break;
                case 21:
                    characterVoicePack.EmoteIndex = 8;
                    break;
                case 24:
                    characterVoicePack.EmoteIndex = 9;
                    break;
                case 37:
                    characterVoicePack.EmoteIndex = 10;
                    break;
                case 40:
                    characterVoicePack.EmoteIndex = 11;
                    break;
                case 42:
                    characterVoicePack.EmoteIndex = 12;
                    break;
                case 48:
                    characterVoicePack.EmoteIndex = 13;
                    break;
                default:
                    isVoicedEmote = false;
                    break;
            }

            if (!string.IsNullOrEmpty(emotePathId)) {
                return emotePathId;
            } else if (!string.IsNullOrEmpty(emotePathEnglish)) {
                return emotePathEnglish;
            } else if (!string.IsNullOrEmpty(emotePathFrench)) {
                return emotePathFrench;
            } else if (!string.IsNullOrEmpty(emotePathGerman)) {
                return emotePathGerman;
            } else if (!string.IsNullOrEmpty(emotePathJapanese)) {
                return emotePathJapanese;
            }
            return string.Empty;
        }

        #endregion Emote Processing
        #region Penumbra Parsing
        public void ExtractSCDOptions(Option option, string directory) {
            Task.Run(() => {
                if (option != null) {
                    foreach (var item in option.Files) {
                        if (item.Key.EndsWith(".scd")) {
                            _filter.Blacklist.Add(item.Key);
                            if (!_scdReplacements.ContainsKey(item.Key)) {
                                try {
                                    _scdReplacements.TryAdd(item.Key, directory + @"\" + item.Value);
                                    if (config.DebugMode) {
                                        Plugin.PluginLog?.Verbose("Found: " + item.Value);
                                    }
                                } catch {
                                    Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                                }
                            }
                        }
                    }
                }
            });
        }

        public void ExtractPapFiles(Option option, string directory, bool skipScd) {
            Task.Run(() => {
                string modName = Path.GetFileName(directory);
                int papFilesFound = 0;
                for (int i = 0; i < option.Files.Count; i++) {
                    var item = option.Files.ElementAt(i);
                    if (item.Key.EndsWith(".pap")) {
                        string[] strings = item.Key.Split("/");
                        string value = strings[strings.Length - 1];
                        if (!_animationMods.ContainsKey(modName)) {
                            _animationMods[modName] = new KeyValuePair<string, List<string>>(directory, new());
                        }
                        if (!_animationMods[modName].Value.Contains(value)) {
                            _animationMods[modName].Value.Add(value);
                        }
                        papFilesFound++;
                        if (!_papSorting.ContainsKey(value)) {
                            try {
                                _papSorting.TryAdd(value, new List<Tuple<string, string, bool>>() { new Tuple<string, string, bool>(directory, modName, !skipScd) });

                                if (config.DebugMode) {
                                    Plugin.PluginLog?.Verbose("Found: " + item.Value);
                                }
                            } catch {
                                Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                            }
                        } else {
                            _papSorting[value].Add(new Tuple<string, string, bool>(directory, modName, !skipScd));
                        }
                    }
                }
            });
        }

        public void ExtractMdlFiles(Option option, string directory, bool skipFile) {
            Task.Run(() => {
                string modName = Path.GetFileName(directory);
                int mdlFilesFound = 0;
                for (int i = 0; i < option.Files.Count; i++) {
                    var item = option.Files.ElementAt(i);
                    //ulong modelId = GetModelID(item.Key);
                    if (item.Key.Contains(".mdl") && (item.Key.Contains("equipment") || item.Key.Contains("accessor")
                        && !directory.ToLower().Contains("hrothgar & viera") && !directory.ToLower().Contains("megapack") && !directory.ToLower().Contains("ivcs"))
                       /* && modelId != 279 && modelId != 9903*/) {
                        mdlFilesFound++;
                    } else if (/*modelId == 279 && modelId == 9903 &&*/ !directory.ToLower().Contains("ivcs")) {
                        _modelDependancyMods[modName] = null;
                    }
                    Thread.Sleep(10);
                }
                if (mdlFilesFound > 0) {
                    _modelMods[modName] = null;
                }
            });
        }

        public ulong GetModelID(string model) {
            string[] strings = model.Split("/");
            ulong newValue = 0;
            foreach (string value in strings) {
                if (value.StartsWith("e") || value.StartsWith("a")) {
                    try {
                        newValue = ulong.Parse(value.Replace("e", "").Replace("a", "").TrimStart('0'));
                        break;
                    } catch {
                    }
                }
            }
            return newValue;
        }



        public void SetDesign(Guid design, int objectId) {
            try {
                PenumbraAndGlamourerIpcWrapper.Instance.ApplyDesign.Invoke(design, objectId);
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }
        public void ApplyByGuid(Guid design, ICharacter? character) {
            PenumbraAndGlamourerIpcWrapper.Instance.ApplyDesign.Invoke(design, character.ObjectIndex);
        }
        public void CleanEquipment(int objectIndex) {
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Head, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Ears, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Neck, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Body, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Legs, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Hands, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.LFinger, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.RFinger, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Feet, 0, null);
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Wrists, 0, null);
        }

        public void RecursivelyFindPapFiles(string modName, string directory, int levels, int maxLevels) {
            foreach (string file in Directory.EnumerateFiles(directory)) {
                if (file.EndsWith(".pap")) {
                    string[] strings = file.Split("\\");
                    string value = strings[strings.Length - 1];
                    if (!_animationMods.ContainsKey(modName)) {
                        _animationMods[modName] = new KeyValuePair<string, List<string>>(directory, new());
                    }
                    if (!_animationMods[modName].Value.Contains(value)) {
                        _animationMods[modName].Value.Add(value);
                    }
                    if (!_papSorting.ContainsKey(value)) {
                        try {
                            _papSorting.TryAdd(value, new List<Tuple<string, string, bool>>()
                            { new Tuple<string, string, bool>(directory, modName, false) });
                        } catch {
                            Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + value + " already exists, ignoring.");
                        }
                    } else {
                        _papSorting[value].Add(new Tuple<string, string, bool>(directory, modName, false));
                    }
                }
            }
            if (levels < maxLevels) {
                foreach (string file in Directory.EnumerateDirectories(directory)) {
                    RecursivelyFindPapFiles(modName, file, levels + 1, maxLevels);
                }
            }
        }

        public async Task<List<KeyValuePair<List<string>, int>>> GetPrioritySortedModPacks(bool skipModelData) {
            Filter.Blacklist?.Clear();
            _scdReplacements?.Clear();
            //_papSorting?.Clear();
            //_mdlSorting?.Clear();
            List<KeyValuePair<List<string>, int>> list = new List<KeyValuePair<List<string>, int>>();
            string refreshGuid = Guid.NewGuid().ToString();
            _currentModPackRefreshGuid = refreshGuid;
            try {
                if (_penumbraReady) {
                    string modPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                    if (Directory.Exists(modPath)) {
                        var collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(0);
                        foreach (var directory in Directory.EnumerateDirectories(modPath)) {
                            if (refreshGuid == _currentModPackRefreshGuid) {
                                if (config.DebugMode) {
                                    Plugin.PluginLog?.Verbose("Examining: " + directory);
                                }
                                Option option = null;
                                List<Group> groups = new List<Group>();
                                string modName = Path.GetFileName(directory);
                                if (Directory.Exists(directory)) {
                                    foreach (string file in Directory.EnumerateFiles(directory)) {
                                        if (file.EndsWith(".json") && !file.EndsWith("meta.json")) {
                                            if (file.EndsWith("default_mod.json")) {
                                                option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(file));
                                            } else {
                                                groups.Add(JsonConvert.DeserializeObject<Group>(File.ReadAllText(file)));
                                            }
                                        }
                                    }
                                }
                                if (!_alreadyScannedMods.ContainsKey(modName)) {
                                    _alreadyScannedMods[modName] = true;
                                    if (option != null) {
                                        ExtractPapFiles(option, directory, true);
                                        if (!skipModelData) {
                                            ExtractMdlFiles(option, directory, true);
                                        }
                                    }
                                    foreach (Group group in groups) {
                                        if (group != null) {
                                            foreach (Option groupOption in group.Options) {
                                                ExtractPapFiles(groupOption, directory, true);
                                                if (!skipModelData) {
                                                    ExtractMdlFiles(groupOption, directory, true);
                                                }
                                            }
                                        }
                                    }
                                }
                                try {
                                    string relativeDirectory = directory.Replace(modPath, null).TrimStart('\\');
                                    var currentModSettings =
                                    PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.
                                    Invoke(collection.Item3.Id, relativeDirectory, null, true);
                                    var result = currentModSettings.Item1;
                                    if (result == Penumbra.Api.Enums.PenumbraApiEc.Success) {
                                        if (currentModSettings.Item2 != null) {
                                            bool enabled = currentModSettings.Item2!.Value!.Item1;
                                            int priority = currentModSettings.Item2!.Value!.Item2;
                                            if (enabled) {
                                                if (option != null) {
                                                    ExtractSCDOptions(option, directory);
                                                }
                                                foreach (Group group in groups) {
                                                    if (group != null) {
                                                        foreach (Option groupOption in group.Options) {
                                                            ExtractSCDOptions(groupOption, directory);
                                                        }
                                                    }
                                                }
                                                string soundPackData = directory + @"\rpvsp";
                                                string soundPackData2 = directory + @"\arksp";
                                                GetSoundPackData(soundPackData, priority, list);
                                                GetSoundPackData(soundPackData2, priority, list);
                                            }
                                        }
                                    }
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                            } else {
                                break;
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning("Error 404, penumbra not found.");
            }
            if (config != null) {
                if (config.CharacterVoicePacks != null) {
                    if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                        string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                        if (!string.IsNullOrEmpty(voice)) {
                            string path = config.CacheFolder + @"\VoicePack\" + voice;
                            if (Directory.Exists(path)) {
                                list.Add(new KeyValuePair<List<string>, int>(Directory.EnumerateFiles(path).ToList(), list.Count));
                            }
                        }
                    }
                }
            }
            if (list.Count > 0) {
                list.Sort((x, y) => y.Value.CompareTo(x.Value));
            }
            return list;
        }

        private void GetSoundPackData(string soundPackData, int priority, List<KeyValuePair<List<string>, int>> list) {
            if (Path.Exists(soundPackData)) {
                var soundList = new List<string>();
                foreach (string file in Directory.EnumerateFiles(soundPackData)) {
                    if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                        soundList.Add(file);
                    }
                }
                list.Add(new KeyValuePair<List<string>, int>(soundList, priority));
            }
        }
        #endregion
        #region String Sanitization
        public string RemoveActionPhrases(string value) {
            return value.Replace("Direct hit ", null)
                    .Replace("Critical direct hit ", null)
                    .Replace("Critical ", null)
                    .Replace("Direct ", null)
                    .Replace("direct ", null);
        }
        public static string CleanSenderName(string senderName) {
            string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(senderName)).Split(" ");
            string playerSender = senderStrings.Length == 1 ? senderStrings[0] : senderStrings.Length == 2 ?
                (senderStrings[0] + " " + senderStrings[1]) :
                (senderStrings[0] + " " + senderStrings[2]);
            return playerSender;
        }
        public static string SplitCamelCase(string input) {
            return Regex.Replace(input, "([A-Z])", " $1",
                RegexOptions.Compiled).Trim();
        }
        public static string RemoveSpecialSymbols(string value) {
            Regex rgx = new Regex(@"[^a-zA-Z0-9:/._\ -]");
            return rgx.Replace(value, "");
        }
        #endregion
        #region Stream Management
        private void TuneIntoStream(string url, RoleplayingMediaCore.IMediaGameObject audioGameObject, bool isNotTwitch) {
            Task.Run(async () => {
                string cleanedURL = RemoveSpecialSymbols(url);
                _streamURLs = isNotTwitch ? new string[] { url } : TwitchFeedManager.GetServerResponse(cleanedURL);
                _videoWindow.IsOpen = config.DefaultTwitchOpen == 0;
                if (_streamURLs.Length > 0) {
                    _mediaManager.PlayStream(audioGameObject, _streamURLs[(int)_videoWindow.FeedType]);
                    lastStreamURL = cleanedURL;
                    if (!isNotTwitch) {
                        _currentStreamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                        _chat?.Print(@"Tuning into " + _currentStreamer + @"! Wanna chat? Use ""/artemis twitch""." +
                            "\r\nYou can also use \"/artemis video\" to toggle the video feed!" +
                            (!IsResidential() ? "\r\nIf you need to end a stream in a public space you can leave the zone or use \"/artemis endlisten\"" : ""));
                    } else {
                        _currentStreamer = "RTMP Streamer";
                        _chat?.Print(@"Tuning into a custom RTMP stream!" +
                            "\r\nYou can also use \"/artemis video\" to toggle the video feed!" +
                            (!IsResidential() ? "\r\nIf you need to end a stream in a public space you can leave the zone or use \"/artemis endlisten\"" : ""));
                    }
                }
            });
            streamWasPlaying = true;
            try {
                _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
            _streamSetCooldown.Stop();
            _streamSetCooldown.Reset();
            _streamSetCooldown.Start();
        }
        private void ChangeStreamQuality() {
            if (_streamURLs != null) {
                if (streamWasPlaying && _streamURLs.Length > 0) {
                    Task.Run(async () => {
                        if ((int)_videoWindow.FeedType < _streamURLs.Length) {
                            if (_lastStreamObject != null) {
                                try {
                                    _mediaManager.ChangeStream(_lastStreamObject,
                                         _streamURLs[(int)_videoWindow.FeedType], _videoWindow.Size.Value.X);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                            }
                        }
                    });
                }
            }
        }
        #endregion
        #region UI Management
        private void UiBuilder_Draw() {
            this.windowSystem.Draw();
        }
        private void UiBuilder_OpenConfigUi() {
            _window?.RefreshVoices();
            _window?.Toggle();
        }
        #endregion
        #region Chat Commands
        [Command("/rpvoice")]
        [HelpMessage("OpenConfig")]
        public void ExecuteCommandA(string command, string args) {
            OpenConfig(command, args);
        }
        [Command("/ark")]
        [HelpMessage("OpenConfig")]
        public void ExecuteCommandB(string command, string args) {
            OpenConfig(command, args);
        }
        [Command("/artemis")]
        [HelpMessage("OpenConfig")]
        public void ExecuteCommandC(string command, string args) {
            OpenConfig(command, args);
        }
        [Command("/cc")]
        [HelpMessage("Chat With Custom NPC")]
        public void ExecuteCommandD(string command, string args) {
            bool handled = false;
            var name = _clientState.LocalPlayer.Name;
            var message = new SeString(new TextPayload(args.Replace("cc", "")));
            _chat.Print(new XivChatEntry() { Name = name, Message = message, Timestamp = -1, Type = XivChatType.Party });
            //Chat_ChatMessage(XivChatType.Say, 0, ref name, ref message, ref handled);
        }
        public void OpenConfig(string command, string args) {
            if (!disposed) {
                string[] splitArgs = args.Split(' ');
                if (splitArgs.Length > 0) {
                    switch (splitArgs[0].ToLower()) {
                        case "help":
                            _chat?.Print("on (Enable AI Voice)\r\n" +
                             "off (Disable AI Voice)\r\n" +
                             "video (toggle twitch stream video)\r\n" +
                             "listen (tune into a publically shared twitch stream)\r\n" +
                             "endlisten (end a publically shared twitch stream)\r\n" +
                             "anim [partial animation mod name] (triggers an animation mod that contains the desired text in its name)\r\n" +
                             "companionanim [partial animation mod name] (triggers an animation mod that contains the desired text in its name on the currently summoned minion)\r\n" +
                             "emotecontrol do [emote command] [NPC or Minion name] (Makes the desired NPC or Minion perform an emote)\r\n" +
                             "emotecontrol stop [NPC or Minion name] (Makes the desired NPC or Minion stop an emote)\r\n" +
                             "summon [Custom NPC Name] (Summons the specified custom NPC or dismisses them)\r\n" +
                             "catalogue (opens the outfit catalogue)\r\n" +
                             "catalogue scan (starts scanning new outfit mods)\r\n" +
                             "catalogue stop (stop scanning new outfit mods)\r\n" +
                             "catalogue clean (disables all outfit mods)\r\n" +
                             "twitch [twitch url] (forcibly tunes into a twitch stream locally)\r\n" +
                             "rtmp [rtmp url] (tunes into a raw RTMP stream locally)\r\n" +
                             "record (Converts spoken speech to in game chat)\r\n" +
                             "recordrp (Converts spoken speech to in game chat, but adds roleplaying quotes)\r\n" +
                             "textadvance (Toggles automatic text advancement when community provided dialogue finishes)\r\n" +
                             "npcvoice (Toggles crowdsourced NPC dialogue for unvoiced cutscenes)\r\n" +
                             "arrvoice (Toggles whether ARR voice acting will be replaced by new voices)\r\n" +
                             "clearsound (Stops all currently playing sounds, and clears out the sound cache for other players)");
                            break;
                        case "on":
                            config.AiVoiceActive = true;
                            _window.Configuration = config;
                            this.pluginInterface.SavePluginConfig(config);
                            config.AiVoiceActive = true;
                            break;
                        case "off":
                            config.AiVoiceActive = false;
                            _window.Configuration = config;
                            this.pluginInterface.SavePluginConfig(config);
                            config.AiVoiceActive = false;
                            break;
                        case "video":
                            _videoWindow.Toggle();
                            break;
                        case "reload":
                            AttemptConnection();
                            break;
                        case "anim":
                            _checkAnimationModsQueue.Enqueue(new Tuple<string[], string, ICharacter>(splitArgs, args, _clientState.LocalPlayer as ICharacter));
                            break;
                        case "companionanim":
                            Task.Run(() => {
                                ICharacter foundCharacter = null;
                                for (int i = 0; i < 4; i++) {
                                    foreach (var item in GetNearestObjects()) {
                                        ICharacter character = item as ICharacter;
                                        if (character != null && character.ObjectKind == ObjectKind.Companion) {
                                            foundCharacter = character;
                                            break;
                                        }
                                    }
                                    if (foundCharacter != null) {
                                        _preOccupiedWithEmoteCommand.Add(foundCharacter.Name.TextValue);
                                        _checkAnimationModsQueue.Enqueue(new Tuple<string[], string, ICharacter>(splitArgs, args, foundCharacter));
                                        break;
                                    }
                                    Thread.Sleep(1000);
                                }
                                if (foundCharacter == null) {
                                    _chat.PrintError("Could not find owned companion to apply animation");
                                }
                            });
                            break;
                        case "emotecontrol":
                            CheckNPCEmoteControl(splitArgs, args);
                            break;
                        case "summon":
                            bool foundNPC = false;
                            string npc = args.Replace(splitArgs[0], null).Trim();
                            foreach (var item in config.CustomNpcCharacters) {
                                if (item.NpcName.ToLower().Contains(npc.ToLower())) {
                                    MessageQueue.Enqueue("/minion " + @"""" + item.MinionToReplace + @"""");
                                    foundNPC = true;
                                    break;
                                }
                            }
                            if (!foundNPC) {
                                _chat.PrintError("Could not find custom NPC with the name " + @"""" + npc + @"""");
                            }
                            break;
                        case "twitch":
                            if (splitArgs.Length > 1 && splitArgs[1].Contains("twitch.tv")) {
                                _lastStreamObject = _playerObject;
                                TuneIntoStream(splitArgs[1], _playerObject, false);
                            } else {
                                if (!string.IsNullOrEmpty(_currentStreamer)) {
                                    try {
                                        Process.Start(new System.Diagnostics.ProcessStartInfo() {
                                            FileName = @"https://www.twitch.tv/popout/" + _currentStreamer + @"/chat?popout=",
                                            UseShellExecute = true,
                                            Verb = "OPEN"
                                        });
                                    } catch (Exception e) {
                                        Plugin.PluginLog?.Warning(e, e.Message);
                                    }
                                } else {
                                    _chat?.PrintError("There is no active stream");
                                }
                            }
                            break;
                        case "rtmp":
                            if (splitArgs.Length > 1 && splitArgs[1].Contains("rtmp")) {
                                TuneIntoStream(splitArgs[1], _playerObject, true);
                            }
                            break;
                        case "listen":
                            if (!string.IsNullOrEmpty(potentialStream)) {
                                TuneIntoStream(potentialStream, _playerObject, false);
                            }
                            break;
                        case "endlisten":
                            if (!IsResidential()) {
                                _mediaManager.StopStream();
                                ResetTwitchValues();
                                potentialStream = "";
                            }
                            break;
                        case "record":
                            _chat?.Print("Speech To Text Started");
                            _speechToTextManager.RpMode = false;
                            _speechToTextManager.RecordAudio();
                            break;
                        case "recordrp":
                            _chat?.Print("Speech To Text Started");
                            _speechToTextManager.RpMode = true;
                            _speechToTextManager.RecordAudio();
                            break;
                        case "textadvance":
                            config.AutoTextAdvance = !config.AutoTextAdvance;
                            if (config.AutoTextAdvance) {
                                _chat?.Print("Auto Text Advance Enabled");
                            } else {
                                _chat?.Print("Auto Text Advance Disabled");
                            }
                            config.Save();
                            break;
                        case "npcvoice":
                            config.NpcSpeechGenerationDisabled = !config.NpcSpeechGenerationDisabled;
                            if (config.NpcSpeechGenerationDisabled) {
                                _chat?.Print("Npc Voice Disabled");
                            } else {
                                _chat?.Print("Npc Voice Enabled");
                            }
                            config.Save();
                            break;
                        case "arrvoice":
                            config.ReplaceVoicedARRCutscenes = !config.ReplaceVoicedARRCutscenes;
                            if (config.ReplaceVoicedARRCutscenes) {
                                _chat?.Print("ARR Voice Replaced");
                            } else {
                                _chat?.Print("ARR Voice Vanilla");
                            }
                            config.Save();
                            break;
                        case "clearsound":
                            CleanSounds();
                            _chat?.Print("All Sounds Cleared!");
                            break;
                        case "catalogue":
                            if (splitArgs.Length > 1) {
                                switch (splitArgs[1].ToLower()) {
                                    case "scan":
                                        StartCatalogingItems();
                                        break;
                                    case "clean":
                                        // PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _modelMods.Keys, _modelDependancyMods.Keys);
                                        break;
                                    case "stop":
                                        _catalogueMods = false;
                                        _chat.Print("Stopping cataloguing.");
                                        //PenumbraAndGlamourerIpcWrapper.Instance.SetCollectionForObject.Invoke(0, _originalCollection.Item3.Id, true, true);
                                        break;
                                }

                            } else {
                                _catalogueWindow.IsOpen = true;
                            }
                            break;
                        default:
                            if (config.AiVoiceActive) {
                                _window.RefreshVoices();
                            }
                            _window.Toggle();
                            break;
                    }
                }
            }
        }

        public void StartCatalogingItems() {
            _originalCollection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(_clientState.LocalPlayer.ObjectIndex);
            _catalogueCollectionName = _originalCollection.Item3.Id;
            Directory.CreateDirectory(_catalogueWindow.CataloguePath);
            _currentScreenshotList = Directory.GetFiles(_catalogueWindow.CataloguePath);
            _chat?.Print("Creating Thumbnails For New Clothing Mods");
            _catalogueMods = true;
            _modelModList = new List<string>();
            _modelModList.AddRange(_modelMods.Keys);
            _catalogueWindow.ScanCatalogue();
            ScanClothingMods();
        }

        private void CheckNPCEmoteControl(string[] splitArgs, string args) {
            switch (splitArgs[1].ToLower()) {
                case "do":
                    DoEmote(splitArgs[2].ToLower(), splitArgs[3].ToLower());
                    break;
                case "stop":
                    foreach (var gameObject in GetNearestObjects()) {
                        try {
                            ICharacter character = gameObject as ICharacter;
                            if (character != null) {
                                if (!character.IsDead) {
                                    if (character.ObjectKind == ObjectKind.Retainer ||
                                        character.ObjectKind == ObjectKind.BattleNpc ||
                                        character.ObjectKind == ObjectKind.EventNpc ||
                                        character.ObjectKind == ObjectKind.Companion ||
                                        character.ObjectKind == ObjectKind.Housing) {
                                        if (character.Name.TextValue.ToLower().Contains(splitArgs[2].ToLower())) {
                                            if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                _toast.ShowNormal(character.Name.TextValue + " ceases your command.");
                                                _addonTalkHandler.StopEmote(character.Address);
                                                if (_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                                    _preOccupiedWithEmoteCommand.Remove(character.Name.TextValue);
                                                }
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        } catch { }
                    }
                    break;
                case "playerpos":
                    foreach (var gameObject in GetNearestObjects()) {
                        try {
                            ICharacter character = gameObject as ICharacter;
                            if (character != null) {
                                if (!character.IsDead) {
                                    if (character.ObjectKind == ObjectKind.Retainer ||
                                        character.ObjectKind == ObjectKind.BattleNpc ||
                                        character.ObjectKind == ObjectKind.EventNpc ||
                                        character.ObjectKind == ObjectKind.Companion ||
                                        character.ObjectKind == ObjectKind.Housing) {
                                        if (character.Name.TextValue.ToLower().Contains(splitArgs[2].ToLower())) {
                                            bool hasQuest = false;
                                            unsafe {
                                                var item = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(character as ICharacter).Address;
                                                item->Balloon.PlayTimer = 1;
                                                item->Balloon.Text = new FFXIVClientStructs.FFXIV.Client.System.String.Utf8String("I will stand here master!");
                                                item->Balloon.Type = BalloonType.Timer;
                                                item->Balloon.State = BalloonState.Active;
                                                item->GameObject.Position = _clientState.LocalPlayer.Position;
                                            }

                                            break;
                                        }
                                    }
                                }
                            }
                        } catch { }
                    }
                    break;
            }
        }

        public unsafe void DoEmote(string command, string targetNPC, bool becomesPreOccupied = true) {
            foreach (var emoteItem in DataManager.GameData.GetExcelSheet<Emote>()) {
                if (emoteItem.TextCommand != null && emoteItem.TextCommand.Value != null) {
                    if ((
                        emoteItem.TextCommand.Value.ShortCommand.RawString.Contains(command) ||
                        emoteItem.TextCommand.Value.Command.RawString.Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.RawString.Contains(command)) {
                        foreach (var gameObject in GetNearestObjects()) {
                            try {
                                ICharacter character = gameObject as ICharacter;
                                if (character != null) {
                                    if (!character.IsDead) {
                                        if (character.ObjectKind == ObjectKind.Retainer ||
                                            character.ObjectKind == ObjectKind.BattleNpc ||
                                            character.ObjectKind == ObjectKind.EventNpc ||
                                            character.ObjectKind == ObjectKind.Companion ||
                                            character.ObjectKind == ObjectKind.Housing) {
                                            if (character.Name.TextValue.ToLower().Contains(targetNPC.ToLower())) {
                                                if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                    if (AgentEmote.Instance()->CanUseEmote((ushort)emoteItem.RowId)) {
                                                        _toast.ShowNormal(character.Name.TextValue + " follows your command!");
                                                        var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                                        if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)emoteItem.RowId);
                                                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                        }
                                                        Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                                        if (becomesPreOccupied) {
                                                            _addonTalkHandler.TriggerEmote(character.Address, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                            if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                                                _preOccupiedWithEmoteCommand.Add(character.Name.TextValue);
                                                            }
                                                        } else {
                                                            _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, character,
                                                                (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                        }
                                                    } else {
                                                        _toast.ShowError(character.Name.TextValue + " you have not unlocked this emote yet.");
                                                    }
                                                } else {
                                                    _toast.ShowError(character.Name.TextValue + " resists your command! (Cannot affect quest NPCs)");
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }
                        break;
                    }
                }
            }
        }
        #endregion
        #region Trigger Animation Mods
        public void DoEmote(string command, ICharacter targetNPC, bool becomesPreOccupied = true) {
            try {
                foreach (var emoteItem in DataManager.GameData.GetExcelSheet<Emote>()) {
                    if (emoteItem.TextCommand != null && emoteItem.TextCommand.Value != null) {
                        if ((
                        emoteItem.TextCommand.Value.ShortCommand.RawString.Contains(command) ||
                        emoteItem.TextCommand.Value.Command.RawString.Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.RawString.Contains(command)) {
                            if (!IsPartOfQuestOrImportant(targetNPC as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                if (becomesPreOccupied) {
                                    _addonTalkHandler.TriggerEmote(targetNPC.Address, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    if (!_preOccupiedWithEmoteCommand.Contains(targetNPC.Name.TextValue)) {
                                        _preOccupiedWithEmoteCommand.Add(targetNPC.Name.TextValue);
                                    }
                                } else {
                                    if (emoteItem.EmoteMode.Value.ConditionMode == 3 || emoteItem.EmoteMode.Value.ConditionMode == 11) {
                                        _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, targetNPC,
                                         (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    } else {
                                        _addonTalkHandler.TriggerEmoteTimed(targetNPC, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            } catch {

            }
        }
        public void CheckAnimationMods(string[] splitArgs, string args, ICharacter character, bool willOpen = true) {
            Task.Run(() => {
                if (splitArgs.Length > 1) {
                    string[] command = null;
                    command = args.Replace(splitArgs[0] + " ", null).ToLower().Trim().Split("emote:");
                    int index = 0;
                    try {
                        if (command.Length > 1) {
                            index = int.Parse(command[1]);
                        }
                    } catch {
                        index = 0;
                    }
                    DoAnimation(command[0].Trim(), index, character);
                } else {
                    if (!_animationCatalogue.IsOpen) {
                        var list = CreateEmoteList(_dataManager);
                        var newList = new List<string>();
                        foreach (string key in _animationMods.Keys) {
                            foreach (string emote in _animationMods[key].Value) {
                                string[] strings = emote.Split("/");
                                string option = strings[strings.Length - 1];
                                if (list.ContainsKey(option)) {
                                    if (!newList.Contains(key)) {
                                        newList.Add(key);
                                        _animationCatalogue.AddNewItem(key);
                                    }
                                }
                            }
                        }
                        newList.Sort();
                        _animationCatalogue.AddNewList(newList);
                        if (willOpen) {
                            _animationCatalogue.IsOpen = true;
                        }
                    } else {
                        _animationCatalogue.IsOpen = false;
                    }
                }
            });
        }

        public void DoAnimation(string animationName, int index, ICharacter targetObject) {
            Task.Run(() => {
                _blockDataRefreshes = true;
                var list = CreateEmoteList(_dataManager);
                List<uint> deDuplicate = new List<uint>();
                List<EmoteModData> emoteData = new List<EmoteModData>();
                var collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke((int)targetObject.ObjectIndex);
                string commandArguments = animationName;
                if (config.DebugMode) {
                    Plugin.PluginLog.Debug("Attempting to find mods that contain \"" + commandArguments + "\".");
                }
                for (int i = 0; i < 20 && emoteData.Count == 0; i++) {
                    foreach (var modName in _animationMods.Keys) {
                        if (modName.ToLower().Contains(commandArguments)) {
                            if (collection.Item3.Name != "None") {
                                var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection.Item3.Id, modName, true);
                                _mediaManager.StopAudio(_playerObject);
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug(modName + " was attempted to be enabled. The result was " + result + ".");
                                }
                                var animationItems = _animationMods[modName];
                                foreach (var foundAnimation in animationItems.Value) {
                                    bool foundEmote = false;
                                    if (_papSorting.ContainsKey(foundAnimation)) {
                                        var sortedList = _papSorting[foundAnimation];
                                        foreach (var mod in sortedList) {
                                            if (mod.Item2.ToLower().Contains(modName.ToLower().Trim())) {
                                                _mediaManager.CleanNonStreamingSounds();
                                                if (!foundEmote) {
                                                    if (list.ContainsKey(foundAnimation)) {
                                                        foreach (var value in list[foundAnimation]) {
                                                            try {
                                                                string name = value.TextCommand.Value.Command.RawString.ToLower().Replace(" ", null).Replace("'", null);
                                                                if (!string.IsNullOrEmpty(name)) {
                                                                    if (!deDuplicate.Contains(value.ActionTimeline[0].Value.RowId)) {
                                                                        emoteData.Add(new
                                                                        EmoteModData(
                                                                        name,
                                                                        value.RowId,
                                                                        value.ActionTimeline[0].Value.RowId,
                                                                        modName));
                                                                        deDuplicate.Add(value.ActionTimeline[0].Value.RowId);
                                                                    }
                                                                    foundEmote = true;
                                                                    break;
                                                                }
                                                            } catch {
                                                            }
                                                        }
                                                    }
                                                }
                                            } else {
                                                // Thread.Sleep(100);
                                                var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection.Item3.Id, mod.Item2, false);
                                                if (config.DebugMode) {
                                                    Plugin.PluginLog.Debug(mod.Item2 + " was attempted to be disabled. The result was " + ipcResult + ".");
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            } else {
                                _chat.PrintError("Failed to trigger animation. The specified character has no assigned Penumbra collection!");
                            }
                        }
                    }
                }
                if (emoteData.Count > 0) {
                    if (emoteData.Count == 1) {
                        TriggerCharacterEmote(emoteData[0], targetObject);
                    } else if (emoteData.Count > 1) {
                        if (index == 0) {
                            _animationEmoteSelection.PopulateList(emoteData, targetObject);
                            _animationEmoteSelection.IsOpen = true;
                        } else {
                            TriggerCharacterEmote(emoteData[index - 1], targetObject);
                        }
                    }
                } else {
                    Task.Run(() => {
                        if (_failCount++ < 10) {
                            Thread.Sleep(3000);
                            DoAnimation(animationName, index, targetObject);
                        } else {
                            _failCount = 0;
                        }
                    });
                }
                _blockDataRefreshes = false;
            });
        }

        public void TriggerCharacterEmote(EmoteModData emoteModData, ICharacter character) {
            if (character == _clientState.LocalPlayer) {
                if (_wasDoingFakeEmote) {
                    _addonTalkHandler.StopEmote(_clientState.LocalPlayer.Address);
                }
            }
            PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(character.ObjectIndex, RedrawType.Redraw);
            Task.Run(() => {
                Thread.Sleep(1000);
                if (character == _clientState.LocalPlayer) {
                    _messageQueue.Enqueue(emoteModData.Emote);
                    if (!_animationModsAlreadyTriggered.Contains(emoteModData.FoundModName) && config.MoveSCDBasedModsToPerformanceSlider) {
                        Thread.Sleep(100);
                        _fastMessageQueue.Enqueue(emoteModData.Emote);
                        _animationModsAlreadyTriggered.Add(emoteModData.FoundModName);
                    }
                    _mediaManager.StopAudio(_playerObject);
                    Thread.Sleep(2000);
                } else {
                    _mediaManager.StopAudio(_playerObject);
                    Thread.Sleep(1000);
                }
                if (_objectRecentlyDidEmote) {
                    Thread.Sleep(1000);
                    _objectRecentlyDidEmote = false;
                } else {
                    _objectRecentlyDidEmote = true;
                }
                ushort value = _addonTalkHandler.GetCurrentEmoteId(character);
                if (!_didRealEmote) {
                    if (character == _clientState.LocalPlayer) {
                        _wasDoingFakeEmote = true;
                    }
                    OnEmote(character, (ushort)emoteModData.EmoteId);
                    _addonTalkHandler.TriggerEmote(character.Address, (ushort)emoteModData.AnimationId);
                    if (character.ObjectKind == ObjectKind.Companion) {
                        if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                            _preOccupiedWithEmoteCommand.Add(character.Name.TextValue);
                        }
                    }
                    unsafe {
                        var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                        if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)emoteModData.EmoteId);
                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)emoteModData.AnimationId);
                        } else {
                            _roleplayingMediaManager.SendShort(character.Name.TextValue + "emoteId", (ushort)emoteModData.EmoteId);
                            _roleplayingMediaManager.SendShort(character.Name.TextValue + "emote", (ushort)emoteModData.AnimationId);
                        }
                        Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                    }
                    Task.Run(() => {
                        Vector3 lastPosition = character.Position;
                        while (true) {
                            Thread.Sleep(500);
                            if (Vector3.Distance(lastPosition, character.Position) > 0.001f) {
                                _addonTalkHandler.StopEmote(character.Address);
                                _wasDoingFakeEmote = false;
                                unsafe {
                                    var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                    if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                        _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name + "MinionEmoteId", (ushort.MaxValue));
                                        _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name + "MinionEmote", (ushort.MaxValue));
                                    } else {
                                        _roleplayingMediaManager.SendShort(character.Name.TextValue + "emoteId", (ushort.MaxValue));
                                        _roleplayingMediaManager.SendShort(character.Name.TextValue + "emote", (ushort.MaxValue));
                                    }
                                    Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                }
                                if (character.ObjectKind == ObjectKind.Companion) {
                                    if (_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                        _preOccupiedWithEmoteCommand.Remove(character.Name.TextValue);
                                    }
                                }
                                break;
                            }
                        }
                    });
                }
                _didRealEmote = false;
            });
        }

        private IReadOnlyDictionary<string, IReadOnlyList<Emote>> CreateEmoteList(IDataManager gameData) {
            if (_emoteList == null) {
                var sheet = gameData.GetExcelSheet<Emote>()!;
                var storage = new ConcurrentDictionary<string, ConcurrentBag<Emote>>();

                void AddEmote(string? key, Emote emote) {
                    if (string.IsNullOrEmpty(key))
                        return;

                    key = key.ToLowerInvariant();
                    if (storage.TryGetValue(key, out var emotes))
                        emotes.Add(emote);
                    else
                        storage[key] = new ConcurrentBag<Emote> { emote };
                }

                var options = new ParallelOptions {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                };
                var seenTmbs = new ConcurrentDictionary<string, TmbFile>();

                void ProcessEmote(Emote emote) {
                    var emoteTmbs = new HashSet<string>(8);
                    var tmbs = new Queue<string>(8);

                    foreach (var timeline in emote.ActionTimeline.Where(t => t.Row != 0 && t.Value != null).Select(t => t.Value!)) {
                        var key = timeline.Key.ToDalamudString().TextValue;
                        AddEmote(Path.GetFileName(key) + ".pap", emote);
                    }

                    while (tmbs.TryDequeue(out var tmbPath)) {
                        if (!emoteTmbs.Add(tmbPath))
                            continue;
                        AddEmote(Path.GetFileName(tmbPath), emote);
                    }
                }

                Parallel.ForEach(sheet.Where(n => n.Name.RawData.Length > 0), options, ProcessEmote);

                var sit = sheet.GetRow(50)!;
                AddEmote("s_pose01_loop.pap", sit);
                AddEmote("s_pose02_loop.pap", sit);
                AddEmote("s_pose03_loop.pap", sit);
                AddEmote("s_pose04_loop.pap", sit);
                AddEmote("s_pose05_loop.pap", sit);

                var sitOnGround = sheet.GetRow(52)!;
                AddEmote("j_pose01_loop.pap", sitOnGround);
                AddEmote("j_pose02_loop.pap", sitOnGround);
                AddEmote("j_pose03_loop.pap", sitOnGround);
                AddEmote("j_pose04_loop.pap", sitOnGround);

                var doze = sheet.GetRow(13)!;
                AddEmote("l_pose01_loop.pap", doze);
                AddEmote("l_pose02_loop.pap", doze);
                AddEmote("l_pose03_loop.pap", doze);

                _emoteList = storage.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Emote>)kvp.Value.Distinct().ToArray());
            }
            return _emoteList;
        }

        #endregion
        #region Error Logging
        private void Window_OnWindowOperationFailed(object sender, PluginWindow.MessageEventArgs e) {
            _chat?.PrintError("[Artemis Roleplaying Kit] " + e.Message);
            Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + e.Message);
        }
        private void Window_OnMoveFailed(object sender, EventArgs e) {
            _chat?.PrintError("[Artemis Roleplaying Kit] Cache swap failed, this is not a valid cache folder. Please select an empty folder that does not require administrator rights.");
        }
        private void _mediaManager_OnErrorReceived(object sender, MediaError e) {
            Plugin.PluginLog?.Warning(e.Exception, e.Exception.Message);
        }
        #endregion
        #region IDisposable Support
        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            try {
                disposed = true;
                config.Save();
                config.OnConfigurationChanged -= Config_OnConfigurationChanged;
                IpcSystem.Dispose();
                _chat.ChatMessage -= Chat_ChatMessage;
                this.pluginInterface.UiBuilder.Draw -= UiBuilder_Draw;
                this.pluginInterface.UiBuilder.OpenConfigUi -= UiBuilder_OpenConfigUi;
                this.windowSystem.RemoveAllWindows();
                this.commandManager?.Dispose();
                if (_filter != null) {
                    _filter.OnSoundIntercepted -= _filter_OnSoundIntercepted;
                }
                try {
                    if (_mediaManager != null) {
                        _mediaManager.OnErrorReceived -= _mediaManager_OnErrorReceived;
                        _mediaManager?.Dispose();
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
                try {
                    _clientState.Login -= _clientState_Login;
                    _clientState.Logout -= _clientState_Logout;
                    _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
                    _clientState.LeavePvP -= _clientState_LeavePvP;
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
                try {
                    _toast.ErrorToast -= _toast_ErrorToast;
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
                try {
                    _framework.Update -= framework_Update;
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e, e.Message);
                }
                _networkedClient?.Dispose();
                Filter?.Dispose();
                if (_emoteReaderHook != null) {
                    if (_emoteReaderHook.OnEmote != null) {
                        _emoteReaderHook.OnEmote -= (instigator, emoteId) => OnEmote(instigator as ICharacter, emoteId);
                    }
                }
                CleanupEmoteWatchList();
                _addonTalkHandler?.Dispose();
                //PenumbraAndGlamourerIPCWrapper.Instance.ModSettingChanged.Event -= modSettingChanged;
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
