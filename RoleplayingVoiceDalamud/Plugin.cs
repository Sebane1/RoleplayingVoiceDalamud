using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ElevenLabs.Voices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVLooseTextureCompiler.Networking;
using FFXIVVoicePackCreator;
using FFXIVVoicePackCreator.VoiceSorting;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;
using PatMe;
using Penumbra.Api;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using RoleplayingVoiceCore.Twitch;
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
using System.Windows.Forms;

namespace RoleplayingVoice {
    public class Plugin : IDalamudPlugin {
        private readonly DalamudPluginInterface pluginInterface;
        private readonly ChatGui _chat;
        private readonly ClientState _clientState;

        private readonly PluginCommandManager<Plugin> commandManager;
        private NetworkedClient _networkedClient;
        private readonly Configuration config;
        private readonly WindowSystem windowSystem;
        private PluginWindow window { get; init; }
        private RoleplayingVoiceManager _roleplayingVoiceManager;
        private Stopwatch stopwatch;
        private Stopwatch cooldown;
        private Stopwatch muteTimer;
        private Stopwatch twitchSetCooldown = new Stopwatch();
        private EmoteReaderHooks _emoteReaderHook;
        private AudioGameObject _playerObject;
        private AudioManager _audioManager;
        private ObjectTable _objectTable;
        private bool isDownloadingZip;
        private RaceVoice _raceVoice;
        private string lastPrintedWarning;
        private bool disposed;
        private DataManager _dataManager;
        private ToastGui _toast;
        private bool ignoreAttack;
        private int attackCount;
        private int castingCount;
        private List<KeyValuePair<List<string>, int>> penumbraSoundPacks;
        private List<string> combinedSoundList;
        private unsafe Camera* _camera;
        private AudioCameraObject _audioCamera;
        private GameConfig _gameConfig;
        private SigScanner _sigScanner;
        private Filter _filter;
        public EventHandler OnMuteTimerOver;
        private Framework _framework;
        private bool voiceMuted;
        private int muteLength;
        Dictionary<string, MovingObject> gameObjectPositions = new Dictionary<string, MovingObject>();
        Queue<string> temporaryWhitelistQueue = new Queue<string>();
        List<string> temporaryWhitelist = new List<string>();
        Stopwatch redrawCooldown = new Stopwatch();
        private int objectsRedrawn;
        private int redrawObjectCount;
        private bool staging;
        private string stagingPath;
        private string lastStreamURL;
        private bool twitchWasPlaying;

        public string Name => "Roleplaying Voice";

        public RoleplayingVoiceManager RoleplayingVoiceManager { get => _roleplayingVoiceManager; set => _roleplayingVoiceManager = value; }
        public NetworkedClient NetworkedClient { get => _networkedClient; set => _networkedClient = value; }
        public SigScanner SigScanner { get => _sigScanner; set => _sigScanner = value; }

        public unsafe Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            ChatGui chat,
            ClientState clientState,
            SigScanner scanner,
            ObjectTable objectTable,
            ToastGui toast,
            DataManager dataManager,
            GameConfig gameConfig,
            Framework framework) {
            this.pluginInterface = pi;
            this._chat = chat;
            this._clientState = clientState;

            // Get or create a configuration object
            this.config = (Configuration)this.pluginInterface.GetPluginConfig()
                          ?? this.pluginInterface.Create<Configuration>();
            // Initialize the UI
            this.windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);

            window = this.pluginInterface.Create<PluginWindow>();
            window.ClientState = this._clientState;
            window.Configuration = this.config;
            window.PluginInterface = this.pluginInterface;
            window.PluginReference = this;
            AttemptConnection();
            if (config.ApiKey != null) {
                InitialzeManager();
            }

