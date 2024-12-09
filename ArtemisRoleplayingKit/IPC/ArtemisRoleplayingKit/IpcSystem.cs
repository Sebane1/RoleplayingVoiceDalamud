using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using RoleplayingMediaCore;
using RoleplayingVoice;
using RoleplayingVoiceDalamud.Voice;
using System;
using System.Collections.Generic;

namespace RoleplayingVoiceDalamud.IPC {
    public class IpcSystem : IArtemisRoleplayingKitIPC, IDisposable {
        private readonly Plugin _plugin;
        private readonly ICallGateProvider<string> _getCacheFolder;
        private readonly ICallGateProvider<nint, ushort, bool> _doAnimation;
        private readonly ICallGateProvider<nint, bool> _stopAnimation;
        private readonly ICallGateProvider<nint, string, int, bool> _playSound;
        private readonly ICallGateProvider<nint, bool> _stopSound;

        private readonly ICallGateProvider<EventHandler<KeyValuePair<nint, ushort>>, bool> _onAnimationTriggered;
        private readonly ICallGateProvider<EventHandler<nint>, bool> _onAnimationStopped;
        private readonly ICallGateProvider<EventHandler, bool> _onVoicePackChanged;

        private bool _isReady;
        private readonly AddonTalkHandler _addonTalkHandler;

        public event EventHandler<KeyValuePair<nint, ushort>> OnTriggerAnimation;
        public event EventHandler<nint> OnStoppedAnimation;
        public event EventHandler OnChangeVoicePack;

        public int APIVersion => 9;

        public bool IsInitialized { get => _isReady; }
        public IpcSystem(IDalamudPluginInterface pluginInterface,
            AddonTalkHandler addonTalkHandler, Plugin plugin) {
            _plugin = plugin;
            _getCacheFolder = pluginInterface.GetIpcProvider<string>("Artemis.GetCacheFolder");
            _doAnimation = pluginInterface.GetIpcProvider<nint, ushort, bool>("Artemis.DoAnimation");
            _stopAnimation = pluginInterface.GetIpcProvider<nint, bool>("Artemis.StopAnimation");
            _playSound = pluginInterface.GetIpcProvider<nint, string, int, bool>("Artemis.PlaySound");
            _stopSound = pluginInterface.GetIpcProvider<nint, bool>("Artemis.StopSound");
            _onAnimationTriggered = pluginInterface
                .GetIpcProvider<EventHandler<KeyValuePair<nint, ushort>>, bool>("Artemis.OnAnimationTriggered");
            _onAnimationStopped = pluginInterface
                .GetIpcProvider<EventHandler<nint>, bool>("Artemis.OnAnimationStopped");
            _onVoicePackChanged = pluginInterface.GetIpcProvider<EventHandler, bool>("Artemis.OnVoicePackChanged");

            _getCacheFolder.RegisterFunc(GetCacheFolder);
            _doAnimation.RegisterFunc(DoAnimation);
            _stopAnimation.RegisterFunc(StopAnimation);
            _playSound.RegisterFunc(PlaySound);
            _stopSound.RegisterFunc(StopSound);
            _onAnimationTriggered.RegisterFunc(OnAnimationTriggered);
            _onAnimationStopped.RegisterFunc(OnAnimationStopped);
            _onVoicePackChanged.RegisterFunc(OnVoicePackChanged);

            _addonTalkHandler = addonTalkHandler;
            _isReady = true;
        }
        public string GetCacheFolder() {
            return _plugin.Config.CacheFolder;
        }
        public void InvokeOnTriggerAnimation(nint objectAddress, ushort animation) {
            if (OnTriggerAnimation != null) {
                OnTriggerAnimation?.Invoke(this, new KeyValuePair<nint, ushort>(objectAddress, animation));
            }
        }
        public void InvokeOnStoppedAnimation(nint objectAddress) {
            if (OnStoppedAnimation != null) {
                OnStoppedAnimation?.Invoke(this, objectAddress);
            }
        }

        public void InvokeOnVoicePackChanged() {
            if (OnChangeVoicePack != null) {
                OnChangeVoicePack?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool DoAnimation(nint objectAddress, ushort animationId) {
            _addonTalkHandler.TriggerEmote(objectAddress, animationId);
            return true;
        }

        public bool StopAnimation(nint objectAddress) {
            _addonTalkHandler.StopEmote(objectAddress);
            return true;
        }

        public bool OnAnimationTriggered(EventHandler<KeyValuePair<nint, ushort>> eventHandler) {
            OnTriggerAnimation += eventHandler;
            return true;
        }


        public bool OnAnimationStopped(EventHandler<nint> eventHandler) {
            OnStoppedAnimation += eventHandler;
            return true;
        }


        public bool OnVoicePackChanged(EventHandler eventHandler) {
            OnChangeVoicePack += eventHandler;
            return true;
        }

        public bool PlaySound(nint objectAddress, string soundPath, int soundType) {
            try {
                unsafe {
                    _plugin.MediaManager.PlayMedia(new MediaGameObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)objectAddress),
                    soundPath, (SoundType)soundType, false);
                }
                return true;
            } catch {
                return false;
            }
        }

        public bool StopSound(nint objectAddress) {
            try {
                unsafe {
                    _plugin.MediaManager.StopAudio(new MediaGameObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)objectAddress));
                }
                return true;
            } catch {
                return false;
            }
        }

        public void Dispose() {
            _isReady = false;
            _getCacheFolder.UnregisterFunc();
            _doAnimation.UnregisterFunc();
            _stopAnimation.UnregisterFunc();
            _playSound.UnregisterFunc();
            _stopSound.UnregisterFunc();
            _onAnimationTriggered.UnregisterFunc();
            _onAnimationStopped.UnregisterFunc();
            _onVoicePackChanged.UnregisterFunc();
        }
    }
}
