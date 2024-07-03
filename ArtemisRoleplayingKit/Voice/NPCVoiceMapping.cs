using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Voice {
    public static class NPCVoiceMapping {
        public static NPCVoiceConfiguration _npcVoiceConfiguration;
        private static Stopwatch _timeSinceLastUpdate;

        public static async Task<bool> Initialize() {
            try {
                HttpClient httpClient = new HttpClient();
                string json = await httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/Sebane1/RoleplayingVoiceDalamud/master/npcVoiceConfiguration.json");
                _npcVoiceConfiguration = JsonConvert.DeserializeObject<NPCVoiceConfiguration>(json);
                if (_timeSinceLastUpdate == null) {
                    _timeSinceLastUpdate = new Stopwatch();
                }
                _timeSinceLastUpdate.Restart();
                return true;
            } catch (Exception ex) {
                if (_npcVoiceConfiguration == null) {
                    _npcVoiceConfiguration = new NPCVoiceConfiguration();
                }
                return false;
            }
        }

        public static string AliasDetector(string name) {
            foreach (var key in _npcVoiceConfiguration.NameAndAliasesList.Keys) {
                foreach (var aliases in _npcVoiceConfiguration.NameAndAliasesList[key]) {
                    if (aliases.Contains(name)) {
                        return key;
                    }
                }
            }
            return name;
        }
        public static async Task<bool> CheckForUpdates() {
            if (_npcVoiceConfiguration == null || (_timeSinceLastUpdate != null && _timeSinceLastUpdate.ElapsedMilliseconds > 60000)) {
                await Initialize();
            }
            return true;
        }
        public static async Task<Dictionary<string, string>> GetVoiceMappings() {
            await CheckForUpdates();
            return _npcVoiceConfiguration.CharacterToVoiceList;
        }
        public static List<KeyValuePair<string, string>> GetExtrasVoiceMappings() {
            CheckForUpdates();
            return _npcVoiceConfiguration.ExtrasVoiceList;
        }
        public static List<KeyValuePair<string, bool>> GetEchoType() {
            CheckForUpdates();
            return _npcVoiceConfiguration.EchoValuesList;
        }
        public static List<KeyValuePair<string, float>> GetPitchValues() {
            CheckForUpdates();
            return _npcVoiceConfiguration.PitchValuesList;
        }
    }
}
