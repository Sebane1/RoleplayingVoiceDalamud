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
        private Dictionary<string, List<string>> _misc = new Dictionary<string, List<string>>();
        private int emoteIndex;
        private string lastMissed;
        private string lastAction;
        private IDataManager _dataManager;
        private ClientLanguage _clientLanguage;

        public int EmoteIndex { get => emoteIndex; set => emoteIndex = value; }

        public CharacterVoicePack(string directory, IDataManager dataManager, Dalamud.ClientLanguage clientLanguage) {
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
        public CharacterVoicePack(List<string> files, IDataManager dataManager, Dalamud.ClientLanguage clientLanguage) {
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

        public string GetNameInClientLanguage(Dalamud.ClientLanguage clientLanguage, string name) {
            uint index = GetLanguageAgnosticActionIndex(name); ;
            if (clientLanguage == Dalamud.ClientLanguage.English) {
                Action actionEnglish = _dataManager.GetExcelSheet<Action>(Dalamud.ClientLanguage.English).GetRow(index);
                return actionEnglish.Name.RawString;
            } else if (clientLanguage == Dalamud.ClientLanguage.Japanese) {
                Action actionJapanese = _dataManager.GetExcelSheet<Action>(Dalamud.ClientLanguage.Japanese).GetRow(index);
                return actionJapanese.Name.RawString;
            } else if (clientLanguage == Dalamud.ClientLanguage.German) {
                Action actionGerman = _dataManager.GetExcelSheet<Action>(Dalamud.ClientLanguage.German).GetRow(index);
                return actionGerman.Name.RawString;
            } else if (clientLanguage == Dalamud.ClientLanguage.French) {
                Action actionFrench = _dataManager.GetExcelSheet<Action>(Dalamud.ClientLanguage.French).GetRow(index);
                return actionFrench.Name.RawString;
            }
            return "Invalid";
        }
        public uint GetLanguageAgnosticActionIndex(string name) {
            uint englishIndex = GetLanguageSpecifcActionIndex(Dalamud.ClientLanguage.English, name);
            if (englishIndex is not 0) {
                return englishIndex;
            }
            uint japeneseIndex = GetLanguageSpecifcActionIndex(Dalamud.ClientLanguage.Japanese, name);
            if (japeneseIndex is not 0) {
                return japeneseIndex;
            }
            uint germanIndex = GetLanguageSpecifcActionIndex(Dalamud.ClientLanguage.German, name);
            if (germanIndex is not 0) {
                return germanIndex;
            }
            uint frenchIndex = GetLanguageSpecifcActionIndex(Dalamud.ClientLanguage.French, name);
            if (frenchIndex is not 0) {
                return frenchIndex;
            }
            return 0;
        }
        public uint GetLanguageSpecifcActionIndex(Dalamud.ClientLanguage clientLanguage, string name) {
            string sanitizedNamed = name.ToLower().Replace(" ", null).Trim();
            foreach (var item in _dataManager.GetExcelSheet<Action>(clientLanguage)) {
                if (item.Name.RawString.ToLower().Replace(" ", null).Trim().Contains(sanitizedNamed)) {
                    return item.RowId;
                }
            }
            return 0;
        }
        //public unsafe int GetRandom(int min, int max) {
        //    var utcTime = Framework.GetServerTime();
        //    long mod = utcTime / 10;
        //    //var time = DateTimeOffset.FromUnixTimeSeconds(utcTime % 10);
        //    //string timeString = time.UtcDateTime.ToString(@"hh\:mm\:ss").Remove(7);
        //    //int hash = GetSimpleHash(timeString);
        //    Random random = new Random((int)mod);
        //    Dalamud.Logging.PluginLog.Log("Time seed is " + mod);
        //    Dalamud.Logging.PluginLog.Log("Raw UTC time seed is " + utcTime);
        //    return random.Next(min, max);
        //}
        public unsafe int GetRandom(int min, int max) {
            var utcTime = Framework.GetServerTime();
            long mod = utcTime / 10;
            Random random = new Random((int)mod);
            Dalamud.Logging.PluginLog.Log("Time seed is " + mod);
            return random.Next(min, max);
        }
        public unsafe int GetRandom(int min, int max, int delay) {
            var utcTime = Framework.GetServerTime();
            long mod = (utcTime - delay) / 10;
            Random random = new Random((int)mod);
            Dalamud.Logging.PluginLog.Log("Time seed is " + mod);
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
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "shieldwall").Replace(" ", null).ToLower(), file); ;
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "stronghold").Replace(" ", null).ToLower(), file);
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "lastbastion").Replace(" ", null).ToLower(), file);
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "landwaker").Replace(" ", null).ToLower(), file);
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "darkforce").Replace(" ", null).ToLower(), file);
                        AddMisc(GetNameInClientLanguage(_clientLanguage, "gunmetalsoul").Replace(" ", null).ToLower(), file);
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
                        string strippedName = StripNonCharacters(name, _clientLanguage).ToLower();
                        string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : name;
                        AddMisc(GetNameInClientLanguage(_clientLanguage, final), file);
                    }
                }
            }
        }
        public static string StripNonCharacters(string str, Dalamud.ClientLanguage clientLanguage) {
            if (clientLanguage != ClientLanguage.Japanese) {
                if (str != null) {
                    Regex rgx = new Regex("[^a-zA-Z]");
                    str = rgx.Replace(str, "");
                } else {
                    return "";
                }
            }
            return str;
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
            if (_attack.Count > 0 && !value.Contains("sprint") && !value.ToLower().Contains("teleport")) {
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
            if (_meleeAttack.Count > 0 && !value.Contains("sprint") && !value.ToLower().Contains("teleport")) {
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
            if (_castedAttack.Count > 0 && !value.Contains("sprint") && !value.ToLower().Contains("teleport")) {
                string action = _castedAttack[GetRandom(0, _castedAttack.Count)];
                if (lastAction != action) {
                    return lastAction = action;
                }
                return "";
            } else {
                return string.Empty;
            }
        }

        public string GetMisc(string value) {
            if (value != null) {
                string strippedName = StripNonCharacters(value, _clientLanguage).ToLower();
                string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
                foreach (string name in _misc.Keys) {
                    if (final.Contains(name) && name.Length > 5 || final.EndsWith(name)) {
                        return _misc[name][GetRandom(0, _misc[name].Count)];
                    }
                }
            }
            return string.Empty;
        }
        public string GetMisc(string value, int delay) {
            if (value != null) {
                string strippedName = StripNonCharacters(value, _clientLanguage).ToLower();
                string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
                foreach (string name in _misc.Keys) {
                    if (final.Contains(name) && name.Length > 5 || final.EndsWith(name)) {
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
            if (_readying.Count > 0 && !value.ToLower().Contains("teleport")) {
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
