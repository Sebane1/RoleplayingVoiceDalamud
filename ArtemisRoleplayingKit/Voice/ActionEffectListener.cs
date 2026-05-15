using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Numerics;

namespace ArtemisRoleplayingKit.Voice
{
    public unsafe class ActionEffectListener : IDisposable
    {
        private readonly IGameInteropProvider _interopProvider;
        private readonly IPluginLog _pluginLog;

        public delegate void ReceiveActionEffectDelegate(
            uint casterId,
            Character* caster,
            Vector3* pos,
            ActionEffectHandler.Header* effectHeader,
            ActionEffectHandler.TargetEffects* effectArray,
            GameObjectId* targets);

        private Hook<ReceiveActionEffectDelegate> _receiveActionEffectHook;

        public event Action<uint, uint> OnActionEffectReceived;

        public ActionEffectListener(IGameInteropProvider interopProvider, IPluginLog pluginLog)
        {
            _interopProvider = interopProvider;
            _pluginLog = pluginLog;

            try
            {
                // Use HookFromSignature with the exact signature from FFXIVClientStructs' MemberFunctionAttribute
                // This bypasses the need for FFXIVClientStructs' Resolver to be initialized.
                string signature = "E8 ?? ?? ?? ?? 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 00 05 00 00";
                
                _receiveActionEffectHook = _interopProvider.HookFromSignature<ReceiveActionEffectDelegate>(signature, ReceiveActionEffectDetour);
                
                if (_receiveActionEffectHook != null)
                {
                    _receiveActionEffectHook.Enable();
                    _pluginLog.Information("[Artemis Roleplaying Kit] Successfully hooked ActionEffectHandler.Receive via signature!");
                }
                else
                {
                    _pluginLog.Warning("[Artemis Roleplaying Kit] Failed to find ActionEffectHandler.Receive signature.");
                }
            }
            catch (Exception ex)
            {
                _pluginLog.Error(ex, "[Artemis Roleplaying Kit] Failed to hook ActionEffectHandler.Receive.");
            }
        }

        private void ReceiveActionEffectDetour(
            uint casterId,
            Character* caster,
            Vector3* pos,
            ActionEffectHandler.Header* effectHeader,
            ActionEffectHandler.TargetEffects* effectArray,
            GameObjectId* targets)
        {
            try
            {
                if (effectHeader != null)
                {
                    uint actionId = effectHeader->ActionId;
                    OnActionEffectReceived?.Invoke(casterId, actionId);
                }
            }
            catch (Exception ex)
            {
                _pluginLog.Warning(ex, "[Artemis Roleplaying Kit] Error processing ActionEffect detour.");
            }

            // Call the original function to let the game process the action
            _receiveActionEffectHook.Original(casterId, caster, pos, effectHeader, effectArray, targets);
        }

        public void Dispose()
        {
            _receiveActionEffectHook?.Disable();
            _receiveActionEffectHook?.Dispose();
        }
    }
}
