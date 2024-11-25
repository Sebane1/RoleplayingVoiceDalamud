using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Sound Sync
        private void CheckForDownloadCancellation() {
            Task.Run(delegate {
                if (_maxDownloadLengthTimer.ElapsedMilliseconds > 30000) {
                    isDownloadingZip = false;
                    _maxDownloadLengthTimer.Reset();
                }
            });
        }
        List<string> GetCombinedWhitelist() {
            List<string> list = new List<string>();
            list.AddRange(config.Whitelist);
            list.AddRange(temporaryWhitelist);
            return list;
        }
        #endregion
    }
}
