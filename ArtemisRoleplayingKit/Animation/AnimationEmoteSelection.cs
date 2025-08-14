using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Bindings.ImGui;
using RoleplayingVoiceDalamud.Animation;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class AnimationEmoteSelection : Window {
        Dictionary<string, AnimationPage> _animationPages = new Dictionary<string, AnimationPage>();
        string _currentCategory = "All";
        private Plugin _plugin;
        int maxItemsPerPage = 25;
        int maxItemsPerCategoryPage = 8;
        int _categoryPage = 0;
        private List<EmoteModData> _emoteData;
        private ICharacter _character;

        public AnimationEmoteSelection(IDalamudPluginInterface pluginInterface) : base("Animation Emote Selections Window") {
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
        }

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<EmoteModData> EmoteData { get => _emoteData; }

        public void PopulateList(List<EmoteModData> list, ICharacter character) {
            _emoteData = list;
            _character = character;
        }

        public void AddItem(string category, string item) {
            if (!_animationPages.ContainsKey(category)) {
                _animationPages[category] = new AnimationPage();
            }
            _animationPages[category].Add(item);
        }
        public string CategoryCleaner(string item) {
            return item.Replace("[", "*").Replace("]", "*").Replace("(", "*")
                       .Replace(")", "*").Replace("[", "*").Replace("[", "*")
                       .Replace("~", "*").Replace("`", "*");
        }
        public override void Draw() {
            if (EmoteData.Count > 0) {
                int count = 0;
                foreach (EmoteModData item in _emoteData) {
                    if (ImGui.Button(item.Emote + " " + item.AnimationId + $" ({1 + count++})")) {
                        _plugin.TriggerCharacterEmote(item, _character);
                        IsOpen = false;
                    }
                }
            } else {
                ImGui.Text("No valid animations detected!");
            }
        }
    }
}