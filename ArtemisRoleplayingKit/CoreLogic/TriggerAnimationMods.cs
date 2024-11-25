using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Penumbra.Api.Enums;
using RoleplayingVoiceDalamud.Animation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using VfxEditor.TmbFormat;

namespace RoleplayingVoice {
    public partial class Plugin : IDalamudPlugin {
        #region Trigger Animation Mods
        public void DoEmote(string command, ICharacter targetNPC, bool becomesPreOccupied = true) {
            try {
                foreach (var emoteItem in DataManager.GameData.GetExcelSheet<Emote>()) {
                    if (!string.IsNullOrEmpty(emoteItem.TextCommand.Value.Command.ToString())) {
                        if ((
                        emoteItem.TextCommand.Value.ShortCommand.ToString().Contains(command) ||
                        emoteItem.TextCommand.Value.Command.ToString().Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.ToString().Contains(command)) {
                            if (!IsPartOfQuestOrImportant(targetNPC as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                if (becomesPreOccupied) {
                                    _addonTalkHandler.TriggerEmote(targetNPC.Address, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    if (!_preOccupiedWithEmoteCommand.Contains(targetNPC.Name.TextValue)) {
                                        _preOccupiedWithEmoteCommand.Add(targetNPC.Name.TextValue);
                                    }
                                } else {
                                    if (emoteItem.EmoteMode.Value.ConditionMode == 3 || emoteItem.EmoteMode.Value.ConditionMode == 11) {
                                        _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, targetNPC,
                                         (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    } else {
                                        _addonTalkHandler.TriggerEmoteTimed(targetNPC, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            } catch {

            }
        }
        private void CheckNPCEmoteControl(string[] splitArgs, string args) {
            switch (splitArgs[1].ToLower()) {
                case "do":
                    DoEmote(splitArgs[2].ToLower(), splitArgs[3].ToLower());
                    break;
                case "stop":
                    foreach (var gameObject in GetNearestObjects()) {
                        try {
                            ICharacter character = gameObject as ICharacter;
                            if (character != null) {
                                if (!character.IsDead) {
                                    if (character.ObjectKind == ObjectKind.Retainer ||
                                        character.ObjectKind == ObjectKind.BattleNpc ||
                                        character.ObjectKind == ObjectKind.EventNpc ||
                                        character.ObjectKind == ObjectKind.Companion ||
                                        character.ObjectKind == ObjectKind.Housing) {
                                        if (character.Name.TextValue.ToLower().Contains(splitArgs[2].ToLower())) {
                                            if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                _toast.ShowNormal(character.Name.TextValue + " ceases your command.");
                                                _addonTalkHandler.StopEmote(character.Address);
                                                if (_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                                    _preOccupiedWithEmoteCommand.Remove(character.Name.TextValue);
                                                }
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        } catch { }
                    }
                    break;
                case "playerpos":
                    foreach (var gameObject in GetNearestObjects()) {
                        try {
                            ICharacter character = gameObject as ICharacter;
                            if (character != null) {
                                if (!character.IsDead) {
                                    if (character.ObjectKind == ObjectKind.Retainer ||
                                        character.ObjectKind == ObjectKind.BattleNpc ||
                                        character.ObjectKind == ObjectKind.EventNpc ||
                                        character.ObjectKind == ObjectKind.Companion ||
                                        character.ObjectKind == ObjectKind.Housing) {
                                        if (character.Name.TextValue.ToLower().Contains(splitArgs[2].ToLower())) {
                                            bool hasQuest = false;
                                            unsafe {
                                                var item = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(character as ICharacter).Address;
                                                item->Balloon.PlayTimer = 1;
                                                item->Balloon.Text = new FFXIVClientStructs.FFXIV.Client.System.String.Utf8String("I will stand here master!");
                                                item->Balloon.Type = BalloonType.Timer;
                                                item->Balloon.State = BalloonState.Active;
                                                item->GameObject.Position = _clientState.LocalPlayer.Position;
                                            }

                                            break;
                                        }
                                    }
                                }
                            }
                        } catch { }
                    }
                    break;
            }
        }

        public unsafe void DoEmote(string command, string targetNPC, bool becomesPreOccupied = true) {
            foreach (var emoteItem in DataManager.GameData.GetExcelSheet<Emote>()) {
                if (!string.IsNullOrEmpty(emoteItem.TextCommand.Value.Command.ToString())) {
                    if ((
                        emoteItem.TextCommand.Value.ShortCommand.ToString().Contains(command) ||
                        emoteItem.TextCommand.Value.Command.ToString().Contains(command)) ||
                        emoteItem.TextCommand.Value.ShortAlias.ToString().Contains(command)) {
                        foreach (var gameObject in GetNearestObjects()) {
                            try {
                                ICharacter character = gameObject as ICharacter;
                                if (character != null) {
                                    if (!character.IsDead) {
                                        if (character.ObjectKind == ObjectKind.Retainer ||
                                            character.ObjectKind == ObjectKind.BattleNpc ||
                                            character.ObjectKind == ObjectKind.EventNpc ||
                                            character.ObjectKind == ObjectKind.Companion ||
                                            character.ObjectKind == ObjectKind.Housing) {
                                            if (character.Name.TextValue.ToLower().Contains(targetNPC.ToLower())) {
                                                if (!IsPartOfQuestOrImportant(character as Dalamud.Game.ClientState.Objects.Types.IGameObject)) {
                                                    if (AgentEmote.Instance()->CanUseEmote((ushort)emoteItem.RowId)) {
                                                        _toast.ShowNormal(character.Name.TextValue + " follows your command!");
                                                        var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                                        if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)emoteItem.RowId);
                                                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                        }
                                                        Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                                        if (becomesPreOccupied) {
                                                            _addonTalkHandler.TriggerEmote(character.Address, (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                            if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                                                _preOccupiedWithEmoteCommand.Add(character.Name.TextValue);
                                                            }
                                                        } else {
                                                            _addonTalkHandler.TriggerEmoteUntilPlayerMoves(_clientState.LocalPlayer, character,
                                                                (ushort)emoteItem.ActionTimeline[0].Value.RowId);
                                                        }
                                                    } else {
                                                        _toast.ShowError(character.Name.TextValue + " you have not unlocked this emote yet.");
                                                    }
                                                } else {
                                                    _toast.ShowError(character.Name.TextValue + " resists your command! (Cannot affect quest NPCs)");
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }
                        break;
                    }
                }
            }
        }
        public void CheckAnimationMods(string[] splitArgs, string args, ICharacter character, bool willOpen = true) {
            Task.Run(() => {
                if (splitArgs.Length > 1) {
                    string[] command = null;
                    command = args.Replace(splitArgs[0] + " ", null).ToLower().Trim().Split("emote:");
                    int index = 0;
                    try {
                        if (command.Length > 1) {
                            index = int.Parse(command[1]);
                        }
                    } catch {
                        index = 0;
                    }
                    DoAnimation(command[0].Trim(), index, character);
                } else {
                    if (!_animationCatalogue.IsOpen) {
                        var list = CreateEmoteList(_dataManager);
                        var newList = new List<string>();
                        foreach (string key in _animationMods.Keys) {
                            foreach (string emote in _animationMods[key].Value) {
                                string[] strings = emote.Split("/");
                                string option = strings[strings.Length - 1];
                                if (list.ContainsKey(option)) {
                                    if (!newList.Contains(key)) {
                                        newList.Add(key);
                                    }
                                }
                            }
                        }
                        newList.Sort();
                        _animationCatalogue.AddNewList(newList);
                        if (willOpen) {
                            _animationCatalogue.IsOpen = true;
                        }
                    } else {
                        _animationCatalogue.IsOpen = false;
                    }
                }
            });
        }

        public void DoAnimation(string animationName, int index, ICharacter targetObject) {
            Task.Run(() => {
                _blockDataRefreshes = true;
                var list = CreateEmoteList(_dataManager);
                List<uint> deDuplicate = new List<uint>();
                List<EmoteModData> emoteData = new List<EmoteModData>();
                var collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke((int)targetObject.ObjectIndex);
                string commandArguments = animationName;
                if (config.DebugMode) {
                    Plugin.PluginLog.Debug("Attempting to find mods that contain \"" + commandArguments + "\".");
                }
                _mediaManager?.CleanNonStreamingSounds();
                // for (int i = 0; i < 20 && emoteData.Count == 0; i++) {
                foreach (var modName in _animationMods.Keys) {
                    if (modName.ToLower().Contains(commandArguments)) {
                        if (collection.Item3.Name != "None") {
                            var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection.Item3.Id, modName, true);
                            var result2 = PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection.Item3.Id, modName, 10);
                            _mediaManager.StopAudio(_playerObject);
                            if (config.DebugMode) {
                                Plugin.PluginLog.Debug(modName + " was attempted to be enabled. The result was " + result + ".");
                            }
                            var animationItems = _animationMods[modName];
                            foreach (var foundAnimation in animationItems.Value) {
                                bool foundEmote = false;
                                if (_papSorting.ContainsKey(foundAnimation)) {
                                    var sortedList = _papSorting[foundAnimation];
                                    foreach (var mod in sortedList) {
                                        if (mod.Item2.ToLower().Contains(modName.ToLower().Trim())) {
                                            if (!foundEmote) {
                                                if (list.ContainsKey(foundAnimation)) {
                                                    foreach (var value in list[foundAnimation]) {
                                                        try {
                                                            string name = value.TextCommand.Value.Command.ToString().ToLower().Replace(" ", null).Replace("'", null);
                                                            if (!string.IsNullOrEmpty(name)) {
                                                                if (!deDuplicate.Contains(value.ActionTimeline[0].Value.RowId)) {
                                                                    emoteData.Add(new
                                                                    EmoteModData(
                                                                    name,
                                                                    value.RowId,
                                                                    value.ActionTimeline[0].Value.RowId,
                                                                    modName));
                                                                    deDuplicate.Add(value.ActionTimeline[0].Value.RowId);
                                                                }
                                                                foundEmote = true;
                                                                break;
                                                            }
                                                        } catch {
                                                        }
                                                    }
                                                }
                                            }
                                        } else {
                                            // Thread.Sleep(100);
                                            var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection.Item3.Id, mod.Item2, false);
                                            var ipcResult2 = PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection.Item3.Id, mod.Item2, -10);
                                            if (config.DebugMode) {
                                                Plugin.PluginLog.Debug(mod.Item2 + " was attempted to be disabled. The result was " + ipcResult + ".");
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        } else {
                            _chat.PrintError("Failed to trigger animation. The specified character has no assigned Penumbra collection!");
                        }
                    }
                }
                //}
                if (emoteData.Count > 0) {
                    if (emoteData.Count == 1) {
                        TriggerCharacterEmote(emoteData[0], targetObject);
                    } else if (emoteData.Count > 1) {
                        if (index == 0) {
                            _animationEmoteSelection.PopulateList(emoteData, targetObject);
                            _animationEmoteSelection.IsOpen = true;
                        } else {
                            TriggerCharacterEmote(emoteData[index - 1], targetObject);
                        }
                    }
                } else {
                    Task.Run(() => {
                        if (_failCount++ < 10) {
                            Thread.Sleep(3000);
                            DoAnimation(animationName, index, targetObject);
                        } else {
                            _failCount = 0;
                        }
                    });
                }
                _blockDataRefreshes = false;
            });
        }

        public void TriggerCharacterEmote(EmoteModData emoteModData, ICharacter character) {
            if (character == _clientState.LocalPlayer) {
                if (_wasDoingFakeEmote) {
                    _addonTalkHandler.StopEmote(_clientState.LocalPlayer.Address);
                }
            }
            PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(character.ObjectIndex, RedrawType.Redraw);
            Task.Run(() => {
                //Thread.Sleep(1000);
                bool ownsEmote = false;
                unsafe {
                    ownsEmote = AgentEmote.Instance()->CanUseEmote((ushort)emoteModData.EmoteId);
                }
                if (character == _clientState.LocalPlayer && ownsEmote) {
                    _messageQueue.Enqueue(emoteModData.Emote);
                    if (!_animationModsAlreadyTriggered.Contains(emoteModData.FoundModName) && config.MoveSCDBasedModsToPerformanceSlider) {
                        Thread.Sleep(100);
                        _fastMessageQueue.Enqueue(emoteModData.Emote);
                        _animationModsAlreadyTriggered.Add(emoteModData.FoundModName);
                    }
                    _mediaManager.StopAudio(_playerObject);
                    Thread.Sleep(2000);
                } else {
                    _mediaManager.StopAudio(_playerObject);
                    //Thread.Sleep(1000);
                }
                if (_objectRecentlyDidEmote) {
                    Thread.Sleep(1000);
                    _objectRecentlyDidEmote = false;
                } else {
                    _objectRecentlyDidEmote = true;
                }
                if (!_didRealEmote || !ownsEmote) {
                    if (character == _clientState.LocalPlayer) {
                        _wasDoingFakeEmote = true;
                    }
                    _addonTalkHandler.TriggerEmote(character.Address, (ushort)emoteModData.AnimationId);
                    OnEmote(character, (ushort)emoteModData.EmoteId);
                    if (character.ObjectKind == ObjectKind.Companion) {
                        if (!_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                            _preOccupiedWithEmoteCommand.Add(character.Name.TextValue);
                        }
                    }
                    unsafe {
                        var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                        if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmoteId", (ushort)emoteModData.EmoteId);
                            _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name.TextValue + "MinionEmote", (ushort)emoteModData.AnimationId);
                        } else {
                            _roleplayingMediaManager.SendShort(character.Name.TextValue + "emoteId", (ushort)emoteModData.EmoteId);
                            _roleplayingMediaManager.SendShort(character.Name.TextValue + "emote", (ushort)emoteModData.AnimationId);
                        }
                        Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                    }
                    Task.Run(() => {
                        Vector3 lastPosition = character.Position;
                        while (true) {
                            Thread.Sleep(500);
                            if (Vector3.Distance(lastPosition, character.Position) > 0.001f) {
                                _didRealEmote = false;
                                _addonTalkHandler.StopEmote(character.Address);
                                _wasDoingFakeEmote = false;
                                unsafe {
                                    var characterStruct = ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)_clientState.LocalPlayer.Address);
                                    if (characterStruct->CompanionObject != null && character.Address == (nint)characterStruct->CompanionObject) {
                                        _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name + "MinionEmoteId", (ushort.MaxValue));
                                        _roleplayingMediaManager.SendShort(_clientState.LocalPlayer.Name + "MinionEmote", (ushort.MaxValue));
                                    } else {
                                        _roleplayingMediaManager.SendShort(character.Name.TextValue + "emoteId", (ushort.MaxValue));
                                        _roleplayingMediaManager.SendShort(character.Name.TextValue + "emote", (ushort.MaxValue));
                                        _animationModsAlreadyTriggered.Remove(emoteModData.FoundModName);
                                    }
                                    Plugin.PluginLog.Verbose("Sent emote to server for " + character.Name);
                                }
                                if (character.ObjectKind == ObjectKind.Companion) {
                                    if (_preOccupiedWithEmoteCommand.Contains(character.Name.TextValue)) {
                                        _preOccupiedWithEmoteCommand.Remove(character.Name.TextValue);
                                    }
                                }
                                break;
                            }
                        }
                    });
                }
                _didRealEmote = false;
            });
        }

        private IReadOnlyDictionary<string, IReadOnlyList<Emote>> CreateEmoteList(IDataManager gameData) {
            if (_emoteList == null) {
                var sheet = gameData.GetExcelSheet<Emote>()!;
                var storage = new ConcurrentDictionary<string, ConcurrentBag<Emote>>();

                void AddEmote(string? key, Emote emote) {
                    if (string.IsNullOrEmpty(key))
                        return;

                    key = key.ToLowerInvariant();
                    if (storage.TryGetValue(key, out var emotes))
                        emotes.Add(emote);
                    else
                        storage[key] = new ConcurrentBag<Emote> { emote };
                }

                var options = new ParallelOptions {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                };
                var seenTmbs = new ConcurrentDictionary<string, TmbFile>();

                void ProcessEmote(Emote emote) {
                    var emoteTmbs = new HashSet<string>(8);
                    var tmbs = new Queue<string>(8);

                    foreach (var timeline in emote.ActionTimeline.Where(t => t.RowId != 0).Select(t => t.Value!)) {
                        var key = timeline.Key.ToDalamudString().TextValue;
                        AddEmote(Path.GetFileName(key) + ".pap", emote);
                    }

                    while (tmbs.TryDequeue(out var tmbPath)) {
                        if (!emoteTmbs.Add(tmbPath))
                            continue;
                        AddEmote(Path.GetFileName(tmbPath), emote);
                    }
                }

                Parallel.ForEach(sheet.Where(n => n.Name.Data.Length > 0), options, ProcessEmote);

                var sit = sheet.GetRow(50)!;
                AddEmote("s_pose01_loop.pap", sit);
                AddEmote("s_pose02_loop.pap", sit);
                AddEmote("s_pose03_loop.pap", sit);
                AddEmote("s_pose04_loop.pap", sit);
                AddEmote("s_pose05_loop.pap", sit);

                var sitOnGround = sheet.GetRow(52)!;
                AddEmote("j_pose01_loop.pap", sitOnGround);
                AddEmote("j_pose02_loop.pap", sitOnGround);
                AddEmote("j_pose03_loop.pap", sitOnGround);
                AddEmote("j_pose04_loop.pap", sitOnGround);

                var doze = sheet.GetRow(13)!;
                AddEmote("l_pose01_loop.pap", doze);
                AddEmote("l_pose02_loop.pap", doze);
                AddEmote("l_pose03_loop.pap", doze);

                _emoteList = storage.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Emote>)kvp.Value.Distinct().ToArray());
            }
            return _emoteList;
        }

        #endregion
    }
}
