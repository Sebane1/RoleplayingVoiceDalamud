using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ElevenLabs.Voices;
using FFXIVLooseTextureCompiler.Networking;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;
using PatMe;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private PlayerObject _playerObject;
        private AudioManager _audioManager;
        private ObjectTable _objectTable;

        public string Name => "Roleplaying Voice";

        public RoleplayingVoiceManager RoleplayingVoiceManager { get => _roleplayingVoiceManager; set => _roleplayingVoiceManager = value; }
        public NetworkedClient NetworkedClient { get => _networkedClient; set => _networkedClient = value; }

        public Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            ChatGui chat,
            ClientState clientState,
            SigScanner scanner,
            ObjectTable objectTable) {
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
            chat.ChatMessage += Chat_ChatMessage;
            cooldown = new Stopwatch();
            muteTimer = new Stopwatch();
            _realChat = new Chat(scanner);
            _emoteReaderHook = new EmoteReaderHooks(scanner, clientState, objectTable);
            _emoteReaderHook.OnEmote += (instigator, emoteId) => OnEmote(instigator as PlayerCharacter, emoteId);
            _objectTable = objectTable;
            _clientState.Login += _clientState_Login;
            CheckDepandancies();

        }

        private void _clientState_Login(object sender, EventArgs e) {
            CheckDepandancies(true);
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
            if (instigator.Name.TextValue == _clientState.LocalPlayer.Name.TextValue) {
                SendingEmote(instigator, emoteId);
            } else {
                Task.Run(() => ReceivingEmote(instigator, emoteId));
            }
        }

        private async void ReceivingEmote(PlayerCharacter instigator, ushort emoteId) {
            string path = @"\VoicePack\Other";
            try {
                string value = await _roleplayingVoiceManager.GetSound(_clientState.LocalPlayer.Name.TextValue, emoteId + "",
                                config.PlayerCharacterVolume * 0.6f, _clientState.LocalPlayer.Position, false, path, true);
                _audioManager.PlayAudio(_playerObject, value, SoundType.OtherPlayer);
            } catch {

            }
        }
        private void SendingEmote(PlayerCharacter instigator, ushort emoteId) {
            string voice = config.Characters[_clientState.LocalPlayer.Name.TextValue];
            string path = config.CacheFolder + @"\VoicePack\" + voice;
            if (Directory.Exists(path)) {
                CharacterVoicePack characterVoicePack = new CharacterVoicePack(voice, path);
                string value = "";
                switch (emoteId) {
                    case 1:
                        value = characterVoicePack.GetSurprised();
                        break;
                    case 2:
                        value = characterVoicePack.GetAngry();
                        break;
                    case 3:
                        value = characterVoicePack.GetFurious();
                        break;
                    case 6:
                        value = characterVoicePack.GetCheer();
                        break;
                    case 13:
                        value = characterVoicePack.GetDoze();
                        break;
                    case 14:
                        value = characterVoicePack.GetFume();
                        break;
                    case 17:
                        value = characterVoicePack.GetHuh();
                        break;
                    case 20:
                        value = characterVoicePack.GetChuckle();
                        break;
                    case 21:
                        value = characterVoicePack.GetLaugh();
                        break;
                    case 24:
                        value = characterVoicePack.GetNo();
                        break;
                    case 37:
                        value = characterVoicePack.GetStretch();
                        break;
                    case 40:
                        value = characterVoicePack.GetUpset();
                        break;
                    case 42:
                        value = characterVoicePack.GetYes();
                        break;
                    case 48:
                        value = characterVoicePack.GetHappy();
                        break;
                }
                if (string.IsNullOrEmpty(value)) {
                    value = characterVoicePack.GetMisc(emoteId + "");
                }
                if (!string.IsNullOrEmpty(value)) {
                    Task.Run(() => _roleplayingVoiceManager.SendSound(_clientState.LocalPlayer.Name.TextValue, emoteId + "",
                    value, config.PlayerCharacterVolume * 0.6f, _clientState.LocalPlayer.Position));
                    _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
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
                } else {
                    chat.Print("[Roleplaying Voice] No sound found for emote Id " + emoteId);
                }
            }
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
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            return rgx.Replace(value, "");
        }
        private void Chat_ChatMessage(XivChatType type, uint senderId,
            ref SeString sender, ref SeString message, ref bool isHandled) {
            CheckDepandancies();
            if (_roleplayingVoiceManager != null && !string.IsNullOrEmpty(config.ApiKey)) {
                if (stopwatch == null) {
                    stopwatch = new Stopwatch();
                    stopwatch.Start();
                }
                // Let the user be fully logged in before we start working.
                if (stopwatch.ElapsedMilliseconds > 5000) {
                    stopwatch.Stop();
                    switch (type) {
                        case XivChatType.Say:
                        case XivChatType.Shout:
                        case XivChatType.Yell:
                        case XivChatType.CustomEmote:
                        case XivChatType.Party:
                        case XivChatType.CrossParty:
                        case XivChatType.TellIncoming:
                        case XivChatType.TellOutgoing:
                            ChatText(sender, message, type, senderId);
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
                            BattleText(message, type);
                            break;
                    }
                }
            }
        }

        private void CheckDepandancies(bool forceNewAssignments = false) {
            if (_clientState.LocalPlayer != null) {
                if (_playerObject == null || forceNewAssignments) {
                    _playerObject = new PlayerObject(_clientState.LocalPlayer);
                }
                if (_audioManager == null || forceNewAssignments) {
                    _audioManager = new AudioManager(_playerObject);
                }
            }
        }

        private void BattleText(SeString message, XivChatType type) {
            CheckDepandancies();
            if (message.TextValue.ToLower().Contains("you")) {
                string value = "";
                string playerMessage = message.TextValue;
                string[] values = message.TextValue.Split(' ');
                string voice = config.Characters[_clientState.LocalPlayer.Name.TextValue];
                string path = config.CacheFolder + @"\VoicePack\" + voice;
                if (Directory.Exists(path)) {
                    CharacterVoicePack characterVoicePack = new CharacterVoicePack(voice, path);
                    if (type == (XivChatType)2729 ||
                        type == (XivChatType)2091) {
                        value = characterVoicePack.GetAction(message.TextValue);
                    } else if (type == (XivChatType)2234) {
                        value = characterVoicePack.GetDeath();
                    } else if (type == (XivChatType)2730) {
                        value = characterVoicePack.GetMissed();
                    } else if (type == (XivChatType)2219) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    } else if (type == (XivChatType)2731) {
                        value = characterVoicePack.GetCasting();
                    } else if (type == (XivChatType)2106) {
                        value = characterVoicePack.GetRevive();
                    } else if (type == (XivChatType)10409) {
                        value = characterVoicePack.GetHurt();
                    }
                }
                if (!string.IsNullOrEmpty(value)) {
                    string[] stringValues = MakeThirdPerson(RemoveActionPhrases(RemoveSpecialSymbols(playerMessage))).Split(' ');
                    string thirdPerson = stringValues[1] + " " + stringValues[stringValues.Length - 2] + stringValues[stringValues.Length - 1];
                    string debug = _clientState.LocalPlayer.Name.TextValue + " " + thirdPerson;
                    chat.Print(debug);
                    Task.Run(() => _roleplayingVoiceManager.SendSound(_clientState.LocalPlayer.Name.TextValue, thirdPerson,
                    value, config.PlayerCharacterVolume * 0.6f, _clientState.LocalPlayer.Position));
                    _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerVoice);
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
            } else {
                string[] senderStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue))).Split(' ');
                string[] messageStrings = RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue)).Split(' ');
                bool isShoutYell = false;
                if (senderStrings.Length > 2) {
                    int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                    string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                    string playerMessage = message.TextValue;
                    string debug = playerSender + " " + senderStrings[3 + offset] + " " + 
                    messageStrings[messageStrings.Length - 2] + messageStrings[messageStrings.Length - 1];
                    chat.Print(debug);
                    Task.Run(async () => {
                        string value = await _roleplayingVoiceManager.GetSound(playerSender, senderStrings[3 + offset] + " " +
                            messageStrings[messageStrings.Length - 2] + messageStrings[messageStrings.Length - 1], config.OtherCharacterVolume,
                        _clientState.LocalPlayer.Position, isShoutYell, @"\VoicePack\Others", true);
                        _audioManager.PlayAudio(_playerObject, value, SoundType.OtherPlayer);
                    });
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

        private void ChatText(SeString sender, SeString message, XivChatType type, uint senderId) {
            if (sender.TextValue.Contains(_clientState.LocalPlayer.Name.TextValue)) {
                if (config.IsActive) {
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
                        _clientState.LocalPlayer.Position, config.UseAggressiveSplicing);
                        _audioManager.PlayAudio(_playerObject, value, SoundType.MainPlayerTts);
                    });
                }
            } else {
                string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender.TextValue)).Split(" ");
                bool isShoutYell = false;
                if (senderStrings.Length > 2) {
                    string playerSender = senderStrings[0] + " " + senderStrings[2];
                    string playerMessage = message.TextValue;
                    bool audioFocus = false;
                    if (_clientState.LocalPlayer.TargetObject != null) {
                        if (_clientState.LocalPlayer.TargetObject.ObjectKind ==
                            Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player) {
                            audioFocus = _clientState.LocalPlayer.TargetObject.Name.TextValue == sender.TextValue
                                || type == XivChatType.Party
                                || type == XivChatType.CrossParty || isShoutYell;
                            isShoutYell = type == XivChatType.Shout
                                || type == XivChatType.Yell;
                        }
                    } else {
                        audioFocus = true;
                    }
                    Task.Run(async () => {
                        PlayerCharacter player = (PlayerCharacter)_objectTable.FirstOrDefault(x => x.OwnerId == senderId);
                        string value = await _roleplayingVoiceManager.
                        GetSound(playerSender, playerMessage, audioFocus ?
                        config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                        _clientState.LocalPlayer.Position, isShoutYell);
                        _audioManager.PlayAudio(new PlayerObject(player), value, SoundType.OtherPlayer);
                    });
                }
            }
        }

        private void Config_OnConfigurationChanged(object sender, EventArgs e) {
            if (config != null) {
                if (_roleplayingVoiceManager == null ||
                    config.ApiKey.All(c => char.IsAsciiLetterOrDigit(c))
                    && !string.IsNullOrEmpty(config.ApiKey)) {
                    InitialzeManager();
                }
                if (_networkedClient != null) {
                    _networkedClient.UpdateIPAddress(config.ConnectionIP);
                }
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
                    window.RefreshVoices();
                    window.Toggle();
                    break;
            }

        }


        #region IDisposable Support
        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;

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
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
