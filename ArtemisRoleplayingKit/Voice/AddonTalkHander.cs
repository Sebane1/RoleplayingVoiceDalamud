using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFBardMusicPlayer.FFXIV;
using NAudio.Lame;
using NAudio.Wave;
using RoleplayingVoice;
using SoundFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using SoundType = RoleplayingMediaCore.SoundType;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkHandler : IDisposable {
        private AddonTalkManager addonTalkManager;
        private IFramework framework;
        private IObjectTable objects;
        private IClientState _clientState;
        private object subscription;
        private string _lastText;
        private Plugin _plugin;
        private bool _blockAudioGeneration;
        private List<string> _currentDialoguePaths = new List<string>();
        private List<bool> _currentDialoguePathsCompleted = new List<bool>();
        private FFXIVHook _hook;
        private MediaGameObject _currentSpeechObject;
        private bool _startedNewDialogue;
        private bool _textIsPresent;
        private bool _alreadyAddedEvent;
        Stopwatch _passthroughTimer = new Stopwatch();
        List<string> _namesToRemove = new List<string>();
        public bool TextIsPresent { get => _textIsPresent; set => _textIsPresent = value; }

        public AddonTalkHandler(AddonTalkManager addonTalkManager, IFramework framework, IObjectTable objects,
            IClientState clientState, Plugin plugin) {
            this.addonTalkManager = addonTalkManager;
            this.framework = framework;
            this.objects = objects;
            _clientState = clientState;
            framework.Update += Framework_Update;
            _plugin = plugin;
            _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
            _hook = new FFXIVHook();
            _hook.Hook(Process.GetCurrentProcess());
        }

        private void Filter_OnCutsceneAudioDetected(object sender, SoundFilter.InterceptedSound e) {
            if (_clientState.IsLoggedIn) {
                if (!_currentDialoguePaths.Contains(e.SoundPath)) {
                    _blockAudioGeneration = e.isBlocking;
                    _currentDialoguePaths.Add(e.SoundPath);
                    _currentDialoguePathsCompleted.Add(false);
                }
            }
        }

        private void Framework_Update(IFramework framework) {
            if (_clientState.IsLoggedIn && !_plugin.Config.NpcSpeechGenerationDisabled) {
                if (_plugin.Filter.IsCutsceneDetectionNull()) {
                    if (!_alreadyAddedEvent) {
                        _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
                        _alreadyAddedEvent = true;
                    }
                }
                var state = GetTalkAddonState();
                if (state != null && !string.IsNullOrEmpty(state.Text) && state.Speaker != "All") {
                    _textIsPresent = true;
                    if (state.Text != _lastText) {
                        _lastText = state.Text;
                        if (!_blockAudioGeneration) {
                            NPCText(state.Speaker, state.Text.TrimStart('.'));
                            _passthroughTimer.Reset();
                        }
#if DEBUG
                        DumpCurrentAudio(state.Speaker);
#endif
                        if (_currentDialoguePaths.Count > 0) {
                            _currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] = true;
                        }
                        _blockAudioGeneration = false;
                    }
                } else {
                    if (_currentDialoguePaths.Count > 0) {
                        if (!_currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] && !_blockAudioGeneration) {
                            try {
                                ScdFile scdFile = GetScdFile(_currentDialoguePaths[_currentDialoguePaths.Count - 1]);
                                WaveStream stream = scdFile.Audio[0].Data.GetStream();
                                var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                                _plugin.MediaManager.PlayAudioStream(new DummyObject(),
                                    pcmStream, SoundType.NPC, false, false, 1, 0, _plugin.Config.AutoTextAdvance ? delegate {
                                        _hook.SendAsyncKey(Keys.NumPad0);
                                    }
                                : null);
                            } catch (Exception e) {
                                Dalamud.Logging.PluginLog.LogError(e, e.Message);
                            }
                        }
                        if (_currentDialoguePaths.Count > 0) {
                            _currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] = true;
                        }
                    }
                    if (_currentSpeechObject != null) {
                        var otherData = _clientState.LocalPlayer.OnlineStatus;
                        if (otherData.Id != 15) {
                            _namesToRemove.Clear();
                            _lastText = "";
                            _plugin.MediaManager.StopAudio(_currentSpeechObject);
                            _plugin.MediaManager.CleanSounds();
                            _currentSpeechObject = null;
                            _currentDialoguePaths.Clear();
                            _currentDialoguePathsCompleted.Clear();
                        }
                    }
                    _blockAudioGeneration = false;
                    _textIsPresent = false;
                }
            }
        }

        private void DumpCurrentAudio(string speaker) {
            try {
                if (_currentDialoguePaths.Count > 0) {
                    Directory.CreateDirectory(_plugin.Config.CacheFolder + @"\Dump\");
                    string name = speaker;
                    string path = _plugin.Config.CacheFolder + @"\Dump\" + name + ".mp3";
                    string pathWave = _plugin.Config.CacheFolder + @"\Dump\" + name + Guid.NewGuid() + ".wav";
                    FileInfo fileInfo = null;
                    try {
                        fileInfo = new FileInfo(path);
                    } catch {

                    }
                    if (!fileInfo.Exists || fileInfo.Length < 7500000) {
                        try {
                            ScdFile scdFile = GetScdFile(_currentDialoguePaths[_currentDialoguePaths.Count - 1]);
                            WaveStream stream = scdFile.Audio[0].Data.GetStream();
                            var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                            using (WaveFileWriter fileStreamWave = new WaveFileWriter(pathWave, pcmStream.WaveFormat)) {
                                pcmStream.CopyTo(fileStreamWave);
                                fileStreamWave.Close();
                                fileStreamWave.Dispose();
                            }
                            if (scdFile != null) {
                                using (var waveStream = new AudioFileReader(pathWave)) {
                                    using (FileStream fileStream = new FileStream(path, FileMode.Append, FileAccess.Write)) {
                                        using (LameMP3FileWriter lame = new LameMP3FileWriter(fileStream, waveStream.WaveFormat, LAMEPreset.VBR_90)) {
                                            waveStream.CopyTo(lame);
                                        }
                                    }
                                }
                            }
                            File.Delete(pathWave);
                        } catch (Exception e) {
                            Dalamud.Logging.PluginLog.LogError(e, e.Message);
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.LogError(e, e.Message);
            }
        }

        public string ConvertRomanNumberals(string text) {
            string value = text;
            for (int i = 25; i > 5; i--) {
                string numeral = Numerals.Roman.To(i);
                if (numeral.Length > 1) {
                    value = value.Replace(numeral, i.ToString());
                }
            }
            return value;
        }
        public ScdFile GetScdFile(string soundPath) {
            if (_plugin.DataManager.FileExists(soundPath)) {
                try {
                    var file = _plugin.DataManager.GetFile(soundPath);
                    MemoryStream data = new MemoryStream(file.Data);
                    return new ScdFile(new BinaryReader(data));
                } catch {
                    return null;
                }
            } else {
                return null;
            }
        }
        private static int GetSimpleHash(string s) {
            return s.Select(a => (int)a).Sum();
        }
        private async void NPCText(string npcName, SeString message) {
            try {
                bool gender = false;
                byte race = 0;
                byte body = 0;
                GameObject npcObject = DiscoverNpc(npcName, ref gender, ref race, ref body);
                string nameToUse = npcObject != null ? npcObject.Name.TextValue : npcName;
                _currentSpeechObject = new MediaGameObject(npcObject != null ? npcObject : _clientState.LocalPlayer);
                string value = StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message.TextValue)));
                KeyValuePair<Stream, bool> stream =
                await _plugin.NpcVoiceManager.GetCharacterAudio(value, nameToUse, gender, PickVoiceBasedOnTraits(nameToUse, gender, race, body), false, _clientState.LocalPlayer.OnlineStatus.Id != 15);
                if (stream.Key != null) {
                    var mp3Stream = new Mp3FileReader(stream.Key);
                    _plugin.MediaManager.PlayAudioStream(_currentSpeechObject, mp3Stream
                    , SoundType.NPC, true, CheckIfshouldUseSmbPitch(nameToUse),
                    stream.Value ? CheckForDefinedPitch(nameToUse) : CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f), 0,
                   _plugin.Config.AutoTextAdvance ? delegate {
                       //_hook.FocusWindow();
                       _hook.SendAsyncKey(Keys.NumPad0);
                       //_hook.SendSyncKey(Keys.NumPad0);
                   }
                    : null);
                    _startedNewDialogue = true;
                } else {
                }
            } catch {
            }
        }

        private GameObject DiscoverNpc(string npcName, ref bool gender, ref byte race, ref byte body) {
            if (npcName == "???") {
                List<string> npcNames = new List<string>(){
                    "Yugiri",
                    "Moenbryda",
                    "Masked Mage",
                    "Nabriales",
                    "Iceheart",
                    "Livia sas Junius",
                    "White-robed Ascian",
                    "Lahabrea",
                    "Y'shtola",
                    "Y'da",
                    "Thancred",
                    "Lyna",
                    "Ameliance",
                    "Pipin",
                    "Lyna",
                    "Beq Lugg"
                };
                foreach (var item in _namesToRemove) {
                    npcNames.Remove(item);
                }
                foreach (var item in objects) {
                    foreach (var name in npcNames) {
                        if (item.Name.TextValue.Contains(name)) {
                            Character character = item as Character;
                            if (character != null) {
                                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                                race = character.Customize[(int)CustomizeIndex.Race];
                                body = character.Customize[(int)CustomizeIndex.ModelType];
#if DEBUG
                                _plugin.Chat.Print(item.Name.TextValue + " is model type " + body);
#endif
                                return character;
                            }
                            return item;
                        }
                    }
                }
            } else {
                foreach (var item in objects) {
                    if (item.Name.TextValue == npcName) {
                        _namesToRemove.Add(npcName);
                        Character character = item as Character;
                        if (character != null) {
                            gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                            race = character.Customize[(int)CustomizeIndex.Race];
                            body = character.Customize[(int)CustomizeIndex.ModelType];
#if DEBUG
                            _plugin.Chat.Print(item.Name.TextValue + " is model type " + body);
#endif
                            return character;
                        }
                        return item;
                    }
                }
            }
            return null;
        }

        private string StripPlayerNameFromNPCDialogue(string value) {
            string[] mainCharacterName = _clientState.LocalPlayer.Name.TextValue.Split(" ");
            return value.Replace(mainCharacterName[0], null).Replace(mainCharacterName[1], null);
        }
        private bool CheckIfshouldUseSmbPitch(string npcName) {
            foreach (var value in NPCVoiceMapping.GetEchoType()) {
                if (npcName.Contains(value.Key)) {
                    return value.Value;
                }
            }
            return false;
        }

        private float CheckForDefinedPitch(string npcName) {
            foreach (var value in NPCVoiceMapping.GetPitchValues()) {
                if (npcName.Contains(value.Key)) {
                    return value.Value;
                }
            }
            return 1;
        }
        private string PhoneticLexiconCorrection(string value) {
            List<KeyValuePair<string, string>> phoneticPronunciations = new List<KeyValuePair<string, string>>();
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Urianger", "Uriawnjay"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Alphinaud", "Alphinau"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Alisaie", "Allizay"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ala Mhigo", "Awla Meego"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ala Mhigan", "Awla Meegan"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Amalj'aa", "Amalja"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("gysahl", "gisawl"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ramuh", "Ramoo"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Papalymo", "Papaleemo"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Gridania", "Gridawnia"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ascian", "Assiyin"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("magitek", "majitech"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Sahagin", "Sahawgin"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("sahagin", "sahawgin"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Eorzea", "Aorzea"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Bahamut", "Bahawmuht"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Lyse", "Leece"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("─", ","));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("...?", "?"));
            string newValue = value;
            foreach (KeyValuePair<string, string> pronunciation in phoneticPronunciations) {
                newValue = newValue.Replace(pronunciation.Key, pronunciation.Value);
            }
            return newValue;
        }

        private float CalculatePitchBasedOnTraits(string value, bool gender, byte race, byte body, float range) {
            string lowered = value.ToLower();
            Random random = new Random(GetSimpleHash(value));
            bool isTinyRace = lowered.Contains("way") || body == 4;
            float pitch = CheckForDefinedPitch(value);
            float pitchOffset = (((float)random.Next(-100, 100) / 100f) * range);
            if (!gender && body != 4) {
                if (body == 4) {
                    switch (gender) {
                        case false:
                            pitchOffset = (((float)Math.Abs(random.Next(-100, 100)) / 100f) * range);
                            isTinyRace = false;
                            break;
                        case true:
                            pitchOffset = (((float)random.Next(0, 100) / 100f) * range);
                            break;

                    }
                }
            }
            if (pitch == 1) {
                return (isTinyRace ? 1.15f : 1) + pitchOffset;
            } else {
                return pitch;
            }
        }
        private AddonTalkState GetTalkAddonState() {
            if (!this.addonTalkManager.IsVisible()) {
                return default;
            }

            var addonTalkText = this.addonTalkManager.ReadText();
            return addonTalkText != null
                ? new AddonTalkState(addonTalkText.Speaker, addonTalkText.Text)
                : default;
        }

        public string PickVoiceBasedOnTraits(string npcName, bool gender, byte race, byte body) {
            string[] maleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, false);
            string[] femaleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, true);
            string[] femaleViera = new string[] { "Aet", "Cet", "Uet" };
            foreach (KeyValuePair<string, string> voice in NPCVoiceMapping.GetExtrasVoiceMappings()) {
                if (npcName.Contains(voice.Key)) {
                    return voice.Value;
                }
            }
            if (npcName.EndsWith("way")) {
                return "Lrit";
            }
            if (npcName.ToLower().Contains("kup") || npcName.ToLower().Contains("puk") || npcName.ToLower().Contains("mog") || npcName.ToLower().Contains("moogle")) {
                return "Kop";
            }
            switch (race) {
                case 0:
                case 1:
                case 2:
                case 3:
                case 5:
                case 6:
                case 4:
                case 7:
                    return !gender && body != 4 ?
                    PickVoice(npcName, maleVoices) :
                    PickVoice(npcName, femaleVoices);
                case 8:
                    return gender ? PickVoice(npcName, femaleViera) :
                    PickVoice(npcName, maleVoices);
            }
            return "";
        }

        public string[] GetVoicesBasedOnTerritory(uint territory, bool gender) {
            string[] maleVoices = new string[] { "Mciv", "Zin", "udm1", "gm1", "Beggarly", "gnat", "ig1", "thord", "vark", "ckeep", "pide", "motanist", "lator", "sail", "lodier" };
            string[] femaleThavnair = new string[] { "tf1", "tf2", "tf3", "tf4" };
            string[] femaleVoices = new string[] { "Maiden", "Dla", "irhm", "ouncil", "igate" };
            string[] maleThavnair = new string[] { "tm1", "tm2", "tm3", "tm4" };
            string[] femaleViera = new string[] { "Aet", "Cet", "Uet" };
            switch (territory) {
                case 963:
                case 957:
                    return gender ? femaleThavnair : maleThavnair;
                default:
                    return gender ? femaleVoices : maleVoices;
            }

        }

        private string PickVoice(string name, string[] choices) {
            Random random = new Random(GetSimpleHash(name));
            return choices[random.Next(0, choices.Length)];
        }

        public void Dispose() {

        }
    }
}
