using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
using System;
using System.Linq;

namespace PatMe {
    /// <summary>
    /// Implementation Based On Findings From PatMe
    /// </summary>
    public class EmoteReaderHooks : IDisposable {
        public Action<GameObject, ushort> OnEmote;

        public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
        private readonly Hook<OnEmoteFuncDelegate> hookEmote;

        public bool IsValid = false;
        private ClientState _clientState;
        private ObjectTable _objectTable;

        public EmoteReaderHooks(SigScanner sigScanner, ClientState clientState, ObjectTable objectTable) {
            try {
                var emoteFuncPtr = sigScanner.ScanText("48 89 5c 24 08 48 89 6c 24 10 48 89 74 24 18 48 89 7c 24 20 41 56 48 83 ec 30 4c 8b 74 24 60 48 8b d9 48 81 c1 60 2f 00 00");
                hookEmote = Hook<OnEmoteFuncDelegate>.FromAddress(emoteFuncPtr, OnEmoteDetour);
                hookEmote.Enable();

                IsValid = true;
            } catch (Exception ex) {
                PluginLog.Error(ex, "oh noes!");
            }
            _clientState = clientState;
            _objectTable = objectTable;
        }

        public void Dispose() {
            hookEmote?.Dispose();
            IsValid = false;
        }

        void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2) {
            // unk - some field of event framework singleton? doesn't matter here anyway
            // PluginLog.Log($"Emote >> unk:{unk:X}, instigatorAddr:{instigatorAddr:X}, emoteId:{emoteId}, targetId:{targetId:X}, unk2:{unk2:X}");
            try {
                if (_clientState.LocalPlayer != null) {
                    var instigatorOb = _objectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr);
                    if (instigatorOb != null) {
                        OnEmote?.Invoke(instigatorOb, emoteId);
                    }
                }
            } catch {

            }

            hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }
}
