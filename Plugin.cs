using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVLooseTextureCompiler.Networking;
using RoleplayingVoice.Attributes;
using RoleplayingVoiceCore;
using System;
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

            var window = this.pluginInterface.Create<PluginWindow>();
            window.Configuration = this.config;
            window.PluginInteface = this.pluginInterface;

            if (window is not null) {
                this.windowSystem.AddWindow(window);
            }

            this.pluginInterface.UiBuilder.Draw += UiBuilder_Draw;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
            config.OnConfigurationChanged += Config_OnConfigurationChanged;
            chat.ChatMessage += Chat_ChatMessage;
            if (_networkedClient == null) {
                _networkedClient = new NetworkedClient(config.ConnectionIP);
            }
            if (!string.IsNullOrEmpty(config.ApiKey)) {
                _roleplayingVoiceManager = new RoleplayingVoiceManager(config.ApiKey, _networkedClient);
            }
        }

        private void Chat_ChatMessage(Dalamud.Game.Text.XivChatType type, uint senderId,
            ref Dalamud.Game.Text.SeStringHandling.SeString sender,
            ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled) {
            if (!_networkedClient.Connected) {
                _networkedClient.Start();
            }
            if (_roleplayingVoiceManager != null) {
                if (!string.IsNullOrEmpty(config.CharacterName)) {
                    if (sender.TextValue.Contains(config.CharacterName)) {
                        string playerSender = sender.TextValue;
                        string playerMessage = message.TextValue;
                        Task.Run(() => _roleplayingVoiceManager.DoVoice(playerSender, playerMessage, config.CharacterVoice));
                    } else {
                        string playerSender = sender.TextValue;
                        string playerMessage = message.TextValue;
                        Task.Run(() => _roleplayingVoiceManager.GetVoice(playerSender, playerMessage));
                    }
                }
            }
        }
        private void Config_OnConfigurationChanged(object sender, EventArgs e) {
            if (config != null) {
                _roleplayingVoiceManager = new RoleplayingVoiceManager(config.ApiKey, _networkedClient);
            }
        }

        private void UiBuilder_Draw() {
            this.windowSystem.Draw();
        }

        [Command("/rpvoice")]
        [HelpMessage("OpenConfig")]
        public void OpenConfig(string command, string args) {
            var window = this.pluginInterface.Create<PluginWindow>();
            window.Configuration = this.config;
            window.PluginInteface = this.pluginInterface;

            if (window is not null) {
                this.windowSystem.AddWindow(window);
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
