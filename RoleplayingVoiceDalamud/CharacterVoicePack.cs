
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RoleplayingVoiceDalamud {
    public class CharacterVoicePack {
        private string _voiceName;
        private List<string> _attack = new List<string>();
        private List<string> _hurt = new List<string>();
        private List<string> _death = new List<string>();
        private List<string> _readying = new List<string>();
        private List<string> _revive = new List<string>();
        private List<string> _missed = new List<string>();
        private List<string> _castingAttack = new List<string>();
        private List<string> _castingHeal = new List<string>();
        private Dictionary<string, List<string>> _misc = new Dictionary<string, List<string>>();
        private Random _random;
        private int emoteIndex;
        private string lastMissed;
        private string lastAction;

        public int EmoteIndex { get => emoteIndex; set => emoteIndex = value; }

        public CharacterVoicePack(string directory) {
            if (!string.IsNullOrEmpty(directory)) {
                foreach (string file in Directory.EnumerateFiles(directory)) {
                    SortFile(file);
                }
            }
            _random = new Random();
        }
        public CharacterVoicePack(List<string> files) {
            if (files != null) {
                foreach (string file in files) {
                    SortFile(file);
                }
                _random = new Random();
            }
        }
        public void SortFile(string file) {
            if (file.ToLower().EndsWith(".mp3") || file.ToLower().EndsWith(".ogg")) {
                bool emoteAdded = false;
                if (!emoteAdded) {
                    if (file.ToLower().Contains("casting attack")) {
                        _castingAttack.Add(file);
                    } else if (file.ToLower().Contains("attack")
                            || file.ToLower().Contains("extra")) {
                        _attack.Add(file);
                    } else if (file.ToLower().Contains("hurt")) {
                        _hurt.Add(file);
                    } else if (file.ToLower().Contains("death")) {
                        _death.Add(file);
                    } else if (file.ToLower().Contains("limit")) {
                        _readying.Add(file);
                    } else if (file.ToLower().Contains("casting heal")) {
                        _castingHeal.Add(file);
                    } else if (file.ToLower().Contains("casting")) {
                        _castingAttack.Add(file);
                        _castingHeal.Add(file);
                    } else if (file.ToLower().Contains("missed")) {
                        _missed.Add(file);
                    } else if (file.ToLower().Contains("revive")) {
                        _revive.Add(file);
                    } else {
                        string name = Path.GetFileNameWithoutExtension(file);
                        string strippedName = StripNonCharacters(name).ToLower();
                        string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : name;
                        if (!_misc.ContainsKey(final)) {
                            _misc[final] = new List<string>();
                        }
                        _misc[final].Add(file);
                    }
                }
            }
        }
        public static string StripNonCharacters(string str) {
            Regex rgx = new Regex("[^a-zA-Z]");
            str = rgx.Replace(str, "");
            return str;
        }

        public string GetAction(string value) {
            if (_attack.Count > 0 && !value.Contains("sprint") && !value.ToLower().Contains("teleport")) {
                string action = _attack[_random.Next(0, _attack.Count)];
                if (lastAction != action) {
                    return lastAction = action;
                }
                return "";
            } else {
                return string.Empty;
            }
        }
        public string GetMisc(string value) {
            string strippedName = StripNonCharacters(value).ToLower();
            string final = !string.IsNullOrWhiteSpace(strippedName) ? strippedName : value;
            foreach (string name in _misc.Keys) {
                if (final.Contains(name) && name.Length > 4 || final.EndsWith(name)) {
                    return _misc[name][_random.Next(0, _misc[name].Count)];
                }
            }
            return string.Empty;
        }
        public string GetHurt() {
            if (_hurt.Count > 0) {
                return _hurt[_random.Next(0, _hurt.Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetDeath() {
            if (_death.Count > 0) {
                return _death[_random.Next(0, _death.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetReadying(string value) {
            if (_readying.Count > 0 && !value.ToLower().Contains("teleport")) {
                return _readying[_random.Next(0, _readying.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetCastingAttack() {
            if (_castingAttack.Count > 0) {
                return _castingAttack[_random.Next(0, _castingAttack.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetCastingHeal() {
            if (_castingHeal.Count > 0) {
                return _castingHeal[_random.Next(0, _castingHeal.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetRevive() {
            if (_revive.Count > 0) {
                return _revive[_random.Next(0, _revive.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetMissed() {
            if (_missed.Count > 0) {
                string missed = _missed[_random.Next(0, _missed.Count)];
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
