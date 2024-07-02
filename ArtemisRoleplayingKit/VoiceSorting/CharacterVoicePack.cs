using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Dalamud;
using Dalamud.Plugin.Services;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Task = System.Threading.Tasks.Task;
using Lumina.Excel;
using System.Windows.Forms;
using System.Collections.Concurrent;
using Dalamud.Game;
using RoleplayingVoice;
namespace RoleplayingVoiceDalamud {
    public class CharacterVoicePack {
        private List<string> _castedAttack = new List<string>();
        private List<string> _meleeAttack = new List<string>();
        private List<string> _attack = new List<string>();
        private List<string> _hurt = new List<string>();
        private List<string> _death = new List<string>();
        private List<string> _readying = new List<string>();
        private List<string> _revive = new List<string>();
        private List<string> _missed = new List<string>();
        private List<string> _castingAttack = new List<string>();
        private List<string> _castingHeal = new List<string>();
        private ConcurrentDictionary<string, List<string>> _misc = new ConcurrentDictionary<string, List<string>>();
        private int emoteIndex;
        private string lastMissed;
        private string lastAction;
        private IDataManager _dataManager;
        private ClientLanguage _clientLanguage;
        private ConcurrentDictionary<ClientLanguage, ExcelSheet<Action>> _dataSheets = new ConcurrentDictionary<ClientLanguage, ExcelSheet<Lumina.Excel.GeneratedSheets.Action>>();
        private string _sprint;
        private string _teleport;

        public int EmoteIndex { get => emoteIndex; set => emoteIndex = value; }

        public CharacterVoicePack(string directory, IDataManager dataManager, ClientLanguage clientLanguage) {
            _dataManager = dataManager;
            _clientLanguage = clientLanguage;
            Task.Run(() => {
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) {
                    foreach (string file in Directory.EnumerateFiles(directory)) {
                        SortFile(file);
                    }
                }
            });

        }
        public CharacterVoicePack(List<string> files, IDataManager dataManager, ClientLanguage clientLanguage) {
            _dataManager = dataManager;
            _clientLanguage = clientLanguage;
            Task.Run(() => {
                if (files != null) {
                    foreach (string file in files) {
                        SortFile(file);
                    }
                }
            });
        }

        public string GetNameInClientLanguage(ClientLanguage clientLanguage, string name) {
            uint index = GetLanguageAgnosticActionIndex(name);
            if (index is not 0) {
                return GetActionInLanguage(clientLanguage, index, name);
            }
            return name;
        }
        public string GetActionInLanguage(ClientLanguage clientLanguage, uint index, string name) {
            Action actionName = _dataSheets[clientLanguage].GetRow(index);
            if (!string.IsNullOrEmpty(actionName.Name.RawString)) {
                return actionName.Name.RawString;
            } else {
                return name;
            }
        }
        public uint GetLanguageAgnosticActionIndex(string name) {
            uint englishIndex = GetLanguageSpecifcActionIndex(ClientLanguage.English, name);
            if (englishIndex is not 0) {
                return englishIndex;
            }
            uint japaneseIndex = GetLanguageSpecifcActionIndex(ClientLanguage.Japanese, name);
            if (japaneseIndex is not 0) {
                return japaneseIndex;
            }
            uint germanIndex = GetLanguageSpecifcActionIndex(ClientLanguage.German, name);
            if (germanIndex is not 0) {
                return germanIndex;
            }
            uint frenchIndex = GetLanguageSpecifcActionIndex(ClientLanguage.French, name);
            if (frenchIndex is not 0) {
                return frenchIndex;
            }
            return 0;
        }
        public uint GetLanguageSpecifcActionIndex(ClientLanguage clientLanguage, string name) {
            if (!string.IsNullOrEmpty(name)) {
                string sanitizedNamed = name.ToLower().Replace(" ", null).Trim();
                if (!_dataSheets.ContainsKey(clientLanguage)) {
                    _dataSheets[clientLanguage] = _dataManager.GetExcelSheet<Action>(clientLanguage);
                }
                foreach (var item in _dataSheets[clientLanguage]) {
                    string strippedName = StripNonCharacters(item.Name.RawString, _clientLanguage).ToLower();
                    string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : item.Name.RawString;
                    if (final.StartsWith(sanitizedNamed) && sanitizedNamed.Length > 5 || final.StartsWith(sanitizedNamed)) {
                        return item.RowId;
                    }
                }
            }
            return 0;
        }

