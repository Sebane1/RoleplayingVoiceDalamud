using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ElevenLabs.Voices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVLooseTextureCompiler.Networking;
using FFXIVVoicePackCreator;
using FFXIVVoicePackCreator.VoiceSorting;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;
using PatMe;
using Penumbra.Api;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XivCommon.Functions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoleplayingVoice {
    public class Plugin : IDalamudPlugin {
        private readonly DalamudPluginInterface pluginInterface;
        private readonly ChatGui chat;
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
        private Chat _realChat;
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

        public string Name => "Roleplaying Voice";

        public RoleplayingVoiceManager RoleplayingVoiceManager { get => _roleplayingVoiceManager; set => _roleplayingVoiceManager = value; }
        public NetworkedClient NetworkedClient { get => _networkedClient; set => _networkedClient = value; }

        public unsafe Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            ChatGui chat,
            ClientState clientState,
            SigScanner scanner,
            ObjectTable objectTable,
            ToastGui toast,
            DataManager dataManager) {
            this.pluginInterface = pi;
            this.chat = chat;
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
            this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
            config.OnConfigurationChanged += Config_OnConfigurationChanged;
            window.Toggle();
            this.chat.ChatMessage += Chat_ChatMessage;
            cooldown = new Stopwatch();
            muteTimer = new Stopwatch();
            _realChat = new Chat(scanner);
            _emoteReaderHook = new EmoteReaderHooks(scanner, clientState, objectTable);
            _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
            _objectTable = objectTable;
            _clientState.Login += _clientState_Login;
            _clientState.Logout += _clientState_Logout;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _clientState.LeavePvP += _clientState_LeavePvP;
            window.OnWindowOperationFailed += Window_OnWindowOperationFailed;
            _dataManager = dataManager;
            _toast = toast;
            _toast.ErrorToast += _toast_ErrorToast;
            _camera = CameraManager.Instance->GetActiveCamera();
            _audioCamera = new AudioCameraObject(_camera);
            RaceVoice.LoadRacialVoiceInfo();
            CheckDependancies();
            Ipc.ModSettingChanged.Subscriber(pluginInterface).Event += Plugin_Event;
            RefreshSoundData();
        }

        private void Plugin_Event(Penumbra.Api.Enums.ModSettingChange arg1, string arg2, string arg3, bool arg4) {
            RefreshSoundData();
        }
        public async void RefreshSoundData() {
            _ = Task.Run(async () => {
                penumbraSoundPacks = await GetPrioritySortedSoundPacks();
                combinedSoundList = await GetCombinedSoundList(penumbraSoundPacks);
            });
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
                    string path = config.CacheFolder + @"\Staging";
                    Directory.CreateDirectory(path);
                    foreach (var sound in list) {
                        File.Copy(sound, Path.Combine(path, Path.GetFileName(sound)), true);
                    }
                }
            });
            return list;
        }
        private void Window_OnWindowOperationFailed(object sender, PluginWindow.MessageEventArgs e) {
            chat.PrintError(e.Message);
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
        }
        public void CleanSounds() {
            string path = config.CacheFolder + @"\VoicePack\Others";
            if (_audioManager != null) {
                _audioManager.Dispose();
                _audioManager = null;
            }
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
            string[] senderStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(instigator.Name.TextValue))).Split(' ');
            bool isShoutYell = false;
            if (senderStrings.Length > 2) {
                int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                string path = config.CacheFolder + @"\VoicePack\Others";
                Directory.CreateDirectory(path);
                string hash = RoleplayingVoiceManager.Shai1Hash(playerSender);
                string clipPath = path + @"\" + hash;
                try {
                    if (config.UsePlayerSync) {
                        if (config.Whitelist.Contains(playerSender)) {
                            if (!isDownloadingZip) {
                                if (!Path.Exists(clipPath)) {
                                    isDownloadingZip = true;
                                    Task.Run(async () => {
                                        string value = await _roleplayingVoiceManager.GetZip(playerSender, path);
                                        isDownloadingZip = false;
                                    });
                                }
                            }
                            if (Directory.Exists(path)) {
                                CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                                string value = GetEmotePath(characterVoicePack, emoteId);
                                if (!string.IsNullOrEmpty(value)) {
                                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                                    TimeCodeData data = RaceVoice.TimeCodeData[instigator.Customize[(int)CustomizeIndex.Race] + "_" + gender];
                                    _audioManager.PlayAudio(new AudioGameObject(instigator), value, SoundType.OtherPlayer,
                                     characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0);
                                    MuteChecK();
                                } else {
                                    _audioManager.StopAudio(new AudioGameObject(instigator));
                                }
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
                string staging = config.CacheFolder + @"\Staging";
                CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                string value = GetEmotePath(characterVoicePack, emoteId);
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
                    MuteChecK(10000);
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
                if (config.CharacterVoicePacks.ContainsKey(_clientState.LocalPlayer.Name.TextValue)) {
                    string voice = config.CharacterVoicePacks[_clientState.LocalPlayer.Name.TextValue];
                    if (!string.IsNullOrEmpty(voice)) {
                        string path = config.CacheFolder + @"\VoicePack\" + voice;
                        list.Add(new KeyValuePair<List<string>, int>(Directory.EnumerateFiles(path).ToList(), list.Count));
                    }
                }
            }
            return list;
        }
        public void MuteChecK(int length = 4000) {
            if (!muteTimer.IsRunning) {
                _realChat.SendMessage("/voice");
                Task.Run(() => {
                    while (muteTimer.ElapsedMilliseconds < length) {
                        Thread.Sleep(4000);
                    }
                    _realChat.SendMessage("/voice");
                    muteTimer.Reset();
                });
            }
            muteTimer.Restart();
        }
        private string GetEmotePath(CharacterVoicePack characterVoicePack, ushort emoteId) {
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

        private void CheckDependancies(bool forceNewAssignments = false) {
            if (_clientState.LocalPlayer != null) {
                if (_playerObject == null || forceNewAssignments) {
                    _playerObject = new AudioGameObject(_clientState.LocalPlayer);
                }
                if (_audioManager == null || forceNewAssignments) {
                    _audioManager = new AudioManager(_playerObject, _audioCamera);
                    _audioManager.OnNewAudioTriggered += _audioManager_OnNewAudioTriggered;
                }
            }
        }

        private void _audioManager_OnNewAudioTriggered(object sender, EventArgs e) {
            _audioManager.MainPlayerVolume = config.PlayerCharacterVolume;
            _audioManager.OtherPlayerVolume = config.OtherCharacterVolume;
            _audioManager.UnfocusedPlayerVolume = config.UnfocusedCharacterVolume;
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
                        string staging = config.CacheFolder + @"\Staging";
                        bool attackIntended = false;
                        CharacterVoicePack characterVoicePack = new CharacterVoicePack(combinedSoundList);
                        if (!message.TextValue.Contains("cancel")) {
                            if (type == (XivChatType)2729 ||
                            type == (XivChatType)2091) {
                                if (attackCount == 3) {
                                    value = characterVoicePack.GetAction(message.TextValue);
                                    attackCount = 0;
                                } else {
                                    attackCount++;
                                    attackIntended = true;
                                }
                            } else if (type == (XivChatType)2234) {
                                value = characterVoicePack.GetDeath();
                            } else if (type == (XivChatType)2730) {
                                value = characterVoicePack.GetMissed();
                            } else if (type == (XivChatType)2219) {
                                if (message.TextValue.Contains("ready") ||
                                    message.TextValue.Contains("readies")) {
                                    value = characterVoicePack.GetReadying(message.TextValue);
                                    attackCount = 3;
                                    castingCount = 3;
                                } else {
                                    if (castingCount == 3) {
                                        value = characterVoicePack.GetCastingHeal();
                                        castingCount = 0;
                                    } else {
                                        castingCount++;
                                        attackIntended = true;
                                    }
                                }
                            } else if (type == (XivChatType)2731) {
                                if (message.TextValue.Contains("ready") ||
                                    message.TextValue.Contains("readies")) {
                                    value = characterVoicePack.GetReadying(message.TextValue);
                                    attackCount = 3;
                                    castingCount = 3;
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

                        if (!string.IsNullOrEmpty(value) || attackIntended) {
                            if (!attackIntended) {
                                _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
                            }
                            if (!muteTimer.IsRunning) {
                                _realChat.SendMessage("/voice");
                                Task.Run(() => {
                                    if (config.UsePlayerSync) {
                                        Task.Run(async () => {
                                            bool success = await _roleplayingVoiceManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                                        });
                                    }
                                    while (muteTimer.ElapsedMilliseconds < 4000) {
                                        Thread.Sleep(4000);
                                    }
                                    attackCount = 0;
                                    _realChat.SendMessage("/voice");
                                    muteTimer.Reset();
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
                    Directory.CreateDirectory(path);
                    if (config.UsePlayerSync) {
                        if (config.Whitelist.Contains(playerSender)) {
                            if (!isDownloadingZip) {
                                if (!Path.Exists(clipPath)) {
                                    isDownloadingZip = true;
                                    Task.Run(async () => {
                                        string value = await _roleplayingVoiceManager.GetZip(playerSender, path);
                                        isDownloadingZip = false;
                                    });
                                }
                            }
                            if (Path.Exists(clipPath)) {
                                CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath);
                                string value = "";
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
                                    _realChat.SendMessage("/voice");
                                    Task.Run(() => {
                                        while (muteTimer.ElapsedMilliseconds < 4000) {
                                            Thread.Sleep(4000);
                                        }
                                        _realChat.SendMessage("/voice");
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
                        if (config.Whitelist.Contains(playerSender)) {
                            Task.Run(async () => {
                                PlayerCharacter player = (PlayerCharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                                string value = await _roleplayingVoiceManager.
                                GetSound(playerSender, playerMessage, audioFocus ?
                                config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                _clientState.LocalPlayer.Position, isShoutYell);
                                _audioManager.PlayAudio(new AudioGameObject(player), value, SoundType.OtherPlayerTts);
                            });
                        }
                    }
                }
            }
        }

        public static string CleanSenderName(string senderName) {
            string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(senderName)).Split(" ");
            string playerSender = senderStrings.Length == 2 ?
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
            switch (args.ToLower()) {
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


        #region IDisposable Support
        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            disposed = true;
            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);
            config.OnConfigurationChanged -= Config_OnConfigurationChanged;
            chat.ChatMessage -= Chat_ChatMessage;
            this.pluginInterface.UiBuilder.Draw -= UiBuilder_Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi -= UiBuilder_OpenConfigUi;
            _emoteReaderHook.OnEmote -= (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
            this.windowSystem.RemoveAllWindows();
            _networkedClient.Dispose();
            _audioManager.Dispose();
            _clientState.Login -= _clientState_Login;
            _clientState.Logout -= _clientState_Logout;
            _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
            _clientState.LeavePvP -= _clientState_LeavePvP;
            _toast.ErrorToast -= _toast_ErrorToast;
            _audioManager.OnNewAudioTriggered -= _audioManager_OnNewAudioTriggered;
            Ipc.ModSettingChanged.Subscriber(pluginInterface).Event -= Plugin_Event;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
