using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using RoleplayingMediaCore;
using RoleplayingMediaCore.Twitch;
using System;
using System.Diagnostics;
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
        private Vector2? _lastWindowSize;
        public event EventHandler WindowResized;
        public TwitchFeedType FeedType = TwitchFeedType._360p;
        private bool _wasNotOpen;
        Stopwatch eventTriggerCooldown = new Stopwatch();

        public VideoWindow(DalamudPluginInterface pluginInterface) :
            base("Video Window", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar, false) {
            //IsOpen = true;
            windowSize = Size = new Vector2(640, 360);
            this.SizeCondition = ImGuiCond.Always;
            initialSize = Size;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
            PositionCondition = ImGuiCond.Once;
            eventTriggerCooldown.Start();
        }

        public MediaManager MediaManager { get => _mediaManager; set => _mediaManager = value; }

        public override void Draw() {
            if (IsOpen) {
                Size = new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().X * 0.5625f);
                SizeConstraints = new WindowSizeConstraints() { MaximumSize = ImGui.GetMainViewport().Size, MinimumSize = new Vector2(360, 480) };
                if (_mediaManager != null && _mediaManager.LastFrame != null && _mediaManager.LastFrame.Length > 0) {
                    try {
                        lock (_mediaManager.LastFrame) {
                            textureWrap = _pluginInterface.UiBuilder.LoadImage(_mediaManager.LastFrame);
                            ImGui.Image(textureWrap.ImGuiHandle, new Vector2(Size.Value.X, Size.Value.X * 0.5625f));
                        }
                    } catch {

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
                if (eventTriggerCooldown.ElapsedMilliseconds > 10000) {
                    CheckWindowSize(true);
                    eventTriggerCooldown.Restart();
                    _lastWindowSize = Size;
                }
            }
        }
        public void CheckWindowSize(bool triggerEvent) {
            if (_lastWindowSize != null) {
                if (_lastWindowSize.Value.X != Size.Value.X || _wasNotOpen) {
                    if (IsOpen) {
                        if (Size.Value.X < 360) {
                            FeedType = TwitchFeedType._160p;
                        }
                        if (Size.Value.X >= 360 || Size.Value.X < 480) {
                            FeedType = TwitchFeedType._360p;
                        }
                        if (Size.Value.X >= 480 || Size.Value.X < 720) {
                            FeedType = TwitchFeedType._480p;
                        }
                        if (Size.Value.X >= 720 || Size.Value.X < 1080) {
                            FeedType = TwitchFeedType._720p;
                        }
                        if (Size.Value.X >= 1080) {
                            FeedType = TwitchFeedType._1080p;
                        }
                    } else {
                        FeedType = TwitchFeedType.Audio;
                        _wasNotOpen = true;
                    }
                    if (triggerEvent) {
                        WindowResized?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
    }
}