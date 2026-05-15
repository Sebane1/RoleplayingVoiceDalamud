using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;

namespace ArtemisRoleplayingKit.Voice
{
    public unsafe class UseActionListener : IDisposable
    {
        private readonly IGameInteropProvider _interopProvider;
        private readonly IPluginLog _pluginLog;

        public delegate bool UseActionDelegate(
            ActionManager* actionManager,
            ActionType actionType,
            uint actionId,
            ulong targetId,
            uint extraParam,
            ActionManager.UseActionMode callType,
            uint comboRouteId,
            bool* outOptAreaTargeted);

        private Hook<UseActionDelegate> _useActionHook;

        public event Action<uint, ActionType> OnUseAction;

        public UseActionListener(IGameInteropProvider interopProvider, IPluginLog pluginLog)
        {
            _interopProvider = interopProvider;
            _pluginLog = pluginLog;

            try
            {
                // Use HookFromSignature with the exact signature from FFXIVClientStructs' MemberFunctionAttribute
                // This bypasses the need for FFXIVClientStructs' Resolver to be initialized.
                string signature = "E8 ?? ?? ?? ?? B0 01 EB B6";
                
                _useActionHook = _interopProvider.HookFromSignature<UseActionDelegate>(signature, UseActionDetour);
                
                if (_useActionHook != null)
                {
                    _useActionHook.Enable();
                    _pluginLog.Information("[Artemis Roleplaying Kit] Successfully hooked ActionManager.UseAction via signature!");
                }
                else
                {
                    _pluginLog.Warning("[Artemis Roleplaying Kit] Failed to find ActionManager.UseAction signature.");
                }
            }
            catch (Exception ex)
            {
                _pluginLog.Error(ex, "[Artemis Roleplaying Kit] Failed to hook ActionManager.UseAction.");
            }
        }

        private bool UseActionDetour(
            ActionManager* actionManager,
            ActionType actionType,
            uint actionId,
            ulong targetId,
            uint extraParam,
            ActionManager.UseActionMode callType,
            uint comboRouteId,
            bool* outOptAreaTargeted)
        {
            bool result = _useActionHook.Original(actionManager, actionType, actionId, targetId, extraParam, callType, comboRouteId, outOptAreaTargeted);

            try
            {
                if (result)
                {
                    OnUseAction?.Invoke(actionId, actionType);
                }
            }
            catch (Exception ex)
            {
                _pluginLog.Warning(ex, "[Artemis Roleplaying Kit] Error processing UseAction detour.");
            }

            return result;
        }

        public void Dispose()
        {
            _useActionHook?.Disable();
            _useActionHook?.Dispose();
        }
    }
}
