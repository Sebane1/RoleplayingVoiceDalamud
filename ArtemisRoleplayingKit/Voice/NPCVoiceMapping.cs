using Newtonsoft.Json;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud.Datamining;
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
        private static Dictionary<string, ReportData> _speakerList;
        private static Dictionary<ulong, string> _npcBubbleRecovery = new Dictionary<ulong, string>();
        private static Dictionary<string, string> _namelessNPCs;
        private static bool alreadyLoaded;

        public static Dictionary<ulong, string> NpcBubbleRecovery { get => _npcBubbleRecovery; set => _npcBubbleRecovery = value; }
        public static Dictionary<string, string> NamelessNPCs { get => _namelessNPCs; set => _namelessNPCs = value; }

        public static async Task<bool> Initialize() {
            try {
                HttpClient httpClient = new HttpClient();
                string json = await httpClient.GetStringAsync(
                "https://raw.githubusercontent.com/Sebane1/RoleplayingVoiceDalamud/master/npcVoiceConfiguration.json");
                _npcVoiceConfiguration = JsonConvert.DeserializeObject<NPCVoiceConfiguration>(json);
                httpClient = new HttpClient();
                json = await httpClient.GetStringAsync("https://raw.githubusercontent.com/Sebane1/RoleplayingVoiceDalamud/master/nameless.json");
                _namelessNPCs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (!alreadyLoaded) {
                    httpClient = new HttpClient();
                    json = await httpClient.GetStringAsync("https://raw.githubusercontent.com/Sebane1/RoleplayingVoiceDalamud/master/speakers.json");
                    _speakerList = JsonConvert.DeserializeObject<Dictionary<string, ReportData>>(json);
                    _npcBubbleRecovery.Clear();
                    foreach (var item in _speakerList) {
                        _npcBubbleRecovery.Add(item.Value.npcid, item.Key);
                    }
                    alreadyLoaded = true;
                }
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
        public static string CheckForNameVariant(string name, int variantDiscriminator) {
            Dictionary<int, Dictionary<string, string>> voiceVariants = new Dictionary<int, Dictionary<string, string>>();
            voiceVariants[1192] = new Dictionary<string, string>() {
                { "Cahciua", "Cahciua Living" },
                { "Otis", "Otis Living" } };

            if (voiceVariants.ContainsKey(variantDiscriminator)) {
                if (voiceVariants[variantDiscriminator].ContainsKey(name)) {
                    return voiceVariants[variantDiscriminator][name];
                }
            }
            return name;
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
            if (_npcVoiceConfiguration == null || (_timeSinceLastUpdate != null && _timeSinceLastUpdate.ElapsedMilliseconds > 120000)) {
                await Initialize();
            }
            return true;
        }
        public static async Task<Dictionary<string, string>> GetVoiceMappings() {
            await CheckForUpdates();
            return _npcVoiceConfiguration.CharacterToVoiceList;
        }
        public static List<KeyValuePair<string, string>> GetExtrasVoiceMappings() {
            Task.Run(() => {
                CheckForUpdates();
            });
            return _npcVoiceConfiguration.ExtrasVoiceList;
        }
        public static List<KeyValuePair<string, bool>> GetEchoType() {
            Task.Run(() => {
                CheckForUpdates();
            });
            return _npcVoiceConfiguration.EchoValuesList;
        }
        public static List<KeyValuePair<string, float>> GetPitchValues() {
            Task.Run(() => {
                CheckForUpdates();
            });
            return _npcVoiceConfiguration.PitchValuesList;
        }

        public static async Task<Dictionary<string, VoiceLinePriority>> GetCharacterToCacheType() {
            Task.Run(() => {
                CheckForUpdates();
            });
            return _npcVoiceConfiguration.CharacterToCacheType;
        }
        internal static Dictionary<string, string> GetNamelessNPCs() {
            Task.Run(() => {
                CheckForUpdates();
            });
            return _namelessNPCs;
        }
    }
}
