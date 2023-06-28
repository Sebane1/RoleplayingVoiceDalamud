using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using RoleplayingVoiceCore;
using System;
using System.Linq;
using System.Net;
using System.Numerics;

namespace RoleplayingVoice {
    public class PluginWindow : Window {
        private Configuration configuration;
        private string apiKey = "";
        private string characterName = "";
        private string characterVoice = "";
        private string serverIP = "";
        private string serverIPErrorMessage = "";
        private string characterNameErrorMessage = "";
        private bool isServerIPValid = true;
        private bool isCharacterNameValid = true;
        private bool characterVoiceActive = false;
        RoleplayingVoiceManager _manager = null;
        private string[] _voiceList = new string[1] { "" };
        BetterComboBox voiceComboBox;

        public PluginWindow() : base("Roleplaying Voice Config") {
            IsOpen = true;
            Size = new Vector2(810, 810);
            SizeCondition = ImGuiCond.FirstUseEver;
            voiceComboBox = new BetterComboBox("Voice List", _voiceList, 810);
            voiceComboBox.OnSelectedIndexChanged += VoiceComboBox_OnSelectedIndexChanged;
            voiceComboBox.SelectedIndex = 0;
        }

        private void VoiceComboBox_OnSelectedIndexChanged(object sender, EventArgs e) {
            if (voiceComboBox != null && _voiceList != null) {
                characterVoice = _voiceList[voiceComboBox.SelectedIndex];
            }
        }

        public Configuration Configuration {
            get => configuration;
            set {
                configuration = value;
                if (configuration != null) {
                    serverIP = configuration.ConnectionIP != null ? configuration.ConnectionIP.ToString() : "";
                    apiKey = configuration.ApiKey != null ? configuration.ApiKey : "";
                    characterName = configuration.CharacterName != null ? configuration.CharacterName : "";
                    characterVoice = configuration.CharacterVoice != null ? configuration.CharacterVoice : "";
                    characterVoiceActive = configuration.IsActive;
                }
            }
        }

        public DalamudPluginInterface PluginInteface { get; internal set; }
        public RoleplayingVoiceManager Manager { get => _manager; set => _manager = value; }

        public override async void Draw() {
            ImGui.Text("Server IP");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##serverIP", ref serverIP, 2000);

            ImGui.Text("Elevenlabs API Key");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##apiKey", ref apiKey, 2000, ImGuiInputTextFlags.Password);

            ImGui.Text("Character Name");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##characterName", ref characterName, 2000);

            if (voiceComboBox != null && _voiceList != null) {
                if (_voiceList.Length > 0) {
                    ImGui.Text("Voice");
                    voiceComboBox.Draw();
                }
            }

            ImGui.Text("Is Active");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.Checkbox("##characterVoiceActive", ref characterVoiceActive);

            if (ImGui.Button("Save")) {
                if (InputValidation()) {
                    if (configuration != null) {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        configuration.CharacterName = characterName;
                        configuration.CharacterVoice = characterVoice;
                        configuration.IsActive = characterVoiceActive;
                        configuration.Save();
                        PluginInteface.SavePluginConfig(configuration);
                    }
                }
            }
            if (!isServerIPValid) {
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), serverIPErrorMessage);
            }
            if (!isCharacterNameValid) {
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), characterNameErrorMessage);
            }
            // Place button in bottom right + some padding / extra space
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionMax().X - 100));
            ImGui.SameLine();
            if (ImGui.Button("Close")) {
                // Because we don't trust the user
                if (configuration != null) {
                    if (InputValidation()) {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        configuration.CharacterName = characterName;
                        configuration.CharacterVoice = characterVoice;
                        configuration.IsActive = characterVoiceActive;
                        configuration.Save();
                        PluginInteface.SavePluginConfig(configuration);
                        IsOpen = false;
                    }
                }
            }
        }

        private bool InputValidation() {
            if (!IPAddress.TryParse(serverIP, out _)) {
                serverIPErrorMessage = "Invalid Server IP! Please check the input.";
                isServerIPValid = false;
            } else {
                serverIPErrorMessage = string.Empty;
                isServerIPValid = true;
            }

            // AsciiLetter is A-Z and a-z, hence the extra check for space
            if (string.IsNullOrEmpty(characterName) || !characterName.All(c => char.IsAsciiLetter(c) || c == ' ')) {
                characterNameErrorMessage = "Invalid Character Name! Please check the input.";
                isCharacterNameValid = false;
            } else {
                characterNameErrorMessage = string.Empty;
                isCharacterNameValid = true;
            }
            if (!isServerIPValid || !isCharacterNameValid)
                return false;
            //TODO: Add logic for API key
            return true;
        }

        public async void RefreshVoices() {
            if (_manager != null) {
                _voiceList = await _manager.GetVoiceList();
            }
            if (voiceComboBox != null) {
                if (_voiceList != null) {
                    voiceComboBox.Contents = _voiceList;
                    for (int i = 0; i < voiceComboBox.Contents.Length; i++) {
                        if (voiceComboBox.Contents[i].Contains(characterVoice)) {
                            voiceComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        internal class BetterComboBox {
            string _label = "";
            int _width = 0;
            int index = -1;
            int _lastIndex = 0;
            bool _enabled = true;
            string[] _contents = new string[1] { "" };
            public event EventHandler OnSelectedIndexChanged;
            public string Text { get { return index > -1 ? _contents[index] : ""; } }
            public BetterComboBox(string _label, string[] contents, int index, int width = 100) {
                if (Label != null) {
                    this._label = _label;
                }
                this._width = width;
                this.index = index;
                if (contents != null) {
                    this._contents = contents;
                }
            }

            public string[] Contents { get => _contents; set => _contents = value; }
            public int SelectedIndex { get => index; set => index = value; }
            public int Width { get => (_enabled ? _width : 0); set => _width = value; }
            public string Label { get => _label; set => _label = value; }
            public bool Enabled { get => _enabled; set => _enabled = value; }

            public void Draw() {
                if (_enabled) {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                    if (_label != null && _contents != null) {
                        if (_contents.Length > 0) {
                            ImGui.Combo("##" + _label, ref index, _contents, _contents.Length);
                        }
                    }
                }
                if (index != _lastIndex) {
                    if (OnSelectedIndexChanged != null) {
                        OnSelectedIndexChanged.Invoke(this, EventArgs.Empty);
                    }
                }
                _lastIndex = index;
            }
        }
    }
}
