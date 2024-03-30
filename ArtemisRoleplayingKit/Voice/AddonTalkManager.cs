using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RoleplayingVoiceDalamud.Voice {
    public class AddonTalkManager : AddonManager {
        public AddonTalkManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
            framework, clientState, condition, gui, "Talk") {
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
            return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
        }

        private unsafe AddonTalk* GetAddonTalk() {
            return (AddonTalk*)Address.ToPointer();
        }
        private unsafe AddonBattleTalk* GetAddonTalkBattle() {
            return (AddonBattleTalk*)Address.ToPointer();
        }
    }
}
