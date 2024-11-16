using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using RoleplayingVoice;
using System;
using System.Linq;

namespace ArtemisRoleplayingKit {
    /// <summary>
    /// Implementation Based On Findings From PatMe
    /// </summary>
    public class EmoteReaderHooks : IDisposable {
        public Action<IGameObject, ushort> OnEmote;

        public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
        private readonly Hook<OnEmoteFuncDelegate> hookEmote;

        public bool IsValid = false;
        private IClientState _clientState;
        private IObjectTable _objectTable;

        public EmoteReaderHooks(IGameInteropProvider interopProvider, IClientState clientState, IObjectTable objectTable) {
            try {
                // var emoteFuncPtr = "48 89 5c 24 08 48 89 6c 24 10 48 89 74 24 18 48 89 7c 24 20 41 56 48 83 ec 30 4c 8b 74 24 60 48 8b d9 48 81 c1 80 2f 00 00";
                // var emoteFuncPtr = "40 53 56 41 54 41 57 48 83 EC ?? 48 8B 02";
                var emoteFuncPtr = "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24";
                hookEmote = interopProvider.HookFromSignature<OnEmoteFuncDelegate>(emoteFuncPtr, OnEmoteDetour);

                hookEmote.Enable();

                IsValid = true;
            } catch (Exception ex) {
                Plugin.PluginLog.Error(ex, "oh noes!");
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