            if (window is not null) {
                this.windowSystem.AddWindow(window);
            }
            window.RequestingReconnect += Window_RequestingReconnect;
            window.OnMoveFailed += Window_OnMoveFailed;
            this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
            config.OnConfigurationChanged += Config_OnConfigurationChanged;
            window.Toggle();
            window.PluginReference = this;
            _emoteReaderHook = new EmoteReaderHooks(scanner, clientState, objectTable);
            _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
            this._chat.ChatMessage += Chat_ChatMessage;
            cooldown = new Stopwatch();
            muteTimer = new Stopwatch();
            _objectTable = objectTable;
            _clientState.Login += _clientState_Login;
            _clientState.Logout += _clientState_Logout;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _clientState.LeavePvP += _clientState_LeavePvP;
            window.OnWindowOperationFailed += Window_OnWindowOperationFailed;
            _dataManager = dataManager;
            _toast = toast;
            _toast.ErrorToast += _toast_ErrorToast;
            _gameConfig = gameConfig;
            _sigScanner = scanner;
            RaceVoice.LoadRacialVoiceInfo();
            CheckDependancies();
            _filter = new Filter(this);
            _filter.Enable();
            _framework = framework;
            _framework.Update += framework_Update;
            RefreshSoundData();
            Ipc.ModSettingChanged.Subscriber(pluginInterface).Event += modSettingChanged;
            Ipc.GameObjectRedrawn.Subscriber(pluginInterface).Event += gameObjectRedrawn;
        }

        private void Window_OnMoveFailed(object sender, EventArgs e) {
            _chat.PrintError("Cache swap failed, this is not a valid cache folder. Please select an empty folder that does not require administrator rights.");
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
            string hash = RoleplayingVoiceManager.Shai1Hash(senderName);
            string clipPath = path + @"\" + hash;
            if (CombinedWhitelist().Contains(senderName) &&
                !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                if (Directory.Exists(clipPath)) {
                    try {
                        Directory.Delete(clipPath, true);
                    } catch {

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
        private void framework_Update(Framework framework) {
            if (config != null && _audioManager != null && _objectTable != null && !disposed) {
                uint voiceVolume = 0;
                uint masterVolume = 0;
                uint soundEffectVolume = 0;
                if (_gameConfig.TryGet(SystemConfigOption.SoundVoice, out voiceVolume)) {
                    if (_gameConfig.TryGet(SystemConfigOption.SoundMaster, out masterVolume)) {
                        _audioManager.MainPlayerVolume = config.PlayerCharacterVolume *
                            ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _audioManager.OtherPlayerVolume = config.OtherCharacterVolume *
                            ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _audioManager.UnfocusedPlayerVolume = config.UnfocusedCharacterVolume *
                            ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        if (_gameConfig.TryGet(SystemConfigOption.SoundEnv, out soundEffectVolume)) {
                            _audioManager.SFXVolume = config.LoopingSFXVolume *
                             ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                        }
                        _audioManager.LiveStreamVolume = config.LivestreamVolume * ((float)masterVolume / 100f);
                        if (muteTimer.ElapsedMilliseconds > muteLength) {
                            if (_filter != null) {
                                lock (_filter) {
                                    _filter.Muted = voiceMuted = false;
                                    RefreshPlayerVoiceMuted();
                                    muteTimer.Stop();
                                    muteTimer.Reset();
                                }
                            }
                        }
                    }
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
                _chat.PrintError("Failed to write to disk, please make sure the cache folder does not require administraive access!");
            }
            string hash = RoleplayingVoiceManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (CombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            if (!Path.Exists(clipPath)) {
                                isDownloadingZip = true;
                                await Task.Run(async () => {
                                    string value = await _roleplayingVoiceManager.GetZip(playerSender, path);
                                    isDownloadingZip = false;
                                });
                            }
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                            bool isVoicedEmote = false;
                            string value = characterVoicePack.GetMisc("moving");
                            if (!string.IsNullOrEmpty(value)) {
                                _audioManager.PlayAudio(new AudioGameObject(gameObject), value, SoundType.LoopWhileMoving, 0);
                                if (isVoicedEmote) {
                                    MuteVoiceChecK(4000);
                                }
                            } else {
                                _audioManager.StopAudio(new AudioGameObject(gameObject));
                            }
                            //string streamPath = GetStreamingPath(clipPath);
                            //if (!string.IsNullOrEmpty(streamPath)) {
                            //    _audioManager.PlayStream(_playerObject, streamPath, SoundType.Livestream);
                            //}
                        }
                    }
                }

            } catch {
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
                            bool success = await _roleplayingVoiceManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                        });
                    }
                    _audioManager.PlayAudio(_playerObject, value, SoundType.LoopWhileMoving, 0);
                } else {
                    _audioManager.StopAudio(_playerObject);
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
                    if (_filter != null) {
                        _filter.Muted = false;
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
                            } catch { }
                        }
                    }
                    if (Directory.Exists(config.CacheFolder)) {
                        foreach (string file in Directory.EnumerateFiles(config.CacheFolder)) {
                            try {
                                if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                                    File.Delete(file);
                                } else {
                                    _chat.PrintError(file + " should not be in the cache folder, please remove it.");
                                }
                            } catch { }
                        }
                    }
                    try {
                        Directory.CreateDirectory(stagingPath);
                    } catch {
                        _chat.PrintError("Failed to write to disk, please make sure the cache folder does not require administraive access!");
                    }
                    if (Directory.Exists(stagingPath)) {
                        foreach (string file in Directory.EnumerateFiles(stagingPath)) {
                            try {
                                File.Delete(file);
                            } catch { }
                        }
                    }
                    foreach (var sound in list) {
                        try {
                            File.Copy(sound, Path.Combine(stagingPath, Path.GetFileName(sound)), true);
                        } catch { }
                    }
                    staging = false;
                }
            });
            return list;
        }
        private void Window_OnWindowOperationFailed(object sender, PluginWindow.MessageEventArgs e) {
            _chat.PrintError(e.Message);
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
                            _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
                        }
                    }
                    cooldown.Restart();
                }
            }
        }

        private void _clientState_LeavePvP() {
            CleanSounds();
        }

        private void _clientState_TerritoryChanged(object sender, ushort e) {
            CleanSounds();
        }

        private void _clientState_Logout(object sender, EventArgs e) {
            CleanSounds();
        }

        private void _clientState_Login(object sender, EventArgs e) {
            CleanSounds();
            CheckDependancies(true);
            RefreshSoundData();
        }
        public void CleanSounds() {
            string path = config.CacheFolder + @"\VoicePack\Others";
            if (_audioManager != null) {
                _audioManager.CleanSounds();
                lastStreamURL = "";
            }
            if (twitchWasPlaying) {
                twitchWasPlaying = false;
                _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
            }
            twitchSetCooldown.Stop();
            twitchSetCooldown.Reset();
            temporaryWhitelist.Clear();
            if (Directory.Exists(path)) {
                try {
                    Directory.Delete(path, true);
                } catch {

                }
            }
        }
        private void Window_RequestingReconnect(object sender, EventArgs e) {
            AttemptConnection();
        }

        private void AttemptConnection() {
            if (_networkedClient != null) {
                _networkedClient.Dispose();
            }
            _networkedClient = new NetworkedClient(config.ConnectionIP);
            if (_roleplayingVoiceManager != null) {
                _roleplayingVoiceManager.NetworkedClient = _networkedClient;
            }
        }

        private void UiBuilder_OpenConfigUi() {
            window.RefreshVoices();
            window.Toggle();
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

        private async void ReceivingEmote(PlayerCharacter instigator, ushort emoteId) {
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
                    _chat.PrintError("Failed to write to disk, please make sure the cache folder does not require administraive access!");
                }
                string hash = RoleplayingVoiceManager.Shai1Hash(playerSender);
                string clipPath = path + @"\" + hash;
                try {
                    if (config.UsePlayerSync) {
                        if (CombinedWhitelist().Contains(playerSender)) {
                            if (!isDownloadingZip) {
                                if (!Path.Exists(clipPath)) {
                                    isDownloadingZip = true;
                                    await Task.Run(async () => {
                                        string value = await _roleplayingVoiceManager.GetZip(playerSender, path);
                                        isDownloadingZip = false;
                                    });
                                }
                            }
                            if (Directory.Exists(path)) {
                                CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                                bool isVoicedEmote = false;
                                string value = GetEmotePath(characterVoicePack, emoteId, out isVoicedEmote);
                                if (!string.IsNullOrEmpty(value)) {
                                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                                    TimeCodeData data = RaceVoice.TimeCodeData[instigator.Customize[(int)CustomizeIndex.Race] + "_" + gender];
                                    _audioManager.PlayAudio(new AudioGameObject(instigator), value, SoundType.OtherPlayer,
                                     characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0);
                                    if (isVoicedEmote) {
                                        MuteVoiceChecK(4000);
                                    }
                                } else {
                                    _audioManager.StopAudio(new AudioGameObject(instigator));
                                }
                                //string streamPath = GetStreamingPath(clipPath);
                                //if (!string.IsNullOrEmpty(streamPath)) {
                                //    _audioManager.PlayStream(_playerObject, streamPath, SoundType.Livestream);
                                //}
                            }
                        }
                    }
                } catch {

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
                    if (config.UsePlayerSync) {
                        Task.Run(async () => {
                            bool success = await _roleplayingVoiceManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                        });
                    }

                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                    TimeCodeData data = RaceVoice.TimeCodeData[instigator.Customize[(int)CustomizeIndex.Race] + "_" + gender];
                    _audioManager.PlayAudio(_playerObject, value, SoundType.Emote,
                    characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0);
                    if (isVoicedEmote) {
                        MuteVoiceChecK(10000);
                    }
                } else {
                    _audioManager.StopAudio(_playerObject);
                }
            }
        }

        public async Task<List<KeyValuePair<List<string>, int>>> GetPrioritySortedSoundPacks() {
            List<KeyValuePair<List<string>, int>> list = new List<KeyValuePair<List<string>, int>>();
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
                if (_filter != null) {
                    _filter.Muted = voiceMuted = true;
                }
                RefreshPlayerVoiceMuted();
                Dalamud.Logging.PluginLog.Log("Mute Triggered");
                muteTimer.Start();
            }
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
            config.CharacterVoices = _roleplayingVoiceManager.CharacterVoices;
            config.Save();
            pluginInterface.SavePluginConfig(config);
        }
        public static string SplitCamelCase(string input) {
            return System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1",
                System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
        }
        public static string RemoveSpecialSymbols(string value) {
            Regex rgx = new Regex("[^a-zA-Z -]");
            return rgx.Replace(value, "");
        }
        private void Chat_ChatMessage(XivChatType type, uint senderId,
            ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!disposed) {
                CheckDependancies();
                string playerName = sender.TextValue;
                if (_roleplayingVoiceManager != null) {
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
                    _playerObject = new AudioGameObject(_clientState.LocalPlayer);
                }
                if (_audioManager == null || forceNewAssignments) {
                    _camera = CameraManager.Instance->GetActiveCamera();
                    _audioCamera = new AudioCameraObject(_camera);
                    _audioManager = new AudioManager(_playerObject, _audioCamera, Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName));
                    _audioManager.OnNewAudioTriggered += _audioManager_OnNewAudioTriggered;
                }
            }
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
                                _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
                            }
                            if (!muteTimer.IsRunning) {
                                if (_filter != null) {
                                    _filter.Muted = true;
                                }
                                Dalamud.Logging.PluginLog.Log("Battle Mute Finalized");
                                Task.Run(() => {
                                    if (config.UsePlayerSync) {
                                        Task.Run(async () => {
                                            bool success = await _roleplayingVoiceManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                        });
                                    }
                                    while (muteTimer.ElapsedMilliseconds < 20) {
                                        Thread.Sleep(20);
                                    }
                                    attackCount = 0;
                                    lock (_filter) {
                                        _filter.Muted = false;
                                        muteTimer.Reset();
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
                    string hash = RoleplayingVoiceManager.Shai1Hash(playerSender);
                    string path = config.CacheFolder + @"\VoicePack\Others";
                    string clipPath = path + @"\" + hash;
                    try {
                        Directory.CreateDirectory(path);
                    } catch {
                        _chat.PrintError("Failed to write to disk, please make sure the cache folder does not require administraive access!");
                    }
                    if (config.UsePlayerSync) {
                        if (CombinedWhitelist().Contains(playerSender)) {
                            if (!isDownloadingZip) {
                                if (!Path.Exists(clipPath)) {
                                    isDownloadingZip = true;
                                    Task.Run(async () => {
                                        string value = await _roleplayingVoiceManager.GetZip(playerSender, path);
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
                                    _audioManager.PlayAudio(new AudioGameObject((PlayerCharacter)character,
                                    playerSender, character.Position), value, SoundType.OtherPlayer);
                                });
                                if (!muteTimer.IsRunning) {
                                    _filter.Muted = true;
                                    Task.Run(() => {
                                        while (muteTimer.ElapsedMilliseconds < 20) {
                                            Thread.Sleep(20);
                                        }
                                        lock (_filter) {
                                            _filter.Muted = false;
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

        private void OtherPlayerCombat(string playerName, SeString message, XivChatType type, CharacterVoicePack characterVoicePack, ref string value) {
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

        private void PlayerCrafting(string playerName, SeString message, XivChatType type, CharacterVoicePack characterVoicePack, ref string value) {
            value = characterVoicePack.GetMisc(message.TextValue);
            if (string.IsNullOrEmpty(value)) {
                value = characterVoicePack.GetReadying(message.TextValue);
            }
        }

        private void LocalPlayerCombat(string playerName, SeString message, XivChatType type, CharacterVoicePack characterVoicePack, ref string value, ref bool attackIntended) {
            if (type == (XivChatType)2729 ||
            type == (XivChatType)2091) {
                value = characterVoicePack.GetMisc(message.TextValue);
                if (string.IsNullOrEmpty(value)) {
                    if (attackCount == 0) {
                        value = characterVoicePack.GetAction(message.TextValue);
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
                    } else {
                        castingCount++;
                        if (attackCount >= 3) {
                            attackCount = 0;
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
                    if (castingCount == 3) {
                        value = characterVoicePack.GetCastingAttack();
                        castingCount = 0;
                    } else {
                        castingCount++;
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2106) {
                value = characterVoicePack.GetRevive();
            } else if (type == (XivChatType)10409) {
                value = characterVoicePack.GetHurt();
            }
        }

        public string MakeThirdPerson(string value) {
            return value.Replace("cast ", "casts ")
                        .Replace("use", "uses")
                        .Replace("lose", "loses")
                        .Replace("hit", "hits")
                        .Replace("begin", "begins")
                        .Replace("You", null)
                        .Replace("!", null);
        }
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
                if (config.IsActive && !string.IsNullOrEmpty(config.ApiKey)) {
                    string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(
                    _clientState.LocalPlayer.Name.TextValue)).Split(" ");
                    string playerSender = senderStrings.Length == 2 ?
                        (senderStrings[0] + " " + senderStrings[1]) :
                        (senderStrings[0] + " " + senderStrings[2]);
                    string playerMessage = message.TextValue;
                    Task.Run(async () => {
                        string value = await _roleplayingVoiceManager.DoVoice(playerSender, playerMessage,
                        config.Characters[_clientState.LocalPlayer.Name.TextValue],
                        type == XivChatType.CustomEmote,
                        config.PlayerCharacterVolume,
                        _clientState.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                        _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerTts);
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
                    if (config.UsePlayerSync) {
                        PlayerCharacter player = (PlayerCharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                        if (CombinedWhitelist().Contains(playerSender)) {
                            Task.Run(async () => {
                                string value = await _roleplayingVoiceManager.
                                GetSound(playerSender, playerMessage, audioFocus ?
                                config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                _clientState.LocalPlayer.Position, isShoutYell, @"\Incoming\");
                                _audioManager.PlayAudio(new AudioGameObject(player), value, SoundType.OtherPlayerTts);
                            });
                        }
                        if (type == XivChatType.Yell) {
                            if (config.TuneIntoTwitchStreams) {
                                if (!twitchSetCooldown.IsRunning || twitchSetCooldown.ElapsedMilliseconds > 30000) {
                                    var strings = message.TextValue.Split(' ');
                                    foreach (string value in strings) {
                                        if (value.Contains("twitch.tv")) {
                                            if (lastStreamURL != value) {
                                                Task.Run(async () => {
                                                    string streamURL = TwitchFeedManager.GetServerResponse(value);
                                                    _audioManager.PlayStream(new AudioGameObject(player), streamURL, SoundType.Livestream);
                                                    lastStreamURL = value;
                                                });
                                                twitchWasPlaying = true;
                                            }
                                            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
                                            twitchSetCooldown.Stop();
                                            twitchSetCooldown.Reset();
                                            twitchSetCooldown.Start();
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
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
                    if (_roleplayingVoiceManager == null ||
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
            _roleplayingVoiceManager = new RoleplayingVoiceManager(config.ApiKey, config.CacheFolder, _networkedClient, config.CharacterVoices);
            _roleplayingVoiceManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
            window.Manager = _roleplayingVoiceManager;
        }
        private void UiBuilder_Draw() {
            this.windowSystem.Draw();
        }

        [Command("/rpvoice")]
        [HelpMessage("OpenConfig")]
        public void OpenConfig(string command, string args) {
            string[] splitArgs = args.Split(' ');
            if (splitArgs.Length > 0) {
                switch (splitArgs[0].ToLower()) {
                    case "on":
                        config.IsActive = true;
                        window.Configuration = config;
                        this.pluginInterface.SavePluginConfig(config);
                        break;
                    case "off":
                        config.IsActive = false;
                        window.Configuration = config;
                        this.pluginInterface.SavePluginConfig(config);
                        break;
                    case "reload":
                        AttemptConnection();
                        break;
                    default:
                        if (config.IsActive) {
                            window.RefreshVoices();
                        }
                        window.Toggle();
                        break;
                }
            }
        }


        #region IDisposable Support
        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            disposed = true;
            config.Save();
            this.pluginInterface.SavePluginConfig(this.config);
            config.OnConfigurationChanged -= Config_OnConfigurationChanged;
            _chat.ChatMessage -= Chat_ChatMessage;
            this.pluginInterface.UiBuilder.Draw -= UiBuilder_Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi -= UiBuilder_OpenConfigUi;
            try {
                _audioManager.OnNewAudioTriggered -= _audioManager_OnNewAudioTriggered;
            } catch {
            }
            _emoteReaderHook.OnEmote -= (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
            try {
                _clientState.Login -= _clientState_Login;
                _clientState.Logout -= _clientState_Logout;
                _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
                _clientState.LeavePvP -= _clientState_LeavePvP;
            } catch {

            }
            _toast.ErrorToast -= _toast_ErrorToast;
            try {
                _framework.Update -= framework_Update;
            } catch {

            }
            this.windowSystem.RemoveAllWindows();
            Ipc.ModSettingChanged.Subscriber(pluginInterface).Event -= modSettingChanged;
            _networkedClient?.Dispose();
            _audioManager?.Dispose();
            _filter?.Dispose();
            this.commandManager?.Dispose();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
