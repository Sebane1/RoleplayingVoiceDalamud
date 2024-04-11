using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using RoleplayingMediaCore;
using RoleplayingVoiceDalamud.Voice;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using static Penumbra.Api.Ipc;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    public class RedoLineWIndow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        private IDalamudTextureWrap textureWrap;
        private MediaManager _mediaManager;
        private DalamudPluginInterface _pluginInterface;
        public event EventHandler RedoLineClicked;


        public RedoLineWIndow(DalamudPluginInterface pluginInterface) :
            base("Redo Line", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            //IsOpen = true;
            windowSize = Size = new Vector2(120, 20);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            IsOpen = false;
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        public override void Draw() {
            Position = new Vector2((ImGui.GetMainViewport().Size.X / 2) - (windowSize.Value.X / 2), ImGui.GetMainViewport().Size.Y - (Size.Value.Y * 2));
            if (ImGui.Button("Report Line", windowSize.Value - new Vector2(10, 0))) {
                RedoLineClicked?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}