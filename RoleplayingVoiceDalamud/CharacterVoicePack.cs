
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RoleplayingVoiceDalamud {
    public class CharacterVoicePack {
        private string _voiceName;
        private List<string>[] _emotes = new List<string>[] {
        new List<string>(), new List<string>(),new List<string>(),
        new List<string>(), new List<string>(),new List<string>(),
        new List<string>(), new List<string>(),new List<string>(),
        new List<string>(), new List<string>(),new List<string>(),
        new List<string>(), new List<string>()
        };
        private List<string> _attack = new List<string>();
        private List<string> _hurt = new List<string>();
        private List<string> _death = new List<string>();
        private List<string> _readying = new List<string>();
        private List<string> _revive = new List<string>();
        private List<string> _missed = new List<string>();
        private List<string> _casting = new List<string>();
        private Dictionary<string, List<string>> _misc = new Dictionary<string, List<string>>();
        private List<string> emotesNames = new List<string>() {
        "suprised", "angry", "furious", "cheer", "doze", "fume", "huh", "chuckle", "laugh", "no",
        "stretch", "upset", "yes", "happy"
        };
        private Random _random;
        private int emoteIndex;

        public string VoiceName { get => _voiceName; set => _voiceName = value; }
        public int EmoteIndex { get => emoteIndex; set => emoteIndex = value; }

        public CharacterVoicePack(string voiceName, string directory) {
            _voiceName = voiceName;
            if (!string.IsNullOrEmpty(directory)) {
                foreach (string file in Directory.EnumerateFiles(directory)) {
                    if (file.ToLower().EndsWith(".mp3")) {
                        for (int i = 0; i < emotesNames.Count; i++) {
                            if (file.ToLower().Contains(emotesNames[i])) {
                                _emotes[i].Add(file);
                                break;
                            }
                        }
                        if (file.ToLower().Contains("attack")
                                || file.ToLower().Contains("extra")) {
                            _attack.Add(file);
                        } else if (file.ToLower().Contains("hurt")) {
                            _hurt.Add(file);
                        } else if (file.ToLower().Contains("death")) {
                            _death.Add(file);
                        } else if (file.ToLower().Contains("limit")) {
                            _readying.Add(file);
                        } else if (file.ToLower().Contains("casting")) {
                            _casting.Add(file);
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
            _random = new Random();
        }
        public string StripNonCharacters(string str) {
            Regex rgx = new Regex("[^a-zA-Z]");
            str = rgx.Replace(str, "");
            return str;
        }

        // Combat --------------------------------------------------------------------------
        public string GetAction(string value) {
            foreach (string name in _misc.Keys) {
                if (value.ToLower().Contains(name)) {
                    return _misc[name][_random.Next(0, _misc[name].Count)];
                }
            }
            if (_attack.Count > 0 && !value.Contains("sprint")) {
                return _attack[_random.Next(0, _attack.Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetMisc(string value) {
            foreach (string name in _misc.Keys) {
                if (value.ToLower().Contains(name)) {
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
            foreach (string name in _misc.Keys) {
                if (value.ToLower().Contains(name)) {
                    return _misc[name][_random.Next(0, _misc[name].Count)];
                }
            }
            if (_readying.Count > 0 && !value.ToLower().Contains("teleport")) {
                return _readying[_random.Next(0, _readying.Count)];
            } else {
                return string.Empty;
            }
        }

        public string GetCasting() {
            if (_casting.Count > 0) {
                return _casting[_random.Next(0, _casting.Count)];
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
                return _missed[_random.Next(0, _missed.Count)];
            } else {
                return string.Empty;
            }
        }


        // Emotes ----------------------------------------------------------------------
        public string GetSurprised() {
            emoteIndex = 0;
            if (_emotes[0].Count > 0) {
                return _emotes[0][_random.Next(0, _emotes[0].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetAngry() {
            emoteIndex = 1;
            if (_emotes[1].Count > 0) {
                return _emotes[1][_random.Next(0, _emotes[1].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetFurious() {
            emoteIndex = 2;
            if (_emotes[2].Count > 0) {
                return _emotes[2][_random.Next(0, _emotes[2].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetCheer() {
            emoteIndex = 3;
            if (_emotes[3].Count > 0) {
                return _emotes[3][_random.Next(0, _emotes[3].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetDoze() {
            emoteIndex = 4;
            if (_emotes[4].Count > 0) {
                return _emotes[4][_random.Next(0, _emotes[4].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetFume() {
            emoteIndex = 5;
            if (_emotes[5].Count > 0) {
                return _emotes[5][_random.Next(0, _emotes[5].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetHuh() {
            emoteIndex = 6;
            if (_emotes[6].Count > 0) {
                return _emotes[6][_random.Next(0, _emotes[6].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetChuckle() {
            emoteIndex = 7;
            if (_emotes[7].Count > 0) {
                return _emotes[7][_random.Next(0, _emotes[7].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetLaugh() {
            emoteIndex = 8;
            if (_emotes[8].Count > 0) {
                return _emotes[8][_random.Next(0, _emotes[8].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetNo() {
            emoteIndex = 9;
            if (_emotes[9].Count > 0) {
                return _emotes[9][_random.Next(0, _emotes[9].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetStretch() {
            emoteIndex = 10;
            if (_emotes[10].Count > 0) {
                return _emotes[10][_random.Next(0, _emotes[10].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetUpset() {
            emoteIndex = 11;
            if (_emotes[11].Count > 0) {
                return _emotes[11][_random.Next(0, _emotes[11].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetYes() {
            emoteIndex = 12;
            if (_emotes[12].Count > 0) {
                return _emotes[12][_random.Next(0, _emotes[12].Count)];
            } else {
                return string.Empty;
            }
        }
        public string GetHappy() {
            emoteIndex = 13;
            if (_emotes[13].Count > 0) {
                return _emotes[13][_random.Next(0, _emotes[13].Count)];
            } else {
                return string.Empty;
            }
        }
    }
}
