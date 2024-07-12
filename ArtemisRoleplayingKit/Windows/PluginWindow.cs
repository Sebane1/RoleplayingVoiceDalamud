using Dalamud.Game.ClientState;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using RoleplayingMediaCore;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Collections.Generic;
using Dalamud.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Hooking;
using FFBardMusicPlayer.FFXIV;
using System.Windows.Forms;
using RoleplayingVoiceCore;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Xml.Linq;
using RoleplayingVoiceDalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using static System.Net.WebRequestMethods;

namespace RoleplayingVoice {
    public class PluginWindow : Window {
        private Configuration configuration;
        RoleplayingMediaManager _manager = null;
        BetterComboBox _voiceEngineComboBox = new BetterComboBox("Generative Voice Engine", new string[] { "Elevenlabs", "XTTS (Hyper Experimental)" }, 0, 390);
        BetterComboBox voiceComboBox;
        BetterComboBox voicePackComboBox;
        BetterComboBox _twitchDefaultPlayback = new BetterComboBox("##twitchDefaultPlayback",
            new string[] { "Start Stream With Video", "Start Stream With Audio" }, 200);
        BetterComboBox _audioOutputType;
        private FileDialogManager fileDialogManager;
        private FFXIVHook hook;
        private IClientState clientState;

        private string apiKey = "";
        private string characterVoice = "";
        private string serverIP = "";
        private string serverIPErrorMessage = string.Empty;
        private string apiKeyErrorMessage = string.Empty;
        private string managerNullMessage = string.Empty;
        private string fileMoveMessage = string.Empty;
        private string[] _voiceList = new string[1] { "" };
        private string cacheFolder;
        private string attemptedMoveLocation = null;

        private bool isServerIPValid = false;
        private bool isApiKeyValid = false;
        private bool _aiVoiceActive = false;
        private bool apiKeyValidated = false;
        private bool SizeYChanged = false;
        private bool runOnLaunch = true;
        private bool save = false;
        private bool fileMoveSuccess;
        private bool managerNull;
        private bool _aggressiveCaching;

        private Vector2? initialSize;
        private Vector2? changedSize;

        private float _playerCharacterVolume = 1;
        private float _otherCharacterVolume = 1;
        private float _unfocusedCharacterVolume = 1;
        private bool _useServer;
        private bool _ignoreWhitelist;
        private int _currentWhitelistItem = 0;
        private string characterVoicePack;
        private string[] _voicePackList = new string[1] { "" };
        private string _newVoicePackName = "";
        private bool _characterVoicePackActive;
        private float _loopingSFXVolume = 1;
        private float _livestreamVolume = 1;
        private float _npcVolume = 1;
        private string _streamPath;
        private bool _tuneIntoTwitchStreams;
        private bool _tuneIntoTwitchStreamPrompt;
        private bool _readQuestObjectives;
        private bool _readLocationAndToastNotifications;
        private bool _performEmotesBasedOnWrittenText;
        private bool _moveSCDBasedModsToPerformanceSlider;
        private bool _npcSpeechGenerationDisabled;
        private bool _npcAutoTextAdvance;
        private bool _replaceVoicedARRCutscenes;
        private bool _refreshing;
        private bool _qualityAssuranceMode;
        private bool _twitchStreamTriggersIfShouter;
        private float _npcPlaybackSpeed;
        private bool _ignoreRetainerSpeech;
        private bool _debugMode;
        private FileSystemWatcher _fileSystemWatcher;
        private bool _lowPerformanceMode;
        private float _spatialAudioAccuracy;
        private static readonly object fileLock = new object();
        private static readonly object currentFileLock = new object();
        public event EventHandler RequestingReconnect;
        public event EventHandler<MessageEventArgs> OnWindowOperationFailed;

        public PluginWindow() : base("Artemis Roleplaying Kit Config") {
            //IsOpen = true;
            Size = new Vector2(700, 750);
            initialSize = Size;
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
            voiceComboBox = new BetterComboBox("Generative Voice List", _voiceList, 0, 390);
            voicePackComboBox = new BetterComboBox("Voice Pack List", _voicePackList, 0, 235);
            _voiceEngineComboBox.OnSelectedIndexChanged += VoiceEngineComboBox_OnSelectedIndexChanged;
            voiceComboBox.OnSelectedIndexChanged += VoiceComboBox_OnSelectedIndexChanged;
            voicePackComboBox.OnSelectedIndexChanged += VoicePackComboBox_OnSelectedIndexChanged;
            fileDialogManager = new FileDialogManager();
            hook = new FFXIVHook();
            hook.Hook(Process.GetCurrentProcess());
        }

        private void VoiceEngineComboBox_OnSelectedIndexChanged(object sender, EventArgs e) {
            RefreshVoices();
        }

        public override void OnOpen() {
            base.OnOpen();
            PluginReference.CheckAnimationMods(new string[1], "", PluginReference.ClientState.LocalPlayer as ICharacter, false);
            PluginReference.NpcPersonalityWindow.OnOpen();
        }
        private void VoicePackComboBox_OnSelectedIndexChanged(object sender, EventArgs e) {
            if (voicePackComboBox != null && _voicePackList != null && !_refreshing) {
                characterVoicePack = _voicePackList[voicePackComboBox.SelectedIndex];
                Save();
            }
        }

        private void VoiceComboBox_OnSelectedIndexChanged(object sender, EventArgs e) {
            if (voiceComboBox != null && _voiceList != null && !_refreshing) {
                characterVoice = _voiceList[voiceComboBox.SelectedIndex];
                Save();
            }
        }

