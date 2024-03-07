using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;

namespace RoleplayingVoiceDalamud.Voice {
    public abstract class AddonManager : IDisposable {
        private readonly IClientState clientState;
        private readonly ICondition condition;
        private readonly IGameGui gui;
        private readonly IDisposable subscription;
        private readonly string name;
        private IClientState _clientState;

        protected nint Address { get; set; }

        protected AddonManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui, string name) {
            this.clientState = clientState;
            this.condition = condition;
            this.gui = gui;
            this.name = name;
            _clientState = clientState;
            _clientState.Logout += ClientState_Logout;
            _clientState.Login += _clientState_Login;
            UpdateAddonAddress();
        }

        private void _clientState_Login() {
            UpdateAddonAddress();
        }

        private void ClientState_Logout() {
            UpdateAddonAddress();
        }

        private void UpdateAddonAddress() {
            if (!this.clientState.IsLoggedIn || this.condition[ConditionFlag.CreatingCharacter]) {
                Address = nint.Zero;
                return;
            }

            if (Address == nint.Zero) {
                Address = this.gui.GetAddonByName(this.name);
            }
        }

        public void Dispose() {
            this.subscription.Dispose();
            _clientState.Login -= ClientState_Logout;
        }
    }
}