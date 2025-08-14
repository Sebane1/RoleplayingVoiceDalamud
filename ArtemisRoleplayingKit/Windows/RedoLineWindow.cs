using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Dalamud.Bindings.ImGui;
using ImGuiScene;
using RoleplayingMediaCore;
using RoleplayingVoiceDalamud.Voice;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    public class RedoLineWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        private IDalamudTextureWrap textureWrap;
        private MediaManager _mediaManager;
        private IDalamudPluginInterface _pluginInterface;
        private string _stringValue = "";
        private EventHandler<string> _currentEvent;

        public RedoLineWindow(IDalamudPluginInterface pluginInterface) :
            base("Redo Line", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            //IsOpen = true;
            windowSize = Size = new Vector2(800, 40);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            IsOpen = false;
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        public override void Draw() {
            Position = new Vector2((ImGui.GetMainViewport().Size.X / 2) - (windowSize.Value.X / 2), ImGui.GetMainViewport().Size.Y - (Size.Value.Y * 2));
            float windowWidth = ImGui.GetContentRegionMax().X;
            ImGui.Text("Optional Note: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(windowWidth - (windowWidth * 0.35f));
            ImGui.InputText("##iuwdqhdiuqwdhwqiohr", ref _stringValue, 500);
            ImGui.SameLine();
            if (ImGui.Button(string.IsNullOrWhiteSpace(_stringValue) ? "Retake Line" : "Send Note")) {
                _currentEvent?.Invoke(this, _stringValue);
                _stringValue = "";
                _currentEvent = null;
                IsOpen = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) {
                _stringValue = "";
                _currentEvent = null;
                IsOpen = false;
            }
        }
        public void OpenReportBox(EventHandler<string> redoLineClicked) {
            _currentEvent = redoLineClicked;
            _stringValue = "";
        }
    }
}