        public Configuration Configuration {
            get => configuration;
            set {
                configuration = value;
                if (configuration != null) {
                    _audioOutputType = new BetterComboBox("##audioOutputMethod", Enum.GetNames(typeof(AudioOutputType)).ToArray(), 0, 200);
                    serverIP = configuration.ConnectionIP != null ? configuration.ConnectionIP.ToString() : "";
                    apiKey = !string.IsNullOrEmpty(configuration.ApiKey) ? configuration.ApiKey : "";
                    _aiVoiceActive = configuration.AiVoiceActive;
                    _characterVoicePackActive = configuration.VoicePackIsActive;
                    _playerCharacterVolume = configuration.PlayerCharacterVolume;
                    _otherCharacterVolume = configuration.OtherCharacterVolume;
                    _unfocusedCharacterVolume = configuration.UnfocusedCharacterVolume;
                    _loopingSFXVolume = configuration.LoopingSFXVolume;
                    _livestreamVolume = configuration.LivestreamVolume;
                    _npcVolume = configuration.NpcVolume;
                    _aggressiveCaching = configuration.UseAggressiveSplicing;
                    _useServer = configuration.UsePlayerSync;
                    _tuneIntoTwitchStreams = configuration.TuneIntoTwitchStreams;
                    _twitchDefaultPlayback.SelectedIndex = configuration.DefaultTwitchOpen;
                    _ignoreWhitelist = configuration.IgnoreWhitelist;
                    _performEmotesBasedOnWrittenText = configuration.PerformEmotesBasedOnWrittenText;
                    _moveSCDBasedModsToPerformanceSlider = configuration.MoveSCDBasedModsToPerformanceSlider;
                    _npcSpeechGenerationDisabled = configuration.NpcSpeechGenerationDisabled;
                    _npcAutoTextAdvance = configuration.AutoTextAdvance;
                    _replaceVoicedARRCutscenes = configuration.ReplaceVoicedARRCutscenes;
                    _audioOutputType.SelectedIndex = configuration.AudioOutputType;
                    _qualityAssuranceMode = configuration.QualityAssuranceMode;
                    _streamPath = configuration.StreamPath;
                    _twitchStreamTriggersIfShouter = configuration.TwitchStreamTriggersIfShouter;
                    _npcPlaybackSpeed = configuration.NPCSpeechSpeed;
                    _ignoreRetainerSpeech = configuration.DontVoiceRetainers;
                    _debugMode = configuration.DebugMode;
                    _tuneIntoTwitchStreamPrompt = configuration.TuneIntoTwitchStreamPrompt;
                    _readQuestObjectives = configuration.ReadQuestObjectives;
                    _readLocationAndToastNotifications = configuration.ReadLocationsAndToastNotifications;
                    _lowPerformanceMode = configuration.LowPerformanceMode;
                    _spatialAudioAccuracy = configuration.SpatialAudioAccuracy;
                    PluginReference.NpcPersonalityWindow.LoadNPCCharacters(configuration.CustomNpcCharacters);
                    _voiceEngineComboBox.SelectedIndex = configuration.PlayerVoiceEngine;

                    cacheFolder = configuration.CacheFolder ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPVoiceCache");
                    if (configuration.Characters != null && clientState.LocalPlayer != null) {
                        if (configuration.Characters.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                            characterVoice = configuration.Characters[clientState.LocalPlayer.Name.TextValue];
                        }
                    }
                    if (configuration.CharacterVoicePacks != null && clientState.LocalPlayer != null) {
                        if (configuration.CharacterVoicePacks.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                            characterVoicePack = configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue];
                        }
                    }
                    RefreshVoices();
                    try {
                        Directory.CreateDirectory(cacheFolder + @"\VoicePack\");
                        if (_fileSystemWatcher != null) {
                            _fileSystemWatcher?.Dispose();
                        }
                        _fileSystemWatcher = new FileSystemWatcher();
                        _fileSystemWatcher.Created += _fileSystemWatcher_Created;
                        _fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                        _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                        _fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                        _fileSystemWatcher.Path = cacheFolder + @"\VoicePack\";
                        _fileSystemWatcher.EnableRaisingEvents = true;
                        _fileSystemWatcher.BeginInit();
                    } catch (Exception e) {
                        Plugin.PluginLog.Warning(e, e.Message);
                    }
                }
            }
        }

        private void _fileSystemWatcher_Renamed(object sender, RenamedEventArgs e) {
            RefreshVoices();
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e) {
            RefreshVoices();
        }

        private void _fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e) {
            RefreshVoices();
        }

        private void _fileSystemWatcher_Created(object sender, FileSystemEventArgs e) {
            RefreshVoices();
        }

        public IDalamudPluginInterface PluginInterface { get; internal set; }
        public RoleplayingMediaManager Manager {
            get => _manager; set {
                _manager = value;
                if (_manager != null) {
                    managerNullMessage = string.Empty;
                    managerNull = false;
                    _manager.OnApiValidationComplete += _manager_OnApiValidationComplete;
                }
            }
        }

        internal IClientState ClientState {
            get => clientState;
            set {
                clientState = value;
                clientState.Login += ClientState_Login;
                clientState.Logout += ClientState_Logout;
            }
        }

        public Plugin PluginReference { get; internal set; }
        public event EventHandler OnMoveFailed;

        private void ClientState_Logout() {
            characterVoice = "None";
        }

