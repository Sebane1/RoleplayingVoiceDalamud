using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using RoleplayingVoice;
using RoleplayingVoiceDalamud;

namespace SoundFilter {
    /// <summary>
    /// Implementation Based On Findings From SoundFilter
    /// </summary>
    internal unsafe class Filter : IDisposable {
        private static class Signatures {
            internal const string PlaySpecificSound = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F";

            internal const string GetResourceSync = "E8 ?? ?? ?? ?? 48 8B D8 8B C7";
            internal const string GetResourceAsync = "E8 ?? ?? ?? ?? 48 8B D8 EB 07 F0 FF 83";
            internal const string LoadSoundFile = "E8 ?? ?? ?? ?? 48 85 C0 75 12 B0 F6";

            internal const string MusicManagerOffset = "48 89 87 ?? ?? ?? ?? 49 8B CC E8 ?? ?? ?? ?? 48 8B 8F";

        }

        // Updated: 5.55
        private const int ResourceDataPointerOffset = 0xB0;
        private const int MusicManagerStreamingOffset = 0x32;
        public event EventHandler<InterceptedSound> OnSoundIntercepted;
        public event EventHandler<InterceptedSound> OnCutsceneAudioDetected;
        public event EventHandler<InterceptedSound> OnFilterWasRan;
        #region Delegates

        private delegate void* PlaySpecificSoundDelegate(long a1, int idx);

        private delegate void* GetResourceSyncPrototype(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown);

        private delegate void* GetResourceAsyncPrototype(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown);

        private delegate IntPtr LoadSoundFileDelegate(IntPtr resourceHandle, uint a2);

        #endregion

        #region Hooks

        private Hook<PlaySpecificSoundDelegate>? PlaySpecificSoundHook { get; set; }

        private Hook<GetResourceSyncPrototype>? GetResourceSyncHook { get; set; }

        private Hook<GetResourceAsyncPrototype>? GetResourceAsyncHook { get; set; }

        private Hook<LoadSoundFileDelegate>? LoadSoundFileHook { get; set; }
        public Plugin Plugin { get; }

        #endregion

        private bool WasStreamingEnabled { get; }

        private ConcurrentDictionary<IntPtr, string> Scds { get; } = new();

        internal ConcurrentQueue<string> Recent { get; } = new();


        private IntPtr NoSoundPtr { get; }
        private IntPtr InfoPtr { get; }

        private List<string> _blacklist = new List<string>();
        private bool muted = false;

        private IntPtr MusicManager {
            get {
                if (!this.Plugin.SigScanner.TryScanText(Signatures.MusicManagerOffset, out var instructionPtr)) {
                    //Plugin.PluginLog.Warning("Could not find music manager");
                    return IntPtr.Zero;
                }

                var offset = *(int*)(instructionPtr + 3);
                return *(IntPtr*)((IntPtr)Framework.Instance() + offset);
            }
        }

        public bool Streaming {
            get {
                var manager = this.MusicManager;
                if (manager == IntPtr.Zero) {
                    return false;
                }

                return *(byte*)(manager + MusicManagerStreamingOffset) > 0;
            }
            set {
                var manager = this.MusicManager;
                if (manager == IntPtr.Zero) {
                    return;
                }

                *(byte*)(manager + MusicManagerStreamingOffset) = (byte)(value ? 1 : 0);
            }
        }

        public bool Muted { get => muted; set => muted = value; }
        public List<string> Blacklist { get => _blacklist; set => _blacklist = value; }

        public Filter(Plugin plugin) {
            this.Plugin = plugin;

            this.WasStreamingEnabled = this.Streaming;
            //this.Streaming = false;

            var (noSoundPtr, infoPtr) = SetUpNoSound();
            this.NoSoundPtr = noSoundPtr;
            this.InfoPtr = infoPtr;
        }

