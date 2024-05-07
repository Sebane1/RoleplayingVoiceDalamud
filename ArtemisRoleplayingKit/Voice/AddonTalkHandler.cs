using Anamnesis;
using Anamnesis.Actor;
using Anamnesis.Core.Memory;
using Anamnesis.GameData.Excel;
using Anamnesis.Memory;
using Anamnesis.Services;
using Concentus.Oggfile;
using Concentus.Structs;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFBardMusicPlayer.FFXIV;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using RoleplayingVoice;
using RoleplayingVoiceDalamud.Datamining;
using RoleplayingVoiceDalamud.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using Character = Dalamud.Game.ClientState.Objects.Types.Character;
using SoundType = RoleplayingMediaCore.SoundType;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkHandler : IDisposable {
        Stopwatch pollingTimer = new Stopwatch();
        private AddonTalkManager addonTalkManager;
        private IFramework framework;
        private IObjectTable _objectTable;
        private IClientState _clientState;
        private object subscription;
        private string _lastText;
        private string _currentText;
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
        private IChatGui _chatGui;
        private AddonTalkState _state;
        private Hook<NPCSpeechBubble> _openChatBubbleHook;
        private bool alreadyConfiguredBubbles;
        private ISigScanner _scanner;
        private bool disposed;
        Stopwatch bubbleCooldown = new Stopwatch();
        private string _chatId;
        private RedoLineWIndow _redoLineWindow;
        private IToastGui _toast;
        private Dictionary<string, string> _knownNpcs;
        private MemoryService _memoryService;
        private SettingsService _settingService;
        private AnimationService _animationService;
        private GameDataService _gameDataService;
        private ActorService _actorService;
        private GposeService _gposeService;
        private AddressService _addressService;
        private PoseService _poseService;
        private TargetService _targetService;
        private List<GameObject> _threadSafeObjectTable;
        ConcurrentDictionary<string, string> _lastBattleNPCLines = new ConcurrentDictionary<string, string>();
        private int _blockAudioGenerationCount;
        private string _lastSoundPath;
        bool _blockNpcChat = false;
        private List<NPCVoiceHistoryItem> _npcVoiceHistoryItems = new List<NPCVoiceHistoryItem>();

        public List<ActionTimeline> LipSyncTypes { get; private set; }
        private readonly List<NPCBubbleInformation> _speechBubbleInfo = new();
        private readonly Queue<NPCBubbleInformation> _speechBubbleInfoQueue = new();
        private readonly List<NPCBubbleInformation> _gameChatInfo = new();
        public ConditionalWeakTable<ActorMemory, UserAnimationOverride> UserAnimationOverrides { get; private set; } = new();
        public bool TextIsPresent { get => _textIsPresent; set => _textIsPresent = value; }
        public List<NPCVoiceHistoryItem> NpcVoiceHistoryItems { get => _npcVoiceHistoryItems; set => _npcVoiceHistoryItems = value; }
        List<string> previouslyAddedLines = new List<string>();
        private bool _gotPlayerDefaultState;
        private ushort _defaultBaseOverride;
        private ushort _defaultCharacterModeInput;
        private byte _defaultCharacterModeRaw;

        public AddonTalkHandler(AddonTalkManager addonTalkManager, IFramework framework, IObjectTable objects,
            IClientState clientState, Plugin plugin, IChatGui chatGui, ISigScanner sigScanner, RedoLineWIndow redoLineWindow, IToastGui toastGui) {
            this.addonTalkManager = addonTalkManager;
            this.framework = framework;
            this._objectTable = objects;
            _clientState = clientState;
            framework.Update += Framework_Update;
            _plugin = plugin;
            _hook = new FFXIVHook();
            _hook.Hook(Process.GetCurrentProcess());
            _chatGui = chatGui;
            _chatGui.ChatMessage += _chatGui_ChatMessage;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _scanner = sigScanner;
            redoLineWindow.RedoLineClicked += RedoLineWIndow_RedoLineClicked;
            _redoLineWindow = redoLineWindow;
            _toast = toastGui;
            _toast.Toast += _toast_Toast;
            _toast.QuestToast += _toast_QuestToast;
            bubbleCooldown.Start();
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("nameless.json"));
            var noSound = assembly.GetManifestResourceStream(resourceName);
            using var memoryStream = new MemoryStream();
            noSound.CopyTo(memoryStream);
            memoryStream.Position = 0;
            using (StreamReader reader = new StreamReader(memoryStream)) {
                _knownNpcs = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
            _memoryService = new MemoryService();
            _settingService = new SettingsService();
            _gameDataService = new GameDataService();
            _animationService = new AnimationService();
            _actorService = new ActorService();
            _gposeService = new GposeService();
            _addressService = new AddressService();
            _poseService = new PoseService();
            _targetService = new TargetService();

            try {
                _memoryService.Initialize();
                _memoryService.OpenProcess(Process.GetCurrentProcess());
                _settingService.Initialize();
                _gameDataService.Initialize();
                _actorService.Initialize();
                _addressService.Initialize();
                _poseService.Initialize();
                _targetService.Initialize();
                _gposeService.Initialize();

                LipSyncTypes = GenerateLipList().ToList();
                _animationService.Initialize();
                _animationService.Start();
                _memoryService.Start();
                _addressService.Start();
                _poseService.Start();
                _targetService.Start();
                _gposeService.Start();
            } catch (Exception e) {
                _plugin.PluginLog.Warning(e, e.Message);
            }
            pollingTimer.Start();
        }

        private void _toast_QuestToast(ref SeString message, ref Dalamud.Game.Gui.Toast.QuestToastOptions options, ref bool isHandled) {
            if (CheckForBannedKeywords(message)) {
                NPCText("Narrator", message.TextValue.Replace(@"0/", "0 out of ")
                    .Replace(@"1/", "1 out of ")
                    .Replace(@"2/", "2 out of ")
                    .Replace(@"3/", "3 out of ")
                    .Replace(@"4/", "4 out of ")
                    .Replace(@"5/", "5 out of ")
                    .Replace(@"6/", "6 out of ")
                    .Replace(@"7/", "7 out of ")
                    .Replace(@"8/", "8 out of ")
                    .Replace(@"9/", "9 out of ")
                    .Replace(@"10/", "10 out of ") + (options.DisplayCheckmark ? " has been completed." : ""), "Hyn", true, !_plugin.Config.ReadQuestObjectives);
            }
        }

        private bool CheckForBannedKeywords(SeString message) {
            return !message.TextValue.Contains("you put up for sale") && !message.TextValue.Contains("You are now selling") && !message.TextValue.Contains("Challenge log entry")
                && !message.TextValue.Contains("You cancel") && !message.TextValue.Contains("You assign your retainer") && !message.TextValue.Contains("loot list") && !message.TextValue.Contains("venture")
                 && !message.TextValue.Contains("retainer") && !message.TextValue.Contains("joins the party") && !message.TextValue.Contains("left the party");
        }

        private void _toast_Toast(ref SeString message, ref Dalamud.Game.Gui.Toast.ToastOptions options, ref bool isHandled) {
            if (CheckForBannedKeywords(message)) {
                NPCText("Hydaelyn", message.TextValue, "Hyn", true, !_plugin.Config.ReadLocationsAndToastNotifications);
            }
        }

        private IEnumerable<ActionTimeline> GenerateLipList() {
            // Grab "no animation" and all "speak/" animations, which are the only ones valid in this slot
            IEnumerable<ActionTimeline> lips = GameDataService.ActionTimelines.Where(x => x.AnimationId == 0 || (x.Key?.StartsWith("speak/") ?? false));
            return lips;
        }
        private void RedoLineWIndow_RedoLineClicked(object sender, EventArgs e) {
            if (!_blockAudioGeneration) {
                NPCText(_state.Speaker, _state.Text.TrimStart('.'), true, true, true);
                _startedNewDialogue = true;
                _passthroughTimer.Reset();
                _redoLineWindow.IsOpen = false;
            }
        }
        private void _clientState_TerritoryChanged(ushort obj) {
            _speechBubbleInfo.Clear();
            _lastBattleNPCLines.Clear();
            _blockAudioGenerationCount = 0;
            _blockNpcChat = false;
        }

        private void _chatGui_ChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            string text = message.TextValue;
            string npcName = sender.TextValue;
            Task.Run(delegate () {
                Thread.Sleep(400);
                if (_clientState.IsLoggedIn &&
                    !_plugin.Config.NpcSpeechGenerationDisabled && Conditions.IsBoundByDuty) {
                    if (_state == null) {
                        if (!_blockNpcChat) {
                            switch (type) {
                                case XivChatType.NPCDialogueAnnouncements:
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("NPC Announcement detected " + npcName + ": "
                                            + text);
                                    }
                                    if (!_lastBattleNPCLines.ContainsKey(npcName)) {
                                        _lastBattleNPCLines[npcName] = "";
                                    }

                                    if (text != _lastBattleNPCLines[npcName] && !Conditions.IsWatchingCutscene) {
                                        _lastBattleNPCLines[npcName] = text;
                                        if (_blockAudioGenerationCount < 1) {
                                            NPCText(npcName, text.TrimStart('.'), true, !Conditions.IsBoundByDuty);
                                            if (_plugin.Config.DebugMode) {
                                                _plugin.Chat.Print("Sent audio from NPC chat.");
                                            }
                                        } else {
                                            _blockAudioGenerationCount--;
                                            if (_plugin.Config.DebugMode) {
                                                _plugin.Chat.Print("Blocked announcement " + npcName + ": "
                                                    + text);
                                            }
                                        }
                                    }
                                    _blockAudioGeneration = false;
                                    break;
                            }
                        }
                    }
                }
            }
            );
        }

        private void Filter_OnCutsceneAudioDetected(object sender, SoundFilter.InterceptedSound e) {
            if (_clientState != null) {
                if (_clientState.IsLoggedIn) {
                    if (!_currentDialoguePaths.Contains(e.SoundPath) || Conditions.IsBoundByDuty) {
                        if (e.SoundPath != _lastSoundPath) {
                            _blockAudioGeneration = e.isBlocking;
                            _currentDialoguePaths.Add(e.SoundPath);
                            _currentDialoguePathsCompleted.Add(false);
                            if (e.isBlocking) {
                                if (_blockAudioGenerationCount < 0) {
                                    _blockAudioGenerationCount = 0;
                                }
                                if (_blockAudioGenerationCount < 1) {
                                    _blockAudioGenerationCount++;
                                    _blockNpcChat = true;
                                }
                                _lastSoundPath = e.SoundPath;
                            }
                            if (_plugin.Config.DebugMode) {
                                _plugin.Chat.Print("Block Next Line Of Dialogue Is " + e.isBlocking);
                                _plugin.Chat.Print("Dialogue block created by " + e.SoundPath);
                                _plugin.Chat.Print("Blocked generation count is " + _blockAudioGenerationCount);
                            }
                        }
                    }
                }
            }
        }
        unsafe private IntPtr NPCBubbleTextDetour(IntPtr pThis, FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* pActor, IntPtr pString, bool param3) {
            try {
                if (_clientState.IsLoggedIn
                    && !Conditions.IsWatchingCutscene && !Conditions.IsWatchingCutscene78) {
                    if (pString != IntPtr.Zero &&
                    !Service.ClientState.IsPvPExcludingDen) {
                        //	Idk if the actor can ever be null, but if it can, assume that we should print the bubble just in case.  Otherwise, only don't print if the actor is a player.
                        if (pActor == null || (ObjectKind)pActor->GetObjectKind() != ObjectKind.Player) {
                            long currentTime_mSec = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                            SeString speakerName = SeString.Empty;
                            if (pActor != null && pActor->Name != null) {
                                speakerName = ((GameObject*)pActor)->Name;
                            }
                            var npcBubbleInformaton = new NPCBubbleInformation(MemoryHelper.ReadSeStringNullTerminated(pString), currentTime_mSec, speakerName);
                            var extantMatch = _speechBubbleInfo.Find((x) => { return x.IsSameMessageAs(npcBubbleInformaton); });
                            if (_plugin.Config.DebugMode) {
                                _plugin.Chat.Print("Bubble detected " + npcBubbleInformaton.SpeakerName.TextValue + ": "
                                    + npcBubbleInformaton.MessageText.TextValue);
                            }
                            if (extantMatch != null) {
                                extantMatch.TimeLastSeen_mSec = currentTime_mSec;
                            } else {
                                _speechBubbleInfo.Add(npcBubbleInformaton);
                                Task.Run(delegate {
                                    try {
                                        FFXIVClientStructs.FFXIV.Client.Game.Character.Character* character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pActor;
                                        if ((ObjectKind)character->GameObject.ObjectKind == ObjectKind.EventNpc || (ObjectKind)character->GameObject.ObjectKind == ObjectKind.BattleNpc) {
                                            string nameID = character->DrawData.Top.Value.ToString() + character->DrawData.Head.Value.ToString() +
                                               character->DrawData.Feet.Value.ToString() + character->DrawData.Ear.Value.ToString() + speakerName.TextValue + character->GameObject.DataID;
                                            Character characterObject = GetCharacterFromId(character->GameObject.ObjectID);
                                            string finalName = characterObject != null && !string.IsNullOrEmpty(characterObject.Name.TextValue) ? characterObject.Name.TextValue : nameID;
                                            if (!_lastBattleNPCLines.ContainsKey(characterObject.Name.TextValue)) {
                                                _lastBattleNPCLines[characterObject.Name.TextValue] = "";
                                            }
                                            if (npcBubbleInformaton.MessageText.TextValue != _lastBattleNPCLines[characterObject.Name.TextValue]) {
                                                _lastBattleNPCLines[characterObject.Name.TextValue] = npcBubbleInformaton.MessageText.TextValue;
                                                if (_blockAudioGenerationCount < 1) {
                                                    if (characterObject != null && characterObject.Customize[(int)CustomizeIndex.ModelType] != 0) {
                                                        NPCText(finalName, npcBubbleInformaton.MessageText.TextValue, true);
                                                        if (_plugin.Config.DebugMode) {
                                                            _plugin.Chat.Print("Sent audio from NPC bubble 1");
                                                        }
                                                    } else {
                                                        NPCText(finalName,
                                                            npcBubbleInformaton.MessageText.TextValue, character->DrawData.CustomizeData.Sex == 1,
                                                            character->DrawData.CustomizeData.Race, character->DrawData.CustomizeData.BodyType != 0 ? character->DrawData.CustomizeData.BodyType : character->CharacterData.ModelSkeletonId,
                                                            character->DrawData.CustomizeData.Tribe, character->DrawData.CustomizeData.EyeShape, character->GameObject.ObjectID, new MediaGameObject(pActor));
                                                        if (_plugin.Config.DebugMode) {
                                                            _plugin.Chat.Print("Sent audio from NPC bubble 2");
                                                        }
                                                    }
                                                    if (_plugin.Config.DebugMode) {
                                                        _plugin.Chat.Print("Sent audio from NPC bubble.");
                                                    }
                                                } else {
                                                    if (_plugin.Config.DebugMode) {
                                                        _blockAudioGenerationCount--;
                                                        _plugin.Chat.Print("Blocked bubble " + npcBubbleInformaton.SpeakerName.TextValue + ": "
                                                            + npcBubbleInformaton.MessageText.TextValue);
                                                    }
                                                }
                                            }
                                        }
                                        bubbleCooldown.Restart();
                                        _blockAudioGeneration = false;
                                    } catch {
                                        NPCText(speakerName.TextValue, npcBubbleInformaton.MessageText.TextValue, true);
                                    }
                                }
                                );
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Dalamud.Logging.PluginLog.Log(e, e.Message);
            }
            return _openChatBubbleHook.Original(pThis, pActor, pString, param3);
        }
        private Character GetCharacterFromId(uint id) {
            foreach (GameObject gameObject in _threadSafeObjectTable) {
                if (gameObject.ObjectId == id
                    && (gameObject.ObjectKind == ObjectKind.EventNpc || gameObject.ObjectKind == ObjectKind.BattleNpc)) {
                    return gameObject as Character;
                }
            }
            return null;
        }
        public string AliasDetector(string name) {
            switch (name) {
                case "Obnoxious Merchant":
                    return "Ungust";
                default:
                    return name;
            }
        }
        private void Framework_Update(IFramework framework) {
            if (!disposed)
                if (pollingTimer.ElapsedMilliseconds > 100) {
                    try {
                        if (_clientState != null) {
                            if (_clientState.IsLoggedIn) {
                                _plugin.Filter.Streaming = !Conditions.IsBoundByDuty && !Conditions.IsInCombat;
                                if (_plugin.Filter.IsCutsceneDetectionNull()) {
                                    if (!_alreadyAddedEvent) {
                                        _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
                                        _alreadyAddedEvent = true;
                                    }
                                }
                                if (_gotPlayerDefaultState) {
                                    GetAnimationDefaults();
                                }
                                if (!alreadyConfiguredBubbles) {
                                    //	Hook
                                    unsafe {
                                        IntPtr fpOpenChatBubble = _scanner.ScanText("E8 ?? ?? ?? ?? F6 86 ?? ?? ?? ?? ?? C7 46 ?? ?? ?? ?? ??");
                                        if (fpOpenChatBubble != IntPtr.Zero) {
                                            PluginLog.LogInformation($"OpenChatBubble function signature found at 0x{fpOpenChatBubble:X}.");
                                            _openChatBubbleHook = Service.GameInteropProvider.HookFromAddress<NPCSpeechBubble>(fpOpenChatBubble, NPCBubbleTextDetour);
                                            _openChatBubbleHook?.Enable();
                                        } else {
                                            throw new Exception("Unable to find the specified function signature for OpenChatBubble.");
                                        }
                                    }
                                    alreadyConfiguredBubbles = true;
                                    if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                                        _plugin.Config.NpcSpeechGenerationDisabled = Service.ClientState.ClientLanguage != Dalamud.ClientLanguage.English;
                                    }
                                }
                                _state = GetTalkAddonState();
                                if (_state == null) {
                                    _state = GetBattleTalkAddonState();
                                }
                                Task.Run(delegate {
                                    if (_state != null && !string.IsNullOrEmpty(_state.Text) && _state.Speaker != "All") {
                                        _textIsPresent = true;
                                        _blockNpcChat = false;
                                        if (_state.Text != _currentText) {
                                            _lastText = _currentText;
                                            _currentText = _state.Text;
                                            _redoLineWindow.IsOpen = false;
                                            if (!_blockAudioGeneration) {
                                                NPCText(AliasDetector(_state.Speaker), _state.Text.TrimStart('.'), false, true);
                                                _startedNewDialogue = true;
                                                _passthroughTimer.Reset();
                                            }
                                            if (_plugin.Config.DebugMode) {
                                                DumpCurrentAudio(_state.Speaker);
                                            }
                                            if (_currentDialoguePaths.Count > 0) {
                                                _currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] = true;
                                            }
                                            _blockAudioGeneration = false;
                                        }
                                    } else {
                                        if (_currentDialoguePaths.Count > 0) {
                                            if (!_currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] && !_blockAudioGeneration && !_plugin.Config.NpcSpeechGenerationDisabled) {
                                                try {
                                                    var otherData = _clientState.LocalPlayer.OnlineStatus;
                                                    if (otherData.Id == 15) {
                                                        ScdFile scdFile = GetScdFile(_currentDialoguePaths[_currentDialoguePaths.Count - 1]);
                                                        WaveStream stream = scdFile.Audio[0].Data.GetStream();
                                                        var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                                                        _plugin.MediaManager.PlayAudioStream(new DummyObject(),
                                                            pcmStream, SoundType.NPC, false, false, 1, 0, true, null);
                                                    }
                                                } catch (Exception e) {
                                                    Dalamud.Logging.PluginLog.LogError(e, e.Message);
                                                }
                                            }
                                            if (_currentDialoguePaths.Count > 0) {
                                                _currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] = true;
                                            }
                                        }
                                        if (_currentSpeechObject != null && _startedNewDialogue) {
                                            var otherData = _clientState.LocalPlayer.OnlineStatus;
                                            if (otherData.Id != 15) {
                                                _namesToRemove.Clear();
                                                _currentText = "";
                                                _currentSpeechObject = null;
                                                _currentDialoguePaths.Clear();
                                                _currentDialoguePathsCompleted.Clear();
                                            }
                                            if (!Conditions.IsBoundByDuty) {
                                                _plugin.MediaManager.StopAudio(_currentSpeechObject);
                                                _plugin.MediaManager.CleanSounds();
                                            }
                                            _startedNewDialogue = false;
                                        }
                                        _threadSafeObjectTable = _objectTable.ToList();
                                        _redoLineWindow.IsOpen = false;
                                        if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                            _blockAudioGeneration = false;
                                        }
                                        _textIsPresent = false;
                                    }
                                });
                            }
                        }
                    } catch (Exception e) {
                        Dalamud.Logging.PluginLog.Log(e, e.Message);
                    }
                    pollingTimer.Restart();
                }
        }

        private void GetAnimationDefaults() {
            var actorMemory = new ActorMemory();
            actorMemory.SetAddress(_clientState.LocalPlayer.Address);
            var animationMemory = actorMemory.Animation;
            animationMemory.LipsOverride = LipSyncTypes[5].Timeline.AnimationId;
            _defaultBaseOverride = MemoryService.Read<ushort>(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)));
            _defaultCharacterModeInput = MemoryService.Read<ushort>(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeInput)));
            _defaultCharacterModeRaw = MemoryService.Read<byte>(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)));
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

        public async void TriggerLipSync(Character character, int lipSyncType) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                animationMemory.LipsOverride = LipSyncTypes[lipSyncType].Timeline.AnimationId;
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), LipSyncTypes[lipSyncType].Timeline.AnimationId, "Lipsync");
                Task.Run(delegate {
                    Thread.Sleep(10000);
                    StopLipSync(character);
                });
            } catch {

            }
        }

        public async void TriggerEmote(Character character, ushort emoteId) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                if (animationMemory.BaseOverride != emoteId) {
                    animationMemory!.BaseOverride = emoteId;
                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), emoteId, "Base Override");
                }
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
            } catch {

            }
        }
        public ushort GetCurrentEmoteId(Character character) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                return MemoryService.Read<ushort>(actorMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)));
            } catch {

            }
            return 0;
        }
        public async void StopEmote(Character character) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;

                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeInput)), _defaultCharacterModeInput, "Animation Mode Input Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), _defaultCharacterModeRaw, "Animation Mode Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
            } catch {

            }
        }
        public async void StopLipSync(Character character) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                animationMemory.LipsOverride = LipSyncTypes[5].Timeline.AnimationId;
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
            } catch {

            }
        }
        public static float[] DecodeOggOpusToPCM(Stream stream) {
            // Read the Opus file
            // Initialize the decoder
            OpusDecoder decoder = new OpusDecoder(48000, 1); // Assuming a sample rate of 48000 Hz and mono audio
            OpusOggReadStream oggStream = new OpusOggReadStream(decoder, stream);

            // Buffer for storing the decoded samples
            List<float> pcmSamples = new List<float>();

            // Read and decode the entire file
            while (oggStream.HasNextPacket) {
                short[] packet = oggStream.DecodeNextPacket();
                if (packet != null) {
                    foreach (var sample in packet) {
                        pcmSamples.Add(sample / 32768f); // Convert to float and normalize
                    }
                }
            }

            return pcmSamples.ToArray();
        }

        public string FeoUlRetainerCleanup(string npcName, string message) {
            if (npcName == "Feo Ul") {
                string[] words = message.Split(' ');
                string cleanedMessage = "";
                if (message.Contains("Oh, my adorable sapling! You have need of ")) {
                    for (int i = 0; i < words.Length; i++) {
                        if (i == 8) {
                            cleanedMessage += "your retainer, ";
                        } else {
                            cleanedMessage += words[i] + " ";
                        }
                    }
                } else if (message.Contains("You have no more need of ")) {
                    for (int i = 0; i < words.Length; i++) {
                        if (i == 6) {
                            cleanedMessage += "your retainer? ";
                        } else {
                            cleanedMessage += words[i] + " ";
                        }
                    }
                }
                return cleanedMessage.Trim();
            }
            return message;
        }
        private async void NPCText(string npcName, string message, string voice, bool lowLatencyMode = false, bool onlySendData = false) {
            if (VerifyIsEnglish(message)) {
                try {
                    bool gender = false;
                    byte race = 0;
                    int body = 0;
                    bool isRetainer = false;
                    if (!isRetainer || !_plugin.Config.DontVoiceRetainers) {
                        string nameToUse = npcName;
                        MediaGameObject currentSpeechObject = new MediaGameObject(_clientState.LocalPlayer);
                        _currentSpeechObject = currentSpeechObject;
                        string backupVoice = voice;
                        Stopwatch downloadTimer = Stopwatch.StartNew();
                        KeyValuePair<Stream, bool> stream =
                        await _plugin.NpcVoiceManager.GetCharacterAudio(message, message, nameToUse, gender, backupVoice, false, true, "", false);
                        //if (!previouslyAddedLines.Contains(value + nameToUse)) {
                        //    _npcVoiceHistoryItems.Add(new NPCVoiceHistoryItem(message, message, nameToUse, gender, backupVoice, false, true, "", true));
                        //    previouslyAddedLines.Add(value + nameToUse);
                        //    if (_npcVoiceHistoryItems.Count > 12) {
                        //        _npcVoiceHistoryItems.RemoveAt(0);
                        //    }
                        //}
                        if (!_plugin.Config.NpcSpeechGenerationDisabled && !onlySendData) {
                            if (stream.Key != null) {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Stream is valid! Download took " + downloadTimer.Elapsed.ToString());
                                }
                                WaveStream wavePlayer = GetWavePlayer(npcName, stream.Key, null);
                                ActorMemory actorMemory = null;
                                AnimationMemory animationMemory = null;
                                ActorMemory.CharacterModes initialState = ActorMemory.CharacterModes.None;
                                Task task = null;
                                ushort lipId = 0;
                                bool canDoLipSync = !Conditions.IsBoundByDuty;
                                if (wavePlayer != null) {
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Waveplayer is valid");
                                    }
                                    bool useSmbPitch = false;
                                    float pitch = stream.Value ? CheckForDefinedPitch(nameToUse) :
                                    CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                                    _chatId = Guid.NewGuid().ToString();
                                    string chatId = _chatId;
                                    bool lipWasSynced = false;
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Attempt to play audio stream.");
                                    }
                                    _plugin.MediaManager.PlayAudioStream(currentSpeechObject, wavePlayer, SoundType.NPC,
                                    Conditions.IsBoundByDuty && Conditions.IsWatchingCutscene, useSmbPitch, pitch, 0,
                                    Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78 || lowLatencyMode, delegate {
                                        if (_hook != null) {
                                            try {
                                            } catch {

                                            }
                                        }
                                    }, delegate (object sender, StreamVolumeEventArgs e) {

                                    }, _plugin.Config.NPCSpeechSpeed);
                                } else {
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Waveplayer failed " + downloadTimer.Elapsed.ToString());
                                    }
                                }
                            } else {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Stream was null! Download took " + downloadTimer.Elapsed.ToString());
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogWarning(e, e.Message);
                    if (_plugin.Config.DebugMode) {
                        _plugin.Chat.Print(e.Message);
                    }
                }
            }
        }
        private async void NPCText(string npcName, string message, bool ignoreAutoProgress, bool lowLatencyMode = false, bool redoLine = false) {
            if (VerifyIsEnglish(message)) {
                try {
                    bool gender = false;
                    byte race = 0;
                    int body = 0;
                    bool isRetainer = false;
                    GameObject npcObject = DiscoverNpc(npcName, message, ref gender, ref race, ref body, ref isRetainer);
                    if (!isRetainer || !_plugin.Config.DontVoiceRetainers) {
                        string nameToUse = npcObject == null || npcName != "???" ? npcName : npcObject.Name.TextValue;
                        MediaGameObject currentSpeechObject = new MediaGameObject(npcObject != null ? npcObject : _clientState.LocalPlayer);
                        _currentSpeechObject = currentSpeechObject;
                        ReportData reportData = new ReportData(npcName, StripPlayerNameFromNPCDialogueArc(message), npcObject);
                        string npcData = JsonConvert.SerializeObject(reportData);
                        string value = FeoUlRetainerCleanup(npcName, StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message))));
                        string arcValue = FeoUlRetainerCleanup(npcName, StripPlayerNameFromNPCDialogueArc(message));
                        string backupVoice = PickVoiceBasedOnTraits(nameToUse, gender, race, body);
                        Stopwatch downloadTimer = Stopwatch.StartNew();
                        if (_plugin.Config.DebugMode) {
                            _plugin.Chat.Print("Get audio from server. Sending " + value);
                        }
                        KeyValuePair<Stream, bool> stream =
                        await _plugin.NpcVoiceManager.GetCharacterAudio(value, arcValue, nameToUse, gender, backupVoice, false, true, npcData, redoLine);
                        if (!previouslyAddedLines.Contains(value + nameToUse)) {
                            _npcVoiceHistoryItems.Add(new NPCVoiceHistoryItem(value, arcValue, nameToUse, gender, backupVoice, false, true, npcData, redoLine));
                            previouslyAddedLines.Add(value + nameToUse);
                            if (_npcVoiceHistoryItems.Count > 10) {
                                _npcVoiceHistoryItems.RemoveAt(0);
                            }
                        }
                        if (stream.Key != null && !_plugin.Config.NpcSpeechGenerationDisabled) {
                            if (_plugin.Config.DebugMode) {
                                _plugin.Chat.Print("Stream is valid! Download took " + downloadTimer.Elapsed.ToString());
                            }
                            WaveStream wavePlayer = GetWavePlayer(npcName, stream.Key, reportData);
                            ActorMemory actorMemory = null;
                            AnimationMemory animationMemory = null;
                            ActorMemory.CharacterModes initialState = ActorMemory.CharacterModes.None;
                            Task task = null;
                            ushort lipId = 0;
                            bool canDoLipSync = !Conditions.IsBoundByDuty;
                            if (wavePlayer != null) {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Waveplayer is valid");
                                }
                                if (npcObject != null && canDoLipSync) {
                                    actorMemory = new ActorMemory();
                                    actorMemory.SetAddress(npcObject.Address);
                                    initialState = actorMemory.CharacterMode;
                                    animationMemory = actorMemory.Animation;
                                    animationMemory.LipsOverride = LipSyncTypes[5].Timeline.AnimationId;
                                    //Stopwatch lipSyncCommitmentTimer = new Stopwatch();
                                    //int lipSyncCommitment = 0;
                                    if (wavePlayer.TotalTime.Seconds < 2) {
                                        lipId = LipSyncTypes[4].Timeline.AnimationId;
                                    } else if (wavePlayer.TotalTime.Seconds < 7) {
                                        //lipSyncCommitment = 6100;
                                        lipId = LipSyncTypes[5].Timeline.AnimationId;
                                    } else {
                                        //lipSyncCommitment = 6100;
                                        lipId = LipSyncTypes[6].Timeline.AnimationId;
                                    }
                                    if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                    }
                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                    if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                    }
                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                    task = Task.Run(delegate {
                                        Thread.Sleep(500);
                                        if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                            MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                        }
                                        MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                        Thread.Sleep((int)wavePlayer.TotalTime.TotalMilliseconds - 1000);
                                        if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                            MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                        }
                                        MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                    });
                                }
                                bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse, body);
                                float pitch = stream.Value ? CheckForDefinedPitch(nameToUse) :
                                CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                                _chatId = Guid.NewGuid().ToString();
                                string chatId = _chatId;
                                bool lipWasSynced = false;
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Attempt to play audio stream.");
                                }
                                _plugin.MediaManager.PlayAudioStream(currentSpeechObject, wavePlayer, SoundType.NPC,
                                Conditions.IsBoundByDuty && Conditions.IsWatchingCutscene, useSmbPitch, pitch, 0,
                                Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78 || lowLatencyMode, delegate {
                                    if (_hook != null) {
                                        try {
                                            if (npcObject != null && canDoLipSync) {
                                                animationMemory.LipsOverride = 0;
                                                if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                                    MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                                }
                                                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                            }
                                            if (_state != null) {
                                                if ((_plugin.Config.AutoTextAdvance && !ignoreAutoProgress
                                            && !_plugin.Config.QualityAssuranceMode)) {
                                                    if (_chatId == chatId) {
                                                        _hook.SendAsyncKey(Keys.NumPad0);
                                                    }
                                                } else {
                                                    if (_plugin.Config.QualityAssuranceMode && !ignoreAutoProgress) {
                                                        _redoLineWindow.IsOpen = true;
                                                    } else if (_plugin.Config.QualityAssuranceMode && !ignoreAutoProgress) {
                                                        _hook.SendAsyncKey(Keys.NumPad0);
                                                    }
                                                }
                                                task.Dispose();
                                            }
                                        } catch {

                                        }
                                    }
                                }, delegate (object sender, StreamVolumeEventArgs e) {
                                    if (npcObject != null && canDoLipSync) {
                                        if (e.MaxSampleValues.Length > 0) {
                                            if (e.MaxSampleValues[0] > 0.2) {
                                                int seconds = wavePlayer.TotalTime.Milliseconds - wavePlayer.CurrentTime.Milliseconds;
                                                float percentage = (float)wavePlayer.CurrentTime.Milliseconds / (float)wavePlayer.TotalTime.Milliseconds;
                                                if (percentage > 0.90f) {
                                                    if (seconds < 2000) {
                                                        lipId = LipSyncTypes[4].Timeline.AnimationId;
                                                    } else if (wavePlayer.TotalTime.Seconds < 7000) {
                                                        lipId = LipSyncTypes[5].Timeline.AnimationId;
                                                    } else {
                                                        lipId = LipSyncTypes[6].Timeline.AnimationId;
                                                    }
                                                }
                                                if ((int)MemoryService.Read(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), typeof(int)) != lipId) {
                                                    if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                                    }
                                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                                    lipWasSynced = true;
                                                }
                                            } else {
                                                if (lipWasSynced) {
                                                    if (!Conditions.IsBoundByDuty || Conditions.IsWatchingCutscene) {
                                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                                    }
                                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                                    lipWasSynced = false;
                                                }
                                            }
                                        }
                                    }
                                }, _plugin.Config.NPCSpeechSpeed);
                            } else {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Waveplayer failed " + downloadTimer.Elapsed.ToString());
                                }
                            }
                        } else {
                            if (_plugin.Config.DebugMode) {
                                _plugin.Chat.Print("Stream was null! Download took " + downloadTimer.Elapsed.ToString());
                            }
                        }
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.LogWarning(e, e.Message);
                    if (_plugin.Config.DebugMode) {
                        _plugin.Chat.Print(e.Message);
                    }
                }
            }
        }

        public WaveStream GetWavePlayer(string npcName, Stream stream, ReportData reportData) {
            WaveStream wavePlayer = null;
            try {
                stream.Position = 0;
                wavePlayer = new Mp3FileReader(stream);
            } catch {
                stream.Position = 0;
                if (stream.Length > 0) {
                    float[] data = DecodeOggOpusToPCM(stream);
                    if (data.Length > 0) {
                        WaveFormat waveFormat = new WaveFormat(48000, 16, 1);
                        MemoryStream memoryStream = new MemoryStream();
                        WaveFileWriter writer = new WaveFileWriter(memoryStream, waveFormat);
                        writer.WriteSamples(data.ToArray(), 0, data.Length);
                        writer.Flush();
                        memoryStream.Position = 0;
                        if (memoryStream.Length > 0) {
                            var newPlayer = new WaveFileReader(memoryStream);
                            if (newPlayer.TotalTime.Milliseconds > 100) {
                                wavePlayer = newPlayer;
                            } else {
                                Dalamud.Logging.PluginLog.LogWarning($"Sound for {npcName} is too short.");
                            }
                        } else {
                            Dalamud.Logging.PluginLog.LogWarning($"Memory stream stream for {npcName} is empty.");
                        }
                    } else {
                        Dalamud.Logging.PluginLog.LogWarning($"PCM Decoded audio stream for {npcName} is empty.");
                    }
                } else {
                    Dalamud.Logging.PluginLog.LogWarning($"Received audio stream for {npcName} is empty.");
                    if (reportData != null) {
                        reportData.ReportToXivVoice();
                    }
                }
            }
            return wavePlayer;
        }

        private async void NPCText(string name, string message, bool gender,
            byte race, int body, byte tribe, byte eyes, uint objectId, MediaGameObject mediaGameObject) {
            if (VerifyIsEnglish(message) && !message.Contains("You have submitted")) {
                try {
                    string nameToUse = name;
                    MediaGameObject currentSpeechObject = mediaGameObject;
                    _currentSpeechObject = currentSpeechObject;
                    string value = StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message)));
                    ReportData reportData = new ReportData(name, message, objectId, body, gender, race, tribe, eyes);
                    string npcData = JsonConvert.SerializeObject(reportData);
                    KeyValuePair<Stream, bool> stream =
                    await _plugin.NpcVoiceManager.GetCharacterAudio(value,
                    StripPlayerNameFromNPCDialogueArc(message), nameToUse, gender,
                    PickVoiceBasedOnTraits(nameToUse, gender, race, body), false, true, npcData);
                    if (stream.Key != null && !_plugin.Config.NpcSpeechGenerationDisabled) {
                        WaveStream wavePlayer = GetWavePlayer(name, stream.Key, reportData);
                        bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse, body);
                        float pitch = stream.Value ? CheckForDefinedPitch(nameToUse) :
                         CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                        _plugin.MediaManager.PlayAudioStream(currentSpeechObject, wavePlayer, SoundType.NPC,
                       Conditions.IsBoundByDuty && Conditions.IsWatchingCutscene, useSmbPitch, pitch, 0,
                       Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78, null);
                    } else {
                    }
                } catch {
                }
            }
        }

        /// <summary>
        /// Line generation is expensive, for now enforce only generating english text.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private bool VerifyIsEnglish(string message) {
            string[] symbolBlacklist = new string[] { "¿", "á", "í", "ó", "ú", "ñ", "ü" };
            foreach (string symbol in symbolBlacklist) {
                if (message.Contains(symbol)) {
                    return false;
                }
            }
            return _clientState.ClientLanguage == Dalamud.ClientLanguage.English;
        }

        private string FindNPCNameFromMessage(string message) {
            try {
                return _knownNpcs[message];
            } catch {
                return "???";
            }
        }
        private unsafe GameObject DiscoverNpc(string npcName, string message, ref bool gender, ref byte race, ref int body, ref bool isRetainer) {
            if (npcName == "???") {
                npcName = FindNPCNameFromMessage(message);
            }
            List<string> npcBlacklist = new List<string>(){
                    "Journeyman Salvager",
                    "Materia Melder",
                    "Steward",
                    "Hokonin",
                    "Material Supplier",
                    "Junkmonger",
                    "Mender",
                    "Allagan Node",
                    "Servingway",
                    "Estate"
                };
            if (npcName == "???" || npcName == "Narrator") {
                List<string> npcNames = new List<string>(){
                    "Minfillia",
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
                    "Beq Lugg"
                };
                foreach (var item in _objectTable) {
                    if (!npcNames.Contains(item.Name.TextValue) && !string.IsNullOrEmpty(item.Name.TextValue)) {
                        Character character = item as Character;
                        if (character != null && character != _clientState.LocalPlayer) {
                            if (character.Customize[(byte)CustomizeIndex.ModelType] > 0) {
                                if (item.ObjectKind == ObjectKind.EventNpc && !item.Name.TextValue.Contains("Estate")) {
                                    if (!npcNames.Contains(item.Name.TextValue)) {
                                        npcNames.Add(item.Name.TextValue);
                                    }
                                }
                            }
                        }
                    }
                }
                foreach (var item in _namesToRemove) {
                    npcNames.Remove(item);
                }
                foreach (var item in npcBlacklist) {
                    npcNames.Remove(item);
                }
                foreach (var name in npcNames) {
                    foreach (var item in _objectTable) {
                        if (item.Name.TextValue.Contains(name) && !string.IsNullOrEmpty(item.Name.TextValue)) {
                            Character character = item as Character;
                            if (character != null && character != _clientState.LocalPlayer) {
                                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                                race = character.Customize[(int)CustomizeIndex.Race];
                                body = character.Customize[(int)CustomizeIndex.ModelType];
                                isRetainer = character.ObjectKind == ObjectKind.Retainer;
                                if (body == 0) {
                                    var gameObject = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)item.Address);
                                    body = gameObject->CharacterData.ModelSkeletonId;
                                }
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print(item.Name.TextValue + " is model type " + body + ", and race " + race + ".");
                                }
                                return character;
                            }
                            return item;
                        }
                    }
                }
            } else {
                foreach (var item in _objectTable) {
                    if (item.Name.TextValue == npcName) {
                        _namesToRemove.Add(npcName);
                        return GetCharacterData(item, ref gender, ref race, ref body, ref isRetainer);
                    }
                }
                foreach (var item in _objectTable) {
                    if (item != _clientState.LocalPlayer && !ContainsItemInList(item.Name.TextValue, npcBlacklist)) {
                        if (item.ObjectKind == ObjectKind.EventNpc) {
                            _namesToRemove.Add(npcName);
                            return GetCharacterData(item, ref gender, ref race, ref body, ref isRetainer);
                        }
                    }
                }
            }
            return null;
        }
        public bool ContainsItemInList(string value, List<string> list) {
            foreach (string item in list) {
                if (value.Contains(item)) {
                    return true;
                }
            }
            return false;
        }
        private unsafe Character GetCharacterData(GameObject gameObject, ref bool gender, ref byte race, ref int body, ref bool isRetainer) {
            Character character = gameObject as Character;
            if (character != null) {
                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                race = character.Customize[(int)CustomizeIndex.Race];
                body = character.Customize[(int)CustomizeIndex.ModelType];
                isRetainer = character.ObjectKind == ObjectKind.Retainer;
                if (body == 0) {
                    var unsafeReference = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)character.Address);
                    body = unsafeReference->CharacterData.ModelSkeletonId;
                }
                if (_plugin.Config.DebugMode) {
                    _plugin.Chat.Print(character.Name.TextValue + " is model type " + body + ", and race " + race + ".");
                }
            }
            return character;
        }

        private string StripPlayerNameFromNPCDialogue(string value) {
            string[] mainCharacterName = _clientState.LocalPlayer.Name.TextValue.Split(" ");
            return value.Replace(mainCharacterName[0], null).Replace(mainCharacterName[1], null);
        }
        private string StripPlayerNameFromNPCDialogueArc(string value) {
            string[] mainCharacterName = _clientState.LocalPlayer.Name.TextValue.Split(" ");
            return value.Replace(mainCharacterName[0] + " " + mainCharacterName[1], "Arc")
                        .Replace(mainCharacterName[0], "Arc")
                        .Replace(mainCharacterName[1], "Arc");
        }
        private bool CheckIfshouldUseSmbPitch(string npcName, int bodyType) {
            foreach (var value in NPCVoiceMapping.GetEchoType()) {
                if (npcName.Contains(value.Key)) {
                    return value.Value;
                }
            }
            switch (bodyType) {
                case 60:
                case 63:
                case 239:
                case 278:
                    return true;
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
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Yda", "Eeda"));
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

        private float CalculatePitchBasedOnTraits(string value, bool gender, byte race, int body, float range) {
            string lowered = value.ToLower();
            Random random = new Random(GetSimpleHash(value));
            bool isTinyRace = lowered.Contains("way") || body == 4 || (body == 0 && _clientState.TerritoryType == 816)
                || (body == 0 && _clientState.TerritoryType == 152) || (body == 110005) || (body == 278);
            bool isDragonOrVoid = false;
            float pitch = CheckForDefinedPitch(value);
            float pitchOffset = (((float)random.Next(-100, 100) / 100f) * range);
            if (!gender && body != 4) {
                switch (race) {
                    case 0:
                        pitchOffset = (((float)Math.Abs(random.Next(-10, 100)) / 100f) * range);
                        break;
                    case 1:
                        pitchOffset = (((float)Math.Abs(random.Next(-50, 100)) / 100f) * range);
                        break;
                    case 2:
                        pitchOffset = (((float)Math.Abs(random.Next(-10, 100)) / 100f) * range);
                        break;
                    case 3:
                        pitchOffset = (((float)Math.Abs(random.Next(-50, 100)) / 100f) * range);
                        break;
                    case 4:
                        pitchOffset = (((float)Math.Abs(random.Next(-100, 100)) / 100f) * range);
                        break;
                    case 5:
                        pitchOffset = (((float)Math.Abs(random.Next(-10, 100)) / 100f) * range);
                        break;
                    case 6:
                        pitchOffset = (((float)Math.Abs(random.Next(-10, 100)) / 100f) * range);
                        break;
                    case 7:
                        pitchOffset = (((float)Math.Abs(random.Next(-100, 100)) / 100f) * range);
                        break;
                    case 8:
                        pitchOffset = (((float)Math.Abs(random.Next(-10, 100)) / 100f) * range);
                        break;
                }
                switch (body) {
                    case 60:
                    case 63:
                    case 239:
                        pitchOffset = (((float)Math.Abs(random.Next(-100, -10)) / 100f) * range);
                        isDragonOrVoid = true;
                        break;
                }
            } else {
                switch (body) {
                    case 4:
                        switch (gender) {
                            case false:
                                pitchOffset = (((float)Math.Abs(random.Next(-100, 100)) / 100f) * range);
                                break;
                            case true:
                                pitchOffset = (((float)random.Next(0, 100) / 100f) * range);
                                break;

                        }
                        break;
                }
            }
            if (pitch == 1) {
                return (isTinyRace ? 1.2f : isDragonOrVoid ? 0.9f : 1) + pitchOffset;
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
        private AddonTalkState GetBattleTalkAddonState() {
            if (!this.addonTalkManager.IsVisible()) {
                return default;
            }

            var addonTalkText = this.addonTalkManager.ReadTextBattle();
            return addonTalkText != null
                ? new AddonTalkState(addonTalkText.Speaker, addonTalkText.Text)
                : default;
        }

        public string PickVoiceBasedOnTraits(string npcName, bool gender, byte race, int body) {
            string[] maleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, false);
            string[] femaleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, true);
            string[] femaleViera = new string[] { "Aet", "Cet", "Uet" };
            foreach (KeyValuePair<string, string> voice in NPCVoiceMapping.GetExtrasVoiceMappings()) {
                if (npcName.Contains(voice.Key)) {
                    return voice.Value;
                }
            }
            if (npcName.EndsWith("way") || _clientState.TerritoryType == 959) {
                return "Lrit";
            }
            if (npcName.ToLower().Contains("kup") || npcName.ToLower().Contains("puk")
                || npcName.ToLower().Contains("mog") || npcName.ToLower().Contains("moogle")
                || npcName.ToLower().Contains("furry creature") || body == 11006) {
                return "Kop";
            }
            if (body == 11029) {
                gender = true;
            }
            if (npcName.ToLower().Contains("siren")) {
                gender = true;
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
            framework.Update -= Framework_Update;
            _chatGui.ChatMessage -= _chatGui_ChatMessage;
            _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
            _toast.Toast -= _toast_Toast;
            disposed = true;

            _memoryService.Shutdown();
            _settingService.Shutdown();
            _gameDataService.Shutdown();
            _actorService.Shutdown();
            _gposeService.Shutdown();
            _addressService.Shutdown();
            _poseService?.Shutdown();
            _targetService?.Shutdown();
            _openChatBubbleHook?.Dispose();
            addonTalkManager?.Dispose();
        }
        private unsafe delegate IntPtr NPCSpeechBubble(IntPtr pThis, FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* pActor, IntPtr pString, bool param3);
    }
}
public class UserAnimationOverride {
    public ushort BaseAnimationId { get; set; } = 0;
    public ushort BlendAnimationId { get; set; } = 0;
    public bool Interrupt { get; set; } = true;
}
