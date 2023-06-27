using Dalamud.Configuration;
using Dalamud.Plugin;
using RoleplayingVoiceCore.AudioRecycler;
using System;

namespace RoleplayingVoice {
    public class Configuration : IPluginConfiguration {
        public event EventHandler OnConfigurationChanged;
        private string connectionIP = "50.70.229.19";
        int IPluginConfiguration.Version { get; set; }

        #region Saved configuration values
        public string ConnectionIP { get => connectionIP; set => connectionIP = value; }
        public string CharacterName { get; set; }
        public string ApiKey { get; set; }
        public string CharacterVoice { get; set; }
        public bool IsActive { get; set; }

        public CharacterVoices CharacterVoices { get; set; }
        #endregion

        private readonly DalamudPluginInterface pluginInterface;

        public Configuration(DalamudPluginInterface pi) {
            this.pluginInterface = pi;
        }

        public void Save() {
            if (this.pluginInterface != null) {
                this.pluginInterface.SavePluginConfig(this);
            }
            OnConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
