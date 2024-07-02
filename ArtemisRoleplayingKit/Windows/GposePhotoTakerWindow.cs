using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using static RoleplayingVoice.PluginWindow;
using System;
using System.Threading.Tasks;
using RoleplayingVoiceDalamud;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
namespace RoleplayingVoice {
    internal class GposePhotoTakerWindow : Window {
        IDalamudTextureWrap textureWrap;
        private IDalamudPluginInterface _pluginInterface;
        GposeWindow gposeWindow;
        BetterComboBox betterComboBox = new BetterComboBox("#photoFrames", null, 0, 300);
        private bool _takingScreenShot;

        public GposePhotoTakerWindow(IDalamudPluginInterface pluginInterface) :
            base("Gpose Photo Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoTitleBar, false) {
            //IsOpen = true;
            Size = new Vector2(350, 150);
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
        }
        public override bool DrawConditions() {
            try {
                return !_takingScreenShot && gposeWindow != null 
                && gposeWindow.FrameNames != null && gposeWindow.FrameNames.Count > 0;
            } catch {
                return false;
            }
        }
        internal GposeWindow GposeWindow { get => gposeWindow; set => gposeWindow = value; }

        public override void Draw() {
            if (DrawConditions()) {
                try {
                    ImGui.LabelText("##gposeLabelThingy", "Pick A Photo Frame");
                    betterComboBox.Contents = gposeWindow.FrameNames.ToArray();
                    betterComboBox.Draw();
                    gposeWindow.SetFrame(betterComboBox.SelectedIndex);
                    gposeWindow.Plugin.DragDrop.CreateImGuiSource("TextureDragDrop", m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m => {
                        ImGui.TextUnformatted($"Dragging texture for import:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
                        return true;
                    });
                    if (ImGui.Button("Open Custom Photo Frames Folder", new Vector2(ImGui.GetWindowSize().X - 10, 40))) {
                        string path = Path.Combine(gposeWindow.Plugin.Config.CacheFolder, @"PhotoFrames\");
                        ProcessStartInfo ProcessInfo;
                        Process Process; ;
                        try {
                            Directory.CreateDirectory(path);
                        } catch {
                        }
                        ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + path + @"""");
                        ProcessInfo.UseShellExecute = true;
                        Process = Process.Start(ProcessInfo);
                    }
                    if (gposeWindow.Plugin.DragDrop.CreateImGuiTarget("TextureDragDrop", out var files, out _)) {
                        if (ValidTextureExtensions.Contains(Path.GetExtension(files[0]))) {
                            string path = Path.Combine(gposeWindow.Plugin.Config.CacheFolder, @"PhotoFrames\");
                            foreach (string file in files) {
                                File.Copy(files[0], Path.Combine(path, Path.GetFileName(files[0])));
                            }
                            gposeWindow.LoadFrames();
                        }
                    }
                    if (ImGui.Button("Take Photo")) {
                        _takingScreenShot = true;
                        Task.Run(delegate {
                            try {
                                NativeGameWindow.BringMainWindowToFront(Process.GetCurrentProcess().ProcessName);
                            } catch { }
                            Thread.Sleep(100);
                            TakeScreenshot();
                            _takingScreenShot = false;
                        });
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("View Photo's")) {
                        string screenshotPath = Path.Combine(gposeWindow.Plugin.Config.CacheFolder, "Screenshots\\");
                        ProcessStartInfo ProcessInfo;
                        Process Process; ;
                        try {
                            Directory.CreateDirectory(screenshotPath);
                        } catch {
                        }
                        ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + screenshotPath + @"""");
                        ProcessInfo.UseShellExecute = true;
                        Process = Process.Start(ProcessInfo);
                    }
                } catch {

                }
            }
        }
        private static readonly List<string> ValidTextureExtensions = new List<string>()
        {
          ".png",
        };
        private void TakeScreenshot() {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height)) {
                using (Graphics g = Graphics.FromImage(bitmap)) {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }
                string screenshotPath = Path.Combine(gposeWindow.Plugin.Config.CacheFolder, "Screenshots\\");
                Directory.CreateDirectory(screenshotPath);
                new Bitmap(new Bitmap(bitmap, Screen.PrimaryScreen.Bounds.Width, 
                Screen.PrimaryScreen.Bounds.Height)).Save(Path.Combine(screenshotPath, DateTime.Now.Ticks + ".jpg"), ImageFormat.Jpeg);
            }
        }
    }
}