        private void ClientState_Login() {
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
            if (configuration.CharacterVoicePacks != null) {
                if (configuration.CharacterVoicePacks.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                    characterVoicePack = configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue]
                        != null ? configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue] : "";
                } else {
                    characterVoicePack = "None";
                }
            } else {
                characterVoicePack = "None";
            }
            if (_aiVoiceActive) {
                RefreshVoices();
            }
        }
        public override void Draw() {
            if (clientState.IsLoggedIn) {
                fileDialogManager.Draw();
                if (ImGui.BeginTabBar("ConfigTabs")) {
                    if (ImGui.BeginTabItem("Player Voice")) {
                        DrawGeneral();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Player Sync")) {
                        DrawPlayerSync();
                        ImGui.EndTabItem();
                    }
                    if (PluginReference.ClientState.ClientLanguage == ClientLanguage.English) {
                        if (ImGui.BeginTabItem("NPC Dialogue")) {
                            DrawNPCDialogue();
                            ImGui.EndTabItem();
                        }
                    }
                    if (ImGui.BeginTabItem("Custom NPC")) {
                        PluginReference.NpcPersonalityWindow.Draw();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Animation")) {
                        DrawAnimationWindow();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Twitch")) {
                        DrawTwitch();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Volume")) {
                        DrawVolume();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Extra's")) {
                        DrawExtras();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                DrawErrors();
                SaveAndClose();
            } else {
                ImGui.TextUnformatted("Please login to access and configure settings.");
            }
        }

        private void DrawAnimationWindow() {
            PluginReference.AnimationCatalogue.Draw();
        }

        private void DrawTwitch() {
            ImGui.Checkbox("##useTwitchStreams", ref _tuneIntoTwitchStreams);
            ImGui.SameLine();
            ImGui.Text("Tune Into Twitch Streams");
            ImGui.TextWrapped("Intended for venues where DJ's are playing. Audio will play inside the venue as soon as their Twitch URL is advertised in yell chat.");
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Checkbox("##useTwitchStreamNotification", ref _tuneIntoTwitchStreamPrompt);
            ImGui.SameLine();
            ImGui.Text("Public Stream Notifications");
            ImGui.TextWrapped("When enabled, you will be offered to quickly join public livestreams shared in a zone.");
            ImGui.Dummy(new Vector2(0, 10));
            _twitchDefaultPlayback.Width = (int)ImGui.GetContentRegionMax().X;
            _twitchDefaultPlayback.Draw();
            ImGui.TextWrapped("When a twitch stream opens up, this defines the default behaviour of whether it starts with video or with audio.");
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Checkbox("##useTwitchStreamsWhenShouter", ref _twitchStreamTriggersIfShouter);
            ImGui.SameLine();
            ImGui.Text("Trigger twitch streams when shouter.");
            ImGui.TextWrapped("Twitch streams will still trigger despite being the twitch stream shouter");
            ImGui.Dummy(new Vector2(0, 10));
        }

        private void DrawExtras() {
            ImGui.Checkbox("##useEmoteBasedOnMessageText", ref _performEmotesBasedOnWrittenText);
            ImGui.SameLine();
            ImGui.Text("Perform Emotes Based On Written Text");
            ImGui.TextWrapped("Your character will emote based on what you write in custom emotes. We recommend turning off log messages for emotes before using this feature.");
            ImGui.Checkbox("##debugMode", ref _debugMode);
            ImGui.SameLine();
            ImGui.Text("A bunch of debug information will be posted in chat. Only useful for developers.");
            ImGui.Dummy(new Vector2(0, 10));
            try {
                ImGui.TextWrapped("You can now add custom photo frames! You can access these while the game UI is hidden in Gpose!");
                PluginReference.DragDrop.CreateImGuiSource("TextureDragDrop", m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m => {
                    ImGui.TextUnformatted($"Dragging texture for import:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
                    return true;
                });
                if (ImGui.Button("Open Custom Photo Frames Folder", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                    string path = Path.Combine(PluginReference.Config.CacheFolder, @"PhotoFrames\");
                    ProcessStartInfo ProcessInfo;
                    Process Process; ;
                    try {
                        Directory.CreateDirectory(path);
                    } catch {
                    }
                    ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + path + @"""");
                    ProcessInfo.UseShellExecute = true;
                    Process = Process.Start(ProcessInfo);
                }
                if (PluginReference.DragDrop.CreateImGuiTarget("TextureDragDrop", out var files, out _)) {
                    if (ValidTextureExtensions.Contains(Path.GetExtension(files[0]))) {
                        string path = Path.Combine(PluginReference.Config.CacheFolder, @"PhotoFrames\");
                        foreach (string file in files) {
                            System.IO.File.Copy(files[0], Path.Combine(path, Path.GetFileName(files[0])));
                        }
                        PluginReference.GposeWindow.LoadFrames();
                    }
                }
                if (clientState != null) {
                    if (ImGui.Button(!clientState.IsGPosing ? "Enter Gpose" : "Exit Gpose", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                        if (!clientState.IsGPosing) {
                            PluginReference.FastMessageQueue.Enqueue("/gpose");
                            hook.SendAsyncKey(Keys.Scroll);
                            IsOpen = false;
                        } else {
                            PluginReference.FastMessageQueue.Enqueue("/gpose");
                            hook.SendAsyncKey(Keys.Scroll);
                        }

                    }
                }
            } catch {

            }
        }
        private static readonly List<string> ValidTextureExtensions = new List<string>(){
          ".png",
        };
        private async void DrawNPCDialogue() {
            if (ImGui.Button("Contribute Your Voice!", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                Process process = new Process();
                try {
                    // true is the default, but it is important not to set it to false
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "https://forms.gle/JrarUbRpnhNyEThAA";
                    process.Start();
                } catch (Exception e) {

                }
            }
            try {
                ImGui.TextWrapped("Crowdsourced NPC speech is currently a work in progress, and will likely run slowly until a majority of dialogue is created and cached.");
                ImGui.Checkbox("Turn Off Crowdsourced NPC Speech", ref _npcSpeechGenerationDisabled);
                ImGui.Checkbox("Ignore Retainer Speech", ref _ignoreRetainerSpeech);
                ImGui.Checkbox("Read Quest Objectives", ref _readQuestObjectives);
                ImGui.Checkbox("Read Location And Toast Notifications", ref _readLocationAndToastNotifications);
                ImGui.Checkbox("Auto Advance Text When NPC Speech Finishes (Numpad 0)", ref _npcAutoTextAdvance);
                ImGui.Checkbox("Replace A Realm Reborn Voice Acting", ref _replaceVoicedARRCutscenes);
                ImGui.Checkbox("Quality Assurance Mode (help fix lines)", ref _qualityAssuranceMode);
                ImGui.Text("NPC Playback Speed");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                ImGui.SliderFloat("##_npcPlaybackSpeed", ref _npcPlaybackSpeed, 1, 2);
                ImGui.Text("Previously Played Lines:");
                int count = 0;
                foreach (var item in PluginReference.AddonTalkHandler.NpcVoiceHistoryItems) {
                    ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionMax().X - (ImGui.GetWindowContentRegionMax().X * (PluginReference.Config.QualityAssuranceMode ? 0.1f : 0.2f)));
                    ImGui.LabelText("##label" + item.Text, item.Character + ": " + item.OriginalValue);
                    ImGui.SameLine();
                    if (ImGui.Button($"Replay Line ({1 + (count++)})")) {
                        var stream = (await PluginReference.NpcVoiceManager.GetCharacterAudio(item.Text, item.OriginalValue, item.Character,
                             item.Gender, item.BackupVoice, false, NPCVoiceManager.VoiceModel.Speed, item.ExtraJson, PluginReference.Config.QualityAssuranceMode)).Key;
                        if (stream.Length > 0) {
                            var player = PluginReference.AddonTalkHandler.GetWavePlayer(item.Character, stream, null);
                            PluginReference.MediaManager.PlayAudioStream(new DummyObject(), player, SoundType.NPC, false, false, 1);
                        }
                        if (PluginReference.Config.QualityAssuranceMode) {
                            PluginReference.AddonTalkHandler.NpcVoiceHistoryItems.Remove(item);
                        }
                        break;
                    }
                    ImGui.SameLine();
                    if (PluginReference.Config.QualityAssuranceMode) {
                        if (ImGui.Button($"Report Line ({1 + (count++)})")) {
                            var stream = (await PluginReference.NpcVoiceManager.GetCharacterAudio(item.Text, item.OriginalValue, item.Character,
                                 item.Gender, item.BackupVoice, false, NPCVoiceManager.VoiceModel.Speed, item.ExtraJson, PluginReference.Config.QualityAssuranceMode)).Key;
                            if (stream.Length > 0) {
                                var player = PluginReference.AddonTalkHandler.GetWavePlayer(item.Character, stream, null);
                                PluginReference.MediaManager.PlayAudioStream(new DummyObject(), player, SoundType.NPC, false, false, 1);
                            }
                            if (PluginReference.Config.QualityAssuranceMode) {
                                PluginReference.AddonTalkHandler.NpcVoiceHistoryItems.Remove(item);
                            }
                            break;
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }

        private void DrawPlayerSync() {
            ImGui.Text("Server IP");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##serverIP", ref serverIP, 2000);

            ImGui.Checkbox("##useServer", ref _useServer);
            ImGui.SameLine();
            ImGui.Text("Allow Sending/Receiving Server Data");
            ImGui.TextWrapped("(Any players with ARK installed and connected to the same server will hear your custom voice and vice versa if added to eachothers whitelists)");

            string[] whitelist = configuration.Whitelist.ToArray();
            if (whitelist.Length == 0) {
                whitelist = new string[] { "None" };
            }
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Player Whitelist");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.ListBox("##whitelist", ref _currentWhitelistItem, whitelist, whitelist.Length, 10);
            bool playerTargetted = (clientState.LocalPlayer != null && clientState.LocalPlayer.TargetObject != null);
            bool playerCloseEnough = playerTargetted && Vector3.Distance(
            clientState.LocalPlayer.Position, clientState.LocalPlayer.TargetObject.Position) < 1;
            string targetedPlayerText = "Add Targetted Player";
            if (!playerTargetted) {
                targetedPlayerText += " (No Target)";
                ImGui.BeginDisabled();
            } else if (playerTargetted && !playerCloseEnough) {
                targetedPlayerText += " (Too Far)";
                ImGui.BeginDisabled();
            }
            if (ImGui.Button(targetedPlayerText)) {
                if (clientState.LocalPlayer.TargetObject.ObjectKind == ObjectKind.Player) {
                    string senderName = Plugin.CleanSenderName(clientState.LocalPlayer.TargetObject.Name.TextValue);
                    if (!configuration.Whitelist.Contains(senderName)) {
                        configuration.Whitelist.Add(senderName);
                    }
                    Save();
                }
            }
            if (!playerTargetted || !playerCloseEnough) {
                ImGui.EndDisabled();
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove Selected Player")) {
                configuration.Whitelist.Remove(configuration.Whitelist[_currentWhitelistItem]);
                Save();
            }
            ImGui.TextWrapped("Add users to your whitelist in order to be able to hear them (assuming they have ARK)");
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Checkbox("Sync Via Penumbra Refresh", ref _ignoreWhitelist);
            ImGui.TextWrapped("You will hear any user thats individually refreshed by another plugin (effectively relying on existing pairs from other plugins)");
            ImGui.Dummy(new Vector2(0, 10));
            if (ImGui.Button("Force Redownload Of New Sounds", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                PluginReference.CleanSounds();
            }
            ImGui.TextWrapped("If others have freshly changed their sound packs while still in your presence, you may need to refresh their sounds.");

        }

        private void SaveAndClose() {
            var originPos = ImGui.GetCursorPos();
            // Place save button in bottom left + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMax().X + 10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Save")) {
                Save();
            }
            ImGui.SetCursorPos(originPos);
            // Place close button in bottom right + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 20f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 10f);
            if (ImGui.Button("Close")) {
                SizeYChanged = false;
                changedSize = null;
                Size = initialSize;
                IsOpen = false;
            }
            ImGui.SetCursorPos(originPos);
        }

        private void Save() {
            Task.Run(delegate {
                if (InputValidation()) {
                    if (configuration != null) {
                        apiKeyValidated = false;
                        save = true;
                        if (_manager == null) {
                            managerNullMessage = "Somehow, the manager went missing. Contact a developer!";
                            managerNull = true;
                            PluginReference.InitialzeManager();
                        }
                        if (_manager != null) {
                            managerNullMessage = string.Empty;
                            managerNull = false;
                            Task.Run(() => _manager.ApiValidation(apiKey));
                            SaveSettings();
                        }
                    }
                    if (string.IsNullOrWhiteSpace(apiKey) && _aiVoiceActive) {
                        isApiKeyValid = false;
                        apiKeyErrorMessage = "API Key is empty! Please check the input.";
                    }

                    SizeYChanged = false;
                    changedSize = null;
                    Size = initialSize;
                }
            });
        }

        private void DrawErrors() {
            if (!isServerIPValid) {
                ErrorMessage(serverIPErrorMessage);
            }
            if ((!isApiKeyValid || string.IsNullOrEmpty(apiKey)) && _aiVoiceActive) {
                ErrorMessage(apiKeyErrorMessage);
            }
            if (managerNull) {
                ErrorMessage(managerNullMessage);
            }
            if (!fileMoveSuccess && !string.IsNullOrEmpty(fileMoveMessage)) {
                if (!string.IsNullOrEmpty(attemptedMoveLocation)) {
                    Task.Run(() => Directory.Delete(attemptedMoveLocation, true));
                    attemptedMoveLocation = null;
                }
                ErrorMessage(fileMoveMessage);
            }

            if (!string.IsNullOrEmpty(apiKey) && runOnLaunch) {
                Task.Run(() => _manager.ApiValidation(apiKey.Trim()));
                InputValidation();
                runOnLaunch = false;
            } else if (string.IsNullOrEmpty(apiKey)) {
                if (runOnLaunch) {
                    InputValidation();
                }
                apiKeyErrorMessage = "API Key is empty! Please check the input.";
                runOnLaunch = false;
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
            if (isServerIPValid) {
                return true;
            }
            return false;
        }

        private void _manager_OnApiValidationComplete(object sender, ValidationResult e) {
            if (e.ValidationSuceeded && !apiKeyValidated) {
                apiKeyErrorMessage = string.Empty;
                isApiKeyValid = true;
            } else if (!e.ValidationSuceeded && !apiKeyValidated) {
                apiKeyErrorMessage = "Invalid API Key! Please check the input.";
                isApiKeyValid = false;
            }
            apiKeyValidated = true;
            RefreshVoices();
            // If the api key was validated, is valid, and the request was sent via the Save or Close button, the settings are saved.
        }
        public void SaveSettings() {
            configuration.ConnectionIP = serverIP;
            if (clientState.LocalPlayer != null) {
                if (configuration.Characters == null) {
                    configuration.Characters = new System.Collections.Generic.Dictionary<string, string>();
                }
                configuration.Characters[clientState.LocalPlayer.Name.TextValue] = characterVoice != null ? characterVoice : "";
            }
            if (_aiVoiceActive && !string.IsNullOrEmpty(apiKey)) {
                configuration.ApiKey = apiKey;
                _manager.SetAPI(apiKey);
            }
            if (configuration.CharacterVoicePacks == null) {
                configuration.CharacterVoicePacks = new System.Collections.Generic.Dictionary<string, string>();
            }
            configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue] = characterVoicePack != null ? characterVoicePack : "";
            configuration.PlayerCharacterVolume = _playerCharacterVolume;
            configuration.OtherCharacterVolume = _otherCharacterVolume;
            configuration.UnfocusedCharacterVolume = _unfocusedCharacterVolume;
            configuration.LoopingSFXVolume = _loopingSFXVolume;
            configuration.LivestreamVolume = _livestreamVolume;
            configuration.NpcVolume = _npcVolume;
            configuration.AiVoiceActive = _aiVoiceActive;
            configuration.VoicePackIsActive = _characterVoicePackActive;
            configuration.UseAggressiveSplicing = _aggressiveCaching;
            configuration.CacheFolder = cacheFolder;
            configuration.UsePlayerSync = _useServer;
            configuration.TuneIntoTwitchStreams = _tuneIntoTwitchStreams;
            configuration.DefaultTwitchOpen = _twitchDefaultPlayback.SelectedIndex;
            configuration.IgnoreWhitelist = _ignoreWhitelist;
            configuration.StreamPath = _streamPath;
            configuration.PerformEmotesBasedOnWrittenText = _performEmotesBasedOnWrittenText;
            configuration.MoveSCDBasedModsToPerformanceSlider = _moveSCDBasedModsToPerformanceSlider;
            configuration.NpcSpeechGenerationDisabled = _npcSpeechGenerationDisabled;
            configuration.AutoTextAdvance = _npcAutoTextAdvance;
            configuration.ReplaceVoicedARRCutscenes = _replaceVoicedARRCutscenes;
            configuration.AudioOutputType = _audioOutputType.SelectedIndex;
            configuration.QualityAssuranceMode = _qualityAssuranceMode;
            configuration.TwitchStreamTriggersIfShouter = _twitchStreamTriggersIfShouter;
            configuration.NPCSpeechSpeed = _npcPlaybackSpeed;
            configuration.DontVoiceRetainers = _ignoreRetainerSpeech;
            configuration.TuneIntoTwitchStreamPrompt = _tuneIntoTwitchStreamPrompt;
            configuration.DebugMode = _debugMode;
            configuration.ReadQuestObjectives = _readQuestObjectives;
            configuration.ReadLocationsAndToastNotifications = _readLocationAndToastNotifications;
            configuration.LowPerformanceMode = _lowPerformanceMode;
            configuration.SpatialAudioAccuracy = (int)_spatialAudioAccuracy;
            configuration.CustomNpcCharacters = PluginReference.NpcPersonalityWindow.CustomNpcCharacters;
            configuration.PlayerVoiceEngine = _voiceEngineComboBox.SelectedIndex;
            if (voicePackComboBox != null && _voicePackList != null) {
                if (voicePackComboBox.SelectedIndex < _voicePackList.Length) {
                    characterVoicePack = _voicePackList[voicePackComboBox.SelectedIndex];
                }
            }
            if (voiceComboBox != null && _voiceList != null) {
                if (voiceComboBox.SelectedIndex < _voiceList.Length) {
                    characterVoice = _voiceList[voiceComboBox.SelectedIndex];
                }
            }
            configuration.Save();
            PluginInterface.SavePluginConfig(configuration);
            save = false;
            RefreshVoices();
        }
        private Vector2? GetSizeChange(float requiredY, float availableY, int Lines, Vector2? initial) {
            // Height
            if (availableY - requiredY * Lines < 1) {
                Vector2? newHeight = new Vector2(initial.Value.X, initial.Value.Y + requiredY * Lines);
                return newHeight;
            }
            return initial;
        }

        private void ErrorMessage(string message) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10f);
            ImGui.BeginChild("ErrorRegion", new Vector2(
            ImGui.GetContentRegionAvail().X,
            ImGui.GetContentRegionAvail().Y - 40f), false);
            var requiredY = ImGui.CalcTextSize(message).Y + 1f;
            var availableY = ImGui.GetContentRegionAvail().Y;
            var initialH = ImGui.GetCursorPos().Y;
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), message);
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
            ImGui.EndChild();
        }

        public async void RefreshVoices() {
            _ = Task.Run(async delegate () {
                _refreshing = true;
                try {
                    if (clientState.LocalPlayer != null) {
                        List<string> voicePacks = new List<string>();
                        string path = cacheFolder + @"\VoicePack\";
                        if (Directory.Exists(path)) {
                            foreach (string voice in Directory.EnumerateDirectories(path)) {
                                if (!voice.EndsWith("Others")) {
                                    voicePacks.Add(Path.GetFileNameWithoutExtension(voice + ".blah"));
                                }
                            }
                            _voicePackList = voicePacks.ToArray();
                            if (voicePacks.Count > voicePackComboBox.Contents.Length) {
                                voicePackComboBox.Contents = _voicePackList;
                            }
                        }
                        if (configuration.CharacterVoicePacks == null) {
                            configuration.CharacterVoicePacks = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        if (configuration.CharacterVoicePacks.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                            if (voicePackComboBox != null) {
                                if (_voicePackList != null) {
                                    if (voiceComboBox != null) {
                                        voicePackComboBox.Contents = _voicePackList;
                                        if (voicePackComboBox.Contents.Length > 0) {
                                            for (int i = 0; i < voicePackComboBox.Contents.Length; i++) {
                                                if (voicePackComboBox.Contents[i].Contains(configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue])) {
                                                    voicePackComboBox.SelectedIndex = i;
                                                    break;
                                                }
                                            }
                                            if (string.IsNullOrWhiteSpace(configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue])) {
                                                if (voicePackComboBox.SelectedIndex < voicePackComboBox.Contents.Length) {
                                                    configuration.CharacterVoicePacks[clientState.LocalPlayer.Name.TextValue] = voicePackComboBox.Contents[voicePackComboBox.SelectedIndex];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (_manager != null) {
                            var newVoiceList = _voiceEngineComboBox.SelectedIndex == 0 ? (await _manager.GetVoiceListElevenlabs()) : await _manager.GetVoiceListXTTS();
                            if (newVoiceList != null && newVoiceList.Length > 0) {
                                _voiceList = newVoiceList;
                                voiceComboBox.Contents = newVoiceList;
                            }
                            if (_voiceEngineComboBox.SelectedIndex == 0) {
                                _manager.SetVoiceElevenlabs(Configuration.Characters[clientState.LocalPlayer.Name.TextValue]);
                                _manager.RefreshElevenlabsSubscriptionInfo();
                            } else {
                                _manager.SetVoiceXTTS(Configuration.Characters[clientState.LocalPlayer.Name.TextValue]);
                            }
                            if (_voiceList != null && _voiceList.Length > 0) {
                                voiceComboBox.Contents = _voiceList;
                            }
                        }
                        if (configuration.Characters == null) {
                            configuration.Characters = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        if (configuration.Characters.ContainsKey(clientState.LocalPlayer.Name.TextValue)) {
                            if (voiceComboBox != null) {
                                if (_voiceList != null && _voiceList.Length > 0) {
                                    voiceComboBox.Contents = _voiceList;
                                    if (voiceComboBox.Contents.Length > 0) {
                                        for (int i = 0; i < voiceComboBox.Contents.Length; i++) {
                                            string value = configuration.Characters[clientState.LocalPlayer.Name.TextValue];
                                            if (voiceComboBox.Contents[i].Contains(value) && !string.IsNullOrEmpty(value)) {
                                                voiceComboBox.SelectedIndex = i;
                                                break;
                                            }
                                        }
                                        if (string.IsNullOrWhiteSpace(configuration.Characters[clientState.LocalPlayer.Name.TextValue])) {
                                            if (voiceComboBox.SelectedIndex < voiceComboBox.Contents.Length) {
                                                configuration.Characters[clientState.LocalPlayer.Name.TextValue] = voiceComboBox.Contents[voiceComboBox.SelectedIndex];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch (Exception ex) {

                }
                if (PluginInterface != null) {
                    try {
                        PluginReference.RefreshData(false, true);
                    } catch (Exception ex) { }
                }
                _refreshing = false;
            });
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
                    ImGui.SetNextItemWidth(_width);
                    if (_label != null && _contents != null) {
                        if (_contents.Length > 0) {
                            ImGui.Combo("##" + _label, ref index, _contents, _contents.Length);
                        }
                    }
                }
                if (index != _lastIndex) {
                    if (OnSelectedIndexChanged != null) {
                        OnSelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                _lastIndex = index;
            }
        }

        private void DrawGeneral() {
            ImGui.Text("Cache Location:");
            ImGui.TextWrapped(cacheFolder);
            ImGui.Text("Pick A New Cache (Optional, only pick empty folders):");
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.FolderClosed)) {
                fileDialogManager.Reset();
                ImGui.OpenPopup("OutputPathDialog");
            }
            if (ImGui.BeginPopup("OutputPathDialog")) {
                fileDialogManager.SaveFolderDialog("Select cache location", "RPVoiceCache", (isOk, folder) => {
                    if (isOk) {
                        if (folder != null && !string.Equals(folder, cacheFolder)) {
                            if (!folder.Contains(cacheFolder)) {
                                attemptedMoveLocation = folder;
                                Task.Run(() => FileMove(ref cacheFolder, folder));
                            } else {
                                OnWindowOperationFailed?.Invoke(this, new MessageEventArgs() {
                                    Message = "You cannot put a cache folder inside the old cache folder."
                                });
                            }
                        }
                    }
                }, null, true);
                ImGui.EndPopup();
            }
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.LabelText("##GCVSLabel", "Generative Character Voice ");
            ImGui.Checkbox("##characterVoiceActive", ref _aiVoiceActive);
            ImGui.SameLine();
            ImGui.Text("Generative Voice Enabled");
            if (clientState.LocalPlayer != null && _aiVoiceActive) {
                ImGui.Text("Generative Voice Engine");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                _voiceEngineComboBox.Width = (int)ImGui.GetContentRegionMax().X;
                _voiceEngineComboBox.Draw();
                if (_voiceEngineComboBox.SelectedIndex == 0) {
                    ImGui.Text("Elevenlabs API Key");
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                    ImGui.InputText("##apiKey", ref apiKey, 2000, ImGuiInputTextFlags.Password);
                    if (string.IsNullOrEmpty(apiKey)) {
                        if (ImGui.Button("Elevenlabs API Key Sign Up", new Vector2(ImGui.GetWindowSize().X - 10, 25))) {
                            Process process = new Process();
                            try {
                                process.StartInfo.UseShellExecute = true;
                                process.StartInfo.FileName = "https://www.elevenlabs.io/?from=partnerthompson2324";
                                process.Start();
                            } catch (Exception e) {

                            }
                        }
                    }
                } else {
                    if (!_manager.XttsReady) {
                        ImGui.Text("XTTS is still getting ready. If this is a first time setup, please wait for initial setup to complete. (May take roughly 30 minutes the first time)");
                    }
                }
                if (voiceComboBox != null && _voiceList != null) {
                    if (_voiceList.Length > 0) {
                        ImGui.Text("Generative Voice");
                        voiceComboBox.Width = (int)ImGui.GetContentRegionMax().X;
                        voiceComboBox.Draw();
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                    } else {

                    }
                } else if (voiceComboBox.Contents.Length == 1 &&
                      (voiceComboBox.Contents[0].Contains("None", StringComparison.OrdinalIgnoreCase) ||
                      voiceComboBox.Contents[0].Contains("", StringComparison.OrdinalIgnoreCase))) {
                    RefreshVoices();
                }
                if (_voiceEngineComboBox.SelectedIndex == 1) {
                    if (Environment.GetEnvironmentVariable("Path").Contains("Python")) {
                        ImGui.TextWrapped($"To add more voices, simply place .wav files of what you want to sound like in the folder the buttons below manage.");
                        if (ImGui.Button("Add More Voices", new Vector2(ImGui.GetWindowSize().X / 2 - 5, 25))) {
                            Process process = new Process();
                            try {
                                Directory.CreateDirectory(Path.Combine(configuration.CacheFolder, "speakers"));
                                process.StartInfo.UseShellExecute = true;
                                process.StartInfo.FileName = Path.Combine(configuration.CacheFolder, "speakers");
                                process.Start();
                            } catch (Exception e) {

                            }
                        }
                        ImGui.SameLine();
                        if (_voiceEngineComboBox.SelectedIndex == 1) {
                            if (ImGui.Button("Refresh Voices", new Vector2(ImGui.GetWindowSize().X / 2 - 5, 25))) {
                                RefreshVoices();
                            }
                        }
                    } else {
                        ImGui.TextWrapped("Python is required to install locally generated player voice dependancies. Click the button below to download the installer. Make sure you enable path variable support.");
                        if (ImGui.Button("Download Python Dependancy", new Vector2(ImGui.GetWindowSize().X - 10, 25))) {
                            Process process = new Process();
                            try {
                                process.StartInfo.UseShellExecute = true;
                                process.StartInfo.FileName = @"https://www.python.org/ftp/python/3.10.0/python-3.10.0-amd64.exe";
                                process.Start();
                            } catch (Exception e) {

                            }
                        }
                    }
                }
                if (_voiceEngineComboBox.SelectedIndex == 0) {
                    if (_manager != null && _manager.Info != null && isApiKeyValid) {
                        ImGui.TextWrapped($"You have used {_manager.Info.CharacterCount}/{_manager.Info.CharacterLimit} characters.");
                        ImGui.TextWrapped($"Once this caps you will either need to upgrade subscription tiers or wait until the next month");
                    }
                } else {
                    ImGui.TextWrapped($"XTTS is free to use and runs on your own machine. Generation speed is hardware dependant. Requires Python 3.10 or below.");
                }
            } else if (voiceComboBox.Contents.Length == 1 && voiceComboBox != null
              && !isApiKeyValid && _aiVoiceActive || clientState.LocalPlayer == null && !isApiKeyValid && _aiVoiceActive) {
                voiceComboBox.Contents[0] = "API not initialized";
                if (_voiceList.Length > 0) {
                    ImGui.Text("Voice");
                    voiceComboBox.Draw();
                }
            } else if (!clientState.IsLoggedIn && isApiKeyValid && _aiVoiceActive) {
                voiceComboBox.Contents[0] = "Not logged in";
                if (_voiceList.Length > 0) {
                    ImGui.Text("Voice");
                    voiceComboBox.Draw();
                }
            }
            if (_aiVoiceActive) {
                ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
                ImGui.Checkbox("##aggressiveCachingActive", ref _aggressiveCaching);
                ImGui.SameLine();
                ImGui.Text("Use Aggressive Caching");
            }
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.LabelText("##EBSLabel", "Emote and Battle Sounds ");
            ImGui.Checkbox("##characterVoicePackActive", ref _characterVoicePackActive);
            ImGui.SameLine();
            ImGui.Text("Voice Pack Enabled");
            if (_characterVoicePackActive) {
                if (_voicePackList.Length > 0 && clientState.IsLoggedIn) {
                    voicePackComboBox.Draw();
                    ImGui.SameLine();
                    if (ImGui.Button("Refresh Changes")) {
                        PluginReference.RefreshData();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Open Sound Directory")) {
                        if (voicePackComboBox != null && _voicePackList != null) {
                            characterVoicePack = _voicePackList[voicePackComboBox.SelectedIndex];
                        }
                        ProcessStartInfo ProcessInfo;
                        Process Process;
                        string directory = configuration.CacheFolder + @"\VoicePack\" + characterVoicePack;
                        try {
                            Directory.CreateDirectory(directory);
                        } catch {
                        }
                        ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + directory + @"""");
                        ProcessInfo.UseShellExecute = true;
                        Process = Process.Start(ProcessInfo);
                    }
                }
                ImGui.SetNextItemWidth(270);
                ImGui.InputText("##newVoicePack", ref _newVoicePackName, 20);
                ImGui.SameLine();
                if (ImGui.Button("New Sound Pack")) {
                    string directory = configuration.CacheFolder + @"\VoicePack\" + _newVoicePackName;
                    Directory.CreateDirectory(directory);
                    RefreshVoices();
                    _newVoicePackName = "";
                }
                if (ImGui.Button("Import Sound Pack")) {
                    fileDialogManager.Reset();
                    ImGui.OpenPopup("ImportDialog");
                }

                ImGui.SameLine();
                if (ImGui.Button("Export Sound Pack")) {
                    fileDialogManager.Reset();
                    ImGui.OpenPopup("ExportDialog");
                }

                if (ImGui.BeginPopup("ImportDialog")) {
                    fileDialogManager.OpenFileDialog("Select Sound Pack", "{.rpvsp}", (isOk, file) => {
                        string directory = configuration.CacheFolder + @"\VoicePack\" + Path.GetFileNameWithoutExtension(file);
                        if (isOk) {
                            ZipFile.ExtractToDirectory(file, directory);
                            RefreshVoices();
                        }
                    });
                    ImGui.EndPopup();
                }

                if (ImGui.BeginPopup("ExportDialog")) {
                    fileDialogManager.SaveFileDialog("Select Sound Pack", "{.rpvsp}", "SoundPack.rpvsp", ".rpvsp", (isOk, file) => {
                        string directory = configuration.CacheFolder + @"\VoicePack\" + characterVoicePack;
                        if (isOk) {
                            ZipFile.CreateFromDirectory(directory, file);
                        }
                    });
                    ImGui.EndPopup();
                }
                ImGui.TextWrapped("(Simply name .mp3 files after the emote or battle action they should be tied to.)");
            }
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.TextWrapped("Artemis Roleplaying Kit relies on donations to continue development. Please consider tossing a dollar if you enjoy using the plugin.");
            if (ImGui.Button("Donate", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                Process process = new Process();
                try {
                    // true is the default, but it is important not to set it to false
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "https://ko-fi.com/sebastina";
                    process.Start();
                } catch (Exception e) {

                }
            }
        }

        private void DrawVolume() {
            ImGui.Text("Current Player Voice Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##playerSlider", ref _playerCharacterVolume, 0, 2);
            ImGui.Text("Other Player Voice Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##otherSlider", ref _otherCharacterVolume, 0, 2);
            ImGui.Text("Unfocused Player Voice Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##unfocusedVolume", ref _unfocusedCharacterVolume, 0, 2);
            ImGui.Text("Looping SFX Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##loopingSFX", ref _loopingSFXVolume, 0.000001f, 2);
            ImGui.Text("Livestream Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##livestreamVolume", ref _livestreamVolume, 0.000001f, 3);
            ImGui.Text("NPC Volume");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##npcVolumeSlider", ref _npcVolume, 0.000001f, 1f);
            ImGui.Text("Audio Output System (Change if you notice playback issues)");
            _audioOutputType.Width = (int)ImGui.GetContentRegionMax().X;
            _audioOutputType.Draw();
            ImGui.Checkbox("##moveSCDBasedModsToPerformanceSlider", ref _moveSCDBasedModsToPerformanceSlider);
            ImGui.SameLine();
            ImGui.Text("Seperate Dance Mods From BGM Track (Experimental)");
            ImGui.TextWrapped("Mods that use .scd files will be moved from the BGM channel and use the Performance slider. They'll also be synced via ARK if sync is enabled.");

            ImGui.Checkbox("##lowPerformanceMode", ref _lowPerformanceMode);
            ImGui.SameLine();
            ImGui.Text("Disable spatial audio for combat sounds.");
            ImGui.TextWrapped("Potentially increases performance at the expense of spatial audio for combat sounds.");
            ImGui.Dummy(new Vector2(10));
            ImGui.Text("Spatial Audio Accuracy");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.SliderFloat("##spatialAudioAccuracy", ref _spatialAudioAccuracy, 100, 700);
            ImGui.TextWrapped("Reduce the accuracy of spatial audio in exchange for possibly better performance. Higher number means less spatial accuracy, so raise this slider until the moment performance improves.");

            if (ImGui.Button("Volume Fix (fixes rare instances of muted sound)", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                PluginReference.MediaManager.VolumeFix();
            }
        }


        private void FileMove(ref string oldFolder, string newFolder) {
            bool canContinue = true;
            // Check if the destination folder exists, create it if necessary
            if (!Directory.Exists(newFolder)) {
                try {
                    Directory.CreateDirectory(newFolder);

                    if (Directory.EnumerateFiles(newFolder).Count<string>() > 0) {
                        fileMoveSuccess = false;
                        canContinue = false;
                        fileMoveMessage = "Folder is not empty.";
                        OnMoveFailed?.Invoke(this, EventArgs.Empty);
                    }
                } catch {
                    fileMoveSuccess = false;
                    fileMoveMessage = "Error, no write access.";
                    canContinue = false;
                    OnMoveFailed?.Invoke(this, EventArgs.Empty);
                }
            }
            if (canContinue) {
                // Calculate the space needed for the move
                long fileSize = Directory.EnumerateFiles(oldFolder, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);

                // Check the amount of available space
                DriveInfo destinationDrive = new DriveInfo(Path.GetPathRoot(newFolder));
                long availableSpace = destinationDrive.AvailableFreeSpace;

                if (fileSize >= availableSpace) {
                    fileMoveSuccess = false;
                    fileMoveMessage = "Not enough space available.";
                    Directory.Delete(newFolder, true);
                } else {
                    if (Directory.Exists(oldFolder)) {
                        var fileCount = Directory.EnumerateFiles(oldFolder, "*", SearchOption.AllDirectories).Count();

                        // Check if oldFolder is empty, if so abort
                        if (fileCount == 0)
                            return;

                        int currentFile = 0;
                        int moveFailed = 0;

                        foreach (string filePath in Directory.EnumerateFiles(oldFolder, "*", SearchOption.AllDirectories)) {
                            // Get the relative path of the file within the oldFolder folder
                            string relativePath = Path.GetRelativePath(oldFolder, filePath);

                            // Create the newFolder directory path
                            string destinationDirectoryPath = Path.Combine(newFolder, Path.GetDirectoryName(relativePath));

                            // Create the newFolder directory if necessary
                            Directory.CreateDirectory(destinationDirectoryPath);

                            // Create the new file to the newFolder directory while preserving the structure
                            string destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(filePath));

                            Task.Run(async () => {
                                bool fileMoved = await MoveFileAsync(filePath, destinationFilePath);
                                lock (currentFileLock) {
                                    currentFile++;
                                    if (!fileMoved)
                                        moveFailed++;
                                };
                            });
                        }

                        // Wait for all threads
                        while (currentFile < fileCount && moveFailed == 0) {
                            if (moveFailed > 0) {
                                break;
                            }
                            Thread.Sleep(100);
                        }
                        if (moveFailed == 0 && currentFile == fileCount) {
                            fileMoveSuccess = true;
                            fileMoveMessage = string.Empty;
                            if (configuration != null && configuration.CharacterVoices != null) {
                                foreach (var voice in configuration.CharacterVoices.VoiceCatalogue) {
                                    string voiceName = voice.Key;
                                    var innerDictionary = voice.Value;
                                    foreach (var message in innerDictionary) {
                                        string text = message.Key;
                                        string path = message.Value;
                                        if (path.StartsWith(oldFolder)) {
                                            string relativePath = path.Substring(oldFolder.Length);
                                            string newPath = newFolder + relativePath;
                                            innerDictionary[text] = newPath;
                                        }

                                    }
                                }
                                Directory.Delete(oldFolder, true);
                            }
                        }
                        oldFolder = newFolder;
                        configuration.CacheFolder = oldFolder;
                        // Right now this solves the issue of the manager having an incorrect cache location until a manual save happens
                        // but subinfo needs a full window redraw to work
                        Configuration = configuration;
                        PluginReference.InitialzeManager();
                    } else {
                        fileMoveSuccess = false;
                        if (string.IsNullOrEmpty(fileMoveMessage)) {
                            fileMoveMessage = "Move failed!";
                        }
                        //Move failed, figure it out nerd
                    }
                }
            }
        }

        private async Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath) {
            bool status = true;
            lock (fileLock) {
                try {
                    using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open,
                        FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                    using (FileStream destinationStream = new FileStream(destinationFilePath, FileMode.Create,
                        FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
                        sourceStream.CopyTo(destinationStream);
                    }
                } catch (Exception e) {
                    fileMoveSuccess = false;
                    if (e.Message.Contains("it is being used by another process")) {
                        fileMoveMessage = $"The file {Path.GetFileName(sourceFilePath)} is in use.";
                    }
                    status = false;
                }
            }
            return status;
        }

        public class MessageEventArgs : EventArgs {
            string message;

            public string Message { get => message; set => message = value; }
        }
    }
}
