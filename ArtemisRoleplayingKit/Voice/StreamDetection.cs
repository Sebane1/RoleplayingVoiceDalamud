using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace RoleplayingVoiceDalamud {
    public static class StreamDetection {
        static bool initialized = false;
        private static bool _screenCaptureDetected = false;
        private static bool _socialPlatformDetected = false;
        private static Process _lastScreenClippingHost;

        public static bool RecordingSoftwareIsActive {
            get {
                if (!initialized) {
                    Task.Run(delegate {
                        try {
                            Screenshots.Main();
                        } catch {

                        }
                    });
                    Task.Run(delegate {
                        while (true) {
                            var processes = Process.GetProcesses();
                            Process process = null;
                            string[] screenCapturingProcess = new string[] { "obs" , "gyazowin", "gyazoreplay", "xsplit", "snippingtool", "sharex", "snagit",
                            "fireshot", "tinytake","screenpresso","screenshot","grab","loom","greenshot","nimbus","monosnap","skitch","lightshot","screensketch"
                            ,"screenclippinghost","droplr","nimbus","picpick"};
                            foreach (string item in screenCapturingProcess) {
                                if (CheckForProcess(item, out process)) {
                                    break;
                                }
                            }
                            processes = null;
                            if (process != null) {
                                _screenCaptureDetected = true;
                                process.WaitForExit();
                                _screenCaptureDetected = false;
                            } else {
                                Thread.Sleep(300);
                            }
                        }
                    });
                    Task.Run(delegate {
                        while (true) {
                            var processes = Process.GetProcesses();
                            Process process = null;
                            bool socialPlatformEnabled = false;
                            if (process == null) {
                                foreach (var item in processes) {
                                    string filename = item.ProcessName.ToLower();
                                    string title = item.MainWindowTitle.ToLower();
                                    if (title.Contains("/ x")) {
                                        socialPlatformEnabled = true;
                                        break;
                                    }
                                }
                            }
                            _socialPlatformDetected = socialPlatformEnabled;
                            Thread.Sleep(1000);
                        }
                    });
                    initialized = true;
                }
                if (Screenshots.PrintScreenHeld) {
                    Task.Run(delegate {
                        Thread.Sleep(5000);
                        Screenshots.PrintScreenHeld = false;
                    });
                    return true;
                }

                if (_lastScreenClippingHost == null) {
                    Task.Run(() => {
                        var screenClippingHost = Process.GetProcessesByName("screenclippinghost");
                        if (screenClippingHost.Length > 0) {
                            _lastScreenClippingHost = screenClippingHost[0];
                            _lastScreenClippingHost.WaitForExit();
                            _screenCaptureDetected = false;
                            _lastScreenClippingHost = null;
                        }
                    });
                }

                return _screenCaptureDetected || _socialPlatformDetected;
            }
        }
        public static bool CheckForProcess(string processName, out Process process) {
            var screenClippingHost = Process.GetProcessesByName(processName);
            if (screenClippingHost.Length > 0) {
                process = screenClippingHost[0];
                return true;
            }
            process = null;
            return false;
        }
    }

    public static class Screenshots {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static bool printScreenHeld;

        public static bool PrintScreenHeld { get => printScreenHeld; set => printScreenHeld = value; }

        public static void Main() {
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc) {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule) {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(
            int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(
            int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN) {
                int vkCode = Marshal.ReadInt32(lParam);
                printScreenHeld = ((Keys)vkCode) == Keys.PrintScreen;
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
