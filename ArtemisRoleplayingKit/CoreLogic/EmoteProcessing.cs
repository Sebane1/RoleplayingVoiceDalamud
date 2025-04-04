using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game;
using Dalamud.Plugin;
using FFXIVVoicePackCreator.VoiceSorting;
using FFXIVVoicePackCreator;
using Lumina.Excel.Sheets;
using NAudio.Wave.SampleProviders;
using RoleplayingVoiceDalamud.Catalogue;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using RoleplayingMediaCore;
using RoleplayingVoiceDalamudWrapper;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {

        #region Emote Processing
        private async Task<bool> CheckEmoteExistsInDirectory(string path, string emoteString) {
            if (Directory.Exists(path)) {
                foreach (string file in Directory.EnumerateFiles(path)) {
                    if (file.ToLower().Contains(emoteString.ToLower())) {
                        return true;
                    }
                }
            }
            return false;
        }
        private async void ReceivingEmote(ICharacter instigator, ushort emoteId) {
            if (instigator != null) {
                try {
                    string[] senderStrings = SplitCamelCase(
                    RemoveActionPhrases(RemoveSpecialSymbols(instigator.Name.TextValue))).Split(' ');
                    bool isShoutYell = false;
                    if (senderStrings.Length > 2) {
                        int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                        string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                        string path = config.CacheFolder + @"\VoicePack\Others";
                        try {
                            Directory.CreateDirectory(path);
                        } catch {
                            _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                        }
                        string hash = RoleplayingMediaCore.RoleplayingMediaManager.Shai1Hash(playerSender);
                        string clipPath = path + @"\" + hash;
                        try {
                            if (config.UsePlayerSync) {
                                if (GetCombinedWhitelist().Contains(playerSender)) {
                                    SetNetworkedVoice(playerSender, instigator);
                                    if (!isDownloadingZip) {
                                        if (!Path.Exists(clipPath) || !(await CheckEmoteExistsInDirectory(clipPath, GetEmoteName(emoteId)))) {
                                            if (Path.Exists(clipPath)) {
                                                RemoveFiles(clipPath);
                                            }
                                            if (_characterVoicePacks.ContainsKey(playerSender)) {
                                                _characterVoicePacks.Remove(playerSender);
                                            }
                                            isDownloadingZip = true;
                                            _maxDownloadLengthTimer.Restart();
                                            await Task.Run(async () => {
                                                try {
                                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                                } catch { }
                                                isDownloadingZip = false;
                                            });
                                        }
                                        await Task.Run(async () => {
                                            Stopwatch copyTimer = new Stopwatch();
                                            copyTimer.Start();
                                            while (isDownloadingZip) {
                                                Thread.Sleep(100);
                                            }
                                            if (Directory.Exists(path)) {
                                                CharacterVoicePack characterVoicePack = null;
                                                if (!_characterVoicePacks.ContainsKey(playerSender)) {
                                                    characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                                                } else {
                                                    characterVoicePack = _characterVoicePacks[playerSender];
                                                }
                                                bool isVoicedEmote = false;
                                                string value = GetEmotePath(characterVoicePack, emoteId, (int)copyTimer.Elapsed.TotalSeconds, out isVoicedEmote);
                                                if (!string.IsNullOrEmpty(value)) {
                                                    string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                                                    TimeCodeData data = RaceVoice.TimeCodeData[PenumbraAndGlamourerHelperFunctions.GetRace(instigator) + "_" + gender];
                                                    copyTimer.Stop();
                                                    bool lipWasSynced = false;
                                                    _mediaManager.PlayMedia(new MediaGameObject(instigator), value, SoundType.OtherPlayer, false,
                                                     characterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000.0 * data.TimeCodes[characterVoicePack.EmoteIndex]) : 0, copyTimer.Elapsed, delegate {
                                                         Task.Run(delegate {
                                                             _addonTalkHandler.StopLipSync(instigator);
                                                         });
                                                     }, delegate (object sender, StreamVolumeEventArgs e) {
                                                         Task.Run(delegate {
                                                             if (e.MaxSampleValues.Length > 0) {
                                                                 if (e.MaxSampleValues[0] > 0.2) {
                                                                     _addonTalkHandler.TriggerLipSync(instigator, 2);
                                                                     lipWasSynced = true;
                                                                 } else {
                                                                     _addonTalkHandler.StopLipSync(instigator);
                                                                 }
                                                             }
                                                         });
                                                     });
                                                    Task.Run(delegate {
                                                        Thread.Sleep((int)((decimal)1000m * data.TimeCodes[characterVoicePack.EmoteIndex]));
                                                        _addonTalkHandler.TriggerLipSync(instigator, 4);
                                                    });
                                                    if (isVoicedEmote) {
                                                        MuteVoiceCheck(6000);
                                                    }
                                                } else {
                                                    _mediaManager.StopAudio(new MediaGameObject(instigator));
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + e.Message);
                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + e.Message);
                }
            }
        }
        private void SendingEmote(ICharacter instigator, ushort emoteId) {
            if (config.CharacterVoicePacks.ContainsKey(instigator.Name.TextValue)) {
                _voice = config.CharacterVoicePacks[instigator.Name.TextValue];
                _voicePackPath = config.CacheFolder + @"\VoicePack\" + _voice;
                _voicePackStaging = config.CacheFolder + @"\Staging\" + instigator.Name.TextValue;
                Task.Run(delegate {
                    if (_mainCharacterVoicePack == null) {
                        _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                    }
                    bool isVoicedEmote = false;
                    _lastEmoteUsed = GetEmoteName(emoteId);
                    string emotePath = GetEmotePath(_mainCharacterVoicePack, emoteId, 0, out isVoicedEmote);
                    if (!string.IsNullOrEmpty(emotePath)) {
                        string gender = instigator.Customize[(int)CustomizeIndex.Gender] == 0 ? "Masculine" : "Feminine";
                        TimeCodeData data = RaceVoice.TimeCodeData[PenumbraAndGlamourerHelperFunctions.GetRace(instigator) + "_" + gender];
                        _mediaManager.StopAudio(new MediaGameObject(instigator));
                        bool lipWasSynced = false;
                        _mediaManager.PlayMedia(_playerObject, emotePath, SoundType.Emote, false,
                        _mainCharacterVoicePack.EmoteIndex > -1 ? (int)((decimal)1000m * data.TimeCodes[_mainCharacterVoicePack.EmoteIndex]) : 0, default, delegate {
                            Task.Run(delegate {
                                _addonTalkHandler.StopLipSync(instigator);
                            });
                        },
                        delegate (object sender, StreamVolumeEventArgs e) {
                            Task.Run(delegate {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(instigator, 2);
                                        lipWasSynced = true;
                                    } else {
                                        _addonTalkHandler.StopLipSync(instigator);
                                    }
                                }
                            });
                        });
                        Task.Run(delegate {
                            if (_mainCharacterVoicePack.EmoteIndex > -1) {
                                Thread.Sleep((int)((decimal)1000m * data.TimeCodes[_mainCharacterVoicePack.EmoteIndex]));
                            }
                            _addonTalkHandler.TriggerLipSync(instigator, 2);
                        });
                        if (isVoicedEmote) {
                            MuteVoiceCheck(10000);
                        }
                    } else {
                        if (!_inGameSoundStartedAudio) {
                            _mediaManager.StopAudio(new MediaGameObject(instigator));
                        }
                        _inGameSoundStartedAudio = false;
                    }
                    if (config.UsePlayerSync) {
                        Task.Run(async () => {
                            bool success = await _roleplayingMediaManager.SendZip(_threadSafeObjectTable.LocalPlayer.Name.TextValue, _voicePackStaging);
                        });
                    }
                });
            }
        }
        private string GetEmoteName(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            if (!string.IsNullOrEmpty(emote.Name.ToString())) {
                return CleanSenderName(emote.Name.ToString()).Replace(" ", "").ToLower();
            } else {
                return "";
            }
        }
        private Emote GetEmoteData(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            if (!string.IsNullOrEmpty(emote.Name.ToString())) {
                return emote;
            } else {
                return new Emote();
            }
        }
        private string GetEmoteCommand(ushort emoteId) {
            Emote emote = _dataManager.GetExcelSheet<Emote>().GetRow(emoteId);
            return CleanSenderName(emote.TextCommand.Value.Command.ToString()).Replace(" ", "").ToLower();
        }
        private string GetEmotePath(CharacterVoicePack characterVoicePack, ushort emoteId, int delay, out bool isVoicedEmote) {
            Emote emoteEnglish = _dataManager.GetExcelSheet<Emote>(ClientLanguage.English).GetRow(emoteId);
            Emote emoteFrench = _dataManager.GetExcelSheet<Emote>(ClientLanguage.French).GetRow(emoteId);
            Emote emoteGerman = _dataManager.GetExcelSheet<Emote>(ClientLanguage.German).GetRow(emoteId);
            Emote emoteJapanese = _dataManager.GetExcelSheet<Emote>(ClientLanguage.Japanese).GetRow(emoteId);

            string emotePathId = characterVoicePack.GetMisc(emoteId.ToString(), delay, true);
            string emotePathEnglish = characterVoicePack.GetMisc(emoteEnglish.Name.ToString(), delay, true);
            string emotePathFrench = characterVoicePack.GetMisc(emoteFrench.Name.ToString(), delay, true);
            string emotePathGerman = characterVoicePack.GetMisc(emoteGerman.Name.ToString(), delay, true);
            string emotePathJapanese = characterVoicePack.GetMisc(emoteJapanese.Name.ToString(), delay, true);

            characterVoicePack.EmoteIndex = -1;
            isVoicedEmote = true;
            switch (emoteId) {
                case 1:
                    characterVoicePack.EmoteIndex = 0;
                    break;
                case 2:
                    characterVoicePack.EmoteIndex = 1;
                    break;
                case 3:
                    characterVoicePack.EmoteIndex = 2;
                    break;
                case 6:
                    characterVoicePack.EmoteIndex = 3;
                    break;
                case 13:
                    characterVoicePack.EmoteIndex = 4;
                    break;
                case 14:
                    characterVoicePack.EmoteIndex = 5;
                    break;
                case 17:
                    characterVoicePack.EmoteIndex = 6;
                    break;
                case 20:
                    characterVoicePack.EmoteIndex = 7;
                    break;
                case 21:
                    characterVoicePack.EmoteIndex = 8;
                    break;
                case 24:
                    characterVoicePack.EmoteIndex = 9;
                    break;
                case 37:
                    characterVoicePack.EmoteIndex = 10;
                    break;
                case 40:
                    characterVoicePack.EmoteIndex = 11;
                    break;
                case 42:
                    characterVoicePack.EmoteIndex = 12;
                    break;
                case 48:
                    characterVoicePack.EmoteIndex = 13;
                    break;
                default:
                    isVoicedEmote = false;
                    break;
            }

            if (!string.IsNullOrEmpty(emotePathId)) {
                return emotePathId;
            } else if (!string.IsNullOrEmpty(emotePathEnglish)) {
                return emotePathEnglish;
            } else if (!string.IsNullOrEmpty(emotePathFrench)) {
                return emotePathFrench;
            } else if (!string.IsNullOrEmpty(emotePathGerman)) {
                return emotePathGerman;
            } else if (!string.IsNullOrEmpty(emotePathJapanese)) {
                return emotePathJapanese;
            }
            return string.Empty;
        }

        #endregion Emote Processing
    }
}
