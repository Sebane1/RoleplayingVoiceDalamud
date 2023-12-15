using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using RoleplayingMediaCore;
using System;
using System.Diagnostics;
using static Penumbra.Api.Ipc;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class VideoWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        IDalamudTextureWrap textureWrap;
        MediaManager _mediaManager;
        private DalamudPluginInterface _pluginInterface;
        Stopwatch deadStreamTimer = new Stopwatch();
        private string fpsCount = "";
        int countedFrames = 0;
        private bool wasStreaming;

        public VideoWindow(DalamudPluginInterface pluginInterface) :
            base("Video Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            IsOpen = true;
            windowSize = Size = new Vector2(640, 360);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        public override void Draw() {
            if (_mediaManager != null && _mediaManager.LastFrame != null && _mediaManager.LastFrame.Length > 0) {
                lock (_mediaManager.LastFrame) {
                    textureWrap = _pluginInterface.UiBuilder.LoadImage(_mediaManager.LastFrame);
                    ImGui.Image(textureWrap.ImGuiHandle, new Vector2(500, 281));
                }
                if (deadStreamTimer.IsRunning) {
                    deadStreamTimer.Stop();
                    deadStreamTimer.Reset();
                }
                wasStreaming = true;
            } else {
                if (wasStreaming) {
                    if (!deadStreamTimer.IsRunning) {
                        deadStreamTimer.Start();
                    }
                    if (deadStreamTimer.ElapsedMilliseconds > 10000) {
                        fpsCount = countedFrames + "";
                        countedFrames = 0;
                        deadStreamTimer.Stop();
                        deadStreamTimer.Reset();
                        IsOpen = false;
                        wasStreaming = false;
                    }
                }
            }
        }
    }
}