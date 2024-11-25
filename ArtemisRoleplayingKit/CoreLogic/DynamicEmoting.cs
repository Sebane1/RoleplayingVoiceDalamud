using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Objects.Enums;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Dynamic Emoting
        private void CheckForNewDynamicEmoteRequests() {
            try {
                if (_messageQueue.Count > 0 && !disposed) {
                    if (!_messageTimer.IsRunning) {
                        _messageTimer.Start();
                    } else {
                        if (_messageTimer.ElapsedMilliseconds > 1000) {
                            try {
                                //_realChat.SendMessage(_messageQueue.Dequeue());
                            } catch (Exception e) {
                                Plugin.PluginLog?.Warning(e, e.Message);
                            }
                            _messageTimer.Restart();
                        }
                    }
                }
                if (_fastMessageQueue.Count > 0 && !disposed) {
                    try {
                        //_realChat.SendMessage(_fastMessageQueue.Dequeue());
                    } catch (Exception e) {
                        Plugin.PluginLog?.Warning(e, e.Message);
                    }
                }
                if (_aiMessageQueue.Count > 0 && !disposed) {
                    var message = _aiMessageQueue.Dequeue();
                    _chat.Print(new XivChatEntry() {
                        Message = new SeString(new List<Payload>() {
                        new TextPayload(" " + message.Item2) }),
                        Name = GetCustomNPCObject(message.Item1).NpcName,
                        Type = message.Item3
                    });
                }
            } catch (Exception e) {
                Plugin.PluginLog?.Warning(e, e.Message);
            }
            try {
                if (!string.IsNullOrEmpty(_lastEmoteAnimationUsed.Name.ToString())) {
                    Emote value = _lastEmoteAnimationUsed;
                    _lastEmoteAnimationUsed = new Emote();
                    if (!Conditions.IsWatchingCutscene) {
                        _isAlreadyRunningEmote = true;
                        Task.Run(() => {
                            if (value.EmoteMode.Value.ConditionMode is not 3) {
                                Thread.Sleep(2000);
                            }
                            if (config.DebugMode) {
                                _chat.Print("Attempt to find nearest objects.");
                            }
                            List<ICharacter> characters = new List<ICharacter>();
                            foreach (var gameObject in GetNearestObjects()) {
                                try {
                                    ICharacter character = gameObject as ICharacter;
                                    if (character != null) {
                                        if (config.DebugMode) {
                                            Plugin.PluginLog.Debug(character.Name.TextValue + " found!");
                                        }
                                        if (!character.IsDead) {
                                            if (((character.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Retainer ||
                                                character.ObjectKind == ObjectKind.BattleNpc ||
                                                character.ObjectKind == ObjectKind.EventNpc) && config.DebugMode) ||
                                                character.ObjectKind == ObjectKind.Companion ||
                                                character.ObjectKind == ObjectKind.Housing) {
                                                if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                    if (character.ObjectKind != ObjectKind.Companion || PenumbraAndGlamourerHelperFunctions.IsHumanoid(character)) {
                                                        characters.Add(character);
                                                    } else if (config.DebugMode) {
                                                        _chat.Print("Cannot apply animations to non humanoid minions.");
                                                    }
                                                } else {
                                                    if (config.DebugMode) {
                                                        _chat.Print("Cannot affect NPC's with map markers.");
                                                    }
                                                    characters.Clear();
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                } catch {
                                    if (config.DebugMode) {
                                        _chat.Print("Could not trigger emote on " + gameObject.Name + ".");
                                    }
                                }
                            }
                            foreach (ICharacter character in characters) {
                                try {
                                    if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                        if (config.DebugMode) {
                                            _chat.Print("Triggering emote! " + value.ActionTimeline[0].Value.RowId);
                                            //_chat.Print("Emote Unknowns: " + $"{value.EmoteMode.Value.ConditionMode} {value.Unknown8},{value.Unknown9},{value.Unknown10},{value.Unknown13},{value.Unknown14}," +
                                            //    $"{value.Unknown17},{value.Unknown24},");
                                        }
                                        if (config.UsePlayerSync) {
                                            unsafe {
                                                var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                                if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                                    _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)value.RowId);
                                                    _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)value.ActionTimeline[0].Value.RowId);
                                                    Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                                }
                                            }
                                        }
                                        if (value.EmoteMode.Value.ConditionMode == 3 || value.EmoteMode.Value.ConditionMode == 11) {
                                            _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, character, (ushort)value.ActionTimeline[0].Value.RowId);
                                        } else {
                                            _addonTalkHandler.TriggerEmoteTimed(character, (ushort)value.ActionTimeline[0].Value.RowId, 1000);
                                        }
                                        Thread.Sleep(500);
                                    }
                                } catch {
                                    if (config.DebugMode) {
                                        _chat.Print("Could not trigger emote on " + character.Name.TextValue + ".");
                                    }
                                }
                            }
                            _isAlreadyRunningEmote = false;
                        });
                    }
                }
            } catch {

            }
        }

        private IGameObject GetObjectByTargetId(ulong objectId) {
            foreach (var item in _objectTable) {
                if (item.GameObjectId == objectId) {
                    return item;
                }
            }
            return null;

        }
        public Dictionary<string, ICharacter> GetLocalCharacters(bool incognito) {
            var _objects = new Dictionary<string, ICharacter>();
            if (_clientState.LocalPlayer != null) {
                _objects.Add(incognito ? "Player Character" :
                _clientState.LocalPlayer.Name.TextValue, _clientState.LocalPlayer as ICharacter);
                bool oneMinionOnly = false;
                foreach (var item in GetNearestObjects()) {
                    ICharacter character = item as ICharacter;
                    if (character != null) {
                        if (character.ObjectKind == ObjectKind.Companion) {
                            if (!oneMinionOnly) {
                                string name = "";
                                foreach (var customNPC in config.CustomNpcCharacters) {
                                    if (character.Name.TextValue.ToLower().Contains(customNPC.MinionToReplace.ToLower())) {
                                        name = customNPC.NpcName;
                                    }
                                }
                                if (!string.IsNullOrEmpty(name)) {
                                    _objects.Add(name, character);
                                }
                                oneMinionOnly = true;
                            }
                        } else if (character.ObjectKind == ObjectKind.EventNpc) {
                            if (!string.IsNullOrEmpty(character.Name.TextValue)) {
                                if (!_objects.ContainsKey(character.Name.TextValue)) {
                                    _objects.Add(character.Name.TextValue, character);
                                }
                            }
                        }
                    }
                }
            }
            return _objects;
        }
        public unsafe bool IsPartOfQuestOrImportant(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject) {
            return ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address)->NamePlateIconId is not 0;
        }

        public Dalamud.Game.ClientState.Objects.Types.IGameObject[] GetNearestObjects() {
            _playerCount = 0;
            List<Dalamud.Game.ClientState.Objects.Types.IGameObject> gameObjects = new List<Dalamud.Game.ClientState.Objects.Types.IGameObject>();
            foreach (var item in _objectTable) {
                if (Vector3.Distance(_clientState.LocalPlayer.Position, item.Position) < 3f
                    && item.GameObjectId != _clientState.LocalPlayer.GameObjectId) {
                    if (item.IsValid()) {
                        gameObjects.Add((item as Dalamud.Game.ClientState.Objects.Types.IGameObject));
                    }
                }
                if (item.ObjectKind == ObjectKind.Player) {
                    _playerCount++;
                }
            }
            return gameObjects.ToArray();
        }
        private async void EmoteReaction(string messageValue) {
            var emotes = _dataManager.GetExcelSheet<Emote>();
            string[] messageEmotes = messageValue.Replace("*", " ").Split("\"");
            string emoteString = " ";
            for (int i = 1; i < messageEmotes.Length + 1; i++) {
                if ((i + 1) % 2 == 0) {
                    emoteString += messageEmotes[i - 1] + " ";
                }
            }
            foreach (var item in emotes) {
                if (!string.IsNullOrWhiteSpace(item.Name.ToString())) {
                    if ((emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + " ") ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "s ") ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "ed ") ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "ing ") ||
                        emoteString.ToLower().EndsWith(" " + item.Name.ToString().ToLower()) ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "s") ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "ed") ||
                        emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower() + "ing"))
                        || (emoteString.ToLower().Contains(" " + item.Name.ToString().ToLower()) && item.Name.ToString().Length > 3)) {
                        _messageQueue.Enqueue("/" + item.Name.ToString().ToLower());
                        break;
                    }
                }
            }
        }
        #endregion
    }
}