        public unsafe int GetRandom(int min, int max) {
            var utcTime = Framework.GetServerTime();
            long mod = utcTime / 10;
            Random random = new Random((int)mod);
            Plugin.PluginLog.Info("Time seed is " + mod);
            return random.Next(min, max);
        }
        public unsafe int GetRandom(int min, int max, int delay) {
            var utcTime = Framework.GetServerTime();
            long mod = (utcTime - delay) / 10;
            Random random = new Random((int)mod);
            Plugin.PluginLog.Info("Time seed is " + mod);
            return random.Next(min, max);
        }
        private static int GetSimpleHash(string s) {
            return s.Select(a => (int)a).Sum();
        }
        public void SortFile(string file) {
            if (file.ToLower().EndsWith(".mp3") || file.ToLower().EndsWith(".ogg")) {
                bool emoteAdded = false;
                if (!emoteAdded) {
                    string filteredString = StripNonCharacters(file.ToLower(), _clientLanguage);
                    if (filteredString.Contains("meleeattack")) {
                        _meleeAttack.Add(file);
                    } else if (filteredString.Contains("castedattack")) {
                        _castedAttack.Add(file);
                    } else if (filteredString.Contains("castingattack")) {
                        _castingAttack.Add(file);
                    } else if (filteredString.Contains("attack")
                            || filteredString.Contains("extra")) {
                        _attack.Add(file);
                    } else if (filteredString.Contains("hurt")) {
                        _hurt.Add(file);
                    } else if (filteredString.Contains("death")) {
                        _death.Add(file);
                    } else if (filteredString.Contains("limit")) {
                        _readying.Add(file);
                        Task.Run(() => {
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "shieldwall").Replace(" ", null).ToLower(), file);
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "stronghold").Replace(" ", null).ToLower(), file);
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "lastbastion").Replace(" ", null).ToLower(), file);
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "landwaker").Replace(" ", null).ToLower(), file);
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "darkforce").Replace(" ", null).ToLower(), file);
                            AddMisc(GetNameInClientLanguage(_clientLanguage, "gunmetalsoul").Replace(" ", null).ToLower(), file);
                        });
                    } else if (filteredString.Contains("castingheal")) {
                        _castingHeal.Add(file);
                    } else if (filteredString.Contains("casting")) {
                        _castingAttack.Add(file);
                        _castingHeal.Add(file);
                    } else if (filteredString.Contains("missed")) {
                        _missed.Add(file);
                    } else if (filteredString.Contains("revive")) {
                        _revive.Add(file);
                    } else if (filteredString.Contains("battleerror")) {
                        AddMisc("invalidtarget", file);
                        AddMisc("targetisnotinrange", file);
                        AddMisc("targetisnotinlineofsight", file);
                        AddMisc("cannotuse", file);
                        AddMisc("notyetready", file);
                    } else {
                        string name = Path.GetFileNameWithoutExtension(file);
                        string strippedName = StripNonCharacters(name.ToLower(), _clientLanguage);
                        string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : name;
                        if (_clientLanguage == ClientLanguage.English) {
                            AddMisc(final, file);
                        } else {
                            AddMisc(final, file);
                            if (final.Length > 3 && !InLanguageBlacklist(final)) {
                                AddMisc(StripNonCharacters(GetNameInClientLanguage(_clientLanguage, final), _clientLanguage).ToLower(), file);
                            }
                        }
                    }
                    _sprint = GetNameInClientLanguage(_clientLanguage, "sprint").ToLower();
                    _teleport = GetNameInClientLanguage(_clientLanguage, "teleport").ToLower();
                }
            }
        }
        public static string StripNonCharacters(string str, ClientLanguage clientLanguage) {
            if (clientLanguage == ClientLanguage.English) {
                if (str != null) {
                    Regex rgx = new Regex("[^a-zA-Z]");
                    str = rgx.Replace(str, "");
                } else {
                    return "";
                }
            } else {
                return str.Replace("0", "").Replace("1", "")
                    .Replace("2", "").Replace("3", "")
                    .Replace("4", "").Replace("5", "")
                    .Replace("6", "").Replace("7", "")
                    .Replace("8", "").Replace("9", "")
                    .Replace("-", "").Replace("|", "").Replace(" ", "");
            }
            return str;
        }

        private bool InLanguageBlacklist(string name) {
            List<string> blacklist = new List<string>() {
            "surprised", "angry", "furious", "cheer",
            "doze", "fume", "huh", "chuckle", "laugh",
            "no", "stretch", "upset", "yes", "happy"
            };
            return blacklist.Contains(name);
        }
        private void AddMisc(string category, string file) {
            if (!_misc.ContainsKey(category)) {
                _misc[category] = new List<string>();
            }
            if (!_misc[category].Contains(file)) {
                _misc[category].Add(file);
            }
        }

        public string GetAction(string value) {
            if (_attack.Count > 0 && !value.Contains(_sprint) && !value.ToLower().Contains(_teleport)) {
                string action = _attack[GetRandom(0, _attack.Count)];
                if (lastAction != action) {
                    return lastAction = action;
                }
                return "";
            } else {
                return string.Empty;
            }
        }

        public string GetMeleeAction(string value) {
            if (_meleeAttack.Count > 0 && !value.Contains(_sprint) && !value.ToLower().Contains(_teleport)) {
                string action = _meleeAttack[GetRandom(0, _meleeAttack.Count)];
                if (lastAction != action) {
                    return lastAction = action;
                }
                return "";
            } else {
                return string.Empty;
            }
        }

        public string GetCastedAction(string value) {
            if (_castedAttack.Count > 0 && !value.Contains(_sprint) && !value.ToLower().Contains(_teleport)) {
                string action = _castedAttack[GetRandom(0, _castedAttack.Count)];
                if (lastAction != action) {
                    return lastAction = action;
                }
                return "";
            } else {
                return string.Empty;
            }
        }

        public string GetMisc(string value, bool allowEmotes = false) {
            if (value != null) {
                string strippedName = StripNonCharacters(value, _clientLanguage).ToLower();
                string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
                foreach (string name in _misc.Keys) {
                    string alternate = name.Remove(name.Length - 1);
                    if (((final.Contains(name) && name.Length > 5 || final.EndsWith(name))
                        || (_clientLanguage == ClientLanguage.Japanese && final.Contains(alternate))) 
                        && (!InLanguageBlacklist(name) || allowEmotes)) {
                        return _misc[name][GetRandom(0, _misc[name].Count)];
                    }
                }
            }
            return string.Empty;
        }
        public string GetMisc(string value, int delay, bool allowEmotes = false) {
            if (value != null) {
                string strippedName = StripNonCharacters(value, _clientLanguage).ToLower();
                string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
                foreach (string name in _misc.Keys) {
                    if ((final.Contains(name) && name.Length > 5 || final.EndsWith(name)) && (!InLanguageBlacklist(name) || allowEmotes)) {
                        return _misc[name][GetRandom(0, _misc[name].Count, delay)];
                    }
                }
            }
            return string.Empty;
        }
        public string GetMiscSpecific(string value, int index) {
            string strippedName = StripNonCharacters(value, _clientLanguage).ToLower();
            string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
            foreach (string name in _misc.Keys) {
                if (final.Contains(name) && name.Length > 5 || final.EndsWith(name)) {
                    if (index < _misc[name].Count) {
                        return _misc[name][index];
                    }
                }
            }
            return string.Empty;
        }
        public string GetHurt() {
            if (_hurt.Count > 0) {
                return _hurt[GetRandom(0, _hurt.Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetDeath() {
            if (_death.Count > 0) {
                return _death[GetRandom(0, _death.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetReadying(string value) {
            if (_readying.Count > 0 && !value.ToLower().Contains(_teleport)) {
                return _readying[GetRandom(0, _readying.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetCastingAttack() {
            if (_castingAttack.Count > 0) {
                return _castingAttack[GetRandom(0, _castingAttack.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetCastingHeal() {
            if (_castingHeal.Count > 0) {
                return _castingHeal[GetRandom(0, _castingHeal.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetRevive() {
            if (_revive.Count > 0) {
                return _revive[GetRandom(0, _revive.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetMissed() {
            if (_missed.Count > 0) {
                string missed = _missed[GetRandom(0, _missed.Count)];
                if (lastMissed != missed) {
                    return lastMissed = missed;
                }
                return "";
            } else {
                return string.Empty;
            }
        }
    }
}
