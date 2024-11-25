using Dalamud.Plugin;
using FFXIVLooseTextureCompiler.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Connection Attempts
        private void Window_RequestingReconnect(object sender, EventArgs e) {
            AttemptConnection();
        }

        private void AttemptConnection() {
            if (_networkedClient != null) {
                _networkedClient.Dispose();
            }
            if (config != null) {
                _networkedClient = new NetworkedClient(config.ConnectionIP);
                _networkedClient.OnConnectionFailed += _networkedClient_OnConnectionFailed;
                if (_roleplayingMediaManager != null) {
                    _roleplayingMediaManager.NetworkedClient = _networkedClient;
                }
            }
        }

        private void _networkedClient_OnConnectionFailed(object sender, FailureMessage e) {
            Plugin.PluginLog.Error(e.Message);
        }
        #endregion
    }
}
