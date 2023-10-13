using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVLooseTextureCompiler.Networking;
using FFXIVVoicePackCreator;
using FFXIVVoicePackCreator.VoiceSorting;
using Lumina.Excel.GeneratedSheets;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Housing;
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

namespace RoleplayingVoice {
    public class Plugin : IDalamudPlugin {
        private readonly DalamudPluginInterface pluginInterface;
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
        private RoleplayingMediaManager _roleplayingMediaManager;

        private Stopwatch stopwatch;
        private Stopwatch cooldown;
        private Stopwatch muteTimer;
        private Stopwatch twitchSetCooldown = new Stopwatch();
        private Stopwatch maxDownloadLengthTimer = new Stopwatch();
        private Stopwatch redrawCooldown = new Stopwatch();
        private Stopwatch _nativeSoundExpiryTimer = new Stopwatch();

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

        private int muteLength;
        private int attackCount;
        private int castingCount;
        private int objectsRedrawn;
        private int redrawObjectCount;
        private bool staging;
        private bool isDownloadingZip;
        private bool ignoreAttack;
        private bool disposed;
        private bool voiceMuted;
        private bool twitchWasPlaying;
        private bool _inGameSoundStartedAudio;

        private string lastPrintedWarning;
        private string stagingPath;
        private string potentialStream;
        private string lastStreamURL;
        private string currentStreamer;

        private Queue<string> messageQueue = new Queue<string>();
        private Stopwatch messageTimer = new Stopwatch();
        private Dictionary<string, string> _scdReplacements = new Dictionary<string, string>();
        private Dictionary<string, IGameObject> _loopEarlyQueue = new Dictionary<string, IGameObject>();
        private WaveStream _nativeAudioStream;
        private string _lastSoundPath;

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

        public unsafe Plugin(
            DalamudPluginInterface pi,
            ICommandManager commands,
            IChatGui chat,
            IClientState clientState,
            ISigScanner scanner,
            IObjectTable objectTable,
            IToastGui toast,
            IDataManager dataManager,
            IGameConfig gameConfig,
            IFramework framework,
            IGameInteropProvider interopProvider) {
            try {
                this.pluginInterface = pi;
                this._chat = chat;
                this._clientState = clientState;
                // Get or create a configuration object
                //this.config = (Configuration)this.pluginInterface.GetPluginConfig()
                //          ?? this.pluginInterface.Create<Configuration>();

                try {
                    this.config = GetConfig();
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }

                // Initialize the UI
                this.windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);
                _window = this.pluginInterface.Create<PluginWindow>();
                _videoWindow = this.pluginInterface.Create<VideoWindow>();
                _window.ClientState = this._clientState;
                _window.Configuration = this.config;
                _window.PluginInterface = this.pluginInterface;
                _window.PluginReference = this;
                AttemptConnection();
                if (config.ApiKey != null) {
                    InitialzeManager();
                }

                if (_window is not null) {
                    this.windowSystem.AddWindow(_window);
                }
                if (_videoWindow is not null) {
                    this.windowSystem.AddWindow(_videoWindow);
                }
                _window.RequestingReconnect += Window_RequestingReconnect;
                _window.OnMoveFailed += Window_OnMoveFailed;
                this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;
                this.pluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

                // Load all of our commands
                this.commandManager = new PluginCommandManager<Plugin>(this, commands);
                config.OnConfigurationChanged += Config_OnConfigurationChanged;
                _window.Toggle();
                _videoWindow.Toggle();
                _window.PluginReference = this;
                _emoteReaderHook = new EmoteReaderHooks(interopProvider, clientState, objectTable);
                _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
                _dataManager = dataManager;
                _toast = toast;
                _toast.ErrorToast += _toast_ErrorToast;
                _gameConfig = gameConfig;
                _sigScanner = scanner;
                _interopProvider = interopProvider;
                _realChat = new Chat(_sigScanner);
                this._chat.ChatMessage += Chat_ChatMessage;
                cooldown = new Stopwatch();
                muteTimer = new Stopwatch();
                _objectTable = objectTable;
                _clientState.Login += _clientState_Login;
                _clientState.Logout += _clientState_Logout;
                _clientState.TerritoryChanged += _clientState_TerritoryChanged;
                _clientState.LeavePvP += _clientState_LeavePvP;
                _window.OnWindowOperationFailed += Window_OnWindowOperationFailed;
                RaceVoice.LoadRacialVoiceInfo();
                CheckDependancies();
                Filter = new Filter(this);
                Filter.Enable();
                _framework = framework;
                _framework.Update += framework_Update;
                RefreshSoundData();
                Ipc.ModSettingChanged.Subscriber(pluginInterface).Event += modSettingChanged;
                Ipc.GameObjectRedrawn.Subscriber(pluginInterface).Event += gameObjectRedrawn;
                _filter.OnSoundIntercepted += _filter_OnSoundIntercepted;
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
                _chat.PrintError("[Artemis Roleplaying Kit] Fatal Error, the plugin did not initialize correctly!");
            }
        }

        private Configuration GetConfig() {
            string currentConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                               + @"\XIVLauncher\pluginConfigs\RoleplayingVoiceDalamud.json";
            if (File.Exists(currentConfig)) {
                return JsonConvert.DeserializeObject<Configuration>(
                    File.OpenText(currentConfig).ReadToEnd());
            }
            return new Configuration(this.pluginInterface);
        }

