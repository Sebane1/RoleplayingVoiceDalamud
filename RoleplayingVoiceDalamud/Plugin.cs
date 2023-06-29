using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVLooseTextureCompiler.Networking;
using ImGuiNET;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using System;
using System.Numerics;
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
        public string Name => "Roleplaying Voice";

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
            window.Configuration = this.config;
            window.PluginInterface = this.pluginInterface;
            if (_networkedClient == null) {
                _networkedClient = new NetworkedClient(config.ConnectionIP);
            }
            if (!string.IsNullOrEmpty(config.ApiKey)) {
                _roleplayingVoiceManager = new RoleplayingVoiceManager(config.ApiKey, _networkedClient, config.CharacterVoices);
                _roleplayingVoiceManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
                window.Manager = _roleplayingVoiceManager;
            }

            if (window is not null) {
                this.windowSystem.AddWindow(window);
            }

            this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
            config.OnConfigurationChanged += Config_OnConfigurationChanged;
            chat.ChatMessage += Chat_ChatMessage;
            window.Toggle();
        }

        private void UiBuilder_OpenConfigUi() {
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
            if (!_networkedClient.Connected) {
                try {
                    _networkedClient.Start();
                } catch {

                }
            }
            if (_roleplayingVoiceManager != null) {
                if (!string.IsNullOrEmpty(config.CharacterName) && config.IsActive) {
                    switch (type) {
                        case Dalamud.Game.Text.XivChatType.Say:
                        case Dalamud.Game.Text.XivChatType.Shout:
                        case Dalamud.Game.Text.XivChatType.Yell:
                        case Dalamud.Game.Text.XivChatType.CustomEmote:
                        case Dalamud.Game.Text.XivChatType.FreeCompany:
                        case Dalamud.Game.Text.XivChatType.Party:
                        case Dalamud.Game.Text.XivChatType.CrossParty:
                        case Dalamud.Game.Text.XivChatType.TellIncoming:
                        case Dalamud.Game.Text.XivChatType.TellOutgoing:
                            if (sender.TextValue.Contains(config.CharacterName)) {
                                string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender.TextValue)).Split(" ");
                                string playerSender = senderStrings[0] + " " + senderStrings[2];
                                string playerMessage = message.TextValue;
                                Task.Run(() => _roleplayingVoiceManager.DoVoice(playerSender, playerMessage,
                                    config.CharacterVoice, type == Dalamud.Game.Text.XivChatType.CustomEmote));
                            } else {
                                string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender.TextValue)).Split(" ");
                                if (senderStrings.Length > 2) {
                                    string playerSender = senderStrings[0] + " " + senderStrings[2];
                                    string playerMessage = message.TextValue;
                                    Task.Run(() => _roleplayingVoiceManager.GetVoice(playerSender, playerMessage));
                                }
                            }
                            break;
                    }
                }
            }
        }
        private void Config_OnConfigurationChanged(object sender, EventArgs e) {
            if (config != null) {
                if (_roleplayingVoiceManager.ApiKey != config.ApiKey) {
                    _roleplayingVoiceManager = new RoleplayingVoiceManager(config.ApiKey, _networkedClient, config.CharacterVoices);
                    _roleplayingVoiceManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
                    window.Manager = _roleplayingVoiceManager;
                }
            }
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
                    this.pluginInterface.SavePluginConfig(config);
                    window.Configuration = config;
                    break;
                case "off":
                    config.IsActive = false;
                    this.pluginInterface.SavePluginConfig(config);
                    window.Configuration = config;
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
