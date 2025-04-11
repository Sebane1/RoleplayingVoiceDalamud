using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        private bool _alreadySyncingVoice;
        #region Sound Sync
        private void SendNetworkedVoice() {
            if (!_alreadySyncingVoice) {
                _alreadySyncingVoice = true;
                Task.Run(async () => {
                    while (AddonTalkHandler == null) {
                        Thread.Sleep(1000);
                    }
                    while (AddonTalkHandler.VoiceList != null) {
                        Thread.Sleep(1000);
                    }
                    while (AddonTalkHandler.VoiceList.Count == 0) {
                        Thread.Sleep(1000);
                    }
                    var voiceItem = AddonTalkHandler.VoiceList.ElementAt(config.ChosenVanillaReplacement);
                    if (AddonTalkHandler != null) {
                        if (config.VoiceReplacementType == 0) {
                            AddonTalkHandler?.SetVanillaVoice(_threadSafeObjectTable.LocalPlayer, 0);
                        }
                        if (config.VoiceReplacementType == 1) {
                            AddonTalkHandler?.SetVanillaVoice(_threadSafeObjectTable.LocalPlayer, voiceItem.Value);
                        }
                    }
                    AddonTalkHandler.SetVanillaVoice(_threadSafeObjectTable.LocalPlayer, voiceItem.Value);
                    if (config.UsePlayerSync) {
                        string senderName = CleanSenderName(_threadSafeObjectTable.LocalPlayer.Name.TextValue);
                        await _roleplayingMediaManager.SendShort(senderName + "vanilla voice" + _clientState.TerritoryType, voiceItem.Value);
                    }
                    _alreadySyncingVoice = false;
                });
            }
        }

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
