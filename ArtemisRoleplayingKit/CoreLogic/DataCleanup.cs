using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Data Cleanup
        public void RemoveFiles(string path) {
            try {
                Directory.Delete(path, true);
            } catch {
                if (Directory.Exists(path)) {
                    foreach (string file in Directory.EnumerateFiles(path)) {
                        try {
                            File.Delete(file);
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }
                }
            }
        }
        private void gameObjectRedrawn(nint arg1, int arg2) {
            if (!disposed) {
                if (_objectTableThreadUnsafe != null) {
                    if (!_redrawCooldown.IsRunning) {
                        _redrawCooldown.Start();
                        redrawObjectCount = _objectTableThreadUnsafe.Count<IGameObject>();
                    }
                    if (_redrawCooldown.IsRunning) {
                        objectsRedrawn++;
                    }
                    try {
                        if (_objectTableThreadUnsafe.Length > 0 && arg2 < _objectTableThreadUnsafe.Length && arg2 > -1) {
                            ICharacter character = _objectTableThreadUnsafe[arg2] as ICharacter;
                            if (character != null) {
                                string senderName = CleanSenderName(character.Name.TextValue);
                                string path = config.CacheFolder + @"\VoicePack\Others";
                                string hash = RoleplayingMediaCore.RoleplayingMediaManager.Shai1Hash(senderName);
                                string clipPath = path + @"\" + hash;
                                if (!temporaryWhitelist.Contains(senderName) && config.IgnoreWhitelist &&
                                     !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                                    temporaryWhitelistQueue.Enqueue(senderName);
                                }
                                if (GetCombinedWhitelist().Contains(senderName) &&
                                    !_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                                    if (Directory.Exists(clipPath)) {
                                        try {
                                            RemoveFiles(clipPath);
                                            if (_characterVoicePacks.ContainsKey(senderName)) {
                                                _characterVoicePacks.Remove(senderName);
                                            }
                                        } catch (Exception e) {
                                            Plugin.PluginLog?.Warning(e, e.Message);
                                        }
                                    }
                                    SetNetworkedVoice(senderName, character);
                                } else if (_clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                                    RefreshData();
                                }
                                if (_wasDoingFakeEmote && _clientState.LocalPlayer.Name.TextValue.Contains(senderName)) {
                                    _addonTalkHandler.StopEmote(_clientState.LocalPlayer.Address);
                                    _wasDoingFakeEmote = false;
                                }
                            }
                        }
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
            }
        }

        private void SetNetworkedVoice(string senderName, ICharacter character) {
            Task.Run(async () => {
                if (config.UsePlayerSync) {
                    Thread.Sleep(2000);
                    var value = await _roleplayingMediaManager.GetShort(senderName + "vanilla voice" + _clientState.TerritoryType);
                    if (value != ushort.MaxValue - 1 && character != null) {
                        AddonTalkHandler.SetVanillaVoice(character, (byte)value);
                    }
                }
            });
        }

        private void _clientState_LeavePvP() {
            CleanSounds();
        }

        private void _clientState_TerritoryChanged(ushort e) {
            if (config.DebugMode) {
                _chat?.Print("Territory is " + e);
            }
            _videoWindow.IsOpen = false;
            CleanSounds();
            if (_recentCFPop > 0) {
                _recentCFPop++;
            }
            Task.Run(async () => {
                Thread.Sleep(5000);
                while (_clientState.LocalPlayer == null) {
                    Thread.Sleep(1000);
                }
                if (_clientState.LocalPlayer != null && _clientState.IsLoggedIn) {
                    Thread.Sleep(5000);
                    SendNetworkedVoice();
                }
            });
            if (config.UsePlayerSync) {
                Task.Run(async () => {
                    if (_clientState.LocalPlayer != null && _clientState.IsLoggedIn) {
                        string staging = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                        bool success = await _roleplayingMediaManager.SendZip(_clientState.LocalPlayer.Name.TextValue, staging);
                    }
                });
            }
        }

        private void SendNetworkedVoice() {
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
                if (config.VoicePackIsActive) {
                    var voiceItem = AddonTalkHandler.VoiceList.ElementAt(config.ChosenVanillaReplacement);
                    if (AddonTalkHandler != null) {
                        if (config.VoiceReplacementType == 0) {
                            AddonTalkHandler?.SetVanillaVoice(_clientState.LocalPlayer, 0);
                        }
                        if (config.VoiceReplacementType == 1) {
                            AddonTalkHandler?.SetVanillaVoice(_clientState.LocalPlayer, voiceItem.Value);
                        }
                    }
                    AddonTalkHandler.SetVanillaVoice(_clientState.LocalPlayer, voiceItem.Value);
                    if (config.UsePlayerSync) {
                        string senderName = CleanSenderName(_clientState.LocalPlayer.Name.TextValue);
                        await _roleplayingMediaManager.SendShort(senderName + "vanilla voice" + _clientState.TerritoryType, voiceItem.Value);
                    }
                }
            });
        }

        private void CleanupEmoteWatchList() {
            foreach (var item in _emoteWatchList.Values) {
                try {
                    item.Dispose();
                } catch {

                }
            }
            _emoteWatchList.Clear();
            _preOccupiedWithEmoteCommand.Clear();
        }
        private unsafe bool IsResidential() {
            return HousingManager.Instance()->IsInside() || HousingManager.Instance()->OutdoorTerritory != null;
        }

        private void _clientState_Logout(int type, int code) {
            CleanSounds();
        }

        private void _clientState_Login() {
            CleanSounds();
            CheckDependancies(true);
            RefreshData();
        }
        public void CleanSounds() {
            Task.Run(async () => {
                _mountingOccured = false;
                string othersPath = config.CacheFolder + @"\VoicePack\Others";
                string incomingPath = config.CacheFolder + @"\Incoming\";
                if (_mediaManager != null) {
                    _mediaManager.CleanSounds();
                }
                ResetTwitchValues();
                temporaryWhitelist.Clear();
                if (Directory.Exists(othersPath)) {
                    try {
                        Directory.Delete(othersPath, true);
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
                if (Directory.Exists(incomingPath)) {
                    try {
                        Directory.Delete(incomingPath, true);
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
                CleanupEmoteWatchList();
            });
        }
        public void ResetTwitchValues() {
            Task.Run(async () => {
                Thread.Sleep(1000);
                while (Conditions.IsInBetweenAreas) {
                    Thread.Sleep(500);
                }
                _lastStreamObject = null;
                _streamURLs = null;
                if (streamWasPlaying) {
                    streamWasPlaying = false;
                    _videoWindow.IsOpen = false;
                    try {
                        _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
                potentialStream = "";
                lastStreamURL = "";
                _currentStreamer = "";
                _streamSetCooldown.Stop();
                _streamSetCooldown.Reset();
            });
        }
        #endregion
    }
}
