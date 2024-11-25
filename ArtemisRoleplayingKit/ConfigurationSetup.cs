using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using RoleplayingMediaCore;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Configuration
        private Configuration GetConfig() {
            string currentConfig = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                               + @"\XIVLauncher\pluginConfigs\RoleplayingVoiceDalamud.json";
            if (File.Exists(currentConfig)) {
                return JsonConvert.DeserializeObject<Configuration>(
                    File.OpenText(currentConfig).ReadToEnd());
            }
            return new Configuration(this.pluginInterface);
        }
        private void _roleplayingVoiceManager_VoicesUpdated(object sender, EventArgs e) {
            config.CharacterVoices = _roleplayingMediaManager.CharacterVoices;
            config.Save();
            pluginInterface.SavePluginConfig(config);
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
                    _voiceEditor.MediaManager = _mediaManager;
                    _mediaManager.OnErrorReceived += _mediaManager_OnErrorReceived;
                    _videoWindow.MediaManager = _mediaManager;
                }
                if (_speechToTextManager == null || forceNewAssignments) {
                    _speechToTextManager = new SpeechToTextManager(Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName));
                    _speechToTextManager.RecordingFinished += _speechToTextManager_RecordingFinished;
                }
            }
        }

        private void _speechToTextManager_RecordingFinished(object sender, EventArgs e) {
            if (!_speechToTextManager.RpMode) {
                _messageQueue.Enqueue(_speechToTextManager.FinalText);
            } else {
                _messageQueue.Enqueue(@"/em says " + "\"" + _speechToTextManager.FinalText + "\"");
            }
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
                RefreshData();
            }
        }
        public void InitialzeManager() {
            if (_roleplayingMediaManager == null) {
                _roleplayingMediaManager = new RoleplayingMediaManager(config.ApiKey, config.CacheFolder, _networkedClient, config.CharacterVoices, _roleplayingMediaManager_InitializationStatus);
                _roleplayingMediaManager.BasePath = Path.GetDirectoryName(pluginInterface.AssemblyLocation.FullName);
                _roleplayingMediaManager.XTTSStatus += _roleplayingMediaManager_XTTSStatus;
                _roleplayingMediaManager.VoicesUpdated += _roleplayingVoiceManager_VoicesUpdated;
                _roleplayingMediaManager.OnVoiceFailed += _roleplayingMediaManager_OnVoiceFailed;
                Window.Manager = _roleplayingMediaManager;
                if (config.PlayerVoiceEngine == 1) {
                    _roleplayingMediaManager.InitializeXTTS();
                }
            }
            Window?.RefreshVoices();
        }
        private void _roleplayingMediaManager_InitializationStatus(object sender, string e) {
            try {
                if (_clientState.LocalPlayer != null) {
                    if (_chat != null) {
                        _chat.Print(e);
                    }
                }
            } catch (Exception error) {
                if (PluginLog != null) {
                    PluginLog.Warning(error, error.Message);
                }
            }
        }
        private void _roleplayingMediaManager_XTTSStatus(object sender, string e) {
            if (PluginLog != null) {
                PluginLog.Verbose(e);
            }
        }

        private void _roleplayingMediaManager_OnVoiceFailed(object sender, VoiceFailure e) {
            Plugin.PluginLog.Error(e.Exception, e.Exception.Message);
        }

        private void modSettingChanged(ModSettingChange arg1, Guid arg2, string arg3, bool arg4) {
            RefreshData();
        }
        #endregion
    }
}
