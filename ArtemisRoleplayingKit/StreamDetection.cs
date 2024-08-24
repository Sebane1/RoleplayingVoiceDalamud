﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace RoleplayingVoiceDalamud {
    public static class StreamDetection {
        static bool initialized = false;
        public static bool RecordingSoftwareIsActive {
            get {
                if (!initialized) {
                    Task.Run(delegate {
                        InterceptKeys.Main();
                    });
                    initialized = true;
                }
                if (InterceptKeys.PrintScreenHeld) {
                    Task.Run(delegate {
                        Thread.Sleep(5000);
                        InterceptKeys.PrintScreenHeld = false;
                    });
                    return true;
                }

                foreach (var item in Process.GetProcesses()) {
                    string filename = item.ProcessName.ToLower();
                    if (filename.Contains("obs") || filename.Contains("gyazowin") || filename.Contains("gyazoreplay") ||
                        filename.Contains("xsplit") || filename.Contains("snippingtool")) {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    public static class InterceptKeys {
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
