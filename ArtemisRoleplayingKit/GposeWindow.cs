using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using RoleplayingMediaCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class GposeWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        IDalamudTextureWrap textureWrap;
        MediaManager _mediaManager;
        Plugin _plugin;
        private DalamudPluginInterface _pluginInterface;
        Stopwatch deadStreamTimer = new Stopwatch();
        private string fpsCount = "";
        int countedFrames = 0;
        private bool wasStreaming;
        List<byte[]> _frames = new List<byte[]>();
        List<string> _frameName = new List<string>();
        private int _currentFrame;

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<string> FrameNames { get => _frameName; set => _frameName = value; }

        public GposeWindow(DalamudPluginInterface pluginInterface) :
            base("Gpose Window", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, true) {
            IsOpen = true;
            windowSize = Size = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
            AllowClickthrough = true;
        }
        public void RefreshFrames(string[] paths) {
            _frames.Clear();
            MemoryStream blank = new MemoryStream();
            Bitmap none = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics graphics = Graphics.FromImage(none);
            graphics.Clear(Color.Transparent);
            none.Save(blank, ImageFormat.Png);
            blank.Position = 0;
            _frames.Add(blank.ToArray());
            _frameName.Add("None");
            foreach (string path in paths) {
                MemoryStream memoryStream = new MemoryStream();
                new Bitmap(path).Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;
                _frames.Add(memoryStream.ToArray());
                _frameName.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        public void LoadFrames() {
            string path = Path.Combine(_plugin.Config.CacheFolder, @"PhotoFrames\");
            RefreshFrames(Directory.GetFiles(path, "*.png"));
        }

        public void SetFrame(int i) {
            if (i < _frames.Count && i > -1) {
                _currentFrame = i;
            }
        }
        public override void Draw() {
            if (_frames.Count > 0) {
                textureWrap = _pluginInterface.UiBuilder.LoadImage(_frames[_currentFrame]);
                ImGui.Image(textureWrap.ImGuiHandle, new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height));
            }
        }
    }
}