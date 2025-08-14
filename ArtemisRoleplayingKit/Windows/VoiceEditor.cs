using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Dalamud.Bindings.ImGui;
using ImGuiScene;
using RoleplayingMediaCore;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud;
using RoleplayingVoiceDalamud.Voice;
using RoleplayingVoiceDalamudWrapper;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static RoleplayingVoice.PluginWindow;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    public class VoiceEditor : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        private IDalamudTextureWrap textureWrap;
        private NPCVoiceManager _npcVoiceManager;
        private MediaManager _mediaManager;
        private IDalamudPluginInterface _pluginInterface;
        private string _stringValue = "";
        private EventHandler<string> _currentEvent;
        private BetterComboBox _characterList = new BetterComboBox("CharacterList", new string[] { "" }, 500);
        private string _currentVoiceLine = "";
        private string _voiceLinePath;
        private int _voiceLinesCount;
        private SpeechRecordingManager _speechRecordingManager;
        private int _currentVoiceLineIndex = 0;
        private string _currentCharacter = "";
        private string _searchText = "";

        public VoiceEditor(IDalamudPluginInterface pluginInterface) :
            base("NPC Voice Editor", ImGuiWindowFlags.None, false) {
            //IsOpen = true;
            _pluginInterface = pluginInterface;
            IsOpen = false;
            _speechRecordingManager = new SpeechRecordingManager();
            _characterList.OnSelectedIndexChanged += _characterList_OnSelectedIndexChanged;
        }

        public NPCVoiceManager NPCVoiceManager {
            get => _npcVoiceManager;
            set {
                _npcVoiceManager = value;
                _npcVoiceManager.OnMasterListAcquired += _npcVoiceManager_OnMasterListAcquired;
                CheckForMasterList();
            }
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        private void CheckForMasterList() {
            if (_npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Count > 0) {
                _currentVoiceLineIndex = 0;
                _characterList.Contents = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.ToArray();
                _characterList.SelectedIndex = 0;
                RefreshCharacterSelection();
            }
        }
        private void _npcVoiceManager_OnMasterListAcquired(object sender, EventArgs e) {
            CheckForMasterList();
        }
        public override void OnOpen() {
            CheckForMasterList();
            _npcVoiceManager.GetVoiceLineMasterList();
        }
        private void _characterList_OnSelectedIndexChanged(object sender, EventArgs e) {
            _currentVoiceLineIndex = 0;
            RefreshCharacterSelection();
        }

        private void RefreshCharacterSelection() {
        if (_npcVoiceManager.CharacterVoicesMasterList != null && _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue != null) {
            _currentCharacter = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.ElementAt(_characterList.SelectedIndex);
            _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(_currentVoiceLineIndex);
            _voiceLinesCount = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Count;
            _voiceLinePath = _npcVoiceManager.VoicelinePath(_currentVoiceLine, _currentCharacter);
            }
        }

        private void PreviousLine() {
            if (_currentVoiceLineIndex > 0) {
                _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(--_currentVoiceLineIndex);
                _voiceLinePath = _npcVoiceManager.VoicelinePath(_currentVoiceLine, _currentCharacter);
            }
        }

        private void NextLine() {
            if (_currentVoiceLineIndex < _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Count - 1) {
                _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(++_currentVoiceLineIndex);
                _voiceLinePath = _npcVoiceManager.VoicelinePath(_currentVoiceLine, _currentCharacter);
            }
        }
        public override void Draw() {
            ImGui.TextWrapped("Use this window to volunteer your own recorded voice lines that can be submitted for use in Accessibility Dialogue." +
                "\r\n\r\nYou will need to record all lines for the selected character to be able to upload.\r\n");
            ImGui.InputText("##Search", ref _searchText, 300);
            ImGui.SameLine();
            if (ImGui.Button("Search##NPC")) {
                _currentVoiceLineIndex = 0;
                _currentCharacter = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.FirstOrDefault(value => value.ToLower().Contains(_searchText.ToLower()));
                _voiceLinePath = _npcVoiceManager.VoicelinePath(_currentVoiceLine, _currentCharacter);
                _characterList.SelectedIndex = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.ToList().IndexOf(_currentCharacter);
            }
            ImGui.Text("Selected NPC Character:");
            ImGui.SameLine();
            _characterList.Width = (int)ImGui.GetWindowSize().X - 300;
            _characterList.Draw();
            ImGui.LabelText("##voiceLineLabel", "Voice line to record:");
            ImGui.TextWrapped(_currentVoiceLine);
            ImGui.Dummy(new Vector2(0, 10));
            if (ImGui.Button("Previous Line")) {
                PreviousLine();
            }
            ImGui.SameLine();
            ImGui.Text((_currentVoiceLineIndex + 1) + "/" + _voiceLinesCount);
            ImGui.SameLine();
            if (ImGui.Button("Next Line")) {
                NextLine();
            }
            if (_speechRecordingManager.IsRecording ? ImGui.Button("Stop Recording") : ImGui.Button("Start Recording")) {
                if (_speechRecordingManager.IsRecording) {
                    CommitAudio();
                } else {
                    _speechRecordingManager.RecordAudio("");
                }
            }
            if (File.Exists(_voiceLinePath) && !_speechRecordingManager.IsRecording) {
                ImGui.SameLine();
                if (ImGui.Button("Listen To Recording")) {
                    _mediaManager.PlayMedia(new DummyObject() { Name = _currentCharacter }, _voiceLinePath, SoundType.NPC, true);
                }
            }
            if (_npcVoiceManager.GetFileCountForCharacter(_currentCharacter) < _voiceLinesCount) {
                ImGui.BeginDisabled(true);
            }
            ImGui.SameLine();
            if (ImGui.Button($"Upload Voice Line Pack ({_currentCharacter.Split("_")[0]})")) {
                if (_npcVoiceManager.GetFileCountForCharacter(_currentCharacter) >= _voiceLinesCount) {
                    _npcVoiceManager.UploadCharacterVoicePack(_currentCharacter);
                }
            }
            if (_npcVoiceManager.GetFileCountForCharacter(_currentCharacter) < _voiceLinesCount) {
                ImGui.EndDisabled();
            }
        }

        private async void CommitAudio() {
            _npcVoiceManager.AddCharacterAudio(await _speechRecordingManager.StopRecording(), _currentVoiceLine, _currentCharacter);
        }
    }
}