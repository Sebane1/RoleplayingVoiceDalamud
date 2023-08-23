using Dalamud.Configuration;
using Dalamud.Plugin;
using RoleplayingVoiceCore.AudioRecycler;
using System;
using System.Collections.Generic;
using System.IO;

namespace RoleplayingVoice {
    public class Configuration : IPluginConfiguration {
        public event EventHandler OnConfigurationChanged;
        private string connectionIP = "50.70.229.19";
        private float _playerCharacterVolume = 1;
        private float _otherCharacterVolume = 1;
        private float _unfocusedCharacterVolume = 0.5f;
        bool useAggressiveCaching = true;
        private string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPVoiceCache");
        private bool usePlayerSync;
        private bool ignoreWhitelist;
        private List<string> whitelist = new List<string>();
        private float _loopingSFXVolume = 1;

        int IPluginConfiguration.Version { get; set; }

        #region Saved configuration values
        public string ConnectionIP { get => connectionIP; set => connectionIP = value; }
        public string ApiKey { get; set; }
        public bool IsActive { get; set; }
        public bool VoicePackIsActive { get; set; }

        public CharacterVoices CharacterVoices { get; set; }

        public Dictionary<string, string> Characters { get; set; }
        public Dictionary<string, string> CharacterVoicePacks { get; set; }
        public float PlayerCharacterVolume { get => _playerCharacterVolume; set => _playerCharacterVolume = value; }
        public float OtherCharacterVolume { get => _otherCharacterVolume; set => _otherCharacterVolume = value; }
        public float UnfocusedCharacterVolume { get => _unfocusedCharacterVolume; set => _unfocusedCharacterVolume = value; }
        public bool UseAggressiveSplicing { get => useAggressiveCaching; set => useAggressiveCaching = value; }
        public bool UsePlayerSync { get => usePlayerSync; set => usePlayerSync = value; }
        public bool IgnoreWhitelist { get => ignoreWhitelist; set => ignoreWhitelist = value; }
        public string CacheFolder { get => cacheFolder; set => cacheFolder = value; }
        public List<string> Whitelist { get => whitelist; set => whitelist = value; }
        public float LoopingSFXVolume { get => _loopingSFXVolume; set => _loopingSFXVolume; }
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
