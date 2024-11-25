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
using Lumina.Excel.Sheets;
using RoleplayingVoiceDalamud.VoiceSorting;
using SixLabors.ImageSharp.Drawing;
using Path = System.IO.Path;
//using Anamnesis.GameData.Excel;
//using Lumina.Excel.GeneratedSheets2;
#endregion
namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Fields
        private int performanceLimiter;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IChatGui _chat;
        private readonly IClientState _clientState;
        private IDataManager _dataManager;
        private IToastGui _toast;
        private IGameConfig _gameConfig;
        private ISigScanner _sigScanner;
        private IGameInteropProvider _interopProvider;
        private IObjectTable _objectTableThreadUnsafe;
        private IFramework _framework;

        private readonly PluginCommandManager<Plugin> commandManager;
        private readonly Configuration config;
        private readonly WindowSystem windowSystem;
        public PluginWindow Window { get; init; }
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
        private VoiceEditor _voiceEditor;
        private static IPluginLog _plugin;
        private static RoleplayingMediaManager _roleplayingMediaManager;

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
        private List<ArtemisVoiceMod> penumbraSoundPacks;
        private unsafe Camera* _camera;
        private MediaCameraObject _playerCamera;

        public EventHandler OnMuteTimerOver;

        ConcurrentDictionary<string, MovingObject> gameObjectPositions = new ConcurrentDictionary<string, MovingObject>();
        Queue<string> temporaryWhitelistQueue = new Queue<string>();
        List<string> temporaryWhitelist = new List<string>();
        private ArtemisVoiceMod combinedSoundList;

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
        private GameObject[] _objectTable;
        private bool _checkingMovementInProgress;

        public string Name => "Artemis Roleplaying Kit";

        public static RoleplayingMediaManager RoleplayingMediaManager { get => _roleplayingMediaManager; set => _roleplayingMediaManager = value; }
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

        public IClientState ClientState => _clientState;

        public IDataManager DataManager1 { get => _dataManager; set => _dataManager = value; }
        public unsafe Camera* Camera { get => _camera; set => _camera = value; }
        public IGameGui GameGui { get => _gameGui; set => _gameGui = value; }
        public List<string> ModelModList { get => _modelModList; set => _modelModList = value; }
        public ConcurrentDictionary<string, List<string>> ModelMods { get => _modelMods; set => _modelMods = value; }
        public ConcurrentDictionary<string, List<string>> ModelDependancyMods { get => _modelDependancyMods; set => _modelDependancyMods = value; }
        public static bool BlockDataRefreshes { get => _blockDataRefreshes; set => _blockDataRefreshes = value; }
        public RedoLineWindow RedoLineWindow { get => _redoLineWindow; set => _redoLineWindow = value; }
        public MediaCameraObject PlayerCamera { get => _playerCamera; set => _playerCamera = value; }
        public GameObject[] ObjectTable { get => _objectTable; set => _objectTable = value; }
        public static bool Disposed { get; internal set; }
        public VoiceEditor VoiceEditor { get => _voiceEditor; set => _voiceEditor = value; }
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
                Window = this.pluginInterface.Create<PluginWindow>();
                _videoWindow = this.pluginInterface.Create<VideoWindow>();
                _catalogueWindow = this.pluginInterface.Create<CatalogueWindow>();
                _redoLineWindow = this.pluginInterface.Create<RedoLineWindow>();
                _gposeWindow = this.pluginInterface.Create<GposeWindow>();
                _gposePhotoTakerWindow = this.pluginInterface.Create<GposePhotoTakerWindow>();
                _animationCatalogue = this.pluginInterface.Create<AnimationCatalogue>();
                _animationEmoteSelection = this.pluginInterface.Create<AnimationEmoteSelection>();
                _npcPersonalityWindow = this.pluginInterface.Create<NPCPersonalityWindow>();
                _dragAndDropTextures = this.pluginInterface.Create<DragAndDropTextureWindow>();
                _voiceEditor = this.pluginInterface.Create<VoiceEditor>();
                _gposePhotoTakerWindow.GposeWindow = _gposeWindow;
                _npcPersonalityWindow.Plugin = this;
                pluginInterface.UiBuilder.DisableAutomaticUiHide = true;
                pluginInterface.UiBuilder.DisableGposeUiHide = true;
                Window.ClientState = this._clientState;
                Window.PluginReference = this;
                Window.PluginInterface = this.pluginInterface;
                Window.Configuration = this.config;
                _gposeWindow.Plugin = this;
                _animationCatalogue.Plugin = this;
                _animationEmoteSelection.Plugin = this;
                _targetManager = targetManager;
                if (Window is not null) {
                    this.windowSystem.AddWindow(Window);
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
                if (_voiceEditor is not null) {
                    this.windowSystem.AddWindow(_voiceEditor);
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
                _objectTableThreadUnsafe = objectTable;
                _framework = framework;
                _framework.Update += framework_Update;
                NPCVoiceMapping.Initialize();
                Task.Run(async () => {
                    _npcVoiceManager = new NPCVoiceManager(await NPCVoiceMapping.GetVoiceMappings(), await NPCVoiceMapping.GetCharacterToCacheType(),
                        config.CacheFolder, "7fe29e49-2d45-423d-8efc-d8e2c1ceaf6d", false);
                    _voiceEditor.NPCVoiceManager = _npcVoiceManager;
                    _addonTalkManager = new AddonTalkManager(_framework, _clientState, condition, gameGui);
                    _addonTalkHandler = new AddonTalkHandler(_addonTalkManager, _framework, _objectTableThreadUnsafe, clientState, this, chat, scanner, _redoLineWindow, _toast);
                    _ipcSystem = new IpcSystem(pluginInterface, _addonTalkHandler, this);
                    _gameGui = gameGui;
                    _dragDrop = dragDrop;
                    //_npcVoiceManager.UseCustomRelayServer = config.UseCustomDialogueRelayServer;
                    //_npcVoiceManager.CustomRelayServer = config.CustomDialogueRelayServerIp;
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
                Window.RequestingReconnect += Window_RequestingReconnect;
                Window.OnMoveFailed += Window_OnMoveFailed;
                config.OnConfigurationChanged += Config_OnConfigurationChanged;
                _emoteReaderHook = new EmoteReaderHooks(_interopProvider, _clientState, _objectTableThreadUnsafe);
                _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as ICharacter, emoteId);
                _realChat = new Chat(_sigScanner);
                RaceVoice.LoadRacialVoiceInfo();
                CheckDependancies();
                Filter = new Filter(this);
                Filter.Enable();
                Filter.OnSoundIntercepted += _filter_OnSoundIntercepted;
                _chat.ChatMessage += Chat_ChatMessage;
                _clientState.Login += _clientState_Login;
                _clientState.Logout += _clientState_Logout; ;
                _clientState.TerritoryChanged += _clientState_TerritoryChanged;
                _clientState.LeavePvP += _clientState_LeavePvP;
                _clientState.CfPop += _clientState_CfPop;
                Window.OnWindowOperationFailed += Window_OnWindowOperationFailed;
                _catalogueWindow.Plugin = this;
                if (_clientState.IsLoggedIn) {
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

        private void _clientState_CfPop(Lumina.Excel.Sheets.ContentFinderCondition obj) {
            _recentCFPop = 1;
        }
        #endregion Plugin Initiialization


        #region UI Management
        private void UiBuilder_Draw() {
            this.windowSystem.Draw();
        }
        private void UiBuilder_OpenConfigUi() {
            Window?.RefreshVoices();
            Window?.Toggle();
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
                            _chat?.Print("on (Enable Player TTS Voice)\r\n" +
                             "off (Disable Player TTS Voice)\r\n" +
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
                             "accessibilitymode (Toggles accessibility mode)\r\n" +
                             "textadvance (Toggles automatic text advancement when accessibility mode finishes)\r\n" +
                             "clearsound (Stops all currently playing sounds, and clears out the sound cache for other players)");
                            break;
                        case "on":
                            config.AiVoiceActive = true;
                            Window.Configuration = config;
                            this.pluginInterface.SavePluginConfig(config);
                            config.AiVoiceActive = true;
                            break;
                        case "off":
                            config.AiVoiceActive = false;
                            Window.Configuration = config;
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
                            _mediaManager.StopStream();
                            ResetTwitchValues();
                            potentialStream = "";
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
                        case "accessibilitymode":
                            config.NpcSpeechIsOn = !config.NpcSpeechIsOn;
                            if (StreamDetection.RecordingSoftwareIsActive) {
                                _chat?.PrintError("Please close " + StreamDetection.LastProcess.ProcessName + ". It is interfering with accessibility mode.");
                            } else {
                                if (config.NpcSpeechIsOn) {
                                    _chat?.Print("Accessibility Mode Enabled");
                                } else {
                                    _chat?.Print("Accessibility Mode Disabled");
                                }
                            }
                            Window.NpcSpeechEnabled = config.NpcSpeechIsOn;
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
                                Window.RefreshVoices();
                            }
                            Window.Toggle();
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
                if (!string.IsNullOrEmpty(emoteItem.TextCommand.Value.Command.ToString())) {
                    if ((
                        emoteItem.TextCommand.Value.ShortCommand.ToString().Contains(command) ||
                        emoteItem.TextCommand.Value.Command.ToString().Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.ToString().Contains(command)) {
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
                    if (!string.IsNullOrEmpty(emoteItem.TextCommand.Value.Command.ToString())) {
                        if ((
                        emoteItem.TextCommand.Value.ShortCommand.ToString().Contains(command) ||
                        emoteItem.TextCommand.Value.Command.ToString().Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.ToString().Contains(command)) {
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
                                        //_animationCatalogue.AddNewItem(key);
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
                _mediaManager?.CleanNonStreamingSounds();
                // for (int i = 0; i < 20 && emoteData.Count == 0; i++) {
                foreach (var modName in _animationMods.Keys) {
                    if (modName.ToLower().Contains(commandArguments)) {
                        if (collection.Item3.Name != "None") {
                            var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection.Item3.Id, modName, true);
                            var result2 = PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection.Item3.Id, modName, 10);
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
                                            if (!foundEmote) {
                                                if (list.ContainsKey(foundAnimation)) {
                                                    foreach (var value in list[foundAnimation]) {
                                                        try {
                                                            string name = value.TextCommand.Value.Command.ToString().ToLower().Replace(" ", null).Replace("'", null);
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
                                            var ipcResult2 = PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection.Item3.Id, mod.Item2, -10);
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
                //}
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

                    foreach (var timeline in emote.ActionTimeline.Where(t => t.RowId != 0).Select(t => t.Value!)) {
                        var key = timeline.Key.ToDalamudString().TextValue;
                        AddEmote(Path.GetFileName(key) + ".pap", emote);
                    }

                    while (tmbs.TryDequeue(out var tmbPath)) {
                        if (!emoteTmbs.Add(tmbPath))
                            continue;
                        AddEmote(Path.GetFileName(tmbPath), emote);
                    }
                }

                Parallel.ForEach(sheet.Where(n => n.Name.Data.Length > 0), options, ProcessEmote);

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
            try {
                disposed = true;
                Disposed = true;
                _mediaManager.Invalidated = true;
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
