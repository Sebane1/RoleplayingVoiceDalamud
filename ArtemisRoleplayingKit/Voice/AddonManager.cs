using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;

namespace RoleplayingVoiceDalamud.Voice {
    public abstract class AddonManager : IDisposable {
        private readonly IClientState clientState;
        private readonly ICondition condition;
        private readonly IGameGui gui;
        private readonly IDisposable subscription;
        private readonly IFramework framework;
        private readonly string name;
        private IClientState _clientState;

        protected nint Address { get; set; }

        protected AddonManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui, string name) {
            this.clientState = clientState;
            this.condition = condition;
            this.gui = gui;
            this.name = name;
            this.framework = framework;
            _clientState = clientState;
            _clientState.Logout += _clientState_Logout;
            _clientState.Login += _clientState_Login;
            this.framework.Update += Framework_Update;
            UpdateAddonAddress();
        }

        private void Framework_Update(IFramework framework) {
            UpdateAddonAddress();
        }

        private void _clientState_Login() {
            UpdateAddonAddress();
        }

        private void _clientState_Logout(int type, int code) {
            UpdateAddonAddress();
        }

        private void UpdateAddonAddress() {
            if (!this.clientState.IsLoggedIn || this.condition[ConditionFlag.CreatingCharacter]) {
                Address = nint.Zero;
                return;
            }

            Address = this.gui.GetAddonByName(this.name);
        }

        public void Dispose() {
            this.subscription?.Dispose();
            this.framework.Update -= Framework_Update;
            _clientState.Login -= _clientState_Login;
            _clientState.Logout -= _clientState_Logout;
        }
    }
}