        private static byte[] GetNoSoundScd() {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("emote.scd"));
            var noSound = assembly.GetManifestResourceStream(resourceName);
            using var memoryStream = new MemoryStream();
            noSound.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        private static (IntPtr noSoundPtr, IntPtr infoPtr) SetUpNoSound() {
            // get the data of an empty scd
            var noSound = GetNoSoundScd();

            // allocate unmanaged memory for this data and copy the data into the memory
            var noSoundPtr = Marshal.AllocHGlobal(noSound.Length);
            Marshal.Copy(noSound, 0, noSoundPtr, noSound.Length);

            // allocate some memory for feeding into the play sound function
            var infoPtr = Marshal.AllocHGlobal(256);
            // write a pointer to the empty scd
            Marshal.WriteIntPtr(infoPtr + 8, noSoundPtr);
            // specify where the game should offset from for the sound index
            Marshal.WriteInt32(infoPtr + 0x88, 0x54);
            // specify the number of sounds in the file
            Marshal.WriteInt16(infoPtr + 0x94, 0);

            return (noSoundPtr, infoPtr);
        }

        internal void Enable() {
            if (this.PlaySpecificSoundHook == null && this.Plugin.SigScanner.TryScanText(Signatures.PlaySpecificSound, out var playPtr)) {
                this.PlaySpecificSoundHook = this.Plugin.InteropProvider.HookFromAddress<PlaySpecificSoundDelegate>(playPtr, this.PlaySpecificSoundDetour);
            }

            if (this.GetResourceSyncHook == null && this.Plugin.SigScanner.TryScanText(Signatures.GetResourceSync, out var syncPtr)) {
                this.GetResourceSyncHook = this.Plugin.InteropProvider.HookFromAddress<GetResourceSyncPrototype>(syncPtr, this.GetResourceSyncDetour);
            }

            if (this.GetResourceAsyncHook == null && this.Plugin.SigScanner.TryScanText(Signatures.GetResourceAsync, out var asyncPtr)) {
                this.GetResourceAsyncHook = this.Plugin.InteropProvider.HookFromAddress<GetResourceAsyncPrototype>(asyncPtr, this.GetResourceAsyncDetour);
            }

            if (this.LoadSoundFileHook == null && this.Plugin.SigScanner.TryScanText(Signatures.LoadSoundFile, out var soundPtr)) {
                this.LoadSoundFileHook = this.Plugin.InteropProvider.HookFromAddress<LoadSoundFileDelegate>(soundPtr, this.LoadSoundFileDetour);
            }

            this.PlaySpecificSoundHook?.Enable();
            this.LoadSoundFileHook?.Enable();
            this.GetResourceSyncHook?.Enable();
            this.GetResourceAsyncHook?.Enable();
        }

        internal void Disable() {
            this.PlaySpecificSoundHook?.Disable();
            this.LoadSoundFileHook?.Disable();
            this.GetResourceSyncHook?.Disable();
            this.GetResourceAsyncHook?.Disable();
        }

        public void Dispose() {
            this.PlaySpecificSoundHook?.Dispose();
            this.LoadSoundFileHook?.Dispose();
            this.GetResourceSyncHook?.Dispose();
            this.GetResourceAsyncHook?.Dispose();

            Marshal.FreeHGlobal(this.InfoPtr);
            Marshal.FreeHGlobal(this.NoSoundPtr);

            this.Streaming = this.WasStreamingEnabled;
        }

        private void* PlaySpecificSoundDetour(long a1, int idx) {
            try {
                var shouldFilter = this.PlaySpecificSoundDetourInner(a1, idx);
                if (shouldFilter) {
                    a1 = (long)this.InfoPtr;
                    idx = 0;
                }
            } catch (Exception ex) {
                Plugin.PluginLog.Error(ex, "Error in PlaySpecificSoundDetour");
            }
            return this.PlaySpecificSoundHook!.Original(a1, idx);
        }

        private void* GetResourceSyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown) {
            return this.ResourceDetour(true, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, false);
        }

