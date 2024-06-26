using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static FFBardMusicPlayer.FFXIV.FFXIVHook;

namespace FFBardMusicPlayer.FFXIV {
    public class FFXIVHook {
        #region WIN32

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_GETMESSAGE = 3;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x104;
        public const int WM_SYSKEYUP = 0x105;
        private const int WM_CHAR = 0x0102;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, MessageProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public struct Input {
            public int Type;
            public InputUnion Un;
        }


        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion {
            [FieldOffset(0)] public Mouseinput mi;
            [FieldOffset(0)] public Keybdinput ki;
            [FieldOffset(0)] public readonly Hardwareinput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Mouseinput {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Keybdinput {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Hardwareinput {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(HandleRef hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(HandleRef hWnd, ref Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width() => Right - Left;

            public int Height() => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point {
            public int X;
            public int Y;

            public Point(int x, int y) {
                X = x;
                Y = y;
            }
        }

        public delegate IntPtr MessageProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        private IntPtr mainWindowHandle;
#pragma warning disable CS8618 // Non-nullable field '_proc' must contain a non-null value when exiting constructor. Consider declaring the field as nullable.
        private static MessageProc _proc;
#pragma warning restore CS8618 // Non-nullable field '_proc' must contain a non-null value when exiting constructor. Consider declaring the field as nullable.
        private IntPtr hookId = IntPtr.Zero;
#pragma warning disable CS8618 // Non-nullable field 'OnKeyPressed' must contain a non-null value when exiting constructor. Consider declaring the field as nullable.
        public EventHandler<Keys> OnKeyPressed;
#pragma warning restore CS8618 // Non-nullable field 'OnKeyPressed' must contain a non-null value when exiting constructor. Consider declaring the field as nullable.
        private bool insideTextBox;
        private int waitQueue;

#pragma warning disable CS8618 // Non-nullable property 'Process' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
        public Process Process { get; private set; }
#pragma warning restore CS8618 // Non-nullable property 'Process' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.

        public bool Hook(Process process, bool useCallback = true) {
            if (process == null) {
                return false;
            }

            if (hookId != IntPtr.Zero) {
                Unhook();
            }

            Process = process;
            mainWindowHandle = process.MainWindowHandle;

            if (useCallback) {
                _proc = HookCallback;
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
                return hookId != IntPtr.Zero;
            }

            return true;
        }

        public void Unhook() {
            if (hookId != IntPtr.Zero) {
                UnhookWindowsHookEx(hookId);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                Process = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                hookId = IntPtr.Zero;
            }
        }
        public void SendText(string text, int sleep = 10) {
            waitQueue += 5000;
            Thread.Sleep(waitQueue);
            if (!insideTextBox) {
                FocusWindow();
                SendSyncKey(Keys.Enter);
                insideTextBox = true;
            }
            if (insideTextBox) {
                FocusWindow();
                Thread.Sleep(sleep);
                SendString(text);
                Thread.Sleep(30 * text.Length);
                insideTextBox = false;
            }
            if (!insideTextBox) {
                SendSyncKey(Keys.Enter);
            }
            waitQueue -= 5000;
        }
        public void FocusWindow() { SetForegroundWindow(mainWindowHandle); }

        public void SendTimedSyncKey(Keys key, bool modifier = true, bool sendDown = true, bool sendUp = true) {
            Task.Run(() => {
                var key2 = key & ~Keys.Control & key & ~Keys.Shift & key & ~Keys.Alt;
                if (sendDown) {
                    if (modifier) {
                        for (var i = 0; i < 5; i++) {
                            if ((key & Keys.Control) == Keys.Control) {
                                SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ControlKey, (IntPtr)0);
                            }

                            if ((key & Keys.Alt) == Keys.Alt) {
                                SendMessage(mainWindowHandle, WM_SYSKEYDOWN, (IntPtr)Keys.Menu, (IntPtr)0);
                            }

                            if ((key & Keys.Shift) == Keys.Shift) {
                                SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                            }

                            Thread.Sleep(5);
                        }
                    }

                    SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)key2, (IntPtr)0);
                    Thread.Sleep(50);
                }

                if (sendUp) {
                    SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)key2, (IntPtr)0);
                    if (modifier) {
                        if ((key & Keys.Shift) == Keys.Shift) {
                            Thread.Sleep(5);
                            SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                        }

                        if ((key & Keys.Alt) == Keys.Alt) {
                            Thread.Sleep(5);
                            SendMessage(mainWindowHandle, WM_SYSKEYUP, (IntPtr)Keys.Menu, (IntPtr)0);
                        }

                        if ((key & Keys.Control) == Keys.Control) {
                            Thread.Sleep(5);
                            SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ControlKey, (IntPtr)0);
                        }
                    }
                }
            });
        }

