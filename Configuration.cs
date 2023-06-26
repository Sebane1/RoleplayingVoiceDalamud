using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace RoleplayingVoice {
    public class Configuration : IPluginConfiguration {
        public event EventHandler OnConfigurationChanged;
        int IPluginConfiguration.Version { get; set; }

        #region Saved configuration values
        public string CharacterName { get; set; }
        public string ApiKey { get; set; }
        public string CharacterVoice { get; internal set; }
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
