using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using RoleplayingMediaCore;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud.Voice;
using System;
using System.Diagnostics;
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
        private IDalamudPluginInterface _pluginInterface;
        private string _stringValue = "";
        private EventHandler<string> _currentEvent;
        private BetterComboBox _characterList = new BetterComboBox("CharacterList", new string[] { "" }, 100);
        private string _currentVoiceLine = "";
        private int _voiceLinesCount;
        private SpeechRecordingManager _speechRecordingManager;
        private int _currentVoiceLineIndex = 0;
        private string _currentCharacter;

        public VoiceEditor(IDalamudPluginInterface pluginInterface) :
            base("Voice Editor", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
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
        private void CheckForMasterList() {
            if (_npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Count > 0) {
                _characterList.Contents = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.ToArray();
            }
        }
        private void _npcVoiceManager_OnMasterListAcquired(object sender, EventArgs e) {
            CheckForMasterList();
        }

        private void _characterList_OnSelectedIndexChanged(object sender, EventArgs e) {
            _currentVoiceLineIndex = 0;
            _currentCharacter = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue.Keys.ElementAt(_characterList.SelectedIndex);
            _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(_currentVoiceLineIndex);
            _voiceLinesCount = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Count;
        }

        private void PreviousLine() {
            if (_currentVoiceLineIndex > 0) {
                _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(_currentVoiceLineIndex--);
            }
        }

        private void NextLine() {
            if (_currentVoiceLineIndex < _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Count) {
                _currentVoiceLine = _npcVoiceManager.CharacterVoicesMasterList.VoiceCatalogue[_currentCharacter].Keys.ElementAt(_currentVoiceLineIndex++);
            }
        }
        public override void Draw() {
            _characterList.Draw();
            ImGui.LabelText("##voiceLineLabel", "Voice line to record");
            ImGui.TextWrapped(_currentVoiceLine);
            if (ImGui.Button("Previous Line")) {
                PreviousLine();
            }
            ImGui.SameLine();
            ImGui.Text(_currentVoiceLineIndex + "/" + _voiceLinesCount);
            ImGui.SameLine();
            if (ImGui.Button("Next Line")) {
                NextLine();
            }
            if (ImGui.Button("Start Recording")) {
                _speechRecordingManager.RecordAudio("");
            }
            ImGui.SameLine();
            if (ImGui.Button("Stop Recording")) {
                CommitAudio();
            }
            if (ImGui.Button("Upload Character Pack")) {

            }
        }

        private async void CommitAudio() {
            await _npcVoiceManager.AddCharacterAudio(await _speechRecordingManager.StopRecording(), _currentVoiceLine, _currentCharacter);
        }
    }
}