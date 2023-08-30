using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using RoleplayingMediaCore;
using System;
using System.Diagnostics;
using static Penumbra.Api.Ipc;

namespace RoleplayingVoice {
    internal class VideoWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        TextureWrap textureWrap;
        MediaManager _mediaManager;
        private DalamudPluginInterface _pluginInterface;
        Stopwatch fpsCounter = new Stopwatch();
        private string fpsCount = "";
        int countedFrames = 0;

        public VideoWindow(DalamudPluginInterface pluginInterface) :
            base("Video Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            IsOpen = true;
            windowSize = Size = new Vector2(640, 360);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            fpsCounter = Stopwatch.StartNew();
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        public override void Draw() {
            Vector2 viewPortSize = ImGui.GetWindowViewport().WorkSize;
            Position = new Vector2(0, 0);
            if (_mediaManager != null && _mediaManager.LastFrame != null && _mediaManager.LastFrame.Length > 0) {
                lock (_mediaManager.LastFrame) {
                    textureWrap = _pluginInterface.UiBuilder.LoadImage(_mediaManager.LastFrame);
                    ImGui.Image(textureWrap.ImGuiHandle, new Vector2(textureWrap.Width, textureWrap.Height));
                }
            }
            if (fpsCounter.ElapsedMilliseconds > 1000) {
                fpsCount = countedFrames + "";
                countedFrames = 0;
                fpsCounter.Restart();
            } else {
                countedFrames++;
            }
        }
    }
}