using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVLooseTextureCompiler.Networking;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public class Plugin : IDalamudPlugin {
        private readonly DalamudPluginInterface pluginInterface;
        private readonly ChatGui chat;
        private readonly ClientState clientState;

        private readonly PluginCommandManager<Plugin> commandManager;
        private NetworkedClient _networkedClient;
        private readonly Configuration config;
        private readonly WindowSystem windowSystem;
        private PluginWindow window { get; init; }
        private RoleplayingVoiceManager _roleplayingVoiceManager;
        private Stopwatch stopwatch;

        public string Name => "Roleplaying Voice";

        public RoleplayingVoiceManager RoleplayingVoiceManager { get => _roleplayingVoiceManager; set => _roleplayingVoiceManager = value; }
        public NetworkedClient NetworkedClient { get => _networkedClient; set => _networkedClient = value; }

        public Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            ChatGui chat,
            ClientState clientState) {
            this.pluginInterface = pi;
            this.chat = chat;
            this.clientState = clientState;

            // Get or create a configuration object
            this.config = (Configuration)this.pluginInterface.GetPluginConfig()
                          ?? this.pluginInterface.Create<Configuration>();
            // Initialize the UI
            this.windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);

            window = this.pluginInterface.Create<PluginWindow>();
            window.ClientState = this.clientState;
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
        private void Chat_ChatMessage(Dalamud.Game.Text.XivChatType type, uint senderId,
            ref Dalamud.Game.Text.SeStringHandling.SeString sender,
            ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled) {
            if (_roleplayingVoiceManager != null && !string.IsNullOrEmpty(config.ApiKey)) {
                if (stopwatch == null) {
                    stopwatch = new Stopwatch();
                    stopwatch.Start();
                }

                // Let the user be fully logged in before we start working.
                if (stopwatch.ElapsedMilliseconds > 5000) {
                    stopwatch.Stop();
                    switch (type) {
                        case Dalamud.Game.Text.XivChatType.Say:
                        case Dalamud.Game.Text.XivChatType.Shout:
                        case Dalamud.Game.Text.XivChatType.Yell:
                        case Dalamud.Game.Text.XivChatType.CustomEmote:
                        case Dalamud.Game.Text.XivChatType.Party:
                        case Dalamud.Game.Text.XivChatType.CrossParty:
                        case Dalamud.Game.Text.XivChatType.TellIncoming:
                        case Dalamud.Game.Text.XivChatType.TellOutgoing:
                            if (sender.TextValue.Contains(clientState.LocalPlayer.Name.TextValue)) {
                                if (config.IsActive) {
                                    string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(
                                    clientState.LocalPlayer.Name.TextValue)).Split(" ");
                                    string playerSender = senderStrings.Length == 2 ? 
                                        (senderStrings[0] + " " + senderStrings[1]) : 
                                        (senderStrings[0] + " " + senderStrings[2]);
                                    string playerMessage = message.TextValue;
                                    Task.Run(() => _roleplayingVoiceManager.DoVoice(playerSender, playerMessage,
                                        config.Characters[clientState.LocalPlayer.Name.TextValue],
                                        type == Dalamud.Game.Text.XivChatType.CustomEmote,
                                        config.PlayerCharacterVolume,
                                        clientState.LocalPlayer.Position, config.UseAggressiveSplicing));
                                }
                            } else {
                                string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender.TextValue)).Split(" ");
                                bool isShoutYell = false;
                                if (senderStrings.Length > 2) {
                                    string playerSender = senderStrings[0] + " " + senderStrings[2];
                                    string playerMessage = message.TextValue;
                                    bool audioFocus = false;
                                    if (clientState.LocalPlayer.TargetObject != null) {
                                        if (clientState.LocalPlayer.TargetObject.ObjectKind ==
                                            Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player) {
                                            audioFocus = clientState.LocalPlayer.TargetObject.Name.TextValue == sender.TextValue
                                                || type == Dalamud.Game.Text.XivChatType.Party
                                                || type == Dalamud.Game.Text.XivChatType.CrossParty || isShoutYell;
                                            isShoutYell = type == Dalamud.Game.Text.XivChatType.Shout
                                                || type == Dalamud.Game.Text.XivChatType.Yell;
                                        }
                                    } else {
                                        audioFocus = true;
                                    }
                                    Task.Run(() => _roleplayingVoiceManager.
                                    GetVoice(playerSender, playerMessage, audioFocus ?
                                    config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                    clientState.LocalPlayer.Position, isShoutYell));
                                }
                            }
                            break;
                    }
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
            this.windowSystem.RemoveAllWindows();
            _networkedClient.Dispose();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
