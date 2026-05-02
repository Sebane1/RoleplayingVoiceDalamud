using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkManager : AddonManager {
        private IGameGui _gui;

        public AddonTalkManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
            framework, clientState, condition, gui, "Talk") {
            _gui = gui;
        }

        public unsafe AddonTalkText? ReadText() {
            var addonTalk = GetAddonTalk();
            return addonTalk == null ? null : TalkUtils.ReadTalkAddon(addonTalk);
        }
        public unsafe AddonTalkText? ReadTextBattle() {
            var addonTalk = GetAddonTalkBattle();
            return addonTalk == null ? null : TalkUtils.ReadTalkAddon(addonTalk);
        }

        public unsafe bool IsVisible() {
            var addonTalk = GetAddonTalk();
            if (addonTalk != null && addonTalk->AtkUnitBase.IsVisible) {
                return true;
            }
            var battleTalk = GetAddonTalkBattle();
            return battleTalk != null && battleTalk->AtkUnitBase.IsVisible;
        }

        private unsafe AddonTalk* GetAddonTalk() {
            return (AddonTalk*)Address.ToPointer();
        }
        private unsafe AddonBattleTalk* GetAddonTalkBattle() {
            // _BattleTalk is a separate addon from Talk — must look it up independently
            nint battleTalkAddr = _gui.GetAddonByName("_BattleTalk");
            return battleTalkAddr == nint.Zero ? null : (AddonBattleTalk*)battleTalkAddr.ToPointer();
        }
    }
}
