using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using FFXIVVoicePackCreator.Json;
using Glamourer.Api.Enums;
using Newtonsoft.Json;
using RoleplayingVoiceDalamud.VoiceSorting;
using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        private bool _isBackingUpAnimations;

        public bool IsBackingUpAnimations { get => _isBackingUpAnimations; set => _isBackingUpAnimations = value; }
        #region Penumbra Parsing
        public void ExtractSCDOptions(Option option, string directory) {
            Task.Run(() => {
                if (option != null) {
                    foreach (var item in option.Files) {
                        if (item.Key.EndsWith(".scd")) {
                            _filter.Blacklist.Add(item.Key);
                            if (!_scdReplacements.ContainsKey(item.Key)) {
                                try {
                                    _scdReplacements.TryAdd(item.Key, directory + @"\" + item.Value);
                                    if (config.DebugMode) {
                                        Plugin.PluginLog?.Verbose("Found: " + item.Value);
                                    }
                                } catch {
                                    Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                                }
                            }
                        }
                    }
                }
            });
        }

        public void ExtractPapFiles(Option option, string directory, bool skipPap) {
            string modName = Path.GetFileName(directory);
            int papFilesFound = 0;
            for (int i = 0; i < option.Files.Count; i++) {
                var item = option.Files.ElementAt(i);
                if (item.Key.EndsWith(".pap")) {
                    string[] strings = item.Key.Split("/");
                    string value = strings[strings.Length - 1];
                    if (!_animationMods.ContainsKey(modName)) {
                        _animationMods[modName] = new KeyValuePair<string, List<string>>(directory, new());
                    }
                    if (!_animationMods[modName].Value.Contains(value)) {
                        _animationMods[modName].Value.Add(value);
                    }
                    papFilesFound++;
                    if (!_papSorting.ContainsKey(value)) {
                        try {
                            _papSorting.TryAdd(value, new List<Tuple<string, string, bool>>() { new Tuple<string, string, bool>(directory, modName, !skipPap) });

                            if (config.DebugMode) {
                                Plugin.PluginLog?.Verbose("Found: " + item.Value);
                            }
                        } catch {
                            Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                        }
                    } else {
                        _papSorting[value].Add(new Tuple<string, string, bool>(directory, modName, !skipPap));
                    }
                }
            }
        }
        public void BackupAnimations() {
            Task.Run(() => {
                _isBackingUpAnimations = true;
                string penumbraPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                string backupPath = Path.Combine(config.CacheFolder, "AnimationBackups");
                Directory.CreateDirectory(backupPath);
                foreach (var item in _animationMods.Values) {
                    string modPath = item.Key;
                    string backup = Path.Combine(backupPath, Path.GetFileNameWithoutExtension(item.Key + ".back"));
                    try {
                        if (Path.Exists(modPath)) {
                            CopyDirectory(modPath, backup, true);
                        }
                    } catch {

                    }
                }
                try {
                    SevenZipCompressor compressor = new SevenZipCompressor();
                    using (FileStream fileStream = new FileStream(backupPath + ".zip", FileMode.Create, FileAccess.Write)) {
                        compressor.CompressDirectory(backupPath, fileStream);
                    }
                } catch (Exception ex) {
                    Plugin.PluginLog.Warning(ex, ex.Message);
                }
                _isBackingUpAnimations = false;
            });
        }
        public void ImportBackup() {
            Task.Run(() => {
                _isBackingUpAnimations = true;
                string penumbraPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                string backupPath = Path.Combine(config.CacheFolder, "AnimationBackups");
                ZipFile.ExtractToDirectory(backupPath, penumbraPath);
                _isBackingUpAnimations = false;
            });
        }
        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive) {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles()) {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive) {
                foreach (DirectoryInfo subDir in dirs) {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        public void ExtractModFiles(Option option, string directory, bool skipPap) {
            string modName = Path.GetFileName(directory);
            int papFilesFound = 0;
            int mdlFilesFound = 0;

            for (int i = 0; i < option.Files.Count; i++) {
                var item = option.Files.ElementAt(i);
                try {
                    if (!string.IsNullOrEmpty(item.Key)) {
                        if (config.DebugMode) {
                            Plugin.PluginLog?.Verbose("Reading: " + item.Key);
                        }
                        if (item.Key.Trim().EndsWith(".pap")) {
                            string[] strings = item.Key.Split("/");
                            string value = strings[strings.Length - 1];
                            if (!_animationMods.ContainsKey(modName)) {
                                _animationMods[modName] = new KeyValuePair<string, List<string>>(directory, new());
                            }
                            if (!_animationMods[modName].Value.Contains(value)) {
                                _animationMods[modName].Value.Add(value);
                            }
                            papFilesFound++;
                            if (!_papSorting.ContainsKey(value)) {
                                try {
                                    _papSorting.TryAdd(value, new List<Tuple<string, string, bool>>() { new Tuple<string, string, bool>(directory, modName, !skipPap) });
                                    if (config.DebugMode) {
                                        Plugin.PluginLog?.Verbose("Found: " + item.Value);
                                    }
                                } catch {
                                    Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + item.Key + " already exists, ignoring.");
                                }
                            } else {
                                _papSorting[value].Add(new Tuple<string, string, bool>(directory, modName, !skipPap));
                            }
                        }
                    }
                } catch (Exception e) {
                    PluginLog.Warning(e, e.Message);
                }
                try {
                    if (item.Key.Contains(".mdl") && (item.Key.Contains("equipment") || item.Key.Contains("accessor")
                        && !directory.ToLower().Contains("hrothgar & viera") && !directory.ToLower().Contains("megapack") && !directory.ToLower().Contains("ivcs"))) {
                        mdlFilesFound++;
                    } else if (!directory.ToLower().Contains("ivcs")) {
                        _modelDependancyMods[modName] = null;
                    }
                } catch (Exception e) {
                    PluginLog.Warning(e, e.Message);
                }
            }
            if (mdlFilesFound > 0) {
                _modelMods[modName] = null;
            }
        }

        public void ExtractMdlFiles(Option option, string directory, bool skipFile) {
            string modName = Path.GetFileName(directory);
            int mdlFilesFound = 0;
            for (int i = 0; i < option.Files.Count; i++) {
                var item = option.Files.ElementAt(i);
                if (item.Key.Contains(".mdl") && (item.Key.Contains("equipment") || item.Key.Contains("accessor")
                    && !directory.ToLower().Contains("hrothgar & viera") && !directory.ToLower().Contains("megapack") && !directory.ToLower().Contains("ivcs"))) {
                    mdlFilesFound++;
                } else if (!directory.ToLower().Contains("ivcs")) {
                    _modelDependancyMods[modName] = null;
                }
            }
            if (mdlFilesFound > 0) {
                _modelMods[modName] = null;
            }
        }

        public ulong GetModelID(string model) {
            string[] strings = model.Split("/");
            ulong newValue = 0;
            foreach (string value in strings) {
                if (value.StartsWith("e") || value.StartsWith("a")) {
                    try {
                        newValue = ulong.Parse(value.Replace("e", "").Replace("a", "").TrimStart('0'));
                        break;
                    } catch {
                    }
                }
            }
            return newValue;
        }



        public void SetDesign(Guid design, int objectId) {
            try {
                PenumbraAndGlamourerIpcWrapper.Instance.ApplyDesign.Invoke(design, objectId);
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }

        public void ApplyByGuid(Guid design, ICharacter? character) {
            PenumbraAndGlamourerIpcWrapper.Instance.ApplyDesign.Invoke(design, character.ObjectIndex);
        }

        public void CleanEquipment(int objectIndex) {
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Head, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Ears, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Neck, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Body, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Legs, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Hands, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.LFinger, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.RFinger, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Feet, 0, new List<byte>());
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, ApiEquipSlot.Wrists, 0, new List<byte>());
        }

        public void RecursivelyFindPapFiles(string modName, string directory, int levels, int maxLevels) {
            if (Directory.Exists(directory)) {
                foreach (string file in Directory.EnumerateFiles(directory)) {
                    if (file.EndsWith(".pap")) {
                        string[] strings = file.Split("\\");
                        string value = strings[strings.Length - 1];
                        if (!_animationMods.ContainsKey(modName)) {
                            _animationMods[modName] = new KeyValuePair<string, List<string>>(directory, new());
                        }
                        if (!_animationMods[modName].Value.Contains(value)) {
                            _animationMods[modName].Value.Add(value);
                        }
                        if (!_papSorting.ContainsKey(value)) {
                            try {
                                _papSorting.TryAdd(value, new List<Tuple<string, string, bool>>()
                                { new Tuple<string, string, bool>(directory, modName, false) });
                            } catch {
                                Plugin.PluginLog?.Warning("[Artemis Roleplaying Kit] " + value + " already exists, ignoring.");
                            }
                        } else {
                            _papSorting[value].Add(new Tuple<string, string, bool>(directory, modName, false));
                        }
                    }
                }
                if (levels < maxLevels) {
                    if (Directory.Exists(directory)) {
                        foreach (string file in Directory.EnumerateDirectories(directory)) {
                            RecursivelyFindPapFiles(modName, file, levels + 1, maxLevels);
                        }
                    }
                }
            }
        }

        public async Task<List<ArtemisVoiceMod>> GetPrioritySortedModPacks(bool skipModelData) {
            Filter.Blacklist?.Clear();
            _scdReplacements?.Clear();
            //_papSorting?.Clear();
            //_mdlSorting?.Clear();
            List<ArtemisVoiceMod> list = new List<ArtemisVoiceMod>();
            string refreshGuid = Guid.NewGuid().ToString();
            _currentModPackRefreshGuid = refreshGuid;
            try {
                if (_penumbraReady) {
                    string modPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                    if (Directory.Exists(modPath)) {
                        var collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(0);
                        foreach (var directory in Directory.EnumerateDirectories(modPath)) {
                            if (refreshGuid == _currentModPackRefreshGuid) {
                                if (config.DebugMode) {
                                    Plugin.PluginLog?.Verbose("Examining: " + directory);
                                }
                                string modName = Path.GetFileName(directory);
                                Option option = null;
                                List<Group> groups = new List<Group>();
                                if (Directory.Exists(directory)) {
                                    foreach (string file in Directory.EnumerateFiles(directory)) {
                                        if (file.EndsWith(".json") && !file.EndsWith("meta.json")) {
                                            if (file.EndsWith("default_mod.json")) {
                                                try {
                                                    option = JsonConvert.DeserializeObject<Option>(File.ReadAllText(file));
                                                } catch {

                                                }
                                            } else {
                                                try {
                                                    groups.Add(JsonConvert.DeserializeObject<Group>(File.ReadAllText(file)));
                                                } catch {

                                                }
                                            }
                                        }
                                    }
                                }
                                if (!_alreadyScannedMods.ContainsKey(modName)) {
                                    _alreadyScannedMods[modName] = true;
                                    if (option != null) {
                                        ExtractModFiles(option, directory, true);
                                    }
                                    foreach (Group group in groups) {
                                        if (group != null) {
                                            foreach (Option groupOption in group.Options) {
                                                ExtractModFiles(groupOption, directory, true);
                                            }
                                        }
                                    }
                                }
                                try {
                                    string relativeDirectory = directory.Replace(modPath, null).TrimStart('\\');
                                    var currentModSettings =
                                    PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.
                                    Invoke(collection.Item3.Id, relativeDirectory, null, true);
                                    var result = currentModSettings.Item1;
                                    if (result == Penumbra.Api.Enums.PenumbraApiEc.Success) {
                                        if (currentModSettings.Item2 != null) {
                                            bool enabled = currentModSettings.Item2!.Value!.Item1;
                                            int priority = currentModSettings.Item2!.Value!.Item2;
                                            if (enabled) {
                                                if (option != null) {
                                                    ExtractSCDOptions(option, directory);
                                                }
                                                foreach (Group group in groups) {
                                                    if (group != null) {
                                                        foreach (Option groupOption in group.Options) {
                                                            ExtractSCDOptions(groupOption, directory);
                                                        }
                                                    }
                                                }
                                                string soundPackData = directory + @"\rpvsp";
                                                string soundPackData2 = directory + @"\arksp";
                                                GetSoundPackData(soundPackData, priority, list);
                                                GetSoundPackData(soundPackData2, priority, list);
                                            }
                                        }
                                    }
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                            } else {
                                break;
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning("Error 404, penumbra not found.");
            }
            if (config != null) {
                if (config.VoicePackIsActive) {
                    if (config.CharacterVoicePacks != null && config.VoiceReplacementType == 0) {
                        if (config.CharacterVoicePacks.ContainsKey(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                            string voice = config.CharacterVoicePacks[_threadSafeObjectTable.LocalPlayer.Name.TextValue];
                            if (!string.IsNullOrEmpty(voice)) {
                                string path = config.CacheFolder + @"\VoicePack\" + voice;
                                if (Directory.Exists(path)) {
                                    ArtemisVoiceMod artemisVoiceMod = new ArtemisVoiceMod();
                                    artemisVoiceMod.Files = Directory.EnumerateFiles(path).ToList();
                                    artemisVoiceMod.Priority = list.Count;
                                    foreach (var subMod in Directory.EnumerateDirectories(path)) {
                                        ArtemisVoiceMod artemisSubVoiceMod = new ArtemisVoiceMod();
                                        artemisSubVoiceMod.Files = Directory.EnumerateFiles(path).ToList();
                                        artemisSubVoiceMod.Priority = list.Count + 1;
                                        artemisVoiceMod.ArtemisSubMods.Add(artemisSubVoiceMod);
                                    }
                                    list.Add(artemisVoiceMod);
                                }
                            }
                        }
                    }
                }
            }
            if (list.Count > 0) {
                list.Sort((x, y) => y.Priority.CompareTo(x.Priority));
            }
            return list;
        }

        private void GetSoundPackData(string soundPackData, int priority, List<ArtemisVoiceMod> list) {
            if (Path.Exists(soundPackData)) {
                ArtemisVoiceMod artemisVoiceMod = new ArtemisVoiceMod();
                artemisVoiceMod.Priority = priority;
                foreach (string file in Directory.EnumerateFiles(soundPackData)) {
                    if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                        artemisVoiceMod.Files.Add(file);
                    }
                }
                foreach (var directory in Directory.EnumerateDirectories(soundPackData)) {
                    ArtemisVoiceMod artemisSubVoiceMod = new ArtemisVoiceMod();
                    artemisSubVoiceMod.Priority = priority;
                    foreach (var file in Directory.EnumerateFiles(directory)) {
                        if (file.EndsWith(".mp3") || file.EndsWith(".ogg")) {
                            artemisSubVoiceMod.Files.Add(file);
                        }
                    }
                    if (artemisSubVoiceMod.Files.Count > 0) {
                        artemisVoiceMod.ArtemisSubMods.Add(artemisSubVoiceMod);
                    }
                }
                list.Add(artemisVoiceMod);
            }
        }
        #endregion
    }
}
