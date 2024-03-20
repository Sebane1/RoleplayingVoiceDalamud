using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Microsoft.VisualBasic.Devices;
using RoleplayingMediaCore;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using static RoleplayingVoice.PluginWindow;
namespace RoleplayingVoice {
    internal class GposePhotoTakerWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        IDalamudTextureWrap textureWrap;
        MediaManager _mediaManager;
        private DalamudPluginInterface _pluginInterface;
        Stopwatch deadStreamTimer = new Stopwatch();
        private string fpsCount = "";
        int countedFrames = 0;
        private bool wasStreaming;
        GposeWindow gposeWindow;
        BetterComboBox betterComboBox = new BetterComboBox("#photoFrames", null, 0, 300);

        public GposePhotoTakerWindow(DalamudPluginInterface pluginInterface) :
            base("Gpose Photo Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            //IsOpen = true;
            windowSize = Size = new Vector2(250, 80);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
        }

        internal GposeWindow GposeWindow { get => gposeWindow; set => gposeWindow = value; }

        public override void Draw() {
            Keyboard keyboard = new Keyboard();
            ImGui.LabelText("##gposeLabelThingy", "Pick A Photo Frame");
            betterComboBox.Contents = gposeWindow.FrameNames.ToArray();
            betterComboBox.Draw();
            gposeWindow.SetFrame(betterComboBox.SelectedIndex);
            if (ImGui.Button("Take Photo")) {

            }
        }
    }
}