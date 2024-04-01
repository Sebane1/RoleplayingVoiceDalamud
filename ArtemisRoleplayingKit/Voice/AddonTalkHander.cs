using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
using ImGuiNET;
using NAudio.Lame;
using NAudio.Wave;
using RoleplayingVoice;
using RoleplayingVoiceDalamud.Services;
using SoundFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using XivCommon.Functions;
using static System.Windows.Forms.AxHost;
using Character = Dalamud.Game.ClientState.Objects.Types.Character;
using SoundType = RoleplayingMediaCore.SoundType;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkHandler : IDisposable {
        private AddonTalkManager addonTalkManager;
        private IFramework framework;
        private IObjectTable objects;
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
        // private readonly Object _speechBubbleInfoLockObj = new();
        //private readonly Object mGameChatInfoLockObj = new();
        private readonly List<NPCBubbleInformation> _speechBubbleInfo = new();
        private readonly Queue<NPCBubbleInformation> _speechBubbleInfoQueue = new();
        private readonly List<NPCBubbleInformation> _gameChatInfo = new();

        public bool TextIsPresent { get => _textIsPresent; set => _textIsPresent = value; }

        public AddonTalkHandler(AddonTalkManager addonTalkManager, IFramework framework, IObjectTable objects,
            IClientState clientState, Plugin plugin, IChatGui chatGui, ISigScanner sigScanner) {
            this.addonTalkManager = addonTalkManager;
            this.framework = framework;
            this.objects = objects;
            _clientState = clientState;
            framework.Update += Framework_Update;
            _plugin = plugin;
            _hook = new FFXIVHook();
            _hook.Hook(Process.GetCurrentProcess());
            _chatGui = chatGui;
            _chatGui.ChatMessage += _chatGui_ChatMessage;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _scanner = sigScanner;
            bubbleCooldown.Start();
        }

        private void _clientState_TerritoryChanged(ushort obj) {
            _speechBubbleInfo.Clear();
        }

        private void _chatGui_ChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (_clientState.IsLoggedIn && 
                !_plugin.Config.NpcSpeechGenerationDisabled && bubbleCooldown.ElapsedMilliseconds > 200 && Conditions.IsBoundByDuty) {
                if (_state == null) {
                    switch (type) {
                        case XivChatType.NPCDialogueAnnouncements:
                            if (message.TextValue != _lastText && !Conditions.IsWatchingCutscene && !_blockAudioGeneration) {
                                _lastText = message.TextValue;
                                NPCText(sender.TextValue, message.TextValue.TrimStart('.'), true, !Conditions.IsBoundByDuty);
#if DEBUG
                                _plugin.Chat.Print("Sent audio from NPC chat.");
#endif
                            }
                            _lastText = message.TextValue;
                            _blockAudioGeneration = false;
                            break;
                    }
                }
            }
        }

        private void Filter_OnCutsceneAudioDetected(object sender, SoundFilter.InterceptedSound e) {
            if (_clientState != null) {
                if (_clientState.IsLoggedIn) {
                    if (!_currentDialoguePaths.Contains(e.SoundPath)) {
                        _blockAudioGeneration = e.isBlocking;
                        _currentDialoguePaths.Add(e.SoundPath);
                        _currentDialoguePathsCompleted.Add(false);
#if DEBUG
                        _plugin.Chat.Print("Block Next Line Of Dialogue Is " + e.isBlocking);
#endif
                    }
                }
            }
        }
        unsafe private IntPtr NPCBubbleTextDetour(IntPtr pThis, GameObject* pActor, IntPtr pString, bool param3) {
            try {
                if (_clientState.IsLoggedIn && !_plugin.Config.NpcSpeechGenerationDisabled
                    && !Conditions.IsWatchingCutscene && !Conditions.IsWatchingCutscene78) {
                    if (pString != IntPtr.Zero &&
                    !Service.ClientState.IsPvPExcludingDen) {
                        //	Idk if the actor can ever be null, but if it can, assume that we should print the bubble just in case.  Otherwise, only don't print if the actor is a player.
                        if (pActor == null || pActor->ObjectKind != ObjectKind.Player) {
                            long currentTime_mSec = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                            SeString speakerName = SeString.Empty;
                            if (pActor != null && pActor->Name != null) {
                                speakerName = pActor->Name;
                            }
                            var npcBubbleInformaton = new NPCBubbleInformation(MemoryHelper.ReadSeStringNullTerminated(pString), currentTime_mSec, speakerName);
                            var extantMatch = _speechBubbleInfo.Find((x) => { return x.IsSameMessageAs(npcBubbleInformaton); });
                            if (extantMatch != null) {
                                extantMatch.TimeLastSeen_mSec = currentTime_mSec;
                            } else {
                                _speechBubbleInfo.Add(npcBubbleInformaton);
                                try {
                                    if (!_blockAudioGeneration && bubbleCooldown.ElapsedMilliseconds > 200) {
                                        FFXIVClientStructs.FFXIV.Client.Game.Character.Character* character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pActor;
                                        if ((ObjectKind)character->GameObject.ObjectKind == ObjectKind.EventNpc || (ObjectKind)character->GameObject.ObjectKind == ObjectKind.BattleNpc) {
                                            string nameID = character->DrawData.Top.Value.ToString() + character->DrawData.Head.Value.ToString() +
                                               character->DrawData.Feet.Value.ToString() + character->DrawData.Ear.Value.ToString() + speakerName.TextValue + character->GameObject.DataID;
                                            Character characterObject = GetCharacterFromId(character->GameObject.ObjectID);
                                            string finalName = characterObject != null && !string.IsNullOrEmpty(characterObject.Name.TextValue) && Conditions.IsBoundByDuty ? characterObject.Name.TextValue : nameID;
                                            if (npcBubbleInformaton.MessageText.TextValue != _lastText) {
                                                NPCText(finalName,
                                                    npcBubbleInformaton.MessageText.TextValue, character->DrawData.CustomizeData.Sex == 1,
                                                    character->DrawData.CustomizeData.Race, character->DrawData.CustomizeData.BodyType, character->GameObject.Position);
#if DEBUG
                                                _plugin.Chat.Print("Sent audio from NPC bubble.");
#endif
                                            }
                                        }
                                    }
                                    _lastText = npcBubbleInformaton.MessageText.TextValue;
                                    bubbleCooldown.Restart();
                                    _blockAudioGeneration = false;
                                } catch {
                                    NPCText(pActor->Name.TextValue, npcBubbleInformaton.MessageText.TextValue, true);
                                }
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
            foreach (GameObject gameObject in Service.ObjectTable) {
                if (gameObject.ObjectId == id) {
                    return gameObject as Character;
                }
            }
            return null;
        }
        private void Framework_Update(IFramework framework) {
            if (!disposed)
                try {
                    if (_clientState != null) {
                        if (_clientState.IsLoggedIn && !_plugin.Config.NpcSpeechGenerationDisabled) {
                            if (_plugin.Filter.IsCutsceneDetectionNull()) {
                                if (!_alreadyAddedEvent) {
                                    _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
                                    _alreadyAddedEvent = true;
                                }
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
                            if (_state != null && !string.IsNullOrEmpty(_state.Text) && _state.Speaker != "All") {
                                _textIsPresent = true;
                                if (_state.Text != _currentText) {
                                    _lastText = _currentText;
                                    _currentText = _state.Text;
                                    if (!_blockAudioGeneration) {
                                        NPCText(_state.Speaker, _state.Text.TrimStart('.'), false);
                                        _startedNewDialogue = true;
                                        _passthroughTimer.Reset();
                                    }
#if DEBUG
                                    DumpCurrentAudio(_state.Speaker);
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
                                            var otherData = _clientState.LocalPlayer.OnlineStatus;
                                            if (otherData.Id == 15) {
                                                ScdFile scdFile = GetScdFile(_currentDialoguePaths[_currentDialoguePaths.Count - 1]);
                                                WaveStream stream = scdFile.Audio[0].Data.GetStream();
                                                var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                                                _plugin.MediaManager.PlayAudioStream(new DummyObject(),
                                                    pcmStream, SoundType.NPC, false, false, 1, 0, true, _plugin.Config.AutoTextAdvance ? delegate {
                                                        if (_hook != null) {
                                                            try {
                                                                _hook.SendAsyncKey(Keys.NumPad0);
                                                            } catch {

                                                            }
                                                        }
                                                    }
                                                : null);
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
                                        _plugin.MediaManager.StopAudio(_currentSpeechObject);
                                        _plugin.MediaManager.CleanSounds();
                                        _currentSpeechObject = null;
                                        _currentDialoguePaths.Clear();
                                        _currentDialoguePathsCompleted.Clear();
                                    }
                                    _startedNewDialogue = false;
                                }
                                _blockAudioGeneration = false;
                                _textIsPresent = false;
                            }
                        }
                    }
                } catch (Exception e) {
                    Dalamud.Logging.PluginLog.Log(e, e.Message);
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
        private async void NPCText(string npcName, string message, bool ignoreAutoProgress, bool lowLatencyMode = false) {
            try {
                bool gender = false;
                byte race = 0;
                byte body = 0;
                GameObject npcObject = DiscoverNpc(npcName, ref gender, ref race, ref body);
                string nameToUse = npcObject != null ? npcObject.Name.TextValue : npcName;
                MediaGameObject currentSpeechObject = new MediaGameObject(npcObject != null ? npcObject : _clientState.LocalPlayer);
                _currentSpeechObject = currentSpeechObject;
                string value = StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message)));
                KeyValuePair<Stream, bool> stream =
                await _plugin.NpcVoiceManager.GetCharacterAudio(value, StripPlayerNameFromNPCDialogueArc(message), nameToUse, gender, PickVoiceBasedOnTraits(nameToUse, gender, race, body), false, true);
                if (stream.Key != null) {
                    var mp3Stream = new Mp3FileReader(stream.Key);
                    bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse);
                    float pitch = stream.Value ? CheckForDefinedPitch(nameToUse) : CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                    _plugin.MediaManager.PlayAudioStream(currentSpeechObject, mp3Stream, SoundType.NPC, true, useSmbPitch, pitch, 0, 
                    Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78 || lowLatencyMode,
                   (_plugin.Config.AutoTextAdvance && !ignoreAutoProgress) ? delegate {
                       if (_hook != null) {
                           try {
                               _hook.SendAsyncKey(Keys.NumPad0);
                           } catch {

                           }
                       }
                   }
                    : null);
                } else {
                }
            } catch {
            }
        }
        private async void NPCText(string name, string message, bool gender, byte race, byte body, Vector3 position) {
            try {
                GameObject npcObject = null;
                string nameToUse = name;
                MediaGameObject currentSpeechObject = new MediaGameObject(name, position);
                _currentSpeechObject = currentSpeechObject;
                string value = StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message)));
                KeyValuePair<Stream, bool> stream =
                await _plugin.NpcVoiceManager.GetCharacterAudio(value, StripPlayerNameFromNPCDialogueArc(message), nameToUse, gender, PickVoiceBasedOnTraits(nameToUse, gender, race, body), false, true);
                if (stream.Key != null) {
                    var mp3Stream = new Mp3FileReader(stream.Key);
                    bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse);
                    float pitch = stream.Value ? CheckForDefinedPitch(nameToUse) : CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                    _plugin.MediaManager.PlayAudioStream(currentSpeechObject, mp3Stream, SoundType.NPC, true, useSmbPitch, pitch, 0,
                   Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78, null);
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
                List<string> npcBlacklist = new List<string>(){
                    "Journeyman Salvager",
                    "Materia Melder",
                    "Masked Mage",
                    "Steward",
                    "Hokonin",
                    "Material Supplier",
                    "Junkmonger",
                    "Mender",
                    "Allagan Node"
                };
                foreach (var item in objects) {
                    if (!npcNames.Contains(item.Name.TextValue) && !string.IsNullOrEmpty(item.Name.TextValue)) {
                        Character character = item as Character;
                        if (character != null && character != _clientState.LocalPlayer) {
                            if (character.Customize[(byte)CustomizeIndex.ModelType] > 0) {
                                if (item.ObjectKind == ObjectKind.EventNpc && !item.Name.TextValue.Contains("Estate")) {
                                    npcNames.Add(item.Name.TextValue);
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
                    foreach (var item in objects) {
                        if (item.Name.TextValue.Contains(name) && !string.IsNullOrEmpty(item.Name.TextValue)) {
                            Character character = item as Character;
                            if (character != null && character != _clientState.LocalPlayer) {
                                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                                race = character.Customize[(int)CustomizeIndex.Race];
                                body = character.Customize[(int)CustomizeIndex.ModelType];
#if DEBUG
                                _plugin.Chat.Print(item.Name.TextValue + " is model type " + body + ", and race " + race + ".");
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
                        return GetCharacterData(item, ref gender, ref race, ref body);
                    }
                }
            }
            return null;
        }

        private GameObject GetCharacterData(GameObject gameObject, ref bool gender, ref byte race, ref byte body) {
            Character character = gameObject as Character;
            if (character != null) {
                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                race = character.Customize[(int)CustomizeIndex.Race];
                body = character.Customize[(int)CustomizeIndex.ModelType];
#if DEBUG
                _plugin.Chat.Print(character.Name.TextValue + " is model type " + body + ", and race " + race + ".");
#endif
            }
            return character;
        }

        private string StripPlayerNameFromNPCDialogue(string value) {
            string[] mainCharacterName = _clientState.LocalPlayer.Name.TextValue.Split(" ");
            return value.Replace(mainCharacterName[0], null).Replace(mainCharacterName[1], null);
        }
        private string StripPlayerNameFromNPCDialogueArc(string value) {
            string[] mainCharacterName = _clientState.LocalPlayer.Name.TextValue.Split(" ");
            return value.Replace(mainCharacterName[0] + " " + mainCharacterName[1], "Arc").Replace(mainCharacterName[0], "Arc").Replace(mainCharacterName[1], "Arc");
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
            } else {
                if (body == 4) {
                    switch (gender) {
                        case false:
                            pitchOffset = (((float)Math.Abs(random.Next(-100, 100)) / 100f) * range);
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
        private AddonTalkState GetBattleTalkAddonState() {
            if (!this.addonTalkManager.IsVisible()) {
                return default;
            }

            var addonTalkText = this.addonTalkManager.ReadTextBattle();
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
            if (npcName.EndsWith("way") || _clientState.TerritoryType == 959) {
                return "Lrit";
            }
            if (npcName.ToLower().Contains("kup") || npcName.ToLower().Contains("puk")
                || npcName.ToLower().Contains("mog") || npcName.ToLower().Contains("moogle")) {
                return "Kop";
            }
            if (body == 0 && gender == false && _clientState.TerritoryType == 612) {
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
            disposed = true;
        }
        private unsafe delegate IntPtr NPCSpeechBubble(IntPtr pThis, GameObject* pActor, IntPtr pString, bool param3);
    }
}
