using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Microsoft.VisualBasic.Devices;
using RoleplayingMediaCore;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Input;
using static RoleplayingVoice.PluginWindow;
using System;
using System.Threading.Tasks;
using RoleplayingVoiceDalamud;
using System.Threading;
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
        private bool _takingScreenShot;

        public GposePhotoTakerWindow(DalamudPluginInterface pluginInterface) :
            base("Gpose Photo Window", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, false) {
            //IsOpen = true;
            windowSize = Size = new Vector2(250, 80);
            initialSize = Size;
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
        }
        public override bool DrawConditions() {
            return !_takingScreenShot && gposeWindow.FrameNames != null && gposeWindow.FrameNames.Count > 0;
        }
        internal GposeWindow GposeWindow { get => gposeWindow; set => gposeWindow = value; }

        public override void Draw() {
            Keyboard keyboard = new Keyboard();
            if (DrawConditions()) {
                ImGui.LabelText("##gposeLabelThingy", "Pick A Photo Frame");
                betterComboBox.Contents = gposeWindow.FrameNames.ToArray();
                betterComboBox.Draw();
                gposeWindow.SetFrame(betterComboBox.SelectedIndex);
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
                    Process Process;;
                    try {
                        Directory.CreateDirectory(screenshotPath);
                    } catch {
                    }
                    ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + screenshotPath + @"""");
                    ProcessInfo.UseShellExecute = true;
                    Process = Process.Start(ProcessInfo);
                }
            }
        }
        private void TakeScreenshot() {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height)) {
                using (Graphics g = Graphics.FromImage(bitmap)) {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }
                string screenshotPath = Path.Combine(gposeWindow.Plugin.Config.CacheFolder, "Screenshots\\");
                Directory.CreateDirectory(screenshotPath);
                new Bitmap(new Bitmap(bitmap, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)).Save(Path.Combine(screenshotPath, DateTime.Now.Ticks + ".jpg"), ImageFormat.Jpeg);
            }
        }
    }
}