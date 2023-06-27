using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System.Linq;
using System.Net;
using System.Numerics;

namespace RoleplayingVoice {
    public class PluginWindow : Window {
        private string testText = "";
        private Configuration configuration;
        private string apiKey = "";
        private string characterName = "";
        private string characterVoice = "";
        private string serverIP;
        private string serverIPErrorMessage = "";
        private string characterNameErrorMessage = "";
        private bool isServerIPValid = true;
        private bool isCharacterNameValid = true;

        public PluginWindow() : base("Roleplaying Voice Config") {
            IsOpen = true;
            Size = new Vector2(810, 520);
            SizeCondition = ImGuiCond.FirstUseEver;
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
                }
            }
        }

        public DalamudPluginInterface PluginInteface { get; internal set; }

        public override async void Draw() {
            ImGui.Text("Server IP");
            ImGui.InputText("##serverIP", ref serverIP, 2000);

            ImGui.Text("Elevenlabs API Key");
            ImGui.InputText("##apiKey", ref apiKey, 2000);

            ImGui.Text("Character Name");
            ImGui.InputText("##characterName", ref characterName, 2000);

            ImGui.Text("Voice");
            ImGui.InputText("##characterVoice", ref characterVoice, 2000);

            if (ImGui.Button("Save"))
            {
                if (InputValidation())
                {
                    if (configuration != null)
                    {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        configuration.CharacterName = characterName;
                        configuration.CharacterVoice = characterVoice;
                        configuration.Save();
                        PluginInteface.SavePluginConfig(configuration);
                    }
                }
            }
            if (!isServerIPValid)
            {
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), serverIPErrorMessage);
            }
            if (!isCharacterNameValid)
            {
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), characterNameErrorMessage);
            }
            // Place button in bottom right + some padding / extra space
            var originPos = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 20f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Close"))
            {
                // Because we don't trust the user
                if (configuration != null)
                {
                    if (InputValidation())
                    {
                        configuration.ConnectionIP = serverIP;
                        configuration.ApiKey = apiKey;
                        configuration.CharacterName = characterName;
                        configuration.CharacterVoice = characterVoice;
                        configuration.Save();
                        PluginInteface.SavePluginConfig(configuration);
                        IsOpen = false;
                    }
                }
            }
            ImGui.SetCursorPos(originPos);
        }

        private bool InputValidation()
        {
            if (!IPAddress.TryParse(serverIP, out _))
            {
                serverIPErrorMessage = "Invalid Server IP! Please check the input.";
                isServerIPValid = false;
            }
            else
            {
                serverIPErrorMessage = string.Empty;
                isServerIPValid = true;
            }

            // AsciiLetter is A-Z and a-z, hence the extra check for space
            if (string.IsNullOrEmpty(characterName) || !characterName.All(c => char.IsAsciiLetter(c) || c == ' '))
            {
                characterNameErrorMessage = "Invalid Character Name! Please check the input.";
                isCharacterNameValid = false;
            }
            else
            {
                characterNameErrorMessage = string.Empty;
                isCharacterNameValid = true;
            }
            if (!isServerIPValid || !isCharacterNameValid)
                return false;
            //TODO: Add logic for API key
            return true;
        }
    }
}
