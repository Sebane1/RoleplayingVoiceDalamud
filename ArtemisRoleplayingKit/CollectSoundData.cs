using Dalamud.Plugin;
using RoleplayingVoiceDalamud.VoiceSorting;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Collect Sound Data
        public async void RefreshData(bool skipModelData = false, bool skipPenumbraScan = false) {
            if (!disposed) {
                if (!_blockDataRefreshes) {
                    if (config.PlayerVoiceEngine == 1) {
                        if (_roleplayingMediaManager != null) {
                            _roleplayingMediaManager?.InitializeXTTS();
                        }
                    }
                    _catalogueWindow.CataloguePath = Path.Combine(config.CacheFolder, "ClothingCatalogue\\");
                    _ = Task.Run(async () => {
                        try {
                            if (penumbraSoundPacks == null || penumbraSoundPacks.Count == 0 || !skipPenumbraScan) {
                                penumbraSoundPacks = await GetPrioritySortedModPacks(skipModelData);
                            }
                            combinedSoundList = await GetCombinedSoundList(penumbraSoundPacks);
                            IpcSystem?.InvokeOnVoicePackChanged();
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        } catch (Exception e) {
                            Plugin.PluginLog.Error(e.Message);
                        }
                    });
                    if (!config.VoicePackIsActive && config.VoiceReplacementType == 0) {
                        try {
                            if (Filter != null) {
                                Filter.Muted = false;
                                voiceMuted = false;
                                RefreshPlayerVoiceMuted();
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog.Error(e.Message);
                        }
                    }
                    if (_clientState.LocalPlayer != null && _clientState.IsLoggedIn) {
                        SendNetworkedVoice();
                    }
                    //if (_npcVoiceManager != null && config != null) {
                    //    _npcVoiceManager.UseCustomRelayServer = config.UseCustomDialogueRelayServer;
                    //}
                }
            }
        }
        public async Task<ArtemisVoiceMod> GetCombinedSoundList(List<ArtemisVoiceMod> soundMods) {
            ArtemisVoiceMod list = new ArtemisVoiceMod();
            Dictionary<string, bool> keyValuePairs = new Dictionary<string, bool>();
            Dictionary<string, ArtemisVoiceMod> subMods = new Dictionary<string, ArtemisVoiceMod>();

            foreach (var soundMod in soundMods) {
                foreach (string value in soundMod.Files) {
                    string strippedValue = CharacterVoicePack.StripNonCharacters(Path.GetFileNameWithoutExtension(value), _clientState.ClientLanguage);
                    bool allowedToAdd;
                    if (keyValuePairs.ContainsKey(strippedValue)) {
                        allowedToAdd = !keyValuePairs[strippedValue];
                    } else {
                        keyValuePairs[strippedValue] = false;
                        allowedToAdd = true;
                    }
                    if (allowedToAdd) {
                        list.Files.Add(value);
                    }
                }
                foreach (string value in keyValuePairs.Keys) {
                    if (!string.IsNullOrEmpty(value)) {
                        try {
                            keyValuePairs[value] = true;
                        } catch {

                        }
                    }
                }
                Dictionary<string, bool> alreadyPairedSounds = new Dictionary<string, bool>();
                foreach (var item in soundMod.ArtemisSubMods) {
                    if (!subMods.ContainsKey(item.Name)) {
                        subMods[item.Name] = new ArtemisVoiceMod();
                    }
                    foreach (string value in soundMod.Files) {
                        string strippedValue = CharacterVoicePack.StripNonCharacters(Path.GetFileNameWithoutExtension(value), _clientState.ClientLanguage);
                        bool allowedToAdd;
                        if (alreadyPairedSounds.ContainsKey(strippedValue)) {
                            allowedToAdd = !alreadyPairedSounds[strippedValue];
                        } else {
                            alreadyPairedSounds[strippedValue] = false;
                            allowedToAdd = true;
                        }
                        if (allowedToAdd) {
                            subMods[item.Name].Files.Add(value);
                        }
                    }
                    foreach (string value in alreadyPairedSounds.Keys) {
                        if (!string.IsNullOrEmpty(value)) {
                            try {
                                alreadyPairedSounds[value] = true;
                            } catch {

                            }
                        }
                    }
                }
            }
            _ = Task.Run(async () => {
                if (list != null) {
                    while (staging) {
                        Thread.Sleep(1000);
                    }
                    staging = true;
                    if (_clientState.LocalPlayer != null) {
                        stagingPath = config.CacheFolder + @"\Staging\" + _clientState.LocalPlayer.Name.TextValue;
                        if (Directory.Exists(config.CacheFolder + @"\Staging")) {
                            foreach (string file in Directory.EnumerateFiles(config.CacheFolder + @"\Staging")) {
                                try {
                                    File.Delete(file);
                                } catch (Exception e) {
                                    //Plugin.PluginLog.Warning(e, e.Message);
                                }
                            }
                        }
                        try {
                            Directory.CreateDirectory(stagingPath);
                        } catch {
                            _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                        }
                        if (Directory.Exists(stagingPath)) {
                            foreach (string file in Directory.EnumerateFiles(stagingPath)) {
                                try {
                                    File.Delete(file);
                                } catch (Exception e) {
                                }
                            }
                        }
                        foreach (var sound in list.Files) {
                            try {
                                File.Copy(sound, Path.Combine(stagingPath, Path.GetFileName(sound)), true);
                            } catch (Exception e) {
                            }
                        }
                    }
                    staging = false;
                }
            });
            return list;
        }
        #endregion
    }
}