        private void* GetResourceAsyncDetour(IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown) {
            return this.ResourceDetour(false, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);
        }

        private void* ResourceDetour(bool isSync, IntPtr pFileManager, uint* pCategoryId, char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown) {
            var ret = this.CallOriginalResourceHandler(isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);

            var path = Util.ReadTerminatedString((byte*)pPath);
            if (ret != null && path.EndsWith(".scd")) {
                var scdData = Marshal.ReadIntPtr((IntPtr)ret + ResourceDataPointerOffset);
                // if we immediately have the scd data, cache it, otherwise add it to a waiting list to hopefully be picked up at sound play time
                if (scdData != IntPtr.Zero) {
                    this.Scds[scdData] = path;
                }
            }

            return ret;
        }

        private void* CallOriginalResourceHandler(bool isSync, IntPtr pFileManager, uint* pCategoryId, char* pResourceType,
            uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown) {
            return isSync
                ? this.GetResourceSyncHook!.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown)
                : this.GetResourceAsyncHook!.Original(pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown);
        }
        private bool PlaySpecificSoundDetourInner(long a1, int idx) {
            if (a1 == 0) {
                return false;
            }

            var scdData = *(byte**)(a1 + 8);
            if (scdData == null) {
                return false;
            }

            // check cached scds for path
            if (!this.Scds.TryGetValue((IntPtr)scdData, out var path)) {
                return false;
            }
            path = path.ToLowerInvariant();
            var specificPath = $"{path}/{idx}";
            string splitPath = specificPath.Split(".scd")[0] + ".scd";
            if ((specificPath.Contains("vo_battle") && muted)
                || (_blacklist.Contains(splitPath) && Plugin.Config.MoveSCDBasedModsToPerformanceSlider) && !splitPath.Contains("sound/foot")) {
                Plugin.PluginLog.Info("Trigger Sound Interception");
                OnSoundIntercepted?.Invoke(this, new InterceptedSound() { SoundPath = splitPath });
                return true;
            } else if (specificPath.Contains("/strm/")) {
                return true;
            }
            if (Plugin.ClientState.ClientLanguage == ClientLanguage.English) {
                if (((specificPath.Contains("vo_voiceman") || specificPath.Contains("vo_man") || specificPath.Contains("vo_line") || specificPath.Contains("vo_line")) || specificPath.Contains("cut/ffxiv/"))) {
                    if ((specificPath.Contains("vo_man") || (specificPath.Contains("cut/ffxiv/") && specificPath.Contains("vo_voiceman"))) && Plugin.Config.ReplaceVoicedARRCutscenes
                        && Plugin.Window.NpcSpeechEnabled) {
                        OnCutsceneAudioDetected?.Invoke(this, new InterceptedSound() { SoundPath = splitPath, isBlocking = false });
                        return true;
                    } else {
                        OnCutsceneAudioDetected?.Invoke(this, new InterceptedSound() { SoundPath = splitPath, isBlocking = true });
                        return false;
                    }
                }
            }
            OnFilterWasRan?.Invoke(this, new InterceptedSound { SoundPath = splitPath });
            return false;
        }
        public bool IsCutsceneDetectionNull() {
            return OnCutsceneAudioDetected == null;
        }
        private IntPtr LoadSoundFileDetour(IntPtr resourceHandle, uint a2) {
            var ret = this.LoadSoundFileHook!.Original(resourceHandle, a2);

            try {
                var handle = (ResourceHandle*)resourceHandle;
                var name = handle->FileName.ToString();
                if (name.EndsWith(".scd") && muted) {
                    var dataPtr = Marshal.ReadIntPtr(resourceHandle + ResourceDataPointerOffset);
                    this.Scds[dataPtr] = name;
                }
            } catch (Exception ex) {
                Plugin.PluginLog.Error(ex, "Error in LoadSoundFileDetour");
            }

            return ret;
        }
    }
}