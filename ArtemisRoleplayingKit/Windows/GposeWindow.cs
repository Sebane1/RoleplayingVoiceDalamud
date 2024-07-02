using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using RoleplayingMediaCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class GposeWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        ISharedImmediateTexture textureWrap;
        Plugin _plugin;
        private IDalamudPluginInterface _pluginInterface;
        List<byte[]> _frames = new List<byte[]>();
        List<string> _frameName = new List<string>();
        private int _currentFrame;
        private string _path;
        private FileSystemWatcher _fileWatcher;
        private string path;
        private bool _alreadyLoadingFrames;
        private ITextureProvider _textureProvider;
        private IDalamudTextureWrap _frameToLoad;
        private byte[] _lastLoadedFrame;
        private bool _alreadyLoadingFrame;

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<string> FrameNames { get => _frameName; set => _frameName = value; }

        public GposeWindow(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider) :
            base("Gpose Window", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, true) {
            IsOpen = true;
            windowSize = Size = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
            AllowClickthrough = true;
            _textureProvider = textureProvider;
        }
        public void Initialize() {
            _path = Path.Combine(_plugin.Config.CacheFolder, @"PhotoFrames\");
            _fileWatcher = new FileSystemWatcher();
            Directory.CreateDirectory(_path);
            _fileWatcher.Path = _path;
            _fileWatcher.EnableRaisingEvents = true;
            _fileWatcher.Created += _fileWatcher_Created;
            LoadFrames();
        }

        private void _fileWatcher_Created(object sender, FileSystemEventArgs e) {
            LoadFrames();
        }

        public void RefreshFrames(string[] paths) {
            _frames.Clear();
            _frameName.Clear();
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
            if (!_alreadyLoadingFrames) {
                _alreadyLoadingFrames = true;
                string path = _path;
                bool value = Directory.Exists(path);
                if (value) {
                    var files = Directory.GetFiles(path, "*.png");
                    if (files.Length > 0) {
                        RefreshFrames(files);
                    }
                }
                _alreadyLoadingFrames = false;
            }
        }

        public void SetFrame(int i) {
            if (i < _frames.Count && i > -1) {
                _currentFrame = i;
            }
        }
        public override void Draw() {
            windowSize = Size = new Vector2(ImGui.GetMainViewport().Size.X, ImGui.GetMainViewport().Size.X);
            try {
                if (_frames != null && _frames.Count > 0 && _currentFrame < _frames.Count) {
                    if (!_alreadyLoadingFrame) {
                        Task.Run(async () => {
                            _alreadyLoadingFrame = true;
                            if (_lastLoadedFrame != _frames[_currentFrame]) {
                                _frameToLoad = await _textureProvider.CreateFromImageAsync(_frames[_currentFrame]);
                                _lastLoadedFrame = _frames[_currentFrame];
                            }
                            _alreadyLoadingFrame = false;
                        });
                    }
                    if (_frameToLoad != null) {
                        ImGui.Image(_frameToLoad.ImGuiHandle,
                            new Vector2(ImGui.GetMainViewport().Size.X, ImGui.GetMainViewport().Size.Y));
                    }
                }
            } catch {

            }
        }
    }
}