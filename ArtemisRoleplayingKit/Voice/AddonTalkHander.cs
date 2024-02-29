using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFBardMusicPlayer.FFXIV;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.GeneratedSheets;
using NAudio.Lame;
using NAudio.Wave;
using RoleplayingVoice;
using SoundFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using static System.Windows.Forms.AxHost;
using SoundType = RoleplayingMediaCore.SoundType;
using Task = System.Threading.Tasks.Task;

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
        private InterceptedSound _currentDialoguePath;
        private FFXIVHook _hook;
        private MediaGameObject _currentSpeechObject;
        private bool _startedNewDialogue;
        private bool _textIsPresent;
        private bool _alreadyAddedEvent;
        Stopwatch _passthroughTimer = new Stopwatch();
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
                _blockAudioGeneration = e.isBlocking;
                _currentDialoguePath = e;
                if (!_blockAudioGeneration) {
                    _passthroughTimer.Restart();
                }
            }
        }

        private async void Framework_Update(IFramework framework) {
            if (_plugin.Filter.IsCutsceneDetectionNull()) {
                if (!_alreadyAddedEvent) {
                    _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
                    _alreadyAddedEvent = true;
                }
            }
            if (_clientState.IsLoggedIn && !_plugin.Config.NpcSpeechGenerationDisabled) {
                var state = GetTalkAddonState();
                if (state != null && !string.IsNullOrEmpty(state.Text) && state.Speaker != "???") {
                    _textIsPresent = true;
                    if (state.Text != _lastText) {
                        _lastText = state.Text;
                        if (!_blockAudioGeneration) {
                            await NPCText(state.Speaker, state.Text.TrimStart('.'));
                        } else {
#if DEBUG
                            DumpCurrentAudio();
#endif
                        }
                        _currentDialoguePath = null;
                    } else {
                        //bool value = await _plugin.MediaManager.CheckAudioStreamIsPlaying(_currentSpeechObject);
                        //if (!value && _startedNewDialogue) {
                        //    _hook.FocusWindow();
                        //    _hook.SendAsyncKey(Keys.NumPad0);
                        //    _hook.SendSyncKey(Keys.NumPad0);
                        //    _startedNewDialogue = false;
                        //}
                    }
                } else {
                    if (_currentDialoguePath != null && !_blockAudioGeneration && _passthroughTimer.ElapsedMilliseconds >= 20) {
                        ScdFile scdFile = GetScdFile(_currentDialoguePath.SoundPath);
                        WaveStream stream = scdFile.Audio[0].Data.GetStream();
                        try {
                            var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                            _plugin.MediaManager.PlayAudioStream(new MediaGameObject(_clientState.LocalPlayer),
                                pcmStream, SoundType.NPC, false, false, 1, 0, _plugin.Config.AutoTextAdvance ? delegate {
                                    _hook.FocusWindow();
                                    _hook.SendAsyncKey(Keys.NumPad0);
                                    _hook.SendSyncKey(Keys.NumPad0);
                                }
                            : null);
                        } catch (Exception e) {
                            _plugin.Chat.Print(e.Message);
                        }
                        _currentDialoguePath = null;
                    }
                    if (_currentSpeechObject != null) {
                        var otherData = _clientState.LocalPlayer.OnlineStatus;
                        if (otherData.Id != 15) {
                            _plugin.MediaManager.StopAudio(_currentSpeechObject);
                            _currentSpeechObject = null;
                            _currentDialoguePath = null;
                            _lastText = "";
                        }
                    }
                    _textIsPresent = false;
                }
                _blockAudioGeneration = false;
            }
        }

        private void DumpCurrentAudio() {
            if (_currentDialoguePath != null) {
                Directory.CreateDirectory(_plugin.Config.CacheFolder + @"\Dump\");
                string name = "";
                string path = _plugin.Config.CacheFolder + @"\Dump\" + name + ".mp3";
                string pathWave = _plugin.Config.CacheFolder + @"\Dump\" + name + Guid.NewGuid() + ".wav";
                FileInfo fileInfo = null;
                try {
                    fileInfo = new FileInfo(path);
                } catch {

                }
                if (!fileInfo.Exists || fileInfo.Length < 7500000) {
                    ScdFile scdFile = GetScdFile(_currentDialoguePath.SoundPath);
                    WaveStream stream = scdFile.Audio[0].Data.GetStream();
                    try {
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
                //_hook.FocusWindow();
                //_hook.SendAsyncKey(Keys.NumPad0);
                //_hook.SendSyncKey(Keys.NumPad0);
                //_currentDialoguePath = null;
            }
        }

        public string ConvertRomanNumberals(string text) {
            string value = text;
            for (int i = 25; i > 5; i--) {
                value = value.Replace(Numerals.Roman.To(i), i.ToString());
            }
            return value;
        }
        public ScdFile GetScdFile(string soundPath) {
            if (_plugin.DataManager.FileExists(_currentDialoguePath.SoundPath)) {
                try {
                    var file = _plugin.DataManager.GetFile(_currentDialoguePath.SoundPath);
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
        private async Task<bool> NPCText(string npcName, SeString message) {
            try {
                bool gender = false;
                byte race = 0;
                Character npcObject = null;
                foreach (var item in objects) {
                    if (item.Name.TextValue == npcName) {
                        Character character = item as Character;
                        if (character != null) {
                            gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                            race = character.Customize[(int)CustomizeIndex.Race];
                            npcObject = character;
                        }
                    }
                }
                _currentSpeechObject = new MediaGameObject(npcObject != null ? npcObject : _clientState.LocalPlayer);
                string value = StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message.TextValue)));
                KeyValuePair<Stream, bool> stream =
                await _plugin.NpcVoiceManager.GetCharacterAudio(value, npcName, gender, PickVoiceBasedOnNameAndRace(npcName, race), false);
                var mp3Stream = new Mp3FileReader(stream.Key);
                _plugin.MediaManager.PlayAudioStream(_currentSpeechObject, mp3Stream
                , SoundType.NPC, true, CheckIfshouldUseSmbPitch(npcName), stream.Value ? CheckForDefinedPitch(npcName) : CalculatePitchBasedOnName(npcName, 0.09f), 0,
               _plugin.Config.AutoTextAdvance ? delegate {
                   _hook.FocusWindow();
                   _hook.SendAsyncKey(Keys.NumPad0);
                   _hook.SendSyncKey(Keys.NumPad0);
               }
                : null);
                _startedNewDialogue = true;
            } catch {
            }
            return true;
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
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Alisae", "Allizay"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ala Mhigo", "Ala Meego"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ala Mhigan", "Ala Meegan"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("gysahl", "gisawl"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ramuh", "Ramoo"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Papalymo", "Papaleemo"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Gridania", "Gridawnia"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Ascian", "Assiyin"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("─", ","));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("...?", "?"));
            string newValue = value;
            foreach (KeyValuePair<string, string> pronunciation in phoneticPronunciations) {
                newValue = newValue.Replace(pronunciation.Key, pronunciation.Value);
            }
            return newValue;
        }

        private float CalculatePitchBasedOnName(string value, float range) {
            string lowered = value.ToLower();
            Random random = new Random(GetSimpleHash(value));
            bool isTinyRace = lowered.Contains("way");
            return (isTinyRace ? 1.3f : 1) + (((float)random.Next(-100, 100) / 100f) * range);
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

        public string PickVoiceBasedOnNameAndRace(string npcName, int race) {
            foreach (KeyValuePair<string, string> voice in NPCVoiceMapping.GetExtrasVoiceMappings()) {
                if (npcName.Contains(voice.Key)) {
                    return voice.Value;
                }
            }
            switch (race) {
                case 8:
                    return PickVeiraVoice(new Random(GetSimpleHash(npcName)).Next(0, 3));
            }
            return "";
        }

        public string PickVeiraVoice(int voice) {
            string[] voices = new string[] {
                "Aet",
                "Cet",
                "Uet"
            };
            return voices[voice];
        }

        public void Dispose() {

        }
    }
}
