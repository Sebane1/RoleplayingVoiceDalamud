using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Voice {
    public class NPCVoiceHistoryItem {
        string _text;
        string _originalValue;
        string _character;
        bool _gender;
        string _backupVoice;
        bool _aggressiveCache;
        bool _fastSpeed;
        string _extraJson;
        bool _redoLine;
        bool _canBeMuted;

        public NPCVoiceHistoryItem(string text, string originalValue, string character, 
            bool gender, string backupVoice, bool aggressiveCache, bool fastSpeed, 
            string extraJson, bool redoLine, bool canBeMuted) {
            _text = text;
            _originalValue = originalValue;
            _character = character;
            _gender = gender;
            _backupVoice = backupVoice;
            _aggressiveCache = aggressiveCache;
            _fastSpeed = fastSpeed;
            _extraJson = extraJson;
            _redoLine = redoLine;
            _canBeMuted = canBeMuted;
        }

        public string Text { get => _text; set => _text = value; }
        public string OriginalValue { get => _originalValue; set => _originalValue = value; }
        public string Character { get => _character; set => _character = value; }
        public bool Gender { get => _gender; set => _gender = value; }
        public string BackupVoice { get => _backupVoice; set => _backupVoice = value; }
        public bool AggressiveCache { get => _aggressiveCache; set => _aggressiveCache = value; }
        public bool FastSpeed { get => _fastSpeed; set => _fastSpeed = value; }
        public string ExtraJson { get => _extraJson; set => _extraJson = value; }
        public bool RedoLine { get => _redoLine; set => _redoLine = value; }
        public bool CanBeMuted { get => _canBeMuted; set => _canBeMuted = value; }
    }
}