        public void SendSyncKey(Keys key, bool modifier = true, bool sendDown = true, bool sendUp = true) {
            var key2 = key & ~Keys.Control & key & ~Keys.Shift;
            if (sendDown) {
                Console.WriteLine(key.ToString() + "down");
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }

                SendMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)key2, (IntPtr)0);
            }

            if (sendUp) {
                Console.WriteLine(key.ToString() + "up");
                SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)key2, (IntPtr)0);
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }
            }
        }
        public void SendHybridKey(Keys key, bool modifier = true, bool sendDown = true, bool sendUp = true) {
            var key2 = key & ~Keys.Control & key & ~Keys.Shift;
            if (sendDown) {
                Console.WriteLine(key.ToString() + "down");
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }

                PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)key2, (IntPtr)0);
            }

            if (sendUp) {
                Console.WriteLine(key.ToString() + "up");
                SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)key2, (IntPtr)0);
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        SendMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }
            }
        }

        public void SendAsyncKey(Keys key, bool modifier = true, bool sendDown = true, bool sendUp = true) {
            var key2 = key & ~Keys.Control & key & ~Keys.Shift;
            if (sendDown) {
                Console.WriteLine(key.ToString() + "down");
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }

                PostMessage(mainWindowHandle, WM_KEYDOWN, (IntPtr)key2, (IntPtr)0);
            }

            if (sendUp) {
                Console.WriteLine(key.ToString() + "up");
                PostMessage(mainWindowHandle, WM_KEYUP, (IntPtr)key2, (IntPtr)0);
                if (modifier) {
                    if ((key & Keys.Control) == Keys.Control) {
                        PostMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ControlKey, (IntPtr)0);
                    }

                    if ((key & Keys.Shift) == Keys.Shift) {
                        PostMessage(mainWindowHandle, WM_KEYUP, (IntPtr)Keys.ShiftKey, (IntPtr)0);
                    }
                }
            }
        }

        public void SendKeyStroke(Keys keyDown = Keys.None, Keys modDown = Keys.None, Keys keyUp = Keys.None,
            Keys modUp = Keys.None) {
            if (keyDown != Keys.None) {
                SendAsyncKey(keyDown | modDown, true, true, false);
            }

            if (keyUp != Keys.None) {
                SendAsyncKey(keyUp | modDown, true, false);
            }
        }

        public void SendSyncKeyStroke(Keys keyDown = Keys.None, Keys modDown = Keys.None, Keys keyUp = Keys.None,
    Keys modUp = Keys.None) {
            if (keyDown != Keys.None) {
                SendSyncKey(keyDown | modDown, true, true, true);
            }

            if (keyUp != Keys.None) {
                SendSyncKey(keyUp | modDown, true, false);
            }
        }

        public void SendKeyInput(List<Keybdinput> keybdInput) {
            var keyList = new List<Input>();
            foreach (var input in keybdInput) {
                keyList.Add(new Input {
                    Type = 1,
                    Un = new InputUnion {
                        ki = input
                    }
                });
            }

            SendInput((uint)keyList.Count, keyList.ToArray(), Marshal.SizeOf(typeof(Input)));
        }

        public void SendAsyncChar(char charInput) {
            SendMessage(mainWindowHandle, WM_CHAR, (IntPtr)charInput, (IntPtr)0);
        }

        public void SendString(string input) {
            if (GetForegroundWindow() != mainWindowHandle) {
                SetForegroundWindow(mainWindowHandle);
            }
            List<Input> keyList = new List<Input>();
            foreach (short c in input) {
                if (c > 10) {
                    Input keyDown = new Input {
                        Type = 1,
                        Un = new InputUnion {
                            ki = new Keybdinput {
                                wVk = 0,
                                wScan = (ushort)c,
                                dwFlags = 0x0004,
                            }
                        }
                    };
                    keyList.Add(keyDown);

                    Input keyUp = new Input {
                        Type = 1,
                        Un = new InputUnion {
                            ki = new Keybdinput {
                                wVk = 0,
                                wScan = (ushort)c,
                                dwFlags = 0x0004 | 0x0002,
                            }
                        }
                    };
                    keyList.Add(keyUp);
                }
            }
            SendInput((uint)keyList.Count, keyList.ToArray(), Marshal.SizeOf(typeof(Input)));
        }

        public void SendMouseClick(int x, int y) {
            var point = new Point(x, y);
            if (GetScreenFromClientPoint(ref point)) {
                Cursor.Position = new System.Drawing.Point(point.X, point.Y);
                var mouseDown = new List<Input>
                {
                    new Input
                    {
                        Type = 0,
                        Un = new InputUnion
                        {
                            mi = new Mouseinput
                            {
                                dx      = point.X,
                                dy      = point.Y,
                                dwFlags = 0x8000 | 0x0002
                            }
                        }
                    }
                };
                SendInput((uint)mouseDown.Count, mouseDown.ToArray(), Marshal.SizeOf(typeof(Input)));

                var mouseUp = new List<Input>
                {
                    new Input
                    {
                        Type = 0,
                        Un = new InputUnion
                        {
                            mi = new Mouseinput
                            {
                                dx      = point.X + 1,
                                dy      = point.Y + 1,
                                dwFlags = 0x8000 | 0x0004
                            }
                        }
                    }
                };
                SendInput((uint)mouseUp.Count, mouseUp.ToArray(), Marshal.SizeOf(typeof(Input)));
            }
            //	SendMessage(mainWindowHandle, WM_LBUTTONDOWN, (IntPtr) 0, (IntPtr) ((y << 16) | (x & 0xFFFF)));
            //	SendMessage(mainWindowHandle, WM_LBUTTONUP, (IntPtr) 0, (IntPtr) (((y + 1) << 16) | ((x + 1) & 0xFFFF)));
        }

        public Rect GetClientRect() {
            if (GetClientRect(new HandleRef(this, mainWindowHandle), out var rect)) {
                return rect;
            }

            return new Rect();
        }

        public bool GetScreenFromClientPoint(ref Point point) => ClientToScreen(new HandleRef(this, mainWindowHandle), ref point);

        public IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (GetForegroundWindow() == mainWindowHandle) {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN) {
                    var vkCode = Marshal.ReadInt32(lParam);
                    OnKeyPressed?.Invoke(this, (Keys)vkCode);
                }
            }

            
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
    }
}
