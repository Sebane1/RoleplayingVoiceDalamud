using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GameObjectHelper.ThreadSafeDalamudObjectTable;
using Lumina.Excel.Sheets;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using RoleplayingMediaCore;
using RoleplayingVoiceCore;
using RoleplayingVoiceDalamud;
using RoleplayingVoiceDalamud.Catalogue;
using RoleplayingVoiceDalamud.GameObjects;
using RoleplayingVoiceDalamud.NPC;
using RoleplayingVoiceDalamud.ThreadSafeDalamudObjectTable;
using RoleplayingVoiceDalamudWrapper;
using SoundFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VfxEditor.ScdFormat;
using SoundType = RoleplayingMediaCore.SoundType;
namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        public unsafe static Conditions* ConditionValues {
            get {
                return Conditions.Instance();
            }
        }
        #region Sound Management
        private unsafe void framework_Update(IFramework framework) {
            try {
                if (!disposed) {
                    _threadSafeObjectTable.DoProfiling = config.DebugMode;
                    if (_queuePenumbraRefresh) {
                        _queuePenumbraRefresh = false;
                        PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(_penumbraRefreshIndex, RedrawType.Redraw);
                    }
                    if (!_hasBeenInitialized && _threadSafeObjectTable.LocalPlayer != null) {
                        InitializeEverything();
                        _hasBeenInitialized = true;
                    }
                    if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat) {
                        CheckCataloging();
                    }
                    if (pollingTimer.ElapsedMilliseconds > 60 && _threadSafeObjectTable.LocalPlayer != null && _clientState.IsLoggedIn && _hasBeenInitialized && _addonTalkHandler != null) {
                        pollingTimer.Restart();
                        CheckIfDied();
                        switch (performanceLimiter++) {
                            case 0:
                                if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForMovingObjects();
                                }
                                break;
                            case 1:
                                if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForNewDynamicEmoteRequests();
                                }
                                break;
                            case 2:
                                CheckForDownloadCancellation();
                                break;
                            case 3:
                                break;
                            case 4:
                                if (!Conditions.Instance()->BoundByDuty && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForCustomMountingAudio();
                                }
                                break;
                            case 5:
                                if (!Conditions.Instance()->BoundByDuty && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForCustomCombatAudio();
                                }
                                break;
                            case 6:
                                if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForGPose();
                                }
                                break;
                            case 7:
                                if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForCustomEmoteTriggers();
                                }
                                break;
                            case 8:
                                if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat && !_addonTalkHandler.IsInACutscene()) {
                                    CheckForCustomNPCMinion();
                                }
                                break;
                            case 9:
                                if (config != null && _mediaManager != null && _objectTable != null && _gameConfig != null && !disposed) {
                                    CheckVolumeLevels();
                                    CheckForNewRefreshes();
                                }
                                break;
                            case 10:
                                if (_addonTalkHandler != null && !_addonTalkHandler.IsInACutscene()) {
                                    if (_checkVanillaEmoteQueue.Count > 0 && !_queueTimer.IsRunning) {
                                        var item = _checkVanillaEmoteQueue.Dequeue();
                                        DoVanillaAnimation(item.Item1[1], item.Item3);
                                        _queueTimer.Restart();
                                    }
                                    if (_checkAnimationModsQueue.Count > 0 && !_queueTimer.IsRunning) {
                                        var item = _checkAnimationModsQueue.Dequeue();
                                        CheckAnimationMods(item.Item1, item.Item2, item.Item3);
                                        _queueTimer.Restart();
                                    } else if (_queueTimer.ElapsedMilliseconds > 500) {
                                        _queueTimer.Reset();
                                    }
                                }
                                performanceLimiter = 0;
                                break;
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog.Error(e, e.Message);
            }
        }

        private void CheckForCustomNPCMinion() {
            foreach (var gameObject in GetNearestObjects()) {
                var item = gameObject as ICharacter;
                if (item != null) {
                    foreach (var customNPC in config.CustomNpcCharacters) {
                        if (item.Name.TextValue.Contains(customNPC.MinionToReplace)) {
                            try {
                                if (!_lastCharacterDesign.ContainsKey(item.Name.TextValue)) {
                                    _lastCharacterDesign[item.Name.TextValue] = Guid.NewGuid();
                                }
                                if (_lastCharacterDesign[item.Name.TextValue].ToString() != customNPC.NpcGlamourerAppearanceString) {
                                    Guid design = Guid.Parse(customNPC.NpcGlamourerAppearanceString);
                                    ApplyByGuid(design, item);
                                    _lastCharacterDesign[item.Name.TextValue] = design;
                                }
                            } catch {
                                customNPC.NpcGlamourerAppearanceString = "";
                            }
                            unsafe {
                                var minionNPC = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(item as ICharacter).Address;
                                var minionNPCOwner = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_threadSafeObjectTable.LocalPlayer.Address;
                                if (_targetManager.Target != null) {
                                    // Have the minion look at what the player is looking at.
                                    minionNPC->SetTargetId(_targetManager.Target.GameObjectId);
                                    minionNPC->TargetId = _targetManager.Target.GameObjectId;
                                } else {
                                    // Have thw minion look at the player.
                                    minionNPC->SetTargetId(_threadSafeObjectTable.LocalPlayer.GameObjectId);
                                    minionNPC->TargetId = _threadSafeObjectTable.LocalPlayer.GameObjectId;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CheckForCustomEmoteTriggers() {
            bool boundByDuty = false;
            bool inCombat = false;
            unsafe {
                boundByDuty = !Conditions.Instance()->BoundByDuty;
                inCombat = !Conditions.Instance()->InCombat;
            }
            Task.Run(delegate {
                if (config.UsePlayerSync && boundByDuty) {
                    if (_emoteSyncCheck.ElapsedMilliseconds > 10000) {
                        _emoteSyncCheck.Restart();
                        try {
                            foreach (Dalamud.Game.ClientState.Objects.Types.IGameObject item in _objectTable) {
                                if ((item as IGameObject).ObjectKind == ObjectKind.Player && item.Name.TextValue != _threadSafeObjectTable.LocalPlayer.Name.TextValue) {
                                    string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(item.Name.TextValue)).Split(" ");
                                    bool isShoutYell = false;
                                    if (senderStrings.Length > 2) {
                                        string playerSender = senderStrings[0] + " " + senderStrings[2];
                                        if (GetCombinedWhitelist().Contains(playerSender) && !_emoteWatchList.ContainsKey(playerSender)) {
                                            var task = Task.Run(async delegate () {
                                                try {
                                                    Vector3 lastPosition = item.Position;
                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                    while (!disposed && _clientState.IsLoggedIn &&
                                                    startingTerritoryId == _clientState.TerritoryType && boundByDuty) {
                                                        if (boundByDuty && inCombat) {
                                                            Plugin.PluginLog?.Verbose("Checking " + playerSender);
                                                            Plugin.PluginLog?.Verbose("Getting emote.");
                                                            ushort animation = await _roleplayingMediaManager.GetShort(playerSender + "emote");
                                                            if (animation > 0 && animation != ushort.MaxValue - 1) {
                                                                Plugin.PluginLog?.Verbose("Applying Emote.");
                                                                if (animation == ushort.MaxValue) {
                                                                    animation = 0;
                                                                }
                                                                _addonTalkHandler.TriggerEmote((item as ICharacter).Address, animation);
                                                                lastPosition = item.Position;
                                                                _ = Task.Run(() => {
                                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                                    while (true) {
                                                                        Thread.Sleep(500);
                                                                        if ((Vector3.Distance(item.Position, lastPosition) > 0.001f)) {
                                                                            _addonTalkHandler.StopEmote((item as ICharacter).Address);
                                                                            break;
                                                                        }
                                                                    }
                                                                });
                                                                //Task.Run(async () => {
                                                                ushort emoteId = await _roleplayingMediaManager.GetShort(playerSender + "emoteId");
                                                                if (emoteId > 0 && emoteId != ushort.MaxValue - 1) {
                                                                    OnEmote(item as ICharacter, emoteId);
                                                                }
                                                                ////});
                                                                Thread.Sleep(3000);
                                                            }
                                                        } else {
                                                            CleanupEmoteWatchList();
                                                            break;
                                                        }
                                                        Thread.Sleep(1000);
                                                    }
                                                } catch (Exception e) {
                                                    Plugin.PluginLog?.Warning(e, e.Message);
                                                }
                                            });
                                            _emoteWatchList[playerSender] = task;
                                            task = Task.Run(async delegate () {
                                                try {
                                                    Vector3 lastPosition = item.Position;
                                                    int startingTerritoryId = _clientState.TerritoryType;
                                                    while (!disposed && _clientState.IsLoggedIn &&
                                                    startingTerritoryId == _clientState.TerritoryType && boundByDuty) {
                                                        if (boundByDuty && inCombat) {
                                                            Plugin.PluginLog?.Verbose("Checking minion from" + playerSender);
                                                            Plugin.PluginLog?.Verbose("Getting Minion Emote.");
                                                            ushort animation = await _roleplayingMediaManager.GetShort(playerSender + "MinionEmote");
                                                            if (animation > 0 && animation != ushort.MaxValue - 1) {
                                                                Plugin.PluginLog?.Verbose("Applying Minion Emote.");
                                                                if (animation == ushort.MaxValue) {
                                                                    animation = 0;
                                                                }
                                                                unsafe {
                                                                    var companion = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(item as ICharacter).Address;
                                                                    if (companion->CompanionObject != null) {
                                                                        _addonTalkHandler.TriggerEmote((nint)companion->CompanionObject, animation);
                                                                        var gameObject = ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)companion->CompanionObject);
                                                                        lastPosition = gameObject->Position;
                                                                        _ = Task.Run(() => {
                                                                            int startingTerritoryId = _clientState.TerritoryType;
                                                                            while (true) {
                                                                                Thread.Sleep(500);
                                                                                if ((Vector3.Distance(gameObject->Position, lastPosition) > 0.001f)) {
                                                                                    _addonTalkHandler.StopEmote((nint)gameObject);
                                                                                    break;
                                                                                }
                                                                            }
                                                                        });
                                                                    }
                                                                }
                                                                //Task.Run(async () => {
                                                                ushort emoteId = await _roleplayingMediaManager.GetShort(playerSender + "MinionEmoteId");
                                                                if (emoteId > 0 && emoteId != ushort.MaxValue - 1) {
                                                                    OnEmote(item as ICharacter, emoteId);
                                                                }
                                                                //});
                                                                Thread.Sleep(3000);
                                                            }
                                                        } else {
                                                            CleanupEmoteWatchList();
                                                            break;
                                                        }
                                                        Thread.Sleep(1000);
                                                    }
                                                } catch (Exception e) {
                                                    Plugin.PluginLog?.Warning(e, e.Message);
                                                }
                                            });
                                            _emoteWatchList[playerSender + " pet"] = task;
                                        }
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }

                    if (!_emoteSyncCheck.IsRunning) {
                        _emoteSyncCheck.Start();
                    }
                }
            });
        }

        private void CheckIfDied() {
            if (config.VoiceReplacementType == 0) {
                Task.Run(delegate {
                    if (_threadSafeObjectTable.LocalPlayer != null) {
                        if (_threadSafeObjectTable.LocalPlayer.CurrentHp <= 0 && !_playerDied) {
                            if (_mainCharacterVoicePack == null) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                            }
                            PlayVoiceLine(_mainCharacterVoicePack.GetDeath());
                            _playerDied = true;
                        } else if (_threadSafeObjectTable.LocalPlayer.CurrentHp > 0 && _playerDied) {
                            if (_mainCharacterVoicePack == null) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                            }
                            PlayVoiceLine(_mainCharacterVoicePack.GetRevive());
                            _playerDied = false;
                        }
                    }
                });
            }
        }

        private void PlayVoiceLine(string value) {
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] Playing sound: " + Path.GetFileName(value));
            }
            Stopwatch audioPlaybackTimer = Stopwatch.StartNew();
            _mediaManager.PlayMedia(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default, delegate {
                Task.Run(delegate {
                    if (_threadSafeObjectTable.LocalPlayer != null) {
                        _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                    }
                });
            },
            delegate (object sender, StreamVolumeEventArgs e) {
                Task.Run(delegate {
                    if (_threadSafeObjectTable.LocalPlayer != null) {
                        if (e.MaxSampleValues.Length > 0) {
                            if (e.MaxSampleValues[0] > 0.2) {
                                _addonTalkHandler.TriggerLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter, 2);
                            } else {
                                _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                            }
                        }
                    }
                });
            });
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] " + Path.GetFileName(value) + " took " + audioPlaybackTimer.ElapsedMilliseconds + " milliseconds to load.");
            }
            if (!_muteTimer.IsRunning) {
                if (Filter != null) {
                    Filter.Muted = true;
                }
            }
            if (config.DebugMode) {
                Plugin.PluginLog.Debug("Battle Voice Muted");
            }
            _muteTimer.Restart();
        }
        private void CheckForGPose() {
            if (_clientState != null && _gameGui != null) {
                if (_threadSafeObjectTable.LocalPlayer != null) {
                    if (_clientState.IsGPosing && _gameGui.GameUiHidden) {
                        if (!_gposeWindow.IsOpen) {
                            _gposeWindow.RespectCloseHotkey = false;
                            _gposeWindow.IsOpen = true;
                            _gposePhotoTakerWindow.IsOpen = true;
                        }
                    } else if (_gposeWindow.IsOpen) {
                        _gposeWindow.IsOpen = false;
                        _gposePhotoTakerWindow.IsOpen = false;
                    }
                }
            }
        }

        private unsafe void CheckForCustomCombatAudio() {
            if (Conditions.Instance()->InCombat && !Conditions.Instance()->Mounted && !Conditions.Instance()->BoundByDuty) {
                if (!_combatOccured) {
                    Task.Run(delegate () {
                        if (_threadSafeObjectTable.LocalPlayer != null) {
                            _combatOccured = true;
                            string voice = config.CharacterVoicePacks[_threadSafeObjectTable.LocalPlayer.Name.TextValue];
                            string path = config.CacheFolder + @"\VoicePack\" + voice;
                            string staging = config.CacheFolder + @"\Staging\" + _threadSafeObjectTable.LocalPlayer.Name.TextValue;
                            if (_mainCharacterVoicePack == null) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                            }
                            bool isVoicedEmote = false;
                            string value = _mainCharacterVoicePack.GetMisc("Battle Song");
                            if (!string.IsNullOrEmpty(value)) {
                                //if (config.UsePlayerSync) {
                                //    Task.Run(async () => {
                                //        bool success = await _roleplayingMediaManager.SendZip(_threadSafeObjectTable.LocalPlayer.Name.TextValue, staging);
                                //    });
                                //}
                                _mediaManager.PlayMedia(_playerObject, value, SoundType.LoopUntilStopped, false, 0);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _combatMusicWasPlayed = true;
                            }
                        }
                    });
                }
            } else {
                if (_combatOccured) {
                    Task.Run(delegate () {
                        _combatOccured = false;
                        if (_combatMusicWasPlayed) {
                            Task.Run(async () => {
                                Thread.Sleep(6000);
                                _mediaManager.StopAudio(_playerObject);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _combatMusicWasPlayed = false;
                            });
                        }
                    });
                }
            }
        }

        private unsafe void CheckForCustomMountingAudio() {
            if (!Conditions.Instance()->BetweenAreas && !Conditions.Instance()->BetweenAreas51 && _threadSafeObjectTable.LocalPlayer != null && _recentCFPop != 2) {
                if (Conditions.Instance()->Mounted) {
                    if (!_mountingOccured) {
                        Task.Run(delegate () {
                            if (_threadSafeObjectTable.LocalPlayer != null) {
                                if (config.CharacterVoicePacks.ContainsKey(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                                    string voice = config.CharacterVoicePacks[_threadSafeObjectTable.LocalPlayer.Name.TextValue];
                                    string path = config.CacheFolder + @"\VoicePack\" + voice;
                                    string staging = config.CacheFolder + @"\Staging\" + _threadSafeObjectTable.LocalPlayer.Name.TextValue;
                                    if (_mainCharacterVoicePack == null) {
                                        _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                                    }
                                    bool isVoicedEmote = false;
                                    var characterReference = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_threadSafeObjectTable.LocalPlayer.Address;
                                    var mountId = characterReference->Mount.MountId;
                                    var mount = DataManager.GetExcelSheet<Mount>(ClientLanguage.English).GetRow(mountId);
                                    string value = _mainCharacterVoicePack.GetMisc(mount.Singular.ToString());
                                    if (!string.IsNullOrEmpty(value)) {
                                        //if (config.UsePlayerSync) {
                                        //    Task.Run(async () => {
                                        //        bool success = await _roleplayingMediaManager.SendZip(_threadSafeObjectTable.LocalPlayer.Name.TextValue, staging);
                                        //    });
                                        //}
                                        _mediaManager.PlayMedia(_playerObject, value, SoundType.LoopUntilStopped, false, 0);
                                        try {
                                            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
                                        } catch (Exception e) {
                                            Plugin.PluginLog?.Warning(e, e.Message);
                                        }
                                        _mountMusicWasPlayed = true;
                                    }
                                }
                                _mountingOccured = true;
                            }
                        });
                    }
                } else {
                    if (_mountingOccured) {
                        Task.Run(delegate () {
                            _mountingOccured = false;
                            if (_mountMusicWasPlayed) {
                                _lastMountingMessage = null;
                                _mediaManager.StopAudio(_playerObject);
                                try {
                                    _gameConfig.Set(SystemConfigOption.IsSndBgm, false);
                                } catch (Exception e) {
                                    Plugin.PluginLog?.Warning(e, e.Message);
                                }
                                _mountMusicWasPlayed = false;
                            }
                        });
                    }
                }
            }
        }

        private void Chat_ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
            var storedSender = sender;
            var storedMessage = message;
            Task.Run(delegate () {
                if (!disposed) {
                    CheckDependancies();
                    string playerName = "";
                    try {
                        foreach (var item in storedSender.Payloads) {
                            PlayerPayload player = item as PlayerPayload;
                            TextPayload text = item as TextPayload;
                            if (player != null) {
                                string possiblePlayerName = RemoveSpecialSymbols(player.PlayerName).Trim();
                                if (!string.IsNullOrEmpty(possiblePlayerName)) {
                                    if (char.IsLower(possiblePlayerName[1])) {
                                        playerName = player.PlayerName;
                                        break;
                                    }
                                }
                            }
                            if (text != null) {
                                string possiblePlayerName = RemoveSpecialSymbols(text.Text).Trim();
                                if (!string.IsNullOrEmpty(possiblePlayerName)) {
                                    if (char.IsLower(possiblePlayerName[1])) {
                                        playerName = text.Text;
                                        break;
                                    }
                                }
                            }
                        }
#if DEBUG
                        //_chat?.Print(playerName);
#endif
                        //playerName = sender.Payloads[0].ToString().Split(" ")[3].Trim() + " " + sender.Payloads[0].ToString().Split(" ")[4].Trim(',');
                        //if (playerName.ToLower().Contains("PlayerName")) {
                        //    playerName = sender.Payloads[0].ToString().Split(" ")[3].Trim();
                        //}
                    } catch {

                    }
                    if (_roleplayingMediaManager != null) {
                        switch (type) {
                            case XivChatType.Say:
                            case XivChatType.Shout:
                            case XivChatType.Yell:
                            case XivChatType.CustomEmote:
                            case XivChatType.Party:
                            case XivChatType.CrossParty:
                            case XivChatType.TellIncoming:
                            case XivChatType.TellOutgoing:
                            case XivChatType.FreeCompany:
                            case XivChatType.Alliance:
                            case XivChatType.PvPTeam:
                                if ((type != XivChatType.Shout && type != XivChatType.Yell) || IsResidential()) {
                                    ChatText(playerName, storedMessage, type, timestamp);
                                }
                                break;
                            case XivChatType.NPCDialogue:
                            case XivChatType.NPCDialogueAnnouncements:
                                //NPCText(playerName, message, type, senderId);
                                break;
                            case (XivChatType)2729:
                            case (XivChatType)2091:
                            case (XivChatType)2234:
                            case (XivChatType)2730:
                            case (XivChatType)2219:
                            case (XivChatType)2859:
                            case (XivChatType)2731:
                            case (XivChatType)2106:
                            case (XivChatType)10409:
                            case (XivChatType)8235:
                            case (XivChatType)9001:
                            case (XivChatType)4139:
                                BattleText(playerName, storedMessage, type);
                                break;
                        }
                    } else {
                        InitialzeManager();
                    }
                }
            });
        }

        private void ChatText(string sender, SeString message, XivChatType type, int timeStamp, bool isCustomNPC = false) {
            try {
                if (_threadSafeObjectTable.LocalPlayer != null) {
                    if (sender.Contains(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                        if (config.PerformEmotesBasedOnWrittenText) {
                            if (type == XivChatType.CustomEmote ||
                                message.TextValue.Split("\"").Length > 1 ||
                                message.TextValue.Contains("*")) {
                                Task.Run(() => EmoteReaction(message.TextValue));
                            }
                        }
                        //if (!Conditions.Instance()->BoundByDuty) {
                        if (true) {
                            Task.Run(async () => {
                                try {
                                    if (_playerCount is 1 || (type == XivChatType.Party && timeStamp == -1)) {
                                        foreach (var gameObject in GetNearestObjects()) {
                                            ICharacter character = gameObject as ICharacter;
                                            if (character != null) {
                                                if (character.ObjectKind == ObjectKind.Companion) {
                                                    if (!_npcConversationManagers.ContainsKey(character.Name.TextValue)) {
                                                        CustomNpcCharacter npcData = null;
                                                        npcData = GetCustomNPCObject(character, true);
                                                        if (npcData != null) {
                                                            _npcConversationManagers[character.Name.TextValue] = new KeyValuePair<CustomNpcCharacter, NPCConversationManager>(npcData,
                                                            new NPCConversationManager(npcData.NpcName, config.CacheFolder + @"\NPCMemories", this, character));
                                                        }
                                                    }
                                                    if (_npcConversationManagers.ContainsKey(character.Name.TextValue)) {
                                                        string formattedText = message.TextValue;
                                                        if (type != XivChatType.CustomEmote && formattedText.Split('"').Length < 2) {
                                                            formattedText = @"""" + message + @"""";
                                                        }

                                                        var npcConversationManager = _npcConversationManagers[character.Name.TextValue];
                                                        string aiResponse = await npcConversationManager.Value
                                                        .SendMessage(_threadSafeObjectTable.LocalPlayer, character, npcConversationManager.Key.NpcName, npcConversationManager.Key.NPCGreeting,
                                                        formattedText, GetWrittenGameState(
                                                            _threadSafeObjectTable.LocalPlayer.Name.TextValue.Split(" ")[0], character.Name.TextValue.Split(" ")[0]),
                                                        npcConversationManager.Key.NpcPersonality);

                                                        _aiMessageQueue.Enqueue(new Tuple<ICharacter, string, XivChatType>(character, aiResponse, type == XivChatType.Party ? XivChatType.Party : XivChatType.CustomEmote));
                                                        //ChatText(character.Name.TextValue, aiResponse, XivChatType.Say, true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                } catch (Exception e) {
                                    Plugin.PluginLog.Warning(e, e.Message);
                                }
                            });
                        }
                        //}
                        string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(
                        _threadSafeObjectTable.LocalPlayer.Name.TextValue)).Split(" ");
                        string playerSender = senderStrings.Length == 2 ?
                            (senderStrings[0] + " " + senderStrings[1]) :
                            (senderStrings[0] + " " + senderStrings[2]);
                        string playerMessage = message.TextValue;
                        ICharacter player = (ICharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                        PluginLog.Verbose("Found " + player.Name.TextValue + " for speech detection.");
                        unsafe {
                            if (config.TwitchStreamTriggersIfShouter && !Conditions.Instance()->BoundByDuty) {
                                TwitchChatCheck(message, type, player, playerSender);
                            }
                        }
                        if (config.AiVoiceActive) {
                            bool lipWasSynced = true;
                            Task.Run(async () => {
                                string value = await GetPlayerVoice(playerSender, playerMessage, type);
                                _mediaManager.PlayMedia(_playerObject, value, SoundType.MainPlayerTts, false, 0, default, delegate {
                                    if (_addonTalkHandler != null) {
                                        if (_threadSafeObjectTable.LocalPlayer != null) {
                                            _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                                        }
                                    }
                                }, delegate (object sender, StreamVolumeEventArgs e) {
                                    Task.Run(delegate {
                                        if (e.MaxSampleValues.Length > 0) {
                                            if (e.MaxSampleValues[0] > 0.2) {
                                                _addonTalkHandler.TriggerLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter, 5);
                                                lipWasSynced = true;
                                            } else {
                                                _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                                            }
                                        }
                                    });
                                });
                            });
                        }
                        CheckForChatSoundEffectLocal(message);
                    } else {
                        string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(sender)).Split(" ");
                        bool isShoutYell = false;
                        isShoutYell = type == XivChatType.Shout
                        || type == XivChatType.Yell || type == XivChatType.Alliance
                        || type == XivChatType.FreeCompany || type == XivChatType.TellIncoming
                        || type == XivChatType.PvPTeam || type == XivChatType.NoviceNetwork;
                        if (senderStrings.Length > 0) {
                            string playerSender = senderStrings[0] + (senderStrings.Length > 2 ? " " + senderStrings[2] : "");
                            string playerMessage = message.TextValue;
                            bool audioFocus = false;
                            if (_threadSafeObjectTable.LocalPlayer.TargetObject != null) {
                                if (_threadSafeObjectTable.LocalPlayer.TargetObject.ObjectKind ==
                                    ObjectKind.Player) {
                                    audioFocus = _threadSafeObjectTable.LocalPlayer.TargetObject.Name.TextValue == sender
                                        || type == XivChatType.Party
                                        || type == XivChatType.CrossParty || isShoutYell;
                                }
                            } else {
                                audioFocus = true;
                            }
                            if (_objectTable != null) {
                                PluginLog.Verbose("Object table was found");
                                ICharacter player = (ICharacter)_objectTable.FirstOrDefault(x => {
                                    var npcObject = GetCustomNPCObject(x as ICharacter);
                                    if (npcObject != null) {
                                        PluginLog.Verbose("Object for speech was found #1.");
                                        return RemoveSpecialSymbols(npcObject.NpcName) == playerSender;
                                    }
                                    if (x != null) {
                                        PluginLog.Verbose("Object for speech was found #2.");
                                        return RemoveSpecialSymbols(x.Name.TextValue) == playerSender;
                                    }
                                    PluginLog.Verbose("No object for speech was found.");
                                    return false;
                                });
                                var playerMediaReference = player != null && !isShoutYell && !audioFocus ? new MediaGameObject(player.ToThreadSafeObject()) : new MediaGameObject(playerSender, _threadSafeObjectTable.LocalPlayer.Position);
                                bool narratePlayer = false;
                                if (config.UsePlayerSync) {
                                    if (GetCombinedWhitelist().Contains(playerSender)) {
                                        Task.Run(async () => {
                                            string value = await _roleplayingMediaManager.
                                            GetSound(GetCustomNPCObject(player).NpcName, playerMessage, audioFocus ?
                                            config.OtherCharacterVolume : config.UnfocusedCharacterVolume,
                                            _threadSafeObjectTable.LocalPlayer.Position, isShoutYell, @"\Incoming\");
                                            bool lipWasSynced = false; ;
                                            _mediaManager.PlayMedia(playerMediaReference, value, SoundType.OtherPlayerTts, (isShoutYell || audioFocus), 0, default, delegate {
                                                Task.Run(delegate {
                                                    _addonTalkHandler.StopLipSync(player);
                                                });
                                            },
                                            delegate (object sender, StreamVolumeEventArgs e) {
                                                Task.Run(delegate {
                                                    if (e.MaxSampleValues.Length > 0) {
                                                        if (e.MaxSampleValues[0] > 0.2) {
                                                            _addonTalkHandler.TriggerLipSync(player, 2);
                                                            lipWasSynced = true;
                                                        } else {
                                                            _addonTalkHandler.StopLipSync(player);
                                                        }
                                                    }
                                                });
                                            });
                                        });
                                        CheckForChatSoundEffectOtherPlayer(sender, player, message);
                                    } else {
                                        narratePlayer = config.LocalVoiceForNonWhitelistedPlayers;
                                    }
                                } else {
                                    narratePlayer = config.LocalVoiceForNonWhitelistedPlayers;
                                }
                                if (narratePlayer) {
                                    Task.Run(async () => {
                                        var list = await _roleplayingMediaManager.GetVoiceListMicrosoftNarrator();
                                        var genderSortedLists = await _roleplayingMediaManager.GetGenderSortedVoiceListsMicrosoftNarrator();
                                        Random random = new Random(AudioConversionHelper.GetSimpleHash(playerSender));
                                        //if (list.Contains("Microsoft Brian Online")) {
                                        _roleplayingMediaManager.SetVoiceMicrosoftNarrator(
                                       PenumbraAndGlamourerHelperFunctions.GetGender(player) != 1 ?
                                       genderSortedLists.Item1[random.Next(list.Contains("Microsoft Brian Online") ? 1 : 0, genderSortedLists.Item1.Length)] :
                                       genderSortedLists.Item2[random.Next(list.Contains("Microsoft Brian Online") ? 1 : 0, genderSortedLists.Item2.Length)]);

                                        var value = await _roleplayingMediaManager.DoVoiceMicrosoftNarrator(playerSender, playerMessage,
                                        type == XivChatType.CustomEmote,
                                        config.PlayerCharacterVolume,
                                        _threadSafeObjectTable.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                                        bool lipWasSynced = false;

                                        _mediaManager.PlayMedia(playerMediaReference, value, SoundType.OtherPlayerTts, (isShoutYell || audioFocus), 0, default, delegate {
                                            Task.Run(delegate {
                                                _addonTalkHandler.StopLipSync(player);
                                            });
                                        },
                                        delegate (object sender, StreamVolumeEventArgs e) {
                                            Task.Run(delegate {
                                                if (e.MaxSampleValues.Length > 0) {
                                                    if (e.MaxSampleValues[0] > 0.2) {
                                                        _addonTalkHandler.TriggerLipSync(player, 2);
                                                        lipWasSynced = true;
                                                    } else {
                                                        _addonTalkHandler.StopLipSync(player);
                                                    }
                                                }
                                            });
                                        });
                                    });
                                }
                                TwitchChatCheck(message, type, player, playerSender);
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }

        private CustomNpcCharacter GetCustomNPCObject(ICharacter character, bool returnNullIfNoFind = false) {
            if (character != null) {
                if (config.CustomNpcCharacters != null) {
                    foreach (var customNPC in config.CustomNpcCharacters) {
                        if (!string.IsNullOrEmpty(customNPC.MinionToReplace) && character.Name.TextValue.Contains(customNPC.MinionToReplace)) {
                            return customNPC;
                        }
                    }
                }
                if (!returnNullIfNoFind) {
                    return new CustomNpcCharacter() { NpcName = character.Name.TextValue };
                }
            }
            return null;
        }
        private async Task<string> GetPlayerVoice(string playerSender, string playerMessage, XivChatType type) {
            switch (config.PlayerVoiceEngine) {
                case 0:
                    return await _roleplayingMediaManager.DoVoiceElevenlabs(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _threadSafeObjectTable.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                case 1:
                    return await _roleplayingMediaManager.DoVoiceXTTS(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _threadSafeObjectTable.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync, Window.XttsLanguageComboBox.Contents[config.XTTSLanguage]);
                case 2:
                    _roleplayingMediaManager.SetVoiceMicrosoftNarrator(config.Characters[_threadSafeObjectTable.LocalPlayer.Name.TextValue]);
                    return await _roleplayingMediaManager.DoVoiceMicrosoftNarrator(playerSender, playerMessage,
                    type == XivChatType.CustomEmote,
                    config.PlayerCharacterVolume,
                    _threadSafeObjectTable.LocalPlayer.Position, config.UseAggressiveSplicing, config.UsePlayerSync);
                default:
                    return "";
            }

        }

        private unsafe string GetWrittenGameState(string playerName, string npcName) {
            string locationValue = HousingManager.Instance()->IsInside() ? "inside a house" : "outside";
            string value = "";
            //string value = $"{playerName} and npc are currently {locationValue}. The current zone is "
            //    + DataManager.GetExcelSheet<TerritoryType>().GetRow(_clientState.TerritoryType).PlaceName.Value.Name.ToString() +
            //    ". The date and time is " + DateTime.Now.ToString("MM/dd/yyyy h:mm tt") + ".";
            return value;
        }

        public void TwitchChatCheck(SeString message, XivChatType type, ICharacter player, string name) {
            if (type == XivChatType.Yell || type == XivChatType.Shout || type == XivChatType.TellIncoming) {
                if (config.TuneIntoTwitchStreams && IsResidential()) {
                    if (!_streamSetCooldown.IsRunning || _streamSetCooldown.ElapsedMilliseconds > 10000) {
                        var strings = message.TextValue.Split(' ');
                        foreach (string value in strings) {
                            if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                _lastStreamObject = player != null ?
                                    player != null ?
                                    new MediaGameObject(player.TargetObject.ToThreadSafeObject()) :
                                    new MediaGameObject(player.ToThreadSafeObject()) :
                                    new MediaGameObject(name, _threadSafeObjectTable.LocalPlayer.Position);
                                var audioGameObject = _lastStreamObject;
                                if (_mediaManager.IsAllowedToStartStream(audioGameObject)) {
                                    TuneIntoStream(value
                                        .Trim('(').Trim(')')
                                        .Trim('[').Trim(']')
                                        .Trim('!').Trim('@'), audioGameObject, false);
                                }
                                break;
                            }
                        }
                    }
                } else {
                    if (config.TuneIntoTwitchStreamPrompt) {
                        var strings = message.TextValue.Split(' ');
                        foreach (string value in strings) {
                            if (value.Contains("twitch.tv") && lastStreamURL != value) {
                                potentialStream = value;
                                lastStreamURL = value;
                                string cleanedURL = RemoveSpecialSymbols(value);
                                string streamer = cleanedURL.Replace(@"https://", null).Replace(@"www.", null).Replace("twitch.tv/", null);
                                _chat?.Print(streamer + " is hosting a stream in this zone! Wanna tune in? You can do \"/artemis listen\"");
                            }
                        }
                    }
                }
            }
        }
        private async void CheckForChatSoundEffectOtherPlayer(string sender, ICharacter player, SeString message) {
            if (message.TextValue.Contains("<") && message.TextValue.Contains(">")) {
                string[] tokenArray = message.TextValue.Replace(">", "<").Split('<');
                string soundTrigger = tokenArray[1];
                string path = config.CacheFolder + @"\VoicePack\Others";
                string hash = RoleplayingMediaManager.Shai1Hash(sender);
                string clipPath = path + @"\" + hash;
                string playerSender = sender;
                int index = GetNumberFromString(soundTrigger);
                CharacterVoicePack characterVoicePack = null;
                if (!_characterVoicePacks.ContainsKey(playerSender)) {
                    characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                } else {
                    characterVoicePack = _characterVoicePacks[playerSender];
                }
                string value = index == -1 ? characterVoicePack.GetMisc(soundTrigger) : characterVoicePack.GetMiscSpecific(soundTrigger, index);
                try {
                    Directory.CreateDirectory(path);
                } catch {
                    _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
                }

                bool isDownloadingZip = false;
                if (!Path.Exists(clipPath) || string.IsNullOrEmpty(value)) {
                    if (Path.Exists(clipPath)) {
                        RemoveFiles(clipPath);
                    }
                    if (_characterVoicePacks.ContainsKey(playerSender)) {
                        _characterVoicePacks.Remove(playerSender);
                    }
                    isDownloadingZip = true;
                    _maxDownloadLengthTimer.Restart();
                    await Task.Run(async () => {
                        try {
                            string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                        } catch {
                        }
                        isDownloadingZip = false;
                    });
                }
                if (string.IsNullOrEmpty(value)) {
                    if (!_characterVoicePacks.ContainsKey(playerSender)) {
                        characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                    } else {
                        characterVoicePack = _characterVoicePacks[playerSender];
                    }
                    value = characterVoicePack.GetMisc(soundTrigger);
                }
                if (!string.IsNullOrEmpty(value)) {
                    _mediaManager.PlayMedia(new MediaGameObject(player.ToThreadSafeObject()), value, SoundType.ChatSound, false);
                }
            }
        }

        private void CheckForChatSoundEffectLocal(SeString message) {
            if (message.TextValue.Contains("<") && message.TextValue.Contains(">")) {
                string[] tokenArray = message.TextValue.Replace(">", "<").Split('<');
                string soundTrigger = tokenArray[1];
                int index = GetNumberFromString(soundTrigger);
                if (_mainCharacterVoicePack == null) {
                    _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                }
                string value = index == -1 ? _mainCharacterVoicePack.GetMisc(soundTrigger) : _mainCharacterVoicePack.GetMiscSpecific(soundTrigger, index);
                if (!string.IsNullOrEmpty(value)) {
                    _mediaManager.PlayMedia(new MediaGameObject(_threadSafeObjectTable.LocalPlayer), value, SoundType.ChatSound, false);
                }
            }
        }

        public int GetNumberFromString(string value) {
            try {
                return int.Parse(value.Split('.')[1]) - 1;
            } catch {
                return -1;
            }
        }

        private void _toast_ErrorToast(ref SeString message, ref bool isHandled) {
            if (_clientState.IsLoggedIn) {
                if (config.CharacterVoicePacks.ContainsKey(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                    if (!_cooldown.IsRunning || _cooldown.ElapsedMilliseconds > 9000) {
                        if (_mainCharacterVoicePack == null) {
                            _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                        }
                        string value = _mainCharacterVoicePack.GetMisc(message.TextValue);
                        if (!string.IsNullOrEmpty(value)) {
                            _mediaManager.PlayMedia(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default, delegate {
                                _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                            },
                            delegate (object sender, StreamVolumeEventArgs e) {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter, 2);
                                    } else {
                                        _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer as ICharacter);
                                    }
                                }
                            });
                        }
                    }
                    _cooldown.Restart();
                }
            }
        }
        private void _filter_OnSoundIntercepted(object sender, InterceptedSound e) {
            if (config.MoveSCDBasedModsToPerformanceSlider) {
                if (_scdReplacements.ContainsKey(e.SoundPath)) {
                    if (!e.SoundPath.Contains("vo_emote") && !e.SoundPath.Contains("vo_battle")) {
                        Plugin.PluginLog.Debug("Sound Mod Intercepted");
                        int i = 0;
                        try {
                            _scdProcessingDelayTimer = new Stopwatch();
                            _scdProcessingDelayTimer.Start();
                            _mediaManager.StopAudio(new MediaGameObject(_threadSafeObjectTable.LocalPlayer));
                            if (_lastPlayerToEmote != null) {
                                _mediaManager.StopAudio(_lastPlayerToEmote);
                            }
                            Task.Run(async () => {
                                try {
                                    ScdFile scdFile = null;
                                    string soundPath = "";
                                    soundPath = e.SoundPath;
                                    scdFile = GetScdFile(e.SoundPath);
                                    QueueSCDTrigger(scdFile);
                                    CheckForValidSCD(_lastPlayerToEmote, _lastEmoteUsed, stagingPath, soundPath, true);
                                } catch (Exception ex) {
                                    Plugin.PluginLog?.Warning(ex, ex.Message);
                                }
                            });
                        } catch (Exception ex) {
                            Plugin.PluginLog?.Warning(ex, ex.Message);
                        }
                    }
                }
            }
        }
        public void OnEmote(ICharacter instigator, ushort emoteId) {
            if (!disposed) {
                _lastPlayerToEmote = new MediaGameObject(instigator.ToThreadSafeObject());
                if (instigator.Name.TextValue == _threadSafeObjectTable.LocalPlayer.Name.TextValue || instigator.OwnerId == _threadSafeObjectTable.LocalPlayer.GameObjectId) {
                    if (!_wasDoingFakeEmote) {
                        _didRealEmote = true;
                    }
                    if (config.VoicePackIsActive) {
                        SendingEmote(instigator, emoteId);
                    }
                    if (!_blockDataRefreshes && !_isAlreadyRunningEmote && _threadSafeObjectTable.LocalPlayer.Name.TextValue == instigator.Name.TextValue) {
                        _lastEmoteAnimationUsed = GetEmoteData(emoteId);
                    }
                    _timeSinceLastEmoteDone.Restart();
                    _lastEmoteTriggered = emoteId;
                } else {
                    Task.Run(() => {
                        ReceivingEmote(instigator, emoteId);
                    });
                    if (_timeSinceLastEmoteDone.ElapsedMilliseconds < 400 && _timeSinceLastEmoteDone.IsRunning && _timeSinceLastEmoteDone.ElapsedMilliseconds > 20) {
                        if (instigator != null) {
                            if (Vector3.Distance(instigator.Position, _threadSafeObjectTable.LocalPlayer.Position) < 3) {
                                _fastMessageQueue.Enqueue(GetEmoteCommand(_lastEmoteTriggered).ToLower());
                                _timeSinceLastEmoteDone.Stop();
                                _timeSinceLastEmoteDone.Reset();
                                _lastEmoteTriggered = 0;
                            }
                        }
                    }
                }
            }
        }
        private void CheckForNewRefreshes() {
            Task.Run(delegate {
                if (_redrawCooldown.ElapsedMilliseconds > 100) {
                    if (temporaryWhitelistQueue.Count < redrawObjectCount - 1) {
                        foreach (var item in temporaryWhitelistQueue) {
                            temporaryWhitelist.Add(item);
                        }
                        temporaryWhitelistQueue.Clear();
                    }
                    _redrawCooldown.Stop();
                    _redrawCooldown.Reset();
                }
            });
        }
        private void CheckForMovingObjects() {
            if (!_checkingMovementInProgress) {
                Task.Run(delegate {
                    _checkingMovementInProgress = true;
                    try {
                        foreach (IGameObject gameObject in _objectTable) {
                            if (gameObject.ObjectKind == ObjectKind.Player) {
                                string cleanedName = CleanSenderName(gameObject.Name.TextValue);
                                if (!string.IsNullOrEmpty(cleanedName)) {
                                    if (gameObjectPositions.ContainsKey(cleanedName)) {
                                        var positionData = gameObjectPositions[cleanedName];
                                        if (Vector3.Distance(positionData.LastPosition, gameObject.Position) > 0.01f ||
                                            positionData.LastRotation.Y != gameObject.Rotation) {
                                            if (!positionData.IsMoving) {
                                                ObjectIsMoving(cleanedName, gameObject);
                                                positionData.IsMoving = true;
                                            }
                                        } else {
                                            positionData.IsMoving = false;
                                        }
                                        positionData.LastPosition = gameObject.Position;
                                        positionData.LastRotation = new Vector3(0, gameObject.Rotation, 0);
                                    } else {
                                        gameObjectPositions[cleanedName] = new MovingObject(gameObject.Position, new Vector3(0, gameObject.Rotation, 0), false);
                                    }
                                    if (cleanedName == CleanSenderName(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                                        MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, _mainCharacterVoicePack, _roleplayingMediaManager, _mediaManager);
                                    } else {
                                        unsafe {
                                            if (Vector3.Distance(_threadSafeObjectTable.LocalPlayer.Position, gameObject.Position) < 5) {
                                                if (!Conditions.Instance()->InCombat) {
                                                    CheckOtherPlayerBoneMovement(cleanedName, gameObject);
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                    _checkingMovementInProgress = false;
                }
            );
            }
        }
        #region Audio Volume
        private void CheckVolumeLevels() {
            uint voiceVolume = 0;
            uint masterVolume = 0;
            uint soundEffectVolume = 0;
            uint soundMicPos = 0;
            try {
                _mediaManager.AudioPlayerType = (AudioOutputType)config.AudioOutputType;
                _mediaManager.SpatialAudioAccuracy = config.SpatialAudioAccuracy;
                _mediaManager.LowPerformanceMode = config.LowPerformanceMode;
                _mediaManager.IgnoreSpatialAudioForTTS = config.IgnoreSpatialAudioForTTS;
                if (_gameConfig.TryGet(SystemConfigOption.SoundVoice, out voiceVolume)) {
                    if (_gameConfig.TryGet(SystemConfigOption.SoundMaster, out masterVolume)) {
                        if (_gameConfig.TryGet(SystemConfigOption.SoundMicpos, out soundMicPos))
                            _mediaManager.MainPlayerVolume = config.PlayerCharacterVolume *
                            ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.OtherPlayerVolume = config.OtherCharacterVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.UnfocusedPlayerVolume = config.UnfocusedCharacterVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.NpcVolume = config.NpcVolume *
                        ((float)voiceVolume / 100f) * ((float)masterVolume / 100f);
                        _mediaManager.CameraAndPlayerPositionSlider = (float)soundMicPos / 100f;
                        if (_gameConfig.TryGet(SystemConfigOption.SoundPerform, out soundEffectVolume)) {
                            _mediaManager.SFXVolume = config.LoopingSFXVolume *
                            ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                            _mediaManager.LiveStreamVolume = config.LivestreamVolume *
                            ((float)soundEffectVolume / 100f) * ((float)masterVolume / 100f);
                        }
                    }
                    if (_muteTimer.ElapsedMilliseconds > _muteLength) {
                        if (Filter != null) {
                            lock (Filter) {
                                Filter.Muted = voiceMuted = false;
                                RefreshPlayerVoiceMuted();
                                _muteTimer.Stop();
                                _muteTimer.Reset();
                                Plugin.PluginLog.Debug("Voice Mute End");
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }
        public void MuteVoiceCheck(int length = 6000) {
            if (!_muteTimer.IsRunning) {
                if (Filter != null) {
                    Filter.Muted = voiceMuted = true;
                }
                RefreshPlayerVoiceMuted();
                Plugin.PluginLog.Debug("Mute Triggered");
            }
            _muteTimer.Restart();
            _muteLength = length;
        }
        private void RefreshPlayerVoiceMuted() {
            if (_gameConfig != null) {
                try {
                    if (voiceMuted) {
                        _gameConfig.Set(SystemConfigOption.IsSndVoice, true);
                    } else {
                        _gameConfig.Set(SystemConfigOption.IsSndVoice, false);
                    }
                } catch (Exception e) {
                    Plugin.PluginLog?.Warning(e.Message);
                }
            }
        }
        #endregion
        #region Combat
        private void BattleText(string playerName, SeString message, XivChatType type) {
            CheckDependancies();
            if ((type != (XivChatType)8235 && type != (XivChatType)4139) || message.TextValue.Contains("You")) {
                Task.Run(delegate () {
                    string value = "";
                    string playerMessage = message.TextValue.Replace("「", " ").Replace("」", " ").Replace("の", " の").Replace("に", " に");
                    string[] values = playerMessage.Split(' ');
                    if (config.CharacterVoicePacks.ContainsKey(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                        string staging = config.CacheFolder + @"\Staging\" + _threadSafeObjectTable.LocalPlayer.Name.TextValue;
                        bool attackIntended = false;
                        Stopwatch performanceTimer = Stopwatch.StartNew();
                        unsafe {
                            if (!Conditions.Instance()->BoundByDuty && !Conditions.Instance()->InCombat) {
                                _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] voice pack took " + performanceTimer.ElapsedMilliseconds + " milliseconds to load.");
                                }
                            }
                        }
                        performanceTimer.Restart();
                        unsafe {
                            if (!message.TextValue.Contains("cancel")) {
                                if (Conditions.Instance()->BoundByDuty || !IsDicipleOfTheHand(_threadSafeObjectTable.LocalPlayer.ClassJob.Value.Abbreviation.ToString())) {
                                    LocalPlayerCombat(playerName, _clientState.ClientLanguage == ClientLanguage.Japanese ?
                                        playerMessage.Replace(values[0], "").Replace(values[1], "") : playerMessage, type, _mainCharacterVoicePack, ref value, ref attackIntended);
                                } else {
                                    PlayerCrafting(playerName, playerMessage, type, _mainCharacterVoicePack, ref value);
                                }
                            }
                        }
                        if (config.DebugMode) {
                            Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] voice line decision took " + performanceTimer.ElapsedMilliseconds + " milliseconds to calculate.");
                        }
                        if (!string.IsNullOrEmpty(value) || attackIntended) {
                            if (!attackIntended) {
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] Playing sound: " + Path.GetFileName(value));
                                }
                                bool boundByDuty = false;
                                unsafe {
                                    boundByDuty = Conditions.Instance()->BoundByDuty;
                                }
                                Stopwatch audioPlaybackTimer = Stopwatch.StartNew();
                                _mediaManager.PlayMedia(_playerObject, value, SoundType.MainPlayerCombat, false, 0, default,
                                  boundByDuty ?
                                    null
                                : delegate {
                                    Task.Run(delegate {
                                        if (_threadSafeObjectTable.LocalPlayer != null) {
                                            _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer);
                                        }
                                    });
                                },
                              boundByDuty ?
                              null
                                : delegate (object sender, StreamVolumeEventArgs e) {
                                    Task.Run(delegate {
                                        if (_threadSafeObjectTable.LocalPlayer != null) {
                                            if (e.MaxSampleValues.Length > 0) {
                                                if (e.MaxSampleValues[0] > 0.2) {
                                                    _addonTalkHandler.TriggerLipSync(_threadSafeObjectTable.LocalPlayer, 2);
                                                } else {
                                                    _addonTalkHandler.StopLipSync(_threadSafeObjectTable.LocalPlayer);
                                                }
                                            }
                                        }
                                    });
                                });
                                if (config.DebugMode) {
                                    Plugin.PluginLog.Debug("[Artemis Roleplaying Kit] " + Path.GetFileName(value) +
                                   " took " + audioPlaybackTimer.ElapsedMilliseconds + " milliseconds to load.");
                                }
                            }
                            if (!_muteTimer.IsRunning) {
                                if (Filter != null) {
                                    Filter.Muted = true;
                                }
                                Task.Run(() => {
                                    bool canProceeed = false;
                                    unsafe {
                                        canProceeed = config.UsePlayerSync && !Conditions.Instance()->BoundByDuty;
                                    }
                                    if (canProceeed) {
                                        Task.Run(async () => {
                                            if (_threadSafeObjectTable.LocalPlayer != null) {
                                                bool success = await _roleplayingMediaManager.SendZip(_threadSafeObjectTable.LocalPlayer.Name.TextValue, staging);
                                            }
                                        });
                                    }
                                });
                            }
                            if (config.DebugMode) {
                                Plugin.PluginLog.Debug("Battle Voice Muted");
                            }
                            _muteTimer.Restart();
                        }
                    }
                });
            } else {
                if (config.UsePlayerSync) {
                    Task.Run(delegate () {
                        string[] senderStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue))).Split(' ');
                        string[] messageStrings = RemoveActionPhrases(RemoveSpecialSymbols(message.TextValue)).Split(' ');
                        bool isShoutYell = false;
                        if (senderStrings.Length > 2) {
                            int offset = !string.IsNullOrEmpty(senderStrings[0]) ? 0 : 1;
                            string playerSender = senderStrings[0 + offset] + " " + senderStrings[2 + offset];
                            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
                            string path = config.CacheFolder + @"\VoicePack\Others";
                            string clipPath = path + @"\" + hash;
                            try {
                                Directory.CreateDirectory(path);
                            } catch {
                                _chat?.PrintError("Failed to write to disk, please make sure the cache folder does not require administrative access!");
                            }
                            Task.Run(delegate () {
                                if (GetCombinedWhitelist().Contains(playerSender)) {
                                    if (!isDownloadingZip) {
                                        if (!Path.Exists(clipPath)) {
                                            isDownloadingZip = true;
                                            _maxDownloadLengthTimer.Restart();
                                            Task.Run(async () => {
                                                try {
                                                    string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                                } catch { }
                                                isDownloadingZip = false;
                                            });
                                        }
                                    }
                                    if (Path.Exists(clipPath) && !isDownloadingZip) {
                                        CharacterVoicePack characterVoicePack = null;
                                        if (!_characterVoicePacks.ContainsKey(playerSender)) {
                                            characterVoicePack = _characterVoicePacks[playerSender] = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage);
                                        } else {
                                            characterVoicePack = _characterVoicePacks[playerSender];
                                        }
                                        string value = "";
                                        unsafe {
                                            if (Conditions.Instance()->BoundByDuty || !IsDicipleOfTheHand(_threadSafeObjectTable.LocalPlayer.ClassJob.Value.Abbreviation.ToString())) {
                                                OtherPlayerCombat(playerName, message, type, characterVoicePack, ref value);
                                            } else {
                                                PlayerCrafting(playerName, message, type, characterVoicePack, ref value);
                                            }
                                        }
                                        IPlayerCharacter player = (IPlayerCharacter)_objectTable.FirstOrDefault(x => x.Name.TextValue == playerSender);
                                        Task.Run(async () => {
                                            IGameObject character = null;
                                            if (_otherPlayerCombatTrigger > 6 || type == (XivChatType)4139) {
                                                foreach (var item in _objectTable) {
                                                    string[] playerNameStrings = SplitCamelCase(RemoveActionPhrases(RemoveSpecialSymbols(item.Name.TextValue))).Split(' ');
                                                    string playerSenderStrings = playerNameStrings[0 + offset] + " " + playerNameStrings[2 + offset];
                                                    if (playerSenderStrings.Contains(playerSender)) {
                                                        character = item;
                                                        break;
                                                    }
                                                }
                                                _mediaManager.PlayMedia(new MediaGameObject(character.ToThreadSafeObject(),
                                                playerSender, character.Position), value, SoundType.OtherPlayerCombat, false, 0, default, delegate {
                                                    Task.Run(delegate {
                                                        _addonTalkHandler.StopLipSync(player as ICharacter);
                                                    });
                                                },
                                        delegate (object sender, StreamVolumeEventArgs e) {
                                            if (e.MaxSampleValues.Length > 0) {
                                                if (e.MaxSampleValues[0] > 0.2) {
                                                    _addonTalkHandler.TriggerLipSync(player as ICharacter, 2);
                                                } else {
                                                    _addonTalkHandler.StopLipSync(player as ICharacter);
                                                }
                                            }
                                        });
                                                _otherPlayerCombatTrigger = 0;
                                            } else {
                                                _otherPlayerCombatTrigger++;
                                            }
                                        });
                                        if (!_muteTimer.IsRunning) {
                                            Filter.Muted = true;
                                        }
                                        _muteLength = 500;
                                        _muteTimer.Restart();
                                    }
                                }
                            });
                        }
                    });
                }
            }
        }

        private void LocalPlayerCombat(string playerName, SeString message,
    XivChatType type, CharacterVoicePack characterVoicePack, ref string value, ref bool attackIntended) {
            if (type == (XivChatType)2729 ||
            type == (XivChatType)2091) {
                if (!LanguageSpecificMount(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        if (attackCount == 0) {
                            if (LanguageSpecificHit(_clientState.ClientLanguage, message)) {
                                value = characterVoicePack.GetMeleeAction(message.TextValue);
                            }
                            if (LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                                value = characterVoicePack.GetCastedAction(message.TextValue);
                            }
                            if (string.IsNullOrEmpty(value)) {
                                value = characterVoicePack.GetAction(message.TextValue);
                            }
                            attackCount++;
                        } else {
                            attackCount++;
                            if (attackCount >= GetMinAttackCounts()) {
                                attackCount = 0;
                            }
                            attackIntended = true;
                        }
                    }
                }
            } else if (type == (XivChatType)2730) {
                value = characterVoicePack.GetMissed();
            } else if (type == (XivChatType)2219) {
                if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetMisc(message.TextValue);
                        if (string.IsNullOrEmpty(value)) {
                            value = characterVoicePack.GetCastingHeal();
                        }
                        castingCount++;
                    } else {
                        castingCount++;
                        if (castingCount >= GetMinAttackCounts()) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)2731) {
                if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMisc(message.TextValue);
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetReadying(message.TextValue);
                    }
                    attackCount = 0;
                    castingCount = 0;
                } else {
                    if (castingCount == 0) {
                        value = characterVoicePack.GetMisc(message.TextValue);
                        if (string.IsNullOrEmpty(value)) {
                            value = characterVoicePack.GetCastingAttack();
                        }
                        castingCount++;
                    } else {
                        castingCount++;
                        if (castingCount >= 3) {
                            castingCount = 0;
                        }
                        attackIntended = true;
                    }
                }
            } else if (type == (XivChatType)10409) {
                if (hurtCount == 0) {
                    if (string.IsNullOrEmpty(value)) {
                        value = characterVoicePack.GetHurt();
                    }
                    hurtCount++;
                } else {
                    hurtCount++;
                    if (hurtCount >= 3) {
                        hurtCount = 0;
                    }
                }
            }
        }

        private int GetMinAttackCounts() {
            switch (_threadSafeObjectTable.LocalPlayer.ClassJob.Value.Abbreviation.ToString().ToLower()) {
                case "rpr":
                    return 9;
                case "mch":
                    return 9;
                case "mnk":
                    return 6;
                case "nin":
                    return 6;
                default:
                    return 3;
            }
        }

        private void OtherPlayerCombat(string playerName, SeString message, XivChatType type,
            CharacterVoicePack characterVoicePack, ref string value) {
            if (LanguageSpecificHit(_clientState.ClientLanguage, message) ||
                LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                value = characterVoicePack.GetAction(message.TextValue);
            }
            if (string.IsNullOrEmpty(value)) {
                if (LanguageSpecificHit(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMeleeAction(message.TextValue);
                } else if (LanguageSpecificCast(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetCastedAction(message.TextValue);
                } else if (LanguageSpecificDefeat(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetDeath();
                } else if (LanguageSpecificMiss(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetMissed();
                } else if (LanguageSpecificReadying(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetReadying(message.TextValue);
                } else if (LanguageSpecificCasting(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetCastingAttack();
                } else if (LanguageSpecificRevive(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetRevive();
                } else if (LanguageSpecificHurt(_clientState.ClientLanguage, message)) {
                    value = characterVoicePack.GetHurt();
                }
            }
        }
        private bool LanguageSpecificHurt(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("damage");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("dégâts");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("schaden");
            }
            return false;
        }
        private bool LanguageSpecificRevive(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("revive");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("réanimes") || message.TextValue.ToLower().Contains("réanimée");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("belebst") || message.TextValue.ToLower().Contains("wiederbelebt");
            }
            return false;
        }
        private bool LanguageSpecificCasting(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("casting");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("lancer");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("ihren");
            }
            return false;
        }
        private bool LanguageSpecificMiss(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("misses");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("manque");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("ihrem");
            }
            return false;
        }
        private bool LanguageSpecificDefeat(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("defeated");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("vaincue");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("besiegt");
            }
            return false;
        }
        private bool LanguageSpecificHit(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("use") || message.TextValue.ToLower().Contains("uses");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("utiliser") || message.TextValue.ToLower().Contains("utilise");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("verwendest") || message.TextValue.ToLower().Contains("benutz");
                case ClientLanguage.Japanese:
                    return message.TextValue.ToLower().Contains("の攻撃");
            }
            return false;
        }
        private bool LanguageSpecificCast(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("hit") || message.TextValue.ToLower().Contains("hits");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("lancé")
                        || message.TextValue.ToLower().Contains("jette")
                        || message.TextValue.ToLower().Contains("jeté");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("sprichst") || message.TextValue.ToLower().Contains("spricht");
                case ClientLanguage.Japanese:
                    return message.TextValue.Contains("を唱えた");
            }
            return false;
        }
        private bool LanguageSpecificMount(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("mount") || message.TextValue.ToLower().Contains("mounts");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("montes") || message.TextValue.ToLower().Contains("monte");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("besteigst") || message.TextValue.ToLower().Contains("besteigt");
            }
            return false;
        }
        private bool LanguageSpecificReadying(ClientLanguage language, SeString message) {
            switch (language) {
                case ClientLanguage.English:
                    return message.TextValue.ToLower().Contains("ready") || message.TextValue.ToLower().Contains("readies");
                case ClientLanguage.French:
                    return message.TextValue.ToLower().Contains("prépares") || message.TextValue.ToLower().Contains("prépare");
                case ClientLanguage.German:
                    return message.TextValue.ToLower().Contains("bereitest") || message.TextValue.ToLower().Contains("bereitet");
                case ClientLanguage.Japanese:
                    return message.TextValue.Split(' ').Length == 5;
            }
            return false;
        }
        #endregion
        #region Player Movement Trigger
        private void ObjectIsMoving(string playerName, IGameObject gameObject) {
            if (_threadSafeObjectTable.LocalPlayer != null) {
                if (playerName == CleanSenderName(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                    SendingMovement(playerName, gameObject);
                } else {
                    ReceivingMovement(playerName, gameObject);
                }
            }
        }

        private async void ReceivingMovement(string playerSender, IGameObject gameObject) {
            string path = config.CacheFolder + @"\VoicePack\Others";
            try {
                Directory.CreateDirectory(path);
            } catch {
                _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administrative access!");
            }
            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (GetCombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            if (!Path.Exists(clipPath)) {
                                isDownloadingZip = true;
                                _maxDownloadLengthTimer.Restart();
                                await Task.Run(async () => {
                                    try {
                                        string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                                    } catch { }
                                    isDownloadingZip = false;
                                });
                            }
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                            bool isVoicedEmote = false;
                            string value = characterVoicePack.GetMisc("moving");
                            if (!string.IsNullOrEmpty(value)) {
                                _mediaManager.PlayMedia(new MediaGameObject(gameObject.ToThreadSafeObject()), value, SoundType.LoopWhileMoving, false, 0);
                                if (isVoicedEmote) {
                                    MuteVoiceCheck(6000);
                                }
                            } else {
                                _mediaManager.StopAudio(new MediaGameObject(gameObject.ToThreadSafeObject()));
                            }
                            //MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, characterVoicePack, _roleplayingMediaManager, _mediaManager);
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }
        private async void CheckOtherPlayerBoneMovement(string playerSender, IGameObject gameObject) {
            string path = config.CacheFolder + @"\VoicePack\Others";
            try {
                Directory.CreateDirectory(path);
            } catch {
                _chat?.PrintError("[Artemis Roleplaying Kit] Failed to write to disk, please make sure the cache folder does not require administraive access!");
            }
            string hash = RoleplayingMediaManager.Shai1Hash(playerSender);
            string clipPath = path + @"\" + hash;
            try {
                if (config.UsePlayerSync) {
                    if (GetCombinedWhitelist().Contains(playerSender)) {
                        if (!isDownloadingZip) {
                            //if (!Path.Exists(clipPath)) {
                            //    isDownloadingZip = true;
                            //    _maxDownloadLengthTimer.Restart();
                            //    await Task.Run(async () => {
                            //        try {
                            //            string value = await _roleplayingMediaManager.GetZip(playerSender, path);
                            //        } catch {

                            //        }
                            //        isDownloadingZip = false;
                            //    });
                            //}
                        }
                        if (Directory.Exists(path)) {
                            CharacterVoicePack characterVoicePack = new CharacterVoicePack(clipPath, DataManager, _clientState.ClientLanguage, false);
                            MediaBoneManager.CheckForValidBoneSounds(gameObject as ICharacter, characterVoicePack, _roleplayingMediaManager, _mediaManager);
                        }
                    }
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
        }

        private void SendingMovement(string playerName, IGameObject gameObject) {
            if (config.CharacterVoicePacks.ContainsKey(_threadSafeObjectTable.LocalPlayer.Name.TextValue)) {
                string voice = config.CharacterVoicePacks[_threadSafeObjectTable.LocalPlayer.Name.TextValue];
                string path = config.CacheFolder + @"\VoicePack\" + voice;
                string staging = config.CacheFolder + @"\Staging\" + _threadSafeObjectTable.LocalPlayer.Name.TextValue;
                if (_mainCharacterVoicePack == null) {
                    _mainCharacterVoicePack = new CharacterVoicePack(combinedSoundList, DataManager, _clientState.ClientLanguage);
                }
                string value = _mainCharacterVoicePack.GetMisc("moving");
                if (!string.IsNullOrEmpty(value)) {
                    //if (config.UsePlayerSync) {
                    //    Task.Run(async () => {
                    //        bool success = await _roleplayingMediaManager.SendZip(_threadSafeObjectTable.LocalPlayer.Name.TextValue, staging);
                    //    });
                    //}
                    _mediaManager.PlayMedia(_playerObject, value, SoundType.LoopWhileMoving, false, 0);
                }
            }
        }
        #endregion
        #region Crafting Checks
        private void PlayerCrafting(string playerName, SeString message,
            XivChatType type, CharacterVoicePack characterVoicePack, ref string value) {
            value = characterVoicePack.GetMisc(message.TextValue);
        }
        public bool IsDicipleOfTheHand(string value) {
            List<string> jobs = new List<string>() { "ALC", "ARM", "BSM", "CUL", "CRP", "GSM", "LTW", "WVR", "BTN", "MIN", "FSH" };
            return jobs.Contains(value.ToUpper());
        }
        #endregion
        #region SCD Management
        public async void CheckForValidSCD(MediaGameObject mediaObject, string emote = "",
    string stagingPath = "", string soundPath = "", bool isSending = false) {
            if (_nativeAudioStream != null) {
                if (isSending) {
                    Plugin.PluginLog.Debug("Emote Trigger Detected");
                    if (!string.IsNullOrEmpty(soundPath)) {
                        try {
                            Stream diskCopy = new MemoryStream();
                            if (!_mediaManager.LowPerformanceMode) {
                                try {
                                    _nativeAudioStream.Position = 0;
                                    _nativeAudioStream.CopyTo(diskCopy);
                                } catch (Exception e) {
                                    string path = Path.Combine(config.CacheFolder, @"temp\");
                                    if (File.Exists(path)) {
                                        try {
                                            File.Delete(path);
                                        } catch { }
                                    }
                                    PluginLog.Warning(e, e.Message);
                                    PluginLog.Warning("Attempting disk write instead of using memory.");
                                    _nativeAudioStream.Position = 0;
                                    Directory.CreateDirectory(path);
                                    diskCopy = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                                    _nativeAudioStream.CopyTo(diskCopy);
                                }
                            }
                            _nativeAudioStream.Position = 0;
                            _nativeAudioStream.CurrentTime = _scdProcessingDelayTimer.Elapsed;
                            _scdProcessingDelayTimer.Stop();
                            bool lipWasSynced = false;
                            ICharacter character = mediaObject.GameObject as ICharacter;
                            _mediaManager.PlayAudioStream(mediaObject, _nativeAudioStream, RoleplayingMediaCore.SoundType.Loop, false, false, 1, 0, false, delegate {
                                _addonTalkHandler.StopLipSync(character);
                            }, delegate (object sender, StreamVolumeEventArgs e) {
                                if (e.MaxSampleValues.Length > 0) {
                                    if (e.MaxSampleValues[0] > 0.2) {
                                        _addonTalkHandler.TriggerLipSync(character, 4);
                                        lipWasSynced = true;
                                    } else {
                                        _addonTalkHandler.StopLipSync(character);
                                    }
                                }
                            });
                            if (!_mediaManager.LowPerformanceMode && diskCopy.Length > 0) {
                                _ = Task.Run(async () => {
                                    try {
                                        using (FileStream fileStream = new FileStream(stagingPath + @"\" + emote + ".mp3", FileMode.Create, FileAccess.Write)) {
                                            diskCopy.Position = 0;
                                            MediaFoundationEncoder.EncodeToMp3(new RawSourceWaveStream(diskCopy, _nativeAudioStream.WaveFormat), fileStream);
                                        }
                                        _nativeAudioStream = null;
                                        diskCopy?.Dispose();
                                    } catch (Exception e) {
                                        Plugin.PluginLog?.Warning(".scd conversion to .mp3 failed");
                                        diskCopy?.Dispose();
                                    }
                                });
                            }
                            soundPath = null;
                        } catch (Exception e) {
                            Plugin.PluginLog?.Warning(e, e.Message);
                        }
                    }
                } else {
                    Plugin.PluginLog?.Warning("Not currently sending");
                }
            } else {
                Plugin.PluginLog?.Warning("There is no available audio stream to play");
            }
        }
        private void QueueSCDTrigger(ScdFile scdFile) {
            if (scdFile != null) {
                if (scdFile.Audio != null) {
                    if (scdFile.Audio.Count == 1) {
                        _inGameSoundStartedAudio = true;
                        _nativeSoundExpiryTimer.Stop();
                        _nativeSoundExpiryTimer.Restart();
                        bool isVorbis = scdFile.Audio[0].Format == SscfWaveFormat.Vorbis;
                        _nativeAudioStream = !isVorbis ? new WaveChannel32(
                        WaveFormatConversionStream.CreatePcmStream(scdFile.Audio[0].Data.GetStream())) : scdFile.Audio[0].Data.GetStream();
                    }
                }
            }
        }
        public ScdFile GetScdFile(string soundPath) {
            using (FileStream fileStream = new FileStream(_scdReplacements[soundPath], FileMode.Open, FileAccess.Read)) {
                using (BinaryReader reader = new BinaryReader(fileStream)) {
                    return new ScdFile(reader);
                }
            }
        }
        #endregion
        #endregion
    }
}
