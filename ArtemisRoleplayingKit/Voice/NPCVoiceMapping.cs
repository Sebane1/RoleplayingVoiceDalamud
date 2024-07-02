using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Voice {
    public static class NPCVoiceMapping {
        public static NPCVoiceConfiguration _npcVoiceConfiguration;
        public static async Task<bool> Initialize() {
            try {
                HttpClient httpClient = new HttpClient();
                string json = await httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/Sebane1/RoleplayingVoiceDalamud/master/npcVoiceConfiguration.json");
                _npcVoiceConfiguration = JsonConvert.DeserializeObject<NPCVoiceConfiguration>(json);
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

        public static async Task<Dictionary<string, string>> GetVoiceMappings() {
            if( _npcVoiceConfiguration == null) {
                await Initialize();
            }
            return _npcVoiceConfiguration.CharacterToVoiceList;
        }
        public static List<KeyValuePair<string, string>> GetExtrasVoiceMappings() {
            return _npcVoiceConfiguration.ExtrasVoiceList;
        }
        public static List<KeyValuePair<string, bool>> GetEchoType() {
            return _npcVoiceConfiguration.EchoValuesList;
        }
        public static List<KeyValuePair<string, float>> GetPitchValues() {
            return _npcVoiceConfiguration.PitchValuesList;
        }
    }
}
