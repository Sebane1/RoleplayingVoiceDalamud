using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFBardMusicPlayer.FFXIV;
using NAudio.Lame;
using NAudio.Wave;
using RoleplayingMediaCore;
using RoleplayingVoice;
using RoleplayingVoiceCore;
using SoundFilter;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using SoundType = RoleplayingMediaCore.SoundType;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkHandler {
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
                _blockAudioGeneration = true;
                _currentDialoguePath = e;
            }
        }

        private async void Framework_Update(IFramework framework) {
            if (_clientState.IsLoggedIn && !_plugin.Config.NpcSpeechGenerationDisabled) {
                var state = GetTalkAddonState();
                if (state != null) {
                    if (state.Text != _lastText) {
                        _lastText = state.Text;
                        if (!_blockAudioGeneration) {
                            NPCText(state.Speaker, state.Text);
                        } else {
#if DEBUG
                            if (_currentDialoguePath != null) {
                                Directory.CreateDirectory(_plugin.Config.CacheFolder + @"\Dump\");
                                string name = state.Speaker;
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
                                _hook.FocusWindow();
                                _hook.SendAsyncKey(Keys.NumPad0);
                                _hook.SendSyncKey(Keys.NumPad0);
                                _currentDialoguePath = null;
#endif
                            }
                        }
                        _blockAudioGeneration = false;
                    }
                }
            }
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

        private async void NPCText(string npcName, SeString message) {
            try {
                bool gender = false;
                GameObject npcObject = null;
                foreach (var item in objects) {
                    if (item.Name.TextValue == npcName) {
                        Character character = item as Character;
                        if (character != null) {
                            gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                            npcObject = character;
                        }
                    }
                }
                _plugin.MediaManager.PlayAudioStream(new MediaGameObject(npcObject != null ? npcObject : _clientState.LocalPlayer),
               new Mp3FileReader(await _plugin.NpcVoiceManager.GetCharacterAudio(message.TextValue, npcName, gender)), SoundType.NPC);
            } catch {

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
    }
}