        private void _filter_OnSoundIntercepted(object sender, InterceptedSound e) {
            if (config.MoveSCDBasedModsToPerformanceSlider) {
                ScdFile scdFile = null;
                if (_scdReplacements.ContainsKey(e.SoundPath)) {
                    if (!e.SoundPath.Contains("vo_emote") && !e.SoundPath.Contains("vo_battle")) {
                        Dalamud.Logging.PluginLog.Log("Sound Mod Intercepted");
                        scdFile = GetScdFile(e.SoundPath);
                        int i = 0;
                        try {
                            if (_loopEarlyQueue.ContainsKey(e.SoundPath)) {
                                _mediaManager.LoopEarly(_loopEarlyQueue[e.SoundPath]);
                                // _loopEarlyQueue.Remove(e.SoundPath);
                            }
                            _lastSoundPath = e.SoundPath;
                            QueueSCDTrigger(scdFile);
                        } catch (Exception ex) {
                            Dalamud.Logging.PluginLog.LogError(ex, ex.Message);
                        }
                    }
                } else {
                    if (_loopEarlyQueue.ContainsKey(e.SoundPath)) {
                        _mediaManager.LoopEarly(_loopEarlyQueue[e.SoundPath]);
                        // _loopEarlyQueue.Remove(e.SoundPath);
                    }
                    _lastSoundPath = e.SoundPath;
                }
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
        private ScdFile GetScdFile(string soundPath) {
            using (FileStream fileStream = new FileStream(_scdReplacements[soundPath], FileMode.Open, FileAccess.Read)) {
                using (BinaryReader reader = new BinaryReader(fileStream)) {
                    return new ScdFile(reader);
                }
            }
        }

        private void Window_OnMoveFailed(object sender, EventArgs e) {
            _chat.PrintError("[Artemis Roleplaying Kit] Cache swap failed, this is not a valid cache folder. Please select an empty folder that does not require administrator rights.");
        }

        private void gameObjectRedrawn(nint arg1, int arg2) {
            if (!redrawCooldown.IsRunning) {
                redrawCooldown.Start();
                redrawObjectCount = _objectTable.Count<GameObject>();
            }
            if (redrawCooldown.IsRunning) {
                objectsRedrawn++;
            }
            string senderName = CleanSenderName(_objectTable[arg2].Name.TextValue);
            string path = config.CacheFolder + @"\VoicePack\Others";
            string hash = RoleplayingMediaManager.Shai1Hash(senderName);
            string clipPath = path + @"\" + hash;
            if (CombinedWhitelist().Contains(senderName) &&
                !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                if (Directory.Exists(clipPath)) {
                    try {
                        RemoveFiles(clipPath);
                    } catch (Exception e) {
                        Dalamud.Logging.PluginLog.LogError(e, e.Message);
                    }
                }
            } else if (!temporaryWhitelist.Contains(senderName) && config.IgnoreWhitelist &&
                !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                temporaryWhitelistQueue.Enqueue(senderName);
            } else if (_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                RefreshSoundData();
            }
        }

        List<string> CombinedWhitelist() {
            List<string> list = new List<string>();
            list.AddRange(config.Whitelist);
            list.AddRange(temporaryWhitelist);
            return list;
        }
        private void framework_Update(IFramework framework) {
            if (config != null && _mediaManager != null && _objectTable != null && _gameConfig != null && !disposed) {
                uint voiceVolume = 0;
                uint masterVolume = 0;
                uint soundEffectVolume = 0;
                try {
                    if (_gameConfig.TryGet(SystemConfigOption.SoundVoice, out voiceVolume)) {
                        if (_gameConfig.TryGet(SystemConfigOption.SoundMaster, out masterVolume)) {
                            _mediaManager.MainPlayerVolume = config.PlayerCharacterVolume *
                                ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                            _mediaManager.OtherPlayerVolume = config.OtherCharacterVolume *
                                ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                            _mediaManager.UnfocusedPlayerVolume = config.UnfocusedCharacterVolume *
                                ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                            if (_gameConfig.TryGet(SystemConfigOption.SoundPerform, out soundEffectVolume)) {
                                _mediaManager.SFXVolume = config.LoopingSFXVolume *
                                 ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                                _mediaManager.LiveStreamVolume = config.LivestreamVolume *
                                 ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                            }
                        }
                        if (muteTimer.ElapsedMilliseconds > muteLength) {
                            if (Filter != null) {
                                lock (Filter) {
                                    Filter.Muted = voiceMuted = false;
                                    RefreshPlayerVoiceMuted();
                                    muteTimer.Stop();
                                    muteTimer.Reset();
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }
                if (redrawCooldown.ElapsedMilliseconds > 100) {
                    if (temporaryWhitelistQueue.Count < redrawObjectCount - 1) {
                        foreach (var item in temporaryWhitelistQueue) {
                            temporaryWhitelist.Add(item);
                        }
                        temporaryWhitelistQueue.Clear();
                    }
                    redrawCooldown.Stop();
                    redrawCooldown.Reset();
                }
            }
            try {
                foreach (GameObject gameObject in _objectTable) {
                    string cleanedName = CleanSenderName(gameObject.Name.TextValue);
                    if (!string.IsNullOrEmpty(cleanedName)) {
                        if (gameObjectPositions.ContainsKey(cleanedName)) {
                            var positionData = gameObjectPositions[cleanedName];
                            if (Vector3.Distance(positionData.LastPosition, gameObject.Position) > 0.01f ||
                                positionData.LastRotation != gameObject.Rotation) {
                                if (!positionData.IsMoving) {
                                    ObjectIsMoving(cleanedName, gameObject);
                                    positionData.IsMoving = true;
                                }
                            } else {
                                positionData.IsMoving = false;
                            }
                            positionData.LastPosition = gameObject.Position;
                            positionData.LastRotation = gameObject.Rotation;
                        } else {
                            gameObjectPositions[cleanedName] = new MovingObject(gameObject.Position, gameObject.Rotation, false);
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
            try {
                if (messageQueue.Count > 0) {
                    if (!messageTimer.IsRunning) {
                        messageTimer.Start();
                    } else {
                        if (messageTimer.ElapsedMilliseconds > 3000) {
                            _realChat.SendMessage(messageQueue.Dequeue());
                            messageTimer.Restart();
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
            if (maxDownloadLengthTimer.ElapsedMilliseconds > 30000) {
                isDownloadingZip = false;
                maxDownloadLengthTimer.Reset();
            }
        }

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
                _chat.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
            }
            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (CombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            if (!Path.Exists(clipPath)) {
                                isDownloadingZip = true;
                                maxDownloadLengthTimer.Restart();
                                await Task.Run(async () => {
                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                    isDownloadingZip = false;
                                });
                            }
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                            bool isVoicedEmote = false;
                            string value = characterVoicePack.GetMisc("moving");
                            if (!string.IsNullOrEmpty(value)) {
                                _mediaManager.PlayAudio(new MediaGameObject(gameObject), value, SoundType.LoopWhileMoving, 0);
                                if (isVoicedEmote) {
                                    MuteVoiceChecK(4000);
                                }
                            } else {
                                _mediaManager.StopAudio(new MediaGameObject(gameObject));
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
        }

        private void SendingMovement(string playerName, GameObject gameObject) {
            if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                string path = config.CacheFolder + @"\VoicePack\" + voice;
                string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                bool isVoicedEmote = false;
                string value = characterVoicePack.GetMisc("moving");
                if (!string.IsNullOrEmpty(value)) {
                    if (config.UsePlayerSync) {
                        Task.Run(async () => {
                            bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                        });
                    }
                    _mediaManager.PlayAudio(_playerObject, value, SoundType.LoopWhileMoving, 0);
                } else {
                    _mediaManager.StopAudio(_playerObject);
                }
            }
        }

        private void RefreshPlayerVoiceMuted() {
            if (voiceMuted) {
                _gameConfig.Set(SystemConfigOption.IsSndVoice, true);
            } else {
                _gameConfig.Set(SystemConfigOption.IsSndVoice, false);
            }
        }
        private void modSettingChanged(Penumbra.Api.Enums.ModSettingChange arg1, string arg2, string arg3, bool arg4) {
            RefreshSoundData();
        }
        public async void RefreshSoundData() {
            _ = Task.Run(async () => {
                try {
                    penumbraSoundPacks = await GetPrioritySortedSoundPacks();
                    combinedSoundList = await GetCombinedSoundList(penumbraSoundPacks);
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.Error(e.Message);
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
                    Dalamud.Logging.PluginLog.Error(e.Message);
                }
            }
            WriteStreamingPath();
        }

        private void WriteStreamingPath() {
            if (!string.IsNullOrEmpty(config.StreamPath)) {
                var writer = File.CreateText(Path.Combine(stagingPath, "streaming.strm"));
                writer.WriteLine(config.StreamPath);
                writer.Flush();
                writer.Close();
                writer.Dispose();
            }
        }
        private string GetStreamingPath(string directory) {
            string path = Path.Combine(directory, "streaming.strm");
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                var writer = File.OpenText(path);
                return writer.ReadToEnd();
            }
            return "";
        }

        public async Task<List<string>> GetCombinedSoundList(List<KeyValuePair<List<string>, int>> sounds) {
            List<string> list = new List<string>();
            Dictionary<string, bool> keyValuePairs = new Dictionary<string, bool>();
            foreach (var sound in sounds) {
                foreach (string value in sound.Key) {
                    string strippedValue = CharacterVoicePack.StripNonCharacters(Path.GetFileNameWithoutExtension(value));
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
                    keyValuePairs[value] = true;
                }
            }
            _ = Task.Run(async () => {
                if (list != null) {
                    while (staging) {
                        Thread.Sleep(1000);
                    }
                    staging = true;
                    stagingPath = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                    if (Directory.Exists(config.CacheFolder + @"\Staging")) {
                        foreach (string file in Directory.EnumerateFiles(config.CacheFolder + @"\Staging")) {
                            try {
                                File.Delete(file);
                            } catch (Exception e) {
                                //Dalamud.Logging.PluginLog.LogError(e, e.Message);
                            }
                        }
                    }
                    if (Directory.Exists(config.CacheFolder)) {
                        foreach (string file in Directory.EnumerateFiles(config.CacheFolder)) {
                            try {
                                if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                                    File.Delete(file);
                                } else {
                                    _chat.PrintError("[Artemis Roleplaying Kit]" + file + " should not be in the cache folder, please remove it.");
                                }
                            } catch (Exception e) {
                                Dalamud.Logging.PluginLog.LogError(e, e.Message);
                            }
                        }
                    }
                    try {
                        Directory.CreateDirectory(stagingPath);
                    } catch {
                        _chat.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
                    }
                    if (Directory.Exists(stagingPath)) {
                        foreach (string file in Directory.EnumerateFiles(stagingPath)) {
                            try {
                                File.Delete(file);
                            } catch (Exception e) {
                                Dalamud.Logging.PluginLog.LogWarning(e, e.Message);
                            }
                        }
                    }
                    foreach (var sound in list) {
                        try {
                            File.Copy(sound, Path.Combine(stagingPath, Path.GetFileName(sound)), true);
                        } catch (Exception e) {
                            Dalamud.Logging.PluginLog.LogError(e, e.Message);
                        }
                    }
                    staging = false;
                }
            });
            return list;
        }
        private void Window_OnWindowOperationFailed(object sender, PluginWindow.MessageEventArgs e) {
            _chat.PrintError("[Artemis Roleplaying Kit] " + e.Message);
            Dalamud.Logging.PluginLog.LogError("[Artemis Roleplaying Kit] " + e.Message);
        }

        private void _toast_ErrorToast(ref SeString message, ref bool isHandled) {
            if (config.VoicePackIsActive) {
                if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                    if (!cooldown.IsRunning || cooldown.ElapsedMilliseconds > 3000) {
                        string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                        string path = config.CacheFolder + @"\VoicePack\" + voice;
                        CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                        string value = characterVoicePack.GetMisc(message.TextValue);
                        if (!string.IsNullOrEmpty(value)) {
                            _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
                        }
                    }
                    cooldown.Restart();
                }
            }
        }

        private void _clientState_LeavePvP() {
            CleanSounds();
        }

        private unsafe void _clientState_TerritoryChanged(ushort e) {
            CleanSounds();
        }
        private unsafe bool IsResidential() {
            return HousingManager.Instance()->IsInside() || HousingManager.Instance()->HousingOutdoorTerritory != null;
        }

        private void _clientState_Logout() {
            CleanSounds();
        }

        private void _clientState_Login() {
            CleanSounds();
            CheckDependancies(true);
            RefreshSoundData();
        }
        public void CleanSounds() {
            string path = config.CacheFolder + @"\VoicePack\Others";
            if (_mediaManager != null) {
                _mediaManager.CleanSounds();
            }
            ResetTwitchValues();
            temporaryWhitelist.Clear();
            if (Directory.Exists(path)) {
                try {
                    Directory.Delete(path, true);
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }
            }
        }
        public void ResetTwitchValues() {
            if (twitchWasPlaying) {
                twitchWasPlaying = false;
                _videoWindow.IsOpen = false;
                _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
            }
            potentialStream = "";
            lastStreamURL = "";
            currentStreamer = "";
            twitchSetCooldown.Stop();
            twitchSetCooldown.Reset();
        }
        private void Window_RequestingReconnect(object sender, EventArgs e) {
            AttemptConnection();
        }

        private void AttemptConnection() {
            if (_networkedClient != null) {
                _networkedClient.Dispose();
            }
            if (config != null) {
                _networkedClient = new NetworkedClient(config.ConnectionIP);
                if (_roleplayingMediaManager != null) {
                    _roleplayingMediaManager.NetworkedClient = _networkedClient;
                }
            }
        }

        private void UiBuilder_OpenConfigUi() {
            _window.RefreshVoices();
            _window.Toggle();
        }
        public void OnEmote(PlayerCharacter instigator, ushort emoteId) {
            if (!disposed) {
                if (instigator.Name.TextValue == _clientState.LocalPlayer.Name.TextValue) {
                    if (config.VoicePackIsActive) {
                        SendingEmote(instigator, emoteId);
                    }
                } else {
                    Task.Run(() => ReceivingEmote(instigator, emoteId));
                }
            }
        }
        public void CheckForValidSCD(PlayerCharacter instigator, string emote = "", string stagingPath = "", bool isSending = false) {
            var mediaObject = new MediaGameObject(instigator);
            if (!_inGameSoundStartedAudio) {
                _mediaManager.StopAudio(mediaObject);
            } else {
                if (_nativeAudioStream != null) {
                    if (isSending) {
                        Dalamud.Logging.PluginLog.Log("Emote Trigger Detected");
                        if (!string.IsNullOrEmpty(_lastSoundPath)) {
                            using (FileStream fileStream = new FileStream(stagingPath + @"\" + emote + ".mp3", FileMode.Create, FileAccess.Write)) {
                                _nativeAudioStream.Position = 0;
                                MediaFoundationEncoder.EncodeToMp3(_nativeAudioStream, fileStream);
                            }
                            _nativeAudioStream.Position = 0;
                            _mediaManager.PlayAudioStream(mediaObject, _nativeAudioStream, RoleplayingMediaCore.SoundType.Loop);
                            _loopEarlyQueue[_lastSoundPath] = mediaObject;
                            _lastSoundPath = null;
                        } else {
                            Dalamud.Logging.PluginLog.LogWarning("Its been too long to associate " + emote + " to recent sound playback. It has been " + _nativeSoundExpiryTimer.Elapsed.TotalMilliseconds + " milliseconds");
                        }
                        _nativeSoundExpiryTimer.Reset();
                        _nativeAudioStream = null;
                        _inGameSoundStartedAudio = false;
                    } else {
                        Dalamud.Logging.PluginLog.LogWarning("Not currently sending");
                    }
                } else {
                    Dalamud.Logging.PluginLog.LogWarning("There is no available audio stream to play");
                }
            }
        }
        public void RemoveFiles(string path) {
            try {
                Directory.Delete(path, true);
            } catch {
                foreach (string file in Directory.EnumerateFiles(path)) {
                    try {
                        File.Delete(file);
                    } catch (Exception e) {
                        Dalamud.Logging.PluginLog.LogError(e, e.Message);
                    }
                }
            }
        }
        private async void ReceivingEmote(PlayerCharacter instigator, ushort emoteId) {
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
                            _chat.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                        }
                        string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
                        string clipPath = path + @"\" + hash;
                        try {
                            if (config.UsePlayerSync) {
                                if (CombinedWhitelist().Contains(playerSender)) {
                                    if (!isDownloadingZip) {
                                        if (!Path.Exists(clipPath) || !File.Exists(clipPath + @"\" + GetEmoteName(emoteId) + ".mp3")) {
                                            if (Path.Exists(clipPath)) {
                                                RemoveFiles(clipPath);
                                                isDownloadingZip = true;
                                                maxDownloadLengthTimer.Restart();
                                                await Task.Run(async () => {
                                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                                    isDownloadingZip = false;
                                                });
                                            }
                                        }
                                        await Task.Run(async () => {
                                            while (isDownloadingZip) {
                                                Thread.Sleep(100);
                                            }
                                            if (Directory.Exists(path)) {
                                                CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                                                bool isVoicedEmote = false;
                                                string value = GetEmotePath(characterVoicePack, emoteId, out isVoicedEmote);
                                                if (!string.IsNullOrEmpty(value)) {
                                                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                                                    TimeCodeData data = RaceVoice.TimeCodeData[instigator.Customize[(int)CustomizeIndex.Race] + "_" + gender];
                                                    _mediaManager.PlayAudio(new MediaGameObject(instigator), value, SoundType.OtherPlayer,
                                                     characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0);
                                                    if (isVoicedEmote) {
                                                        MuteVoiceChecK(4000);
                                                    }
                                                } else {
                                                    CheckForValidSCD(instigator, value);
                                                }
                                                if (!string.IsNullOrEmpty(_lastSoundPath)) {
                                                    _loopEarlyQueue[_lastSoundPath] = new MediaGameObject(instigator);
                                                    _lastSoundPath = null;
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Dalamud.Logging.PluginLog.LogError("[Artemis Roleplaying Kit] " + e.Message);
                        }
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError("[Artemis Roleplaying Kit] " + e.Message);
                }
            }
        }
        private void SendingEmote(PlayerCharacter instigator, ushort emoteId) {
            if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                string path = config.CacheFolder + @"\VoicePack\" + voice;
                string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                bool isVoicedEmote = false;
                string value = GetEmotePath(characterVoicePack, emoteId, out isVoicedEmote);
                if (!string.IsNullOrEmpty(value)) {
                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                    TimeCodeData data = RaceVoice.TimeCodeData[instigator.Customize[(int)CustomizeIndex.Race] + "_" + gender];
                    _mediaManager.PlayAudio(_playerObject, value, SoundType.Emote,
                    characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0);
                    if (isVoicedEmote) {
                        MuteVoiceChecK(10000);
                    }
                } else {
                    CheckForValidSCD(instigator, GetEmoteName(emoteId), stagingPath, true);
                }
                if (config.UsePlayerSync) {
                    Task.Run(async () => {
                        bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                    });
                }
            }
        }
        public void ExtractSCDOptions(Option option, string directory) {
            if (option != null) {
                foreach (var item in option.Files) {
                    if (item.Key.EndsWith(".scd")) {
                        _filter.Blacklist.Add(item.Key);
                        if (!_scdReplacements.ContainsKey(item.Key)) {
                            try {
                                _scdReplacements.Add(item.Key, directory + @"\" + item.Value);
                            } catch {
                                Dalamud.Logging.PluginLog.LogWarning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                            }
                        }
                    }
                }
            }
        }
        public async Task<List<KeyValuePair<List<string>, int>>> GetPrioritySortedSoundPacks() {
            _filter.Blacklist?.Clear();
            _scdReplacements?.Clear();
            List<KeyValuePair<List<string>, int>> list = new List<KeyValuePair<List<string>, int>>();
            try {
                string modPath = Ipc.GetModDirectory.Subscriber(pluginInterface).Invoke();
                if (Directory.Exists(modPath)) {
                    var collection = Ipc.GetCollectionForObject.Subscriber(pluginInterface).Invoke(0);
                    foreach (var directory in Directory.EnumerateDirectories(modPath)) {
                        string relativeDirectory = directory.Replace(modPath, null).TrimStart('\\');
                        var currentModSettings =
                        Ipc.GetCurrentModSettings.Subscriber(pluginInterface).
                        Invoke(collection.Item3, relativeDirectory, null, true);
                        var result = currentModSettings.Item1;
                        if (result == Penumbra.Api.Enums.PenumbraApiEc.Success) {
                            if (currentModSettings.Item2 != null) {
                                bool enabled = currentModSettings.Item2.Value.Item1;
                                int priority = currentModSettings.Item2.Value.Item2;
                                if (enabled) {
                                    string soundPackData = directory + @"\rpvsp";
                                    if (Directory.Exists(directory)) {
                                        foreach (string file in Directory.EnumerateFiles(directory)) {
                                            if (file.EndsWith(".json") && !file.EndsWith("meta.json")) {
                                                if (file.EndsWith("default_mod.json")) {
                                                    Option option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(file));
                                                    ExtractSCDOptions(option, directory);
                                                } else {
                                                    Group group = JsonConvert.DeserializeObject<Group>(File.ReadAllText(file));
                                                    foreach (Option option in group.Options) {
                                                        ExtractSCDOptions(option, directory);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (Path.Exists(soundPackData)) {
                                        var soundList = new List<string>();
                                        foreach (string file in Directory.EnumerateFiles(soundPackData)) {
                                            if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                                                soundList.Add(file);
                                            }
                                        }
                                        list.Add(new KeyValuePair<List<string>, int>(soundList, priority));
                                    } else {
                                        soundPackData = directory + @"\arksp";
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
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
            list.Sort((x, y) => y.Value.CompareTo(x.Value));
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
            return list;
        }
        public void MuteVoiceChecK(int length = 20) {
            muteLength = length;
            if (!muteTimer.IsRunning) {
                if (Filter != null) {
                    Filter.Muted = voiceMuted = true;
                }
                RefreshPlayerVoiceMuted();
                Dalamud.Logging.PluginLog.Log("Mute Triggered");
                muteTimer.Start();
            }
        }
        private string GetEmoteName(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            return CleanSenderName(emote.Name).Replace(" ", "").ToLower();
        }
        private string GetEmotePath(CharacterVoicePack characterVoicePack, ushort emoteId, out bool isVoicedEmote) {
            Emote emoteEnglish = _dataManager.GetExcelSheet<Emote>(Dalamud.ClientLanguage.English).GetRow(emoteId);
            Emote emoteFrench = _dataManager.GetExcelSheet<Emote>(Dalamud.ClientLanguage.French).GetRow(emoteId);
            Emote emoteGerman = _dataManager.GetExcelSheet<Emote>(Dalamud.ClientLanguage.German).GetRow(emoteId);
            Emote emoteJapanese = _dataManager.GetExcelSheet<Emote>(Dalamud.ClientLanguage.Japanese).GetRow(emoteId);

            string emotePathId = characterVoicePack.GetMisc(emoteId.ToString());
            string emotePathEnglish = characterVoicePack.GetMisc(emoteEnglish.Name);
            string emotePathFrench = characterVoicePack.GetMisc(emoteFrench.Name);
            string emotePathGerman = characterVoicePack.GetMisc(emoteGerman.Name);
            string emotePathJapanese = characterVoicePack.GetMisc(emoteJapanese.Name);

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

        private void _roleplayingVoiceManager_VoicesUpdated(object sender, EventArgs e) {
            config.CharacterVoices = _roleplayingMediaManager.CharacterVoices;
            config.Save();
            pluginInterface.SavePluginConfig(config);
        }
        public static string SplitCamelCase(string input) {
            return System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1",
                System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
        }
        public static string RemoveSpecialSymbols(string value) {
            Regex rgx = new Regex("[^a-zA-Z:/._ -]");
            return rgx.Replace(value, "");
        }
        private void Chat_ChatMessage(XivChatType type, uint senderId,
            ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!disposed) {
                CheckDependancies();
                string playerName = sender.TextValue;
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
                            ChatText(playerName, message, type, senderId);
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
                            BattleText(playerName, message, type);
                            break;
                    }
                } else {
                    InitialzeManager();
                }
            }
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
            }
        }

        private void _mediaManager_OnErrorReceived(object sender, MediaError e) {
            Dalamud.Logging.PluginLog.LogError(e.Exception, e.Exception.Message);
        }

        private void _audioManager_OnNewAudioTriggered(object sender, EventArgs e) {
        }

        private void BattleText(string playerName, SeString message, XivChatType type) {
            CheckDependancies();
            if (type != (XivChatType)8235 || message.TextValue.Contains("You")) {
                if (config.VoicePackIsActive) {
                    string value = "";
                    string playerMessage = message.TextValue;
                    string[] values = message.TextValue.Split(' ');
                    if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                        string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                        string path = config.CacheFolder + @"\VoicePack\" + voice;
                        string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                        bool attackIntended = false;
                        CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                        if (!message.TextValue.Contains("cancel")) {
                            if (!IsDicipleOfTheHand(_clientState.LocalPlayer.ClassJob.GameData.Abbreviation)) {
                                LocalPlayerCombat(playerName, message, type, characterVoicePack, ref value, ref attackIntended);
                            } else {
                                PlayerCrafting(playerName, message, type, characterVoicePack, ref value);
                            }
                        }

                        if (!string.IsNullOrEmpty(value) || attackIntended) {
                            if (!attackIntended) {
                                _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerCombat);
                            }
                            if (!muteTimer.IsRunning) {
                                if (Filter != null) {
                                    Filter.Muted = true;
                                }
                                Task.Run(() => {
                                    if (config.UsePlayerSync) {
                                        Task.Run(async () => {
                                            bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                        });
                                    }
                                    while (muteTimer.ElapsedMilliseconds < 20) {
                                        Thread.Sleep(20);
                                    }
                                    try {
                                        if (Filter != null) {
                                            lock (Filter) {
                                                Filter.Muted = false;
                                                muteTimer.Reset();
                                            }
                                        }
                                    } catch (Exception e) {
                                        Dalamud.Logging.PluginLog.LogError(e.Message);
                                    }
                                });
                            }
                            muteTimer.Restart();
                        }
                    }
                }
            } else {
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
                        _chat.PrintError("Failed to write to disk, please make sure the cache folder does not require administrative access!");
                    }
                    if (config.UsePlayerSync) {
                        if (CombinedWhitelist().Contains(playerSender)) {
                            if (!isDownloadingZip) {
                                if (!Path.Exists(clipPath)) {
                                    isDownloadingZip = true;
                                    maxDownloadLengthTimer.Restart();
                                    Task.Run(async () => {
                                        string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                        isDownloadingZip = false;
                                    });
                                }
                            }
                            if (Path.Exists(clipPath) && !isDownloadingZip) {
                                CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                                string value = "";
                                if (!IsDicipleOfTheHand(_clientState.LocalPlayer.ClassJob.GameData.Abbreviation)) {
                                    OtherPlayerCombat(playerName, message, type, characterVoicePack, ref value);
                                } else {
                                    PlayerCrafting(playerName, message, type, characterVoicePack, ref value);
                                }
                                Task.Run(async () => {
                                    GameObject character = null;
                                    foreach (var item in _objectTable) {
                                        string[] playerNameStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(item.Name.TextValue))).Split(' ');
                                        string playerSenderStrings = playerNameStrings[0 + offset] + " " + playerNameStrings[2 + offset];
                                        if (playerNameStrings.Contains(playerSender)) {
                                            character = item;
                                        }
                                    }
                                    _mediaManager.PlayAudio(new MediaGameObject((PlayerCharacter)character,
                                    playerSender, character.Position), value, SoundType.OtherPlayerCombat);
                                });
                                if (!muteTimer.IsRunning) {
                                    Filter.Muted = true;
                                    Task.Run(() => {
                                        while (muteTimer.ElapsedMilliseconds < 20) {
                                            Thread.Sleep(20);
                                        }
                                        lock (Filter) {
                                            Filter.Muted = false;
                                        }
                                        muteTimer.Reset();
                                    });
                                }
                                muteTimer.Restart();
                            }
                        }
                    }
                }
            }
        }
        private async void EmoteReaction(string messageValue) {
            var emotes = _dataManager.GetExcelSheet<Emote>(Dalamud.ClientLanguage.English);
            foreach (var item in emotes) {
                if (!string.IsNullOrWhiteSpace(item.Name.RawString)) {
                    if (messageValue.ToLower().Contains(" " + item.Name.RawString.ToLower() + " ") ||
                        messageValue.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s ") ||
                        messageValue.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed ") ||
                        messageValue.ToLower().EndsWith(" " + item.Name.RawString.ToLower()) ||
                        messageValue.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s") ||
                        messageValue.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed")) {
                        messageQueue.Enqueue("/" + item.Name.RawString.ToLower());
                        break;
                    }
                }
            }
        }
        private void OtherPlayerCombat(string playerName, SeString message, XivChatType type,
            CharacterVoicePack characterVoicePack, ref string value) {
            if (message.TextValue.Contains("hit") ||
              message.TextValue.Contains("uses") ||
              message.TextValue.Contains("casts")) {
                value = characterVoicePack.GetAction(message.TextValue);
            } else if (message.TextValue.Contains("defeated")) {
                value = characterVoicePack.GetDeath();
            } else if (message.TextValue.Contains("miss")) {
                value = characterVoicePack.GetMissed();
            } else if (message.TextValue.Contains("readies")) {
                value = characterVoicePack.GetReadying(message.TextValue);
            } else if (message.TextValue.Contains("casting")) {
                value = characterVoicePack.GetCastingAttack();
            } else if (message.TextValue.Contains("revive")) {
                value = characterVoicePack.GetRevive();
            } else if (message.TextValue.Contains("damage")) {
                value = characterVoicePack.GetHurt();
            }
        }

        private void PlayerCrafting(string playerName, SeString message,
            XivChatType type, CharacterVoicePack characterVoicePack, ref string value) {
            value = characterVoicePack.GetMisc(message.TextValue);
            if (string.IsNullOrEmpty(value)) {
                value = characterVoicePack.GetReadying(message.TextValue);
            }
        }

        private void LocalPlayerCombat(string playerName, SeString message,
            XivChatType type, CharacterVoicePack characterVoicePack, ref string value, ref bool attackIntended) {
            if (type == (XivChatType)2729 ||
            type == (XivChatType)2091) {
                value = characterVoicePack.GetMisc(message.TextValue);
                if (string.IsNullOrEmpty(value)) {
                    if (attackCount == 0) {
                        value = characterVoicePack.GetAction(message.TextValue);
                        attackCount++;
                    } else {
                        attackCount++;
                        if (attackCount >= 3) {
                            attackCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2234) {
                value = characterVoicePack.GetDeath();
            } else if (type == (XivChatType)2730) {
                value = characterVoicePack.GetMissed();
            } else if (type == (XivChatType)2219) {
                if (message.TextValue.Contains("ready") ||
                    message.TextValue.Contains("readies")) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetCastingHeal();
                        castingCount++;
                    } else {
                        castingCount++;
                        if (attackCount >= 3) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2731) {
                if (message.TextValue.Contains("ready") ||
                    message.TextValue.Contains("readies")) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetCastingAttack();
                        castingCount++;
                    } else {
                        castingCount++;
                        if (castingCount >= 3) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2106) {
                value = characterVoicePack.GetRevive();
            } else if (type == (XivChatType)10409) {
                value = characterVoicePack.GetHurt();
            }
        }

        //public string MakeThirdPerson(string value) {
        //    return value.Replace("cast ", "casts ")
        //                .Replace("use", "uses")
        //                .Replace("lose", "loses")
        //                .Replace("hit", "hits")
        //                .Replace("begin", "begins")
        //                .Replace("You", null)
        //                .Replace("!", null);
        //}
        public string RemoveActionPhrases(string value) {
            return value.Replace("Direct hit ", null)
                    .Replace("Critical direct hit ", null)
                    .Replace("Critical ", null)
                    .Replace("Direct ", null)
                    .Replace("direct ", null);
        }
        public bool IsDicipleOfTheHand(string value) {
            List<string> jobs = new List<string>() { "ALC", "ARM", "BSM", "CUL", "CRP", "GSM", "LTW", "WVR" };
            return jobs.Contains(value.ToUpper());
        }

        private void ChatText(string sender, SeString message, XivChatType type, uint senderId) {
            if (sender.Contains(_clientState.LocalPlayer.Name.TextValue)) {
                if (config.PerformEmotesBasedOnWrittenText) {
                    if (type == XivChatType.CustomEmote) {
                        Task.Run(() => EmoteReaction(message.TextValue));
                    }
                }
                if (config.AiVoiceActive && !string.IsNullOrEmpty(config.ApiKey)) {
                    string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(
                    _clientState.LocalPlayer.Name.TextValue)).Split(" ");
                    string playerSender = senderStrings.Length == 2 ?
                        (senderStrings[0] + " " + senderStrings[1]) :
                        (senderStrings[0] + " " + senderStrings[2]);
                    string playerMessage = message.TextValue;
                    Task.Run(async () => {
                        string value = await _roleplayingMediaManager.DoVoice(playerSender, playerMessage,
                        config.Characters[_clientState.LocalPlayer.Name.TextValue],
                        type == XivChatType.CustomEmote,
                        config.PlayerCharacterVolume,
                        _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                        _mediaManager.PlayAudio(_playerObject, value, SoundType.MainPlayerTts);
                    });
                }
            } else {
                string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender)).Split(" ");
                bool isShoutYell = false;
                if (senderStrings.Length > 2) {
                    string playerSender = senderStrings[0] + " " + senderStrings[2];
                    string playerMessage = message.TextValue;
                    bool audioFocus = false;
                    if (_clientState.LocalPlayer.TargetObject != null) {
                        if (_clientState.LocalPlayer.TargetObject.ObjectKind ==
                            ObjectKind.Player) {
                            audioFocus = _clientState.LocalPlayer.TargetObject.Name.TextValue == sender
                                || type == XivChatType.Party
                                || type == XivChatType.CrossParty || isShoutYell;
                            isShoutYell = type == XivChatType.Shout
                                || type == XivChatType.Yell;
                        }
                    } else {
                        audioFocus = true;
                    }
                    PlayerCharacter player = (PlayerCharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                    if (config.UsePlayerSync) {
                        if (CombinedWhitelist().Contains(playerSender)) {
                            Task.Run(async () => {
                                string value = await _roleplayingMediaManager.
                                GetSound(playerSender, playerMessage, audioFocus ?
                                config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                _clientState.LocalPlayer.Position, isShoutYell, @"\Incoming\");
                                _mediaManager.PlayAudio(new MediaGameObject(player), value, SoundType.OtherPlayerTts);
                            });
                        }
                    }
                    if (type == XivChatType.Yell || type == XivChatType.Shout) {
                        if (config.TuneIntoTwitchStreams && IsResidential()) {
                            if (!twitchSetCooldown.IsRunning || twitchSetCooldown.ElapsedMilliseconds > 10000) {
                                var strings = message.TextValue.Split(' ');
                                foreach (string value in strings) {
                                    if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                        var audioGameObject = new MediaGameObject(player);
                                        if (_mediaManager.IsAllowedToStartStream(audioGameObject)) {
                                            TuneIntoStream(value, audioGameObject);
                                        }
                                        break;
                                    }
                                }
                            }
                        } else {
                            var strings = message.TextValue.Split(' ');
                            foreach (string value in strings) {
                                if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                    potentialStream = value;
                                    lastStreamURL = value;
                                    string cleanedURL = RemoveSpecialSymbols(value);
                                    string streamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                                    _chat.Print(streamer + " is hosting a stream in this zone! Wanna tune in? You can do \"/artemis listen\"");
                                }
                            }
                        }
                    }
                }
            }
        }

        private void TuneIntoStream(string url, IGameObject audioGameObject) {
            Task.Run(async () => {
                string cleanedURL = RemoveSpecialSymbols(url);
                string streamURL = TwitchFeedManager.GetServerResponse(cleanedURL, TwitchFeedManager.TwitchFeedType._360p);
                if (!string.IsNullOrEmpty(streamURL)) {
                    _mediaManager.PlayStream(audioGameObject, streamURL);
                    lastStreamURL = cleanedURL;
                    currentStreamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                    _chat.Print(@"Tuning into " + currentStreamer + @"! Wanna chat? Use ""/artemis twitch""." +
                        "\r\nYou can also use \"/artemis video\" to toggle the video feed!" +
                        (!IsResidential() ? "\r\nIf you need to end a stream in a public space you can leave the zone or use \"/artemis endlisten\"" : ""));
                    _videoWindow.IsOpen = true;
                }
            });
            twitchWasPlaying = true;
            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
            twitchSetCooldown.Stop();
            twitchSetCooldown.Reset();
            twitchSetCooldown.Start();
        }

        public static string CleanSenderName(string senderName) {
            string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(senderName)).Split(" ");
            string playerSender = senderStrings.Length == 1 ? senderStrings[0] : senderStrings.Length == 2 ?
                (senderStrings[0] + " " + senderStrings[1]) :
                (senderStrings[0] + " " + senderStrings[2]);
            return playerSender;
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
                RefreshSoundData();
            }
        }
        public void InitialzeManager() {
            _roleplayingMediaManager = new RoleplayingMediaManager(config.ApiKey, config.CacheFolder, _networkedClient, config.CharacterVoices);
            _roleplayingMediaManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
            _window.Manager = _roleplayingMediaManager;
        }
        private void UiBuilder_Draw() {
            this.windowSystem.Draw();
        }

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
        public void OpenConfig(string command, string args) {
            if (!disposed) {
                string[] splitArgs = args.Split(' ');
                if (splitArgs.Length > 0) {
                    switch (splitArgs[0].ToLower()) {
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
                        case "twitch":
                            if (splitArgs.Length > 1 && splitArgs[1].Contains("twitch.tv")) {
                                TuneIntoStream(splitArgs[1], _playerObject);
                            } else {
                                if (!string.IsNullOrEmpty(currentStreamer)) {
                                    try {
                                        Process.Start(new System.Diagnostics.ProcessStartInfo() {
                                            FileName = @"https://www.twitch.tv/popout/" + currentStreamer + @"/chat?popout=",
                                            UseShellExecute = true,
                                            Verb = "OPEN"
                                        });
                                    } catch (Exception e) {
                                        Dalamud.Logging.PluginLog.LogError(e, e.Message);
                                    }
                                } else {
                                    _chat.PrintError("There is no active stream");
                                }
                            }
                            break;
                        case "listen":
                            if (!string.IsNullOrEmpty(potentialStream)) {
                                TuneIntoStream(potentialStream, _playerObject);
                            }
                            break;
                        case "endlisten":
                            if (!IsResidential()) {
                                _mediaManager.StopStream();
                                ResetTwitchValues();
                                potentialStream = "";
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


        #region IDisposable Support
        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            try {
                disposed = true;
                config.Save();
                this.pluginInterface.SavePluginConfig(this.config);
                config.OnConfigurationChanged -= Config_OnConfigurationChanged;
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
                        _mediaManager.OnNewMediaTriggered -= _audioManager_OnNewAudioTriggered;
                        _mediaManager.OnErrorReceived -= _mediaManager_OnErrorReceived;
                        _mediaManager?.Dispose();
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }
                _emoteReaderHook.OnEmote -= (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
                try {
                    _clientState.Login -= _clientState_Login;
                    _clientState.Logout -= _clientState_Logout;
                    _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
                    _clientState.LeavePvP -= _clientState_LeavePvP;
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }
                _toast.ErrorToast -= _toast_ErrorToast;
                try {
                    _framework.Update -= framework_Update;
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                }
                Ipc.ModSettingChanged.Subscriber(pluginInterface).Event -= modSettingChanged;
                _networkedClient?.Dispose();
                Filter?.Dispose();
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
