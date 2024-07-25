using Anamnesis;
using Anamnesis.Actor;
using Anamnesis.Core.Memory;
using Anamnesis.GameData;
using Anamnesis.GameData.Excel;
using Anamnesis.Memory;
using Anamnesis.Services;
using Concentus.Oggfile;
using Concentus.Structs;
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
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ktisis.Structs.Actor;
using Lumina.Excel.GeneratedSheets;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using NVorbis;
using RoleplayingMediaCore;
using RoleplayingVoice;
using RoleplayingVoiceCore;
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
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using ActionTimeline = Anamnesis.GameData.Excel.ActionTimeline;
using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;
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
        private RedoLineWindow _redoLineWindow;
        private IToastGui _toast;
        //private Dictionary<string, string> _knownNpcs;
        private MemoryService _memoryService;
        private SettingsService _settingService;
        private AnimationService _animationService;
        private GameDataService _gameDataService;
        private ActorService _actorService;
        private GposeService _gposeService;
        private AddressService _addressService;
        private PoseService _poseService;
        private TargetService _targetService;
        private List<Dalamud.Game.ClientState.Objects.Types.IGameObject> _threadSafeObjectTable;
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
        private string _lastNPCAnnouncementName;
        ConcurrentDictionary<string, string> _currentlyEmotingCharacters = new ConcurrentDictionary<string, string>();
        Queue<KeyValuePair<string, string>> _npcDungeonDialogueQueue = new Queue<KeyValuePair<string, string>>();
        List<string> _knownNPCBossAnnouncers = new List<string>();
        List<string> _knownNPCAnnouncers = new List<string>();

        public AddonTalkHandler(AddonTalkManager addonTalkManager, IFramework framework, IObjectTable objects,
            IClientState clientState, Plugin plugin, IChatGui chatGui, ISigScanner sigScanner, RedoLineWindow redoLineWindow, IToastGui toastGui) {
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
                //_knownNpcs = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
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
                Plugin.PluginLog.Warning(e, e.Message);
            }
            pollingTimer.Start();
        }


        private void _toast_QuestToast(ref SeString message, ref Dalamud.Game.Gui.Toast.QuestToastOptions options, ref bool isHandled) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                if (_clientState.IsLoggedIn) {
                    if (CheckForBannedKeywords(message) || message.TextValue.Contains("friend request")) {
                        string newMessage = message.TextValue.Contains("friend request") ? "Friend request received." : message.TextValue;
                        NPCText("Narrator", newMessage.Replace(@"0/", "0 out of ")
                            .Replace(@"1/", "1 out of ")
                            .Replace(@"2/", "2 out of ")
                            .Replace(@"3/", "3 out of ")
                            .Replace(@"4/", "4 out of ")
                            .Replace(@"5/", "5 out of ")
                            .Replace(@"6/", "6 out of ")
                            .Replace(@"7/", "7 out of ")
                            .Replace(@"8/", "8 out of ")
                            .Replace(@"9/", "9 out of ")
                            .Replace(@"10/", "10 out of ") + (options.DisplayCheckmark ? " has been completed." : ""), "Hyn", NPCVoiceManager.VoiceModel.Cheap, true, !_plugin.Config.ReadQuestObjectives);
                    }
                }
            }
        }

        private bool CheckForBannedKeywords(SeString message) {
            return !message.TextValue.Contains("you put up for sale") && !message.TextValue.Contains("You are now selling") && !message.TextValue.Contains("picked up by")
                && !message.TextValue.Contains("Challenge log entry") && !message.TextValue.Contains("returned to party")
                && !message.TextValue.Contains("You cancel") && !message.TextValue.Contains("You assign your retainer")
                && !message.TextValue.Contains("loot list") && !message.TextValue.Contains("venture") && !message.TextValue.Contains("added to you favorites")
                 && !message.TextValue.Contains("retainer") && !message.TextValue.Contains("joins the party")
                 && !message.TextValue.Contains("left the party") && !message.TextValue.Contains("You synthesize")
                 && !message.TextValue.Contains("matches found") && !message.TextValue.Contains("is now playing. (Play Mode")
                 && !message.TextValue.Contains("places a hand into") && !message.TextValue.Contains("PATS") && !message.TextValue.Contains("HUGS") && !message.TextValue.Contains("You join") && !message.TextValue.Contains("ready check")
                  && !message.TextValue.Contains("gains experience points.") && !message.TextValue.Contains("has sold") && !message.TextValue.Contains("gone offline.")
                  && !message.TextValue.Contains("friend list.") && !message.TextValue.Contains("sent you a friend request") && !message.TextValue.Contains(" de ")
                  && !message.TextValue.Contains("You sense a") && !message.TextValue.Contains("Las") && !message.TextValue.Contains("You sense a grade")
                  && !message.TextValue.Contains("equip") && !message.TextValue.Contains("El ") && !message.TextValue.Contains("partido") && !message.TextValue.Contains("You obtain")
                  && !message.TextValue.Contains("expelled from the duty") && !message.TextValue.Contains("you can now summon")
                  && !message.TextValue.Contains("allagan tomestones") && !message.TextValue.Contains("recorded in gathering log")
                  && !message.TextValue.Contains("expelled from the duty") && !message.TextValue.Contains("Ready check complete") && !message.TextValue.Contains("aetherpool")
                  && !message.TextValue.Contains("Battle commencing in") && !message.TextValue.Contains("Need") && !message.TextValue.Contains("greed")
                  && !message.TextValue.Contains("pass") && !message.TextValue.Contains("finalizes the") && !message.TextValue.Contains("Free company petition")
                  && !message.TextValue.Contains("Free company petition") && !message.TextValue.Contains("promoted you") && !message.TextValue.Contains("attains rank")
                  && !message.TextValue.Contains("levels increased") && !message.TextValue.Contains("has embarked on an") && !message.TextValue.Contains("voyage finalized")
                  && !message.TextValue.Contains("has joined") && !message.TextValue.Contains("you exchanged") && !message.TextValue.Contains("The compass detects a current approximately")
                  && !message.TextValue.Contains("invites you") && !message.TextValue.Contains("purchase a Jumbo Cactpot") && !message.TextValue.Contains("recorded in gathering")
                  && !message.TextValue.Contains("trophy crystals") && !message.TextValue.Contains("Quality of") && !message.TextValue.Contains("Following") && !message.TextValue.Contains("KO'd") && !message.TextValue.Contains("Unable to equip all items");
        }

        private List<ushort> BannedTerritories = new List<ushort>() {
            674,719,778,746,810,824,677,720,779,758,811,825,845,846,847,922,858,848,885,923, 992,995,997,1092,1071,1095,1140,1168
        };
        private bool _filterWasRan;

        private void _toast_Toast(ref SeString message, ref Dalamud.Game.Gui.Toast.ToastOptions options, ref bool isHandled) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                if (_clientState.IsLoggedIn) {
                    if (CheckForBannedKeywords(message) && message.TextValue.Length < 21) {
                        NPCText("Narrator", message.TextValue, "Hyn", NPCVoiceManager.VoiceModel.Cheap, true, !_plugin.Config.ReadLocationsAndToastNotifications);
                    }
                }
            }
        }

        private IEnumerable<ActionTimeline> GenerateLipList() {
            // Grab "no animation" and all "speak/" animations, which are the only ones valid in this slot
            IEnumerable<ActionTimeline> lips = GameDataService.ActionTimelines.Where(x => x.AnimationId == 0 || (x.Key?.StartsWith("speak/") ?? false));
            return lips;
        }
        private void RedoLineWindow_RedoLineClicked(object sender, string value) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                if (!_blockAudioGeneration) {
                    NPCText(_state.Speaker, _state.Text.TrimStart('.'), true, NPCVoiceManager.VoiceModel.Speed, true, true, value, !string.IsNullOrEmpty(value) ? VoiceLinePriority.SendNote : VoiceLinePriority.None);
                    _startedNewDialogue = true;
                    _passthroughTimer.Reset();
                    _redoLineWindow.IsOpen = false;
                }
            }
        }
        private void _clientState_TerritoryChanged(ushort obj) {
            _speechBubbleInfo.Clear();
            _lastBattleNPCLines.Clear();
            _blockAudioGenerationCount = 0;
            _blockNpcChat = BannedTerritories.Contains(obj);
            _knownNPCBossAnnouncers.Clear();
        }

        private void _chatGui_ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                string text = message.TextValue;
                string npcName = sender.TextValue;
                _lastNPCAnnouncementName = sender.TextValue;
                Task.Run(delegate () {
                    if (_clientState.IsLoggedIn &&
                        !_plugin.Config.NpcSpeechGenerationDisabled && Conditions.IsBoundByDuty) {
                        if (_state == null) {
                            switch (type) {
                                case XivChatType.NPCDialogueAnnouncements:
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("NPC Announcement detected " + npcName + ": "
                                            + text);
                                    }
                                    if (!_lastBattleNPCLines.ContainsKey(npcName)) {
                                        _lastBattleNPCLines[npcName] = "";
                                    }
                                    if (text != _lastBattleNPCLines[npcName] && !IsInACutscene()) {
                                        _lastBattleNPCLines[npcName] = text;
                                        Task.Run(() => {
                                            if (!_knownNPCBossAnnouncers.Contains(npcName) && !_knownNPCAnnouncers.Contains(npcName)) {
                                                bool gender = false, isRetainer = false;
                                                byte race = 0;
                                                int body = 0;
                                                ICharacter npc = (DiscoverNpc(npcName, text, ref gender, ref race, ref body, ref isRetainer) as ICharacter);
                                                if (npc != null && npc.Customize[(int)CustomizeIndex.ModelType] is 0) {
                                                    _knownNPCBossAnnouncers.Add(npcName);
                                                } else {
                                                    _knownNPCAnnouncers.Add(npcName);
                                                }
                                            }
                                            if (!_blockNpcChat) {
                                                Task.Run(() => {
                                                    Thread.Sleep(50);
                                                    _npcDungeonDialogueQueue.Enqueue(new KeyValuePair<string, string>(npcName, text));
                                                });
                                            } else {
                                                _blockNpcChat = false;
                                            }
                                        });
                                    }
                                    break;
                            }
                        }
                    }
                }
                );
            }
        }

        private void Filter_OnCutsceneAudioDetected(object sender, SoundFilter.InterceptedSound e) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                if (_clientState != null) {
                    if (_clientState.IsLoggedIn) {
                        if (!_currentDialoguePaths.Contains(e.SoundPath) || Conditions.IsBoundByDuty) {
                            if (e.SoundPath != _lastSoundPath) {
                                if (e.isBlocking) {
                                    if (_blockAudioGenerationCount < 0) {
                                        _blockAudioGenerationCount = 0;
                                    }
                                    _blockAudioGenerationCount++;
                                    _npcDungeonDialogueQueue.Clear();
                                    _lastSoundPath = e.SoundPath;
                                    foreach (string name in _knownNPCBossAnnouncers) {
                                        _plugin.MediaManager.StopAudio(new MediaGameObject(name, new Vector3()));
                                    }
                                    Task.Run(() => {
                                        Thread.Sleep(1000);
                                        _blockAudioGenerationCount--;
                                    });
                                }
                                _blockAudioGeneration = e.isBlocking;
                                _lastNPCAnnouncementName = null;
                                _currentDialoguePaths.Add(e.SoundPath);
                                _currentDialoguePathsCompleted.Add(false);
                                if (_plugin.Config.DebugMode) {
                                    if (_lastNPCAnnouncementName != null) {
                                        DumpCurrentAudio(_lastNPCAnnouncementName);
                                    }
                                    _plugin.Chat.Print("Block Next Line Of Dialogue Is " + e.isBlocking);
                                    _plugin.Chat.Print("Dialogue block created by " + e.SoundPath);
                                    _plugin.Chat.Print("Blocked generation count is " + _blockAudioGenerationCount);
                                }
                            }
                        }
                    }
                }
            }
        }
        unsafe private IntPtr NPCBubbleTextDetour(IntPtr pThis, FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* pActor, IntPtr pString, bool param3) {
            if (!_plugin.Config.NpcSpeechGenerationDisabled && !_plugin.Config.IgnoreBubblesFromOverworldNPCs) {
                try {
                    if (_clientState.IsLoggedIn
                        && !IsInACutscene() && !Conditions.IsBoundByDuty) {
                        if (pString != IntPtr.Zero &&
                        !Service.ClientState.IsPvPExcludingDen) {
                            //	Idk if the actor can ever be null, but if it can, assume that we should print the bubble just in case.  Otherwise, only don't print if the actor is a player.
                            if (pActor == null || (ObjectKind)pActor->GetObjectKind() != ObjectKind.Player) {
                                long currentTime_mSec = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                                SeString speakerName = SeString.Empty;
                                if (pActor != null && pActor->Name != null) {
                                    var objectId = pActor->GetGameObjectId().ObjectId;
                                    if (NPCVoiceMapping.NpcBubbleRecovery.ContainsKey(objectId)) {
                                        speakerName = NPCVoiceMapping.NpcBubbleRecovery[objectId];
                                    } else {
                                        speakerName = pActor->NameString;
                                    }
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
                                                string nameID =
                                                character->DrawData.EquipmentModelIds[(int)EquipIndex.Chest].Value.ToString() +
                                                character->DrawData.EquipmentModelIds[(int)EquipIndex.Head].Value.ToString() +
                                                character->DrawData.EquipmentModelIds[(int)EquipIndex.Feet].Value.ToString() +
                                                character->DrawData.EquipmentModelIds[(int)EquipIndex.Earring].Value.ToString() +
                                                speakerName.TextValue +
                                                character->BaseId;
                                                ICharacter characterObject = GetCharacterFromId(character->GameObject.GetGameObjectId().ObjectId);
                                                string finalName = characterObject != null && !string.IsNullOrEmpty(characterObject.Name.TextValue) ? characterObject.Name.TextValue : nameID;
                                                if (!_lastBattleNPCLines.ContainsKey(finalName)) {
                                                    _lastBattleNPCLines[finalName] = "";
                                                }
                                                if (npcBubbleInformaton.MessageText.TextValue != _lastBattleNPCLines[finalName]) {
                                                    _lastBattleNPCLines[finalName] = npcBubbleInformaton.MessageText.TextValue;
                                                    if (_blockAudioGenerationCount < 1) {
                                                        if (characterObject != null && characterObject.Customize[(int)CustomizeIndex.ModelType] != 0) {
                                                            if (!_knownNPCBossAnnouncers.Contains(finalName)) {
                                                                _npcDungeonDialogueQueue.Enqueue(new KeyValuePair<string, string>(finalName, npcBubbleInformaton.MessageText.TextValue));
                                                            } else {
                                                                Task.Run(() => {
                                                                    Thread.Sleep(50);
                                                                    _npcDungeonDialogueQueue.Enqueue(new KeyValuePair<string, string>(finalName, npcBubbleInformaton.MessageText.TextValue));
                                                                });
                                                            }
                                                        } else {
                                                            NPCText(finalName,
                                                                npcBubbleInformaton.MessageText.TextValue, character->DrawData.CustomizeData.Sex == 1,
                                                                character->DrawData.CustomizeData.Race, character->DrawData.CustomizeData.BodyType != 0 ?
                                                                character->DrawData.CustomizeData.BodyType : character->CharacterData.ModelSkeletonId,
                                                                character->DrawData.CustomizeData.Tribe, character->DrawData.CustomizeData.EyeShape,
                                                                character->GameObject.GetGameObjectId().ObjectId, new MediaGameObject(pActor), NPCVoiceManager.VoiceModel.Speed);
                                                        }
                                                        if (_plugin.Config.DebugMode) {
                                                            _plugin.Chat.Print("Sent audio from NPC bubble.");
                                                        }
                                                    } else {
                                                        if (_plugin.Config.DebugMode) {
                                                            _plugin.Chat.Print("Blocked bubble " + npcBubbleInformaton.SpeakerName.TextValue + ": "
                                                                + npcBubbleInformaton.MessageText.TextValue);
                                                        }
                                                    }
                                                    //_blockAudioGenerationCount--;
                                                }
                                            }
                                            bubbleCooldown.Restart();
                                            _blockAudioGeneration = false;
                                        } catch {
                                            NPCText(speakerName.TextValue, npcBubbleInformaton.MessageText.TextValue, true, NPCVoiceManager.VoiceModel.Speed);
                                        }
                                    }
                                    );
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog.Info(e, e.Message);
                }
            }
            return _openChatBubbleHook.Original(pThis, pActor, pString, param3);
        }
        private ICharacter GetCharacterFromId(uint id) {
            foreach (Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject in _threadSafeObjectTable) {
                if (gameObject.GameObjectId == id
                    && (gameObject.ObjectKind == ObjectKind.EventNpc || gameObject.ObjectKind == ObjectKind.BattleNpc)) {
                    return gameObject as ICharacter;
                }
            }
            return null;
        }
        private void Framework_Update(IFramework framework) {
            if (!disposed)
                if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                    if (_npcDungeonDialogueQueue.Count > 0) {
                        Task.Run(() => {
                            while (_npcDungeonDialogueQueue.Count > 0) {
                                var item = _npcDungeonDialogueQueue.Dequeue();
                                if (_blockAudioGenerationCount is 0 && !_blockAudioGeneration) {
                                    NPCText(item.Key, item.Value.TrimStart('.'), true, NPCVoiceManager.VoiceModel.Speed, Conditions.IsBoundByDuty);
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Sent audio from NPC chat.");
                                    }
                                } else {
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Blocked announcement " + item.Key + ": "
                                            + item.Value);
                                    }
                                }
                            }
                            //_blockAudioGenerationCount--;
                            _blockAudioGeneration = false;
                        });
                    }
                    if (pollingTimer.ElapsedMilliseconds > 100) {
                        try {
                            if (_clientState != null) {
                                if (_clientState.IsLoggedIn) {
                                    _plugin.Filter.Streaming = !Conditions.IsBoundByDuty && !Conditions.IsInCombat;
                                    if (_plugin.Filter.IsCutsceneDetectionNull()) {
                                        if (!_alreadyAddedEvent) {
                                            _plugin.Filter.OnCutsceneAudioDetected += Filter_OnCutsceneAudioDetected;
                                            //_plugin.Filter.OnFilterWasRan += Filter_OnFilterWasRan;
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
                                                Plugin.PluginLog.Information($"OpenChatBubble function signature found at 0x{fpOpenChatBubble:X}.");
                                                _openChatBubbleHook = Service.GameInteropProvider.HookFromAddress<NPCSpeechBubble>(fpOpenChatBubble, NPCBubbleTextDetour);
                                                _openChatBubbleHook?.Enable();
                                            } else {
                                                throw new Exception("Unable to find the specified function signature for OpenChatBubble.");
                                            }
                                        }
                                        alreadyConfiguredBubbles = true;
                                        if (!_plugin.Config.NpcSpeechGenerationDisabled) {
                                            _plugin.Config.NpcSpeechGenerationDisabled = Service.ClientState.ClientLanguage != ClientLanguage.English;
                                        }
                                    }
                                    _state = GetTalkAddonState();
                                    if (_state == null) {
                                        _state = GetBattleTalkAddonState();
                                    }
                                    Task.Run(delegate {
                                        if (_state != null && !string.IsNullOrEmpty(_state.Text) && _state.Speaker != "All") {
                                            _textIsPresent = true;
                                            if (_state.Text != _currentText) {
                                                _lastText = _currentText;
                                                _currentText = _state.Text;
                                                _redoLineWindow.IsOpen = false;
                                                if (!_blockAudioGeneration) {
                                                    NPCText(NPCVoiceMapping.AliasDetector(_state.Speaker), _state.Text.TrimStart('.'), false, NPCVoiceManager.VoiceModel.Speed, true);
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
                                                if (!_currentDialoguePathsCompleted[_currentDialoguePathsCompleted.Count - 1] &&
                                                !_blockAudioGeneration && !_plugin.Config.NpcSpeechGenerationDisabled) {
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
                                                        Plugin.PluginLog.Error(e, e.Message);
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
                                                if (!IsInACutscene() && !_plugin.Config.AllowDialogueQueuingOutsideCutscenes) {
                                                    _plugin.MediaManager.StopAudio(_currentSpeechObject);
                                                    _plugin.MediaManager.CleanSounds();
                                                }
                                                _startedNewDialogue = false;
                                                _redoLineWindow.IsOpen = false;
                                            }
                                            _threadSafeObjectTable = _objectTable.ToList();
                                            if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                                _blockAudioGeneration = false;
                                            }
                                            _textIsPresent = false;
                                        }
                                    });
                                }
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog.Info(e, e.Message);
                        }
                        pollingTimer.Restart();
                    }
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

        public bool IsInACutscene() {
            return Conditions.IsWatchingCutscene || Conditions.IsWatchingCutscene78 || Conditions.IsOccupiedInCutSceneEvent;
        }
        private void DumpCurrentAudio(string speaker) {
            try {
                if (_currentDialoguePaths.Count > 0) {
                    Directory.CreateDirectory(_plugin.Config.CacheFolder + @"\Dump\");
                    string name = speaker;
                    //string path = _plugin.Config.CacheFolder + @"\Dump\" + name + ".mp3";
                    Directory.CreateDirectory(_plugin.Config.CacheFolder + @"\Dump\" + name);
                    string pathWave = _plugin.Config.CacheFolder + @"\Dump\" + name + @"\" + name + " - " + Guid.NewGuid() + ".wav";
                    //FileInfo fileInfo = null;
                    //try {
                    //    fileInfo = new FileInfo(path);
                    //} catch {

                    //}
                    //if (!fileInfo.Exists || fileInfo.Length < 7500000) {
                    //    try {
                    ScdFile scdFile = GetScdFile(_currentDialoguePaths[_currentDialoguePaths.Count - 1]);
                    WaveStream stream = scdFile.Audio[0].Data.GetStream();
                    var pcmStream = WaveFormatConversionStream.CreatePcmStream(stream);
                    using (WaveFileWriter fileStreamWave = new WaveFileWriter(pathWave, pcmStream.WaveFormat)) {
                        pcmStream.CopyTo(fileStreamWave);
                        fileStreamWave.Close();
                        fileStreamWave.Dispose();
                    }
                    //if (scdFile != null) {
                    //    using (var waveStream = new AudioFileReader(pathWave)) {
                    //        using (FileStream fileStream = new FileStream(path, FileMode.Append, FileAccess.Write)) {
                    //            using (LameMP3FileWriter lame = new LameMP3FileWriter(fileStream, waveStream.WaveFormat, LAMEPreset.VBR_90)) {
                    //                waveStream.CopyTo(lame);
                    //            }
                    //        }
                    //    }
                    //}
                    //File.Delete(pathWave);
                    //    } catch (Exception e) {
                    //        Plugin.PluginLog.Error(e, e.Message);
                    //    }
                    //}
                }
            } catch (Exception e) {
                Plugin.PluginLog.Error(e, e.Message);
            }
        }

        public static string ConvertRomanNumberals(string text) {
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

        public async void TriggerLipSync(ICharacter character, int lipSyncType) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                animationMemory.LipsOverride = LipSyncTypes[lipSyncType].Timeline.AnimationId;
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)),
                    LipSyncTypes[lipSyncType].Timeline.AnimationId, "Lipsync");
                await Task.Run(delegate {
                    Thread.Sleep(10000);
                    StopLipSync(character);
                });
            } catch {

            }
        }

        public async void TriggerEmote(nint character, ushort animationId) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character);
                var animationMemory = actorMemory.Animation;
                if (animationMemory.BaseOverride != animationId) {
                    animationMemory!.BaseOverride = animationId;
                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), animationId, "Base Override");
                }
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
                _plugin.IpcSystem?.InvokeOnTriggerAnimation(character, animationId);
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }
        public async void TriggerEmoteTimed(ICharacter character, ushort animationId, int time = 2000) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                if (animationMemory.BaseOverride != animationId) {
                    animationMemory!.BaseOverride = animationId;
                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), animationId, "Base Override");
                }
                byte originalMode = MemoryService.Read<byte>(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)));
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
                Task.Run(() => {
                    ICharacter reference = character;
                    Thread.Sleep(time);
                    StopEmote(reference.Address);
                    if (_plugin.Config.UsePlayerSync) {
                        unsafe {
                            var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                            if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                _plugin.RoleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", ushort.MaxValue);
                                _plugin.RoleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", ushort.MaxValue);
                                Plugin.PluginLog.Verbose("Sent emote cancellation to server for " + reference);
                            }
                        }
                    }
                });
            } catch {

            }
        }
        public void TriggerEmoteUntilPlayerMoves(IPlayerCharacter player, ICharacter character, ushort emoteId) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                if (animationMemory.BaseOverride != emoteId) {
                    animationMemory!.BaseOverride = emoteId;
                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), emoteId, "Base Override");
                }
                byte originalMode = MemoryService.Read<byte>(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)));
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
                Task.Run(() => {
                    string taskId = Guid.NewGuid().ToString();
                    _currentlyEmotingCharacters[character.GameObjectId.ToString()] = taskId;
                    ICharacter reference = character;
                    Vector3 startingPosition = player.Position;
                    Thread.Sleep(2000);
                    while (_currentlyEmotingCharacters[character.GameObjectId.ToString()] == taskId) {
                        if (Vector3.Distance(startingPosition, player.Position) > 0.001f) {
                            StopEmote(reference.Address);
                            _currentlyEmotingCharacters.Remove(reference.GameObjectId.ToString(), out var item);
                            if (_plugin.Config.UsePlayerSync) {
                                unsafe {
                                    var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address);
                                    if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                        _plugin.RoleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)0);
                                        _plugin.RoleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)0);
                                    }
                                }
                            }
                            break;
                        } else {
                            Thread.Sleep(1000 * _currentlyEmotingCharacters.Count);
                        }
                    }
                });
            } catch {

            }
        }
        public ushort GetCurrentEmoteId(ICharacter character) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;
                return MemoryService.Read<ushort>(actorMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)));
            } catch {

            }
            return 0;
        }
        public async void StopEmote(nint character) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character);
                var animationMemory = actorMemory.Animation;

                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeInput)), _defaultCharacterModeInput, "Animation Mode Input Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), _defaultCharacterModeRaw, "Animation Mode Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.Normal, "Animation Mode Override");
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
                _plugin.IpcSystem.InvokeOnStoppedAnimation(character);
            } catch {

            }
        }
        public async void StopEmote(ICharacter character, byte originalMode) {
            try {
                var actorMemory = new ActorMemory();
                actorMemory.SetAddress(character.Address);
                var animationMemory = actorMemory.Animation;

                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeInput)), _defaultCharacterModeInput, "Animation Mode Input Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), _defaultCharacterModeRaw, "Animation Mode Override");
                MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), originalMode, "Animation Mode Override");
                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.BaseOverride)), _defaultBaseOverride, "Base Override");
                _plugin.IpcSystem.InvokeOnStoppedAnimation(character.Address);
            } catch {

            }
        }
        public async void StopLipSync(ICharacter character) {
            if (character != null) {
                try {
                    var actorMemory = new ActorMemory();
                    actorMemory.SetAddress(character.Address);
                    var animationMemory = actorMemory.Animation;
                    animationMemory.LipsOverride = 0;
                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                } catch {

                }
            }
        }
        public static RawSourceWaveStream DecodeOggOpusToPCM(Stream stream) {
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

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            var memoryStream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            foreach (var sample in pcmSamples.ToArray()) {
                writer.Write(sample);
            }
            memoryStream.Position = 0;
            return new RawSourceWaveStream(stream, waveFormat);
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
        private async void NPCText(string npcName, string message, string voice, NPCVoiceManager.VoiceModel voiceModel, bool lowLatencyMode = false, bool onlySendData = false, string note = "", VoiceLinePriority voiceLinePriority = VoiceLinePriority.None) {
            if (VerifyIsEnglish(message) && !message.Contains("You have submitted")) {
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
                        bool foundName = false;
                        ReportData reportData = new ReportData(npcName, StripPlayerNameFromNPCDialogue(message, _clientState.LocalPlayer.Name.TextValue, ref foundName), 0, 0, true, 0, 0, 0, _clientState.TerritoryType, note);
                        string npcData = JsonConvert.SerializeObject(reportData);
                        var stream =
                        await _plugin.NpcVoiceManager.GetCharacterAudio(message, message, message, nameToUse, gender, backupVoice, false, voiceModel, npcData, false, false, (Conditions.IsBoundByDuty && !IsInACutscene()), _plugin.Config.NpcSpeechGenerationDisabled ? VoiceLinePriority.Datamining : voiceLinePriority);
                        if (!previouslyAddedLines.Contains(message + nameToUse) && !_plugin.Config.NpcSpeechGenerationDisabled) {
                            _npcVoiceHistoryItems.Add(new NPCVoiceHistoryItem(message, message, message, nameToUse, gender, backupVoice, false, true, npcData, false, Conditions.IsBoundByDuty && !IsInACutscene(), stream.Item3));
                            previouslyAddedLines.Add(message + nameToUse);
                            if (_npcVoiceHistoryItems.Count > 10) {
                                _npcVoiceHistoryItems.RemoveAt(0);
                            }
                        }
                        if (!_plugin.Config.NpcSpeechGenerationDisabled && !onlySendData) {
                            if (stream.Item1 != null) {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Stream is valid! Download took " + downloadTimer.Elapsed.ToString());
                                }
                                WaveStream wavePlayer = stream.Item1;
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
                                    float pitch = stream.Item2 ? CheckForDefinedPitch(nameToUse) :
                                    CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                                    _chatId = Guid.NewGuid().ToString();
                                    string chatId = _chatId;
                                    bool lipWasSynced = false;
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Attempt to play audio stream.");
                                    }
                                    _plugin.MediaManager.PlayAudioStream(currentSpeechObject, wavePlayer, SoundType.NPC, IsInACutscene() || _plugin.Config.AllowDialogueQueuingOutsideCutscenes, useSmbPitch, pitch, 0,
                                    IsInACutscene() || lowLatencyMode, delegate {
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
                    Plugin.PluginLog.Warning(e, e.Message);
                    if (_plugin.Config.DebugMode) {
                        _plugin.Chat.Print(e.Message);
                    }
                }
            }
        }
        public static string CleanMessage(string message, string name) {
            bool foundName = false;
            return StripPlayerNameFromNPCDialogue(PhoneticLexiconCorrection(ConvertRomanNumberals(message)), name, ref foundName);
        }
        private async void NPCText(string npcName, string message, bool ignoreAutoProgress, NPCVoiceManager.VoiceModel voiceModel,
            bool lowLatencyMode = false, bool redoLine = false, string note = "", VoiceLinePriority voiceLinePriority = VoiceLinePriority.None) {
            if (VerifyIsEnglish(message) && !message.Contains("You have submitted")) {
                try {
                    bool gender = false;
                    byte race = 0;
                    int body = 0;
                    bool isRetainer = false;
                    Dalamud.Game.ClientState.Objects.Types.IGameObject npcObject = DiscoverNpc(npcName, message, ref gender, ref race, ref body, ref isRetainer);
                    if (!isRetainer || !_plugin.Config.DontVoiceRetainers) {
                        string nameToUse = NPCVoiceMapping.CheckForNameVariant(npcObject == null || npcName != "???" ? npcName : npcObject.Name.TextValue, _clientState.TerritoryType);
                        MediaGameObject currentSpeechObject = new MediaGameObject(npcObject != null ? npcObject : (_clientState.LocalPlayer as Dalamud.Game.ClientState.Objects.Types.IGameObject));
                        _currentSpeechObject = new MediaGameObject(_clientState.LocalPlayer);
                        bool foundName = false;
                        bool isExtra = false;
                        bool isTerritorySpecific = false;
                        string initialCleanedValue = PhoneticLexiconCorrection(ConvertRomanNumberals(message));
                        string value = FeoUlRetainerCleanup(nameToUse, StripPlayerNameFromNPCDialogue(initialCleanedValue, _clientState.LocalPlayer.Name.TextValue, ref foundName));
                        string arcValue = FeoUlRetainerCleanup(nameToUse, StripPlayerNameFromNPCDialogueArc(message));
                        string backupVoice = PickVoiceBasedOnTraits(nameToUse, gender, race, body, ref isExtra, ref isTerritorySpecific);
                        ReportData reportData = new ReportData(npcName, value, npcObject, _clientState.TerritoryType, note);
                        string npcData = JsonConvert.SerializeObject(reportData);
                        Stopwatch downloadTimer = Stopwatch.StartNew();
                        if (_plugin.Config.DebugMode) {
                            _plugin.Chat.Print("Get audio from server. Sending " + value);
                        }
                        var conditionsToUseXivV = foundName || Conditions.IsBoundByDuty ? VoiceLinePriority.Alternative : VoiceLinePriority.None;
                        var conditionToUseElevenLabs = isExtra || isTerritorySpecific ? VoiceLinePriority.Elevenlabs : conditionsToUseXivV;
                        var conditionToUseOverride = voiceLinePriority != VoiceLinePriority.None ? voiceLinePriority : conditionToUseElevenLabs;
                        var conditionsForDatamining = _plugin.Config.NpcSpeechGenerationDisabled ? VoiceLinePriority.Datamining : conditionToUseOverride;
                        for (int i = 0; i < 2; i++) {
                            var stream =
                            await _plugin.NpcVoiceManager.GetCharacterAudio(value, arcValue, initialCleanedValue, nameToUse, gender, backupVoice, false, voiceModel, npcData, redoLine,
                            false, (Conditions.IsBoundByDuty && !IsInACutscene()), conditionsForDatamining);
                            if (!previouslyAddedLines.Contains(value + nameToUse) && !_plugin.Config.NpcSpeechGenerationDisabled) {
                                _npcVoiceHistoryItems.Add(new NPCVoiceHistoryItem(value, arcValue, initialCleanedValue, nameToUse, gender, backupVoice, false,
                                    true, npcData, redoLine, Conditions.IsBoundByDuty && !IsInACutscene(), stream.Item3));
                                previouslyAddedLines.Add(value + nameToUse);
                                if (_npcVoiceHistoryItems.Count > 10) {
                                    _npcVoiceHistoryItems.RemoveAt(0);
                                }
                            }
                            if (stream.Item1 != null && stream.Item1.Length > 50 && !_plugin.Config.NpcSpeechGenerationDisabled) {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Stream is valid! Download took " + downloadTimer.Elapsed.ToString());
                                }
                                WaveStream wavePlayer = stream.Item1;
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
                                    Vector3 startingPosition = npcObject.Position;
                                    if (npcObject != null && canDoLipSync) {
                                        actorMemory = new ActorMemory();
                                        try {
                                            actorMemory.SetAddress(npcObject.Address);
                                            initialState = actorMemory.CharacterMode;
                                            animationMemory = actorMemory.Animation;
                                            animationMemory.LipsOverride = 630;
                                            animationMemory.LipsOverride = LipSyncTypes[5].Timeline.AnimationId;
                                            if (wavePlayer.TotalTime.Seconds < 2 || !IsInACutscene()) {
                                                lipId = LipSyncTypes[2].Timeline.AnimationId;
                                            } else if (wavePlayer.TotalTime.Seconds < 7) {
                                                lipId = LipSyncTypes[5].Timeline.AnimationId;
                                            } else {
                                                lipId = LipSyncTypes[6].Timeline.AnimationId;
                                            }
                                            //if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                            //    MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                            //}
                                            //MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                            //if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                            //    MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                            //}
                                            //MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                            //task = Task.Run(delegate {
                                            //    Thread.Sleep(500);
                                            //    if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                            //        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                            //    }
                                            //    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                            //    Thread.Sleep((int)wavePlayer.TotalTime.TotalMilliseconds - 1000);
                                            //    if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                            //        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                            //    }
                                            //    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                            //});
                                        } catch {
                                            Plugin.PluginLog.Error("Lip sync has failed, developer please fix!");
                                        }
                                    }
                                    bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse, body);
                                    float pitch = stream.Item2 ? CheckForDefinedPitch(nameToUse) :
                                    CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                                    _chatId = Guid.NewGuid().ToString();
                                    string chatId = _chatId;
                                    bool lipWasSynced = false;
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Attempt to play audio stream.");
                                    }
                                    if (!_blockAudioGeneration) {
                                        _plugin.MediaManager.PlayAudioStream(_currentSpeechObject, wavePlayer, SoundType.NPC,
                                       IsInACutscene() || _plugin.Config.AllowDialogueQueuingOutsideCutscenes, useSmbPitch, pitch, 0,
                                        IsInACutscene() || lowLatencyMode, delegate (object obj, string value) {
                                            if (_hook != null) {
                                                try {
                                                    if (animationMemory != null) {
                                                        if (npcObject != null && canDoLipSync) {
                                                            animationMemory.LipsOverride = 0;
                                                            if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                                                if (IsInACutscene()) {
                                                                    MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), initialState, "Animation Mode Override");
                                                                }
                                                                MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                                            }
                                                        }
                                                    }
                                                    if (_state != null && value == "OK") {
                                                        if ((_plugin.Config.AutoTextAdvance && !ignoreAutoProgress
                                                    && !_plugin.Config.QualityAssuranceMode)) {
                                                            if (_chatId == chatId) {
                                                                _hook.SendAsyncKey(Keys.NumPad0);
                                                            }
                                                        } else {
                                                            if (_plugin.Config.QualityAssuranceMode && !ignoreAutoProgress) {
                                                                _redoLineWindow.OpenReportBox(RedoLineWindow_RedoLineClicked);
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
                                            if (animationMemory != null) {
                                                if (npcObject != null && canDoLipSync) {
                                                    if (e.MaxSampleValues.Length > 0) {
                                                        if (e.MaxSampleValues[0] > 0.2 && Vector3.Distance(startingPosition, npcObject.Position) < 0.1f && _state != null) {
                                                            int seconds = wavePlayer.TotalTime.Milliseconds - wavePlayer.CurrentTime.Milliseconds;
                                                            float percentage = (float)wavePlayer.CurrentTime.Milliseconds / (float)wavePlayer.TotalTime.Milliseconds;
                                                            if (percentage > 0.90f) {
                                                                if (seconds < 2000) {
                                                                    lipId = LipSyncTypes[2].Timeline.AnimationId;
                                                                } else if (wavePlayer.TotalTime.Seconds < 7000) {
                                                                    lipId = LipSyncTypes[5].Timeline.AnimationId;
                                                                } else {
                                                                    lipId = LipSyncTypes[6].Timeline.AnimationId;
                                                                }
                                                            }
                                                            if ((int)MemoryService.Read(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)), typeof(int)) != lipId) {
                                                                if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                                                    if (IsInACutscene()) {
                                                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)),
                                                                            ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                                                    }
                                                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), lipId, "Lipsync");
                                                                    lipWasSynced = true;
                                                                }
                                                            }
                                                        } else {
                                                            if (lipWasSynced) {
                                                                if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                                                    if (IsInACutscene()) {
                                                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)),
                                                                           ActorMemory.CharacterModes.EmoteLoop, "Animation Mode Override");
                                                                    }
                                                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                                                    lipWasSynced = false;
                                                                }
                                                            } else if (Vector3.Distance(startingPosition, npcObject.Position) > 0.01f) {
                                                                if (!Conditions.IsBoundByDuty || IsInACutscene()) {
                                                                    if (IsInACutscene()) {
                                                                        MemoryService.Write(actorMemory.GetAddressOfProperty(nameof(ActorMemory.CharacterModeRaw)),
                                                                           initialState, "Animation Mode Override");
                                                                    }
                                                                    MemoryService.Write(animationMemory.GetAddressOfProperty(nameof(AnimationMemory.LipsOverride)), 0, "Lipsync");
                                                                }
                                                            }
                                                            startingPosition = npcObject.Position;
                                                        }
                                                    }
                                                }
                                            }
                                        }, !Conditions.IsBoundByDuty ? _plugin.Config.NPCSpeechSpeed : 1.2f, stream.Item3 == "Elevenlabs" ? 1 : (stream.Item3 == "XTTS" ? 1.2f : 1.2f));
                                    }
                                    break;
                                } else {
                                    if (_plugin.Config.DebugMode) {
                                        _plugin.Chat.Print("Waveplayer failed, trying again." + downloadTimer.Elapsed.ToString());
                                    }
                                }
                            } else {
                                if (_plugin.Config.DebugMode) {
                                    _plugin.Chat.Print("Stream was null! trying again. " + downloadTimer.Elapsed.ToString());
                                }
                                if (_plugin.Config.NpcSpeechGenerationDisabled) {
                                    break;
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog.Warning(e, e.Message);
                    if (_plugin.Config.DebugMode) {
                        _plugin.Chat.Print(e.Message);
                    }
                }
            }
        }

        public WaveStream GetWavePlayer(string npcName, Stream stream, ReportData reportData) {
            if (_plugin.Config.DebugMode) {
                _plugin.Chat.Print("Stream length " + stream.Length);
            }
            float streamLength = stream.Length;
            WaveStream wavePlayer = null;
            try {
                stream.Position = 0;
                if (_plugin.Config.DebugMode) {
                    _plugin.Chat.Print("Trying MP3");
                }
                if (stream.Length > 0) {
                    var player = new StreamMediaFoundationReader(stream);
                    if (_plugin.Config.DebugMode) {
                        _plugin.Chat.Print("Data length " + player.Length);
                    }
                    if (player.Length > 300) {
                        wavePlayer = player;
                    } else {
                        Plugin.PluginLog.Warning($"Sound for {npcName} is too short.");
                    }
                } else {
                    Plugin.PluginLog.Warning($"Received audio stream for {npcName} is empty.");
                }
            } catch (Exception e) {
                _plugin.Chat.Print(e.Message);
            }
            return wavePlayer;
        }

        private async void NPCText(string name, string message, bool gender,
            byte race, int body, byte tribe, byte eyes, uint objectId, MediaGameObject mediaGameObject, NPCVoiceManager.VoiceModel voiceModel, string note = "", VoiceLinePriority voiceLinePriority = VoiceLinePriority.None) {
            if (VerifyIsEnglish(message) && !message.Contains("You have submitted")) {
                try {
                    string nameToUse = name;
                    MediaGameObject currentSpeechObject = mediaGameObject;
                    _currentSpeechObject = currentSpeechObject;
                    bool foundName = false;
                    string initialConvertedString = PhoneticLexiconCorrection(ConvertRomanNumberals(message));
                    string value = StripPlayerNameFromNPCDialogue(initialConvertedString, _clientState.LocalPlayer.Name.TextValue, ref foundName);
                    ReportData reportData = new ReportData(name, message, objectId, body, gender, race, tribe, eyes, _clientState.TerritoryType, note);
                    string npcData = JsonConvert.SerializeObject(reportData);
                    bool isExtra = false;
                    bool isTerritorySpecific = false;
                    string voice = PickVoiceBasedOnTraits(nameToUse, gender, race, body, ref isExtra, ref isTerritorySpecific);
                    var conditionsForElevenlabs = isExtra || isTerritorySpecific ? VoiceLinePriority.Elevenlabs : VoiceLinePriority.None;
                    var conditionsForOverride = (voiceLinePriority != RoleplayingVoiceCore.VoiceLinePriority.None) ? voiceLinePriority : conditionsForElevenlabs;
                    var conditionsForDatamining = _plugin.Config.NpcSpeechGenerationDisabled ? VoiceLinePriority.Datamining : conditionsForOverride;
                    var stream =
                    await _plugin.NpcVoiceManager.GetCharacterAudio(value, StripPlayerNameFromNPCDialogueArc(message), initialConvertedString, nameToUse, gender, voice
                    , false, voiceModel, npcData, false, false, (Conditions.IsBoundByDuty && !IsInACutscene()), conditionsForDatamining);
                    if (stream.Item1 != null && !_plugin.Config.NpcSpeechGenerationDisabled) {
                        WaveStream wavePlayer = stream.Item1;
                        bool useSmbPitch = CheckIfshouldUseSmbPitch(nameToUse, body);
                        float pitch = stream.Item2 ? CheckForDefinedPitch(nameToUse) :
                         CalculatePitchBasedOnTraits(nameToUse, gender, race, body, 0.09f);
                        _plugin.MediaManager.PlayAudioStream(currentSpeechObject, wavePlayer, SoundType.NPC,
                       Conditions.IsBoundByDuty && IsInACutscene(), useSmbPitch, pitch, 0,
                      IsInACutscene(), null);
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
            string[] symbolBlacklist = new string[] { "¿", "á", "í", "ó", "ú", "ñ", "ü", "Las ", "Los ", "Esta", " que ", " haces ", " tiene ", " las ", " los ",
            " puente ", "Heuso ", "Campamento", "Muéstrale", "evidencia", " un ", "Busca ", " frasco ", " de ", " billis ", "Sepulcro", " sur ", "¡", " cerca", "descubierto",
            "DESTINO", " y ", "puede", " es ", " muchas ", " pero ", "asesino", " agua ", " rota.", "Por ", " tu ", " nombre ", " porque ", " mi ", " querido ", " amigo", " caer ",
            "en la", "Te ", "esperaré", "Muy", "bien", " lugar ", " termine ", "Y ", "en lo", "de luto ","Si "," hecho "," usted ", "nosotros", "también", " haremos "};
            foreach (string symbol in symbolBlacklist) {
                if (message.Contains(symbol)) {
                    return false;
                }
            }
            return _clientState.ClientLanguage == ClientLanguage.English;
        }

        private string FindNPCNameFromMessage(string message) {
            try {
                return NPCVoiceMapping.GetNamelessNPCs()[message.Replace(_clientState.LocalPlayer.Name.TextValue.Split(" ")[0], "_NAME_")];
            } catch {
                return "???";
            }
        }
        private unsafe Dalamud.Game.ClientState.Objects.Types.IGameObject DiscoverNpc(string npcName, string message, ref bool gender, ref byte race, ref int body, ref bool isRetainer) {
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
                        ICharacter character = item as ICharacter;
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
                            ICharacter character = item as ICharacter;
                            if (character != null && character != _clientState.LocalPlayer) {
                                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                                race = character.Customize[(int)CustomizeIndex.Race];
                                body = character.Customize[(int)CustomizeIndex.ModelType];
                                isRetainer = character.ObjectKind == ObjectKind.Retainer;
                                if (body == 0) {
                                    var gameObject = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(item as ICharacter).Address);
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
        private unsafe ICharacter GetCharacterData(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject, ref bool gender, ref byte race, ref int body, ref bool isRetainer) {
            ICharacter character = gameObject as ICharacter;
            if (character != null) {
                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]);
                race = character.Customize[(int)CustomizeIndex.Race];
                body = character.Customize[(int)CustomizeIndex.ModelType];
                isRetainer = character.ObjectKind == ObjectKind.Retainer;
                if (body == 0) {
                    var unsafeReference = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(character as ICharacter).Address);
                    body = unsafeReference->CharacterData.ModelSkeletonId;
                }
                if (_plugin.Config.DebugMode) {
                    _plugin.Chat.Print(character.Name.TextValue + " is model type " + body + ", and race " + race + ".");
                }
            }
            return character;
        }

        private static string StripPlayerNameFromNPCDialogue(string value, string playerName, ref bool foundName) {
            string[] mainCharacterName = playerName.Split(" ");
            foundName = value.Contains(mainCharacterName[0]) || value.Contains(mainCharacterName[1]);
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
                case 8329:
                case 626:
                case 11051:
                case 706:
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
        private static string PhoneticLexiconCorrection(string value) {
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
            phoneticPronunciations.Add(new KeyValuePair<string, string>("YoRHa", "Yourha"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("Lyse", "Leece"));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("─", ","));
            phoneticPronunciations.Add(new KeyValuePair<string, string>("...?", "?"));
            string newValue = value;
            foreach (KeyValuePair<string, string> pronunciation in phoneticPronunciations) {
                newValue = newValue.Replace(pronunciation.Key, pronunciation.Value);
            }
            return newValue;
        }

        public float CalculatePitchBasedOnTraits(string value, bool gender, byte race, int body, float range) {
            string lowered = value.ToLower();
            Random random = new Random(AudioConversionHelper.GetSimpleHash(value));
            bool isDawntrail = _clientState.TerritoryType == 1187 || _clientState.TerritoryType == 1188 ||
                    _clientState.TerritoryType == 1189 || _clientState.TerritoryType == 1185;
            bool isHigherVoiced = lowered.Contains("way") || (body == 4 && !isDawntrail) || (body == 0 && _clientState.TerritoryType == 816)
                || (body == 0 && _clientState.TerritoryType == 152) || (body == 11005) || (body == 278) || (body == 626) || (body == 11051);
            bool isDeepVoiced = false;
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
                    case 8329:
                    case 626:
                    case 706:
                        pitchOffset = (((float)Math.Abs(random.Next(-100, -10)) / 100f) * range);
                        isDeepVoiced = true;
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
                return (isHigherVoiced ? 1.2f : isDeepVoiced ? 0.9f : 1) + pitchOffset;
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

        public string PickVoiceBasedOnTraits(string npcName, bool gender, byte race, int body, ref bool isExtra, ref bool isTerritorySpecific) {
            string[] maleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, false, ref isTerritorySpecific);
            string[] femaleVoices = GetVoicesBasedOnTerritory(_clientState.TerritoryType, true, ref isTerritorySpecific);
            string[] femaleViera = new string[] { "Aet", "Cet", "Uet" };
            foreach (KeyValuePair<string, string> voice in NPCVoiceMapping.GetExtrasVoiceMappings()) {
                if (npcName.Contains(voice.Key)) {
                    isExtra = true;
                    return voice.Value;
                }
            }
            if (npcName.EndsWith("way") || body == 11052) {
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
            if (npcName.ToLower().Contains("siren") || npcName.ToLower().Contains("il ja")) {
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
                    bool isDawntrail = _clientState.TerritoryType == 1187 || _clientState.TerritoryType == 1188 ||
                                        _clientState.TerritoryType == 1189 || _clientState.TerritoryType == 1185;
                    return !gender && (body != 4 || isDawntrail) ?
                    PickVoice(npcName, maleVoices) :
                    PickVoice(npcName, femaleVoices);
                case 8:
                    if (_clientState.TerritoryType == 817) {
                        isTerritorySpecific = true;
                        return gender ? PickVoice(npcName, femaleViera) : PickVoice(npcName, maleVoices);
                    } else {
                        return !gender && body != 4 ?
                        PickVoice(npcName, maleVoices) : PickVoice(npcName, femaleVoices);
                    }
                    break;
            }
            return "";
        }

        public static string[] GetVoicesBasedOnTerritory(uint territory, bool gender, ref bool isTerritorySpecific) {
            string[] maleVoices = new string[] { "Mciv", "Zin", "udm1", "gm1", "Beggarly", "gnat", "ig1", "thord", "vark", "ckeep", "pide", "motanist", "lator", "sail", "lodier" };
            string[] femaleThavnair = new string[] { "tf1", "tf2", "tf3", "tf4" };
            string[] femaleVoices = new string[] { "Maiden", "Dla", "irhm", "ouncil", "igate" };
            string[] maleThavnair = new string[] { "tm1", "tm2", "tm3", "tm4" };
            string[] femaleViera = new string[] { "Aet", "Cet", "Uet" };

            string[] maleVoiceYokTural = { "DTM1", "DTM2", "DTM3", "DTM4", "DTM5", "DTM6", "DTM7", "DTM8", "DTM9", "DTM10" };
            string[] femaleVoiceYokTural = { "DTF1", "DTF2" };

            // We messed up and mixed up the solution 9 voice list with the wrong zone. Keeping the same item count prevents needing to regenerate a bunch of lines.
            string[] maleVoiceXakTural = {"XTM1", "XTM2", "XTM3", "XTM4", "XTM5", "XTM6", "XTM7", "XTM8", "XTM9",
            "XTM10","XTM1", "XTM2", "XTM3", "XTM4", "XTM5",
            "XTM6", "XTM7", "XTM1", "XTM2", "XTM3", "XTM4", "XTM5", "XTM6", "XTM7", "XTM8", "XTM9", "XTM10" };

            string[] femaleVoiceXakTural = {
                "XTF1", "XTF2", "XTF3", "XTF4", "XTF5", "XTF6", "XTF1", "XTF2", "XTF3", "XTF4", "XTF5", "XTF6", "XTF7"
            };

            string[] maleVoiceSolutionNine = {
            "Mciv", "Zin", "udm1", "gm1", "Beggarly", "gnat", "ig1", "thord", "vark",
            "ckeep", "pide", "motanist", "lator", "sail", "lodier",
            "SNM1", "SNM2", "XTM1", "XTM2", "XTM3", "XTM4", "XTM5", "XTM6", "XTM7", "XTM8", "XTM9", "XTM10"};

            string[] femaleVoiceSolutionNine = {
               "SNF1","Maiden", "Dla", "irhm", "ouncil", "igate","XTF1", "XTF2", "XTF3", "XTF4", "XTF5", "XTF6","XTF7"
            };


            string[] maleVoiceTuliyolal = {
            "DTM1", "DTM2", "DTM3", "DTM4", "DTM5", "DTM6", "DTM7", "DTM8", "DTM9", "DTM10",
            "XTM1", "XTM2", "XTM3", "XTM4", "XTM5", "XTM6", "XTM7", "XTM8", "XTM9", "XTM10"};

            string[] femaleVoiceTuliyolal = {
               "DTF1","DTF2", "XTF1", "XTF2", "XTF3", "XTF4", "XTF5", "XTF6","XTF7"
            };

            switch (territory) {
                // Spanish/American - Accents Tuliyolal
                case 1185:
                    isTerritorySpecific = true;
                    return gender ? femaleVoiceTuliyolal : maleVoiceTuliyolal;
                // Spanish Accents - Yok Tural
                case 1187:
                case 1188:
                case 1189:
                    isTerritorySpecific = true;
                    return gender ? femaleVoiceYokTural : maleVoiceYokTural;
                // Western Accents - Xak Tural
                case 1190:
                case 1191:
                    isTerritorySpecific = true;
                    return gender ? femaleVoiceXakTural : maleVoiceXakTural;
                // American/British Accents - Solution Nine
                case 1186:
                    isTerritorySpecific = true;
                    return gender ? femaleVoiceSolutionNine : maleVoiceSolutionNine;
                // Thavnair Accents
                case 963:
                case 957:
                    isTerritorySpecific = true;
                    return gender ? femaleThavnair : maleThavnair;
                default:
                    return gender ? femaleVoices : maleVoices;
            }

        }
        private string PickVoice(string name, string[] choices) {
            Random random = new Random(AudioConversionHelper.GetSimpleHash(name));
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
