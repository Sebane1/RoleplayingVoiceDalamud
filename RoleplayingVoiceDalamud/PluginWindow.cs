using Dalamud.Game.ClientState;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using RoleplayingVoiceCore;
using System;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;

namespace RoleplayingVoice {
    public class PluginWindow : Window {
        private Configuration configuration;
        RoleplayingVoiceManager _manager = null;
        BetterComboBox voiceComboBox;
        private string apiKey = "";
        private string characterVoice = "";
        private string serverIP = "";
        private string serverIPErrorMessage = "";
        private string characterNameErrorMessage = "";
        private string apiKeyErrorMessage = "";
        private string[] _voiceList = new string[1] { "" };
        private bool isServerIPValid = false;
        private bool isCharacterNameValid = false;
        private bool isapiKeyValid = false;
        private bool characterVoiceActive = false;
        private bool apiKeyValidated = false;
        private bool SizeYChanged = false;
        private bool runOnLaunch = true;
        private bool save = false;
        private bool managerNull;
        private Vector2? initialSize;
        private Vector2? changedSize;
        private ClientState clientState;
        private bool _loggedIn;

        public PluginWindow() : base("Roleplaying Voice Config") {
            IsOpen = true;
            Size = new Vector2(295, 379);
            initialSize = Size;
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
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
                    apiKey = configuration.ApiKey != null && configuration.ApiKey.All(c => char.IsAsciiLetterOrDigit(c)) ? configuration.ApiKey : "";
                    characterVoiceActive = configuration.IsActive;
                }
            }
        }

        public DalamudPluginInterface PluginInterface { get; internal set; }
        public RoleplayingVoiceManager Manager
        {
            get => _manager; set
            {
                _manager = value;
                if (_manager != null)
                {
                    managerNull = false;
                    _manager.OnApiValidationComplete += _manager_OnApiValidationComplete;
                }
            }
        }

        internal ClientState ClientState {
            get => clientState;
            set {
                clientState = value;
                clientState.Login += ClientState_Login;
                clientState.Logout += ClientState_Logout;
            }
        }

        private void ClientState_Logout(object sender, EventArgs e) {
            characterVoice = "None";
            _loggedIn = false;
        }

        private void ClientState_Login(object sender, EventArgs e) {
            if (configuration.Characters != null) {
                if (configuration.Characters.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                    characterVoice = configuration.Characters[clientState.LocalPlayer.Name.TextValue]
                        != null ? configuration.Characters[clientState.LocalPlayer.Name.TextValue] : "";
                } else {
                    characterVoice = "None";
                }
            } else {
                characterVoice = "None";
            }
            _loggedIn = true;
            RefreshVoices();
        }
        public override void Draw() {
            ImGui.Text("Server IP");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##serverIP", ref serverIP, 2000);

            ImGui.Text("Elevenlabs API Key");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##apiKey", ref apiKey, 2000, ImGuiInputTextFlags.Password);

            if (!string.IsNullOrEmpty(configuration.ApiKey) && configuration.ApiKey.All(c => char.IsAsciiLetterOrDigit(c)) && isapiKeyValid && clientState.LocalPlayer != null) {
                if (voiceComboBox != null && _voiceList != null) {
                    if (_voiceList.Length > 0) {
                        ImGui.Text("Voice");
                        voiceComboBox.Draw();                    }
                }
                ImGui.Text("Is Active");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                ImGui.Checkbox("##characterVoiceActive", ref characterVoiceActive);
            }
            var testComboBoxPreButtons = ImGui.GetCursorPos();
            var originPos = ImGui.GetCursorPos();
            // Place save button in bottom left + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMax().X + 10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Save")) {
                if (InputValidation())
                {
                    if (configuration != null && !string.IsNullOrEmpty(apiKey))
                    {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        if (clientState.LocalPlayer != null) {
                            if (configuration.Characters == null) {
                                configuration.Characters = new System.Collections.Generic.Dictionary<string, string>();
                            }
                            configuration.Characters[clientState.LocalPlayer.Name.TextValue] = characterVoice != null ? characterVoice : "";
                        apiKeyValidated = false;
                        save = true;
                        if (_manager != null)
                        {
                            Task.Run(() => _manager.ApiValidation(apiKey));
                        }
                        else
                        {
                            managerNull = true;
                        }
                        configuration.IsActive = characterVoiceActive;
                        PluginInterface.SavePluginConfig(configuration);
                        configuration.Save();
                        RefreshVoices();
                        SizeYChanged = false;
                        changedSize = null;
                        Size = initialSize;
                    }
                }
            }
            ImGui.SetCursorPos(originPos);
            // Place close button in bottom right + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 20f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Close"))
            {
                // Because we don't trust the user
                if (InputValidation())
                {
                    if (configuration != null && !string.IsNullOrEmpty(apiKey))
                    {
                        apiKeyValidated = false;
                        save = true;
                        if (_manager != null)
                        {
                            Task.Run(() => _manager.ApiValidation(apiKey));
                        }
                        else
                        {
                            managerNull = true;
                        }
                    }
                }
                SizeYChanged = false;
                changedSize = null;
                Size = initialSize;
                IsOpen = false;
            }
            ImGui.SetCursorPos(originPos);
            if (!string.IsNullOrEmpty(apiKey) && runOnLaunch)
            {
                Task.Run(() => _manager.ApiValidation(apiKey));
                runOnLaunch = false;
            }
            else if (string.IsNullOrEmpty(apiKey))
            {
                apiKeyErrorMessage = "API Key is empty! Please check the input.";
                runOnLaunch = false;
            }
            var testPostButtons = ImGui.GetCursorPos();
            ImGui.BeginChild("ErrorRegion", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), false);
            var testStartChild = ImGui.GetCursorPos();
            if (!isServerIPValid) {
                // Calculate the number of lines taken by the wrapped text
                var requiredY = ImGui.CalcTextSize(serverIPErrorMessage).Y + 1f;
                var availableY = ImGui.GetContentRegionAvail().Y;
                var initialH = ImGui.GetCursorPos().Y;
                ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), serverIPErrorMessage);
                ImGui.PopTextWrapPos();
                var changedH = ImGui.GetCursorPos().Y;
                float textHeight = changedH - initialH;
                int textLines = (int)(textHeight / ImGui.GetTextLineHeight());

                // Check height and increase if necessarry
                if (availableY - requiredY * textLines < 1 && !SizeYChanged) {
                    SizeYChanged = true;
                    changedSize = GetSizeChange(requiredY, availableY, textLines, initialSize);
                    Size = changedSize;
                }
            }
            if (!isapiKeyValid && string.IsNullOrEmpty(apiKey))
            {
                // Calculate the number of lines taken by the wrapped text
                var testChildAPI = ImGui.GetCursorPos();
                var requiredY = ImGui.CalcTextSize(apiKeyErrorMessage).Y + 1f;
                var availableY = ImGui.GetContentRegionAvail().Y;
                var initialH = ImGui.GetCursorPos().Y;
                ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), apiKeyErrorMessage);
                ImGui.PopTextWrapPos();
                var changedH = ImGui.GetCursorPos().Y;
                float textHeight = changedH - initialH;
                int textLines = (int)(textHeight / ImGui.GetTextLineHeight());

                // Check height and increase if necessarry
                if (availableY - requiredY * textLines < 1 && !SizeYChanged)
                {
                    SizeYChanged = true;
                    changedSize = GetSizeChange(requiredY, availableY, textLines, initialSize);
                    Size = changedSize;
                }
            }
            if (!isCharacterNameValid) {
                // Calculate the number of lines taken by the wrapped text
                var requiredY = ImGui.CalcTextSize(characterNameErrorMessage).Y + 1f;
                var availableY = ImGui.GetContentRegionAvail().Y;
                var initialH = ImGui.GetCursorPos().Y;
                ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), characterNameErrorMessage);
                ImGui.PopTextWrapPos();
                var changedH = ImGui.GetCursorPos().Y;
                float textHeight = changedH - initialH;
                int textLines = (int)(textHeight / ImGui.GetTextLineHeight());

                // Check height and increase if necessarry
                if (availableY - requiredY * textLines < 1 && !SizeYChanged) {
                    SizeYChanged = true;
                    changedSize = GetSizeChange(requiredY, availableY, textLines, initialSize);
                    Size = changedSize;
                }
            }
            ImGui.EndChild();
            // Place button in bottom right + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 20f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Close")) {
                // Because we don't trust the user
                if (configuration != null) {
                    if (InputValidation()) {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        configuration.CharacterName = characterName != null ? characterName : "";
                        configuration.CharacterVoice = characterVoice != null ? characterVoice : "";
                        configuration.IsActive = characterVoiceActive;
                        configuration.Save();
                        PluginInterface.SavePluginConfig(configuration);
                        SizeYChanged = false;
                        changedSize = null;
                        Size = initialSize;
                        IsOpen = false;
                    }
                }
            }
            ImGui.EndChild();
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
            if (string.IsNullOrEmpty(characterName) || !characterName.All(c => char.IsAsciiLetter(c) || c == ' ' || c == '\'')) {
                characterNameErrorMessage = "Invalid Character Name! Please check the input.";
                isCharacterNameValid = false;
            } else {
                characterNameErrorMessage = string.Empty;
                isCharacterNameValid = true;
            }

            if (isServerIPValid && isCharacterNameValid)// && isapiKeyValid && apiKeyValidated)
            {
                return true;
            }
            return false;
        }

       private void _manager_OnApiValidationComplete(object sender, ValidationResult e)
        {
            if (e.ValidationSuceeded && !apiKeyValidated)
            {
                apiKeyErrorMessage = string.Empty;
                isapiKeyValid = true;
            }
            else if (!e.ValidationSuceeded && !apiKeyValidated)
            {
                apiKeyErrorMessage = "Invalid API Key! Please check the input.";
                isapiKeyValid = false;
            }
            apiKeyValidated = true;
            if (isapiKeyValid && save && apiKeyValidated)
            {
                configuration.ConnectionIP = serverIP;
                configuration.ApiKey = apiKey;
                configuration.CharacterName = characterName != null ? characterName : "";
                configuration.CharacterVoice = characterVoice != null ? characterVoice : "";
                configuration.IsActive = characterVoiceActive;
                PluginInterface.SavePluginConfig(configuration);
                configuration.Save();
                RefreshVoices();
                save = false;
            }
        }

        private Vector2? GetSizeChange(float requiredY, float availableY, int Lines, Vector2? initial) {
            // Height
            if (availableY - requiredY * Lines < 1) {
                Vector2? newHeight = new Vector2(initial.Value.X, initial.Value.Y + requiredY * Lines);
                return newHeight;
            }
            return initial;
        }

        public async void RefreshVoices() {
            if (_manager != null) {
                _voiceList = await _manager.GetVoiceList();
            }
            if (clientState.LocalPlayer != null) {
                if (configuration.Characters == null) {
                    configuration.Characters = new System.Collections.Generic.Dictionary<string, string>();
                }
                if (configuration.Characters.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                    if (voiceComboBox != null) {
                        if (_voiceList != null) {
                            voiceComboBox.Contents = _voiceList;
                            for (int i = 0; i < voiceComboBox.Contents.Length; i++) {
                                if (voiceComboBox.Contents[i].Contains(configuration.Characters[clientState.LocalPlayer.Name.TextValue])) {
                                    voiceComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
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
