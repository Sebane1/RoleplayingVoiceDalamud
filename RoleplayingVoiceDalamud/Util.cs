using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dalamud.Game;
using Dalamud.Interface;
using ImGuiNET;

namespace RoleplayingVoiceDalamud {
    internal static class Util {
        internal static bool TryScanText(this SigScanner scanner, string sig, out IntPtr result) {
            result = IntPtr.Zero;
            try {
                result = scanner.ScanText(sig);
                return true;
            } catch (KeyNotFoundException) {
                return false;
            }
        }

        private static unsafe byte[] ReadTerminatedBytes(byte* ptr) {
            if (ptr == null) {
                return new byte[0];
            }

            var bytes = new List<byte>();
            while (*ptr != 0) {
                bytes.Add(*ptr);
                ptr += 1;
            }

            return bytes.ToArray();
        }

        internal static unsafe string ReadTerminatedString(byte* ptr) {
            return Encoding.UTF8.GetString(ReadTerminatedBytes(ptr));
        }

        internal static bool ContainsIgnoreCase(this string haystack, string needle) {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase) >= 0;
        }

        internal static bool IconButton(FontAwesomeIcon icon, string id) {
            ImGui.PushFont(UiBuilder.IconFont);
            var ret = ImGui.Button($"{icon.ToIconString()}##{id}");
            ImGui.PopFont();
            return ret;
        }
    }
}